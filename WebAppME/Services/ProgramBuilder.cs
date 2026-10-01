using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Utils;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BatteryTestingSystem.Services
{
    public class ProgramBuilder
    {

        #region Global Variables
        public static List<GlobalVariable> ExtractGlobalVariables(List<StepModel> programStepsDTO)
        {
            List<GlobalVariable> globalVariables = new();

            foreach (var step in programStepsDTO)
            {
                if (step.OperatorCode == OperatorConstants.SET)
                {
                    foreach (var nomVal in step.NominalValues)
                    {
                        var variable = ValidationHelper.ParseSetVariable(nomVal, step.Id);
                        if (variable != null)
                            globalVariables.Add(variable);
                    }
                }
            }

            return globalVariables;
        }

        #endregion
      
        #region lables
        public static Dictionary<string, int> ExtractLables(List<StepModel> programStepsDTO)
        {
            Dictionary<string, int> labels = new();
           
            foreach (var step in programStepsDTO)
            {
                if (!string.IsNullOrWhiteSpace(step.Label))
                {
                    labels.Add(step.Label.Trim().ToLower(), step.StepNumber);
                }
            }

            return labels;
        }
        #endregion

        #region Step Processing

        public static void AddStepId(List<byte> stepBytes, int stepNumber)
        {
            ushort stepIdValue = (ushort)stepNumber;
            byte[] stepIdBytes = BitConverter.GetBytes(stepIdValue);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(stepIdBytes);
            stepBytes.AddRange(stepIdBytes);
        }

        #endregion

        #region Operator Processors
        public static void ProcessSetOperator(List<byte> stepBytes, StepModel step, List<StepModel> programStepsDTO)
        {
            var globalParams = new List<(byte Code, byte[] Value)>();

            foreach (var nomVal in step.NominalValues)
            {
                var variable = ValidationHelper.ParseSetVariable(nomVal, step.Id);
                if (variable == null)
                    continue;

                // Only "Ah"/"Wh" are real protocol global parameters (accumulated capacity/energy
                // reset, §3.4). Any other SET variable name is unaffected - stays a purely in-app
                // named variable for other steps to reference, exactly as before.
                byte? code = variable.Name.ToLowerInvariant() switch
                {
                    "ah" => (byte)RegistrationType.AccumulatedCapacity,
                    "wh" => (byte)RegistrationType.AccumulatedEnergy,
                    _ => null
                };

                if (code == null || !float.TryParse(variable.Value, out var value))
                    continue;

                var valueBytes = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(valueBytes);

                globalParams.Add((code.Value, valueBytes));
            }

            stepBytes.Add((byte)globalParams.Count);
            foreach (var (code, value) in globalParams)
            {
                stepBytes.Add(code);
                stepBytes.AddRange(value);
            }

            // A global-parameter reset needs its registration logged. If this step didn't choose
            // one explicitly, inherit the nearest preceding SET step's Registrations - same rule
            // ProcessRegOperator below already applies for REG - falling back to STANDARD only
            // when there is no earlier SET to inherit from (matches the reference program, whose
            // first block has nothing to inherit from and is explicitly STANDARD).
            if (globalParams.Count > 0 && step.Registrations.Count == 0)
            {
                var inherited = FindNearestPrecedingSet(step, programStepsDTO)?.Registrations;
                step.Registrations.AddRange(inherited?.Count > 0 ? inherited : new List<string> { "STANDARD" });
            }

            AddRegCount(stepBytes, step);
        }

        // Nearest preceding SET in program order, not just the first SET anywhere in the program -
        // multi-block programs can have distinct SET steps each defining their own registration set.
        private static StepModel? FindNearestPrecedingSet(StepModel step, List<StepModel> programStepsDTO)
        {
            int stepIndex = programStepsDTO.IndexOf(step);
            for (int i = stepIndex - 1; i >= 0; i--)
            {
                if (programStepsDTO[i].OperatorCode == OperatorConstants.SET)
                    return programStepsDTO[i];
            }
            return null;
        }

        public static void ProcessLockAhOperator(List<byte> stepBytes, StepModel step)
        {
            // LOCKAh is instantaneous - operator byte + float32 multiplier + end, same minimal
            // shape as GOTO. No cutoff-condition count and no registration count at all (§14.6).
            var nominal = step.NominalValues.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            if (nominal != null && float.TryParse(nominal, out var multiplier))
            {
                var valueBytes = BitConverter.GetBytes(multiplier);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(valueBytes);
                stepBytes.AddRange(valueBytes);
            }
        }

        public static void AddRegCount(List<byte> stepBytes, StepModel step)
        {
            var dbloads = RStandards.GetStandards();
            var defaultStandard = dbloads.FirstOrDefault(s => s.StandardName.Equals("STANDARD", StringComparison.OrdinalIgnoreCase));
            var defaultUnits = defaultStandard?.UnitList ?? new List<string>();

            var matchedStandard = step.Registrations
                .Select(r => dbloads.FirstOrDefault(s => s.StandardName.Equals(r, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(s => s != null);

            List<string> unitsToUse = matchedStandard?.UnitList ?? defaultUnits;

            int unitCount = RStandards.unitValueMap["h,min,sec"];

            unitCount += unitsToUse
            .Where(unit => RStandards.unitValueMap.ContainsKey(unit))
                .Sum(unit => RStandards.unitValueMap[unit]);

            unitCount += RStandards.unitValueMap["ERR_E"];
            unitCount += RStandards.unitValueMap["MSG_E"];

            byte[] RType = BitConverter.GetBytes((short)unitCount);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(RType);

            stepBytes.AddRange(RType);
        }

        public static void ProcessRegOperator(List<byte> stepBytes, StepModel step, List<StepModel> programStepsDTO)
        {
            if (step.Registrations?.Any() == true)
            {
                AddRegCount(stepBytes, step);
                return;
            }

            // Bind to the SET step that opened this step's block.
            var targetStep = FindNearestPrecedingSet(step, programStepsDTO);

            if (targetStep != null)
            {
                AddRegCount(stepBytes, targetStep);
            }
            else
            {
                // No SET step precedes this REG step. RType is a fixed 2-byte
                // field on the wire — emitting nothing here would silently
                // shift every field after this step, so fall back to 0x0000
                // rather than skip the field.
                stepBytes.AddRange(new byte[] { 0x00, 0x00 });
            }
        }

        public static void ProcessTableOperator(List<byte> stepBytes, StepModel step, BatteryDTO? battery = null)
        {
            foreach (var item in step.NominalValues)
            {
                if (!string.IsNullOrEmpty(item))
                {
                    var TableData = FileManagerService.ReadFileLines(item);
                    if (TableData.Success)
                    {
                        var TableBytes = BuildTableOPTxtToBinary(TableData.Data);
                        if (TableBytes.Success)
                        {
                            stepBytes.AddRange(TableBytes.Data);
                        }
                    }
                }
            }

            AddRegistrations(stepBytes, step.Registrations, null, battery);
        }

        public static void ProcessPauOperator(List<byte> stepBytes, StepModel step, List<GlobalVariable> globalVariables, List<StepModel> programStepsDTO, BatteryDTO? battery = null)
        {
            ProcessLimitsWithActions(stepBytes, step, globalVariables, programStepsDTO, isPauOperator: true, battery);
            AddRegistrations(stepBytes, step.Registrations, globalVariables, battery);
        }

        public static void ProcessGotoOperator(List<byte> stepBytes, StepModel step, List<GlobalVariable> globalVariables, List<StepModel> programStepsDTO, BatteryDTO? battery = null)
        {
            int stepId = step.StepNumber+1;

            var target = step.NominalValues.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            if (!string.IsNullOrWhiteSpace(target))
            {
                var targetStep = programStepsDTO.FirstOrDefault(s =>
                    !string.IsNullOrWhiteSpace(s.Label) &&
                    s.Label.Equals(target, StringComparison.OrdinalIgnoreCase));

                if (targetStep == null && int.TryParse(target, out int targetStepNumber))
                {
                    targetStep = programStepsDTO.FirstOrDefault(s => s.StepNumber == targetStepNumber);
                }

                if (targetStep != null)
                {
                    stepId = targetStep.StepNumber; 
                }
                
            }

            // Add Step Id
            AddStepId(stepBytes, stepId);
            // Process Registrations
            AddRegistrations(stepBytes, step.Registrations, globalVariables, battery);

        }

        public static void ProcessDefaultOperator(List<byte> stepBytes, StepModel step, List<GlobalVariable> globalVariables, List<StepModel> programStepsDTO, BatteryDTO? battery = null)
        {
            // Process Nominal Values
            ProcessNominalValues(stepBytes, step.NominalValues, globalVariables, battery);

            // Process Limits
            ProcessLimitsWithActions(stepBytes, step, globalVariables, programStepsDTO, isPauOperator: false, battery);

            // Process Registrations
            AddRegistrations(stepBytes, step.Registrations, globalVariables, battery);
        }

        #endregion

        #region Nominal Values

        public static void ProcessNominalValues(List<byte> stepBytes, List<string> nominalValues, List<GlobalVariable> globalVariables, BatteryDTO? battery = null)
        {
            foreach (var item in nominalValues)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    var parts = item
                        .Trim()
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 0)
                        continue;

                    string value;
                    string unit = string.Empty;

                    var gv = globalVariables.FirstOrDefault(e => string.Equals(e.Name, parts[0], StringComparison.OrdinalIgnoreCase));

                    if (gv != null)
                    {
                        value = gv.Value;
                        unit = gv.Unit ?? string.Empty;
                    }
                    else
                    {
                        value = parts[0];
                    }

                    if (parts.Length > 1 && string.IsNullOrEmpty(unit))
                    {
                        unit = parts[1];
                    }

                    // Resolve battery-relative units (ACNx, VN) to plain A/V before encoding —
                    // the wire protocol has no native concept of them. See BatteryUnitResolver.
                    if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue))
                    {
                        var resolved = BatteryUnitResolver.Resolve(parsedValue, unit, battery);
                        if (resolved != null)
                        {
                            value = resolved.Value.Value.ToString(CultureInfo.InvariantCulture);
                            unit = resolved.Value.Unit;
                        }
                    }

                    var nominalValue = ExtractFloatAsByteArraySafe($"{value} {unit}".Trim());

                    if (nominalValue != null)
                    {
                        stepBytes.AddRange(nominalValue);
                    }
                }

            }
            
        }

        #endregion

        #region Limits and Actions

        public static void ProcessLimitsWithActions(List<byte> stepBytes, StepModel step, List<GlobalVariable> globalVariables, List<StepModel> programStepsDTO, bool isPauOperator, BatteryDTO? battery = null)
        {
            if (step.Limits.Count == 0)
                return;

            if (!isPauOperator && step.OperatorCode != OperatorConstants.PAU)
                stepBytes.Add((byte)step.Limits.Count);

            EnsureActionsCount(step);

            for (int i = 0; i < step.Limits.Count; i++)
            {
                string item = step.Limits[i];
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                var parts = item.Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries);

                if (isPauOperator)
                {
                    ProcessPauLimit(stepBytes, parts, globalVariables);
                }
                else
                {
                    ProcessStandardLimit(stepBytes, parts, globalVariables, step.OperatorCode, battery);
                }

                // Process Action
                ProcessAction(stepBytes, step.Actions[i], programStepsDTO);
            }
        }

        public static void ProcessPauLimit(List<byte> stepBytes, string[] parts, List<GlobalVariable> globalVariables)
        {
            string valueStr = string.Empty;
            string unit = null;

            if (parts.Length == 2)
            {
                var lvalue = globalVariables.FirstOrDefault(e => e.Name == parts[0]);
                valueStr = lvalue != null ? lvalue.Value.ToString() : parts[0];
                unit = parts[1];
            }
            else if (parts.Length == 1)
            {
                var lvalue = globalVariables.FirstOrDefault(e => e.Name == parts[0]);
                valueStr = lvalue != null ? lvalue.Value.ToString() : parts[0];
                unit = lvalue != null ? lvalue.Unit != null ? lvalue.Unit : parts[0] : "s" ;
            }

            if (float.TryParse(valueStr, out float value))
                stepBytes.AddRange(ExtractFloatAsByteArraySafe($"{value} {unit}"));
        }

        public static void ProcessStandardLimit(List<byte> stepBytes, string[] parts, List<GlobalVariable> globalVariables, byte operatorCode, BatteryDTO? battery = null)
        {
            string op = "=";
            string valueStr = string.Empty;
            string unit = string.Empty;

            int index = 0;

            // 1. Condition check
            if (parts.Length > 0 && IsCondition(parts[0]))
            {
                op = parts[0];
                index = 1;
            }

            // 2. Value or variable
            if (parts.Length > index)
            {
                var gv = globalVariables.FirstOrDefault(e => string.Equals(e.Name, parts[index], StringComparison.OrdinalIgnoreCase));

                if (gv != null)
                {
                    valueStr = gv.Value.ToString();
                    unit = gv.Unit;
                }
                else
                {
                    valueStr = parts[index];
                }

                index++;
            }

            // 3. Unit (optional)
            if (parts.Length > index)
            {
                unit = parts[index];
            }

            // Resolve battery-relative units (ACNx, VN) to plain A/V before encoding — the
            // cutoff-condition byte (TryParseUnit) and the wire value both need the resolved
            // unit, since the hardware protocol has no native concept of ACNx/VN.
            bool hasValue = float.TryParse(valueStr, out float value);
            if (hasValue)
            {
                var resolved = BatteryUnitResolver.Resolve(value, unit, battery);
                if (resolved != null)
                {
                    value = resolved.Value.Value;
                    unit = resolved.Value.Unit;
                }
                else
                {
                    // "0.8 CNom" etc — a coefficient applied to the bare CNom token, distinct from
                    // CNom used alone (see ProcessNominalValues/the GlobalVariable substitution
                    // above, which already handles bare CNom as Accumulated Capacity). Maps to
                    // Step Capacity per program_packet_v0.15.md §14.10.
                    var cnomResolved = BatteryUnitResolver.ResolveCNomCoefficient(value, unit, battery);
                    if (cnomResolved != null)
                    {
                        value = cnomResolved.Value.Value;
                        unit = cnomResolved.Value.Unit;
                    }
                }
            }

            // Add UNIT
            if (TryParseUnit(unit, out byte unitByte) && operatorCode != OperatorConstants.PAU)
                stepBytes.Add(unitByte);

            // Add Condition Operator
            if (TryParseOperator(op, out byte opByte))
                stepBytes.Add(opByte);

            // Add VALUE
            if (hasValue)
            {
                byte[]? v = ExtractFloatAsByteArraySafe($"{value} {unit}");
                if (v != null)
                    stepBytes.AddRange(v);
            }
        }

        public static void ProcessAction(List<byte> stepBytes, string action, List<StepModel> programStepsDTO)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                stepBytes.Add(0x00);
                return;
            }

            Dictionary<string, int> lables = ExtractLables(programStepsDTO);

            if (TryParseOpcodeFull(action, lables, out byte opcode, out byte[]? extraByte))
            {
                stepBytes.Add(opcode);

                if (extraByte != null && extraByte.Length > 0)
                {
                    stepBytes.AddRange(extraByte);
                }
            }
        }

        public static void EnsureActionsCount(StepModel step)
        {
            if (step.Actions == null)
                step.Actions = new List<string>();

            while (step.Actions.Count < step.Limits.Count)
                step.Actions.Add(string.Empty);
        }

        #endregion

        #region Registrations

        public static void AddRegistrations(List<byte> stepBytes, List<string> registrations, List<GlobalVariable> globalVariables, BatteryDTO? battery = null)
        {
            if (registrations.Count == 0)
            {
                stepBytes.Add(0x00);
                return;
            }

            stepBytes.Add((byte)registrations.Count);

            foreach (var item in registrations)
            {
                if (string.IsNullOrEmpty(item))
                    continue;

                string processedItem = item;

                // Replace global variable if applicable
                if (globalVariables != null)
                {
                    var parts = item.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var isGVariable = globalVariables.FirstOrDefault(e =>
                        string.Equals(e.Name, parts[0], StringComparison.OrdinalIgnoreCase));

                    if (isGVariable != null)
                    {
                        var unit = isGVariable.Unit ?? (parts.Length > 1 ? parts[1] : string.Empty);
                        processedItem = $"{isGVariable.Value} {unit}".Trim();
                    }
                }

                if (TryParseRegistration(processedItem, out byte rgType, out byte[]? value, battery))
                {
                    stepBytes.Add(rgType);
                    if (value != null && value.Length > 0)
                    {
                        stepBytes.AddRange(value);
                    }
                }
            }
        }

        #endregion

        #region Packet Building

        public static List<byte[]> BuildPackets(List<byte[]> formattedSteps)
        {
            List<byte[]> packets = new();
            int StepOffset = 0;

            for (int index = 0; index < formattedSteps.Count; index++)
            {
                var NewStep = formattedSteps[index];
                bool isLastStep = (index == formattedSteps.Count - 1);
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

        #endregion

        #region Helpers

        // Safe float extraction with unit handling
        public static byte[]? ExtractFloatAsByteArraySafe(string input)
        {

            if (string.IsNullOrWhiteSpace(input))
                return null;

            string unit = new string(input.Where(c => char.IsLetter(c)).ToArray());

            float multiplier = unit.ToLower() switch
            {
                "s" or "sec" or "second" => 1000,
                "m" or "min" or "minute" => 60000,
                "h" or "hr" or "hour" => 3600000,
                _ => 1
            };

            string numberStr = new string(input.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());

            numberStr = numberStr.Replace(',', '.');

            if (float.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float value))
            {
                value *= multiplier;

                byte[] output;

                if (multiplier > 1)
                {
                    int intValue = (int)value; // Convert to int
                    output = BitConverter.GetBytes(intValue);
                }
                else
                {
                    output = BitConverter.GetBytes(value); // Otherwise, treat as float
                }


                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(output);  // Convert to big-endian
                }

                return output;
            }

            return null;

        }

        public static bool IsCondition(string s) => !string.IsNullOrWhiteSpace(s) && s.Trim() is ">" or "<" or ">=" or "<=" or "=" or "==" or "!=" or "≥" or "≤" or "≠" or "=>" or "=<";

        // Operator Parser
        public static bool TryParseOperator(string op, out byte code)
        {
            code = op switch
            {
                ">" => (byte)LogicOperator.GreaterThan,
                "<" => (byte)LogicOperator.LessThan,
                // GREATER OR EQUAL
                "≥" or ">=" => (byte)LogicOperator.GreaterThanOrEqual,
                // LESS OR EQUAL
                "≤" or "<=" => (byte)LogicOperator.LessThanOrEqual,
                "≠" or "!=" => (byte)LogicOperator.NotEqual,
                "=" or "==" => (byte)LogicOperator.Equal,
                _ => 0
            };

            return code != 0;
        }

        // CutOfCondition Parser
        public static bool TryParseUnit(string unit, out byte code)
        {
            unit = unit?.Trim().ToLower() ?? "";

            // ACN<n> — any positive-integer hour divisor (ACN3, ACN7, ...), not just the fixed set
            // (ACN1/2/4/5/10/20). Normally BatteryUnitResolver.Resolve has already collapsed this to
            // plain "a" before it reaches here; the prefix check keeps this mapping correct even if
            // it hasn't (e.g. called directly, or unresolved because the value wasn't numeric).
            if (unit == "a" || unit == "amp" || unit == "amps" || unit.StartsWith("acn"))
            {
                code = (byte)CutoffCondition.Current;
                return true;
            }

            code = unit switch
            {
                // ===== ENERGY =====
                "wh" => (byte)CutoffCondition.AccumulatedEnergy,
                "whcha" => (byte)CutoffCondition.ChargeEnergy,
                "whdch" => (byte)CutoffCondition.DischargeEnergy,
                "whstep" => (byte)CutoffCondition.StepEnergy,


                // ===== CAPACITY =====
                "ah" => (byte)CutoffCondition.AccumulatedCapacity,
                "ahcha" => (byte)CutoffCondition.ChargeCapacity,
                "ahdch" => (byte)CutoffCondition.DischargeCapacity,
                "ahstep" => (byte)CutoffCondition.StepCapacity,
                "percah" => (byte)CutoffCondition.PercAh,

                // ===== VOLTAGE =====
                "v" or "volt" or "volts" or "vn" =>
                    (byte)CutoffCondition.Voltage,

                // ===== POWER =====
                "w" or "watt" or "watts" =>
                    (byte)CutoffCondition.Power,

                // ===== TEMPERATURE =====
                "c" or "degc" or "°c" =>
                    (byte)CutoffCondition.Temperature,

                // ===== TIME =====
                "h" or "hr" or "hour" or
                "m" or "min" or "minute" or
                "s" or "sec" or "second" =>
                    (byte)CutoffCondition.Time,

                // ===== DEFAULT =====
                _ => 0

            };
            return code != 0;
        }

        //Action Parser
        public static bool TryParseOpcodeFull(string input, Dictionary<string, int> lables, out byte opcode, out byte[]? extraByte)
        {
            opcode = 0x00;
            extraByte = null;

            // blank or null = valid, opcode=0x00
            if (string.IsNullOrWhiteSpace(input))
                return true;

            var parts = input.Trim()
                             .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            string key = parts[0].ToUpper();
            string? value = parts.Length > 1 ? parts[1] : null;

            // 1) OPCODE LOOKUP
            opcode = key switch
            {
                "INT" => 0x0E,
                "STO" => 0x0B,
                "ERR" => 0x10,
                "MSG" => 0x11,
                "GOTO" => 0x09,

                // Unknown is treated as opcode=0x00 but still valid
                _ => 0x00
            };

            // If opcode=0x00 → valid; no extra
            if (opcode == 0x00)
                return true;

            // 2) EXTRA PARSING BASED ON OPCODE
            switch (opcode)
            {
                case 0x10: // ERR
                case 0x11: // MSG
                    if (value == null) return false;
                    if (!int.TryParse(value, out int numeric) || numeric < 0 || numeric > 255)
                        return false;

                    extraByte = new byte[] { (byte)numeric };
                    return true;

                case 0x09: // GOTO
                    if (string.IsNullOrWhiteSpace(value))
                        return false;

                    if (lables != null && lables.Count == 0)
                        return false;
                   
                    if (lables.TryGetValue(value.Trim().ToLower(), out int stepNumber))
                    {
                        extraByte = BitConverter.GetBytes((ushort)stepNumber);
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(extraByte);
                        return true;
                    }
                    else  if (int.TryParse(value, out int stepNum))
                    {
                        extraByte = BitConverter.GetBytes((ushort)stepNum);
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(extraByte);
                        return true;
                    }

                    return false;

                case 0x0E: // INT
                case 0x0B: // STO
                           // No extra value allowed
                    return value == null;
            }

            return false;
        }

        // Defalut Registration Parser
        public static bool TryParseRegistration(string input, out byte RGtype, out byte[]? RGValue, BatteryDTO? battery = null)
        {
            RGtype = (byte)RegistrationType.Time;
            RGValue = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();

            // Split on whitespace to separate value from unit — a char-class filter (stripping
            // every digit from the whole string) would mangle a unit containing digits, like
            // "ACN5" -> "ACN" or "ACN10" -> "ACN". Registration entries are always "value unit".
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out float value))
                return false;

            string unitText = parts.Length > 1 ? parts[1] : string.Empty;

            // Resolve battery-relative units (ACNx, VN) to plain A/V before encoding — the
            // hardware protocol has no native concept of them.
            var resolved = BatteryUnitResolver.Resolve(value, unitText, battery);
            if (resolved != null)
            {
                value = resolved.Value.Value;
                unitText = resolved.Value.Unit;
            }
            else
            {
                // "0.1 CNom" etc — see the matching comment in ProcessStandardLimit. Bare "CNom"
                // (no coefficient) is handled earlier, by AddRegistrations' GlobalVariable
                // substitution, and never reaches this branch with unitText == "CNom".
                var cnomResolved = BatteryUnitResolver.ResolveCNomCoefficient(value, unitText, battery);
                if (cnomResolved != null)
                {
                    value = cnomResolved.Value.Value;
                    unitText = cnomResolved.Value.Unit;
                }
            }

            RGValue = ExtractFloatAsByteArraySafe($"{value} {unitText}".Trim());
            if (RGValue == null)
                return false;

            // Map unit to RegistrationType
            var unitLower = unitText.ToLowerInvariant();

            // ACN<n> — any positive-integer hour divisor (ACN3, ACN7, ...), not just the fixed set
            // (ACN1/2/4/5/10/20). Normally BatteryUnitResolver.Resolve above has already collapsed
            // this to plain "a"; the prefix check keeps this mapping correct even if it hasn't.
            if (unitLower == "a" || unitLower == "amp" || unitLower == "amps" || unitLower.StartsWith("acn"))
            {
                RGtype = (byte)RegistrationType.Current;
                return true;
            }

            RGtype = unitLower switch
            {
                // ===== ENERGY =====
                "wh" => (byte)RegistrationType.AccumulatedEnergy,
                "whcha" => (byte)RegistrationType.ChargeEnergy,
                "whdch" => (byte)RegistrationType.DischargeEnergy,
                "whstep" => (byte)RegistrationType.StepEnergy,

                // ===== CAPACITY =====
                "ah" => (byte)RegistrationType.AccumulatedCapacity,
                "ahcha" => (byte)RegistrationType.ChargeCapacity,
                "ahdch" => (byte)RegistrationType.DischargeCapacity,
                "ahstep" => (byte)RegistrationType.StepCapacity,

                // ===== VOLTAGE =====
                "v" or "volt" or "volts" or "vn" =>
                    (byte)RegistrationType.Voltage,

                // ===== POWER =====
                "w" or "watt" or "watts" =>
                    (byte)RegistrationType.Power,

                // ===== TEMPERATURE =====
                "c" or "degc" or "°c" =>
                    (byte)RegistrationType.Temperature,

                // ===== TIME =====
                "h" or "hr" or "hour" or
                "m" or "min" or "minute" or
                "s" or "sec" or "second" =>
                    (byte)RegistrationType.Time,

                // ===== DEFAULT =====
                _ => (byte)RegistrationType.Time
            };

            return true;
        }

        #endregion

        #region TableOperatior Helper Methods

        public static CommonResponse<byte[]> BuildTableOPTxtToBinary(string[] lines)
        {
            try
            {
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                // Count non-empty lines
                ushort lineCount = (ushort)lines.Count(l => !string.IsNullOrWhiteSpace(l));

                // Convert to bytes (little-endian on Windows)
                byte[] bytes = BitConverter.GetBytes(lineCount);

                // Reverse → big-endian
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);

                bw.Write(bytes);   // 2 bytes

                foreach (var raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;

                    string[] parts = raw.Split(';');

                    // -----------------------------
                    // TIME (mandatory)
                    // -----------------------------
                    if (!TryParseTime(parts[0], out int timeMs))
                        return CommonResponse<byte[]>.Fail($"Invalid time format: '{parts[0]}'");

                    // -----------------------------
                    // OPTIONAL A, W, V
                    // -----------------------------
                    float? A = TryParseFloat(parts, 1, out string? errA);
                    if (errA != null)
                        return CommonResponse<byte[]>.Fail($"Invalid A value: {errA}");

                    float? W = TryParseFloat(parts, 2, out string? errW);
                    if (errW != null)
                        return CommonResponse<byte[]>.Fail($"Invalid W value: {errW}");

                    float? V = TryParseFloat(parts, 3, out string? errV);
                    if (errV != null)
                        return CommonResponse<byte[]>.Fail($"Invalid V value: {errV}");

                    // Voltage rule → force positive
                    if (V.HasValue && V < 0)
                        V = Math.Abs(V.Value);

                    // -----------------------------
                    // BUILD OPCODE + COUNT
                    // -----------------------------
                    byte opcode = 8;      // time always bit 8
                    byte count = 1;       // time always included

                    if (A.HasValue) { opcode += 4; count++; }
                    if (W.HasValue) { opcode += 2; count++; }
                    if (V.HasValue) { opcode += 1; count++; }

                    // -----------------------------
                    // WRITE ROW
                    // -----------------------------
                    bw.Write(count);     // 1 byte
                    bw.Write(opcode);    // 1 byte
                    WriteInt32BE(bw, timeMs);// 4 bytes

                    if (A.HasValue) WriteFloatBE(bw, A.Value);
                    if (W.HasValue) WriteFloatBE(bw, W.Value);
                    if (V.HasValue) WriteFloatBE(bw, V.Value);
                }

                return CommonResponse<byte[]>.Ok(ms.ToArray());

            }
            catch (Exception ex)
            {
                return CommonResponse<byte[]>.Fail(ex.Message);
            }
        }

        private static void WriteFloatBE(BinaryWriter bw, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            bw.Write(bytes);
        }

        private static void WriteInt32BE(BinaryWriter bw, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            bw.Write(bytes);
        }
        // ============================================================
        // FLOAT PARSER (A, W, V)
        // ============================================================
        public static float? TryParseFloat(string[] parts, int index, out string? error)
        {
            error = null;

            if (parts.Length <= index)
                return null;

            string s = parts[index].Trim();
            if (string.IsNullOrWhiteSpace(s))
                return null;

            if (float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out float val))
                return val;   // negative allowed, handled outside for V

            error = s;
            return null;
        }

        // ============================================================
        // TIME PARSER (modern switch expression)
        // ============================================================
        public static bool TryParseTime(string input, out int ms)
        {
            input = input.Trim().ToLower();

            // Extract numeric part
            string number = new string(input.TakeWhile(c =>
                char.IsDigit(c) || c == '.' || c == '-'
            ).ToArray());

            if (!double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
            {
                ms = 0;
                return false;
            }

            // Extract unit (after number)
            string unit = input[number.Length..].Trim();

            double multiplier = unit switch
            {
                // seconds
                "s" or "sec" or "second" or "seconds" => 1000,

                // minutes
                "m" or "min" or "minute" or "minutes" => 60000,

                // hours
                "h" or "hr" or "hour" or "hours" => 3600000,

                // no unit → seconds
                "" => 1000,

                // invalid unit
                _ => -1
            };

            if (multiplier < 0)
            {
                ms = 0;
                return false;
            }

            ms = (int)(value * multiplier);
            return true;
        }

        public static CommonResponse<txtFileLoadResult> LoadFileAndHash(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return CommonResponse<txtFileLoadResult>.Fail("File not found.");
                }

                // --- Read all lines ---
                var lines = File.ReadAllLines(filePath);

                // --- Compute SHA256 ---
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hashBytes = sha.ComputeHash(stream);

                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                    sb.Append(b.ToString("x2"));

                // --- Build result ---
                var result = new txtFileLoadResult
                {
                    Lines = lines,
                    Hash = sb.ToString()
                };

                return CommonResponse<txtFileLoadResult>.Ok(result, "File loaded and hashed successfully.");
            }
            catch (Exception ex)
            {
                return CommonResponse<txtFileLoadResult>.Fail(ex.Message);
            }
        }

        #endregion

        #region PRODUCER Expansion

        /// <summary>
        /// Expands PRODUCER steps in-place: each PRODUCER step is replaced by the inner
        /// program's steps, skipping the first SET step and the last STO step.
        /// All step numbers are then renumbered sequentially starting from 1.
        /// </summary>
        /// <param name="steps">The program step list (may be mutated).</param>
        /// <param name="resolvedPrograms">
        ///   Dictionary keyed by program name (case-insensitive) → its parsed steps.
        ///   Must not contain the outer program itself (circular reference guard).
        /// </param>
        /// <returns>New list with PRODUCER steps expanded and steps renumbered.</returns>
        public static List<StepModel> ExpandProducerSteps(
            List<StepModel> steps,
            Dictionary<string, List<StepModel>>? resolvedPrograms)
        {
            if (resolvedPrograms == null || resolvedPrograms.Count == 0)
                return steps;

            var expanded = new List<StepModel>();

            foreach (var step in steps)
            {
                if (step.OperatorCode != OperatorConstants.PRODUCER)
                {
                    expanded.Add(step);
                    continue;
                }

                // Nominal value [0] holds the referenced program name
                var programName = step.NominalValues.FirstOrDefault() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(programName) ||
                    !resolvedPrograms.TryGetValue(programName.Trim(), out var innerSteps) ||
                    innerSteps == null || innerSteps.Count == 0)
                {
                    // Cannot resolve — leave the PRODUCER step as a no-op placeholder
                    expanded.Add(step);
                    continue;
                }

                // Skip first step (SET) and last step (STO), inline the rest
                var innerCore = innerSteps
                    .Skip(1)               // skip SET
                    .SkipLast(1)           // skip STO
                    .ToList();

                if (innerCore.Count == 0)
                {
                    // Referenced program has < 3 steps — nothing to inline
                    expanded.Add(step);
                    continue;
                }

                foreach (var innerStep in innerCore)
                {
                    // Deep-clone each step so the original program model is not mutated
                    var clone = new StepModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        StepNumber = 0,                        // renumbered below
                        OperatorCode = innerStep.OperatorCode,
                        Label = innerStep.Label,
                        Comment = innerStep.Comment,
                        NominalValues = new List<string>(innerStep.NominalValues),
                        Limits = new List<string>(innerStep.Limits ?? new()),
                        Actions = new List<string>(innerStep.Actions ?? new()),
                        Registrations = new List<string>(innerStep.Registrations ?? new())
                    };
                    expanded.Add(clone);
                }
            }

            // Renumber all steps sequentially from 1
            for (int i = 0; i < expanded.Count; i++)
                expanded[i].StepNumber = i + 1;

            return expanded;
        }

        #endregion

    }

}
