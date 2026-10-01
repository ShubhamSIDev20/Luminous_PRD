| Byte Index | Field Name     | Size   | Data Type   | Description             |
| ---------- | -------------- | ------ | ----------- | ----------------------- |
| 0          | Start          | 1 Byte | Hex / UInt8 | Start Sequence (`0xA0`) |
| 1          | Device Number  | 1 Byte | UInt8 (int) | Device Identifier       |
| 2          | Circuit Number | 1 Byte | UInt8 (int) | Circuit Identifier      |
| 3          | Query ID       | 1 Byte | UInt8 (int) | Query Identifier        |
| 4          | Err. Value     | 1 Byte | UInt8 (int) | Error Status            |

# RangeFull

| Byte Index | Field Name                       | Size    | Data Type                |
| ---------- | -------------------------------- | ------- | ------------------------ |
| 5 – 8      | Current Gain in Charge Mode      | 4 Bytes | Float                    |
| 9 – 12     | Current Offset in Charge Mode    | 4 Bytes | Float                    |
| 13 – 16    | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |
| 17 – 20    | Current Gain in Discharge Mode   | 4 Bytes | Float                    |
| 21 – 24    | Current Offset in Discharge Mode | 4 Bytes | Float                    |
| 25 – 28    | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |
| 29 – 32    | Voltage Gain in Charge Mode      | 4 Bytes | Float                    |
| 33 – 36    | Voltage Offset in Charge Mode    | 4 Bytes | Float                    |
| 37 – 40    | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |
| 41 – 44    | Voltage Gain in Discharge Mode   | 4 Bytes | Float                    |
| 45 – 48    | Voltage Offset in Discharge Mode | 4 Bytes | Float                    |
| 49 – 52    | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |

# Range 1

| Byte Index | Field Name                       | Size    | Data Type                |
| ---------- | -------------------------------- | ------- | ------------------------ |
| 53 – 56    | Current Gain in Charge Mode      | 4 Bytes | Float                    |
| 57 – 60    | Current Offset in Charge Mode    | 4 Bytes | Float                    |
| 61 – 64    | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |
| 65 – 68    | Current Gain in Discharge Mode   | 4 Bytes | Float                    |
| 69 – 72    | Current Offset in Discharge Mode | 4 Bytes | Float                    |
| 73 – 76    | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |

# Range 2

| Byte Index | Field Name                       | Size    | Data Type                |
| ---------- | -------------------------------- | ------- | ------------------------ |
| 77 – 80    | Current Gain in Charge Mode      | 4 Bytes | Float                    |
| 81 – 84    | Current Offset in Charge Mode    | 4 Bytes | Float                    |
| 85 – 88    | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |
| 89 – 92    | Current Gain in Discharge Mode   | 4 Bytes | Float                    |
| 93 – 96    | Current Offset in Discharge Mode | 4 Bytes | Float                    |
| 97 – 100   | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |

# Range 3

| Byte Index | Field Name                       | Size    | Data Type                |
| ---------- | -------------------------------- | ------- | ------------------------ |
| 101 – 104  | Current Gain in Charge Mode      | 4 Bytes | Float                    |
| 105 – 108  | Current Offset in Charge Mode    | 4 Bytes | Float                    |
| 109 – 112  | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |
| 113 – 116  | Current Gain in Discharge Mode   | 4 Bytes | Float                    |
| 117 – 120  | Current Offset in Discharge Mode | 4 Bytes | Float                    |
| 121 – 124  | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |

# Range 4

| Byte Index | Field Name                       | Size    | Data Type                |
| ---------- | -------------------------------- | ------- | ------------------------ |
| 125 – 128  | Current Gain in Charge Mode      | 4 Bytes | Float                    |
| 129 – 132  | Current Offset in Charge Mode    | 4 Bytes | Float                    |
| 133 – 136  | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |
| 137 – 140  | Current Gain in Discharge Mode   | 4 Bytes | Float                    |
| 141 – 144  | Current Offset in Discharge Mode | 4 Bytes | Float                    |
| 145 – 148  | Calibration Date and Time        | 4 Bytes | UInt32 (int / timestamp) |

# Temperature Calibration

| Byte Index | Field Name                | Size    | Data Type                |
| ---------- | ------------------------- | ------- | ------------------------ |
| 149 – 152  | Temperature Gain          | 4 Bytes | Float                    |
| 153 – 156  | Temperature Offset        | 4 Bytes | Float                    |
| 157 – 160  | Calibration Date and Time | 4 Bytes | UInt32 (int / timestamp) |

# Footer

| Byte Index | Field Name | Size    | Data Type    |
| ---------- | ---------- | ------- | ------------ |
| 161 – 162  | CRC        | 2 Bytes | UInt16 (int) |
