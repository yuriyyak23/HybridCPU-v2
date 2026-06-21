using System;
using System.Linq;
using System.Reflection;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlVall = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VallInstruction;
using CloseToRtlVany = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VanyInstruction;
using CloseToRtlVfirst = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VfirstInstruction;
using CloseToRtlVmsif = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VmsifInstruction;
using CloseToRtlVmsof = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VmsofInstruction;

namespace HybridCPU_ISE.Tests;

public sealed class VectorPredicateScalarAndPublicationFailClosedTests
{
    private static readonly string[] ScalarResultMnemonics = ["VFIRST", "VANY", "VALL"];
    private static readonly string[] PredicateOnlyMnemonics = ["VMSIF", "VMSOF"];

    [Fact]
    public void ScalarResultAndPredicateOnlyRows_RemainReservedNoAllocation()
    {
        foreach (string mnemonic in ScalarResultMnemonics.Concat(PredicateOnlyMnemonics))
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
    [InlineData(typeof(CloseToRtlVfirst), "VFIRST", true, false)]
    [InlineData(typeof(CloseToRtlVany), "VANY", false, true)]
    [InlineData(typeof(CloseToRtlVall), "VALL", false, true)]
    public void ScalarResultRows_RecordPhase05BNegativeDecisionGate(
        Type templateType,
        string mnemonic,
        bool expectsFirstIndexPolicy,
        bool expectsBooleanEncodingPolicy)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorScalarResultContourFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Lanes00_03Vector", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.Contains("scalar rd", GetConstant<string>(templateType, "ScalarResultDestinationPolicy"), StringComparison.Ordinal);
        Assert.Contains("Unresolved", GetConstant<string>(templateType, "EmptyMaskResultPolicy"), StringComparison.Ordinal);
        Assert.Contains("Explicit carrier sideband", GetConstant<string>(templateType, "PredicateMaskSidebandPolicy"), StringComparison.Ordinal);
        Assert.Contains("active VL", GetConstant<string>(templateType, "ActiveVlTailPolicy"), StringComparison.Ordinal);

        Assert.True(GetConstant<bool>(templateType, "RequiresPredicateMaskSideband"));
        Assert.True(GetConstant<bool>(templateType, "RequiresScalarResultAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresScalarResultDestinationAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresEmptyMaskResultPolicy"));
        Assert.True(GetConstant<bool>(templateType, "RequiresActiveVlTailPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedVectorMicroOp"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireReplayPublicationAbi"));
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

        if (expectsFirstIndexPolicy)
        {
            Assert.Contains("first-index", GetConstant<string>(templateType, "FirstIndexWidthSignPolicy"), StringComparison.Ordinal);
            Assert.True(GetConstant<bool>(templateType, "RequiresFirstIndexWidthSignPolicy"));
        }

        if (expectsBooleanEncodingPolicy)
        {
            Assert.Contains("false/true", GetConstant<string>(templateType, "BooleanResultEncodingPolicy"), StringComparison.Ordinal);
            Assert.True(GetConstant<bool>(templateType, "RequiresBooleanResultEncodingAbi"));
        }

        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVmsif), "VMSIF", "including-first")]
    [InlineData(typeof(CloseToRtlVmsof), "VMSOF", "only-first")]
    public void PredicateOnlyRows_RecordPhase05CNegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string semanticPolicyFragment)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorPredicateOnlyContourFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Lanes00_03Vector", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.Contains("predicate-only destination", GetConstant<string>(templateType, "PredicateDestinationRepresentationPolicy"), StringComparison.Ordinal);
        Assert.Contains(semanticPolicyFragment, GetConstant<string>(templateType, "PrefixSuffixMaskSemanticsPolicy"), StringComparison.Ordinal);
        Assert.Contains("tail/mask", GetConstant<string>(templateType, "TailMaskPolicy"), StringComparison.Ordinal);
        Assert.Contains("retire-staged", GetConstant<string>(templateType, "StagedPredicatePublicationPolicy"), StringComparison.Ordinal);

        Assert.True(GetConstant<bool>(templateType, "RequiresPredicateMaskSideband"));
        Assert.True(GetConstant<bool>(templateType, "RequiresPredicateOnlyPublication"));
        Assert.True(GetConstant<bool>(templateType, "RequiresPredicateOnlyDestinationRepresentation"));
        Assert.True(GetConstant<bool>(templateType, "RequiresPrefixSuffixMaskSemantics"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTailMaskPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedVectorMicroOp"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"));
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackConformance"));
        Assert.True(GetConstant<bool>(templateType, "RequiresGoldenArtifacts"));
        Assert.True(GetConstant<bool>(templateType, "NoVectorRfExposure"));
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
    public void ScalarResultAndPredicateOnlyRows_DoNotInheritVpopcOrVmsbfEvidence()
    {
        VectorLegalityMatrixRow vpopcRow = VectorLegalityMatrix.GetRow(InstructionsEnum.VPOPC);
        VectorLegalityMatrixRow vmsbfRow = VectorLegalityMatrix.GetRow(InstructionsEnum.VMSBF);

        Assert.Equal("VectorPredicateMaskScalarResult", vpopcRow.FamilyName);
        Assert.Equal([InstructionsEnum.VPOPC], vpopcRow.Opcodes);
        Assert.Equal("VectorMaskPrefixPublication", vmsbfRow.FamilyName);
        Assert.Equal([InstructionsEnum.VMSBF], vmsbfRow.Opcodes);

        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row => row.FamilyName == "VectorMaskSelect");
        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row => row.Opcodes.Any(opcode =>
            ScalarResultMnemonics.Concat(PredicateOnlyMnemonics).Contains(opcode.ToString(), StringComparer.Ordinal)));
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
