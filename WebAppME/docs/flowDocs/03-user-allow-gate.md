# 3. The User "Allow" Gate

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

Between "hardware asked to join" and "hardware can be used" sits a human.

```
  +----------------------------------------------------------------+
  |  BLAZOR UI  --  Components/Pages/Devices/DeviceList.razor       |
  |                                                                 |
  |  Device / Board / Channel | Status  | Registration | Actions     |
  |  -------------------------|---------|--------------|---------    |
  |  1 - 0 - 1                | Online  |  [ Allow  ]  | Info  Del   |
  |  1 - 0 - 2                | Online  |  [Disallow]  | Info  Del   |
  |  2 - 1 - 7                | Offline |  [Disallow]  | Info  Del   |
  |                                                                 |
  |  [ Register All ]   [ Deregister All ]                          |
  +--------------------------------+--------------------------------+
                                   |
                     operator clicks the badge
                                   |
                                   v
                  +--------------------------------+
                  | ShowRegisteredConfirm(circuit) |
                  |                                |
                  | IsRegistered ? "Deregister"    |
                  |              : "Register"      |
                  | -> confirmation dialog         |
                  +---------------+----------------+
                                  |
                     operator confirms
                                  |
                                  v
         +------------------------+------------------------+
         |                                                 |
    REGISTER path                                    DEREGISTER path
         |                                                 |
         v                                                 v
  DCService.AllowCicuitAsync(circuit, true)     DCService.AllowCicuitAsync(circuit,false)
         |                                                 |
         v                                                 v
  _deviceChannelRepository                        _deviceChannelRepository
    .AllowCicuitAsync(...)                          .AllowCicuitAsync(...)
         |                                                 |
         v                                                 v
  MAIN DB: Channels.IsRegistered = 1              Channels.IsRegistered = 0
         |                                                 |
         |                                                 v
         |                                        CM.Remove(circuit, force)
         |                                          -> handler.UnregisterAsync()
         |                                             sends 0xDD / Query 0x02 (Delete)
         |                                          -> handler removed from _devices
         |
         v
  (nothing sent to hardware here -- see below)
         |
         v
  +--------------------------------------------------------------+
  | HARDWARE'S NEXT RETRY                                          |
  |                                                                |
  |  channel resends [0xDD][0x01]...                               |
  |         -> ProcessRegistrationPacketAsync                      |
  |         -> now matches "EXISTS && IsRegistered == true"        |
  |         -> ChannelManager.Add(existingChannel, client)         |
  |               |                                                |
  |               +--> GetOrCreateDeviceConnection(               |
  |               |         DeviceID, SecondaryBoardNumber)        |
  |               +--> AttachLink(GetOrCreateDeviceLink(client))   |
  |               |      (same DeviceLink for boards that share    |
  |               |       one socket - see Ch 04)                  |
  |               +--> ChannelSlotKeys.Add("Dev-Board-Ch")         |
  |               +--> handler = CreateNewHandler()                |
  |               +--> handler.Channel = channel                   |
  |               +--> handler.Connection = deviceConnection       |
  |               +--> await handler.InitializeAsync()             |
  |               +--> _devices.TryAdd(key, handler)               |
  |               |                                                |
  |               v                                                |
  |         respond SUCCESS  --> channel is now usable             |
  +--------------------------------------------------------------+
                                  |
                                  v
                    HardwareManagerChanged?.Invoke()
                    -> Blazor cards refresh to "Connected"
```

### What `InitializeAsync` restores

When a handler is created, it does **not** start empty — it rehydrates the last
known session so a restart does not lose context:

```
  handler.InitializeAsync()
        |
        +--> IProgramServices.GetLastSessionAsync(Channel)
        |         |
        |         +--> Session  = last BatterySession row
        |         +--> Program  = Session.programs
        |         +--> Battery  = Session.battery
        |
        +--> IDbcService.GetByIdAsync(Session.DbcFileRecordID)       (port 1)
        +--> IDbcService.GetByIdAsync(Session.Port2DbcFileRecordID)  (port 2)
        +--> IDbcService.GetByIdAsync(Session.Port3DbcFileRecordID)  (port 3)
        |         |
        |         +--> MergeDbcDatabases(p1, p2, p3) -> Session.dbcDatabse
        |
        +--> finally: _ = StartStoreWorkerAsync()
                      (the per-channel SQLite writer loop -- Chapter 7)
```

**Files involved:** `Components/Pages/Devices/DeviceList.razor`,
`Services/Implementations/DeviceChannelServices.cs:19-49`,
`Services/ChannelManager.cs:252-374`,
`Services/Implementations/ChannelCommandHandler.cs:97-136`

---


---

[⬅ Previous](02-device-registration.md) · [⬅ Index](README.md) · [Next ➡](04-connection-multiplexing.md)
