using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Repositories.Interfaces;

namespace BatteryTestingSystem.Tests.Services;

/// <summary>
/// In-memory stand-in for IAlarmRepository. Also counts writes so tests can assert the
/// debounce actually prevents database churn.
/// </summary>
public class FakeAlarmRepository : IAlarmRepository
{
    public readonly List<AlarmLog> Rows = new();
    public int InsertCount;
    public int SaveCount;

    private long _nextId = 1;

    public Task<AlarmLog?> GetActiveByKeyAsync(string alarmKey) =>
        Task.FromResult(Rows.FirstOrDefault(a =>
            a.AlarmKey == alarmKey && a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null));

    public Task<AlarmLog> InsertAsync(AlarmLog alarm)
    {
        alarm.Id = _nextId++;
        Rows.Add(alarm);
        InsertCount++;
        return Task.FromResult(alarm);
    }

    public Task SaveAsync(AlarmLog alarm)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<List<AlarmLog>> GetActiveAsync(int take) =>
        Task.FromResult(Rows.Where(a => a.ClearedAtUtc == null)
            .OrderByDescending(a => a.LastSeenUtc).Take(take).ToList());

    public Task<int> CountUnacknowledgedAsync() =>
        Task.FromResult(Rows.Count(a => a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null));

    public Task<bool> AcknowledgeAsync(long id, string user, DateTime whenUtc)
    {
        var row = Rows.FirstOrDefault(a => a.Id == id);
        if (row == null || row.AcknowledgedAtUtc != null) return Task.FromResult(false);
        row.AcknowledgedAtUtc = whenUtc;
        row.AcknowledgedBy = user;
        return Task.FromResult(true);
    }

    public Task<int> AcknowledgeAllAsync(string user, DateTime whenUtc)
    {
        var rows = Rows.Where(a => a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null).ToList();
        foreach (var row in rows)
        {
            row.AcknowledgedAtUtc = whenUtc;
            row.AcknowledgedBy = user;
        }
        return Task.FromResult(rows.Count);
    }

    public Task<bool> ClearByKeyAsync(string alarmKey, DateTime whenUtc)
    {
        var row = Rows.FirstOrDefault(a =>
            a.AlarmKey == alarmKey && a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null);
        if (row == null) return Task.FromResult(false);
        row.ClearedAtUtc = whenUtc;
        return Task.FromResult(true);
    }

    public Task<CommonResponse<List<AlarmLog>>> QueryAsync(AlarmQueryParameters? request) =>
        Task.FromResult(CommonResponse<List<AlarmLog>>.Ok(Rows.ToList()));

    public Task<int> PruneAsync(DateTime cutoffUtc, int batchSize)
    {
        var doomed = Rows.Where(a => a.LastSeenUtc < cutoffUtc
                                     && (a.AcknowledgedAtUtc != null || a.ClearedAtUtc != null))
                         .Take(batchSize).ToList();
        foreach (var row in doomed) Rows.Remove(row);
        return Task.FromResult(doomed.Count);
    }
}
