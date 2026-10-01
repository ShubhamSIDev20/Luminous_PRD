using BatteryTestingSystem.Controllers;
using BatteryTestingSystem.Models.APIModels;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Interfaces;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BatteryTestingSystem.MCP
{
    /// <summary>
    /// MCP tools for Claude Desktop / LLM clients.
    /// Reuses the same static Core helpers from DeviceController — zero duplicated logic.
    /// Register in Program.cs:
    ///     builder.Services.AddMcpServer()
    ///         .WithHttpTransport(o => o.Stateless = true)
    ///         .WithTools&lt;DeviceMcpTools&gt;();
    ///     app.MapMcp("/mcp").RequireAuthorization();
    /// </summary>
    [McpServerToolType]
    public class DeviceMcpTools
    {
        private readonly CircuitManager _cm;
        private readonly IProgramServices _programServices;
        private readonly IBatteryServices _batteryServices;
        private readonly IDbcService _dbcService;

        public DeviceMcpTools(
            CircuitManager cm,
            IProgramServices programServices,
            IBatteryServices batteryServices,
            IDbcService dbcService)
        {
            _cm = cm;
            _programServices = programServices;
            _batteryServices = batteryServices;
            _dbcService = dbcService;
        }

        // ─────────────────────────────────────────────────────────────
        // CONTROL TOOLS
        // ─────────────────────────────────────────────────────────────

        [McpServerTool]
        [Description("Send program, battery config, and DBC file to a circuit before starting a test. " +
                     "batteryId and dbcId are optional — omit if not needed.")]
        public async Task<string> SendProgram(
            [Description("Device ID (integer)")] int deviceId,
            [Description("Circuit ID (integer)")] int circuitId,
            [Description("Program ID to load")] int programId,
            [Description("Battery ID to configure (optional)")] int? batteryId,
            [Description("DBC file record ID (optional)")] int? dbcId)
        {
            var request = new List<CommonRequest>
            {
                new() { DeviceID = deviceId, CircuitID = circuitId, ProgramId = programId, BatteryId = batteryId, dbcId = dbcId }
            };
            var messages = await DeviceController.CoreSendProgram(_cm, _programServices, _batteryServices, _dbcService, request);
            return string.Join("\n", messages);
        }

        [McpServerTool]
        [Description("Start a battery test program on a circuit.")]
        public async Task<string> StartProgram(
            [Description("Device ID")] int deviceId,
            [Description("Circuit ID")] int circuitId)
        {
            var messages = await DeviceController.CoreStart(_cm,
                new List<CommonRequest> { new() { DeviceID = deviceId, CircuitID = circuitId } });
            return string.Join("\n", messages);
        }

        [McpServerTool]
        [Description("Stop a running battery test on a circuit.")]
        public async Task<string> StopProgram(
            [Description("Device ID")] int deviceId,
            [Description("Circuit ID")] int circuitId)
        {
            var messages = await DeviceController.CoreStop(_cm,
                new List<CommonRequest> { new() { DeviceID = deviceId, CircuitID = circuitId } });
            return string.Join("\n", messages);
        }

        [McpServerTool]
        [Description("Pause a running battery test on a circuit.")]
        public async Task<string> PauseProgram(
            [Description("Device ID")] int deviceId,
            [Description("Circuit ID")] int circuitId)
        {
            var messages = await DeviceController.CorePause(_cm,
                new List<CommonRequest> { new() { DeviceID = deviceId, CircuitID = circuitId } });
            return string.Join("\n", messages);
        }

        [McpServerTool]
        [Description("Resume (continue) a paused battery test on a circuit.")]
        public async Task<string> ContinueProgram(
            [Description("Device ID")] int deviceId,
            [Description("Circuit ID")] int circuitId)
        {
            var messages = await DeviceController.CoreContinue(_cm,
                new List<CommonRequest> { new() { DeviceID = deviceId, CircuitID = circuitId } });
            return string.Join("\n", messages);
        }

        // ─────────────────────────────────────────────────────────────
        // DATA / STATUS TOOLS
        // ─────────────────────────────────────────────────────────────

        [McpServerTool]
        [Description("Get the current session info (ID, name, start/end time, battery, program, DBC) for a circuit.")]
        public string GetSession(
            [Description("Device ID")] int deviceId,
            [Description("Circuit ID")] int circuitId)
        {
            var key = $"{deviceId}-{circuitId}";
            if (!_cm._devices.TryGetValue(key, out var handler))
                return $"Handler not found for {key}";

            var s = handler.Session;
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                s.SessionID,
                s.SessionName,
                s.StartTime,
                s.EndTime,
                s.DeviceID,
                s.CircuitID,
                s.BatteryID,
                s.BatteryName,
                s.ProgramID,
                s.ProgramName,
                s.DbcFileRecordID,
                s.DbcName,
                s.Storerecordcount,
                s.Unstorerecordcount
            });
        }

        [McpServerTool]
        [Description("Get real-time readings for a circuit: voltage, current, temperature, state, etc.")]
        public string GetDeviceStatus(
            [Description("Device ID")] int deviceId,
            [Description("Circuit ID")] int circuitId)
        {
            var key = $"{deviceId}-{circuitId}";
            if (!_cm._devices.TryGetValue(key, out var handler))
                return $"Handler not found for {key}";

            return System.Text.Json.JsonSerializer.Serialize(handler.RealTime.RealTimeRecord);
        }

        [McpServerTool]
        [Description("Get live data records for a circuit. Same as GetDeviceStatus but supports querying multiple circuits at once.")]
        public async Task<string> GetLiveData(
            [Description("Device ID")] int deviceId,
            [Description("Circuit ID")] int circuitId)
        {
            var data = await DeviceController.CoreLiveData(_cm,
                new List<CommonRequest> { new() { DeviceID = deviceId, CircuitID = circuitId } });

            if (data.Count == 0)
                return $"No live data found for {deviceId}-{circuitId}";

            return System.Text.Json.JsonSerializer.Serialize(data);
        }

        [McpServerTool]
        [Description("List all available test programs with their ID, name, and description.")]
        public async Task<string> GetPrograms()
        {
            var result = await _programServices.GetProgramsAsync();
            if (!result.Success) return $"Failed to retrieve programs: {result.Message}";

            return System.Text.Json.JsonSerializer.Serialize(result.Data.Select(p => new
            {
                p.ProgramId,
                p.ProgramName,
                p.Description,
                p.CreatedBy,
                p.CreatedAt
            }));
        }

        [McpServerTool]
        [Description("List all batteries registered in the system.")]
        public async Task<string> GetBatteries()
        {
            var result = await _batteryServices.GetBatteries();
            return System.Text.Json.JsonSerializer.Serialize(result);
        }
    }
}
