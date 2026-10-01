<#
.SYNOPSIS
    Debug tool: hand-craft and send a single "LiveData" (0xCC) realtime UDP packet
    to the BatteryTestingSystem server, matching HardwareSimulator/simulator.py's
    build_realtime_packet() / Services/DecoderService.cs's ParseRealTimeData().

.DESCRIPTION
    Every field is a separate variable in the $Fields block below - edit any value,
    re-run, and the script re-packs + re-sends. No simulator/CoreEngine involved,
    so you can send exactly the byte pattern you want to exercise a specific decode
    path (e.g. an undefined enum value, a boundary CRC, a truncated packet by
    trimming $bytes before send).

    Target port is 10000 (dataViewPort / data_view_port in ChannelManager.cs /
    config.json) - the port ChannelManager.RunUdpViewListenerAsync listens on and
    routes to DecoderService.ParseRealTimeData via ChannelManager.ViewUdpData.

.PARAMETER TargetHost
    Server IP/hostname running BatteryTestingSystem. Default: 127.0.0.1

.PARAMETER Port
    UDP port. Default: 10000 (data_view_port). Use 10001 (data_store_port) only if
    you also flip $Fields.QueryType semantics to match RealStoreData's format -
    this script's byte layout targets the LiveData/view packet, not the store one.

.EXAMPLE
    # Default run sends CircuitStatus=Idle, ProgramStatus=Stop, dummy current/voltage.
    .\Send-RealtimeUdp.ps1

.EXAMPLE
    # Point at a remote server and re-send after tweaking $Fields below.
    .\Send-RealtimeUdp.ps1 -TargetHost 192.168.1.50
#>
param(
    [string]$TargetHost = "127.0.0.1",
    [int]$Port = 10000
)

# =============================================================================
# ENUM REFERENCE (Models/Enums/CircuitEnums.cs) - copy the byte value you want
# into $Fields below. Kept as comments, not a strict PS enum, so you can freely
# send undefined/out-of-range values on purpose (e.g. to test decode robustness).
# =============================================================================
#
# StartByte            : LiveData=0xCC (this script), Registration=0xDD, Configuration=0xAA,
#                        Program=0xBB, Control=0xEE, Calibration=0xA0
#
# ProgramRunningStatus : Stop=0x00, Running=0x01
#                        (C# enum only defines these 2 values - see
#                         .claude/CODEBASE_MAP.md 2026-08-12 gotcha before sending anything else)
#
# CircuitStatus        : Idle=0x00, Charge=0x01, Discharging=0x02, Pause=0x03,
#                        Countinue=0x04, Interrupt=0x05, Error=0x06, Msg=0x07, Offline=0x08
#
# OperatorCode         : CC_CHG=1, CV_CHG=2, CP_CHG=3, CCCV_CHG=4, CC_DCHG=5, CP_DCHG=6,
#                        CCCV_DCHG=7, PAU=8, GOTO=9, SET=10, STO=11, CYC=12, BEG=13,
#                        INT=14, REG=15, ERR=16, MSG=17, TABLE=18, CV_DCHG=19, PRODUCER=20

# =============================================================================
# EDIT ME — every field DecoderService.ParseRealTimeData(case StartByte.LiveData) reads
# =============================================================================
$Fields = @{
    StartByte           = 0xCC   # StartByte.LiveData - don't change unless testing a different frame type
    DeviceID            = 1      # byte
    BoardNumber         = 1      # 1-8, packed into address byte high nibble
    ChannelNumber       = 1      # 1-8, packed into address byte low nibble
    QueryType           = 0x01   # byte - ChannelManager.ViewUdpData only routes type==0x01 to ParseRealTimeData

    StepNumber          = 1      # short (signed 16-bit BE)
    ProgramStatus       = 0x00   # byte - ProgramRunningStatus.Stop
    CircuitStatus       = 0x00   # byte - CircuitStatus.Idle  <-- the "circuit status idle" ask
    ErrorId             = 0      # byte

    SystemErrorId       = 0      # int32 BE
    StepRunningTimeMs   = 0      # int32 BE
    RunningTimeMs       = 0      # int32 BE

    Current             = 0.0    # float BE (A)
    Voltage             = 0.0    # float BE (V)
    Temperature         = 25.0   # float BE (C)
    Power               = 0.0    # float BE (W)
    AccumulatedCapacity = 0.0    # float BE (Ah)
    ChargeCapacity      = 0.0    # float BE (Ah)
    DischargeCapacity   = 0.0    # float BE (Ah)
    StepCapacity        = 0.0    # float BE (Ah)
    AccumulatedEnergy   = 0.0    # float BE (Wh)
    ChargeEnergy        = 0.0    # float BE (Wh)
    DischargeEnergy     = 0.0    # float BE (Wh)
    StepEnergy          = 0.0    # float BE (Wh)

    Operator            = 0      # byte - OperatorCode (0 = none/idle)
    CycleStatus         = 0      # byte
    CycleNumber         = 0      # uint16 BE
    CycleRunIteration   = 0      # uint16 BE
    TableStepNumber     = 0      # uint16 BE
    TableTotalRowNumber = 0      # uint16 BE
    # 2 reserved bytes (always 0) go here automatically - not editable

    IOStatus            = @(0x00, 0x00, 0x00)  # exactly 3 raw bytes
}

