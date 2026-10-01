# 10. Read-Back Commands

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

Two configuration blocks can be read out of the hardware and mirrored into the
main database.

```
  GetManufacturingDetails()                GetFactoryConfigDetails()
  0xAA / Query 0x03                        0xAA / Query 0x02
        |                                        |
        +-- guard: not Running ------------------+
        |          else "Program is running"     |
        v                                        v
  SendAndWaitForResponseAsync              SendAndWaitForResponseAsync
    <ManufacturingDetailDTO>                 <FactoryConfigDetailDTO>
        |                                        |
        v                                        v
  success?                                 success?
        |                                        |
        v                                        v
  IDeviceChannelServices                   IDeviceChannelServices
    .UpdateManufacturingAsync(Channel, d)    .UpdateFactoryAsync(Channel, d)
        |                                        |
        +-- failure -> _log.Warning only         +-- failure -> _log.Warning only
        |   (the READ still succeeded; the       |   (same)
        |    caller gets the data even if the    |
        |    mirror-write failed)                |
        v                                        v
  OnChannelChanged?.Invoke()               OnChannelChanged?.Invoke()
  return Md                                return Fc
```

Related read-backs on the same 0xAA channel:

```
  ReadBatteryParams        0xAA / 0x04    read what the channel currently holds
  HWReady                  0xAA / 0x01    generic hardware-ready probe
  HWReadyForProgram        0xBB / 0x01    ready to accept a program download
```

**Files involved:** `Services/Implementations/ChannelCommandHandler.cs:783-856`

---


---

[⬅ Previous](09-control-commands.md) · [⬅ Index](README.md) · [Next ➡](11-error-disconnect-reconnect.md)
