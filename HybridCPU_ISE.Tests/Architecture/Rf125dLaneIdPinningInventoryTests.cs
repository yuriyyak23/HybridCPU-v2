namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf125dLaneIdPinningInventoryTests
{

    [Fact]
    public void PaperTaxonomyRequiresLaneIdentityWithoutSelectingTheSchedulerInvalidWinner()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", "rf12.5d-laneid-pinning-closed-world-inventory.md");

        Assert.Contains("`LaneId` denotes only a post-Stage-B physical lane `0..7`", paper, StringComparison.Ordinal);
        Assert.Contains("Flexible` carries no lane, while `HardPinned` carries one `LaneId`", paper, StringComparison.Ordinal);
        Assert.Contains("select the exact existing-owner result and winner", evidence, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] path) => File.ReadAllText(Path.Combine(new[] { root }.Concat(path).ToArray()));

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
