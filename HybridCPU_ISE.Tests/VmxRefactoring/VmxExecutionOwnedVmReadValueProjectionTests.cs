using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests;

public sealed class VmxExecutionOwnedVmReadValueProjectionTests
{
    [Fact]
    public void ExecutionReadOnlyStateView_MaterializesOnlyGuestPcSpFlagsAndCarriesNeutralEpoch()
    {
        ExecutionDomainReadOnlyStateView empty = ExecutionDomainReadOnlyStateView.Unmaterialized;
        Assert.False(empty.IsMaterialized);
        Assert.False(empty.HasCompleteGuestPcSpFlags);
        Assert.Equal(0UL, empty.StateEpoch);

        ExecutionDomainReadOnlyStateView view =
            ExecutionDomainReadOnlyStateView.FromGuestPcSpFlags(
                guestPc: 0x1000UL,
                guestSp: 0x2000UL,
                guestFlags: 0x202UL,
                stateEpoch: 77UL);

        Assert.True(view.IsMaterialized);
        Assert.True(view.HasCompleteGuestPcSpFlags);
        Assert.Equal(77UL, view.StateEpoch);
        Assert.Equal(0x1000UL, view.GuestPc);
        Assert.Equal(0x2000UL, view.GuestSp);
        Assert.Equal(0x202UL, view.GuestFlags);

        Type viewType = typeof(ExecutionDomainReadOnlyStateView);
        Assert.Null(viewType.GetProperty("GuestCr0"));
        Assert.Null(viewType.GetProperty("GuestCr4"));
        Assert.Null(viewType.GetProperty("HostPc"));
        Assert.Null(viewType.GetProperty("HostSp"));
        Assert.Null(viewType.GetProperty("HostFlags"));
        Assert.Null(viewType.GetProperty("HostCr0"));
    }

