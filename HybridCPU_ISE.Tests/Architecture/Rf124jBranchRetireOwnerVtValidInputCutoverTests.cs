using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124jBranchRetireOwnerVtValidInputCutoverTests
{
    [Fact]
    public void ActivePathUsesCheckedProjectionWithExactRawInvalidArm()
    {
        string branch = BranchBody();
        Order(branch,
            "if (!redirectsControlFlow &&",
            "return;",
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            "? checkedOwner.Value",
            ": NormalizeExecutionVtId(OwnerThreadId);",
            "if (WritesRegister && hasArchitecturalDestinationRegister)",
            "RetireRecord.RegisterWrite(vtId, DestRegID, _capturedPrimaryWriteBackResult)",
            "if (!redirectsControlFlow)",
            "if (!_hasResolvedRetireTargetAddress)",
            "RetireRecord.PcWrite(vtId, _resolvedRetireTargetAddress)");
        Assert.Equal(1, Regex.Matches(branch,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
        Assert.Equal(1, Regex.Matches(branch,
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
    }

    [Fact]
    public void Vt0ThroughVt3PreserveBothRecordOwnersAndOrder()
    {
        for (int rawOwner = VtId.MinValue;
             rawOwner <= VtId.MaxValue;
             rawOwner++)
        {
            var operation = new BranchMicroOp
            {
                OwnerThreadId = rawOwner,
                VirtualThreadId = (rawOwner + 1) % VtId.SmtWayCount,
                IsConditional = false,
                WritesRegister = true,
                DestRegID = (ushort)(8 + rawOwner)
            };
            operation.CapturePrimaryWriteBackResult(0x500UL + (ulong)rawOwner);
            operation.CaptureResolvedRetireTargetAddress(
                0x900UL + (ulong)rawOwner);
            Processor.CPU_Core core = null!;
            RetireRecord[] records = new RetireRecord[2];
            int count = 0;

            operation.EmitWriteBackRetireRecords(ref core, records, ref count);

            Assert.Equal(2, count);
            Assert.Equal(RetireRecordKind.RegisterWrite, records[0].Kind);
            Assert.Equal(RetireRecordKind.PcWrite, records[1].Kind);
            Assert.Equal(rawOwner, records[0].VtId);
            Assert.Equal(rawOwner, records[1].VtId);
            Assert.Equal(8 + rawOwner, records[0].ArchReg);
            Assert.Equal(0x500UL + (ulong)rawOwner, records[0].Value);
            Assert.Equal(0x900UL + (ulong)rawOwner, records[1].Value);
            Assert.Equal((rawOwner + 1) % VtId.SmtWayCount,
                operation.VirtualThreadId);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void NoEffectInvalidOwnerStillReturnsWithoutMutation(int rawOwner)
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
    public void ActiveInvalidOwnerKeepsExactFaultAndWinsLaterFaults(int rawOwner)
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
    public void MissingTargetAndSecondCapacityFailureRetainFirstLocalRecord()
    {
        var operation = new BranchMicroOp
        {
            OwnerThreadId = 2,
            IsConditional = false,
            WritesRegister = true,
            DestRegID = 6
        };
        operation.CapturePrimaryWriteBackResult(0x66UL);
        Processor.CPU_Core core = null!;
        RetireRecord[] records = new RetireRecord[1];
        int count = 0;

        InvalidOperationException missingTarget =
            Assert.Throws<InvalidOperationException>(
                () => operation.EmitWriteBackRetireRecords(
                    ref core, records, ref count));
        Assert.Contains("resolved control-flow target address",
            missingTarget.Message, StringComparison.Ordinal);
        Assert.Equal(1, count);
        Assert.Equal(2, records[0].VtId);

        operation.CaptureResolvedRetireTargetAddress(0x900UL);
        count = 0;
        InvalidOperationException capacity =
            Assert.Throws<InvalidOperationException>(
                () => operation.EmitWriteBackRetireRecords(
                    ref core, records, ref count));
        Assert.Equal("WB retire record buffer exhausted.", capacity.Message);
        Assert.Equal(1, count);
        Assert.Equal(RetireRecordKind.RegisterWrite, records[0].Kind);
        Assert.Equal(2, records[0].VtId);
    }

    [Fact]
    public void ProjectionCallerInventoryAddsOnlyBranchConsumer()
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
