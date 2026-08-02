namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4af freezes the architecture-owner C-C decision for the closed,
/// non-issued ReplayToken rollback contour. It authorizes no production change.
/// </summary>
public sealed class Rf084afReplayTokenApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesClosedNonIssuedContourAtRf08Exit()
    {
        string paper = ReadPaper(
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains(
            "RF-08.4af approved replay-rollback `RegisterWrite` C-C residual exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "`ReplayToken.CaptureRegisterState` and `ReplayToken.Rollback`",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "no production core rollback caller",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "admissible at RF-08 exit",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "reviewed only by a separate architecture revision",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperPreservesProtocolLimitationsAndForbidsSyntheticIdentity()
    {
        string admissionPaper = ReadPaper(
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string replayPaper = ReadPaper(
            "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");

        Assert.Contains(
            "`CanSafelyRollback` is an",
            admissionPaper,
            StringComparison.Ordinal);
        Assert.Contains(
            "register snapshots are republished one-by-one before later memory restoration",
            admissionPaper,
            StringComparison.Ordinal);
        Assert.Contains(
            "neither immutable rollback provenance nor",
            admissionPaper,
            StringComparison.Ordinal);
        Assert.Contains(
            "No token field, register ordinal, owner VT, trace identity, timestamp, memory",
            admissionPaper,
            StringComparison.Ordinal);
        Assert.Contains(
            "authorizes no replay/LoopBuffer change",
            admissionPaper,
            StringComparison.Ordinal);
        Assert.Contains(
            "does not establish all-or-none register-plus-memory",
            replayPaper,
            StringComparison.Ordinal);
        Assert.Contains(
            "explicit RF-08.4af limitations",
            replayPaper,
            StringComparison.Ordinal);
    }

    private static string ReadPaper(string fileName)
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            root,
            "ResearchPaper",
            "section",
            "md base",
            fileName));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "ResearchPaper")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
