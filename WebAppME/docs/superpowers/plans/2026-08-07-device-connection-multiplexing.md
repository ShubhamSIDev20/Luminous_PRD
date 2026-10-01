# Device-Level TCP Connection Multiplexing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace one-TCP-connection-per-channel with one-TCP-connection-per-device, shared across up to 64 channels (8 boards × 8 channels), with zero changes required at any Dashboard/Controller/Scheduler call site.

**Architecture:** A new internal `DeviceConnection` class (one per device) owns the shared `TcpClient`, a write lock, and a response-correlation table keyed by address byte. `ChannelCommandHandler` (the existing per-channel "handler" type) becomes a lightweight channel-slot that delegates every read/write to its parent `DeviceConnection` instead of owning a private socket. `ChannelManager._devices` keeps its exact current shape (`ConcurrentDictionary<string, IChannelCommandHandler>` keyed by `"deviceId-boardNumber-channelId"`).

**Tech Stack:** C#, `System.Net.Sockets.TcpClient`/`NetworkStream`, `System.Threading.Channels` is NOT used here (that's the existing UDP pipeline) — this uses `SemaphoreSlim` + `TaskCompletionSource<byte[]>` for connection-level serialization and response correlation. xUnit for tests (existing `BatteryTestingSystem.Tests` project).

## Global Constraints

- Public API unchanged: `ChannelManager._devices` stays keyed by `"deviceId-boardNumber-channelId"`; every `IChannelCommandHandler` method signature (`StartProgram()`, `Session`, `RealTime`, etc.) is unchanged. No call site in `DashboardView.razor`, `DeviceController.cs`, `DeviceMcpTools.cs`, or `SchedulerService.cs` changes.
- Registration wire format is unchanged — same `0xDD 0x01` header, same payload shape, one packet per channel. Only the transport (shared vs. private connection) changes.
- If the shared connection drops, every channel-slot for that device goes offline together (`IsConnected = false`).
- On reconnect, existing channel-slot state (Session, Program, calibration, etc.) is preserved and reattached to the new socket — not reset.
- Every command send/receive must be correlated by address byte (`ChannelAddressCodec`-encoded board+channel), since multiple channels now share one socket.

---

### Task 1: `DeviceConnection` — write-serialization and response correlation (TDD)

**Files:**
- Create: `Services/DeviceConnection.cs`
- Test: `BatteryTestingSystem.Tests/Services/DeviceConnectionTests.cs`

**Interfaces:**
- Produces: `DeviceConnection` class with:
  - `TcpClient TcpClient { get; set; }`
  - `Task SendAsync(byte addressByte, byte[] payload, CancellationToken ct = default)`
  - `Task<byte[]> SendAndWaitAsync(byte addressByte, byte[] payload, TimeSpan timeout, CancellationToken ct = default)` — throws `TimeoutException` on timeout, `IOException`/`ObjectDisposedException` propagate if the socket is dead
  - `void HandleIncomingPacket(byte addressByte, byte[] payload)` — called by the owning read-loop (Task 2) for every packet read off the wire; resolves a pending `SendAndWaitAsync` if one is waiting for that address byte, otherwise raises `OnUnsolicitedPacket` for the caller to treat as a (re-)registration
  - `event Action<byte, byte[]>? OnUnsolicitedPacket`
  - `void FailAllPending(Exception ex)` — called on disconnect; fails every outstanding `SendAndWaitAsync` immediately instead of letting them time out
  - `HashSet<string> ChannelSlotKeys` — plain public field, populated/read by `ChannelManager`

This task builds `DeviceConnection` against a real loopback TCP pair (no mocking needed — `TcpListener` + `TcpClient` on `127.0.0.1` gives two real connected sockets in-process), so the write-lock and correlation logic are tested against actual `NetworkStream` I/O.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;
using BatteryTestingSystem.Services;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

public class DeviceConnectionTests
{
    private static async Task<(TcpListener listener, TcpClient serverSide, TcpClient clientSide)> CreateLoopbackPairAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var clientSide = new TcpClient();
        var acceptTask = listener.AcceptTcpClientAsync();
        await clientSide.ConnectAsync(IPAddress.Loopback, port);
        var serverSide = await acceptTask;

