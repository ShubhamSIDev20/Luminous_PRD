﻿using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Models.SqliteEntities;
using BatteryTestingSystem.Services.BROADCAST;
using BatteryTestingSystem.Utils;
using Newtonsoft.Json;
using Serilog;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;

namespace BatteryTestingSystem.Services
{
    public static class DecoderService
    {

        private static readonly Serilog.ILogger _log =  Log.ForContext(typeof(DecoderService));

        #region CRC16 Calculation and Binding
        public static ushort CalculateCRC16(byte[] data, int offset, int length)
        {
            ushort crc = 0xFFFF;

            for (int i = offset; i < offset + length; i++)
            {
                crc ^= data[i];

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    else
                        crc >>= 1;
                }
            }

            return crc;
        }
        public static byte[] BindCRC16(byte[] data, ushort crc)
        {
            byte[] result = new byte[data.Length + 2];
            Array.Copy(data, 0, result, 0, data.Length);
            result[data.Length] = (byte)(crc & 0xFF);        // Low byte first
            result[data.Length + 1] = (byte)(crc >> 8);      // High byte second
            return result;

            // Per docs/PROTOCOL.md §1.3/§2.2 (Little-Endian CRC-16, low byte first,
            // high byte second) — the only documented convention for this protocol.
        }

        #endregion

        #region Payload Parsing Helpers
        public static CommonResponse<T> TryDecode<T>(byte[] payload)
        {
            try
            {
                // ---- Basic validation ----
                if (payload == null || payload.Length < 5)
                    return CommonResponse<T>.Fail("Invalid payload: too short.");

                //_log.Debug("TryDecode Command Responce : {Payload} ", BitConverter.ToString(payload));

                byte identity = payload[0];
                byte queryType = 0x00;
                dynamic result = null;

                try
                {
                    switch (identity)
                    {
                        // ------------------------ CONFIGURATION (0xAA) ------------------------
                        case (byte)StartByte.Configuration:
                            queryType = payload[3];

                            switch (queryType)
                            {
                                case 0x01:
                                    result = payload[4] == (byte)CommandStatus.Success;
                                    break;

                                case 0x02:
                                    result = FactoryParameters(payload);
                                    break;

                                case 0x03:
                                    result = ManufacturingParameters(payload);
                                    break;

                                case 0x04:
                                    result = BatteryParameters(payload);
                                    break;

                                case 0x05:
                                case 0x06:
                                    result = payload[4] == (byte)CommandStatus.Success;
                                    break;
                            }
                            break;

                        // ------------------------ PROGRAM (0xBB) ------------------------
                        case (byte)StartByte.Program:
                            queryType = payload[3];

                            switch (queryType)
                            {
                                case 0x01:
                                case 0x02:
                                case 0x03:
                                case 0x04:
                                case 0x07:
                                case 0x08:
                                    result = payload[4] == (byte)CommandStatus.Success;
                                    break;

                                case 0x05:
                                    // result = ParseProgramMetadata(payload);
                                    break;

                                case 0x06:
                                    // result = PayloadBuilder.ParseProgramData(payload);
                                    break;

                                // Q9 (0x09): unlike the plain-ACK queries above, the response carries
                                // a second byte (payload[5]) - REASON - alongside STATUS (payload[4]).
                                // On rejection we return Fail(reason message) directly here, bypassing
                                // the generic Ok(result) path at the bottom of this method, so the
                                // caller's CommonResponse.Message is the human-readable reason rather
                                // than the default "Success".
                                case (byte)ProgramDataQuery.LiveStepUpdate:
                                    if (payload.Length < 8)
                                        return CommonResponse<T>.Fail("Live step update response too short.");

                                    if (payload[4] != (byte)CommandStatus.Success)
                                    {
                                        var reason = (LiveStepUpdateReason)payload[5];
                                        return CommonResponse<T>.Fail(LiveStepUpdateReasonMessage(reason));
                                    }

                                    result = true;
                                    break;

                                // Q10: Jump to Program Step (bm_program_v3.2). Response carries a
                                // second byte, REASON, that plain STATUS-only queries above don't have
                                // - a rejection needs its own message, not the generic Ok(false).
                                case (byte)ProgramDataQuery.JumpToStep:
                                    if (payload[4] == (byte)CommandStatus.Success)
                                    {
                                        result = true;
                                    }
                                    else
                                    {
                                        byte reasonByte = payload.Length > 5 ? payload[5] : byte.MaxValue;
                                        return CommonResponse<T>.Fail(JumpToStepReasonMessage((JumpToStepReason)reasonByte));
                                    }
                                    break;
                            }
                            break;

                        // ------------------------ CONTROL (0xEE) ------------------------
                        case (byte)StartByte.Control:
                            queryType = payload[3];

                            switch (queryType)
                            {
                                case 0x01:
                                case 0x02:
                                case 0x03:
                                case 0x04:
                                case 0x05:
                                case 0x06:
                                    result = payload[4] == (byte)CommandStatus.Success;
                                    break;
                            }
                            break;

                        // ------------------------ Registration (0xDD) ------------------------
                        case (byte)StartByte.Registration:
                            queryType = payload[1];

                            switch (queryType)
                            {
                                case 0x01:
                                case 0x02:                                    
                                    result = payload[4] == (byte)CommandStatus.Success;
                                    break;
                                case 0x04:
                                case 0x05:
                                    result = payload[2] == (byte)CommandStatus.Success;
                                    break;
                            }

                            break;

                        // ------------------------ Calibration (0xA0) ------------------------
                        case (byte)StartByte.Calibration:
                            queryType = payload[3];

                            switch (queryType)
                            {
                                case (byte)CalibrationQueryId.PreviousCalibration:     // 0x17 — returns full CalibrationData block
                                    result = ParseCalibrationPayload(payload);
                                    break;

                                case (byte)CalibrationQueryId.CurrentChargeLowPoint:
                                case (byte)CalibrationQueryId.CurrentChargeHighPoint:
                                case (byte)CalibrationQueryId.CurrentChargeGainOffset:
                                case (byte)CalibrationQueryId.CurrentDisChargeLowPoint:
                                case (byte)CalibrationQueryId.CurrentDisChargeHighPoint:
                                case (byte)CalibrationQueryId.CurrentDisChargeGainOffset:
                                    result = payload[5] == (byte)CommandStatus.Success;
                                    break;

                                default:
                                    result = payload[4] == (byte)CommandStatus.Success;
                                    break;
                            }

                            break;

                        // ------------------------ UNKNOWN IDENTITY ------------------------
                        default:
                            return CommonResponse<T>.Fail($"Unknown Identity: {identity}");
                    }
                }
                catch (Exception ex)
                {
                    return CommonResponse<T>.Fail($"Exception while parsing: {ex.Message}");
                }

                // ---- Attempt converting result to T ----
                if (result is T finalResult)
                    return CommonResponse<T>.Ok(finalResult);

                return CommonResponse<T>.Fail($"Failed to parse result as type {typeof(T).Name}");
            }
            catch (Exception ex)
            {
                return CommonResponse<T>.Fail($"Decoding failed: {ex.Message}");
            }
        }

        // Q9 REASON byte -> user-facing message. 0x02/0x03/0x06 are Primary-only checks; every
        // Secondary-side rejection (paused, operator-wait, link failure) arrives as 0x05, so that
        // message is deliberately worded as "rejected" rather than "link failure".
        private static string LiveStepUpdateReasonMessage(LiveStepUpdateReason reason) => reason switch
        {
            LiveStepUpdateReason.Success => "Applied by the Secondary.",
            LiveStepUpdateReason.Idle => "Program is idle, or awaiting operator action (interrupt / error wait / message wait).",
            LiveStepUpdateReason.StepNotCurrent => "Step number is not the currently executing step.",
            LiveStepUpdateReason.OperatorChangeNotPermitted => "Operator change not permitted - parameters only.",
            LiveStepUpdateReason.OperatorNotEligible => "Operator not eligible for live update (TABLE / BEG / CYC / GOTO and other non-regulating operators).",
            LiveStepUpdateReason.SecondaryRejectedOrLinkDown => "Secondary NACK - any cause - or Primary-Secondary link failure.",
            LiveStepUpdateReason.MalformedPacket => "Malformed step packet.",
            LiveStepUpdateReason.PreviousUpdateInFlight => "A previous live update is still in flight - retry once the pending response arrives.",
            _ => $"Unknown reason code 0x{(byte)reason:X2}."
        };

        /// <summary>
        /// Q10 (Jump to Program Step) REASON byte -> operator-facing message. Per bm_program_v3.2.md,
        /// every Secondary-side rejection (paused, operator-wait, a step request already in flight)
        /// reaches here as <see cref="JumpToStepReason.SecondaryRejectedOrLinkDown"/> - the Secondary's
        /// ACK/NACK frame carries no reason field, so the Primary can't be more specific than that.
        /// </summary>
        private static string JumpToStepReasonMessage(JumpToStepReason reason) => reason switch
        {
            JumpToStepReason.NoProgramRunning => "No program is running or paused on this circuit.",
            JumpToStepReason.StepDoesNotExist => "That step does not exist in the loaded program.",
            JumpToStepReason.MalformedFrame => "Jump request was malformed.",
            JumpToStepReason.SecondaryRejectedOrLinkDown =>
                "Rejected by the hardware - the channel may be paused, busy, or awaiting an operator action. Try again in a moment.",
            JumpToStepReason.JumpAlreadyInFlight => "A previous jump is still in progress. Try again once it completes.",
            _ => "Jump rejected by the hardware.",
        };
        #endregion

        #region Command Builder
        public static byte[] BuildCommand(CommandRequest req)
        {

            byte addressByte = ChannelAddressCodec.Encode(req.SecondaryBoardNumber, req.ChannelId);

            byte[] header = req.Range.HasValue
            ? new byte[]
            {
                (byte)req.Start,
                (byte)(int)req.DeviceId,
                addressByte,
                req.QueryId,
                (byte)req.Range.Value   // ← extends header to 5 bytes
            }
            : new byte[]
            {
                (byte)req.Start,
                (byte)(int)req.DeviceId,
                addressByte,
                req.QueryId             // ← normal 4 bytes
            };

            byte[] payload;

           

            if (req.Data != null && req.Data.Length > 0)
            {
                payload = new byte[header.Length + req.Data.Length];
                Array.Copy(header, payload, header.Length);
                Array.Copy(req.Data, 0, payload, header.Length, req.Data.Length);

            }
            else if (req.SingleByte.HasValue)
            {
                payload = new byte[header.Length + 1];
                Array.Copy(header, payload, header.Length);
                payload[header.Length] = req.SingleByte.Value;
            }
            else
            {
                payload = header;
            }

            ushort crc = CalculateCRC16(payload, 0, payload.Length);
            return BindCRC16(payload, crc);
        }

