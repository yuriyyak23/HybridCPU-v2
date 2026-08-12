using System.Collections.Immutable;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase43GuestPcSpFlagsScalarDeliveryProductionTests
{
    private const ulong Domain = 7;
    private const ulong AddressSpace = 9;

    [Theory]
    [InlineData(VmcsField.GuestPc, 0x1111UL)]
    [InlineData(VmcsField.GuestSp, 0x2222UL)]
    [InlineData(VmcsField.GuestFlags, 0x3333UL)]
    public void ExactField_UsesAtomicExecutionDomainCaptureCanonicalCarrierAndRetireCoordinator(
        VmcsField field,
        ulong expected)
    {
        Fixture fixture = Prepare(field);
        VmReadScalarResultReceipt receipt = Receipt(fixture);
        Assert.Equal(GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
            receipt.DecisionId);
        Assert.Same(fixture.Context.Execution, receipt.SourceOwner);
        Assert.Equal(fixture.Runtime.CurrentSourceEpoch.Value, receipt.SourceEpoch);
        Assert.Equal(expected, receipt.Value);
        Assert.Equal(field, receipt.Field);
        Assert.False(receipt.BackendExecutionAuthorized);
        Assert.False(receipt.CompletionPublicationAuthorized);
        Assert.False(receipt.VmxRetireEffectAuthorized);

        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
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
    public void Activation_IsDefaultDisabledAndExactPolicyOnly()
    {
        Fixture disabled = CreateFixture(VmcsField.GuestPc, enable: false);
        Assert.False(disabled.Composition.IsEnabled);
        Assert.False(disabled.Scheduler.HasExactGuestPcSpFlagsVmReadScalarDelivery);

        GuestPcSpFlagsVmReadScalarDeliveryPolicyLookup wrong =
            GuestPcSpFlagsVmReadScalarDeliveryAcceptedPolicyResolver.ExactLookup() with
            {
                FieldIds = ImmutableArray.Create((ushort)VmcsField.GuestPc),
            };
        Fixture mismatched = CreateFixture(VmcsField.GuestPc, lookup: wrong, enable: false);
        Assert.False(mismatched.Composition.PolicyResolution.IsResolved);
        Assert.False(mismatched.Composition.EnableExact());
        Fixture revoked = CreateFixture(
            VmcsField.GuestPc,
            lookup: GuestPcSpFlagsVmReadScalarDeliveryAcceptedPolicyResolver.ExactLookup(revoked: true),
            enable: false);
        Assert.False(revoked.Composition.EnableExact());
    }

    [Theory]
    [InlineData(VmcsField.GuestCr0)]
    [InlineData(VmcsField.GuestCr4)]
    [InlineData(VmcsField.GuestCr3)]
    [InlineData(VmcsField.HostCr0)]
    [InlineData(VmcsField.HostCr3)]
    [InlineData(VmcsField.PinBasedControls)]
    public void AdjacentHostAndCompatibilityControlFields_AreDenied(VmcsField field)
    {
        Fixture fixture = CreateFixture(field);
        Admit(fixture);
        Assert.False(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)field, 1));
        Assert.Equal(VmReadScalarDeliveryDecision.FieldDenied,
            fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
        Assert.False(fixture.Carrier.HasVmReadScalarResultReceipt);
    }

    [Fact]
    public void MissingStaleCrossDomainAndCrossAddressSource_CannotIssueReceipt()
    {
        Fixture fixture = CreateFixture(VmcsField.GuestPc);
        Admit(fixture);
        ExecutionDomainSourceBindResult replacement = fixture.Runtime.ReplaceAuthoritativeReadOnlyState(
            Descriptor(10, 20, 30), AddressSpace);
        Assert.False(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)VmcsField.GuestPc, 1));
        Assert.Equal(VmReadScalarDeliveryDecision.SourceDenied,
            fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
        Assert.True(replacement.IsBound);

        var foreignContext = Context(replacement.Descriptor!, domain: Domain + 1);
        Assert.False(fixture.Composition.RefreshSourceContext(foreignContext));
        Assert.False(fixture.Composition.RefreshSourceContext(
            Context(replacement.Descriptor!, addressSpace: AddressSpace + 1)));
    }

    [Fact]
    public void OrdinarySourceReplacement_DoesNotCancelCapturedValueButFutureCaptureUsesNewSource()
    {
        Fixture old = Prepare(VmcsField.GuestPc);
        VmReadScalarResultReceipt oldReceipt = Receipt(old);
        ExecutionDomainSourceBindResult replacement = old.Runtime.ReplaceAuthoritativeReadOnlyState(
            Descriptor(0xaaaa, 0xbbbb, 0xcccc), AddressSpace);
        Assert.True(old.Composition.RefreshSourceContext(Context(replacement.Descriptor!)));
        Assert.True(oldReceipt.TryValidateSpeculative());

        YAKSys_Hybrid_CPU.Processor.CPU_Core oldCore = old.Core;
        Assert.True(old.Carrier.Execute(ref oldCore));
        RetireOne(old.Carrier, oldCore);
        Assert.Equal(0x1111UL, oldCore.ReadArch(0, 3));

        Fixture current = CreateFixture(
            VmcsField.GuestPc,
            runtime: old.Runtime,
            composition: old.Composition,
            context: Context(replacement.Descriptor!),
            configureExisting: true);
        Admit(current);
        Assert.True(current.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            current.Packet, current.Lane, (ushort)VmcsField.GuestPc, 1));
        Assert.Equal(0xaaaaUL, Receipt(current).Value);
        Assert.NotEqual(oldReceipt.SourceEpoch, Receipt(current).SourceEpoch);
    }

    [Fact]
    public void ReplaySquashRestoreAndDisable_InvalidateOutstandingReceiptWithZeroArchitecturalEffect()
    {
        Fixture replay = Prepare(VmcsField.GuestFlags);
        VmReadScalarResultReceipt replayReceipt = Receipt(replay);
        replay.Scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
            false, 18, 0x4000, 1, 2, 0, 0, ReplayPhaseInvalidationReason.Manual));
        Assert.False(replayReceipt.TryValidateSpeculative());
        YAKSys_Hybrid_CPU.Processor.CPU_Core replayCore = replay.Core;
        Assert.True(replay.Carrier.Execute(ref replayCore));
        Assert.False(replay.Carrier.TryGetPrimaryWriteBackResult(out _));
        Assert.Equal(0UL, replayCore.ReadArch(0, 3));

        Fixture disabled = Prepare(VmcsField.GuestSp);
        VmReadScalarResultReceipt disabledReceipt = Receipt(disabled);
        disabled.Scheduler.DisableExactGuestPcSpFlagsVmReadScalarDelivery();
        Assert.False(disabledReceipt.TryValidateSpeculative());
        Assert.False(disabledReceipt.TryConsumeAtRetire(1));
        Assert.Equal(0UL, disabled.Core.ReadArch(0, 3));

        Fixture restored = Prepare(VmcsField.GuestPc);
        VmReadScalarResultReceipt restoredReceipt = Receipt(restored);
        ExecutionDomainSourceBindResult rebound = restored.Runtime.RebindAuthoritativeReadOnlyStateAfterRestore(
            Descriptor(4, 5, 6), AddressSpace);
        restored.Composition.ReplaceAfterRestore(Context(rebound.Descriptor!), 2);
        Assert.False(restoredReceipt.TryValidateSpeculative());
        Assert.False(restoredReceipt.TryConsumeAtRetire(1));
        Assert.Equal(0UL, restored.Core.ReadArch(0, 3));
    }

    [Fact]
    public void Receipt_IsAttemptFieldVtDomainDestinationBoundAndSingleUse()
    {
        Fixture fixture = Prepare(VmcsField.GuestSp);
        VmReadScalarResultReceipt receipt = Receipt(fixture);
        Assert.False(receipt.MatchesCarrier(fixture.Carrier, (ushort)VmcsField.GuestPc));

        VmxMicroOp crossVt = CreateVmRead(vt: 1);
        Assert.False(receipt.MatchesCarrier(crossVt, (ushort)VmcsField.GuestSp));
        VmxMicroOp crossDomain = CreateVmRead(domain: Domain + 1);
        Assert.False(receipt.MatchesCarrier(crossDomain, (ushort)VmcsField.GuestSp));
        VmxMicroOp crossDestination = CreateVmRead(destination: 4);
        Assert.False(receipt.MatchesCarrier(crossDestination, (ushort)VmcsField.GuestSp));

        Assert.True(receipt.TryConsumeAtRetire(1));
        Assert.False(receipt.TryConsumeAtRetire(1));
    }

    [Fact]
    public void ForgedSourceOwnerEpochFieldOrValueBinding_IsRejected()
    {
        Fixture fixture = Prepare(VmcsField.GuestPc);
        VmReadScalarResultReceipt valid = Receipt(fixture);
        ExecutionDomainRuntime.SourceCapture capture = valid.ExecutionDomainCapture!;

        VmReadScalarResultReceipt Forged(object owner, ulong epoch, VmcsField field, ulong value) => new(
            fixture.Composition,
            new VmReadScalarAttemptBinding(fixture.Carrier.VirtualizationAdmission!),
            valid.DecisionId,
            owner,
            epoch,
            capture,
            valid.ProfileGeneration,
            valid.AttemptId,
            valid.IssuerGeneration,
            valid.BundleIdentity,
            valid.ReplayEpoch,
            valid.RestoreGeneration,
            valid.DomainTag,
            valid.AddressSpaceTag,
            default,
            field,
            valid.DestinationRegister,
            value);

        Assert.False(Forged(new object(), valid.SourceEpoch, valid.Field, valid.Value).TryValidateSpeculative());
        Assert.False(Forged(valid.SourceOwner, valid.SourceEpoch + 1, valid.Field, valid.Value).TryValidateSpeculative());
        Assert.False(Forged(valid.SourceOwner, valid.SourceEpoch, VmcsField.GuestSp, valid.Value).TryValidateSpeculative());
        Assert.False(Forged(valid.SourceOwner, valid.SourceEpoch, valid.Field, valid.Value + 1).TryValidateSpeculative());
    }

    [Fact]
    public void VmWriteWrongDestinationAndMissingFullDomainEvidence_NeverIssueReceipt()
    {
        Fixture vmwrite = CreateFixture(
            VmcsField.GuestPc,
            carrier: CreateVmRead(opcode: IsaOpcodeValues.VMWRITE));
        Assert.True(vmwrite.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            vmwrite.Packet, vmwrite.Lane));
        Assert.False(vmwrite.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            vmwrite.Packet, vmwrite.Lane, (ushort)VmcsField.GuestPc, 1));
        Assert.False(vmwrite.Carrier.HasVmReadScalarResultReceipt);

        Fixture x0 = CreateFixture(VmcsField.GuestPc, carrier: CreateVmRead(destination: 0));
        Admit(x0);
        Assert.False(x0.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            x0.Packet, x0.Lane, (ushort)VmcsField.GuestPc, 1));
        Assert.Equal(VmReadScalarDeliveryDecision.DestinationDenied,
            x0.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);

        Fixture missingDomain = CreateFixture(VmcsField.GuestPc, includeMemoryIo: false);
        Admit(missingDomain);
        Assert.False(missingDomain.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            missingDomain.Packet, missingDomain.Lane, (ushort)VmcsField.GuestPc, 1));
        Assert.Equal(VmReadScalarDeliveryDecision.ProjectionDenied,
            missingDomain.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);

        Fixture evidenceDenied = CreateFixture(
            VmcsField.GuestPc,
            evidence: EvidencePolicyDescriptor.FailClosed);
        Admit(evidenceDenied);
        Assert.False(evidenceDenied.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            evidenceDenied.Packet, evidenceDenied.Lane, (ushort)VmcsField.GuestPc, 1));
        Assert.Equal(VmReadScalarDeliveryDecision.ProjectionDenied,
            evidenceDenied.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
    }

    [Fact]
    public async Task PrepareVersusDisable_LeavesEveryOutstandingReceiptInvalid()
    {
        for (int iteration = 0; iteration < 64; iteration++)
        {
            Fixture fixture = CreateFixture(VmcsField.GuestPc);
            Admit(fixture);
            Task<VmReadScalarDeliveryResult> prepare = Task.Run(() => fixture.Composition.Prepare(
                Replay(), fixture.Carrier,
                new VmReadScalarAttemptBinding(fixture.Carrier.VirtualizationAdmission!),
                (ushort)VmcsField.GuestPc, 1));
            Task disable = Task.Run(fixture.Composition.Disable);
            await Task.WhenAll(prepare, disable);
            VmReadScalarResultReceipt? receipt = (await prepare).Receipt;
            Assert.False(fixture.Composition.IsEnabled);
            Assert.False(receipt?.TryValidateSpeculative() ?? false);
            Assert.False(receipt?.TryConsumeAtRetire(1) ?? false);
        }
    }

    [Fact]
    public void TwoDomains_DoNotShareSourceOrReceiptAuthority()
    {
        Fixture first = Prepare(VmcsField.GuestPc);
        Fixture second = Prepare(VmcsField.GuestPc, domain: 8, addressSpace: 10);
        VmReadScalarResultReceipt firstReceipt = Receipt(first);
        VmReadScalarResultReceipt secondReceipt = Receipt(second);
        Assert.False(first.Composition.ValidateLive(secondReceipt));
        Assert.False(second.Composition.ValidateLive(firstReceipt));
        first.Composition.Disable();
        Assert.False(firstReceipt.TryValidateSpeculative());
        Assert.True(secondReceipt.TryValidateSpeculative());
    }

    private static Fixture Prepare(
        VmcsField field,
        ulong domain = Domain,
        ulong addressSpace = AddressSpace)
    {
        Fixture fixture = CreateFixture(field, domain: domain, addressSpace: addressSpace);
        Admit(fixture);
        Assert.True(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)field, 1));
        return fixture;
    }

    private static void Admit(Fixture fixture) =>
        Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            fixture.Packet, fixture.Lane));

    private static VmReadScalarResultReceipt Receipt(Fixture fixture) =>
        Assert.IsType<VmReadScalarResultReceipt>(
            fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt);

    private static Fixture CreateFixture(
        VmcsField field,
        GuestPcSpFlagsVmReadScalarDeliveryPolicyLookup? lookup = null,
        bool enable = true,
        bool includeMemoryIo = true,
        EvidencePolicyDescriptor? evidence = null,
        VmxMicroOp? carrier = null,
        ulong domain = Domain,
        ulong addressSpace = AddressSpace,
        ExecutionDomainRuntime? runtime = null,
        GuestPcSpFlagsVmReadScalarDeliveryCanonicalComposition? composition = null,
        DomainRuntimeContext? context = null,
        bool configureExisting = false)
    {
        runtime ??= new ExecutionDomainRuntime();
        if (context is null)
        {
            ExecutionDomainSourceBindResult bound = runtime.BindAuthoritativeReadOnlyState(
                Descriptor(0x1111, 0x2222, 0x3333, domain), addressSpace);
            Assert.True(bound.IsBound);
            context = Context(bound.Descriptor!, domain, addressSpace, includeMemoryIo);
        }
        composition ??= new GuestPcSpFlagsVmReadScalarDeliveryCanonicalComposition(
            runtime,
            context,
            new RootAuthorityDescriptor(
                RootAuthorityClass.RuntimeRoot, 1, 0,
                allowCompatibilityFrontendActivation: false,
                allowAuthoritativeStateMutation: false),
            evidence ?? new EvidencePolicyDescriptor(
                allowCompatibilityAliases: true,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            restoreGeneration: 1,
            lookup ?? GuestPcSpFlagsVmReadScalarDeliveryAcceptedPolicyResolver.ExactLookup());

        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(Replay());
        if (enable && !composition.IsEnabled)
            Assert.True(composition.EnableExact());
        if (enable || configureExisting)
            scheduler.ConfigureExactGuestPcSpFlagsVmReadScalarDelivery(composition);
        carrier ??= CreateVmRead(domain: domain);
        IssuePacketLane lane = CreateLane(carrier);
        BundleIssuePacket packet = CreatePacket(lane);
        return new(scheduler, carrier, lane, packet, composition, runtime, context,
            new YAKSys_Hybrid_CPU.Processor.CPU_Core(0));
    }

    private static DomainRuntimeContext Context(
        ExecutionDomainDescriptor execution,
        ulong domain = Domain,
        ulong addressSpace = AddressSpace,
        bool includeMemoryIo = true) => new(
            execution,
            includeMemoryIo ? new MemoryDomainDescriptor() : null,
            includeMemoryIo ? new IoDomainDescriptor() : null,
            CapabilityDescriptorSet.Empty,
            secureCompute: null,
            domain,
            addressSpace);

    private static ExecutionDomainDescriptor Descriptor(
        ulong pc,
        ulong sp,
        ulong flags,
        ulong domain = Domain) => new(
            domain, null, null, null, true,
            ExecutionDomainReadOnlyStateView.FromGuestPcSpFlags(pc, sp, flags));

    private static VmxMicroOp CreateVmRead(
        ushort opcode = IsaOpcodeValues.VMREAD,
        byte destination = 3,
        ulong domain = Domain,
        int vt = 0)
    {
        var vmx = new VmxMicroOp
        {
            OpCode = opcode,
            OwnerThreadId = vt,
            VirtualThreadId = vt,
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

    private static void RetireOne(
        VmxMicroOp carrier,
        YAKSys_Hybrid_CPU.Processor.CPU_Core core)
    {
        Span<RetireRecord> records = stackalloc RetireRecord[1];
        int count = 0;
        carrier.EmitWriteBackRetireRecords(ref core, records, ref count);
        core.RetireCoordinator.Retire(records[..count]);
    }

    private sealed record Fixture(
        MicroOpScheduler Scheduler,
        VmxMicroOp Carrier,
        IssuePacketLane Lane,
        BundleIssuePacket Packet,
        GuestPcSpFlagsVmReadScalarDeliveryCanonicalComposition Composition,
        ExecutionDomainRuntime Runtime,
        DomainRuntimeContext Context,
        YAKSys_Hybrid_CPU.Processor.CPU_Core Core);
}
