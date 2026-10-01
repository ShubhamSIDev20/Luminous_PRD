using BatteryTestingSystem.Models.APIModels;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using BatteryTestingSystem.Utils;
using Serilog;

namespace BatteryTestingSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeviceController : ControllerBase
    {
        private readonly ChannelManager _cm;
        private readonly IDeviceChannelServices _circuitServices;
        private readonly IProgramServices _programServices;
        private readonly IBatteryServices _batteryServices;
        private readonly IDbcService _dbcService;
        private readonly Serilog.ILogger _logger = Log.ForContext<DeviceController>();

        public DeviceController(
            ChannelManager cm,
            IProgramServices programServices,
            IDeviceChannelServices circuitServices,
            IBatteryServices batteryServices,
            IDbcService dbcService)
        {
            _cm = cm;
            _programServices = programServices;
            _circuitServices = circuitServices;
            _batteryServices = batteryServices;
            _dbcService = dbcService;
        }

        // ─────────────────────────────────────────────────────────────
        // INTERNAL CORE HELPERS  (shared with DeviceMcpTools via service layer)
        // ─────────────────────────────────────────────────────────────

        internal static async Task<List<string>> CoreSendProgram(
            ChannelManager cm,
            IProgramServices programServices,
            IBatteryServices batteryServices,
            IDbcService dbcService,
            List<CommonRequest> request)
        {
            var messages = new List<string>();

            foreach (var req in request)
            {
                List<int> channels;
                try
                {
                    channels = ChannelExpander.GetChannels(req);
                }
                catch (ArgumentException ex)
                {
                    messages.Add($"{req.DeviceID}-{req.SecondaryBoardNumber} -> {ex.Message}");
                    continue;
                }

                // Program/Battery/DBC are the same for every channel in this request —
                // fetch each once instead of re-querying per channel.
                var program = (req.ProgramId.HasValue && req.ProgramId.Value > 0)
                    ? await programServices.GetProgramAsync((long)req.ProgramId.Value)
                    : null;
                var battery = (req.BatteryId.HasValue && req.BatteryId.Value > 0)
                    ? await batteryServices.GetBattery((long)req.BatteryId.Value)
                    : null;
                var dbc = (req.dbcId.HasValue && req.dbcId.Value > 0)
                    ? await dbcService.GetByIdAsync((long)req.dbcId.Value)
                    : null;

                foreach (var ch in channels)
                {
                    var key = $"{req.DeviceID}-{req.SecondaryBoardNumber}-{ch}";

                    if (!cm._devices.TryGetValue(key, out IChannelCommandHandler? handler))
                    {
                        messages.Add($"{key} -> Handler not found");
                        continue;
                    }

                    var isReady = await handler.HWReadyToReadWriteAsync();
                    if (!isReady.Success)
                    {
                        messages.Add($"{key} -> Hardware not ready: {isReady.Message}");
                        continue;
                    }

                    // Program
                    if (program != null)
                    {
                        if (program.Success)
                        {
                            var send = await handler.SetProgramAsync(program.Data, battery?.Data);
                            messages.Add(send.Success
                                ? $"Program : {key} -> Success"
                                : $"Program : {key} -> Failed: {send.Message}");
                        }
                        else
                            messages.Add($"Program : {key} -> {program.Message}");
                    }

                    // Battery
                    if (battery != null)
                    {
                        if (battery.Success)
                        {
                            var send = await handler.SetBatteryParamAsync(battery.Data);
                            messages.Add(send.Success
                                ? $"Battery : {key} -> Success"
                                : $"Battery : {key} -> Failed: {send.Message}");
                        }
                        else
                            messages.Add($"Battery : {key} -> {battery.Message}");
                    }

                    // DBC
                    if (dbc != null)
                    {
                        if (dbc.Success)
                        {
                            var send = await handler.TransferDbcFile(dbc.Data);
                            messages.Add(send.Success
                                ? $"DBC : {key} -> Success"
                                : $"DBC : {key} -> Failed: {send.Message}");
                        }
                        else
                            messages.Add($"DBC : {key} -> {dbc.Message}");

                        handler.dbcData.DbcValues = null;
                    }
                }
            }

            return messages;
        }

        internal static async Task<List<string>> CoreStart(ChannelManager cm, List<CommonRequest> request)
        {
            var messages = new List<string>();
            foreach (var req in request)
            {
                List<int> channels;
                try { channels = ChannelExpander.GetChannels(req); }
                catch (ArgumentException ex)
                { messages.Add($"{req.DeviceID}-{req.SecondaryBoardNumber} -> {ex.Message}"); continue; }

                foreach (var ch in channels)
                {
                    var key = $"{req.DeviceID}-{req.SecondaryBoardNumber}-{ch}";
                    if (!cm._devices.TryGetValue(key, out var handler))
                    { messages.Add($"{key} -> Handler not found"); continue; }

                    var result = await handler.StartProgram();
                    messages.Add(result.Success ? $"{key} -> Success" : $"{key} -> Failed: {result.Message}");
                }
            }
            return messages;
        }

        internal static async Task<List<string>> CoreStop(ChannelManager cm, List<CommonRequest> request)
        {
            var messages = new List<string>();
            foreach (var req in request)
            {
                List<int> channels;
                try { channels = ChannelExpander.GetChannels(req); }
                catch (ArgumentException ex)
                { messages.Add($"{req.DeviceID}-{req.SecondaryBoardNumber} -> {ex.Message}"); continue; }

                foreach (var ch in channels)
                {
                    var key = $"{req.DeviceID}-{req.SecondaryBoardNumber}-{ch}";
                    if (!cm._devices.TryGetValue(key, out var handler))
                    { messages.Add($"{key} -> Handler not found"); continue; }

                    var result = await handler.StopProgram();
                    messages.Add(result.Success ? $"{key} -> Success" : $"{key} -> Failed: {result.Message}");
                }
            }
            return messages;
        }

        internal static async Task<List<string>> CorePause(ChannelManager cm, List<CommonRequest> request)
        {
            var messages = new List<string>();
            foreach (var req in request)
            {
                List<int> channels;
                try { channels = ChannelExpander.GetChannels(req); }
                catch (ArgumentException ex)
                { messages.Add($"{req.DeviceID}-{req.SecondaryBoardNumber} -> {ex.Message}"); continue; }

                foreach (var ch in channels)
                {
                    var key = $"{req.DeviceID}-{req.SecondaryBoardNumber}-{ch}";
                    if (!cm._devices.TryGetValue(key, out var handler))
                    { messages.Add($"{key} -> Handler not found"); continue; }

                    var result = await handler.PauseProgram();
                    messages.Add(result.Success ? $"{key} -> Success" : $"{key} -> Failed: {result.Message}");
                }
            }
            return messages;
        }

        internal static async Task<List<string>> CoreContinue(ChannelManager cm, List<CommonRequest> request)
        {
            var messages = new List<string>();
            foreach (var req in request)
            {
                List<int> channels;
                try { channels = ChannelExpander.GetChannels(req); }
                catch (ArgumentException ex)
                { messages.Add($"{req.DeviceID}-{req.SecondaryBoardNumber} -> {ex.Message}"); continue; }

                foreach (var ch in channels)
                {
                    var key = $"{req.DeviceID}-{req.SecondaryBoardNumber}-{ch}";
                    if (!cm._devices.TryGetValue(key, out var handler))
                    { messages.Add($"{key} -> Handler not found"); continue; }

                    var result = await handler.ContinueProgram();
                    messages.Add(result.Success ? $"{key} -> Success" : $"{key} -> Failed: {result.Message}");
                }
            }
            return messages;
        }

        internal static async Task<List<RealTimeRecordDto>> CoreLiveData(ChannelManager cm, List<CommonRequest> request)
        {
            var data = new List<RealTimeRecordDto>();
            foreach (var req in request)
            {
                List<int> channels;
                try { channels = ChannelExpander.GetChannels(req); }
                catch (ArgumentException) { continue; }

                foreach (var ch in channels)
                {
                    var key = $"{req.DeviceID}-{req.SecondaryBoardNumber}-{ch}";
                    if (cm._devices.TryGetValue(key, out var handler))
                        data.Add(handler.RealTime.RealTimeRecord);
                }
            }
            return data;
        }

        // ─────────────────────────────────────────────────────────────
        // REST ENDPOINTS
        // ─────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Get() => Ok("DeviceController is working!");

        [HttpGet("logs")]
        public IActionResult Logs() => Ok(InMemoryLogStore.Logs);

        [HttpPost("SendProgram")]
        public async Task<IActionResult> SendProgram([FromBody] List<CommonRequest> request)
        {
            var messages = await CoreSendProgram(_cm, _programServices, _batteryServices, _dbcService, request);
            return Ok(CommonResponse<List<string>>.Ok(messages, "Processed"));
        }

        [HttpPost("Start")]
        public async Task<IActionResult> Start([FromBody] List<CommonRequest> request)
        {
            var messages = await CoreStart(_cm, request);
            return Ok(CommonResponse<List<string>>.Ok(messages, "Processed"));
        }

        [HttpPost("Stop")]
        public async Task<IActionResult> Stop([FromBody] List<CommonRequest> request)
        {
            var messages = await CoreStop(_cm, request);
            return Ok(CommonResponse<List<string>>.Ok(messages, "Processed"));
        }

        [HttpPost("Pause")]
        public async Task<IActionResult> Pause([FromBody] List<CommonRequest> request)
        {
            var messages = await CorePause(_cm, request);
            return Ok(CommonResponse<List<string>>.Ok(messages, "Processed"));
        }

        [HttpPost("Continue")]
        public async Task<IActionResult> Continue([FromBody] List<CommonRequest> request)
        {
            var messages = await CoreContinue(_cm, request);
            return Ok(CommonResponse<List<string>>.Ok(messages, "Processed"));
        }

        [HttpGet("GetSessions")]
        public async Task<IActionResult> GetSessionId([FromBody] List<CommonRequest> request)
        {
            var sessions = new List<object>();
            foreach (var req in request)
            {
                List<int> channels;
                try { channels = ChannelExpander.GetChannels(req); }
                catch (ArgumentException) { continue; }

                foreach (var ch in channels)
                {
                    var key = $"{req.DeviceID}-{req.SecondaryBoardNumber}-{ch}";
                    if (_cm._devices.TryGetValue(key, out var handler))
                    {
                        sessions.Add(new
                        {
                            handler.Session.SessionID,
                            handler.Session.SessionName,
                            handler.Session.StartTime,
                            handler.Session.EndTime,
                            handler.Session.DeviceID,
                            handler.Session.SecondaryBoardNumber,
                            handler.Session.ChannelNumber,
                            handler.Session.BatteryID,
                            handler.Session.BatteryName,
                            handler.Session.ProgramID,
                            handler.Session.ProgramName,
                            handler.Session.DbcFileRecordID,
                            handler.Session.DbcName,
                            handler.Session.Storerecordcount,
                            handler.Session.Unstorerecordcount
                        });
                    }
                }
            }
            return Ok(sessions);
        }

        [HttpPost("GetDevices")]
        public async Task<IActionResult> GetDevices([FromBody] List<CommonRequest> request)
        {
            var status = new List<RealTimeRecordDto>();
            foreach (var req in request)
            {
                List<int> channels;
                try { channels = ChannelExpander.GetChannels(req); }
                catch (ArgumentException) { continue; }

                foreach (var ch in channels)
                {
                    var key = $"{req.DeviceID}-{req.SecondaryBoardNumber}-{ch}";
                    if (_cm._devices.TryGetValue(key, out var handler))
                        status.Add(handler.RealTime.RealTimeRecord);
                }
            }
            return Ok(status);
        }

        [HttpPost("GetPrograms")]
        public async Task<IActionResult> GetPrograms()
        {
            var result = await _programServices.GetProgramsAsync();
            if (!result.Success) return Ok(result);

            return Ok(CommonResponse<object>.Ok(result.Data.Select(p => new
            {
                p.ProgramId,
                p.ProgramName,
                p.Description,
                p.CreatedBy,
                p.CreatedAt,
                p.UpdatedBy,
                p.UpdatedAt
            }), "success!"));
        }

        [HttpPost("GetBatteries")]
        public async Task<IActionResult> GetBatteries()
        {
            var result = await _batteryServices.GetBatteries();
            return Ok(result);
        }

        [HttpPost("GetDbcFiles")]
        public async Task<IActionResult> GetDbcFiles()
        {
            var result = await _dbcService.GetAllAsync();
            return Ok(result);
        }

        [HttpPost("GetLiveData")]
        public async Task<IActionResult> GetLiveData([FromBody] List<CommonRequest> request)
        {
            var data = await CoreLiveData(_cm, request);

            if (data.Count == 0)
                return Ok(CommonResponse<List<RealTimeRecordDto>>.Fail("No live data found for the provided device and circuit IDs."));

            return Ok(CommonResponse<List<RealTimeRecordDto>>.Ok(data));
        }

        [HttpGet("GetLiveSSE")]
        public async Task SubscribeLive([FromQuery] CommonRequest request)
        {
            int channel = request.ChannelList.Count > 0 ? request.ChannelList[0] : 0;
            var key = $"{request.DeviceID}-{request.SecondaryBoardNumber}-{channel}";
            _cm._devices.TryGetValue(key, out IChannelCommandHandler? handler);

            if (handler == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                await Response.WriteAsync(
                    JsonConvert.SerializeObject(CommonResponse<object>.Fail("Channel not found!")),
                    HttpContext.RequestAborted);
                return;
            }

            Response.ContentType = "text/event-stream";

            async void Handler()
            {
                try
                {
                    var json = JsonConvert.SerializeObject(CommonResponse<object>.Ok(handler.RealTime.RealTimeRecord));
                    await Response.WriteAsync($"{json}\n\n", HttpContext.RequestAborted);
                    await Response.Body.FlushAsync(HttpContext.RequestAborted);
                }
                catch { }
            }

            handler.RealTime.OnDataChanged += Handler;

            var initialJson = JsonConvert.SerializeObject(CommonResponse<object>.Ok(handler.RealTime.RealTimeRecord, "success!"));
            await Response.WriteAsync(initialJson, HttpContext.RequestAborted);
            await Response.Body.FlushAsync(HttpContext.RequestAborted);

            try
            {
                while (!HttpContext.RequestAborted.IsCancellationRequested)
                    await Task.Delay(1000, HttpContext.RequestAborted);
            }
            catch (TaskCanceledException) { }
            finally
            {
                handler.RealTime.OnDataChanged -= Handler;
            }
        }
    }
}
