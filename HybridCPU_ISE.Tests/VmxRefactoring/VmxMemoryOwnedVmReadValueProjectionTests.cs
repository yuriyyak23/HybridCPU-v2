using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class VmxMemoryOwnedVmReadValueProjectionTests
{
    [Fact]
    public void VmReadProjectionPath_ProjectsSelectedMemoryOwnedFieldsFromNeutralTranslationView()
    {
        MemoryDomainTranslationControl control = CreateTranslationControl(
            addressSpaceRoot: 0x1234_5000UL,
            secondStageRoot: 0xABCD_F000UL);
        MemoryDomainDescriptor memory = CreateMemory(control);

        AssertProjected(
            VmcsField.GuestCr3,
            unchecked((long)control.AddressSpaceRoot),
            EvidenceVisibilityClass.GuestArchitecturalState,
            memory);
        AssertProjected(
            VmcsField.EptPointer,
            unchecked((long)control.SecondStageRoot),
            EvidenceVisibilityClass.CompatibilityAlias,
            memory);
        AssertProjected(
            VmcsField.Vpid,
            control.AddressSpaceTag,
            EvidenceVisibilityClass.CompatibilityAlias,
            memory);
        AssertProjected(
            VmcsField.Cr3TargetCount,
            control.AddressSpaceTargetCount,
            EvidenceVisibilityClass.CompatibilityAlias,
            memory);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesGuestCr3WhenGuestArchitecturalEvidenceClosed()
    {
        var service = new VmxCompatibilityAdmissionService();
        MemoryDomainDescriptor memory = CreateMemory(CreateTranslationControl());

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.GuestCr3,
                memory,
                CreateAliasEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(VmcsReadOnlyValueProjectionDecision.AliasAccessDenied, result.ValueProjection.Decision);
        Assert.Equal(EvidenceVisibilityClass.GuestArchitecturalState, result.ValueProjection.SchemaEntry.EvidenceClass);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void VmReadProjectionPath_KeepsMemoryOwnedFieldsWithoutNeutralValueSourceDenied()
    {
        var service = new VmxCompatibilityAdmissionService();
        MemoryDomainDescriptor memory = CreateMemory(CreateTranslationControl());

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.HostCr3,
                memory,
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.HostAddressSpaceOwnerMissing,
            result.ValueProjection.Decision);
        Assert.Equal(VmcsFieldProjectionOwner.MemoryDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(0, result.Value);
        Assert.Contains("no neutral host-address-space owner", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesHostCr3BeforeGuestTranslationViewFallback()
    {
        var service = new VmxCompatibilityAdmissionService();
        MemoryDomainDescriptor memory = CreateMemory(new MemoryDomainTranslationControl(
            TranslationEnabled: true,
            AddressSpaceTaggingEnabled: true,
            AddressSpaceRoot: 0x1234_5001UL,
            SecondStageRoot: 0xABCD_F000UL,
            DomainTag: 7,
            AddressSpaceTag: 9,
            AddressSpaceGeneration: 3,
            DefaultMemoryType: MemoryDomainTranslationControl.WriteBackMemoryType));

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.HostCr3,
                memory,
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.HostAddressSpaceOwnerMissing,
            result.ValueProjection.Decision);
        Assert.Equal(VmcsFieldProjectionOwner.MemoryDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(0, result.Value);
        Assert.Contains("no neutral host-address-space owner", result.Reason);
        Assert.DoesNotContain("translation control is not valid", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_ProjectsZeroCr3TargetCountWhenNoNeutralTargetsAreMaterialized()
    {
        var service = new VmxCompatibilityAdmissionService();
        MemoryDomainDescriptor memory = CreateMemory(CreateTranslationControl(
            addressSpaceTargetCount: 0));

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.Cr3TargetCount,
                memory,
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyValueProjected, result.Decision);
        Assert.True(result.IsReadOnlyValueProjected);
        Assert.Equal(0, result.Value);
        Assert.Equal(VmcsFieldProjectionOwner.MemoryDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesCr3TargetCountWhenNeutralTargetCountIsInvalid()
    {
        var service = new VmxCompatibilityAdmissionService();
        MemoryDomainDescriptor memory = CreateMemory(CreateTranslationControl(
            addressSpaceTargetCount: MemoryDomainTranslationControl.MaxAddressSpaceTargetCount + 1));

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.Cr3TargetCount,
                memory,
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(VmcsReadOnlyValueProjectionDecision.MemorySourceDenied, result.ValueProjection.Decision);
        Assert.Contains("translation control is not valid", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesVpidWhenAddressSpaceTaggingIsNotNeutralMaterialized()
    {
        var service = new VmxCompatibilityAdmissionService();
        MemoryDomainDescriptor memory = CreateMemory(CreateTranslationControl(
            addressSpaceTaggingEnabled: false,
            addressSpaceTag: 0));

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.Vpid,
                memory,
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(VmcsReadOnlyValueProjectionDecision.MemorySourceDenied, result.ValueProjection.Decision);
        Assert.Equal(VmcsFieldProjectionOwner.MemoryDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(0, result.Value);
        Assert.Contains("neutral address-space tagging", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesEptPointerWhenMemoryDomainDoesNotOwnSecondStage()
    {
        var service = new VmxCompatibilityAdmissionService();
        MemoryDomainDescriptor memory = CreateMemory(
            CreateTranslationControl(),
            ownsSecondStageTranslation: false);

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.EptPointer,
                memory,
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.NeutralOwnerValueSourceMissing,
            result.ValueProjection.Decision);
        Assert.Equal(VmcsFieldProjectionOwner.MemoryDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesInvalidMemoryTranslationControl()
    {
        var service = new VmxCompatibilityAdmissionService();
        MemoryDomainDescriptor memory = CreateMemory(new MemoryDomainTranslationControl(
            TranslationEnabled: true,
            AddressSpaceTaggingEnabled: true,
            AddressSpaceRoot: 0x1234_5001UL,
            SecondStageRoot: 0xABCD_F000UL,
            DomainTag: 7,
            AddressSpaceTag: 9,
            AddressSpaceGeneration: 3,
            DefaultMemoryType: MemoryDomainTranslationControl.WriteBackMemoryType));

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.GuestCr3,
                memory,
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(VmcsReadOnlyValueProjectionDecision.MemorySourceDenied, result.ValueProjection.Decision);
        Assert.Equal(0, result.Value);
        Assert.Contains("translation control is not valid", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_MemoryOwnedSourceUsesNeutralDescriptorAndNoVmcsStoreFallback()
    {
        string source = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Domains/Descriptors/MemoryDomain/MemoryDomainDescriptor.cs",
            "CloseToRTL/Core/Runtime/Memory/Translation/MemoryDomainReadOnlyTranslationView.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");

        Assert.Contains("RuntimeBoundaryAdmissionService", source);
        Assert.Contains("VmcsFieldProjectionSchema.TryGet", source);
        Assert.Contains("VmcsFieldProjectionOwner.MemoryDomainDescriptor", source);
        Assert.Contains("TryCreateReadOnlyTranslationView", source);
        Assert.Contains("MemoryDomainReadOnlyTranslationView", source);
        Assert.Contains("OwnsSecondStageTranslation", source);
        Assert.Contains("AddressSpaceRoot", source);
        Assert.Contains("SecondStageRoot", source);
        Assert.Contains("AddressSpaceTaggingEnabled", source);
        Assert.Contains("AddressSpaceTag", source);
        Assert.Contains("AddressSpaceTargetCount", source);
        Assert.Contains("VmcsField.Vpid", source);
        Assert.Contains("VmcsField.Cr3TargetCount", source);
        Assert.Contains("HostAddressSpaceOwnerMissing", source);
        Assert.Contains("no neutral host-address-space owner", source);
        Assert.DoesNotContain("case VmcsField.HostCr3", source);
        Assert.DoesNotContain("TryReadScalarField", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("HardwareWrite(", source);
        Assert.DoesNotContain("DirectWrite(", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
    }

    private static void AssertProjected(
        VmcsField field,
        long expected,
        EvidenceVisibilityClass evidenceClass,
        MemoryDomainDescriptor memory)
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                field,
                memory,
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyValueProjected, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.True(result.IsReadOnlyValueProjected);
        Assert.Equal(expected, result.Value);
        Assert.True(result.ValueProjection.AliasAccess.IsAllowed);
        Assert.Equal(VmcsFieldProjectionOwner.MemoryDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(evidenceClass, result.ValueProjection.SchemaEntry.EvidenceClass);
        Assert.Equal(VmcsV2ValidationCode.Success, result.VmcsValidation.Code);
        Assert.Contains("neutral MemoryDomainDescriptor translation view", result.Reason);
    }

    private static VmxCompatibilityVmReadAdmissionRequest CreateVmReadRequest(
        VmcsField field,
        MemoryDomainDescriptor memory,
        EvidencePolicyDescriptor evidencePolicy) =>
        new(
            Context: CreateContext(memory),
            RootAuthority: CreateRoot(),
            EvidencePolicy: evidencePolicy,
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

    private static DomainRuntimeContext CreateContext(MemoryDomainDescriptor memory) =>
        new(
            execution: new ExecutionDomainDescriptor(),
            memory: memory,
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

    private static MemoryDomainDescriptor CreateMemory(
        MemoryDomainTranslationControl control,
        bool ownsSecondStageTranslation = true) =>
        new MemoryDomainDescriptor(
            addressSpace: null,
            translationPolicy: null,
            translationControl: control,
            dirtyTracking: null,
            ownsSecondStageTranslation: ownsSecondStageTranslation);

    private static MemoryDomainTranslationControl CreateTranslationControl(
        ulong addressSpaceRoot = 0x1234_5000UL,
        ulong secondStageRoot = 0xABCD_F000UL,
        bool addressSpaceTaggingEnabled = true,
        ushort addressSpaceTag = 9,
        byte addressSpaceTargetCount = 2) =>
        new(
            TranslationEnabled: true,
            AddressSpaceTaggingEnabled: addressSpaceTaggingEnabled,
            AddressSpaceRoot: addressSpaceRoot,
            SecondStageRoot: secondStageRoot,
            DomainTag: 7,
            AddressSpaceTag: addressSpaceTag,
            AddressSpaceGeneration: 3,
            DefaultMemoryType: MemoryDomainTranslationControl.WriteBackMemoryType,
            AddressSpaceTargetCount: addressSpaceTargetCount);
}
