using System;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlMtileLoad = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileLoadInstruction;
using CloseToRtlMtileMacc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileMaccInstruction;
using CloseToRtlMtileStore = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileStoreInstruction;
using CloseToRtlMtranspose = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtransposeInstruction;
using CloseToRtlVdotAccum = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotAccumInstruction;
using CloseToRtlVdotBlockscale = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotBlockscaleInstruction;
using CloseToRtlVdotWideI16 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotWideI16Instruction;
using CloseToRtlVdotWideI32 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotWideI32Instruction;

namespace HybridCPU_ISE.Tests;

public sealed class DotMatrixDeferralBoundaryTests
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
    public void AdvancedDotRowsRemainReservedWhileMatrixTileRowsArePhase13Executable()
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
            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.Equal("XMatrix", status.ExtensionName);
            Assert.True(status.HasNumericOpcode);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
            Assert.True(HasEnum(mnemonic));
            Assert.True(HasIsaOpcodeValue(mnemonic));
            Assert.True(HasRegistryMnemonic(mnemonic));
            Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
            Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
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

    [Fact]
    public void AdvancedDotRows_HaveCompilerVisiblePlanningOnlyNoEmissionContracts()
    {
        Assert.Equal(
            CompilerFailClosedEmissionInventory.VectorDotTileVariantMnemonics.Order(StringComparer.Ordinal),
            CompilerVectorVlmBlockedAbiContract.AllVlmBlockedRows
                .Where(static contract => contract.AbiClass == CompilerVectorVlmBlockedAbiClass.DotTileVariant)
                .Select(static contract => contract.Mnemonic)
                .Order(StringComparer.Ordinal));

        foreach (CompilerFailClosedEmissionRow row in CompilerFailClosedEmissionInventory.VectorDotTileVariantRows)
        {
            CompilerVectorVlmBlockedAbiContract contract = Assert.Single(
                CompilerVectorVlmBlockedAbiContract.AllVlmBlockedRows,
                contract => contract.Mnemonic == row.Mnemonic);

            bool isBlockscale = row.Mnemonic == "VDOT.BLOCKSCALE";
            bool isAccumulator = row.Mnemonic == "VDOT.ACCUM";
            bool isWideInteger = row.Mnemonic is "VDOT.WIDE.I16" or "VDOT.WIDE.I32";
            Assert.Equal("VectorDotTileVariantVlmBlocked", contract.ExtensionName);
            Assert.Equal("VectorDotMatrixDeferredNoExecution", contract.EvidenceBoundary);
            Assert.False(contract.CompilerEmissionAllowed);
            Assert.False(contract.CompilerHelperAllowed);
            Assert.False(contract.TypedFacadeAllowed);
            Assert.False(contract.TypedHelperAllowed);
            Assert.True(contract.IsDotTileVariant);
            Assert.Equal(isBlockscale, contract.IsDotBlockscaleVariant);
            Assert.Equal(isAccumulator, contract.IsDotAccumulatorVariant);
            Assert.Equal(isWideInteger, contract.IsDotWideIntegerVariant);
            Assert.True(contract.RequiresDotVariantAbi);
            Assert.True(contract.RequiresDotTileHelperAbi);
            Assert.True(contract.RequiresAccumulatorPrecisionAbi);
            Assert.True(contract.RequiresAccumulatorResultFootprintAbi);
            Assert.Equal(isBlockscale, contract.RequiresScaleMetadataAbi);
            Assert.Equal(!isWideInteger, contract.RequiresSeparateResultSurfaceAbi);
            Assert.Equal(isWideInteger, contract.RequiresWiderIntegerContourAbi);
            Assert.True(contract.RequiresDeterministicOrderingReplayPolicy);
            Assert.True(contract.RequiresVlmMaterializationPolicy);
            Assert.True(contract.RequiresStagedPublicationRetirePolicy);
            Assert.True(contract.RequiresReplayRollbackGoldenEvidence);
            Assert.True(contract.SeparateFromScopedVdotWide);
            Assert.True(contract.NoScopedVdotWideFallback);
            Assert.True(contract.NoNameOnlyVdotWideExtension);
            Assert.True(contract.NoBaseDotProductFallback);
            Assert.True(contract.NoWideningFmaFallback);
            Assert.True(contract.NoLane6DescriptorFallback);
            Assert.True(contract.NoMatrixTileFallback);
            Assert.True(contract.NoScalarHelperFallback);
            Assert.True(contract.NoLane6StreamFallback);
            Assert.True(contract.NoLane7AcceleratorFallback);
            Assert.True(contract.NoVmxSpecificPathFallback);
            Assert.True(contract.NoExecutableRowAliasPromotion);
            Assert.True(contract.NoHostOwnedEvidencePublication);
            Assert.Contains("DotVariantAbi", contract.RequiredPolicyDecisions);
            Assert.Contains("DotTileHelperAbi", contract.RequiredPolicyDecisions);
            Assert.Contains("AccumulatorPrecisionAbi", contract.RequiredPolicyDecisions);
            Assert.Contains("AccumulatorResultFootprintAbi", contract.RequiredPolicyDecisions);
            Assert.Contains("DeterministicOrderingReplayPolicy", contract.RequiredPolicyDecisions);
            Assert.Contains("SeparateFromScopedVdotWide", contract.RequiredPolicyDecisions);
            Assert.Contains("NoScopedVdotWideFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoNameOnlyVdotWideExtension", contract.RequiredPolicyDecisions);
            Assert.Contains("NoBaseDotProductFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoWideningFmaFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoLane6DescriptorFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoMatrixTileFallback", contract.RequiredPolicyDecisions);
            Assert.Contains("NoHostOwnedEvidencePublication", contract.RequiredPolicyDecisions);

            if (isBlockscale)
            {
                Assert.Contains("ScaleMetadataAbi", contract.RequiredPolicyDecisions);
            }

            if (!isWideInteger)
            {
                Assert.Contains("SeparateResultSurfaceAbi", contract.RequiredPolicyDecisions);
            }

            if (isWideInteger)
            {
                Assert.Contains("WiderIntegerContourAbi", contract.RequiredPolicyDecisions);
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                contract.RequireCompilerHelperAuthority);
            Assert.Contains($"{row.Mnemonic} typed compiler helper emission is blocked", exception.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlMtileLoad), "MTILE_LOAD", true, false, false, true)]
    [InlineData(typeof(CloseToRtlMtileStore), "MTILE_STORE", true, false, false, false)]
    [InlineData(typeof(CloseToRtlMtileMacc), "MTILE_MACC", false, true, false, true)]
    [InlineData(typeof(CloseToRtlMtranspose), "MTRANSPOSE", false, false, true, true)]
    public void MatrixTileLeafMarkers_RecordPhase14ExecutableHandoffDecision(
        Type templateType,
        string mnemonic,
        bool expectsMemoryShapeFaultModel,
        bool expectsAccumulatorTileAbi,
        bool expectsTransposePolicyAbi,
        bool expectsPublication)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("MatrixTileRuntimeExecutableAuthority", GetConstant<string>(templateType, "EvidenceBoundary"));
        AssertCommonFailClosedMarkers(
            templateType,
            hasIrProjectionAndMaterializerPublication: true,
            hasTypedMicroOpSchedulerPublication: true,
            hasExecutionCapturePublication: true,
            hasRetirePublication: true,
            hasReplayRollbackPublication: true,
            productionDecision: "Phase14TileStreamResourceContourClosed",
            isExecutable: true);
        Assert.False(GetConstant<bool>(templateType, "OptionalDisabledInIsaV4"));
        Assert.True(GetConstant<bool>(templateType, "OptionalEnabledInIsaV4"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTileExecutionModel"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTileDescriptorAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedTileMicroOp"));
        Assert.True(GetConstant<bool>(templateType, "NoLane6DscFallback"));
        if (expectsMemoryShapeFaultModel)
        {
            Assert.True(GetConstant<bool>(templateType, "UsesDedicatedMatrixTileLane6Transport"));
            Assert.True(GetConstant<bool>(templateType, "NoGenericStreamEngineExecutionAuthority"));
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "NoLane6Placement"));
        }
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
    public void MatrixTileRows_AreCompilerPositiveEmissionScopeFromPhase13Handoff()
    {
        Assert.Empty(CompilerFailClosedEmissionInventory.MatrixTileOptionalDisabledRows);
        Assert.Equal(
            MatrixTileMnemonics.Order(StringComparer.Ordinal),
            CompilerMatrixTilePositiveEmissionAbiContract.Rows
                .Select(static row => row.Mnemonic)
                .Order(StringComparer.Ordinal));

        Assert.True(CompilerMatrixTilePositiveEmissionAbiContract.HasCurrentCompilerImplementation);
        Assert.True(CompilerMatrixTilePositiveEmissionAbiContract.UsesPhase13RuntimeHandoff);
        Assert.True(CompilerMatrixTilePositiveEmissionAbiContract.RuntimeOwnedLegalityIsFinal);
        Assert.False(CompilerMatrixTilePositiveEmissionAbiContract.AllowsCompilerToOverrideRuntimeLegality);
        Assert.False(CompilerMatrixTilePositiveEmissionAbiContract.UsesOldOptionalDisabledMetadataAsAuthority);
        Assert.False(CompilerMatrixTilePositiveEmissionAbiContract.UsesFallbackPath);
        Assert.False(CompilerMatrixTilePositiveEmissionAbiContract.UsesAliasPromotion);

        foreach (CompilerMatrixTilePositiveEmissionRow row in CompilerMatrixTilePositiveEmissionAbiContract.Rows)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);
            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.Equal("XMatrix", status.ExtensionName);
            Assert.True(status.HasNumericOpcode);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
            Assert.True(HasEnum(row.Mnemonic));
            Assert.True(HasIsaOpcodeValue(row.Mnemonic));
            Assert.True(HasRegistryMnemonic(row.Mnemonic));
            Assert.True(row.UsesPhase13RuntimeHandoff);
            Assert.True(row.RuntimeOwnedLegalityIsFinal);
            Assert.True(row.EmitsDirectMatrixTileOpcode);
            Assert.False(row.UsesFallbackPath);
            Assert.False(row.UsesAliasPromotion);
            Assert.False(string.IsNullOrWhiteSpace(row.RequiredTypedOperandContract));
            CompilerMatrixTilePositiveEmissionAbiContract.RequireRuntimeHandoffAuthority(row.Mnemonic);
        }

        Assert.All(CompilerMatrixTileOptionalDisabledAbiContract.AllOptionalDisabledRows, static legacy =>
        {
            Assert.False(legacy.CompilerEmissionAllowed);
            Assert.False(legacy.CompilerHelperAllowed);
            Assert.False(legacy.TypedFacadeAllowed);
            Assert.False(legacy.TypedHelperAllowed);
        });
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
            Assert.True(VectorLegalityMatrix.TryGetRow(matrixOpcode, out VectorLegalityMatrixRow? matrixRow));
            Assert.Equal("XMatrix", matrixRow.FamilyName);
            Assert.Equal(VectorContourLegalityStatus.DescriptorOnly, matrixRow.DescriptorBacked);
            Assert.Equal(VectorContourLegalityStatus.FailClosed, matrixRow.OneDimensional);
            Assert.Equal(VectorContourLegalityStatus.FailClosed, matrixRow.IndexedAddressing);
            Assert.Equal(VectorContourLegalityStatus.FailClosed, matrixRow.TwoDimensionalAddressing);
            Assert.Equal(VectorContourLegalityStatus.FailClosed, matrixRow.Masked);
            Assert.Equal(VectorContourLegalityStatus.FailClosed, matrixRow.TailMaskPolicy);
            Assert.Equal(VectorContourLegalityStatus.FailClosed, matrixRow.Reduction);
            Assert.False(VectorLegalityMatrix.AllowsAddressingExecution(
                matrixOpcode,
                indexed: false,
                is2D: false));
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

    private static void AssertCommonFailClosedMarkers(
        Type templateType,
        bool hasIrProjectionAndMaterializerPublication = false,
        bool hasTypedMicroOpSchedulerPublication = false,
        bool hasExecutionCapturePublication = false,
        bool hasRetirePublication = false,
        bool hasReplayRollbackPublication = false,
        string productionDecision = "Phase09NegativeDecisionGate",
        bool isExecutable = false)
    {
        string mnemonic = GetConstant<string>(templateType, "Mnemonic");
        bool isMatrixTile =
            templateType.Namespace?.Contains(".MatrixTile", StringComparison.Ordinal) == true;
        Assert.Equal(
            isMatrixTile
                ? mnemonic is "MTILE_LOAD" or "MTILE_STORE"
                    ? "MatrixTileStreamLane6"
                    : "MatrixTileComputeLanes00_03"
                : "Lanes00_03Vector",
            GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.Equal(productionDecision, GetConstant<string>(templateType, "ProductionDecision"));
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"));
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureRetireReplayEvidence"));
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackConformance"));
        Assert.True(GetConstant<bool>(templateType, "RequiresGoldenArtifacts"));
        Assert.True(GetConstant<bool>(templateType, "NoDecoderEncoderAbiPublication"));
        Assert.Equal(
            !hasIrProjectionAndMaterializerPublication,
            GetConstant<bool>(templateType, "NoInstructionIrProjectionPublication"));
        Assert.Equal(
            !hasIrProjectionAndMaterializerPublication,
            GetConstant<bool>(templateType, "NoRegistryMaterializerPublication"));
        Assert.Equal(
            !hasTypedMicroOpSchedulerPublication,
            GetConstant<bool>(templateType, "NoTypedMicroOpPublication"));
        Assert.Equal(
            !hasTypedMicroOpSchedulerPublication,
            GetConstant<bool>(templateType, "NoSchedulerLaneBindingPublication"));
        Assert.Equal(
            !hasExecutionCapturePublication,
            GetConstant<bool>(templateType, "NoExecutionCapturePublication"));
        Assert.Equal(
            !hasRetirePublication,
            GetConstant<bool>(templateType, "NoRetireWritebackPublication"));
        Assert.Equal(
            !hasReplayRollbackPublication,
            GetConstant<bool>(templateType, "NoReplayRollbackPublication"));
        Assert.True(GetConstant<bool>(templateType, "NoCompilerHelperEmission"));
        Assert.True(GetConstant<bool>(templateType, "NoDescriptorFallback"));
        Assert.True(GetConstant<bool>(templateType, "NoDescriptorFallbackWithoutGenericRuntimeOwnership"));
        Assert.True(GetConstant<bool>(templateType, "NoHiddenScalarLowering"));
        Assert.True(GetConstant<bool>(templateType, "NoHiddenVectorLowering"));
        Assert.True(GetConstant<bool>(templateType, "NoMultiOpEmission"));
        Assert.True(GetConstant<bool>(templateType, "NoVmxSpecificPath"));
        Assert.Equal(isExecutable, GetConstant<bool>(templateType, "IsExecutable"));
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
