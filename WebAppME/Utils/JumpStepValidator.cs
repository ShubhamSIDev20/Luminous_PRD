namespace BatteryTestingSystem.Utils
{
    /// <summary>
    /// Client-side pre-check for "Jump to Step" (bm_program_v3.2 Q10) — used by both
    /// <c>JumpToStepDialog.razor</c> (fast feedback, no round-trip) and
    /// <c>ChannelCommandHandler.JumpToStepAsync</c> (defense in depth against a caller that
    /// bypasses the dialog). Kept as one pure, DI-free class so the two can't drift apart.
    /// </summary>
    public static class JumpStepValidator
    {
        public const string InvalidMessage = "Step not valid. Kindly enter a valid step.";

        /// <summary>
        /// <paramref name="totalSteps"/> is the loaded program's known step count (ProgramDTO.ProgramSteps).
        /// Null or non-positive means "unknown" — only the lower bound and the wire-format ceiling
        /// (ushort, matching the 2-byte big-endian step field) are checked in that case.
        /// </summary>
        public static bool IsValid(int? stepNumber, int? totalSteps)
        {
            if (stepNumber is null || stepNumber < 1 || stepNumber > ushort.MaxValue)
                return false;

            if (totalSteps is > 0 && stepNumber > totalSteps)
                return false;

            return true;
        }
    }
}
