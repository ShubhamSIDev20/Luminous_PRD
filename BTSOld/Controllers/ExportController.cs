using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Implementations;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using IODirectory = System.IO.Directory;

namespace BatteryTestingSystem.Controllers;

/// <summary>
/// REST controller for async export requests.
///
/// POST /api/export/request   → queue a Hangfire job, return ExportRecord.Id
/// GET  /api/export/status/{id} → poll job status
/// GET  /api/export/list      → list caller's past exports
/// GET  /api/export/download/{id} → stream the ready .xlsx file
/// DELETE /api/export/{id}    → soft-delete an export record (and optionally file)
/// </summary>
[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IExportRepository _repo;
    private readonly IBackgroundJobClient _hangfire;
    private readonly ExportJobService _jobService;
    private readonly ILogger<ExportController> _logger;

    public ExportController(
        IExportRepository repo,
        IBackgroundJobClient hangfire,
        ExportJobService jobService,
        ILogger<ExportController> logger)
    {
        _repo       = repo;
        _hangfire   = hangfire;
        _jobService = jobService;
        _logger     = logger;
    }

    // ── Request a new export ─────────────────────────────────────────────────
    [HttpPost("request")]
    public async Task<IActionResult> RequestExport([FromBody] ExportRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SessionFilePath))
            return BadRequest("SessionFilePath is required.");

        var userName = User.Identity?.Name ?? "unknown";

        // Check if a Ready export already exists for this session + user.
        // Return the existing one so we don't regenerate unnecessarily.
        var existing = await _repo.GetBySessionAsync(dto.SessionFilePath);
        var ready = existing.FirstOrDefault(e =>
            e.RequestedBy == userName && e.Status == ExportStatus.Ready &&
            IOFile.Exists(e.ExportFilePath));
        if (ready != null && !dto.ForceRegenerate)
            return Ok(new { ready.Id, ready.Status, ready.FileSizeBytes, Reused = true });

        // Create the DB record first (job needs its Id)
        var record = new ExportRecord
        {
            SessionFilePath = dto.SessionFilePath,
            RequestedBy     = userName,
            RequestedAt     = DateTime.Now,
            Status          = ExportStatus.Pending,
            DisplayName     = dto.DisplayName ?? IOPath.GetFileNameWithoutExtension(dto.SessionFilePath)
        };
        var created = await _repo.CreateAsync(record);

        // Enqueue Hangfire job
        var jobId = _hangfire.Enqueue<ExportJobService>(svc => svc.RunAsync(created.Id, CancellationToken.None));

        // Persist the Hangfire job ID for observability
        await _repo.UpdateStatusAsync(created.Id, ExportStatus.Pending);

        _logger.LogInformation("Export requested: record={Id} job={JobId} session={Path}",
            created.Id, jobId, dto.SessionFilePath);

        return Ok(new { created.Id, created.Status, JobId = jobId, Reused = false });
    }

    // ── Poll status ──────────────────────────────────────────────────────────
    [HttpGet("status/{id:int}")]
    public async Task<IActionResult> Status(int id)
    {
        var rec = await _repo.GetByIdAsync(id);
        if (rec == null) return NotFound();

        return Ok(new
        {
            rec.Id, rec.Status, rec.FileSizeBytes,
            rec.RequestedAt, rec.CompletedAt, rec.ErrorMessage,
            CanDownload = rec.Status == ExportStatus.Ready && IOFile.Exists(rec.ExportFilePath)
        });
    }

    // ── List caller's exports ─────────────────────────────────────────────────
    [HttpGet("list")]
    public async Task<IActionResult> List()
    {
        var userName = User.Identity?.Name ?? "unknown";
        var records  = await _repo.GetByUserAsync(userName);
        return Ok(records.Select(r => new
        {
            r.Id, r.Status, r.DisplayName, r.SessionFilePath,
            r.RequestedAt, r.CompletedAt, r.FileSizeBytes, r.ErrorMessage,
            CanDownload = r.Status == ExportStatus.Ready && IOFile.Exists(r.ExportFilePath)
        }));
    }

    // ── Download ──────────────────────────────────────────────────────────────
    [HttpGet("download/{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        var rec = await _repo.GetByIdAsync(id);
        if (rec == null)                             return NotFound("Export record not found.");
        if (rec.Status != ExportStatus.Ready)        return BadRequest($"Export is not ready (status: {rec.Status}).");
        if (!IOFile.Exists(rec.ExportFilePath))      return NotFound("Export file not found on disk.");

        var userName = User.Identity?.Name ?? "unknown";
        if (rec.RequestedBy != userName && !User.IsInRole("Admin"))
            return Forbid();

        var stream   = IOFile.OpenRead(rec.ExportFilePath!);
        var fileName = IOPath.GetFileName(rec.ExportFilePath);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // ── Soft-delete ───────────────────────────────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool deleteFile = false)
    {
        var rec = await _repo.GetByIdAsync(id);
        if (rec == null) return NotFound();

        if (deleteFile && !string.IsNullOrEmpty(rec.ExportFilePath) && IOFile.Exists(rec.ExportFilePath))
        {
            try { IOFile.Delete(rec.ExportFilePath); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete export file {Path}", rec.ExportFilePath);
            }
        }

        await _repo.DeleteAsync(id);
        return NoContent();
    }
}

// ── DTO ───────────────────────────────────────────────────────────────────────
public record ExportRequestDto(
    string SessionFilePath,
    string? DisplayName = null,
    bool ForceRegenerate = false);
