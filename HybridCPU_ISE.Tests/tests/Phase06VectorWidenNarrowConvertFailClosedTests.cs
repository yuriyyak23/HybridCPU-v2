using System;
using System.Linq;
using System.Reflection;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlVcvtF = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VcvtFInstruction;
using CloseToRtlVcvtI = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VcvtIInstruction;
using CloseToRtlVcvtU = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VcvtUInstruction;
using CloseToRtlVnsra = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Narrowing.VnsraInstruction;
using CloseToRtlVnsrl = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Narrowing.VnsrlInstruction;
using CloseToRtlVsext = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VsextInstruction;
using CloseToRtlVwadd = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwaddInstruction;
using CloseToRtlVwaddu = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwadduInstruction;
using CloseToRtlVwmacc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwmaccInstruction;
using CloseToRtlVwmul = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwmulInstruction;
using CloseToRtlVwmulu = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwmuluInstruction;
using CloseToRtlVwsub = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwsubInstruction;
using CloseToRtlVwsubu = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwsubuInstruction;

namespace HybridCPU_ISE.Tests;

public sealed class VectorWidenNarrowConvertFailClosedTests
{
    private static readonly string[] Phase06Mnemonics =
    [
        "VWADD", "VWADDU", "VWSUB", "VWSUBU", "VWMUL", "VWMULU", "VWMACC",
        "VNSRL", "VNSRA", "VSEXT", "VCVT.I", "VCVT.U", "VCVT.F"
    ];

    [Fact]
    public void Rows_RemainReservedNoAllocationAndDoNotPublishOpcodeSurface()
    {
        foreach (string mnemonic in Phase06Mnemonics)
        {
            Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(
                mnemonic,
                out InstructionSupportStatus status));
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.Equal("VectorWidenNarrowConvert", status.ExtensionName);
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
    [InlineData(typeof(CloseToRtlVwadd), "VWADD", false, "signed widening add")]
    [InlineData(typeof(CloseToRtlVwaddu), "VWADDU", false, "unsigned widening add")]
    [InlineData(typeof(CloseToRtlVwsub), "VWSUB", false, "signed widening subtract")]
    [InlineData(typeof(CloseToRtlVwsubu), "VWSUBU", false, "unsigned widening subtract")]
    [InlineData(typeof(CloseToRtlVwmul), "VWMUL", false, "signed widening multiply")]
    [InlineData(typeof(CloseToRtlVwmulu), "VWMULU", false, "unsigned widening multiply")]
    [InlineData(typeof(CloseToRtlVwmacc), "VWMACC", true, "widening multiply-accumulate")]
    public void WideningRows_RecordPhase06ANegativeDecisionGate(
        Type templateType,
        string mnemonic,
        bool expectsAccumulatorAbi,
        string signednessFragment)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.Contains("element-width", GetConstant<string>(templateType, "WidthTransformPolicy"), StringComparison.Ordinal);
        Assert.Contains(signednessFragment, GetConstant<string>(templateType, "SignednessPolicy"), StringComparison.Ordinal);
        Assert.Contains("result footprint", GetConstant<string>(templateType, "OverflowPublicationPolicy"), StringComparison.Ordinal);
        Assert.Contains("mask/tail", GetConstant<string>(templateType, "MaskTailPolicy"), StringComparison.Ordinal);

        Assert.True(GetConstant<bool>(templateType, "RequiresSourceDestinationWidthSideband"));
        Assert.True(GetConstant<bool>(templateType, "RequiresSignednessAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresElementWidthLmulVlAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresWideningOverflowPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskTailPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "SeparateFromBaseVectorArithmetic"));

        if (expectsAccumulatorAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresAccumulatorAbi"));
            Assert.Contains("accumulator precision", GetConstant<string>(templateType, "AccumulatorFootprintPolicy"), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVnsrl), "VNSRL", "logical shift")]
    [InlineData(typeof(CloseToRtlVnsra), "VNSRA", "arithmetic shift")]
    public void NarrowingRows_RecordPhase06BNegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string shiftFragment)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.Contains("narrowing width", GetConstant<string>(templateType, "WidthTransformPolicy"), StringComparison.Ordinal);
        Assert.Contains(shiftFragment, GetConstant<string>(templateType, "ShiftOperandPolicy"), StringComparison.Ordinal);
        Assert.Contains("truncation", GetConstant<string>(templateType, "NarrowingResultPolicy"), StringComparison.Ordinal);
        Assert.Contains("mask/tail", GetConstant<string>(templateType, "MaskTailPolicy"), StringComparison.Ordinal);

        Assert.True(GetConstant<bool>(templateType, "RequiresSourceDestinationWidthSideband"));
        Assert.True(GetConstant<bool>(templateType, "RequiresNarrowingPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRoundingSaturationTrapPolicy"));
        Assert.True(GetConstant<bool>(templateType, "RequiresShiftOperandAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresElementWidthLmulVlAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskTailPolicyAbi"));
    }

