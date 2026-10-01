using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Services.Implementations;
using BatteryTestingSystem.Services.Interfaces;
using DocumentFormat.OpenXml.Presentation;
using System.Text.RegularExpressions;

namespace BatteryTestingSystem.Components.UI;

public static class OperatorConstants
{
    public const byte CC_CHG = 1;
    public const byte CV_CHG = 2;
    public const byte CP_CHG = 3;
    public const byte CCCV_CHG = 4;
    public const byte CC_DCHG = 5;
    public const byte CP_DCHG = 6;
    public const byte CCCV_DCHG = 7;
    public const byte PAU = 8;
    public const byte GOTO = 9;
    public const byte SET = 10;
    public const byte STO = 11;
    public const byte CYC = 12;
    public const byte BEG = 13;
    public const byte INT = 14;
    public const byte REG = 15;
    public const byte ERR = 16;
    public const byte MSG = 17;
    public const byte TABLE = 18;
    public const byte CV_DCHG = 19;
    public const byte PRODUCER = 20;

    public static readonly List<Operator> Operators = new()
    {
        new() { Code = SET, Name = "SET", Color = "set", Category = "set" },
        new() { Code = STO, Name = "STO", Color = "sto", Category = "sto" },
        new() { Code = CC_CHG, Name = "CC Chg", Color = "charge", Category = "charge" },
        new() { Code = CV_CHG, Name = "CV Chg", Color = "charge", Category = "charge" },
        new() { Code = CP_CHG, Name = "CP Chg", Color = "charge", Category = "charge" },
        new() { Code = CCCV_CHG, Name = "CCCV Chg", Color = "charge", Category = "charge" },
        new() { Code = CC_DCHG, Name = "CC DChg", Color = "discharge", Category = "discharge" },
        new() { Code = CP_DCHG, Name = "CP DChg", Color = "discharge", Category = "discharge" },
        new() { Code = CCCV_DCHG, Name = "CCCV DChg", Color = "discharge", Category = "discharge" },
        new() { Code = CV_DCHG, Name = "CV DChg", Color = "discharge", Category = "discharge" },
        new() { Code = PAU, Name = "PAU", Color = "pause", Category = "pause" },
        new() { Code = GOTO, Name = "GOTO", Color = "goto", Category = "goto" },
        new() { Code = BEG, Name = "BEG", Color = "goto", Category = "loop" },
        new() { Code = CYC, Name = "CYC", Color = "goto", Category = "loop" },
        new() { Code = INT, Name = "INT", Color = "control", Category = "control" },
        new() { Code = ERR, Name = "ERR", Color = "control", Category = "control" },
        new() { Code = MSG, Name = "MSG", Color = "control", Category = "control" },
        new() { Code = TABLE, Name = "TABLE", Color = "control", Category = "control" },
        new() { Code = REG, Name = "REG", Color = "set", Category = "set" },
        new() { Code = PRODUCER, Name = "PRODUCER", Color = "control", Category = "control" }
    };

    public static string ToName(byte? code = 0)
    {
        return code switch
        {
            CC_CHG => nameof(CC_CHG),
            CV_CHG => nameof(CV_CHG),
            CP_CHG => nameof(CP_CHG),
            CCCV_CHG => nameof(CCCV_CHG),
            CC_DCHG => nameof(CC_DCHG),
            CP_DCHG => nameof(CP_DCHG),
            CCCV_DCHG => nameof(CCCV_DCHG),
            PAU => nameof(PAU),
            GOTO => nameof(GOTO),
            SET => nameof(SET),
            STO => nameof(STO),
            CYC => nameof(CYC),
            BEG => nameof(BEG),
            INT => nameof(INT),
            REG => nameof(REG),
            ERR => nameof(ERR),
            MSG => nameof(MSG),
            TABLE => nameof(TABLE),
            CV_DCHG => nameof(CV_DCHG),
            PRODUCER => nameof(PRODUCER),
            _ => "-"
        };
    }

    public static readonly string[] ValidUnits = { "A", "V", "W", "Wh", "WhCha", "WhDch", "WhStep", "Ah", "AhCha", "AhDch", "AhStep", "C", "s", "sec", "min", "m", "hr", "h" };
    
    public static readonly string[] TimeUnits = { "s", "sec", "min", "m", "hr", "h" };
    
    public static readonly string[] ConditionalOperators = new[] { ">=", "<=", "!=", "==", ">", "<", "=" };

    public static readonly Dictionary<byte, bool> LimitActionAllowed = new()
    {
        [CC_CHG] = true, [CV_CHG] = true, [CP_CHG] = true, [CCCV_CHG] = true,
        [CC_DCHG] = true, [CP_DCHG] = true, [CCCV_DCHG] = true, [CV_DCHG] = true,
        [PAU] = true, [GOTO] = false, [SET] = false, [STO] = false,
        [BEG] = false, [CYC] = false, [INT] = false, [REG] = false,
        [ERR] = false, [MSG] = false, [TABLE] = false,
        [PRODUCER] = false
    };

    public static readonly Dictionary<byte, bool> NominalDisabled = new()
    {
        [STO] = true, [PAU] = true, [INT] = true
    };

