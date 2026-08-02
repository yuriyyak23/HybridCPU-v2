namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1212cFinalPostReconciliationExitAuditTests
{
    [Fact]
    public void CurrentLedgerClosesOnlyAfterThePostReconciliationEvidenceChain()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        string final = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12c-final-post-reconciliation-closed-world-exit-audit.md");

        Assert.Contains("RF-12 is closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12 | superseded exit audit", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12a | superseded audit amendment", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12b | closed reconciliation", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12c | superseded final exit audit", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12d | closed current-status reconciliation", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12h | closed audit-disposition and stale-wording reconciliation", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 overall | closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 is **closed**", final, StringComparison.Ordinal);

        string[] required =
        [
            "rf12.5j-raw-pinning-compatibility-retention-decision.md",
            "rf12.10a-complete-parser-serializer-compiler-runtime-bridge-matrix.md",
            "rf12.11a-expanded-bridge-compatibility-deletion-eligibility-reconciliation.md",
            "rf12.12c-final-post-reconciliation-closed-world-exit-audit.md"
        ];
        foreach (string artifact in required)
            Assert.True(File.Exists(Path.Combine(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", artifact)));
    }

    [Fact]
    public void GenericFamiliesRemainAbsentAndConstructorDistinctionsAreExplicit()
    {
        string root = FindRepositoryRoot();
        string production = ReadTree(root, "HybridCPU_ISE") + ReadTree(root, "HybridCPU_Compiler");
        string memoryController = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");
        string final = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12c-final-post-reconciliation-closed-world-exit-audit.md");

        Assert.DoesNotMatch(@"\b(?:record\s+struct|struct|class)\s+(?:VirtualThreadId|ChannelId|DomainId|TokenId)\b", production);
        Assert.Contains("public readonly record struct MemoryRequestId(ulong Value)", memoryController, StringComparison.Ordinal);
        Assert.Contains("`MemoryRequestId(ulong)`", final, StringComparison.Ordinal);
        Assert.Contains("public raw `MemoryRequestToken`", final, StringComparison.Ordinal);
        Assert.Contains("constructor remains absent", final, StringComparison.Ordinal);
        Assert.Contains("`DmaStreamComputeTokenHandle`", final, StringComparison.Ordinal);
        Assert.Contains("`AcceleratorTokenHandle(ulong)`", final, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPinnedLaneValidationDominatesTheStageBShift()
    {
        string root = FindRepositoryRoot();
        string scheduler = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Admission", "MicroOpScheduler.Admission.cs");
        int validation = scheduler.IndexOf("LaneId.TryCreate(candidate.Placement.PinnedLaneId", StringComparison.Ordinal);
        int rejection = scheduler.IndexOf("TypedSlotRejectReason.InvalidPinnedLane", StringComparison.Ordinal);
        int shift = scheduler.IndexOf("1 << lane", StringComparison.Ordinal);

        Assert.True(validation >= 0);
        Assert.True(rejection > validation);
        Assert.True(shift > rejection);
        Assert.Contains("selectedLane = -1;", scheduler, StringComparison.Ordinal);
    }

    [Fact]
    public void FinalAuditCoversOwnersRawSeamsBridgesInvalidPathsAndInvariants()
    {
        string root = FindRepositoryRoot();
        string final = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12c-final-post-reconciliation-closed-world-exit-audit.md");
        string[] sections =
        [
            "Closed-world owner and family exit matrix", "Invalid, default and bypass exit audit",
            "Constructor, arithmetic, key and removal audit", "Bridge and evidence-chain exit audit",
            "Preserved invariants and limitations"
        ];
        foreach (string section in sections)
            Assert.Contains($"## {section}", final, StringComparison.Ordinal);

        Assert.Contains("Stage A/B topology", final, StringComparison.Ordinal);
        Assert.Contains("SMT/FSP owner/donor", final, StringComparison.Ordinal);
        Assert.Contains("bounded", final, StringComparison.Ordinal);
        Assert.Contains("retirement/publication", final, StringComparison.Ordinal);
        Assert.Contains("timed-memory and RF-11 ownership", final, StringComparison.Ordinal);
        Assert.Contains("There is no next RF-12 task", final, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(path).ToArray()));

    private static string ReadTree(string root, string path) =>
        string.Join("\n", Directory.EnumerateFiles(Path.Combine(root, path), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
