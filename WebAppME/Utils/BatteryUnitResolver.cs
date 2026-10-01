using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BatteryTestingSystem.Utils
{
    /// <summary>
    /// Resolves the battery-relative nominal-value/limit/registration units documented in the
    /// "Battery Manager" hardware manual (ACN family, VN) into the plain A/V values the wire
    /// protocol actually understands. The hardware has no native concept of "ACN5" or "VN" —
    /// they are a program-authoring convenience so one program can be written once and reused
    /// across batteries with different capacities/cell counts; the scaling must happen here,
    /// using whichever battery is selected at transfer time.
    /// See docs/manual-extract/VNC-ACN-battery-parameters.md for the manual excerpts and formulas.
    /// </summary>
    public static class BatteryUnitResolver
    {
        // ACN<n> — current as a multiple of nominal capacity over n hours. Bare "ACN" defaults to
        // 5h, per the manual's own worked example ("the battery is charged with 1 x 1 Ah / 5 h") —
        // same divisor as ACN5 ("C5"). Any positive integer hour divisor is accepted (ACN3, ACN7,
        // ACN15, ...), not just the manual's own named C-rates (1/2/4/5/10/20) — those remain the
        // canonical suggestions surfaced in the editor (see AcnUnits) but the resolver itself
        // accepts any "ACN<n>".
        private static readonly Regex AcnPattern = new(@"^ACN(\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public const string VncUnit = "VN";

        /// <summary>The canonical ACN-family unit tokens surfaced in the editor UI (for field configs
        /// that accept Current + ACNx). The resolver accepts any "ACN&lt;n&gt;", not just these.
        /// Deliberately excludes bare "ACN" — the editor requires an explicit hour divisor to be
        /// typed (see <see cref="IsValidAcnUnit"/>), even though bare "ACN" remains a recognized
        /// unit *shape* elsewhere (<see cref="IsAcnUnit"/>, <c>OperatorConstants.ValidUnits</c>) so
        /// it round-trips through auto-formatting as typed instead of being silently rewritten to
        /// a different unit.</summary>
        public static readonly string[] AcnUnits = { "ACN1", "ACN2", "ACN4", "ACN5", "ACN10", "ACN20" };

        /// <summary>The VN unit token, as a single-element array (for editor field configs that accept Voltage + VN).</summary>
        public static readonly string[] VncUnits = { VncUnit };

        /// <summary>All battery-relative unit tokens this resolver understands (for editor validation).</summary>
        public static readonly string[] AllUnits = AcnUnits.Append(VncUnit).ToArray();

        /// <summary>True for "ACN" (bare) or "ACN" followed by any positive integer hour divisor.
        /// Purely a shape check — "ACN0" matches this too. Use <see cref="IsValidAcnUnit"/> where
        /// the divisor must actually be usable.</summary>
        public static bool IsAcnUnit(string? unit) =>
            !string.IsNullOrWhiteSpace(unit) && AcnPattern.IsMatch(unit.Trim());

        /// <summary>
        /// True only when <paramref name="unit"/> is a *usable, explicitly-typed* ACN unit —
        /// matches the ACN shape AND carries an explicit positive-integer hour divisor. Both
        /// "ACN0" (a zero divisor) and bare "ACN" (no divisor typed at all) are shape-valid (<see
        /// cref="IsAcnUnit"/> returns true for both) but rejected here — the editor requires the
        /// user to type a real number, not rely on an implicit default. Note this is stricter than
        /// <see cref="Resolve"/>, which still treats a bare "ACN" it's handed as 5h for backward
        /// compatibility with already-saved programs; this method governs what the editor accepts
        /// as newly typed input, not what the encoder can still resolve.
        /// </summary>
        public static bool IsValidAcnUnit(string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
                return false;

            var match = AcnPattern.Match(unit.Trim());
            if (!match.Success)
                return false;

            var suffix = match.Groups[1].Value;
            return suffix.Length > 0 &&
                   int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var hours) &&
                   hours > 0;
        }

        public static bool IsBatteryRelativeUnit(string? unit) =>
            IsAcnUnit(unit) ||
            (!string.IsNullOrWhiteSpace(unit) && string.Equals(unit.Trim(), VncUnit, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Resolves a battery-relative unit ("ACN5", "VN", ...) into the equivalent plain unit
        /// ("A", "V") and the scaled value, using <paramref name="battery"/>'s NominalCapacity /
        /// NumberOfCells. Returns null (pass-through) when <paramref name="unit"/> isn't
        /// battery-relative — callers should feed the original value/unit through unchanged.
        /// </summary>
        /// <exception cref="BatteryUnitResolutionException">
        /// A battery-relative unit was used but no usable battery was supplied (null battery, or
        /// the needed battery field is zero/unset).
        /// </exception>
        public static (float Value, string Unit)? Resolve(float value, string? unit, BatteryDTO? battery)
        {
            if (string.IsNullOrWhiteSpace(unit))
                return null;

            var trimmed = unit.Trim();

            var acnMatch = AcnPattern.Match(trimmed);
            if (acnMatch.Success)
            {
                var suffix = acnMatch.Groups[1].Value;
                var hours = suffix.Length == 0 ? 5 : int.Parse(suffix, CultureInfo.InvariantCulture);

                if (hours <= 0)
                    throw new BatteryUnitResolutionException(
                        $"'{unit}' has an invalid hour divisor — must be greater than 0.");

                if (battery == null || battery.NominalCapacity <= 0)
                    throw new BatteryUnitResolutionException(
                        $"'{unit}' requires a battery with a nominal capacity greater than 0, but none was selected.");

                return (value * (battery.NominalCapacity / hours), "A");
            }

            if (string.Equals(trimmed, VncUnit, StringComparison.OrdinalIgnoreCase))
            {
                if (battery == null || battery.NumberOfCells <= 0)
                    throw new BatteryUnitResolutionException(
                        $"'{unit}' requires a battery with a number of cells greater than 0, but none was selected.");

                return (value * battery.NumberOfCells, "V");
            }

            return null;
        }

        /// <summary>
        /// Convenience overload for the "value unit" string form used throughout ProgramBuilder
        /// (e.g. "1.0 ACn5"). Returns the original string unchanged if it isn't battery-relative
        /// or can't be parsed as "&lt;float&gt; &lt;unit&gt;".
        /// </summary>
        public static string ResolveToken(string valueUnit, BatteryDTO? battery)
        {
            if (string.IsNullOrWhiteSpace(valueUnit))
                return valueUnit;

            var parts = valueUnit.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !float.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
                return valueUnit;

            var resolved = Resolve(value, parts[1], battery);
            return resolved == null ? valueUnit : $"{resolved.Value.Value} {resolved.Value.Unit}";
        }

        /// <summary>
        /// Resolves a <em>coefficient</em> applied to the bare "CNom" token — e.g. "0.8 CNom" in a
        /// Limit field, or "0.1 CNom" in a Registration field — into an absolute Step Capacity
        /// value ("AhStep"), per docs/manual-extract/program_packet_v0.15.md §14.10. This is
        /// distinct from the bare "CNom" token alone (see <see cref="GetBatteryGlobalVariables"/>),
        /// which is an existing §12.3 identifier that resolves to Accumulated Capacity ("Ah") with
        /// no multiplier. A coefficient'd CNom must use Step Capacity instead: it counts the Ah
        /// delivered <em>during this step</em>, which is what "0.8 CNom" as a per-step cutoff means
        /// — Accumulated Capacity runs across steps and would make the cutoff fire at the wrong
        /// time depending on what earlier steps already added. Only "CNom" is recognized here —
        /// none of the other nine §12.3 tokens (INom, UGas, ...) take a coefficient; the manual
        /// only ever shows them used bare.
        /// </summary>
        /// <returns>Null when <paramref name="token"/> isn't (case-insensitively) "CNom".</returns>
        /// <exception cref="BatteryUnitResolutionException">
        /// "CNom" was used but no battery with a nominal capacity greater than 0 was supplied.
        /// </exception>
        public static (float Value, string Unit)? ResolveCNomCoefficient(float coefficient, string? token, BatteryDTO? battery)
        {
            if (!string.Equals(token?.Trim(), "CNom", StringComparison.OrdinalIgnoreCase))
                return null;

            if (battery == null || battery.NominalCapacity <= 0)
                throw new BatteryUnitResolutionException(
                    "'CNom' requires a battery with a nominal capacity greater than 0, but none was selected.");

            return (coefficient * battery.NominalCapacity, "AhStep");
        }

        /// <summary>
        /// True if any Nominal Value, Limit or Registration entry across <paramref name="steps"/>
        /// uses a battery-relative token (ACNx, VN, or the §12.3 bare-parameter names including
        /// CNom) — i.e. this program cannot be fully encoded without a battery selected. Used to
        /// gate battery-selection prompts (e.g. before a hex-packet export) so programs that don't
        /// need one aren't interrupted.
        /// </summary>
        public static bool ProgramUsesBatteryRelativeTokens(IEnumerable<StepModel> steps)
        {
            bool HasToken(IEnumerable<string>? values) =>
                values != null && values.Any(v =>
                    !string.IsNullOrWhiteSpace(v) &&
                    v.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(tok =>
                        IsBatteryRelativeUnit(tok) || InternVariableNames.Contains(tok, StringComparer.OrdinalIgnoreCase)));

            return steps.Any(s => HasToken(s.NominalValues) || HasToken(s.Limits) || HasToken(s.Registrations));
        }

        // ───────────────────────────────────────────────────────────────────
        // §12.3 "Using Battery Parameters" — bare INTERN[] tokens (manual
        // p.159-160). Unlike ACNx/VN these carry no multiplier when used bare — the bare
        // name IS the value, e.g. "the Ah counter is set to CNom". That's
        // exactly what ProgramBuilder's SET-variable substitution already
        // does, so these are exposed as ordinary GlobalVariable entries
        // rather than a new resolve step — every existing consumer
        // (ProcessNominalValues, ProcessStandardLimit, AddRegistrations)
        // picks them up with no changes. `Rin` (internal resistance) is
        // deliberately excluded — no CutoffCondition/RegistrationType byte
        // exists for Ohms anywhere in docs/PROTOCOL.md.
        // ───────────────────────────────────────────────────────────────────

        /// <summary>The 10 bare battery-parameter names this resolver injects as GlobalVariables (excludes Rin).</summary>
        public static readonly string[] InternVariableNames =
        {
            "CNom", "NoCell", "UGas", "UMax", "UNom", "CutOff", "INom", "ICrank", "ChargeF", "EDensity"
        };

        /// <summary>
        /// Builds the §12.3 battery-parameter tokens as GlobalVariable entries so they resolve
        /// through the existing SET-variable substitution path (ProcessNominalValues /
        /// ProcessStandardLimit / AddRegistrations all already look up globalVariables by name).
        /// </summary>
        public static List<GlobalVariable> GetBatteryGlobalVariables(BatteryDTO battery)
        {
            static string F(float v) => v.ToString(CultureInfo.InvariantCulture);

            return new List<GlobalVariable>
            {
                new() { Name = "CNom", Value = F(battery.NominalCapacity), Unit = "Ah" },
                new() { Name = "NoCell", Value = battery.NumberOfCells.ToString(CultureInfo.InvariantCulture), Unit = "" },
                new() { Name = "UGas", Value = F(battery.GassingVoltage), Unit = "V" },
                new() { Name = "UMax", Value = F(battery.MaximumVoltage), Unit = "V" },
                new() { Name = "UNom", Value = F(battery.NominalVoltage), Unit = "V" },
                new() { Name = "CutOff", Value = F(battery.BreakVoltage), Unit = "V" },
                new() { Name = "INom", Value = F(battery.NominalCurrent), Unit = "A" },
                new() { Name = "ICrank", Value = F(battery.ColdCrankingCurrent), Unit = "A" },
                new() { Name = "ChargeF", Value = F(battery.ChargeFactor), Unit = "" },
                new() { Name = "EDensity", Value = F(battery.EnergyDensity), Unit = "" },
            };
        }

        // A battery with every field at its type's default (0) — used only to get the correct
        // Name+Unit pairing for editor-time placeholders (see GetPlaceholderGlobalVariables).
        // Values are meaningless here; only Name/Unit matter for validation while authoring,
        // before any real battery is selected.
        private static readonly BatteryDTO PlaceholderBattery = new()
        {
            Name = string.Empty,
            Producer = string.Empty,
        };

        /// <summary>
        /// Editor-time placeholders for the §12.3 tokens — same Name/Unit pairing as
        /// GetBatteryGlobalVariables (single source of truth, so the editor's validation and the
        /// encoder's real resolution can never disagree about which unit a token carries), with
        /// a dummy Value since no real battery is selected while authoring a program.
        /// </summary>
        public static List<GlobalVariable> GetPlaceholderGlobalVariables() => GetBatteryGlobalVariables(PlaceholderBattery);
    }

    /// <summary>
    /// Thrown when a program step uses a battery-relative unit (ACNx, VN) but no battery with
    /// the required field (NominalCapacity / NumberOfCells) is available to resolve it.
    /// </summary>
    public class BatteryUnitResolutionException : Exception
    {
        public BatteryUnitResolutionException(string message) : base(message) { }
    }
}
