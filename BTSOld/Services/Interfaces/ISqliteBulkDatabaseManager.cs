using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.SqliteEntities;
using BatteryTestingSystem.Models.ViewModels;
using System.Runtime.CompilerServices;

namespace BatteryTestingSystem.Services.Interfaces;

public interface ISqliteBulkDatabaseManager
{
    string BuildPath(string dbFileName);

    Task InsertRecordAsync(recordStoreRequest recordDto);
    Task InsertSessionAsync(SessionRequest session);
    Task<SessionRequest?> GetSessionAsync(string filePath);

    Task<List<MeasurementData>> ReadSessionDataAsync(string filePath);
    Task<PagedResult<MeasurementData>> ReadSessionPageAsync(string filePath, int page, int pageSize, int? stepFilter = null);
    Task<int> CountSessionRowsAsync(string filePath, int? stepFilter = null);
    Task<List<ChartPoint>> ReadChartSampleAsync(string filePath, int? stepFilter = null, int maxPoints = 3000);
    IAsyncEnumerable<List<MeasurementData>> StreamSessionDataAsync(string filePath, int batchSize = 5000, [EnumeratorCancellation] CancellationToken ct = default);

    // REG operator labeled logs — stored separately from Measurements (bifurcated inline by InsertRecordAsync)
    Task<List<string>> GetDistinctRegLabelsAsync(string filePath);
    Task<PagedResult<RegLogRecord>> ReadRegLogPageAsync(string filePath, string regLabel, int page, int pageSize);
}
