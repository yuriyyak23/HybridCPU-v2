using System;
using System.Linq;
using System.Reflection;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlMtileLoad = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileLoadInstruction;
using CloseToRtlMtileMacc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileMaccInstruction;
using CloseToRtlMtileStore = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileStoreInstruction;
using CloseToRtlMtranspose = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtransposeInstruction;
using CloseToRtlVdotAccum = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotAccumInstruction;
using CloseToRtlVdotBlockscale = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotBlockscaleInstruction;
using CloseToRtlVdotWideI16 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotWideI16Instruction;
using CloseToRtlVdotWideI32 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotWideI32Instruction;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09DotMatrixDeferralBoundaryTests
{
    private static readonly string[] AdvancedDotMnemonics =
    [
        "VDOT.BLOCKSCALE",
        "VDOT.ACCUM",
        "VDOT.WIDE.I16",
        "VDOT.WIDE.I32"
    ];

    private static readonly string[] MatrixTileMnemonics =
    [
        "MTILE_LOAD",
        "MTILE_STORE",
        "MTILE_MACC",
        "MTRANSPOSE"
    ];

    [Fact]
    public void Phase09Rows_RemainReservedOrOptionalDisabledWithoutProductionPublication()
    {
        foreach (string mnemonic in AdvancedDotMnemonics)
        {
            Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(
                mnemonic,
                out InstructionSupportStatus status));
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.Equal("VectorDotMatrixDeferred", status.ExtensionName);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(status.IsExecutableClaim);
            Assert.False(HasEnum(mnemonic));
            Assert.False(HasIsaOpcodeValue(mnemonic));
            Assert.False(HasRegistryMnemonic(mnemonic));
            Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
            Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
            Assert.DoesNotContain(mnemonic, IsaV4Surface.PipelineClassMap.Keys);
        }

        foreach (string mnemonic in MatrixTileMnemonics)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.OptionalDisabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.DeclaredOnly, status.RuntimeEvidence);
            Assert.Equal("XMatrix", status.ExtensionName);
            Assert.True(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(status.IsExecutableClaim);
            Assert.True(HasEnum(mnemonic));
            Assert.True(HasIsaOpcodeValue(mnemonic));
            Assert.False(HasRegistryMnemonic(mnemonic));
            Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
            Assert.Contains(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
            Assert.DoesNotContain(mnemonic, IsaV4Surface.PipelineClassMap.Keys);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVdotBlockscale), "VDOT.BLOCKSCALE", true, false, false)]
    [InlineData(typeof(CloseToRtlVdotAccum), "VDOT.ACCUM", false, true, false)]
    [InlineData(typeof(CloseToRtlVdotWideI16), "VDOT.WIDE.I16", false, false, true)]
    [InlineData(typeof(CloseToRtlVdotWideI32), "VDOT.WIDE.I32", false, false, true)]
    public void AdvancedDotLeafMarkers_RecordPhase09NegativeDecisionGate(
        Type templateType,
        string mnemonic,
        bool expectsScaleMetadata,
        bool expectsAccumulatorFootprint,
        bool expectsWiderIntegerContour)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorDotMatrixDeferredNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresDotAbiDecision"));
        Assert.True(GetConstant<bool>(templateType, "RequiresAccumulatorPrecisionAbi"));
        Assert.True(GetConstant<bool>(templateType, "NoHostOwnedEvidencePublication"));

        if (expectsScaleMetadata)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresScaleMetadataAbi"));
            Assert.Contains("scale metadata ABI", GetConstant<string>(templateType, "DotAbiPolicy"), StringComparison.Ordinal);
        }

        if (expectsAccumulatorFootprint)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresAccumulatorResultFootprintAbi"));
            Assert.Contains("result footprint", GetConstant<string>(templateType, "AccumulatorPolicy"), StringComparison.Ordinal);
        }

        if (expectsWiderIntegerContour)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresWiderIntegerContourAbi"));
            Assert.True(GetConstant<bool>(templateType, "SeparateFromScopedVdotWide"));
            Assert.True(GetConstant<bool>(templateType, "NoNameOnlyVdotWideExtension"));
            Assert.Contains("separate from scoped VDOT.WIDE", GetConstant<string>(templateType, "WiderIntegerContourPolicy"), StringComparison.Ordinal);
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresSeparateResultSurfaceAbi"));
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlMtileLoad), "MTILE_LOAD", true, false, false, true)]
    [InlineData(typeof(CloseToRtlMtileStore), "MTILE_STORE", true, false, false, false)]
    [InlineData(typeof(CloseToRtlMtileMacc), "MTILE_MACC", false, true, false, true)]
    [InlineData(typeof(CloseToRtlMtranspose), "MTRANSPOSE", false, false, true, true)]
    public void MatrixTileLeafMarkers_RecordPhase09OptionalDisabledDecisionGate(
        Type templateType,
        string mnemonic,
        bool expectsMemoryShapeFaultModel,
        bool expectsAccumulatorTileAbi,
        bool expectsTransposePolicyAbi,
        bool expectsPublication)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorDotMatrixDeferredNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "OptionalDisabledInIsaV4"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTileExecutionModel"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTileDescriptorAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedTileMicroOp"));
        Assert.True(GetConstant<bool>(templateType, "NoLane6Fallback"));
        Assert.True(GetConstant<bool>(templateType, "NoLane7Fallback"));
        Assert.True(GetConstant<bool>(templateType, "NoExternalBackendFallback"));
        Assert.True(GetConstant<bool>(templateType, "NoHostOwnedEvidencePublication"));

        if (expectsMemoryShapeFaultModel)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresTileMemoryShapeFaultModel"));
        }

        if (expectsAccumulatorTileAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresAccumulatorTileAbi"));
            Assert.Contains("accumulator tile", GetConstant<string>(templateType, "TileExecutionPolicy"), StringComparison.Ordinal);
        }

        if (expectsTransposePolicyAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresTransposeTilePolicyAbi"));
            Assert.Contains("transpose tile policy", GetConstant<string>(templateType, "TransposePolicy"), StringComparison.Ordinal);
        }

        if (expectsPublication)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"));
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedCommit"));
        }
    }

    [Fact]
    public void VectorLegalityMatrix_DoesNotTreatDeferredDotOrMatrixRowsAsExecutable()
    {
        VectorLegalityMatrixRow scopedWide = VectorLegalityMatrix.GetRow(InstructionsEnum.VDOT_WIDE);
        Assert.Equal("VectorDotProductWideScalarFootprint", scopedWide.FamilyName);
        Assert.Equal([InstructionsEnum.VDOT_WIDE], scopedWide.Opcodes);
        Assert.Equal(VectorContourLegalityStatus.Executable, scopedWide.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, scopedWide.DescriptorBacked);
        Assert.Contains("wider integer", scopedWide.RuntimeEvidenceNote, StringComparison.Ordinal);
        Assert.Contains("block-scaled", scopedWide.RuntimeEvidenceNote, StringComparison.Ordinal);
        Assert.Contains("separate-destination", scopedWide.RuntimeEvidenceNote, StringComparison.Ordinal);

        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row =>
            row.FamilyName == "VectorDotMatrixDeferredNoExecution" ||
            row.FamilyName == "VectorDotMatrixDeferred");

        foreach (InstructionsEnum matrixOpcode in new[]
                 { InstructionsEnum.MTILE_LOAD, InstructionsEnum.MTILE_STORE, InstructionsEnum.MTILE_MACC, InstructionsEnum.MTRANSPOSE })
        {
            Assert.False(VectorLegalityMatrix.TryGetRow(matrixOpcode, out _));
        }
    }

    [Fact]
    public void ScopedVdotWideEvidence_DoesNotAuthorizeAdvancedDotNames()
    {
        InstructionSupportStatus scopedWide = InstructionSupportStatusCatalog.GetStatus("VDOT.WIDE");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, scopedWide.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, scopedWide.RuntimeEvidence);
        Assert.True(scopedWide.IsExecutableClaim);

        foreach (string mnemonic in AdvancedDotMnemonics)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.IsExecutableClaim);
            Assert.False(HasEnum(mnemonic));
            Assert.False(HasRegistryMnemonic(mnemonic));
        }
    }

    private static void AssertCommonFailClosedMarkers(Type templateType)
    {
        Assert.Equal("Lanes00_03Vector", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.Equal("Phase09NegativeDecisionGate", GetConstant<string>(templateType, "ProductionDecision"));
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"));
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureRetireReplayEvidence"));
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackConformance"));
        Assert.True(GetConstant<bool>(templateType, "RequiresGoldenArtifacts"));
        Assert.True(GetConstant<bool>(templateType, "NoDecoderEncoderAbiPublication"));
        Assert.True(GetConstant<bool>(templateType, "NoInstructionIrProjectionPublication"));
        Assert.True(GetConstant<bool>(templateType, "NoRegistryMaterializerPublication"));
        Assert.True(GetConstant<bool>(templateType, "NoTypedMicroOpPublication"));
        Assert.True(GetConstant<bool>(templateType, "NoSchedulerLaneBindingPublication"));
        Assert.True(GetConstant<bool>(templateType, "NoExecutionCapturePublication"));
        Assert.True(GetConstant<bool>(templateType, "NoRetireWritebackPublication"));
        Assert.True(GetConstant<bool>(templateType, "NoReplayRollbackPublication"));
        Assert.True(GetConstant<bool>(templateType, "NoCompilerHelperEmission"));
        Assert.True(GetConstant<bool>(templateType, "NoDescriptorFallback"));
        Assert.True(GetConstant<bool>(templateType, "NoDescriptorFallbackWithoutGenericRuntimeOwnership"));
        Assert.True(GetConstant<bool>(templateType, "NoHiddenScalarLowering"));
        Assert.True(GetConstant<bool>(templateType, "NoHiddenVectorLowering"));
        Assert.True(GetConstant<bool>(templateType, "NoMultiOpEmission"));
        Assert.True(GetConstant<bool>(templateType, "NoVmxSpecificPath"));
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"));
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"));
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetField("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    private static bool HasEnum(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        return Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
    }

    private static bool HasIsaOpcodeValue(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        return typeof(Processor.CPU_Core.IsaOpcodeValues).GetField(enumCandidate, BindingFlags.Public | BindingFlags.Static) is not null;
    }

    private static bool HasRegistryMnemonic(string mnemonic) =>
        OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));

    private static T GetConstant<T>(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.FullName}.{name} was not found.");
        Assert.True(field.IsLiteral, $"{type.FullName}.{name} must remain a const template marker.");
        return Assert.IsType<T>(field.GetRawConstantValue());
    }
}
