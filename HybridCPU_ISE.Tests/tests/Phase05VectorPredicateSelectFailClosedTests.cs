using System;
using System.Linq;
using System.Reflection;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlVmerge = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VmergeInstruction;
using CloseToRtlVselect = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VselectInstruction;

namespace HybridCPU_ISE.Tests.Phase05;

public sealed class Phase05VectorPredicateSelectFailClosedTests
{
    private static readonly string[] SelectMergeMnemonics = ["VMERGE", "VSELECT"];

    [Fact]
    public void VmergeVselect_StatusCatalogAndIsaSurface_RemainReservedNoAllocation()
    {
        foreach (string mnemonic in SelectMergeMnemonics)
        {
            Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(
                mnemonic,
                out InstructionSupportStatus status));
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.Equal("VectorMaskSelect", status.ExtensionName);
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
    [InlineData(typeof(CloseToRtlVmerge), "VMERGE")]
    [InlineData(typeof(CloseToRtlVselect), "VSELECT")]
    public void VmergeVselect_CloseToRtlMarkers_RecordPhase05ANegativeDecisionGate(
        Type templateType,
        string mnemonic)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorContourFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Lanes00_03Vector", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.Contains("Distinct mnemonic", GetConstant<string>(templateType, "AliasPolicy"), StringComparison.Ordinal);
        Assert.Contains("Unresolved", GetConstant<string>(templateType, "ResultSourcePolarityPolicy"), StringComparison.Ordinal);
        Assert.Contains("mask/tail ABI", GetConstant<string>(templateType, "MaskedOffTailPolicy"), StringComparison.Ordinal);
        Assert.Contains("Explicit carrier sideband", GetConstant<string>(templateType, "PredicateMaskSidebandPolicy"), StringComparison.Ordinal);
        Assert.Contains("LMUL", GetConstant<string>(templateType, "ElementWidthLmulVlPolicy"), StringComparison.Ordinal);

        Assert.True(GetConstant<bool>(templateType, "RequiresPredicateMaskSideband"));
        Assert.True(GetConstant<bool>(templateType, "RequiresAliasPolarityDecision"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskedOffTailPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresElementWidthLmulVlAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedVectorMicroOp"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"));
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackConformance"));
        Assert.True(GetConstant<bool>(templateType, "RequiresGoldenArtifacts"));
        Assert.False(GetConstant<bool>(templateType, "MaskSidebandInferredFromVlm"));
        Assert.True(GetConstant<bool>(templateType, "NoHiddenScalarLowering"));
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

    [Fact]
    public void VmergeVselect_DoNotReuseClosedVmsbfOrPredicateMovementEvidence()
    {
        VectorLegalityMatrixRow vmsbfRow = VectorLegalityMatrix.GetRow(InstructionsEnum.VMSBF);

        Assert.Equal("VectorMaskPrefixPublication", vmsbfRow.FamilyName);
        Assert.Equal([InstructionsEnum.VMSBF], vmsbfRow.Opcodes);
        Assert.Contains("VMSIF/VMSOF and vector select/merge remain closed", vmsbfRow.RuntimeEvidenceNote, StringComparison.Ordinal);

        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row => row.FamilyName == "VectorMaskSelect");
        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row => row.Opcodes.Any(opcode =>
            string.Equals(opcode.ToString(), "VMERGE", StringComparison.Ordinal) ||
            string.Equals(opcode.ToString(), "VSELECT", StringComparison.Ordinal)));
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
