using System;
using System.Linq;
using System.Reflection;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToHSLVdeinterleave = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VdeinterleaveInstruction;
using CloseToHSLVgatherIndexed2D = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Indexed2D.VgatherIndexed2DContour;
using CloseToHSLVinterleave = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VinterleaveInstruction;
using CloseToHSLVldseg2 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vldseg2Instruction;
using CloseToHSLVldseg4 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vldseg4Instruction;
using CloseToHSLVldseg8 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vldseg8Instruction;
using CloseToHSLVload2D = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Shapes2D.Vload2DContour;
using CloseToHSLVscatterIndexed2D = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Indexed2D.VscatterIndexed2DContour;
using CloseToHSLVstseg2 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vstseg2Instruction;
using CloseToHSLVstseg4 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vstseg4Instruction;
using CloseToHSLVstseg8 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vstseg8Instruction;
using CloseToHSLVstore2D = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Shapes2D.Vstore2DContour;
using CloseToHSLVunzip = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VunzipInstruction;
using CloseToHSLVzip = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VzipInstruction;

namespace HybridCPU_ISE.Tests;

public sealed class VectorSegmentStructureMemoryFailClosedTests
{
    private static readonly string[] ReservedPhase07Mnemonics =
    [
        "VLDSEG2", "VLDSEG4", "VLDSEG8",
        "VSTSEG2", "VSTSEG4", "VSTSEG8",
        "VZIP", "VUNZIP", "VINTERLEAVE", "VDEINTERLEAVE"
    ];

