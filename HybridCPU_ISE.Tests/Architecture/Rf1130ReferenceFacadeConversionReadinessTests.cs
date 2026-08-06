using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Machine;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1130ReferenceFacadeConversionReadinessTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void TransitionalFacadeHasNoMutableDirectStorageButIsStillValueType()
    {
        Type core = typeof(Processor.CPU_Core);
        Assert.False(core.IsValueType);
        FieldInfo field = Assert.Single(core.GetFields(Fields));
        Assert.Equal("_runtime", field.Name);
        Assert.True(field.IsInitOnly);
        Assert.Equal("CoreRuntimeState", field.FieldType.Name);
        Assert.Equal(typeof(CpuCoreDiagnosticSnapshot),
            typeof(Processor).GetMethod(nameof(Processor.GetCoreSnapshot))?.ReturnType);
    }

    [Fact]
    public void PartialAndReferenceSignatureCutoverSurfaceIsFrozen()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Equal(0, Regex.Matches(production, @"partial\s+struct\s+CPU_Core").Count);
        Assert.Equal(67, Regex.Matches(production, @"partial\s+class\s+CPU_Core").Count);
        Assert.Equal(289, Regex.Matches(production,
            @"\b(?:ref|in|out)\s+(?:Processor\.)?CPU_Core\b").Count);
        Assert.Equal(40, SourceFiles(Path.Combine(root, "HybridCPU_ISE")).Count(file =>
            Regex.IsMatch(File.ReadAllText(file), @"\b(?:ref|in|out)\s+(?:Processor\.)?CPU_Core\b")));
    }

    [Fact]
    public void RefThisAndReadonlyStructMemberBlockersRemainExplicit()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\.") );

        string runtime = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "State", "CPU_Core.RuntimeState.cs");
        string matrix = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire",
            "Evidence", "CPU_Core.MatrixTileRetireState.cs");
        Assert.DoesNotContain("private readonly ref", runtime, StringComparison.Ordinal);
        Assert.Contains("internal bool OwnsMatrixTileReplayJournal", matrix, StringComparison.Ordinal);
    }

    [Fact]
    public void TableLifecycleDefaultAndNullSemanticsRequireSeparateHardening()
    {
        string root = FindRoot();
        string identity = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.CoreIdentity.cs");
        string processor = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.cs");
        string obsolete = Read(root, "HybridCPU_ISE", "Legacy", "Obsolete", "Processor.Initialization.Obsolete.cs");
        Assert.Contains("public static CPU_Core[] CPU_Cores { get; private set; } = Array.Empty<CPU_Core>();", processor, StringComparison.Ordinal);
        Assert.Contains("BeginCoreTableConstruction(1024);", obsolete, StringComparison.Ordinal);
        Assert.Contains("private static ref CPU_Core GetCoreSlotRef", identity, StringComparison.Ordinal);
        Assert.Contains("public static void ReplaceCore", identity, StringComparison.Ordinal);
        Assert.Contains("_ = liveCore.Runtime;", identity, StringComparison.Ordinal);
        Assert.Contains("_ = replacement.Runtime;", identity, StringComparison.Ordinal);

        string tests = ReadSources(Path.Combine(root, "HybridCPU_ISE.Tests"));
        Assert.Contains("Assert.Null(default(Processor.CPU_Core));", tests, StringComparison.Ordinal);
        Assert.DoesNotContain("Processor.CPU_Core core = " + "default;", tests, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotReflectionSerializationAndLifecycleCopiesAreSeparated()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        string matrix = Read(root, "TestAssemblerConsoleApps", "MatrixTileSpecSuite.cs");
        Assert.DoesNotContain("SetValueDirect", production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize(core", production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<Processor.CPU_Core", production, StringComparison.Ordinal);
        Assert.DoesNotContain("CPU_" + "Cores[", production, StringComparison.Ordinal);
        string identity = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.CoreIdentity.cs");
        Assert.Contains("#if TESTING", identity, StringComparison.Ordinal);
        Assert.Contains("EnsureCoreTableForTesting", identity, StringComparison.Ordinal);
        Assert.Contains("originalCoreLifecycleHandle = Processor.GetCoreRef(0);", matrix, StringComparison.Ordinal);
        Assert.Contains("Processor.ReplaceCore(0, originalCoreLifecycleHandle);", matrix, StringComparison.Ordinal);
        Assert.DoesNotContain("Processor.GetCoreSnapshot(0)", matrix, StringComparison.Ordinal);
    }

    [Fact]
    public void FrozenCrossStageMutationAndCycleOrderRemainUnchanged()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Equal(34, AssignmentCount(production, "IF"));
        Assert.Equal(49, AssignmentCount(production, "ID"));
        Assert.Equal(119, AssignmentCount(production, "EX"));
        Assert.Equal(62, AssignmentCount(production, "MEM"));
        Assert.Equal(60, AssignmentCount(production, "WB"));

        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        AssertOrder(stageFlow, "MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(",
            "RefreshInFlightExplicitMemoryProgress();", "PipelineStage_WriteBack();",
            "PipelineStage_Memory();", "PipelineStage_Execute();", "PipelineStage_Decode();",
            "PipelineStage_Fetch();");
    }

    [Fact]
    public void LedgerAndEvidenceCloseReadinessInventoryWithoutConversion()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.30-reference-facade-conversion-readiness.md");
        Assert.Contains("RF-11.30 | closed reference-facade conversion readiness", ledger, StringComparison.Ordinal);
        Assert.Contains("not ready", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no production state declaration", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.31 | closed execution ref-this seam hardening", ledger, StringComparison.Ordinal);
        Assert.Contains("TestAssemblerConsoleApps was not run", evidence, StringComparison.Ordinal);
    }

    private static int AssignmentCount(string text, string stage) => Regex.Matches(text,
        $@"\bpipe{stage}\.\w+\s*(?:[+\-*/%&|^]=|=(?!=))").Count;
    private static void AssertOrder(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, prior + 1, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{marker}' after prior marker.");
            prior = current;
        }
    }
    private static string ReadSources(string path) => string.Join('\n', SourceFiles(path).Select(File.ReadAllText));
    private static IEnumerable<string> SourceFiles(string path) => Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException();
    }
}
