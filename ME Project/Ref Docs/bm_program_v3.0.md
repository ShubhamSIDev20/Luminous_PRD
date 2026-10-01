# BM Program Frame Format V3.0

**Source:** `BM Documents/SW & HW Query-Responce/Excel/Program/Program Frame Format V3.0.xlsx`
**Interface:** Web Application (SW) ↔ BTS Hardware (HW)
**Start byte:** `0xBB`

## Packet Structure

`Start | Device Number | Circuit Number | Query ID | [Payload] | CRC (2 Bytes)`

Value `0x01` = OK/Yes, `0x00` = Fail/No

## Handshake Sequence

| Q# | Direction | Description | Query ID |
|----|-----------|-------------|----------|
| Q1 | SW→HW | Is HW ready to accept program data? | `0x01` |
| Q2 | SW→HW | Send Program Metadata/Information | `0x02` |
| Q3 | SW→HW | Send number of program data packets | `0x03` |
| Q4 | SW→HW | Send actual program step data | `0x04` |
| Q5 | SW→HW | Read Program metadata saved on HW | `0x05` |
| Q6 | SW→HW | Read Program saved on HW | `0x06` |

## Program Metadata Payload (Q2 / Q5)

| ID | Parameter | Format |
|----|-----------|--------|
| 1 | Program Name | ASCII string, max 15 bytes, length-prefixed (1 byte) |
| 2 | Program Version | ASCII fixed 11 bytes, e.g. `999.999.999` |
| 3 | Host IP Address | 4 bytes hex (e.g. 192.168.100.14 → `C0 A8 64 0E`) |
| 4 | Host MAC Address | 6 bytes hex |
| 5 | Program Creation Date | 4 bytes Epoch time |

## Q3: Number of Packets
```
BB 01 01 03 00 03 -- --   (3 packets follow)
```

## Q4: Send Program Step Data
```
BB 01 01 04 LEN_HI LEN_LO [step bytes...] -- --
```
- Step packet format: refer to "Program Packet V0.10" document
- Response ACK per step: `BB 01 01 04 01 -- --`

## Q6 Response: Send saved program
```
BB 00 01 06 NUM_STEPS -- --   (header, then step data packets follow)
```

## Notes
- Firmware module: PRG in `bts_app.c`
- Program packet encoding per BM4 operator semantics (see `project_bm4_bts600_semantics.md`)
