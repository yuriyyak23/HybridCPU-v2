using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084awRegisterWriteClosedWorldBlockerReconciliationTests
{
    [Fact]
    public void PaperReplacesGenericCatchAllWithExactlyTwoOpenRows()
    {
        string paper = ReadPaper();

        Assert.DoesNotContain("| residual `RegisterWrite` | legacy scalar/vector/dispatch/replay", paper, StringComparison.Ordinal);
        Assert.Contains("| scalar-register StreamEngine compatibility `RegisterWrite` |", paper, StringComparison.Ordinal);
        Assert.Contains("| atomic returned-result `RegisterWrite` |", paper, StringComparison.Ordinal);
        Assert.Contains("This is an authority clarification, not a C-C", paper, StringComparison.Ordinal);
        Assert.Contains("At RF-08.4aw both rows still expired at RF-08 exit", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4az and RF-08.4ba now supersede", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ScalarStreamContourRemainsExactAndOutsideTypedFsp()
    {
        string root = FindRepositoryRoot();
        string stream = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "StreamEngine", "Modes", "StreamEngine.Execute1D.cs");
        string ingress = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "StreamEngine", "Modes", "StreamEngine.cs");
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");

        foreach (string opcode in new[] { "ADD", "SUB", "MUL", "DIV", "XOR", "OR", "AND", "SLL", "SRL" })
            Assert.Contains($"InstructionsEnum.{opcode} => true", stream, StringComparison.Ordinal);

        MatchCollection acceptedOpcodes = Regex.Matches(
            ExtractMethod(stream, "private static bool IsSupportedScalarRegisterStreamOpcode", "private static InvalidOperationException"),
            @"InstructionsEnum\.([A-Z0-9_]+)\s*=>\s*true");
        Assert.Equal(9, acceptedOpcodes.Count);
        Assert.Contains("if (streamLength == 1 && request.IsScalar)", ingress, StringComparison.Ordinal);
        Assert.Contains("PublishScalarRetireRegisterWrite(", ingress, StringComparison.Ordinal);

        string[] productionConstructors = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("new VectorALUMicroOp", StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(productionConstructors);
    }

    [Fact]
    public void AtomicReturnedResultRemainsPostApplyAndSeparate()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string atomic = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "Memory", "AtomicMemory", "AtomicMemoryUnit.cs");
        string paper = ReadPaper();

        int apply = retire.IndexOf("ApplyRetiredAtomicEffect(retireEffect.AtomicEffect)", StringComparison.Ordinal);
        int condition = retire.IndexOf("if (retiredAtomicOutcome.HasRegisterWriteback)", apply, StringComparison.Ordinal);
        int publication = retire.IndexOf("RetireRecord.RegisterWrite(", condition, StringComparison.Ordinal);
        Assert.True(apply >= 0 && condition > apply && publication > condition);
        Assert.Contains("AtomicRetireOutcome ApplyResolvedRetireEffect", atomic, StringComparison.Ordinal);
        Assert.Contains("AtomicReservationRegistry.ConsumeReservation", atomic, StringComparison.Ordinal);
        Assert.Contains("optional returned value is published as a separate", paper, StringComparison.Ordinal);
        Assert.Contains("is not absorbed into this exclusion", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExitLedgerNamesTheTwoFormerWriterContoursAsSeparatelyDecided()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "03_RF08_EXIT_READINESS_LEDGER.md");
        string status = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");

        Assert.DoesNotContain("`RegisterWrite` — all other producers", ledger, StringComparison.Ordinal);
        Assert.Contains("`RegisterWrite` — scalar-register StreamEngine compatibility", ledger, StringComparison.Ordinal);
        Assert.Contains("`RegisterWrite` — atomic returned result", ledger, StringComparison.Ordinal);
        Assert.Contains("No open production-reachable `RegisterWrite` row remains", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4az", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ba", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4aw", status, StringComparison.Ordinal);
        Assert.Contains("| RF-09 | closed; RF-09.0 through RF-09.4 complete |", status, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
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
