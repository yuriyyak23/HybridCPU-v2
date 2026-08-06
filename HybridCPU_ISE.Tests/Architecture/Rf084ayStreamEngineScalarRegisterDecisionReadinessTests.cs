using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084ayStreamEngineScalarRegisterDecisionReadinessTests
{
    private static readonly string[] ExpectedOpcodes =
    [
        "ADD", "AND", "DIV", "MUL", "OR", "SLL", "SRL", "SUB", "XOR"
    ];

    [Fact]
    public void ScalarCompatibilityEnvelopeRemainsExactlyNineOpcodes()
    {
        string root = FindRepositoryRoot();
        string stream = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "StreamEngine", "Modes", "StreamEngine.Execute1D.cs");
        string ingress = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "StreamEngine", "Modes", "StreamEngine.cs");

        string method = ExtractMethod(
            stream,
            "private static bool IsSupportedScalarRegisterStreamOpcode",
            "private static InvalidOperationException");
        string[] actual = Regex.Matches(method, @"InstructionsEnum\.([A-Z0-9_]+)\s*=>\s*true")
            .Select(match => match.Groups[1].Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedOpcodes, actual);
        Assert.Contains("if (streamLength == 1 && request.IsScalar)", ingress, StringComparison.Ordinal);
        Assert.Contains("ResolveRequiredScalarRegisterOperandsOrThrow(", stream, StringComparison.Ordinal);
        Assert.Contains("if (!core.LaneActive(predIndex, 0))", stream, StringComparison.Ordinal);
    }

    [Fact]
    public void RawAdapterHasNoProductionConstructorOrTypedFspNomination()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] files = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories);

        Assert.DoesNotContain(files, path =>
            File.ReadAllText(path).Contains("new VectorALUMicroOp", StringComparison.Ordinal));

        string rawAdapter = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Vector", "MicroOp.Compute.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        Assert.Contains("StreamEngine.Execute(ref core, Instruction, vtId)", rawAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("VectorALUMicroOp", fsp, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedAndBoundedSurfacesRetainTheirExistingOwners()
    {
        string root = FindRepositoryRoot();
        string stream = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "StreamEngine", "Modes", "StreamEngine.Execute1D.cs");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.TestSupport.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("core.AppendGeneratedExecutingLaneRetireRecord(", stream, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(testSupport,
            @"StreamEngine\.CaptureRetireWindowPublications\(").Count);
        Assert.Contains("/// TEST-ONLY: explicit direct stream compat executor", testSupport, StringComparison.Ordinal);
        Assert.Contains("/// TEST-ONLY: drive StreamEngine direct retire publication", testSupport, StringComparison.Ordinal);
        Assert.Contains("AppendLaneGeneratedRetireRecords(ref retireBatch, lane)", retire, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(retireBatch.RetireRecords)", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPackageRemainsEvidenceOnly()
    {
        string root = FindRepositoryRoot();
        string paper = ReadPaper();
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF08",
            "rf08.4ay-streamengine-scalar-register-decision-readiness-audit.md");
        string status = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");

        Assert.Contains("At RF-08.4aw both rows still expired at RF-08 exit", paper, StringComparison.Ordinal);
        Assert.DoesNotContain("RF-08.4ay approves", paper, StringComparison.Ordinal);
        Assert.Contains("Recommended narrow C-C wording", evidence, StringComparison.Ordinal);
        Assert.Contains("Authority delta", evidence, StringComparison.Ordinal);
        Assert.Contains("None.", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ay", status, StringComparison.Ordinal);
        Assert.Contains("disposition supplied by RF-08.4ba", status, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ba approved scalar-register StreamEngine", paper, StringComparison.Ordinal);
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
