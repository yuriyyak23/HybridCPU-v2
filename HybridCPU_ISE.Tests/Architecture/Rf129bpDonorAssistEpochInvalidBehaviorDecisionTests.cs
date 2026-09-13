namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129bpDonorAssistEpochInvalidBehaviorDecisionTests
{
    [Fact]
    public void PaperRequiresCandidateRejectionButDoesNotCreateNewFaultWinnerAuthority()
    {
        string paper = Read("ResearchPaper", "section", "md base", "6_Assist_Coupled_Data_Movement_and_Donor_Semantics.md");
        string scheduler = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "InterCore", "MicroOpScheduler.Assist.InterCore.cs");
        string execution = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "CPU_Core.Assist.cs");
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        Assert.Contains("transport is still only a candidate", paper, StringComparison.Ordinal);
        Assert.Contains("identity, donor ownership, donor epoch, or domain continuity has drifted", paper, StringComparison.Ordinal);
        Assert.Contains("existing owner/domain/donor-epoch checks before injection", paper, StringComparison.Ordinal);
        Assert.Contains("ClearInterCoreAssistNominationPort(coreId)", scheduler, StringComparison.Ordinal);
        Assert.Contains("Core.AssistInvalidationReason.InterCoreOwnerDrift", execution, StringComparison.Ordinal);
        Assert.Contains("Core.AssistInvalidationReason.InterCoreBoundaryDrift", execution, StringComparison.Ordinal);
        Assert.Contains("RF-12.9bp | closed architecture decision", ledger, StringComparison.Ordinal);
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
