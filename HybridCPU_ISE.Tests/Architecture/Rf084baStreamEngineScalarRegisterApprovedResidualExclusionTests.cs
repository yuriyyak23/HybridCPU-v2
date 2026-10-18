using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084baStreamEngineScalarRegisterApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesOnlyTheNineOpcodeCompatibilityEnvelope()
    {
        string paper = ReadPaper();

        Assert.Contains("RF-08.4ba approved scalar-register StreamEngine", paper, StringComparison.Ordinal);
        Assert.Contains("ADD, SUB, MUL, DIV, XOR, OR, AND, SLL and SRL", paper, StringComparison.Ordinal);
        Assert.Contains("remains outside typed-FSP candidate membership", paper, StringComparison.Ordinal);
        Assert.Contains("no complete", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionConstructorAndBoundedCallerClosureRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] files = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories);
        Assert.DoesNotContain(files, path =>
            File.ReadAllText(path).Contains("new VectorALUMicroOp", StringComparison.Ordinal));

        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.TestSupport.cs");
        Assert.Equal(2, Regex.Matches(testSupport,
            @"StreamEngine\.CaptureRetireWindowPublications\(").Count);
    }

    [Fact]
    public void CurrentLedgersTreatCompatibilityAsApprovedNotTypedFsp()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "03_RF08_EXIT_READINESS_LEDGER.md");
        string status = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");

        Assert.Contains("RF-08.4ba", ledger, StringComparison.Ordinal);
        Assert.Contains("exit-admissible approved residual exclusion", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ba", status, StringComparison.Ordinal);
        Assert.Contains("outside typed-FSP membership", status, StringComparison.Ordinal);
    }

    private static string ReadPaper() =>
        Read(FindRepositoryRoot(), "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
