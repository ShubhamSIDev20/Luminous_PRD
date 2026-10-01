using BatteryTestingSystem.Components.UI;
using System.Text;

namespace BatteryTestingSystem.Services
{
    public static class PacketAnalyzerNoReverse
    {
        /// <summary>
        /// Analyzes all packets and returns detailed breakdown
        /// </summary>
        public static List<PacketAnalysis> AnalyzePackets(List<byte[]> packets, List<StepModel> originalSteps = null)
        {
            List<PacketAnalysis> analyses = new();

            for (int i = 0; i < packets.Count; i++)
            {
                var analysis = AnalyzeSinglePacket(packets[i], i, originalSteps?.ElementAtOrDefault(i));
                analyses.Add(analysis);
            }

            return analyses;
        }

        /// <summary>
        /// Analyzes a single packet byte by byte
        /// </summary>
        public static PacketAnalysis AnalyzeSinglePacket(byte[] packet, int packetIndex, StepModel originalStep = null)
        {
            var analysis = new PacketAnalysis
            {
                PacketIndex = packetIndex,
                RawHex = BitConverter.ToString(packet).Replace("-", " ")
            };

            int position = 0;

            try
            {
                // Parse Header (0xAA 0x55)
                analysis.Header = $"Header: 0x{packet[position]:X2} 0x{packet[position + 1]:X2}";
                analysis.DetailedBreakdown.Add($"[Pos {position}-{position + 1}] Header: 0x{packet[position]:X2} 0x{packet[position + 1]:X2}");
                position += 2;

                // Parse Step Offset (4 bytes) - Read as stored in packet
                uint offset = ReadUInt32(packet, position);
                string offsetStr = offset == 0xFFFFFFFF ? "LAST STEP (0xFFFFFFFF)" : $"Next Offset: {offset} (0x{offset:X8})";
                analysis.DetailedBreakdown.Add($"[Pos {position}-{position + 3}] Step Offset: {offsetStr} [Bytes: {packet[position]:X2} {packet[position + 1]:X2} {packet[position + 2]:X2} {packet[position + 3]:X2}]");
                position += 4;

                // Parse Step ID (2 bytes) - Read as stored in packet
                ushort stepId = ReadUInt16(packet, position);
                analysis.StepId = $"Step ID: {stepId} (0x{packet[position]:X2} 0x{packet[position + 1]:X2})";
                analysis.DetailedBreakdown.Add($"[Pos {position}-{position + 1}] {analysis.StepId}");
                position += 2;

                // Parse Operator Code (1 byte)
                byte operatorCode = packet[position];
                string operatorName = GetOperatorName(operatorCode);
                analysis.Operator = $"Operator: {operatorName} (0x{operatorCode:X2})";
                analysis.DetailedBreakdown.Add($"[Pos {position}] {analysis.Operator}");
                position += 1;

                // Parse based on operator type
                if (operatorCode == OperatorConstants.SET)
                {
                    position = ParseSetOperator(packet, position, analysis);
                }
                else if (operatorCode == OperatorConstants.TABLE)
                {
                    position = ParseTableOperator(packet, position, analysis, originalStep);
                }
                else if (operatorCode == OperatorConstants.PAU)
                {
                    position = ParsePauOperator(packet, position, analysis, originalStep);
                }
                else if (operatorCode == OperatorConstants.STO)
                {
                    analysis.DetailedBreakdown.Add($"[Pos {position}] STO Operator - No additional data");
                }
                else
                {
                    position = ParseDefaultOperator(packet, position, analysis, originalStep, operatorCode);
                }

                // Parse Footer (0x55 0xAA)
                analysis.Footer = $"Footer: 0x{packet[packet.Length - 2]:X2} 0x{packet[packet.Length - 1]:X2}";
                analysis.DetailedBreakdown.Add($"[Pos {packet.Length - 2}-{packet.Length - 1}] {analysis.Footer}");

            }
            catch (Exception ex)
            {
                analysis.DetailedBreakdown.Add($"ERROR at position {position}: {ex.Message}");
            }

            return analysis;
        }

        #region Manual Byte Reading Methods (No Reverse)

