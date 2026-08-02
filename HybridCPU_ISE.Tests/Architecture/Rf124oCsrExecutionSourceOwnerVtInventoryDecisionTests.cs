using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124oCsrExecutionSourceOwnerVtInventoryDecisionTests
{
    [Fact]
    public void PaperFreezesTheExecutionSourceOwnerContour()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");
        const string marker =
            "#### 3.7.23 CSR execution source-register owner-VT consumer contour";
        int decisionStart = paper.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(decisionStart >= 0);
        string decision = paper[decisionStart..];

        Order(decision,
            "`UsesSourceRegisterWriteValue`",
            "`HasArchitecturalSourceRegister`",
            "`NormalizeExecutionVtId(OwnerThreadId)`",
            "`TryReadUnifiedArchValue`",
            "`WriteValue`");
        Assert.Contains("VT0..VT3 are valid", decision,
            StringComparison.Ordinal);
        Assert.Contains("none aliases an absent owner to VT0", decision,
            StringComparison.Ordinal);
        Assert.Contains("`NoArchReg=255`", decision,
            StringComparison.Ordinal);
        Assert.Contains("`NoReg=65535`", decision,
            StringComparison.Ordinal);
        Assert.Contains("32..254 except 255", decision,
            StringComparison.Ordinal);
        Assert.Contains("256..65534", decision,
            StringComparison.Ordinal);
        Assert.Contains("eager `ExecutionDispatcherV4.ResolveCsrEffect`",
            decision,
            StringComparison.Ordinal);
        Assert.Contains("representational safety", decision,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolverUsesApprovedCheckedSelectionBelowBothFallbackGates()
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
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
        Assert.Equal(1, Regex.Matches(resolver,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
        Assert.DoesNotContain("VirtualThreadId", resolver,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void BothFallbackGatesSuppressInvalidOwnerValidation(int rawOwner)
    {
        Processor.CPU_Core core = null!;
        var disabled = new ResolverProbe
        {
            OwnerThreadId = rawOwner,
            SrcRegID = 7,
            WriteValue = 0xA5,
            UsesSource = false
        };
        Assert.Equal(0xA5UL, disabled.Resolve(ref core));

        foreach (ushort absent in new ushort[]
                 {
                     0,
                     VLIW_Instruction.NoArchReg,
                     VLIW_Instruction.NoReg
                 })
        {
            var operation = new ResolverProbe
            {
                OwnerThreadId = rawOwner,
                SrcRegID = absent,
                WriteValue = 0xB6,
                UsesSource = true
            };
            Assert.Equal(0xB6UL, operation.Resolve(ref core));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ActiveSourceKeepsOwnerFaultBeforeReadOrFallback(int rawOwner)
    {
        Processor.CPU_Core core = null!;
        foreach (ushort source in new ushort[] { 1, 31, 32, 254, 256, 65534 })
        {
            var operation = new ResolverProbe
            {
                OwnerThreadId = rawOwner,
                SrcRegID = source,
                WriteValue = 0xC7,
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
    public void RepresentableOwnersKeepReadFailureFallbackForEveryRawSourceClass(
        int owner)
    {
        Processor.CPU_Core core = null!;
        foreach (ushort source in new ushort[]
                 {
                     1, 31, 32, 254, 256, 65534
                 })
        {
            var operation = new ResolverProbe
            {
                OwnerThreadId = owner,
                VirtualThreadId = (owner + 1) & 3,
                SrcRegID = source,
                WriteValue = 0xD8,
                UsesSource = true
            };

            Assert.Equal(0xD8UL, operation.Resolve(ref core));
        }
    }

    [Fact]
    public void ExactlyThreeRegisterSourceSubclassesCallTheResolver()
    {
        string control = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Control",
            "MicroOp.Control.cs");
        Assert.Equal(3, Regex.Matches(control,
            @"\bResolveConfiguredWriteValue\s*\(\s*ref core\s*\)").Count);
        foreach (string subclass in new[]
                 {
                     "CsrReadWriteMicroOp",
                     "CsrReadSetMicroOp",
                     "CsrReadClearMicroOp"
                 })
        {
            string body = ExtractBalanced(control,
                $"public sealed class {subclass}");
            Assert.Equal(1, Regex.Matches(body,
                @"\bResolveConfiguredWriteValue\s*\(").Count);
        }

        foreach (string subclass in new[]
                 {
                     "CsrReadWriteImmediateMicroOp",
                     "CsrReadSetImmediateMicroOp",
                     "CsrReadClearImmediateMicroOp",
                     "CsrReadCounterMicroOp",
                     "CsrClearMicroOp"
                 })
        {
            Assert.DoesNotContain("ResolveConfiguredWriteValue",
                ExtractBalanced(control, $"public sealed class {subclass}"),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EffectMaterializationAndDirectEagerContourRemainSeparate()
    {
        string root = FindRepositoryRoot();
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        string eager = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch",
            "ExecutionDispatcherV4.CsrAndSmtVt.cs");

        string create = Slice(control,
            "public CsrRetireEffect CreateRetireEffect(",
            "public override void EmitWriteBackRetireRecords(");
        Order(create,
            "ResolveStorageSurface(ref core, CSRAddress)",
            "ReadCsr(ref core, storageSurface, CSRAddress)",
            "bool hasCsrWrite = WritesCsr;",
            "hasCsrWrite",
            "? ResolveWriteValue(ref core, priorValue)",
            "CsrRetireEffect.Create(");
        Order(retire,
            "MaterializeCsrEffectWithStableCoreIdentity(csrMicroOp)",
            "effect.ClearsArchitecturalExceptionState",
            "effect.HasCsrWrite",
            "effect.HasRegisterWriteback");

        Assert.DoesNotContain("ResolveConfiguredWriteValue", eager,
            StringComparison.Ordinal);
        Assert.Contains("ReadExecutionRegister(state, vtId, instr.Rs1)",
            eager, StringComparison.Ordinal);
    }

    [Fact]
    public void RawConstructionReflectionWireAndTestSupportSeamsStayFrozen()
    {
        Type type = typeof(CSRMicroOp);
        ConstructorInfo constructor = Assert.Single(type.GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.True(constructor.IsFamily);
        Assert.True(type.GetProperty(nameof(MicroOp.OwnerThreadId))!.CanWrite);
        Assert.Equal(typeof(int),
            type.GetProperty(nameof(MicroOp.OwnerThreadId))!.PropertyType);
        Assert.True(type.GetProperty(nameof(CSRMicroOp.SrcRegID))!.CanWrite);
        Assert.Equal(typeof(ushort),
            type.GetProperty(nameof(CSRMicroOp.SrcRegID))!.PropertyType);
        Assert.True(type.GetProperty(nameof(CSRMicroOp.WriteValue))!.CanWrite);

        MethodInfo resolver = type.GetMethod("ResolveConfiguredWriteValue",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(type.FullName,
                "ResolveConfiguredWriteValue");
        Assert.True(resolver.IsFamily);
        Assert.False(resolver.IsVirtual);

        string root = FindRepositoryRoot();
        string factory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Csr.cs");
        Assert.Contains("NormalizeCanonicalOrSuppressedCsrSourceRegister",
            factory, StringComparison.Ordinal);
        Assert.Contains("SrcRegID = NormalizeCanonicalOrSuppressedCsrSourceRegister",
            factory, StringComparison.Ordinal);
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        Assert.DoesNotContain("CSRMicroOp", testSupport,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionAddsNoCheckedProjectionCallerOrAdjacentIdFamily()
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
                     "DomainId", "TokenId", "AcceleratorTokenHandle",
                     "JsonSerializer", "Dictionary<", "Math.Clamp",
                     "%", "<<", ">>"
                 })
        {
            Assert.DoesNotContain(forbidden, ResolverBody(),
                StringComparison.Ordinal);
        }
    }

    private sealed class ResolverProbe : CSRMicroOp
    {
        public bool UsesSource { get; init; }

        protected override bool UsesSourceRegisterWriteValue => UsesSource;

        public ulong Resolve(ref Processor.CPU_Core core) =>
            ResolveConfiguredWriteValue(ref core);
    }

    private static string ResolverBody() => Slice(CsrCarrierBody(),
        "protected ulong ResolveConfiguredWriteValue(",
        "internal static CsrStorageSurface ResolveStorageSurface(");

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
