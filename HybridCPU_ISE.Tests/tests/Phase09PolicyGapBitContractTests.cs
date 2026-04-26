using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.CompilerTests;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

[CollectionDefinition("Phase09 Compat Ingress Strict Gate", DisableParallelization = true)]
public sealed class Phase09CompatIngressStrictGateCollection;

[Collection("Phase09 Compat Ingress Strict Gate")]
public sealed class Phase09PolicyGapBitContractTests
{
    [Fact]
    public void PolicyGapBitZero_CompatBundleLoadSanitizesRetiredBitButPreservesRemainingWord3State()
    {
        VLIW_Instruction legacyInstruction = CreateCompatInstruction(vtHint: 3);
        legacyInstruction.Word3 |= 1UL << 50;

        byte[] rawBundleBytes = new byte[256];
        Assert.True(legacyInstruction.TryWriteBytes(rawBundleBytes.AsSpan(0, 32)));

        Assert.True(VliwCompatIngressTestAdapter.TryReadBundleForCompatIngress(rawBundleBytes, out VLIW_Bundle bundle));

        VLIW_Instruction loadedInstruction = bundle.GetInstruction(0);
        Assert.Equal(legacyInstruction.Word3 & ~(1UL << 50), loadedInstruction.Word3);
        Assert.Equal(legacyInstruction.VirtualThreadId, loadedInstruction.VirtualThreadId);
        Assert.Equal(legacyInstruction.OpCode, loadedInstruction.OpCode);
    }

    [Fact]
    public void PolicyGapBitZero_CompilerBundleIngressDoesNotRoundTripRetiredBit()
    {
        VLIW_Instruction legacyInstruction = CreateCompatInstruction(vtHint: 2);
        legacyInstruction.Word3 |= 1UL << 50;

        IrProgram program = BuildProgram(legacyInstruction);
        IrInstruction irInstruction = Assert.Single(program.Instructions);
        IrMaterializedBundle materializedBundle = CreateManualBundle((irInstruction, 0, IrIssueSlotMask.Slot0));

        VLIW_Bundle loweredBundle = new HybridCpuBundleLowerer().LowerBundle(materializedBundle);
        VLIW_Instruction loweredInstruction = loweredBundle.GetInstruction(0);

        Assert.Equal(0UL, loweredInstruction.Word3 & (1UL << 50));
        Assert.Equal((byte)2, loweredInstruction.VirtualThreadId);
        Assert.Equal(legacyInstruction.StreamLength, loweredInstruction.StreamLength);
        Assert.Equal(legacyInstruction.Stride, loweredInstruction.Stride);
        Assert.Equal(legacyInstruction.RowStride, loweredInstruction.RowStride);
    }

    [Fact]
    public void PolicyGapBitZero_DirectCompatDecodeRejectsRetiredBitWithCanonicalMessage()
    {
        VLIW_Instruction legacyInstruction = CreateCompatInstruction(vtHint: 1);
        legacyInstruction.Word3 |= 1UL << 50;

        var decoder = new VliwDecoderV4();
        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => decoder.Decode(in legacyInstruction, slotIndex: 0));

