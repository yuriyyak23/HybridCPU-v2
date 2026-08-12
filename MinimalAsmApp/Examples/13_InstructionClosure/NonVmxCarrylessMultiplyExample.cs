using HybridCPU.Compiler.Core.API.Facade;
using MinimalAsmApp.Examples.Abstractions;
using YAKSys_Hybrid_CPU;

namespace MinimalAsmApp.Examples.InstructionClosure;

using Instruction = Processor.CPU_Core.InstructionsEnum;

public sealed class NonVmxCarrylessMultiplyExample : ICpuExample
{
    public string Name => "non-vmx-carryless-multiply";

    public string Description => "Emits CLMUL, CLMULH, and CLMULR through the open compiler carry-less multiply helpers.";

    public string Category => "13_InstructionClosure";

    public CpuExampleResult Run()
    {
        return NonVmxCompilerExampleSupport.RunScalarCompilerExample(
            "Typed scalar compiler emitted canonical carry-less multiply carriers.",
            [
                new(
                    "CLMUL",
                    "HybridCpuNonVmxScalarCompiler.BinaryPolynomialProductLow",
                    Instruction.CLMUL,
                    compiler => compiler.BinaryPolynomialProductLow(new AsmRegister(18), new AsmRegister(1), new AsmRegister(2)),
                    ExpectedRd: 18,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "CLMULH",
                    "HybridCpuNonVmxScalarCompiler.BinaryPolynomialProductHigh",
                    Instruction.CLMULH,
                    compiler => compiler.BinaryPolynomialProductHigh(new AsmRegister(19), new AsmRegister(1), new AsmRegister(2)),
                    ExpectedRd: 19,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64),
                new(
                    "CLMULR",
                    "HybridCpuNonVmxScalarCompiler.BinaryPolynomialProductReverse",
                    Instruction.CLMULR,
                    compiler => compiler.BinaryPolynomialProductReverse(new AsmRegister(20), new AsmRegister(1), new AsmRegister(2)),
                    ExpectedRd: 20,
                    ExpectedRs1: 1,
                    ExpectedRs2: 2,
                    ExpectedDataType: DataTypeEnum.UINT64)
            ]);
    }
}