# =============================================================================
# BIG-ENDIAN PACK HELPERS (network order - matches DecoderService's ReadXBigEndian)
# =============================================================================
function ConvertTo-BEBytes-Int16 {
    param([int16]$Value)
    $b = [BitConverter]::GetBytes($Value)
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($b) }
    return $b
}
function ConvertTo-BEBytes-UInt16 {
    param([uint16]$Value)
    $b = [BitConverter]::GetBytes($Value)
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($b) }
    return $b
}
function ConvertTo-BEBytes-Int32 {
    param([int32]$Value)
    $b = [BitConverter]::GetBytes($Value)
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($b) }
    return $b
}
function ConvertTo-BEBytes-Single {
    param([single]$Value)
    $b = [BitConverter]::GetBytes($Value)
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($b) }
    return $b
}

# CRC-16/MODBUS - byte-for-byte port of DecoderService.CalculateCRC16 /
# HardwareSimulator/simulator.py's calculate_crc16 (init 0xFFFF, poly 0xA001 reflected).
function Get-Crc16Modbus {
    param([byte[]]$Data)
    $crc = 0xFFFF
    foreach ($byte in $Data) {
        $crc = $crc -bxor $byte
        for ($i = 0; $i -lt 8; $i++) {
            if ($crc -band 0x0001) {
                $crc = ($crc -shr 1) -bxor 0xA001
            } else {
                $crc = $crc -shr 1
            }
        }
    }
    return $crc -band 0xFFFF
}

# =============================================================================
# BUILD PAYLOAD (field order/sizes must match DecoderService.ParseRealTimeData exactly)
# =============================================================================
if ($Fields.IOStatus.Count -ne 3) { throw "IOStatus must be exactly 3 bytes, got $($Fields.IOStatus.Count)" }

$addressByte = ((([byte]$Fields.BoardNumber) -band 0xF) -shl 4) -bor (([byte]$Fields.ChannelNumber) -band 0xF)

$bytes = [System.Collections.Generic.List[byte]]::new()
$bytes.Add([byte]$Fields.StartByte)
$bytes.Add([byte]$Fields.DeviceID)
$bytes.Add([byte]$addressByte)
$bytes.Add([byte]$Fields.QueryType)
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Int16 -Value $Fields.StepNumber))
$bytes.Add([byte]$Fields.ProgramStatus)
$bytes.Add([byte]$Fields.CircuitStatus)
$bytes.Add([byte]$Fields.ErrorId)

$bytes.AddRange([byte[]](ConvertTo-BEBytes-Int32 -Value $Fields.SystemErrorId))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Int32 -Value $Fields.StepRunningTimeMs))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Int32 -Value $Fields.RunningTimeMs))

$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.Current))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.Voltage))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.Temperature))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.Power))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.AccumulatedCapacity))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.ChargeCapacity))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.DischargeCapacity))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.StepCapacity))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.AccumulatedEnergy))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.ChargeEnergy))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.DischargeEnergy))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-Single -Value $Fields.StepEnergy))

$bytes.Add([byte]$Fields.Operator)
$bytes.Add([byte]$Fields.CycleStatus)
$bytes.AddRange([byte[]](ConvertTo-BEBytes-UInt16 -Value $Fields.CycleNumber))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-UInt16 -Value $Fields.CycleRunIteration))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-UInt16 -Value $Fields.TableStepNumber))
$bytes.AddRange([byte[]](ConvertTo-BEBytes-UInt16 -Value $Fields.TableTotalRowNumber))
$bytes.AddRange([byte[]]@(0x00, 0x00))  # reserved
$bytes.AddRange([byte[]]$Fields.IOStatus)

$payload = $bytes.ToArray()
$crc = Get-Crc16Modbus -Data $payload
$packet = $payload + (ConvertTo-BEBytes-UInt16 -Value $crc)

# =============================================================================
# SEND
# =============================================================================
Write-Host "Packet ($($packet.Length) bytes): $(($packet | ForEach-Object { $_.ToString('X2') }) -join ' ')"
Write-Host "Address byte: 0x$($addressByte.ToString('X2')) (Board=$($Fields.BoardNumber), Channel=$($Fields.ChannelNumber))"
Write-Host "CRC16: 0x$($crc.ToString('X4'))"

$udpClient = New-Object System.Net.Sockets.UdpClient
try {
    $sent = $udpClient.Send($packet, $packet.Length, $TargetHost, $Port)
    Write-Host "Sent $sent bytes to $TargetHost`:$Port"
}
finally {
    $udpClient.Close()
}
