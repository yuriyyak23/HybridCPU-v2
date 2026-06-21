using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlPopcnt = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitCount.PopcntInstruction;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class NonVmxIteration04AScalarDeferredTemplateTests
{
    public static IEnumerable<object[]> DeferredTemplateCases()
    {
        yield return Template(typeof(CloseToRtlPopcnt), "POPCNT", "rd, rs1", "FacadeAliasNoEmissionClosed");
    }

    public static IEnumerable<object[]> DeferredStatusCases()
    {
        foreach (object[] testCase in DeferredTemplateCases())
        {
            yield return new[] { testCase[1] };
        }
    }

    public static IEnumerable<object[]> MetadataPass01BMarkerCases()
    {
        yield return MarkerTemplate(typeof(CloseToRtlPopcnt), "AliasNoEmissionPolicyClosed", "CanonicalMnemonicDecisionClosed", "SelectedAsNoEmissionAlias");
    }

    [Theory]
    [MemberData(nameof(DeferredTemplateCases))]
    public void Iteration04A_ScalarDeferredTemplates_DoNotExposeExecutionOrOpcodeAuthority(
        Type templateType,
        string mnemonic,
        string operandShape,
        string evidenceBoundary)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal(operandShape, GetConstant<string>(templateType, "OperandShape"));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "ParameterDescriptor")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "MicroOpShape")));
        Assert.Equal("Lanes00_03Scalar", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal(evidenceBoundary, GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal(64, GetConstant<int>(templateType, "XLen"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireRegisterWriteback"));
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackEvidence"));
        Assert.True(GetConstant<bool>(templateType, "NoVmxFrontendIntegrationRequired"));
        Assert.False(GetConstant<bool>(templateType, "RequiresVmxProjection"));
        Assert.False(GetConstant<bool>(templateType, "HasOpcodeAllocation"));
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"));
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"));

        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    [Theory]
    [MemberData(nameof(DeferredStatusCases))]
    public void Iteration04A_ScalarDeferredTemplates_RemainReservedWithoutRuntimeEvidence(string mnemonic)
    {
        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                mnemonic,
                out InstructionSupportStatus status),
            $"{mnemonic} must be explicit in the support-status catalog.");

        Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
        Assert.False(status.IsExecutableClaim, mnemonic);
        Assert.False(status.HasNumericOpcode, mnemonic);
        Assert.False(status.HasRuntimeOpcodeMetadata, mnemonic);
        Assert.False(status.HasCanonicalDecoderAcceptance, mnemonic);
        Assert.False(status.HasRegistryFactory, mnemonic);
        Assert.False(status.HasExecutionSemantics, mnemonic);
        AssertNoEnumOrRegistryMnemonic(mnemonic);
    }

    [Fact]
    public void Iteration04A_BitfieldTemplates_DoNotChangeClosedRegisterRotateEvidence()
    {
        InstructionSupportStatus rol = InstructionSupportStatusCatalog.GetStatus("ROL");
        InstructionSupportStatus ror = InstructionSupportStatusCatalog.GetStatus("ROR");

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, rol.Status);
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, ror.Status);
        Assert.True(rol.IsExecutableClaim);
        Assert.True(ror.IsExecutableClaim);
    }

    [Theory]
    [MemberData(nameof(MetadataPass01BMarkerCases))]
    public void MetadataPass01B_Scalar04ALeafTemplates_CarrySpecificAbiAndBoundaryMarkers(
        Type templateType,
        string requiredMarker,
        string optionalMarker1,
        string optionalMarker2)
    {
        Assert.True(GetConstant<bool>(templateType, requiredMarker), templateType.FullName);

        if (!string.IsNullOrEmpty(optionalMarker1))
        {
            Assert.True(GetConstant<bool>(templateType, optionalMarker1), templateType.FullName);
        }

        if (!string.IsNullOrEmpty(optionalMarker2))
        {
            Assert.True(GetConstant<bool>(templateType, optionalMarker2), templateType.FullName);
        }
    }

    private static object[] Template(
        Type templateType,
        string mnemonic,
        string operandShape,
        string evidenceBoundary) =>
        [templateType, mnemonic, operandShape, evidenceBoundary];

    private static object[] MarkerTemplate(
        Type templateType,
        string requiredMarker,
        string optionalMarker1,
        string optionalMarker2) =>
        [templateType, requiredMarker, optionalMarker1, optionalMarker2];

    private static T GetConstant<T>(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.FullName}.{name} was not found.");
        Assert.True(field.IsLiteral, $"{type.FullName}.{name} must remain a const template marker.");
        return Assert.IsType<T>(field.GetRawConstantValue());
    }

    private static void AssertNoEnumOrRegistryMnemonic(string mnemonic)
    {
        Assert.False(
            Enum.TryParse<InstructionsEnum>(mnemonic, ignoreCase: false, out _),
            $"{mnemonic} must not allocate an InstructionsEnum value in Iteration 04A.");
        Assert.DoesNotContain(
            OpcodeRegistry.Opcodes,
            info => string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
    }
}
