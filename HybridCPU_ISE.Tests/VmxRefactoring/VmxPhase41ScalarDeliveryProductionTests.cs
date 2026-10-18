using System.Collections.Immutable;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase41ScalarDeliveryProductionTests
{
    [Theory]
    [InlineData(VmcsField.GuestCr0, 0x80000011UL)]
    [InlineData(VmcsField.GuestCr4, 0x00000620UL)]
    public void ExactField_UsesCanonicalE1ScalarCarrierAndRetireCoordinator(
        VmcsField field,
        ulong expected)
    {
        Fixture fixture = CreateFixture(field);

        Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            fixture.Packet, fixture.Lane));
        Assert.False(fixture.Scheduler.TryAttachVirtualizationOperandSnapshotAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)field, restoreGeneration: 1));
        Assert.True(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)field, restoreGeneration: 1));

        VmReadScalarDeliveryResult prepared = Assert.IsType<VmReadScalarDeliveryResult>(
            fixture.Scheduler.LastVmReadScalarDeliveryResult);
        VmReadScalarResultReceipt receipt = Assert.IsType<VmReadScalarResultReceipt>(prepared.Receipt);
        Assert.True(prepared.IsPrepared);
        Assert.Equal(expected, receipt.Value);
        Assert.Equal(field, receipt.Field);
        Assert.Equal((byte)3, receipt.DestinationRegister);
        Assert.False(receipt.BackendExecutionAuthorized);
        Assert.False(receipt.CompletionPublicationAuthorized);
        Assert.False(receipt.VmxRetireEffectAuthorized);

        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        Assert.True(fixture.Carrier.TryGetPrimaryWriteBackResult(out ulong carried));
        Assert.Equal(expected, carried);
        Assert.False(fixture.Carrier.CreateRetireEffect().IsValid);

        Span<RetireRecord> records = stackalloc RetireRecord[1];
        int count = 0;
        fixture.Carrier.EmitWriteBackRetireRecords(ref core, records, ref count);
        Assert.Equal(1, count);
        Assert.Equal(RetireRecordKind.RegisterWrite, records[0].Kind);
        Assert.Equal(3, records[0].ArchReg);
        Assert.Equal(expected, records[0].Value);
        core.RetireCoordinator.Retire(records[..count]);
        Assert.Equal(expected, core.ReadArch(0, 3));
        Assert.True(receipt.IsConsumed);
    }

    [Theory]
    [InlineData(VmcsField.GuestCr3)]
    [InlineData(VmcsField.HostCr3)]
    [InlineData(VmcsField.HostCr0)]
    [InlineData(VmcsField.PinBasedControls)]
    public void AdjacentAndHostFields_AreDeterministicallyDenied(VmcsField field)
    {
        Fixture fixture = CreateFixture(field);
        Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            fixture.Packet, fixture.Lane));
        Assert.False(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)field, restoreGeneration: 1));
        Assert.Equal(VmReadScalarDeliveryDecision.FieldDenied,
            fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
        Assert.False(fixture.Carrier.HasVmReadScalarResultReceipt);
    }

    [Fact]
    public void MissingWrongOrRevokedPolicy_NeverEnablesComposition()
    {
        VmReadScalarDeliveryPolicyLookup wrong =
            VmReadScalarDeliveryAcceptedPolicyResolver.ExactLookup() with
            {
                FieldIds = ImmutableArray.Create((ushort)VmcsField.GuestCr0),
            };
        VmReadScalarDeliveryCanonicalComposition mismatched = CreateComposition(Descriptor(), wrong);
        VmReadScalarDeliveryCanonicalComposition revoked = CreateComposition(
            Descriptor(), VmReadScalarDeliveryAcceptedPolicyResolver.ExactLookup(revoked: true));

        Assert.False(mismatched.PolicyResolution.IsResolved);
        Assert.False(mismatched.EnableExact());
        Assert.False(revoked.PolicyResolution.IsResolved);
        Assert.False(revoked.EnableExact());
    }

    [Fact]
    public void DisableAfterSpeculativeDelivery_DeniesRetireAndLeavesZeroArchitecturalEffect()
    {
        Fixture fixture = Prepare(VmcsField.GuestCr0);
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        fixture.Scheduler.DisableExactVmReadScalarDelivery();

        Assert.Throws<InvalidOperationException>(() => EmitOne(fixture.Carrier, core));
        Assert.Equal(0UL, core.ReadArch(0, 3));
    }

    [Fact]
    public void AlteredWritebackValue_CannotEscapeAttemptBoundReceipt()
    {
        Fixture fixture = Prepare(VmcsField.GuestCr0);
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        fixture.Carrier.CapturePrimaryWriteBackResult(0xDEAD_BEEFUL);

        Assert.Throws<InvalidOperationException>(() => EmitOne(fixture.Carrier, core));
        Assert.Equal(0UL, core.ReadArch(0, 3));
    }

    [Fact]
    public void DescriptorEpochReplacement_InvalidatesPreRestoreReceipt()
    {
        Fixture fixture = Prepare(VmcsField.GuestCr4);
        VmReadScalarResultReceipt receipt = Assert.IsType<VmReadScalarResultReceipt>(
            fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt);
        fixture.Composition.ReplaceAfterRestore(
            Descriptor() with { PolicyEpoch = new PrivilegedExecutionStateEpoch(12) },
            new PrivilegedExecutionStateEpoch(12),
            restoreGeneration: 2);

        Assert.False(fixture.Composition.IsEnabled);
        Assert.False(receipt.TryValidateSpeculative());
        Assert.False(receipt.TryConsumeAtRetire(1));
    }

    [Fact]
    public void Receipt_IsSingleUseAndRestoreGenerationBound()
    {
        Fixture fixture = Prepare(VmcsField.GuestCr0);
        VmReadScalarResultReceipt receipt = Assert.IsType<VmReadScalarResultReceipt>(
            fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt);

        Assert.False(receipt.TryConsumeAtRetire(currentRestoreGeneration: 2));
        Assert.True(receipt.TryConsumeAtRetire(currentRestoreGeneration: 1));
        Assert.False(receipt.TryConsumeAtRetire(currentRestoreGeneration: 1));
    }

    [Fact]
    public void NewRestoreGenerationWithoutRevalidatedDescriptor_DisablesExactProfile()
    {
        Fixture fixture = CreateFixture(VmcsField.GuestCr0);
        Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            fixture.Packet, fixture.Lane));

        Assert.False(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet,
            fixture.Lane,
            (ushort)VmcsField.GuestCr0,
            restoreGeneration: 2));
        Assert.Equal(VmReadScalarDeliveryDecision.StaleReceipt,
            fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
        Assert.False(fixture.Composition.IsEnabled);
        Assert.False(fixture.Carrier.HasVmReadScalarResultReceipt);
    }

    [Fact]
    public void ReplayPhaseReplacement_InvalidatesReceiptBeforeExecuteOrRetire()
    {
        Fixture fixture = Prepare(VmcsField.GuestCr0);
        VmReadScalarResultReceipt receipt = Assert.IsType<VmReadScalarResultReceipt>(
            fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt);
        fixture.Scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
            false, 18, 0x4000, 1, 2, 0, 0,
            ReplayPhaseInvalidationReason.Manual));

        Assert.False(receipt.TryValidateSpeculative());
        Assert.False(receipt.TryConsumeAtRetire(1));
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
        Assert.False(fixture.Carrier.TryGetPrimaryWriteBackResult(out _));
    }

    [Fact]
    public void MissingDescriptorAndDomainMismatch_FailClosedInOwnerProjection()
    {
        PrivilegedExecutionStateDescriptor missing = PrivilegedExecutionStateDescriptor.Unmaterialized;
        VmReadScalarDeliveryCanonicalComposition composition = CreateComposition(missing);
        Assert.True(composition.EnableExact());
        Fixture fixture = CreateFixture(VmcsField.GuestCr0, composition);
        Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            fixture.Packet, fixture.Lane));
        Assert.False(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)VmcsField.GuestCr0, 1));
        Assert.Equal(VmReadScalarDeliveryDecision.ProjectionDenied,
            fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
    }

    [Fact]
    public void EveryOwnerValueEvidenceMigrationAndConformanceFailure_FailsClosed()
    {
        PrivilegedExecutionStateDescriptor baseline = Descriptor();
        PrivilegedExecutionStateDescriptor[] deniedDescriptors =
        [
            baseline with { DomainTag = 8 },
            baseline with { AddressSpaceTag = 10 },
            baseline with { GuestCr0 = new(PrivilegedControlRegisterKind.GuestCr4, baseline.GuestCr0.Value) },
            baseline with { EvidenceClass = PrivilegedExecutionStateEvidenceClass.CompatibilityAlias },
            baseline with { MigrationClass = PrivilegedExecutionStateMigrationClass.DomainLocal },
            baseline with { GuestCr0 = new(PrivilegedControlRegisterKind.GuestCr0, 0x1_0000_0000UL) },
            baseline with { GuestCr0 = new(PrivilegedControlRegisterKind.GuestCr0, 0) },
            baseline with { GuestCr4 = new(PrivilegedControlRegisterKind.GuestCr4, 0x1_0000_0000UL) },
            baseline with { GuestCr4 = new(PrivilegedControlRegisterKind.GuestCr4, 0) },
        ];

        foreach (PrivilegedExecutionStateDescriptor descriptor in deniedDescriptors)
            AssertProjectionDenied(CreateComposition(descriptor));

        AssertProjectionDenied(CreateComposition(
            baseline,
            currentEpoch: new PrivilegedExecutionStateEpoch(12)));
        AssertProjectionDenied(CreateComposition(
            baseline,
            conformanceProven: false));
        AssertProjectionDenied(CreateComposition(
            baseline,
            evidence: EvidencePolicyDescriptor.FailClosed));
    }

    [Fact]
    public void VmWriteAndWrongDestinationShape_NeverAttachReceipt()
    {
        Fixture vmwrite = CreateFixture(
            VmcsField.GuestCr0,
            carrier: CreateVmRead(IsaOpcodeValues.VMWRITE));
        Assert.True(vmwrite.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            vmwrite.Packet, vmwrite.Lane));
        Assert.False(vmwrite.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            vmwrite.Packet, vmwrite.Lane, (ushort)VmcsField.GuestCr0, 1));
        Assert.False(vmwrite.Carrier.HasVmReadScalarResultReceipt);

        Fixture x0 = CreateFixture(
            VmcsField.GuestCr0,
            carrier: CreateVmRead(destination: 0));
        Assert.True(x0.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            x0.Packet, x0.Lane));
        Assert.False(x0.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            x0.Packet, x0.Lane, (ushort)VmcsField.GuestCr0, 1));
        Assert.Equal(VmReadScalarDeliveryDecision.DestinationDenied,
            x0.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
    }

    [Fact]
    public async Task ProjectionVersusDisable_FinalStateDeniesEveryOutstandingReceipt()
    {
        for (int iteration = 0; iteration < 64; iteration++)
        {
            Fixture fixture = CreateFixture(VmcsField.GuestCr0);
            Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
                fixture.Packet, fixture.Lane));

            Task<VmReadScalarDeliveryResult> prepare = Task.Run(() =>
                fixture.Composition.Prepare(
                    Replay(),
                    fixture.Carrier,
                    fixture.Carrier.VirtualizationAdmission,
                    (ushort)VmcsField.GuestCr0,
                    1));
            Task disable = Task.Run(fixture.Scheduler.DisableExactVmReadScalarDelivery);
            await Task.WhenAll(prepare, disable);

            VmReadScalarResultReceipt? receipt = (await prepare).Receipt;
            Assert.False(fixture.Composition.IsEnabled);
            Assert.False(receipt?.TryValidateSpeculative() ?? false);
            Assert.False(receipt?.TryConsumeAtRetire(1) ?? false);
        }
    }

    [Fact]
    public async Task ProjectionVersusRestore_ObservesOldOrNewAtomicDescriptorEpochSnapshotOnly()
    {
        PrivilegedExecutionStateDescriptor oldDescriptor = Descriptor();
        PrivilegedExecutionStateDescriptor newDescriptor = Descriptor(
            epoch: 12,
            guestCr0: 0x80010011UL,
            guestCr4: 0x00010620UL);

        for (int iteration = 0; iteration < 64; iteration++)
        {
            VmReadScalarDeliveryCanonicalComposition composition = CreateComposition(oldDescriptor);
            Fixture fixture = CreateFixture(VmcsField.GuestCr0, composition);
            Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
                fixture.Packet, fixture.Lane));

            Task<VmReadScalarDeliveryResult> prepare = Task.Run(() =>
                composition.Prepare(
                    Replay(),
                    fixture.Carrier,
                    fixture.Carrier.VirtualizationAdmission,
                    (ushort)VmcsField.GuestCr0,
                    1));
            Task replace = Task.Run(() => composition.ReplaceAfterRestore(
                newDescriptor,
                new PrivilegedExecutionStateEpoch(12),
                restoreGeneration: 2));
            await Task.WhenAll(prepare, replace);

            VmReadScalarResultReceipt? receipt = (await prepare).Receipt;
            if (receipt is null)
                continue;

            bool isOld = receipt.DescriptorEpoch == new PrivilegedExecutionStateEpoch(11) &&
                receipt.Value == oldDescriptor.GuestCr0.Value;
            Assert.True(isOld);
            Assert.False(receipt.TryValidateSpeculative());
        }

        VmReadScalarDeliveryCanonicalComposition revalidated = CreateComposition(
            newDescriptor,
            currentEpoch: new PrivilegedExecutionStateEpoch(12),
            restoreGeneration: 2);
        Fixture activeAfterRestore = CreateFixture(VmcsField.GuestCr0, revalidated);
        Assert.True(activeAfterRestore.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            activeAfterRestore.Packet, activeAfterRestore.Lane));
        Assert.True(activeAfterRestore.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            activeAfterRestore.Packet,
            activeAfterRestore.Lane,
            (ushort)VmcsField.GuestCr0,
            restoreGeneration: 2));
        Assert.Equal(newDescriptor.GuestCr0.Value,
            activeAfterRestore.Scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt!.Value);
    }

    [Fact]
    public void IndependentDomains_CannotShareDescriptorOrReceiptAuthority()
    {
        VmReadScalarDeliveryCanonicalComposition domain7 = CreateComposition(Descriptor());
        VmReadScalarDeliveryCanonicalComposition domain8 = CreateComposition(
            Descriptor(domainTag: 8, addressSpaceTag: 10),
            context: Context(domainTag: 8, addressSpaceTag: 10));
        Fixture first = PrepareWithComposition(VmcsField.GuestCr0, domain7);
        Fixture second = PrepareWithComposition(
            VmcsField.GuestCr0,
            domain8,
            CreateVmRead(domainTag: 8));
        VmReadScalarResultReceipt firstReceipt = Assert.IsType<VmReadScalarResultReceipt>(
            first.Scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt);
        VmReadScalarResultReceipt secondReceipt = Assert.IsType<VmReadScalarResultReceipt>(
            second.Scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt);

        Assert.Equal(7UL, firstReceipt.DomainTag);
        Assert.Equal(9UL, firstReceipt.AddressSpaceTag);
        Assert.Equal(8UL, secondReceipt.DomainTag);
        Assert.Equal(10UL, secondReceipt.AddressSpaceTag);
        domain7.Disable();
        Assert.False(firstReceipt.TryValidateSpeculative());
        Assert.True(secondReceipt.TryValidateSpeculative());
    }

    private static Fixture Prepare(VmcsField field)
    {
        Fixture fixture = CreateFixture(field);
        Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            fixture.Packet, fixture.Lane));
        Assert.True(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)field, 1));
        return fixture;
    }

    private static Fixture PrepareWithComposition(
        VmcsField field,
        VmReadScalarDeliveryCanonicalComposition composition,
        VmxMicroOp? carrier = null)
    {
        Fixture fixture = CreateFixture(field, composition, carrier);
        Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            fixture.Packet, fixture.Lane));
        Assert.True(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)field, 1));
        return fixture;
    }

    private static Fixture CreateFixture(
        VmcsField field,
        VmReadScalarDeliveryCanonicalComposition? supplied = null,
        VmxMicroOp? carrier = null)
    {
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(Replay());
        VmReadScalarDeliveryCanonicalComposition composition = supplied ?? CreateComposition(Descriptor());
        if (!composition.IsEnabled)
            Assert.True(composition.EnableExact());
        scheduler.ConfigureExactVmReadScalarDelivery(composition);

        carrier ??= CreateVmRead();
        IssuePacketLane lane = CreateLane(carrier);
        BundleIssuePacket packet = CreatePacket(lane);
        return new(scheduler, carrier, lane, packet, composition,
            new YAKSys_Hybrid_CPU.Processor.CPU_Core(0));
    }

    private static VmReadScalarDeliveryCanonicalComposition CreateComposition(
        PrivilegedExecutionStateDescriptor descriptor,
        VmReadScalarDeliveryPolicyLookup? lookup = null,
        PrivilegedExecutionStateEpoch? currentEpoch = null,
        ulong restoreGeneration = 1,
        bool conformanceProven = true,
        EvidencePolicyDescriptor? evidence = null,
        DomainRuntimeContext? context = null) => new(
            context ?? Context(),
            new RootAuthorityDescriptor(
                RootAuthorityClass.RuntimeRoot, 1, 0,
                allowCompatibilityFrontendActivation: false,
                allowAuthoritativeStateMutation: false),
            evidence ?? new EvidencePolicyDescriptor(
                allowCompatibilityAliases: true,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            descriptor,
            currentEpoch ?? new PrivilegedExecutionStateEpoch(11),
            restoreGeneration,
            lookup ?? VmReadScalarDeliveryAcceptedPolicyResolver.ExactLookup(),
            conformanceProven);

    private static DomainRuntimeContext Context(
        ulong domainTag = 7,
        ulong addressSpaceTag = 9) => new(
        new ExecutionDomainDescriptor(
            domainTag, bundleLegality: null, schedulingBudget: null, extension: null,
            compatibilityProjectionEnabled: true),
        memory: null,
        io: null,
        CapabilityDescriptorSet.Empty,
        secureCompute: null,
        domainTag,
        addressSpaceTag);

    private static PrivilegedExecutionStateDescriptor Descriptor(
        ulong domainTag = 7,
        ulong addressSpaceTag = 9,
        ulong epoch = 11,
        ulong guestCr0 = 0x80000011UL,
        ulong guestCr4 = 0x00000620UL) => new(
        domainTag, addressSpaceTag, new PrivilegedExecutionStateEpoch(epoch), true,
        new(PrivilegedControlRegisterKind.GuestCr0, guestCr0),
        new(PrivilegedControlRegisterKind.GuestCr4, guestCr4),
        new(0xFFFF_FFFFUL, 0x1UL, 0xFFFF_FFFFUL, 0x20UL, true),
        PrivilegedExecutionStateEvidenceClass.GuestVisibleReadOnlyProjection,
        PrivilegedExecutionStateMigrationClass.RevalidatedAfterRestore);

    private static VmxMicroOp CreateVmRead(
        ushort opcode = IsaOpcodeValues.VMREAD,
        byte destination = 3,
        ulong domainTag = 7)
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
        vmx.Placement = vmx.Placement with { DomainTag = domainTag };
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

    private static void EmitOne(
        VmxMicroOp carrier,
        YAKSys_Hybrid_CPU.Processor.CPU_Core core)
    {
        Span<RetireRecord> records = stackalloc RetireRecord[1];
        int count = 0;
        carrier.EmitWriteBackRetireRecords(ref core, records, ref count);
    }

    private static void AssertProjectionDenied(
        VmReadScalarDeliveryCanonicalComposition composition)
    {
        Fixture fixture = CreateFixture(VmcsField.GuestCr0, composition);
        Assert.True(fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            fixture.Packet, fixture.Lane));
        Assert.False(fixture.Scheduler.TryPrepareVmReadScalarAfterCanonicalValueRead(
            fixture.Packet, fixture.Lane, (ushort)VmcsField.GuestCr0, 1));
        Assert.Equal(VmReadScalarDeliveryDecision.ProjectionDenied,
            fixture.Scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
    }

    private sealed record Fixture(
        MicroOpScheduler Scheduler,
        VmxMicroOp Carrier,
        IssuePacketLane Lane,
        BundleIssuePacket Packet,
        VmReadScalarDeliveryCanonicalComposition Composition,
        YAKSys_Hybrid_CPU.Processor.CPU_Core Core);
}
