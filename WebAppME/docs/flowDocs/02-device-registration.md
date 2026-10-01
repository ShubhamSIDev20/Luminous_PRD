# 2. Device / Channel Registration

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

A powered-on channel does not wait to be discovered — it dials the server on
TCP 9999 and announces itself. Registration is per **channel**, not per device:
a 64-channel device sends 64 registration packets over the one socket.

### 2.1 The handshake

```
  HARDWARE CHANNEL                              SERVER (ChannelManager)
  ================                              =======================

  power on / cable in
        |
        | TCP connect to <server>:9999
        +----------------------------------------------->
                                            RunCommandListenerAsync
                                            AcceptTcpClientAsync()
                                                    |
                                                    v
                                            HandleCommandClientAsync
                                            (one task per socket,
                                             1024-byte read buffer)
        |
        | Registration packet (>= 32 bytes)
        | [0xDD][0x01][len][DeviceID][AddrByte]
        | [Name 16B][IP 4B][MAC 6B][CRC16]
        +----------------------------------------------->
                                                    |
                                     buffer[0]==0xDD && buffer[1]==0x01 ?
                                                    | yes
                                                    v
                                        ProcessRegistrationPacketAsync()
                                                    |
                                                    v
                                        (decision tree -- see 2.2)
                                                    |
        |   Response [0xDD][0x01][DevID][Addr]      |
        |            [Status][CRC16]                |
        <-----------------------------------------------+
        |                                        SendOnly()
        |
   Status = 0x01 Success           -> channel is live
   Status = 0x02 AlreadyRegistered -> re-attached after reconnect
   Status = 0x00 Failed            -> NOT approved yet; hardware retries
```

### 2.2 The registration decision tree

This is the heart of the approval mechanism. Note that the **row is written to
the database even when the answer is `Failed`** — that is what puts the channel
in front of the operator.

```
                  ProcessRegistrationPacketAsync(buffer, client)
                                    |
                                    v
                  DecoderService.ParseRegistrationPacket(buffer)
                                    |
                    +---------------+---------------+
                    |                               |
              newChannel == null            newChannel != null
                    |                               |
                    v                               v
            log "Invalid                  db.GetChannelAsync(newChannel)
             registration packet"          (lookup by Device+Board+Channel)
            return null                            |
                                                   |
        +------------------------+-----------------+------------------------+
        |                        |                                          |
   EXISTS &&                EXISTS &&                                 DOES NOT EXIST
   IsRegistered == true     IsRegistered == false                            |
        |                        |                                          |
        v                        v                                          v
  ChannelManager.Add(       if (IsDeleted)                       db.InsertAsync(newChannel)
    existingChannel,          db.UpdateIsDeleteAsync(              -> new row, IsRegistered = 0
    client)                       channel, false)                          |
        |                     (un-delete: hardware is                       v
        |                      back on the network)                  respond FAILED
        |                        |                                          |
        v                        v                            +-------------+
  Add succeeded?           respond FAILED                     |
        |                        |                            |
   +----+----+                   |                            |
   |         |                   +--------------+-------------+
  yes       no                                  |
   |         |                                  v
   |         +--> respond FAILED       Channel now sits in the UI as
   |                                   "Disallow" awaiting operator action.
   v                                   Hardware keeps retrying on a timer.
 message contains                      -> continue to Chapter 3
 "already exists"?
   |
   +-- yes --> respond ALREADY_REGISTERED
   |           + db.UpdateRegistration(newChannel)
   |             (refresh Name / IP / MAC -- device may have
   |              moved to a new IP since last boot)
   |
   +-- no  --> respond SUCCESS
               (brand-new handler created this run)
```

**Design note — why fail-first works.** The protocol has no server→device
"provision" command. Instead the server answers `Failed` and relies on the
hardware's own retry loop. When the operator later flips `IsRegistered` to `1`,
the *next* retry naturally lands in the `Success` branch. No push, no polling,
no extra command needed.

**Files involved:** `Services/ChannelManager.cs:579-792`,
`Services/DecoderService.cs` (`ParseRegistrationPacket`,
`ParseRegistrationResponse`), `docs/PROTOCOL.md` §8.1

---


---

[⬅ Previous](01-background-service-startup.md) · [⬅ Index](README.md) · [Next ➡](03-user-allow-gate.md)
