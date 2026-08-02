using System;
namespace YAKSys_Hybrid_CPU.Core.Nested;

[Flags]
public enum NestedEnablementGate : ulong
{
    None = 0,
    NestedCompatibilityPolicyEnabled = 1UL << 0,
    CompleteGprPersistence = 1UL << 1,
    HostEvidenceExcluded = 1UL << 2,
    BundleLegalityReady = 1UL << 3,
    InterruptFabricReady = 1UL << 4,
    SecurityIsolationReady = 1UL << 5,
    NestedMemoryCompositionReady = 1UL << 6,
    InterceptTranslationReady = 1UL << 7,
    Lane6Lane7PassthroughBlocked = 1UL << 8,

    RequiredForPhase7 =
        NestedCompatibilityPolicyEnabled |
        CompleteGprPersistence |
        HostEvidenceExcluded |
        BundleLegalityReady |
        InterruptFabricReady |
        SecurityIsolationReady |
        NestedMemoryCompositionReady |
        InterceptTranslationReady |
        Lane6Lane7PassthroughBlocked,
}

[Flags]
public enum NestedCapabilityGrantMask : ulong
{
    None = 0,
    ChildDomainIntentDescriptor = 1UL << 0,
    ComposedDomainProjection = 1UL << 1,
    VmReadVmWriteBitmaps = 1UL << 2,
    InterceptTranslation = 1UL << 3,
    ExitMapping = 1UL << 4,
    NestedMemoryComposition = 1UL << 5,
    VirtualInterruptReflection = 1UL << 6,
    PreemptionTimerReflection = 1UL << 7,
    LanePassthroughBlocked = 1UL << 8,

    RequiredForPhase7 =
        ChildDomainIntentDescriptor |
        ComposedDomainProjection |
        VmReadVmWriteBitmaps |
        InterceptTranslation |
        ExitMapping |
        NestedMemoryComposition |
        VirtualInterruptReflection |
        PreemptionTimerReflection |
        LanePassthroughBlocked,
}

public readonly record struct PublishedCapabilityProjection(
    ulong CompatibilityWord,
    bool DescriptorBacked,
    bool PolicyFiltered)
{
    public bool IsProjectionOnly => DescriptorBacked && PolicyFiltered;

    public bool Contains(ulong capabilityMask) =>
        capabilityMask != 0 &&
        (CompatibilityWord & capabilityMask) == capabilityMask;

    public static PublishedCapabilityProjection FromCompatibilityAlias(ulong compatibilityWord) =>
        new(
            compatibilityWord,
            DescriptorBacked: false,
            PolicyFiltered: false);

    public static PublishedCapabilityProjection FromTypedGrant(CapabilityGrant grant) =>
        new(
            grant.IsPublishableCompatibilityGrant ? grant.CapabilityMask : 0,
            DescriptorBacked: true,
            PolicyFiltered: true);
}

public enum NestedDomainCapability : ushort
{
    None = 0,
    NestedCompatibilityProjection = 1,
}

public readonly record struct NestedDomainCapabilityProjection(
    NestedDomainCapability Capability,
    ulong FrontendCapabilityMask)
{
    public bool IsValid =>
        Capability != NestedDomainCapability.None &&
        FrontendCapabilityMask != 0;

    public static NestedDomainCapabilityProjection VmxCompatibility { get; } =
        new(
            NestedDomainCapability.NestedCompatibilityProjection,
            VmxV2InstructionCaps.NestedVmx);
}

public readonly record struct NestedCompatibilityCapabilityRequirement(
    NestedDomainCapabilityProjection Projection,
    CapabilityGrant RequiredGrant)
{
    public NestedDomainCapability Capability => Projection.Capability;

    public ulong CapabilityMask => Projection.FrontendCapabilityMask;

    public bool IsTypedAuthority =>
        Projection.IsValid &&
        RequiredGrant.HasTypedAuthority &&
        RequiredGrant.IsPublishableCompatibilityGrant &&
        RequiredGrant.Grants(CapabilityMask);

    public static NestedCompatibilityCapabilityRequirement Create(
        ulong ownerDomainId) =>
        new(
            NestedDomainCapabilityProjection.VmxCompatibility,
            new CapabilityGrant(
                VmxV2InstructionCaps.NestedVmx,
                CapabilityGrantScope.CompatibilityProjection,
                isGranted: ownerDomainId != CapabilityGrant.NoOwnerDomainId,
                ownerDomainId,
                CapabilityDelegationPolicy.NonDelegable,
                CapabilityRevocationPolicy.RuntimeRevocable,
                CapabilityMigrationClass.DomainLocal,
                CapabilityEvidenceVisibility.GuestVisibleProjection,
                CapabilityFrontendProjectionPolicy.ProjectIfCompatible));
}

