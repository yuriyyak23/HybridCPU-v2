using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.IR.Artifacts;
using HybridCPU.Compiler.Core.IR.Authority;
using HybridCPU.Compiler.Core.IR.Contours;
using HybridCPU.Compiler.Core.IR.Intent;
using HybridCPU.Compiler.Core.IR.Lowering.Production;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerPhase13ProductionDispatchTests
{
    [Fact]
    public void ExactScalarIntentUsesCanonicalProviderWithoutGrantingRuntimeAuthority()
    {
        CompilerSemanticIntent intent = ScalarIntent();

        CompilerProductionDispatchResult dispatch =
            HybridCpuCanonicalCompiler.DispatchProductionPackage(
                ExplicitRequest(
                    intent,
                    CreateScalarProgram(),
                    ExecutionContourKind.NativeVliwScalar));

        Assert.Equal(CompilerProductionDispatchKind.ProviderEvaluated, dispatch.DispatchKind);
        Assert.Equal(CompilerProductionDispatchAuthority.NoRuntimeAuthority, dispatch.Authority);
        Assert.Equal(ExecutionContourKind.NativeVliwScalar, dispatch.Selection.Kind);
        Assert.Equal("NativeVliwScalarProductionProvider", dispatch.ProviderType);
        Assert.NotNull(dispatch.ProviderResult);
        Assert.Equal(
            CompilerProductionLoweringResultKind.RuntimeAuthorityPending,
            dispatch.ProviderResult!.ResultKind);
        Assert.NotNull(dispatch.ProviderResult.Package);
        Assert.True(dispatch.ProviderResult.GateResult.IsSatisfied);
        AssertRuntimeLifecycleStillRequired(dispatch.RuntimeAuthorityStillRequired);
    }

    [Fact]
    public void AllSixRegisteredProvidersSucceedThroughCanonicalCompiledProgramDispatch()
    {
        foreach (ProductionCase testCase in CreateProductionCases())
        {
            CompilerProductionDispatchResult dispatch =
                HybridCpuCanonicalCompiler.DispatchProductionPackage(
                    ExplicitRequest(
                        testCase.Intent,
                        testCase.Program,
                        testCase.Contour,
                        testCase.Readiness));

            Assert.Equal(CompilerProductionDispatchKind.ProviderEvaluated, dispatch.DispatchKind);
            Assert.Equal(CompilerProductionDispatchAuthority.NoRuntimeAuthority, dispatch.Authority);
            Assert.Equal(testCase.Contour, dispatch.Selection.Kind);
            Assert.Equal(testCase.ProviderType, dispatch.ProviderType);
            Assert.NotNull(dispatch.ProviderResult);
            Assert.True(
                dispatch.ProviderResult!.ResultKind == CompilerProductionLoweringResultKind.RuntimeAuthorityPending,
                $"{testCase.Contour} canonical dispatch failed: {dispatch.ProviderResult.Reason}");
            Assert.NotNull(dispatch.ProviderResult.Package);
            Assert.Equal(testCase.Contour, dispatch.ProviderResult.Package!.Identity.ContourKind);
            Assert.Equal(testCase.Intent.Kind, dispatch.ProviderResult.Package.Identity.IntentKind);
            if (testCase.Intent.RequiresDescriptor)
            {
                Assert.Equal(
                    SidebandRequirement.RequiredForDescriptorSubmit,
                    dispatch.ProviderResult.Package.Sideband!.Requirement);
                Assert.Same(
                    dispatch.ProviderResult.Package.Sideband,
                    dispatch.ProviderResult.Package.RuntimeBridgeInput!.Sideband);
            }
            AssertRuntimeLifecycleStillRequired(dispatch.RuntimeAuthorityStillRequired);
        }
    }

    [Fact]
    public void IntentCannotRelabelCarrierIntoAnotherContour()
    {
        CompilerSemanticIntent forgedIntent = Intent(SemanticIntentKind.LoadStore, "LW");
        CompilerProductionDispatchResult dispatch =
            HybridCpuCanonicalCompiler.DispatchProductionPackage(
                ExplicitRequest(
                    forgedIntent,
                    CreateScalarProgram(),
                    ExecutionContourKind.NativeVliwLoadStore,
                    CompilerProductionLoweringReadiness.CompleteLoadStore));

        Assert.Equal(CompilerProductionDispatchKind.ProviderEvaluated, dispatch.DispatchKind);
        Assert.Equal(CompilerProductionLoweringResultKind.Rejected, dispatch.ProviderResult!.ResultKind);
        Assert.Null(dispatch.ProviderResult.Package);
        Assert.Contains("outside", dispatch.ProviderResult.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CompilerProductionDispatchAuthority.NoRuntimeAuthority, dispatch.Authority);
    }

    [Fact]
    public void ProfileForDifferentContourCannotEnableSelectedProvider()
    {
        CompilerCompiledProgramDispatchRequest request = ExplicitRequest(
            ScalarIntent(),
            CreateScalarProgram(),
            ExecutionContourKind.NativeVliwLoadStore,
            CompilerProductionLoweringReadiness.CompleteLoadStore);

        CompilerProductionDispatchResult dispatch =
            HybridCpuCanonicalCompiler.DispatchProductionPackage(request);

        Assert.Equal(CompilerProductionDispatchKind.FutureGatedNoProvider, dispatch.DispatchKind);
        Assert.Null(dispatch.ProviderResult);
        Assert.Equal(ExecutionContourKind.NativeVliwScalar, dispatch.Selection.Kind);
        Assert.Equal(CompilerProductionDispatchAuthority.NoRuntimeAuthority, dispatch.Authority);
    }

    [Fact]
    public void CompatibilityProfileStopsBeforeProviderAndCannotFallback()
    {
        CompilerCompiledProgramDispatchRequest request = new(
            ScalarIntent(),
            CreateScalarProgram(),
            new CompilerProductionLoweringProfile(
                "phase13-compatibility-only",
                CompilerProductionLoweringProfileMode.CompatibilityOnly,
                new HashSet<ExecutionContourKind> { ExecutionContourKind.NativeVliwScalar },
                CompilerProductionLoweringGateIds.AllFor(ExecutionContourKind.NativeVliwScalar)),
            CompilerProductionLoweringReadiness.Complete,
            CompilerCarrierProductionMode.CompatibilityOnly,
            "CompilerPhase13ProductionDispatchTests");

        CompilerProductionDispatchResult dispatch =
            HybridCpuCanonicalCompiler.DispatchProductionPackage(request);

        Assert.Equal(CompilerProductionDispatchKind.FutureGatedNoProvider, dispatch.DispatchKind);
        Assert.Equal(CompilerProductionDispatchAuthority.NoRuntimeAuthority, dispatch.Authority);
        Assert.Null(dispatch.ProviderResult);
        Assert.Contains("fallback is forbidden", dispatch.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SemanticIntentKind.VmxCompatibilityProjection)]
    [InlineData(SemanticIntentKind.SecureComputeAdmission)]
    [InlineData(SemanticIntentKind.RuntimeAssist)]
    [InlineData(SemanticIntentKind.Unknown)]
    public void NonProductionIntentStopsBeforeProvider(SemanticIntentKind kind)
    {
        CompilerSemanticIntent intent = new(
            kind,
            kind.ToString(),
            RequiresDescriptor: false,
            RequiresSideband: false,
            RequiresToken: false,
            RequiresRuntimeLegality: false,
            IsCompatibilityProjection: kind == SemanticIntentKind.VmxCompatibilityProjection,
            IsPolicyAdmissionOnly: kind == SemanticIntentKind.SecureComputeAdmission,
            IsHelperAbiOnly: false,
            IsParserOnly: kind == SemanticIntentKind.Unknown,
            "Phase 13 negative dispatch intent.");

        CompilerProductionDispatchResult dispatch =
            HybridCpuCanonicalCompiler.DispatchProductionPackage(
                ExplicitRequest(
                    intent,
                    CreateScalarProgram(),
                    ExecutionContourKind.NativeVliwScalar));

        Assert.Equal(CompilerProductionDispatchKind.RejectedBeforeProvider, dispatch.DispatchKind);
        Assert.Equal(CompilerProductionDispatchAuthority.NoRuntimeAuthority, dispatch.Authority);
        Assert.Null(dispatch.ProviderResult);
    }

    [Fact]
    public void DispatcherSourceHasNoRuntimeBackendOrLifecycleCalls()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string path = Path.Combine(
            repoRoot,
            "HybridCPU_Compiler",
            "Core",
            "IR",
            "Lowering",
            "Production",
            "CompilerProductionLoweringDispatcher.cs");
        string source = File.ReadAllText(path);

        string[] forbidden =
        [
            "SafetyVerifier.",
            "VmxExecutionBackend",
            "ExecuteVmx",
            "PublishCompletion",
            "PublishRetire",
            "CommitInstruction",
            "CreateCapability",
            "GrantCapability"
        ];
        foreach (string token in forbidden)
        {
            Assert.DoesNotContain(token, source, StringComparison.Ordinal);
        }

        Assert.Contains("provider.TryProduce(intent, analysis, context)", source, StringComparison.Ordinal);
        Assert.Contains("CompilerProductionDispatchAuthority.NoRuntimeAuthority", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalRequestHasNoCallerSuppliedContourOrCandidatePackage()
    {
        Type requestType = typeof(CompilerCompiledProgramDispatchRequest);

        Assert.Null(requestType.GetProperty("ContourKind"));
        Assert.Null(requestType.GetProperty("CandidatePackage"));
        Assert.NotNull(requestType.GetProperty("Intent"));
        Assert.NotNull(requestType.GetProperty("CompiledProgram"));
        Assert.Empty(typeof(CompilerProductionLoweringDispatcher).GetConstructors());
        Assert.Null(typeof(CompilerProductionLoweringDispatcher).GetMethod(
            "Dispatch",
            BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void LocalCompilerCallersDoNotUseRawMatrixTileOrLegacyBackendBoolApis()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string matrixTileSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "TestAssemblerConsoleApps",
            "MatrixTileSpecSuite.cs"));
        string diagnosticsSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "TestAssemblerConsoleApps",
            "WhiteBookContractDiagnostics.cs"));

        string[] forbiddenRawMatrixTileCalls =
        [
            ".CompileMtileLoad(",
            ".CompileMtileStore(",
            ".CompileMtileMacc(",
            ".CompileMtranspose("
        ];
        foreach (string token in forbiddenRawMatrixTileCalls)
        {
            Assert.DoesNotContain(token, matrixTileSource, StringComparison.Ordinal);
        }

        Assert.Contains("CompileMtileLoadWithDecision(", matrixTileSource, StringComparison.Ordinal);
        Assert.Contains("CompileMtileStoreWithDecision(", matrixTileSource, StringComparison.Ordinal);
        Assert.Contains("CompileMtileMaccWithDecision(", matrixTileSource, StringComparison.Ordinal);
        Assert.Contains("CompileMtransposeWithDecision(", matrixTileSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".IsAllowed", diagnosticsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanSelectForProductionLowering", diagnosticsSource, StringComparison.Ordinal);
        Assert.Contains(
            "CompilerLoweringDecision.FromLegacyBackendLoweringDecision(",
            diagnosticsSource,
            StringComparison.Ordinal);
    }

    private static CompilerSemanticIntent ScalarIntent() => new(
        SemanticIntentKind.ScalarAlu,
        "ADD",
        RequiresDescriptor: false,
        RequiresSideband: false,
        RequiresToken: false,
        RequiresRuntimeLegality: true,
        IsCompatibilityProjection: false,
        IsPolicyAdmissionOnly: false,
        IsHelperAbiOnly: false,
        IsParserOnly: false,
        "Phase 13 canonical scalar dispatch intent.");

    private static CompilerCompiledProgramDispatchRequest ExplicitRequest(
        CompilerSemanticIntent intent,
        HybridCpuCompiledProgram program,
        ExecutionContourKind contour,
        CompilerProductionLoweringReadiness? readiness = null) =>
        new(
            intent,
            program,
            new CompilerProductionLoweringProfile(
                "phase13-exact-provider",
                CompilerProductionLoweringProfileMode.ExplicitlyEnabled,
                new HashSet<ExecutionContourKind> { contour },
                CompilerProductionLoweringGateIds.AllFor(contour)),
            readiness ?? CompilerProductionLoweringReadiness.Complete,
            CompilerCarrierProductionMode.ExplicitCarrierProduction,
            "CompilerPhase13ProductionDispatchTests");

    private static IReadOnlyList<ProductionCase> CreateProductionCases()
    {
        var vectorCompiler = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        vectorCompiler.CompileVloadWithDecision(
            CompilerVectorTransferMemoryAddressAbi.Create(0x200),
            CompilerVectorTransferMemoryAddressAbi.Create(0x300),
            CompilerVectorTransferShapeAbi.CreateContiguous(DataTypeEnum.INT32, 4));

        var dscCompiler = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        dscCompiler.CompileDmaStreamCompute(DmaStreamComputeTestDescriptorFactory.CreateDescriptor());

        AcceleratorCommandDescriptor l7Descriptor = L7SdcTestDescriptorFactory.ParseValidDescriptor();
        HybridCpuThreadCompilerContext l7Compiler =
            L7SdcCompilerEmissionTests.CreateContextForDescriptor(l7Descriptor);
        l7Compiler.CompileAcceleratorSubmit(
            IrAcceleratorIntent.ForMatMul(l7Descriptor, tokenDestinationRegister: 9),
            CompilerAcceleratorCapabilityModel.ReferenceMatMul);

        return
        [
            new(
                ScalarIntent(),
                CreateScalarProgram(),
                ExecutionContourKind.NativeVliwScalar,
                "NativeVliwScalarProductionProvider",
                CompilerProductionLoweringReadiness.Complete),
            new(
                Intent(SemanticIntentKind.LoadStore, "LW"),
                CreateRawProgram(
                    InstructionsEnum.LW,
                    VLIW_Instruction.PackArchRegs(9, 1, 0),
                    src2: 0x100),
                ExecutionContourKind.NativeVliwLoadStore,
                "NativeVliwLoadStoreProductionProvider",
                CompilerProductionLoweringReadiness.CompleteLoadStore),
            new(
                Intent(SemanticIntentKind.BranchControl, "JAL"),
                CreateRawProgram(
                    InstructionsEnum.JAL,
                    VLIW_Instruction.PackArchRegs(
                        0,
                        VLIW_Instruction.NoArchReg,
                        VLIW_Instruction.NoArchReg),
                    immediate: 0x20),
                ExecutionContourKind.NativeVliwBranchControl,
                "NativeVliwBranchControlProductionProvider",
                CompilerProductionLoweringReadiness.Complete),
            new(
                Intent(SemanticIntentKind.VectorStream, "VLOAD"),
                vectorCompiler.CompileProgram(),
                ExecutionContourKind.StreamEngineVector,
                "StreamEngineVectorDirectTransferProductionProvider",
                CompilerProductionLoweringReadiness.Complete),
            new(
                Intent(
                    SemanticIntentKind.DmaStreamCompute,
                    "DmaStreamCompute",
                    requiresDescriptor: true,
                    requiresSideband: true,
                    requiresToken: true),
                dscCompiler.CompileProgram(),
                ExecutionContourKind.DmaStreamComputeLane6,
                "DmaStreamComputeLane6ProductionProvider",
                CompilerProductionLoweringReadiness.Complete),
            new(
                Intent(
                    SemanticIntentKind.ExternalAcceleratorCommand,
                    "ACCEL_SUBMIT",
                    requiresDescriptor: true,
                    requiresSideband: true,
                    requiresToken: true),
                l7Compiler.CompileProgram(),
                ExecutionContourKind.L7SdcLane7,
                "L7SdcLane7ProductionProvider",
                CompilerProductionLoweringReadiness.Complete)
        ];
    }

    private static HybridCpuCompiledProgram CreateScalarProgram() =>
        CreateRawProgram(
            InstructionsEnum.ADD,
            VLIW_Instruction.PackArchRegs(1, 2, 3));

    private static HybridCpuCompiledProgram CreateRawProgram(
        InstructionsEnum opcode,
        ulong word1,
        ulong src2 = 0,
        ushort immediate = 0)
    {
        var compiler = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        compiler.CompileInstruction(
            (uint)opcode,
            (byte)DataTypeEnum.INT32,
            predicate: 0,
            immediate,
            destSrc1: word1,
            src2,
            streamLength: 0,
            stride: 0,
            StealabilityPolicy.NotStealable);
        return compiler.CompileProgram();
    }

    private static CompilerSemanticIntent Intent(
        SemanticIntentKind kind,
        string opcodeFamily,
        bool requiresDescriptor = false,
        bool requiresSideband = false,
        bool requiresToken = false) =>
        new(
            kind,
            opcodeFamily,
            requiresDescriptor,
            requiresSideband,
            requiresToken,
            RequiresRuntimeLegality: true,
            IsCompatibilityProjection: false,
            IsPolicyAdmissionOnly: false,
            IsHelperAbiOnly: false,
            IsParserOnly: false,
            "Phase 13 canonical successful production dispatch intent.");

    private static void AssertRuntimeLifecycleStillRequired(
        CompilerRuntimeAuthorityDependency dependencies)
    {
        Assert.True(dependencies.HasFlag(CompilerRuntimeAuthorityDependency.RuntimeLegalityARequired));
        Assert.True(dependencies.HasFlag(CompilerRuntimeAuthorityDependency.RuntimeLegalityBRequired));
        Assert.True(dependencies.HasFlag(CompilerRuntimeAuthorityDependency.RuntimeExecutionRequired));
        Assert.True(dependencies.HasFlag(CompilerRuntimeAuthorityDependency.RuntimePublicationRequired));
        Assert.True(dependencies.HasFlag(CompilerRuntimeAuthorityDependency.RuntimeCommitRequired));
        Assert.True(dependencies.HasFlag(CompilerRuntimeAuthorityDependency.RuntimeRetireRequired));
    }

    private sealed record ProductionCase(
        CompilerSemanticIntent Intent,
        HybridCpuCompiledProgram Program,
        ExecutionContourKind Contour,
        string ProviderType,
        CompilerProductionLoweringReadiness Readiness);
}
