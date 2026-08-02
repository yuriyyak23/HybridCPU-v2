using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123wSystemEventEcallRegisterMaskInventoryTests
{
    [Fact]
    public void PaperDefinesConstantRoleAbsenceWireAndLaterCheckedCutoverOnly()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.10 System-event ECALL architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("private compile-time `int` constant\n`EcallCodeRegister=17`",
            paper, StringComparison.Ordinal);
        Assert.Contains("Metadata absence is expressed only by the event-kind\npredicate",
            paper, StringComparison.Ordinal);
        Assert.Contains("no explicit architectural-register operand in the ISA/compiler wire",
            paper, StringComparison.Ordinal);
        Assert.Contains("no raw per-value fallback is required at this call site",
            paper, StringComparison.Ordinal);
        Assert.Contains("Changing\nevent-kind or opcode invalid behavior",
            paper, StringComparison.Ordinal);
    }


    [Fact]
    public void SourceShapeFreezesPredicateListsLaterCheckedFoldAndPublicationOrder()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "System",
            "MicroOp.System.cs");
        string carrier = ExtractBalanced(source,
            "public sealed class SysEventMicroOp");
        string initialize = ExtractBalanced(carrier,
            "public void InitializeMetadata()");

        Assert.Equal(1, Count(carrier,
            "private const int EcallCodeRegister = 17;"));
        Assert.Equal(2, Count(initialize,
            "EventKind == SystemEventKind.Ecall"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(initialize,
            "ArchRegId.Create(EcallCodeRegister)"));
        Assert.DoesNotContain(
            "ResourceMaskBuilder.ForRegisterRead(EcallCodeRegister)",
            initialize, StringComparison.Ordinal);
        AssertOrdered(initialize,
            "ApplyCanonicalClassificationAndPlacement();",
            "ReadRegisters = EventKind == SystemEventKind.Ecall",
            "? new[] { EcallCodeRegister }",
            "WriteRegisters = Array.Empty<int>();",
            "ReadMemoryRanges = Array.Empty<(ulong, ulong)>();",
            "WriteMemoryRanges = Array.Empty<(ulong, ulong)>();",
            "ResourceMask = ResourceBitset.Zero;",
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(",
            "ArchRegId.Create(EcallCodeRegister)",
            "PublishExplicitStructuralSafetyMask();",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void EveryEventKindBytePreservesEcallOnlyDependencyAndInvalidNeutrality()
    {
        for (int raw = byte.MinValue; raw <= byte.MaxValue; raw++)
        {
            var operation = new SysEventMicroOp
            {
                EventKind = (SystemEventKind)(byte)raw
            };
            operation.InitializeMetadata();

            bool ecall = raw == (byte)SystemEventKind.Ecall;
            Assert.Equal(ecall ? [17] : [], operation.ReadRegisters);
            Assert.Empty(operation.WriteRegisters);
            Assert.Empty(operation.ReadMemoryRanges);
            Assert.Empty(operation.WriteMemoryRanges);
            Assert.Equal(
                ecall
                    ? ResourceMaskBuilder.ForRegisterRead(17)
                    : ResourceBitset.Zero,
                operation.ResourceMask);

            if (!Enum.IsDefined(typeof(SystemEventKind), (byte)raw))
            {
                YAKSys_Hybrid_CPU.Processor.CPU_Core core = new(0);
                Assert.Null(operation.CreatePipelineEvent(ref core));
            }
        }
    }

    [Fact]
    public void DefaultMismatchReflectionAndArrayMutationSeamsRemainExplicit()
    {
        var defaultCarrier = new SysEventMicroOp();
        defaultCarrier.InitializeMetadata();
        Assert.Equal(SystemEventKind.Fence, defaultCarrier.EventKind);
        Assert.Empty(defaultCarrier.ReadRegisters);

        var mismatched = new SysEventMicroOp
        {
            OpCode = IsaOpcodeValues.FENCE,
            EventKind = SystemEventKind.Ecall,
            OrderGuarantee = SystemEventOrderGuarantee.None
        };
        mismatched.InitializeMetadata();
        Assert.Equal([17], mismatched.ReadRegisters);

        int[] reads = Assert.IsType<int[]>(mismatched.ReadRegisters);
        ResourceBitset cachedHazard =
            mismatched.AdmissionMetadata.RegisterHazardMask;
        reads[0] = 0;
        Assert.Equal(0, mismatched.AdmissionMetadata.ReadRegisters[0]);
        Assert.Equal(cachedHazard,
            mismatched.AdmissionMetadata.RegisterHazardMask);
        Assert.Equal(
            MicroOpAdmissionMetadata.BuildRegisterHazardMask([17], []),
            cachedHazard);

        PropertyInfo eventKind = typeof(SysEventMicroOp).GetProperty(
            nameof(SysEventMicroOp.EventKind)) ??
            throw new MissingMemberException();
        eventKind.SetValue(mismatched, SystemEventKind.Fence);
        mismatched.InitializeMetadata();
        Assert.Empty(mismatched.ReadRegisters);
        Assert.Equal(ResourceBitset.Zero, mismatched.ResourceMask);
    }

    [Fact]
    public void FactoryCompilerExecutionEventAndRetireAuthoritiesRemainSeparate()
    {
        string root = FindRepositoryRoot();
        string registry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Initialize.Base.cs");
        string compiler = ReadTree(root, "HybridCPU_Compiler");
        string system = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "System", "MicroOp.System.cs");
        string direct = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.System.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        string production = ReadTree(root, "HybridCPU_ISE");

        Assert.Contains(
            "ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForEcall)",
            registry, StringComparison.Ordinal);
        Assert.Contains("case InstructionsEnum.ECALL:", compiler,
            StringComparison.Ordinal);
        Assert.Contains(
            "systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Ecall;",
            compiler, StringComparison.Ordinal);
        Assert.DoesNotContain("new SysEventMicroOp", compiler,
            StringComparison.Ordinal);
        Assert.Contains("EcallCodeRegister = 17", system,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryReadUnifiedArchValue(ref core, vtId, EcallCodeRegister, out ulong value)",
            system, StringComparison.Ordinal);
        Assert.Contains("refusing hidden zero-code fallback", system,
            StringComparison.Ordinal);
        Assert.Equal(2, Count(direct,
            "ReadExecutionRegister(state, vtId, 17)"));
        Assert.Contains("EcallCode = 0", retire, StringComparison.Ordinal);
        Assert.Contains("EcallCode = ecallEvent.EcallCode", retire,
            StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<SysEventMicroOp>",
            production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize(sysEventMicroOp",
            production, StringComparison.OrdinalIgnoreCase);

        SysEventMicroOp canonical = SysEventMicroOp.ForEcall();
        Assert.Equal((uint)InstructionsEnum.ECALL, canonical.OpCode);
        Assert.Equal(SystemEventKind.Ecall, canonical.EventKind);
        Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary,
            canonical.OrderGuarantee);
        Assert.Equal([17], canonical.ReadRegisters);

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x1230, activeVtId: 0);
        core.WriteCommittedArch(0, 17, 0UL);
        EcallEvent zero = Assert.IsType<EcallEvent>(
            canonical.CreatePipelineEvent(ref core));
        Assert.Equal(0L, zero.EcallCode);
        core.WriteCommittedArch(0, 17, 93UL);
        EcallEvent nonzero = Assert.IsType<EcallEvent>(
            canonical.CreatePipelineEvent(ref core));
        Assert.Equal(93L, nonzero.EcallCode);

        YAKSys_Hybrid_CPU.Processor.CPU_Core unavailable = default;
        Assert.Throws<InvalidOperationException>(
            () => canonical.CreatePipelineEvent(ref unavailable));
    }

    private static void AssertManifest(
        string root,
        string relativeRoot,
        string token,
        int count,
        string sha256)
    {
        string[] paths = FindFilesContaining(root, relativeRoot, token);
        Assert.Equal(count, paths.Length);
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join("\n", paths))))
            .ToLowerInvariant());
    }

    private static string[] FindFilesContaining(
        string root,
        string relativeRoot,
        string token)
    {
        string directory = Path.Combine(root, relativeRoot);
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => Path.GetFileName(path) is not
                "Rf123wSystemEventEcallRegisterMaskInventoryTests.cs" and not
                "Rf123xSystemEventEcallRegisterMaskValidInputCutoverTests.cs")
            .Where(path => Regex.IsMatch(File.ReadAllText(path),
                $@"\b{Regex.Escape(token)}\b",
                RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(root, path)
                .Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ExtractBalanced(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{signature}' was not found.");
        int brace = source.IndexOf('{', start);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }
        throw new InvalidOperationException($"'{signature}' was not closed.");
    }

    private static void AssertOrdered(string source, params string[] markers)
    {
        int previous = -1;
        foreach (string marker in markers)
        {
            int index = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previous,
                $"Marker '{marker}' was missing or out of order.");
            previous = index;
        }
    }

    private static string ReadTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.Exists(Path.Combine(root, relativeRoot))
            ? Directory.EnumerateFiles(Path.Combine(root, relativeRoot),
                    "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText)
            : []);

    private static bool IsBuildOutput(string path) =>
        path.Contains(
            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset,
            StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

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
