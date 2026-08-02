using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf116DecodeStateExtractionTests
{
    private const BindingFlags InstanceDeclared =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    private static readonly string[] Forwarders =
    [
        "bundleDecodedAndPacked",
        "decodedBundleDerivedIssuePlanState",
        "decodedBundleProgressState",
        "decodedBundleRuntimeState",
        "decodedBundleStateEpochCounter",
        "decodedBundleStateVersionCounter",
        "pipeID",
        "pipeIDClusterPreparation",
        "pipelineBundleSlot"
    ];

    [Fact]
    public void RuntimeContainsOneExactDecodeDomain()
    {
        Type runtime = RequiredType("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type decode = RequiredType("YAKSys_Hybrid_CPU.Core.DecodeState");
        Assert.Single(runtime.GetFields(InstanceDeclared), field => field.FieldType == decode);

        string[] fields = decode.GetFields(InstanceDeclared)
            .Select(field => field.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "BundleDecodedAndPacked",
                "BundleProgress",
                "BundleRuntime",
                "BundleStateEpochCounter",
                "BundleStateVersionCounter",
                "ClusterPreparation",
                "Decode",
                "DerivedIssuePlan",
                "PipelineBundleSlot"
            },
            fields);
    }

    [Fact]
    public void LegacyDecodeFieldsAreRemovedAndEveryForwarderIsByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in Forwarders)
        {
            Assert.Null(core.GetField(name, InstanceDeclared));
            PropertyInfo property = core.GetProperty(name, InstanceDeclared) ??
                throw new InvalidOperationException($"Decode ref-forwarder '{name}' was not found.");
            Assert.True(property.PropertyType.IsByRef);
            Assert.True(property.GetMethod!.ReturnParameter.ParameterType.IsByRef);
        }
    }

    [Fact]
    public void TransitionalCoreCopiesAliasTheSameDecodeOwner()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;

        core.Runtime.Decode.Decode.PC = 0x6600;
        core.Runtime.Decode.Decode.Valid = true;
        core.Runtime.Decode.PipelineBundleSlot = 3;
        core.Runtime.Decode.BundleStateEpochCounter = 17;

        Assert.Equal(0x6600UL, copy.Runtime.Decode.Decode.PC);
        Assert.True(copy.Runtime.Decode.Decode.Valid);
        Assert.Equal((byte)3, copy.Runtime.Decode.PipelineBundleSlot);
        Assert.Equal(17UL, copy.Runtime.Decode.BundleStateEpochCounter);
    }

    [Fact]
    public void DecodeCrossStageAndLifecycleWritesRemainAtTheirFrozenSites()
    {
        string root = FindRepositoryRoot();
        string pipeline = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Issue", "CPU_Core.Pipeline.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");
        string decoder = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Frontend",
            "Decode", "BundleParser", "CPU_Core.Decoder.cs");

        Assert.Contains("pipeID.Clear();", pipeline, StringComparison.Ordinal);
        Assert.Contains("pipelineBundleSlot = 0;", pipeline, StringComparison.Ordinal);
        Assert.Contains("if (!pipeID.Valid && pipeIF.Valid)", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeID.Valid = true;", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeID.Valid = false;", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeEX.Valid = pipeID.Valid;", materialization, StringComparison.Ordinal);
        Assert.Contains("decodedBundleRuntimeState = publishedRuntimeState;", decoder, StringComparison.Ordinal);
        Assert.Contains("++decodedBundleStateEpochCounter", decoder, StringComparison.Ordinal);
    }

    [Fact]
    public void AdmissionRetireReplayAndCacheOwnersRemainOutsideDecodeState()
    {
        Type core = typeof(Processor.CPU_Core);
        Type decode = RequiredType("YAKSys_Hybrid_CPU.Core.DecodeState");
        Type runtime = RequiredType("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type admission = RequiredType("YAKSys_Hybrid_CPU.Core.AdmissionState");
        string[] decodeFields = decode.GetFields(InstanceDeclared).Select(field => field.Name).ToArray();

        Assert.Single(runtime.GetFields(InstanceDeclared), field => field.FieldType == admission);

        Assert.NotNull(core.GetProperty("rf08OperationAttemptIssuer", InstanceDeclared));
        Assert.Null(core.GetField("L1_VLIWBundles", InstanceDeclared));
        Assert.True((core.GetProperty("L1_VLIWBundles", InstanceDeclared) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.Single(runtime.GetFields(InstanceDeclared), field => field.FieldType.Name == "CacheState");

        Assert.DoesNotContain(decodeFields, name => name.Contains("Admission", StringComparison.Ordinal));
        Assert.DoesNotContain(decodeFields, name => name.Contains("Retire", StringComparison.Ordinal));
        Assert.DoesNotContain(decodeFields, name => name.Contains("Replay", StringComparison.Ordinal));
        Assert.DoesNotContain(decodeFields, name => name.Contains("Cache", StringComparison.Ordinal));
    }

    [Fact]
    public void SnapshotAndTestSupportRemainExplicitAdapters()
    {
        string root = FindRepositoryRoot();
        string pipeline = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Issue", "CPU_Core.Pipeline.cs");
        string support = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.TestSupport.cs");

        Assert.Contains("public DecodeStage GetDecodeStage()", pipeline, StringComparison.Ordinal);
        Assert.Contains("return pipeID;", pipeline, StringComparison.Ordinal);
        Assert.Contains("internal void TestSetDecodedBundle", support, StringComparison.Ordinal);
        Assert.Contains("pipeID.Clear();", support, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyDecodeState()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.6-decode-state-extraction.md");

        Assert.Contains("RF-11.6 | closed DecodeState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.7 AdmissionState", ledger, StringComparison.Ordinal);
        Assert.Contains("nine", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Admission", evidence, StringComparison.Ordinal);
        Assert.Contains("ref-forward", evidence, StringComparison.OrdinalIgnoreCase);
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
