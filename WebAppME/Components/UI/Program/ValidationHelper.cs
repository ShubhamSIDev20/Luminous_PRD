using BatteryTestingSystem.Components.UI.Input;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BatteryTestingSystem.Components.UI;

public static class ValidationHelper
{
    private static NominalFieldConfig? GetConfig(byte OperatorCode)
    {
        if (NominalConfig.Configs.ContainsKey(OperatorCode))
        {
            return NominalConfig.Configs[OperatorCode];
        }

        return new NominalFieldConfig();
    }

    public static (bool IsValid, string Corrected) ValidateAndCorrectValue(string value, bool requiresUnit = false)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (false, value);

        value = value.Trim();
        var parts = System.Text.RegularExpressions.Regex.Split(value, @"\s+");
        
        if (parts.Length == 0)
            return (false, value);

        // Check if numeric
        if (!float.TryParse(parts[0], out _))
            return (false, value);

        if (parts.Length == 1)
        {
            return requiresUnit ? (false, value) : (true, value);
        }

        // Extract and validate unit
        var unitPart = parts[parts.Length - 1];
        var correctedUnit = AutoCorrectUnit(unitPart);
        
        if (correctedUnit == null)
            return (false, value);

        return (true, $"{parts[0]} {correctedUnit}");
    }

    public static string? AutoCorrectUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            return null;

        var exact = unit.Trim().ToLowerInvariant() switch
        {
           // ===== CURRENT =====
            "a" or "amp" or "amps" => "A",

            // ===== TEMPERATURE =====
            "c" or "degc" or "°c" => "C",

            // ===== VOLTAGE =====
            "v" or "volt" or "volts" => "V",

            // ===== POWER =====
            "w" or "watt" or "watts" => "W",

            // ===== CAPACITY =====
            "ah" or "amphour" or "amphours" => "Ah",
            "ahcha" or "ahchg" or "ahcharge" => "AhCha",
            "ahdch" or "ahdis" or "ahdischarge" => "AhDch",
            "ahstep" => "AhStep",

            // ===== ENERGY =====
            "wh" or "watt hour" or "watthour" or "watthours" => "Wh",
            "whcha" or "whchg" or "whcharge" => "WhCha",
            "whdch" or "whdis" or "whdischarge" => "WhDch",
            "whstep" => "WhStep",

            // ===== TIME =====
            "s" or "sec" or "second" or "seconds" => "s",
            "m" or "min" or "minute" or "minutes" => "min",
            "h" or "hr" or "hour" or "hours" => "hr",

            _ => null
        };

        if (exact != null)
            return exact;

        // ACN<n> — any positive-integer hour divisor, not just the fixed set literally registered
        // in ValidUnits (ACN1/2/4/5/10/20). Any ACN-shaped input is handled entirely in this
        // branch and never falls through to the generic fallback below: bare "ACN" is still a
        // literal ValidUnits entry (kept there so AutoSpace round-trips it as typed instead of
        // fuzzy-matching it away, see OperatorConstants.ValidUnits), so the fallback's
        // u.StartsWith(unit) would otherwise re-accept "ACN" via "ACN".StartsWith("ACN") — exactly
        // the implicit-default the editor must now reject (the user must type an explicit hour
        // divisor). "ACN0" is rejected the same way (IsValidAcnUnit requires divisor > 0), so
        // ValidateLimit/ValidateRegistration report "Invalid unit: ACN"/"Invalid unit: ACN0"
        // instead of accepting something that would only fail later, at transfer time.
        if (BatteryUnitResolver.IsAcnUnit(unit))
            return BatteryUnitResolver.IsValidAcnUnit(unit) ? unit.Trim().ToUpperInvariant() : null;

        return OperatorConstants.ValidUnits
            .FirstOrDefault(u =>
                u.StartsWith(unit, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValidTimeUnit(string unit)
    {
        return OperatorConstants.TimeUnits.Contains(unit.ToLower());
    }

    public static (bool IsValid, string? Error, string? Corrected) ValidateNominal(string value, int index, byte operatorCode, HashSet<string> AllLabels, List<GlobalVariable> globalVars, HashSet<int>? allStepNumbers = null)
    {
        NominalFieldConfig OpConfig = GetConfig(operatorCode);

        if (operatorCode == OperatorConstants.SET)
        {
            value = value.Trim().Trim('(', ')');
            var parts = value.Split('=', StringSplitOptions.TrimEntries);

            if (parts.Length > 0 && !OperatorConstants.SetGlobalParameterNames.Contains(parts[0].Trim()))
            {
                var duplicateCount = globalVars
                    .Count(e => string.Equals(e.Name, parts[0], StringComparison.OrdinalIgnoreCase));

                if (duplicateCount > 1)
                {
                    return (false, "duplicate variable", value);
                }
            }

            if (parts.Length != 2)
                return (false, "Format: (var = value) or (var = value unit)", value);

            var varName = parts[0];
            if (!System.Text.RegularExpressions.Regex.IsMatch(varName, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
                return (false, "Invalid variable name", value);

            var valueParts = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (valueParts.Length == 0)
                return (false, "Value required", value);

            if (!float.TryParse(valueParts[0], out var number))
                return (false, "Invalid number", value);

            var normalizedNumber = number.ToString("0.#######");
            string? correctedUnit = null;

            if (valueParts.Length > 1)
            {
                var unit = valueParts[1];

                if (!OperatorConstants.ValidUnits.Contains(unit))
                    return (false, $"Invalid unit: {unit}", value);

                correctedUnit = unit;
            }



            var corrected = correctedUnit is null
                  ? $"({varName} = {normalizedNumber})"
                  : $"({varName} = {normalizedNumber} {correctedUnit})";

            return (true, null, corrected);

        }

        if (operatorCode == OperatorConstants.GOTO)
        {
            var trimmedValue = value?.Trim() ?? string.Empty;

            if (int.TryParse(trimmedValue, out var stepNum))
            {
                if (allStepNumbers != null && allStepNumbers.Count > 0 && !allStepNumbers.Contains(stepNum))
                    return (false, $"Step {stepNum} does not exist", string.Empty);

                return (true, null, trimmedValue);
            }

            if (AllLabels.Contains(trimmedValue)) return (true, null, trimmedValue);
            return (false, "Must be step number or valid label", string.Empty);
        }

        if (operatorCode == OperatorConstants.BEG)
        {
            return string.IsNullOrWhiteSpace(value) ? (false, "Cycle name required", string.Empty) : (true, null, value);
        }

        if (operatorCode == OperatorConstants.CYC || operatorCode == OperatorConstants.ERR || operatorCode == OperatorConstants.MSG)
        {
            return int.TryParse(value, out _) ? (true, null, value) : (false, "Must be a number", value);
        }

        if (operatorCode == OperatorConstants.TABLE)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (false, "Table name required", string.Empty);
            }

            var tov = FileManagerService.ValidateFile(value);

            if (!tov.Success)
            {
                return (false, tov.Message ?? "Invalid table content", string.Empty);
            }

            return (true, null, value);
        }

        if (operatorCode == OperatorConstants.REG)
        {
            // Optional label: when blank, REG behaves exactly like SET (no change from prior behavior).
            // When provided, it's a ≤8-char grouping key used only by this app (never serialized to the
            // device — see ProgramBuilder.ProcessSetOperator) to store this step's logged data separately.
            if (string.IsNullOrWhiteSpace(value))
                return (true, null, string.Empty);

            var label = value.Trim();

            if (label.Length > 8)
                return (false, "REG label must be 8 characters or fewer", value);

            if (!System.Text.RegularExpressions.Regex.IsMatch(label, @"^[A-Za-z0-9_-]+$"))
                return (false, "REG label may only contain letters, numbers, '_' and '-'", value);

            return (true, null, label);
        }

        if (operatorCode == OperatorConstants.LOCKAH)
        {
            if (!float.TryParse(value, out var multiplier))
                return (false, "Invalid number", value);

            if (multiplier < 1.0f)
                return (false, "Must be 1.0 or greater", value);

            return (true, null, multiplier.ToString("0.#######"));
        }

        NominalField field = OpConfig.Fields.Count > index ? OpConfig.Fields[index] : new NominalField();

        if (string.IsNullOrEmpty(value))
            return (false, "Empty value", value);

        if (!string.IsNullOrEmpty(field.Unit))
        {
            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0) 
            {
                var Match = globalVars?
                    .FirstOrDefault(v => string.Equals(v.Name, parts[0], StringComparison.OrdinalIgnoreCase));

                if (Match != null)
                {
                    if (parts.Length == 0 && Match.Unit != field.Unit)
                        return (false, $"Variable {Match.Name} must have unit {field.Unit}", value);
                    else if (parts.Length >= 1 && Match.Unit != null && Match.Unit != field.Unit)
                        return (false, $"Variable {Match.Name} must have unit {field.Unit}, don't add unit", value);
                    else
                        return (true, null, value);
                }
            }

            if (parts.Length != 2)
                return (false, $"Format: value {field.Unit}", value);

            if (!float.TryParse(parts[0], out var number))
                return (false, "Invalid number", value);

            var typedUnit = parts[1];
            var matchedUnit = field.AcceptedUnits.FirstOrDefault(u => string.Equals(u, typedUnit, StringComparison.OrdinalIgnoreCase));

            // ACN<n> — any positive-integer hour divisor is allowed, but only on a field that
            // accepts the ACN family at all (a Current field, i.e. where A/current is calculated —
            // ExtraUnits == BatteryUnitResolver.AcnUnits). Not exposed on Voltage/Power/etc. fields.
            // Marker is a shape check (StartsWith "ACN"), not a literal Contains("ACN") — bare
            // "ACN" was deliberately removed from AcnUnits (it's no longer independently
            // acceptable, see IsValidAcnUnit), so a literal-equality marker would always be false.
            // IsValidAcnUnit (not IsAcnUnit) so a bare "ACN" or a zero divisor ("ACN0") is rejected
            // here rather than accepted and only failing later at transfer time.
            if (matchedUnit == null &&
                BatteryUnitResolver.IsValidAcnUnit(typedUnit) &&
                field.ExtraUnits.Any(u => u.StartsWith("ACN", StringComparison.OrdinalIgnoreCase)))
            {
                matchedUnit = typedUnit.Trim().ToUpperInvariant();
            }

            if (matchedUnit == null)
                return (false, $"Unit must be one of: {string.Join(", ", field.AcceptedUnits)}", value);

            // The coefficient in "<n> ACN<x>" must be a real, non-zero value — "0 ACN3" would
            // silently encode to 0 A.
            if (BatteryUnitResolver.IsAcnUnit(matchedUnit) && number == 0)
                return (false, "ACN value must be greater than 0", value);

            var normalizedNumber = number.ToString("0.#######");
            var corrected = $"{normalizedNumber} {matchedUnit}";

            return (true, null, corrected);
        }

        return (true, null, value);
    }

    public static (bool IsValid, string? Error, string? Corrected) ValidateLimit(string limit, byte operatorCode, List<GlobalVariable> globalVars)
    {
        if (string.IsNullOrWhiteSpace(limit))
            return (false, "Empty limit", null);

        limit = limit.Trim();

        // -----------------------------------

        bool isPAU = operatorCode == OperatorConstants.PAU;

        // PAU does NOT allow operators
        if (isPAU && Regex.IsMatch(limit, @"^\s*[<>!=≥≤]"))
            return Invalid("PAU does not allow comparison operators");

        // Extract operator (non-PAU only)
        string op = "";
        string valuePart = limit;

        if (!isPAU)
        {
            var m = Regex.Match(limit, @"^(=|==|>=|<=|!=|>|<)?\s*(.+)$");
            if (!m.Success)
                return Invalid("Invalid limit format");

            op = m.Groups[1].Value;
            valuePart = m.Groups[2].Value.Trim();
        }

        var parts = Regex.Split(valuePart, @"\s+");
        if (parts.Length == 0)
            return Invalid("Missing value");

        var nameOrValue = parts[0];
        var unit = parts.Length > 1 ? parts[^1] : null;

        // ---------- Numeric ----------
        if (double.TryParse(nameOrValue, out double n))
        {
            if (!IsValidNumber(n))
            {
                return Invalid("Numeric value allows max 6 digits before and after decimal");
            }

            if (unit == null)
                return Invalid(isPAU
                    ? "Numeric value requires a time unit (s, sec, min, m, hr, h)"
                    : "Numeric value requires a unit");

            // "0.8 CNom" — a coefficient applied to the bare CNom token (§14.10 of
            // program_packet_v0.15.md), handled by the generic path below: "CNom" is a registered
            // ValidUnits entry, so AutoCorrectUnit resolves it directly (and PAU still rejects it
            // via IsValidTimeUnit, same as any non-time unit — PAU only carries a plain duration).
            // Bare "CNom" alone (no coefficient) is handled further down, via the ordinary
            // GlobalVariable/§12.3-token branch.
            var correctedUnit = AutoCorrectUnit(unit);
            if (correctedUnit == null || (isPAU && !IsValidTimeUnit(correctedUnit)))
            {
                return Invalid(isPAU
                    ? "PAU only allows time units (s, sec, min, m, hr, h)"
                    : $"Invalid unit: {unit}");
            }

            if (string.Equals(correctedUnit, "PERCAh", StringComparison.OrdinalIgnoreCase) && n <= 0)
                return Invalid("PERCAh limit value must be greater than 0");

            // The coefficient in "<n> ACN<x>" must be a real, non-zero value — "0 ACN3" would
            // silently encode to 0 A.
            if (BatteryUnitResolver.IsAcnUnit(correctedUnit) && n == 0)
                return Invalid("ACN value must be greater than 0");

            return Ok($"{op}{(op == "" ? "" : " ")}{nameOrValue} {correctedUnit}");
        }

        // ---------- Variable ----------
        var gv = FindVar(nameOrValue, globalVars);
        if (gv == null)
            return IsVariable(nameOrValue)
                ? Invalid($"Variable {nameOrValue} not defined in SET")
                : Invalid("Invalid limit value");

        bool hasVarUnit = !string.IsNullOrEmpty(gv.Unit);
        bool hasInputUnit = unit != null;

        if (hasVarUnit && hasInputUnit)
            return Invalid($"{nameOrValue} already has unit {gv.Unit}, don't add unit");

        if (hasVarUnit)
        {
            if (isPAU && !IsValidTimeUnit(gv.Unit))
                return Invalid($"PAU only allows time units, but {nameOrValue} has unit {gv.Unit}");

            return Ok($"{op}{(op == "" ? "" : " ")}{nameOrValue}");
        }

        // Variable without unit
        if (!hasInputUnit)
        {
            if (isPAU)
                return Ok($"{nameOrValue} s");

            return Invalid($"{nameOrValue} requires a unit");
        }

        var corrected = AutoCorrectUnit(unit!);
        if (corrected == null ||
            (isPAU && !IsValidTimeUnit(corrected)))
        {
            return Invalid(isPAU
                ? "PAU only allows time units (s, sec, min, m, hr, h)"
                : $"Invalid unit: {unit}");
        }

        return Ok($"{op}{(op == "" ? "" : " ")}{nameOrValue} {corrected}");
    }

    public static (bool IsValid, string? Error) ValidateAction(string action, HashSet<string> allLabels, HashSet<int>? allStepNumbers = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            return (true, null);

        action = action.Trim();
        var upper = action.ToUpper();

        // Valid simple actions
        if (new[] { "INT", "STO" }.Contains(upper))
            return (true, null);

        // ERR with number
        if (upper.StartsWith("ERR"))
        {
            var num = upper.Substring(3).Trim();
            if (string.IsNullOrEmpty(num))
                return (false, "ERR requires a number (e.g., ERR 1)");
            if (!int.TryParse(num, out _))
                return (false, "ERR requires a valid number");
            return (true, null);
        }

        // MSG with number
        if (upper.StartsWith("MSG"))
        {
            var num = upper.Substring(3).Trim();
            if (string.IsNullOrEmpty(num))
                return (false, "MSG requires a number (e.g., MSG 1)");
            if (!int.TryParse(num, out _))
                return (false, "MSG requires a valid number");
            return (true, null);
        }

        // GOTO with label or step number
        if (upper.StartsWith("GOTO"))
        {
            var target = action.Substring(4).Trim();
            if (string.IsNullOrEmpty(target))
                return (false, "GOTO requires a target (step number or label)");

            if (int.TryParse(target, out var stepNum))
            {
                if (allStepNumbers != null && allStepNumbers.Count > 0 && !allStepNumbers.Contains(stepNum))
                    return (false, $"Step {stepNum} does not exist");

                return (true, null);
            }

            if (allLabels.Contains(target))
                return (true, null);

            return (false, $"Label '{target}' not found");
        }

        return (false, "Only allowed: INT, STO, ERR <num>, MSG <num>, GOTO <label>");
    }

    public static (bool IsValid, string? Error, string? Corrected) ValidateRegistration(string value, byte operatorCode, List<GlobalVariable> globalVars)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (false, "Empty value", null);


        if (operatorCode == OperatorConstants.SET)
        {
            var match = RStandards.GetStandards().FirstOrDefault(s => s.StandardName.Equals(value, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return (true, null, match.StandardName);
            }

            return (false, "Invalid registration", null);

        }

        var parts = System.Text.RegularExpressions.Regex.Split(value.Trim(), @"\s+");

        if (parts.Length == 0)
            return (false, "Invalid input", null);

        string val;
        string unit = string.Empty;

        var globalVar = globalVars?.FirstOrDefault(v => string.Equals(v.Name, parts[0], StringComparison.OrdinalIgnoreCase));

        if (globalVar != null)
        {
            val = globalVar.Value;
            unit = globalVar.Unit ?? (parts.Length > 1 ? parts[^1] : string.Empty);
        }
        else
        {
            val = parts[0];
            unit = parts.Length > 1 ? parts[^1] : string.Empty;
        }

        if (!float.TryParse(val, out var parsedVal))
            return (false, "Value must be numeric", null);

        if (string.IsNullOrEmpty(unit))
            return (false, "Unit is required", null);

        // "0.1 CNom" — handled by the generic path below, since "CNom" is a registered ValidUnits
        // entry. Bare "CNom" alone never reaches here with unit == "CNom": the globalVar branch
        // above already resolved it to CNom's own Unit ("Ah") before this point.
        var correctedUnit = AutoCorrectUnit(unit);

        if (correctedUnit == null)
            return (false, $"Invalid unit: {unit}", null);

        // The coefficient in "<n> ACN<x>" must be a real, non-zero value — "0 ACN3" would
        // silently encode to 0 A.
        if (BatteryUnitResolver.IsAcnUnit(correctedUnit) && parsedVal == 0)
            return (false, "ACN value must be greater than 0", null);

        return (true, null, $"{val} {correctedUnit}");

    }

    public static GlobalVariable? ParseSetVariable(string value, string stepId)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim().Trim('(', ')');

        var parts = value.Split('=', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return null;

        var varName = parts[0];

        var valueParts = parts[1]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (valueParts.Length == 0)
            return null;

        var unit = valueParts.Length > 1
            ? AutoCorrectUnit(valueParts[1])
            : null;

        return new GlobalVariable
        {
            Name = varName,
            Value = valueParts[0],
            Unit = unit,
            StepId = stepId
        };
    }

    // ---------- Local helpers ----------
    private static bool IsValidNumber(double value)
    {
        value = Math.Abs(value);
        string str = value.ToString("G15", System.Globalization.CultureInfo.InvariantCulture);
        string digitsOnly = str.Replace(".", "");
        return digitsOnly.Length <= 6;
    }

    public static (bool, string?, string?) Invalid(string msg) => (false, msg, null);
    public static (bool, string?, string?) Ok(string corrected) => (true, null, corrected);
    public static GlobalVariable? FindVar(string name, List<GlobalVariable>? vars) => vars?.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
    public static bool IsVariable(string value) => Regex.IsMatch(value, @"^[a-zA-Z]\w*$");
}