        /// <summary>
        /// Read UInt32 directly from bytes as they are stored
        /// </summary>
        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        /// <summary>
        /// Read UInt16 directly from bytes as they are stored
        /// </summary>
        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        /// <summary>
        /// Read Int16 directly from bytes as they are stored
        /// </summary>
        private static short ReadInt16(byte[] data, int offset)
        {
            return (short)((data[offset] << 8) | data[offset + 1]);
        }

        /// <summary>
        /// Read float (4 bytes) directly from bytes as they are stored
        /// </summary>
        private static float ReadFloat(byte[] data, int offset)
        {
            // Read bytes directly without reversing
            byte[] floatBytes = new byte[4];
            Array.Copy(data, offset, floatBytes, 0, 4);
            return BitConverter.ToSingle(floatBytes, 0);
        }

        #endregion

        #region Operator Parsers

        private static int ParseSetOperator(byte[] packet, int position, PacketAnalysis analysis)
        {
            // Global Limit Parameters count
            byte globalLimitCount = packet[position];
            analysis.DetailedBreakdown.Add($"[Pos {position}] Global Limit Parameters: {globalLimitCount} (0x{globalLimitCount:X2})");
            position++;

            // R-Type (2 bytes) - Read as stored
            short rType = ReadInt16(packet, position);
            analysis.DetailedBreakdown.Add($"[Pos {position}-{position + 1}] R-Type Unit Count: {rType} (0x{packet[position]:X2} 0x{packet[position + 1]:X2})");
            position += 2;

            return position;
        }

        private static int ParseTableOperator(byte[] packet, int position, PacketAnalysis analysis, StepModel originalStep)
        {
            analysis.DetailedBreakdown.Add($"[Pos {position}] TABLE Operator - Table data section");

            // TABLE data would be here - skip to registration count
            int regCountPos = packet.Length - 3;

            while (regCountPos > position && packet[regCountPos] > 0x20)
            {
                regCountPos--;
            }

            if (regCountPos > position)
            {
                byte[] tableData = new byte[regCountPos - position];
                Array.Copy(packet, position, tableData, 0, tableData.Length);
                analysis.DetailedBreakdown.Add($"[Pos {position}-{regCountPos - 1}] Table Data: {BitConverter.ToString(tableData).Replace("-", " ")}");
            }

            position = ParseRegistrations(packet, regCountPos, analysis, originalStep);

            return position;
        }

        private static int ParsePauOperator(byte[] packet, int position, PacketAnalysis analysis, StepModel originalStep)
        {
            int limitCount = originalStep?.Limits.Count ?? 0;

            analysis.DetailedBreakdown.Add($"[Pos {position}] PAU Operator - Processing {limitCount} limits");

            for (int i = 0; i < limitCount && position < packet.Length - 2; i++)
            {
                int limitStartPos = position;

                // Parse float value (4 bytes) - Read as stored
                if (position + 4 <= packet.Length - 2)
                {
                    float value = ReadFloat(packet, position);
                    byte[] floatBytes = new byte[4];
                    Array.Copy(packet, position, floatBytes, 0, 4);

                    string limitStr = $"Limit {i + 1}: {value:F6} (Hex: {BitConverter.ToString(floatBytes).Replace("-", " ")})";
                    analysis.Limits.Add(limitStr);
                    analysis.DetailedBreakdown.Add($"[Pos {position}-{position + 3}] {limitStr}");
                    position += 4;
                }

                // Parse action (1 or 2 bytes)
                if (position < packet.Length - 2)
                {
                    int actionStartPos = position;
                    byte actionCode = packet[position];
                    string actionName = GetActionName(actionCode);
                    string actionStr = $"Action {i + 1}: {actionName} (0x{actionCode:X2})";
                    position++;

                    if (actionCode == 0x20 || actionCode == 0x21)
                    {
                        if (position < packet.Length - 2)
                        {
                            byte extraByte = packet[position];
                            actionStr += $" -> Step {extraByte} (0x{extraByte:X2})";
                            position++;
                        }
                    }

                    analysis.Actions.Add(actionStr);
                    analysis.DetailedBreakdown.Add($"[Pos {actionStartPos}-{position - 1}] {actionStr}");
                }
            }

            if (position < packet.Length - 2)
            {
                position = ParseRegistrations(packet, position, analysis, originalStep);
            }

            return position;
        }

