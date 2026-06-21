using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests;

public sealed class VmxCompatibilityControlOwnerDesignTests
{
    [Fact]
    public void CompatibilityControlDescriptor_DefaultsToNoMaterializedReadOnlyView()
    {
        CompatibilityControlDescriptor descriptor = CompatibilityControlDescriptor.NotMaterialized;

        Assert.True(descriptor.IsRuntimeAuthoritativeControlOwner);
        Assert.False(descriptor.HasMaterializedReadOnlyProjection);
        Assert.False(descriptor.TryCreateReadOnlyControlView(
            out CompatibilityControlReadOnlyView view,
            out string reason));
        Assert.False(view.IsMaterialized);
        Assert.Contains("no materialized read-only control view", reason);
    }

    [Fact]
    public void CompatibilityControlDescriptor_MaterializesFailClosedNeutralSemantics()
    {
        CompatibilityControlDescriptor descriptor = CompatibilityControlDescriptor.FailClosedProjectionOnly;

        Assert.True(descriptor.IsRuntimeAuthoritativeControlOwner);
        Assert.Equal(
            CompatibilityControlMaterializationState.ReadOnlyProjectionAvailable,
            descriptor.MaterializationState);
        Assert.True(descriptor.HasMaterializedReadOnlyProjection);
        Assert.True(descriptor.TryCreateReadOnlyControlView(
            out CompatibilityControlReadOnlyView view,
            out string reason));
        Assert.True(view.IsMaterialized);
        Assert.True(view.IsSemanticallyComplete);
        Assert.True(view.RequiresRuntimeBoundaryAdmission);
        Assert.True(view.DeniesWrites);
        Assert.True(view.DeniesBackendExecution);
        Assert.True(view.DeniesAuthoritativeMutation);
        Assert.True(view.RequiresNeutralPublicationFence);
        Assert.True(view.KeepsControlValuesUnprojected);
        Assert.Contains("materialized read-only control view", reason);
    }

    [Fact]
    public void VmReadProjectionPath_KeepsCompatibilityControlFieldsDeniedUntilVmreadMapperAdmitsControlSource()
    {
        CompatibilityControlDescriptor descriptor = CompatibilityControlDescriptor.FailClosedProjectionOnly;
        var service = new VmxCompatibilityAdmissionService();

        Assert.True(descriptor.TryCreateReadOnlyControlView(
            out CompatibilityControlReadOnlyView view,
            out _));
        Assert.True(view.KeepsControlValuesUnprojected);

        foreach (VmcsField field in new[]
                 {
                     VmcsField.PinBasedControls,
                     VmcsField.ProcBasedControls,
                     VmcsField.ExitControls,
                     VmcsField.EntryControls,
                     VmcsField.SecondaryProcControls,
                 })
        {
            VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
                CreateVmReadRequest(field));

            Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
            Assert.True(result.RuntimeAdmissionAllowed);
            Assert.False(result.IsReadOnlyValueProjected);
            Assert.Equal(
                VmcsReadOnlyValueProjectionDecision.CompatibilityControlValueProjectionDenied,
                result.ValueProjection.Decision);
            Assert.Equal(
                VmcsFieldProjectionOwner.CompatibilityControlDescriptor,
                result.ValueProjection.SchemaEntry.Owner);
            Assert.Equal(0, result.Value);
            Assert.Contains("intentionally denied", result.Reason);
            Assert.Contains("no frozen VMX control-bit value projection contract", result.Reason);
        }
    }

    [Fact]
    public void CompatibilityControlOwnerSource_RemainsNeutralAndDisconnectedFromVmreadProjection()
    {
        string runtimeSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Capabilities/CompatibilityControls/CompatibilityControlDescriptor.cs");
        string projectionSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");

        Assert.Contains("CompatibilityControlDescriptor", runtimeSource);
        Assert.Contains("TryCreateReadOnlyControlView", runtimeSource);
        Assert.Contains("EventRoutingPolicy", runtimeSource);
        Assert.Contains("ExecutionPolicy", runtimeSource);
        Assert.Contains("ExitPublicationPolicy", runtimeSource);
        Assert.Contains("FailClosedProjectionOnly", runtimeSource);
        Assert.Contains("ControlValueProjectionDenied", runtimeSource);
        Assert.Contains("FromNeutralSemantics", runtimeSource);
        Assert.Contains("KeepsControlValuesUnprojected", runtimeSource);
        Assert.DoesNotContain("VmcsField", runtimeSource);
        Assert.DoesNotContain("PinBasedControls", runtimeSource);
        Assert.DoesNotContain("ProcBasedControls", runtimeSource);
        Assert.DoesNotContain("ExitControls", runtimeSource);
        Assert.DoesNotContain("EntryControls", runtimeSource);
        Assert.DoesNotContain("SecondaryProcControls", runtimeSource);

        Assert.Contains("VmcsFieldProjectionOwner.CompletionRecord", projectionSource);
        Assert.Contains("VmcsFieldProjectionOwner.MemoryDomainDescriptor", projectionSource);
        Assert.Contains("CompatibilityControlValueProjectionDenied", projectionSource);
        Assert.DoesNotContain("ProjectCompatibilityControl", projectionSource);
        Assert.DoesNotContain("VmcsFieldProjectionOwner.CompatibilityControlDescriptor =>", projectionSource);
        Assert.DoesNotContain("PinBasedControls", projectionSource);
        Assert.DoesNotContain("ProcBasedControls", projectionSource);
        Assert.DoesNotContain("ExitControls", projectionSource);
        Assert.DoesNotContain("EntryControls", projectionSource);
        Assert.DoesNotContain("SecondaryProcControls", projectionSource);
        Assert.DoesNotContain("ReadFieldValue(", runtimeSource + projectionSource);
        Assert.DoesNotContain("WriteFieldValue(", runtimeSource + projectionSource);
        Assert.DoesNotContain("VmxExecutionUnit", runtimeSource + projectionSource);
        Assert.DoesNotContain("VmcsManager", runtimeSource + projectionSource);
    }

    private static VmxCompatibilityVmReadAdmissionRequest CreateVmReadRequest(
        VmcsField field) =>
        new(
            Context: CreateContext(),
            RootAuthority: CreateRoot(),
            EvidencePolicy: CreateAliasEvidencePolicy(),
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
}
