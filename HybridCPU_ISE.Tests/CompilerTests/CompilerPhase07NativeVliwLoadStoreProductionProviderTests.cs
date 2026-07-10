using System;
using System.Collections.Generic;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.IR.Artifacts;
using HybridCPU.Compiler.Core.IR.Authority;
using HybridCPU.Compiler.Core.IR.Contours;
using HybridCPU.Compiler.Core.IR.Intent;
using HybridCPU.Compiler.Core.IR.Lowering;
using HybridCPU.Compiler.Core.IR.Lowering.Production;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.CompilerTests;

/// <summary>
/// Phase 07 tests for the explicit native LSU production provider. Memory
/// publication, ordering, fault resolution, commit, and retire stay runtime-owned.
/// </summary>
public sealed class CompilerPhase07NativeVliwLoadStoreProductionProviderTests
{
    [Theory]
    [InlineData(InstructionsEnum.LB, 0x100)]
    [InlineData(InstructionsEnum.LH, 0x100)]
    [InlineData(InstructionsEnum.LW, 0x100)]
    [InlineData(InstructionsEnum.LD, 0x100)]
    [InlineData(InstructionsEnum.SB, 0x100)]
    [InlineData(InstructionsEnum.SH, 0x100)]
    [InlineData(InstructionsEnum.SW, 0x100)]
    [InlineData(InstructionsEnum.SD, 0x100)]
    public void SupportedLoadStoreCarriersProduceRuntimePendingPackages(
        InstructionsEnum opcode,
        ulong address)
    {
        CompilerEmissionPackage candidate = CreateLoadStorePackage(opcode, address);
        CompilerSemanticIntent intent = CreateLoadStoreIntent(opcode);
        CompilerProductionLoweringContext context = CreateProductionContext(candidate);
        CompilerProductionLoweringResult result =
            NativeVliwLoadStoreProductionProvider.Instance.TryProduce(
                intent,
                Analyze(intent),
                context);

        Assert.True(
            result.ResultKind == CompilerProductionLoweringResultKind.RuntimeAuthorityPending,
            $"Phase 07 provider rejected {opcode}: {result.Reason}");
        Assert.True(result.GateResult.IsSatisfied);
        Assert.NotNull(result.Package);
        Assert.Equal(
            "NativeVliwLoadStoreProductionProvider",
            result.Package!.Identity.ProducerSurface);
        Assert.Equal(
            candidate.Carrier!.Image.SerializedImage,
            result.Package.Carrier!.Image.SerializedImage);
        Assert.Equal(FallbackPolicyKind.Forbidden, result.NoFallbackProof.PolicyKind);
        Assert.Contains("contour=NativeVliwLoadStore", result.TelemetryEvidence);
        Assert.Contains("memory-order-and-fault=runtime-owned", result.TelemetryEvidence);
        Assert.Contains(
            result.TelemetryEvidence,
            entry => entry.StartsWith("artifact-id=", StringComparison.Ordinal));

        CompilerToIseParitySnapshot parity =
            CompilerToIseParityHarness.AssertContourAndOpcode(
                result.Package,
                ExecutionContourKind.NativeVliwLoadStore,
                opcode);
        Assert.Equal(ExpectedGoldenHash(opcode), parity.CarrierBytesHash);
        Assert.Equal(parity.CarrierBytesHash, parity.ReencodedBytesHash);
        CompilerToIseParityHarness.AssertRuntimeAuthorityPending(result.Package);
    }

    [Fact]
    public void ExplicitLoadStoreProfileResolvesProviderButCompatibilityProfileDoesNot()
    {
        CompilerEmissionPackage candidate = CreateLoadStorePackage(InstructionsEnum.LW, 0x100);
        CompilerProductionLoweringContext compatibility =
            CreateProductionContext(candidate) with
            {
                ProductionProfile = CreateProfile(
                    CompilerProductionLoweringProfileMode.CompatibilityOnly)
            };

        Assert.Null(
            DefaultContourLoweringProviderRegistry.Instance.ResolveProductionProvider(
                ExecutionContourKind.NativeVliwLoadStore,
                compatibility));

        CompilerProductionLoweringContext explicitProfile = CreateProductionContext(candidate);
        Assert.Same(
            NativeVliwLoadStoreProductionProvider.Instance,
            DefaultContourLoweringProviderRegistry.Instance.ResolveProductionProvider(
                ExecutionContourKind.NativeVliwLoadStore,
                explicitProfile));
    }

