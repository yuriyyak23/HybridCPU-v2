using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1123CacheStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly string[] CacheNames =
    [
        "Current_DataObject_Position", "Current_VLIWBundle_Position", "L1_Data",
        "L1_VLIWBundles", "L2_Data", "L2_VLIWBundles", "ulong_MinL1Query", "ulong_MinL2Query"
    ];

    [Fact]
    public void RuntimeContainsExactEightFieldCacheState()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type cache = Required("YAKSys_Hybrid_CPU.Core.CacheState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == cache);
        Assert.Equal(new[]
        {
            "CurrentDataObjectPosition", "CurrentVliwBundlePosition", "L1Data", "L1VliwBundles",
            "L2Data", "L2VliwBundles", "MinimumL1Query", "MinimumL2Query"
        }, cache.GetFields(Flags).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(4, cache.GetFields(Flags).Count(field => field.FieldType.IsArray));
        Assert.Equal(4, cache.GetFields(Flags).Count(field => field.FieldType == typeof(ulong)));
        Assert.DoesNotContain(cache.GetMethods(Flags), method => method.Name is
            "AdvanceCycle" or "Execute" or "Commit" or "Publish" or "Flush" or "Invalidate");
    }

    [Fact]
    public void LegacyFieldsAreRemovedAndForwardByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in CacheNames)
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ??
                throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void ConstructionAndCopiesPreserveCacheAllocationIdentity()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;

        Assert.Null(core.L1_VLIWBundles);
        Assert.Null(core.L1_Data);
        Assert.Equal(65536, core.L2_VLIWBundles.Length);
        Assert.Equal(65536, core.L2_Data.Length);
        Assert.Same(core.L2_VLIWBundles, copy.L2_VLIWBundles);
        Assert.Same(core.L2_Data, copy.L2_Data);

        core.L1_Data = new Processor.CPU_Core.Cache_Data_Object[8];
        Assert.Same(core.L1_Data, copy.L1_Data);
    }

    [Fact]
    public void ExistingLazyFillAndInvalidationOrderRemainAtCacheHelpers()
    {
        string cache = Read(FindRoot(), "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "Memory", "Cache", "CPU_Core.Cache.cs");
        AssertOrder(cache, "if (L1_VLIWBundles == null || L1_Data == null)",
            "L1_VLIWBundles = new Cache_VLIWBundle_Object[256];",
            "L1_Data = new Cache_Data_Object[2048]");
        AssertOrder(cache, "AdvanceReplayCodeGenerationEpoch();",
            "if (!_hasMaterializedVliwFetchState)", "InvalidateVliwBundleCacheLine(L1_VLIWBundles",
            "InvalidateVliwBundleCacheLine(L2_VLIWBundles", "_loopBuffer.Invalidate(");
        AssertOrder(cache, "if (_hasMaterializedVliwFetchState)",
            "ClearVliwBundleCache(L1_VLIWBundles);", "ClearVliwBundleCache(L2_VLIWBundles);",
            "_hasMaterializedVliwFetchState = false;", "_loopBuffer.Invalidate(invalidationReason);");
    }

    [Fact]
    public void ClosedWorldReadersStayInCacheAssistObservationAndTestSupportContours()
    {
        string root = FindRoot();
        string[] expected =
        [
            Path.Combine("CloseToHSL", "Core", "Architecture", "State", "Architectural", "CPU_Core.StateData.cs"),
            Path.Combine("CloseToHSL", "Core", "Execution", "Memory", "Cache", "CPU_Core.Cache.cs"),
            Path.Combine("CloseToHSL", "Core", "Execution", "Memory", "Cache", "CPU_Core.Cache.Assist.cs"),
            Path.Combine("CloseToHSL", "Core", "Memory", "MemoryCoherencyObserver.cs"),
            Path.Combine("CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "CPU_Core.Assist.cs"),
            Path.Combine("CloseToHSL", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs"),
            Path.Combine("CloseToHSL", "Core", "State", "CPU_Core.RuntimeState.cs"),
            Path.Combine("Machine", "IseObservationService.cs")
        ];
        string production = Path.Combine(root, "HybridCPU_ISE");
        string[] actual = Directory.GetFiles(production, "*.cs", SearchOption.AllDirectories)
            .Where(file => CacheNames.Any(name => File.ReadAllText(file).Contains(name, StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(production, file))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(file => file, StringComparer.Ordinal), actual);
    }

    [Fact]
    public void TimingCoherenceReplayAndFrontendAuthoritiesRemainSeparate()
    {
        Type cache = Required("YAKSys_Hybrid_CPU.Core.CacheState");
        Assert.DoesNotContain(cache.GetFields(Flags), field =>
            field.FieldType.Name.Contains("MemoryCycle", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Replay", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Frontend", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Coherency", StringComparison.Ordinal));
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyCacheStorage()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.23-cache-state-extraction.md");
        Assert.Contains("RF-11.23 | closed CacheState", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly eight", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.24 MatrixTileState", ledger, StringComparison.Ordinal);
    }

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ??
        throw new InvalidOperationException(name);

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

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException();
    }
}
