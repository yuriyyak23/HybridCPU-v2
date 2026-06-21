using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlDsc2DShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.Dsc2DShapeContour;
using CloseToRtlDscAbsDiff = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscAbsDiffDescriptorOp;
using CloseToRtlDscClamp = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscClampDescriptorOp;
using CloseToRtlDscCompare = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Predicate.DscCompareDescriptorOp;
using CloseToRtlDscConvert = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.TypeConversion.DscConvertDescriptorOp;
using CloseToRtlDscMax = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscMaxDescriptorOp;
using CloseToRtlDscMin = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscMinDescriptorOp;
using CloseToRtlDscMultiRangeShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscMultiRangeShapeContour;
using CloseToRtlDscReduceAnd = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceAndDescriptorOp;
using CloseToRtlDscReduceMax = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceMaxDescriptorOp;
using CloseToRtlDscReduceMin = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceMinDescriptorOp;
using CloseToRtlDscReduceOr = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceOrDescriptorOp;
using CloseToRtlDscReduceSum = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceSumDescriptorOp;
using CloseToRtlDscReduceXor = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceXorDescriptorOp;
using CloseToRtlDscScatterGatherShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscScatterGatherShapeContour;
using CloseToRtlDscSelect = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Predicate.DscSelectDescriptorOp;
using CloseToRtlDscStridedShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscStridedShapeContour;
using CloseToRtlDscSub = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscSubDescriptorOp;
using CloseToRtlDscTiledShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscTiledShapeContour;

namespace HybridCPU_ISE.Tests;

public sealed class Lane6DescriptorOpsFailClosedTests
{
    private static IReadOnlyList<string> DescriptorOpMnemonics =>
        CompilerFailClosedEmissionInventory.Lane6DescriptorMnemonics;

    private static IReadOnlyList<string> ShapeContourMnemonics =>
        CompilerFailClosedEmissionInventory.Lane6ShapeMnemonics;