public readonly record struct NestedCapabilityPublication(
    PublishedCapabilityProjection Projection,
    NestedCompatibilityCapabilityRequirement Requirement)
{
    public bool IsDescriptorBacked =>
        Projection.IsProjectionOnly &&
        Requirement.IsTypedAuthority;

    public bool CanPublishRequiredCapability =>
        IsDescriptorBacked &&
        Projection.Contains(Requirement.CapabilityMask);

    public static NestedCapabilityPublication FromCompatibilityAlias(
        ulong compatibilityWord,
        ulong ownerDomainId) =>
        new(
            PublishedCapabilityProjection.FromCompatibilityAlias(compatibilityWord),
            NestedCompatibilityCapabilityRequirement.Create(ownerDomainId));

    public static NestedCapabilityPublication FromTypedGrant(
        ulong ownerDomainId)
    {
        NestedCompatibilityCapabilityRequirement requirement =
            NestedCompatibilityCapabilityRequirement.Create(ownerDomainId);

        return new(
            PublishedCapabilityProjection.FromTypedGrant(requirement.RequiredGrant),
            requirement);
    }
}

public readonly record struct NestedCapabilityGrantDescriptor(
    NestedCapabilityGrantMask Capability,
    CapabilityGrantScope Scope,
    bool DescriptorBacked,
    bool EvidencePolicyBound)
{
    public bool IsAuthoritative =>
        Capability != NestedCapabilityGrantMask.None &&
        Scope == CapabilityGrantScope.DomainGranted &&
        DescriptorBacked &&
        EvidencePolicyBound;
}

public readonly record struct NestedProofAuthoritySource(
    bool DescriptorBacked,
    bool EvidencePolicyBound,
    EvidenceVisibilityClass GateEvidenceClass,
    bool RuntimeValidated)
{
    public bool CanAuthorizeCapabilityProof =>
        DescriptorBacked &&
        EvidencePolicyBound;

    public bool CanAuthorizeGateProof =>
        RuntimeValidated &&
        GateEvidenceClass == EvidenceVisibilityClass.GuestArchitecturalState;

    public static NestedProofAuthoritySource FailClosed { get; } =
        new(
            DescriptorBacked: false,
            EvidencePolicyBound: false,
            EvidenceVisibilityClass.HostOwnedRuntimeEvidence,
            RuntimeValidated: false);

    public static NestedProofAuthoritySource RuntimePolicyValidated { get; } =
        new(
            DescriptorBacked: true,
            EvidencePolicyBound: true,
            EvidenceVisibilityClass.GuestArchitecturalState,
            RuntimeValidated: true);
}

public readonly record struct NestedEnablementGateDescriptor(
    NestedEnablementGate Gate,
    EvidenceVisibilityClass EvidenceClass,
    bool RuntimeValidated)
{
    public bool IsAuthoritative =>
        Gate != NestedEnablementGate.None &&
        RuntimeValidated &&
        EvidenceClass == EvidenceVisibilityClass.GuestArchitecturalState;
}

