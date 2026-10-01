# 15. Broadcast — Device Discovery & Network Config

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

Registration (Chapter 2) assumes the device already knows the server's address. Broadcast
is how a device that does *not* gets found and configured — a SADP-style discovery
protocol on its own ports, entirely separate from `ChannelManager`.

```
  OPERATOR: Settings -> Device Discovery -> [Scan]
        |
        v
  +--------------------------------------------------------------+
  | BroadcastUdpService.StartAsync()                              |
  |                                                               |
  |   _listener = new UdpClient(IPAddress.Any, 10003)             |
  |   _listener.EnableBroadcast = true                            |
  |   IsListening = true                                          |
  |   _receiveTask = ReceiveLoopAsync(cts.Token)                  |
  |                                                               |
  |   Idempotent -- guarded by _startLock + IsListening, so       |
  |   repeated calls are no-ops.                                  |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | SendBroadcastAsync(payload)                                   |
  |                                                               |
  |   Does NOT send one datagram to 255.255.255.255. Instead it   |
  |   enumerates every network interface that is:                 |
  |       OperationalStatus == Up                                 |
  |       && type != Loopback                                     |
  |       && Supports(IPv4)                                       |
  |   and sends from EACH interface's unicast address.            |
  |                                                               |
  |   Why: on a multi-homed machine (Wi-Fi + Ethernet + VPN +     |
  |   Hyper-V switch -- common on an engineering laptop) a single |
  |   broadcast leaves via whichever interface the OS routing     |
  |   table picks, which is frequently NOT the one the test rig   |
  |   is on. Per-interface send makes discovery deterministic.    |
  +--------------------------------+------------------------------+
                                   |
        +--------------------------+--------------------------+
        |                          |                          |
        v                          v                          v
  Q3 DeviceDiscovery       Q4 ChangeNetworkConfig      Q5 ChangeServerConfig
  [0xDD, 0x03] + CRC16     set device IP / mask / gw   point the device at
  "who is out there?"                                  a different server
        |
        v
  +--------------------------------------------------------------+
  | DEVICES REPLY                                                 |
  |        |                                                      |
  |        v                                                      |
  | ReceiveLoopAsync                                              |
  |        |                                                      |
  |        v                                                      |
  | RawPacketReceived?.Invoke(bytes, remoteEndPoint)   (event)    |
  |        |                                                      |
  |        v                                                      |
  | DecoderService.DecodeDeviceDiscovery(payload)                 |
  |        -> CommonResponse<BroadcastDeviceInfo>                 |
  |           (device name, IP, MAC, server config)               |
  |        |                                                      |
  |        v                                                      |
  | UI list of discovered devices                                 |
  +--------------------------------------------------------------+
                                   |
                                   v
                  operator sets IP / server address (Q4 / Q5)
                                   |
                                   v
                  device reboots and dials TCP 9999
                                   |
                                   v
                          ==> Chapter 2 (registration)
```

> **⚠ The implemented broadcast ports do not match `docs/PROTOCOL.md`.**
> `BroadcastUdpService` uses **send `10002`, listen `10003`** (`SendPort = 10002`,
> `ListenPort = 10003`, with the class doc-comment stating
> "Server → clients : 255.255.255.255:10002 / Clients → server : 255.255.255.255:10003").
> `docs/PROTOCOL.md` §1.2 documents **send `10003`, receive `10004`**. Both cannot be
> right. Trust the code for what the app actually does today, and reconcile the spec
> against firmware before relying on either.
> Source: `Services/BROADCAST/BroadcastUdpService.cs:8-22` vs `docs/PROTOCOL.md` §1.2, §8.2.

**Files involved:** `Services/BROADCAST/BroadcastUdpService.cs`,
`Services/BROADCAST/BroadCastModels.cs`, `Services/DecoderService.cs:2204-2330`,
`docs/PROTOCOL.md` §8.2–8.4

---


---

[⬅ Previous](14-calibration.md) · [⬅ Index](README.md) · [Next ➡](16-scheduled-execution.md)
