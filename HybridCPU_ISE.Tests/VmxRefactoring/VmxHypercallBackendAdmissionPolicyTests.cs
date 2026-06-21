using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class VmxHypercallBackendAdmissionPolicyTests
{
    private const ulong HypercallBackendCapability = 1UL << 41;

    [Fact]
    public void MissingNeutralHypercallBackendOwner_KeepsBackendExecutionDenied()
    {
        NeutralTrapResult trap = CreateNeutralTrap();

        HypercallBackendAdmissionResult result =
            HypercallBackendAdmissionService.Default.Admit(
                HypercallBackendAdmissionRequest.MissingNeutralOwner(
                    trap,
                    RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                    CreateCapabilities(HypercallBackendCapability),
                    GuestArchitecturalEvidencePolicy(),
                    domainValidated: true));

        Assert.Equal(
            HypercallBackendAdmissionDecision.MissingBackendDescriptor,
            result.Decision);
        Assert.False(result.IsAllowed);
        Assert.True(result.DeniesBackendExecution);
        Assert.Contains("no neutral runtime backend descriptor", result.Reason);
    }

    [Fact]
    public void HypercallBackendAdmission_RequiresRuntimeAuthorityCapabilityAndEvidence()
    {
        NeutralTrapResult trap = CreateNeutralTrap();
        HypercallBackendDescriptor descriptor =
            HypercallBackendDescriptor.RuntimeOwnedDesignFence(
                CapabilityBoundaryRequirement.TypedGrant(
                    HypercallBackendCapability,
                    CapabilityGrantScope.DomainGranted),
                EvidenceBoundaryRequirement.GuestVisible(
                    EvidenceVisibilityClass.GuestArchitecturalState));

        HypercallBackendAdmissionResult missingCapability =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                trap,
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                descriptor,
                CapabilityDescriptorSet.Empty,
                GuestArchitecturalEvidencePolicy(),
                DomainValidated: true));

        Assert.Equal(
            HypercallBackendAdmissionDecision.DeniedCapability,
            missingCapability.Decision);
        Assert.True(missingCapability.DeniesBackendExecution);

        HypercallBackendAdmissionResult missingEvidence =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                trap,
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                descriptor,
                CreateCapabilities(HypercallBackendCapability),
                EvidencePolicyDescriptor.FailClosed,
                DomainValidated: true));

        Assert.Equal(
            HypercallBackendAdmissionDecision.DeniedEvidence,
            missingEvidence.Decision);
        Assert.True(missingEvidence.DeniesBackendExecution);

        HypercallBackendAdmissionResult backendOwnerMissing =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                trap,
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                descriptor,
                CreateCapabilities(HypercallBackendCapability),
                GuestArchitecturalEvidencePolicy(),
                DomainValidated: true));

        Assert.Equal(
            HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerMissing,
            backendOwnerMissing.Decision);
        Assert.True(backendOwnerMissing.DeniesBackendExecution);
        Assert.False(backendOwnerMissing.IsAllowed);
        Assert.Contains("neutral backend owner semantics are not materialized", backendOwnerMissing.Reason);
    }

    [Fact]
    public void HypercallBackendAdmission_DraftOwnerSkeletonRemainsDeniedWithoutAcceptedSemantics()
    {
        NeutralTrapResult trap = CreateNeutralTrap();
        HypercallBackendDescriptor descriptor =
            HypercallBackendDescriptor.RuntimeOwnedDraftOwnerFence(
                CapabilityBoundaryRequirement.None,
                EvidenceBoundaryRequirement.None,
                NeutralHypercallBackendOwnerDescriptor.DraftNoStateCandidate(
                    ownerId: 0x060A));

        HypercallBackendAdmissionResult result =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                trap,
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                descriptor,
                CapabilityDescriptorSet.Empty,
                EvidencePolicyDescriptor.FailClosed,
                DomainValidated: true));

        Assert.Equal(
            HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerRfcAdr,
            result.Decision);
        Assert.False(result.IsAllowed);
        Assert.True(result.DeniesBackendExecution);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.Contains("draft only", result.Reason);
        Assert.Contains("no accepted owner semantics", result.Reason);
    }

    [Theory]
    [InlineData(NeutralHypercallBackendOwnerSource.CompatibilityProjection)]
    [InlineData(NeutralHypercallBackendOwnerSource.CompatibilityStateVocabulary)]
    [InlineData(NeutralHypercallBackendOwnerSource.CapabilityProjection)]
    [InlineData(NeutralHypercallBackendOwnerSource.MigrationCheckpoint)]
    [InlineData(NeutralHypercallBackendOwnerSource.LaneStreamBoundary)]
    [InlineData(NeutralHypercallBackendOwnerSource.SecureComputeBoundary)]
    [InlineData(NeutralHypercallBackendOwnerSource.CompilerEmission)]
    public void HypercallBackendAdmission_DeniesNonNeutralOwnerAuthoritySources(
        NeutralHypercallBackendOwnerSource ownerSource)
    {
        NeutralTrapResult trap = CreateNeutralTrap();
        var owner = new NeutralHypercallBackendOwnerDescriptor(
            OwnerId: 0x060A,
            Source: ownerSource,
            RfcAdrId: NeutralHypercallBackendOwnerDescriptor.CandidateRfcAdrId,
            RfcAdrState: NeutralHypercallBackendOwnerRfcAdrState.DraftOnly,
            OperationClass:
                NeutralHypercallBackendOperationClass.NoStateNoPayloadDomainLocal,
            LeafSelection:
                NeutralHypercallBackendLeafSelection.CandidateOnlyNoNumericLeaf,
            NoPayloadOnly: true,
            DomainLocalOnly: true,
            NegativeTestsPresent: true);
        HypercallBackendDescriptor descriptor =
            HypercallBackendDescriptor.RuntimeOwnedDraftOwnerFence(
                CapabilityBoundaryRequirement.None,
                EvidenceBoundaryRequirement.None,
                owner);

        HypercallBackendAdmissionResult result =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                trap,
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                descriptor,
                CapabilityDescriptorSet.Empty,
                EvidencePolicyDescriptor.FailClosed,
                DomainValidated: true));

        Assert.Equal(
            HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerAuthority,
            result.Decision);
        Assert.True(result.DeniesBackendExecution);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Fact]
    public void HypercallBackendAdmission_DeniesDraftOwnerWithoutNegativeConformanceProof()
    {
        NeutralTrapResult trap = CreateNeutralTrap();
        HypercallBackendDescriptor descriptor =
            HypercallBackendDescriptor.RuntimeOwnedDraftOwnerFence(
                CapabilityBoundaryRequirement.None,
                EvidenceBoundaryRequirement.None,
                NeutralHypercallBackendOwnerDescriptor.DraftNoStateCandidate(
                    ownerId: 0x060A,
                    negativeTestsPresent: false));

        HypercallBackendAdmissionResult result =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                trap,
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                descriptor,
                CapabilityDescriptorSet.Empty,
                EvidencePolicyDescriptor.FailClosed,
                DomainValidated: true));

        Assert.Equal(
            HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerProof,
            result.Decision);
        Assert.True(result.DeniesBackendExecution);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Fact]
    public void HypercallBackendAdmission_RejectsCompatibilityProjectionOwnedBackendAuthority()
    {
        NeutralTrapResult trap = CreateNeutralTrap();
        var descriptor = new HypercallBackendDescriptor(
            HypercallBackendAuthority.CompatibilityProjection,
            CapabilityBoundaryRequirement.None,
            EvidenceBoundaryRequirement.None,
            requiresValidatedDomain: true,
            neutralBackendOwnerMaterialized: false);

        HypercallBackendAdmissionResult result =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                trap,
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                descriptor,
                CapabilityDescriptorSet.Empty,
                EvidencePolicyDescriptor.FailClosed,
                DomainValidated: true));

        Assert.Equal(
            HypercallBackendAdmissionDecision.DeniedBackendAuthority,
            result.Decision);
        Assert.True(result.DeniesBackendExecution);
        Assert.Contains("Compatibility projection cannot own hypercall backend execution", result.Reason);
    }

    [Fact]
    public void HypercallBackendAdmission_SourceHasNoVmxExitOrVmcsAuthority()
    {
        string source = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs",
            "CloseToRTL/Core/Runtime/Events/Hypercalls/NeutralHypercallBackendOwnerDescriptor.cs");

        Assert.Contains("HypercallBackendAdmissionService", source);
        Assert.Contains("NeutralHypercallBackendOwnerDescriptor", source);
        Assert.Contains("DraftOnly", source);
        Assert.Contains("CandidateOnlyNoNumericLeaf", source);
        Assert.DoesNotContain("NeutralHypercallBackendOwnerRfcAdrState.Accepted", source);
        Assert.DoesNotContain("Accepted = ", source);
        Assert.Contains("CapabilityBoundaryRequirement", source);
        Assert.Contains("EvidenceBoundaryRequirement", source);
        Assert.Contains("HypercallBackendAuthority.Runtime", source);
        Assert.DoesNotContain("HypercallBackendAdmissionDecision.Allowed", source);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("AllowedBackend", source);
        Assert.DoesNotContain("RuntimeOwnedPublication", source);
        Assert.DoesNotContain("VmExitReason", source);
        Assert.DoesNotContain("Vmx", source);
        Assert.DoesNotContain("VMX", source);
        Assert.DoesNotContain("Vmcs", source);
        Assert.DoesNotContain("VMCS", source);
        Assert.DoesNotContain("TrapDecision", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("VmcsManager", source);
    }

    private static NeutralTrapResult CreateNeutralTrap() =>
        NeutralTrapResult.Trap(
            TrapRequest.ForVmxOperation(
                VmxOperationKind.VmCall,
                IsaOpcodeValues.VMCALL,
                vtId: 1,
                executionDomainTag: 2,
                addressSpaceTag: 3),
            NeutralTrapResultKind.CompatibilityOperationIntercept);

    private static CapabilityDescriptorSet CreateCapabilities(ulong capability) =>
        new(new CapabilityGrantCollection(
        [
            new CapabilityGrant(
                capability,
                CapabilityGrantScope.DomainGranted,
                isGranted: true),
        ]));

    private static EvidencePolicyDescriptor GuestArchitecturalEvidencePolicy() =>
        new(
            allowCompatibilityAliases: false,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);
}
