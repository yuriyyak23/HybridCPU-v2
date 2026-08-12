using HybridCPU.Compiler.Core.API.Facade;
using MinimalAsmApp.Examples.Abstractions;
using YAKSys_Hybrid_CPU;

namespace MinimalAsmApp.Examples.InstructionClosure;

using Instruction = Processor.CPU_Core.InstructionsEnum;

public sealed class NonVmxScalarExtensionExample : ICpuExample
{
    public string Name => "non-vmx-scalar-extension";

    public string Description => "Emits SEXT.B, SEXT.H, ZEXT.H, SEXT.W, and ZEXT.W through the open compiler scalar extension helpers.";

    public string Category => "13_InstructionClosure";

    public CpuExampleResult Run()
    {
        return NonVmxCompilerExampleSupport.RunScalarCompilerExample(
            "Typed scalar compiler emitted canonical byte/half/word scalar extension carriers.",
            [
                new(
                    "SEXT.B",
                    "HybridCpuNonVmxScalarCompiler.SignExtendByte",
                    Instruction.SEXT_B,
                    compiler => compiler.SignExtendByte(new AsmRegister(20), new AsmRegister(1)),
                    ExpectedRd: 20,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "SEXT.H",
                    "HybridCpuNonVmxScalarCompiler.SignExtendHalf",
                    Instruction.SEXT_H,
                    compiler => compiler.SignExtendHalf(new AsmRegister(21), new AsmRegister(1)),
                    ExpectedRd: 21,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "ZEXT.H",
                    "HybridCpuNonVmxScalarCompiler.ZeroExtendHalf",
                    Instruction.ZEXT_H,
                    compiler => compiler.ZeroExtendHalf(new AsmRegister(22), new AsmRegister(1)),
                    ExpectedRd: 22,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "SEXT.W",
                    "HybridCpuNonVmxScalarCompiler.SignExtendWord",
                    Instruction.SEXT_W,
                    compiler => compiler.SignExtendWord(new AsmRegister(23), new AsmRegister(1)),
                    ExpectedRd: 23,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedDataType: DataTypeEnum.INT32),
                new(
                    "ZEXT.W",
                    "HybridCpuNonVmxScalarCompiler.ZeroExtendWord",
                    Instruction.ZEXT_W,
                    compiler => compiler.ZeroExtendWord(new AsmRegister(24), new AsmRegister(1)),
                    ExpectedRd: 24,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedDataType: DataTypeEnum.INT32)
            ]);
    }
}
