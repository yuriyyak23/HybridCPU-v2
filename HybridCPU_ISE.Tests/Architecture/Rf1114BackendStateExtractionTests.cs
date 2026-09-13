using System.Reflection;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1114BackendStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void RuntimeContainsExactPrfRenameCommitAndFreeListContour()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type backend = Required("YAKSys_Hybrid_CPU.Core.BackendState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == backend);
        Assert.Equal(new[] { "CommitMap", "FreeList", "PhysicalRegisters", "RenameMap" },
            backend.GetFields(Flags).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Empty(backend.GetMethods(Flags).Where(method =>
            method.Name is "Commit" or "Rollback" or "Retire" or "Publish" or "Execute"));
    }

    [Fact]
    public void LegacyStorageIsRemovedAndAllFourNamesForwardByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "PhysicalRegisters", "ArchRenameMap", "ArchCommitMap", "PhysRegFreeList" })
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void FacadeCopiesAndRetireCoordinatorShareTheSameBackendObjects()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        Assert.Same(core.Runtime.Backend, copy.Runtime.Backend);
        Assert.Same(core.PhysicalRegisters, copy.PhysicalRegisters);
        Assert.Same(core.ArchRenameMap, copy.ArchRenameMap);
        Assert.Same(core.ArchCommitMap, copy.ArchCommitMap);
        Assert.Same(core.PhysRegFreeList, copy.PhysRegFreeList);

        Type retireType = core.RetireCoordinator.GetType();
        Assert.Same(core.PhysicalRegisters, retireType.GetField("_physicalRegisters", Flags)!.GetValue(core.RetireCoordinator));
        Assert.Same(core.ArchRenameMap, retireType.GetField("_archRenameMap", Flags)!.GetValue(core.RetireCoordinator));
        Assert.Same(core.ArchCommitMap, retireType.GetField("_archCommitMap", Flags)!.GetValue(core.RetireCoordinator));
    }

    [Fact]
    public void ExistingConstructionReadSetupAndRetirePathsRemainExact()
    {
        string root = FindRoot();
        string state = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State", "Architectural", "CPU_Core.StateData.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Registers", "Retire", "RetireCoordinator.cs");
        AssertOrder(state, "new PhysicalRegisterFile()", "new RenameMap(SmtWays)", "new CommitMap(SmtWays)", "new FreeList()", "new RetireCoordinator(");
        Assert.Contains("ArchRenameMap.Lookup(vtId, archReg)", state, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(RetireRecord.RegisterWrite", state, StringComparison.Ordinal);
        Assert.Contains("_archRenameMap.Lookup", retire, StringComparison.Ordinal);
        Assert.Contains("_physicalRegisters.Write", retire, StringComparison.Ordinal);
        Assert.Contains("_archCommitMap.Commit", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchitecturalRetireReplaySchedulerAndExtensionAuthoritiesRemainOutside()
    {
        Type backend = Required("YAKSys_Hybrid_CPU.Core.BackendState");
        Assert.DoesNotContain(backend.GetFields(Flags), field =>
            field.FieldType.Name is "ArchContextState" or "RetireCoordinator" or "LoopBuffer" or "MicroOpScheduler" ||
            field.Name.Contains("Matrix", StringComparison.Ordinal) ||
            field.Name.Contains("Checkpoint", StringComparison.Ordinal));

        Type core = typeof(Processor.CPU_Core);
        Assert.True((core.GetProperty("ArchContexts", Flags) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.True((core.GetProperty("RetireCoordinator", Flags) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.True((core.GetProperty("_matrixTileRegisterFile", Flags) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyBackendState()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.14-backend-state-extraction.md");
        Assert.Contains("RF-11.14 | closed BackendState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.15 ExtensionState", ledger, StringComparison.Ordinal);
        Assert.Contains("allocation", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RestoreInto", evidence, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator", evidence, StringComparison.Ordinal);
    }

    private static void AssertOrder(string text, params string[] markers)
    {
        int previous = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(current > previous, $"Expected '{marker}' after prior marker.");
            previous = current;
        }
    }

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ?? throw new InvalidOperationException(name);
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null) { if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current; current = Directory.GetParent(current)?.FullName; }
        throw new DirectoryNotFoundException();
    }
}
