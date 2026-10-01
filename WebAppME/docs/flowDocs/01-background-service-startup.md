# 1. Background Service Startup

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

Nothing in this system is polled by the UI. All hardware I/O lives inside one
long-running .NET `BackgroundService` that starts with the web host and never
exits until shutdown.

```
   APPLICATION START (Program.cs)
              |
              v
  +-----------------------------------------+
  | ServiceCollectionExtensions.cs : 82      |
  | services.AddHostedService(provider =>    |
  |   provider.GetRequiredService<           |
  |               ChannelManager>())         |
  |                                          |
  | NOTE: the SAME singleton instance is     |
  | both the hosted service AND the object   |
  | the Blazor pages inject. There is only   |
  | ever one ChannelManager in the process.  |
  +--------------------+---------------------+
                       |
                       v
  +-----------------------------------------+
  | ChannelManager.ExecuteAsync()            |
  +--------------------+---------------------+
                       |
        +--------------+---------------+
        |                              |
        v                              v
  +-----------------+        +--------------------------+
  | LoadDevicesAsync|        | StartInternal()          |
  |                 |        |                          |
  | reads Channels  |        | creates _lifecycleCts    |
  | from main DB    |        | and spawns 4 Task.Run    |
  | WHERE           |        | loops (see below)        |
  |  IsRegistered=1 |        +------------+-------------+
  |                 |                     |
  | -> Add(channel) |                     |
  |    for each     |                     |
  +--------+--------+                     |
           |                              |
           v                              |
  Channel handler objects                 |
  recreated in memory so the              |
  system survives an app restart          |
  without needing the hardware            |
  to re-register first.                   |
                                          |
        +---------------------------------+
        |
        +--> Task 1 : StartUdpProcessorAsync   (drains the store queue)
        |
        +--> Task 2 : RunUdpStoreListenerAsync (UDP :10001 receive)
        |
        +--> Task 3 : RunCommandListenerAsync  (TCP :9999 accept loop)
        |
        +--> Task 4 : RunUdpViewListenerAsync  (UDP :10000 receive)
                       |
                       +--> event OnUdpViewDataReceived += ViewUdpData

              |
              v
  +-----------------------------------------+
  | await Task.Delay(Timeout.Infinite,       |
  |                  stoppingToken)          |
  |                                          |
  | The service parks here forever. All work |
  | now happens on the 4 spawned loops.      |
  +-----------------------------------------+
```

### Shutdown / restart

```
  StopAsync()  or  RestartService()
        |
        v
  StopInternalAsync()
        |
        +--> unsubscribe OnUdpViewDataReceived
        +--> _lifecycleCts.Cancel()
        +--> _udpChannel.Writer.TryComplete()
        +--> _commandListener.Stop()
        +--> dispose both UdpClients
        +--> await WhenAny(all 4 tasks, 5-second timeout)
        |
        v
  RestartService() only: wait 5 s, then StartInternal() again
  (fresh CancellationTokenSource, fresh sockets)
```

**Why the 5-second timeout matters:** if a loop is wedged, shutdown still
completes rather than hanging the whole web host.

**Files involved:** `Services/ChannelManager.cs:81-229`,
`Extensions/ServiceCollectionExtensions.cs:82`

---


---

[⬅ Previous](00-master-block-diagram.md) · [⬅ Index](README.md) · [Next ➡](02-device-registration.md)
