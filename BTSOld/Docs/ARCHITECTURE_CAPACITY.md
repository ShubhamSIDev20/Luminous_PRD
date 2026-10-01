# BTS Architecture — System Capacity & Performance Analysis

> **Document**: Architecture Capacity & Data Flow Analysis  
> **Version**: 1.0  
> **Date**: 2026-05-06  
> **Author**: Maya (AI Assistant) — based on CircuitManager.cs + CircuitCommandHandler.cs  

---

## 1. System Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│                     BTS SERVER (Blazor App)                  │
│                                                              │
│  CircuitManager (BackgroundService)                          │
│  ├── TCP Listener         port 9999  (Command Channel)       │
│  ├── UDP Listener         port 10000 (Live / View Data)      │
│  └── UDP Listener         port 10001 (Store / Record Data)   │
│                                                              │
│  In-Memory Dictionary<string, ICircuitCommandHandler>        │
│  Key = "DeviceID-CircuitID"                                  │
│                                                              │
│  Per Handler:                                                │
│  ├── TcpClient          (1 per circuit)                      │
│  ├── Channel<recordStoreRequest>  (unbounded queue)          │
│  ├── StoreWorker Task   (SQLite write loop)                  │
│  └── ConnectionAlive Task (TCP keepalive loop)               │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. Port / Protocol Map

| Port  | Protocol | Direction        | Purpose                          | Rate (given) |
|-------|----------|------------------|----------------------------------|--------------|
| 9999  | TCP      | Device → Server  | Registration + Command channel   | On-demand    |
| 10000 | UDP      | Device → Server  | Live / Dashboard view data       | **10 ms**    |
| 10001 | UDP      | Device → Server  | Data store / recording           | **10 ms**    |

### Key Points
- **Port 10001 (Store)** — data queued into `Channel<UdpReceiveResult>` → single processor task → per-circuit `Channel<recordStoreRequest>` → SQLite write.
- **Port 10000 (Live)** — direct event invoke on each UDP packet → updates `RealTime` on the handler → UI dashboard refresh.
- **Port 9999 (TCP)** — registration handshake, then the TCP connection stays alive per circuit for bidirectional commands.

---

## 3. Data Rate Analysis

### Per-Circuit Data Rate

| Channel | Frequency | Packets/sec/circuit | Approx Payload |
|---------|-----------|---------------------|----------------|
| UDP Store (10001) | 10 ms | **100 pkt/s** | ~varies (MeasurementData) |
| UDP Live  (10000) | 10 ms | **100 pkt/s** | ~RealTimeRecord |
| TCP Command | On-demand | ~0-5/min | ~33–1400 bytes |

> **Combined per circuit = ~200 UDP packets/second**

---

## 4. Capacity Estimate — How Many Devices/Circuits

### Architecture Constraints

| Constraint | Detail |
|---|---|
| UDP Store Processor | **Single task** — sequential `ReadAllAsync` loop (SingleReader = true) |
| UDP Live Handler | **Per-packet async invoke** — `ViewUdpData` fires per packet |
| In-Memory Dictionary | No hard limit — RAM bound |
| SQLite per circuit | Each circuit writes to its own `.db` file (per session) |
| TCP per circuit | 1 dedicated TcpClient + `ConnectionAlive` task each |
| StoreWorker per circuit | 1 dedicated background task each |

---

### Conservative Capacity Estimate

#### UDP Store Processing (Bottleneck)

```
Single processor loop → handles ALL circuits' store packets sequentially

At 10ms per circuit:
  - 10 circuits  → 1000 pkts/sec  → OK (1ms average budget per packet)
  - 20 circuits  → 2000 pkts/sec  → Tight
  - 50 circuits  → 5000 pkts/sec  → Likely backlog builds
  - 100 circuits → 10000 pkts/sec → Queue grows unbounded
```

#### Per-Circuit Memory Overhead (In-Process)

