using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf115FrontendStateExtractionTests
{
    private const BindingFlags InstanceDeclared =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    [Fact]
    public void RuntimeContainsOneExactFrontendDomain()
    {
        Type runtime = RequiredType("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type frontend = RequiredType("YAKSys_Hybrid_CPU.Core.FrontendState");
        Assert.Single(runtime.GetFields(InstanceDeclared), field => field.FieldType == frontend);

        string[] fields = frontend.GetFields(InstanceDeclared)
            .Select(field => field.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[] { "ActiveLivePc", "BranchPredictor", "Fetch", "FetchVliwBuffer", "HasMaterializedVliwFetchState" },
            fields);
        Assert.DoesNotContain(fields, name => name.Contains("Decode", StringComparison.Ordinal));
        Assert.DoesNotContain(fields, name => name.Contains("Admission", StringComparison.Ordinal));
        Assert.DoesNotContain(fields, name => name.Contains("Cache", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyFrontendFieldsAreRemovedAndRefForwardersPreserveInPlaceWrites()
    {
        Type core = typeof(Processor.CPU_Core);
        Assert.Null(core.GetField("pipeIF", InstanceDeclared));
        Assert.Null(core.GetField("_fetchVliwBuffer", InstanceDeclared));
        Assert.Null(core.GetField("branchPred", InstanceDeclared));

        foreach (string propertyName in new[] { "pipeIF", "_fetchVliwBuffer", "branchPred" })
        {
            PropertyInfo property = core.GetProperty(propertyName, InstanceDeclared) ??
                throw new InvalidOperationException($"Frontend ref-forwarder '{propertyName}' was not found.");
            MethodInfo getter = property.GetMethod ??
                throw new InvalidOperationException($"Frontend ref-forwarder '{propertyName}' has no getter.");
            Assert.True(getter.ReturnParameter.ParameterType.IsByRef);
        }
    }

    [Fact]
    public void TransitionalCoreCopiesAliasTheSameFrontendOwner()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;

        // RF-11 TEST-ONLY mutation: proves root identity, not a production API.
        core.Runtime.Frontend.Fetch.PC = 0x4400;
        core.Runtime.Frontend.Fetch.Valid = true;
        core.Runtime.Frontend.FetchVliwBuffer = new byte[256];

        Assert.Equal(0x4400UL, copy.Runtime.Frontend.Fetch.PC);
        Assert.True(copy.Runtime.Frontend.Fetch.Valid);
        Assert.Same(core.Runtime.Frontend.FetchVliwBuffer, copy.Runtime.Frontend.FetchVliwBuffer);
    }

    [Fact]
    public void FetchDecodeAndFlushCallSitesRemainInPlace()
    {
        string root = FindRepositoryRoot();
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        string pipeline = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Issue", "CPU_Core.Pipeline.cs");
        string support = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.TestSupport.cs");

        Assert.Contains("if (pipeIF.Valid)", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeIF.Valid = false;", stageFlow, StringComparison.Ordinal);
        Assert.Contains("if (!pipeID.Valid && pipeIF.Valid)", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeIF.Clear();", pipeline, StringComparison.Ordinal);
        Assert.Contains("branchPred.Clear();", pipeline, StringComparison.Ordinal);
        Assert.Contains("pipeIF.DecodeContext = CaptureReplayDecodeContext();", support, StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeCacheAndArchitecturalBoundariesRemainSeparatelyOwned()
    {
        Type core = typeof(Processor.CPU_Core);
        Type runtime = RequiredType("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type decode = RequiredType("YAKSys_Hybrid_CPU.Core.DecodeState");
        Assert.Single(runtime.GetFields(InstanceDeclared), field => field.FieldType == decode);
        Assert.Null(core.GetField("L1_VLIWBundles", InstanceDeclared));
        Assert.Null(core.GetField("Current_VLIWBundle_Position", InstanceDeclared));
        Assert.True((core.GetProperty("L1_VLIWBundles", InstanceDeclared) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.True((core.GetProperty("Current_VLIWBundle_Position", InstanceDeclared) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.Single(runtime.GetFields(InstanceDeclared), field => field.FieldType.Name == "CacheState");
        Assert.Null(core.GetField("_hasMaterializedVliwFetchState", InstanceDeclared));
        Assert.Null(core.GetField("ulong_InstructionPointer", InstanceDeclared));
        Assert.True((core.GetProperty("_hasMaterializedVliwFetchState", InstanceDeclared) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.True((core.GetProperty("ulong_InstructionPointer", InstanceDeclared) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyFrontendState()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.5-frontend-state-extraction.md");

        Assert.Contains("RF-11.5 | closed FrontendState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.6 DecodeState", ledger, StringComparison.Ordinal);
        Assert.Contains("ref-forwarding", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Decode/admission", evidence, StringComparison.Ordinal);
    }

    private static Type RequiredType(string name) =>
        typeof(Processor.CPU_Core).Assembly.GetType(name) ??
        throw new InvalidOperationException($"Required type '{name}' was not found.");

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
