using System.Reflection;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf111StableCoreIdentityDecisionTests
{
    [Fact]
    public void DecisionDefinesStableReferenceFacadeWithoutPrematureRuntimeExtraction()
    {
        string root = FindRepositoryRoot();
        string decision = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "02_Authority",
            "ADR-010_CPU_Core_State_Ownership.md");

        Assert.Contains("target facade is a reference type", decision, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exactly one live `CoreRuntimeState` identity", decision, StringComparison.Ordinal);
        Assert.Contains("Containment does not transfer semantic authority", decision, StringComparison.Ordinal);
        Assert.Contains("temporary implementation under", decision, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("strict no-copy-as-mutation freeze", decision, StringComparison.Ordinal);

        Type core = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core);
        Assert.False(core.IsValueType);
        Type runtimeRoot = core.Assembly.GetType("YAKSys_Hybrid_CPU.Core.CoreRuntimeState") ??
            throw new InvalidOperationException("CoreRuntimeState was not found.");
        FieldInfo[] runtimeFields = runtimeRoot.GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.DeclaredOnly);
        Assert.Equal(
            new[] { "AdmissionState", "ArchitecturalState", "AssistState", "BackendState", "CacheState", "CoreBindingState", "DecodeState", "ExecutionState", "ExtensionState", "FrontendState", "LegacyCompatibilityState", "MemoryPipelineState", "ReplayState", "ResourceState", "RetireState", "SchedulingState", "ScratchState", "TelemetryState", "VirtualThreadControlState" },
            runtimeFields.Select(field => field.FieldType.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void DecisionKeepsBackendRetireReplayMemoryAndExtensionAuthoritiesDistinct()
    {
        string decision = Decision();

        Assert.Contains("`PhysicalRegisterFile`, per-VT `RenameMap`, per-VT `CommitMap`, `FreeList`", decision, StringComparison.Ordinal);
        Assert.Contains("`RetireCoordinator` remains functional authority", decision, StringComparison.Ordinal);
        Assert.Contains("`MemoryCycleController` remains timed-memory owner", decision, StringComparison.Ordinal);
        Assert.Contains("replay token is not a rename checkpoint", decision, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ExtensionState` must not provide common `Execute`, `Commit` or `Fallback`", decision, StringComparison.Ordinal);
        Assert.Contains("No universal commit, rollback, execute, fallback, checkpoint or migration", decision, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionMapsFrozenCycleOrderAndEarlyReturnWithoutCurrentNextRewrite()
    {
        string decision = Decision();
        AssertOrder(decision,
            "cycle counters",
            "timed-memory observed platform edge",
            "explicit-memory completion refresh",
            "hazard evaluation and existing early return",
            "WB / bounded retirement",
            "MEM",
            "EX",
            "Decode including current admission/issue work",
            "Fetch",
            "end-cycle timeline");

        Assert.Contains("may not change early-return reachability", decision, StringComparison.Ordinal);
        Assert.Contains("may not introduce strict current/next latch semantics", decision, StringComparison.Ordinal);
        Assert.Contains("Decode/admission conceptual containment does not authorize physically", decision, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionDefinesSnapshotLiveReplacementDualWriteAndRollbackRules()
    {
        string decision = Decision();

        Assert.Contains("`GetCoreSnapshot` is read-only diagnostic observation", decision, StringComparison.Ordinal);
        Assert.Contains("`GetCoreRef` or `GetCoreHandle` names live identity-preserving access", decision, StringComparison.Ordinal);
        Assert.Contains("`ReplaceCore` is an explicit platform lifecycle operation only", decision, StringComparison.Ordinal);
        Assert.Contains("No dual-write is allowed", decision, StringComparison.Ordinal);
        Assert.Contains("remain reversible by restoring that group's previous facade storage", decision, StringComparison.Ordinal);
        Assert.Contains("RF-11.1 changes no production state declaration", decision, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerClosesOnlyDecisionAndQueuesIdentityHardening()
    {
        string root = FindRepositoryRoot();
        string status = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.1-stable-core-identity-and-containment-decision.md");

        Assert.Contains("RF-11.1 | closed architecture decision", status, StringComparison.Ordinal);
        Assert.Contains("RF-11.1 | closed architecture decision", status, StringComparison.Ordinal);
        Assert.Contains("RF-11.2 identity and copy-seam hardening", evidence, StringComparison.Ordinal);
        Assert.Contains("moves no state", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("One next task", evidence, StringComparison.Ordinal);
    }

    private static string Decision()
    {
        string root = FindRepositoryRoot();
        return Read(root, "Documentation", "ArchitectureAuthorityRefactor", "02_Authority",
            "ADR-010_CPU_Core_State_Ownership.md");
    }

    private static void AssertOrder(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, prior + 1, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{marker}' after prior decision marker.");
            prior = current;
        }
    }

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
