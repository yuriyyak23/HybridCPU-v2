namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129bmDonorAssistEpochRepresentationDecisionTests
{
    [Fact]
    public void DonorObservationRemainsSeparateRawCompatibilityCarrier()
    {
        string paper = Read("ResearchPaper", "section", "md base", "6_Assist_Coupled_Data_Movement_and_Donor_Semantics.md");
        string transport = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "AssistRuntime.cs");
        string scheduler = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "InterCore", "MicroOpScheduler.Assist.InterCore.cs");
        string ledger = Read("Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        Assert.Contains("remain separate until each has a representation and caller decision", paper, StringComparison.Ordinal);
        Assert.Contains("raw wrap/default behavior is compatibility state", paper, StringComparison.Ordinal);
        Assert.Contains("ulong donorAssistEpochId", transport, StringComparison.Ordinal);
        Assert.Contains("ulong sourceAssistEpochId = 0", transport, StringComparison.Ordinal);
        Assert.DoesNotContain("struct DonorAssistEpoch", transport, StringComparison.Ordinal);
        Assert.Contains("ownerSnapshot.AssistEpochId != transport.DonorAssistEpochId", scheduler, StringComparison.Ordinal);
        Assert.Contains("RF-12.9bm | closed architecture decision", ledger, StringComparison.Ordinal);
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
