using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public interface IWorkflowTopologyProvider
{
    Task<TopologySnapshot> GetSnapshotAsync();

    Task<PaletteCatalog> GetCatalogAsync();
}

/// <summary>
/// The single bridge between EF and the pure workflow classes. Everything downstream takes a
/// TopologySnapshot, which is why the builder, validator and service need no database at all.
/// One query per table, projected immediately — this runs on the Blazor circuit's shared
/// AppDbContext, so it must never be called concurrently with itself (ADR-6).
/// </summary>
public class WorkflowTopologyProvider : IWorkflowTopologyProvider
{
    private readonly AppDbContext _context;

    public WorkflowTopologyProvider(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TopologySnapshot> GetSnapshotAsync()
    {
        var devices = await _context.Devices
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .Select(d => new { d.DeviceID, d.DeviceName })
            .ToListAsync();

        var boards = await _context.SecondaryBoards
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .Select(b => new { b.Id, b.DeviceId, b.BoardNumber })
            .ToListAsync();

        var channels = await _context.Channels
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .Select(c => new { c.Id, c.SecondaryBoardId, c.ChannelNumber })
            .ToListAsync();

        var snapshot = devices
            .Select(d => new TopologyDevice(
                d.DeviceID,
                d.DeviceName ?? $"Device {d.DeviceID}",
                boards
                    .Where(b => b.DeviceId == d.DeviceID)
                    .OrderBy(b => b.BoardNumber)
                    .Select(b => new TopologyBoard(
                        b.Id,
                        b.BoardNumber,
                        channels
                            .Where(c => c.SecondaryBoardId == b.Id)
                            .OrderBy(c => c.ChannelNumber)
                            .Select(c => new TopologyChannel(c.Id, c.ChannelNumber))
                            .ToList()))
                    .ToList()))
            .OrderBy(d => d.DeviceId)
            .ToList();

        return new TopologySnapshot(snapshot);
    }

    public async Task<PaletteCatalog> GetCatalogAsync()
    {
        // Program and DBC file ids are `long` in the database; NodeAttachment stores `int?`
        // because no realistic program/DBC library approaches int range, and widening the
        // document schema to long would bump WorkflowGraph.CurrentVersion for no real benefit.
        var programs = await _context.Programs
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Select(p => new { p.Id, p.ProgramName })
            .ToListAsync();

        var dbcs = await _context.dbcFileRecords
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync();

        // Batteries, NOT BatteryTypes. BatteryTypes is empty on real deployments while the
        // batteries the rest of the app works with - the ones the transfer dialog offers and the
        // ones a channel reports on its node face - live in Batteries. Reading the wrong table
        // made the palette say "No battery types configured", so no battery could be placed, so no
        // Channel-Battery power edge could exist, so the flow animation had nothing to animate.
        var batteryTypes = await _context.Batteries
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .Select(b => new { b.Id, b.Name })
            .ToListAsync();

        return new PaletteCatalog(
            programs.Select(p => new PaletteProgram((int)p.Id, p.ProgramName)).ToList(),
            dbcs.Select(d => new PaletteDbc((int)d.Id, d.Name)).ToList(),
            batteryTypes.Select(b => new PaletteBatteryType((int)b.Id, b.Name)).ToList());
    }
}