```
Per ICircuitCommandHandler (estimated):
  - TcpClient            ~few KB
  - Channel (unbounded)  ~grows with backlog
  - RealTime state       ~1 KB
  - Session state        ~2 KB
  - Calibration buffer   ~few KB (max 3 records)
  - ConnectionAlive task ~1 thread
  - StoreWorker task     ~1 thread

Estimate: ~2–5 MB active RAM per circuit (excluding queue growth)
```

#### Thread / Task Count

```
Per Circuit:
  - 1 ConnectionAlive loop task
  - 1 StoreWorker task

Global:
  - 1 UDP Store Listener task
  - 1 UDP Store Processor task (SINGLE THREADED)
  - 1 UDP View Listener task
  - 1 TCP Command Listener task

For N circuits:
  Total tasks ≈ (2 × N) + 4
```

---

### Recommended Operating Ranges

| Scenario | Circuits | Expected Behavior |
|---|---|---|
| ✅ Safe | 1–10 | Smooth, no queue build-up, <1ms store latency |
| ⚠️ Moderate Load | 11–20 | Mild queue growth possible, monitor backlog |
| ⚠️ Heavy Load | 21–40 | Store processor may fall behind, queue grows |
| ❌ Overload | 41+ | UDP store queue grows unbounded, memory risk |

> **Practical recommendation: ≤ 20 circuits for reliable real-time operation on a standard server.**

---

## 5. Delay / Latency Analysis

### UDP Store Pipeline (Port 10001)

```
Device sends @ 10ms
  → OS UDP receive buffer
  → RunUdpStoreListenerAsync picks up
  → _udpChannel.Writer.WriteAsync (non-blocking)
  → StartUdpProcessorAsync single loop dequeues
  → DecoderService.RealStoreData (decode)
  → Get handler
  → handler.EnqueueForStore → per-circuit Channel
  → StartStoreWorkerAsync dequeues
  → SQLite BulkInsert

Total pipeline steps: 7
```

| Step | Estimated Delay |
|---|---|
| UDP OS buffer → app receive | ~0–2 ms |
| _udpChannel enqueue | ~0 ms (non-blocking) |
| Single processor dequeue | **0 ms if only 1 circuit, up to N×10ms if N circuits** |
| Decode | ~0.1 ms |
| EnqueueForStore | ~0 ms |
| SQLite write | ~1–10 ms (batch size dependent) |
| **Total @ 1 circuit** | **~2–15 ms** |
| **Total @ 10 circuits** | **~10–110 ms** |
| **Total @ 20 circuits** | **~20–200 ms+** |

> ⚠️ The **single UDP store processor** is the critical bottleneck. At 20 circuits × 100 pkt/s = 2000 pkt/s, if each decode+enqueue costs >0.5ms, backlog accumulates.

### UDP Live Pipeline (Port 10000)

```
Device sends @ 10ms
  → RunUdpViewListenerAsync receives
  → OnUdpViewDataReceived event fires
  → ViewUdpData async handler
  → DecoderService.ParseRealTimeData
  → handler.RealTime.NotifyDataChanged → UI update

Total: ~1–5 ms (independent per packet, no queue)
```

> Live/View is **NOT queued** — each packet triggers an immediate async handler, so latency stays low but CPU spikes with many circuits.

### TCP Command Latency

```
SendAndWaitForResponseAsync:
  - Timeout: 15 seconds
  - Blocks circuit command channel (commandinterrupt = true)
  - Single TCP stream per circuit — commands are sequential

Typical round-trip: 10–200 ms (hardware dependent)
```

---

## 6. Known Risks & Bottlenecks

### Risk 1: Single UDP Store Processor
- `SingleReader = true` means **one thread** processes ALL circuits' store data.
- With 10ms data rate × many circuits, this becomes the #1 bottleneck.
- **Fix**: Dispatch per-circuit processing or use a partitioned channel.

