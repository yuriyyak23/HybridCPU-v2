using System;
using System.Reflection;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Nested;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class VmxDescriptorReadinessPolicyAuditTests
{
    private const BindingFlags InstanceAnyVisibility =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    [Fact]
    public void VmcsV2Descriptor_ReadinessRemainsFailClosedEvenWhenVmreadCanProjectGuestState()
    {
        ExecutionDomainReadOnlyStateView view =
            ExecutionDomainReadOnlyStateView.FromGuestPcSpFlags(
                guestPc: 0x1000_2000UL,
                guestSp: 0x3000_4000UL,
                guestFlags: 0x202UL);
        var admissionService = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult vmread = admissionService.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.GuestPc,
                new ExecutionDomainDescriptor().WithReadOnlyState(view)));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyValueProjected, vmread.Decision);
        Assert.True(vmread.IsReadOnlyValueProjected);
        Assert.Equal(unchecked((long)view.GuestPc), vmread.Value);

        var descriptor = VmcsV2Descriptor.CreateDefault();
        VmcsV2ValidationResult migration = descriptor.ValidateMigrationReadiness();
        VmcsV2ValidationResult nested = descriptor.ValidateNestedEnablementReadiness();

        Assert.Equal(VmcsV2ValidationCode.GuestGprPersistenceIncomplete, migration.Code);
        Assert.Equal(
            VmcsV2BlockDirectory.GuestIntegerRegisterFileFieldId,
            migration.FieldId);
        Assert.Equal(VmcsV2ValidationCode.GuestGprPersistenceIncomplete, nested.Code);
        Assert.Equal(
            VmcsV2BlockDirectory.GuestIntegerRegisterFileFieldId,
            nested.FieldId);

        PropertyInfo? gprPersistence = typeof(VirtualCpuBlock).GetProperty(
            nameof(VirtualCpuBlock.GprPersistence),
            InstanceAnyVisibility);
        Assert.NotNull(gprPersistence);
        Assert.False(gprPersistence.SetMethod?.IsPublic ?? false);
        AssertNoInstanceMethod(typeof(VirtualCpuBlock), "MarkGprPersistenceComplete");
        AssertNoInstanceMethod(typeof(VirtualCpuBlock), "RestoreGuestIntegerRegisters");
    }

    [Fact]
    public void RestoreReadiness_RejectsCompatibilityProjectionMetadataAsAuthoritativeState()
    {
        var service = new RestoreValidationService();
        MigrationValidationPolicy policy = CreateSerializableGuestStatePolicy();

        DomainCheckpointImage compatibilityAuthority = new DomainCheckpointImage(
            DomainCheckpointAuthority.CompatibilityProjection,
            checkpointEpoch: 11,
            payloadMask: 0,
            evidenceMask: 0,
            containsCompatibilityProjectionMetadata: false)
            .WithPayload(MigrationPayloadClass.GuestArchitecturalState);
        RestoreValidationResult compatibilityAuthorityResult = service.ValidateRestore(
            compatibilityAuthority,
            policy,
            EvidenceRestorePolicy.PreserveGuestArchitecturalState,
            expectedCheckpointEpoch: 11);

        Assert.False(compatibilityAuthorityResult.IsAllowed);
        Assert.Equal(
            RestoreValidationDecision.CompatibilityProjectionDenied,
            compatibilityAuthorityResult.Decision);

        DomainCheckpointImage compatibilityMetadata = new DomainCheckpointImage(
            DomainCheckpointAuthority.DomainDescriptor,
            checkpointEpoch: 12,
            payloadMask: 0,
            evidenceMask: 0,
            containsCompatibilityProjectionMetadata: false)
            .WithPayload(MigrationPayloadClass.CompatibilityProjectionMetadata);
        RestoreValidationResult metadataResult = service.ValidateRestore(
            compatibilityMetadata,
            CreateCompatibilityMetadataRejectingPolicy(),
            EvidenceRestorePolicy.PreserveGuestArchitecturalState,
            expectedCheckpointEpoch: 12);

        Assert.False(metadataResult.IsAllowed);
        Assert.Equal(
            RestoreValidationDecision.CompatibilityProjectionDenied,
            metadataResult.Decision);

        MigrationValidationResult importResult =
            CreateCompatibilityMetadataRejectingPolicy().ValidateImport(
                MigrationPayloadClass.CompatibilityProjectionMetadata,
                EvidenceVisibilityClass.CompatibilityAlias,
                EvidenceRestorePolicy.PreserveGuestArchitecturalState);
        Assert.Equal(MigrationValidationDecision.PayloadClassDenied, importResult.Decision);
        Assert.Contains("not authoritative restore state", importResult.Message);
    }

    [Fact]
    public void MigrationReadiness_RequiresExplicitGuestStatePreservePolicyAndRejectsHostEvidence()
    {
        MigrationValidationPolicy policy = CreateSerializableGuestStatePolicy();

        MigrationValidationResult guestStateWrongPolicy = policy.ValidateImport(
            MigrationPayloadClass.GuestArchitecturalState,
            EvidenceVisibilityClass.GuestArchitecturalState,
            EvidenceRestorePolicy.RecomputeAfterRestore);
        Assert.Equal(
            MigrationValidationDecision.RestorePolicyRejected,
            guestStateWrongPolicy.Decision);

        MigrationValidationResult guestStatePreserved = policy.ValidateImport(
            MigrationPayloadClass.GuestArchitecturalState,
            EvidenceVisibilityClass.GuestArchitecturalState,
            EvidenceRestorePolicy.PreserveGuestArchitecturalState);
        Assert.True(guestStatePreserved.IsAllowed);

        DomainCheckpointImage hostEvidenceCheckpoint = CreateGuestStateCheckpoint(17)
            .WithPayload(MigrationPayloadClass.NativeTokenEvidence);
        RestoreValidationResult hostEvidenceResult = new RestoreValidationService().ValidateRestore(
            hostEvidenceCheckpoint,
            policy,
            EvidenceRestorePolicy.PreserveGuestArchitecturalState,
            expectedCheckpointEpoch: 17);

        Assert.False(hostEvidenceResult.IsAllowed);
        Assert.Equal(
            RestoreValidationDecision.MigrationPolicyDenied,
            hostEvidenceResult.Decision);
        Assert.Equal(
            MigrationValidationDecision.HostOwnedEvidenceRejected,
            hostEvidenceResult.MigrationResult.Decision);
    }

    [Fact]
    public void NestedReadiness_RequiresNeutralProjectionCheckpointAndRestorePolicy()
    {
        var service = new NestedDomainProjectionCheckpointService();
        NestedProjectionRequest projectionRequest = CreateAllowedNestedProjectionRequest();

        NestedDomainProjectionCheckpointResult missingCheckpoint = service.Validate(
            new NestedDomainProjectionCheckpointRequest(
                projectionRequest,
                Checkpoint: null,
                MigrationPolicy: null,
                EvidenceRestorePolicy.PreserveGuestArchitecturalState,
                ExpectedCheckpointEpoch: 21));
        Assert.Equal(
            NestedDomainProjectionCheckpointDecision.RestoreDenied,
            missingCheckpoint.Decision);
        Assert.Equal(
            RestoreValidationDecision.EmptyCheckpoint,
            missingCheckpoint.RestoreResult.Decision);

        NestedDomainProjectionCheckpointResult projectionDenied = service.Validate(
            new NestedDomainProjectionCheckpointRequest(
                CreateMissingDescriptorProjectionRequest(),
                CreateGuestStateCheckpoint(21),
                CreateSerializableGuestStatePolicy(),
                EvidenceRestorePolicy.PreserveGuestArchitecturalState,
                ExpectedCheckpointEpoch: 21));
        Assert.Equal(
            NestedDomainProjectionCheckpointDecision.ProjectionDenied,
            projectionDenied.Decision);
        Assert.Equal(
            NestedProjectionDecision.MissingDescriptor,
            projectionDenied.ProjectionResult.Decision);

        NestedDomainProjectionCheckpointResult failClosedPolicy = service.Validate(
            new NestedDomainProjectionCheckpointRequest(
                projectionRequest,
                CreateGuestStateCheckpoint(21),
                MigrationValidationPolicy.FailClosed,
                EvidenceRestorePolicy.PreserveGuestArchitecturalState,
                ExpectedCheckpointEpoch: 21));
        Assert.Equal(
            NestedDomainProjectionCheckpointDecision.RestoreDenied,
            failClosedPolicy.Decision);
        Assert.Equal(
            RestoreValidationDecision.MigrationPolicyDenied,
            failClosedPolicy.RestoreResult.Decision);
        Assert.Equal(
            MigrationValidationDecision.PayloadClassDenied,
            failClosedPolicy.RestoreResult.MigrationResult.Decision);

        NestedDomainProjectionCheckpointResult wrongRestorePolicy = service.Validate(
            new NestedDomainProjectionCheckpointRequest(
                projectionRequest,
                CreateGuestStateCheckpoint(21),
                CreateSerializableGuestStatePolicy(),
                EvidenceRestorePolicy.RecomputeAfterRestore,
                ExpectedCheckpointEpoch: 21));
        Assert.Equal(
            NestedDomainProjectionCheckpointDecision.RestoreDenied,
            wrongRestorePolicy.Decision);
        Assert.Equal(
            MigrationValidationDecision.RestorePolicyRejected,
            wrongRestorePolicy.RestoreResult.MigrationResult.Decision);
    }

    [Fact]
    public void DescriptorReadinessPolicySource_HasNoVmreadProjectionOrVmcsStoreFallback()
    {
        string descriptorSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs");
        string descriptorReadinessSource = ExtractBetween(
            descriptorSource,
            "public VmcsV2ValidationResult ValidateMigrationReadiness()",
            "public void RecordVectorExceptionExit");
        string neutralReadinessSource = descriptorReadinessSource +
            ActiveVmxConformanceHelpers.ReadProjectSource(
                "CloseToRTL/Core/Runtime/Nested/NestedDomainProjectionCheckpointService.cs",
                "CloseToRTL/Core/Runtime/Migration/Restore/RestoreValidationService.cs",
                "CloseToRTL/Core/Runtime/Migration/Validation/MigrationValidationPolicy.cs",
                "CloseToRTL/Core/Runtime/Migration/Checkpoint/DomainCheckpointImage.cs");

        foreach (string marker in new[]
                 {
                     "ValidateMigrationReadiness",
                     "ValidateNestedEnablementReadiness",
                     "GuestGprPersistenceIncomplete",
                     "NestedVmxDisabled",
                     "NestedProjectionService",
                     "RestoreValidationService",
                     "MigrationValidationPolicy",
                     "RejectCompatibilityProjectionMetadata",
                     "CompatibilityProjectionDenied",
                     "ContainsCompatibilityProjectionMetadata",
                 })
        {
            Assert.Contains(marker, neutralReadinessSource);
        }

        foreach (string forbidden in new[]
                 {
                     "AdmitVmReadProjection",
                     "VmcsReadOnlyValueProjectionService",
                     "ReadOnlyValueProjected",
                     "TryReadScalarField",
                     "VmcsFieldProjectionSchema",
                     "ExecutionDomainReadOnlyStateView",
                     "MemoryDomainReadOnlyTranslationView",
                     "CompatibilityControlDescriptor",
                     "ReadFieldValue(",
                     "WriteFieldValue(",
                     "HardwareWrite(",
                     "DirectWrite(",
                     "VmxCompatibilityAdmissionService",
                     "VmcsManager",
                     "IVmcsManager",
                     "VmxExecutionUnit",
                 })
        {
            Assert.DoesNotContain(forbidden, neutralReadinessSource);
        }
    }

    private static VmxCompatibilityVmReadAdmissionRequest CreateVmReadRequest(
        VmcsField field,
        ExecutionDomainDescriptor execution) =>
        new(
            Context: new DomainRuntimeContext(
                execution,
                memory: new MemoryDomainDescriptor(),
                io: new IoDomainDescriptor(),
                capabilities: new CapabilityDescriptorSet(
                    globalHardwareCaps: 0,
                    runtimeEnabledCaps: 0,
                    domainGrantedCaps: 0)),
            RootAuthority: new RootAuthorityDescriptor(
                RootAuthorityClass.RuntimeRoot,
                authorityEpoch: 1,
                grantedCapabilityMask: 0,
                allowCompatibilityFrontendActivation: true,
                allowAuthoritativeStateMutation: false),
            EvidencePolicy: new EvidencePolicyDescriptor(
                allowCompatibilityAliases: true,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            Descriptor: null,
            FieldId: (ushort)field,
            DestinationRegister: 3,
            FieldSelectorRegister: 1,
            ReservedRegister: 0,
            DescriptorValidated: true,
            CapabilityValidated: true,
            SchedulingValidated: true,
            NoEmissionValidated: true,
            ProjectionEvidenceValidated: true);

    private static NestedProjectionRequest CreateAllowedNestedProjectionRequest()
    {
        var descriptor = new NestedDomainDescriptor(
            NestedDomainAuthority.Runtime,
            parentDomainId: 1,
            childDomainId: 2,
            capabilities: NestedCapabilityGrantMask.RequiredForPhase7,
            domainCompositionEnabled: true,
            allowsCompatibilityProjection: true,
            hostEvidenceExcluded: true,
            lanePassthroughBlocked: true);

        return new NestedProjectionRequest(
            descriptor,
            NestedDomainRuntimeResult.Allowed,
            NestedCapabilityFilterResult.Allowed,
            NestedEvidencePolicyResult.Allowed,
            CompletionMapping: default,
            RequiresCompletionMapping: false,
            RequiresCompatibilityProjection: true);
    }

    private static NestedProjectionRequest CreateMissingDescriptorProjectionRequest() =>
        new(
            Descriptor: null,
            NestedDomainRuntimeResult.Allowed,
            NestedCapabilityFilterResult.Allowed,
            NestedEvidencePolicyResult.Allowed,
            CompletionMapping: default,
            RequiresCompletionMapping: false,
            RequiresCompatibilityProjection: true);

    private static DomainCheckpointImage CreateGuestStateCheckpoint(ulong epoch) =>
        new DomainCheckpointImage(
            DomainCheckpointAuthority.DomainDescriptor,
            checkpointEpoch: epoch,
            payloadMask: 0,
            evidenceMask: 0,
            containsCompatibilityProjectionMetadata: false)
            .WithPayload(MigrationPayloadClass.GuestArchitecturalState);

    private static MigrationValidationPolicy CreateSerializableGuestStatePolicy() =>
        new(
            new MigrationDescriptor(
                allowGuestArchitecturalState: true,
                allowDomainDescriptorState: true,
                allowCompatibilityProjectionMetadata: false),
            new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: true),
            rejectCompatibilityProjectionMetadata: true,
            requireGuestStatePreservePolicy: true);

    private static MigrationValidationPolicy CreateCompatibilityMetadataRejectingPolicy() =>
        new(
            new MigrationDescriptor(
                allowGuestArchitecturalState: true,
                allowDomainDescriptorState: true,
                allowCompatibilityProjectionMetadata: true),
            new EvidencePolicyDescriptor(
                allowCompatibilityAliases: true,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: true),
            rejectCompatibilityProjectionMetadata: true,
            requireGuestStatePreservePolicy: true);

    private static string ExtractBetween(
        string source,
        string startMarker,
        string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing start marker: {startMarker}");

        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing end marker: {endMarker}");

        return source[start..end];
    }

    private static void AssertNoInstanceMethod(Type type, string methodName) =>
        Assert.DoesNotContain(
            type.GetMethods(InstanceAnyVisibility),
            method => method.Name == methodName);
}