        #endregion

        #region ByteToModal Helpers

        public static bool EpochSecondsToDateTime(int epochSeconds, out DateTime dateTime, bool isLocal = false)
        {
            dateTime = default;
            if (epochSeconds < 0)
                return false;
            try
            {
                var dto = DateTimeOffset.FromUnixTimeSeconds((uint)epochSeconds);

                dateTime = isLocal
                    ? dto.LocalDateTime
                    : dto.UtcDateTime;

                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        /// <summary>
        /// Reconstructs the session date/time from a packed session ID created by
        /// <see cref="GetSessionIdBytes"/>.
        ///
        /// Packed layout: [DeviceID 8-bit][Board/Channel address 8-bit][epoch low 16-bit]
        ///
        /// The low-16-bit epoch wraps every 65 536 s (~18.2 h).  We recover the full
        /// epoch by anchoring to "now" and picking the high-word multiple of 65536
        /// whose low 16 bits match the stored value.  Sessions are never older than
        /// a few days, so the result is always accurate within ±9 hours of now.
        /// </summary>
        public static bool SessionIdToDateTime(int packedSessionId, out DateTime dateTime, bool isLocal = false)
        {
            dateTime = default;
            try
            {
                uint packed     = (uint)packedSessionId;
                uint epochLow16 = packed & 0xFFFF;                     // bits 0-15

                uint nowEpoch   = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                uint nowHigh    = nowEpoch & 0xFFFF0000u;              // bits 16-31

                // Build the nearest candidate: same upper 16 bits + stored lower 16 bits
                uint candidate  = nowHigh | epochLow16;

                // If candidate is >9 h in the future → step back one 65536-s period
                // If candidate is >9 h in the past   → step forward one period
                const uint halfWrap = 32768u;
                if (candidate > nowEpoch + halfWrap)
                    candidate -= 65536u;
                else if (candidate + halfWrap < nowEpoch)
                    candidate += 65536u;

                var dto = DateTimeOffset.FromUnixTimeSeconds(candidate);
                dateTime = isLocal ? dto.LocalDateTime : dto.UtcDateTime;
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static ChannelDto ParseRegistrationPacket(byte[] payload)
        {
            if (payload == null || payload.Length < 32)
                throw new ArgumentException("Invalid payload length");

            ChannelDto dto = new ChannelDto();

            dto.ChannelType = "Single";

            int index = 3; // Skip 0xEE, QueryId, Length

            // 1 byte → Device ID
            dto.DeviceID = payload[index++];

            // 1 byte → Board/Channel address
            var (boardNumber, channelNumber) = ChannelAddressCodec.Decode(payload[index++]);
            dto.SecondaryBoardNumber = boardNumber;
            dto.ChannelNumber = channelNumber;

            // 16 bytes → Device Name
            byte[] nameBytes = new byte[16];
            Array.Copy(payload, index, nameBytes, 0, 16);
            dto.DeviceName = Encoding.ASCII.GetString(nameBytes).Trim('\0');
            index += 16;

            // 4 bytes → IP Address
            dto.IPAddress = string.Join(".", payload.Skip(index).Take(4));
            index += 4;

            // 6 bytes → MAC Address
            byte[] macBytes = payload.Skip(index).Take(6).ToArray();
            dto.MACID = string.Join(":", macBytes.Select(b => b.ToString("X2")));
            index += 6;

            dto.PrimarySerialNumber = string.Empty;

            dto.IsRegistered = false;
            
            dto.SecondarySerialNumber = string.Empty;

            return dto;
        }
        public static recordRequest ParseRealTimeData(byte[] payload)
        {
            try
            {
                
                if (payload == null)
                    return null;

                //_log.Debug("Payload for RealTimeData: {Payload}", BitConverter.ToString(payload));

                recordRequest recordRequest = new recordRequest();
                recordRequest.RealTimeRecord = new RealTimeRecordDto();

                int index = 0;

                // Start byte (0xCC) //calibration (0xA0)
                StartByte sb = (StartByte)payload[index++];

                // Minimum: 1(start) + 1(dev) + 1(circuit) + 1(query) + 
                // 2(step) + 4 + 4 + 4 + 4 + 4 + 1 + 1 + 4 + 4 + 1 + 3(io) + 2(crc)

                if (sb == StartByte.LiveData && payload.Length < 37)
                    return null;

                // Minimum: 1(start) + 1(dev) + 1(circuit) + 1(query) + 
                // 4 + 4 + 4 + 4 + 4 + 4 + 2(crc)
                if (sb == StartByte.Calibration && payload.Length < 29)
                    return null;

                // ----- CRC CHECK -----
                ushort receivedCrc = ReadUInt16BigEndian(payload, payload.Length - 2);

                ushort computedCrc = CalculateCRC16(payload, 0, payload.Length - 2);

                if (receivedCrc != computedCrc)
                {
                    // CRC failed — log but continue parsing
                     //_log.Debug($"CRC mismatch: recv={receivedCrc:X4} calc={computedCrc:X4}");
                    // return null;
                }

                // DeviceID (1B)
                recordRequest.RealTimeRecord.DeviceID = payload[index++];

                // Board/Channel address (1B)
                var (rtBoardNumber, rtChannelNumber) = ChannelAddressCodec.Decode(payload[index++]);
                recordRequest.RealTimeRecord.SecondaryBoardNumber = rtBoardNumber;
                recordRequest.RealTimeRecord.ChannelNumber = rtChannelNumber;

                // QueryID (1B) → skip
                recordRequest.RealTimeRecord.QueryID = payload[index++];

                // Timestamp local generated
                recordRequest.RealTimeRecord.TimeStamp = DateTime.Now;

                switch (sb)
                {
                    case StartByte.LiveData:
                        // Step Number (2B)
                        recordRequest.RealTimeRecord.StepNumber = ReadInt16BigEndian(payload, index);
                        index += 2;

                        // Program Running Status (1B)
                        recordRequest.RealTimeRecord.ProgramStatus = (ProgramRunningStatus)payload[index++];

                        // Circuit Status (1B)
                        recordRequest.RealTimeRecord.CircuitStatus = (CircuitStatus)payload[index++];

                        // if CircuitStatus is Error or Msg, then read MessageType and MessageCode
                        recordRequest.RealTimeRecord.ErrorId = payload[index++];

                        // Execption/Error ID (4B)
                        recordRequest.RealTimeRecord.SystemErrorId = ReadInt32BigEndian(payload, index);
                        index += 4;

                        // Step Running Time (4B, ms)
                        recordRequest.RealTimeRecord.StepRunningTime = TimeSpan.FromMilliseconds(ReadInt32BigEndian(payload, index));
                        index += 4;

                        // Total Running Time (4B, ms)
                        recordRequest.RealTimeRecord.RunningTime = TimeSpan.FromMilliseconds(ReadInt32BigEndian(payload, index));
                        index += 4;

                        // Current (float, 4B)
                        recordRequest.RealTimeRecord.Current = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Voltage (float, 4B)
                        recordRequest.RealTimeRecord.Voltage = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Temperature (float, 4B)
                        recordRequest.RealTimeRecord.Temperature = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Power (float, 4B) W
                        recordRequest.RealTimeRecord.Power = ReadSingleBigEndian(payload, index);
                        index += 4;

                        //Accumulated Capacity (float, 4B) Ah
                        recordRequest.RealTimeRecord.AccumulatedCapacity = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Charge Capacity (float, 4B) AhCha
                        recordRequest.RealTimeRecord.ChargeCapacity = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Discharge Capacity (float, 4B) AhDch
                        recordRequest.RealTimeRecord.DischargeCapacity = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Step Capacity (float, 4B) AhStep
                        recordRequest.RealTimeRecord.StepCapacity = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Accumulated Energy (float, 4B) Wh
                        recordRequest.RealTimeRecord.AccumulatedEnergy = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Charge Energy (float, 4B) WhCha
                        recordRequest.RealTimeRecord.ChargeEnergy = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Discharge Energy (float, 4B) WhDch
                        recordRequest.RealTimeRecord.DischargeEnergy = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Step Energy (float, 4B) WhStep
                        recordRequest.RealTimeRecord.StepEnergy = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Operator Code (1B)
                        recordRequest.RealTimeRecord.Operator = payload[index++];

                        // Cycle Status
                        recordRequest.RealTimeRecord.CycleStatus = (int)payload[index++];

                        // Cycle Number (2B)
                        recordRequest.RealTimeRecord.CycleNumber = ReadInt16BigEndian(payload, index);
                        index += 2;

                        // Cycle RUN Iteration (2B)
                        recordRequest.RealTimeRecord.CycleRunIteration = ReadInt16BigEndian(payload, index);
                        index += 2;

                        // Table Step Number (2B)
                        recordRequest.RealTimeRecord.TableStepNumber = ReadInt16BigEndian(payload, index);
                        index += 2;

                        // Table Total Row Number (2B)
                        recordRequest.RealTimeRecord.TableTotalRowNumber = ReadInt16BigEndian(payload, index);
                        index += 2;

                        // skip 2 bytes 
                        index += 2;

                        // IO Status (3B)
                        byte[] ioBytes = payload.Skip(index).Take(3).ToArray();
                        recordRequest.IOStatus = ParseIOBytes(ioBytes);
                        index += 3;
                      
                        // if DBC Available

                        break;
                    case StartByte.Calibration:

                        // Current (float, 4B)
                        recordRequest.RealTimeRecord.Current = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Current (int, 4B) ADC Count
                        recordRequest.RealTimeRecord.CurrentInt = ReadInt32BigEndian(payload, index);
                        index += 4;

                        // Voltage (float, 4B)
                        recordRequest.RealTimeRecord.Voltage = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Voltage (int, 4B) ADC Count
                        recordRequest.RealTimeRecord.VoltageInt = ReadInt32BigEndian(payload, index);
                        index += 4;

                        // Temperature  (float, 4B)
                        recordRequest.RealTimeRecord.Temperature = ReadSingleBigEndian(payload, index);
                        index += 4;

                        // Temperature (int, 4B) ADC Count
                        recordRequest.RealTimeRecord.TemperatureInt = ReadInt32BigEndian(payload, index);
                        index += 4;
                        // ErrorCode (int, 1B) 
                        recordRequest.RealTimeRecord.ErrorId = (int)payload[index++];

                        break;
                    

                    default:

                        break;
                }

                return recordRequest;

            }
            catch (Exception ex)
            {
                _log.Error(ex, "ParseRealTimeData exception: " + ex);
                return null;
            }
        }

        public static DbcRecord ParseDBCValues(byte[] payload)
        {
            try
            {
                DbcRecord dbcRecord = new DbcRecord();
                dbcRecord.DbcValues = new Dictionary<string, object?>();

                payload = payload[..^2]; // crc16 removes last 2 bytes


                int i = 0;

                // Start byte (0xCC)
                i++;

                // DeviceID (1B)
                int DeviceID = payload[i++];

                // Board/Channel address (1B) — decoded for symmetry with other parsers; DbcRecord has no channel field.
                var (_, _) = ChannelAddressCodec.Decode(payload[i++]);

                // QueryID (1B) → skip
                i++;

                while (i < payload.Length)
                {
                    string dbcKey = ((int)payload[i++]).ToString(); 

                    byte datatypelength = payload[i++];

                    int dataType = (datatypelength >> 4) & 0x0F;
                    int dataLength = datatypelength & 0x0F;

                    //int temp = 0;
                    int defulatInt(byte[] payload, int i, int dataLength)
                    {
                        int temp = 0;

                        for (int j = 0; j < dataLength; j++)
                        {
                            temp = (temp << 8) | payload[i + j];
                        }

                        return temp;
                    }

                    (object? val, int size) = dataType switch
                    {
                        0 => ((object?)(ReadInt32BigEndian(payload, i) != 0), 4), // 4 byte bool value
                        1 => ((object?)ReadInt32BigEndian(payload, i), 4),  // int32
                        2 => ((object?)ReadInt32BigEndian(payload, i), 4),  // int32
                        3 => ((object?)ReadSingleBigEndian(payload, i), 4),  // float
                        _ => ((object?)defulatInt(payload, i, dataLength), dataLength)
                    };

                    dbcRecord.DbcValues[dbcKey] = val;

                    i += dataLength;

                }

                return dbcRecord;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "ParseDBCValues exception: " + ex);
                return null;
            }
        }

        public static CommonResponse<recordStoreRequest> RealStoreData(byte[] packet)
        {
            //_log.Debug("Payload for Store: {Payload}", BitConverter.ToString(packet));
           
            byte[] payload = packet[..^2]; // removes last 2 bytes

            try
            {
                recordStoreRequest RequestData = new recordStoreRequest
                {
                    RealStoreRecord = new List<MeasurementData>()
                };

                MeasurementData? current = null;

                Dictionary<string, object?> dbcValues = new();

                int i = 0;

                // Start byte (0xCC)
                i++;

                // DeviceID (1B)
                int DeviceID = payload[i++];

                // Board/Channel address (1B)
                var (boardNumber, channelNumber) = ChannelAddressCodec.Decode(payload[i++]);

                // QueryID (1B) → skip
                i++;

                // Session ID (4B)
                int SessionId = ReadInt32BigEndian(payload, i);
                i += 4;

                // Step Number (2B)
                int StepNumber = (int)ReadInt16BigEndian(payload, i);
                i += 2;

                // OperatorCode (1B)
                int OperatorCode = payload[i++];

                // Circuit Status (1B)
                CircuitStatus circuitStatus = (CircuitStatus)payload[i++];

                while (i < payload.Length)
                {
                    //if (i + 5 > payload.Length)
                    //{
                    //    // Not enough bytes for opcode + float
                        //_log.Debug($"Incomplete data at the end of payload.{i} {payload.Length}");
                    //    break;
                    //}
                        
                    byte opcode = payload[i++];
                    int intValue = 0;
                    float floatValue = 0;
                    string dbcKey = string.Empty;
                    object? dbcValue = null; 

                    if (opcode == 1 || opcode == 14)
                    {
                        intValue = ReadInt32BigEndian(payload, i);
                        i += 4;

                    }
                    else if (opcode == 15 || opcode == 16)
                    {
                        intValue = (int)payload[i++];
                    }
                    else if (opcode == 17)
                    {
                        intValue = ReadInt16BigEndian(payload, i);
                        i += 2;
                    }
                    else if (opcode == 0 || (opcode >= 31 && opcode <= 250))
                    {
                        dbcKey = ((int)opcode).ToString();

                        byte value = payload[i++];

                        int dataType = (value >> 4) & 0x0F;
                        int dataLength = value & 0x0F;

                        if (opcode != 0)
                        {
                            //int temp = 0;
                            int defulatInt(byte[] payload, int i, int dataLength)
                            {
                                int temp = 0;

                                for (int j = 0; j < dataLength; j++)
                                {
                                    temp = (temp << 8) | payload[i + j];
                                }

                                return temp;
                            }

                            (object? val, int size) = dataType switch
                            {
                                0 => ((object?)(ReadInt32BigEndian(payload, i) != 0), 4), // 4 byte bool value
                                1 => ((object?)ReadInt32BigEndian(payload, i), 4),  // int32
                                2 => ((object?)ReadInt32BigEndian(payload, i), 4),  // int32
                                3 => ((object?)ReadSingleBigEndian(payload, i), 4),  // float
                                _ => ((object?)defulatInt(payload, i, dataLength), dataLength)
                            };

                            dbcValue = val;
                        }
                      
                        i += dataLength;
                    }
                    else
                    {
                        floatValue = ReadSingleBigEndian(payload, i);
                        i += 4;
                    }

                    switch (opcode)
                    {
                        case 1: 

                            if (current != null)
                            {
                                // Add previous record to list
                                current.dbcValues = JsonConvert.SerializeObject(dbcValues);
                                RequestData.RealStoreRecord.Add(current);
                                dbcValues = new();
                                //_log.Debug($"UDPStore row added in list:{JsonConvert.SerializeObject(current)}");
                                //Set Null to current
                                current = null;
                            }

                            current = new MeasurementData
                            {
                                DeviceId = DeviceID,
                                SecondaryBoardNumber = boardNumber,
                                ChannelId = channelNumber,
                                StepNumber = StepNumber,
                                CircuitStatus = circuitStatus,
                                Operator = OperatorCode,
                                SessionID = SessionId,
                                ProgramRunningTime = intValue,
                                DateTime = DateTime.Now
                            };

                            break;
                        case 2: current!.Current = floatValue; break;
                        case 3: current!.Voltage = floatValue; break;
                        case 4: current!.Temperature = floatValue; break;
                        case 5: current!.Power = floatValue; break;
                        case 6: current!.AccumulatedCapacity = floatValue; break;
                        case 7: current!.ChargeCapacity = floatValue; break;
                        case 8: current!.DischargeCapacity = floatValue; break;
                        case 9: current!.StepCapacity = floatValue; break;
                        case 10: current!.AccumulatedEnergy = floatValue; break;
                        case 11: current!.ChargeEnergy = floatValue; break;
                        case 12: current!.DischargeEnergy = floatValue; break;
                        case 13: current!.StepEnergy = floatValue; break;
                        case 14: 
                            current!.SystemErrorID = intValue;
                            List<string> errorList = new List<string>();
                            foreach (SystemError error in Enum.GetValues(typeof(SystemError)))
                            {
                                if ((intValue & (int)error) != 0)
                                {
                                    errorList.Add(error.ToString());
                                }
                            }
                            current.Remark = string.Join(", ", errorList);
                            break;
                        case 15:
                            current!.MessageId = intValue;
                            current.Remark = ErrorMessages.Messages
                                .FirstOrDefault(m => m.Index == intValue)?.Message ?? string.Empty;
                            break;
                        case 16:
                            current!.ErrorId = intValue;
                            current.Remark = ErrorMessages.Errors
                                .FirstOrDefault(e => e.Index == intValue)?.Message ?? string.Empty;
                            break;
                        case 17:
                            current!.Remark = $"Custom Remark: {CommandTracker.GetCommand(ushort.Parse(current.Remark ?? "-1"), true)}";
                            break;

                        case >= 31 and <= 250:

                            if (!string.IsNullOrEmpty(dbcKey) && dbcValue != null)
                                dbcValues.Add(dbcKey, dbcValue);

                            dbcValue = null;
                            dbcKey = string.Empty;

                            break;
                        default:
                            // Unknown opcode → ignore
                            break;
                    }
                }

                // 🔹 Add LAST record (very important)
                if (current != null)
                {
                    current.dbcValues = JsonConvert.SerializeObject(dbcValues);
                    RequestData.RealStoreRecord.Add(current);
                    dbcValues = new();
                    //_log.Debug($"UDPStore ROW :{JsonConvert.SerializeObject(current)}");
                }

                // Get a valid source record that has proper DBC values
                var getDbc = RequestData.RealStoreRecord
                    .FirstOrDefault(r => r.DbcValuesParsed != null && r.DbcValuesParsed.Count > 1);

                if (getDbc?.dbcValues != null)
                {
                    foreach (var record in RequestData.RealStoreRecord)
                    {
                        // Check safely for null before accessing Count
                        if (record.DbcValuesParsed == null || record.DbcValuesParsed.Count <= 1)
                        {
                            record.dbcValues = getDbc.dbcValues;
                        }
                    }
                }

                if (RequestData.RealStoreRecord.Count == 0)
                {
                    return CommonResponse<recordStoreRequest>
                        .Fail("No measurement data found in payload");
                }

                return CommonResponse<recordStoreRequest>
                    .Ok(RequestData, "Payload decoded successfully");
            }
            catch (Exception ex)
            {
                return CommonResponse<recordStoreRequest>
                    .Fail($"DecodePayload failed: {ex.Message}");
            }
        }
        public static CommonResponse<recordStoreRequest> RealStoreDataV2(byte[] packet)
        {
            //_log.Debug("Payload for StoreV2: {Payload}", BitConverter.ToString(packet));

            byte[] payload = packet[..^2]; // removes last 2 bytes (CRC)

            try
            {
                recordStoreRequest RequestData = new recordStoreRequest
                {
                    RealStoreRecord = new List<MeasurementData>()
                };

                MeasurementData? current = null;
                int i = 0;

                // Start byte (0xCC)
                i++;

                // DeviceID (1B)
                int DeviceID = payload[i++];

                // Board/Channel address (1B)
                var (boardNumber, channelNumber) = ChannelAddressCodec.Decode(payload[i++]);

                // QueryID (1B) → skip
                i++;

                // Session ID (4B)
                int SessionId = ReadInt32BigEndian(payload, i);
                i += 4;

                // Step Number (2B)
                int StepNumber = (int)ReadInt16BigEndian(payload, i);
                i += 2;

                // OperatorCode (1B)
                int OperatorCode = payload[i++];

                // Circuit Status (1B)
                CircuitStatus circuitStatus = (CircuitStatus)payload[i++];

                // ── V2: Field-presence bitmask (2 bytes, big-endian) ──────────────
                // Bits 1–17 map to opcodes 1–17. Bit 0 is reserved/unused (no opcode 0).
                // Bit N = 1 → opcode N data bytes are present in the stream.
                // Bit N = 0 → opcode N absent; not sent in packet.
                // NOTE: DBC opcodes (31–250) are not supported in V2.
                ushort presenceMask = ReadUInt16BigEndian(payload, i);
                i += 2;

                // Collect present indices in ascending order.
                // Valid opcodes are 1–17. Bit 0 is unused/reserved — skip it.
                List<int> sortedIndices = new List<int>();
                for (int bit = 0; bit <= 16; bit++)
                {
                    if ((presenceMask & (1 << bit)) != 0)
                        sortedIndices.Add(bit + 1);
                }

                // ── Parse loop: repeat sortedIndices cycle until payload exhausted ──
                // Each full cycle of sortedIndices = one measurement row.
                // Opcode 1 is always the row-start sentinel (ProgramRunningTime).
                // The bitmask is fixed for all rows in this packet — same fields every row.
                // Sizes:
                //   opcode  1, 14      → int32  (4B)
                //   opcode 15, 16      → uint8  (1B)
                //   opcode 17          → int16  (2B)
                //   opcode  2–13       → float  (4B)
                int fieldIndex = 0;
                while (i < payload.Length)
                {
                    int opcode = sortedIndices[fieldIndex];
                    fieldIndex = (fieldIndex + 1) % sortedIndices.Count; // wrap back after last field

                    int intValue = 0;
                    float floatValue = 0;

                    if (opcode == 1 || opcode == 14)
                    {
                        intValue = ReadInt32BigEndian(payload, i);
                        i += 4;
                    }
                    else if (opcode == 15 || opcode == 16)
                    {
                        intValue = (int)payload[i++];
                    }
                    else if (opcode == 17)
                    {
                        intValue = ReadInt16BigEndian(payload, i);
                        i += 2;
                    }
                    else
                    {
                        // opcodes 2–13 → float (4B)
                        floatValue = ReadSingleBigEndian(payload, i);
                        i += 4;
                    }

                    switch (opcode)
                    {
                        case 1:
                            if (current != null)
                            {
                                RequestData.RealStoreRecord.Add(current);
                                current = null;
                            }

                            current = new MeasurementData
                            {
                                DeviceId = DeviceID,
                                SecondaryBoardNumber = boardNumber,
                                ChannelId = channelNumber,
                                StepNumber = StepNumber,
                                CircuitStatus = circuitStatus,
                                Operator = OperatorCode,
                                SessionID = SessionId,
                                ProgramRunningTime = intValue,
                                DateTime = DateTime.Now
                            };
                            break;

                        case 2:  current!.Current             = floatValue; break;
                        case 3:  current!.Voltage             = floatValue; break;
                        case 4:  current!.Temperature         = floatValue; break;
                        case 5:  current!.Power               = floatValue; break;
                        case 6:  current!.AccumulatedCapacity = floatValue; break;
                        case 7:  current!.ChargeCapacity      = floatValue; break;
                        case 8:  current!.DischargeCapacity   = floatValue; break;
                        case 9:  current!.StepCapacity        = floatValue; break;
                        case 10: current!.AccumulatedEnergy   = floatValue; break;
                        case 11: current!.ChargeEnergy        = floatValue; break;
                        case 12: current!.DischargeEnergy     = floatValue; break;
                        case 13: current!.StepEnergy          = floatValue; break;

                        case 14:
                            current!.SystemErrorID = intValue;
                            List<string> errorList = new List<string>();
                            foreach (SystemError error in Enum.GetValues(typeof(SystemError)))
                            {
                                if ((intValue & (int)error) != 0)
                                    errorList.Add(error.ToString());
                            }
                            current.Remark = string.Join(", ", errorList);
                            break;

                        case 15:
                            current!.MessageId = intValue;
                            current.Remark = ErrorMessages.Messages
                                .FirstOrDefault(m => m.Index == intValue)?.Message ?? string.Empty;
                            break;

                        case 16:
                            current!.ErrorId = intValue;
                            current.Remark = ErrorMessages.Errors
                                .FirstOrDefault(e => e.Index == intValue)?.Message ?? string.Empty;
                            break;

                        case 17:
                            current!.Remark = $"Custom Remark: {CommandTracker.GetCommand(ushort.Parse(current.Remark ?? "-1"), true)}";
                            break;

                        default:
                            // Unknown / unsupported opcode → ignore
                            break;
                    }
                }

                // Add the last record
                if (current != null)
                    RequestData.RealStoreRecord.Add(current);

                if (RequestData.RealStoreRecord.Count == 0)
                    return CommonResponse<recordStoreRequest>.Fail("No measurement data found in payload");

                return CommonResponse<recordStoreRequest>.Ok(RequestData, "Payload decoded successfully");
            }
            catch (Exception ex)
            {
                return CommonResponse<recordStoreRequest>.Fail($"RealStoreDataV2 failed: {ex.Message}");
            }
        }

        private static List<IOStatus> ParseIOBytes(byte[] bytes)
        {
            var list = new List<IOStatus>();

            try
            {
                if (bytes == null || bytes.Length != 3)
                    return list;

                // 1️⃣ Digital Inputs (Primary + Secondary)
                byte diByte = bytes[0];

                // Bits 7–4 → Primary DI3–DI0
                for (int bit = 4; bit < 8; bit++)
                {
                    list.Add(new IOStatus
                    {
                        Id = bit - 4,
                        Value = (diByte >> bit) & 1,
                        Board = "Primary",
                        Type = "DI"
                    });
                }

                // Bits 3–0 → Secondary DI3–DI0
                for (int bit = 0; bit < 4; bit++)
                {
                    list.Add(new IOStatus
                    {
                        Id = bit,
                        Value = (diByte >> bit) & 1,
                        Board = "Secondary",
                        Type = "DI"
                    });
                }

                // 2️⃣ Digital Outputs (Secondary board)
                byte doSecondary = bytes[1];
                for (int bit = 0; bit < 8; bit++)
                {
                    list.Add(new IOStatus
                    {
                        Id = bit,
                        Value = (doSecondary >> bit) & 1,
                        Board = "Secondary",
                        Type = "DO"
                    });
                }

                // 3️⃣ Digital Outputs (Primary board) — only DO0–DO2 valid
                byte doPrimary = bytes[2];
                for (int bit = 0; bit <= 2; bit++) // only 0–2
                {
                    list.Add(new IOStatus
                    {
                        Id = bit,
                        Value = (doPrimary >> bit) & 1,
                        Board = "Primary",
                        Type = "DO"
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"[ParseIOBytes] Error: {ex.Message}");
            }

            return list;
        }

        public static ManufacturingDetailDTO ManufacturingParameters(byte[] data)
        {
            int index = 4;

            var dto = new ManufacturingDetailDTO
            {

                MasterSWVersion = Encoding.ASCII.GetString(data, index, 11).TrimEnd('\0'),
                ComSWVersion = Encoding.ASCII.GetString(data, index += 11, 11).TrimEnd('\0'),
                SecondarySWVersion = Encoding.ASCII.GetString(data, index += 11, 11).TrimEnd('\0'),

                PrimarySerialNumber = BitConverter.ToUInt32(data.Skip(index += 11).Take(4).Reverse().ToArray(), 0).ToString(),
                SecondarySerialNumber = BitConverter.ToUInt32(data.Skip(index += 4).Take(4).Reverse().ToArray(), 0).ToString(),

                ManufactureDateTime = DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToUInt32(data.Skip(index += 4).Take(4).Reverse().ToArray(), 0)).UtcDateTime,
                CommissioningDateTime = DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToUInt32(data.Skip(index += 4).Take(4).Reverse().ToArray(), 0)).UtcDateTime,
                PrimaryPCBAssemblyDateTime = DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToUInt32(data.Skip(index += 4).Take(4).Reverse().ToArray(), 0)).UtcDateTime,
                SecondaryPCBAssemblyDateTime = DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToUInt32(data.Skip(index += 4).Take(4).Reverse().ToArray(), 0)).UtcDateTime

            };

            return dto;
        }
        public static FactoryConfigDetailDTO FactoryParameters(byte[] packet)
        {
            int index = 0;

            byte start = packet[index++];
            byte deviceId = packet[index++];
            var (_, _) = ChannelAddressCodec.Decode(packet[index++]); // header board/channel address, unused here
            byte queryId = packet[index++];

            // MAC ID (6 bytes)
            string mac = string.Join(":", packet.Skip(index).Take(6).Select(b => b.ToString("X2")));
            index += 6;

            // Device IP
            string deviceIp = new IPAddress(packet.Skip(index).Take(4).ToArray()).ToString();
            index += 4;

            // Client Remote IP
            string clientIp = new IPAddress(packet.Skip(index).Take(4).ToArray()).ToString();
            index += 4;

            // Ports
            int tcpPort = ReadUInt16BigEndian(packet, index);
            index += 2;

            int udpPort = ReadUInt16BigEndian(packet, index);
            index += 2;

            int udpStorePort = ReadUInt16BigEndian(packet, index);
            index += 2;

            // DHCP
            bool dhcp = packet[index++] == 0x01;

            // Circuit type
            byte circuitValue = packet[index++]; // Read only once
            string circuitType = circuitValue == 0
                ? "Single Transistor Bank"
                : circuitValue == 1
                    ? "Dual Transistor Bank"
                    : "Unknown";
            // Floats
            float znt = ReadSingleBigEndian(packet, index);
            index += 4;
            float lnt = ReadSingleBigEndian(packet, index);
            index += 4;
            float cMaxV = ReadSingleBigEndian(packet, index);
            index += 4;
            float cMinV = ReadSingleBigEndian(packet, index);
            index += 4;
            float cDis = ReadSingleBigEndian(packet, index);
            index += 4;
            float cChg = ReadSingleBigEndian(packet, index);
            index += 4;
            int crno = packet[index++];

            return new FactoryConfigDetailDTO
            {
                MacID = mac,
                DeviceIPAddress = deviceIp,
                ClientRemoteIPAddress = clientIp,
                TcpClientRemotePort = tcpPort,
                UdpClientRemotePort = udpPort,
                UdpStoreRemotePort = udpStorePort,
                DhcpEnabled = dhcp,
                CircuitType = circuitType,
                ZntMaxVoltage = znt,
                LntMaxVoltage = lnt,
                CircuitMaxVoltage = cMaxV,
                CircuitMinVoltage = cMinV,
                CircuitMaxDischargeCurrent = cDis,
                CircuitMaxChargeCurrent = cChg,
                CircuitNumber = crno
            };
        }
        public static BatteryDTO BatteryParameters(byte[] data)
        {
            var dto = new BatteryDTO();
            int index = 0;

            // Header: Start (0xAA), DeviceID (1B), Board/Channel address (1B), QueryID (1B)
            byte bpStart = data[index++];
            byte bpDeviceIdByte = data[index++];
            var (_, _) = ChannelAddressCodec.Decode(data[index++]);
            byte bpQueryId = data[index++];

            // Nominal Capacity (4 bytes)
            dto.NominalCapacity = ReadSingleBigEndian(data, index);
            index += 4;

            // Number of Cells (1 byte)
            dto.NumberOfCells = data[index++];

            // Gassing Voltage (4 bytes)
            dto.GassingVoltage = ReadSingleBigEndian(data, index);
            index += 4;

            // Maximum Voltage (4 bytes)
            dto.MaximumVoltage = ReadSingleBigEndian(data, index);
            index += 4;

            // Nominal Current (4 bytes)
            dto.NominalCurrent = ReadSingleBigEndian(data, index);
            index += 4;

            // Cold Cranking Current (4 bytes)
            dto.ColdCrankingCurrent = ReadSingleBigEndian(data, index);
            index += 4;

            // Charge Factor (1 byte)
            dto.ChargeFactor = data[index++];

            // Impedance (4 bytes)
            dto.Impedance = ReadSingleBigEndian(data, index);
            index += 4;

            // Break Voltage (4 bytes)
            dto.BreakVoltage = ReadSingleBigEndian(data, index);
            index += 4;

            // Nominal Voltage (4 bytes)
            dto.NominalVoltage = ReadSingleBigEndian(data, index);
            index += 4;

            // Energy Density (4 bytes)
            dto.EnergyDensity = ReadSingleBigEndian(data, index);
            index += 4;

            // Battery ID (2 bytes)
            dto.Id = ReadInt16BigEndian(data, index);
            index += 2;

            // Populate other fields with default/placeholder values
            dto.Name = $"Battery-{dto.Id}";
            dto.BatteryTypeId = 0;  // Not in packet
            dto.Quantity = 0;       // Not in packet
            dto.Comments = "";
            dto.Producer = "";

            return dto;
        }

        public static CalibrationData? ParseCalibrationPayload(byte[] data)
        {
            try
            {
                if (data == null || data.Length < 10)
                    return null;

                // ── Wire layout for 0x17 PreviousCalibration response ────────────
                //
                // [0]      Start  (0xA0)
                // [1]      Device
                // [2]      Circuit
                // [3]      QueryId  (0x17)
                // [4]      Error status
                //
                // ── RangeFull (bytes 5–52) ────────────────────────────────────────
                //  5– 8   Current  Charge   Gain
                //  9–12   Current  Charge   Offset
                // 13–16   Current  Charge   DateTime
                // 17–20   Current  Discharge Gain
                // 21–24   Current  Discharge Offset
                // 25–28   Current  Discharge DateTime
                // 29–32   Voltage  Charge   Gain
                // 33–36   Voltage  Charge   Offset
                // 37–40   Voltage  Charge   DateTime
                // 41–44   Voltage  Discharge Gain
                // 45–48   Voltage  Discharge Offset
                // 49–52   Voltage  Discharge DateTime
                //
                // ── Range1 (bytes 53–76) — Current only ──────────────────────────
                // 53–56   Current  Charge   Gain
                // 57–60   Current  Charge   Offset
                // 61–64   Current  Charge   DateTime
                // 65–68   Current  Discharge Gain
                // 69–72   Current  Discharge Offset
                // 73–76   Current  Discharge DateTime
                //
                // ── Range2 (bytes 77–100) — Current only ─────────────────────────
                // 77–80   Current  Charge   Gain
                // 81–84   Current  Charge   Offset
                // 85–88   Current  Charge   DateTime
                // 89–92   Current  Discharge Gain
                // 93–96   Current  Discharge Offset
                // 97–100  Current  Discharge DateTime
                //
                // ── Range3 (bytes 101–124) — Current only ────────────────────────
                // 101–104 Current  Charge   Gain
                // 105–108 Current  Charge   Offset
                // 109–112 Current  Charge   DateTime
                // 113–116 Current  Discharge Gain
                // 117–120 Current  Discharge Offset
                // 121–124 Current  Discharge DateTime
                //
                // ── Range4 (bytes 125–148) — Current only ────────────────────────
                // 125–128 Current  Charge   Gain
                // 129–132 Current  Charge   Offset
                // 133–136 Current  Charge   DateTime
                // 137–140 Current  Discharge Gain
                // 141–144 Current  Discharge Offset
                // 145–148 Current  Discharge DateTime
                //
                // ── Temperature (bytes 149–160) ───────────────────────────────────
                // 149–152 Temperature Gain
                // 153–156 Temperature Offset
                // 157–160 Temperature DateTime
                //
                // ── Footer ────────────────────────────────────────────────────────
                // 161–162 CRC16
                //
                // Total = 163 bytes

                int index = 0;

                byte start   = data[index++]; // 0
                byte device  = data[index++]; // 1
                var (calBoardNumber, calChannelNumber) = ChannelAddressCodec.Decode(data[index++]); // 2
                byte queryId = data[index++]; // 3
                byte errCode = data[index++]; // 4  ← error status byte

                var result = new CalibrationData();

                int dataEnd = data.Length - 2; // exclude 2-byte CRC

                // ── Primitive readers (advance index) ────────────────────────────
                float ReadFloat() { var v = ReadSingleBigEndian(data, index); index += 4; return v; }
                DateTime ReadDateTime() { var v = DateTimeOffset.FromUnixTimeSeconds((uint)ReadInt32BigEndian(data, index)).LocalDateTime; index += 4; return v; }

                // ── Reads one Charge + one Discharge block for the given range ───
                // Each pair = 24 bytes: [ChGain ChOffset ChTime DchGain DchOffset DchTime]
                void ReadCurrentRangePair(CalibrationRange range)
                {
                    if (index + 24 > dataEnd) return;

                    result.CurrentCharge.Add(new CalibrationDataPointDto
                    {
                        DeviceId  = device,
                        SecondaryBoardNumber = calBoardNumber,
                        ChannelId = calChannelNumber,
                        Type      = CalibrationType.Current,
                        Mode      = CalibrationMode.Charge,
                        Range     = range,
                        Gain      = ReadFloat(),
                        Offset    = ReadFloat(),
                        DateTime  = ReadDateTime()
                    });

                    result.CurrentDischarge.Add(new CalibrationDataPointDto
                    {
                        DeviceId  = device,
                        SecondaryBoardNumber = calBoardNumber,
                        ChannelId = calChannelNumber,
                        Type      = CalibrationType.Current,
                        Mode      = CalibrationMode.Discharge,
                        Range     = range,
                        Gain      = ReadFloat(),
                        Offset    = ReadFloat(),
                        DateTime  = ReadDateTime()
                    });
                }

                // ── RangeFull: Current Charge + Current Discharge + Voltage Charge + Voltage Discharge ──
                // 48 bytes (bytes 5–52)
                if (index + 48 <= dataEnd)
                {
                    // Current Charge (bytes 5–16)
                    result.CurrentCharge.Add(new CalibrationDataPointDto
                    {
                        DeviceId  = device, SecondaryBoardNumber = calBoardNumber, ChannelId = calChannelNumber,
                        Type      = CalibrationType.Current, Mode = CalibrationMode.Charge,
                        Range     = CalibrationRange.Full_Range,
                        Gain      = ReadFloat(), Offset = ReadFloat(), DateTime = ReadDateTime()
                    });

                    // Current Discharge (bytes 17–28)
                    result.CurrentDischarge.Add(new CalibrationDataPointDto
                    {
                        DeviceId  = device, SecondaryBoardNumber = calBoardNumber, ChannelId = calChannelNumber,
                        Type      = CalibrationType.Current, Mode = CalibrationMode.Discharge,
                        Range     = CalibrationRange.Full_Range,
                        Gain      = ReadFloat(), Offset = ReadFloat(), DateTime = ReadDateTime()
                    });

                    // Voltage Charge (bytes 29–40)
                    result.VoltageCharge = new CalibrationDataPointDto
                    {
                        DeviceId  = device, SecondaryBoardNumber = calBoardNumber, ChannelId = calChannelNumber,
                        Type      = CalibrationType.Voltage, Mode = CalibrationMode.Charge,
                        Range     = CalibrationRange.Full_Range,
                        Gain      = ReadFloat(), Offset = ReadFloat(), DateTime = ReadDateTime()
                    };

                    // Voltage Discharge (bytes 41–52)
                    result.VoltageDischarge = new CalibrationDataPointDto
                    {
                        DeviceId  = device, SecondaryBoardNumber = calBoardNumber, ChannelId = calChannelNumber,
                        Type      = CalibrationType.Voltage, Mode = CalibrationMode.Discharge,
                        Range     = CalibrationRange.Full_Range,
                        Gain      = ReadFloat(), Offset = ReadFloat(), DateTime = ReadDateTime()
                    };
                }

                // ── Range1 – Range4: Current only, 24 bytes each ─────────────────
                ReadCurrentRangePair(CalibrationRange.Range1); // bytes 53–76
                ReadCurrentRangePair(CalibrationRange.Range2); // bytes 77–100
                ReadCurrentRangePair(CalibrationRange.Range3); // bytes 101–124
                ReadCurrentRangePair(CalibrationRange.Range4); // bytes 125–148

                // ── Temperature: 12 bytes (bytes 149–160) ────────────────────────
                if (index + 12 <= dataEnd)
                {
                    result.Temperature = new CalibrationDataPointDto
                    {
                        DeviceId  = device, SecondaryBoardNumber = calBoardNumber, ChannelId = calChannelNumber,
                        Type      = CalibrationType.Current, // temperature sensor type
                        Mode      = CalibrationMode.Charge,  // not applicable but required
                        Range     = CalibrationRange.Full_Range,
                        Gain      = ReadFloat(),
                        Offset    = ReadFloat(),
                        DateTime  = ReadDateTime()
                    };
                }

                //_log.Debug("ParseCalibrationPayload: {Result}", JsonConvert.SerializeObject(result));
                return result;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error in ParseCalibrationPayload");
                return null;
            }
        }

        #endregion

        #region ModalToByte Helpers
        public static byte[] BuildBatteryBytes(BatteryDTO detailDTO)
        {
            List<byte> frame = new();

            frame.AddRange(GetFloatBytes((float)detailDTO.NominalCapacity));      // 4B
            frame.Add((byte)detailDTO.NumberOfCells);                             // 1B
            frame.AddRange(GetFloatBytes((float)detailDTO.GassingVoltage));       // 4B
            frame.AddRange(GetFloatBytes((float)detailDTO.MaximumVoltage));       // 4B
            frame.AddRange(GetFloatBytes((float)detailDTO.NominalCurrent));       // 4B
            frame.AddRange(GetFloatBytes((float)detailDTO.ColdCrankingCurrent));  // 4B
            frame.Add((byte)detailDTO.ChargeFactor);                              // 1B
            frame.AddRange(GetFloatBytes((float)detailDTO.Impedance));            // 4B
            frame.AddRange(GetFloatBytes((float)detailDTO.BreakVoltage));         // 4B
            frame.AddRange(GetFloatBytes((float)detailDTO.NominalVoltage));       // 4B
            frame.AddRange(GetFloatBytes((float)detailDTO.EnergyDensity));        // 4B

            frame.AddRange(GetInt16Bytes((short)detailDTO.Id));                   // 2B

            return frame.ToArray();
        }

        public static byte[] GetEpochTimeBytes(out int EpochSeconds, out DateTime? dt)
        {
            DateTime dateTime = DateTime.UtcNow;
            dt = dateTime;
            uint epochTime = (uint)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;

            EpochSeconds = (int)epochTime;

            byte[] epochBytes = BitConverter.GetBytes(epochTime);

            // Convert to Big-Endian if system is Little-Endian
            if (BitConverter.IsLittleEndian)
                Array.Reverse(epochBytes);

            return epochBytes; // Always 4 bytes
        }

        /// <summary>
        /// Builds a 4-byte session ID that is unique per channel even when multiple channels
        /// start in the same second (parallel start from the dashboard).
        ///
        /// Layout (Big-Endian, 4 bytes total):
        ///   Byte 0 : DeviceID (0–255)
        ///   Byte 1 : Board/Channel address (0–255)
        ///   Byte 2–3: Low 16 bits of Unix epoch-seconds
        ///
        /// This guarantees DB UNIQUE constraint is never violated by parallel starts,
        /// the 4-byte wire limit is respected, and the folder path
        /// ({SessionID}_{DeviceID}_{BoardNumber}_{ChannelID}.db) remains meaningful.
        /// </summary>
        public static byte[] GetSessionIdBytes(int deviceId, byte channelAddressByte, out int sessionId, out DateTime? dt)
        {
            DateTime dateTime = DateTime.UtcNow;
            dt = dateTime;

            uint epochSeconds = (uint)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;

            // Pack: [DeviceID 8-bit][Board/Channel address 8-bit][epoch low 16-bit]
            uint packed = ((uint)(deviceId & 0xFF) << 24)
                        | ((uint)channelAddressByte << 16)
                        | (epochSeconds & 0xFFFF);

            sessionId = (int)packed;

            byte[] bytes = BitConverter.GetBytes(packed);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return bytes; // Always 4 bytes
        }
      
        public static byte[] ParseRegistrationResponse(int DeviceID, byte channelAddressByte, CommandStatus status)
        {
            byte[] payload = new byte[5];
            payload[0] = 0xDD; // Start byte
            payload[1] = 0x01; // Query ID
            payload[2] = (byte)DeviceID;
            payload[3] = channelAddressByte;
            payload[4] = (byte)status;
            ushort crc = CalculateCRC16(payload, 0, payload.Length);
            payload = BindCRC16(payload, crc);
            return payload;
        }
        public static byte[] BuildRegistration(int DeviceID, byte channelAddressByte, byte QueryID, byte? status = null)
        {
            byte[] payload = new byte[5];
            payload[0] = 0xDD; // Start byte
            payload[1] = QueryID; // Query ID Delete Device
            payload[2] = (byte)DeviceID;
            payload[3] = channelAddressByte;
            
            if (status.HasValue)
                payload[4] = status.Value;

            ushort crc = CalculateCRC16(payload, 0, payload.Length);
            payload = BindCRC16(payload, crc);
            return payload;
        }
        public static List<byte[]> ConvertProgramBackup(List<StepModel> programStepsDTO)
        {
            List<byte[]> packets = new();

            if (programStepsDTO == null || programStepsDTO.Count == 0)
                return packets;

            List<byte[]> formattedSteps = new List<byte[]>();
           
            #region GlobalVariable
         
            List<GlobalVariable> globalVariables = new();

            foreach (var step in programStepsDTO)
            {
                if (step.OperatorCode == OperatorConstants.SET)
                {
                    foreach (var nomVal in step.NominalValues)
                    {
                        var veriable = ValidationHelper.ParseSetVariable(nomVal, step.Id);
                        if (veriable != null)
                            globalVariables.Add(veriable);
                    }
                }
            }
           
            #endregion
           
            foreach (var step in programStepsDTO)
            {
                List<byte> stepBytes = new List<byte>();

                ushort stepIdValue = (ushort)step.StepNumber;
                byte[] stepIdBytes = BitConverter.GetBytes(stepIdValue);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(stepIdBytes);

                stepBytes.AddRange(stepIdBytes);

                stepBytes.Add(step.OperatorCode);

                //SET Operator 
                if (step.OperatorCode == OperatorConstants.SET)
                {
                    //No of Global Limit Parameters
                    stepBytes.Add(0x00);

                    var dbloads = RStandards.GetStandards();

                    var defaultStandard = dbloads.FirstOrDefault(s => s.StandardName.Equals("STANDARD", StringComparison.OrdinalIgnoreCase));
                    var defaultUnits = defaultStandard?.UnitList ?? new List<string>(); // fallback empty list if somehow missing

                    // Find first matching registration
                    var matchedStandard = step.Registrations
                        .Select(r => dbloads.FirstOrDefault(s => s.StandardName.Equals(r, StringComparison.OrdinalIgnoreCase)))
                        .FirstOrDefault(s => s != null);

                    // Use units from matched standard, otherwise use default standard units
                    List<string> unitsToUse = matchedStandard?.UnitList ?? defaultUnits;

                    int unitCount = 0;

                    foreach (var unit in unitsToUse)
                    {
                        if (RStandards.unitValueMap.TryGetValue(unit, out int value))
                        {
                            unitCount += value;
                        }
                    }

                    unitCount += RStandards.unitValueMap["ERR_E"];
                    unitCount += RStandards.unitValueMap["MSG_E"];

                    byte[] RType = BitConverter.GetBytes((short)unitCount); // Use short to keep 2 bytes

                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(RType);

                    stepBytes.AddRange(RType); // Big-endian

                }

                // Table Operator 
                else if (step.OperatorCode == OperatorConstants.TABLE)
                {
                    if (step.NominalValues.Count > 0)
                    {
                        foreach (var item in step.NominalValues)
                        {
                            if (!string.IsNullOrEmpty(item))
                            {
                                var TableData = FileManagerService.ReadFileLines(item);

                                if (TableData.Success)
                                {
                                    var TableBytes = ProgramBuilder.BuildTableOPTxtToBinary(TableData.Data);

                                    if (TableBytes.Success)
                                    {
                                        stepBytes.AddRange(TableBytes.Data);
                                    }
                                }

                            }
                        }
                    }

                    if (step.Registrations.Count > 0)
                    {
                        stepBytes.Add((byte)step.Registrations.Count);

                        foreach (var item in step.Registrations)
                        {
                            if (!string.IsNullOrEmpty(item))
                            {
                                if (ProgramBuilder.TryParseRegistration(item, out byte Rgtype, out byte[]? value))
                                {
                                    stepBytes.Add(Rgtype);

                                    if (value != null)
                                    {
                                        stepBytes.AddRange(value);
                                    }
                                }
                                else
                                {
                                    stepBytes.Add(Rgtype);
                                }
                            }
                        }
                       
                    }
                    else
                    {
                        stepBytes.Add(0x00);
                    }
                }
                
                // PAU Operator 
                else if (step.OperatorCode == OperatorConstants.PAU)
                {
                    if (step.Limits.Count > 0)
                    {
                        for (int i = 0; i < step.Limits.Count; i++)
                        {
                            string item = step.Limits[i];

                            if (!string.IsNullOrEmpty(item))
                            {
                                if (string.IsNullOrWhiteSpace(item))
                                    continue;

                                var parts = item.Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries);

                                string valueStr = string.Empty;
                                string unit = null;

                                if (parts.Length == 2)
                                {
                                    var lvalue = globalVariables.FirstOrDefault(e => e.Name == parts[0]);
                                    
                                    if (lvalue !=null)
                                    {
                                        valueStr = lvalue.Value.ToString();
                                    }
                                    else
                                    {
                                        valueStr = parts[0];
                                        unit = parts[1];
                                    }

                                    if (unit == null)
                                        unit = parts[1];
                                }
                                else if (parts.Length == 1)
                                {
                                    var lvalue = globalVariables.FirstOrDefault(e => e.Name == parts[0]);

                                    if (lvalue != null)
                                    {
                                        valueStr = lvalue.Value.ToString();
                                    }
                                    else
                                    {
                                        valueStr = parts[0];
                                        unit = "s";
                                    }
                                }

                                if (float.TryParse(valueStr, out float value))
                                    stepBytes.AddRange(ProgramBuilder.ExtractFloatAsByteArraySafe($"{value} {unit}"));


                                // Ensure index exists in step.Actions; if missing → add empty
                                while (step.Actions.Count <= i)
                                    step.Actions.Add(string.Empty);

                                string action = step.Actions[i];

                                if (string.IsNullOrWhiteSpace(action))
                                {
                                    stepBytes.Add(0x00);
                                    continue;
                                }

                                if (ProgramBuilder.TryParseOpcodeFull(action, ProgramBuilder.ExtractLables(programStepsDTO), out byte opcode, out byte[]? extraByte))
                                {
                                    stepBytes.Add(opcode);
                                    if (extraByte != null && extraByte.Length > 0)
                                    {
                                        stepBytes.AddRange(extraByte);
                                    }
                                }
                            }
                        }
                    }

                    if (step.Registrations.Count > 0)
                    {
                        stepBytes.Add((byte)step.Registrations.Count);

                        foreach (var item in step.Registrations)
                        {
                            if (!string.IsNullOrEmpty(item))
                            {
                                var parts = item.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                                GlobalVariable? isGVariable = globalVariables
                                    .FirstOrDefault(e =>
                                        string.Equals(e.Name, parts[0], StringComparison.OrdinalIgnoreCase));

                                var tm = item;

                                if (isGVariable != null)
                                {
                                    var unit = isGVariable.Unit
                                                ?? (parts.Length > 1 ? parts[1] : string.Empty);

                                    tm = $"{isGVariable.Value} {unit}".Trim();
                                }

                                if (ProgramBuilder.TryParseRegistration(tm, out byte rgType, out byte[]? value))
                                {
                                    stepBytes.Add(rgType);

                                    if (value != null && value.Length > 0)
                                    {
                                        stepBytes.AddRange(value);
                                    }
                                }
                                
                            }
                        }
                    }
                    else
                    {
                       stepBytes.Add(0x00);
                    }

                }

                // STO Operator 
                else if (step.OperatorCode == OperatorConstants.STO)
                {
                    // No additional data for STO


                }
              
                // other operators
                else
                {
                    if (step.NominalValues.Count > 0)
                    {
                        foreach (var item in step.NominalValues)
                        {
                            if (!string.IsNullOrEmpty(item))
                            {
                                var gv = globalVariables.FirstOrDefault(e => e.Name == item);
                              
                                byte[] NominalValue = null;
                               
                                if (gv != null)
                                {
                                    NominalValue = ProgramBuilder.ExtractFloatAsByteArraySafe($"{gv.Value} {gv.Unit}");
                                }
                                else
                                {
                                    NominalValue = ProgramBuilder.ExtractFloatAsByteArraySafe(item);
                                }

                                if (NominalValue != null)
                                {
                                    stepBytes.AddRange(NominalValue);
                                }
                            }
                        }
                    }

                    if (step.Limits.Count > 0)
                    {
                        if (step.OperatorCode != 0x08)
                            stepBytes.Add((byte)step.Limits.Count);

                        for (int i = 0; i < step.Limits.Count; i++)
                        {
                            string item = step.Limits[i];

                            if (string.IsNullOrWhiteSpace(item))
                                continue;

                            var parts = item.Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries);

                            string op = "=";
                            string valueStr = string.Empty;
                            string unit = string.Empty;

                            if (ProgramBuilder.TryParseUnit(parts[0], out byte check))
                            {
                                op = parts[0];
                            }

                            if (parts.Length > 1)
                            {
                                var gv = globalVariables.FirstOrDefault(e =>
                                string.Equals(e.Name, parts[1], StringComparison.OrdinalIgnoreCase));

                                if (gv != null)
                                {
                                    valueStr = gv.Value.ToString();
                                    unit = gv.Unit; // may be null
                                }
                                else
                                {
                                    valueStr = parts[1]; // literal value
                                }
                            }

                            if (parts.Length > 2)
                            {
                                unit = parts[2];
                            }

                            // ---- UNIT ----
                            if (ProgramBuilder.TryParseUnit(unit, out byte unitByte) && step.OperatorCode != OperatorConstants.PAU)
                                stepBytes.Add(unitByte);

                            // ---- OPERATOR ----
                            if (ProgramBuilder.TryParseOperator(op, out byte opByte))
                                stepBytes.Add(opByte);

                            // ---- VALUE ----

                            if (float.TryParse(valueStr, out float value))
                                stepBytes.AddRange(ProgramBuilder.ExtractFloatAsByteArraySafe($"{value} {unit}"));


                            // ------------------------------------------------------------------
                            // ACTION HANDLING (Only when more than 1 limit)
                            // ------------------------------------------------------------------
                            if (step.Actions == null)
                                step.Actions = new List<string>();

                            // Ensure index exists in step.Actions; if missing → add empty
                            while (step.Actions.Count <= i)
                                step.Actions.Add(string.Empty);

                            string action = step.Actions[i];

                            if (string.IsNullOrWhiteSpace(action))
                            {
                                stepBytes.Add(0x00);
                                continue;
                            }

                            if (ProgramBuilder.TryParseOpcodeFull(action, ProgramBuilder.ExtractLables(programStepsDTO), out byte opcode, out byte[]? extraByte))
                            {
                                stepBytes.Add(opcode);
                               
                                if (extraByte != null && extraByte.Length > 0)
                                {
                                    stepBytes.AddRange(extraByte);
                                }
                            }
                        }

                    }

                    if (step.Registrations.Count > 0)
                    {
                        stepBytes.Add((byte)step.Registrations.Count);

                        foreach (var item in step.Registrations)
                        {
                            if (!string.IsNullOrEmpty(item))
                            {
                                var parts = item.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                                GlobalVariable? isGVariable = globalVariables
                                    .FirstOrDefault(e =>
                                        string.Equals(e.Name, parts[0], StringComparison.OrdinalIgnoreCase));

                                var tm = item;

                                if (isGVariable != null)
                                {
                                    var unit = isGVariable.Unit
                                                ?? (parts.Length > 1 ? parts[1] : string.Empty);

                                    tm = $"{isGVariable.Value} {unit}".Trim();
                                }

                                if (ProgramBuilder.TryParseRegistration(tm, out byte rgType, out byte[]? value))
                                {
                                    stepBytes.Add(rgType);

                                    if (value != null && value.Length > 0)
                                    {
                                        stepBytes.AddRange(value);
                                    }
                                }

                            }
                        }
                    }
                 
                    else
                    {
                        stepBytes.Add(0x00);
                    }
                }

                formattedSteps.Add(stepBytes.ToArray());

            }

