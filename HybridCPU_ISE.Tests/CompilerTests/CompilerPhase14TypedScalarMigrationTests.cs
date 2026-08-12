using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.IR.Authority;
using HybridCPU.Compiler.Core.IR.Contours;
using HybridCPU.Compiler.Core.IR.Intent;
using HybridCPU.Compiler.Core.IR.Lowering;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerPhase14TypedScalarMigrationTests
{
    [Fact]
    public void EveryTypedScalarMethodEmitsItsExactPlanWithoutRuntimeAuthority()
    {
        MethodInfo[] methods = typeof(HybridCpuNonVmxScalarCompiler)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .OrderBy(static method => method.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(43, methods.Length);
        Assert.DoesNotContain(
            methods,
            static method => method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(InstructionsEnum) ||
                parameter.ParameterType == typeof(uint) ||
                parameter.ParameterType == typeof(ushort)));

        foreach (MethodInfo method in methods)
        {
            var context = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
            var compiler = new HybridCpuNonVmxScalarCompiler(context);
            object[] arguments = method.GetParameters()
                .Select((parameter, index) => CreateArgument(parameter.ParameterType, index))
                .ToArray();

            var result = Assert.IsType<CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan>>(
                method.Invoke(compiler, arguments));
            CompilerNonVmxScalarEmissionPlan plan = result.Plan;
            VLIW_Instruction instruction = Assert.Single(context.GetCompiledInstructions().ToArray());

            Assert.Equal((uint)plan.Opcode, instruction.OpCode);
            Assert.Equal(plan.DataType, instruction.DataTypeValue);
            Assert.Equal(plan.Immediate, instruction.Immediate);
            Assert.Equal(plan.StreamLength, instruction.StreamLength);
            Assert.Equal(plan.Stride, instruction.Stride);
            Assert.True(VLIW_Instruction.TryUnpackArchRegs(
                instruction.Word1,
                out byte rd,
                out byte rs1,
                out byte rs2));
            Assert.Equal(plan.DestinationRegister, rd);
            Assert.Equal(plan.SourceRegister1, rs1);
            Assert.Equal(plan.SourceRegister2, rs2);

            Assert.Equal(CompilerLoweringDecisionKind.StructuralOnly, result.Decision.DecisionKind);
            Assert.Equal(SemanticIntentKind.ScalarAlu, result.Decision.IntentKind);
            Assert.Equal(ExecutionContourKind.NativeVliwScalar, result.Decision.ContourKind);
            Assert.Equal(CompilerEmissionClass.CarrierCandidate, result.Decision.EmissionClass);
            Assert.Equal(
                CompilerProductionLoweringStatus.NotProductionLowering,
                result.Decision.ProductionLoweringStatus);
            Assert.Equal(CompilerAuthorityClass.TransportConstruction, result.Decision.AuthorityClass);
            Assert.Equal(CompilerExecutionClaim.NoExecutionClaim, result.Decision.ExecutionClaim);
            Assert.Equal(CompilerPublicationClass.CarrierBytesOnly, result.Decision.PublicationClass);
            Assert.False(result.Decision.FallbackPolicy.AllowsCrossContourFallback);
            Assert.Null(result.Decision.LegacyTranslation);
            AssertRuntimeOwnersRemainRequired(result.Decision.RuntimeAuthorityDependency);
        }
    }

    [Fact]
    public void DisabledFeatureAndInvalidImmediateFailBeforeCarrierEmission()
    {
        var disabledContext = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        var disabled = new HybridCpuNonVmxScalarCompiler(
            disabledContext,
            CompilerNonVmxScalarCapabilityModel.Disabled);

        Assert.Throws<InvalidOperationException>(() =>
            disabled.ScalarMinSigned(new AsmRegister(1), new AsmRegister(2), new AsmRegister(3)));
        Assert.Equal(0, disabledContext.InstructionCount);

        var immediateContext = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        var immediate = new HybridCpuNonVmxScalarCompiler(immediateContext);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            immediate.RotateLeftByImmediate(new AsmRegister(1), new AsmRegister(2), 64));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            immediate.RotateRightByImmediate(new AsmRegister(1), new AsmRegister(2), -1));
        Assert.Equal(0, immediateContext.InstructionCount);
    }

    [Fact]
    public void TypedScalarProducerAndMigratedExamplesHaveNoRuntimeOrFacadeBypass()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string producerSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_Compiler",
            "API",
            "Compilation",
            "HybridCpuNonVmxScalarCompiler.cs"));
        string examplesRoot = Path.Combine(
            repoRoot,
            "MinimalAsmApp",
            "Examples",
            "13_InstructionClosure");
        string examples = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(examplesRoot, "NonVmx*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        string[] forbiddenProducerTokens =
        [
            "SafetyVerifier",
            "RuntimeAdmission",
            "ExecuteVmx",
            "VmxExecutionBackend",
            "PublishCompletion",
            "PublishRetire",
            "CommitInstruction",
            "CreateCapability",
            "GrantCapability",
            "SecureCompute",
            "DmaStreamCompute",
            "ACCEL_"
        ];
        foreach (string token in forbiddenProducerTokens)
        {
            Assert.DoesNotContain(token, producerSource, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("AppAsmFacade", examples, StringComparison.Ordinal);
        Assert.DoesNotContain("PlatformAsmFacade", examples, StringComparison.Ordinal);
        Assert.DoesNotContain("#pragma warning disable CS0618", examples, StringComparison.Ordinal);
        Assert.Contains("HybridCpuNonVmxScalarCompiler", examples, StringComparison.Ordinal);
    }

    [Fact]
    public void CompatibilityFacadesDelegateEveryExactScalarMethodToTheTypedProducer()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string appFacade = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_Compiler",
            "API",
            "Facade",
            "AppAsmFacade.cs"));
        string platformFacade = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_Compiler",
            "API",
            "Facade",
            "PlatformAsmFacade.cs"));
        string combined = appFacade + Environment.NewLine + platformFacade;

        MethodInfo[] methods = typeof(HybridCpuNonVmxScalarCompiler)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.Equal(43, methods.Length);
        foreach (MethodInfo method in methods)
        {
            Assert.Contains(
                $"_ = NonVmxScalarCompiler.{method.Name}(",
                combined,
                StringComparison.Ordinal);
        }

        Assert.DoesNotContain("RequireScalarFeature", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("_scalarCapabilities", combined, StringComparison.Ordinal);
    }

    private static object CreateArgument(Type parameterType, int index)
    {
        if (parameterType == typeof(AsmRegister))
        {
            return new AsmRegister(index + 1);
        }

        if (parameterType == typeof(int))
        {
            return 3;
        }

        throw new InvalidOperationException($"Unexpected typed scalar parameter {parameterType}.");
    }

    private static void AssertRuntimeOwnersRemainRequired(
        CompilerRuntimeAuthorityDependency dependency)
    {
        Assert.True(dependency.HasFlag(CompilerRuntimeAuthorityDependency.RuntimeLegalityARequired));
        Assert.True(dependency.HasFlag(CompilerRuntimeAuthorityDependency.RuntimeLegalityBRequired));
        Assert.True(dependency.HasFlag(CompilerRuntimeAuthorityDependency.RuntimeExecutionRequired));
        Assert.True(dependency.HasFlag(CompilerRuntimeAuthorityDependency.RuntimeCommitRequired));
        Assert.True(dependency.HasFlag(CompilerRuntimeAuthorityDependency.RuntimeRetireRequired));
        Assert.True(dependency.HasFlag(CompilerRuntimeAuthorityDependency.RuntimePublicationRequired));
    }
}