    [Fact]
    public void Rows_RemainDescriptorOnlyDeclaredWithoutProductionPublication()
    {
        foreach (string mnemonic in DescriptorOpMnemonics)
        {
            AssertDescriptorOnlyRow(mnemonic, "Lane6DescriptorOp");
        }

        foreach (string mnemonic in ShapeContourMnemonics)
        {
            AssertDescriptorOnlyRow(mnemonic, "Lane6DescriptorShape");
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlDscSub), "DmaStreamCompute.SUB", "RequiresArithmeticPolicyAbi", "")]
    [InlineData(typeof(CloseToRtlDscMin), "DmaStreamCompute.MIN", "RequiresSignednessTypePolicyAbi", "")]
    [InlineData(typeof(CloseToRtlDscMax), "DmaStreamCompute.MAX", "RequiresSignednessTypePolicyAbi", "")]
    [InlineData(typeof(CloseToRtlDscAbsDiff), "DmaStreamCompute.ABSDIFF", "RequiresOverflowPolicyAbi", "")]
    [InlineData(typeof(CloseToRtlDscClamp), "DmaStreamCompute.CLAMP", "RequiresBoundsPolicyAbi", "")]
    [InlineData(typeof(CloseToRtlDscConvert), "DmaStreamCompute.CONVERT", "RequiresConversionPolicyAbi", "RequiresRoundingSaturationTrapPolicy")]
    [InlineData(typeof(CloseToRtlDscCompare), "DmaStreamCompute.COMPARE", "RequiresPredicateFootprintAbi", "")]
    [InlineData(typeof(CloseToRtlDscSelect), "DmaStreamCompute.SELECT", "RequiresPredicateFootprintAbi", "RequiresSelectResultFootprintAbi")]
    [InlineData(typeof(CloseToRtlDscReduceSum), "DmaStreamCompute.REDUCE_SUM", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToRtlDscReduceMin), "DmaStreamCompute.REDUCE_MIN", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToRtlDscReduceMax), "DmaStreamCompute.REDUCE_MAX", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToRtlDscReduceAnd), "DmaStreamCompute.REDUCE_AND", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToRtlDscReduceOr), "DmaStreamCompute.REDUCE_OR", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToRtlDscReduceXor), "DmaStreamCompute.REDUCE_XOR", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    public void DescriptorOpLeafMarkers_RecordPhase10NegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string requiredPolicyMarker,
        string optionalPolicyMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("Lane6DescriptorOwnedNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        AssertCommonDescriptorFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorOpTypeAllocation"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorParserValidation"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, requiredPolicyMarker), templateType.FullName);

        if (!string.IsNullOrEmpty(optionalPolicyMarker))
        {
            Assert.True(GetConstant<bool>(templateType, optionalPolicyMarker), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlDscStridedShape), "DSC_SHAPE_STRIDED", "RequiresStrideAbi")]
    [InlineData(typeof(CloseToRtlDscTiledShape), "DSC_SHAPE_TILED", "RequiresTileShapeAbi")]
    [InlineData(typeof(CloseToRtlDscScatterGatherShape), "DSC_SHAPE_SCATTER_GATHER", "RequiresIndexSurfaceAbi")]
    [InlineData(typeof(CloseToRtlDsc2DShape), "DSC_SHAPE_2D", "Requires2DShapeAbi")]
    [InlineData(typeof(CloseToRtlDscMultiRangeShape), "DSC_SHAPE_MULTI_RANGE", "RequiresMultiRangeAbi")]
    public void ShapeContourLeafMarkers_RecordPhase10NegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string requiredShapeMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("Lane6ShapeContourNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        AssertCommonDescriptorFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresShapeEnumAllocation"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresShapeParserManifest"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresShapeFaultModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresAliasOverlapPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoShapeEnumPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDescriptorParserPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDsc2Fallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, requiredShapeMarker), templateType.FullName);
    }

    [Fact]
    public void GenericDmaStreamComputeEvidence_DoesNotAuthorizePhase10DescriptorOpsOrShapes()
    {
        InstructionSupportStatus genericStatus =
            InstructionSupportStatusCatalog.GetStatus("DmaStreamCompute");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, genericStatus.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, genericStatus.RuntimeEvidence);
        Assert.True(genericStatus.IsExecutableClaim);
        Assert.True(Enum.IsDefined(InstructionsEnum.DmaStreamCompute));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.DmaStreamCompute));

        Assert.Equal(
            ["Copy", "Add", "Mul", "Fma", "Reduce"],
            Enum.GetNames<DmaStreamComputeOperationKind>());
        Assert.Equal(
            ["Contiguous1D", "FixedReduce"],
            Enum.GetNames<DmaStreamComputeShapeKind>());

        foreach (string forbiddenOperation in new[]
                 { "Sub", "Min", "Max", "AbsDiff", "Clamp", "Convert", "Compare", "Select",
                   "ReduceSum", "ReduceMin", "ReduceMax", "ReduceAnd", "ReduceOr", "ReduceXor" })
        {
            Assert.False(
                Enum.TryParse(forbiddenOperation, ignoreCase: false, out DmaStreamComputeOperationKind _),
                forbiddenOperation);
        }

        foreach (string forbiddenShape in new[]
                 { "Strided", "Tiled", "ScatterGather", "Shape2D", "MultiRange" })
        {
            Assert.False(
                Enum.TryParse(forbiddenShape, ignoreCase: false, out DmaStreamComputeShapeKind _),
                forbiddenShape);
        }

        foreach (string mnemonic in DescriptorOpMnemonics.Concat(ShapeContourMnemonics))
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.DescriptorOnly, status.Status);
            Assert.False(status.IsExecutableClaim);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(HasEnum(mnemonic));
            Assert.False(HasIsaOpcodeValue(mnemonic));
            Assert.False(HasRegistryMnemonic(mnemonic));
        }
    }

    [Fact]
    public void VectorLegalityMatrix_DoesNotTreatLane6DescriptorRowsAsExecutableVectorContours()
    {
        foreach (string mnemonic in DescriptorOpMnemonics.Concat(ShapeContourMnemonics))
        {
            Assert.DoesNotContain(
                VectorLegalityMatrix.Rows,
                row =>
                    row.FamilyName.Contains(mnemonic, StringComparison.Ordinal) ||
                    row.RuntimeEvidenceNote.Contains(mnemonic, StringComparison.Ordinal));
        }

        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row =>
            row.FamilyName is "Lane6DescriptorOwnedNoExecution" or "Lane6ShapeContourNoExecution");
    }

    [Fact]
    public void CompilerDeferredAbi_RecordsDescriptorOpsAndShapesWithoutEmissionAuthority()
    {
        CompilerLane6DeferredAbiContract[] rows =
        [
            .. CompilerLane6DeferredAbiContract.AllDeferredLane6Rows
                .Where(static row => row.AbiClass is
                    CompilerLane6DeferredAbiClass.DescriptorOp or
                    CompilerLane6DeferredAbiClass.DescriptorShape)
        ];

        Assert.Equal(
            DescriptorOpMnemonics.Concat(ShapeContourMnemonics).Order(StringComparer.Ordinal).ToArray(),
            rows.Select(static row => row.Mnemonic).Order(StringComparer.Ordinal).ToArray());

        foreach (CompilerLane6DeferredAbiContract contract in rows)
        {
            Assert.False(contract.HasOpcodeAllocation);
            Assert.False(contract.HasScalarOpcodeAllocation);
            Assert.False(contract.IsExecutable);
            Assert.False(contract.CompilerEmissionAllowed);
            Assert.False(contract.CompilerHelperAllowed);
            Assert.True(contract.IsDescriptorOwned);
            Assert.True(contract.IsDescriptorOnly);
            Assert.True(contract.IsDescriptorParserOnlyBoundary);
            Assert.True(contract.RuntimeExecutionEvidenceAbsent);
            Assert.True(contract.RequiresRuntimeAdmission);
            Assert.True(contract.RequiresRetireCommitAuthority);
            Assert.True(contract.RequiresReplayRollbackConformance);
            Assert.True(contract.RequiresGoldenArtifacts);
            Assert.True(contract.RequiresRetireReplayGoldenEvidence);
            Assert.True(contract.RuntimeOwnedLegalityIsFinal);
            Assert.True(contract.NoCompilerHelperEmission);
            Assert.True(contract.NoHiddenScalarLowering);
            Assert.True(contract.NoHiddenVectorLowering);
            Assert.True(contract.NoMultiOpEmission);
            Assert.True(contract.NoDmaStreamComputeFallback);
            Assert.True(contract.NoGenericDmaStreamComputeFallbackAsAuthority);
            Assert.True(contract.NoDsc2Fallback);
            Assert.True(contract.NoLane7Fallback);
            Assert.True(contract.NoExternalBackendFallback);
            Assert.True(contract.NoVmxSpecificPath);
            Assert.Contains("RuntimeExecutionEvidenceAbsent", contract.RequiredPolicyDecisions);
            Assert.Contains("RuntimeOwnedLegalityFinal", contract.RequiredPolicyDecisions);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                contract.RequireCompilerEmissionAuthority);
            Assert.Contains($"{contract.Mnemonic} compiler emission is blocked", exception.Message, StringComparison.Ordinal);

            if (contract.AbiClass == CompilerLane6DeferredAbiClass.DescriptorOp)
            {
                Assert.Equal("Lane6DescriptorOp", contract.ExtensionName);
                Assert.Equal("Lane6DescriptorOwnedNoExecution", contract.EvidenceBoundary);
                Assert.True(contract.IsDescriptorOp);
                Assert.False(contract.IsDescriptorShape);
                Assert.True(contract.RequiresDescriptorOpAbi);
                Assert.True(contract.RequiresDescriptorPayloadAbi);
                Assert.True(contract.RequiresDescriptorParserValidation);
                Assert.True(contract.RequiresDescriptorOpTypeAbi);
                Assert.Contains("DescriptorOpAbi", contract.RequiredPolicyDecisions);
            }
            else
            {
                Assert.Equal(CompilerLane6DeferredAbiClass.DescriptorShape, contract.AbiClass);
                Assert.Equal("Lane6DescriptorShape", contract.ExtensionName);
                Assert.Equal("Lane6ShapeContourNoExecution", contract.EvidenceBoundary);
                Assert.False(contract.IsDescriptorOp);
                Assert.True(contract.IsDescriptorShape);
                Assert.True(contract.RequiresShapeAbi);
                Assert.True(contract.RequiresShapeEnumAbi);
                Assert.True(contract.RequiresShapeParserManifest);
                Assert.True(contract.RequiresShapeFaultModel);
                Assert.True(contract.RequiresAliasOverlapPolicy);
                Assert.Contains("ShapeAbi", contract.RequiredPolicyDecisions);
            }
        }
    }

    private static void AssertDescriptorOnlyRow(string mnemonic, string expectedExtension)
    {
        Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(
            mnemonic,
            out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.DescriptorOnly, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.DeclaredOnly, status.RuntimeEvidence);
        Assert.Equal(expectedExtension, status.ExtensionName);
        Assert.False(status.HasNumericOpcode);
        Assert.False(status.HasRuntimeOpcodeMetadata);
        Assert.False(status.HasCanonicalDecoderAcceptance);
        Assert.False(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);
        Assert.Contains(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.PipelineClassMap.Keys);
        Assert.False(HasEnum(mnemonic));
        Assert.False(HasIsaOpcodeValue(mnemonic));
        Assert.False(HasRegistryMnemonic(mnemonic));
    }

    private static void AssertCommonDescriptorFailClosedMarkers(Type templateType)
    {
        Assert.Equal("Lane06DmaStream", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.Equal("Phase10NegativeDecisionGate", GetConstant<string>(templateType, "ProductionDecision"));
        Assert.True(GetConstant<bool>(templateType, "IsDescriptorOwned"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "HasScalarOpcodeAllocation"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresOwnerDomainGuard"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTokenAdmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorPayloadAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedDescriptorProjection"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorMaterializer"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresBackendRuntimeAdmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresStagedCommit"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireCommitAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureRetireReplayEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayDeterminism"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackConformance"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRollbackPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresGoldenArtifacts"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGuestVisibleHostEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostOwnedEvidencePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingDmaStreamComputeEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoScalarOpcodePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDecoderEncoderAbiPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoInstructionIrProjectionPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoRegistryMaterializerPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoTypedMicroOpPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoSchedulerLaneBindingPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoExecutionCapturePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoRetireWritebackPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoReplayRollbackPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoCompilerHelperEmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHiddenScalarLowering"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHiddenVectorLowering"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoMultiOpEmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGenericDmaStreamComputeFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoStreamEngineFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDmaControllerFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoLane7Fallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoExternalBackendFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoVmxSpecificPath"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureVirtualizationBoundaryPolicy"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
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
