using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Legality;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03AdmissionCoexistenceTailTests
{
    [Fact]
    public void CanonicalDecodeTransportFacts_RetainLegalityBackedDependencySummary_UntilCarrierRepublish()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x1000);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x1000, bundleSerial: 41);

        BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleDependencySummary canonicalDependencySummary =
            RequireDependencySummary(legalityDescriptor.DependencySummary);

        DecodedBundleTransportFacts beforeTransportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        RuntimeClusterAdmissionHandoff beforeHandoff = BuildAdmissionHandoff(beforeTransportFacts);
        DecodedBundleDependencySummary beforeDependencySummary =
            RequireDependencySummary(beforeHandoff.DependencySummary);

        Assert.Equal(canonicalDependencySummary.AggregateResourceMask, beforeDependencySummary.AggregateResourceMask);
        Assert.NotEqual(ResourceBitset.Zero, beforeDependencySummary.AggregateResourceMask);

        AtomicMicroOp microOp = Assert.IsType<AtomicMicroOp>(beforeTransportFacts.Slots[0].MicroOp);
        microOp.ResourceMask = ResourceBitset.Zero;
        microOp.OriginalResourceMask = ResourceBitset.Zero;

        DecodedBundleTransportFacts cachedTransportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        RuntimeClusterAdmissionHandoff cachedHandoff = BuildAdmissionHandoff(cachedTransportFacts);
        DecodedBundleDependencySummary cachedDependencySummary =
            RequireDependencySummary(cachedHandoff.DependencySummary);

        Assert.Equal(canonicalDependencySummary.AggregateResourceMask, cachedDependencySummary.AggregateResourceMask);

        core.TestSetDecodedBundle(cachedTransportFacts.Slots[0].MicroOp);

        DecodedBundleTransportFacts rebuiltTransportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        RuntimeClusterAdmissionHandoff rebuiltHandoff = BuildAdmissionHandoff(rebuiltTransportFacts);
        DecodedBundleDependencySummary rebuiltDependencySummary =
            RequireDependencySummary(rebuiltHandoff.DependencySummary);

        Assert.NotEqual(canonicalDependencySummary.AggregateResourceMask, rebuiltDependencySummary.AggregateResourceMask);
        Assert.Equal(ResourceBitset.Zero, rebuiltDependencySummary.AggregateResourceMask);
    }

    private static RuntimeClusterAdmissionHandoff BuildAdmissionHandoff(
        in DecodedBundleTransportFacts transportFacts)
    {
        ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
            transportFacts.PC,
            transportFacts.Slots,
            transportFacts.AdmissionPrep,
            transportFacts.DependencySummary);
        RuntimeClusterAdmissionPreparation runtimePreparation =
            RuntimeClusterAdmissionPreparation.Create(clusterPreparation);
        RuntimeClusterAdmissionCandidateView candidateView =
            RuntimeClusterAdmissionCandidateView.Create(
                transportFacts.PC,
                transportFacts.Slots,
                clusterPreparation,
                runtimePreparation);
        RuntimeClusterAdmissionDecisionDraft decisionDraft =
            RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
                candidateView,
                clusterPreparedModeEnabled: true);

        return RuntimeClusterAdmissionHandoff.Create(
            transportFacts.PC,
            transportFacts.Slots,
            clusterPreparation,
            candidateView,
            decisionDraft);
    }

    private static DecodedBundleDependencySummary RequireDependencySummary(
        DecodedBundleDependencySummary? dependencySummary)
    {
        Assert.True(dependencySummary.HasValue);
        return dependencySummary.Value;
    }

    private static VLIW_Instruction[] CreateBundle(
        VLIW_Instruction slot0)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }
}

