using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1117FrontendStateResidualCompletionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void FrontendOwnsExactLatchBufferPredictorAndTwoLiveCursorFields()
    {
        Type frontend = Required("YAKSys_Hybrid_CPU.Core.FrontendState");
        Assert.Equal(new[]
        {
            "ActiveLivePc", "BranchPredictor", "Fetch", "FetchVliwBuffer",
            "HasMaterializedVliwFetchState"
        }, frontend.GetFields(Flags).Select(field => field.Name)
            .OrderBy(name => name, StringComparer.Ordinal));
        Assert.DoesNotContain(frontend.GetFields(Flags), field =>
            field.Name.Contains("Committed", StringComparison.Ordinal) ||
            field.Name.Contains("Cache", StringComparison.Ordinal) ||
            field.Name.Contains("Decode", StringComparison.Ordinal) ||
            field.Name.Contains("Admission", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoLegacyScalarsAreRemovedAndForwardByRefWithoutDualStorage()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "ulong_InstructionPointer", "_hasMaterializedVliwFetchState" })
        {
            Assert.Null(core.GetField(name, Flags));
            PropertyInfo property = core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name);
            Assert.True(property.PropertyType.IsByRef);
            Assert.True(property.GetMethod!.ReturnParameter.ParameterType.IsByRef);
        }
    }

    [Fact]
    public void TransitionalCopiesShareLivePcAndMaterializationIdentity()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;

        core.WriteActiveLivePc(0x4400);
        core.Runtime.Frontend.HasMaterializedVliwFetchState = true;

        Assert.Equal(0x4400UL, copy.ReadActiveLivePc());
        Assert.True(copy.Runtime.Frontend.HasMaterializedVliwFetchState);
    }

    [Fact]
    public void ExistingReadersWritersAndInvalidationOrderRemainUnchanged()
    {
        string root = FindRoot();
        string state = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State", "Architectural", "CPU_Core.StateData.cs");
        string cache = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Memory", "Cache", "CPU_Core.Cache.cs");
        Assert.Contains("this.ulong_InstructionPointer = 0;", state, StringComparison.Ordinal);
        Assert.Contains("public ulong ReadActiveLivePc() => ulong_InstructionPointer;", state, StringComparison.Ordinal);
        Assert.Contains("ulong_InstructionPointer += incrementValue;", state, StringComparison.Ordinal);
        Assert.Contains("ulong_InstructionPointer -= decrementValue;", state, StringComparison.Ordinal);
        AssertOrder(cache, "AdvanceReplayCodeGenerationEpoch();", "if (!_hasMaterializedVliwFetchState)", "_loopBuffer.Invalidate(");
        AssertOrder(cache, "if (_hasMaterializedVliwFetchState)", "ClearVliwBundleCache(L1_VLIWBundles);", "_hasMaterializedVliwFetchState = false;", "_loopBuffer.Invalidate(invalidationReason);");
    }

    [Fact]
    public void CommittedPcCacheReplayAndAdmissionOwnersRemainSeparate()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "L1_VLIWBundles", "L1_Data", "L2_VLIWBundles", "L2_Data", "Current_VLIWBundle_Position", "Current_DataObject_Position" })
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
        Assert.Single(Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState").GetFields(Flags),
            field => field.FieldType.Name == "CacheState");
        Type architectural = Required("YAKSys_Hybrid_CPU.Core.ArchitecturalState");
        Assert.Contains(architectural.GetFields(Flags), field => field.Name == "Contexts");
        Type frontend = Required("YAKSys_Hybrid_CPU.Core.FrontendState");
        Assert.DoesNotContain(frontend.GetFields(Flags), field => field.FieldType.Name is "ReplayState" or "AdmissionState");
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyFrontendResidualCompletion()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.17-frontend-state-residual-completion.md");
        Assert.Contains("RF-11.17 | closed FrontendState residual completion", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly two", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no current/next", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.18", ledger, StringComparison.Ordinal);
    }

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ?? throw new InvalidOperationException(name);
    private static void AssertOrder(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{marker}' after prior marker.");
            prior = current;
        }
    }
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null) { if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current; current = Directory.GetParent(current)?.FullName; }
        throw new DirectoryNotFoundException();
    }
}
