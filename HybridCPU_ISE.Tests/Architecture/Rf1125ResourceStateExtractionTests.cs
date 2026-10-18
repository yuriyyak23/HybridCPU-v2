using System.Reflection;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1125ResourceStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly string[] LegacyNames =
    [
        "globalResourceLocks", "tokenGeneration", "resourceTokens", "StructuralStalls",
        "resourceUsageCounts", "resourceContentionCounts", "_readCounters", "syncCounter",
        "_grlbBanks", "_bankContentionCounts"
    ];

    [Fact]
    public void RuntimeContainsExactStorageOnlyResourceState()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type resources = Required("YAKSys_Hybrid_CPU.Core.ResourceState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == resources);
        Assert.Equal(new[]
        {
            "BankContentionCounts", "GlobalResourceLocks", "GrlbBanks", "ReadCounters",
            "ResourceContentionCounts", "ResourceTokens", "ResourceUsageCounts", "StructuralStalls",
            "SyncCounter", "TokenGeneration"
        }, resources.GetFields(Flags).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(7, resources.GetFields(Flags).Count(field => field.FieldType == typeof(ulong) || field.FieldType == typeof(ulong[])));
        Assert.DoesNotContain(resources.GetMethods(Flags), method => method.Name is
            "Acquire" or "Release" or "Execute" or "Commit" or "Rollback" or "Publish" or "Reset");
    }

    [Fact]
    public void LegacyDirectFieldsAreRemovedAndFacadeShapeIsFrozen()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in LegacyNames)
            Assert.Null(core.GetField(name, Flags));

        foreach (string name in LegacyNames.Where(name => name != "StructuralStalls"))
            Assert.True((core.GetProperty(name, Flags) ??
                throw new InvalidOperationException(name)).PropertyType.IsByRef);

        PropertyInfo structural = core.GetProperty("StructuralStalls", Flags) ??
            throw new InvalidOperationException("StructuralStalls");
        Assert.Equal(typeof(ulong), structural.PropertyType);
        Assert.True(structural.GetMethod?.IsPublic);
        Assert.True(structural.SetMethod?.IsPrivate);
    }

    [Fact]
    public void ConstructionAndCopiesShareResourceAndSyncIdentity()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        Assert.Same(core.Runtime.Resources, copy.Runtime.Resources);
        Assert.Equal(128, core.Runtime.Resources.ResourceTokens.Length);
        Assert.Equal(128, core.Runtime.Resources.ResourceUsageCounts.Length);
        Assert.Equal(128, core.Runtime.Resources.ResourceContentionCounts.Length);
        Assert.Equal(16, core.Runtime.Resources.ReadCounters.Length);
        Assert.Equal(4, core.Runtime.Resources.GrlbBanks.Length);
        Assert.Equal(4, core.Runtime.Resources.BankContentionCounts.Length);

        var mask = new ResourceBitset(1UL << 20, 0);
        Assert.True(core.AcquireResourcesWithToken(mask, out ulong token));
        Assert.True(copy.AreResourcesLocked(mask));
        Assert.False(copy.AcquireResources(mask));
        Assert.Equal(1UL, core.StructuralStalls);
        copy.ReleaseResourcesWithToken(mask, token);
        Assert.False(core.AreResourcesLocked(mask));

        core.IncrementSyncCounter();
        Assert.Equal(1UL, copy.GetSyncCounter());
        copy.ResetSyncCounter();
        Assert.Equal(0UL, core.GetSyncCounter());
    }

    [Fact]
    public void ExistingAcquireReleaseResetAndObservationAuthoritiesRemainInPlace()
    {
        string root = FindRoot();
        string grlb = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire",
            "Rollback", "CPU_Core.GRLB.cs");
        AssertOrder(grlb, "token = ++tokenGeneration;", "globalResourceLocks = new Core.ResourceBitset(",
            "resourceTokens[bit] = token;", "resourceUsageCounts[bit]++;");
        AssertOrder(grlb, "public void ReleaseResourcesWithToken", "if (resourceTokens[bit] == token)",
            "globalResourceLocks.Low &= ~bitMask;", "SyncBanksFromUnified();");
        AssertOrder(grlb, "public void ClearAllResourceLocks", "globalResourceLocks = Core.ResourceBitset.Zero;",
            "_grlbBanks[0] = 0;", "_readCounters[i] = 0;");
        AssertOrder(grlb, "public void ResetGRLBCounters", "StructuralStalls = 0;",
            "resourceUsageCounts[i] = 0;", "resourceContentionCounts[i] = 0;",
            "_bankContentionCounts[i] = 0;");

        string sync = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Synchronization",
            "Processor.SyncPrimitives.cs");
        Assert.Contains("SyncPrimitives.FetchAndIncrement(ref syncCounter);", sync, StringComparison.Ordinal);
        Assert.Contains("SyncPrimitives.AtomicStoreRelease(ref syncCounter, 0);", sync, StringComparison.Ordinal);
        string observation = Read(root, "HybridCPU_ISE", "Machine", "IseObservationService.cs");
        Assert.Contains("StructuralStalls = core.StructuralStalls", observation, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedWorldNameInventoryHasNoReflectionSerializationOrDuplicateStorageSeam()
    {
        string root = FindRoot();
        string production = Path.Combine(root, "HybridCPU_ISE");
        string[] expected =
        [
            Path.Combine("CloseToHSL", "Core", "Pipeline", "Assist", "InterCore", "MicroOpScheduler.Assist.InterCore.cs"),
            Path.Combine("CloseToHSL", "Core", "Pipeline", "MicroOps", "Types", "MicroOp.cs"),
            Path.Combine("CloseToHSL", "Core", "Pipeline", "Retire", "Rollback", "CPU_Core.GRLB.cs"),
            Path.Combine("CloseToHSL", "Core", "Pipeline", "Scheduling", "BundlePacking", "MicroOpScheduler.PackBundle.cs"),
            Path.Combine("CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs"),
            Path.Combine("CloseToHSL", "Core", "State", "CPU_Core.RuntimeState.cs"),
            Path.Combine("CloseToHSL", "Core", "State", "ResourceState.cs"),
            Path.Combine("CloseToHSL", "Processor", "Core", "PodController.cs"),
            Path.Combine("Machine", "IseObservationService.cs"),
            Path.Combine("NonRTL", "Processor", "Synchronization", "Processor.SyncPrimitives.cs")
        ];
        string[] actual = Directory.GetFiles(production, "*.cs", SearchOption.AllDirectories)
            .Where(file => LegacyNames.Any(name => (File.ReadAllText(file) ?? string.Empty).Contains(name, StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(production, file))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(file => file, StringComparer.Ordinal), actual);

        string[] reflectionMutationSeams = Directory.GetFiles(Path.Combine(root, "HybridCPU_ISE.Tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith(nameof(Rf1125ResourceStateExtractionTests) + ".cs", StringComparison.Ordinal))
            .Where(file => !file.EndsWith("Rf1116ResidualOwnerAndFacadeReadinessTests.cs", StringComparison.Ordinal))
            .Where(file =>
            {
                string text = File.ReadAllText(file);
                return text.Contains("SetValueDirect", StringComparison.Ordinal) &&
                    LegacyNames.Any(name => text.Contains(name, StringComparison.Ordinal));
            })
            .ToArray();
        Assert.Empty(reflectionMutationSeams);
        Assert.DoesNotContain("JsonSerializer.Serialize(core.Runtime.Resources", ReadSources(production), StringComparison.Ordinal);
    }

    [Fact]
    public void SchedulingRetireMemoryAndBackendAuthoritiesRemainOutsideResourceStorage()
    {
        Type resources = Required("YAKSys_Hybrid_CPU.Core.ResourceState");
        Assert.DoesNotContain(resources.GetFields(Flags), field =>
            field.FieldType.Name.Contains("Scheduler", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Retire", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Memory", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("RegisterFile", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Replay", StringComparison.Ordinal));
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyResourceStorage()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.25-resource-state-extraction.md");
        Assert.Contains("RF-11.25 | closed ResourceState", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly ten", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.26 VirtualThreadControlState", ledger, StringComparison.Ordinal);
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

    private static string ReadSources(string path) => string.Join('\n',
        Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));
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
