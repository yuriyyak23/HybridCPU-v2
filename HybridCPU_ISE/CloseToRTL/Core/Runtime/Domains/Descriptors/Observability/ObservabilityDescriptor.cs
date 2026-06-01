namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class ObservabilityDescriptor
{
    public ObservabilityDescriptor()
        : this(
            allowGuestArchitecturalObservability: false,
            allowCompatibilityAliasObservability: false,
            allowHostLocalRuntimeCapture: false)
    {
    }

    public ObservabilityDescriptor(
        bool allowGuestArchitecturalObservability,
        bool allowCompatibilityAliasObservability,
        bool allowHostLocalRuntimeCapture)
    {
        AllowGuestArchitecturalObservability = allowGuestArchitecturalObservability;
        AllowCompatibilityAliasObservability = allowCompatibilityAliasObservability;
        AllowHostLocalRuntimeCapture = allowHostLocalRuntimeCapture;
    }

    public static ObservabilityDescriptor FailClosed { get; } = new();

    public bool AllowGuestArchitecturalObservability { get; }

    public bool AllowCompatibilityAliasObservability { get; }

    public bool AllowHostLocalRuntimeCapture { get; }

    public bool CanPublishToGuest(EvidenceVisibilityClass evidenceClass) =>
        evidenceClass switch
        {
            EvidenceVisibilityClass.GuestArchitecturalState => AllowGuestArchitecturalObservability,
            EvidenceVisibilityClass.CompatibilityAlias => AllowCompatibilityAliasObservability,
            _ => false,
        };

    public bool CanCaptureHostLocal(EvidenceVisibilityClass evidenceClass) =>
        AllowHostLocalRuntimeCapture &&
        evidenceClass is EvidenceVisibilityClass.HostOwnedRuntimeEvidence
            or EvidenceVisibilityClass.SchedulerEvidence
            or EvidenceVisibilityClass.BackendBindingEvidence
            or EvidenceVisibilityClass.NativeTokenEvidence;

    public bool MustRedactForGuest(EvidenceVisibilityClass evidenceClass) =>
        !CanPublishToGuest(evidenceClass);
}
