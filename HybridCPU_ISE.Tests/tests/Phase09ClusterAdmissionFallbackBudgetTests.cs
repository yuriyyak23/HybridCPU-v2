using System;
using System.IO;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ClusterAdmissionFallbackBudgetTests
{
    [Fact]
    public void ClusterAdmission_WhenScalarCandidatesAreBlocked_UsesBudgetedReferenceSequentialFallback()
    {
        (
            ClusterIssuePreparation clusterPreparation,
            RuntimeClusterAdmissionPreparation runtimePreparation,
            RuntimeClusterAdmissionCandidateView candidateView,
            RuntimeClusterAdmissionDecisionDraft decision) = CreateBlockedFallbackChain();

        Assert.Equal(DecodeMode.ClusterPreparedMode, clusterPreparation.DecodeMode);
        Assert.True(clusterPreparation.HasScalarClusterCandidate);
        Assert.True(runtimePreparation.ShouldConsiderClusterAdmission);
        Assert.True(candidateView.HasClusterCandidate);
        Assert.Equal((byte)0b0000_0011, candidateView.FallbackDiagnosticsMask);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.ReferenceSequentialFallback, decision.ExecutionMode);
        Assert.Equal(
            RuntimeClusterFallbackReasonMask.ResidualDiagnostics |
            RuntimeClusterFallbackReasonMask.BlockedScalarCandidates,
            decision.FallbackReasonMask);
        Assert.True(decision.ShouldProbeClusterPath);
        Assert.False(decision.UsesIssuePacketAsExecutionSource);
    }

    [Fact]
    public void CreateExecutable_WhenReferenceSequentialFallbackHasNoDerivedReasonMask_Throws()
    {
        RuntimeClusterAdmissionCandidateView candidateView = new(
            pc: 0x1200UL,
            decodeMode: DecodeMode.ClusterPreparedMode,
            validNonEmptyMask: 0b0000_0001,
            scalarCandidateMask: 0b0000_0001,
            preparedScalarMask: 0,
            refinedPreparedScalarMask: 0,
            blockedScalarCandidateMask: 0,
            auxiliaryCandidateMask: 0,
            auxiliaryReservationMask: 0,
            fallbackDiagnosticsSnapshot: new ClusterFallbackDiagnosticsSnapshot(0, false),
            registerHazardMask: 0,
            orderingHazardMask: 0,
            advisoryScalarIssueWidth: 0,
            refinedAdvisoryScalarIssueWidth: 0,
            hasDraftCandidate: true,
            shouldConsiderClusterAdmission: true);

        Assert.Throws<InvalidOperationException>(
            () => RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
                candidateView,
                clusterPreparedModeEnabled: true));
    }

    [Fact]
    public void ReferenceSequentialFallbackCount_Increments_WhenBudgetedFallbackChoiceIsRecorded()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        RuntimeClusterAdmissionDecisionDraft decision = CreateBlockedFallbackChain().Decision;
        core.TestRecordExecutableClusterAdmissionChoice(decision);

        PipelineControl control = core.GetPipelineControl();
        Assert.Equal(1UL, control.ReferenceSequentialFallbackCount);
        Assert.Equal(1UL, control.ClusterModeFallbackCount);
        Assert.Equal(1UL, control.WidePathGate6_PreparedMaskZeroCount);
    }

    [Fact]
    public void FetchedWideScalarBundle_WhenDecodeFindsWidePath_LeavesReferenceSequentialFallbackBudgetAtZero()
    {
        const ulong pc = 0x2000UL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: 0);
        core.WriteCommittedPc(0, pc);

        core.TestRunDecodeStageWithFetchedBundle(
            CreateBundle(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 1),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 3, rs1: 4, immediate: 2)),
            pc);

        RuntimeClusterAdmissionDecisionDraft decision = core.GetDecodeStageAdmissionDecisionDraft();
        PipelineControl control = core.GetPipelineControl();

        Assert.Equal(RuntimeClusterAdmissionExecutionMode.ClusterPrepared, decision.ExecutionMode);
        Assert.Equal(0UL, control.ReferenceSequentialFallbackCount);
    }

    [Fact]
    public void RecordExecutableClusterAdmissionChoice_WhenClusterPreparedModeIsDisabled_BudgetsExplicitReferenceSelection()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.SetClusterPreparedMode(false);

        RuntimeClusterAdmissionDecisionDraft decision =
            RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
                CreateClusterCandidateView(preparedScalarMask: 0b0000_0001),
                clusterPreparedModeEnabled: false);

        core.TestRecordExecutableClusterAdmissionChoice(decision);

        PipelineControl control = core.GetPipelineControl();
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.ReferenceSequential, decision.ExecutionMode);
        Assert.Equal(1UL, control.WidePathGate3_ReferenceSequentialCount);
        Assert.Equal(0UL, control.ReferenceSequentialFallbackCount);
        Assert.Equal(0UL, control.ClusterModeFallbackCount);
    }

    [Fact]
    public void PipelineControl_SeparatesExplicitReferenceModeFromFallbackBudgetRate()
    {
        PipelineControl control = new();
        control.Clear();
        control.WidePathGate3_ReferenceSequentialCount = 11;
        control.WidePathSuccessCount = 18;
        control.ClusterModeFallbackCount = 2;
        control.ReferenceSequentialFallbackCount = 2;

        Assert.Equal(20UL, control.GetClusterPreparedOpportunityCount());
        Assert.Equal(0.10, control.GetReferenceSequentialFallbackRate(), 3);
        Assert.True(control.ExceedsReferenceSequentialFallbackRateBudget());
    }

    [Fact]
    public void PipelineControl_DoesNotFlagReferenceFallbackBudgetViolation_AtThresholdBoundary()
    {
        PipelineControl control = new();
        control.Clear();
        control.WidePathGate3_ReferenceSequentialCount = 9;
        control.WidePathSuccessCount = 19;
        control.ClusterModeFallbackCount = 1;
        control.ReferenceSequentialFallbackCount = 1;

        Assert.Equal(20UL, control.GetClusterPreparedOpportunityCount());
        Assert.Equal(
            PipelineControl.ReferenceSequentialFallbackRateBudgetThreshold,
            control.GetReferenceSequentialFallbackRate(),
            3);
        Assert.False(control.ExceedsReferenceSequentialFallbackRateBudget());
    }

    [Fact]
    public void ReferenceSequentialNaming_ReplacesLegacyExecutionIdentifiersInProductionSurface()
    {
        string repoRoot = FindRepoRoot();
        string clusterIssuePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Decoder",
            "ClusterIssuePreparation.cs");
        string executionModePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "RuntimeClusterAdmissionPreparation.ExecutionMode.cs");
        string decisionDraftPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "RuntimeClusterAdmissionPreparation.DecisionDraft.cs");
        string pipelineControlPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.Pipeline.Helpers.PipelineControl.cs");
        string stageFlowPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.StageFlow.cs");

        string clusterIssueText = File.ReadAllText(clusterIssuePath);
        string executionModeText = File.ReadAllText(executionModePath);
        string decisionDraftText = File.ReadAllText(decisionDraftPath);
        string pipelineControlText = File.ReadAllText(pipelineControlPath);
        string stageFlowText = File.ReadAllText(stageFlowPath);

        Assert.Contains("ReferenceSequentialMode", clusterIssueText, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacySequentialMode", clusterIssueText, StringComparison.Ordinal);

        Assert.Contains("ReferenceSequential", executionModeText, StringComparison.Ordinal);
        Assert.Contains("AuxiliaryOnlyReference", executionModeText, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacySequential", executionModeText, StringComparison.Ordinal);
        Assert.DoesNotContain("AuxiliaryOnlyLegacy", executionModeText, StringComparison.Ordinal);

        Assert.Contains("ReferenceSequentialFallback", decisionDraftText, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacySequentialFallback", decisionDraftText, StringComparison.Ordinal);

        Assert.Contains("ReferenceSequentialFallbackCount", pipelineControlText, StringComparison.Ordinal);
        Assert.Contains("WidePathGate3_ReferenceSequentialCount", pipelineControlText, StringComparison.Ordinal);
        Assert.Contains("ReferenceSequentialFallbackRateBudgetThreshold", pipelineControlText, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacySequentialFallbackCount", pipelineControlText, StringComparison.Ordinal);
        Assert.DoesNotContain("WidePathGate3_LegacySequentialCount", pipelineControlText, StringComparison.Ordinal);

        Assert.Contains("WidePathGate3_ReferenceSequentialCount++", stageFlowText, StringComparison.Ordinal);
    }

    private static (
        ClusterIssuePreparation ClusterPreparation,
        RuntimeClusterAdmissionPreparation RuntimePreparation,
        RuntimeClusterAdmissionCandidateView CandidateView,
        RuntimeClusterAdmissionDecisionDraft Decision) CreateBlockedFallbackChain()
    {
        const ulong pc = 0x1100UL;

        DecodedBundleSlotDescriptor[] slots = CreateBlockedScalarCandidateSlots();
        DecodedBundleAdmissionPrep admissionPrep = new(
            scalarCandidateMask: 0b0000_0011,
            wideReadyScalarMask: 0,
            auxiliaryOpMask: 0,
            narrowOnlyMask: 0b0000_0011,
            flags: DecodedBundleAdmissionFlags.SuggestNarrowFallback);

        ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
            pc,
            slots,
            admissionPrep,
            dependencySummary: null);
        RuntimeClusterAdmissionPreparation runtimePreparation =
            RuntimeClusterAdmissionPreparation.Create(clusterPreparation);
        RuntimeClusterAdmissionCandidateView candidateView =
            RuntimeClusterAdmissionCandidateView.Create(
                pc,
                slots,
                clusterPreparation,
                runtimePreparation);
        RuntimeClusterAdmissionDecisionDraft decision =
            RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
                candidateView,
                clusterPreparedModeEnabled: true);

        return (clusterPreparation, runtimePreparation, candidateView, decision);
    }

    private static RuntimeClusterAdmissionCandidateView CreateClusterCandidateView(
        byte preparedScalarMask,
        byte refinedPreparedScalarMask = 0b0000_0001)
    {
        return new RuntimeClusterAdmissionCandidateView(
            pc: 0x1A00UL,
            decodeMode: DecodeMode.ClusterPreparedMode,
            validNonEmptyMask: 0b0000_0001,
            scalarCandidateMask: 0b0000_0001,
            preparedScalarMask: preparedScalarMask,
            refinedPreparedScalarMask: refinedPreparedScalarMask,
            blockedScalarCandidateMask: 0,
            auxiliaryCandidateMask: 0,
            auxiliaryReservationMask: 0,
            fallbackDiagnosticsSnapshot: new ClusterFallbackDiagnosticsSnapshot(0, false),
            registerHazardMask: 0,
            orderingHazardMask: 0,
            advisoryScalarIssueWidth: 1,
            refinedAdvisoryScalarIssueWidth: 1,
            hasDraftCandidate: true,
            shouldConsiderClusterAdmission: true);
    }

    private static DecodedBundleSlotDescriptor[] CreateBlockedScalarCandidateSlots()
    {
        DecodedBundleSlotDescriptor[] slots = new DecodedBundleSlotDescriptor[BundleMetadata.BundleSlotCount];
        slots[0] = DecodedBundleSlotDescriptor.Create(
            0,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));
        slots[1] = DecodedBundleSlotDescriptor.Create(
            1,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 4, src1Reg: 5, src2Reg: 6));

        for (byte slotIndex = 2; slotIndex < BundleMetadata.BundleSlotCount; slotIndex++)
        {
            slots[slotIndex] = DecodedBundleSlotDescriptor.Create(slotIndex, microOp: null);
        }

        return slots;
    }

    private static VLIW_Instruction[] CreateBundle(
        params VLIW_Instruction[] slots)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < slots.Length && slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = slots[slotIndex];
        }

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
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            bool hasRepoLayout =
                Directory.Exists(Path.Combine(current.FullName, "Documentation")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests"));
            if (hasRepoLayout)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate HybridCPU ISE repository root from test output directory.");
    }
}

