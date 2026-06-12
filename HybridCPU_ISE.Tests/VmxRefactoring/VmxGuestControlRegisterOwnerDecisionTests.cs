using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxGuestControlRegisterOwnerDecisionTests
{
    [Theory]
    [InlineData(VmcsField.GuestCr0)]
    [InlineData(VmcsField.GuestCr4)]
    public void GuestControlRegisterVmread_RemainsDeniedDespiteSchemaAndGuestReadonlyView(
        VmcsField field)
    {
        var service = new VmxCompatibilityAdmissionService();

        Assert.True(VmcsFieldProjectionSchema.TryGet(field, out VmcsFieldProjectionSchemaEntry entry));
        Assert.True(VmcsFieldProjectionSchema.CanRead(entry));
        Assert.False(VmcsFieldProjectionSchema.CanWrite(entry));
        Assert.Equal(VmcsFieldProjectionOwner.ExecutionDomainDescriptor, entry.Owner);
        Assert.Equal(EvidenceVisibilityClass.GuestArchitecturalState, entry.EvidenceClass);
        Assert.Equal(VmcsFieldProjectionMigrationPolicy.DescriptorOwned, entry.MigrationPolicy);

        VmxCompatibilityVmReadAdmissionResult result =
            service.AdmitVmReadProjection(CreateVmReadRequest(field));

        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.True(result.ValueProjection.AliasAccess.IsAllowed);
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.PrivilegedExecutionStateProjectionDenied,
            result.ValueProjection.Decision);
        Assert.Equal(VmcsFieldProjectionOwner.ExecutionDomainDescriptor, result.ValueProjection.SchemaEntry.Owner);
        Assert.Equal(0, result.Value);
        Assert.Contains("descriptor value source", result.Reason);
    }

    [Fact]
    public void GuestControlRegisterVmread_DoesNotLeakThroughAliasesControlsOrBroadSchemaScan()
    {
        var service = new VmxCompatibilityAdmissionService();

        Dictionary<VmcsField, VmxCompatibilityVmReadAdmissionResult> scan = new();
        foreach (VmcsFieldProjectionSchemaEntry entry in VmcsFieldProjectionSchema.Entries)
        {
            scan.Add(entry.Field, service.AdmitVmReadProjection(CreateVmReadRequest(entry.Field)));
        }

        AssertDeniedGuestControl(scan[VmcsField.GuestCr0]);
        AssertDeniedGuestControl(scan[VmcsField.GuestCr4]);

        VmcsField[] projectedFields = scan
            .Where(pair => pair.Value.IsReadOnlyValueProjected)
            .Select(pair => pair.Key)
            .ToArray();

        Assert.DoesNotContain(VmcsField.GuestCr0, projectedFields);
        Assert.DoesNotContain(VmcsField.GuestCr4, projectedFields);

        foreach (VmcsField field in new[] { VmcsField.HostPc, VmcsField.HostSp, VmcsField.HostFlags, VmcsField.HostCr0 })
        {
            Assert.Equal(
                VmcsReadOnlyValueProjectionDecision.HostExecutionStateOwnerMissing,
                scan[field].ValueProjection.Decision);
            Assert.False(scan[field].IsReadOnlyValueProjected);
        }

        foreach (VmcsField field in new[]
        {
            VmcsField.PinBasedControls,
            VmcsField.ProcBasedControls,
            VmcsField.ExitControls,
            VmcsField.EntryControls,
            VmcsField.SecondaryProcControls,
        })
        {
            Assert.Equal(
                VmcsReadOnlyValueProjectionDecision.CompatibilityControlValueProjectionDenied,
                scan[field].ValueProjection.Decision);
            Assert.False(scan[field].IsReadOnlyValueProjected);
        }
    }

    [Theory]
    [InlineData(VmcsField.GuestCr0)]
    [InlineData(VmcsField.GuestCr4)]
    public void GuestControlRegisterVmwriteScalarFallbackAndSchemaWriteRemainDenied(
        VmcsField field)
    {
        Assert.True(VmcsFieldProjectionSchema.TryGet(field, out VmcsFieldProjectionSchemaEntry entry));
        Assert.False(VmcsFieldProjectionSchema.CanWrite(entry));

        VmcsFieldAliasResult aliasWrite = new VmcsFieldAliasProjection().ValidateAccess(
            new VmcsFieldAliasRequest(
                field,
                VmcsFieldAliasAccess.Write,
                entry.EvidenceClass,
                entry.IsGeneratedAlias,
                DescriptorValidated: true,
                AllowWrite: true),
            CreateAliasAndGuestEvidencePolicy());

        Assert.Equal(VmcsFieldAliasDecision.WriteDenied, aliasWrite.Decision);

        Assert.False(VmcsV2Descriptor.CreateDefault().TryReadScalarField(
            (ushort)field,
            out long value,
            out VmcsV2ValidationResult validation));
        Assert.Equal(0, value);
        Assert.Equal(VmcsV2ValidationCode.AccessDenied, validation.Code);
        Assert.Contains("scalar projection cache was removed", validation.Message);
        Assert.Null(typeof(VmcsV2Descriptor).GetMethod("TryWriteScalarField"));
    }

    [Fact]
    public void GuestControlRegisterProjectionMatrix_AllowsOnlyReadOnlyValueAvailability()
    {
        var secureMatrix = new SecureComputeCompatibilityBoundaryMatrixPolicy();

        foreach (VmcsField field in new[] { VmcsField.GuestCr0, VmcsField.GuestCr4 })
        {
            SecureComputeCompatibilityMatrixResult read =
                secureMatrix.AdmitVmRead(new SecureComputeCompatibilityReadMatrixRequest(
                    FieldId: (ulong)field,
                    FieldClass: SecureComputeCompatibilityFieldClass.GuestPrivilegedControl,
                    SchemaOwner: SecureComputeProjectionOwnerKind.SecureCompatibilityProjectionPolicy,
                    ExpectedOwner: SecureComputeProjectionOwnerKind.SecureCompatibilityProjectionPolicy,
                    HasNeutralOwner: true,
                    HasReadOnlySource: true,
                    SecureVisibilityAllowed: true,
                    MigrationClassified: true,
                    ConformanceProven: true));

            Assert.True(read.IsAllowed);
            Assert.True(read.ValueAvailable);
            Assert.False(read.BackendSuccessAuthorized);
            Assert.False(read.MutationAuthorized);
            Assert.Equal(
                SecureComputeCompatibilityMatrixDecision.AllowedReadOnlyProjection,
                read.Decision);
        }

        SecureComputeCompatibilityMatrixResult write = secureMatrix.AdmitVmWrite(secureSensitiveField: true);
        Assert.Equal(SecureComputeCompatibilityMatrixDecision.DeniedWriteMutation, write.Decision);
        Assert.False(write.MutationAuthorized);

        var capsFence = new SecureComputeVmxCapsProjectionFence();
        Assert.Equal(
            SecureComputeVmxCapsProjectionFenceDecision.DeniedAuthorityGrant,
            capsFence.Validate(
                attemptsAuthorityGrant: true,
                attemptsActivation: false,
                attemptsWriteMutation: false));

        NeutralTrapResult projectionOnlyTrap = NeutralTrapResult.Trap(
            TrapRequest.ForCompatibilityOperation((byte)VmxOperationKind.VmRead, opcode: 0, vtId: 0),
            NeutralTrapResultKind.CompatibilityOperationIntercept);
        RuntimeBoundaryAdmissionResult runtimeAdmission = RuntimeBoundaryAdmissionResult.Allowed(default);
        TrapCompletionRouteRequest routeRequest =
            TrapCompletionRouteRequest.ProjectionOnlyDenied(projectionOnlyTrap, runtimeAdmission);
        TrapCompletionRouteResult route = TrapCompletionRouteService.Default.Authorize(routeRequest);
        TrapCompletionPublicationFenceResult fence =
            TrapCompletionRouteService.Default.EvaluateFence(routeRequest, route);

        Assert.Equal(TrapCompletionRouteDecision.DeniedBackendExecution, route.Decision);
        Assert.False(route.CompletionPublicationAuthorized);
        Assert.False(route.RetirePublicationAuthorized);
        Assert.Equal(TrapCompletionPublicationDecision.DeniedBackendExecution, fence.Decision);
        Assert.False(fence.CompletionPublicationAllowed);
        Assert.False(fence.RetirePublicationAllowed);
    }

    private static void AssertDeniedGuestControl(VmxCompatibilityVmReadAdmissionResult result)
    {
        Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.PrivilegedExecutionStateProjectionDenied,
            result.ValueProjection.Decision);
        Assert.Equal(0, result.Value);
    }

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
                    guestFlags: 0x202UL,
                    stateEpoch: 9UL)),
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
