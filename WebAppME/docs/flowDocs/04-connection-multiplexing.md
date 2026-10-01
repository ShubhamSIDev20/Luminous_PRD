# 4. Connection Multiplexing (Sockets, Boards and Channels)

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

A device never opens one socket per channel. Everything after registration rides a
shared socket, which forces a multiplexing design.

**How many sockets a device opens is the hardware's choice, not the server's.** A single
physical LAN device may put every secondary board on one TCP client, or give each board
its own. Both are legal, they can differ per deployment, and the server discovers which
it is at runtime — from the socket each board's registration packet actually arrives on.
Nothing in the server assumes either shape.

That is why connection state is split across **two** layers:

```
  Hardware may present EITHER shape - both are handled:

  SHAPE A: boards share one socket        SHAPE B: a socket per board
  ------------------------------          ---------------------------
  DEVICE 1                                DEVICE 1
    board 1 --+                             board 1 --> TcpClient #1
    board 2 --+--> ONE TcpClient            board 2 --> TcpClient #2

        |                                        |
        v                                        v
  +------------------+                    +------------------+ +------------------+
  | DeviceLink       |                    | DeviceLink       | | DeviceLink       |
  | (1 per SOCKET)   |                    | (socket #1)      | | (socket #2)      |
  |                  |                    +------------------+ +------------------+
  | TcpClient        |                            ^                    ^
  | SemaphoreSlim    | <-- THE write lock         |                    |
  +------------------+                            |                    |
        ^        ^                                |                    |
        |        |                                |                    |
  +-----+--+  +--+-----+                    +-----+--+           +-----+--+
  | board1 |  | board2 |                    | board1 |           | board2 |
  +--------+  +--------+                    +--------+           +--------+
   DeviceConnection, 1 per (DeviceID, SecondaryBoardNumber)
```

```
  +---------------------------------------------------------------+
  |  DeviceLink            (1 instance per PHYSICAL SOCKET)        |
  |                                                                |
  |  TcpClient           : the socket itself                       |
  |  SemaphoreSlim       : serializes every write to that socket    |
  +---------------------------------------------------------------+

  +---------------------------------------------------------------+
  |  DeviceConnection      (1 instance per DeviceID + BoardNumber) |
  |                                                                |
  |  Link                : the DeviceLink it currently rides on    |
  |  TcpClient           : read-only pass-through to Link.TcpClient|
  |  ChannelSlotKeys     : "1-1-1", "1-1-2", ... (this board only) |
  |  _pendingResponses   : ConcurrentDictionary                    |
  |                        key = (addressByte, queryId)            |
  |                        val = TaskCompletionSource<byte[]>      |
  +-------+-------------------------------------------+-----------+
          |                                           |
     WRITE side                                  READ side
          |                                           |
          v                                           v
  SendAndWaitAsync(addr, queryId, cmd, 15s)   HandleCommandClientAsync
          |                                    (ONE read loop per socket)
          | 1. register TCS under (addr,queryId)      |
          | 2. Link.SendAsync(command):               | stream.ReadAsync(buf[1024])
          |      await the SOCKET's semaphore         |
          |      WriteAsync / Flush                   | buf[0]==0xDD && buf[1]==0x01 ?
          |      release                              |    yes -> registration path;
          | 3. await TCS with 15 s timeout            |           record this board in
          |                                           |           boardsOnThisSocket
          |                                           |    no  -> decode board from
          |                                           |           buf[2] high nibble,
          |                                           |           hand to THAT board's
          |                                           |           HandleIncomingPacket
          |                                           |               |
          <-------------------------------------------+---------------+
                       TCS.TrySetResult(payload)
```

### Why the write lock lives on the socket, not the board

```
  +---------------------------------------------------------------+
  | INVARIANT 0 -- one write lock per SOCKET                       |
  |                                                                |
  | If each board owned its own lock, two boards sharing one socket |
  | (Shape A) could pass their locks simultaneously and write to    |
  | the same NetworkStream at once, interleaving their bytes        |
  | mid-frame and corrupting BOTH packets.                          |
  |                                                                |
  | Putting the lock on DeviceLink means boards that share a socket |
  | queue behind each other, while boards on separate sockets stay  |
  | fully independent. Never move this lock onto DeviceConnection.  |
  +---------------------------------------------------------------+
```

### Two more invariants that were learned the hard way

```
  +---------------------------------------------------------------+
  | INVARIANT 1 -- read buffer must be 1024 bytes                  |
  |                                                                |
  | The largest response (PreviousCalibration) is 163 bytes, not   |
  | the 33-byte registration packet. A short buffer truncates a    |
  | frame mid-packet; the leftover bytes are then read as the      |
  | START of the next iteration, which corrupts buffer[2] -- the   |
  | address byte every subsequent response is routed on.           |
  | Symptom: responses silently delivered to the WRONG channel.    |
  +---------------------------------------------------------------+

  +---------------------------------------------------------------+
  | INVARIANT 2 -- correlate on (addressByte, queryId), not addr   |
  |                                                                |
  | Two different query types in flight for the same channel would |
  | otherwise collide on one TCS slot.                             |
  +---------------------------------------------------------------+
```

### Routing a response to the right board

One read loop serves one socket, and that socket may carry several boards. The loop keeps
a `board -> DeviceConnection` map of every board that registered on it, and routes each
non-registration packet by decoding the board from the **high nibble of the address byte**
(`ChannelAddressCodec.Decode(buffer[2])`, see [A.5](A-appendix-reference-tables.md)).

`Decode` does not validate, so a malformed address byte can name a board that never
registered; when the decoded board is unknown and the socket carries exactly one board,
the packet falls back to that board rather than being dropped.

### Losing a socket

A socket close takes down **every board riding it** — one read loop exit tears down all of
them. Each board is still checked individually against the closing socket first
(`DeviceConnection.IsOn(client)`), so a board that has already reattached to a *newer*
socket is not wrongly marked offline by the old socket's teardown. See
[Chapter 11](11-error-disconnect-reconnect.md).

Comms-loss alarms are raised **per `(device, board)`** (`{device}/{board}/comms-loss`), not
per device: boards can now drop and recover independently, and board 1 reconnecting must not
clear a still-dead board 3's fault. Once no board is left on a link, the link is dropped so a
reconnecting socket never inherits a stale write lock.

**Files involved:** `Services/DeviceConnection.cs` (both `DeviceLink` and `DeviceConnection`),
`Services/ChannelManager.cs` (`GetOrCreateDeviceConnection`, `GetOrCreateDeviceLink`,
`HandleCommandClientAsync`),
`Services/Implementations/ChannelCommandHandler.cs:300-338`

---


---

[⬅ Previous](03-user-allow-gate.md) · [⬅ Index](README.md) · [Next ➡](05-transfer-flow.md)
