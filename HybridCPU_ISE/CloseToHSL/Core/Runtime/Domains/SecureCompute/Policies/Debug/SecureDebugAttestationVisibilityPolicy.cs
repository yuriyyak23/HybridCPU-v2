namespace YAKSys_Hybrid_CPU.Core;

public enum SecureDebugAttestationQueryKind : byte
{
    DebugTrace = 0,
    AttestationReport = 1,
    TelemetrySnapshot = 2,
    HostInspectionMetadata = 3,
    CompatibilityAliasEvidence = 4,
}

public enum SecureDebugAttestationVisibilityDecision : byte
{
    VisibilityClassified = 0,
    DeniedMissingDescriptor = 1,
    DeniedMissingMeasurement = 2,
    DeniedStaleMeasurement = 3,
    DeniedDebugPolicy = 4,
    DeniedEvidencePolicy = 5,
    DeniedHostOwnedEvidence = 6,
    DeniedMigrationAuthority = 7,
    DeniedVmreadAuthority = 8,
    DeniedActivationEvidence = 9,
    DeniedBackendOwnerProof = 10,
    DeniedPrivateMemoryInspection = 11,
    DeniedCompletionPublication = 12,
    DeniedRetirePublication = 13,
    DeniedCompatibilityProjectionPolicy = 14,
}

public readonly record struct SecureDebugAttestationVisibilityRequest(
    SecureDebugAttestationQueryKind QueryKind,
    SecureComputeDomainDescriptor? Descriptor,
    DomainMeasurementDescriptor? Measurement,
    EvidencePolicyDescriptor? NeutralEvidencePolicy,
    bool RequestsMigrationPayload = false,
    bool RequestsVmreadValueSource = false,
    bool RequestsActivationEvidence = false,
    bool RequestsBackendOwnerProof = false,
    bool RequestsPrivateMemoryInspection = false,
    bool RequestsCompletionPublication = false,
    bool RequestsRetirePublication = false);

public readonly record struct SecureDebugAttestationVisibilityResult(
    SecureDebugAttestationVisibilityDecision Decision,
    SecureEvidenceVisibilityClass VisibilityClass,
    bool GuestVisible,
    bool HostOnly,
    bool DebugOnly,
    bool AttestationOnly,
    bool CreatesRuntimeAuthority,
    bool CreatesVmreadAuthority,
    bool CreatesMigrationAuthority,
    bool CreatesActivationEvidence,
    bool CreatesBackendOwnerProof,
    bool CompletionPublicationAuthorized,
    bool RetirePublicationAuthorized,
    string Reason)
{
    public bool IsAllowed =>
        Decision == SecureDebugAttestationVisibilityDecision.VisibilityClassified;

    public bool CreatesAnyAuthority =>
        CreatesRuntimeAuthority ||
        CreatesVmreadAuthority ||
        CreatesMigrationAuthority ||
        CreatesActivationEvidence ||
        CreatesBackendOwnerProof ||
        CompletionPublicationAuthorized ||
        RetirePublicationAuthorized;

    public static SecureDebugAttestationVisibilityResult Classified(
        SecureEvidenceVisibilityClass visibilityClass,
        bool guestVisible,
        bool hostOnly,
        bool debugOnly,
        bool attestationOnly,
        string reason) =>
        new(
            SecureDebugAttestationVisibilityDecision.VisibilityClassified,
            visibilityClass,
            guestVisible,
            hostOnly,
            debugOnly,
            attestationOnly,
            CreatesRuntimeAuthority: false,
            CreatesVmreadAuthority: false,
            CreatesMigrationAuthority: false,
            CreatesActivationEvidence: false,
            CreatesBackendOwnerProof: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            reason);

    public static SecureDebugAttestationVisibilityResult Denied(
        SecureDebugAttestationVisibilityDecision decision,
        SecureEvidenceVisibilityClass visibilityClass,
        string reason) =>
        new(
            decision,
            visibilityClass,
            GuestVisible: false,
            HostOnly: false,
            DebugOnly: false,
            AttestationOnly: false,
            CreatesRuntimeAuthority: false,
            CreatesVmreadAuthority: false,
            CreatesMigrationAuthority: false,
            CreatesActivationEvidence: false,
            CreatesBackendOwnerProof: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            reason);
}

public sealed class SecureDebugAttestationVisibilityPolicy
{
    public static SecureDebugAttestationVisibilityPolicy FailClosed { get; } = new();