    [Fact]
    public void SegmentAndStructureRows_RemainReservedNoAllocation()
    {
        foreach (string mnemonic in ReservedPhase07Mnemonics)
        {
            Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(
                mnemonic,
                out InstructionSupportStatus status));
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.Equal("VectorScanSegmentMovement", status.ExtensionName);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(status.IsExecutableClaim);
            Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
            Assert.DoesNotContain(mnemonic, IsaV4Surface.PipelineClassMap.Keys);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic));
        }
    }

    [Theory]
    [InlineData(typeof(CloseToHSLVzip), "VZIP")]
    [InlineData(typeof(CloseToHSLVunzip), "VUNZIP")]
    [InlineData(typeof(CloseToHSLVinterleave), "VINTERLEAVE")]
    [InlineData(typeof(CloseToHSLVdeinterleave), "VDEINTERLEAVE")]
    public void StructureMovementRows_RecordPhase07ANegativeDecisionGate(
        Type templateType,
        string mnemonic)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorStructureMovementFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Lanes00_03Vector", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresStructureShapeAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresShapeOrderingPolicy"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskTailPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedVectorMicroOp"));
        Assert.Contains("element order", GetConstant<string>(templateType, "ShapeContract"), StringComparison.Ordinal);
        Assert.Contains("staged vector publication", GetConstant<string>(templateType, "PublicationPolicy"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(CloseToHSLVldseg2), "VLDSEG2", 2, true)]
    [InlineData(typeof(CloseToHSLVldseg4), "VLDSEG4", 4, true)]
    [InlineData(typeof(CloseToHSLVldseg8), "VLDSEG8", 8, true)]
    [InlineData(typeof(CloseToHSLVstseg2), "VSTSEG2", 2, false)]
    [InlineData(typeof(CloseToHSLVstseg4), "VSTSEG4", 4, false)]
    [InlineData(typeof(CloseToHSLVstseg8), "VSTSEG8", 8, false)]
    public void SegmentRows_RecordPhase07BNegativeDecisionGate(
        Type templateType,
        string mnemonic,
        int segmentCount,
        bool expectsLoad)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorSegmentMemoryFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Lanes04_05Memory", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal(segmentCount, GetConstant<int>(templateType, "SegmentCount"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresMemoryShapeAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresFaultReplayPolicy"));
        Assert.True(GetConstant<bool>(templateType, "RequiresSegmentShapeAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresByteOrderingPolicy"));
        Assert.True(GetConstant<bool>(templateType, "RequiresAlignmentFaultPolicy"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskTailPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedVectorMemoryMicroOp"));
        Assert.Contains("segment memory shape sideband", GetConstant<string>(templateType, "ShapeContract"), StringComparison.Ordinal);

        if (expectsLoad)
        {
            Assert.True(GetConstant<bool>(templateType, "IsSegmentLoad"));
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"));
            Assert.Contains("load deinterleaving", GetConstant<string>(templateType, "SegmentOrderingPolicy"), StringComparison.Ordinal);
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "IsSegmentStore"));
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedCommit"));
            Assert.Contains("store interleaving", GetConstant<string>(templateType, "SegmentOrderingPolicy"), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToHSLVload2D), "VLOAD", "2D", false, true)]
    [InlineData(typeof(CloseToHSLVstore2D), "VSTORE", "2D", false, false)]
    [InlineData(typeof(CloseToHSLVgatherIndexed2D), "VGATHER", "Indexed2D", true, true)]
    [InlineData(typeof(CloseToHSLVscatterIndexed2D), "VSCATTER", "Indexed2D", true, false)]
    public void MemoryContourRows_RecordPhase07CNegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string contour,
        bool expectsIndexed,
        bool expectsLoadLikePublication)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal(contour, GetConstant<string>(templateType, "Contour"));
        Assert.Equal("VectorMemoryContourFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Lanes04_05Memory", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresMemoryShapeAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresFaultReplayPolicy"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRowColumnStrideAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresAddressBoundsPolicy"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskTailPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedVectorMemoryMicroOp"));
        Assert.Contains("does not authorize", GetConstant<string>(templateType, "AddressingContourPolicy"), StringComparison.Ordinal);

        if (expectsIndexed)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresIndexed2DShapeSideband"));
            Assert.True(GetConstant<bool>(templateType, "RequiresIndexSurfaceAbi"));
            Assert.Contains("indexed+2D", GetConstant<string>(templateType, "ShapeContract"), StringComparison.Ordinal);
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "Requires2DShapeSideband"));
            Assert.Contains("2D memory shape sideband", GetConstant<string>(templateType, "ShapeContract"), StringComparison.Ordinal);
        }

        if (expectsLoadLikePublication)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"));
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedCommit"));
        }
    }

    [Fact]
    public void ClosedOneDimensionalMemoryEvidence_DoesNotOpenPhase07Contours()
    {
        VectorLegalityMatrixRow transfer = VectorLegalityMatrix.GetRow(InstructionsEnum.VLOAD);
        Assert.Equal("VectorTransferCarrier", transfer.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, transfer.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, transfer.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VLOAD, indexed: false, is2D: true));
        Assert.Equal(VectorContourLegalityStatus.FailClosed,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VSTORE, indexed: false, is2D: true));

        VectorLegalityMatrixRow gather = VectorLegalityMatrix.GetRow(InstructionsEnum.VGATHER);
        Assert.Equal("VectorIndexedGatherMemory", gather.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, gather.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, gather.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VGATHER, indexed: true, is2D: true));

        VectorLegalityMatrixRow scatter = VectorLegalityMatrix.GetRow(InstructionsEnum.VSCATTER);
        Assert.Equal("VectorIndexedScatterMemory", scatter.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, scatter.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, scatter.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VSCATTER, indexed: true, is2D: true));

        Assert.False(VectorLegalityMatrix.AllowsAddressingExecution(InstructionsEnum.VLOAD, indexed: false, is2D: true));
        Assert.False(VectorLegalityMatrix.AllowsAddressingExecution(InstructionsEnum.VSTORE, indexed: false, is2D: true));
        Assert.False(VectorLegalityMatrix.AllowsAddressingExecution(InstructionsEnum.VGATHER, indexed: true, is2D: true));
        Assert.False(VectorLegalityMatrix.AllowsAddressingExecution(InstructionsEnum.VSCATTER, indexed: true, is2D: true));
    }

    private static void AssertCommonFailClosedMarkers(Type templateType)
    {
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"));
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackConformance"));
        Assert.True(GetConstant<bool>(templateType, "RequiresGoldenArtifacts"));
        Assert.True(GetConstant<bool>(templateType, "NoBaseOpcodeDuplication"));
        Assert.True(GetConstant<bool>(templateType, "NoDescriptorFallback"));
        Assert.True(GetConstant<bool>(templateType, "NoHiddenStreamEngineFallback"));
        Assert.True(GetConstant<bool>(templateType, "NoHiddenDmaFallback"));
        Assert.True(GetConstant<bool>(templateType, "NoMultiOpEmission"));
        Assert.True(GetConstant<bool>(templateType, "NoVmxSpecificPath"));
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"));
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"));
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    private static bool HasEnumOrRegistryMnemonic(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        bool hasEnum = Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
        bool hasRegistryMnemonic = OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));

        return hasEnum || hasRegistryMnemonic;
    }

    private static T GetConstant<T>(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.FullName}.{name} was not found.");
        Assert.True(field.IsLiteral, $"{type.FullName}.{name} must remain a const template marker.");
        return Assert.IsType<T>(field.GetRawConstantValue());
    }
}
