using System;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Accelerators;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Execution;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcLegacyQuarantineTests
{
    [Fact]
    public void L7SdcLegacyQuarantine_CustomAcceleratorMicroOpExecute_RemainsFailClosed()
    {
        var microOp = new CustomAcceleratorMicroOp
        {
            OpCode = 0xC000,
            Accelerator = new MatMulAccelerator(),
            Operands = new ulong[] { 0x1000, 0x2000, 0x3000, 2, 2, 2 },
            Config = Array.Empty<byte>()
        };
        var core = new Processor.CPU_Core(0);

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => microOp.Execute(ref core));

        Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Direct/manual publication", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcLegacyQuarantine_RegisterAccelerator_DoesNotGrantDecodeOrExecutionAuthority()
    {
        InstructionRegistry.Clear();
        InstructionRegistry.Initialize();
        InstructionRegistry.RegisterAccelerator(new MatMulAccelerator());

        Assert.True(InstructionRegistry.IsCustomAcceleratorOpcode(0xC000));
        Assert.False(InstructionRegistry.IsRegistered(0xC000));

        var decoder = new VliwDecoderV4();
        var instruction = new VLIW_Instruction
        {
            OpCode = 0xC000,
            DataTypeValue = DataTypeEnum.INT32,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3)
        };

        void Decode() => decoder.Decode(in instruction, slotIndex: 7);
        InvalidOpcodeException decodeEx = Assert.Throws<InvalidOpcodeException>((Action)Decode);
        Assert.Contains("custom accelerator", decodeEx.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fail closed", decodeEx.Message, StringComparison.OrdinalIgnoreCase);

        InvalidOpcodeException runtimeEx = Assert.Throws<InvalidOpcodeException>(
            () => InstructionRegistry.CreateMicroOp(0xC000, new DecoderContext { OpCode = 0xC000 }));
        Assert.Contains("Direct/manual publication", runtimeEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcLegacyQuarantine_RegisterAccelerator_DoesNotGrantCommitAuthority()
    {
        InstructionRegistry.Clear();
        InstructionRegistry.Initialize();
        InstructionRegistry.RegisterAccelerator(new MatMulAccelerator());

        var registryMethods = typeof(InstructionRegistry).GetMethods()
            .Where(static method =>
                method.Name.Contains("Accelerator", StringComparison.OrdinalIgnoreCase) &&
                (method.Name.Contains("Commit", StringComparison.OrdinalIgnoreCase) ||
                 method.Name.Contains("Submit", StringComparison.OrdinalIgnoreCase) ||
                 method.Name.Contains("Token", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.Empty(registryMethods);
        Assert.False(InstructionRegistry.IsRegistered(0xC000));
    }

    [Fact]
    public void L7SdcLegacyQuarantine_MatMulFixtureExecute_CannotPublishArchitecturalMemory()
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x5A, 16);
        WriteMemory(0x3000, original);

        var matMul = new MatMulAccelerator();
        ulong[] result = matMul.Execute(
            0xC000,
            new ulong[] { 0x1000, 0x2000, 0x3000, 2, 2, 2 },
            Array.Empty<byte>());

        Assert.Equal(new ulong[] { 0x3000 }, result);
        Assert.Equal(original, ReadMemory(0x3000, 16));
    }

    [Fact]
    public void L7SdcLegacyQuarantine_AcceleratorRuntimeFailClosed_RemainsUnsupported()
    {
        NotSupportedException registrationEx = Assert.Throws<NotSupportedException>(
            AcceleratorRuntimeFailClosed.ThrowRegistrationNotSupported);
        Assert.Contains("fail closed", registrationEx.Message, StringComparison.OrdinalIgnoreCase);

        NotSupportedException transferEx = Assert.Throws<NotSupportedException>(
            () => AcceleratorRuntimeFailClosed.ThrowTransferNotSupported());
        Assert.Contains("fail closed", transferEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcNoLegacyExecuteBackend_ProductionExternalAcceleratorCode_DoesNotCallLegacyExecute()
    {
        string root = FindRepositoryRoot();
        string externalAcceleratorPath = Path.Combine(
            root,
            "HybridCPU_ISE",
            "Core",
            "Execution",
            "ExternalAccelerators");

        if (!Directory.Exists(externalAcceleratorPath))
        {
            return;
        }

        string[] offenders = Directory.GetFiles(externalAcceleratorPath, "*.cs", SearchOption.AllDirectories)
            .Where(static path =>
            {
                string text = File.ReadAllText(path);
                return text.Contains("ICustomAccelerator", StringComparison.Ordinal) ||
                       text.Contains("MatMulAccelerator", StringComparison.Ordinal) ||
                       text.Contains("YAKSys_Hybrid_CPU.Core.Accelerators", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static void InitializeMainMemory(ulong bytes)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, bytes);
        Processor.Memory = null;
    }

    private static byte[] Fill(byte value, int count)
    {
        byte[] bytes = new byte[count];
        Array.Fill(bytes, value);
        return bytes;
    }

    private static void WriteMemory(ulong address, byte[] bytes) =>
        Assert.True(Processor.MainMemory.TryWritePhysicalRange(address, bytes));

    private static byte[] ReadMemory(ulong address, int length)
    {
        byte[] bytes = new byte[length];
        Assert.True(Processor.MainMemory.TryReadPhysicalRange(address, bytes));
        return bytes;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string projectPath = Path.Combine(
                directory.FullName,
                "HybridCPU_ISE",
                "HybridCPU_ISE.csproj");
            if (File.Exists(projectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