        private static int ParseDefaultOperator(byte[] packet, int position, PacketAnalysis analysis,
            StepModel originalStep, byte operatorCode)
        {
            int nominalCount = originalStep?.NominalValues.Count ?? 0;
            int limitCount = originalStep?.Limits.Count ?? 0;

            // Parse Nominal Values
            analysis.DetailedBreakdown.Add($"[Pos {position}] Processing {nominalCount} nominal values");
            for (int i = 0; i < nominalCount && position + 4 <= packet.Length - 2; i++)
            {
                float value = ReadFloat(packet, position);
                byte[] floatBytes = new byte[4];
                Array.Copy(packet, position, floatBytes, 0, 4);

                string nomStr = $"Nominal {i + 1}: {value:F6} (Hex: {BitConverter.ToString(floatBytes).Replace("-", " ")})";
                analysis.NominalValues.Add(nomStr);
                analysis.DetailedBreakdown.Add($"[Pos {position}-{position + 3}] {nomStr}");
                position += 4;
            }

            // Parse Limit Count
            if (limitCount > 0 && operatorCode != 0x08 && position < packet.Length - 2)
            {
                byte limitCountByte = packet[position];
                analysis.DetailedBreakdown.Add($"[Pos {position}] Limit Count: {limitCountByte} (0x{limitCountByte:X2})");
                position++;
            }

            // Parse Limits
            analysis.DetailedBreakdown.Add($"[Pos {position}] Processing {limitCount} limits");
            for (int i = 0; i < limitCount && position < packet.Length - 2; i++)
            {
                int limitStartPos = position;
                StringBuilder limitInfo = new StringBuilder();
                limitInfo.Append($"Limit {i + 1}: ");

                // Unit byte (CutoffCondition)
                if (operatorCode != OperatorConstants.PAU && position < packet.Length - 2)
                {
                    byte unitByte = packet[position];
                    string unitName = GetCutoffConditionName(unitByte);
                    limitInfo.Append($"Unit={unitName} (0x{unitByte:X2}) ");
                    position++;
                }

                // Operator byte (LogicOperator)
                if (position < packet.Length - 2)
                {
                    byte opByte = packet[position];
                    string opName = GetLogicOperatorName(opByte);
                    limitInfo.Append($"Op={opName} (0x{opByte:X2}) ");
                    position++;
                }

                // Value (4 bytes float)
                if (position + 4 <= packet.Length - 2)
                {
                    float value = ReadFloat(packet, position);
                    byte[] floatBytes = new byte[4];
                    Array.Copy(packet, position, floatBytes, 0, 4);

                    limitInfo.Append($"Value={value:F6} (Hex: {BitConverter.ToString(floatBytes).Replace("-", " ")})");
                    position += 4;
                }

                analysis.Limits.Add(limitInfo.ToString());
                analysis.DetailedBreakdown.Add($"[Pos {limitStartPos}-{position - 1}] {limitInfo}");

                // Parse action
                if (position < packet.Length - 2)
                {
                    int actionStartPos = position;
                    byte actionCode = packet[position];
                    string actionName = GetActionName(actionCode);
                    string actionStr = $"Action {i + 1}: {actionName} (0x{actionCode:X2})";
                    position++;

                    if (actionCode == 0x20 || actionCode == 0x21)
                    {
                        if (position < packet.Length - 2)
                        {
                            byte extraByte = packet[position];
                            actionStr += $" -> Step {extraByte} (0x{extraByte:X2})";
                            position++;
                        }
                    }

                    analysis.Actions.Add(actionStr);
                    analysis.DetailedBreakdown.Add($"[Pos {actionStartPos}-{position - 1}] {actionStr}");
                }
            }

            // Parse Registrations
            if (position < packet.Length - 2)
            {
                position = ParseRegistrations(packet, position, analysis, originalStep);
            }

            return position;
        }

