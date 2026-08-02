namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3c makes the narrow architecture authorization executable while
/// preserving the RF-08.3b fact that no production carrier exists yet.
/// </summary>
public sealed class Rf083cAuthorizedExactPostStageBHandoffTests
{
    [Fact]
    public void AuthorityPermitsOnlyTheSameSuccessfulStageBDecisionToCrossTheExistingTopology()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string adr = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "02_Authority",
            "ADR-009_VLIW_Retirement.md");

        foreach (string source in new[] { paper, adr })
        {
            Assert.Contains("PostStageBIssuedAttempt", source, StringComparison.Ordinal);
            Assert.Contains("ScheduledOperation", source, StringComparison.Ordinal);
            Assert.Contains("GeneratedStaticBinding", source, StringComparison.Ordinal);
            Assert.Contains("successful Stage-B", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("RestoreUnmaterializedSmtCandidates", source, StringComparison.Ordinal);
            Assert.Contains("RuntimeClusterAdmissionHandoff", source, StringComparison.Ordinal);
            Assert.Contains("BundleIssuePacket", source, StringComparison.Ordinal);
        }

        Assert.Contains("second scheduler", paper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not rerun Stage A/B", adr, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorizationDoesNotAuthorizeAnyFamilyBeyondTheScalarRegisterWriteContour()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] productionReferences = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("PostStageBIssuedAttempt", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(productionReferences, path =>
            path.EndsWith("PostStageBIssuedAttempt.cs", StringComparison.OrdinalIgnoreCase));

        string migration = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "04_CoreMigration",
            "04_RF07_RF13_Core_Migration.md");
        Assert.Contains("RF-08.3d scalar RegisterWrite approved-carrier transport", migration, StringComparison.Ordinal);
        Assert.Contains("RegisterWrite transport", migration, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