            int StepOffset = 0;

            for (int index = 0; index < formattedSteps.Count; index++)
            {
                var NewStep = formattedSteps[index];
                bool isLastStep = (index == formattedSteps.Count - 1); // last step check for null 
                StepOffset += NewStep.Length + 8;

                byte[] stepCountBytes = isLastStep
                    ? new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }
                    : BitConverter.GetBytes(StepOffset);

                List<byte> modified = new();
                modified.Add(0xAA);
                modified.Add(0x55);

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(stepCountBytes);
                }

                modified.AddRange(stepCountBytes);
                modified.AddRange(NewStep);
                modified.Add(0x55);
                modified.Add(0xAA);

                packets.Add(modified.ToArray());
            }

            return packets;               

        }

        public static List<byte[]> ConvertProgramIntoBytesPackets(List<StepModel> programStepsDTO, BatteryDTO? battery = null)
        {
            List<byte[]> packets = new();

            if (programStepsDTO == null || programStepsDTO.Count == 0)
                return packets;

            List<byte[]> formattedSteps = new List<byte[]>();

            List<GlobalVariable> globalVariables = ResolveGlobalVariables(programStepsDTO, battery);

            foreach (var step in programStepsDTO)
            {
                formattedSteps.Add(FormatStepBytes(step, globalVariables, programStepsDTO, battery));
            }

            return ProgramBuilder.BuildPackets(formattedSteps);
        }

        // Extracted from ConvertProgramIntoBytesPackets so Q9 (BuildLiveStepUpdatePacket) encodes a
        // single step through the exact same switch - the wire format must never drift between the
        // two, since the hardware treats a Q9 payload as byte-identical to what Q4 carries.
        private static byte[] FormatStepBytes(StepModel step, List<GlobalVariable> globalVariables, List<StepModel> programStepsDTO, BatteryDTO? battery)
        {
            List<byte> stepBytes = new List<byte>();

            // Add step ID
            ProgramBuilder.AddStepId(stepBytes, step.StepNumber);

            // Add operator code
            stepBytes.Add(step.OperatorCode);

            // Process based on operator type
            switch (step.OperatorCode)
            {
                case OperatorConstants.SET:
                    ProgramBuilder.ProcessSetOperator(stepBytes, step, programStepsDTO);
                    break;

                case OperatorConstants.REG:
                    ProgramBuilder.ProcessRegOperator(stepBytes, step, programStepsDTO);
                    break;

                case OperatorConstants.TABLE:
                    ProgramBuilder.ProcessTableOperator(stepBytes, step, battery);
                    break;

                case OperatorConstants.PAU:
                    ProgramBuilder.ProcessPauOperator(stepBytes, step, globalVariables, programStepsDTO, battery);
                    break;

                case OperatorConstants.GOTO:
                    ProgramBuilder.ProcessGotoOperator(stepBytes, step, globalVariables, programStepsDTO, battery);
                    break;

                case OperatorConstants.LOCKAH:
                    ProgramBuilder.ProcessLockAhOperator(stepBytes, step);
                    break;

                case OperatorConstants.STO:
                    // No additional data for STO
                    break;

                default:
                    ProgramBuilder.ProcessDefaultOperator(stepBytes, step, globalVariables, programStepsDTO, battery);
                    break;
            }

            return stepBytes.ToArray();
        }

        private static List<GlobalVariable> ResolveGlobalVariables(List<StepModel> programStepsDTO, BatteryDTO? battery)
        {
            List<GlobalVariable> globalVariables = ProgramBuilder.ExtractGlobalVariables(programStepsDTO);

            // §12.3 "Using Battery Parameters" — inject the battery's own fields (CNom, INom,
            // UGas, ...) as ordinary GlobalVariables. A SET-defined variable with the same name
            // always wins (skip on collision) — user intent overrides the implicit battery value.
            if (battery != null)
            {
                var batteryVariables = BatteryUnitResolver.GetBatteryGlobalVariables(battery);
                globalVariables.AddRange(batteryVariables.Where(bv =>
                    !globalVariables.Any(gv => string.Equals(gv.Name, bv.Name, StringComparison.OrdinalIgnoreCase))));
            }

            return globalVariables;
        }

        /// <summary>
        /// Q9 ("Live Step Update"): encodes ONE step exactly as ConvertProgramIntoBytesPackets would,
        /// then wraps it the same way ProgramBuilder.BuildPackets wraps the last step of a program -
        /// a single-element list always gets the isLastStep branch, so the 4-byte next-packet-offset
        /// field comes out as the TERMINATOR_INDEX (0xFFFFFFFF) sentinel the protocol requires for a
        /// standalone one-step payload. <paramref name="fullProgramSteps"/> is the whole resident
        /// program (for GlobalVariable/label resolution context) - <paramref name="targetStep"/> is
        /// the one step actually being amended.
        /// </summary>
        public static byte[] BuildLiveStepUpdatePacket(StepModel targetStep, List<StepModel> fullProgramSteps, BatteryDTO? battery = null)
        {
            List<GlobalVariable> globalVariables = ResolveGlobalVariables(fullProgramSteps, battery);
            byte[] stepBytes = FormatStepBytes(targetStep, globalVariables, fullProgramSteps, battery);
            return ProgramBuilder.BuildPackets(new List<byte[]> { stepBytes })[0];
        }

        #endregion

        #region Convertion Helpers

        public static double ReadDoubleBigEndian(byte[] data, int index)
        {
            // read 8 bytes
            byte[] bytes = data.Skip(index).Take(8).ToArray();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);

        }
        public static ushort ReadUInt16BigEndian(byte[] data, int index)
        {
            // Reads 2 bytes
            byte[] bytes = data.Skip(index).Take(2).ToArray();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        public static short ReadInt16BigEndian(byte[] data, int index)
        {
            // Reads 2 bytes
            byte[] bytes = data.Skip(index).Take(2).ToArray();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        public static int ReadInt32BigEndian(byte[] data, int index)
        {
            // Reads 4 bytes
            byte[] bytes = data.Skip(index).Take(4).ToArray();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public static float ReadSingleBigEndian(byte[] data, int index)
        {
            // Reads 4 bytes
            byte[] bytes = data.Skip(index).Take(4).ToArray();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        public static byte[] GetFloatBytes(float value)
        {
            // Returns 4 bytes
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes; // 4 bytes
        }

        public static byte[] GetInt16Bytes(short value)
        {
            // Returns 2 bytes
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes; // 2 bytes
        }

        public static byte[] GetInt32Bytes(int value)
        {
            // Returns 4 bytes
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes; // 4 bytes
        }


        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Broadcast (Scope / SADP — 0xDD queries on UDP :10003 / :10004)
        // ─────────────────────────────────────────────────────────────────────
        //
        //  Build helpers:  BuildQ3()  BuildQ4()  BuildQ5()
        //  Decode helpers: DecodeQ3Response()  DecodeQ4Response()  DecodeQ5Response()
        //
        //  All decoders call TryDecode<T> internally — results come back as
        //  CommonResponse<T> so callers get uniform success / failure / data.
        //
        //  Frame format shared by all broadcast packets:
        //    [0]      0xDD  (Start / Identity)
        //    [1]      QueryId
        //    [2..]    Payload fields (big-endian, 4 bytes per IP / port)
        //    [last-1] CRC high byte
        //    [last]   CRC low byte
        // ─────────────────────────────────────────────────────────────────────

        // ── Q3 ── "Is any device available?" broadcast ───────────────────────
        /// <summary>
        /// Builds a Q3 broadcast frame: DD 03 [CRC-2].
        /// Send to 255.255.255.255:10003; devices reply on :10004.
        /// </summary>
        public static byte[] DeviceDiscovery()
        {
            byte[] body = { 0xDD, 0x03 };
            ushort crc = CalculateCRC16(body, 0, body.Length);
            return BindCRC16(body, crc);
        }

        /// <summary>
        /// Decodes a Q3 response (device announcement) received on :10004.
        /// Returns CommonResponse with a <see cref="ScopeDeviceInfo"/> on success.
        /// </summary>
        public static CommonResponse<BroadcastDeviceInfo> DecodeDeviceDiscovery(byte[] payload)
        {
            // Delegate CRC + length guard to TryDecode, then do field extraction
            // Expected length: 1(start) + 1(qid) + 40(fields) + 2(crc) = 44
            var guard = TryDecode<bool>(payload);   // validates start byte + CRC
            if (!guard.Success && guard.Message.StartsWith("CRC"))
                return CommonResponse<BroadcastDeviceInfo>.Fail(guard.Message);

            if (payload == null || payload.Length < 44)
                return CommonResponse<BroadcastDeviceInfo>.Fail("Q3 response too short.");

            if (payload[0] != (byte)StartByte.Registration || payload[1] != 0x03)
                return CommonResponse<BroadcastDeviceInfo>.Fail("Not a Q3 response.");

            try
            {
                int o = 2;
                var info = new BroadcastDeviceInfo
                {
                    UniqueId = payload[o..(o + 4)],
                    RemoteIp = ReadIpString(payload, o += 4),
                    TcpPort = ReadInt32BigEndian(payload, o += 4),
                    DeviceIp = ReadIpString(payload, o += 4),
                    SubnetMask = ReadIpString(payload, o += 4),
                    Gateway = ReadIpString(payload, o += 4),
                    Dns1 = ReadIpString(payload, o += 4),
                    Dns2 = ReadIpString(payload, o += 4),
                    UdpLivePort = ReadInt32BigEndian(payload, o += 4),
                    UdpRegPort = ReadInt32BigEndian(payload, o + 4),
                };
                return CommonResponse<BroadcastDeviceInfo>.Ok(info);
            }
            catch (Exception ex)
            {
                return CommonResponse<BroadcastDeviceInfo>.Fail($"Q3 parse error: {ex.Message}");
            }
        }

        // ── Q4 ── Set device IP configuration ────────────────────────────────
        /// <summary>
        /// Builds a Q4 frame: DD 04 [uid 4] [devIP 4] [mask 4] [gw 4] [dns1 4] [dns2 4] [CRC-2].
        /// Send to 255.255.255.255:10003.
        /// </summary>
        public static byte[] ChangeNetConfig(BroadcastIpConfig cfg)
        {
            var body = new List<byte> { 0xDD, 0x04 };
            body.AddRange(cfg.UniqueId);
            body.AddRange(IpToBytes(cfg.DeviceIp));
            body.AddRange(IpToBytes(cfg.SubnetMask));
            body.AddRange(IpToBytes(cfg.Gateway));
            body.AddRange(IpToBytes(cfg.Dns1));
            body.AddRange(IpToBytes(cfg.Dns2));
            byte[] raw = body.ToArray();
            ushort crc = CalculateCRC16(raw, 0, raw.Length);
            return BindCRC16(raw, crc);
        }

        /// <summary>
        /// Decodes a Q4 response: DD 04 [0x01=ok | 0x00=fail] [CRC-2].
        /// Uses TryDecode to validate the Registration (0xDD) frame header + CRC,
        /// then reads the single success byte.
        /// </summary>
        public static CommonResponse<bool> DecodeChangeNetConfig(byte[] payload)
        {
            // TryDecode already handles 0xDD identity + CRC for Registration bytes
            var result = TryDecode<bool>(payload);
            if (!result.Success)
                return CommonResponse<bool>.Fail(result.Message);

            if (payload[1] != 0x04)
                return CommonResponse<bool>.Fail("Not a Q4 response.");

            bool ok = payload[2] == 0x01;
            return ok
                ? CommonResponse<bool>.Ok(true, "IP configuration applied.")
                : CommonResponse<bool>.Fail("Device rejected IP configuration.");
        }

        // ── Q5 ── Set server / remote configuration ───────────────────────────
        /// <summary>
        /// Builds a Q5 frame: DD 05 [uid 4] [remIP 4] [tcpPort 4] [udpLive 4] [udpReg 4] [CRC-2].
        /// Send to 255.255.255.255:10003.
        /// </summary>
        public static byte[] ChangeServerConfiguration(BroadcastServerConfig cfg)
        {
            var body = new List<byte> { 0xDD, 0x05 };
            body.AddRange(cfg.UniqueId);
            body.AddRange(IpToBytes(cfg.RemoteIp));
            body.AddRange(GetInt32Bytes(cfg.TcpPort));
            body.AddRange(GetInt32Bytes(cfg.UdpLivePort));
            body.AddRange(GetInt32Bytes(cfg.UdpRegPort));
            byte[] raw = body.ToArray();
            ushort crc = CalculateCRC16(raw, 0, raw.Length);
            return BindCRC16(raw, crc);
        }

        /// <summary>
        /// Decodes a Q5 response: DD 05 [0x01=ok | 0x00=fail] [CRC-2].
        /// </summary>
        public static CommonResponse<bool> DecodeChangeServerConfiguration(byte[] payload)
        {
            var result = TryDecode<bool>(payload);
            if (!result.Success)
                return CommonResponse<bool>.Fail(result.Message);

            if (payload[1] != 0x05)
                return CommonResponse<bool>.Fail("Not a Q5 response.");

            bool ok = payload[2] == 0x01;
            return ok
                ? CommonResponse<bool>.Ok(true, "Server configuration applied.")
                : CommonResponse<bool>.Fail("Device rejected server configuration.");
        }

        // ── Private helpers (broadcast region only) ───────────────────────────

        private static string ReadIpString(byte[] data, int offset)
            => $"{data[offset]}.{data[offset + 1]}.{data[offset + 2]}.{data[offset + 3]}";

        private static byte[] IpToBytes(string ip)
        {
            var parts = ip.Split('.');
            return new[] { byte.Parse(parts[0]), byte.Parse(parts[1]), byte.Parse(parts[2]), byte.Parse(parts[3]) };
        }

        #endregion

        #region PRODUCER-aware program conversion

        /// <summary>
        /// Converts a program step list to hardware byte packets, expanding any PRODUCER steps
        /// by inlining the referenced sub-program's core steps (skipping SET + STO at edges).
        /// </summary>
        /// <param name="programStepsDTO">The outer program steps.</param>
        /// <param name="resolvedPrograms">
        ///   Dictionary of program-name → parsed steps for every PRODUCER reference found in
        ///   the outer program. Pass null or empty to skip expansion (same as the base overload).
        /// </param>
        public static List<byte[]> ConvertProgramIntoBytesPackets(
            List<StepModel> programStepsDTO,
            Dictionary<string, List<StepModel>>? resolvedPrograms,
            BatteryDTO? battery = null)
        {
            var expanded = ProgramBuilder.ExpandProducerSteps(programStepsDTO, resolvedPrograms);
            return ConvertProgramIntoBytesPackets(expanded, battery);
        }

        #endregion

    }
}