    [Fact]
    public void MissingMemoryOrderingAndFaultGateRemainsFutureGated()
    {
        CompilerEmissionPackage candidate = CreateLoadStorePackage(InstructionsEnum.LW, 0x100);
        CompilerSemanticIntent intent = CreateLoadStoreIntent(InstructionsEnum.LW);
        CompilerProductionLoweringContext context = CreateProductionContext(candidate) with
        {
            Readiness = CompilerProductionLoweringReadiness.Complete
        };

        CompilerProductionLoweringResult result =
            NativeVliwLoadStoreProductionProvider.Instance.TryProduce(
                intent,
                Analyze(intent),
                context);

        Assert.Equal(CompilerProductionLoweringResultKind.FutureGated, result.ResultKind);
        Assert.Null(result.Package);
        Assert.Contains(
            CompilerProductionLoweringGateIds.MemoryFaultRuntimeDependency,
            result.GateResult.MissingGates);
        Assert.Equal(FallbackPolicyKind.Forbidden, result.NoFallbackProof.PolicyKind);
    }

    [Fact]
    public void ZeroAddressFailsClosedBeforeProductionPackage()
    {
        CompilerEmissionPackage candidate = CreateLoadStorePackage(InstructionsEnum.LW, 0);
        CompilerSemanticIntent intent = CreateLoadStoreIntent(InstructionsEnum.LW);

        CompilerProductionLoweringResult result =
            NativeVliwLoadStoreProductionProvider.Instance.TryProduce(
                intent,
                Analyze(intent),
                CreateProductionContext(candidate));

        Assert.Equal(CompilerProductionLoweringResultKind.Rejected, result.ResultKind);
        Assert.Null(result.Package);
        Assert.Contains("explicit 64-bit memory address", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StreamOrIndexedFieldsFailClosedBeforeProductionPackage()
    {
        var compiler = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        compiler.CompileInstruction(
            (uint)InstructionsEnum.LW,
            (byte)DataTypeEnum.INT32,
            predicate: 0,
            immediate: 0,
            destSrc1: VLIW_Instruction.PackArchRegs(9, 1, 0),
            src2: 0x100,
            streamLength: 1,
            stride: 0,
            StealabilityPolicy.NotStealable);
        CompilerEmissionPackage candidate = Project(compiler);
        CompilerSemanticIntent intent = CreateLoadStoreIntent(InstructionsEnum.LW);

        CompilerProductionLoweringResult result =
            NativeVliwLoadStoreProductionProvider.Instance.TryProduce(
                intent,
                Analyze(intent),
                CreateProductionContext(candidate));

        Assert.Equal(CompilerProductionLoweringResultKind.Rejected, result.ResultKind);
        Assert.Null(result.Package);
        Assert.Contains("stream/indexed/2D", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SemanticIntentKind.ScalarAlu, ExecutionContourKind.NativeVliwScalar)]
    [InlineData(SemanticIntentKind.BranchControl, ExecutionContourKind.NativeVliwBranchControl)]
    [InlineData(SemanticIntentKind.VectorStream, ExecutionContourKind.StreamEngineVector)]
    [InlineData(SemanticIntentKind.MatrixTile, ExecutionContourKind.MatrixTileHelperOnly)]
    [InlineData(SemanticIntentKind.DmaStreamCompute, ExecutionContourKind.DmaStreamComputeLane6)]
    [InlineData(SemanticIntentKind.ExternalAcceleratorCommand, ExecutionContourKind.L7SdcLane7)]
    public void NonLsuContoursAreRejectedWithoutFallback(
        SemanticIntentKind intentKind,
        ExecutionContourKind contourKind)
    {
        CompilerEmissionPackage candidate = CreateLoadStorePackage(InstructionsEnum.LW, 0x100);
        CompilerSemanticIntent intent = CreateNonLoadStoreIntent(intentKind);
        CompilerProductionLoweringResult result =
            NativeVliwLoadStoreProductionProvider.Instance.TryProduce(
                intent,
                DefaultContourLoweringProviderRegistry.Instance
                    .ResolveAnalyzer(contourKind)
                    .Analyze(
                        intent,
                        new CompilerLoweringContext(
                            new CompilerTargetProfile(
                                "phase07-negative-analysis",
                                AllowsCarrierEmission: true,
                                AllowsBackendEmission: true),
                            "CompilerPhase07NativeVliwLoadStoreProductionProviderTests")),
                CreateProductionContext(candidate));

        Assert.Equal(CompilerProductionLoweringResultKind.Rejected, result.ResultKind);
        Assert.Null(result.Package);
        Assert.Equal(FallbackPolicyKind.Forbidden, result.NoFallbackProof.PolicyKind);
        Assert.Contains("cross-contour", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static CompilerProductionLoweringContext CreateProductionContext(
        CompilerEmissionPackage candidate) =>
        new(
            new CompilerTargetProfile(
                "phase07-native-vliw-load-store-production",
                AllowsCarrierEmission: true,
                AllowsBackendEmission: true),
            "CompilerPhase07NativeVliwLoadStoreProductionProviderTests",
            CreateProfile(CompilerProductionLoweringProfileMode.ExplicitlyEnabled))
        {
            CandidatePackage = candidate,
            Readiness = CompilerProductionLoweringReadiness.CompleteLoadStore
        };

    private static CompilerProductionLoweringProfile CreateProfile(
        CompilerProductionLoweringProfileMode mode) =>
        new(
            "phase07-native-vliw-load-store-profile",
            mode,
            new HashSet<ExecutionContourKind> { ExecutionContourKind.NativeVliwLoadStore },
            CompilerProductionLoweringGateIds.AllFor(ExecutionContourKind.NativeVliwLoadStore));

    private static ContourAnalysisReport Analyze(CompilerSemanticIntent intent) =>
        DefaultContourLoweringProviderRegistry.Instance
            .ResolveAnalyzer(ExecutionContourKind.NativeVliwLoadStore)
            .Analyze(
                intent,
                new CompilerLoweringContext(
                    new CompilerTargetProfile(
                        "phase07-load-store-analysis",
                        AllowsCarrierEmission: true,
                        AllowsBackendEmission: true),
                    "CompilerPhase07NativeVliwLoadStoreProductionProviderTests"));

    private static CompilerSemanticIntent CreateLoadStoreIntent(InstructionsEnum opcode) =>
        new(
            SemanticIntentKind.LoadStore,
            opcode.ToString(),
            RequiresDescriptor: false,
            RequiresSideband: false,
            RequiresToken: false,
            RequiresRuntimeLegality: true,
            IsCompatibilityProjection: false,
            IsPolicyAdmissionOnly: false,
            IsHelperAbiOnly: false,
            IsParserOnly: false,
            "Phase 07 explicit native LSU production intent.");

    private static CompilerSemanticIntent CreateNonLoadStoreIntent(
        SemanticIntentKind intentKind) =>
        new(
            intentKind,
            intentKind.ToString(),
            RequiresDescriptor: intentKind is
                SemanticIntentKind.DmaStreamCompute or
                SemanticIntentKind.ExternalAcceleratorCommand,
            RequiresSideband: intentKind is
                SemanticIntentKind.VectorStream or
                SemanticIntentKind.MatrixTile or
                SemanticIntentKind.DmaStreamCompute or
                SemanticIntentKind.ExternalAcceleratorCommand,
            RequiresToken: intentKind is
                SemanticIntentKind.DmaStreamCompute or
                SemanticIntentKind.ExternalAcceleratorCommand,
            RequiresRuntimeLegality: true,
            IsCompatibilityProjection: false,
            IsPolicyAdmissionOnly: false,
            IsHelperAbiOnly: intentKind is SemanticIntentKind.VectorStream or SemanticIntentKind.MatrixTile,
            IsParserOnly: false,
            "Phase 07 negative contour boundary intent.");

    private static CompilerEmissionPackage CreateLoadStorePackage(
        InstructionsEnum opcode,
        ulong address)
    {
        var compiler = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        compiler.CompileInstruction(
            (uint)opcode,
            (byte)DataTypeEnum.INT32,
            predicate: 0,
            immediate: 0,
            destSrc1: opcode is InstructionsEnum.SB or InstructionsEnum.SH or InstructionsEnum.SW or InstructionsEnum.SD
                ? VLIW_Instruction.PackArchRegs(0, 1, 9)
                : VLIW_Instruction.PackArchRegs(9, 1, 0),
            src2: address,
            streamLength: 0,
            stride: 0,
            StealabilityPolicy.NotStealable);
        return Project(compiler);
    }

    private static string ExpectedGoldenHash(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.LB => "2a7ff0e8c0a9edfabff7ed33ff3f8a4a10d5abb174fa45f75ebc2df3b67a498e",
            InstructionsEnum.LH => "01cffe0c060d36773daf3355baf2289213af1afe7cafec6301578388f0800edf",
            InstructionsEnum.LW => "8009b82fcb72c7bc091e6cca707a61cbec9982ce59fc91bb9f2fa511fc1a72e8",
            InstructionsEnum.LD => "5fbac9a7f7955d16df6e38601a73164818a33dab4696bb1b46decbcee02348db",
            InstructionsEnum.SB => "d57de485870e7e81f19da6e5c3b11776f977444725a848ded28c3d576cb46162",
            InstructionsEnum.SH => "9bc38bfd890e1cc62c322c75ad0fccce85ad82f9aa7e463fdee571fa29485acb",
            InstructionsEnum.SW => "18e3fd951ab6fcc8aad803150eec175517193ac89c47b31e3e5efef3e2f59dfb",
            InstructionsEnum.SD => "946a453714b70760e2e7b4a60d74debc26f6da329d29c3b814317d4a7c1b27e6",
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Missing Phase 07 load/store golden hash.")
        };

    private static CompilerEmissionPackage Project(
        HybridCpuThreadCompilerContext compiler)
    {
        HybridCpuCompiledProgram compiled = compiler.CompileProgram();
        return HybridCpuCompiledProgramEnvelopeAdapter.Instance.Project(
            compiled,
            new CompilerArtifactProjectionOptions(
                SemanticIntentKind.LoadStore,
                ExecutionContourKind.NativeVliwLoadStore,
                "HybridCpuThreadCompilerContext.CompileInstruction",
                "Phase 07 native LSU shadow candidate."));
    }
}
