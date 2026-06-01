namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureComputePlanAuditTests
{
    [Fact]
    public void OpenDecisionItems_AreCentralizedInBacklogOnly()
    {
        string planDirectory = PlanDirectory();
        string backlogPath = Path.Combine(Plan2Directory(), "14-securecompute-open-decision-backlog.md");

        Assert.True(File.Exists(backlogPath), "SecureCompute open-decision backlog must exist in Plan2/.");
        Assert.False(
            File.Exists(Path.Combine(planDirectory, "14-securecompute-open-decision-backlog.md")),
            "SecureCompute open-decision backlog must be moved out of Plan/ into Plan2/.");

        List<string> strayOpenItems = new();
        foreach (string path in Directory.EnumerateFiles(planDirectory, "*.md").OrderBy(path => path, StringComparer.Ordinal))
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index].TrimStart().StartsWith("- [ ]", StringComparison.Ordinal))
                {
                    strayOpenItems.Add($"{Relative(path)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        Assert.True(
            strayOpenItems.Count == 0,
            "Open decision items must be transferred to Plan2/14-securecompute-open-decision-backlog.md:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, strayOpenItems));
    }

    [Fact]
    public void Backlog_ContainsAllKnownUnresolvedDecisionPools()
    {
        string backlog = ReadPlan2("14-securecompute-open-decision-backlog.md");

        Assert.Contains("Positive secure backend runtime execution owner/RFC: unopened", backlog);
        Assert.Contains("SecureCompute compatibility advertisement policy", backlog);
        Assert.Contains("Secure visibility alias placement", backlog);
        Assert.Contains("Future capability-aware ISA profile shape", backlog);
        Assert.Contains("Future 2048-bit bundle legality for capability operands", backlog);
        Assert.Contains("Future tag/provenance migration format", backlog);

        Assert.Contains("Phase 10 is closed as conformance/doc/source release gate, not feature-complete SecureCompute", backlog);
        Assert.Contains("AllowedProofOnlyNoExecution` does not authorize runtime execution", backlog);
        Assert.Contains("must not be read as permission to add VMX mode support, secure VMCS state, `VmxCaps` authority", backlog);

        int openItemCount = File
            .ReadLines(Path.Combine(Plan2Directory(), "14-securecompute-open-decision-backlog.md"))
            .Count(line => line.TrimStart().StartsWith("- [ ]", StringComparison.Ordinal));

        Assert.Equal(6, openItemCount);
    }

    [Fact]
    public void Phase14Backlog_RecordsAuditHardenedQuarantineRules()
    {
        string backlog = ReadPlan2("14-securecompute-open-decision-backlog.md");

        Assert.Contains("Phase 14 is an open-decision backlog, not an implementation phase.", backlog);
        Assert.Contains("No item in this file authorizes code changes without a separate RFC/ADR and phase plan.", backlog);
        Assert.Contains("Phase 14 backlog hardening closed on 2026-05-31", backlog);

        Assert.Contains("`AllowedProofOnlyNoExecution` must remain the maximum result until a separate runtime-execution phase exists.", backlog);
        Assert.Contains("No policy-evidence admission may publish completion, retire effects or backend-visible success.", backlog);

        Assert.Contains("Default decision: zero VMX exposure.", backlog);
        Assert.Contains("Any advertisement requires explicit neutral evidence owner, read-only projection, migration class and conformance proof.", backlog);
        Assert.Contains("Preferred default: separate neutral debug/attestation API.", backlog);
        Assert.Contains("VMX schema alias is allowed only as generated projection and must remain denied by default.", backlog);

        Assert.Contains("These decisions require a new repository-level architecture proposal.", backlog);
        Assert.Contains("They must not create placeholder product types under current SecureCompute Layer 1/2 namespaces.", backlog);
        Assert.Contains("No provisional capability operand metadata may be added to current typed slots, bundle legality or compiler helper ABI.", backlog);
        Assert.Contains("Current SecureMigrationDescriptor must not grow provisional tag/provenance payload classes.", backlog);
        Assert.Contains("Future tag/provenance migration requires separate format, restore validation and host-evidence separation proof.", backlog);
    }

    [Fact]
    public void Plan2RfcGuidance_IsAdvisoryOnlyAndDoesNotCloseExecution()
    {
        string rfcGuidance = ReadPlan2("SecureCompute RFC HybridCPU-v2.md");

        Assert.Contains("this RFC guidance is advisory input only", rfcGuidance);
        Assert.Contains("It does not authorize product code, backend execution, completion publication, retire effects", rfcGuidance);
        Assert.Contains("Any implementation requires a separate approved RFC/ADR, phase plan, proof chain and negative conformance tests.", rfcGuidance);
        Assert.Contains("RFC-14A: Positive Secure Backend Runtime Execution Owner", rfcGuidance);
        Assert.Contains("AllowedProofOnlyNoExecution", rfcGuidance);
        Assert.Contains("negative conformance coverage", rfcGuidance);
    }

    [Fact]
    public void CompletedPhasePlans_DoNotRetainStaleOpenStatus()
    {
        string allPlans = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(PlanDirectory(), "*.md")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("Next dependent work: Phase 7", allPlans, StringComparison.Ordinal);
        Assert.DoesNotContain("What exact project/folder shape should `HybridCPU_ISE.Tests/SecureComputeRefactoring`", allPlans, StringComparison.Ordinal);
        Assert.DoesNotContain("Future coverage pool remains unopened", allPlans, StringComparison.Ordinal);
        Assert.DoesNotContain("Future runtime-execution pool remains unopened", allPlans, StringComparison.Ordinal);
        Assert.DoesNotContain("Remaining Layer 2 work:", allPlans, StringComparison.Ordinal);
        Assert.DoesNotContain("Future quarantine remains outside current SecureCompute", allPlans, StringComparison.Ordinal);

        Assert.Contains("Phase 7 dependency closed on 2026-05-31", ReadPlan("03-layer1-secure-memory-plan.md"));
        Assert.Contains("neutral runtime tests use `HybridCPU_ISE.Tests/SecureComputeRefactoring`", ReadPlan("11-test-and-conformance-master-plan.md"));
        Assert.Contains("Phase 0-10, Phase 1.5 and the Post-Phase10 owner/RFC proof gate", ReadPlan("12-phasing-and-pr-breakdown.md"));
        Assert.Contains("future capability-aware ISA profile shape remains open", ReadPlan("13-future-capability-aware-isa-memory-layer.md"));
    }

    private static string ReadPlan(string fileName) =>
        File.ReadAllText(Path.Combine(PlanDirectory(), fileName));

    private static string ReadPlan2(string fileName) =>
        File.ReadAllText(Path.Combine(Plan2Directory(), fileName));

    private static string PlanDirectory() =>
        Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "CloseToRTL",
            "Core",
            "Runtime",
            "Domains",
            "SecureCompute",
            "Plan");

    private static string Plan2Directory() =>
        Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "CloseToRTL",
            "Core",
            "Runtime",
            "Domains",
            "SecureCompute",
            "Plan2");

    private static string Relative(string path) =>
        Path.GetRelativePath(FindRepositoryRoot(), path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