    #region ValueSuffixParsing
    private const string ValuePattern = @"(\d+\.?\d*)";
    private const string VariablePattern = @"([A-Za-z_]\w*)";

    private static readonly string OperatorPattern =
        $"({string.Join("|", OperatorConstants.ConditionalOperators.OrderByDescending(o => o.Length).Select(Regex.Escape))})";

    private static readonly string UnitPattern =
        $"({string.Join("|", OperatorConstants.ValidUnits.OrderByDescending(u => u.Length).Select(Regex.Escape))})";

    // Strict patterns (exact match)
    private static readonly Regex FullPattern = new(
        $@"^\s*{OperatorPattern}\s*{ValuePattern}\s*{UnitPattern}\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ValueUnitPattern = new(
        $@"^\s*{ValuePattern}\s*{UnitPattern}\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OperatorValuePattern = new(
        $@"^\s*{OperatorPattern}\s*{ValuePattern}\s*$",
        RegexOptions.Compiled);

    private static readonly Regex OperatorVariablePattern = new(
        $@"^\s*{OperatorPattern}\s*{VariablePattern}\s*$",
        RegexOptions.Compiled);

    // Fuzzy — operator + digits + anything (has digit, unknown unit suffix)
    private static readonly Regex FuzzyOperatorValuePattern = new(
        $@"^\s*{OperatorPattern}\s*{ValuePattern}(.+)\s*$",
        RegexOptions.Compiled);

    // Fuzzy — digits + anything (no operator)
    private static readonly Regex FuzzyValuePattern = new(
        $@"^\s*{ValuePattern}(.+)\s*$",
        RegexOptions.Compiled);

    public static string AutoSpace(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input ?? "";

        // 1. operator + value + unit  →  "> 2 min"
        var m = FullPattern.Match(input);
        if (m.Success) return $"{m.Groups[1].Value} {m.Groups[2].Value} {m.Groups[3].Value}";

        // 2. value + unit  →  "2 min"
        m = ValueUnitPattern.Match(input);
        if (m.Success) return $"{m.Groups[1].Value} {m.Groups[2].Value}";

        // 3. operator + value  →  "> 2"
        m = OperatorValuePattern.Match(input);
        if (m.Success) return $"{m.Groups[1].Value} {m.Groups[2].Value}";

        // 4. operator + variable  →  "> myVar"
        m = OperatorVariablePattern.Match(input);
        if (m.Success) return $"{m.Groups[1].Value} {m.Groups[2].Value}";

        // 5. Fuzzy: operator + digit + garbage  →  find nearest unit in suffix
        m = FuzzyOperatorValuePattern.Match(input);
        if (m.Success)
        {
            var op = m.Groups[1].Value;
            var val = m.Groups[2].Value;
            var suffix = m.Groups[3].Value.Trim();
            var unit = FindNearestUnit(suffix);
            return unit != null ? $"{op} {val} {unit}" : $"{op} {val}";
        }

        // 6. Fuzzy: digit + garbage  →  find nearest unit in suffix
        m = FuzzyValuePattern.Match(input);
        if (m.Success)
        {
            var val = m.Groups[1].Value;
            var suffix = m.Groups[2].Value.Trim();
            var unit = FindNearestUnit(suffix);
            return unit != null ? $"{val} {unit}" : val;
        }

        // 7. No digit anywhere → plain variable, return as-is
        return input.Trim();
    }

