using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1119ResidualDomainAuthorityDecisionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void DecisionMapsEveryResidualContourWithoutUniversalAuthority()
    {
        string adr = Read(FindRoot(), "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "02_Authority", "ADR-011_RF11_Residual_State_Containment.md");
        foreach (string group in new[] { "TelemetryState", "AssistState", "ScratchState", "CacheState", "MatrixTileState", "ResourceState", "VirtualThreadControlState", "LegacyCompatibilityState", "CoreBindingState" })
            Assert.Contains($"`{group}`", adr, StringComparison.Ordinal);
        foreach (string operation in new[] { "Execute", "Commit", "Publish", "Rollback", "Fallback", "Checkpoint", "Migrate" })
            Assert.Contains(operation, adr, StringComparison.Ordinal);
        Assert.Contains("None exposes", adr, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentProductionRootGrowsOnlyInDecisionOrder()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Assert.InRange(runtime.GetFields(Flags).Length, 12, 20);
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType.Name == "AssistState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType.Name == "ScratchState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType.Name == "CacheState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType.Name == "ResourceState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType.Name == "VirtualThreadControlState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType.Name == "LegacyCompatibilityState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType.Name == "CoreBindingState");
        Type extensions = Required("YAKSys_Hybrid_CPU.Core.ExtensionState");
        Assert.Single(extensions.GetFields(Flags), field => field.FieldType.Name == "MatrixTileState");
        FieldInfo[] coreFields = typeof(Processor.CPU_Core).GetFields(Flags);
        Assert.InRange(coreFields.Length, 1, 50);
        Assert.Contains(coreFields, field => field.Name == "_runtime" && field.IsInitOnly);
    }

    [Fact]
    public void DecisionPreservesSpecialOwnerBoundaries()
    {
        string adr = Read(FindRoot(), "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "02_Authority", "ADR-011_RF11_Residual_State_Containment.md");
        Assert.Contains("CacheState` is not `MemoryCycleController", adr, StringComparison.Ordinal);
        Assert.Contains("AssistState` is not scheduler/replay/retire authority", adr, StringComparison.Ordinal);
        Assert.Contains("does not unify MatrixTile, DSC or L7", adr, StringComparison.Ordinal);
        Assert.Contains("not neutral-virtualization or VMCS authority", adr, StringComparison.Ordinal);
        Assert.Contains("not RF-13 removal", adr, StringComparison.Ordinal);
        Assert.Contains("cannot replace `CoreRuntimeState` during a live cycle", adr, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionFreezesSingleGroupOrderAndSnapshotBlocker()
    {
        string adr = Read(FindRoot(), "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "02_Authority", "ADR-011_RF11_Residual_State_Containment.md");
        AssertOrder(adr, "RF-11.20 `TelemetryState`", "`AssistState`;", "`ScratchState`;", "`CacheState`;", "nested `MatrixTileState`;", "`ResourceState`;", "`VirtualThreadControlState`;", "`LegacyCompatibilityState`;", "`CoreBindingState`;", "diagnostic snapshot hardening");
        Assert.Contains("true detached", adr, StringComparison.Ordinal);
        Assert.Contains("One slice may move only the named group", adr, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerAndEvidenceCloseDecisionOnly()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.19-residual-domain-authority-decision.md");
        Assert.Contains("RF-11.19 | closed residual-domain authority decision", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.20 TelemetryState", ledger, StringComparison.Ordinal);
        Assert.Contains("moves no state", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TestAssemblerConsoleApps was not run", evidence, StringComparison.Ordinal);
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
