using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;
using Hangfire;
using Newtonsoft.Json;
using Serilog;

namespace BatteryTestingSystem.Services.Implementations;

public class SchedulerService : ISchedulerService
{
    private readonly ISchedulerRepository _repo;
    private readonly IProgramServices _programService;
    private readonly IBatteryServices _batteryService;
    private readonly IDbcService _dbcService;
    private readonly ChannelManager _circuitManager;

    public SchedulerService(
        ISchedulerRepository repo,
        IProgramServices programService,
        IBatteryServices batteryService,
        IDbcService dbcService,
        ChannelManager circuitManager)
    {
        _repo = repo;
        _programService = programService;
        _batteryService = batteryService;
        _dbcService = dbcService;
        _circuitManager = circuitManager;
    }

    // ─────────────────────────────────────────────────────
    // GET ALL
    // ─────────────────────────────────────────────────────
    public async Task<CommonResponse<List<ProgramScheduleDto>>> GetAllSchedulesAsync()
    {
        try
        {
            var schedules = await _repo.GetAllAsync();
            var active = schedules.Where(s => !s.IsDeleted).OrderByDescending(s => s.CreatedAt).ToList();

            var programsResult = await _programService.GetProgramsAsync();
            var batteriesResult = await _batteryService.GetBatteries();

            // Resolve DBC names for every distinct assigned port-dbc id across all schedules (avoids N+1 per-port lookups)
            var dbcIds = active
                .SelectMany(s => new[] { s.Port1DbcFileId, s.Port2DbcFileId, s.Port3DbcFileId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var dbcNameById = new Dictionary<long, string>();
            foreach (var id in dbcIds)
            {
                var dbcResult = await _dbcService.GetByIdAsync(id);
                if (dbcResult.Success && dbcResult.Data != null)
                    dbcNameById[id] = dbcResult.Data.Name;
            }

            var dtos = active.Select(s =>
            {
                var programName = programsResult.Data?.FirstOrDefault(p => p.ProgramId == s.ProgramId)?.ProgramName ?? $"#{s.ProgramId}";
                var batteryName = batteriesResult.Data?.FirstOrDefault(b => b.Id == s.BatteryId)?.Name ?? $"#{s.BatteryId}";
                var targets = JsonConvert.DeserializeObject<List<ScheduleCircuitTarget>>(s.TargetCircuitsJson) ?? new();

                return new ProgramScheduleDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    ProgramId = s.ProgramId,
                    ProgramName = programName,
                    BatteryId = s.BatteryId,
                    BatteryName = batteryName,
                    Port1DbcFileId = s.Port1DbcFileId,
                    Port1DbcFileName = s.Port1DbcFileId.HasValue ? dbcNameById.GetValueOrDefault(s.Port1DbcFileId.Value) : null,
                    Port2DbcFileId = s.Port2DbcFileId,
                    Port2DbcFileName = s.Port2DbcFileId.HasValue ? dbcNameById.GetValueOrDefault(s.Port2DbcFileId.Value) : null,
                    Port3DbcFileId = s.Port3DbcFileId,
                    Port3DbcFileName = s.Port3DbcFileId.HasValue ? dbcNameById.GetValueOrDefault(s.Port3DbcFileId.Value) : null,
                    ScheduledAt = s.ScheduledAt,
                    TargetCircuits = targets,
                    IsActive = s.IsActive,
                    HangfireJobId = s.HangfireJobId,
                    CreatedBy = s.CreatedBy,
                    CreatedAt = s.CreatedAt
                };
            }).ToList();

            return CommonResponse<List<ProgramScheduleDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SchedulerService.GetAllSchedulesAsync failed");
            return CommonResponse<List<ProgramScheduleDto>>.Fail(ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────
    // CREATE
    // ─────────────────────────────────────────────────────
    public async Task<CommonResponse<ProgramScheduleDto>> CreateScheduleAsync(CreateScheduleRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return CommonResponse<ProgramScheduleDto>.Fail("Schedule name is required.");

            if (request.TargetCircuits == null || !request.TargetCircuits.Any())
                return CommonResponse<ProgramScheduleDto>.Fail("Select at least one target circuit.");

            if (request.ScheduledAt <= DateTime.Now)
                return CommonResponse<ProgramScheduleDto>.Fail("Scheduled time must be in the future.");

            var entity = new ProgramSchedule
            {
                Name = request.Name,
                ProgramId = request.ProgramId,
                BatteryId = request.BatteryId,
                Port1DbcFileId = request.Port1DbcFileId,
                Port2DbcFileId = request.Port2DbcFileId,
                Port3DbcFileId = request.Port3DbcFileId,
                ScheduledAt = request.ScheduledAt,
                TargetCircuitsJson = JsonConvert.SerializeObject(request.TargetCircuits),
                IsActive = true,
                CreatedBy = Utils.CurrentUser.UserName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _repo.AddAsync(entity);
            await _repo.SaveChangesAsync();

            // Register Hangfire delayed job
            var delay = request.ScheduledAt - DateTime.Now;
            var jobId = BackgroundJob.Schedule<ISchedulerService>(
                svc => svc.ExecuteScheduleAsync(entity.Id),
                delay);

            entity.HangfireJobId = jobId;
            await _repo.UpdateAsync(entity);
            await _repo.SaveChangesAsync();

            return CommonResponse<ProgramScheduleDto>.Ok(new ProgramScheduleDto
            {
                Id = entity.Id,
                Name = entity.Name,
                ScheduledAt = entity.ScheduledAt,
                IsActive = entity.IsActive,
                HangfireJobId = jobId
            }, "Schedule created successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SchedulerService.CreateScheduleAsync failed");
            return CommonResponse<ProgramScheduleDto>.Fail(ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────
    // DELETE
    // ─────────────────────────────────────────────────────
    public async Task<CommonResponse<bool>> DeleteScheduleAsync(long scheduleId)
    {
        try
        {
            var entity = await _repo.GetByIdAsync(scheduleId);
            if (entity == null) return CommonResponse<bool>.Fail("Schedule not found.");

            // Cancel Hangfire job if still pending
            if (!string.IsNullOrEmpty(entity.HangfireJobId))
                BackgroundJob.Delete(entity.HangfireJobId);

            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.Now;
            await _repo.UpdateAsync(entity);
            await _repo.SaveChangesAsync();

            return CommonResponse<bool>.Ok(true, "Schedule deleted.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SchedulerService.DeleteScheduleAsync failed");
            return CommonResponse<bool>.Fail(ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────
    // TOGGLE ACTIVE
    // ─────────────────────────────────────────────────────
    public async Task<CommonResponse<bool>> ToggleActiveAsync(long scheduleId, bool isActive)
    {
        try
        {
            var entity = await _repo.GetByIdAsync(scheduleId);
            if (entity == null) return CommonResponse<bool>.Fail("Schedule not found.");

            entity.IsActive = isActive;
            entity.UpdatedAt = DateTime.Now;
            await _repo.UpdateAsync(entity);
            await _repo.SaveChangesAsync();

            return CommonResponse<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SchedulerService.ToggleActiveAsync failed");
            return CommonResponse<bool>.Fail(ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────
    // GET LOGS
    // ─────────────────────────────────────────────────────
    public async Task<CommonResponse<List<ScheduleExecutionLogDto>>> GetLogsAsync(long? scheduleId = null)
    {
        try
        {
            List<ScheduleExecutionLog> logs;

            if (scheduleId.HasValue)
                logs = await _repo.GetLogsByScheduleIdAsync(scheduleId.Value);
            else
                logs = await _repo.GetAllLogsAsync();

            var dtos = logs.Select(l => new ScheduleExecutionLogDto
            {
                Id = l.Id,
                ScheduleId = l.ScheduleId,
                ScheduleName = l.Schedule?.Name ?? $"#{l.ScheduleId}",
                DeviceId = l.DeviceId,
                CircuitId = l.CircuitId,
                ExecutedAt = l.ExecutedAt,
                Status = l.Status,
                FailReason = l.FailReason
            }).ToList();

            return CommonResponse<List<ScheduleExecutionLogDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SchedulerService.GetLogsAsync failed");
            return CommonResponse<List<ScheduleExecutionLogDto>>.Fail(ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────
    // EXECUTE (called by Hangfire at scheduled time)
    // ─────────────────────────────────────────────────────
    public async Task ExecuteScheduleAsync(long scheduleId)
    {
        Log.Information("Scheduler: Executing schedule {ScheduleId}", scheduleId);

        var entity = await _repo.GetByIdAsync(scheduleId);
        if (entity == null || !entity.IsActive || entity.IsDeleted)
        {
            Log.Warning("Scheduler: Schedule {ScheduleId} skipped — not found / inactive.", scheduleId);
            return;
        }

        var targets = JsonConvert.DeserializeObject<List<ScheduleCircuitTarget>>(entity.TargetCircuitsJson) ?? new();

        // Load program, battery, dbc
        var programResult = await _programService.GetProgramAsync(entity.ProgramId);
        var batteryResult = await _batteryService.GetBattery(entity.BatteryId);

        async Task<DbcFileRecordDto?> ResolveDbcAsync(long? dbcFileId)
        {
            if (!dbcFileId.HasValue) return null;
            var result = await _dbcService.GetByIdAsync(dbcFileId.Value);
            return result.Success ? result.Data : null;
        }

        var port1Dbc = await ResolveDbcAsync(entity.Port1DbcFileId);
        var port2Dbc = await ResolveDbcAsync(entity.Port2DbcFileId);
        var port3Dbc = await ResolveDbcAsync(entity.Port3DbcFileId);

        foreach (var target in targets)
        {
            var log = new ScheduleExecutionLog
            {
                ScheduleId = scheduleId,
                DeviceId = target.DeviceId,
                CircuitId = target.CircuitId,
                ExecutedAt = DateTime.Now
            };

            // Find live circuit handler
            var key = (target.DeviceId, target.CircuitId);
            var handler = _circuitManager._devices.Values
                .FirstOrDefault(c => c.Channel.DeviceID == target.DeviceId && c.Channel.ChannelNumber == target.CircuitId);

            if (handler == null)
            {
                log.Status = "Skipped_Offline";
                log.FailReason = "Channel not found in ChannelManager";
                await _repo.AddLogAsync(log);
                continue;
            }

            var cs = handler.RealTime.RealTimeRecord.CircuitStatus;
            var ps = handler.RealTime.RealTimeRecord.ProgramStatus;

            // Must be Online + Idle + Stop
            if (cs == CircuitStatus.Offline)
            {
                log.Status = "Skipped_Offline";
                log.FailReason = $"Circuit is Offline";
                await _repo.AddLogAsync(log);
                continue;
            }

            if (cs != CircuitStatus.Idle || ps != ProgramRunningStatus.Stop)
            {
                log.Status = "Skipped_AlreadyRunning";
                log.FailReason = $"CircuitStatus={cs}, ProgramStatus={ps}";
                await _repo.AddLogAsync(log);
                continue;
            }

            // ── Transfer Steps ──
            var isReady = await handler.HWReadyToReadWriteAsync();
            if (!isReady.Success)
            {
                log.Status = "Failed";
                log.FailReason = $"IsReady failed: {isReady.Message}";
                await _repo.AddLogAsync(log);
                continue;
            }

            if (batteryResult.Success && batteryResult.Data != null)
            {
                var batteryRes = await handler.SetBatteryParamAsync(batteryResult.Data);
                if (!batteryRes.Success)
                {
                    log.Status = "Failed";
                    log.FailReason = $"Battery transfer failed: {batteryRes.Message}";
                    await _repo.AddLogAsync(log);
                    continue;
                }
            }

            if (port1Dbc != null || port2Dbc != null || port3Dbc != null)
            {
                var dbcRes = await handler.TransferDbcFile(port1Dbc, port2Dbc, port3Dbc);
                if (!dbcRes.Success)
                {
                    log.Status = "Failed";
                    log.FailReason = $"DBC transfer failed: {dbcRes.Message}";
                    await _repo.AddLogAsync(log);
                    continue;
                }
            }

            if (programResult.Success && programResult.Data != null)
            {
                var progRes = await handler.SetProgramAsync(programResult.Data, batteryResult.Success ? batteryResult.Data : null);
                if (!progRes.Success)
                {
                    log.Status = "Failed";
                    log.FailReason = $"Program transfer failed: {progRes.Message}";
                    await _repo.AddLogAsync(log);
                    continue;
                }
            }

            // ── START ──
            var startRes = await handler.StartProgram();
            if (!startRes.Success)
            {
                log.Status = "Failed";
                log.FailReason = $"Start failed: {startRes.Message}";
                await _repo.AddLogAsync(log);
                continue;
            }

            log.Status = "Success";
            await _repo.AddLogAsync(log);
            Log.Debug("Scheduler: Circuit {D}-{C} started successfully.", target.DeviceId, target.CircuitId);
        }
    }
}