    /// Finds the longest valid unit that appears at the START of the suffix.
    /// ">2hbc"  → suffix="hbc" → matches "h"
    /// ">2hrbd" → suffix="hrbd" → matches "hr"
    private static string? FindNearestUnit(string suffix)
    {
        if (string.IsNullOrEmpty(suffix)) return null;

        return OperatorConstants.ValidUnits
            .OrderByDescending(u => u.Length)
            .FirstOrDefault(u => suffix.StartsWith(u, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}

public static class NominalConfig
{
    public static Dictionary<byte, NominalFieldConfig> Configs = new()
    {
        [OperatorConstants.CC_CHG] = new() { Fields = new() { new() { Label = "Current", Unit = "A", Placeholder = "20 A" } } },
        [OperatorConstants.CV_CHG] = new() { Fields = new() { new() { Label = "Voltage", Unit = "V", Placeholder = "12.6 V" } } },
        [OperatorConstants.CP_CHG] = new() { Fields = new() { new() { Label = "Power", Unit = "W", Placeholder = "100 W" } } },
        [OperatorConstants.CCCV_CHG] = new()
        {
            Fields = new() {
            new() { Label = "Current", Unit = "A", Placeholder = "0 A" },
            new() { Label = "Voltage", Unit = "V", Placeholder = "0 V" }
        }
        },
        [OperatorConstants.CC_DCHG] = new() { Fields = new() { new() { Label = "Current", Unit = "A", Placeholder = "20 A" } } },
        [OperatorConstants.CP_DCHG] = new() { Fields = new() { new() { Label = "Power", Unit = "W", Placeholder = "100 W" } } },
        [OperatorConstants.CCCV_DCHG] = new()
        {
            Fields = new() {
            new() { Label = "Current", Unit = "A", Placeholder = "0 A" },
            new() { Label = "Voltage", Unit = "V", Placeholder = "0 V" }
        }
        },
        [OperatorConstants.CV_DCHG] = new() { Fields = new() { new() { Label = "Voltage", Unit = "V", Placeholder = "0 V" } } },
        [OperatorConstants.GOTO] = new() { Fields = new() { new() { Label = "Target", Unit = "", Placeholder = "step or label" } } },
        [OperatorConstants.SET] = new() { AllowCustom = true },
        [OperatorConstants.BEG] = new() { Fields = new() { new() { Label = "Cycle Name", Unit = "", Placeholder = "cycle name" } } },
        [OperatorConstants.CYC] = new() { Fields = new() { new() { Label = "Count", Unit = "", Placeholder = "0" } } },
        [OperatorConstants.REG] = new() { Fields = new() { new() { Label = "Value", Unit = "", Placeholder = "optional label" } } },
        [OperatorConstants.ERR] = new() { Fields = new() { new() { Label = "Error Code", Unit = "", Placeholder = "0" } } },
        [OperatorConstants.MSG] = new() { Fields = new() { new() { Label = "Message Code", Unit = "", Placeholder = "0" } } },
        [OperatorConstants.TABLE] = new() { Fields = new() { new() { Label = "Table", Unit = "", Placeholder = "TABLE1" } } },
        [OperatorConstants.STO] = new() { Disabled = true },
        [OperatorConstants.PAU] = new() { Disabled = true },
        [OperatorConstants.INT] = new() { Disabled = true },
        [OperatorConstants.PRODUCER] = new() { Fields = new() { new() { Label = "Program", Unit = "", Placeholder = "Select program" } } }
    };
}



public class NominalFieldConfig
{
    public List<NominalField> Fields { get; set; } = new();
    public bool AllowCustom { get; set; } = false;
    public bool Disabled { get; set; } = false;
}

public class NominalField
{
    public string Label { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
}


#region Registration Standards

public static class RStandards
{
    private static List<RegistrationStandardsDTO> _standards = new();
    private static readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Synchronous getter (returns cached data, will not reload from DB)
    /// </summary>
    public static IReadOnlyList<RegistrationStandardsDTO> GetStandards()
    {
        return _standards;
    }

    /// <summary>
    /// Async getter (reloadFromDb = true to refresh from database)
    /// </summary>
    public static async Task<IReadOnlyList<RegistrationStandardsDTO>> GetStandardsAsync(bool reloadFromDb = false)
    {
        if (!reloadFromDb && _standards.Any())
            return _standards;

        await _lock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (!reloadFromDb && _standards.Any())
                return _standards;

            using var programService = ServiceLocator.GetScoped<IProgramServices>();
            var response = await programService.Service.GetRegistrationStandardAsync();

            _standards = response.Success
                ? response.Data ?? new List<RegistrationStandardsDTO>()
                : new List<RegistrationStandardsDTO>();

            return _standards;
        }
        finally
        {
            _lock.Release();
        }
    }

    public static Dictionary<string, int> unitValueMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        { "h,min,sec", 1 },
        { "A", 2 },
        { "V", 4 },
        { "C", 8 },
        { "W", 16 },
        { "Ah", 32 },
        { "AhCha", 64 },
        { "AhDch", 128 },
        { "AhStep", 256 },
        { "Wh", 512 },
        { "WhCha", 1024 },
        { "WhDch", 2048 },
        { "WhStep", 4096 },
        {"ERR_E", 8192 },
        {"MSG_E", 16384 }
    };

}

#endregion

#region ErrorAndMessages 
public static class ErrorMessages
{
    public static List<CodeMessageDto> Errors = new();
    public static List<CodeMessageDto> Messages = new();
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public static async Task<IReadOnlyList<CodeMessageDto>> GetErrors(bool reloadFromDb = false)
    {
        if (!reloadFromDb && Errors.Any())
            return Errors;

        await _lock.WaitAsync();

        try
        {
            // Double-check after acquiring lock
            if (!reloadFromDb && Errors.Any())
                return Errors;

            using var programService = ServiceLocator.GetScoped<ICodeMessageService>();
            var response = await programService.Service.GetErrorsAsync();

            Errors = response.Success
                ? response.Data ?? new List<CodeMessageDto>()
                : new List<CodeMessageDto>();

            return Errors;
        }
        finally
        {
            _lock.Release();
        }

    }

    public static async Task<IReadOnlyList<CodeMessageDto>> GetMessages(bool reloadFromDb = false)
    {
        if (!reloadFromDb && Messages.Any())
            return Messages;

        await _lock.WaitAsync();

        try
        {
            // Double-check after acquiring lock
            if (!reloadFromDb && Messages.Any())
                return Messages;

            using var programService = ServiceLocator.GetScoped<ICodeMessageService>();
            var response = await programService.Service.GetMessagesAsync();

            Messages = response.Success
                ? response.Data ?? new List<CodeMessageDto>()
                : new List<CodeMessageDto>();

            return Messages;
        }
        finally
        {
            _lock.Release();
        }

    }

}

#endregion

