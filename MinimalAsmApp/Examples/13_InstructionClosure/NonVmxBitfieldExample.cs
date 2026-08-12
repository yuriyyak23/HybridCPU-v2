using HybridCPU.Compiler.Core.API.Facade;
using MinimalAsmApp.Examples.Abstractions;
using YAKSys_Hybrid_CPU;

namespace MinimalAsmApp.Examples.InstructionClosure;

using Instruction = Processor.CPU_Core.InstructionsEnum;

public sealed class NonVmxBitfieldExample : ICpuExample
{
    public string Name => "non-vmx-bitfield";

    public string Description => "Emits register and immediate BSET/BCLR/BINV/BEXT helpers through compiler bitfield support.";

    public string Category => "13_InstructionClosure";

    public CpuExampleResult Run()
    {
        return NonVmxCompilerExampleSupport.RunScalarCompilerExample(
            "Typed scalar compiler emitted canonical register and immediate bitfield carriers.",
            [
                new(
                    "BSET",
                    "HybridCpuNonVmxScalarCompiler.SetBitRegister",
                    Instruction.BSET,
                    compiler => compiler.SetBitRegister(new AsmRegister(2), new AsmRegister(1), new AsmRegister(3)),
                    ExpectedRd: 2,
                    ExpectedRs1: 1,
                    ExpectedRs2: 3,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "BCLR",
                    "HybridCpuNonVmxScalarCompiler.ClearBitRegister",
                    Instruction.BCLR,
                    compiler => compiler.ClearBitRegister(new AsmRegister(3), new AsmRegister(1), new AsmRegister(4)),
                    ExpectedRd: 3,
                    ExpectedRs1: 1,
                    ExpectedRs2: 4,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "BINV",
                    "HybridCpuNonVmxScalarCompiler.InvertBitRegister",
                    Instruction.BINV,
                    compiler => compiler.InvertBitRegister(new AsmRegister(4), new AsmRegister(1), new AsmRegister(5)),
                    ExpectedRd: 4,
                    ExpectedRs1: 1,
                    ExpectedRs2: 5,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "BEXT",
                    "HybridCpuNonVmxScalarCompiler.ExtractBitRegister",
                    Instruction.BEXT,
                    compiler => compiler.ExtractBitRegister(new AsmRegister(5), new AsmRegister(1), new AsmRegister(6)),
                    ExpectedRd: 5,
                    ExpectedRs1: 1,
                    ExpectedRs2: 6,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "BSETI",
                    "HybridCpuNonVmxScalarCompiler.SetBitImmediate",
                    Instruction.BSETI,
                    compiler => compiler.SetBitImmediate(new AsmRegister(6), new AsmRegister(1), 3),
                    ExpectedRd: 6,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedImmediate: 3,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "BCLRI",
                    "HybridCpuNonVmxScalarCompiler.ClearBitImmediate",
                    Instruction.BCLRI,
                    compiler => compiler.ClearBitImmediate(new AsmRegister(7), new AsmRegister(1), 5),
                    ExpectedRd: 7,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedImmediate: 5,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "BINVI",
                    "HybridCpuNonVmxScalarCompiler.InvertBitImmediate",
                    Instruction.BINVI,
                    compiler => compiler.InvertBitImmediate(new AsmRegister(8), new AsmRegister(1), 7),
                    ExpectedRd: 8,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedImmediate: 7,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "BEXTI",
                    "HybridCpuNonVmxScalarCompiler.ExtractBitImmediate",
                    Instruction.BEXTI,
                    compiler => compiler.ExtractBitImmediate(new AsmRegister(9), new AsmRegister(1), 11),
                    ExpectedRd: 9,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedImmediate: 11,
                    ExpectedDataType: DataTypeEnum.UINT64)
            ]);
    }
}
