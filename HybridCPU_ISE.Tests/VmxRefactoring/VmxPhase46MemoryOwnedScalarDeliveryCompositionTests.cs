using System.Collections.Immutable;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase46MemoryOwnedScalarDeliveryCompositionTests
{
    private const ulong Domain = 7;
    private const ulong AddressSpace = 9;

    [Theory]
    [InlineData(VmcsField.GuestCr3, 0x12345000UL)]
    [InlineData(VmcsField.EptPointer, 0xabcdf000UL)]
    [InlineData(VmcsField.Vpid, 9UL)]
    [InlineData(VmcsField.Cr3TargetCount, 2UL)]
    public void CanonicalSchedulerPrfWritebackAndRetireCoordinator_DeliverExactScalarOnly(
        VmcsField field,
        ulong expected)
    {
        Fixture fixture = CreateFixture(field);
        fixture.Scheduler.ConfigureExactMemoryOwnedVmReadScalarDelivery(fixture.Composition);
        Admit(fixture);
        Assert.True(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)field, 1));
        VmReadScalarResultReceipt receipt = fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt!;
        Assert.True(fixture.Carrier.HasVmReadScalarResultReceipt);

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        Assert.True(fixture.Carrier.Execute(ref core));
        Assert.True(fixture.Carrier.TryGetPrimaryWriteBackResult(out ulong prfValue));
        Assert.Equal(expected, prfValue);
        Assert.Equal(0UL, core.ReadArch(0, 3));
        Assert.False(fixture.Carrier.CreateRetireEffect().IsValid);

        Span<RetireRecord> records = stackalloc RetireRecord[1];
        int count = 0;
        fixture.Carrier.EmitWriteBackRetireRecords(ref core, records, ref count);
        Assert.Equal(1, count);
        Assert.Equal(RetireRecordKind.RegisterWrite, records[0].Kind);
        Assert.Equal(3, records[0].ArchReg);
        Assert.Equal(expected, records[0].Value);
        Assert.Equal(0UL, core.ReadArch(0, 3));
        core.RetireCoordinator.Retire(records[..count]);
        Assert.Equal(expected, core.ReadArch(0, 3));
        Assert.True(receipt.IsConsumed);
    }

    [Fact]
    public void SchedulerBinding_IsDefaultDisabledAndDisableInvalidatesOutstandingReceipt()
    {
        Fixture disabled = CreateFixture(VmcsField.GuestCr3, enable: false);
        Assert.Throws<InvalidOperationException>(() =>
            disabled.Scheduler.ConfigureExactMemoryOwnedVmReadScalarDelivery(disabled.Composition));

        Fixture live = CreateFixture(VmcsField.GuestCr3);
        live.Scheduler.ConfigureExactMemoryOwnedVmReadScalarDelivery(live.Composition);
        Admit(live);
        Assert.True(live.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            live.Packet, live.Lane, (ushort)VmcsField.GuestCr3, 1));
        VmReadScalarResultReceipt receipt = live.Scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt!;
        live.Scheduler.DisableExactMemoryOwnedVmReadScalarDelivery();
        Assert.False(live.Scheduler.HasExactMemoryOwnedVmReadScalarDelivery);
        Assert.False(receipt.TryValidateSpeculative());
        Assert.False(receipt.TryConsumeAtRetire(1));
    }

    [Fact]
    public void SchedulerReplayInvalidationAndAdjacentRouting_HaveZeroArchitecturalEffect()
    {
        Fixture replay = CreateFixture(VmcsField.Vpid);
        replay.Scheduler.ConfigureExactMemoryOwnedVmReadScalarDelivery(replay.Composition);
        Admit(replay);
        Assert.True(replay.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            replay.Packet, replay.Lane, (ushort)VmcsField.Vpid, 1));
        VmReadScalarResultReceipt receipt = replay.Scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt!;
        replay.Scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
            false, 18, 0x4000, 1, 2, 0, 0, ReplayPhaseInvalidationReason.Manual));
        Assert.False(receipt.TryValidateSpeculative());
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        Assert.True(replay.Carrier.Execute(ref core));
        Assert.False(replay.Carrier.TryGetPrimaryWriteBackResult(out _));
        Assert.Equal(0UL, core.ReadArch(0, 3));

        Fixture adjacent = CreateFixture(VmcsField.GuestPc);
        adjacent.Scheduler.ConfigureExactMemoryOwnedVmReadScalarDelivery(adjacent.Composition);
        Admit(adjacent);
        Assert.False(adjacent.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            adjacent.Packet, adjacent.Lane, (ushort)VmcsField.GuestPc, 1));
        Assert.Equal(VmReadScalarDeliveryDecision.FieldDenied,
            adjacent.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
        Assert.False(adjacent.Carrier.HasVmReadScalarResultReceipt);
    }

    [Fact]
    public void PolicyResolver_IsExactGovernanceConstraintAndDefaultActivationIsDisabled()
    {
        Fixture fixture = CreateFixture(VmcsField.GuestCr3, enable: false);
        Assert.True(fixture.Composition.PolicyResolution.IsResolved);
        Assert.False(fixture.Composition.PolicyResolution.RuntimeAuthorityGranted);
        Assert.False(fixture.Composition.PolicyResolution.SourceValueAvailable);
        Assert.False(fixture.Composition.PolicyResolution.ResultReceiptIssued);
        Assert.False(fixture.Composition.IsEnabled);

        MemoryOwnedVmReadScalarDeliveryPolicyLookup wrong =
            MemoryOwnedVmReadScalarDeliveryAcceptedPolicyResolver.ExactLookup() with
            { FieldIds = ImmutableArray.Create((ushort)VmcsField.GuestCr3) };
        Fixture mismatched = CreateFixture(VmcsField.GuestCr3, lookup: wrong, enable: false);
        Assert.False(mismatched.Composition.PolicyResolution.IsResolved);
        Assert.False(mismatched.Composition.EnableExact());
        Fixture revoked = CreateFixture(VmcsField.GuestCr3,
            lookup: MemoryOwnedVmReadScalarDeliveryAcceptedPolicyResolver.ExactLookup(revoked: true),
            enable: false);
        Assert.False(revoked.Composition.EnableExact());
    }

    [Theory]
    [InlineData(VmcsField.GuestCr3, 0x12345000UL)]
    [InlineData(VmcsField.EptPointer, 0xabcdf000UL)]
    [InlineData(VmcsField.Vpid, 9UL)]
    [InlineData(VmcsField.Cr3TargetCount, 2UL)]
    public void Prepare_CapturesExactMemoryOwnerValueAndGeneration(
        VmcsField field,
        ulong expected)
    {
        Fixture fixture = Prepare(field);
        VmReadScalarResultReceipt receipt = fixture.Receipt!;
        Assert.Equal(MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
            receipt.DecisionId);
        Assert.Same(fixture.Context.Memory, receipt.SourceOwner);
        Assert.Equal(fixture.Runtime.CurrentAddressSpaceGeneration, receipt.SourceEpoch);
        Assert.NotNull(receipt.MemoryDomainCapture);
        Assert.Null(receipt.ExecutionDomainCapture);
        Assert.Equal(field, receipt.Field);
        Assert.Equal(expected, receipt.Value);
        Assert.Equal((byte)3, receipt.DestinationRegister);
        Assert.False(receipt.BackendExecutionAuthorized);
        Assert.False(receipt.CompletionPublicationAuthorized);
        Assert.False(receipt.VmxRetireEffectAuthorized);
    }

    [Theory]
    [InlineData(VmcsField.GuestCr0)]
    [InlineData(VmcsField.GuestCr4)]
    [InlineData(VmcsField.GuestPc)]
    [InlineData(VmcsField.GuestSp)]
    [InlineData(VmcsField.GuestFlags)]
    [InlineData(VmcsField.HostCr3)]
    [InlineData(VmcsField.PinBasedControls)]
    public void AdjacentHostAndCompatibilityFields_AreDenied(VmcsField field)
    {
        Fixture fixture = CreateFixture(field);
        Admit(fixture);
        VmReadScalarDeliveryResult result = fixture.Composition.Prepare(
            Replay(), fixture.Carrier, Attempt(fixture), (ushort)field, 1);
        Assert.Equal(VmReadScalarDeliveryDecision.FieldDenied, result.Decision);
        Assert.Null(result.Receipt);
    }

    [Fact]
    public void FieldSpecificMemoryGates_DoNotGrantNeighboringValues()
    {
        Fixture noSecondStage = CreateFixture(VmcsField.EptPointer,
            memory: Descriptor(ownsSecondStage: false));
        Admit(noSecondStage);
        Assert.Equal(VmReadScalarDeliveryDecision.SourceDenied,
            noSecondStage.Composition.Prepare(Replay(), noSecondStage.Carrier,
                Attempt(noSecondStage), (ushort)VmcsField.EptPointer, 1).Decision);

        Fixture noTagging = CreateFixture(VmcsField.Vpid,
            memory: Descriptor(tagging: false, addressSpaceTag: 0));
        Admit(noTagging);
        Assert.Equal(VmReadScalarDeliveryDecision.SourceDenied,
            noTagging.Composition.Prepare(Replay(), noTagging.Carrier,
                Attempt(noTagging), (ushort)VmcsField.Vpid, 1).Decision);
        Assert.Equal(VmReadScalarDeliveryDecision.Prepared,
            noTagging.Composition.Prepare(Replay(), noTagging.Carrier,
                Attempt(noTagging), (ushort)VmcsField.GuestCr3, 1).Decision);
    }

    [Fact]
    public void StaleCrossDomainCrossAddressAndCallerGeneration_CannotIssueReceipt()
    {
        Fixture stale = CreateFixture(VmcsField.GuestCr3);
        Admit(stale);
        MemoryDomainSourceBindResult replacement = stale.Runtime.ReplaceAuthoritativeTranslationView(
            Descriptor(root: 0x22222000), AddressSpace);
        Assert.Equal(VmReadScalarDeliveryDecision.SourceDenied,
            stale.Composition.Prepare(Replay(), stale.Carrier, Attempt(stale),
                (ushort)VmcsField.GuestCr3, 1).Decision);

        Assert.False(stale.Composition.RefreshSourceContext(Context(
            replacement.Descriptor!, domain: Domain + 1)));
        Assert.False(stale.Composition.RefreshSourceContext(Context(
            replacement.Descriptor!, addressSpace: AddressSpace + 1)));
        Assert.NotEqual(0xdeadUL, replacement.AddressSpaceGeneration);
    }

    [Fact]
    public void OrdinaryReplacement_PreservesCapturedResultAndFutureCaptureUsesNewGeneration()
    {
        Fixture old = Prepare(VmcsField.GuestCr3);
        VmReadScalarResultReceipt oldReceipt = old.Receipt!;
        MemoryDomainSourceBindResult replacement = old.Runtime.ReplaceAuthoritativeTranslationView(
            Descriptor(root: 0x55555000), AddressSpace);
        Assert.True(old.Composition.RefreshSourceContext(Context(replacement.Descriptor!)));
        Assert.True(oldReceipt.TryValidateSpeculative());
        Assert.Equal(0x12345000UL, oldReceipt.Value);

        Fixture current = CreateFixture(VmcsField.GuestCr3,
            runtime: old.Runtime,
            composition: old.Composition,
            context: Context(replacement.Descriptor!));
        Admit(current);
        VmReadScalarDeliveryResult next = current.Composition.Prepare(
            Replay(), current.Carrier, Attempt(current),
            (ushort)VmcsField.GuestCr3, 1);
        Assert.True(next.IsPrepared, next.Reason);
        Assert.Equal(0x55555000UL, next.Receipt!.Value);
        Assert.NotEqual(oldReceipt.SourceEpoch, next.Receipt.SourceEpoch);
    }

    [Fact]
    public void ReplayRestoreDisableAndSingleUse_AreFailClosed()
    {
        Fixture replay = Prepare(VmcsField.Vpid);
        VmReadScalarResultReceipt replayReceipt = replay.Receipt!;
        replay.Composition.ObserveReplayPhase(new ReplayPhaseContext(
            false, 18, 0x4000, 1, 2, 0, 0, ReplayPhaseInvalidationReason.Manual));
        Assert.False(replayReceipt.TryValidateSpeculative());

        Fixture disabled = Prepare(VmcsField.GuestCr3);
        VmReadScalarResultReceipt disabledReceipt = disabled.Receipt!;
        disabled.Composition.Disable();
        Assert.False(disabledReceipt.TryValidateSpeculative());
        Assert.False(disabledReceipt.TryConsumeAtRetire(1));

        Fixture restored = Prepare(VmcsField.EptPointer);
        VmReadScalarResultReceipt restoredReceipt = restored.Receipt!;
        MemoryDomainSourceBindResult rebound = restored.Runtime.RebindAuthoritativeTranslationViewAfterRestore(
            Descriptor(root: 0x77777000), AddressSpace);
        restored.Composition.ReplaceAfterRestore(Context(rebound.Descriptor!), 2);
        Assert.False(restoredReceipt.TryValidateSpeculative());
        Assert.False(restoredReceipt.TryConsumeAtRetire(1));

        Fixture once = Prepare(VmcsField.Cr3TargetCount);
        Assert.True(once.Receipt!.TryConsumeAtRetire(1));
        Assert.False(once.Receipt.TryConsumeAtRetire(1));
    }

    [Fact]
    public void ForgedOwnerGenerationFieldValueAndCrossCompositionReceipt_AreRejected()
    {
        Fixture fixture = Prepare(VmcsField.GuestCr3);
        VmReadScalarResultReceipt valid = fixture.Receipt!;
        MemoryDomainRuntime.SourceCapture capture = valid.MemoryDomainCapture!;

        VmReadScalarResultReceipt Forged(object owner, ulong generation, VmcsField field, ulong value) => new(
            fixture.Composition, Attempt(fixture), valid.DecisionId,
            owner, generation, capture, valid.ProfileGeneration,
            valid.AttemptId, valid.IssuerGeneration, valid.BundleIdentity,
            valid.ReplayEpoch, valid.RestoreGeneration, valid.DomainTag,
            valid.AddressSpaceTag, default, field, valid.DestinationRegister, value);

        Assert.False(Forged(new object(), valid.SourceEpoch, valid.Field, valid.Value).TryValidateSpeculative());
        Assert.False(Forged(valid.SourceOwner, valid.SourceEpoch + 1, valid.Field, valid.Value).TryValidateSpeculative());
        Assert.False(Forged(valid.SourceOwner, valid.SourceEpoch, VmcsField.Vpid, valid.Value).TryValidateSpeculative());
        Assert.False(Forged(valid.SourceOwner, valid.SourceEpoch, valid.Field, valid.Value + 1).TryValidateSpeculative());

        Fixture foreign = CreateFixture(VmcsField.GuestCr3, domain: 8, addressSpace: 10);
        Assert.False(foreign.Composition.ValidateLive(valid));
    }

    [Fact]
    public void VmWriteWrongDestinationAndMissingFullDomainEvidence_AreDenied()
    {
        Fixture vmwrite = CreateFixture(VmcsField.GuestCr3,
            carrier: CreateVmRead(opcode: IsaOpcodeValues.VMWRITE));
        Assert.True(vmwrite.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            vmwrite.Packet, vmwrite.Lane));
        Assert.Equal(VmReadScalarDeliveryDecision.AdmissionDenied,
            vmwrite.Composition.Prepare(Replay(), vmwrite.Carrier, Attempt(vmwrite),
                (ushort)VmcsField.GuestCr3, 1).Decision);

        Fixture x0 = CreateFixture(VmcsField.GuestCr3, carrier: CreateVmRead(destination: 0));
        Admit(x0);
        Assert.Equal(VmReadScalarDeliveryDecision.DestinationDenied,
            x0.Composition.Prepare(Replay(), x0.Carrier, Attempt(x0),
                (ushort)VmcsField.GuestCr3, 1).Decision);

        Fixture missingIo = CreateFixture(VmcsField.GuestCr3, includeIo: false);
        Admit(missingIo);
        Assert.Equal(VmReadScalarDeliveryDecision.ProjectionDenied,
            missingIo.Composition.Prepare(Replay(), missingIo.Carrier, Attempt(missingIo),
                (ushort)VmcsField.GuestCr3, 1).Decision);

        Fixture evidence = CreateFixture(VmcsField.GuestCr3,
            evidence: EvidencePolicyDescriptor.FailClosed);
        Admit(evidence);
        Assert.Equal(VmReadScalarDeliveryDecision.ProjectionDenied,
            evidence.Composition.Prepare(Replay(), evidence.Carrier, Attempt(evidence),
                (ushort)VmcsField.GuestCr3, 1).Decision);
    }

    [Fact]
    public async Task PrepareVersusDisable_InvalidatesEveryOutstandingReceipt()
    {
        for (int iteration = 0; iteration < 64; iteration++)
        {
            Fixture fixture = CreateFixture(VmcsField.GuestCr3);
            Admit(fixture);
            Task<VmReadScalarDeliveryResult> prepare = Task.Run(() => fixture.Composition.Prepare(
                Replay(), fixture.Carrier, Attempt(fixture),
                (ushort)VmcsField.GuestCr3, 1));
            Task disable = Task.Run(fixture.Composition.Disable);
            await Task.WhenAll(prepare, disable);
            VmReadScalarResultReceipt? receipt = (await prepare).Receipt;
            Assert.False(fixture.Composition.IsEnabled);
            Assert.False(receipt?.TryValidateSpeculative() ?? false);
            Assert.False(receipt?.TryConsumeAtRetire(1) ?? false);
        }
    }

    private static Fixture Prepare(VmcsField field)
    {
        Fixture fixture = CreateFixture(field);
        Admit(fixture);
        VmReadScalarDeliveryResult result = fixture.Composition.Prepare(
            Replay(), fixture.Carrier, Attempt(fixture), (ushort)field, 1);
        Assert.True(result.IsPrepared, result.Reason);
        return fixture with { Receipt = result.Receipt };
    }

    private static void Admit(Fixture fixture) =>
        Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            fixture.Packet, fixture.Lane));

    private static VmReadScalarAttemptBinding Attempt(Fixture fixture) =>
        new(fixture.Carrier.VirtualizationAdmission!);

    private static Fixture CreateFixture(
        VmcsField field,
        MemoryOwnedVmReadScalarDeliveryPolicyLookup? lookup = null,
        bool enable = true,
        bool includeIo = true,
        EvidencePolicyDescriptor? evidence = null,
        VmxMicroOp? carrier = null,
        ulong domain = Domain,
        ulong addressSpace = AddressSpace,
        MemoryDomainDescriptor? memory = null,
        MemoryDomainRuntime? runtime = null,
        MemoryOwnedVmReadScalarDeliveryCanonicalComposition? composition = null,
        DomainRuntimeContext? context = null)
    {
        runtime ??= new MemoryDomainRuntime();
        if (context is null)
        {
            MemoryDomainSourceBindResult bound = runtime.BindAuthoritativeTranslationView(
                memory ?? Descriptor(domain: domain), addressSpace);
            Assert.True(bound.IsBound, bound.Reason);
            context = Context(bound.Descriptor!, domain, addressSpace, includeIo);
        }
        composition ??= new MemoryOwnedVmReadScalarDeliveryCanonicalComposition(
            runtime,
            context,
            new RootAuthorityDescriptor(RootAuthorityClass.RuntimeRoot, 1, 0,
                allowCompatibilityFrontendActivation: false,
                allowAuthoritativeStateMutation: false),
            evidence ?? new EvidencePolicyDescriptor(
                allowCompatibilityAliases: true,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            restoreGeneration: 1,
            lookup ?? MemoryOwnedVmReadScalarDeliveryAcceptedPolicyResolver.ExactLookup());
        composition.ObserveReplayPhase(Replay());
        if (enable && !composition.IsEnabled)
            Assert.True(composition.EnableExact());

        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(Replay());
        carrier ??= CreateVmRead(domain: domain);
        IssuePacketLane lane = CreateLane(carrier);
        BundleIssuePacket packet = CreatePacket(lane);
        return new(scheduler, carrier, lane, packet, composition, runtime, context, null);
    }

    private static DomainRuntimeContext Context(
        MemoryDomainDescriptor memory,
        ulong domain = Domain,
        ulong addressSpace = AddressSpace,
        bool includeIo = true) => new(
            new ExecutionDomainDescriptor(domain, null, null, null, true,
                ExecutionDomainReadOnlyStateView.FromGuestPcSpFlags(1, 2, 3)),
            memory,
            includeIo ? new IoDomainDescriptor() : null,
            CapabilityDescriptorSet.Empty,
            secureCompute: null,
            domain,
            addressSpace);

    private static MemoryDomainDescriptor Descriptor(
        ulong root = 0x12345000,
        ulong secondStage = 0xabcdf000,
        bool tagging = true,
        ushort addressSpaceTag = 9,
        byte targetCount = 2,
        ulong generation = 0xdead,
        bool ownsSecondStage = true,
        ulong domain = Domain) => new(
            new AddressSpaceDescriptor(),
            new MemoryTranslationPolicy(),
            new MemoryDomainTranslationControl(
                true, tagging, root, secondStage, (ushort)domain,
                addressSpaceTag, generation,
                MemoryDomainTranslationControl.WriteBackMemoryType, targetCount),
            new DirtyTrackingServiceDescriptor(),
            ownsSecondStage);

    private static VmxMicroOp CreateVmRead(
        ushort opcode = IsaOpcodeValues.VMREAD,
        byte destination = 3,
        ulong domain = Domain)
    {
        var vmx = new VmxMicroOp
        {
            OpCode = opcode,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rd = destination,
            Rs1 = 1,
            Rs2 = 0,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(opcode),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = destination,
                Rs1 = 1,
                Rs2 = 0,
                Imm = 0,
            },
        };
        vmx.Placement = vmx.Placement with { DomainTag = domain };
        vmx.RefreshWriteMetadata();
        return vmx;
    }

    private static IssuePacketLane CreateLane(VmxMicroOp vmx) => new(
        7, true, 7, 0, 0, unchecked((ushort)vmx.OpCode), vmx,
        SlotClass.SystemSingleton, SlotPinningKind.HardPinned,
        countsTowardScalarProjection: false);

    private static BundleIssuePacket CreatePacket(IssuePacketLane lane7) => new(
        0x7000, DecodeMode.ClusterPreparedMode, 0x80, 0, 0, 0x80, 0, 0, 0, 0, 0,
        RuntimeClusterAdmissionExecutionMode.ClusterPrepared, false, true, false,
        IssuePacketLane.CreateEmpty(0), IssuePacketLane.CreateEmpty(1),
        IssuePacketLane.CreateEmpty(2), IssuePacketLane.CreateEmpty(3),
        IssuePacketLane.CreateEmpty(4), IssuePacketLane.CreateEmpty(5),
        IssuePacketLane.CreateEmpty(6), lane7, BundleIssueFallbackInfo.CreateEmpty());

    private static ReplayPhaseContext Replay() => new(
        true, 17, 0x4000, 1, 1, 1, 0x80, ReplayPhaseInvalidationReason.None);

    private sealed record Fixture(
        MicroOpScheduler Scheduler,
        VmxMicroOp Carrier,
        IssuePacketLane Lane,
        BundleIssuePacket Packet,
        MemoryOwnedVmReadScalarDeliveryCanonicalComposition Composition,
        MemoryDomainRuntime Runtime,
        DomainRuntimeContext Context,
        VmReadScalarResultReceipt? Receipt);
}