public readonly record struct NestedEnablementProof(
    NestedCapabilityGrantDescriptor[] CapabilityGrants,
    NestedEnablementGateDescriptor[] GateProofs)
{
    public bool HasRequiredCapabilities
    {
        get
        {
            NestedCapabilityGrantMask granted = NestedCapabilityGrantMask.None;

            foreach (NestedCapabilityGrantDescriptor grant in CapabilityGrants ?? Array.Empty<NestedCapabilityGrantDescriptor>())
            {
                if (grant.IsAuthoritative)
                {
                    granted |= grant.Capability;
                }
            }

            return (granted & NestedCapabilityGrantMask.RequiredForPhase7) ==
                NestedCapabilityGrantMask.RequiredForPhase7;
        }
    }

    public bool HasRequiredGates
    {
        get
        {
            NestedEnablementGate proven = NestedEnablementGate.None;

            foreach (NestedEnablementGateDescriptor gate in GateProofs ?? Array.Empty<NestedEnablementGateDescriptor>())
            {
                if (gate.IsAuthoritative)
                {
                    proven |= gate.Gate;
                }
            }

            return (proven & NestedEnablementGate.RequiredForPhase7) ==
                NestedEnablementGate.RequiredForPhase7;
        }
    }

    public bool IsAuthoritative =>
        HasRequiredCapabilities &&
        HasRequiredGates;

    public static NestedEnablementProof FromTypedDescriptorProofs(
        NestedCapabilityGrantMask capabilities,
        NestedEnablementGate gates,
        NestedProofAuthoritySource authoritySource)
    {
        NestedCapabilityGrantDescriptor[] capabilityGrants =
        {
            CreateGrant(capabilities, NestedCapabilityGrantMask.ChildDomainIntentDescriptor, authoritySource),
            CreateGrant(capabilities, NestedCapabilityGrantMask.ComposedDomainProjection, authoritySource),
            CreateGrant(capabilities, NestedCapabilityGrantMask.VmReadVmWriteBitmaps, authoritySource),
            CreateGrant(capabilities, NestedCapabilityGrantMask.InterceptTranslation, authoritySource),
            CreateGrant(capabilities, NestedCapabilityGrantMask.ExitMapping, authoritySource),
            CreateGrant(capabilities, NestedCapabilityGrantMask.NestedMemoryComposition, authoritySource),
            CreateGrant(capabilities, NestedCapabilityGrantMask.VirtualInterruptReflection, authoritySource),
            CreateGrant(capabilities, NestedCapabilityGrantMask.PreemptionTimerReflection, authoritySource),
            CreateGrant(capabilities, NestedCapabilityGrantMask.LanePassthroughBlocked, authoritySource),
        };

        NestedEnablementGateDescriptor[] gateProofs =
        {
            CreateGate(gates, NestedEnablementGate.NestedCompatibilityPolicyEnabled, authoritySource),
            CreateGate(gates, NestedEnablementGate.CompleteGprPersistence, authoritySource),
            CreateGate(gates, NestedEnablementGate.HostEvidenceExcluded, authoritySource),
            CreateGate(gates, NestedEnablementGate.BundleLegalityReady, authoritySource),
            CreateGate(gates, NestedEnablementGate.InterruptFabricReady, authoritySource),
            CreateGate(gates, NestedEnablementGate.SecurityIsolationReady, authoritySource),
            CreateGate(gates, NestedEnablementGate.NestedMemoryCompositionReady, authoritySource),
            CreateGate(gates, NestedEnablementGate.InterceptTranslationReady, authoritySource),
            CreateGate(gates, NestedEnablementGate.Lane6Lane7PassthroughBlocked, authoritySource),
        };

        return new NestedEnablementProof(capabilityGrants, gateProofs);
    }

    private static NestedCapabilityGrantDescriptor CreateGrant(
        NestedCapabilityGrantMask source,
        NestedCapabilityGrantMask capability,
        NestedProofAuthoritySource authoritySource) =>
        new(
            capability,
            CapabilityGrantScope.DomainGranted,
            DescriptorBacked: (source & capability) == capability &&
                authoritySource.CanAuthorizeCapabilityProof,
            EvidencePolicyBound: (source & capability) == capability &&
                authoritySource.CanAuthorizeCapabilityProof);

    private static NestedEnablementGateDescriptor CreateGate(
        NestedEnablementGate source,
        NestedEnablementGate gate,
        NestedProofAuthoritySource authoritySource) =>
        new(
            gate,
            authoritySource.GateEvidenceClass,
            RuntimeValidated: (source & gate) == gate &&
                authoritySource.CanAuthorizeGateProof);
}

public readonly record struct NestedEnablementRequest(
    ulong CompatibilityCapabilityProjection,
    NestedCapabilityGrantMask NestedCapabilities,
    NestedEnablementGate ProvenGates,
    ulong OwnerDomainId = CapabilityGrant.NoOwnerDomainId,
    NestedProofAuthoritySource AuthoritySource = default)
{
    public ulong CompatibilityProjectionWord => CompatibilityCapabilityProjection;

    public PublishedCapabilityProjection CompatibilityCapabilities =>
        PublishedCapabilityProjection.FromCompatibilityAlias(CompatibilityProjectionWord);

    public NestedCapabilityPublication CapabilityPublication =>
        NestedCapabilityPublication.FromTypedGrant(OwnerDomainId);

    public NestedEnablementProof EnablementProof =>
        NestedEnablementProof.FromTypedDescriptorProofs(
            NestedCapabilities,
            ProvenGates,
            AuthoritySource);

    public bool HasExplicitVmxCapability =>
        CapabilityPublication.CanPublishRequiredCapability;

    public bool HasRequiredNestedCapabilities =>
        EnablementProof.HasRequiredCapabilities;

    public bool HasRequiredGates =>
        EnablementProof.HasRequiredGates;
}

public static partial class NestedDomainController
{
    public static bool TryEnable(
        NestedDomainDescriptor domain,
        INestedProjectionService projectionService,
        NestedEnablementRequest request,
        out NestedValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(projectionService);

        if (!domain.CanProjectToCompatibilityFrontend)
        {
            validation = NestedValidationResult.Fail(
                NestedValidationCode.ProjectionDenied,
                "Nested domain must be runtime-authoritative and composition-ready before compatibility projection can be enabled.");
            return false;
        }

        return projectionService.TryEnable(domain, request, out validation);
    }

    public static void Disable(
        NestedDomainDescriptor domain,
        INestedProjectionService projectionService)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(projectionService);
        projectionService.Disable(domain);
    }
}
