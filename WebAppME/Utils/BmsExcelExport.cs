using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.SqliteEntities;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;

namespace BatteryTestingSystem.Utils
{
    public class BmsExcelExport
    {
        private ProgramDTO? _program;
        private List<MeasurementData>? _measurements;
        private BatteryDTO? _battery;
        private byte[]? _chartImageBytes;

        // ─── Fluent add methods ──────────────────────────────────────────────

        public BmsExcelExport AddProgram(ProgramDTO program)
        {
            _program = program;
            return this;
        }

        public BmsExcelExport AddMeasurementData(List<MeasurementData> data)
        {
            _measurements = data;
            return this;
        }

        public BmsExcelExport AddBattery(BatteryDTO battery)
        {
            _battery = battery;
            return this;
        }

        public BmsExcelExport AddChartImage(byte[]? pngBytes)
        {
            _chartImageBytes = pngBytes;
            return this;
        }

        // ─── Export ──────────────────────────────────────────────────────────

        /// <summary>Returns the Excel file as a byte array.</summary>
        public byte[] Export()
        {
            using var wb = new XLWorkbook();

            if (_program != null)
                BuildProgramSheet(wb, _program);

            if (_measurements != null)
                BuildMeasurementSheet(wb, _measurements);

            if (_battery != null)
                BuildBatterySheet(wb, _battery);

            if (_chartImageBytes != null && _chartImageBytes.Length > 0)
                BuildChartImageSheet(wb, _chartImageBytes);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ─── Sheet 1 – Program ───────────────────────────────────────────────

        private static void BuildProgramSheet(XLWorkbook wb, ProgramDTO p)
        {
            var ws = wb.Worksheets.Add("Program");

            // ── Program header info ──
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

            // ── Steps sub-table ──
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

            var steps = p.ProgramStepModel;
            foreach (var s in steps)
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

            ws.Columns().AdjustToContents();
        }

        // ─── Sheet 2 – Measurement Data ──────────────────────────────────────

        private static void BuildMeasurementSheet(XLWorkbook wb, List<MeasurementData> data)
        {
            var ws = wb.Worksheets.Add("MeasurementData");

            var headers = new[]
            {
                "ID", "Session ID", "Device ID", "Circuit ID",
                "Step #", "Operator", "Circuit Status",
                "Date Time", "Run Time (s)",
                "Current (A)", "Voltage (V)", "Temperature (°C)", "Power (W)",
                "Accum. Cap (Ah)", "Charge Cap (Ah)", "Discharge Cap (Ah)", "Step Cap (Ah)",
                "Accum. Energy (Wh)", "Charge Energy (Wh)", "Discharge Energy (Wh)", "Step Energy (Wh)",
                "System Error ID", "Error ID", "Message ID"
            };

            var dbcKeys = data
               .Where(d => d.DbcValuesParsed != null && d.DbcValuesParsed.Count > 0)
               .SelectMany(d => d.DbcValuesParsed.Keys)
               .Distinct()
               .OrderBy(k => k)
               .ToList();

            headers = headers.Concat(dbcKeys).Append("Remark").ToArray();

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A73E8");
                cell.Style.Font.FontColor = XLColor.White;
            }

            ws.SheetView.FreezeRows(1);

            int row = 2;
            foreach (var d in data)
            {
                ws.Cell(row, 1).Value = d.Id;
                ws.Cell(row, 2).Value = d.SessionID;
                ws.Cell(row, 3).Value = d.DeviceId;
                ws.Cell(row, 4).Value = d.ChannelId;
                ws.Cell(row, 5).Value = d.StepNumber;
                ws.Cell(row, 6).Value = OperatorConstants.ToName((byte)d.Operator);
                ws.Cell(row, 7).Value = d.CircuitStatus.ToString();
                ws.Cell(row, 8).Value = d.DateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
                ws.Cell(row, 9).Value = TimeSpan.FromMilliseconds(d.ProgramRunningTime).ToString(@"hh\:mm\:ss\.fff");
                ws.Cell(row, 10).Value = d.Current;
                ws.Cell(row, 11).Value = d.Voltage;
                ws.Cell(row, 12).Value = d.Temperature;
                ws.Cell(row, 13).Value = d.Power;
                ws.Cell(row, 14).Value = d.AccumulatedCapacity;
                ws.Cell(row, 15).Value = d.ChargeCapacity;
                ws.Cell(row, 16).Value = d.DischargeCapacity;
                ws.Cell(row, 17).Value = d.StepCapacity;
                ws.Cell(row, 18).Value = d.AccumulatedEnergy;
                ws.Cell(row, 19).Value = d.ChargeEnergy;
                ws.Cell(row, 20).Value = d.DischargeEnergy;
                ws.Cell(row, 21).Value = d.StepEnergy;
                ws.Cell(row, 22).Value = d.SystemErrorID.HasValue ? d.SystemErrorID.Value.ToString() : "";
                ws.Cell(row, 23).Value = d.ErrorId?.ToString() ?? "";
                ws.Cell(row, 24).Value = d.MessageId?.ToString() ?? "";

                int dynamicStartCol = 25;

                if (dbcKeys.Count > 0 && d.DbcValuesParsed != null)
                {
                    for (int i = 0; i < dbcKeys.Count; i++)
                    {
                        var key = dbcKeys[i];
                        if (d.DbcValuesParsed.TryGetValue(key, out var value) && value != null)
                        {
                            var cell = ws.Cell(row, dynamicStartCol + i);
                            if (value is double || value is float || value is decimal || value is int || value is long)
                            {
                                cell.Value = Convert.ToDouble(value);
                                cell.Style.NumberFormat.Format = "0.0000";
                            }
                            else
                            {
                                cell.Value = value.ToString();
                            }
                        }
                        else
                        {
                            ws.Cell(row, dynamicStartCol + i).Value = "";
                        }
                    }
                }

                ws.Cell(row, dynamicStartCol + dbcKeys.Count).Value = d.Remark?.ToString() ?? "";

                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");

                row++;
            }

            ws.Range(2, 10, row - 1, 21).Style.NumberFormat.Format = "0.0000";
            ws.Columns().AdjustToContents();
        }

        // ─── Sheet 3 – Battery ───────────────────────────────────────────────

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
                ("Impedance (Ω)",         b.Impedance.ToString("F3")),
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
                var labelCell = ws.Cell(row, 1);
                labelCell.Value = label;
                labelCell.Style.Font.Bold = true;
                labelCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0FE");

                ws.Cell(row, 2).Value = value;

                if (row % 2 == 0)
                    ws.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");

                row++;
            }

            ws.Columns().AdjustToContents();
        }

        // ─── Sheet 4 – Chart Image ───────────────────────────────────────────

        private static void BuildChartImageSheet(XLWorkbook wb, byte[] pngBytes)
        {
            var ws = wb.Worksheets.Add("Chart");

            // Title cell
            ws.Cell(1, 1).Value = "BMS Chart";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 13;
            ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1A73E8");
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
            ws.Range(1, 1, 1, 10).Merge();

            // Insert PNG image anchored at B3
            using var ms = new MemoryStream(pngBytes);
            var pic = ws.AddPicture(ms, XLPictureFormat.Png)
                        .MoveTo(ws.Cell(2, 1))
                        .WithSize(900, 375); // ~1200x500 scaled to fit nicely

            ws.Column(1).Width = 12;
        }
    }
}
