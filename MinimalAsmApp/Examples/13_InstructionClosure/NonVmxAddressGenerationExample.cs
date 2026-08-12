using HybridCPU.Compiler.Core.API.Facade;
using MinimalAsmApp.Examples.Abstractions;
using YAKSys_Hybrid_CPU;

namespace MinimalAsmApp.Examples.InstructionClosure;

using Instruction = Processor.CPU_Core.InstructionsEnum;

public sealed class NonVmxAddressGenerationExample : ICpuExample
{
    public string Name => "non-vmx-address-generation";

    public string Description => "Emits SHxADD, ADD.UW, SHxADD.UW, and SLLI.UW through open address-generation helpers.";

    public string Category => "13_InstructionClosure";

    public CpuExampleResult Run()
    {
        return NonVmxCompilerExampleSupport.RunScalarCompilerExample(
            "Typed scalar compiler emitted canonical scalar address-generation carriers.",
            [
                new(
                    "SH1ADD",
                    "HybridCpuNonVmxScalarCompiler.ShiftLeftOneAndAdd",
                    Instruction.SH1ADD,
                    compiler => compiler.ShiftLeftOneAndAdd(new AsmRegister(10), new AsmRegister(1), new AsmRegister(2)),
                    ExpectedRd: 10,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "SH2ADD",
                    "HybridCpuNonVmxScalarCompiler.ShiftLeftTwoAndAdd",
                    Instruction.SH2ADD,
                    compiler => compiler.ShiftLeftTwoAndAdd(new AsmRegister(11), new AsmRegister(1), new AsmRegister(2)),
                    ExpectedRd: 11,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "SH3ADD",
                    "HybridCpuNonVmxScalarCompiler.ShiftLeftThreeAndAdd",
                    Instruction.SH3ADD,
                    compiler => compiler.ShiftLeftThreeAndAdd(new AsmRegister(12), new AsmRegister(1), new AsmRegister(2)),
                    ExpectedRd: 12,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "ADD.UW",
                    "HybridCpuNonVmxScalarCompiler.AddUnsignedWord",
                    Instruction.ADD_UW,
                    compiler => compiler.AddUnsignedWord(new AsmRegister(13), new AsmRegister(1), new AsmRegister(2)),
                    ExpectedRd: 13,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "SH1ADD.UW",
                    "HybridCpuNonVmxScalarCompiler.ShiftLeftOneAndAddUnsignedWord",
                    Instruction.SH1ADD_UW,
                    compiler => compiler.ShiftLeftOneAndAddUnsignedWord(new AsmRegister(14), new AsmRegister(1), new AsmRegister(2)),
                    ExpectedRd: 14,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "SH2ADD.UW",
                    "HybridCpuNonVmxScalarCompiler.ShiftLeftTwoAndAddUnsignedWord",
                    Instruction.SH2ADD_UW,
                    compiler => compiler.ShiftLeftTwoAndAddUnsignedWord(new AsmRegister(15), new AsmRegister(1), new AsmRegister(2)),
                    ExpectedRd: 15,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "SH3ADD.UW",
                    "HybridCpuNonVmxScalarCompiler.ShiftLeftThreeAndAddUnsignedWord",
                    Instruction.SH3ADD_UW,
                    compiler => compiler.ShiftLeftThreeAndAddUnsignedWord(new AsmRegister(16), new AsmRegister(1), new AsmRegister(2)),
                    ExpectedRd: 16,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "SLLI.UW",
                    "HybridCpuNonVmxScalarCompiler.ShiftLeftUnsignedWordByImmediate",
                    Instruction.SLLI_UW,
                    compiler => compiler.ShiftLeftUnsignedWordByImmediate(new AsmRegister(17), new AsmRegister(1), 4),
                    ExpectedRd: 17,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedImmediate: 4,
                    ExpectedDataType: DataTypeEnum.UINT64)
            ]);
    }
}
