using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests;

public sealed class VmxGeneratedReadOnlyVmReadValueProjectionTests
{
    [Fact]
    public void VmReadProjectionPath_ProjectsCompletionOwnedFieldsAfterRuntimeAdmission()
    {
        VmxCompletionProjection expected = new(
            VmExitReason.VmCall,
            new VmxExitQualification(0x1234, VmxInvalidationScope.None, 0xABCD).Encode(),
            GuestPhysicalAddress: 0x4444,
            EptViolationQualification: 0x55);
        CompletionRecord completion = CreateCompatibilityCompletion(expected);

        AssertProjected(VmcsField.ExitReason, (long)expected.ExitReason, completion);
        AssertProjected(VmcsField.ExitQualification, unchecked((long)expected.ExitQualification), completion);
        AssertProjected(VmcsField.GuestPhysicalAddress, unchecked((long)expected.GuestPhysicalAddress), completion);
        AssertProjected(VmcsField.EptViolationQualification, unchecked((long)expected.EptViolationQualification), completion);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesCompletionOwnedFieldWithoutNeutralCompletionSource()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(VmcsField.ExitReason, completion: null));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.CompletionSourceMissing,
            result.ValueProjection.Decision);
        Assert.Equal(VmcsFieldProjectionOwner.CompletionRecord, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(0, result.Value);
        Assert.Equal(VmcsV2ValidationCode.AccessDenied, result.VmcsValidation.Code);
        Assert.Contains("neutral CompletionRecord source", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesExecutionOwnedFieldWithoutMaterializedStateView()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.GuestPc,
                CreateCompatibilityCompletion(),
                evidencePolicy: CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.ExecutionSourceMissing,
            result.ValueProjection.Decision);
        Assert.Equal(
            VmcsFieldProjectionOwner.ExecutionDomainDescriptor,
            result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(0, result.Value);
        Assert.Contains("no materialized read-only guest architectural state view", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_DoesNotEvaluateValueProjectionWhenRuntimeAdmissionFails()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.ExitReason,
                CreateCompatibilityCompletion(),
                evidencePolicy: new EvidencePolicyDescriptor(
                    allowCompatibilityAliases: false,
                    allowGuestArchitecturalState: true,
                    allowMigrationSerializableState: false)));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.RuntimeAdmissionDenied, result.Decision);
        Assert.False(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(VmcsReadOnlyValueProjectionDecision.NotEvaluated, result.ValueProjection.Decision);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void VmReadProjectionPath_SourceUsesSchemaOwnerAndNoVmcsFieldStoreFallback()
    {
        string source = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");

        Assert.Contains("RuntimeBoundaryAdmissionService", source);
        Assert.Contains("DomainRuntimeOperationKind.ReadCompatibilityProjection", source);
        Assert.Contains("VmcsFieldProjectionSchema.TryGet", source);
        Assert.Contains("VmcsFieldProjectionOwner.CompletionRecord", source);
        Assert.Contains("VmcsFieldProjectionOwner.ExecutionDomainDescriptor", source);
        Assert.Contains("CompletionProjectionService", source);
        Assert.Contains("TryCreateReadOnlyStateView", source);
        Assert.Contains("EvidenceVisibilityClass.CompatibilityAlias", source);
        Assert.DoesNotContain("TryReadScalarField", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("VmxRetireEffect.VmcsRead", source);
    }

    private static void AssertProjected(
        VmcsField field,
        long expected,
        CompletionRecord completion)
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(field, completion));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyValueProjected, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.True(result.IsReadOnlyValueProjected);
        Assert.Equal(expected, result.Value);
        Assert.True(result.ValueProjection.AliasAccess.IsAllowed);
        Assert.Equal(VmcsFieldProjectionOwner.CompletionRecord, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(EvidenceVisibilityClass.CompatibilityAlias, result.ValueProjection.SchemaEntry.EvidenceClass);
        Assert.Equal(VmcsV2ValidationCode.Success, result.VmcsValidation.Code);
        Assert.Contains("neutral CompletionRecord owner", result.Reason);
    }

    private static VmxCompatibilityVmReadAdmissionRequest CreateVmReadRequest(
        VmcsField field,
        CompletionRecord? completion,
        EvidencePolicyDescriptor? evidencePolicy = null) =>
        new(
            Context: CreateContext(),
            RootAuthority: CreateRoot(),
            EvidencePolicy: evidencePolicy ?? CreateAliasEvidencePolicy(),
            Descriptor: null,
            FieldId: (ushort)field,
            DestinationRegister: 3,
            FieldSelectorRegister: 1,
            ReservedRegister: 0,
            DescriptorValidated: true,
            CapabilityValidated: true,
            SchedulingValidated: true,
            NoEmissionValidated: true,
            ProjectionEvidenceValidated: true,
            Completion: completion);

    private static DomainRuntimeContext CreateContext() =>
        new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(
                globalHardwareCaps: 0,
                runtimeEnabledCaps: 0,
                domainGrantedCaps: 0));

    private static RootAuthorityDescriptor CreateRoot() =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: 0,
            allowCompatibilityFrontendActivation: true,
            allowAuthoritativeStateMutation: false);

    private static EvidencePolicyDescriptor CreateAliasEvidencePolicy() =>
        new(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: false,
            allowMigrationSerializableState: false);

    private static EvidencePolicyDescriptor CreateAliasAndGuestEvidencePolicy() =>
        new(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);

    private static CompletionRecord CreateCompatibilityCompletion() =>
        CreateCompatibilityCompletion(new VmxCompletionProjection(
            VmExitReason.VmCall,
            VmxExitQualification.None.Encode(),
            GuestPhysicalAddress: 0,
            EptViolationQualification: 0));

    private static CompletionRecord CreateCompatibilityCompletion(
        VmxCompletionProjection projection)
    {
        NeutralTrapResult trap = NeutralTrapResult.Trap(
            TrapRequest.ForCompatibilityOperation((byte)VmxOperationKind.VmCall, opcode: 0, vtId: 0),
            NeutralTrapResultKind.CompatibilityOperationIntercept);

        TrapCompletionPublicationFenceResult publicationFence =
            TrapCompletionPublicationFence.Default.Evaluate(
                trap,
                runtimeAdmissionAllowed: true,
                completionPublicationAuthorized: true,
                retirePublicationAuthorized: true,
                neutralReasonCode: (uint)NeutralTrapResultKind.CompatibilityOperationIntercept,
                evidenceClass: EvidenceVisibilityClass.CompatibilityAlias,
                migrationClass: TrapCompletionMigrationClass.RecomputedAfterRestore);

        return CompletionRecord.FromCompatibilityExit(
            publicationFence,
            projection.ExitReason,
            new VmxExitQualification(
                unchecked((ushort)(projection.ExitQualification & 0xFFFFUL)),
                VmxInvalidationScope.None,
                projection.ExitQualification >> 32),
            projection.GuestPhysicalAddress,
            projection.EptViolationQualification);
    }
}
