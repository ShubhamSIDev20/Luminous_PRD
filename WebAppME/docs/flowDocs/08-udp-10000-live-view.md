# 8. UDP 10000 — Live View Path

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

The second UDP stream is display-only. **Nothing on this path is written to the
session file** (with one exception noted below).

```
  HARDWARE
     |
     |  UDP :10000
     v
  RunUdpViewListenerAsync
     |
     |  OnUdpViewDataReceived?.Invoke(result)   (event, fire-and-forget)
     v
  ViewUdpData(result, token)
     |
     +--> payload guard: null or length < 4  -> return
     |
     +--> decode header:
     |       payload[0] = identify (StartByte)
     |       payload[1] = DeviceID
     |       payload[2] = address byte -> ChannelAddressCodec.Decode
     |                                     -> (board, channel)
     |       payload[3] = type
     |
     +--> handler lookup; not found -> log error, return
     |
     v
   switch (identify)
     |
     +-- 0xCC LiveData, type 0x01  ------------------------------+
     |     rec = DecoderService.ParseRealTimeData(payload)       |
     |     data.RealTime.IOStatus = rec.IOStatus                 |
     |     data.RealTime.NotifyDataChanged(rec.RealTimeRecord)   |
     |       -> dashboard cards, gauges, live charts re-render   |
     |       -> ALSO feeds the CircuitStatus / ProgramStatus     |
     |          values that every command guard reads            |
     |                                                           |
     +-- 0xCC LiveData, type 0x02  ------------------------------+
     |     rec = DecoderService.ParseDBCValues(payload)          |
     |     data.dbcData.CreatedDate = DateTime.Now               |
     |     data.dbcData.DbcValues   = rec.DbcValues              |
     |       -> THIS is the cache that EnqueueForStore reads     |
     |          (Chapter 7, Stage 4) -- the only way live-view    |
     |          data reaches the session file                    |
     |                                                           |
     +-- 0xA0 Calibration  --------------------------------------+
     |     rec = DecoderService.ParseRealTimeData(payload)       |
     |     rolling buffer, max 4 entries:                        |
     |        if (buffer.Count > 3) buffer.RemoveAt(0)           |
     |        buffer.Add(rec.RealTimeRecord)                     |
     |     data.RealTime.NotifyDataChanged(...)                  |
     |                                                           |
     +-- default -----------------------------------------------+
           log "unknown UDP packet, identify byte 0xNN"
```

**Files involved:** `Services/ChannelManager.cs:377-457`,
`Services/DecoderService.cs`, `docs/PROTOCOL.md` §10

---


---

[⬅ Previous](07-udp-10001-session-storage.md) · [⬅ Index](README.md) · [Next ➡](09-control-commands.md)
