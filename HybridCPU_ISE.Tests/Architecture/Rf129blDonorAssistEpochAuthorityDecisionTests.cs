namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129blDonorAssistEpochAuthorityDecisionTests
{
    [Fact]
    public void PaperSeparatesRemoteZeroAbsenceAndForbidsCrossCoreEpochAuthority()
    {
        string paper = Read("ResearchPaper", "section", "md base", "6_Assist_Coupled_Data_Movement_and_Donor_Semantics.md");
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        Assert.Contains("Remote donor transport carries a distinct donor-assist freshness observation", paper, StringComparison.Ordinal);
        Assert.Contains("retained compatibility form for no explicit donor-epoch observation", paper, StringComparison.Ordinal);
        Assert.Contains("cannot be reinterpreted as one", paper, StringComparison.Ordinal);
        Assert.Contains("It does not allocate", paper, StringComparison.Ordinal);
        Assert.Contains("no common checked `AssistEpoch` type, generic epoch conversion", paper, StringComparison.Ordinal);
        Assert.Contains("no JSON, binary replay trace or compiler/ISA encoding", paper, StringComparison.Ordinal);
        Assert.Contains("RF-12.9bl | closed architecture decision", ledger, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(path).ToArray()));
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
