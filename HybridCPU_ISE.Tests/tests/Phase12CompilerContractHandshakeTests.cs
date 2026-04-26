using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase12;

[CollectionDefinition("Phase12 Compiler Contract Handshake", DisableParallelization = true)]
public sealed class Phase12CompilerContractHandshakeCollection;

[Collection("Phase12 Compiler Contract Handshake")]
public sealed class Phase12CompilerContractHandshakeTests
{
    [Fact]
    public void LegacyCompilerBootstrap_DeclaresCurrentCompilerContractVersionOnBridge()
    {
        _ = new Processor(ProcessorMode.Compiler);

        Assert.True(Processor.Compiler.HasContractHandshake);
        Assert.Equal(CompilerContract.Version, Processor.Compiler.DeclaredCompilerContractVersion);
        Assert.Equal("Processor.InitializeLegacyCompatRuntime", Processor.Compiler.ContractHandshakeProducerSurface);
    }

    [Fact]
    public void LegacyCompilerBootstrap_RejectsDuplicateBridgeHandshakeDeclaration()
    {
        _ = new Processor(ProcessorMode.Compiler);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Processor.Compiler.DeclareCompilerContractVersion(
                CompilerContract.Version,
                "Phase12CompilerContractHandshakeTests.Duplicate"));

        Assert.Contains("already has compiler contract handshake", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanonicalCompiledProgram_PublishesCurrentCompilerContractVersion()
    {
        HybridCpuCompiledProgram program = CompileSingleInstructionProgram();

        Assert.Equal(CompilerContract.Version, program.ContractVersion);
    }

    [Fact]
    public void CanonicalCompiledProgram_PublishesAgreementSummary()
    {
        HybridCpuCompiledProgram program = CompileSingleInstructionProgram();

        Assert.NotNull(program.AdmissibilityAgreement);
        Assert.Equal(program.BundleCount, program.AdmissibilityAgreement.TotalBundleCount);
        Assert.True(program.AdmissibilityAgreement.AllBundlesAdmissible);
        Assert.True(program.AdmissibilityAgreement.AllTypedSlotFactsValid);
        Assert.Equal(0, program.AdmissibilityAgreement.StructuralAgreementFailureCount);
        Assert.Equal(0, program.AdmissibilityAgreement.TypedSlotInvalidBundleCount);
        Assert.Equal(program.BundleCount, program.AdmissibilityAgreement.TypedSlotEmittedBundleCount);
    }

    [Fact]
    public void CanonicalCompileProgress_EmitsAgreementSummarySignal()
    {
        var events = new List<(string Stage, string Message)>();
        HybridCpuThreadCompilerContext context = CreateSingleInstructionContext();

        _ = HybridCpuCanonicalCompiler.CompileProgram(
            virtualThreadId: 0,
            instructions: context.GetCompiledInstructions(),
            bundleAnnotations: context.GetBundleAnnotations(),
            progressObserver: (stage, message) => events.Add((stage, message)));

        List<(string Stage, string Message)> summaries =
            events.FindAll(e => e.Stage == "AgreementSummary");

        var summary = Assert.Single(summaries);
        Assert.Contains("agreement failures=0", summary.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("typed-slot valid", summary.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanonicalProgramEmission_PreservesAgreementSummary()
    {
        _ = new Processor(ProcessorMode.Emulation);
        HybridCpuCompiledProgram program = CompileSingleInstructionProgram();

        HybridCpuCompiledProgram emittedProgram = HybridCpuCanonicalCompiler.EmitProgram(program, baseAddress: 0);

        Assert.Same(program.AdmissibilityAgreement, emittedProgram.AdmissibilityAgreement);
        Assert.Equal(program.AdmissibilityAgreement.TotalBundleCount, emittedProgram.AdmissibilityAgreement.TotalBundleCount);
    }

    [Fact]
    public void CanonicalCompiledProgram_PropagatesThreadDomainTag_AndInvalidatesCachedArtifactWhenDomainChanges()
    {
        HybridCpuThreadCompilerContext context = CreateSingleInstructionContext();
        context.DomainTag = 0x10;

        HybridCpuCompiledProgram firstProgram = context.CompileProgram();
        IrInstruction firstInstruction = Assert.Single(firstProgram.ProgramSchedule.Program.Instructions);

        context.DomainTag = 0x20;
        HybridCpuCompiledProgram secondProgram = context.CompileProgram();
        IrInstruction secondInstruction = Assert.Single(secondProgram.ProgramSchedule.Program.Instructions);

        Assert.NotSame(firstProgram, secondProgram);
        Assert.Equal(0x10UL, firstInstruction.Annotation.DomainTag);
        Assert.Equal(0x20UL, secondInstruction.Annotation.DomainTag);
    }

    [Fact]
    public void CompilerResultStore_RejectsStaleCompiledProgramContractVersion()
    {
        HybridCpuCompiledProgram staleProgram = CreateProgramWithContractVersion(CompilerContract.Version - 1);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => CompilerResultStore.StoreResult(77, staleProgram));

        Assert.Contains("Compiler contract mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmitProgram_RejectsStaleCompiledProgramContractVersion()
    {
        _ = new Processor(ProcessorMode.Emulation);
        HybridCpuCompiledProgram staleProgram = CreateProgramWithContractVersion(CompilerContract.Version - 1);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => HybridCpuCanonicalCompiler.EmitProgram(staleProgram, baseAddress: 0));

        Assert.Contains("Compiler contract mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HybridCpuCompiledProgram CompileSingleInstructionProgram()
    {
        return CreateSingleInstructionContext().CompileProgram();
    }

    private static HybridCpuThreadCompilerContext CreateSingleInstructionContext()
    {
        var context = new HybridCpuThreadCompilerContext(0);
        context.CompileInstruction(
            opCode: (uint)InstructionsEnum.Addition,
            dataType: 0,
            predicate: 0xFF,
            immediate: 0,
            destSrc1: 0x1111,
            src2: 0x2222,
            streamLength: 0,
            stride: 0,
            stealabilityPolicy: StealabilityPolicy.Stealable);
        return context;
    }

    private static HybridCpuCompiledProgram CreateProgramWithContractVersion(int contractVersion)
    {
        HybridCpuCompiledProgram currentProgram = CompileSingleInstructionProgram();
        return new HybridCpuCompiledProgram(
            currentProgram.ProgramSchedule,
            currentProgram.BundleLayout,
            currentProgram.LoweredBundles,
            currentProgram.ProgramImage,
            contractVersion,
            currentProgram.EmissionBaseAddress,
            currentProgram.AdmissibilityAgreement);
    }
}
