using HybridCPU.Compiler.Core.API.Facade;
using MinimalAsmApp.Examples.Abstractions;
using YAKSys_Hybrid_CPU;

namespace MinimalAsmApp.Examples.InstructionClosure;

using Instruction = Processor.CPU_Core.InstructionsEnum;

public sealed class NonVmxBooleanInvertExample : ICpuExample
{
    public string Name => "non-vmx-boolean-invert";

    public string Description => "Emits ANDN, ORN, and XNOR through the open compiler boolean-invert helpers.";

    public string Category => "13_InstructionClosure";

    public CpuExampleResult Run()
    {
        return NonVmxCompilerExampleSupport.RunScalarCompilerExample(
            "Typed scalar compiler emitted canonical ANDN, ORN, and XNOR carriers.",
            [
                new(
                    "ANDN",
                    "HybridCpuNonVmxScalarCompiler.AndWithInvertedSecond",
                    Instruction.ANDN,
                    compiler => compiler.AndWithInvertedSecond(
                        new AsmRegister(11),
                        new AsmRegister(1),
                        new AsmRegister(2)),
                    ExpectedRd: 11,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "ORN",
                    "HybridCpuNonVmxScalarCompiler.OrWithInvertedSecond",
                    Instruction.ORN,
                    compiler => compiler.OrWithInvertedSecond(
                        new AsmRegister(12),
                        new AsmRegister(1),
                        new AsmRegister(2)),
                    ExpectedRd: 12,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "XNOR",
                    "HybridCpuNonVmxScalarCompiler.ExclusiveNor",
                    Instruction.XNOR,
                    compiler => compiler.ExclusiveNor(
                        new AsmRegister(13),
                        new AsmRegister(1),
                        new AsmRegister(2)),
                    ExpectedRd: 13,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64)
            ]);
    }
}
