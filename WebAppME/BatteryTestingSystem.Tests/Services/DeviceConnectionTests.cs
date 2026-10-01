using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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

    /// <summary>One board on its own socket - the common single-board case.</summary>
    private static DeviceConnection ConnectionOn(TcpClient client)
    {
        var conn = new DeviceConnection();
        conn.AttachLink(new DeviceLink(client));
        return conn;
    }

    [Fact]
    public async Task SendAsync_WritesBytesToSocket()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = ConnectionOn(clientSide);

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

        var conn = ConnectionOn(clientSide);

        var sendTask = conn.SendAndWaitAsync(0x22, 0x01, Encoding.ASCII.GetBytes("ping"), TimeSpan.FromSeconds(5));

        // Simulate the device replying — DeviceConnection doesn't read on its own in
        // this test (that's the read-loop's job, built in Task 2), so we drive
        // HandleIncomingPacket directly here to isolate correlation logic.
        var responseBytes = Encoding.ASCII.GetBytes("pong");
        conn.HandleIncomingPacket(0x22, 0x01, responseBytes);

        var result = await sendTask;
        Assert.Equal("pong", Encoding.ASCII.GetString(result));
    }

    [Fact]
    public async Task HandleIncomingPacket_WithNoPendingWait_RaisesOnUnsolicitedPacket()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = ConnectionOn(clientSide);

        byte? gotAddress = null;
        byte? gotQueryId = null;
        byte[]? gotPayload = null;
        conn.OnUnsolicitedPacket += (addr, queryId, payload) => { gotAddress = addr; gotQueryId = queryId; gotPayload = payload; };

        conn.HandleIncomingPacket(0x33, 0x02, new byte[] { 1, 2, 3 });

        Assert.Equal((byte)0x33, gotAddress);
        Assert.Equal((byte)0x02, gotQueryId);
        Assert.Equal(new byte[] { 1, 2, 3 }, gotPayload);
    }

    [Fact]
    public async Task SendAndWaitAsync_TimesOutIfNoResponseArrives()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = ConnectionOn(clientSide);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            conn.SendAndWaitAsync(0x44, 0x01, new byte[] { 9 }, TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public async Task FailAllPending_FailsOutstandingWaitsImmediately()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = ConnectionOn(clientSide);

        var sendTask = conn.SendAndWaitAsync(0x55, 0x01, new byte[] { 1 }, TimeSpan.FromSeconds(30));

        conn.FailAllPending(new IOException("connection closed"));

        var ex = await Assert.ThrowsAsync<IOException>(() => sendTask);
        Assert.Equal("connection closed", ex.Message);
    }

    [Fact]
    public async Task TwoConcurrentSendAsync_DoNotInterleaveBytes()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var conn = ConnectionOn(clientSide);

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

    /// <summary>
    /// The reason DeviceLink exists. When the hardware multiplexes two secondary boards
    /// over ONE socket, each board gets its own DeviceConnection but they must share the
    /// link - so their writes serialize. If each board held its own write lock, two boards
    /// sending at the same moment could interleave bytes mid-frame and corrupt both packets.
    /// </summary>
    [Fact]
    public async Task TwoBoardsSharingOneLink_DoNotInterleaveBytes()
    {
        var (listener, serverSide, clientSide) = await CreateLoopbackPairAsync();
        using var _l = listener; using var _s = serverSide; using var _c = clientSide;

        var sharedLink = new DeviceLink(clientSide);

        var board1 = new DeviceConnection();
        var board2 = new DeviceConnection();
        board1.AttachLink(sharedLink);
        board2.AttachLink(sharedLink);

        var payload1 = Enumerable.Repeat((byte)0xA1, 500).ToArray();
        var payload2 = Enumerable.Repeat((byte)0xB2, 500).ToArray();

        var readTask = Task.Run(async () =>
        {
            var buffer = new byte[2000];
            int total = 0;
            while (total < 1000)
                total += await serverSide.GetStream().ReadAsync(buffer, total, buffer.Length - total);
            return buffer.Take(1000).ToArray();
        });

        await Task.WhenAll(
            board1.SendAsync(0x11, payload1),
            board2.SendAsync(0x21, payload2));

        var received = await readTask;

        var firstBlock = received.Take(500).ToArray();
        var secondBlock = received.Skip(500).Take(500).ToArray();
        Assert.True(firstBlock.All(b => b == 0xA1) || firstBlock.All(b => b == 0xB2));
        Assert.True(secondBlock.All(b => b == 0xA1) || secondBlock.All(b => b == 0xB2));
    }

    /// <summary>
    /// Two boards on SEPARATE sockets stay fully independent: a response arriving for one
    /// board must not resolve the other board's identically-addressed pending request.
    /// </summary>
    [Fact]
    public async Task BoardsOnSeparateLinks_DoNotSharePendingResponses()
    {
        var (listenerA, serverA, clientA) = await CreateLoopbackPairAsync();
        var (listenerB, serverB, clientB) = await CreateLoopbackPairAsync();
        using var _la = listenerA; using var _sa = serverA; using var _ca = clientA;
        using var _lb = listenerB; using var _sb = serverB; using var _cb = clientB;

        var board1 = ConnectionOn(clientA);
        var board2 = ConnectionOn(clientB);

        var board1Wait = board1.SendAndWaitAsync(0x11, 0x01, new byte[] { 1 }, TimeSpan.FromMilliseconds(300));
        var board2Wait = board2.SendAndWaitAsync(0x11, 0x01, new byte[] { 1 }, TimeSpan.FromSeconds(5));

        // Answer only board 2. Board 1's wait must still time out - it is a different link.
        board2.HandleIncomingPacket(0x11, 0x01, Encoding.ASCII.GetBytes("for-board-2"));

        Assert.Equal("for-board-2", Encoding.ASCII.GetString(await board2Wait));
        await Assert.ThrowsAsync<TimeoutException>(() => board1Wait);
    }

    /// <summary>
    /// IsOn is what the disconnect path uses to decide whether a closing socket still owns
    /// a board, so that a board already reattached to a NEW socket isn't wrongly marked
    /// offline by the old socket's teardown.
    /// </summary>
    [Fact]
    public async Task IsOn_TracksTheCurrentlyAttachedSocketOnly()
    {
        var (listenerA, serverA, clientA) = await CreateLoopbackPairAsync();
        var (listenerB, serverB, clientB) = await CreateLoopbackPairAsync();
        using var _la = listenerA; using var _sa = serverA; using var _ca = clientA;
        using var _lb = listenerB; using var _sb = serverB; using var _cb = clientB;

        var board = ConnectionOn(clientA);
        Assert.True(board.IsOn(clientA));
        Assert.False(board.IsOn(clientB));

        // Reconnect onto a new socket - the old one must no longer claim this board.
        board.AttachLink(new DeviceLink(clientB));
        Assert.False(board.IsOn(clientA));
        Assert.True(board.IsOn(clientB));
    }
}
