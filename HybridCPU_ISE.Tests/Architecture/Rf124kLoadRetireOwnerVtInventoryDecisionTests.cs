using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124kLoadRetireOwnerVtInventoryDecisionTests
{
    [Fact]
    public void PaperOwnsTheExactLoadRetireOwnerContour()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.21 Scalar-load retire-record owner-VT consumer contour",
            paper, StringComparison.Ordinal);
        Assert.Contains("There is no local completion or result-presence flag",
            paper, StringComparison.Ordinal);
        Assert.Contains("Architectural x0 is not absence", paper,
            StringComparison.Ordinal);
        Assert.Contains("Invalid owner therefore wins over capacity exhaustion",
            paper, StringComparison.Ordinal);
        Assert.Contains("Raw destinations\n   32..65534 can materialize packet-locally",
            paper, StringComparison.Ordinal);
        Assert.Contains("may replace only the one producing-path owner",
            paper, StringComparison.Ordinal);
        Assert.Contains("may not add completion/result presence", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SuppressionGatesOwnerAppendAndValueOrderRemainFrozen()
    {
        string load = LoadRetireBody();
        Order(load,
            "if (this.IsSpeculative && this.Faulted)",
            "return;",
            "if (WritesRegister && DestRegID != VLIW_Instruction.NoReg)",
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            "? checkedOwner.Value",
            ": NormalizeExecutionVtId(OwnerThreadId);",
            "AppendWriteBackRetireRecord(",
            "retireRecords,",
            "ref retireRecordCount,",
            "RetireRecord.RegisterWrite(vtId, DestRegID, _loadedValue));");
        Assert.Equal(1, Regex.Matches(load,
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
        Assert.Equal(1, Regex.Matches(load,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
        Assert.DoesNotContain("_has", load, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void SpeculativeFaultAndNoRecordGatesSuppressInvalidOwner(int rawOwner)
    {
        Processor.CPU_Core core = null!;
        RetireRecord[] records =
        [
            RetireRecord.RegisterWrite(3, 7, 0xA5UL)
        ];

        var suppressed = new LoadMicroOp
        {
            OwnerThreadId = rawOwner,
            IsSpeculative = true,
            Faulted = true,
            WritesRegister = true,
            DestRegID = 1
        };
        int count = 1;
        suppressed.EmitWriteBackRetireRecords(ref core, records, ref count);
        AssertUnchanged(records, count);

        var nonWriting = new LoadMicroOp
        {
            OwnerThreadId = rawOwner,
            WritesRegister = false,
            DestRegID = 1
        };
        count = 1;
        nonWriting.EmitWriteBackRetireRecords(ref core, records, ref count);
        AssertUnchanged(records, count);

        var absentDestination = new LoadMicroOp
        {
            OwnerThreadId = rawOwner,
            WritesRegister = true,
            DestRegID = VLIW_Instruction.NoReg
        };
        count = 1;
        absentDestination.EmitWriteBackRetireRecords(
            ref core, records, ref count);
        AssertUnchanged(records, count);
    }

    [Fact]
    public void X0AndDefaultStoredValueStillMaterializeWithoutCompletionProof()
    {
        var operation = new LoadMicroOp
        {
            OwnerThreadId = 2,
            WritesRegister = true,
            DestRegID = 0
        };
        Processor.CPU_Core core = null!;
        RetireRecord[] records = new RetireRecord[1];
        int count = 0;

        operation.EmitWriteBackRetireRecords(ref core, records, ref count);

        Assert.Equal(1, count);
        Assert.Equal(RetireRecordKind.RegisterWrite, records[0].Kind);
        Assert.Equal(2, records[0].VtId);
        Assert.Equal(0, records[0].ArchReg);
        Assert.Equal(0UL, records[0].Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ActiveInvalidOwnerWinsOverCapacity(int rawOwner)
    {
        var operation = new LoadMicroOp
        {
            OwnerThreadId = rawOwner,
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
    public void ValidOwnerCapacityFailureIsNonMutating()
    {
        var operation = new LoadMicroOp
        {
            OwnerThreadId = 1,
            WritesRegister = true,
            DestRegID = 9
        };
        operation.CapturePrimaryWriteBackResult(0xCAFEUL);
        Processor.CPU_Core core = null!;
        RetireRecord[] records =
        [
            RetireRecord.RegisterWrite(3, 7, 0xA5UL)
        ];
        int count = 1;

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => operation.EmitWriteBackRetireRecords(
                    ref core, records, ref count));

        Assert.Equal("WB retire record buffer exhausted.", exception.Message);
        AssertUnchanged(records, count);
    }

    [Fact]
    public void RawInvalidDestinationRemainsPacketLocalUntilPrevalidation()
    {
        var operation = new LoadMicroOp
        {
            OwnerThreadId = 0,
            WritesRegister = true,
            DestRegID = 32
        };
        operation.CapturePrimaryWriteBackResult(0x32UL);
        Processor.CPU_Core core = null!;
        RetireRecord[] records = new RetireRecord[1];
        int count = 0;

        operation.EmitWriteBackRetireRecords(ref core, records, ref count);

        Assert.Equal(1, count);
        Assert.Equal(32, records[0].ArchReg);
        Assert.Equal(0x32UL, records[0].Value);

        string retire = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        Assert.Contains(
            "RetireCoordinator.Prevalidate(retireBatch.RetireRecords);",
            retire, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicReflectionSubclassAndSelectedPrefixSeamsRemainFrozen()
    {
        Type type = typeof(LoadMicroOp);
        Assert.False(type.IsSealed);
        Assert.NotEmpty(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.True(type.GetMethod(
            nameof(LoadMicroOp.EmitWriteBackRetireRecords))!.IsVirtual);
        Assert.True(type.GetMethod(
            nameof(LoadMicroOp.CapturePrimaryWriteBackResult))!.IsVirtual);
        Assert.NotNull(type.GetField("_loadedValue",
            BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.True(type.GetProperty(nameof(MicroOp.OwnerThreadId))!.CanWrite);
        Assert.True(type.GetProperty(nameof(MicroOp.VirtualThreadId))!.CanWrite);
        Assert.True(type.GetProperty(nameof(MicroOp.DestRegID))!.CanWrite);

        string root = FindRepositoryRoot();
        Assert.Empty(FindDerivedClasses(root, "HybridCPU_ISE", "LoadMicroOp"));
        Assert.Equal(3, FindDerivedClasses(
            root, "HybridCPU_ISE.Tests", "LoadMicroOp").Count);

        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.Types.cs");
        string stage = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.cs");
        Order(types,
            "lane.MicroOp.CapturePrimaryWriteBackResult(lane.ResultValue);",
            "lane.MicroOp.EmitWriteBackRetireRecords(");
        Order(stage,
            "CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane);",
            "PrevalidateRetireWindowBatchForPublication(",
            "PublishRetiredWriteBackLaneForwarding(lane);",
            "ApplyRetireBatchImmediateEffects(");
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
        Assert.Empty(FindOccurrences(root, "HybridCPU_Compiler",
            "TryGetArchitecturalOwnerVtId"));
        Assert.Empty(FindOccurrences(root, "TestAssemblerConsoleApps",
            "TryGetArchitecturalOwnerVtId"));

        string load = LoadRetireBody();
        foreach (string forbidden in new[]
                 {
                     "MemoryBankId", "SlotId", "LaneId", "ChannelId",
                     "DomainId", "TokenId", "JsonSerializer", "Dictionary<",
                     "Math.Clamp", "%", "<<", ">>"
                 })
        {
            Assert.DoesNotContain(forbidden, load,
                StringComparison.Ordinal);
        }
    }

    private static void AssertUnchanged(RetireRecord[] records, int count)
    {
        Assert.Equal(1, count);
        Assert.Equal(3, records[0].VtId);
        Assert.Equal(7, records[0].ArchReg);
        Assert.Equal(0xA5UL, records[0].Value);
    }

    private static string LoadRetireBody()
    {
        string carrier = ExtractBalanced(Read(FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Memory", "MicroOp.LoadStore.cs"), "public class LoadMicroOp");
        return Slice(carrier,
            "public override void EmitWriteBackRetireRecords(",
            "public override bool TryGetPrimaryWriteBackResult(");
    }

    private static List<string> FindDerivedClasses(
        string root, string sourceRoot, string baseType) =>
        Directory.EnumerateFiles(Path.Combine(root, sourceRoot), "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !HasPathSegment(path, "bin") &&
                           !HasPathSegment(path, "obj") &&
                           !HasPathSegment(path, "Legacy"))
            .Where(path => Regex.IsMatch(File.ReadAllText(path),
                $@"\bclass\s+\w+\s*:\s*{baseType}\b"))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToList();

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
