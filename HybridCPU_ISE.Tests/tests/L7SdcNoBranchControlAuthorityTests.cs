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
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcNoBranchControlAuthorityTests
{
    [Theory]
    [MemberData(nameof(L7SdcPhase03TestCases.AllOpcodes), MemberType = typeof(L7SdcPhase03TestCases))]
    public void L7SdcNoBranchControlAuthority_CarriersDoNotUseBranchOrLane6Authority(
        InstructionsEnum opcode,
        ushort _,
        string mnemonic,
        SerializationClass expectedSerialization,
        Type expectedCarrierType)
    {
        Assert.Equal(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode));
        MicroOp carrier = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            new DecoderContext { OpCode = (uint)opcode });

        Assert.Equal(expectedCarrierType, carrier.GetType());
        Assert.Equal(InstructionClass.System, carrier.InstructionClass);
        Assert.Equal(expectedSerialization, carrier.SerializationClass);
        Assert.False(carrier.IsControlFlow);
        Assert.False(carrier.IsMemoryOp);
        Assert.Equal(SlotClass.SystemSingleton, carrier.Placement.RequiredSlotClass);
        Assert.NotEqual(SlotClass.BranchControl, carrier.Placement.RequiredSlotClass);
        Assert.NotEqual(SlotClass.DmaStreamClass, carrier.Placement.RequiredSlotClass);
        Assert.Equal((byte)7, carrier.Placement.PinnedLaneId);
    }

    [Theory]
    [MemberData(nameof(L7SdcPhase03TestCases.AllOpcodes), MemberType = typeof(L7SdcPhase03TestCases))]
    public void L7SdcNoBranchControlAuthority_DirectExecuteThrowsFailClosedWithoutArchitecturalEffects(
        InstructionsEnum opcode,
        ushort _,
        string mnemonic,
        SerializationClass expectedSerialization,
        Type expectedCarrierType)
    {
        Assert.Equal(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode));
        MicroOp carrier = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            new DecoderContext { OpCode = (uint)opcode });

        Assert.Equal(expectedCarrierType, carrier.GetType());
        Assert.Equal(expectedSerialization, carrier.SerializationClass);
        Assert.Empty(carrier.WriteMemoryRanges);
        Assert.Empty(carrier.ReadMemoryRanges);
        Assert.False(carrier.TryGetPrimaryWriteBackResult(out ulong writeBackValue));
        Assert.Equal(0UL, writeBackValue);
        Assert.Equal(typeof(MicroOp), carrier.GetType().GetMethod(nameof(MicroOp.Commit))!.DeclaringType);

        Processor.MainMemoryArea previousMainMemory = Processor.MainMemory;
        var previousMemorySubsystem = Processor.Memory;
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x1000);
        Processor.Memory = null;
        byte[] original = new byte[] { 0x5A, 0xC3, 0x7E, 0x11 };
        try
        {
            Assert.True(Processor.MainMemory.TryWritePhysicalRange(0x100, original));
            var core = new Processor.CPU_Core(0);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => carrier.Execute(ref core));

            Assert.Contains("unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("backend execution", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("token lifecycle", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("staged writes", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("commit", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fallback", ex.Message, StringComparison.OrdinalIgnoreCase);

            byte[] observed = new byte[original.Length];
            Assert.True(Processor.MainMemory.TryReadPhysicalRange(0x100, observed));
            Assert.Equal(original, observed);
        }
        finally
        {
            Processor.MainMemory = previousMainMemory;
            Processor.Memory = previousMemorySubsystem;
        }
    }

    [Fact]
    public void L7SdcNoBranchControlAuthority_CustomAcceleratorRegistryOpcodesAreNotCarrierAuthority()
    {
        InstructionRegistry.Clear();
        InstructionRegistry.Initialize();

        try
        {
            InstructionRegistry.RegisterAccelerator(new MatMulAccelerator());

            Assert.True(InstructionRegistry.IsCustomAcceleratorOpcode(0xC000));
            Assert.False(InstructionRegistry.IsRegistered(0xC000));
            Assert.False(OpcodeRegistry.IsSystemDeviceCommandOpcode(0xC000));

            var decoder = new VliwDecoderV4();
            VLIW_Instruction instruction = L7SdcPhase03TestCases.CreateNativeInstruction(
                InstructionsEnum.ACCEL_SUBMIT);
            instruction.OpCode = 0xC000;

            InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in instruction, slotIndex: 7));

            Assert.Contains("custom accelerator", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("L7-SDC", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(AcceleratorSubmitMicroOp), ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            InstructionRegistry.Clear();
            InstructionRegistry.Initialize();
        }
    }

    [Fact]
    public void L7SdcNoBranchControlAuthority_BranchSystemAliasRemainsLane7ButAuthorityDistinct()
    {
        Assert.Equal((byte)0b_1000_0000, SlotClassLaneMap.GetLaneMask(SlotClass.BranchControl));
        Assert.Equal((byte)0b_1000_0000, SlotClassLaneMap.GetLaneMask(SlotClass.SystemSingleton));
        Assert.True(SlotClassLaneMap.HasAliasedLanes(SlotClass.BranchControl));
        Assert.True(SlotClassLaneMap.HasAliasedLanes(SlotClass.SystemSingleton));
        Assert.Equal(new[] { SlotClass.SystemSingleton }, SlotClassLaneMap.GetAliasedClasses(SlotClass.BranchControl).ToArray());
        Assert.Equal(new[] { SlotClass.BranchControl }, SlotClassLaneMap.GetAliasedClasses(SlotClass.SystemSingleton).ToArray());

        MicroOp submit = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.ACCEL_SUBMIT,
            new DecoderContext { OpCode = (uint)InstructionsEnum.ACCEL_SUBMIT });

        Assert.Equal(SlotClass.SystemSingleton, submit.Placement.RequiredSlotClass);
        Assert.NotEqual(SlotClass.BranchControl, submit.Placement.RequiredSlotClass);
    }

    [Fact]
    public void L7SdcNoBranchControlAuthority_SurfaceDoesNotReferenceLegacyOrFallbackExecutionPaths()
    {
        string source = File.ReadAllText(FindRepoPath(
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "MicroOps",
            "SystemDeviceCommandMicroOp.cs"));

        Assert.DoesNotContain("ICustomAccelerator", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MatMulAccelerator", source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(DmaStreamComputeMicroOp), source, StringComparison.Ordinal);
        Assert.DoesNotContain("StreamEngine", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VectorALU", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new GenericMicroOp", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StageDestinationWrite", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AcceleratorToken", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IExternalAcceleratorBackend", source, StringComparison.Ordinal);
    }

    private static string FindRepoPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate repository file '{Path.Combine(segments)}' from test base directory.");
    }
}
