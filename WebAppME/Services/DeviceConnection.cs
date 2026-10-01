using System.Collections.Concurrent;
using System.Net.Sockets;

namespace BatteryTestingSystem.Services
{
    /// <summary>
    /// One physical TCP socket, and the write serialization that guards it.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from <see cref="DeviceConnection"/>: the hardware team may run
    /// every secondary board of a device over its own socket, or multiplex several boards
    /// over a single socket, and that choice is theirs to make per deployment. The server
    /// discovers which it is at runtime (from the socket a board's registration packet
    /// actually arrives on) rather than assuming either shape.
    ///
    /// The write lock has to live HERE, on the socket, not on the per-board connection.
    /// If two boards share a socket and each held its own lock, both could write to the
    /// same stream at once and interleave their bytes mid-frame, corrupting the packet for
    /// both. One lock per socket means boards sharing a socket correctly queue behind each
    /// other, while boards on separate sockets stay fully independent.
    /// </remarks>
    public class DeviceLink
    {
        public TcpClient TcpClient { get; }

        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public DeviceLink(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
        }

        public async Task SendAsync(byte[] payload, CancellationToken ct = default)
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
    }

    /// <summary>
    /// One logical connection for a single (device, secondary board) pair: the channels it
    /// owns, its in-flight request correlation, and the physical link it currently rides on.
    /// </summary>
    /// <remarks>
    /// Keyed per board rather than per device because a board can arrive on its own socket.
    /// Several DeviceConnection instances may share one <see cref="DeviceLink"/> when the
    /// hardware multiplexes boards over a single socket - that is the point of the split.
    /// </remarks>
    public class DeviceConnection
    {
        /// <summary>The physical socket this board is currently reachable on.</summary>
        public DeviceLink? Link { get; private set; }

        /// <summary>
        /// Pass-through kept so existing consumers (ChannelCommandHandler's IsConnected /
        /// _tcpClient, MainLayout's active-device badge) read the live socket without
        /// needing to know about the link layer.
        /// </summary>
        public TcpClient TcpClient => Link?.TcpClient!;

        public HashSet<string> ChannelSlotKeys { get; } = new();

        public event Action<byte, byte, byte[]>? OnUnsolicitedPacket;

        // Keyed by (addressByte, queryId) rather than addressByte alone: addressByte
        // (board+channel) is unique per channel on this board, but a single channel
        // can legitimately have more than one query type in flight-adding queryId
        // gives the full Board(implicit-per-connection)+Channel+Query correlation
        // instead of colliding every command for a channel onto one slot.
        private readonly ConcurrentDictionary<(byte AddressByte, byte QueryId), TaskCompletionSource<byte[]>> _pendingResponses = new();

        /// <summary>
        /// Points this board at the socket its latest registration arrived on. Safe to call
        /// with the same link repeatedly (re-registration on an unchanged socket).
        /// </summary>
        /// <remarks>
        /// A null link is ignored rather than clearing the current one: callers that have no
        /// socket (startup rehydration, the operator "Allow" gate) must not wipe out a live
        /// link that a real registration already attached.
        /// </remarks>
        public void AttachLink(DeviceLink? link)
        {
            if (link != null) Link = link;
        }

        public bool IsOn(TcpClient client) => Link is not null && ReferenceEquals(Link.TcpClient, client);

        public async Task SendAsync(byte addressByte, byte[] payload, CancellationToken ct = default)
        {
            var link = Link ?? throw new InvalidOperationException(
                $"No TCP link attached for address byte 0x{addressByte:X2}.");

            await link.SendAsync(payload, ct);
        }

        public async Task<byte[]> SendAndWaitAsync(byte addressByte, byte queryId, byte[] payload, TimeSpan timeout, CancellationToken ct = default)
        {
            var key = (addressByte, queryId);
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            // If a previous request for this exact channel+query is still sitting here
            // (e.g. its caller abandoned it without awaiting the timeout), evict and
            // fail it instead of throwing - this keeps the pending buffer from growing
            // unbounded when the same identifier gets reused before it's cleaned up.
            if (_pendingResponses.TryRemove(key, out var stale))
                stale.TrySetException(new OperationCanceledException(
                    $"Superseded by a newer request for address byte 0x{addressByte:X2}, query 0x{queryId:X2}."));

            _pendingResponses[key] = tcs;

            try
            {
                await SendAsync(addressByte, payload, ct);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(timeout);

                var timeoutTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);

                var completed = await Task.WhenAny(tcs.Task, timeoutTask);
                if (completed != tcs.Task)
                    throw new TimeoutException($"No response for address byte 0x{addressByte:X2}, query 0x{queryId:X2} within {timeout}.");

                return await tcs.Task;
            }
            finally
            {
                // Always remove on the way out - success, timeout, or exception - so a
                // completed/abandoned request never lingers in the pending buffer.
                _pendingResponses.TryRemove(key, out _);
            }
        }

        public void HandleIncomingPacket(byte addressByte, byte queryId, byte[] payload)
        {
            if (_pendingResponses.TryGetValue((addressByte, queryId), out var tcs))
            {
                tcs.TrySetResult(payload);
                return;
            }

            OnUnsolicitedPacket?.Invoke(addressByte, queryId, payload);
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
