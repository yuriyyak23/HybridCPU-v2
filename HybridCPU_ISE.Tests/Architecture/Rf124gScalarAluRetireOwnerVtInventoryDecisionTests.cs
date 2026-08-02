using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124gScalarAluRetireOwnerVtInventoryDecisionTests
{
    [Fact]
    public void PaperOwnsOnlyTheScalarRetireRecordOwnerConsumer()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.19 Scalar ALU retire-record owner-VT consumer contour",
            paper, StringComparison.Ordinal);
        Assert.Contains("`WritesRegister == false` returns without validating",
            paper, StringComparison.Ordinal);
        Assert.Contains("wins over buffer exhaustion", paper,
            StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Prevalidate", paper,
            StringComparison.Ordinal);
        Assert.Contains("may replace only the writing-path local",
            paper, StringComparison.Ordinal);
        Assert.Contains("may not move validation\nabove the `WritesRegister` gate",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void GateOwnerRecordCapacityAndCountOrderRemainFrozen()
    {
        string root = FindRepositoryRoot();
        string carrier = ExtractBalanced(Read(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Vector",
            "MicroOp.Compute.cs"), "public class ScalarALUMicroOp");
        string retire = Slice(carrier,
            "public override void EmitWriteBackRetireRecords(",
            "public override bool TryGetPrimaryWriteBackResult(");
        Order(retire,
            "if (!WritesRegister)",
            "return;",
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            "? checkedOwner.Value",
            ": NormalizeExecutionVtId(OwnerThreadId);",
            "AppendWriteBackRetireRecord(",
            "retireRecords,",
            "ref retireRecordCount,",
            "RetireRecord.RegisterWrite(vtId, DestRegID, _result));");

        string baseCarrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        Order(baseCarrier,
            "if ((uint)retireRecordCount >= (uint)retireRecords.Length)",
            "throw new InvalidOperationException(\"WB retire record buffer exhausted.\");",
            "retireRecords[retireRecordCount++] = retireRecord;");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void NonWritingPathSuppressesInvalidOwnerAndLeavesBufferUntouched(
        int rawOwner)
    {
        var operation = new ScalarALUMicroOp
        {
            OwnerThreadId = rawOwner,
            WritesRegister = false
        };
        Processor.CPU_Core core = null!;
        RetireRecord[] storage =
        [
            RetireRecord.RegisterWrite(3, 7, 0xA5UL)
        ];
        int count = 1;

        operation.EmitWriteBackRetireRecords(ref core, storage, ref count);

        Assert.Equal(1, count);
        Assert.Equal(3, storage[0].VtId);
        Assert.Equal(7, storage[0].ArchReg);
        Assert.Equal(0xA5UL, storage[0].Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void WritingInvalidOwnerWinsOverZeroCapacity(int rawOwner)
    {
        var operation = new ScalarALUMicroOp
        {
            OwnerThreadId = rawOwner,
            WritesRegister = true
        };
        Processor.CPU_Core core = null!;
        RetireRecord[] storage = [];
        int count = 0;

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => operation.EmitWriteBackRetireRecords(
                    ref core, storage, ref count));

        Assert.Equal("ownerThreadId", exception.ParamName);
        Assert.Equal(rawOwner, exception.ActualValue);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ValidOwnerExhaustionAndSuccessfulRecordRemainExact()
    {
        var operation = new ScalarALUMicroOp
        {
            OwnerThreadId = 2,
            DestRegID = 9,
            WritesRegister = true
        };
        operation.CapturePrimaryWriteBackResult(0xCAFEUL);
        Processor.CPU_Core core = null!;
        RetireRecord[] storage =
        [
            RetireRecord.RegisterWrite(1, 4, 0xA5UL)
        ];
        int count = 1;

        InvalidOperationException exhausted =
            Assert.Throws<InvalidOperationException>(
                () => operation.EmitWriteBackRetireRecords(
                    ref core, storage, ref count));
        Assert.Equal("WB retire record buffer exhausted.", exhausted.Message);
        Assert.Equal(1, count);
        Assert.Equal(1, storage[0].VtId);
        Assert.Equal(4, storage[0].ArchReg);
        Assert.Equal(0xA5UL, storage[0].Value);

        count = 0;
        operation.EmitWriteBackRetireRecords(ref core, storage, ref count);
        Assert.Equal(1, count);
        Assert.Equal(2, storage[0].VtId);
        Assert.Equal(9, storage[0].ArchReg);
        Assert.Equal(0xCAFEUL, storage[0].Value);
    }

    [Fact]
    public void SelectedPrefixPrevalidationAndPublicationBoundaryStayFrozen()
    {
        string root = FindRepositoryRoot();
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.Types.cs");
        string stage = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");

        Order(types,
            "lane.MicroOp.CapturePrimaryWriteBackResult(lane.ResultValue);",
            "lane.MicroOp.EmitWriteBackRetireRecords(");
        Order(stage,
            "CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane);",
            "PrevalidateRetireWindowBatchForPublication(",
            "PublishRetiredWriteBackLaneForwarding(lane);",
            "FinalizeRetiredWriteBackLane(ref retireBatch, laneIndex, lane);",
            "ApplyRetireBatchImmediateEffects(");
        Assert.Contains(
            "RetireCoordinator.Prevalidate(retireBatch.RetireRecords);",
            retire, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionAddsNoProjectionCallerOrOtherIdentifierFamily()
    {
        string root = FindRepositoryRoot();
        Assert.Equal(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Control/MicroOp.Control.cs"] = 3,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.Misc.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/MicroOp.Compute.cs"] = 2
        }, FindOccurrences(root, "HybridCPU_ISE",
            "TryGetArchitecturalOwnerVtId"));

        string carrier = ExtractBalanced(Read(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Vector",
            "MicroOp.Compute.cs"), "public class ScalarALUMicroOp");
        string retire = Slice(carrier,
            "public override void EmitWriteBackRetireRecords(",
            "public override bool TryGetPrimaryWriteBackResult(");
        Assert.Equal(1, Regex.Matches(retire,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
        foreach (string forbidden in new[]
                 {
                     "MemoryBankId", "SlotId", "LaneId", "ChannelId",
                     "DomainId", "TokenId", "JsonSerializer", "Dictionary<",
                     "Math.Clamp", "%", "<<", ">>"
                 })
        {
            Assert.DoesNotContain(forbidden, retire,
                StringComparison.Ordinal);
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
