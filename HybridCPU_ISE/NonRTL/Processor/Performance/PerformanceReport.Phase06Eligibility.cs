using System.Text;

namespace YAKSys_Hybrid_CPU
{
    public partial class PerformanceReport
    {
        /// <summary>
        /// Number of scheduler cycles where ready SMT candidates were masked out by the FSM-owned eligibility mask.
        /// </summary>
        public long EligibilityMaskedCycles { get; set; }

        /// <summary>
        /// Number of ready SMT candidates hidden by the FSM-owned eligibility mask.
        /// </summary>
        public long EligibilityMaskedReadyCandidates { get; set; }

        /// <summary>
        /// Last requested VT eligibility mask observed by the scheduler diagnostics snapshot.
        /// </summary>
        public byte LastEligibilityRequestedMask { get; set; }

        /// <summary>
        /// Last normalized VT eligibility mask observed by the scheduler diagnostics snapshot.
        /// </summary>
        public byte LastEligibilityNormalizedMask { get; set; }

        /// <summary>
        /// Last ready-port mask before eligibility filtering.
        /// </summary>
        public byte LastEligibilityReadyPortMask { get; set; }

        /// <summary>
        /// Last ready-port mask visible after eligibility filtering.
        /// </summary>
        public byte LastEligibilityVisibleReadyMask { get; set; }

        /// <summary>
        /// Last ready-port subset removed by eligibility filtering.
        /// </summary>
        public byte LastEligibilityMaskedReadyMask { get; set; }

        /// <summary>
        /// Whether explicit eligibility telemetry is present in the report.
        /// Zero-valued fields from older runs remain indistinguishable from no telemetry, so this is a best-effort signal.
        /// </summary>
        public bool HasEligibilityTelemetry =>
            EligibilityMaskedCycles > 0 ||
            EligibilityMaskedReadyCandidates > 0 ||
            LastEligibilityRequestedMask != 0 ||
            LastEligibilityNormalizedMask != 0 ||
            LastEligibilityReadyPortMask != 0 ||
            LastEligibilityVisibleReadyMask != 0 ||
            LastEligibilityMaskedReadyMask != 0;

        /// <summary>
        /// Average masked ready candidates among cycles where the eligibility gate hid at least one candidate.
        /// </summary>
        public double EligibilityMaskedReadyCandidatesPerMaskedCycle =>
            EligibilityMaskedCycles == 0
                ? 0.0
                : (double)EligibilityMaskedReadyCandidates / EligibilityMaskedCycles;

        private void AppendEligibilityValidationSummary(StringBuilder sb)
        {
            if (!HasEligibilityTelemetry)
                return;

            sb.AppendLine(
                $"  Eligibility Gate: masked cycles {EligibilityMaskedCycles:N0}, masked ready candidates {EligibilityMaskedReadyCandidates:N0}, avg masked-ready/cycle {EligibilityMaskedReadyCandidatesPerMaskedCycle:F2}");
        }

        private void AppendEligibilityExecutionSummary(StringBuilder sb)
        {
            if (!HasEligibilityTelemetry)
                return;

            AppendEligibilityValidationSummary(sb);
            sb.AppendLine(
                $"  Eligibility Snapshot: requested 0x{LastEligibilityRequestedMask:X2}, normalized 0x{LastEligibilityNormalizedMask:X2}, ready 0x{LastEligibilityReadyPortMask:X2}, visible 0x{LastEligibilityVisibleReadyMask:X2}, masked 0x{LastEligibilityMaskedReadyMask:X2}");
        }
    }
}