        private static int ParseRegistrations(byte[] packet, int position, PacketAnalysis analysis, StepModel originalStep)
        {
            if (position >= packet.Length - 2)
                return position;

            byte regCount = packet[position];
            analysis.DetailedBreakdown.Add($"[Pos {position}] Registration Count: {regCount} (0x{regCount:X2})");
            position++;

            for (int i = 0; i < regCount && position < packet.Length - 2; i++)
            {
                int regStartPos = position;

                byte regType = packet[position];
                string regTypeName = GetRegistrationTypeName(regType);
                StringBuilder regInfo = new StringBuilder();
                regInfo.Append($"Registration {i + 1}: Type={regTypeName} (0x{regType:X2})");
                position++;

                // Parse registration value (4 bytes float)
                if (position + 4 <= packet.Length - 2)
                {
                    float value = ReadFloat(packet, position);
                    byte[] valueBytes = new byte[4];
                    Array.Copy(packet, position, valueBytes, 0, 4);

                    regInfo.Append($" Value={value:F6} (Hex: {BitConverter.ToString(valueBytes).Replace("-", " ")})");
                    position += 4;
                }

                analysis.Registrations.Add(regInfo.ToString());
                analysis.DetailedBreakdown.Add($"[Pos {regStartPos}-{position - 1}] {regInfo}");
            }

            return position;
        }

        #endregion

        #region Helper Methods for Decoding

        private static string GetOperatorName(byte operatorCode)
        {
            return operatorCode switch
            {
                OperatorConstants.CC_CHG => "CC_CHG",
                OperatorConstants.CV_CHG => "CV_CHG",
                OperatorConstants.CP_CHG => "CP_CHG",
                OperatorConstants.CCCV_CHG => "CCCV_CHG",
                OperatorConstants.CC_DCHG => "CC_DCHG",
                OperatorConstants.CP_DCHG => "CP_DCHG",
                OperatorConstants.CCCV_DCHG => "CCCV_DCHG",
                OperatorConstants.PAU => "PAU",
                OperatorConstants.GOTO => "GOTO",
                OperatorConstants.SET => "SET",
                OperatorConstants.STO => "STO",
                OperatorConstants.CYC => "CYC",
                OperatorConstants.BEG => "BEG",
                OperatorConstants.INT => "INT",
                OperatorConstants.REG => "REG",
                OperatorConstants.ERR => "ERR",
                OperatorConstants.MSG => "MSG",
                OperatorConstants.TABLE => "TABLE",
                OperatorConstants.CV_DCHG => "CV_DCHG",
                _ => $"UNKNOWN (0x{operatorCode:X2})"
            };
        }

        private static string GetCutoffConditionName(byte unitByte)
        {
            return unitByte switch
            {
                0x31 => "Current (A)",
                0x32 => "Voltage (V)",
                0x33 => "Power (W)",
                0x34 => "ChargeCapacity (AhCha)",
                0x35 => "DischargeCapacity (AhDch)",
                0x36 => "ChargeEnergy (WhCha)",
                0x37 => "DischargeEnergy (WhDch)",
                0x38 => "Temperature (°C)",
                0x39 => "Time (s/m/h)",
                0x3A => "AccumulatedCapacity (Ah)",
                0x3B => "StepCapacity (AhStep)",
                0x3C => "AccumulatedEnergy (Wh)",
                0x3D => "StepEnergy (WhStep)",
                _ => $"UNKNOWN (0x{unitByte:X2})"
            };
        }

        private static string GetLogicOperatorName(byte opByte)
        {
            return opByte switch
            {
                0x51 => "> (GreaterThan)",
                0x52 => "< (LessThan)",
                0x53 => ">= (GreaterThanOrEqual)",
                0x54 => "<= (LessThanOrEqual)",
                0x55 => "!= (NotEqual)",
                0x56 => "= (Equal)",
                _ => $"UNKNOWN (0x{opByte:X2})"
            };
        }

