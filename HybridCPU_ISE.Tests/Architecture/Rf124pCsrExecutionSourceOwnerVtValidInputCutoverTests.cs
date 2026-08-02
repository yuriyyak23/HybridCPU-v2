using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124pCsrExecutionSourceOwnerVtValidInputCutoverTests
{
    [Fact]
    public void ActiveResolverUsesCheckedProjectionWithExactRawInvalidArm()
    {
        string resolver = ResolverBody();
        Order(resolver,
            "if (!UsesSourceRegisterWriteValue)",
            "return WriteValue;",
            "if (!HasArchitecturalSourceRegister)",
            "return WriteValue;",
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            "? checkedOwner.Value",
            ": NormalizeExecutionVtId(OwnerThreadId);",
            "TryReadUnifiedArchValue(ref core, vtId, SrcRegID, out ulong value)",
            "? value",
            ": WriteValue;");
        Assert.Equal(1, Regex.Matches(resolver,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
        Assert.Equal(1, Regex.Matches(resolver,
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Vt0ThroughVt3ReadTheOwnerContextNotTheCarrierContext(int owner)
    {
        const ushort source = 7;
        ulong ownerValue = 0xA500_0000UL + (ulong)owner;
        ulong carrierValue = 0xB600_0000UL + (ulong)owner;
        int carrier = (owner + 1) & 3;
        var core = new Processor.CPU_Core(0);
        int ownerPhysical = 96 + owner;
        int carrierPhysical = 104 + carrier;
        core.ArchRenameMap.Remap(owner, source, ownerPhysical);
        core.ArchRenameMap.Remap(carrier, source, carrierPhysical);
        core.PhysicalRegisters.Write(ownerPhysical, ownerValue);
        core.PhysicalRegisters.Write(carrierPhysical, carrierValue);
        var operation = new ResolverProbe
        {
            OwnerThreadId = owner,
            VirtualThreadId = carrier,
            SrcRegID = source,
            WriteValue = 0xC700_0000UL,
            UsesSource = true
        };

        Assert.Equal(ownerValue, operation.Resolve(ref core));
        Assert.NotEqual(carrierValue, operation.Resolve(ref core));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void BothFallbackGatesStillSuppressInvalidOwnerValidation(int rawOwner)
    {
        Processor.CPU_Core core = null!;
        var disabled = new ResolverProbe
        {
            OwnerThreadId = rawOwner,
            SrcRegID = 7,
            WriteValue = 0xD8,
            UsesSource = false
        };
        Assert.Equal(0xD8UL, disabled.Resolve(ref core));

        foreach (ushort source in new ushort[]
                 {
                     0,
                     VLIW_Instruction.NoArchReg,
                     VLIW_Instruction.NoReg
                 })
        {
            var absent = new ResolverProbe
            {
                OwnerThreadId = rawOwner,
                SrcRegID = source,
                WriteValue = 0xE9,
                UsesSource = true
            };
            Assert.Equal(0xE9UL, absent.Resolve(ref core));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ActiveInvalidOwnerKeepsExactFaultBeforeReadAndFallback(int rawOwner)
    {
        Processor.CPU_Core core = null!;
        foreach (ushort source in new ushort[] { 1, 31, 32, 254, 256, 65534 })
        {
            var operation = new ResolverProbe
            {
                OwnerThreadId = rawOwner,
                SrcRegID = source,
                WriteValue = 0xFA,
                UsesSource = true
            };

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => operation.Resolve(ref core));
            Assert.Equal("ownerThreadId", exception.ParamName);
            Assert.Equal(rawOwner, exception.ActualValue);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ReadFailureAndNoncanonicalSourceFallbackRemainExact(int owner)
    {
        Processor.CPU_Core core = null!;
        foreach (ushort source in new ushort[] { 1, 31, 32, 254, 256, 65534 })
        {
            var operation = new ResolverProbe
            {
                OwnerThreadId = owner,
                SrcRegID = source,
                WriteValue = 0x10B,
                UsesSource = true
            };
            Assert.Equal(0x10BUL, operation.Resolve(ref core));
        }
    }

    [Fact]
    public void ThreeConcreteCallersAndAdjacentCsrContoursRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        string eager = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch",
            "ExecutionDispatcherV4.CsrAndSmtVt.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Equal(3, Regex.Matches(control,
            @"\bResolveConfiguredWriteValue\s*\(\s*ref core\s*\)").Count);
        foreach (string subclass in new[]
                 {
                     "CsrReadWriteMicroOp",
                     "CsrReadSetMicroOp",
                     "CsrReadClearMicroOp"
                 })
        {
            Assert.Equal(1, Regex.Matches(
                ExtractBalanced(control, $"public sealed class {subclass}"),
                @"\bResolveConfiguredWriteValue\s*\(").Count);
        }

        Assert.DoesNotContain("ResolveConfiguredWriteValue", eager,
            StringComparison.Ordinal);
        Assert.Contains("ReadExecutionRegister(state, vtId, instr.Rs1)",
            eager, StringComparison.Ordinal);
        Assert.Contains("lane.VirtualThreadId,", retire,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionProjectionInventoryAddsOnlyTheResolverConsumer()
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

    [Fact]
    public void SignaturesStorageAndUnrelatedIdentifierFamiliesStayUnchanged()
    {
        string resolver = ResolverBody();
        Assert.DoesNotContain("VirtualThreadId", resolver,
            StringComparison.Ordinal);
        foreach (string forbidden in new[]
                 {
                     "MemoryBankId", "SlotId", "LaneId", "ChannelId",
                     "DomainId", "TokenId", "AcceleratorTokenHandle",
                     "JsonSerializer", "Dictionary<", "Math.Clamp",
                     "%", "<<", ">>"
                 })
        {
            Assert.DoesNotContain(forbidden, resolver,
                StringComparison.Ordinal);
        }

        Assert.Equal(typeof(int),
            typeof(CSRMicroOp).GetProperty(nameof(MicroOp.OwnerThreadId))!
                .PropertyType);
        Assert.Equal(typeof(ushort),
            typeof(CSRMicroOp).GetProperty(nameof(CSRMicroOp.SrcRegID))!
                .PropertyType);
        Assert.Equal(typeof(ulong),
            typeof(CSRMicroOp).GetProperty(nameof(CSRMicroOp.WriteValue))!
                .PropertyType);
    }

    private sealed class ResolverProbe : CSRMicroOp
    {
        public bool UsesSource { get; init; }

        protected override bool UsesSourceRegisterWriteValue => UsesSource;

        public ulong Resolve(ref Processor.CPU_Core core) =>
            ResolveConfiguredWriteValue(ref core);
    }

    private static string ResolverBody() => Slice(
        ExtractBalanced(Read(FindRepositoryRoot(), "HybridCPU_ISE",
                "CloseToHSL", "Core", "Pipeline", "MicroOps", "Control",
                "MicroOp.Control.cs"),
            "public abstract class CSRMicroOp"),
        "protected ulong ResolveConfiguredWriteValue(",
        "internal static CsrStorageSurface ResolveStorageSurface(");

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
