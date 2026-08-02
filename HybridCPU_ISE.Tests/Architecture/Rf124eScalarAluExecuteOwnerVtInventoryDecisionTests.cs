using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124eScalarAluExecuteOwnerVtInventoryDecisionTests
{
    [Fact]
    public void PaperOwnsOnlyTheScalarExecuteOwnerConsumer()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.18 Scalar ALU execution owner-VT consumer contour",
            paper, StringComparison.Ordinal);
        Assert.Contains("only the first read in\n`ScalarALUMicroOp.Execute",
            paper, StringComparison.Ordinal);
        Assert.Contains("The second read in\n`EmitWriteBackRetireRecords`",
            paper, StringComparison.Ordinal);
        Assert.Contains("inventoried and migrated separately", paper,
            StringComparison.Ordinal);
        Assert.Contains("TryGetArchitecturalOwnerVtId(out checkedOwner)",
            paper, StringComparison.Ordinal);
        Assert.Contains(": NormalizeExecutionVtId(OwnerThreadId)", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExecuteOrderAndSeparateRetireOwnerReadRemainFrozen()
    {
        string carrier = ExtractBalanced(Read(FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Vector", "MicroOp.Compute.cs"), "public class ScalarALUMicroOp");

        string execute = Slice(carrier,
            "public override bool Execute(ref Processor.CPU_Core core)",
            "public override void EmitWriteBackRetireRecords(");
        Order(execute,
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            "? checkedOwner.Value",
            ": NormalizeExecutionVtId(OwnerThreadId);",
            "ulong op1 = ReadUnifiedScalarSourceOperand(ref core, vtId, Src1RegID);",
            "ulong op2 = UsesImmediate",
            "? Immediate",
            ": ReadUnifiedScalarSourceOperand(ref core, vtId, Src2RegID);",
            "ulong executionPc = core.ResolveCurrentScalarMicroOpExecutionPc();",
            "_result = ExecuteScalarOp(OpCode, op1, op2, executionPc);",
            "return true;");

        Assert.Equal(2, Regex.Matches(carrier,
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
        Assert.Equal(2, Regex.Matches(carrier,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
        Assert.Contains(
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            Slice(carrier,
                "public override void EmitWriteBackRetireRecords(",
                "public override bool TryGetPrimaryWriteBackResult("),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidOwnerWinsBeforeCoreReadPcOrResultMutation(int rawOwner)
    {
        const ulong priorResult = 0xCAFE_BABEUL;
        var operation = new ScalarALUMicroOp
        {
            OwnerThreadId = rawOwner,
            Src1RegID = 1,
            Src2RegID = 2,
            UsesImmediate = false,
            WritesRegister = true
        };
        operation.CapturePrimaryWriteBackResult(priorResult);
        Processor.CPU_Core core = null!;

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => operation.Execute(ref core));

        Assert.Equal("ownerThreadId", exception.ParamName);
        Assert.Equal(rawOwner, exception.ActualValue);
        Assert.True(operation.TryGetPrimaryWriteBackResult(out ulong result));
        Assert.Equal(priorResult, result);
    }

    [Fact]
    public void CallTopologySubclassabilityAndRawMutationSeamsRemainExplicit()
    {
        string root = FindRepositoryRoot();
        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "Materialization",
            "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Fsp",
            "CPU_Core.PipelineExecution.Fsp.cs");

        Assert.Contains("return microOp.Execute(ref stableCoreIdentity);",
            execute, StringComparison.Ordinal);
        Assert.Contains("return laneIndex < 4 ||", execute,
            StringComparison.Ordinal);
        Assert.Contains("candidate is not Core.ScalarALUMicroOp", fsp,
            StringComparison.Ordinal);
        Assert.False(typeof(ScalarALUMicroOp).IsSealed);
        Assert.Equal(typeof(int), typeof(MicroOp).GetProperty(
            nameof(MicroOp.OwnerThreadId),
            BindingFlags.Public | BindingFlags.Instance)!.PropertyType);

        Assert.Equal(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Control/MicroOp.Control.cs"] = 3,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.Misc.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/MicroOp.Compute.cs"] = 2
        }, FindOccurrences(root, "HybridCPU_ISE",
            "TryGetArchitecturalOwnerVtId"));
        Assert.Empty(FindOccurrences(root, "HybridCPU_Compiler",
            "TryGetArchitecturalOwnerVtId"));
        Assert.Empty(FindOccurrences(root, "TestAssemblerConsoleApps",
            "TryGetArchitecturalOwnerVtId"));
    }

    [Fact]
    public void ExecuteIntroducesNoOtherIdentifierFamilyOrWireSurface()
    {
        string carrier = ExtractBalanced(Read(FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Vector", "MicroOp.Compute.cs"), "public class ScalarALUMicroOp");
        string execute = Slice(carrier,
            "public override bool Execute(ref Processor.CPU_Core core)",
            "public override void EmitWriteBackRetireRecords(");

        foreach (string forbidden in new[]
                 {
                     "MemoryBankId", "SlotId", "LaneId", "ChannelId",
                     "DomainId", "TokenId", "JsonSerializer", "Dictionary<",
                     "Math.Clamp", "%", "<<", ">>"
                 })
        {
            Assert.DoesNotContain(forbidden, execute, StringComparison.Ordinal);
        }
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
            int next = text.IndexOf(marker, offset + 1,
                StringComparison.Ordinal);
            Assert.True(next > offset,
                $"Missing or out-of-order marker: {marker}");
            offset = next;
        }
    }

    private static string Slice(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        int endIndex = text.IndexOf(end, startIndex + start.Length,
            StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return text[startIndex..endIndex];
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
            depth += source[index] == '{' ? 1 :
                source[index] == '}' ? -1 : 0;
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

        throw new DirectoryNotFoundException(
            "HybridCPU repository root was not found.");
    }
}
