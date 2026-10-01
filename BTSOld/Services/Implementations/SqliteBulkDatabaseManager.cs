using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Config;
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.SqliteEntities;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Services.Interfaces;
using BatteryTestingSystem.Utils;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BatteryTestingSystem.Services.Implementations
{
    public class SqliteBulkDatabaseManager : ISqliteBulkDatabaseManager
    {
        private string BasePath = string.Empty;
        public SqliteBulkDatabaseManager()
        {
            try
            {
                BasePath = string.IsNullOrWhiteSpace(GlobalConfig.AppSettings?.Data)
                       ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sessions")
                       : Path.Combine(GlobalConfig.AppSettings.Data, "sessions");

                string dbPath = Path.Combine(BasePath);

                string? directory = Path.GetDirectoryName(dbPath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to initialize SqliteBulkDatabaseManager: {ex.Message}");
            }
        }

        public string BuildPath(string dbFileName)
        {
            var path = Path.Combine(BasePath, dbFileName);
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {

                Log.Error($"Failed to BuildPath SqliteBulkDatabaseManager {dbFileName}");
            }
            return path;
        }
        private SqliteDbContext GetContext(string dbFileName)
        {
           
             var context = new SqliteDbContext(BuildPath(dbFileName));

            try
            {
                context.Database.EnsureCreated();

                // sync model properties with table columns
                SqliteSchemaSync.SyncModelWithDatabase(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create database: {ex.Message}");
            }

            return context;
        }
        public async Task InsertRecordAsync(recordStoreRequest recordDto)
        {
            try
            {
      
                using var ctx = GetContext(recordDto.filePath);

                // Build ordered signal ID list (matches DbcSignalNames index order)
                DbcDatabase dbcdata = await FetchDbcDatabaseAsync(ctx);

                if (dbcdata?.Messages?.Count > 0)
                {
                    var orderedSignalIds = dbcdata.Messages
                        .SelectMany(m => m.Value.Signals)
                        .Where(s => s.IsSelected && s.SingalId != 0)
                        .DistinctBy(s => s.SingalId)
                        .OrderBy(s => s.SingalId)
                        .Select(s => s.SingalId.ToString())
                        .ToList();

                    foreach (var rec in recordDto.RealStoreRecord)
                    {
                        if (string.IsNullOrEmpty(rec.dbcValues)) continue;

                        // Parse the signal-ID-keyed dict written by EnqueueForStore
                        var dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(rec.dbcValues);
                        if (dict == null) continue;

                        // Convert to index-aligned string array
                        var arr = orderedSignalIds
                            .Select(id => dict.TryGetValue(id, out var v) ? v?.ToString() ?? "" : "")
                            .ToArray();

                        rec.dbcValues = JsonConvert.SerializeObject(arr);
                    }
                }

                // REG operator: a row whose program step carries a Nominal Value label (StepModel.NominalValues[0])
                // is bifurcated into RegLogs instead of Measurements — grouped case-insensitively by label so
                // multiple REG steps sharing a name accumulate together. A REG row with no label (or any
                // non-REG row) goes into Measurements exactly as before.
                //
                // The step list is resolved from this session's own stored "ExpandedProgram" config
                // (FetchExpandedProgramStepsAsync, same source InsertSessionAsync already writes and the
                // same pattern already used above for DBC signal names) — not from any live in-memory
                // program state on the caller — so this works identically regardless of which
                // CircuitCommandHandler instance enqueued the record.
                var expandedSteps = await FetchExpandedProgramStepsAsync(ctx);

                var regularRows = new List<MeasurementData>(recordDto.RealStoreRecord.Count);
                var regRowsByLabel = new Dictionary<string, List<RegLogRecord>>(StringComparer.OrdinalIgnoreCase);

                foreach (var rec in recordDto.RealStoreRecord)
                {
                    string? label = null;

                    if (rec.Operator == OperatorConstants.REG && expandedSteps.Count > 0)
                    {
                        var step = expandedSteps.FirstOrDefault(s => s.StepNumber == rec.StepNumber);
                        var nominal = step?.NominalValues?.FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(nominal))
                            label = nominal.Trim();
                    }

                    if (label != null)
                    {
                        if (!regRowsByLabel.TryGetValue(label, out var list))
                        {
                            list = new List<RegLogRecord>();
                            regRowsByLabel[label] = list;
                        }
                        list.Add(ToRegLogRecord(rec, label));
                    }
                    else
                    {
                        regularRows.Add(rec);
                    }
                }

                ctx.Measurements.AddRange(regularRows);

                foreach (var kv in regRowsByLabel)
                    ctx.RegLogs.AddRange(kv.Value);

                await ctx.SaveChangesAsync();

                ctx.Dispose();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB ERROR] InsertRecord failed for Device={recordDto.RealStoreRecord[0].DeviceId}, Circuit={recordDto.RealStoreRecord[0].CircuitId} {ex.Message}");
            }

        }

        // RegLogRecord's field shape mirrors MeasurementData deliberately (so the Data Viewer's existing
        // table rendering can be reused for the REG Logs view) — this is the write-side counterpart of
        // that mapping.
        private static RegLogRecord ToRegLogRecord(MeasurementData r, string label) => new()
        {
            RegLabel            = label,
            SessionID           = r.SessionID,
            DeviceId            = r.DeviceId,
            CircuitId           = r.CircuitId,
            StepNumber          = r.StepNumber,
            Operator            = r.Operator,
            CircuitStatus       = r.CircuitStatus,
            DateTime            = r.DateTime,
            ProgramRunningTime  = r.ProgramRunningTime,
            Current             = r.Current,
            Voltage             = r.Voltage,
            Temperature         = r.Temperature,
            Power               = r.Power,
            AccumulatedCapacity = r.AccumulatedCapacity,
            ChargeCapacity      = r.ChargeCapacity,
            DischargeCapacity   = r.DischargeCapacity,
            StepCapacity        = r.StepCapacity,
            AccumulatedEnergy   = r.AccumulatedEnergy,
            ChargeEnergy        = r.ChargeEnergy,
            DischargeEnergy     = r.DischargeEnergy,
            StepEnergy          = r.StepEnergy,
            SystemErrorID       = r.SystemErrorID,
            ErrorId             = r.ErrorId,
            MessageId           = r.MessageId,
            Remark              = r.Remark,
            dbcValues           = r.dbcValues
        };

        #region Config
        public async Task InsertSessionAsync(SessionRequest session)
        {
            if (session is null)
            {
                Log.Warning("[InsertSessionAsync] session is null.");
                return;
            }

            try
            {
                await using var ctx = GetContext(session.filePath);
                await using var transaction = await ctx.Database.BeginTransactionAsync();

                var now = DateTime.Now;
                string? user = CurrentUser.UserName;
                // -------- PROGRAM --------
                if (session.Program is not null)
                {
                    ctx.configurationEntities.Add(new ConfigurationEntity
                    {
                        Key = "Program",
                        Value = JsonConvert.SerializeObject(session.Program),
                        CreatedAt = now,
                        UpdatedAt = now,
                        IsDeleted = false,
                        CreatedBy = user,
                        UpdatedBy = user
                    });
                }

                // -------- BATTERY --------
                if (session.Battery is not null)
                {
                    ctx.configurationEntities.Add(new ConfigurationEntity
                    {
                        Key = "Battery",
                        Value = JsonConvert.SerializeObject(session.Battery),
                        CreatedAt = now,
                        UpdatedAt = now,
                        IsDeleted = false,
                        CreatedBy = user,
                        UpdatedBy = user
                    });
                }

                // -------- DBC --------
                if (session.dbcDatabase is not null)
                {
                    ctx.configurationEntities.Add(new ConfigurationEntity
                    {
                        Key = "dbc",
                        Value = JsonConvert.SerializeObject(session.dbcDatabase),
                        CreatedAt = now,
                        UpdatedAt = now,
                        IsDeleted = false,
                        CreatedBy = user,
                        UpdatedBy = user
                    });
                }

                // -------- DBC SIGNAL NAMES (ordered index for compact array rows) --------
                if (session.dbcDatabase?.Messages != null)
                {
                    var orderedNames = session.dbcDatabase.Messages
                        .SelectMany(m => m.Value.Signals)
                        .Where(s => s.IsSelected && s.SingalId != 0)
                        .DistinctBy(s => s.SingalId)
                        .OrderBy(s => s.SingalId)
                        .Select(s => s.Name)
                        .ToArray();

                    if (orderedNames.Length > 0)
                    {
                        ctx.configurationEntities.Add(new ConfigurationEntity
                        {
                            Key = "DbcSignalNames",
                            Value = JsonConvert.SerializeObject(orderedNames),
                            CreatedAt = now,
                            UpdatedAt = now,
                            IsDeleted = false,
                            CreatedBy = user,
                            UpdatedBy = user
                        });
                    }
                }

                // -------- PRODUCER PROGRAMS --------
                if (session.ProducerPrograms is not null)
                {
                    foreach (var prod in session.ProducerPrograms.Where(p => p?.ProgramName != null))
                    {
                        ctx.configurationEntities.Add(new ConfigurationEntity
                        {
                            Key = $"ProducerProgram_{prod.ProgramName}",
                            Value = JsonConvert.SerializeObject(prod),
                            CreatedAt = now,
                            UpdatedAt = now,
                            IsDeleted = false,
                            CreatedBy = user,
                            UpdatedBy = user
                        });
                    }
                }

                // -------- TABLE FILE DATA --------
                if (session.TableFileData is not null)
                {
                    foreach (var kv in session.TableFileData.Where(kv => kv.Value != null))
                    {
                        ctx.configurationEntities.Add(new ConfigurationEntity
                        {
                            Key = $"TableFile_{kv.Key}",
                            Value = JsonConvert.SerializeObject(kv.Value),
                            CreatedAt = now,
                            UpdatedAt = now,
                            IsDeleted = false,
                            CreatedBy = user,
                            UpdatedBy = user
                        });
                    }
                }

                // -------- EXPANDED PROGRAM STEPS --------
                if (session.ExpandedProgramSteps is not null && session.ExpandedProgramSteps.Count > 0)
                {
                    ctx.configurationEntities.Add(new ConfigurationEntity
                    {
                        Key = "ExpandedProgram",
                        Value = JsonConvert.SerializeObject(session.ExpandedProgramSteps),
                        CreatedAt = now,
                        UpdatedAt = now,
                        IsDeleted = false,
                        CreatedBy = user,
                        UpdatedBy = user
                    });
                }

                await ctx.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DB ERROR] InsertSessionAsync failed for FilePath={FilePath}, Program={Program}, Battery={Battery}",
                    session.filePath,
                    session.Program?.ProgramName,
                    session.Battery?.Name);
            }
        }
        public async Task<SessionRequest?> GetSessionAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Log.Warning("[GetSessionAsync] filePath is null or empty.");
                return null;
            }

            try
            {
                await using var ctx = GetContext(filePath);

                var program = await FetchProgramAsync(ctx);
                var battery = await FetchBatteryAsync(ctx);
                var dbcDatabase = await FetchDbcDatabaseAsync(ctx);

                var producerPrograms = await FetchProducerProgramsAsync(ctx);
                var tableFileData = await FetchTableFileDataAsync(ctx);
                var expandedSteps = await FetchExpandedProgramStepsAsync(ctx);

                return new SessionRequest
                {
                    filePath = filePath,
                    Program = program,
                    Battery = battery,
                    dbcDatabase = dbcDatabase,
                    ProducerPrograms = producerPrograms.Count > 0 ? producerPrograms : null,
                    TableFileData = tableFileData.Count > 0 ? tableFileData : null,
                    ExpandedProgramSteps = expandedSteps.Count > 0 ? expandedSteps : null
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DB ERROR] GetSessionAsync failed for path: {FilePath}", filePath);
                return null;
            }
        }
        private async Task<ProgramDTO?> FetchProgramAsync(SqliteDbContext ctx)
        {
            ProgramDTO? program = null;

            try
            {
                program = await ctx.Program
                  .AsNoTracking()
                  .Select(p => new ProgramDTO
                  {
                      ProgramName = p.ProgramName,
                      Description = p.Description,
                      MaxAh = p.MaxAh,
                      ProgramSteps = p.ProgramSteps,
                      ProgramJson = p.ProgramJson,
                      ProgramTimeTicks = p.ProgramTimeTicks,
                      CreatedBy = p.CreatedBy,
                      UpdatedBy = p.UpdatedBy
                  })
                  .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                
            }

            if (program is not null)
                return program;

            var fallback = await ctx.configurationEntities
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Key == "Program");

            if (fallback is null || string.IsNullOrEmpty(fallback.Value))
                return new ProgramDTO();

            return fallback is not null
                ? JsonConvert.DeserializeObject<ProgramDTO>(fallback.Value)
                : null;
        }
        private async Task<BatteryDTO?> FetchBatteryAsync(SqliteDbContext ctx)
        {
            BatteryDTO? battery = null;

            try
            {
                battery = await ctx.Battery
                       .AsNoTracking()
                       .Select(b => new BatteryDTO
                       {
                           Name = b.Name,
                           BatteryTypeId = b.BatteryTypeId,
                           Comments = b.Comments,
                           Quantity = b.Quantity,
                           Producer = b.Producer,
                           NominalVoltage = b.NominalVoltage,
                           NominalCurrent = b.NominalCurrent,
                           NominalCapacity = b.NominalCapacity,
                           ChargeFactor = b.ChargeFactor,
                           Impedance = b.Impedance,
                           EnergyDensity = b.EnergyDensity,
                           ColdCrankingCurrent = b.ColdCrankingCurrent,
                           NumberOfCells = b.NumberOfCells,
                           MaximumVoltage = b.MaximumVoltage,
                           GassingVoltage = b.GassingVoltage,
                           BreakVoltage = b.BreakVoltage,
                           CreatedBy = b.CreatedBy,
                           UpdatedBy = b.UpdatedBy
                       })
                       .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {

            }

            if (battery is not null)
                return battery;

            var fallback = await ctx.configurationEntities
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Key == "Battery");

            if (fallback is null || string.IsNullOrEmpty(fallback.Value))
                return new BatteryDTO();

            return fallback is not null
                ? JsonConvert.DeserializeObject<BatteryDTO>(fallback.Value)
                : null;
        }
        private async Task<DbcDatabase> FetchDbcDatabaseAsync(SqliteDbContext ctx)
        {
            var dbc = await ctx.configurationEntities
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Key == "dbc");

            if (dbc is null || string.IsNullOrEmpty(dbc.Value))
                return new DbcDatabase();

            return JsonConvert.DeserializeObject<DbcDatabase>(dbc.Value) ?? new DbcDatabase();
        }

        private async Task<string[]> FetchDbcSignalNamesAsync(SqliteDbContext ctx)
        {
            var entity = await ctx.configurationEntities
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Key == "DbcSignalNames");

            if (entity is null || string.IsNullOrEmpty(entity.Value))
                return Array.Empty<string>();

            return JsonConvert.DeserializeObject<string[]>(entity.Value) ?? Array.Empty<string>();
        }

        private async Task<List<ProgramDTO>> FetchProducerProgramsAsync(SqliteDbContext ctx)
        {
            var result = new List<ProgramDTO>();
            try
            {
                var entities = await ctx.configurationEntities
                    .AsNoTracking()
                    .Where(e => e.Key.StartsWith("ProducerProgram_") && !e.IsDeleted)
                    .ToListAsync();

                foreach (var e in entities)
                {
                    if (!string.IsNullOrEmpty(e.Value))
                    {
                        var prog = JsonConvert.DeserializeObject<ProgramDTO>(e.Value);
                        if (prog != null) result.Add(prog);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[DB] FetchProducerProgramsAsync failed");
            }
            return result;
        }

        private async Task<Dictionary<string, string[]>> FetchTableFileDataAsync(SqliteDbContext ctx)
        {
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var entities = await ctx.configurationEntities
                    .AsNoTracking()
                    .Where(e => e.Key.StartsWith("TableFile_") && !e.IsDeleted)
                    .ToListAsync();

                foreach (var e in entities)
                {
                    if (!string.IsNullOrEmpty(e.Value))
                    {
                        var lines = JsonConvert.DeserializeObject<string[]>(e.Value);
                        var fileName = e.Key["TableFile_".Length..];
                        if (lines != null) result[fileName] = lines;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[DB] FetchTableFileDataAsync failed");
            }
            return result;
        }

        private async Task<List<StepModel>> FetchExpandedProgramStepsAsync(SqliteDbContext ctx)
        {
            try
            {
                var entity = await ctx.configurationEntities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Key == "ExpandedProgram" && !e.IsDeleted);

                if (entity != null && !string.IsNullOrEmpty(entity.Value))
                {
                    var steps = JsonConvert.DeserializeObject<List<StepModel>>(entity.Value);
                    if (steps != null) return steps;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[DB] FetchExpandedProgramStepsAsync failed");
            }
            return new();
        }

        #endregion

        #region Read Data
        public async Task<List<MeasurementData>> ReadSessionDataAsync(string filePath)
        {
            try
            {
                using var ctx = GetContext(filePath);
                var rows = await ctx.Measurements.ToListAsync();
                var signalNames = await FetchDbcSignalNamesAsync(ctx);
                ApplyDbcForwardFill(rows, signalNames);
                return rows;
            }
            catch (Exception ex)
            {
                Log.Error($"[DB ERROR] ReadSessionDataAsync failed for {filePath}");
                return new List<MeasurementData>();
            }
        }

        // ── NEW: server-side paginated read ─────────────────────────────────
        public async Task<PagedResult<MeasurementData>> ReadSessionPageAsync(
            string filePath, int page, int pageSize, int? stepFilter = null)
        {
            try
            {
                await using var ctx = GetContext(filePath);

                var query = ctx.Measurements.AsNoTracking();
                if (stepFilter.HasValue)
                    query = query.Where(m => m.StepNumber == stepFilter.Value);

                var total = await query.CountAsync();

                var items = await query
                    .OrderBy(m => m.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new MeasurementData
                    {
                        Id                   = m.Id,
                        SessionID            = m.SessionID,
                        DeviceId             = m.DeviceId,
                        CircuitId            = m.CircuitId,
                        StepNumber           = m.StepNumber,
                        Operator             = m.Operator,
                        CircuitStatus        = m.CircuitStatus,
                        DateTime             = m.DateTime,
                        ProgramRunningTime   = m.ProgramRunningTime,
                        Current              = m.Current,
                        Voltage              = m.Voltage,
                        Temperature          = m.Temperature,
                        Power                = m.Power,
                        AccumulatedCapacity  = m.AccumulatedCapacity,
                        ChargeCapacity       = m.ChargeCapacity,
                        DischargeCapacity    = m.DischargeCapacity,
                        StepCapacity         = m.StepCapacity,
                        AccumulatedEnergy    = m.AccumulatedEnergy,
                        ChargeEnergy         = m.ChargeEnergy,
                        DischargeEnergy      = m.DischargeEnergy,
                        StepEnergy           = m.StepEnergy,
                        SystemErrorID        = m.SystemErrorID,
                        ErrorId              = m.ErrorId,
                        MessageId            = m.MessageId,
                        Remark               = m.Remark,
                        dbcValues            = m.dbcValues
                    })
                    .ToListAsync();

                // Forward-fill DBC nulls
                var signalNames = await FetchDbcSignalNamesAsync(ctx);
                string? seedJson = null;
                if (signalNames.Length > 0 && items.Count > 0)
                {
                    var firstId = items[0].Id;
                    var seedQuery = ctx.Measurements
                        .AsNoTracking()
                        .Where(m => m.Id < firstId && m.dbcValues != null);
                    if (stepFilter.HasValue)
                        seedQuery = seedQuery.Where(m => m.StepNumber == stepFilter.Value);
                    seedJson = await seedQuery
                        .OrderByDescending(m => m.Id)
                        .Select(m => m.dbcValues)
                        .FirstOrDefaultAsync();
                }

                ApplyDbcForwardFill(items, signalNames, seedJson);

                return new PagedResult<MeasurementData>
                {
                    Items     = items,
                    TotalRows = total,
                    Page      = page,
                    PageSize  = pageSize
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DB ERROR] ReadSessionPageAsync failed for {FilePath}", filePath);
                return PagedResult<MeasurementData>.Empty(pageSize);
            }
        }

        public async Task<int> CountSessionRowsAsync(string filePath, int? stepFilter = null)
        {
            try
            {
                await using var ctx = GetContext(filePath);
                var query = ctx.Measurements.AsNoTracking();
                if (stepFilter.HasValue)
                    query = query.Where(m => m.StepNumber == stepFilter.Value);
                return await query.CountAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DB ERROR] CountSessionRowsAsync failed for {FilePath}", filePath);
                return 0;
            }
        }

        // ── REG logs: distinct labels + paginated read, kept fully separate from Measurements ──
        public async Task<List<string>> GetDistinctRegLabelsAsync(string filePath)
        {
            try
            {
                await using var ctx = GetContext(filePath);

                var labels = await ctx.RegLogs
                    .AsNoTracking()
                    .Select(r => r.RegLabel)
                    .ToListAsync();

                // Case-insensitive de-duplication, preserving first-seen casing for display.
                return labels
                    .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DB ERROR] GetDistinctRegLabelsAsync failed for {FilePath}", filePath);
                return new List<string>();
            }
        }

        public async Task<PagedResult<RegLogRecord>> ReadRegLogPageAsync(
            string filePath, string regLabel, int page, int pageSize)
        {
            try
            {
                await using var ctx = GetContext(filePath);

                var query = ctx.RegLogs
                    .AsNoTracking()
                    .Where(r => r.RegLabel.ToUpper() == regLabel.ToUpper());

                var total = await query.CountAsync();

                var items = await query
                    .OrderBy(r => r.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new PagedResult<RegLogRecord>
                {
                    Items     = items,
                    TotalRows = total,
                    Page      = page,
                    PageSize  = pageSize
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DB ERROR] ReadRegLogPageAsync failed for {FilePath}, Label={RegLabel}", filePath, regLabel);
                return PagedResult<RegLogRecord>.Empty(pageSize);
            }
        }

        // ── NEW: chart downsampled read ──────────────────────────────────────
        public async Task<List<ChartPoint>> ReadChartSampleAsync(
            string filePath, int? stepFilter = null, int maxPoints = 3000)
        {
            try
            {
                await using var ctx = GetContext(filePath);

                var query = ctx.Measurements.AsNoTracking();
                if (stepFilter.HasValue)
                    query = query.Where(m => m.StepNumber == stepFilter.Value);

                int total = await query.CountAsync();
                if (total == 0) return new List<ChartPoint>();

                // Small session — read everything, no downsampling needed.
                if (total <= maxPoints)
                {
                    return await query
                        .OrderBy(m => m.Id)
                        .Select(m => new ChartPoint
                        {
                            Id                   = m.Id,
                            ProgramRunningTimeMs = m.ProgramRunningTime,
                            DateTime             = m.DateTime,
                            StepNumber           = m.StepNumber,
                            Current              = m.Current,
                            Voltage              = m.Voltage,
                            Temperature          = m.Temperature,
                            Power                = m.Power,
                            AccumulatedCapacity  = m.AccumulatedCapacity,
                            ChargeCapacity       = m.ChargeCapacity,
                            DischargeCapacity    = m.DischargeCapacity,
                            StepCapacity         = m.StepCapacity,
                            AccumulatedEnergy    = m.AccumulatedEnergy,
                            ChargeEnergy         = m.ChargeEnergy,
                            DischargeEnergy      = m.DischargeEnergy,
                            StepEnergy           = m.StepEnergy,
                        })
                        .ToListAsync();
                }

                // Large session — stride-sample INSIDE SQLite so only ~4×maxPoints
                // rows are ever materialized in .NET (previously ALL rows were
                // loaded: 1 ms data over 1 h = 3.6 M objects), then LTTB-fit the
                // candidate set down to maxPoints to preserve visual shape.
                int candidates = maxPoints * 4;
                int stride = Math.Max(1, total / candidates);

                string where = stepFilter.HasValue ? "WHERE \"StepNumber\" = @step" : "";
                string sql = $"""
                    SELECT "Id",
                           "ProgramRunningTime" AS "ProgramRunningTimeMs",
                           "DateTime", "StepNumber",
                           "Current", "Voltage", "Temperature", "Power",
                           "AccumulatedCapacity", "ChargeCapacity", "DischargeCapacity", "StepCapacity",
                           "AccumulatedEnergy", "ChargeEnergy", "DischargeEnergy", "StepEnergy"
                    FROM (
                        SELECT *, ROW_NUMBER() OVER (ORDER BY "Id") AS rn
                        FROM "Measurements"
                        {where}
                    )
                    WHERE (rn - 1) % @stride = 0
                    ORDER BY "Id"
                    """;

                var pars = new List<object>
                {
                    new Microsoft.Data.Sqlite.SqliteParameter("@stride", stride)
                };
                if (stepFilter.HasValue)
                    pars.Add(new Microsoft.Data.Sqlite.SqliteParameter("@step", stepFilter.Value));

                var raw = await ctx.Database
                    .SqlQueryRaw<ChartPoint>(sql, pars.ToArray())
                    .ToListAsync();

                if (raw.Count <= maxPoints) return raw;

                // Apply LTTB using ProgramRunningTime as x, Voltage as primary y
                // (LTTB picks visually significant points regardless of which field
                //  is ultimately displayed — shape preserved for all fields)
                return LttbDownsampler.Downsample(
                    raw,
                    maxPoints,
                    p => (double)p.ProgramRunningTimeMs,
                    p => (double)(p.Voltage ?? 0f));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DB ERROR] ReadChartSampleAsync failed for {FilePath}", filePath);
                return new List<ChartPoint>();
            }
        }

        // ── NEW: streaming read for export job ───────────────────────────────
        public async IAsyncEnumerable<List<MeasurementData>> StreamSessionDataAsync(
            string filePath, int batchSize = 5000,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Fetch signal names once before streaming starts
            string[] signalNames;
            try
            {
                await using var initCtx = GetContext(filePath);
                signalNames = await FetchDbcSignalNamesAsync(initCtx);
            }
            catch
            {
                signalNames = Array.Empty<string>();
            }

            string? lastDbcJson = null;
            int page = 1;

            while (true)
            {
                List<MeasurementData> batch;
                try
                {
                    await using var ctx = GetContext(filePath);
                    batch = await ctx.Measurements
                        .AsNoTracking()
                        .OrderBy(m => m.Id)
                        .Skip((page - 1) * batchSize)
                        .Take(batchSize)
                        .ToListAsync(ct);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DB ERROR] StreamSessionDataAsync batch {Page} failed for {FilePath}", page, filePath);
                    yield break;
                }

                if (batch.Count == 0) yield break;

                ApplyDbcForwardFill(batch, signalNames, lastDbcJson);

                // Carry the last non-null dbcValues forward to the next batch
                var lastNonNull = batch.LastOrDefault(r => !string.IsNullOrEmpty(r.dbcValues));
                if (lastNonNull != null) lastDbcJson = lastNonNull.dbcValues;

                yield return batch;
                if (batch.Count < batchSize) yield break;
                page++;
            }
        }

        private static List<MeasurementData> ApplyDbcForwardFill(
            List<MeasurementData> rows, string[] signalNames, string? seedJson = null)
        {
            if (signalNames.Length == 0) return rows;

            // Parse seed into currentValues
            string?[] currentValues = new string[signalNames.Length];
            if (!string.IsNullOrEmpty(seedJson))
            {
                if (seedJson.TrimStart().StartsWith('['))
                {
                    var arr = JsonConvert.DeserializeObject<string[]>(seedJson);
                    if (arr != null)
                        for (int i = 0; i < Math.Min(arr.Length, currentValues.Length); i++)
                            currentValues[i] = arr[i];
                }
                else if (seedJson.TrimStart().StartsWith('{'))
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(seedJson);
                    if (dict != null)
                        for (int i = 0; i < signalNames.Length; i++)
                            currentValues[i] = dict.TryGetValue(signalNames[i], out var v) ? v?.ToString() : null;
                }
            }

            foreach (var row in rows)
            {
                var raw = row.dbcValues?.TrimStart();

                if (string.IsNullOrEmpty(raw))
                {
                    // No change — apply last known values
                    row.dbcValues = BuildDictJson(signalNames, currentValues);
                }
                else if (raw.StartsWith('['))
                {
                    // New array format — update currentValues, rewrite as dict
                    var arr = JsonConvert.DeserializeObject<string[]>(row.dbcValues!);
                    if (arr != null)
                        for (int i = 0; i < Math.Min(arr.Length, currentValues.Length); i++)
                            currentValues[i] = arr[i];
                    row.dbcValues = BuildDictJson(signalNames, currentValues);
                }
                else if (raw.StartsWith('{'))
                {
                    // Old dict format — carry forward signal values
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(row.dbcValues!);
                    if (dict != null)
                        for (int i = 0; i < signalNames.Length; i++)
                            currentValues[i] = dict.TryGetValue(signalNames[i], out var v) ? v?.ToString() : currentValues[i];
                    // leave row.dbcValues as-is (already a valid dict)
                }
            }

            return rows;
        }

        private static string? BuildDictJson(string[] names, string?[] values)
        {
            if (names.Length == 0) return null;
            var d = new Dictionary<string, object?>(names.Length);
            for (int i = 0; i < names.Length; i++)
                d[names[i]] = values.Length > i ? values[i] : null;
            return JsonConvert.SerializeObject(d);
        }

        #endregion
    }
}
