using HybridCPU_ISE.Arch;
using System.Text.Json;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase65PinBasedControlsScalarDeliveryTests
{
    private const CompatibilityEventRoutingPolicy FullPolicy =
        CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired |
        CompatibilityEventRoutingPolicy.NeutralTrapResultRequired |
        CompatibilityEventRoutingPolicy.PublicationFenceRequired;

    [Fact]
    public void ProductionConstruction_IsExactExplicitAndDefaultDisabled()
    {
        var memory = Memory();
        CpuCorePlatformContext defaults = CpuCorePlatformContext.CreateFixed(
            memory, ProcessorMode.Emulation,
            compatibilityControlPolicyProfile:
                new CompatibilityControlPolicyConstructionProfile(7, FullPolicy));
        Assert.False(defaults.EnablePinBasedControlsVmRead);
        Assert.False(new Processor.CPU_Core(0, defaults).HasActivePinBasedControlsVmReadProfile);

        CpuCorePlatformContext exact = CpuCorePlatformContext.CreateFixed(
            memory, ProcessorMode.Emulation,
            compatibilityControlPolicyProfile:
                new CompatibilityControlPolicyConstructionProfile(7, FullPolicy),
            pinBasedControlsVmReadProfile:
                PinBasedControlsVmReadProductionConstructionProfile.CreateExact(7));
        Assert.True(exact.EnablePinBasedControlsVmRead);
        Assert.True(new Processor.CPU_Core(0, exact).HasActivePinBasedControlsVmReadProfile);

        Assert.Throws<ArgumentException>(() =>
            CpuCorePlatformContext.CreateFixed(
                memory, ProcessorMode.Emulation,
                pinBasedControlsVmReadProfile:
                    PinBasedControlsVmReadProductionConstructionProfile.CreateExact(7)));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    [InlineData(6UL)]
    [InlineData(7UL)]
    public void ExactProjection_UsesOwnerIssuedSnapshotAndExistingScalarReceipt(ulong mask)
    {
        Processor.CPU_Core core = CreateCore((CompatibilityEventRoutingPolicy)mask, enabled: true);
        Prepared prepared = Prepare(core, VmcsField.PinBasedControls);

        Assert.Equal(mask, prepared.Receipt.Value);
        Assert.Same(core.CompatibilityControlPolicyOwner, prepared.Receipt.SourceOwner);
        Assert.Same(core.CaptureCompatibilityControlPolicy(),
            prepared.Receipt.CompatibilityControlCapture);
        Assert.True(prepared.Carrier.Execute(ref core));
        Assert.True(prepared.Carrier.TryGetPrimaryWriteBackResult(out ulong value));
        Assert.Equal(mask, value);

        Span<RetireRecord> records = stackalloc RetireRecord[1];
        int count = 0;
        prepared.Carrier.EmitWriteBackRetireRecords(ref core, records, ref count);
        Assert.Equal(1, count);
        Assert.True(records[0].IsRegisterWrite);
        Assert.Equal(3, records[0].ArchReg);
        Assert.Equal(mask, records[0].Value);
        Assert.Throws<InvalidOperationException>(() =>
        {
            Span<RetireRecord> duplicate = stackalloc RetireRecord[1];
            int duplicateCount = 0;
            prepared.Carrier.EmitWriteBackRetireRecords(
                ref core, duplicate, ref duplicateCount);
        });
    }

    [Theory]
    [InlineData(VmcsField.ProcBasedControls)]
    [InlineData(VmcsField.ExitControls)]
    [InlineData(VmcsField.EntryControls)]
    [InlineData(VmcsField.SecondaryProcControls)]
    public void AdjacentControlFields_AreExplicitlyDenied(VmcsField field)
    {
        Processor.CPU_Core core = CreateCore(FullPolicy, enabled: true);
        var composition = new PinBasedControlsVmReadScalarDeliveryCanonicalComposition(
            core.CompatibilityControlPolicyOwner!, enabled: true);
        var scheduler = Scheduler();
        VmxMicroOp carrier = CreateVmRead();
        IssuePacketLane lane = CreateLane(carrier);
        Assert.True(scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            CreatePacket(lane), lane));

        Assert.False(scheduler.TryPreparePinBasedControlsVmReadAfterCanonicalValueRead(
            composition, lane, (ushort)field, core.CurrentVirtualizationRestoreGeneration));
        Assert.Equal(VmReadScalarDeliveryDecision.FieldDenied,
            scheduler.LastVmReadScalarDeliveryResult!.Value.Decision);
    }

    [Fact]
    public void ReplaceRebindRestoreAndKillSwitch_RevokeOutstandingReceipts()
    {
        Processor.CPU_Core replaceCore = CreateCore(FullPolicy, enabled: true);
        Prepared replaced = Prepare(replaceCore, VmcsField.PinBasedControls);
        replaceCore.ReplaceCompatibilityControlPolicy(
            CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired);
        Assert.False(replaced.Receipt.TryValidateSpeculative(
            replaceCore.CurrentVirtualizationRestoreGeneration));

        Processor.CPU_Core rebindCore = CreateCore(FullPolicy, enabled: true);
        Prepared rebound = Prepare(rebindCore, VmcsField.PinBasedControls);
        rebindCore.RebindCompatibilityControlPolicy(8);
        Assert.False(rebound.Receipt.TryValidateSpeculative(
            rebindCore.CurrentVirtualizationRestoreGeneration));

        Processor.CPU_Core restoreCore = CreateCore(FullPolicy, enabled: true);
        Prepared restored = Prepare(restoreCore, VmcsField.PinBasedControls);
        Processor.CPU_Core.VectorContext saved = restoreCore.SaveVectorContext();
        restoreCore.RestoreVectorContext(saved);
        Assert.False(restored.Receipt.TryValidateSpeculative(
            restoreCore.CurrentVirtualizationRestoreGeneration));

        Processor.CPU_Core disabledCore = CreateCore(FullPolicy, enabled: true);
        Prepared disabled = Prepare(disabledCore, VmcsField.PinBasedControls);
        Assert.True(disabled.Composition.Disable());
        Assert.False(disabled.Composition.IsEnabled);
        Assert.False(disabled.Receipt.TryValidateSpeculative(
            disabledCore.CurrentVirtualizationRestoreGeneration));
        Assert.True(disabled.Composition.Disable());
    }

    [Fact]
    public async Task PrepareVersusKillSwitch_NeverLeavesALiveReceipt()
    {
        Processor.CPU_Core core = CreateCore(FullPolicy, enabled: true);
        var composition = new PinBasedControlsVmReadScalarDeliveryCanonicalComposition(
            core.CompatibilityControlPolicyOwner!, enabled: true);
        var scheduler = Scheduler();
        VmxMicroOp carrier = CreateVmRead();
        IssuePacketLane lane = CreateLane(carrier);
        Assert.True(scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            CreatePacket(lane), lane));

        Task<bool> prepare = Task.Run(() =>
            scheduler.TryPreparePinBasedControlsVmReadAfterCanonicalValueRead(
                composition, lane, (ushort)VmcsField.PinBasedControls,
                core.CurrentVirtualizationRestoreGeneration));
        Task<bool> disable = Task.Run(composition.Disable);
        await Task.WhenAll(prepare, disable);

        Assert.True(await disable);
        Assert.False(composition.IsEnabled);
        if (scheduler.LastVmReadScalarDeliveryResult?.Receipt is { } receipt)
        {
            Assert.False(receipt.TryValidateSpeculative(
                core.CurrentVirtualizationRestoreGeneration));
            Assert.False(receipt.TryConsumeAtRetire(
                core.CurrentVirtualizationRestoreGeneration));
        }
    }

    [Fact]
    public void ProductionGraph_ReusesCanonicalReceiptWritebackAndRetireOnly()
    {
        string materialization = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs");
        string scheduler = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/Scheduling/Smt/MicroOpScheduler.ExactVmReadScalarDelivery.cs");
        string carrier = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs");
        string composition = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Events/VmRead/PinBasedControlsVmReadScalarDeliveryCanonicalComposition.cs");

        Assert.Contains("TryPreparePinBasedControlsVmReadAfterCanonicalValueRead", materialization);
        Assert.Contains("AttachVmReadScalarResultReceipt", scheduler);
        Assert.Contains("TryConsumeAtRetire", carrier);
        Assert.Contains("RetireRecord.RegisterWrite", carrier);
        Assert.DoesNotContain("VmcsReadOnlyValueProjectionService", composition);
        Assert.DoesNotContain("VmExitReason", composition);
        Assert.DoesNotContain("VMCALL", composition, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VMWRITE", composition, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MachineStatusAndDecision_CloseDeliveryOnlyAndReturnToNone()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string plan = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan");
        using JsonDocument status = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            plan, "VirtualizationActivationStatusV1.json")));
        using JsonDocument decision = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            plan, "PinBasedControlsVmReadScalarDeliveryDecisionV1.json")));

        Assert.Equal("None", status.RootElement.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateOwnerAuthorizationForNextExactFieldOrOperation",
            status.RootElement.GetProperty("NextCandidatePool").GetString());
        JsonElement phase = status.RootElement.GetProperty(
            "Phase65PinBasedControlsVmReadScalarDelivery");
        Assert.Equal("VmcsField.PinBasedControls", phase.GetProperty("ExactField").GetString());
        Assert.Equal("DeniedPendingSeparateAuthorization",
            phase.GetProperty("ProductionRelease").GetString());
        Assert.Equal("None", phase.GetProperty("RuntimeAuthority").GetString());

        Assert.Equal(PinBasedControlsVmReadScalarDeliveryCanonicalComposition.DecisionId,
            decision.RootElement.GetProperty("DecisionId").GetString());
        Assert.Equal("NotAuthorized",
            decision.RootElement.GetProperty("ProductionRelease").GetString());
        Assert.False(decision.RootElement.GetProperty("CompatibilityAuthority").GetBoolean());
        Assert.Equal("Denied", decision.RootElement.GetProperty("AdjacentFields").GetString());
    }

    private static Prepared Prepare(Processor.CPU_Core core, VmcsField field)
    {
        var composition = new PinBasedControlsVmReadScalarDeliveryCanonicalComposition(
            core.CompatibilityControlPolicyOwner!, enabled: true);
        var scheduler = Scheduler();
        VmxMicroOp carrier = CreateVmRead();
        IssuePacketLane lane = CreateLane(carrier);
        Assert.True(scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            CreatePacket(lane), lane));
        Assert.True(scheduler.TryPreparePinBasedControlsVmReadAfterCanonicalValueRead(
            composition, lane, (ushort)field, core.CurrentVirtualizationRestoreGeneration));
        return new(composition, carrier, Assert.IsType<VmReadScalarResultReceipt>(
            scheduler.LastVmReadScalarDeliveryResult!.Value.Receipt));
    }

    private static Processor.CPU_Core CreateCore(
        CompatibilityEventRoutingPolicy policy,
        bool enabled) => new(
            0,
            CpuCorePlatformContext.CreateFixed(
                Memory(),
                ProcessorMode.Emulation,
                compatibilityControlPolicyProfile:
                    new CompatibilityControlPolicyConstructionProfile(7, policy),
                pinBasedControlsVmReadProfile: enabled
                    ? PinBasedControlsVmReadProductionConstructionProfile.CreateExact(7)
                    : null));

    private static Processor.MultiBankMemoryArea Memory()
    {
        var memory = new Processor.MultiBankMemoryArea(1, 0x1000);
        memory.SetLength(0x1000);
        return memory;
    }

    private static MicroOpScheduler Scheduler()
    {
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(Replay());
        return scheduler;
    }

    private static VmxMicroOp CreateVmRead()
    {
        var vmx = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMREAD,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 3,
            Rd = 3,
            Rs1 = 1,
            Rs2 = 0,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMREAD),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = 3,
                Rs1 = 1,
                Rs2 = 0,
                Imm = 0,
            },
        };
        vmx.Placement = vmx.Placement with { DomainTag = 7 };
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

    private sealed record Prepared(
        PinBasedControlsVmReadScalarDeliveryCanonicalComposition Composition,
        VmxMicroOp Carrier,
        VmReadScalarResultReceipt Receipt);
}
