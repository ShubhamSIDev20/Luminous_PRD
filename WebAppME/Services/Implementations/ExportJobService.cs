using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Config;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Models.SqliteEntities;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Hangfire;
using Serilog;

namespace BatteryTestingSystem.Services.Implementations;

/// <summary>
/// Hangfire background job -- generates an Excel export for a session.
///
/// Sheet layout:
///   Sheet 1 - Program         (info + steps table)      -- ClosedXML  (small, always fits in RAM)
///   Sheet 2 - MeasurementData (all rows)                -- OpenXML SAX writer (O(1) RAM, any size)
///   Sheet 3 - Battery         (field list)              -- ClosedXML  (small)
///   Sheet 4 - Chart           (placeholder note)        -- ClosedXML  (small)
///
/// Strategy:
///   1. Build the three small sheets with ClosedXML and SaveAs to disk.
///   2. Re-open the file with the OpenXML SDK SpreadsheetDocument in edit mode.
///   3. Replace the empty MeasurementData placeholder sheet with a full SAX-written
///      sheet that streams rows directly to the ZIP entry -- peak RAM stays bounded
///      to one batch of rows (~5,000) no matter how large the session is.
///
/// No AdjustToContents() is called on the large sheet (that would scan every cell).
/// </summary>
public class ExportJobService
{
    private readonly IServiceProvider _sp;
    private static readonly Serilog.ILogger _log = Log.ForContext<ExportJobService>();

    public ExportJobService(IServiceProvider sp) => _sp = sp;

