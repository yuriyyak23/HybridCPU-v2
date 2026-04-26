using System.Linq;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerParallelDecompositionCanonicalContourTests
{
    [Fact]
    public void ParallelForCompiler_SynthesizedWorkerBackEdge_UsesCanonicalUnsignedBranchContour()
    {
        var compiler = new ParallelForCompiler();

        ParallelCompilationResult? result = compiler.CompileParallelFor(
            iterationStart: 0,
            iterationEnd: 12,
            iterationStep: 1,
            bodyOpcodes: new[] { InstructionsEnum.Addition },
            inductionReg: 4,
            reduction: null);

        Assert.NotNull(result);

        foreach (IrProgram workerProgram in result!.WorkerPrograms)
        {
            Assert.Contains(workerProgram.Instructions, instruction => instruction.Opcode == InstructionsEnum.BLTU);
            Assert.DoesNotContain(workerProgram.Instructions, instruction => instruction.Opcode == InstructionsEnum.JumpIfBelow);
        }
    }

    [Fact]
    public void ParallelForCompiler_SynthesizedReductionPrograms_UseCanonicalScalarMemoryContours()
    {
        var compiler = new ParallelForCompiler();
        var reduction = new ReductionPlan(
            ReduceOpcode: InstructionsEnum.Addition,
            IdentityElement: 0,
            AccumulatorRegister: 20,
            PartialResultBaseAddress: 0x20000);

        ParallelCompilationResult? result = compiler.CompileParallelFor(
            iterationStart: 0,
            iterationEnd: 12,
            iterationStep: 1,
            bodyOpcodes: new[] { InstructionsEnum.Addition },
            inductionReg: 4,
            reduction: reduction);

        Assert.NotNull(result);

        Assert.Contains(result!.CoordinatorProgram.Instructions, instruction => instruction.Opcode == InstructionsEnum.LD);
        Assert.Contains(result.CoordinatorProgram.Instructions, instruction => instruction.Opcode == InstructionsEnum.SD);
        Assert.DoesNotContain(
            result.CoordinatorProgram.Instructions,
            instruction => instruction.Opcode is InstructionsEnum.Load or InstructionsEnum.Store);

        foreach (IrProgram workerProgram in result.WorkerPrograms)
        {
            Assert.Contains(workerProgram.Instructions, instruction => instruction.Opcode == InstructionsEnum.SD);
            Assert.DoesNotContain(workerProgram.Instructions, instruction => instruction.Opcode == InstructionsEnum.Store);
        }
    }

    [Fact]
    public void ParallelCompilationResult_OverallAgreement_IncludesWorkersAndCoordinator()
    {
        var compiler = new ParallelForCompiler();
        var reduction = new ReductionPlan(
            ReduceOpcode: InstructionsEnum.Addition,
            IdentityElement: 0,
            AccumulatorRegister: 20,
            PartialResultBaseAddress: 0x20000);

        ParallelCompilationResult? result = compiler.CompileParallelFor(
            iterationStart: 0,
            iterationEnd: 12,
            iterationStep: 1,
            bodyOpcodes: new[] { InstructionsEnum.Addition },
            inductionReg: 4,
            reduction: reduction);

        Assert.NotNull(result);

        int expectedTotalBundles =
            result!.WorkerAgreements.Sum(agreement => agreement.TotalBundleCount)
            + result.CoordinatorAgreement.TotalBundleCount;

        Assert.Equal(expectedTotalBundles, result.OverallAgreement.TotalBundleCount);
        Assert.Equal(result.AllTypedSlotFactsValid, result.OverallAgreement.AllTypedSlotFactsValid);
        Assert.Equal(result.AllProgramsAdmissible, result.OverallAgreement.AllBundlesAdmissible);
        Assert.True(result.AllWorkerTypedSlotFactsValid);
    }

    [Fact]
    public void ParallelCompilationResult_AllTypedSlotFactsValid_And_AllProgramsAdmissible_IncludeCoordinatorAgreement()
    {
        var compiler = new ParallelForCompiler();
        ParallelCompilationResult? maybeResult = compiler.CompileParallelFor(
            iterationStart: 0,
            iterationEnd: 12,
            iterationStep: 1,
            bodyOpcodes: new[] { InstructionsEnum.Addition },
            inductionReg: 4,
            reduction: null);
        Assert.NotNull(maybeResult);
        ParallelCompilationResult result = maybeResult!;

        IrAdmissibilityAgreement validWorkerAgreement = new(
        [
            new IrBundleAdmissionResult(
                BundleCycle: 0,
                Classification: AdmissibilityClassification.StructurallyAdmissible,
                SafetyMaskResult: default(SafetyMaskCompatibilityResult),
                StealVerdicts: [],
                TypedSlotFacts: default,
                TypedSlotFactsValid: true)
        ]);

        IrAdmissibilityAgreement invalidCoordinatorAgreement = new(
        [
            new IrBundleAdmissionResult(
                BundleCycle: 0,
                Classification: AdmissibilityClassification.TypedSlotFactsInvalid,
                SafetyMaskResult: default(SafetyMaskCompatibilityResult),
                StealVerdicts: [],
                TypedSlotFacts: default,
                TypedSlotFactsValid: false)
        ]);

        ParallelCompilationResult mutated = result with
        {
            WorkerAgreements = [validWorkerAgreement],
            CoordinatorAgreement = invalidCoordinatorAgreement
        };

        Assert.True(mutated.AllWorkersAdmissible);
        Assert.True(mutated.AllWorkerTypedSlotFactsValid);
        Assert.False(mutated.CoordinatorAgreement.AllBundlesAdmissible);
        Assert.False(mutated.CoordinatorAgreement.AllTypedSlotFactsValid);
        Assert.False(mutated.AllProgramsAdmissible);
        Assert.False(mutated.AllTypedSlotFactsValid);
        Assert.Equal(1, mutated.OverallAgreement.StructuralAgreementFailureCount);
        Assert.Equal(1, mutated.OverallAgreement.TypedSlotInvalidBundleCount);
    }

    [Theory]
    [InlineData(InstructionsEnum.JumpIfNotEqual, InstructionsEnum.BNE, 3, 4, 3, 4)]
    [InlineData(InstructionsEnum.JumpIfBelowOrEqual, InstructionsEnum.BGEU, 5, 6, 6, 5)]
    [InlineData(InstructionsEnum.JumpIfAbove, InstructionsEnum.BLTU, 7, 8, 8, 7)]
    public void WorkerFunctionSynthesizer_SynthesizeWorkerFromInstructions_CanonicalizesRetainedConditionalWrappers(
        InstructionsEnum legacyOpcode,
        InstructionsEnum expectedOpcode,
        byte rs1,
        byte rs2,
        ulong expectedRs1,
        ulong expectedRs2)
    {
        var synthesizer = new WorkerFunctionSynthesizer();
        ulong packedRegisters = VLIW_Instruction.PackArchRegs(0, rs1, rs2);

        IrProgram program = synthesizer.SynthesizeWorkerFromInstructions(
            workerVtId: 1,
            instructions:
            [
                (legacyOpcode, packedRegisters, 0x4000UL, (ushort)0x20)
            ]);

        IrInstruction instruction = Assert.Single(program.Instructions);

        Assert.Equal(expectedOpcode, instruction.Opcode);
        Assert.Contains(instruction.Annotation.Uses, operand => operand.Name == "rs1" && operand.Value == expectedRs1);
        Assert.Contains(instruction.Annotation.Uses, operand => operand.Name == "rs2" && operand.Value == expectedRs2);
    }
}

