using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124cAtomicExecuteOwnerVtInventoryDecisionTests
{
    [Fact]
    public void PaperOwnsTheExactAtomicOwnerConsumerAndValidOnlyCutover()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.17 Atomic execution owner-VT consumer contour",
            paper, StringComparison.Ordinal);
        Assert.Contains("the single\n`AtomicMicroOp.Execute(ref CPU_Core)` owner read",
            paper, StringComparison.Ordinal);
        Assert.Contains("NormalizeExecutionVtId(OwnerThreadId)", paper,
            StringComparison.Ordinal);
        Assert.Contains("before any\n   architectural-state read or carrier mutation",
            paper, StringComparison.Ordinal);
        Assert.Contains("TryGetArchitecturalOwnerVtId(out checkedOwner)",
            paper, StringComparison.Ordinal);
        Assert.Contains("? checkedOwner.Value", paper,
            StringComparison.Ordinal);
        Assert.Contains(": NormalizeExecutionVtId(OwnerThreadId)", paper,
            StringComparison.Ordinal);
        Assert.Contains("byte-for-byte retained throwing arm", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AtomicExecuteOrderAndEffectPublicationBoundaryRemainFrozen()
    {
        string root = FindRepositoryRoot();
        string carrier = ExtractBalanced(Read(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.Misc.cs"), "public sealed class AtomicMicroOp");

        Order(carrier,
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            "? checkedOwner.Value",
            ": NormalizeExecutionVtId(OwnerThreadId);",
            "ulong address = ReadUnifiedScalarSourceOperand(ref core, vtId, BaseRegID);",
            "ulong sourceValue = UsesSourceRegister",
            "Address = address;",
            "_resolvedRetireEffect = core.AtomicMemoryUnit.ResolveRetireEffect(",
            "vtId,",
            "AcquireOrdering,",
            "ReleaseOrdering);",
            "return true;");
        Assert.Equal(1, Regex.Matches(carrier,
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
        Assert.Contains("public AtomicRetireEffect CreateRetireEffect() => _resolvedRetireEffect;",
            carrier, StringComparison.Ordinal);

        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        Order(retire,
            "PrevalidateAtomicEffect(retireEffect.AtomicEffect)",
            "ApplyRetiredAtomicEffect(retireEffect.AtomicEffect)");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidOwnerStillWinsBeforeCoreReadAddressOrEffectMutation(int rawOwner)
    {
        const ulong originalAddress = 0xCAFE_BABEUL;
        var operation = new AtomicMicroOp
        {
            OwnerThreadId = rawOwner,
            Address = originalAddress,
            BaseRegID = 1,
            SrcRegID = 2
        };
        Processor.CPU_Core core = null!;

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => operation.Execute(ref core));

        Assert.Equal("ownerThreadId", exception.ParamName);
        Assert.Equal(rawOwner, exception.ActualValue);
        Assert.Equal(originalAddress, operation.Address);
        Assert.False(operation.CreateRetireEffect().IsValid);
    }

    [Fact]
    public void ProductionAndTestSupportCallTopologyAndRawBypassesStayExplicit()
    {
        string root = FindRepositoryRoot();
        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "Materialization",
            "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string support = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string effect = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");

        Assert.Contains("return microOp.Execute(ref stableCoreIdentity);",
            execute, StringComparison.Ordinal);
        Assert.Contains("bool success = ExecuteMicroOpWithStableCoreIdentity(pipeEX.MicroOp);",
            execute, StringComparison.Ordinal);
        Assert.Contains("ExecuteTestAtomicWithStableCoreIdentity(AtomicMicroOp atomicMicroOp)",
            support, StringComparison.Ordinal);
        Assert.Contains("return atomicMicroOp.Execute(ref stableCoreIdentity);",
            support, StringComparison.Ordinal);
        Assert.Contains("atomicMicroOp.OwnerThreadId = vtId;",
            support, StringComparison.Ordinal);
        Assert.Contains("public int VirtualThreadId { get; }", effect,
            StringComparison.Ordinal);
        Assert.Contains("public static AtomicRetireEffect Create(", effect,
            StringComparison.Ordinal);
        Assert.Contains("int virtualThreadId,", effect,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionAllowsOnlyTheAuthorizedAtomicAndScalarProjectionCallers()
    {
        string root = FindRepositoryRoot();
        const string projection = "TryGetArchitecturalOwnerVtId";
        Dictionary<string, int> occurrences = FindOccurrences(
            root, "HybridCPU_ISE", projection);

        Assert.Equal(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Control/MicroOp.Control.cs"] = 3,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.Misc.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/MicroOp.Compute.cs"] = 2
        }, occurrences);
        Assert.Empty(FindOccurrences(root, "HybridCPU_Compiler", projection));
        Assert.Empty(FindOccurrences(root, "TestAssemblerConsoleApps", projection));

        string carrier = ExtractBalanced(Read(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.Misc.cs"), "public sealed class AtomicMicroOp");
        Assert.DoesNotContain("MemoryBankId", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotId", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("LaneId", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("ChannelId", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainId", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("TokenId", carrier, StringComparison.Ordinal);
    }

    private static Dictionary<string, int> FindOccurrences(
        string root, string sourceRoot, string token) =>
        Directory.EnumerateFiles(Path.Combine(root, sourceRoot), "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !HasPathSegment(path, "bin") &&
                           !HasPathSegment(path, "obj") &&
                           !HasPathSegment(path, "Legacy"))
            .Select(path => new
            {
                Path = Path.GetRelativePath(root, path).Replace('\\', '/'),
                Count = Regex.Matches(File.ReadAllText(path),
                    $@"\b{token}\b").Count
            })
            .Where(entry => entry.Count != 0)
            .ToDictionary(entry => entry.Path, entry => entry.Count,
                StringComparer.Ordinal);

    private static void Order(string text, params string[] markers)
    {
        int offset = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, offset + 1, StringComparison.Ordinal);
            Assert.True(next > offset, $"Missing or out-of-order marker: {marker}");
            offset = next;
        }
    }

    private static string ExtractBalanced(string source, string marker)
    {
        int markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0);
        int open = source.IndexOf('{', markerIndex);
        Assert.True(open >= 0);
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            depth += source[index] == '{' ? 1 : source[index] == '}' ? -1 : 0;
            if (depth == 0)
                return source[markerIndex..(index + 1)];
        }

        throw new InvalidOperationException("Unbalanced source contour.");
    }

    private static bool HasPathSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
