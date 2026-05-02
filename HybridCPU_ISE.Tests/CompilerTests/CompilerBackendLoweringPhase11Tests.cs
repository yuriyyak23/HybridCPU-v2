using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Fences;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerBackendLoweringPhase11Tests
{
    [Theory]
    [InlineData(CompilerBackendCapabilityState.Unavailable, false, false, false, false, false)]
    [InlineData(CompilerBackendCapabilityState.DescriptorOnly, true, true, false, false, false)]
    [InlineData(CompilerBackendCapabilityState.ParserOnly, true, false, true, false, false)]
    [InlineData(CompilerBackendCapabilityState.ModelOnly, true, false, false, true, false)]
    [InlineData(CompilerBackendCapabilityState.ExecutableExperimental, true, true, true, false, false)]
    [InlineData(CompilerBackendCapabilityState.ProductionExecutable, true, true, true, false, true)]
    public void Phase11_CapabilityStatesDefineSelectionAndProductionBoundaries(
        CompilerBackendCapabilityState state,
        bool canSelectFeature,
        bool allowsDescriptorEvidence,
        bool allowsParserValidation,
        bool allowsModelOrTestHelper,
        bool canProductionLower)
    {
        Assert.Contains(state, CompilerBackendLoweringContract.CapabilityStates);
        Assert.Equal(canSelectFeature, CompilerBackendLoweringContract.CanSelectFeature(state));
        Assert.Equal(allowsDescriptorEvidence, CompilerBackendLoweringContract.AllowsDescriptorEvidence(state));
        Assert.Equal(allowsParserValidation, CompilerBackendLoweringContract.AllowsParserValidation(state));
        Assert.Equal(allowsModelOrTestHelper, CompilerBackendLoweringContract.AllowsModelOrTestHelper(state));
        Assert.Equal(canProductionLower, CompilerBackendLoweringContract.CanSelectForProductionLowering(state));
    }

    [Theory]
    [InlineData(CompilerBackendCapabilityState.Unavailable)]
    [InlineData(CompilerBackendCapabilityState.DescriptorOnly)]
    [InlineData(CompilerBackendCapabilityState.ParserOnly)]
    [InlineData(CompilerBackendCapabilityState.ModelOnly)]
    [InlineData(CompilerBackendCapabilityState.ExecutableExperimental)]
    public void Phase11_NonProductionStatesRejectProductionLoweringEvenWithFutureBits(
        CompilerBackendCapabilityState state)
    {
        CompilerBackendLoweringDecision dscDecision =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = state,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureDscRequiredRequirements
                });
        CompilerBackendLoweringDecision l7Decision =
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
                    State = state,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureL7RequiredRequirements
                });

        Assert.False(dscDecision.IsAllowed);
        Assert.False(l7Decision.IsAllowed);
        Assert.Contains("ProductionExecutable", dscDecision.Reason, StringComparison.Ordinal);
        Assert.Contains("ProductionExecutable", l7Decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase11_DscDescriptorCarrierEmissionIsPreservationNotProductionExecution()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();

        CompilerBackendLoweringDecision productionDecision =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.DescriptorOnly,
                    UsesDescriptorEvidenceOnly = true
                });

        Assert.False(productionDecision.IsAllowed);
        Assert.Contains("ProductionExecutable", productionDecision.Reason, StringComparison.Ordinal);

        var context = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        context.CompileDmaStreamCompute(descriptor);
        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(6);
        Assert.Equal(InstructionsEnum.DmaStreamCompute, (InstructionsEnum)lowered.OpCode);
        Assert.True(compiledProgram.LoweredBundleAnnotations[0].TryGetInstructionSlotMetadata(
            6,
            out InstructionSlotMetadata metadata));
        Assert.Same(descriptor, metadata.DmaStreamComputeDescriptor);
        Assert.Null(metadata.AcceleratorCommandDescriptor);
        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);

        MicroOp projected = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            compiledProgram.LoweredBundleAnnotations[0],
            slotIndex: 6);
        DmaStreamComputeMicroOp carrier = Assert.IsType<DmaStreamComputeMicroOp>(projected);
        Assert.False(carrier.WritesRegister);
        Assert.Empty(carrier.WriteRegisters);

        var core = new Processor.CPU_Core(0);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => carrier.Execute(ref core));
        Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase11_L7CarrierEmissionIsDescriptorSidebandNotProductionBackendProtocol()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();

        CompilerBackendLoweringDecision productionDecision =
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
                    State = CompilerBackendCapabilityState.DescriptorOnly,
                    UsesDescriptorEvidenceOnly = true
                });

        Assert.False(productionDecision.IsAllowed);
        Assert.Contains("ProductionExecutable", productionDecision.Reason, StringComparison.Ordinal);

        HybridCpuThreadCompilerContext context =
            L7SdcCompilerEmissionTests.CreateContextForDescriptor(descriptor);
        CompilerAcceleratorLoweringDecision carrierDecision =
            context.CompileAcceleratorSubmit(
                IrAcceleratorIntent.ForMatMul(descriptor, tokenDestinationRegister: 9),
                CompilerAcceleratorCapabilityModel.ReferenceMatMul);
        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();

        Assert.True(carrierDecision.EmitsAcceleratorSubmit);
        Assert.Contains("ACCEL_SUBMIT emission", carrierDecision.Reason, StringComparison.Ordinal);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(7);
        Assert.Equal(InstructionsEnum.ACCEL_SUBMIT, (InstructionsEnum)lowered.OpCode);
        Assert.True(compiledProgram.LoweredBundleAnnotations[0].TryGetInstructionSlotMetadata(
            7,
            out InstructionSlotMetadata metadata));
        Assert.Same(descriptor, metadata.AcceleratorCommandDescriptor);
        Assert.Null(metadata.DmaStreamComputeDescriptor);

        MicroOp projected = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            compiledProgram.LoweredBundleAnnotations[0],
            slotIndex: 7);
        AcceleratorSubmitMicroOp carrier = Assert.IsType<AcceleratorSubmitMicroOp>(projected);
        Assert.False(carrier.WritesRegister);
        Assert.Empty(carrier.WriteRegisters);

        var core = new Processor.CPU_Core(0);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => carrier.Execute(ref core));
        Assert.Contains("direct execution is unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fallback routing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase11_ParserAvailabilityDoesNotImplyExecutionOrProductionLowering()
    {
        byte[] descriptorBytes = DmaStreamComputeTestDescriptorFactory.BuildDescriptor();
        DmaStreamComputeDescriptorReference reference =
            DmaStreamComputeTestDescriptorFactory.CreateReference(descriptorBytes);
        DmaStreamComputeOwnerGuardDecision guardDecision =
            DmaStreamComputeTestDescriptorFactory.CreateGuardDecision(descriptorBytes, reference);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                guardDecision,
                reference);

        Assert.True(result.IsValid, result.Message);
        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);

        CompilerBackendLoweringDecision productionDecision =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.ParserOnly,
                    UsesParserValidationOnly = true
                });

        Assert.False(productionDecision.IsAllowed);
        Assert.Contains("ParserOnly", productionDecision.Reason, StringComparison.Ordinal);
        Assert.Contains("non-production", productionDecision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase11_ModelAndTestHelperSurfacesAreNotProductionBackends()
    {
        var fakeBackend = new FakeMatMulExternalAcceleratorBackend();
        Assert.True(fakeBackend.IsTestOnly);

        CompilerBackendLoweringDecision dscDecision =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureDscRequiredRequirements,
                    UsesModelOrTestHelper = true
                });
        CompilerBackendLoweringDecision l7Decision =
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureL7RequiredRequirements,
                    UsesModelOrTestHelper = true
                });

        Assert.False(dscDecision.IsAllowed);
        Assert.False(l7Decision.IsAllowed);
        Assert.Contains("model or test helper", dscDecision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("model or test helper", l7Decision.Reason, StringComparison.OrdinalIgnoreCase);

        string compilerSource = ReadAllSourceText(Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_Compiler"));
        Assert.DoesNotContain("DmaStreamComputeRuntime", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorTokenStore), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorCommandQueue), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorFenceCoordinator), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorRegisterAbi), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(IExternalAcceleratorBackend), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FakeMatMulExternalAcceleratorBackend), compilerSource, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(MissingDscCapabilityCases))]
    public void Phase11_FutureDscProductionLoweringRejectsAbsentRequiredCapabilities(
        CompilerBackendLoweringRequirement missingRequirement)
    {
        CompilerBackendLoweringDecision decision =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements =
                        CompilerBackendLoweringContract.FutureDscRequiredRequirements & ~missingRequirement
                });

        Assert.False(decision.IsAllowed);
        Assert.True((decision.MissingRequirements & missingRequirement) != 0);
    }

    [Theory]
    [MemberData(nameof(MissingL7CapabilityCases))]
    public void Phase11_FutureL7ProductionLoweringRejectsAbsentRequiredCapabilities(
        CompilerBackendLoweringRequirement missingRequirement)
    {
        CompilerBackendLoweringDecision decision =
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements =
                        CompilerBackendLoweringContract.FutureL7RequiredRequirements & ~missingRequirement
                });

        Assert.False(decision.IsAllowed);
        Assert.True((decision.MissingRequirements & missingRequirement) != 0);
    }

    [Fact]
    public void Phase11_CompilerBackendRejectsHardwareCoherenceAndPartialSuccessAssumptions()
    {
        CompilerBackendLoweringDecision dscCoherence =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureDscRequiredRequirements,
                    AssumesHardwareCoherence = true
                });
        CompilerBackendLoweringDecision dscPartial =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureDscRequiredRequirements,
                    AssumesSuccessfulPartialCompletion = true
                });
        CompilerBackendLoweringDecision l7Coherence =
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureL7RequiredRequirements,
                    AssumesHardwareCoherence = true
                });
        CompilerBackendLoweringDecision l7Partial =
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureL7RequiredRequirements,
                    AssumesSuccessfulPartialCompletion = true
                });

        Assert.False(dscCoherence.IsAllowed);
        Assert.Contains("coherence", dscCoherence.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(dscPartial.IsAllowed);
        Assert.Contains("partial completion", dscPartial.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(l7Coherence.IsAllowed);
        Assert.Contains("coherence", l7Coherence.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(l7Partial.IsAllowed);
        Assert.Contains("partial completion", l7Partial.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase11_DocumentationClaimSafetyKeepsCurrentProductionExecutableLoweringForbidden()
    {
        string phase11 = ReadRepoFile("Documentation/Refactoring/Phases Ex1/11_Compiler_Backend_Lowering_Contract.md");
        string adr11 = ReadRepoFile("Documentation/Refactoring/Phases Ex1/ADR_11_Compiler_Backend_Lowering_Contract.md");
        string combined = phase11 + Environment.NewLine + adr11;

        Assert.Contains("No production executable lowering under current contract.", phase11, StringComparison.Ordinal);
        Assert.Contains("This ADR does not approve compiler/backend production lowering", adr11, StringComparison.Ordinal);
        Assert.Contains("Only `ProductionExecutable` may be used for production lowering.", adr11, StringComparison.Ordinal);
        Assert.Contains("Unavailable", combined, StringComparison.Ordinal);
        Assert.Contains("DescriptorOnly", combined, StringComparison.Ordinal);
        Assert.Contains("ParserOnly", combined, StringComparison.Ordinal);
        Assert.Contains("ModelOnly", combined, StringComparison.Ordinal);
        Assert.Contains("ExecutableExperimental", combined, StringComparison.Ordinal);
        Assert.Contains("ProductionExecutable", combined, StringComparison.Ordinal);

        Assert.DoesNotContain("current production executable lowering is allowed", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("current lowering may depend on execution", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fake/test L7 backend is production protocol", combined, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> MissingDscCapabilityCases()
    {
        yield return new object[] { CompilerBackendLoweringRequirement.ExecutableCarrier };
        yield return new object[] { CompilerBackendLoweringRequirement.BackendAddressSpace };
        yield return new object[] { CompilerBackendLoweringRequirement.OrderCacheFaultContract };
        yield return new object[] { CompilerBackendLoweringRequirement.AllOrNoneRetirePublication };
        yield return new object[] { CompilerBackendLoweringRequirement.StagedCommitBoundary };
    }

    public static IEnumerable<object[]> MissingL7CapabilityCases()
    {
        yield return new object[] { CompilerBackendLoweringRequirement.ExecutableCarrier };
        yield return new object[] { CompilerBackendLoweringRequirement.ResultPublication };
        yield return new object[] { CompilerBackendLoweringRequirement.ProductionBackendProtocol };
        yield return new object[] { CompilerBackendLoweringRequirement.QueueTokenFenceContract };
        yield return new object[] { CompilerBackendLoweringRequirement.OrderCacheFaultContract };
        yield return new object[] { CompilerBackendLoweringRequirement.StagedCommitBoundary };
    }

    private static string ReadRepoFile(string relativePath)
    {
        string fullPath = Path.Combine(
            CompatFreezeScanner.FindRepoRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Missing repository file: {relativePath}");
        return File.ReadAllText(fullPath);
    }

    private static string ReadAllSourceText(string directory)
    {
        Assert.True(Directory.Exists(directory), $"Missing source directory: {directory}");
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !CompatFreezeScanner.IsGeneratedPath(path))
                .Select(File.ReadAllText));
    }
}