    [Fact]
    public void VmReadProjectionPath_ProjectsGuestPcSpFlagsFromNeutralExecutionStateView()
    {
        ExecutionDomainReadOnlyStateView view =
            ExecutionDomainReadOnlyStateView.FromGuestPcSpFlags(
                guestPc: 0x1000_2000UL,
                guestSp: 0x3000_4000UL,
                guestFlags: 0x202UL,
                stateEpoch: 99UL);
        ExecutionDomainDescriptor execution = CreateExecution(view);

        AssertProjected(VmcsField.GuestPc, unchecked((long)view.GuestPc), execution);
        AssertProjected(VmcsField.GuestSp, unchecked((long)view.GuestSp), execution);
        AssertProjected(VmcsField.GuestFlags, unchecked((long)view.GuestFlags), execution);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesGuestPcWhenGuestArchitecturalEvidenceClosed()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.GuestPc,
                CreateExecution(ExecutionDomainReadOnlyStateView.FromGuestPcSpFlags(
                    guestPc: 0x1000UL,
                    guestSp: 0x2000UL,
                    guestFlags: 0x202UL)),
                CreateAliasEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(VmcsReadOnlyValueProjectionDecision.AliasAccessDenied, result.ValueProjection.Decision);
        Assert.Equal(EvidenceVisibilityClass.GuestArchitecturalState, result.ValueProjection.SchemaEntry.EvidenceClass);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesExecutionOwnedFieldsWithoutMaterializedStateView()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.GuestPc,
                new ExecutionDomainDescriptor(),
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(VmcsReadOnlyValueProjectionDecision.ExecutionSourceMissing, result.ValueProjection.Decision);
        Assert.Equal(VmcsFieldProjectionOwner.ExecutionDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(EvidenceVisibilityClass.GuestArchitecturalState, result.ValueProjection.SchemaEntry.EvidenceClass);
        Assert.Equal(0, result.Value);
        Assert.Contains("no materialized read-only guest architectural state view", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesUnmaterializedGuestFlagsField()
    {
        var service = new VmxCompatibilityAdmissionService();
        ExecutionDomainDescriptor execution = CreateExecution(new ExecutionDomainReadOnlyStateView(
            GuestPc: 0x1000UL,
            GuestSp: 0x2000UL,
            GuestFlags: 0,
            HasMaterializedGuestPc: true,
            HasMaterializedGuestSp: true,
            HasMaterializedGuestFlags: false));

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.GuestFlags,
                execution,
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(VmcsReadOnlyValueProjectionDecision.ExecutionSourceDenied, result.ValueProjection.Decision);
        Assert.Equal(VmcsFieldProjectionOwner.ExecutionDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(0, result.Value);
        Assert.Contains("does not materialize field 'GuestFlags'", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_KeepsGuestControlRegistersDenied()
    {
        var service = new VmxCompatibilityAdmissionService();
        ExecutionDomainDescriptor execution = CreateExecution(
            ExecutionDomainReadOnlyStateView.FromGuestPcSpFlags(
                guestPc: 0x1000UL,
                guestSp: 0x2000UL,
                guestFlags: 0x202UL));

        foreach (VmcsField field in new[] { VmcsField.GuestCr0, VmcsField.GuestCr4 })
        {
            VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
                CreateVmReadRequest(
                    field,
                    execution,
                    CreateAliasAndGuestEvidencePolicy()));

            Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
            Assert.True(result.RuntimeAdmissionAllowed);
            Assert.False(result.IsReadOnlyValueProjected);
            Assert.Equal(
                VmcsReadOnlyValueProjectionDecision.PrivilegedExecutionStateProjectionDenied,
                result.ValueProjection.Decision);
            Assert.Equal(VmcsFieldProjectionOwner.ExecutionDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
            Assert.Equal(0, result.Value);
            Assert.Contains("descriptor value source", result.Reason);
        }
    }

    [Fact]
    public void VmReadProjectionPath_KeepsHostExecutionAliasesDeniedWithoutHostOwner()
    {
        var service = new VmxCompatibilityAdmissionService();
        ExecutionDomainDescriptor execution = CreateExecution(
            ExecutionDomainReadOnlyStateView.FromGuestPcSpFlags(
                guestPc: 0x1000UL,
                guestSp: 0x2000UL,
                guestFlags: 0x202UL));

        foreach (VmcsField field in new[] { VmcsField.HostPc, VmcsField.HostSp, VmcsField.HostFlags, VmcsField.HostCr0 })
        {
            VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
                CreateVmReadRequest(
                    field,
                    execution,
                    CreateAliasEvidencePolicy()));

            Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
            Assert.True(result.RuntimeAdmissionAllowed);
            Assert.False(result.IsReadOnlyValueProjected);
            Assert.Equal(
                VmcsReadOnlyValueProjectionDecision.HostExecutionStateOwnerMissing,
                result.ValueProjection.Decision);
            Assert.Equal(VmcsFieldProjectionOwner.ExecutionDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
            Assert.Equal(EvidenceVisibilityClass.CompatibilityAlias, result.ValueProjection.SchemaEntry.EvidenceClass);
            Assert.Equal(0, result.Value);
            Assert.Contains("no neutral host-execution owner", result.Reason);
        }
    }

    [Fact]
    public void VmReadProjectionPath_HostExecutionAliasesDoNotDependOnGuestReadOnlyView()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                VmcsField.HostPc,
                new ExecutionDomainDescriptor(),
                CreateAliasEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.HostExecutionStateOwnerMissing,
            result.ValueProjection.Decision);
        Assert.Equal(VmcsFieldProjectionOwner.ExecutionDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(EvidenceVisibilityClass.CompatibilityAlias, result.ValueProjection.SchemaEntry.EvidenceClass);
        Assert.DoesNotContain("guest architectural state view", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_ExecutionOwnedSourceUsesNeutralDescriptorAndNoVmcsStoreFallback()
    {
        string source = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Domains/Descriptors/ExecutionDomain/ExecutionDomainDescriptor.cs",
            "CloseToRTL/Core/Runtime/Domains/Descriptors/ExecutionDomain/ExecutionDomainReadOnlyStateView.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");

        Assert.Contains("VmcsFieldProjectionOwner.ExecutionDomainDescriptor", source);
        Assert.Contains("ExecutionDomainReadOnlyStateView", source);
        Assert.Contains("TryCreateReadOnlyStateView", source);
        Assert.Contains("IsMaterialized", source);
        Assert.Contains("StateEpoch", source);
        Assert.Contains("HasCompleteGuestPcSpFlags", source);
        Assert.Contains("ProjectExecutionOwnedValue", source);
        Assert.Contains("TryProjectExecutionField", source);
        Assert.Contains("VmcsField.GuestPc", source);
        Assert.Contains("VmcsField.GuestSp", source);
        Assert.Contains("VmcsField.GuestFlags", source);
        Assert.Contains("PrivilegedExecutionStateProjectionDenied", source);
        Assert.Contains("HostExecutionStateOwnerMissing", source);
        Assert.Contains("VmcsField.HostPc", source);
        Assert.Contains("VmcsField.HostSp", source);
        Assert.Contains("VmcsField.HostFlags", source);
        Assert.Contains("VmcsField.HostCr0", source);
        Assert.DoesNotContain("TryReadScalarField", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("HardwareWrite(", source);
        Assert.DoesNotContain("DirectWrite(", source);
        Assert.DoesNotContain("CaptureGuestStateEager", source);
        Assert.DoesNotContain("MaterializeLazyGuestRegisters", source);
        Assert.DoesNotContain("MaterializeVmExitGuestState", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);

        string projectionSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");
        Assert.Contains("CurrentPrivilegedExecutionStateEpoch", projectionSource);
        Assert.DoesNotContain("request.Execution.ReadOnlyState.StateEpoch", projectionSource);
    }

    private static void AssertProjected(
        VmcsField field,
        long expected,
        ExecutionDomainDescriptor execution)
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                field,
                execution,
                CreateAliasAndGuestEvidencePolicy()));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyValueProjected, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.True(result.IsReadOnlyValueProjected);
        Assert.Equal(expected, result.Value);
        Assert.True(result.ValueProjection.AliasAccess.IsAllowed);
        Assert.Equal(VmcsFieldProjectionOwner.ExecutionDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(EvidenceVisibilityClass.GuestArchitecturalState, result.ValueProjection.SchemaEntry.EvidenceClass);
        Assert.Equal(VmcsV2ValidationCode.Success, result.VmcsValidation.Code);
        Assert.Contains("neutral ExecutionDomainDescriptor read-only state view", result.Reason);
    }

    private static VmxCompatibilityVmReadAdmissionRequest CreateVmReadRequest(
        VmcsField field,
        ExecutionDomainDescriptor execution,
        EvidencePolicyDescriptor evidencePolicy) =>
        new(
            Context: CreateContext(execution),
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

    private static DomainRuntimeContext CreateContext(ExecutionDomainDescriptor execution) =>
        new(
            execution: execution,
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

    private static ExecutionDomainDescriptor CreateExecution(
        ExecutionDomainReadOnlyStateView readOnlyState) =>
        new ExecutionDomainDescriptor().WithReadOnlyState(readOnlyState);
}
