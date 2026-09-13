namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1212aFinalExitAuditEvidenceCompletenessTests
{
    [Fact]
    public void FinalAuditMatrixNamesEveryPaperFamilyAndItsClosureEvidence()
    {
        string root = FindRepositoryRoot();
        string audit = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12a-final-exit-audit-evidence-completeness-amendment.md");
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        string[] requiredRows =
        [
            "SMT VT: sole `VtId`", "architectural and physical register",
            "bundle slot: `SlotId`", "physical lane and pinning: `LaneId`",
            "scheduler-visible bank: `MemoryBankId`", "topology-local bank position",
            "DMA channel: `DmaChannelId`", "stream engine: `StreamEngineId`",
            "accelerator / I-O device and queue", "execution owner context and domain tag",
            "I/O/address-space domain, translation tag and certificates",
            "accepted request and allocated token handles",
            "issued attempt, replay token, epoch and donor epoch",
            "replay/certificate template identity"
        ];

        foreach (string row in requiredRows)
            Assert.Contains(row, audit, StringComparison.Ordinal);

        Assert.Contains("No universal channel, domain, device, token,", paper, StringComparison.Ordinal);
        string reconciliation = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12b-external-audit-reconciliation-and-reopened-handoff.md");
        Assert.Contains("closure verdict is superseded by this reconciliation", reconciliation, StringComparison.Ordinal);
        Assert.Contains("No compatibility API has the required zero caller inventory", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryReferencedFinalArtifactExistsAndGenericIdentityTypesRemainAbsent()
    {
        string root = FindRepositoryRoot();
        string audit = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12a-final-exit-audit-evidence-completeness-amendment.md");
        string production = ReadTree(root, "HybridCPU_ISE");
        string[] artifacts =
        [
            "rf12.7ak-dma-channel-family-final-closed-world-exit-audit.md",
            "rf12.7bk-stream-engine-family-final-closed-world-exit-audit.md",
            "rf12.7bu-accelerator-device-id-final-closed-world-exit-audit.md",
            "rf12.8j-execution-domain-tag-family-final-closed-world-exit-audit.md",
            "rf12.8q-iommu-domain-binding-family-final-closed-world-exit-audit.md",
            "rf12.8x-address-space-nested-tlb-family-final-closed-world-exit-audit.md",
            "rf12.9g-memory-request-id-family-final-closed-world-exit-audit.md",
            "rf12.9av-replay-token-family-final-closed-world-exit-audit.md",
            "rf12.9cb-phase-certificate-template-key-final-exit-audit.md"
        ];

        foreach (string artifact in artifacts)
        {
            Assert.Contains(artifact, audit, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", artifact)));
        }

        Assert.DoesNotMatch(@"\b(?:record\s+struct|struct|class)\s+(?:VirtualThreadId|ChannelId|DomainId|TokenId)\b", production);
        Assert.Contains("reflection and `CPU_Core.TestSupport` remain observable", audit, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(path).ToArray()));

    private static string ReadTree(string root, params string[] path) =>
        string.Join("\n", Directory.EnumerateFiles(Path.Combine(new[] { root }.Concat(path).ToArray()), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

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
