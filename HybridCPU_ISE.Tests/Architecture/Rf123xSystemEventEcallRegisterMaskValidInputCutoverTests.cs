using System.Reflection;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123xSystemEventEcallRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void SourceContainsExactlyOneCheckedConstantFoldAndNoRawFallback()
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
        Assert.DoesNotContain("ArchRegId.TryCreate", initialize,
            StringComparison.Ordinal);
        AssertOrdered(initialize,
            "ResourceMask = ResourceBitset.Zero;",
            "if (EventKind == SystemEventKind.Ecall)",
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(",
            "ArchRegId.Create(EcallCodeRegister)",
            "PublishExplicitStructuralSafetyMask();",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void CheckedConstantAndRawHelperHaveExactMaskParity()
    {
        ArchRegId register = ArchRegId.Create(17);
        Assert.Equal(17, register.Value);

        ResourceBitset raw = ResourceMaskBuilder.ForRegisterRead(17);
        ResourceBitset checkedMask =
            ResourceMaskBuilder.ForArchitecturalRegisterRead(register);
        Assert.Equal(raw, checkedMask);
        Assert.Equal(1UL << 4, checkedMask.Low);
        Assert.Equal(0UL, checkedMask.High);
    }

    [Fact]
    public void EveryEventKindByteRetainsExactDependencyAndMaskBehavior()
    {
        ResourceBitset expectedEcall =
            ResourceMaskBuilder.ForRegisterRead(17);

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
            Assert.Equal(ecall ? expectedEcall : ResourceBitset.Zero,
                operation.ResourceMask);

            if (!Enum.IsDefined(typeof(SystemEventKind), (byte)raw))
            {
                YAKSys_Hybrid_CPU.Processor.CPU_Core core = new(0);
                Assert.Null(operation.CreatePipelineEvent(ref core));
            }
        }
    }

    [Fact]
    public void DefaultMismatchFactoryAndMutationSeamsRemainUnchanged()
    {
        var defaultCarrier = new SysEventMicroOp();
        defaultCarrier.InitializeMetadata();
        Assert.Empty(defaultCarrier.ReadRegisters);
        Assert.Equal(ResourceBitset.Zero, defaultCarrier.ResourceMask);

        var mismatch = new SysEventMicroOp
        {
            OpCode = IsaOpcodeValues.FENCE,
            EventKind = SystemEventKind.Ecall,
            OrderGuarantee = SystemEventOrderGuarantee.None
        };
        mismatch.InitializeMetadata();
        Assert.Equal([17], mismatch.ReadRegisters);
        Assert.Equal(ResourceMaskBuilder.ForRegisterRead(17),
            mismatch.ResourceMask);

        int[] reads = Assert.IsType<int[]>(mismatch.ReadRegisters);
        ResourceBitset cached =
            mismatch.AdmissionMetadata.RegisterHazardMask;
        reads[0] = 0;
        Assert.Equal(0, mismatch.AdmissionMetadata.ReadRegisters[0]);
        Assert.Equal(cached,
            mismatch.AdmissionMetadata.RegisterHazardMask);

        PropertyInfo property = typeof(SysEventMicroOp).GetProperty(
            nameof(SysEventMicroOp.EventKind)) ??
            throw new MissingMemberException();
        property.SetValue(mismatch, SystemEventKind.Fence);
        mismatch.InitializeMetadata();
        Assert.Empty(mismatch.ReadRegisters);
        Assert.Equal(ResourceBitset.Zero, mismatch.ResourceMask);

        SysEventMicroOp canonical = SysEventMicroOp.ForEcall();
        Assert.Equal((uint)InstructionsEnum.ECALL, canonical.OpCode);
        Assert.Equal(SystemEventKind.Ecall, canonical.EventKind);
        Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary,
            canonical.OrderGuarantee);
        Assert.Equal([17], canonical.ReadRegisters);
    }

    [Fact]
    public void ExecutionEventAndRetireContoursRemainIndependentOfMaskTyping()
    {
        string root = FindRepositoryRoot();
        string system = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "System", "MicroOp.System.cs");
        string direct = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.System.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains(
            "TryReadUnifiedArchValue(ref core, vtId, EcallCodeRegister, out ulong value)",
            system, StringComparison.Ordinal);
        Assert.Contains("refusing hidden zero-code fallback", system,
            StringComparison.Ordinal);
        Assert.Equal(2, Count(direct,
            "ReadExecutionRegister(state, vtId, 17)"));
        Assert.Contains("EcallCode = 0", retire,
            StringComparison.Ordinal);
        Assert.Contains("EcallCode = ecallEvent.EcallCode", retire,
            StringComparison.Ordinal);

        SysEventMicroOp operation = SysEventMicroOp.ForEcall();
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x12A0, activeVtId: 0);
        core.WriteCommittedArch(0, 17, 0UL);
        Assert.Equal(0L, Assert.IsType<EcallEvent>(
            operation.CreatePipelineEvent(ref core)).EcallCode);
        core.WriteCommittedArch(0, 17, 93UL);
        Assert.Equal(93L, Assert.IsType<EcallEvent>(
            operation.CreatePipelineEvent(ref core)).EcallCode);

        YAKSys_Hybrid_CPU.Processor.CPU_Core unavailable = default;
        Assert.Throws<InvalidOperationException>(
            () => operation.CreatePipelineEvent(ref unavailable));
    }

    [Fact]
    public void CompilerWireFspAndOtherIdentifierFamiliesRemainIsolated()
    {
        string root = FindRepositoryRoot();
        string compiler = ReadTree(root, "HybridCPU_Compiler");
        string system = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "System", "MicroOp.System.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.StageFlow.cs");

        Assert.Contains("case InstructionsEnum.ECALL:", compiler,
            StringComparison.Ordinal);
        Assert.Contains(
            "systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Ecall;",
            compiler, StringComparison.Ordinal);
        Assert.DoesNotContain("new SysEventMicroOp", compiler,
            StringComparison.Ordinal);
        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)",
            system, StringComparison.Ordinal);
        Assert.Contains("|| microOp is Core.SysEventMicroOp", stageFlow,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", ExtractBalanced(system,
            "public sealed class SysEventMicroOp"), StringComparison.Ordinal);
        Assert.DoesNotContain("AcceleratorTokenHandle",
            ExtractBalanced(system, "public sealed class SysEventMicroOp"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("ChannelId", system,
            StringComparison.Ordinal);
        Assert.DoesNotContain("DomainId", system,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TokenId", system,
            StringComparison.Ordinal);
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
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

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
