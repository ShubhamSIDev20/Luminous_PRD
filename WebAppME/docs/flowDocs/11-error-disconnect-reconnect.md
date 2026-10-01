# 11. Error, Disconnect & Reconnect Paths

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

### 11.1 Socket death and cleanup

```
  HandleCommandClientAsync  --  the finally block
        |
        v
  +--------------------------------------------------------------+
  |  STALE-SOCKET GUARD                                           |
  |                                                               |
  |  if (deviceConnection != null &&                              |
  |      ReferenceEquals(deviceConnection.TcpClient, client))     |
  |                                                               |
  |  Only clean up if the socket that just died is STILL the      |
  |  live one on the device connection.                           |
  |                                                               |
  |  Without this guard, a device restart races:                  |
  |    t0  old socket dies (its finally has not run yet)          |
  |    t1  device reconnects, new socket, channels re-register    |
  |        -> Add() resets Connection, clears _manuallyDisconnected|
  |    t2  OLD socket's finally finally runs                      |
  |        -> would re-mark the just-reconnected channels offline |
  |  Symptom: "only 1 of N cards shows Connected after restart".  |
  +--------------------------------+------------------------------+
                                   | guard passes
                                   v
  +--------------------------------------------------------------+
  |  for EACH board that registered on this socket                |
  |  (boardsOnThisSocket - one socket may carry several boards),   |
  |  skipping any whose IsOn(client) is already false:            |
  |                                                               |
  |    boardConnection.FailAllPending(                            |
  |        new IOException("Device connection closed."))          |
  |      -> every awaiting SendAndWaitAsync fails immediately     |
  |         instead of waiting out its 15-second timeout          |
  |                                                               |
  |    foreach (slotKey in boardConnection.ChannelSlotKeys)       |
  |        _devices[slotKey].MarkDisconnected()                   |
  |      -> ALL channels on that BOARD go offline together,       |
  |         because they shared the one socket                    |
  |                                                               |
  |    alarm {device}/{board}/comms-loss  (CRITICAL)              |
  |      -> one per board, not per channel and not per device     |
  |                                                               |
  |  then: if no board is left on this socket, drop its           |
  |        DeviceLink so a reconnect cannot inherit a stale       |
  |        write lock                                             |
  |                                                               |
  |  HardwareManagerChanged?.Invoke()  -> UI refresh              |
  +--------------------------------------------------------------+
```

Losing a socket takes down **every board riding it** — if the hardware put boards 1 and 2 on
one TCP client, both go offline together. If each board has its own socket, board 3 dropping
leaves boards 1 and 2 untouched, and only board 3's alarm is raised. See
[Chapter 4](04-connection-multiplexing.md) for why the shape is discovered rather than assumed.

### 11.2 Reconnect

```
  device comes back
        |
        v
  new TCP connection accepted -> new HandleCommandClientAsync task
        |
        v
  channels re-register one by one (Chapter 2)
        |
        v
  ChannelManager.Add(existingChannel, newClient)
        |
        +-- handler already exists?
        |     |
        |     +-- YES, and ReferenceEquals(oldClient, newClient)
        |     |     -> re-arm this slot's own offline flag:
        |     |          Connection = Connection
        |     |        (the first channel to register after reconnect is
        |     |         what actually swaps the shared TcpClient; every
        |     |         OTHER channel would otherwise see it as "already
        |     |         assigned" and never reset _manuallyDisconnected,
        |     |         staying stuck on Offline forever)
        |     |     -> ChannelSlotKeys.Add(key)
        |     |     -> return "already TCP Client"
        |     |
        |     +-- YES, different socket
        |     |     -> existingDeviceConnection.TcpClient = newClient
        |     |     -> ChannelSlotKeys.Add(key)
        |     |     -> handler.Connection = existingDeviceConnection
        |     |        (SAME DeviceConnection object is reused, so
        |     |         pending-response state and slot keys carry over;
        |     |         the old socket is superseded, not explicitly
        |     |         closed -- its read loop exits on its own)
        |     |     -> return "already exists"
        |     |
        |     +-- NO -> full new-handler path (Chapter 3)
        |
        v
  KEY PROPERTY: Session, Program, Battery and DBC state live on the
  handler, not on the socket. A reconnect does not lose them, and the
  store worker keeps draining its queue throughout.
```

### 11.3 Failure catalogue

| Failure | Detected where | Response |
|---|---|---|
| Invalid registration packet | `ProcessRegistrationPacketAsync` | log warning, return `null`, nothing sent |
| Channel not approved | registration decision tree | `CommandStatus.Failed`, row kept in DB for the operator |
| Store packet fails to decode | `StoreUdpData` | `TrySendFailureNotification` → UI notification, packet dropped |
| Store packet for unknown channel | `StoreUdpData` | debug log, packet dropped |
| SessionID → date reconstruction fails | `StoreUdpData` | fall back to `DateTime.Now`, logged |
| SQLite write throws | `StartStoreWorkerAsync` | log error, **continue the loop** (never kills the worker) |
| System error in the data | `StartStoreWorkerAsync` | `OnNotify` with the `SystemError` enum name (IDs 1–17) |
| Command sent while another in flight | `SendAndWaitForResponseAsync` | `"Another command is in progress."` |
| No response in 15 s | `DeviceConnection` | `"No response received from device."` |
| Socket closed mid-command | finally block | `FailAllPending` → immediate failure for all waiters |
| DBC signal IDs exhaust the 31–255 range | `TransferDbcFile` | `SingalId = 0`, names collected, success **with warning** |
| Program edited after transfer | `StartProgram` guard 2 | refuse to start; operator must re-transfer |

**Files involved:** `Services/ChannelManager.cs:252-337, 552-575, 608-695`,
`Services/DeviceConnection.cs`

---


---

[⬅ Previous](10-read-back-commands.md) · [⬅ Index](README.md) · [Next ➡](12-entry-points.md)
