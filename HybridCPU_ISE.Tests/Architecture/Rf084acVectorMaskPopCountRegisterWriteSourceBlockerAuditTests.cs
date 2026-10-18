namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4ac freezes the scalar-result VPOPC RegisterWrite source/caller
/// inventory without treating vector payload or lane placement as identity.
/// </summary>
public sealed class Rf084acVectorMaskPopCountRegisterWriteSourceBlockerAuditTests
{
    [Fact]
    public void CanonicalVpopcMaterializesDedicatedScalarResultCarrier()
    {
        string root = FindRepositoryRoot();
        string initialize = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Initialize.Vector.cs");
        string helper = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Vector.cs");

        Assert.Contains(
            "RegisterPublishedVectorMaskPopCountOp((uint)Processor.CPU_Core.InstructionsEnum.VPOPC);",
            initialize,
            StringComparison.Ordinal);
        Assert.Contains(
            "VectorMaskPopCountMicroOp vectorMaskPopCountMicroOp = new VectorMaskPopCountMicroOp",
            helper,
            StringComparison.Ordinal);
        Assert.Contains("WritesRegister = true", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void MainlineCarrierStoresMutableResultAndEmitsAtSelectedWriteBack()
    {
        string root = FindRepositoryRoot();
        string compute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Compute.cs");
        string carrier = Slice(
            compute,
            "public sealed class VectorMaskPopCountMicroOp",
            "public class VectorZeroExtendMicroOp");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("private ulong _result;", carrier, StringComparison.Ordinal);
        Assert.Contains("_result = VectorALU.MaskPopCount(", carrier, StringComparison.Ordinal);
        Assert.Contains("public override void EmitWriteBackRetireRecords(", carrier, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(vtId, DestRegID, _result)", carrier, StringComparison.Ordinal);
        Assert.Contains("retireBatch.EmitMicroOpRetireRecords(", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void VpopcIsNotCoupledToPredicateOrDirtyRetireEffects()
    {
        string root = FindRepositoryRoot();
        string compute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Compute.cs");
        string carrier = Slice(
            compute,
            "public sealed class VectorMaskPopCountMicroOp",
            "public class VectorZeroExtendMicroOp");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string dirtyClassifier = Slice(
            retire,
            "private static bool IsVectorStreamDirtyRetireOpcode(",
            "private void ApplyRetiredSystemEvent(");

        Assert.DoesNotContain("SetPredicateRegister(", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("PredicateState", carrier, StringComparison.Ordinal);
        Assert.Contains("IsaOpcodeValues.VPOPC", dirtyClassifier, StringComparison.Ordinal);
        Assert.Contains("return false;", dirtyClassifier, StringComparison.Ordinal);
    }

    [Fact]
    public void RawAndDirectStreamVpopcSurfacesHaveTestOnlyCoreCallers()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] vectorAluConstructors = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("new VectorALUMicroOp", StringComparison.Ordinal))
            .ToArray();
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string stream = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "StreamEngine", "Modes", "StreamEngine.cs");

        Assert.Empty(vectorAluConstructors);
        Assert.Contains("TEST-ONLY: explicit direct stream compat executor", testSupport, StringComparison.Ordinal);
        Assert.Contains("StreamEngine.CaptureRetireWindowPublications(", testSupport, StringComparison.Ordinal);
        Assert.Contains("CaptureVpopcRetireWindowPublication(", stream, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactCarrierRemainsAbsentAndPaperNowApprovesVpopcExclusion()
    {
        string root = FindRepositoryRoot();
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string attach = Slice(
            fsp,
            "private void AttachRf08PostStageBIdentityTemplate(",
            "private byte ResolveForegroundRunnableVirtualThreadMask()");
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        Assert.Contains("candidate is not Core.ScalarALUMicroOp", attach, StringComparison.Ordinal);
        Assert.DoesNotContain("VectorMaskPopCountMicroOp", attach, StringComparison.Ordinal);
        Assert.Contains("approved scalar-result vector `VPOPC` `RegisterWrite`", paper, StringComparison.Ordinal);
        Assert.Contains("retained raw `VectorALUMicroOp` generated-record", paper, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing start marker: {startMarker}");
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing end marker: {endMarker}");
        return source[start..end];
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
