using System;
using System.Reflection;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Nested;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class VmxNestedChildIntentHardeningTests
{
    private const BindingFlags InstanceAnyVisibility =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    [Fact]
    public void SecureNestedAdmission_KeepsChildIntentAsDesignFenceWithoutBackendSuccess()
    {
        SecureNestedDomainAdmissionPolicy policy = SecureNestedDomainAdmissionPolicy.Default;

        foreach (SecureNestedCheckpointPayloadClass payloadClass in new[]
                 {
                     SecureNestedCheckpointPayloadClass.Vmcs12Authority,
                     SecureNestedCheckpointPayloadClass.Vmcs02Authority,
                 })
        {
            SecureNestedDomainAdmissionResult vmcsAuthority =
                policy.AdmitCheckpointPayload(payloadClass);

            Assert.Equal(
                SecureNestedDomainAdmissionDecision.DeniedNestedVmcsAuthority,
                vmcsAuthority.Decision);
            Assert.False(vmcsAuthority.BackendSuccessAuthorized);
            Assert.False(vmcsAuthority.MutableNestedStateAuthorized);
        }

        SecureNestedDomainAdmissionResult mutableShadow =
            policy.AdmitCheckpointPayload(
                SecureNestedCheckpointPayloadClass.MutableShadowVmcsAuthority);
        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedMutableShadowVmcsAuthority,
            mutableShadow.Decision);
        Assert.False(mutableShadow.BackendSuccessAuthorized);
        Assert.False(mutableShadow.MutableNestedStateAuthorized);

        SecureNestedDomainAdmissionResult bridgeOnly =
            policy.AdmitCheckpointPayload(
                SecureNestedCheckpointPayloadClass.ShadowVmcsCompatibilityBridge);
        Assert.Equal(SecureNestedDomainAdmissionDecision.AllowedDesignFence, bridgeOnly.Decision);
        Assert.False(bridgeOnly.BackendSuccessAuthorized);
        Assert.False(bridgeOnly.MutableNestedStateAuthorized);

        SecureNestedDomainAdmissionResult missingOwner =
            policy.Admit(new SecureNestedDomainAdmissionRequest(
                ParentDescriptor: null,
                ParentBounds: SecureAuthorityBounds.None,
                ChildIntent: CreateSecureChildIntent(),
                CurrentEpoch: new SecureRevocationEpoch(7),
                HasNeutralChildIntentOwner: false,
                ParentHostEvidenceExposedToChild: false,
                ChildHostEvidenceExposedToParent: false,
                NestedProjectionExceedsParent: false,
                CheckpointPayloadClass: SecureNestedCheckpointPayloadClass.NeutralChildIntentDescriptor,
                ShadowVmcsStoresMutableAuthority: false));

        Assert.Equal(
            SecureNestedDomainAdmissionDecision.DeniedMissingChildIntentOwner,
            missingOwner.Decision);
        Assert.False(missingOwner.BackendSuccessAuthorized);
        Assert.False(missingOwner.MutableNestedStateAuthorized);
    }

    [Fact]
    public void ChildDomainIntentDescriptor_RemainsReadOnlyAndHasNoVmcs12OrMutableFieldStore()
    {
        Type descriptorType = typeof(ChildDomainIntentDescriptor);

        Assert.Null(descriptorType.GetField("_fields", InstanceAnyVisibility));
        Assert.Null(descriptorType.GetProperty("Generation", InstanceAnyVisibility));
        foreach (string methodName in new[]
                 {
                     "TryWriteIntentField",
                     "TryGetRawField",
                     "CreateSnapshot",
                     "RestoreSnapshot",
                     "AdvanceGeneration",
                     "TryVmRead",
                     "TryVmWrite",
                 })
        {
            Assert.DoesNotContain(
                descriptorType.GetMethods(InstanceAnyVisibility),
                method => method.Name == methodName);
        }

        var descriptor = new ChildDomainIntentDescriptor(childIntentPointer: 0x4000);
        Assert.True(descriptor.IsReadOnlyCompatibilityProjection);
        Assert.False(descriptor.ContainsHostEvidence(VmcsV2HostEvidenceKind.HostAcceleratorBackendHandles));
        Assert.False(descriptor.TryReadIntentField(
            VmcsV2BlockDirectory.CreateDefault(),
            ChildDomainIntentFieldIds.AddressSpaceTag,
            out long value,
            out ChildDomainIntentAccessResult result));
        Assert.Equal(0, value);
        Assert.Equal(ChildDomainIntentAccessDisposition.VmFail, result.Disposition);
        Assert.Contains("neutral runtime-owned nested intent state", result.Message);

        Type snapshotType = typeof(ChildDomainIntentSnapshot);
        Assert.NotNull(snapshotType.GetProperty("ChildIntentPointer"));
        Assert.Null(snapshotType.GetProperty("Vmcs12Pointer"));
        Assert.Null(snapshotType.GetProperty("Vmcs02Pointer"));
        Assert.Null(snapshotType.GetProperty("Fields"));
    }

    [Fact]
    public void ShadowVmcsCompatibilityBridge_FailsClosedBeforeNestedExecutionCanEnable()
    {
        VmcsV2Descriptor vmcs = VmcsV2Descriptor.CreateDefault();
        NestedEnablementRequest request = CreateFullNestedEnablementRequest();
        NestedDomainDescriptor domain = CreateComposedNestedDomain(
            NestedCapabilityGrantMask.RequiredForPhase7,
            allowsCompatibilityProjection: true);

        var shadowService = new ShadowVmcsNestedProjectionService(vmcs);
        Assert.True(ShadowVmcsNestedProjectionService.IsRetirementFenced);
        Assert.False(shadowService.TryEnable(domain, request, out NestedValidationResult nestedValidation));
        Assert.Equal(NestedValidationCode.CompatibilityProjectionFailed, nestedValidation.Code);
        Assert.Contains("removed without replacement", nestedValidation.Message);
        Assert.Contains("cannot bypass the neutral nested projection/checkpoint service", nestedValidation.Message);

        Assert.False(NestedDomainController.TryEnable(
            vmcs,
            request,
            out VmcsV2ValidationResult vmcsValidation));
        Assert.Equal(VmcsV2ValidationCode.InvalidVmcs12, vmcsValidation.Code);
        Assert.Contains("Shadow VMCS block was removed without replacement", vmcsValidation.Message);

        VmcsV2ValidationResult readiness = vmcs.ValidateNestedEnablementReadiness();
        Assert.False(readiness.Succeeded);
        Assert.Equal(VmcsV2ValidationCode.GuestGprPersistenceIncomplete, readiness.Code);
    }

    [Fact]
    public void NestedRuntimeProjection_DeniesMissingOwnerAndPublicationShortcuts()
    {
        NestedDomainDescriptor compatibilityOwned = new(
            NestedDomainAuthority.CompatibilityProjection,
            parentDomainId: 1,
            childDomainId: 2,
            capabilities: NestedCapabilityGrantMask.RequiredForPhase7,
            domainCompositionEnabled: true,
            allowsCompatibilityProjection: true,
            hostEvidenceExcluded: true,
            lanePassthroughBlocked: true);

        NestedCapabilityFilterResult compatibilityFilter =
            new NestedCapabilityFilter().Validate(new NestedCapabilityFilterRequest(
                compatibilityOwned,
                NestedCapabilityGrantMask.ChildDomainIntentDescriptor,
                RequiresHostEvidenceExclusion: true,
                RequiresLanePassthroughBlocked: true,
                RequiresDomainComposition: true));
        Assert.Equal(NestedCapabilityFilterDecision.RuntimeAuthorityRequired, compatibilityFilter.Decision);

        NestedDomainRuntimeResult runtimeDenied =
            new NestedDomainRuntime().Validate(new NestedDomainRuntimeRequest(
                compatibilityOwned,
                NestedCapabilityFilterResult.Allowed,
                RequiresCompatibilityProjection: true));
        Assert.Equal(NestedDomainRuntimeDecision.RuntimeAuthorityRequired, runtimeDenied.Decision);

        NestedDomainDescriptor noLaneFence = new(
            NestedDomainAuthority.Runtime,
            parentDomainId: 1,
            childDomainId: 2,
            capabilities: NestedCapabilityGrantMask.RequiredForPhase7,
            domainCompositionEnabled: true,
            allowsCompatibilityProjection: true,
            hostEvidenceExcluded: true,
            lanePassthroughBlocked: false);

        NestedEvidencePolicyResult evidenceDenied =
            new NestedEvidencePolicy().Validate(new NestedEvidencePolicyRequest(
                noLaneFence,
                new EvidencePolicyDescriptor(
                    allowCompatibilityAliases: false,
                    allowGuestArchitecturalState: true,
                    allowMigrationSerializableState: false),
                new ObservabilityDescriptor(
                    allowGuestArchitecturalObservability: true,
                    allowCompatibilityAliasObservability: false,
                    allowHostLocalRuntimeCapture: false),
                EvidenceVisibilityClass.HostOwnedRuntimeEvidence,
                RequiresGuestProjection: true));
        Assert.Equal(NestedEvidencePolicyDecision.HostOwnedEvidenceDenied, evidenceDenied.Decision);

        NestedCompletionMappingResult completionDenied =
            new NestedCompletionMapper().Validate(new NestedCompletionMappingRequest(
                CreateComposedNestedDomain(
                    NestedCapabilityGrantMask.RequiredForPhase7,
                    allowsCompatibilityProjection: true),
                new CompletionRouteDescriptor(
                    CompletionRouteAuthority.Runtime,
                    enabledSourceMask: 1,
                    requiresPostedEventQueue: true,
                    allowsCompatibilityProjection: false),
                CreateGuestVisibleCompletion(),
                RequiresCompatibilityProjection: true));
        Assert.Equal(
            NestedCompletionMappingDecision.CompatibilityProjectionDenied,
            completionDenied.Decision);
        Assert.False(completionDenied.IsAllowed);

        NestedProjectionResult projectionDenied =
            new NestedProjectionService().Validate(new NestedProjectionRequest(
                Descriptor: null,
                NestedDomainRuntimeResult.Allowed,
                NestedCapabilityFilterResult.Allowed,
                NestedEvidencePolicyResult.Allowed,
                CompletionMapping: default,
                RequiresCompletionMapping: false,
                RequiresCompatibilityProjection: true));
        Assert.Equal(NestedProjectionDecision.MissingDescriptor, projectionDenied.Decision);
    }

    [Fact]
    public void NestedChildIntentSource_DoesNotIntroduceExecutionPublicationOrVmcsAuthority()
    {
        string runtimeNestedSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Nested/Descriptors/NestedDomainDescriptor.cs",
            "CloseToRTL/Core/Runtime/Nested/Projection/NestedProjectionService.cs",
            "CloseToRTL/Core/Runtime/Nested/CapabilityFilter/NestedCapabilityFilter.cs",
            "CloseToRTL/Core/Runtime/Nested/Policies/NestedEvidencePolicy.cs",
            "CloseToRTL/Core/Runtime/Nested/NestedDomainProjectionCheckpointService.cs",
            "CloseToRTL/Core/Runtime/Domains/Admission/Nested/NestedDomainRuntime.cs");
        string secureNestedSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Nested/SecureChildDomainIntentDescriptor.cs",
            "CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Nested/SecureNestedDomainAdmissionPolicy.cs");
        string compatibilityNestedSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Nested/ChildDomainIntentDescriptor.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Nested/NestedCompletionMapper.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/NestedDomainControllerCompatibilityProjection.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs");
        string productionCallers = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs",
            "CloseToRTL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs",
            "CloseToRTL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs");

        Assert.Contains("NestedProjectionService", runtimeNestedSource);
        Assert.Contains("NestedCapabilityFilter", runtimeNestedSource);
        Assert.Contains("NestedEvidencePolicy", runtimeNestedSource);
        Assert.Contains("NestedDomainProjectionCheckpointService", runtimeNestedSource);
        Assert.DoesNotContain("VmcsManager", runtimeNestedSource);
        Assert.DoesNotContain("IVmcsManager", runtimeNestedSource);
        Assert.DoesNotContain("VmxExecutionUnit", runtimeNestedSource);
        Assert.DoesNotContain("ShadowVmcs", runtimeNestedSource);
        Assert.DoesNotContain("Vmcs12", runtimeNestedSource);
        Assert.DoesNotContain("Vmcs02", runtimeNestedSource);
        Assert.DoesNotContain("BackendSuccessAuthorized: true", runtimeNestedSource);
        Assert.DoesNotContain("MutableNestedStateAuthorized: true", runtimeNestedSource);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor.RuntimeOwnedPublication", runtimeNestedSource);
        Assert.DoesNotContain("VmxRetireEffect", runtimeNestedSource);
        Assert.DoesNotContain("CompletionRecord.FromCompatibilityExit", runtimeNestedSource);

        Assert.Contains("SecureChildDomainIntentDescriptor", secureNestedSource);
        Assert.Contains("DeniedNestedVmcsAuthority", secureNestedSource);
        Assert.Contains("DeniedMutableShadowVmcsAuthority", secureNestedSource);
        Assert.Contains("BackendSuccessAuthorized: false", secureNestedSource);
        Assert.Contains("MutableNestedStateAuthorized: false", secureNestedSource);
        Assert.DoesNotContain("BackendSuccessAuthorized: true", secureNestedSource);
        Assert.DoesNotContain("MutableNestedStateAuthorized: true", secureNestedSource);
        Assert.DoesNotContain("VmxCaps", secureNestedSource);
        Assert.DoesNotContain("VmxExecutionUnit", secureNestedSource);

        Assert.Contains("Child-domain intent field read requires neutral runtime-owned nested intent state", compatibilityNestedSource);
        Assert.Contains("Shadow VMCS block was removed without replacement", compatibilityNestedSource);
        Assert.Contains("CompatibilityProjectionFailed", compatibilityNestedSource);
        Assert.DoesNotContain("TryWriteIntentField", compatibilityNestedSource);
        Assert.DoesNotContain("TryVmWrite", compatibilityNestedSource);
        Assert.DoesNotContain("ReadFieldValue(", compatibilityNestedSource);
        Assert.DoesNotContain("WriteFieldValue(", compatibilityNestedSource);
        Assert.DoesNotContain("VmcsManager", compatibilityNestedSource);
        Assert.DoesNotContain("VmxExecutionUnit", compatibilityNestedSource);

        Assert.Contains("VmxRetireEffect.Fault", productionCallers);
        Assert.DoesNotContain("NestedDomainController.TryEnable", productionCallers);
        Assert.DoesNotContain("ShadowVmcsNestedProjectionService", productionCallers);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", productionCallers);
        Assert.DoesNotContain("VmxRetireEffect.VmCall", productionCallers);
        Assert.DoesNotContain("CompletionRecord.FromCompatibilityExit", productionCallers);
    }

    private static SecureChildDomainIntentDescriptor CreateSecureChildIntent() =>
        new(
            parentDomainTag: 1,
            childDomainTag: 2,
            requestedSecurityLevel: SecureComputeSecurityLevel.Measured,
            requestedBounds: SecureAuthorityBounds.None,
            derivation: SecurePolicyDerivationRecord.None,
            state: SecureChildDomainIntentState.Declared);

    private static NestedEnablementRequest CreateFullNestedEnablementRequest() =>
        new(
            CompatibilityCapabilityProjection: 0,
            NestedCapabilityGrantMask.RequiredForPhase7,
            NestedEnablementGate.RequiredForPhase7,
            OwnerDomainId: 1,
            AuthoritySource: NestedProofAuthoritySource.RuntimePolicyValidated);

    private static NestedDomainDescriptor CreateComposedNestedDomain(
        NestedCapabilityGrantMask capabilities,
        bool allowsCompatibilityProjection) =>
        new(
            NestedDomainAuthority.Runtime,
            parentDomainId: 1,
            childDomainId: 2,
            capabilities,
            domainCompositionEnabled: true,
            allowsCompatibilityProjection,
            hostEvidenceExcluded: true,
            lanePassthroughBlocked: true);

    private static CompletionSidebandEnvelope CreateGuestVisibleCompletion() =>
        new(
            new CompletionRecord(
                CompletionRecordClass.Trap,
                reasonCode: (uint)VmExitReason.VmCall,
                qualification: 0,
                faultAddress: 0,
                faultAux: 0),
            routeId: 1,
            sequence: 1,
            isHostOwnedEvidence: false,
            EvidenceVisibilityClass.GuestArchitecturalState);
}
