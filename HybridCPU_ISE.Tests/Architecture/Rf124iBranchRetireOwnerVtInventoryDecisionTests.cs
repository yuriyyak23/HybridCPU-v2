using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124iBranchRetireOwnerVtInventoryDecisionTests
{
    [Fact]
    public void PaperOwnsTheExactBranchRetireOwnerContour()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.20 Branch retire-record owner-VT consumer contour",
            paper, StringComparison.Ordinal);
        Assert.Contains("Invalid owner therefore wins over all later faults",
            paper, StringComparison.Ordinal);
        Assert.Contains("`RegisterWrite(owner, DestRegID, capturedValue)`",
            paper, StringComparison.Ordinal);
        Assert.Contains("`PcWrite(owner, resolvedTarget)` is appended second",
            paper, StringComparison.Ordinal);
        Assert.Contains("partial packet mutation is not\n   architectural publication",
            paper, StringComparison.Ordinal);
        Assert.Contains("may replace only the one owner assignment",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void GatesCheckedOwnerFaultsAndTwoRecordAppendOrderRemainFrozen()
    {
        string branch = BranchBody();
        Order(branch,
            "bool hasArchitecturalDestinationRegister =",
            "DestRegID != 0",
            "DestRegID != VLIW_Instruction.NoReg;",
            "bool redirectsControlFlow = !IsConditional || ConditionMet;",
            "if (!redirectsControlFlow &&",
            "(!WritesRegister || !hasArchitecturalDestinationRegister))",
            "return;",
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            "? checkedOwner.Value",
            ": NormalizeExecutionVtId(OwnerThreadId);",
            "if (WritesRegister && hasArchitecturalDestinationRegister)",
            "if (!_hasCapturedPrimaryWriteBackResult)",
            "RetireRecord.RegisterWrite(vtId, DestRegID, _capturedPrimaryWriteBackResult)",
            "if (!redirectsControlFlow)",
            "return;",
            "if (!_hasResolvedRetireTargetAddress)",
            "RetireRecord.PcWrite(vtId, _resolvedRetireTargetAddress)");
        Assert.Equal(1, Regex.Matches(branch,
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
        Assert.Equal(1, Regex.Matches(branch,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void NoEffectReturnSuppressesInvalidOwner(int rawOwner)
    {
        var operation = new BranchMicroOp
        {
            OwnerThreadId = rawOwner,
            IsConditional = true,
            ConditionMet = false,
            WritesRegister = false,
            DestRegID = 0
        };
        Processor.CPU_Core core = null!;
        RetireRecord[] records =
        [
            RetireRecord.RegisterWrite(3, 7, 0xA5UL)
        ];
        int count = 1;

        operation.EmitWriteBackRetireRecords(ref core, records, ref count);

        Assert.Equal(1, count);
        Assert.Equal(3, records[0].VtId);
        Assert.Equal(0xA5UL, records[0].Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ActiveInvalidOwnerWinsOverMissingPayloadAndCapacity(int rawOwner)
    {
        var operation = new BranchMicroOp
        {
            OwnerThreadId = rawOwner,
            IsConditional = false,
            WritesRegister = true,
            DestRegID = 1
        };
        Processor.CPU_Core core = null!;
        RetireRecord[] records = [];
        int count = 0;

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => operation.EmitWriteBackRetireRecords(
                    ref core, records, ref count));

        Assert.Equal("ownerThreadId", exception.ParamName);
        Assert.Equal(rawOwner, exception.ActualValue);
        Assert.Equal(0, count);
    }

    [Fact]
    public void MissingLinkWinsBeforeAppendAndMissingTargetFollowsLinkAppend()
    {
        var operation = new BranchMicroOp
        {
            OwnerThreadId = 1,
            IsConditional = false,
            WritesRegister = true,
            DestRegID = 5
        };
        Processor.CPU_Core core = null!;
        RetireRecord[] records = new RetireRecord[2];
        int count = 0;

        InvalidOperationException missingLink =
            Assert.Throws<InvalidOperationException>(
                () => operation.EmitWriteBackRetireRecords(
                    ref core, records, ref count));
        Assert.Contains("captured primary write-back value", missingLink.Message,
            StringComparison.Ordinal);
        Assert.Equal(0, count);

        operation.CapturePrimaryWriteBackResult(0x55UL);
        InvalidOperationException missingTarget =
            Assert.Throws<InvalidOperationException>(
                () => operation.EmitWriteBackRetireRecords(
                    ref core, records, ref count));
        Assert.Contains("resolved control-flow target address",
            missingTarget.Message, StringComparison.Ordinal);
        Assert.Equal(1, count);
        Assert.Equal(RetireRecordKind.RegisterWrite, records[0].Kind);
        Assert.Equal(1, records[0].VtId);
        Assert.Equal(5, records[0].ArchReg);
        Assert.Equal(0x55UL, records[0].Value);
    }

    [Fact]
    public void SecondCapacityFailureRetainsFirstPacketLocalRecord()
    {
        var operation = new BranchMicroOp
        {
            OwnerThreadId = 2,
            IsConditional = false,
            WritesRegister = true,
            DestRegID = 6
        };
        operation.CapturePrimaryWriteBackResult(0x66UL);
        operation.CaptureResolvedRetireTargetAddress(0x900UL);
        Processor.CPU_Core core = null!;
        RetireRecord[] records = new RetireRecord[1];
        int count = 0;

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => operation.EmitWriteBackRetireRecords(
                    ref core, records, ref count));

        Assert.Equal("WB retire record buffer exhausted.", exception.Message);
        Assert.Equal(1, count);
        Assert.Equal(RetireRecordKind.RegisterWrite, records[0].Kind);
        Assert.Equal(2, records[0].VtId);
        Assert.Equal(6, records[0].ArchReg);
        Assert.Equal(0x66UL, records[0].Value);
    }

    [Fact]
    public void LinkOnlyRedirectOnlyAndTwoRecordShapesRemainExact()
    {
        Processor.CPU_Core core = null!;

        var linkOnly = new BranchMicroOp
        {
            OwnerThreadId = 0,
            IsConditional = true,
            ConditionMet = false,
            WritesRegister = true,
            DestRegID = 3
        };
        linkOnly.CapturePrimaryWriteBackResult(0x33UL);
        RetireRecord[] linkRecords = new RetireRecord[1];
        int linkCount = 0;
        linkOnly.EmitWriteBackRetireRecords(
            ref core, linkRecords, ref linkCount);
        Assert.Equal(1, linkCount);
        Assert.Equal(RetireRecordKind.RegisterWrite, linkRecords[0].Kind);

        var redirectOnly = new BranchMicroOp
        {
            OwnerThreadId = 1,
            IsConditional = true,
            ConditionMet = true,
            WritesRegister = false,
            DestRegID = 0
        };
        redirectOnly.CaptureResolvedRetireTargetAddress(0xA00UL);
        RetireRecord[] redirectRecords = new RetireRecord[1];
        int redirectCount = 0;
        redirectOnly.EmitWriteBackRetireRecords(
            ref core, redirectRecords, ref redirectCount);
        Assert.Equal(1, redirectCount);
        Assert.Equal(RetireRecordKind.PcWrite, redirectRecords[0].Kind);
        Assert.Equal(1, redirectRecords[0].VtId);
        Assert.Equal(0xA00UL, redirectRecords[0].Value);

        var both = new BranchMicroOp
        {
            OwnerThreadId = 3,
            IsConditional = false,
            WritesRegister = true,
            DestRegID = 4
        };
        both.CapturePrimaryWriteBackResult(0x44UL);
        both.CaptureResolvedRetireTargetAddress(0xB00UL);
        RetireRecord[] bothRecords = new RetireRecord[2];
        int bothCount = 0;
        both.EmitWriteBackRetireRecords(
            ref core, bothRecords, ref bothCount);
        Assert.Equal(2, bothCount);
        Assert.Equal(RetireRecordKind.RegisterWrite, bothRecords[0].Kind);
        Assert.Equal(RetireRecordKind.PcWrite, bothRecords[1].Kind);
        Assert.Equal(3, bothRecords[0].VtId);
        Assert.Equal(3, bothRecords[1].VtId);
    }

    [Fact]
    public void CallerAndSelectedPrefixPublicationBoundaryRemainFrozen()
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
        Assert.Contains("lane.MicroOp.EmitWriteBackRetireRecords(",
            types, StringComparison.Ordinal);
        Order(stage,
            "CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane);",
            "PrevalidateRetireWindowBatchForPublication(",
            "PublishRetiredWriteBackLaneForwarding(lane);",
            "ApplyRetireBatchImmediateEffects(");
        Assert.Contains(
            "RetireCoordinator.Prevalidate(retireBatch.RetireRecords);",
            retire, StringComparison.Ordinal);

        MethodInfo method = typeof(BranchMicroOp).GetMethod(
            nameof(BranchMicroOp.EmitWriteBackRetireRecords),
            BindingFlags.Public | BindingFlags.Instance)!;
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.True(typeof(BranchMicroOp).IsSealed);
    }

    [Fact]
    public void ClosedDecisionAcceptsOnlyTheAuthorizedBranchProjectionCaller()
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

        string branch = BranchBody();
        foreach (string forbidden in new[]
                 {
                     "MemoryBankId", "SlotId", "ChannelId", "DomainId",
                     "TokenId", "JsonSerializer", "Dictionary<",
                     "Math.Clamp", "%", "<<", ">>"
                 })
        {
            Assert.DoesNotContain(forbidden, branch,
                StringComparison.Ordinal);
        }
    }

    private static string BranchBody()
    {
        string carrier = ExtractBalanced(Read(FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Control", "MicroOp.Control.cs"), "public sealed class BranchMicroOp");
        return Slice(carrier,
            "public override void EmitWriteBackRetireRecords(",
            "public override bool TryGetPrimaryWriteBackResult(");
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
