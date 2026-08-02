using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ai authority and closed-world guards for the canonical
/// vector-transfer multi-address physical-bank envelope. This decision
/// authorizes no production declaration or caller migration.
/// </summary>
public sealed class
    Rf126aiCanonicalVectorPhysicalBankEnvelopeArchitectureDecisionTests
{
    [Fact]
    public void PaperSelectsOneGenerationAndOrderedPerElementIndexes()
    {
        string decision = PaperDecision();

        Order(decision,
            "CanonicalVectorPhysicalBankEnvelope =",
            "(MemoryBankGeometryGeneration,",
            "ordered immutable PhysicalMemoryBankIndex[ElementCount])",
            "SourceAddress[i] = SourceAddress + i * Stride",
            "(SourceAddress[i] / BankWidthBytes) % BankCount");
        Assert.Contains("exact private copy", decision,
            StringComparison.Ordinal);
        Assert.Contains("has exactly the accepted `ElementCount`", decision,
            StringComparison.Ordinal);
        Assert.Contains("Physical bank zero", decision,
            StringComparison.Ordinal);
        Assert.Contains("is a valid entry", decision,
            StringComparison.Ordinal);
        Assert.Contains("Duplicate", decision, StringComparison.Ordinal);
        Assert.Contains("retained in element order rather than deduplicated",
            decision, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperRejectsSingleBaseRangeAliasAndCurrentGeometryReresolution()
    {
        string decision = PaperDecision();

        Assert.Contains(
            "A single\n`PhysicalMemoryBankBinding` resolved only from the source base",
            decision, StringComparison.Ordinal);
        Assert.Contains("cannot represent the physical-bank location evidence",
            decision, StringComparison.Ordinal);
        Assert.Contains("may not be treated as a range binding",
            decision, StringComparison.Ordinal);
        Assert.Contains("may not divide, modulo, clamp, normalize or re-resolve",
            decision, StringComparison.Ordinal);
        Assert.Contains("against the current geometry", decision,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperKeepsOneRequestOneServiceAndOuterAbsence()
    {
        string decision = PaperDecision();

        Assert.Contains("one accepted asynchronous", decision,
            StringComparison.Ordinal);
        Assert.Contains("one controller service decision", decision,
            StringComparison.Ordinal);
        Assert.Contains("does not create a\nper-element request",
            decision, StringComparison.Ordinal);
        Assert.Contains("Zero elements have no envelope", decision,
            StringComparison.Ordinal);
        Assert.Contains("Backpressure creates neither request identity nor envelope",
            decision, StringComparison.Ordinal);
        Assert.Contains("There is no empty,\nnull, default, zero-index",
            decision, StringComparison.Ordinal);
        Assert.Contains("fails closed at the memory-owner boundary",
            decision, StringComparison.Ordinal);
        Assert.Contains("public invalid-input result and migration order remain separate",
            decision, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCanonicalContourHasOneCallerOneIdAndNoBaseCompatibilityBinding()
    {
        string root = FindRepositoryRoot();
        string controller = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Timing", "MemoryCycleController.cs");
        string vector = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Data.cs");
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Helpers.cs");

        Assert.Equal(1, Regex.Matches(vector,
            @"controller\.TryAcceptCanonicalVectorTransfer\(").Count);
        Assert.Contains(
            "MemoryRequestId? _controllerRequestId",
            vector, StringComparison.Ordinal);
        Assert.Contains(
            "CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate(",
            controller, StringComparison.Ordinal);
        Assert.Contains(
            "int packedSize,\n            CanonicalVectorPhysicalBankEnvelope physicalBankEnvelope",
            controller, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "physicalBankEnvelope.GetSourceBankIndex(0)",
            controller, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(controller,
            @"request\.PhysicalBankBinding").Count);
        Assert.Contains(
            "request.ReadRequestClass == ReadRequestClass.CanonicalVectorTransfer",
            controller, StringComparison.Ordinal);
        Assert.Contains(
            "ExecuteControllerVectorTransferReadStep(",
            controller, StringComparison.Ordinal);
        Assert.Contains(
            "ExecuteControllerCanonicalVectorElementReadStepUnderOwnerGate(",
            helpers, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankBinding physicalBankBinding",
            CanonicalServiceSlice(helpers), StringComparison.Ordinal);
    }

    [Fact]
    public void LaterSelectedContourAddsNoWireSerializerOrMutationSurface()
    {
        string root = FindRepositoryRoot();
        string envelopeContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "CanonicalVectorPhysicalBankEnvelope.cs"));
        string production = ReadSourceTree(
            Path.Combine(root, "HybridCPU_ISE"), envelopeContractPath);
        string compiler = ReadSourceTree(
            Path.Combine(root, "HybridCPU_Compiler"));
        string bridge = ReadSourceTree(
            Path.Combine(root, "CpuInterfaceBridge"));
        string assembler = ReadSourceTree(
            Path.Combine(root, "TestAssemblerConsoleApps"));

        Assert.Equal(6, Regex.Matches(
            production,
            @"\bCanonicalVectorPhysicalBankEnvelope\b").Count);
        Assert.Contains(
            "CapturePublishedCanonicalVectorPhysicalBankEnvelopeUnderControllerGate",
            production,
            StringComparison.Ordinal);
        Assert.Contains(
            "public readonly struct CanonicalVectorPhysicalBankEnvelope",
            File.ReadAllText(envelopeContractPath),
            StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalVectorPhysicalBankEnvelope",
            compiler, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalVectorPhysicalBankEnvelope",
            bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalVectorPhysicalBankEnvelope",
            assembler, StringComparison.Ordinal);

        MethodInfo admission = typeof(MemoryCycleController).GetMethod(
            nameof(MemoryCycleController.TryAcceptCanonicalVectorTransfer))!;
        Assert.Equal(typeof(MemoryAdmissionResult), admission.ReturnType);
        Assert.Equal(
            new[]
            {
                typeof(uint), typeof(ulong), typeof(ulong), typeof(ulong),
                typeof(ulong), typeof(int), typeof(ushort)
            },
            admission.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Null(typeof(MemoryCycleController).GetProperty(
            "CanonicalVectorPhysicalBankEnvelope",
            BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void EvidenceInventoriesAuthorityConsumersAndCompatibilitySeams()
    {
        string evidence = Read(FindRepositoryRoot(), "Documentation", "ArchitectureAuthorityRefactor",
            "Evidence", "RF12",
            "rf12.6ai-canonical-vector-physical-bank-envelope-architecture-decision.md");

        Assert.Contains("production caller", evidence,
            StringComparison.Ordinal);
        Assert.Contains("Invalid-to-zero aliases", evidence,
            StringComparison.Ordinal);
        Assert.Contains("Unchecked public constructors", evidence,
            StringComparison.Ordinal);
        Assert.Contains("Cross-family conflation", evidence,
            StringComparison.Ordinal);
        Assert.Contains("Raw compatibility bypass", evidence,
            StringComparison.Ordinal);
        Assert.Contains("Reflection and TestSupport mutation", evidence,
            StringComparison.Ordinal);
        Assert.Contains("Parser/serializer/compiler-runtime", evidence,
            StringComparison.Ordinal);
        Assert.Contains("Replay, certificate and telemetry", evidence,
            StringComparison.Ordinal);
    }


    private static string PaperDecision()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");
        const string marker =
            "#### 3.7.25 Canonical vector-transfer physical-bank envelope";
        int start = paper.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        return paper[start..];
    }

    private static string CanonicalServiceSlice(string helpers)
    {
        int start = helpers.IndexOf(
            "private bool ExecuteControllerCanonicalVectorElementReadStepUnderOwnerGate(",
            StringComparison.Ordinal);
        int end = helpers.IndexOf("#endregion", start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return helpers[start..end];
    }

    private static void Order(string text, params string[] markers)
    {
        int offset = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, offset + 1,
                StringComparison.Ordinal);
            Assert.True(next > offset,
                $"Missing or out-of-order marker: {marker}");
            offset = next;
        }
    }

    private static string ReadSourceTree(
        string root,
        params string[] excludedPaths) =>
        string.Join("\n", Directory.EnumerateFiles(root, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !excludedPaths.Contains(
                Path.GetFullPath(path),
                StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static bool IsBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/",
                   StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName,
                    "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName,
                    "Documentation")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