### Risk 2: Unbounded Channels
- Both `_udpChannel` (global) and per-circuit `_StoreQueue` are **unbounded**.
- Memory grows if processing can't keep up.
- **Fix**: Add bounded channels with overflow handling.

### Risk 3: In-Memory Dictionary Thread Safety
- `_devices` is a plain `Dictionary<string, ICircuitCommandHandler>`.
- Concurrent `Add` + `Get` during registration could cause race conditions.
- **Fix**: Use `ConcurrentDictionary`.

### Risk 4: commandinterrupt Flag
- Simple `bool` flag — not thread-safe.
- **Fix**: Use `Interlocked` or `SemaphoreSlim`.

### Risk 5: Live View — No Backpressure
- `OnUdpViewDataReceived` fires an async event per packet with no throttle.
- At high circuit counts, this can flood the UI thread.

---

## 7. Configuration Summary (Current Hardcoded Values)

```csharp
commandPort    = 9999    // TCP Registration + Commands
dataStorePort  = 10001   // UDP Recording data
dataViewPort   = 10000   // UDP Live/View data

TCP timeout          = 15 seconds (command response)
TCP loop delay       = 50 ms (connection alive check)
Calibration buffer   = max 3 records in memory
Program packet size  = 1400 bytes max per chunk
DBC packet size      = 1400 bytes max per chunk
```

---

## 8. Recommended System Configuration for Production

### For 1–10 Circuits
| Resource | Recommendation |
|---|---|
| RAM | 4 GB minimum |
| CPU | 4-core, 2.5GHz+ |
| Storage | SSD (SQLite write performance) |
| OS | Windows Server 2019+ or Windows 10+ |
| Network | 100 Mbps LAN (UDP + TCP) |

### For 11–20 Circuits
| Resource | Recommendation |
|---|---|
| RAM | 8 GB |
| CPU | 8-core, 3GHz+ |
| Storage | NVMe SSD |
| Network | Gigabit LAN |
| Consider | Parallel UDP store processor per N circuits |

### For 20+ Circuits (Future Scaling)
- Refactor `StartUdpProcessorAsync` → parallel per-circuit dispatch
- Replace `Dictionary` → `ConcurrentDictionary`
- Add `BoundedChannel` with drop/warn policy
- Consider SQLite WAL mode for concurrent writes
- Monitor `Unstorerecordcount` per handler — this is the real-time backlog indicator

---

## 9. Backlog Monitor

The system already tracks backlog per circuit:

```csharp
Session.Unstorerecordcount  // Records received but not yet written to SQLite
Session.Storerecordcount    // Records successfully written
```

> Alert threshold: If `Unstorerecordcount` grows continuously → store processor falling behind.

---

## 10. Architecture Diagram (Full Flow)

```
┌─────────────┐    TCP:9999      ┌──────────────────────────────────────────┐
│  Hardware   │ ─────────────→  │  RunCommandListenerAsync                 │
│  Device 1   │ ←────────────── │  HandleCommandClientAsync                │
│  Circuit 1  │   Registration  │  → Add(circuit, tcpClient)               │
│             │   + Commands    │  → _devices["D1-C1"] = new Handler       │
└─────────────┘                 └──────────────────────────────────────────┘
       │
       │  UDP:10001 @ 10ms          _udpChannel (unbounded)
       ├─────────────────────────→ [RunUdpStoreListenerAsync]
       │                            → StartUdpProcessorAsync (SINGLE THREAD)
       │                            → StoreUdpData
       │                            → handler._StoreQueue.Writer
       │                            → StartStoreWorkerAsync
       │                            → SQLite INSERT
       │
       │  UDP:10000 @ 10ms
       └─────────────────────────→ [RunUdpViewListenerAsync]
                                    → OnUdpViewDataReceived event
                                    → ViewUdpData
                                    → handler.RealTime.NotifyDataChanged
                                    → Blazor UI update
```

---

*Generated by Maya — Deepak's JARVIS 🤖 | BTS Project | Ador Powertron Ltd*
