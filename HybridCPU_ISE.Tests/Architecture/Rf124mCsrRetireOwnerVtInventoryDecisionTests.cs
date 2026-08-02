using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124mCsrRetireOwnerVtInventoryDecisionTests
{
    [Fact]
    public void PaperOwnsAndSeparatesTheThreeCsrContours()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.22 CSR compatibility retire-record owner-VT consumer contour",
            paper, StringComparison.Ordinal);
        Assert.Contains("contains CSR surface/address/read/write and\n   destination facts but no VT field",
            paper, StringComparison.Ordinal);
        Assert.Contains("already-carried\n   `lane.VirtualThreadId`",
            paper, StringComparison.Ordinal);
        Assert.Contains("Register-source CSR write-value resolution is an execution-time operand",
            paper, StringComparison.Ordinal);
        Assert.Contains("There is no local completion or result-presence flag",
            paper, StringComparison.Ordinal);
        Assert.Contains("Invalid owner therefore wins over\n   exhausted capacity",
            paper, StringComparison.Ordinal);
        Assert.Contains("may replace only the one producing-path owner",
            paper, StringComparison.Ordinal);
        Assert.Contains("may not change `CsrRetireEffect`",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void PresenceGatesOwnerAppendAndPayloadOrderRemainFrozen()
    {
        string emitter = CsrEmitterBody();
        Order(emitter,
            "if (ReadsCsr &&",
            "WritesRegister &&",
            "HasArchitecturalDestinationRegister)",
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            "? checkedOwner.Value",
            ": NormalizeExecutionVtId(OwnerThreadId);",
            "AppendWriteBackRetireRecord(",
            "retireRecords,",
            "ref retireRecordCount,",
            "RetireRecord.RegisterWrite(vtId, DestRegID, _readValue));");
        Assert.Equal(1, Regex.Matches(emitter,
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
        Assert.Equal(1, Regex.Matches(emitter,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
        Assert.DoesNotContain("CreateRetireEffect", emitter,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void EveryNonProducingGateSuppressesInvalidOwner(int rawOwner)
    {
        Processor.CPU_Core core = null!;
        RetireRecord[] records =
        [
            RetireRecord.RegisterWrite(3, 7, 0xA5UL)
        ];

        foreach (CSRMicroOp operation in new CSRMicroOp[]
                 {
                     new CsrReadCounterMicroOp
                     {
                         OwnerThreadId = rawOwner,
                         WritesRegister = false,
                         DestRegID = 1
                     },
                     new CsrReadCounterMicroOp
                     {
                         OwnerThreadId = rawOwner,
                         WritesRegister = true,
                         DestRegID = 0
                     },
                     new CsrReadCounterMicroOp
                     {
                         OwnerThreadId = rawOwner,
                         WritesRegister = true,
                         DestRegID = VLIW_Instruction.NoArchReg
                     },
                     new CsrReadCounterMicroOp
                     {
                         OwnerThreadId = rawOwner,
                         WritesRegister = true,
                         DestRegID = VLIW_Instruction.NoReg
                     },
                     new CsrClearMicroOp
                     {
                         OwnerThreadId = rawOwner,
                         WritesRegister = true,
                         DestRegID = 1
                     }
                 })
        {
            int count = 1;
            operation.EmitWriteBackRetireRecords(
                ref core, records, ref count);
            AssertUnchanged(records, count);
        }
    }

    [Fact]
    public void DefaultAndCapturedReadValuesRemainDataWithoutCompletionProof()
    {
        var operation = new CsrReadCounterMicroOp
        {
            OwnerThreadId = 2,
            WritesRegister = true,
            DestRegID = 1
        };
        Processor.CPU_Core core = null!;
        RetireRecord[] records = new RetireRecord[2];
        int count = 0;

        operation.EmitWriteBackRetireRecords(ref core, records, ref count);
        operation.CapturePrimaryWriteBackResult(0xC5AUL);
        operation.EmitWriteBackRetireRecords(ref core, records, ref count);

        Assert.Equal(2, count);
        Assert.Equal(2, records[0].VtId);
        Assert.Equal(1, records[0].ArchReg);
        Assert.Equal(0UL, records[0].Value);
        Assert.Equal(0xC5AUL, records[1].Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ActiveInvalidOwnerWinsOverCapacity(int rawOwner)
    {
        var operation = new CsrReadCounterMicroOp
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
    public void ValidOwnerCapacityFailureAndRawDestinationBehaviorStayExact()
    {
        var full = new CsrReadCounterMicroOp
        {
            OwnerThreadId = 1,
            WritesRegister = true,
            DestRegID = 9
        };
        full.CapturePrimaryWriteBackResult(0xCAFEUL);
        Processor.CPU_Core core = null!;
        RetireRecord[] fullRecords =
        [
            RetireRecord.RegisterWrite(3, 7, 0xA5UL)
        ];
        int fullCount = 1;

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => full.EmitWriteBackRetireRecords(
                    ref core, fullRecords, ref fullCount));
        Assert.Equal("WB retire record buffer exhausted.", exception.Message);
        AssertUnchanged(fullRecords, fullCount);

        var rawDestination = new CsrReadCounterMicroOp
        {
            OwnerThreadId = 0,
            WritesRegister = true,
            DestRegID = 32
        };
        rawDestination.CapturePrimaryWriteBackResult(0x32UL);
        RetireRecord[] rawRecords = new RetireRecord[1];
        int rawCount = 0;
        rawDestination.EmitWriteBackRetireRecords(
            ref core, rawRecords, ref rawCount);
        Assert.Equal(1, rawCount);
        Assert.Equal(32, rawRecords[0].ArchReg);
        Assert.Equal(0x32UL, rawRecords[0].Value);

        string retire = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        Assert.Contains(
            "RetireCoordinator.Prevalidate(retireBatch.RetireRecords);",
            retire, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedMainlineAndExecutionSourceOwnerRemainSeparate()
    {
        string root = FindRepositoryRoot();
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.Types.cs");

        string effect = Slice(control,
            "public readonly struct CsrRetireEffect",
            "public readonly struct AtomicRetireEffect");
        Assert.DoesNotContain("VtId", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("VirtualThreadId", effect,
            StringComparison.Ordinal);
        Order(retire,
            "else if (lane.GeneratedCsrEffect is Core.CsrRetireEffect csrEffect)",
            "retireBatch.CaptureGeneratedCsrEffect(",
            "lane.VirtualThreadId,",
            "csrEffect);");
        Order(types,
            "public void CaptureGeneratedCsrEffect(",
            "int virtualThreadId,",
            "CPU_Core.EmitGeneratedCsrRetireRecords(",
            "virtualThreadId,",
            "csrEffect,");

        string resolver = Slice(CsrCarrierBody(),
            "protected ulong ResolveConfiguredWriteValue(",
            "internal static CsrStorageSurface ResolveStorageSurface(");
        Order(resolver,
            "if (!UsesSourceRegisterWriteValue)",
            "if (!HasArchitecturalSourceRegister)",
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            "? checkedOwner.Value",
            ": NormalizeExecutionVtId(OwnerThreadId);",
            "TryReadUnifiedArchValue(ref core, vtId, SrcRegID, out ulong value)");
        Assert.Equal(1, Regex.Matches(resolver,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
    }

    [Fact]
    public void PublicConstructionReflectionAndGenericCallerSeamsRemainLive()
    {
        Type baseType = typeof(CSRMicroOp);
        Assert.True(baseType.IsAbstract);
        Assert.True(baseType.GetMethod(
            nameof(CSRMicroOp.EmitWriteBackRetireRecords))!.IsVirtual);
        Assert.True(baseType.GetMethod(
            nameof(CSRMicroOp.CapturePrimaryWriteBackResult))!.IsVirtual);
        Assert.NotNull(baseType.GetField("_readValue",
            BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.True(baseType.GetProperty(nameof(MicroOp.OwnerThreadId))!.CanWrite);
        Assert.True(baseType.GetProperty(nameof(MicroOp.VirtualThreadId))!.CanWrite);
        Assert.True(baseType.GetProperty(nameof(MicroOp.DestRegID))!.CanWrite);

        Type[] concrete = baseType.Assembly.GetTypes()
            .Where(type => type.BaseType == baseType)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(8, concrete.Length);
        Assert.All(concrete, type =>
        {
            Assert.True(type.IsSealed);
            Assert.NotEmpty(type.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance));
        });

        string root = FindRepositoryRoot();
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.Types.cs");
        Order(types,
            "lane.MicroOp.CapturePrimaryWriteBackResult(lane.ResultValue);",
            "lane.MicroOp.EmitWriteBackRetireRecords(");
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

        foreach (string forbidden in new[]
                 {
                     "MemoryBankId", "SlotId", "LaneId", "ChannelId",
                     "DomainId", "TokenId", "JsonSerializer", "Dictionary<",
                     "Math.Clamp", "%", "<<", ">>"
                 })
        {
            Assert.DoesNotContain(forbidden, CsrEmitterBody(),
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

    private static string CsrEmitterBody() => Slice(CsrCarrierBody(),
        "public override void EmitWriteBackRetireRecords(",
        "public override bool TryGetPrimaryWriteBackResult(");

    private static string CsrCarrierBody() => ExtractBalanced(
        Read(FindRepositoryRoot(), "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs"),
        "public abstract class CSRMicroOp");

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