    public SecureDebugAttestationVisibilityResult Classify(
        SecureDebugAttestationVisibilityRequest request)
    {
        SecureDebugAttestationVisibilityResult shortcut = DenyAuthorityShortcut(request);
        if (!shortcut.IsAllowed)
        {
            return shortcut;
        }

        if (request.Descriptor is null)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedMissingDescriptor,
                SecureEvidenceVisibilityClass.Denied,
                "Debug/attestation visibility requires a secure descriptor.");
        }

        return request.QueryKind switch
        {
            SecureDebugAttestationQueryKind.DebugTrace =>
                ClassifyDebugTrace(request.Descriptor, request.Measurement),

            SecureDebugAttestationQueryKind.AttestationReport =>
                ClassifyAttestationReport(
                    request.Descriptor,
                    request.Measurement,
                    request.NeutralEvidencePolicy),

            SecureDebugAttestationQueryKind.TelemetrySnapshot =>
                ClassifyTelemetrySnapshot(),

            SecureDebugAttestationQueryKind.HostInspectionMetadata =>
                ClassifyHostInspectionMetadata(request.Descriptor),

            SecureDebugAttestationQueryKind.CompatibilityAliasEvidence =>
                ClassifyCompatibilityAlias(
                    request.Descriptor,
                    request.Measurement,
                    request.NeutralEvidencePolicy),

            _ => Deny(
                SecureDebugAttestationVisibilityDecision.DeniedEvidencePolicy,
                SecureEvidenceVisibilityClass.Denied,
                "Debug/attestation query kind is not classified."),
        };
    }

    private static SecureDebugAttestationVisibilityResult DenyAuthorityShortcut(
        SecureDebugAttestationVisibilityRequest request)
    {
        if (request.RequestsMigrationPayload)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedMigrationAuthority,
                SecureEvidenceVisibilityClass.Denied,
                "Debug/attestation visibility cannot be migration authority.");
        }

        if (request.RequestsVmreadValueSource)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedVmreadAuthority,
                SecureEvidenceVisibilityClass.Denied,
                "Debug/attestation visibility cannot be compatibility-read value authority.");
        }

        if (request.RequestsActivationEvidence)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedActivationEvidence,
                SecureEvidenceVisibilityClass.Denied,
                "Debug/attestation visibility cannot be production activation evidence.");
        }

        if (request.RequestsBackendOwnerProof)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedBackendOwnerProof,
                SecureEvidenceVisibilityClass.Denied,
                "Debug telemetry cannot satisfy backend-owner proof.");
        }

        if (request.RequestsPrivateMemoryInspection)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedPrivateMemoryInspection,
                SecureEvidenceVisibilityClass.Denied,
                "Debug/attestation visibility cannot bypass private-memory policy.");
        }

        if (request.RequestsCompletionPublication)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedCompletionPublication,
                SecureEvidenceVisibilityClass.Denied,
                "Debug/attestation visibility cannot publish completion effects.");
        }

        if (request.RequestsRetirePublication)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedRetirePublication,
                SecureEvidenceVisibilityClass.Denied,
                "Debug/attestation visibility cannot publish retire effects.");
        }

        return SecureDebugAttestationVisibilityResult.Classified(
            SecureEvidenceVisibilityClass.Denied,
            guestVisible: false,
            hostOnly: false,
            debugOnly: false,
            attestationOnly: false,
            string.Empty);
    }

    private static SecureDebugAttestationVisibilityResult ClassifyDebugTrace(
        SecureComputeDomainDescriptor descriptor,
        DomainMeasurementDescriptor? measurement)
    {
        if (!descriptor.DebugPolicy.AllowsDebug ||
            !descriptor.EvidenceVisibilityPolicy.AllowDebugEvidence)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedDebugPolicy,
                SecureEvidenceVisibilityClass.DebugOnly,
                "Debug trace visibility requires explicit secure debug and debug-evidence policy.");
        }

        if (measurement is null || !measurement.IsMaterialized)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedMissingMeasurement,
                SecureEvidenceVisibilityClass.DebugOnly,
                "Debug trace visibility requires a materialized measurement.");
        }

        if (measurement.IsStale || !measurement.IsCurrentFor(descriptor.MigrationPolicy.PolicyEpoch))
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedStaleMeasurement,
                SecureEvidenceVisibilityClass.DebugOnly,
                "Debug trace visibility requires a current measurement epoch.");
        }

        if (measurement.DebugClass != SecureMeasurementDebugClass.MeasuredDebug)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedDebugPolicy,
                SecureEvidenceVisibilityClass.DebugOnly,
                "Debug trace visibility requires measured-debug evidence.");
        }

        return SecureDebugAttestationVisibilityResult.Classified(
            SecureEvidenceVisibilityClass.DebugOnly,
            guestVisible: false,
            hostOnly: false,
            debugOnly: true,
            attestationOnly: false,
            "Debug trace is classified debug-only and carries no runtime authority.");
    }

    private static SecureDebugAttestationVisibilityResult ClassifyAttestationReport(
        SecureComputeDomainDescriptor descriptor,
        DomainMeasurementDescriptor? measurement,
        EvidencePolicyDescriptor? neutralEvidencePolicy)
    {
        SecureDebugAttestationVisibilityResult measurementResult =
            ValidateAttestationMeasurement(measurement);
        if (!measurementResult.IsAllowed)
        {
            return measurementResult;
        }

        SecureEvidenceVisibilityClass evidenceClass = measurement!.AttestationEvidenceClass;
        if (descriptor.EvidenceVisibilityPolicy.MustQuarantine(evidenceClass))
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedHostOwnedEvidence,
                evidenceClass,
                "Host-owned attestation evidence remains quarantined.");
        }

        if (evidenceClass != SecureEvidenceVisibilityClass.GuestVisible ||
            neutralEvidencePolicy is null ||
            !descriptor.EvidenceVisibilityPolicy.CanExposeToGuest(neutralEvidencePolicy, evidenceClass))
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedEvidencePolicy,
                evidenceClass,
                "Attestation report visibility requires explicit guest-visible evidence policy.");
        }

        return SecureDebugAttestationVisibilityResult.Classified(
            SecureEvidenceVisibilityClass.GuestVisible,
            guestVisible: true,
            hostOnly: false,
            debugOnly: false,
            attestationOnly: true,
            "Attestation report is guest-visible evidence only and carries no runtime authority.");
    }

    private static SecureDebugAttestationVisibilityResult ClassifyTelemetrySnapshot() =>
        SecureDebugAttestationVisibilityResult.Classified(
            SecureEvidenceVisibilityClass.HostOwnedQuarantined,
            guestVisible: false,
            hostOnly: true,
            debugOnly: false,
            attestationOnly: false,
            "Telemetry is host-owned visibility only and is not backend-owner proof.");

    private static SecureDebugAttestationVisibilityResult ClassifyHostInspectionMetadata(
        SecureComputeDomainDescriptor descriptor)
    {
        if (descriptor.HostInspectionPolicy.Mode is not SecureHostInspectionMode.MetadataOnly
            and not SecureHostInspectionMode.ExplicitSharedOnly
            and not SecureHostInspectionMode.DebugOnly)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedPrivateMemoryInspection,
                SecureEvidenceVisibilityClass.HostOwnedQuarantined,
                "Host inspection metadata requires an explicit metadata/debug policy.");
        }

        return SecureDebugAttestationVisibilityResult.Classified(
            SecureEvidenceVisibilityClass.HostOwnedQuarantined,
            guestVisible: false,
            hostOnly: true,
            debugOnly: descriptor.HostInspectionPolicy.Mode == SecureHostInspectionMode.DebugOnly,
            attestationOnly: false,
            "Host inspection metadata is host-only visibility and cannot inspect private memory.");
    }

    private static SecureDebugAttestationVisibilityResult ClassifyCompatibilityAlias(
        SecureComputeDomainDescriptor descriptor,
        DomainMeasurementDescriptor? measurement,
        EvidencePolicyDescriptor? neutralEvidencePolicy)
    {
        SecureDebugAttestationVisibilityResult measurementResult =
            ValidateAttestationMeasurement(measurement);
        if (!measurementResult.IsAllowed)
        {
            return measurementResult;
        }

        if (!descriptor.CompatibilityProjectionPolicy.AllowReadOnlyAliases)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedCompatibilityProjectionPolicy,
                SecureEvidenceVisibilityClass.CompatibilityAlias,
                "Compatibility alias evidence requires an explicit read-only compatibility policy.");
        }

        if (neutralEvidencePolicy is null ||
            !descriptor.EvidenceVisibilityPolicy.CanExposeToGuest(
                neutralEvidencePolicy,
                SecureEvidenceVisibilityClass.CompatibilityAlias))
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedEvidencePolicy,
                SecureEvidenceVisibilityClass.CompatibilityAlias,
                "Compatibility alias visibility requires secure and neutral evidence policy.");
        }

        return SecureDebugAttestationVisibilityResult.Classified(
            SecureEvidenceVisibilityClass.CompatibilityAlias,
            guestVisible: true,
            hostOnly: false,
            debugOnly: false,
            attestationOnly: true,
            "Compatibility alias attestation is visibility only and is not compatibility-read authority.");
    }

    private static SecureDebugAttestationVisibilityResult ValidateAttestationMeasurement(
        DomainMeasurementDescriptor? measurement)
    {
        if (measurement is null || !measurement.IsMaterialized)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedMissingMeasurement,
                SecureEvidenceVisibilityClass.Denied,
                "Attestation visibility requires a materialized measurement.");
        }

        if (measurement.IsStale)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedStaleMeasurement,
                measurement.AttestationEvidenceClass,
                "Attestation visibility requires current evidence.");
        }

        if (!measurement.IsAttestationEvidenceOnly)
        {
            return Deny(
                SecureDebugAttestationVisibilityDecision.DeniedEvidencePolicy,
                measurement.AttestationEvidenceClass,
                "Attestation descriptor must remain evidence-only.");
        }

        return SecureDebugAttestationVisibilityResult.Classified(
            measurement.AttestationEvidenceClass,
            guestVisible: false,
            hostOnly: false,
            debugOnly: false,
            attestationOnly: true,
            string.Empty);
    }

    private static SecureDebugAttestationVisibilityResult Deny(
        SecureDebugAttestationVisibilityDecision decision,
        SecureEvidenceVisibilityClass visibilityClass,
        string reason) =>
        SecureDebugAttestationVisibilityResult.Denied(decision, visibilityClass, reason);
}
