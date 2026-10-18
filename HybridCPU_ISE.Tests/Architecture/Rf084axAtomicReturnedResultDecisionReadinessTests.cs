using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084axAtomicReturnedResultDecisionReadinessTests
{
    private static readonly string[] ExpectedAtomicOpcodes =
    [
        "AMOADD_D", "AMOADD_W", "AMOAND_D", "AMOAND_W",
        "AMOMAXU_D", "AMOMAXU_W", "AMOMAX_D", "AMOMAX_W",
        "AMOMINU_D", "AMOMINU_W", "AMOMIN_D", "AMOMIN_W",
        "AMOOR_D", "AMOOR_W", "AMOSWAP_D", "AMOSWAP_W",
        "AMOXOR_D", "AMOXOR_W", "LR_D", "LR_W", "SC_D", "SC_W"
    ];

    [Fact]
    public void AtomicApplyEnvelopeRemainsExactlyTwentyTwoOpcodes()
    {
        string root = FindRepositoryRoot();
        string atomic = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "Memory", "AtomicMemory", "AtomicMemoryUnit.cs");

        string[] actual = Regex.Matches(atomic, @"IsaOpcodeValues\.((?:LR|SC|AMO)[A-Z0-9_]+)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedAtomicOpcodes, actual);
    }

    [Fact]
    public void BothApprovedIngressContoursConvergeBeforeReturnedPublication()
    {
        string root = FindRepositoryRoot();
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Types", "MicroOp.Misc.cs");
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.Scalar.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("_resolvedRetireEffect = core.AtomicMemoryUnit.ResolveRetireEffect(", microOp, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureRetireWindowAtomicEffect(effect)", dispatcher, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedAtomicEffect(", retire, StringComparison.Ordinal);

        int apply = retire.IndexOf("ApplyRetiredAtomicEffect(retireEffect.AtomicEffect)", StringComparison.Ordinal);
        int publish = retire.IndexOf("RetireRecord.RegisterWrite(", apply, StringComparison.Ordinal);
        Assert.True(apply >= 0 && publish > apply);
    }

    [Fact]
    public void DestinationAndOutcomeSemanticsRemainEffectBound()
    {
        string root = FindRepositoryRoot();
        string model = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Control", "MicroOp.Control.cs");
        string atomic = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "Memory", "AtomicMemory", "AtomicMemoryUnit.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("effect.DestinationRegister,", model, StringComparison.Ordinal);
        Assert.Contains("registerWritebackValue: 1", atomic, StringComparison.Ordinal);
        Assert.Contains("registerWritebackValue: 0", atomic, StringComparison.Ordinal);
        Assert.Contains("? SignExtendWord(previousValue)", atomic, StringComparison.Ordinal);
        Assert.Contains("PrevalidateAtomicEffect(retireEffect.AtomicEffect)", retire, StringComparison.Ordinal);
        Assert.Contains("atomicEffect.DestinationRegister >= RenameMap.ArchRegs", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPackageDoesNotMasqueradeAsPaperAuthority()
    {
        string root = FindRepositoryRoot();
        string paper = ReadPaper();
        string status = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF08",
            "rf08.4ax-atomic-returned-result-decision-readiness-audit.md");

        Assert.Contains("RF-08.4al explicitly excludes this publication", paper, StringComparison.Ordinal);
        Assert.Contains("disposition supplied by RF-08.4az", status, StringComparison.Ordinal);
        Assert.Contains("Recommended narrow paired-C-C wording", evidence, StringComparison.Ordinal);
        Assert.Contains("Authority delta", evidence, StringComparison.Ordinal);
        Assert.Contains("None.", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("RF-08.4ax approves", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4az approved atomic returned-result", paper, StringComparison.Ordinal);
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
