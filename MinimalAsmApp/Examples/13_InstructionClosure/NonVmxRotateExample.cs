using HybridCPU.Compiler.Core.API.Facade;
using MinimalAsmApp.Examples.Abstractions;
using YAKSys_Hybrid_CPU;

namespace MinimalAsmApp.Examples.InstructionClosure;

using Instruction = Processor.CPU_Core.InstructionsEnum;

public sealed class NonVmxRotateExample : ICpuExample
{
    public string Name => "non-vmx-rotate";

    public string Description => "Emits ROL, ROR, ROLI, and RORI through the open compiler rotate helpers.";

    public string Category => "13_InstructionClosure";

    public CpuExampleResult Run()
    {
        return NonVmxCompilerExampleSupport.RunScalarCompilerExample(
            "Typed scalar compiler emitted canonical register and immediate rotate carriers.",
            [
                new(
                    "ROL",
                    "HybridCpuNonVmxScalarCompiler.RotateLeftRegister",
                    Instruction.ROL,
                    compiler => compiler.RotateLeftRegister(
                        new AsmRegister(7),
                        new AsmRegister(1),
                        new AsmRegister(2)),
                    ExpectedRd: 7,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "ROR",
                    "HybridCpuNonVmxScalarCompiler.RotateRightRegister",
                    Instruction.ROR,
                    compiler => compiler.RotateRightRegister(
                        new AsmRegister(8),
                        new AsmRegister(1),
                        new AsmRegister(2)),
                    ExpectedRd: 8,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "ROLI",
                    "HybridCpuNonVmxScalarCompiler.RotateLeftByImmediate",
                    Instruction.ROLI,
                    compiler => compiler.RotateLeftByImmediate(new AsmRegister(9), new AsmRegister(1), 5),
                    ExpectedRd: 9,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedImmediate: 5,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "RORI",
                    "HybridCpuNonVmxScalarCompiler.RotateRightByImmediate",
                    Instruction.RORI,
                    compiler => compiler.RotateRightByImmediate(new AsmRegister(10), new AsmRegister(1), 9),
                    ExpectedRd: 10,
                    ExpectedRs1: 1,
                    ExpectedRs2: 0,
                    ExpectedImmediate: 9,
                    ExpectedDataType: DataTypeEnum.UINT64)
            ]);
    }
}
