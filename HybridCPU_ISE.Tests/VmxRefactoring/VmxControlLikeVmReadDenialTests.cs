using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class VmxControlLikeVmReadDenialTests
{
    [Fact]
    public void VmReadProjectionPath_KeepsRemainingControlLikeFieldsDeniedWithoutNeutralValueSource()
    {
        var service = new VmxCompatibilityAdmissionService();

        foreach ((VmcsField field,
                     VmcsReadOnlyValueProjectionDecision decision,
                     VmcsFieldProjectionOwner owner,
                     EvidenceVisibilityClass evidence) in RemainingDeniedControlLikeFields())
        {
            VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
                CreateVmReadRequest(field));

            Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
            Assert.True(result.RuntimeAdmissionAllowed);
            Assert.False(result.IsReadOnlyValueProjected);
            Assert.Equal(decision, result.ValueProjection.Decision);
            Assert.Equal(owner, result.ValueProjection.SchemaEntry.Owner);
            Assert.Equal(evidence, result.ValueProjection.SchemaEntry.EvidenceClass);
            Assert.Equal(0, result.Value);
        }
    }

    [Fact]
    public void VmReadProjectionPath_ControlLikeDenialsDoNotReuseOpenedNeutralSources()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult hostCr3 = service.AdmitVmReadProjection(
            CreateVmReadRequest(VmcsField.HostCr3));
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.HostAddressSpaceOwnerMissing,
            hostCr3.ValueProjection.Decision);
        Assert.Contains("no neutral host-address-space owner", hostCr3.Reason);
        Assert.DoesNotContain("translation control is not valid", hostCr3.Reason);

        VmxCompatibilityVmReadAdmissionResult hostCr0 = service.AdmitVmReadProjection(
            CreateVmReadRequest(VmcsField.HostCr0));
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.HostExecutionStateOwnerMissing,
            hostCr0.ValueProjection.Decision);
        Assert.Contains("no neutral host-execution owner", hostCr0.Reason);
        Assert.DoesNotContain("guest architectural state view", hostCr0.Reason);

        VmxCompatibilityVmReadAdmissionResult guestCr0 = service.AdmitVmReadProjection(
            CreateVmReadRequest(VmcsField.GuestCr0));
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.PrivilegedExecutionStateProjectionDenied,
            guestCr0.ValueProjection.Decision);
        Assert.Contains("privileged execution-state semantics", guestCr0.Reason);
        Assert.DoesNotContain("GuestPc", guestCr0.Reason);
    }

    [Fact]
    public void VmcsSchema_KeepsRemainingControlLikeFieldsReadOnlyAndWriteDenied()
    {
        foreach ((VmcsField field, _, _, _) in RemainingDeniedControlLikeFields())
        {
            Assert.True(VmcsFieldProjectionSchema.TryGet(field, out VmcsFieldProjectionSchemaEntry entry));
            Assert.True(VmcsFieldProjectionSchema.CanRead(entry));
            Assert.False(VmcsFieldProjectionSchema.CanWrite(entry));
            Assert.Equal(VmcsFieldProjectionAccessPolicy.ReadOnly, entry.AccessPolicy);
        }
    }

    [Fact]
    public void ControlLikeDenialSource_HasNoVmcsStoreOrControlBitMapperFallback()
    {
        string source = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs",
            "CloseToRTL/Core/Runtime/Domains/Descriptors/ExecutionDomain/ExecutionDomainReadOnlyStateView.cs",
            "CloseToRTL/Core/Runtime/Memory/Translation/MemoryDomainReadOnlyTranslationView.cs",
            "CloseToRTL/Core/Runtime/Capabilities/CompatibilityControls/CompatibilityControlDescriptor.cs");

        Assert.Contains("PrivilegedExecutionStateProjectionDenied", source);
        Assert.Contains("HostExecutionStateOwnerMissing", source);
        Assert.Contains("HostAddressSpaceOwnerMissing", source);
        Assert.Contains("CompatibilityControlValueProjectionDenied", source);
        Assert.Contains("KeepsControlValuesUnprojected", source);
        Assert.DoesNotContain("ProjectCompatibilityControl", source);
        Assert.DoesNotContain("VmcsFieldProjectionOwner.CompatibilityControlDescriptor =>", source);
        Assert.DoesNotContain("case VmcsField.GuestCr0", source);
        Assert.DoesNotContain("case VmcsField.GuestCr4", source);
        Assert.DoesNotContain("case VmcsField.HostCr0", source);
        Assert.DoesNotContain("case VmcsField.HostCr3", source);
        Assert.DoesNotContain("TryReadScalarField", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("HardwareWrite(", source);
        Assert.DoesNotContain("DirectWrite(", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("VmcsManager", source);
    }

    private static (VmcsField Field,
        VmcsReadOnlyValueProjectionDecision Decision,
        VmcsFieldProjectionOwner Owner,
        EvidenceVisibilityClass Evidence)[] RemainingDeniedControlLikeFields() =>
        new[]
        {
            (
                VmcsField.GuestCr0,
                VmcsReadOnlyValueProjectionDecision.PrivilegedExecutionStateProjectionDenied,
                VmcsFieldProjectionOwner.ExecutionDomainDescriptor,
                EvidenceVisibilityClass.GuestArchitecturalState),
            (
                VmcsField.GuestCr4,
                VmcsReadOnlyValueProjectionDecision.PrivilegedExecutionStateProjectionDenied,
                VmcsFieldProjectionOwner.ExecutionDomainDescriptor,
                EvidenceVisibilityClass.GuestArchitecturalState),
            (
                VmcsField.HostCr0,
                VmcsReadOnlyValueProjectionDecision.HostExecutionStateOwnerMissing,
                VmcsFieldProjectionOwner.ExecutionDomainDescriptor,
                EvidenceVisibilityClass.CompatibilityAlias),
            (
                VmcsField.HostCr3,
                VmcsReadOnlyValueProjectionDecision.HostAddressSpaceOwnerMissing,
                VmcsFieldProjectionOwner.MemoryDomainDescriptor,
                EvidenceVisibilityClass.CompatibilityAlias),
            (
                VmcsField.PinBasedControls,
                VmcsReadOnlyValueProjectionDecision.CompatibilityControlValueProjectionDenied,
                VmcsFieldProjectionOwner.CompatibilityControlDescriptor,
                EvidenceVisibilityClass.CompatibilityAlias),
            (
                VmcsField.ProcBasedControls,
                VmcsReadOnlyValueProjectionDecision.CompatibilityControlValueProjectionDenied,
                VmcsFieldProjectionOwner.CompatibilityControlDescriptor,
                EvidenceVisibilityClass.CompatibilityAlias),
            (
                VmcsField.ExitControls,
                VmcsReadOnlyValueProjectionDecision.CompatibilityControlValueProjectionDenied,
                VmcsFieldProjectionOwner.CompatibilityControlDescriptor,
                EvidenceVisibilityClass.CompatibilityAlias),
            (
                VmcsField.EntryControls,
                VmcsReadOnlyValueProjectionDecision.CompatibilityControlValueProjectionDenied,
                VmcsFieldProjectionOwner.CompatibilityControlDescriptor,
                EvidenceVisibilityClass.CompatibilityAlias),
            (
                VmcsField.SecondaryProcControls,
                VmcsReadOnlyValueProjectionDecision.CompatibilityControlValueProjectionDenied,
                VmcsFieldProjectionOwner.CompatibilityControlDescriptor,
                EvidenceVisibilityClass.CompatibilityAlias),
        };

    private static VmxCompatibilityVmReadAdmissionRequest CreateVmReadRequest(
        VmcsField field) =>
        new(
            Context: CreateContext(),
            RootAuthority: CreateRoot(),
            EvidencePolicy: CreateAliasAndGuestEvidencePolicy(),
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

    private static DomainRuntimeContext CreateContext() =>
        new(
            execution: new ExecutionDomainDescriptor().WithReadOnlyState(
                ExecutionDomainReadOnlyStateView.FromGuestPcSpFlags(
                    guestPc: 0x1000UL,
                    guestSp: 0x2000UL,
                    guestFlags: 0x202UL)),
            memory: CreateMemory(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(
                globalHardwareCaps: 0,
                runtimeEnabledCaps: 0,
                domainGrantedCaps: 0));

    private static MemoryDomainDescriptor CreateMemory() =>
        new(
            addressSpace: null,
            translationPolicy: null,
            translationControl: new MemoryDomainTranslationControl(
                TranslationEnabled: true,
                AddressSpaceTaggingEnabled: true,
                AddressSpaceRoot: 0x1234_5000UL,
                SecondStageRoot: 0xABCD_F000UL,
                DomainTag: 7,
                AddressSpaceTag: 9,
                AddressSpaceGeneration: 3,
                DefaultMemoryType: MemoryDomainTranslationControl.WriteBackMemoryType,
                AddressSpaceTargetCount: 2),
            dirtyTracking: null,
            ownsSecondStageTranslation: true);

    private static RootAuthorityDescriptor CreateRoot() =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: 0,
            allowCompatibilityFrontendActivation: true,
            allowAuthoritativeStateMutation: false);

    private static EvidencePolicyDescriptor CreateAliasAndGuestEvidencePolicy() =>
        new(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);
}