    [Fact]
    public void Vsext_RecordsPhase06CSignExtensionGateAndDoesNotInheritVzext()
    {
        Type templateType = typeof(CloseToRtlVsext);

        Assert.Equal("VSEXT", GetConstant<string>(templateType, "Mnemonic"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.Contains("sign-extension", GetConstant<string>(templateType, "WidthTransformPolicy"), StringComparison.Ordinal);
        Assert.Contains("signed source", GetConstant<string>(templateType, "SignednessPolicy"), StringComparison.Ordinal);
        Assert.True(GetConstant<bool>(templateType, "RequiresSourceDestinationWidthSideband"));
        Assert.True(GetConstant<bool>(templateType, "RequiresSignednessAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresElementWidthLmulVlAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskTailPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "SeparateFromClosedVzext"));
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVcvtI), "VCVT.I", "signed integer")]
    [InlineData(typeof(CloseToRtlVcvtU), "VCVT.U", "unsigned integer")]
    [InlineData(typeof(CloseToRtlVcvtF), "VCVT.F", "floating-point")]
    public void ConversionRows_RecordPhase06CConversionPolicyGate(
        Type templateType,
        string mnemonic,
        string resultFootprintFragment)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        AssertCommonFailClosedMarkers(templateType);
        Assert.Contains("type conversion ABI", GetConstant<string>(templateType, "ConversionPolicy"), StringComparison.Ordinal);
        Assert.Contains(resultFootprintFragment, GetConstant<string>(templateType, "ResultFootprintPolicy"), StringComparison.Ordinal);
        Assert.Contains("NaN", GetConstant<string>(templateType, "NanPolicy"), StringComparison.Ordinal);
        Assert.Contains("rounding", GetConstant<string>(templateType, "RoundingSaturationTrapPolicy"), StringComparison.Ordinal);
        Assert.Contains("mask/tail", GetConstant<string>(templateType, "MaskTailPolicy"), StringComparison.Ordinal);

        Assert.True(GetConstant<bool>(templateType, "RequiresConversionPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRoundingSaturationTrapPolicy"));
        Assert.True(GetConstant<bool>(templateType, "RequiresResultFootprintAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresNanPolicyAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMaskTailPolicyAbi"));
    }

    [Fact]
    public void Rows_DoNotReuseClosedVzextOrBaseArithmeticEvidence()
    {
        VectorLegalityMatrixRow vzextRow = VectorLegalityMatrix.GetRow(InstructionsEnum.VZEXT);

        Assert.Equal("VectorZeroExtendPublication", vzextRow.FamilyName);
        Assert.Equal([InstructionsEnum.VZEXT], vzextRow.Opcodes);
        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row => row.FamilyName == "VectorWidenNarrowConvert");
        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row => row.Opcodes.Any(opcode =>
            Phase06Mnemonics.Contains(opcode.ToString(), StringComparer.Ordinal)));
    }

    private static void AssertCommonFailClosedMarkers(Type templateType)
    {
        Assert.Equal("VectorWidenNarrowConvertFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Lanes00_03Vector", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"));
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"));
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedVectorMicroOp"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"));
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackConformance"));
        Assert.True(GetConstant<bool>(templateType, "RequiresGoldenArtifacts"));
        Assert.True(GetConstant<bool>(templateType, "NoDescriptorFallback"));
        Assert.True(GetConstant<bool>(templateType, "NoHiddenScalarLowering"));
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
