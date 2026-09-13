using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1122ScratchStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly string[] ScratchNames =
    [
        "ActiveBufferSet", "BankedScratchA", "BankedScratchB", "BankedScratchDst",
        "ScratchA", "ScratchA_DB0", "ScratchA_DB1", "ScratchB", "ScratchB_DB0",
        "ScratchB_DB1", "ScratchDst", "ScratchDst_DB0", "ScratchDst_DB1", "ScratchIndex"
    ];

    [Fact]
    public void RuntimeContainsExactFourteenFieldScratchState()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type scratch = Required("YAKSys_Hybrid_CPU.Core.ScratchState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == scratch);
        Assert.Equal(ScratchNames, scratch.GetFields(Flags).Select(field => field.Name)
            .OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(10, scratch.GetFields(Flags).Count(field => field.FieldType == typeof(byte[])));
        Assert.Equal(3, scratch.GetFields(Flags).Count(field => field.FieldType.Name == "ScratchBankController"));
        Assert.Single(scratch.GetFields(Flags), field => field.FieldType == typeof(int));
        Assert.DoesNotContain(scratch.GetMethods(Flags), method =>
            method.Name is "Execute" or "Commit" or "Rollback" or "Publish" or "AdvanceCycle");
    }

    [Fact]
    public void LegacyFieldsAreRemovedAndForwardByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in ScratchNames)
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ??
                throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void ConstructionAndCopiesKeepOneInitializedScratchIdentity()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;

        Assert.Equal(256, core.GetScratchA().Length);
        Assert.Equal(256, core.GetScratchB().Length);
        Assert.Equal(256, core.GetScratchDst().Length);
        Assert.Equal(256, core.GetScratchIndex().Length);
        Assert.Equal(0, core.GetActiveBufferSet());

        core.ToggleDoubleBuffer();
        Assert.Equal(1, copy.GetActiveBufferSet());
        Assert.Same(core.Runtime.Scratch.ScratchA, copy.Runtime.Scratch.ScratchA);
    }

    [Fact]
    public void InitializationBufferSelectionAndBankConflictCallsRemainOrdered()
    {
        string source = Read(FindRoot(), "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "Memory", "Scratch", "CPU_Core.StreamScratch.cs");
        AssertOrder(source, "ActiveBufferSet = 0;", "ScratchA = new byte[SCRATCH_BUFFER_SIZE];",
            "ScratchIndex = new byte[SCRATCH_BUFFER_SIZE];", "ScratchA_DB0 = new byte[SCRATCH_BUFFER_SIZE];",
            "ScratchDst_DB1 = new byte[SCRATCH_BUFFER_SIZE];", "BankedScratchA.Initialize();",
            "BankedScratchDst.Initialize();");
        Assert.Contains("ActiveBufferSet = 1 - ActiveBufferSet", source, StringComparison.Ordinal);
        Assert.Contains("return BankedScratchA.CheckConflict", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TestOnlyReflectionMutationTargetsNestedLiveOwner()
    {
        string test = Read(FindRoot(), "HybridCPU_ISE.Tests", "tests",
            "Phase09StreamEngineDeferredParityTests.cs");
        Assert.Contains("typeof(ScratchState).GetField", test, StringComparison.Ordinal);
        Assert.Contains("field.SetValue(core.Runtime.Scratch, value);", test, StringComparison.Ordinal);
        Assert.DoesNotContain("field.SetValueDirect(__makeref(core), value);", test, StringComparison.Ordinal);
        Assert.Contains("not a CPU_Core typed-reference", test, StringComparison.Ordinal);
    }

    [Fact]
    public void TimedMemoryArchitectureAndPublicationAuthoritiesRemainSeparate()
    {
        Type scratch = Required("YAKSys_Hybrid_CPU.Core.ScratchState");
        Assert.DoesNotContain(scratch.GetFields(Flags), field =>
            field.FieldType.Name.Contains("MemoryCycle", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Retire", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Architectural", StringComparison.Ordinal));
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyScratchStorage()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.22-scratch-state-extraction.md");
        Assert.Contains("RF-11.22 | closed ScratchState", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly fourteen", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.23 CacheState", ledger, StringComparison.Ordinal);
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
