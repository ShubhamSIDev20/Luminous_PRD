# BM Device Registration Frame Format V5.0

**Source:** `BM Documents/SW & HW Query-Responce/Excel/Device Registration/Device Registration Frame format V5.0.xlsx`
**Interface:** Web Application (SW) ↔ BTS Hardware (HW)
**Start byte:** `0xDD`

## Packet Structure

`Start | Query ID | [Device Number | Circuit Number |] [Payload] | CRC (2 Bytes)`

Response value: `0x01` = Success, `0x00` = Fail, `0x02` = Already Registered

## Query Summary

| Q# | Description | Query ID | Type |
|----|-------------|----------|------|
| Q1 | Register device | `0x01` | Unicast, with registration payload |
| Q2 | Delete device | `0x02` | Unicast, Device+Circuit numbers |
| Q3 | Is any device available? | `0x03` | **Broadcast** — no device/circuit in query |
| Q4 | Device IP Configuration | `0x04` | **Broadcast** — Unique ID + network config |

## Q1: Register Device

Query:
```
DD 01 LEN [registration payload...] CRC_HI CRC_LO
```
Length = number of payload bytes (excludes CRC).

Registration payload details: see sheet "Device Registration Data" in source Excel.

Responses:
- Registered OK: `DD 01 01 01 01 -- --` (DevNum=0x01, CktNum=0x01, Value=0x01)
- Failed: `DD 01 01 01 00 -- --`
- Already registered: `DD 01 01 01 02 -- --`

## Q2: Delete Device

```
DD 02 DevNum CktNum -- --
```
Response OK: `DD 02 DevNum CktNum 01 -- --`

## Q3: Discovery Broadcast

Query: `DD 03 -- --`

Response (device found): Full network registration info:
```
DD 03 | UID[8] | RemoteIP[4] | TCPPort[4] | DeviceIP[4] | SubnetMask[4] |
       GatewayIP[4] | DNS1[4] | DNS2[4] | UDPLiveDataPort[4] | UDPRegDataPort[4] | CRC[2]
```

Example response:
- Remote IP: `192.168.0.10`, TCP Port: `9999`
- Device IP: `192.168.0.14`, Mask: `255.255.255.0`, GW: `192.168.0.1`
- UDP Live Data Port: `10000`, UDP Reg Data Port: `10001`

## Q4: IP Configuration Broadcast

```
DD 04 | UID[8] | DeviceIP[4] | SubnetMask[4] | GatewayIP[4] | DNS1[4] | DNS2[4] | CRC[2]
```

## Notes
- Firmware module: BCT in `bts_app.c`, `networkDataHandler.c`
- Unique ID is 8-byte device identifier (e.g., MAC-derived)
- UDP ports 10000/10001 confirmed here match ICD port definitions