        private static string GetRegistrationTypeName(byte regType)
        {
            return regType switch
            {
                0xF0 => "STANDARD",
                0x21 => "Time",
                0x22 => "Current",
                0x23 => "Voltage",
                0x24 => "Temperature",
                0x25 => "Power",
                0x26 => "AccumulatedCapacity",
                0x27 => "ChargeCapacity",
                0x28 => "DischargeCapacity",
                0x29 => "StepCapacity",
                0x2A => "AccumulatedEnergy",
                0x2B => "ChargeEnergy",
                0x2C => "DischargeEnergy",
                0x2D => "StepEnergy",
                _ => $"UNKNOWN (0x{regType:X2})"
            };
        }

        private static string GetActionName(byte actionCode)
        {
            return actionCode switch
            {
                0x00 => "NONE/CONTINUE",
                0x20 => "GOTO",
                0x21 => "CALL",
                0x30 => "STOP",
                0x31 => "NEXT",
                _ => $"UNKNOWN (0x{actionCode:X2})"
            };
        }

        #endregion

        #region Output Formatting

        /// <summary>
        /// Generates formatted text output for console or file (returns byte array for download)
        /// </summary>
        public static byte[] GenerateAnalysisReportBytes(List<PacketAnalysis> analyses)
        {
            string report = GenerateAnalysisReport(analyses);
            return Encoding.UTF8.GetBytes(report);
        }

        /// <summary>
        /// Generates formatted text output for console or file
        /// </summary>
        public static string GenerateAnalysisReport(List<PacketAnalysis> analyses)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("================================================================================");
            report.AppendLine("                   PACKET ANALYSIS REPORT (NO REVERSE)");
            report.AppendLine("================================================================================");
            report.AppendLine();

            foreach (var analysis in analyses)
            {
                report.AppendLine($"╔════════════════════════════════════════════════════════════════════════════╗");
                report.AppendLine($"║ PACKET #{analysis.PacketIndex}".PadRight(77) + "║");
                report.AppendLine($"╚════════════════════════════════════════════════════════════════════════════╝");
                report.AppendLine();
                report.AppendLine($"Raw Hex: {analysis.RawHex}");
                report.AppendLine();

                report.AppendLine("┌─ DETAILED BREAKDOWN ────────────────────────────────────────────────────────┐");
                foreach (var detail in analysis.DetailedBreakdown)
                {
                    report.AppendLine($"│ {detail}");
                }
                report.AppendLine("└─────────────────────────────────────────────────────────────────────────────┘");
                report.AppendLine();

                report.AppendLine("┌─ SUMMARY ───────────────────────────────────────────────────────────────────┐");
                report.AppendLine($"│ {analysis.Header}");
                report.AppendLine($"│ {analysis.StepId}");
                report.AppendLine($"│ {analysis.Operator}");

                if (analysis.NominalValues.Count > 0)
                {
                    report.AppendLine($"│");
                    report.AppendLine($"│ Nominal Values ({analysis.NominalValues.Count}):");
                    foreach (var nom in analysis.NominalValues)
                        report.AppendLine($"│   • {nom}");
                }

                if (analysis.Limits.Count > 0)
                {
                    report.AppendLine($"│");
                    report.AppendLine($"│ Limits ({analysis.Limits.Count}):");
                    foreach (var limit in analysis.Limits)
                        report.AppendLine($"│   • {limit}");
                }

                if (analysis.Actions.Count > 0)
                {
                    report.AppendLine($"│");
                    report.AppendLine($"│ Actions ({analysis.Actions.Count}):");
                    foreach (var action in analysis.Actions)
                        report.AppendLine($"│   • {action}");
                }

                if (analysis.Registrations.Count > 0)
                {
                    report.AppendLine($"│");
                    report.AppendLine($"│ Registrations ({analysis.Registrations.Count}):");
                    foreach (var reg in analysis.Registrations)
                        report.AppendLine($"│   • {reg}");
                }

                report.AppendLine($"│");
                report.AppendLine($"│ {analysis.Footer}");
                report.AppendLine("└─────────────────────────────────────────────────────────────────────────────┘");
                report.AppendLine();
                report.AppendLine();
            }

            report.AppendLine("================================================================================");
            report.AppendLine("                            END OF REPORT");
            report.AppendLine("================================================================================");

            return report.ToString();
        }

        #endregion
    }
}
