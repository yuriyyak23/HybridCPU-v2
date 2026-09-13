using System.Reflection;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPrFCanonicalHypercallCompositionTests
{
    [Fact]
    public void CanonicalSchedulerAndExecute_ComposeExactE1OperandE2ToOneE3_WhileRetireFaults()
    {
        Fixture fixture = CreateFixture(configure: true);

        Assert.True(Materialize(fixture, rs1Value: 1));
        DomainHypercallCompositionResult prepared = Assert.IsType<DomainHypercallCompositionResult>(
            fixture.Scheduler.LastExactVirtualizationCompositionResult);
        Assert.True(prepared.IsPrepared);
        Assert.NotNull(prepared.E2);
        Assert.False(prepared.BackendExecutionAuthorized);
        Assert.False(prepared.CompletionPublicationAuthorized);
        Assert.False(prepared.RetirePublicationAuthorized);

        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        DomainHypercallExecutionResult execution = Assert.IsType<DomainHypercallExecutionResult>(
            fixture.Carrier.ExactHypercallExecutionResult);
        DomainHypercallRuntimeExecutor.ExecutionReceipt receipt =
            Assert.IsType<DomainHypercallRuntimeExecutor.ExecutionReceipt>(execution.Receipt);
        Assert.True(execution.IsExecuted);
        Assert.Equal(prepared.E2!.CertificateDigest, receipt.E2Digest);
        Assert.Equal((ushort)1, receipt.NumericLeaf);
        Assert.False(receipt.HasStateEffect);
        Assert.False(receipt.HasPayload);
        Assert.False(receipt.CompletionPublicationAuthorized);
        Assert.False(receipt.RetirePublicationAuthorized);
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, fixture.Carrier.CreateRetireEffect().FailureReason);
    }

    [Fact]
    public void DefaultNoBinding_PreservesPrDFaultOnlyRollbackAndNeverIssuesE2OrE3()
    {
        Fixture fixture = CreateFixture(configure: false);

        Assert.True(Materialize(fixture, rs1Value: 1));
        Assert.Null(fixture.Scheduler.LastExactVirtualizationCompositionResult);
        Assert.Null(fixture.Carrier.ExactHypercallExecutionResult);

        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        Assert.Null(fixture.Carrier.ExactHypercallExecutionResult);
        Assert.Null(fixture.Carrier.ExactHypercallExecutionReceipt);
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
    }

    [Fact]
    public void DisableAfterE2BeforeExecute_RevokesBindingAndProducesNoE3()
    {
        Fixture fixture = CreateFixture(configure: true);
        Assert.True(Materialize(fixture, rs1Value: 1));
        Assert.True(fixture.Composition!.IsEnabled);

        fixture.Scheduler.DisableExactVirtualizationComposition();
        Assert.False(fixture.Composition.IsEnabled);
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));

        DomainHypercallExecutionResult denied = Assert.IsType<DomainHypercallExecutionResult>(
            fixture.Carrier.ExactHypercallExecutionResult);
        Assert.Equal(DomainHypercallExecutionDecision.Disabled, denied.Decision);
        Assert.Null(denied.Receipt);
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(2UL)]
    [InlineData(0x1_0001UL)]
    public void ZeroAdjacentAndHighBitLeaf_NeverPrepareE2OrReachExecutor(ulong leaf)
    {
        Fixture fixture = CreateFixture(configure: true);

        Assert.False(Materialize(fixture, leaf));
        Assert.Null(fixture.Scheduler.LastExactVirtualizationCompositionResult);
        Assert.Null(fixture.Carrier.ExactHypercallExecutionResult);
    }

    [Fact]
    public void ReplayInvalidationAfterComposition_DeniesE3AndKeepsFaultOnlyRetire()
    {
        Fixture fixture = CreateFixture(configure: true);
        Assert.True(Materialize(fixture, rs1Value: 1));

        fixture.Scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
            isActive: false,
            epochId: 0,
            cachedPc: 0x4000,
            epochLength: 1,
            completedReplays: 0,
            validSlotCount: 0,
            stableDonorMask: 0,
            ReplayPhaseInvalidationReason.Manual));
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));

        DomainHypercallExecutionResult denied = Assert.IsType<DomainHypercallExecutionResult>(
            fixture.Carrier.ExactHypercallExecutionResult);
        Assert.Equal(DomainHypercallExecutionDecision.InvalidAdmission, denied.Decision);
        Assert.Null(denied.Receipt);
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
    }

    [Fact]
    public void InvokeHypercall_IsNeutralNoStateOperationAndDispatchIsOpaqueCarrierBound()
    {
        var operation = new DomainRuntimeOperation(
            DomainRuntimeOperationKind.InvokeHypercall,
            DomainRuntimeOperationSource.RuntimeService,
            requiresCapabilityGrant: true,
            DomainRuntimeOperationAuthorityClass.NoStateExecution);
        Assert.Equal(DomainRuntimeOperationSource.RuntimeService, operation.Source);
        Assert.True(operation.RequiresCapabilityGrant);
        Assert.True(operation.IsNoStateExecution);
        Assert.False(operation.IsProjectionOnly);
        Assert.False(operation.CanMutateAuthoritativeState);

        ConstructorInfo dispatchConstructor = Assert.Single(
            typeof(DomainHypercallCanonicalComposition.ExecutionDispatch)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.True(dispatchConstructor.IsPrivate);
        Assert.Empty(typeof(DomainHypercallCanonicalComposition.ExecutionDispatch).GetConstructors());
    }

    internal static bool Materialize(Fixture fixture, ulong rs1Value)
    {
        bool e1 = fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            fixture.Packet,
            fixture.Lane);
        if (!e1)
            return false;

        return fixture.Scheduler.TryAttachVirtualizationOperandSnapshotAfterCanonicalValueRead(
            fixture.Packet,
            fixture.Lane,
            rs1Value,
            fixture.RestoreOwner.CurrentGeneration);
    }

    internal static Fixture CreateFixture(
        bool configure,
        bool completion = false,
        bool retirement = false,
        ulong domainTag = 7)
    {
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(CreateReplayPhase());
        VmxMicroOp carrier = CreateVmCall(domainTag);
        IssuePacketLane lane = CreateLane(carrier);
        BundleIssuePacket packet = CreateIssuePacket(lane);
        var restoreOwner = new VirtualizationRestoreGenerationOwner();
        var lifecycleGate = new DomainHypercallLifecycleGate(domainTag);
        DomainHypercallCanonicalComposition? composition = null;
        DomainHypercallCompletionOwner? completionOwner = null;
        DomainHypercallRuntimeExecutor? executor = null;
        DomainRuntimeContext? context = null;
        RootAuthorityDescriptor? root = null;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);

        if (configure)
        {
            Assert.True(lifecycleGate.TryActivateExact(DomainHypercallExactActivationRequest.Phase38Exact));
            CapabilityGrant grant = CreateGrant(domainTag);
            var capabilityOwner = new RuntimeCapabilityGrantOwner();
            RuntimeCapabilityGrantLease lease = capabilityOwner.Issue(grant);
            executor = new DomainHypercallRuntimeExecutor(ExactProbeExecutionMode.ExactProbeOnly);
            context = CreateDomainContext(grant, domainTag);
            root = new RootAuthorityDescriptor(
                RootAuthorityClass.RuntimeRoot,
                authorityEpoch: 1,
                RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
                allowCompatibilityFrontendActivation: false,
                allowAuthoritativeStateMutation: false);
            completionOwner = completion ? new DomainHypercallCompletionOwner() : null;
            composition = new(
                context,
                root,
                capabilityOwner,
                lease,
                restoreOwner,
                executor,
                completionOwner,
                retirement ? core.ExactHypercallRetireOwner : null,
                lifecycleGate);
            scheduler.ConfigureExactVirtualizationComposition(composition);
        }

        return new(
            scheduler,
            carrier,
            lane,
            packet,
            restoreOwner,
            composition,
            completionOwner,
            executor,
            context,
            root,
            lifecycleGate,
            core);
    }

    private static CapabilityGrant CreateGrant(ulong domainTag) => new(
        RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
        CapabilityGrantScope.DomainGranted,
        isGranted: true,
        ownerDomainId: domainTag,
        CapabilityDelegationPolicy.NonDelegable,
        CapabilityRevocationPolicy.RuntimeRevocable,
        CapabilityMigrationClass.DomainLocal,
        CapabilityEvidenceVisibility.HostOnly,
        CapabilityFrontendProjectionPolicy.NeverProject);

    private static DomainRuntimeContext CreateDomainContext(CapabilityGrant grant, ulong domainTag) => new(
        new ExecutionDomainDescriptor(
            domainTag,
            new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(),
            schedulingBudget: null,
            extension: null,
            compatibilityProjectionEnabled: false),
        memory: null,
        io: null,
        new CapabilityDescriptorSet(new CapabilityGrantCollection([grant])),
        secureCompute: null,
        domainTag,
        addressSpaceTag: 0);

    private static VmxMicroOp CreateVmCall(ulong domainTag)
    {
        var vmx = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rd = 0,
            Rs1 = 5,
            Rs2 = 0,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = 0,
                Rs1 = 5,
                Rs2 = 0,
                Imm = 0,
            },
        };
        vmx.Placement = vmx.Placement with { DomainTag = domainTag };
        vmx.RefreshWriteMetadata();
        return vmx;
    }

    private static ReplayPhaseContext CreateReplayPhase() => new(
        isActive: true,
        epochId: 17,
        cachedPc: 0x4000,
        epochLength: 1,
        completedReplays: 0,
        validSlotCount: 0,
        stableDonorMask: 0,
        ReplayPhaseInvalidationReason.None);

    private static IssuePacketLane CreateLane(VmxMicroOp vmx) => new(
        physicalLaneIndex: 7,
        isOccupied: true,
        slotIndex: 7,
        virtualThreadId: 0,
        ownerThreadId: 0,
        opCode: IsaOpcodeValues.VMCALL,
        microOp: vmx,
        requiredSlotClass: SlotClass.SystemSingleton,
        pinningKind: SlotPinningKind.HardPinned,
        countsTowardScalarProjection: false);

    private static BundleIssuePacket CreateIssuePacket(IssuePacketLane lane7) => new(
        pc: 0x7000,
        decodeMode: DecodeMode.ClusterPreparedMode,
        validNonEmptyMask: 0x80,
        scalarCandidateMask: 0,
        scalarIssueMask: 0,
        selectedSlotMask: 0x80,
        unmappedSelectedSlotMask: 0,
        preparedScalarMask: 0,
        refinedPreparedScalarMask: 0,
        advisoryScalarIssueWidth: 0,
        refinedAdvisoryScalarIssueWidth: 0,
        executionMode: RuntimeClusterAdmissionExecutionMode.ClusterPrepared,
        shouldProbeClusterPath: false,
        usesIssuePacketAsExecutionSource: true,
        retainsReferenceSequentialPath: false,
        IssuePacketLane.CreateEmpty(0),
        IssuePacketLane.CreateEmpty(1),
        IssuePacketLane.CreateEmpty(2),
        IssuePacketLane.CreateEmpty(3),
        IssuePacketLane.CreateEmpty(4),
        IssuePacketLane.CreateEmpty(5),
        IssuePacketLane.CreateEmpty(6),
        lane7,
        BundleIssueFallbackInfo.CreateEmpty());

    internal sealed record Fixture(
        MicroOpScheduler Scheduler,
        VmxMicroOp Carrier,
        IssuePacketLane Lane,
        BundleIssuePacket Packet,
        VirtualizationRestoreGenerationOwner RestoreOwner,
        DomainHypercallCanonicalComposition? Composition,
        DomainHypercallCompletionOwner? CompletionOwner,
        DomainHypercallRuntimeExecutor? Executor,
        DomainRuntimeContext? Context,
        RootAuthorityDescriptor? Root,
        DomainHypercallLifecycleGate LifecycleGate,
        YAKSys_Hybrid_CPU.Processor.CPU_Core Core);
}
