using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6n architecture-authority guards for the distinct physical
/// memory-bank position and geometry-lifetime families. No production
/// declaration, caller or invalid-input behavior is authorized here.
/// </summary>
public sealed class
    Rf126nPhysicalMemoryBankGeometryLifetimeArchitectureDecisionTests
{
    [Fact]
    public void PaperDefinesDistinctPhysicalPositionAndGenerationFamilies()
    {
        string decision = PaperDecision();

        Assert.Contains("`PhysicalMemoryBankIndex` denotes only one queue/array position",
            decision, StringComparison.Ordinal);
        Assert.Contains("non-negative `Int32`", decision,
            StringComparison.Ordinal);
        Assert.Contains("zero is the first physical bank and is valid",
            decision, StringComparison.Ordinal);
        Assert.Contains("`index < BankCount`", decision,
            StringComparison.Ordinal);
        Assert.Contains("`MemoryBankGeometryGeneration` is allocated monotonically",
            decision, StringComparison.Ordinal);
        Assert.Contains("Zero is unissued/absent", decision,
            StringComparison.Ordinal);
        Assert.Contains("This generation is not a request ID, replay",
            decision, StringComparison.Ordinal);
        Assert.Contains("epoch, queue epoch, domain epoch or universal generation",
            decision, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperRequiresSnapshotResolutionAndNonAliasingAbsence()
    {
        string decision = PaperDecision();

        Order(decision,
            "A published physical geometry is the immutable tuple:",
            "(positive BankCount, positive BankWidthBytes,",
            "non-zero MemoryBankGeometryGeneration)",
            "Address resolution consumes one immutable geometry snapshot",
            "(address / BankWidthBytes) % BankCount",
            "binds the result to that snapshot's generation");

        Assert.Contains("synthetic `4096/16` substitution is not validation",
            decision, StringComparison.Ordinal);
        Assert.Contains("never produces physical bank zero", decision,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperBindsAcceptedRequestsAndForbidsLiveReconfiguration()
    {
        string decision = PaperDecision();

        Order(decision,
            "Every accepted asynchronous memory request",
            "resolved physical bank index and",
            "geometry generation before it enters a bank queue",
            "Queue lookup, arbitration,",
            "completion and cancellation use that captured binding",
            "cancellation may not",
            "re-resolve the request address against current geometry",
            "Geometry replacement is an explicit quiescent lifecycle action");
        Assert.Contains("no request is pending or", decision,
            StringComparison.Ordinal);
        Assert.Contains("queued, no bank or port is active", decision,
            StringComparison.Ordinal);
        Assert.Contains("leave the old snapshot and generation unchanged",
            decision, StringComparison.Ordinal);
        Assert.Contains("neither truncates live queues nor", decision,
            StringComparison.Ordinal);
        Assert.Contains("rehashes accepted requests",
            decision, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperKeepsSchedulerProjectionPartialAndExact()
    {
        string decision = PaperDecision();

        Assert.Contains("Only a",
            decision, StringComparison.Ordinal);
        Assert.Contains("geometry with `BankCount` in `1..16` may project",
            decision, StringComparison.Ordinal);
        Assert.Contains("the ordinal is unchanged", decision,
            StringComparison.Ordinal);
        Assert.Contains("positions above 15 are not clamped, wrapped or aliased",
            decision, StringComparison.Ordinal);
        Assert.Contains("positions 0..15 do",
            decision, StringComparison.Ordinal);
        Assert.Contains("not make the wider geometry scheduler-resolvable",
            decision, StringComparison.Ordinal);
        Assert.Contains("Topology-neutral aggregate", decision,
            StringComparison.Ordinal);
        Assert.Contains("pressure may still describe the whole timed-memory owner",
            decision,
            StringComparison.Ordinal);
        Assert.Contains("existing low-sixteen projection under wider topology is compatibility",
            decision, StringComparison.Ordinal);
        Assert.Contains("behavior, not authority",
            decision, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperRetainsPrimitiveWiresWithoutIdentityAuthority()
    {
        string decision = PaperDecision();

        Assert.Contains("burst `BankId` field remains a raw physical ordinal",
            decision, StringComparison.Ordinal);
        Assert.Contains("`BankQueueDepths` remains a positional array",
            decision, StringComparison.Ordinal);
        Assert.Contains("raw-to-checked-to-raw exactly", decision,
            StringComparison.Ordinal);
        Assert.Contains("legacy wires carry no geometry generation",
            decision, StringComparison.Ordinal);
        Assert.Contains("cannot establish cross-generation request, replay, cancellation or",
            decision, StringComparison.Ordinal);
        Assert.Contains("publication identity",
            decision, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionEvidencePrecedesLaterZeroCallerContracts()
    {
        string root = FindRepositoryRoot();
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence",
            "RF12",
            "rf12.6n-physical-memory-bank-geometry-lifetime-architecture-decision.md");
        Assert.Contains("Neither type is declared in production",
            evidence, StringComparison.Ordinal);

        string indexContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankIndex.cs"));
        string generationContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemoryBankGeometryGeneration.cs"));
        string geometryContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankGeometry.cs"));
        string bindingContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankBinding.cs"));
        string envelopeContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "CanonicalVectorPhysicalBankEnvelope.cs"));
        string production = ReadSourceTree(
            Path.Combine(root, "HybridCPU_ISE"), indexContractPath,
            generationContractPath, geometryContractPath, bindingContractPath,
            envelopeContractPath);

        Assert.DoesNotMatch(new Regex(
                @"\b(?:record\s+struct|readonly\s+struct|class)\s+PhysicalMemoryBankIndex\b",
                RegexOptions.CultureInvariant),
            production);
        Assert.DoesNotMatch(new Regex(
                @"\b(?:record\s+struct|readonly\s+struct|class)\s+MemoryBankGeometryGeneration\b",
                RegexOptions.CultureInvariant),
            production);
        Assert.Equal(12, Regex.Matches(production,
            @"\bPhysicalMemoryBankIndex\b").Count);
        Assert.Contains(
            "private ulong _lastIssuedPhysicalBankGeometryGeneration;",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankGeometryGeneration.Create(nextGenerationRaw)",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "public MemoryBankGeometryGeneration Generation { get; }",
            File.ReadAllText(geometryContractPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "public PhysicalMemoryBankIndex BankIndex { get; }",
            File.ReadAllText(bindingContractPath),
            StringComparison.Ordinal);

        Type token = typeof(MemorySubsystem.MemoryRequestToken);
        Assert.Null(token.GetProperty("PhysicalBankIndex",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(token.GetProperty("GeometryGeneration",
            BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void CurrentFallbackMutationCancellationAndWireSeamsRemainExact()
    {
        string root = FindRepositoryRoot();
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Helpers.cs");
        string operations = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "MemorySubsystem.Operations.cs");
        string subsystem = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "MemorySubsystem.cs");

        Assert.Contains("private const int DefaultBankWidthBytes = 4096",
            routing, StringComparison.Ordinal);
        Assert.Contains("private const int DefaultNumBanks = 16", routing,
            StringComparison.Ordinal);
        Assert.Contains("int resolvedBankWidthBytes = bankWidthBytes > 0",
            routing, StringComparison.Ordinal);
        Assert.Contains("int resolvedNumBanks = numBanks > 0", routing,
            StringComparison.Ordinal);
        Assert.Contains(
            "private PhysicalMemoryBankIndex ComputeBankId(ulong address)",
            helpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "return RemoveQueuedBankRequest(",
            operations, StringComparison.Ordinal);
        Assert.Contains("int sanitized = Math.Max(1, value)", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("int trackedBanks = Math.Min(NumBanks, 16)",
            subsystem, StringComparison.Ordinal);

        PropertyInfo bankId = typeof(MemorySubsystem.BurstEventArgs)
            .GetProperty(nameof(MemorySubsystem.BurstEventArgs.BankId))!;
        Assert.Equal(typeof(int), bankId.PropertyType);
        Assert.True(bankId.CanWrite);
    }


    private static string PaperDecision()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");
        const string marker =
            "#### 3.7.24 Physical memory-bank position and geometry lifetime";
        int start = paper.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        return paper[start..];
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
            .Where(path => !excludedPaths.Contains(Path.GetFullPath(path),
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