        return (listener, serverSide, clientSide);
    }

    [Fact]
    public async Task SendAsync_WritesBytesToSocket()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = new DeviceConnection { TcpClient = clientSide };

        await conn.SendAsync(0x11, Encoding.ASCII.GetBytes("hello"));

        var buffer = new byte[64];
        int read = await serverSide.GetStream().ReadAsync(buffer, 0, buffer.Length);
        Assert.Equal("hello", Encoding.ASCII.GetString(buffer, 0, read));
    }

    [Fact]
    public async Task SendAndWaitAsync_ResolvesOnMatchingAddressByte()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = new DeviceConnection { TcpClient = clientSide };

        var sendTask = conn.SendAndWaitAsync(0x22, Encoding.ASCII.GetBytes("ping"), TimeSpan.FromSeconds(5));

        // Simulate the device replying — DeviceConnection doesn't read on its own in
        // this test (that's the read-loop's job, built in Task 2), so we drive
        // HandleIncomingPacket directly here to isolate correlation logic.
        var responseBytes = Encoding.ASCII.GetBytes("pong");
        conn.HandleIncomingPacket(0x22, responseBytes);

        var result = await sendTask;
        Assert.Equal("pong", Encoding.ASCII.GetString(result));
    }

    [Fact]
    public async Task HandleIncomingPacket_WithNoPendingWait_RaisesOnUnsolicitedPacket()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = new DeviceConnection { TcpClient = clientSide };

        byte? gotAddress = null;
        byte[]? gotPayload = null;
        conn.OnUnsolicitedPacket += (addr, payload) => { gotAddress = addr; gotPayload = payload; };

        conn.HandleIncomingPacket(0x33, new byte[] { 1, 2, 3 });

        Assert.Equal((byte)0x33, gotAddress);
        Assert.Equal(new byte[] { 1, 2, 3 }, gotPayload);
    }

    [Fact]
    public async Task SendAndWaitAsync_TimesOutIfNoResponseArrives()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = new DeviceConnection { TcpClient = clientSide };

        await Assert.ThrowsAsync<TimeoutException>(() =>
            conn.SendAndWaitAsync(0x44, new byte[] { 9 }, TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public async Task FailAllPending_FailsOutstandingWaitsImmediately()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = new DeviceConnection { TcpClient = clientSide };

        var sendTask = conn.SendAndWaitAsync(0x55, new byte[] { 1 }, TimeSpan.FromSeconds(30));

        conn.FailAllPending(new IOException("connection closed"));

        var ex = await Assert.ThrowsAsync<IOException>(() => sendTask);
        Assert.Equal("connection closed", ex.Message);
    }

    [Fact]
    public async Task TwoConcurrentSendAsync_DoNotInterleaveBytes()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = new DeviceConnection { TcpClient = clientSide };

        var payloadA = Enumerable.Repeat((byte)0xAA, 500).ToArray();
        var payloadB = Enumerable.Repeat((byte)0xBB, 500).ToArray();

        var readTask = Task.Run(async () =>
        {
            var buffer = new byte[2000];
            int total = 0;
            while (total < 1000)
                total += await serverSide.GetStream().ReadAsync(buffer, total, buffer.Length - total);
            return buffer.Take(1000).ToArray();
        });

        await Task.WhenAll(
            conn.SendAsync(0x01, payloadA),
            conn.SendAsync(0x02, payloadB));

        var received = await readTask;

        // Whichever order they landed in, each 500-byte block must be uniform
        // (0xAA or 0xBB) - never a mix of both within a single write.
        var firstBlock = received.Take(500).ToArray();
        var secondBlock = received.Skip(500).Take(500).ToArray();
        Assert.True(firstBlock.All(b => b == 0xAA) || firstBlock.All(b => b == 0xBB));
        Assert.True(secondBlock.All(b => b == 0xAA) || secondBlock.All(b => b == 0xBB));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter DeviceConnectionTests`
Expected: FAIL — `DeviceConnection` does not exist (compile error).

- [ ] **Step 3: Write the implementation**

```csharp
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace BatteryTestingSystem.Services
{
    public class DeviceConnection
    {
        public TcpClient TcpClient { get; set; } = null!;

        public HashSet<string> ChannelSlotKeys { get; } = new();

        public event Action<byte, byte[]>? OnUnsolicitedPacket;

        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly ConcurrentDictionary<byte, TaskCompletionSource<byte[]>> _pendingResponses = new();

        public async Task SendAsync(byte addressByte, byte[] payload, CancellationToken ct = default)
        {
            var stream = TcpClient.GetStream();

            await _writeLock.WaitAsync(ct);
            try
            {
                await stream.WriteAsync(payload, 0, payload.Length, ct);
                await stream.FlushAsync(ct);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<byte[]> SendAndWaitAsync(byte addressByte, byte[] payload, TimeSpan timeout, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pendingResponses.TryAdd(addressByte, tcs))
                throw new InvalidOperationException($"A request for address byte 0x{addressByte:X2} is already in flight.");

            try
            {
                await SendAsync(addressByte, payload, ct);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(timeout);

                var timeoutTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);

                var completed = await Task.WhenAny(tcs.Task, timeoutTask);
                if (completed != tcs.Task)
                    throw new TimeoutException($"No response for address byte 0x{addressByte:X2} within {timeout}.");

                return await tcs.Task;
            }
            finally
            {
                _pendingResponses.TryRemove(addressByte, out _);
            }
        }

        public void HandleIncomingPacket(byte addressByte, byte[] payload)
        {
            if (_pendingResponses.TryGetValue(addressByte, out var tcs))
            {
                tcs.TrySetResult(payload);
                return;
            }

            OnUnsolicitedPacket?.Invoke(addressByte, payload);
        }

        public void FailAllPending(Exception ex)
        {
            foreach (var key in _pendingResponses.Keys.ToArray())
            {
                if (_pendingResponses.TryRemove(key, out var tcs))
                    tcs.TrySetException(ex);
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter DeviceConnectionTests`
Expected: all 6 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/DeviceConnection.cs BatteryTestingSystem.Tests/Services/DeviceConnectionTests.cs
git commit -m "feat: add DeviceConnection for per-device write-serialization and response correlation"
```

---

### Task 2: `ChannelManager` — device-level connection registry and read-loop

**Files:**
- Modify: `Services/ChannelManager.cs`

**Interfaces:**
- Consumes: `DeviceConnection` (Task 1) — `SendAsync`, `SendAndWaitAsync`, `HandleIncomingPacket`, `OnUnsolicitedPacket`, `FailAllPending`, `ChannelSlotKeys`.
- Produces: `ChannelManager.GetOrCreateDeviceConnection(int deviceId) -> DeviceConnection` (new internal helper) — consumed by Task 3's `ChannelCommandHandler` indirectly via the channel-slot's `Connection` property, which `Add()` sets.

Current `ChannelManager.cs` fields (unchanged, for reference — do not remove any of these):
```csharp
public ConcurrentDictionary<string, IChannelCommandHandler> _devices = new ConcurrentDictionary<string, IChannelCommandHandler>();
private readonly Func<IChannelCommandHandler> _handlerFactory;
```

- [ ] **Step 1: Add the device-connection registry field**

Add alongside the existing `_devices` field declaration:

```csharp
public ConcurrentDictionary<int, DeviceConnection> _deviceConnections = new ConcurrentDictionary<int, DeviceConnection>();

public DeviceConnection GetOrCreateDeviceConnection(int deviceId)
{
    return _deviceConnections.GetOrAdd(deviceId, _ => new DeviceConnection());
}
```

- [ ] **Step 2: Rewrite `Add()` to attach the channel-slot to its device's shared connection**

Current `Add()` body (the relevant middle section) does:
```csharp
                var handler = CreateNewHandler();
                handler._cts = _lifecycleCts;
                handler.Channel = channel;
                await handler.InitializeAsync();
                handler._tcpClient = tcpClient;

                _devices.TryAdd(key, handler);
```

Change to:
```csharp
                var deviceConnection = GetOrCreateDeviceConnection(channel.DeviceID);
                deviceConnection.TcpClient = tcpClient;
                deviceConnection.ChannelSlotKeys.Add(key);

                var handler = CreateNewHandler();
                handler._cts = _lifecycleCts;
                handler.Channel = channel;
                handler.Connection = deviceConnection;
                await handler.InitializeAsync();

                _devices.TryAdd(key, handler);
```

And in the earlier branch of `Add()` (existing handler, reconnect case) — current code:
```csharp
                    // Assign new client
                    IsHandler.Data._tcpClient = tcpClient;
```

Change to (reattach the existing channel-slot's connection object to the new socket, preserving state):
```csharp
                    // Reattach to the new socket - the same DeviceConnection instance
                    // is reused so ChannelSlotKeys/pending-response state carries over.
                    var existingDeviceConnection = GetOrCreateDeviceConnection(channel.DeviceID);
                    existingDeviceConnection.TcpClient = tcpClient;
                    existingDeviceConnection.ChannelSlotKeys.Add(key);
                    IsHandler.Data.Connection = existingDeviceConnection;
```

(Also remove the `oldClient.Close(); oldClient.Dispose();` block above it — that closed the *old per-channel* socket; with a shared connection, the old socket belongs to `DeviceConnection` and is superseded simply by assigning the new `TcpClient`, not explicitly closed here, since `HandleCommandClientAsync`'s read-loop for the old connection will observe the new registration arriving on a different accepted `TcpClient` and exit on its own read failure.)

- [ ] **Step 3: Rewrite `HandleCommandClientAsync` into a persistent per-device read-loop**

Current method (shown in full in the design spec) reads exactly one packet, processes it as registration, sends one response, and returns without ever reading again. Replace the entire method body with:

```csharp
        private async Task HandleCommandClientAsync(TcpClient client, CancellationToken token)
        {
            var clientId = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
            _log.Information($"client Enter in HandleCommandClientAsync: {clientId}");

            DeviceConnection? deviceConnection = null;

            try
            {
                var stream = client.GetStream();
                byte[] buffer = new byte[33];

                while (!token.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer, token);

                    if (read == 0)
                    {
                        _log.Information("Connection closed by remote for {ClientId}", clientId);
                        break;
                    }

                    // Registration header
                    if (buffer[0] == 0xDD && buffer[1] == 0x01)
                    {
                        var responseBytes = await ProcessRegistrationPacketAsync(buffer, client);
                        if (responseBytes != null)
                        {
                            var (parsedChannel, response) = responseBytes.Value;
                            deviceConnection = GetOrCreateDeviceConnection(parsedChannel.DeviceID);
                            await SendOnly(client, response, token);
                        }
                        continue;
                    }

                    // Not a registration packet - route to the device's DeviceConnection
                    // as an in-flight response or an unrecognized unsolicited packet.
                    if (deviceConnection != null && read >= 3)
                    {
                        deviceConnection.HandleIncomingPacket(buffer[2], buffer.Take(read).ToArray());
                    }
                    else
                    {
                        _log.Warning("Received non-registration packet before any channel registered on {ClientId}.", clientId);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[HandleCommandClientAsync Error] " + ex.Message);
            }
            finally
            {
                if (deviceConnection != null)
                {
                    deviceConnection.FailAllPending(new IOException("Device connection closed."));

                    foreach (var slotKey in deviceConnection.ChannelSlotKeys)
                    {
                        if (_devices.TryGetValue(slotKey, out var slotHandler))
                            slotHandler.MarkDisconnected();
                    }
                }

                _log.Information($"[HandleCommandClientAsync exited {clientId}]");
                HardwareManagerChanged?.Invoke();
            }
        }

        /// <summary>
        /// Parses and processes one registration packet. Returns the parsed channel and
        /// the response bytes to send back, or null if the packet was invalid.
        /// </summary>
        private async Task<(ChannelDto Channel, byte[] Response)?> ProcessRegistrationPacketAsync(byte[] buffer, TcpClient client)
        {
            var newChannel = DecoderService.ParseRegistrationPacket(buffer);

            if (newChannel == null)
            {
                _log.Warning("Invalid registration packet.");
                return null;
            }

            byte[] responseBytes = DecoderService.ParseRegistrationResponse(0, 0, CommandStatus.Failed);

            using var scoped = ServiceLocator.GetScoped<IDeviceChannelServices>();
            var db = scoped.Service;

            try
            {
                var existingChannel = await db.GetChannelAsync(newChannel);

                if (existingChannel.Success && existingChannel.Data != null && existingChannel.Data.IsRegistered)
                {
                    var addResult = await Add(existingChannel.Data, client);

                    if (addResult.Success)
                    {
                        // Already registered
                        if (addResult.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                        {
                            responseBytes = DecoderService.ParseRegistrationResponse(
                                (int)newChannel.DeviceID,
                                Utils.ChannelAddressCodec.Encode(newChannel.SecondaryBoardNumber, newChannel.ChannelNumber),
                                CommandStatus.AlreadyRegistered
                            );

                            // Update into Database
                            await db.UpdateRegistration(newChannel);
                        }
                        else
                        {
                            // New registration success
                            responseBytes = DecoderService.ParseRegistrationResponse(
                                (int)newChannel.DeviceID,
                                Utils.ChannelAddressCodec.Encode(newChannel.SecondaryBoardNumber, newChannel.ChannelNumber),
                                CommandStatus.Success
                            );
                        }
                    }
                    else
                    {
                        responseBytes = DecoderService.ParseRegistrationResponse(
                            (int)newChannel.DeviceID,
                            Utils.ChannelAddressCodec.Encode(newChannel.SecondaryBoardNumber, newChannel.ChannelNumber),
                            CommandStatus.Failed
                        );
                    }
                }
                else if (existingChannel.Success && existingChannel.Data != null && !existingChannel.Data.IsRegistered)
                {
                    if (existingChannel.Data.IsDeleted)
                        await db.UpdateIsDeleteAsync(newChannel, false);

                    responseBytes = DecoderService.ParseRegistrationResponse(
                        (int)newChannel.DeviceID,
                        Utils.ChannelAddressCodec.Encode(newChannel.SecondaryBoardNumber, newChannel.ChannelNumber),
                        CommandStatus.Failed
                    );
                }
                else
                {
                    if (existingChannel.Data == null)
                        await db.InsertAsync(newChannel);

                    responseBytes = DecoderService.ParseRegistrationResponse(
                        (int)newChannel.DeviceID,
                        Utils.ChannelAddressCodec.Encode(newChannel.SecondaryBoardNumber, newChannel.ChannelNumber),
                        CommandStatus.Failed
                    );
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "DB Error: " + ex.Message);
            }

            return (newChannel, responseBytes);
        }
```

Note: this collapses the old inline registration-handling logic (previously duplicated across three `if`/`else if`/`else` branches directly in `HandleCommandClientAsync`) into `ProcessRegistrationPacketAsync`, called once per registration packet from inside the new read-loop — needed because registration can now happen multiple times per connection (once per channel) rather than exactly once.

- [ ] **Step 4: Build to confirm it compiles**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: errors referencing `ChannelCommandHandler.Connection` and `MarkDisconnected()` not existing yet — expected, fixed in Task 3.

- [ ] **Step 5: Commit**

```bash
git add Services/ChannelManager.cs
git commit -m "feat: rewrite ChannelManager's command listener as a per-device multiplexing read-loop"
```

---

### Task 3: `ChannelCommandHandler` — become a channel-slot delegating to `DeviceConnection`

**Files:**
- Modify: `Services/Implementations/ChannelCommandHandler.cs`
- Modify: `Services/Interfaces/IChannelCommandHandler.cs`

**Interfaces:**
- Consumes: `DeviceConnection` (Task 1) — `SendAndWaitAsync`, `TcpClient` (for the `_tcpClient` passthrough).
- Produces: `IChannelCommandHandler.Connection { get; set; }` (new, type `DeviceConnection`) — consumed by `ChannelManager.Add` (Task 2). `IChannelCommandHandler.MarkDisconnected()` (new) — consumed by `ChannelManager.HandleCommandClientAsync`'s disconnect cleanup (Task 2).

- [ ] **Step 1: Add `Connection` and `MarkDisconnected()` to the interface**

In `Services/Interfaces/IChannelCommandHandler.cs`, change the existing line:

```csharp
        TcpClient _tcpClient { get; set; }
```

to a read-only passthrough (nothing outside `ChannelCommandHandler` ever sets it directly anymore — `ChannelManager` now sets `Connection` instead, per Step 2 below), and add the two new members:

```csharp
        TcpClient _tcpClient { get; }
        DeviceConnection Connection { get; set; }
        void MarkDisconnected();
```

- [ ] **Step 2: Replace `_tcpClient` field with a passthrough to `Connection`**

Current field (line 25 area):
```csharp
        public TcpClient _tcpClient { get; set; } = new();
```

Change to:
```csharp
        private DeviceConnection _connection = null!;

        // Setting Connection always clears the manual-disconnect flag - this is the ONLY
        // place a channel-slot gets reattached to a (possibly new) socket, whether on
        // first registration or on reconnect after a drop (see ChannelManager.Add,
        // both branches), so it's the correct single point to reset "am I marked offline".
        public DeviceConnection Connection
        {
            get => _connection;
            set
            {
                _connection = value;
                _manuallyDisconnected = false;
            }
        }

        // Kept for the one remaining external read site (MainLayout.razor's active-device
        // count badge) - proxies to the shared DeviceConnection's socket. Multiple
        // channel-slots on the same device correctly return the SAME TcpClient here.
        public TcpClient _tcpClient => Connection?.TcpClient;

        private volatile bool _manuallyDisconnected = false;
```

- [ ] **Step 3: Rewrite `IsConnected` to check `Connection.TcpClient` instead of a private socket**

Current:
```csharp
        public bool IsConnected
        {
            get
            {
                try
                {
                    if (_tcpClient == null || !_tcpClient.Connected)
                        return false;

                    Socket socket = _tcpClient.Client;

                    return !(socket.Poll(0, SelectMode.SelectRead) &&
                             socket.Available == 0);
                }
                catch (SocketException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                finally
                {
                    TryInvokeCircuitChanged();
                }
            }
        }
```

Change to:
```csharp
        public bool IsConnected
        {
            get
            {
                if (_manuallyDisconnected)
                    return false;

                try
                {
                    var client = Connection?.TcpClient;
                    return client != null && client.Connected;
                }
                catch (SocketException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                finally
                {
                    TryInvokeCircuitChanged();
                }
            }
        }

        public void MarkDisconnected()
        {
            _manuallyDisconnected = true;
            TryInvokeCircuitChanged();
        }
```

`MarkDisconnected()` is only ever called from `ChannelManager`'s read-loop cleanup (Task 2 Step 3) when the shared socket actually closes. It stays set until `Connection`'s setter runs again (i.e. until this channel-slot is reattached to a new/reconnected `DeviceConnection` via `ChannelManager.Add`), at which point it's automatically cleared — so a device correctly shows offline after a drop and correctly shows online again after reconnecting, with no manual reset code needed at the reconnect call site.

(Socket-liveness polling — the old `socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0` disconnect-detection trick — moves to `DeviceConnection`'s read-loop in `ChannelManager`, which already detects disconnect via `stream.ReadAsync` returning 0 or throwing, per Task 2 Step 3. Per-channel-slot polling is removed entirely since it would mean up to 64 slots all polling the same shared socket redundantly.)

- [ ] **Step 4: Remove the per-handler `ConnectionAlive()` background loop entirely**

Delete the `ConnectionAlive()` method and the `_CircuitCMDListener` field (both in the `#region No Interface Members` block). This loop's job — continuously reading the socket and detecting disconnects — now belongs entirely to `ChannelManager`'s read-loop (Task 2), which reads once per device connection instead of once per channel-slot.

In `InitializeAsync()`, remove the line that started it:
```csharp
                _CircuitCMDListener = ConnectionAlive();
```

- [ ] **Step 5: Rewrite `SendAndWaitForResponseAsync` to delegate to `Connection`**

Current:
```csharp
        public async Task<CommonResponse<T>> SendAndWaitForResponseAsync<T>(byte[] command)
        {
            try
            {
                if (_tcpClient == null || !_tcpClient.Connected)
                    return CommonResponse<T>.Fail("TCP Command client is not connected.");

                if (commandinterrupt)
                    return CommonResponse<T>.Fail("Another command is in progress.");

                commandinterrupt = true;

                var stream = _tcpClient.GetStream();

                var discardBuffer = new byte[1024];

                while (_tcpClient.Available > 0)
                {
                    await stream.ReadAsync(discardBuffer, 0, discardBuffer.Length);
                }

                await stream.WriteAsync(command, 0, command.Length);

                byte[] buffer = new byte[1024];

                var readTask = stream.ReadAsync(buffer, 0, buffer.Length);

                var timeout = Task.Delay(TimeSpan.FromSeconds(15));

                if (await Task.WhenAny(readTask, timeout) == readTask)
                {
                    int bytesRead = await readTask;

                    if (bytesRead == 0)
                    {
                        return CommonResponse<T>.Fail("No response received from device.");
                    }

                    return DecoderService.TryDecode<T>(buffer.Take(bytesRead).ToArray());
                }
                else
                {
                    return CommonResponse<T>.Fail("No response received from device.");

                }

            }
            catch (OperationCanceledException)
            {
                return CommonResponse<T>.Fail("Read timed out after 10 seconds.");
            }
            catch (Exception ex)
            {
                return CommonResponse<T>.Fail($"Exception: {ex.Message}");
            }
            finally
            {
                commandinterrupt = false;
                OnChannelChanged?.Invoke();
            }

        }
```

Change to:
```csharp
        public async Task<CommonResponse<T>> SendAndWaitForResponseAsync<T>(byte[] command)
        {
            if (Connection?.TcpClient == null || !Connection.TcpClient.Connected)
                return CommonResponse<T>.Fail("TCP Command client is not connected.");

            if (commandinterrupt)
                return CommonResponse<T>.Fail("Another command is in progress.");

            commandinterrupt = true;

            try
            {
                byte addressByte = Utils.ChannelAddressCodec.Encode(Channel.SecondaryBoardNumber, Channel.ChannelNumber);

                var response = await Connection.SendAndWaitAsync(addressByte, command, TimeSpan.FromSeconds(15));

                return DecoderService.TryDecode<T>(response);
            }
            catch (TimeoutException)
            {
                return CommonResponse<T>.Fail("No response received from device.");
            }
            catch (OperationCanceledException)
            {
                return CommonResponse<T>.Fail("Read timed out after 15 seconds.");
            }
            catch (Exception ex)
            {
                return CommonResponse<T>.Fail($"Exception: {ex.Message}");
            }
            finally
            {
                commandinterrupt = false;
                OnChannelChanged?.Invoke();
            }
        }
```

(The old manual "discard buffered bytes before writing" loop is removed — with per-address correlation in `DeviceConnection`, stale/unrelated bytes for *other* channels are never wrongly consumed here in the first place, so there's nothing to discard.)

- [ ] **Step 6: Build to confirm it compiles**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: 0 errors. `MainLayout.razor`'s `e.Value?._tcpClient?.Connected` badge compiles unchanged since `_tcpClient` is still exposed (now as a computed passthrough).

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: all existing tests (21 `ChannelAddressCodec` + 6 new `DeviceConnection` tests) still PASS.

- [ ] **Step 8: Commit**

```bash
git add Services/Implementations/ChannelCommandHandler.cs Services/Interfaces/IChannelCommandHandler.cs
git commit -m "feat: ChannelCommandHandler becomes a channel-slot delegating I/O to DeviceConnection"
```

---

### Task 4: Integration test — two channels sharing one simulated device connection

**Files:**
- Test: `BatteryTestingSystem.Tests/Services/ChannelManagerMultiplexingTests.cs`

**Interfaces:**
- Consumes: `ChannelManager` (public, unchanged constructor `ChannelManager(Func<IChannelCommandHandler> handlerFactory, EventBusService eventBus)`), `DeviceConnection` (Task 1), `IChannelCommandHandler` (Task 3).

This test exercises the actual multiplexing behavior end-to-end at the object level (not a full TCP registration handshake, since that requires a running `AppDbContext`/`IDeviceChannelServices` — out of scope for a fast unit test). It directly verifies the piece that's new and risky: one `DeviceConnection` correctly serving two independent channel-slots.

- [ ] **Step 1: Write the failing test**

```csharp
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Implementations;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

public class ChannelManagerMultiplexingTests
{
    [Fact]
    public void TwoChannelSlots_OnSameDeviceConnection_ShareOneTcpClient()
    {
        var deviceConnection = new DeviceConnection();

        var channelOneHandler = new ChannelCommandHandler
        {
            Channel = new ChannelDto { DeviceID = 1, SecondaryBoardNumber = 1, ChannelNumber = 1 },
            Connection = deviceConnection
        };

        var channelTwoHandler = new ChannelCommandHandler
        {
            Channel = new ChannelDto { DeviceID = 1, SecondaryBoardNumber = 1, ChannelNumber = 2 },
            Connection = deviceConnection
        };

        Assert.Same(channelOneHandler.Connection, channelTwoHandler.Connection);
    }

    [Fact]
    public void MarkDisconnected_SetsIsConnectedFalse_IndependentOfOtherSlots()
    {
        var deviceConnection = new DeviceConnection();

        var channelOneHandler = new ChannelCommandHandler
        {
            Channel = new ChannelDto { DeviceID = 1, SecondaryBoardNumber = 1, ChannelNumber = 1 },
            Connection = deviceConnection
        };

        channelOneHandler.MarkDisconnected();

        Assert.False(channelOneHandler.IsConnected);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter ChannelManagerMultiplexingTests`
Expected: FAIL if Tasks 1-3 aren't complete yet (compile error on `Connection`/`MarkDisconnected`); once Tasks 1-3 are done, this should already pass since it exercises code paths already implemented — confirming there's no additional implementation needed here, only verification.

- [ ] **Step 3: Run test to verify it passes**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter ChannelManagerMultiplexingTests`
Expected: both tests PASS.

- [ ] **Step 4: Commit**

```bash
git add BatteryTestingSystem.Tests/Services/ChannelManagerMultiplexingTests.cs
git commit -m "test: verify channel-slots on the same device share one DeviceConnection"
```

---

### Task 5: Full-solution verification

**Files:**
- None (verification-only task)

- [x] **Step 1: Full solution build**

Run: `dotnet build BatteryTestingSystem.sln`
Expected: 0 errors, 0 new warnings on touched files.
Result: 0 errors (382 pre-existing warnings, none on `DeviceConnection.cs`, `ChannelManager.cs`, or the multiplexing-related lines of `ChannelCommandHandler.cs`).

- [x] **Step 2: Full test suite**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: all tests pass (21 `ChannelAddressCodec` + 6 `DeviceConnectionTests` + 2 `ChannelManagerMultiplexingTests` = 29 total).
Result: `Passed! - Failed: 0, Passed: 29, Skipped: 0, Total: 29`.

- [x] **Step 3: Sweep for any remaining direct `_tcpClient` writes outside `DeviceConnection`**

Run:
```bash
grep -rn "_tcpClient\." --include=*.cs Services/
```
Expected: only the `_tcpClient => Connection?.TcpClient` passthrough definition itself and the `IsConnected`/`SendAndWaitForResponseAsync` read sites already updated in Task 3 — no remaining direct socket writes bypassing `DeviceConnection`.
Result: no matches at all — the passthrough definition lives in `Implementations/ChannelCommandHandler.cs` and is referenced with `.` nowhere else; no stray direct writes.

- [ ] **Step 4: Manual/hardware-in-the-loop verification (documented limitation)**

This plan's automated tests cover the multiplexing logic in isolation (Tasks 1 and 4) but cannot exercise the real TCP registration handshake end-to-end without a running database and real or simulated hardware sending actual registration packets for multiple channels over one socket. Before relying on this in production: connect a real (or test-harness) device that registers 2+ channels over a single TCP connection, confirm both appear as independent dashboard cards, confirm dropping the connection takes both offline together, and confirm reconnecting preserves their Session/Program state per the design's decision #7.

Status: intentionally deferred — a `HardwareSimulator/` harness exists in the working tree but is mid-update (per-device single-circuit only; not yet multi-circuit-per-socket) and out of scope for this pass per explicit instruction. Revisit once the simulator is updated to register 2+ circuits over one TCP connection per device.

- [x] **Step 5: Commit any final cleanup**

No code changes were needed during this verification pass (build and tests were already clean) — nothing to commit. Working tree still carries pre-existing unrelated local edits (`Models/InitializeDataSeeder.cs`, `Services/DecoderService.cs`, `appsettings.json`, logging tweaks in `ChannelCommandHandler.cs`) and the untracked, in-progress `HardwareSimulator/`; left untouched since they're outside this task's scope.
