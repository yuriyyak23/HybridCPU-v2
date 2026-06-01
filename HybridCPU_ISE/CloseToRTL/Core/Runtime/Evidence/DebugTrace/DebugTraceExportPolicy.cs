namespace YAKSys_Hybrid_CPU.Core;

public enum DebugTraceExportDecision : byte
{
    Allowed = 0,
    GuestExportDenied = 1,
    HostLocalExportDenied = 2,
    EvidencePolicyDenied = 3,
    RedactionRequired = 4,
}

public readonly record struct DebugTraceExportResult(
    DebugTraceExportDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == DebugTraceExportDecision.Allowed;

    public static DebugTraceExportResult Allowed { get; } =
        new(DebugTraceExportDecision.Allowed, string.Empty);

    public static DebugTraceExportResult Denied(
        DebugTraceExportDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class DebugTraceExportPolicy
{
    public DebugTraceExportPolicy()
        : this(
            allowGuestExport: false,
            allowHostLocalExport: false,
            redactHostOwnedEvidence: true)
    {
    }

    public DebugTraceExportPolicy(
        bool allowGuestExport,
        bool allowHostLocalExport,
        bool redactHostOwnedEvidence)
    {
        AllowGuestExport = allowGuestExport;
        AllowHostLocalExport = allowHostLocalExport;
        RedactHostOwnedEvidence = redactHostOwnedEvidence;
    }

    public static DebugTraceExportPolicy FailClosed { get; } = new();

    public bool AllowGuestExport { get; }

    public bool AllowHostLocalExport { get; }

    public bool RedactHostOwnedEvidence { get; }

    public DebugTraceExportResult ValidateExport(
        ObservabilityDescriptor observability,
        EvidencePolicyDescriptor evidencePolicy,
        EvidenceVisibilityClass evidenceClass,
        bool guestVisibleExport)
    {
        return guestVisibleExport
            ? ValidateGuestExport(observability, evidencePolicy, evidenceClass)
            : ValidateHostLocalExport(observability, evidenceClass);
    }

    private DebugTraceExportResult ValidateGuestExport(
        ObservabilityDescriptor observability,
        EvidencePolicyDescriptor evidencePolicy,
        EvidenceVisibilityClass evidenceClass)
    {
        if (!AllowGuestExport)
        {
            return DebugTraceExportResult.Denied(
                DebugTraceExportDecision.GuestExportDenied,
                "Debug trace policy does not permit guest-visible export.");
        }

        if (RedactHostOwnedEvidence && observability.MustRedactForGuest(evidenceClass))
        {
            return DebugTraceExportResult.Denied(
                DebugTraceExportDecision.RedactionRequired,
                "Debug trace evidence must be redacted before guest-visible export.");
        }

        if (!evidencePolicy.CanExposeToGuest(evidenceClass))
        {
            return DebugTraceExportResult.Denied(
                DebugTraceExportDecision.EvidencePolicyDenied,
                "Evidence policy does not permit guest-visible export for this evidence class.");
        }

        if (!observability.CanPublishToGuest(evidenceClass))
        {
            return DebugTraceExportResult.Denied(
                DebugTraceExportDecision.GuestExportDenied,
                "Observability policy does not permit guest-visible export for this evidence class.");
        }

        return DebugTraceExportResult.Allowed;
    }

    private DebugTraceExportResult ValidateHostLocalExport(
        ObservabilityDescriptor observability,
        EvidenceVisibilityClass evidenceClass)
    {
        if (!AllowHostLocalExport)
        {
            return DebugTraceExportResult.Denied(
                DebugTraceExportDecision.HostLocalExportDenied,
                "Debug trace policy does not permit host-local export.");
        }

        if (!observability.CanCaptureHostLocal(evidenceClass))
        {
            return DebugTraceExportResult.Denied(
                DebugTraceExportDecision.HostLocalExportDenied,
                "Observability policy does not permit host-local export for this evidence class.");
        }

        return DebugTraceExportResult.Allowed;
    }
}