    // -------------------------------------------------------------------------
    // Hangfire entry point
    // -------------------------------------------------------------------------
    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(int exportRecordId, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var repo  = scope.ServiceProvider.GetRequiredService<IExportRepository>();
        var dbMgr = scope.ServiceProvider.GetRequiredService<ISqliteBulkDatabaseManager>();

        var record = await repo.GetByIdAsync(exportRecordId);
        if (record == null)
        {
            _log.Warning("ExportJob: record {Id} not found -- skipping.", exportRecordId);
            return;
        }

        await repo.UpdateStatusAsync(exportRecordId, ExportStatus.Processing);
        _log.Information("ExportJob {Id} starting -- session: {Path}", exportRecordId, record.SessionFilePath);

        var exportDir = Path.Combine(GlobalConfig.AppSettings.Data, "exports");
        Directory.CreateDirectory(exportDir);

        var safeName = record.SessionFilePath
            .Replace('/', '_').Replace('\\', '_')
            .Replace(':', '_').Trim('_');
        var fileName = $"export_{safeName}_{record.RequestedAt:yyyyMMdd_HHmmss}.xlsx";
        var fullPath = Path.Combine(exportDir, fileName);

        try
        {
            var session = await dbMgr.GetSessionAsync(record.SessionFilePath);
            await WriteExcelAsync(record.SessionFilePath, fullPath, session, dbMgr, ct);

            var size = new FileInfo(fullPath).Length;
            await repo.MarkReadyAsync(exportRecordId, fullPath, size);
            _log.Information("ExportJob {Id} ready -- {Bytes:N0} bytes at {Path}", exportRecordId, size, fullPath);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "ExportJob {Id} failed", exportRecordId);
            await repo.UpdateStatusAsync(exportRecordId, ExportStatus.Failed, ex.Message);
            if (File.Exists(fullPath))
                try { File.Delete(fullPath); } catch { /* ignore */ }
        }
    }

    // -------------------------------------------------------------------------
    // Phase 1 + 2: build small sheets, then SAX-stream the large one
    // -------------------------------------------------------------------------
    private static async Task WriteExcelAsync(
        string sessionFilePath,
        string outputPath,
        SessionRequest? session,
        ISqliteBulkDatabaseManager dbMgr,
        CancellationToken ct)
    {
        // Phase 1: write the three small sheets with ClosedXML and flush to disk.
        // The MeasurementData sheet is added as an empty placeholder so the sheet
        // exists in the correct tab position (index 1, i.e. the second sheet).
        using (var wb = new XLWorkbook())
        {
            if (session?.Program != null)
                BuildProgramSheet(wb, session.Program);

            wb.Worksheets.Add("MeasurementData"); // placeholder -- filled in Phase 2

            if (session?.Battery != null)
                BuildBatterySheet(wb, session.Battery);

            BuildChartPlaceholderSheet(wb);

            wb.SaveAs(outputPath);
        } // ClosedXML workbook fully disposed and file handle released before Phase 2

        // Phase 2: re-open the file with OpenXML SDK and SAX-stream measurement rows.
        await WriteMeasurementSheetSaxAsync(outputPath, sessionFilePath, dbMgr, ct);
    }

    // -------------------------------------------------------------------------
    // Phase 2: SAX writer for MeasurementData -- constant RAM regardless of size
    // -------------------------------------------------------------------------
    private static async Task WriteMeasurementSheetSaxAsync(
        string outputPath,
        string sessionFilePath,
        ISqliteBulkDatabaseManager dbMgr,
        CancellationToken ct)
    {
        var fixedHeaders = new[]
        {
            "ID", "Session ID", "Device ID", "Circuit ID",
            "Step #", "Operator", "Circuit Status",
            "Date Time", "Run Time",
            "Current (A)", "Voltage (V)", "Temperature (C)", "Power (W)",
            "Accum. Cap (Ah)", "Charge Cap (Ah)", "Discharge Cap (Ah)", "Step Cap (Ah)",
            "Accum. Energy (Wh)", "Charge Energy (Wh)", "Discharge Energy (Wh)", "Step Energy (Wh)",
            "System Error ID", "Error ID", "Message ID"
            // Remark appended after dynamic DBC columns
        };

        using var doc = SpreadsheetDocument.Open(outputPath, isEditable: true);
        var wbPart = doc.WorkbookPart!;

        // Locate the MeasurementData placeholder sheet
        var sheetRef = wbPart.Workbook.Descendants<Sheet>()
            .FirstOrDefault(s => s.Name?.Value == "MeasurementData");
        if (sheetRef == null) return;

        var wsPart = (WorksheetPart)wbPart.GetPartById(sheetRef.Id!);

        List<string>? dbcKeys = null;
        bool headerWritten    = false;
        uint rowIndex         = 1;
        int  colIndex         = 0;
        uint lastRow          = 0;

        using var writer = OpenXmlWriter.Create(wsPart);
        writer.WriteStartElement(new Worksheet());

        // Freeze top header row
        writer.WriteStartElement(new SheetViews());
        writer.WriteStartElement(new SheetView(),
            new[] { new OpenXmlAttribute("workbookViewId", "", "0") });
        writer.WriteElement(new Pane
        {
            VerticalSplit = 1,
            TopLeftCell   = "A2",
            ActivePane    = PaneValues.BottomLeft,
            State         = PaneStateValues.Frozen
        });
        writer.WriteEndElement(); // SheetView
        writer.WriteEndElement(); // SheetViews

        writer.WriteStartElement(new SheetData());

        await foreach (var batch in dbMgr.StreamSessionDataAsync(sessionFilePath, batchSize: 5000, ct))
        {
            if (ct.IsCancellationRequested) break;

            // Discover DBC column keys once from first batch that has them
            if (dbcKeys == null)
            {
                dbcKeys = batch
                    .Where(d => d.DbcValuesParsed is { Count: > 0 })
                    .SelectMany(d => d.DbcValuesParsed.Keys)
                    .Distinct()
                    .OrderBy(k => k)
                    .ToList();
            }

            // Write header row exactly once
            if (!headerWritten)
            {
                var allHeaders = fixedHeaders
                    .Concat(dbcKeys ?? [])
                    .Append("Remark")
                    .ToList();

                WriteRow(writer, rowIndex,
                    allHeaders.Select(h => (h, true)).ToList(),
                    ref colIndex, ref lastRow);
                rowIndex++;
                headerWritten = true;
            }

            foreach (var d in batch)
            {
                var cells = new List<(string value, bool bold)>
                {
                    (d.Id.ToString(),                                                              false),
                    (d.SessionID.ToString(),                                                       false),
                    (d.DeviceId.ToString(),                                                        false),
                    (d.ChannelId.ToString(),                                                       false),
                    (d.StepNumber.ToString(),                                                      false),
                    (OperatorConstants.ToName((byte)d.Operator),                                  false),
                    (d.CircuitStatus.ToString(),                                                   false),
                    (d.DateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),                              false),
                    (TimeSpan.FromMilliseconds(d.ProgramRunningTime).ToString(@"hh\:mm\:ss\.fff"),false),
                    (Fmt(d.Current),              false),
                    (Fmt(d.Voltage),              false),
                    (Fmt(d.Temperature),          false),
                    (Fmt(d.Power),                false),
                    (Fmt(d.AccumulatedCapacity),  false),
                    (Fmt(d.ChargeCapacity),       false),
                    (Fmt(d.DischargeCapacity),    false),
                    (Fmt(d.StepCapacity),         false),
                    (Fmt(d.AccumulatedEnergy),    false),
                    (Fmt(d.ChargeEnergy),         false),
                    (Fmt(d.DischargeEnergy),      false),
                    (Fmt(d.StepEnergy),           false),
                    (d.SystemErrorID.HasValue ? d.SystemErrorID.Value.ToString() : "", false),
                    (d.ErrorId?.ToString()  ?? "",  false),
                    (d.MessageId?.ToString() ?? "", false),
                };

                // Dynamic DBC columns
                if (dbcKeys is { Count: > 0 })
                {
                    var dbc = d.DbcValuesParsed;
                    foreach (var key in dbcKeys)
                    {
                        string cellVal = "";
                        if (dbc != null && dbc.TryGetValue(key, out var val) && val != null)
                            cellVal = val is double or float or decimal or int or long
                                ? Convert.ToDouble(val).ToString("0.0000")
                                : val.ToString() ?? "";
                        cells.Add((cellVal, false));
                    }
                }

                // Remark -- always last
                cells.Add((d.Remark ?? "", false));

                WriteRow(writer, rowIndex, cells, ref colIndex, ref lastRow);
                rowIndex++;
            }
        }

        writer.WriteEndElement(); // SheetData
        writer.WriteEndElement(); // Worksheet
        writer.Close();

        doc.Save();
    }

    // -------------------------------------------------------------------------
    // SAX helpers
    // -------------------------------------------------------------------------

    /// <summary>Writes one complete row using inline-string cells.</summary>
    private static void WriteRow(
        OpenXmlWriter writer,
        uint rowIdx,
        List<(string value, bool bold)> cells,
        ref int colIndex,
        ref uint lastRow)
    {
        writer.WriteStartElement(new Row(),
            new[] { new OpenXmlAttribute("r", "", rowIdx.ToString()) });

        colIndex = 1;
        lastRow  = rowIdx;

        foreach (var (value, _) in cells)
        {
            var cellRef = $"{ColLetter(colIndex)}{rowIdx}";
            writer.WriteStartElement(new Cell(),
                new[]
                {
                    new OpenXmlAttribute("r", "", cellRef),
                    new OpenXmlAttribute("t", "", "inlineStr")
                });
            writer.WriteElement(new InlineString(new Text(value)));
            writer.WriteEndElement(); // Cell
            colIndex++;
        }

        writer.WriteEndElement(); // Row
    }

    /// <summary>1-based column index to Excel letter: 1=A, 26=Z, 27=AA ...</summary>
    private static string ColLetter(int col)
    {
        string result = "";
        while (col > 0)
        {
            col--;
            result = (char)('A' + col % 26) + result;
            col /= 26;
        }
        return result;
    }

    private static string Fmt(float? v) => v.HasValue ? v.Value.ToString("0.0000") : "";

    // -------------------------------------------------------------------------
    // Sheet 1: Program (ClosedXML -- always small)
    // -------------------------------------------------------------------------
    private static void BuildProgramSheet(XLWorkbook wb, ProgramDTO p)
    {
        var ws = wb.Worksheets.Add("Program");

        var infoRows = new (string Label, string? Value)[]
        {
            ("Program ID",    p.ProgramId.ToString()),
            ("Program Name",  p.ProgramName),
            ("Description",   p.Description),
            ("Max Ah",        p.MaxAh?.ToString("F3")),
            ("Program Steps", p.ProgramSteps?.ToString()),
            ("Program Time",  p.ProgramTimeTicks.HasValue
                                  ? TimeSpan.FromTicks(p.ProgramTimeTicks.Value).ToString(@"hh\:mm\:ss\.fff")
                                  : null),
            ("Created By",    p.CreatedBy),
            ("Updated By",    p.UpdatedBy),
            ("Created At",    p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
            ("Updated At",    p.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
            ("Valid",         p.IsVaild.ToString()),
            ("Hash",          p.ProgramHash),
        };

        int row = 1;
        foreach (var (label, value) in infoRows)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0FE");
            ws.Cell(row, 2).Value = value ?? "";
            row++;
        }
        row++; // blank separator

        var stepHeaders = new[] { "Step #", "Op Code", "Label", "Comment",
                                   "Nominal Values", "Limits", "Actions", "Registrations" };
        for (int c = 0; c < stepHeaders.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = stepHeaders[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A73E8");
            cell.Style.Font.FontColor = XLColor.White;
        }
        row++;

        foreach (var s in p.ProgramStepModel)
        {
            ws.Cell(row, 1).Value = s.StepNumber;
            ws.Cell(row, 2).Value = OperatorConstants.ToName((byte)s.OperatorCode);
            ws.Cell(row, 3).Value = s.Label;
            ws.Cell(row, 4).Value = s.Comment;
            ws.Cell(row, 5).Value = string.Join(", ", s.NominalValues);
            ws.Cell(row, 6).Value = string.Join(", ", s.Limits);
            ws.Cell(row, 7).Value = string.Join(", ", s.Actions);
            ws.Cell(row, 8).Value = string.Join(", ", s.Registrations);
            if (row % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
            row++;
        }

        ws.Columns().AdjustToContents(); // safe -- program steps are always small
    }

    // -------------------------------------------------------------------------
    // Sheet 3: Battery (ClosedXML -- always small)
    // -------------------------------------------------------------------------
    private static void BuildBatterySheet(XLWorkbook wb, BatteryDTO b)
    {
        var ws = wb.Worksheets.Add("Battery");

        var fields = new (string Label, string Value)[]
        {
            ("ID",                    b.Id.ToString()),
            ("Name",                  b.Name),
            ("Battery Type ID",       b.BatteryTypeId.ToString()),
            ("Comments",              b.Comments ?? ""),
            ("Quantity",              b.Quantity.ToString()),
            ("Producer",              b.Producer),
            ("Nominal Voltage (V)",   b.NominalVoltage.ToString("F3")),
            ("Nominal Current (A)",   b.NominalCurrent.ToString("F3")),
            ("Nominal Capacity (Ah)", b.NominalCapacity.ToString("F3")),
            ("Charge Factor",         b.ChargeFactor.ToString("F3")),
            ("Impedance (Ohm)",       b.Impedance.ToString("F3")),
            ("Energy Density",        b.EnergyDensity.ToString("F3")),
            ("Cold Cranking A",       b.ColdCrankingCurrent.ToString("F3")),
            ("Number of Cells",       b.NumberOfCells.ToString()),
            ("Maximum Voltage (V)",   b.MaximumVoltage.ToString("F3")),
            ("Gassing Voltage (V)",   b.GassingVoltage.ToString("F3")),
            ("Break Voltage (V)",     b.BreakVoltage.ToString("F3")),
            ("Created By",            b.CreatedBy ?? ""),
            ("Updated By",            b.UpdatedBy ?? ""),
            ("Created At",            b.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
            ("Updated At",            b.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
        };

        int row = 1;
        foreach (var (label, value) in fields)
        {
            var lc = ws.Cell(row, 1);
            lc.Value = label;
            lc.Style.Font.Bold = true;
            lc.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0FE");
            ws.Cell(row, 2).Value = value;
            if (row % 2 == 0)
                ws.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
            row++;
        }

        ws.Columns().AdjustToContents(); // safe -- only 21 rows
    }

    // -------------------------------------------------------------------------
    // Sheet 4: Chart placeholder (ClosedXML -- always small)
    // -------------------------------------------------------------------------
    private static void BuildChartPlaceholderSheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("Chart");

        var title = ws.Cell(1, 1);
        title.Value = "BMS Chart";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 13;
        title.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A73E8");
        title.Style.Font.FontColor = XLColor.White;
        ws.Range(1, 1, 1, 10).Merge();

        var note = ws.Cell(3, 1);
        note.Value = "Chart image is available for in-browser exports only. " +
                     "For large sessions, open the BMS Data Viewer and use the " +
                     "in-browser export button to include the chart PNG.";
        note.Style.Alignment.WrapText = true;
        ws.Column(1).Width = 80;
        ws.Row(1).Height = 40;
    }
}