        Assert.Equal(
            "Instruction at slot 0 has word3[50] set. This retired legacy scheduling-policy bit must be zero in production decoded streams.",
            ex.Message);
    }

    [Fact]
    public void PolicyGapBitZero_CompatContractDocsFreezeTransportHintWording()
    {
        string repoRoot = FindRepoRoot();
        string compatLayoutPath = Path.Combine(repoRoot, "HybridCPU_ISE", "Arch", "Compat", "VLIW_Instruction.Layout.cs");
        string compilerContractPath = Path.Combine(repoRoot, "HybridCPU_ISE", "Core", "Contracts", "CompilerContract.cs");

        string compatLayout = File.ReadAllText(compatLayoutPath);
        string compilerContract = File.ReadAllText(compilerContractPath);

        Assert.Contains("transport hint only", compatLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("Binds this instruction to a specific SMT context", compatLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("MetadataUnpacker", compilerContract, StringComparison.Ordinal);
    }

    [Fact]
    public void PolicyGapBitSanitizedCount_Increments_WhenCompatIngressRepairsRetiredBit()
    {
        VliwCompatIngressTestAdapter.ResetTelemetryForTesting();

        try
        {
            VLIW_Instruction legacyInstruction = CreateCompatInstruction(vtHint: 3);
            legacyInstruction.Word3 |= 1UL << 50;

            byte[] rawBundleBytes = new byte[256];
            Assert.True(legacyInstruction.TryWriteBytes(rawBundleBytes.AsSpan(0, 32)));

            Assert.True(VliwCompatIngressTestAdapter.TryReadBundleForCompatIngress(rawBundleBytes, out _));

            Assert.Equal(1UL, VliwCompatIngressTestAdapter.PolicyGapBitSanitizedCount);
        }
        finally
        {
            VliwCompatIngressTestAdapter.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void PolicyGapBitZero_ProductionBundleLoadRejectsRetiredBitWithoutSanitizing()
    {
        VliwCompatIngressTestAdapter.ResetTelemetryForTesting();

        try
        {
            VLIW_Instruction legacyInstruction = CreateCompatInstruction(vtHint: 1);
            legacyInstruction.Word3 |= 1UL << 50;

            byte[] rawBundleBytes = new byte[256];
            Assert.True(legacyInstruction.TryWriteBytes(rawBundleBytes.AsSpan(0, 32)));

            var bundle = new VLIW_Bundle();
            InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
                () => bundle.TryReadBytes(rawBundleBytes));

            Assert.Equal(
                VLIW_Instruction.GetRetiredPolicyGapViolationMessage(),
                ex.Message);
            Assert.Equal(0UL, VliwCompatIngressTestAdapter.PolicyGapBitSanitizedCount);
        }
        finally
        {
            VliwCompatIngressTestAdapter.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void PolicyGapBitZero_ProductionBundleLoadAcceptsCanonicalInstructionWithoutSanitizing()
    {
        VliwCompatIngressTestAdapter.ResetTelemetryForTesting();

        try
        {
            VLIW_Instruction canonicalInstruction = CreateCompatInstruction(vtHint: 1);

            byte[] rawBundleBytes = new byte[256];
            Assert.True(canonicalInstruction.TryWriteBytes(rawBundleBytes.AsSpan(0, 32)));

            var bundle = new VLIW_Bundle();
            Assert.True(bundle.TryReadBytes(rawBundleBytes));

            VLIW_Instruction loadedInstruction = bundle.GetInstruction(0);
            Assert.Equal(canonicalInstruction.Word3, loadedInstruction.Word3);
            Assert.Equal(canonicalInstruction.VirtualThreadId, loadedInstruction.VirtualThreadId);
            Assert.Equal(0UL, VliwCompatIngressTestAdapter.PolicyGapBitSanitizedCount);
        }
        finally
        {
            VliwCompatIngressTestAdapter.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void PolicyGapBitZero_FetchedPipelineDecodePublishesDecodeFaultWithoutCompatSanitizing()
    {
        VliwCompatIngressTestAdapter.ResetTelemetryForTesting();

        try
        {
            var core = new Processor.CPU_Core(0);
            VLIW_Instruction legacyInstruction = CreateCompatInstruction(vtHint: 1);
            legacyInstruction.Word3 |= 1UL << 50;

            byte[] rawBundleBytes = new byte[256];
            Assert.True(legacyInstruction.TryWriteBytes(rawBundleBytes.AsSpan(0, 32)));

            core.TestDecodeFetchedBundleBytes(
                rawBundleBytes,
                pc: 0x2400,
                annotations: CreateUniformOwnerAnnotations(1));

            DecodedBundleRuntimeState runtimeState = core.TestReadCurrentDecodedBundleRuntimeState();

            Assert.Equal(DecodedBundleStateKind.DecodeFault, runtimeState.StateKind);
            Assert.Equal(DecodedBundleStateOrigin.DecodeFallbackTrap, runtimeState.StateOrigin);
            Assert.True(runtimeState.HasDecodeFault);
            Assert.Equal(0x2400UL, runtimeState.BundlePc);
            Assert.Equal(0UL, VliwCompatIngressTestAdapter.PolicyGapBitSanitizedCount);
        }
        finally
        {
            VliwCompatIngressTestAdapter.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void PolicyGapBitZero_ProductionSetInstructionRejectsRetiredBitWithoutSanitizing()
    {
        VliwCompatIngressTestAdapter.ResetTelemetryForTesting();

        try
        {
            VLIW_Instruction legacyInstruction = CreateCompatInstruction(vtHint: 3);
            legacyInstruction.Word3 |= 1UL << 50;

            var bundle = new VLIW_Bundle();
            InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
                () => bundle.SetInstruction(0, legacyInstruction));

            Assert.Equal(
                VLIW_Instruction.GetRetiredPolicyGapViolationMessage(),
                ex.Message);
            Assert.Equal(0UL, VliwCompatIngressTestAdapter.PolicyGapBitSanitizedCount);
        }
        finally
        {
            VliwCompatIngressTestAdapter.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void PolicyGapBitZero_CompatSetInstructionSanitizesRetiredBitAndTracksTelemetry()
    {
        VliwCompatIngressTestAdapter.ResetTelemetryForTesting();

        try
        {
            VLIW_Instruction legacyInstruction = CreateCompatInstruction(vtHint: 2);
            legacyInstruction.Word3 |= 1UL << 50;

            var bundle = new VLIW_Bundle();
            VliwCompatIngressTestAdapter.SetInstructionForCompatIngress(ref bundle, 0, legacyInstruction);

            VLIW_Instruction loadedInstruction = bundle.GetInstruction(0);
            Assert.Equal(legacyInstruction.Word3 & ~(1UL << 50), loadedInstruction.Word3);
            Assert.Equal((byte)2, loadedInstruction.VirtualThreadId);
            Assert.Equal(1UL, VliwCompatIngressTestAdapter.PolicyGapBitSanitizedCount);
        }
        finally
        {
            VliwCompatIngressTestAdapter.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void PolicyGapBitZero_CompatRepairApis_AreQuarantinedBehindExplicitAdapter()
    {
        Assert.Null(typeof(VLIW_Bundle).GetMethod(
            "TryReadBytesCompat",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic));
        Assert.Null(typeof(VLIW_Bundle).GetMethod(
            "SetInstructionCompat",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic));
        Assert.Null(typeof(VLIW_Instruction).GetMethod(
            "TryReadBytesCompat",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic));
        Assert.Null(typeof(VLIW_Instruction).GetMethod(
            "SanitizeRetiredPolicyGapBitForCompatIngress",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic));

        Type? runtimeAdapterType = typeof(VLIW_Instruction).Assembly.GetType(
            "HybridCPU_ISE.Arch.Compat.VliwCompatIngress",
            throwOnError: false);
        Assert.Null(runtimeAdapterType);

        Type adapterType = typeof(VliwCompatIngressTestAdapter);
        Assert.NotNull(adapterType.GetMethod(
            "TryReadBundleForCompatIngress",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic));
        Assert.NotNull(adapterType.GetMethod(
            "SetInstructionForCompatIngress",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic));
    }

    private static IrProgram BuildProgram(params VLIW_Instruction[] instructions)
    {
        _ = new Processor(ProcessorMode.Compiler);
        var builder = new HybridCpuIrBuilder();
        return builder.BuildProgram(
            0,
            instructions,
            bundleAnnotations: LegacyInstructionAnnotationBuilder.Build(instructions));
    }

    private static IrMaterializedBundle CreateManualBundle(
        params (IrInstruction Instruction, int SlotIndex, IrIssueSlotMask LegalSlots)[] placements)
    {
        IReadOnlyList<IrInstruction> instructions = placements.Select(static placement => placement.Instruction).ToArray();
        IrCandidateBundleAnalysis legalityAnalysis = new HybridCpuInstructionLegalityChecker().AnalyzeCandidateBundle(instructions);
        IrIssueSlotMask combinedLegalSlots = placements.Aggregate(
            IrIssueSlotMask.None,
            static (current, placement) => current | placement.LegalSlots);
        IReadOnlyList<IrIssueSlotMask> legalSlotMasks = placements.Select(static placement => placement.LegalSlots).ToArray();
        IReadOnlyList<int> instructionSlots = placements.Select(static placement => placement.SlotIndex).ToArray();
        var slotAssignment = new IrMaterializedSlotAssignment(
            new IrSlotAssignmentAnalysis(
                CandidateInstructionCount: placements.Length,
                CombinedLegalSlots: combinedLegalSlots,
                DistinctLegalSlotCount: BitOperations.PopCount((uint)combinedLegalSlots),
                HasLegalAssignment: true,
                InstructionLegalSlots: legalSlotMasks),
            InstructionSlots: instructionSlots,
            Quality: IrBundlePlacementQuality.Create(instructionSlots, legalSlotMasks, slotCount: 8),
            SearchSummary: IrBundlePlacementSearchSummary.Empty,
            TransitionQuality: IrBundleTransitionQuality.Empty);

        var slots = new IrMaterializedBundleSlot[8];
        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            int placementIndex = Array.FindIndex(placements, placement => placement.SlotIndex == slotIndex);
            if (placementIndex < 0)
            {
                slots[slotIndex] = new IrMaterializedBundleSlot(slotIndex, null, null, IrIssueSlotMask.None, EmptyReason: "nop");
                continue;
            }

            (IrInstruction Instruction, int SlotIndex, IrIssueSlotMask LegalSlots) match = placements[placementIndex];
            slots[slotIndex] = new IrMaterializedBundleSlot(
                slotIndex,
                match.Instruction,
                OrderInCycle: placementIndex,
                InstructionLegalSlots: match.LegalSlots,
                BindingKind: match.Instruction.Annotation.BindingKind,
                AssignedClass: match.Instruction.Annotation.RequiredSlotClass);
        }

        return new IrMaterializedBundle(
            cycle: 0,
            cycleGroup: new IrScheduleCycleGroup(0, instructions, legalityAnalysis),
            legalityAnalysis,
            slotAssignment,
            slots);
    }

    private static VLIW_Instruction CreateCompatInstruction(byte vtHint)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.ADDI,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            Immediate = 7,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 3),
            Src2Pointer = 1,
            StreamLength = 4,
            Stride = 2,
            RowStride = 16,
            VirtualThreadId = vtHint
        };
    }

    private static VliwBundleAnnotations CreateUniformOwnerAnnotations(byte vtId)
    {
        var slotMetadata = new InstructionSlotMetadata[8];
        for (int slotIndex = 0; slotIndex < slotMetadata.Length; slotIndex++)
        {
            slotMetadata[slotIndex] = new InstructionSlotMetadata(
                YAKSys_Hybrid_CPU.Core.Registers.VtId.Create(vtId),
                SlotMetadata.Default);
        }

        return new VliwBundleAnnotations(slotMetadata);
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

        throw new DirectoryNotFoundException("Could not locate HybridCPU ISE repository root from test output directory.");
    }
}
