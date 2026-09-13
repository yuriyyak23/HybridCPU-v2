using System;
using System.IO;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf06ScalarSchedulerRoutingTests
{
    [Theory]
    [InlineData(IsaOpcodeValues.ADD)]
    [InlineData(IsaOpcodeValues.SUB)]
    [InlineData(IsaOpcodeValues.AND)]
    [InlineData(IsaOpcodeValues.OR)]
    [InlineData(IsaOpcodeValues.XOR)]
    public void ScalarFamily_UsesExistingSchedulerStageAAndStageB(ushort opcode)
    {
        (CanonicalDecodedInstruction canonical, CanonicalBundle bundle) = Decode(opcode);
        MicroOpScheduler scheduler = new() { TypedSlotEnabled = true };
        OperationAttemptIssuer issuer = new();

        Rf06ScalarRoutingResult result = Rf06ScalarSchedulerRouting.Route(
            scheduler,
            canonical,
            CreateProvenance(bundle, canonical, virtualThreadId: 1),
            ownerContextId: 0,
            new MicroOp?[BundleMetadata.BundleSlotCount],
            ownerVirtualThreadId: 0,
            localCoreId: 0,
            eligibleVirtualThreadMask: 0b_1111,
            workingBundleSequence: 77,
            issuer);

        Assert.NotNull(result.ScheduledOperation);
        ScheduledOperation scheduled = result.ScheduledOperation!;
        Assert.True(result.IsScheduled);
        Assert.Equal(Rf06ScalarRoutingRejectReason.None, result.RejectReason);
        Assert.NotNull(result.Admission);
        Assert.Equal(1, scheduled.OperationId.VirtualThreadId);
        Assert.Equal(77UL, scheduled.OperationId.WorkingBundleSequence);
        Assert.Equal(canonical.SlotIndex, scheduled.OperationId.WorkingSlotIndex);
        Assert.InRange(scheduled.PhysicalLane, 0, 3);
        Assert.Same(result.Admission, scheduled.Admission);
    }

    [Fact]
    public void StageAReject_DoesNotCreateOperationId()
    {
        (CanonicalDecodedInstruction canonical, CanonicalBundle bundle) = Decode(IsaOpcodeValues.ADD);
        MicroOpScheduler scheduler = new() { TypedSlotEnabled = false };

        Rf06ScalarRoutingResult result = Rf06ScalarSchedulerRouting.Route(
            scheduler,
            canonical,
            CreateProvenance(bundle, canonical, virtualThreadId: 1),
            ownerContextId: 17,
            new MicroOp?[BundleMetadata.BundleSlotCount],
            ownerVirtualThreadId: 0,
            localCoreId: 0,
            eligibleVirtualThreadMask: 0b_1111,
            workingBundleSequence: 77,
            new OperationAttemptIssuer());

        Assert.Equal(Rf06ScalarRoutingRejectReason.TypedSchedulerPathDisabled, result.RejectReason);
        Assert.NotNull(result.Admission);
        Assert.Null(result.ScheduledOperation);
    }

    [Fact]
    public void InvalidDirectCanonicalOperandsMapToNotScalarFamilyBeforeAdmission()
    {
        (CanonicalDecodedInstruction canonical, CanonicalBundle bundle) = Decode(IsaOpcodeValues.ADD);

        foreach (CanonicalDecodedInstruction invalid in new[]
                 {
                     canonical with { Rd = 32 },
                     canonical with { Rs1 = 32 },
                     canonical with { Rs2 = 32 },
                     canonical with { Rd = VLIW_Instruction.NoArchReg },
                     canonical with { Rs1 = VLIW_Instruction.NoArchReg },
                     canonical with { Rs2 = VLIW_Instruction.NoArchReg },
                 })
        {
            Rf06ScalarRoutingResult result = Rf06ScalarSchedulerRouting.Route(
                new MicroOpScheduler { TypedSlotEnabled = true },
                invalid,
                CreateProvenance(bundle, invalid, virtualThreadId: 1),
                ownerContextId: 17,
                new MicroOp?[BundleMetadata.BundleSlotCount],
                ownerVirtualThreadId: 0,
                localCoreId: 0,
                eligibleVirtualThreadMask: 0b_1111,
                workingBundleSequence: 77,
                new OperationAttemptIssuer());

            Assert.Equal(Rf06ScalarRoutingRejectReason.NotScalarFamily, result.RejectReason);
            Assert.Null(result.Admission);
            Assert.Null(result.Carrier);
            Assert.Null(result.ScheduledOperation);
            Assert.False(result.IsScheduled);
        }
    }

    [Fact]
    public void StageAEligibilityReject_DoesNotCreateOperationId()
    {
        (CanonicalDecodedInstruction canonical, CanonicalBundle bundle) = Decode(IsaOpcodeValues.ADD);
        MicroOpScheduler scheduler = new() { TypedSlotEnabled = true };

        Rf06ScalarRoutingResult result = Rf06ScalarSchedulerRouting.Route(
            scheduler,
            canonical,
            CreateProvenance(bundle, canonical, virtualThreadId: 1),
            ownerContextId: 17,
            new MicroOp?[BundleMetadata.BundleSlotCount],
            ownerVirtualThreadId: 0,
            localCoreId: 0,
            eligibleVirtualThreadMask: 0b_0001,
            workingBundleSequence: 77,
            new OperationAttemptIssuer());

        Assert.Equal(Rf06ScalarRoutingRejectReason.StageAEligibility, result.RejectReason);
        Assert.NotNull(result.Admission);
        Assert.Null(result.ScheduledOperation);
    }

    [Fact]
    public void StageBNoLane_DoesNotCreateOperationId()
    {
        (CanonicalDecodedInstruction canonical, CanonicalBundle bundle) = Decode(IsaOpcodeValues.ADD);
        MicroOpScheduler scheduler = new() { TypedSlotEnabled = true };
        var workingBundle = new MicroOp?[BundleMetadata.BundleSlotCount];
        for (int lane = 0; lane < 4; lane++)
            workingBundle[lane] = new NopMicroOp();

        Rf06ScalarRoutingResult result = Rf06ScalarSchedulerRouting.Route(
            scheduler,
            canonical,
            CreateProvenance(bundle, canonical, virtualThreadId: 1),
            ownerContextId: 0,
            workingBundle,
            ownerVirtualThreadId: 0,
            localCoreId: 0,
            eligibleVirtualThreadMask: 0b_1111,
            workingBundleSequence: 77,
            new OperationAttemptIssuer());

        Assert.Equal(Rf06ScalarRoutingRejectReason.StageBNoLane, result.RejectReason);
        Assert.NotNull(result.Admission);
        Assert.Null(result.ScheduledOperation);
    }

    [Fact]
    public void ReplayAdmission_ReceivesFreshOperationId()
    {
        (CanonicalDecodedInstruction canonical, CanonicalBundle bundle) = Decode(IsaOpcodeValues.ADD);
        OperationAttemptIssuer issuer = new();

        Rf06ScalarRoutingResult first = RouteOnce(canonical, bundle, issuer, 101);
        Rf06ScalarRoutingResult replay = RouteOnce(canonical, bundle, issuer, 101);

        Assert.NotNull(first.ScheduledOperation);
        Assert.NotNull(replay.ScheduledOperation);
        VliwOperationId firstId = first.ScheduledOperation!.OperationId;
        VliwOperationId replayId = replay.ScheduledOperation!.OperationId;
        Assert.Equal(firstId.VirtualThreadId, replayId.VirtualThreadId);
        Assert.Equal(firstId.WorkingBundleSequence, replayId.WorkingBundleSequence);
        Assert.Equal(firstId.WorkingSlotIndex, replayId.WorkingSlotIndex);
        Assert.NotEqual(firstId.OperationAttempt, replayId.OperationAttempt);
    }

    [Fact]
    public void SubstituteCarrier_WithEquivalentContract_UsesSameSchedulerPath()
    {
        (CanonicalDecodedInstruction canonical, CanonicalBundle bundle) = Decode(IsaOpcodeValues.ADD);
        ExecutionContract contract = Rf06ScalarLegacyProjection.CreateContract(canonical);
        CheckedScalarLegacyProjection original = Rf06ScalarLegacyProjection.Project(canonical, contract);
        SourceOperationProvenance provenance = CreateProvenance(bundle, canonical, virtualThreadId: 1);
        AdmissionRecord admission = AdmissionRecord.Create(provenance, contract, 1, 17, 0);

        ScalarALUMicroOp substitute = new()
        {
            OpCode = original.Carrier.OpCode,
            DestRegID = original.Carrier.DestRegID,
            Src1RegID = original.Carrier.Src1RegID,
            Src2RegID = original.Carrier.Src2RegID,
            UsesImmediate = false,
            WritesRegister = true,
        };
        substitute.InitializeMetadata();

        Rf06ScalarRoutingResult result = Rf06ScalarSchedulerRouting.Route(
            new MicroOpScheduler { TypedSlotEnabled = true },
            admission,
            substitute,
            new MicroOp?[BundleMetadata.BundleSlotCount],
            ownerVirtualThreadId: 0,
            localCoreId: 0,
            eligibleVirtualThreadMask: 0b_1111,
            workingBundleSequence: 77,
            workingSlotIndex: canonical.SlotIndex,
            new OperationAttemptIssuer());

        Assert.True(result.IsScheduled);
        Assert.Same(substitute, result.Carrier);
        Assert.NotNull(result.ScheduledOperation);
    }

    [Fact]
    public void RoutingAdapter_DoesNotCreateSecondSchedulerOrRegistryLookup()
    {
        DirectoryInfo root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root.FullName,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "Scheduling",
            "Rf06ScalarSchedulerRouting.cs"));

        Assert.DoesNotContain("new MicroOpScheduler", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateMicroOp(", source, StringComparison.Ordinal);
        Assert.Contains("NominateSmtCandidate", source, StringComparison.Ordinal);
        Assert.Contains("PackBundleIntraCoreSmt", source, StringComparison.Ordinal);
        Assert.Contains("CreateAfterStageB", source, StringComparison.Ordinal);
    }

    private static Rf06ScalarRoutingResult RouteOnce(
        CanonicalDecodedInstruction canonical,
        CanonicalBundle bundle,
        OperationAttemptIssuer issuer,
        ulong sequence)
    {
        return Rf06ScalarSchedulerRouting.Route(
            new MicroOpScheduler { TypedSlotEnabled = true },
            canonical,
            CreateProvenance(bundle, canonical, virtualThreadId: 1),
            ownerContextId: 17,
            new MicroOp?[BundleMetadata.BundleSlotCount],
            ownerVirtualThreadId: 0,
            localCoreId: 0,
            eligibleVirtualThreadMask: 0b_1111,
            sequence,
            issuer);
    }

    private static SourceOperationProvenance CreateProvenance(
        CanonicalBundle bundle,
        CanonicalDecodedInstruction canonical,
        int virtualThreadId) =>
        new(
            bundle.SemanticKey,
            virtualThreadId,
            bundle.BundleSerial,
            SlotId.Create(canonical.SlotIndex),
            fetchEpoch: 1);

    private static (CanonicalDecodedInstruction Canonical, CanonicalBundle Bundle) Decode(ushort opcode)
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction
        {
            OpCode = opcode,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(3, 1, 2),
        };

        var decoded = new VliwDecoderV4().DecodeInstructionBundle(raw, 0x4000, 9);
        CanonicalBundle bundle = Assert.IsType<CanonicalBundle>(decoded.CanonicalBundle);
        return (bundle.GetSlot(0), bundle);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current;
            }
        }

        throw new DirectoryNotFoundException("HybridCPU ISE repository root was not found.");
    }
}
