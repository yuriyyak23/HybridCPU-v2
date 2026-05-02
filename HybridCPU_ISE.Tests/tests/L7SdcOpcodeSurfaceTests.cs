using System;
using System.Collections.Generic;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcOpcodeSurfaceTests
{
    [Theory]
    [MemberData(nameof(L7SdcPhase03TestCases.AllOpcodes), MemberType = typeof(L7SdcPhase03TestCases))]
    public void L7SdcOpcodeSurface_NativeEnumAndIsaOpcodeValues_ArePublished(
        InstructionsEnum opcode,
        ushort expectedValue,
        string mnemonic,
        SerializationClass expectedSerialization,
        Type _)
    {
        Assert.True(Enum.IsDefined(typeof(InstructionsEnum), opcode));
        Assert.Equal(expectedValue, (ushort)opcode);
        Assert.Equal(expectedValue, L7SdcPhase03TestCases.GetIsaOpcodeValue(opcode));

        OpcodeInfo? maybeInfo = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.True(maybeInfo.HasValue);
        OpcodeInfo info = maybeInfo.Value;
        Assert.Equal(mnemonic, info.Mnemonic);
        Assert.Equal(InstructionClass.System, info.InstructionClass);
        Assert.Equal(expectedSerialization, info.SerializationClass);

        Assert.True(OpcodeRegistry.IsSystemDeviceCommandOpcode((uint)opcode));
        Assert.Contains(mnemonic, IsaV4Surface.SystemDeviceCommandOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
    }

    [Theory]
    [MemberData(nameof(L7SdcPhase03TestCases.AllOpcodes), MemberType = typeof(L7SdcPhase03TestCases))]
    public void L7SdcOpcodeSurface_ClassifiesAsSystemWithConservativeSerialization(
        InstructionsEnum opcode,
        ushort _,
        string mnemonic,
        SerializationClass expectedSerialization,
        Type expectedCarrierType)
    {
        Assert.Equal(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode));
        Assert.NotNull(expectedCarrierType);
        Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(opcode));
        Assert.Equal(expectedSerialization, InstructionClassifier.GetSerializationClass(opcode));
        Assert.Equal(
            (InstructionClass.System, expectedSerialization),
            InstructionClassifier.Classify(opcode));

        Assert.True(OpcodeRegistry.TryGetPublishedSemantics(
            opcode,
            out InstructionClass publishedClass,
            out SerializationClass publishedSerialization));
        Assert.Equal(InstructionClass.System, publishedClass);
        Assert.Equal(expectedSerialization, publishedSerialization);
    }

    [Theory]
    [MemberData(nameof(L7SdcPhase03TestCases.AllOpcodes), MemberType = typeof(L7SdcPhase03TestCases))]
    public void L7SdcOpcodeSurface_DirectFactoryCreatesExactCarrier_NotGeneric(
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
        Assert.IsAssignableFrom<SystemDeviceCommandMicroOp>(carrier);
        Assert.IsNotType<GenericMicroOp>(carrier);
        Assert.IsNotType<TrapMicroOp>(carrier);
        Assert.Equal(InstructionClass.System, carrier.InstructionClass);
        Assert.Equal(expectedSerialization, carrier.SerializationClass);
    }

    [Theory]
    [MemberData(nameof(L7SdcPhase03TestCases.AllOpcodes), MemberType = typeof(L7SdcPhase03TestCases))]
    public void L7SdcOpcodeSurface_AllCarriersRemainNoWriteAndFailClosedOnExecute(
        InstructionsEnum opcode,
        ushort _,
        string mnemonic,
        SerializationClass _2,
        Type _3)
    {
        MicroOp carrier = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            new DecoderContext { OpCode = (uint)opcode });
        var core = new Processor.CPU_Core(0);

        Assert.False(carrier.WritesRegister);
        Assert.Empty(carrier.WriteRegisters);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => carrier.Execute(ref core));

        Assert.Contains(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode), StringComparison.Ordinal);
        Assert.Contains("lane7", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runtime-side APIs", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("backend execution", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("staged write publication", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("architectural rd writeback", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fallback routing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcOpcodeSurface_DedicatedSurfaceDoesNotMutateFrozenMandatoryCore()
    {
        Assert.Equal(97, IsaV4Surface.IsaMandatoryOpcodeCount);
        Assert.Equal(IsaV4Surface.IsaMandatoryOpcodeCount, IsaV4Surface.MandatoryCoreOpcodes.Count);

        foreach ((InstructionsEnum _, ushort _, string mnemonic, SerializationClass _, Type _) in
                 L7SdcPhase03TestCases.Cases)
        {
            Assert.Contains(mnemonic, IsaV4Surface.SystemDeviceCommandOpcodes);
            Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
            Assert.Equal("SYS_SERIAL", IsaV4Surface.PipelineClassMap[mnemonic]);
        }
    }
}

public static class L7SdcPhase03TestCases
{
    public static TheoryData<InstructionsEnum, ushort, string, SerializationClass, Type> AllOpcodes => new()
    {
        {
            InstructionsEnum.ACCEL_QUERY_CAPS,
            260,
            "ACCEL_QUERY_CAPS",
            SerializationClass.CsrOrdered,
            typeof(AcceleratorQueryCapsMicroOp)
        },
        {
            InstructionsEnum.ACCEL_SUBMIT,
            261,
            "ACCEL_SUBMIT",
            SerializationClass.MemoryOrdered,
            typeof(AcceleratorSubmitMicroOp)
        },
        {
            InstructionsEnum.ACCEL_POLL,
            262,
            "ACCEL_POLL",
            SerializationClass.CsrOrdered,
            typeof(AcceleratorPollMicroOp)
        },
        {
            InstructionsEnum.ACCEL_WAIT,
            263,
            "ACCEL_WAIT",
            SerializationClass.FullSerial,
            typeof(AcceleratorWaitMicroOp)
        },
        {
            InstructionsEnum.ACCEL_CANCEL,
            264,
            "ACCEL_CANCEL",
            SerializationClass.FullSerial,
            typeof(AcceleratorCancelMicroOp)
        },
        {
            InstructionsEnum.ACCEL_FENCE,
            265,
            "ACCEL_FENCE",
            SerializationClass.FullSerial,
            typeof(AcceleratorFenceMicroOp)
        },
    };

    public static IReadOnlyList<(InstructionsEnum Opcode, ushort Value, string Mnemonic, SerializationClass Serialization, Type CarrierType)> Cases =>
        new[]
        {
            (
                InstructionsEnum.ACCEL_QUERY_CAPS,
                (ushort)260,
                "ACCEL_QUERY_CAPS",
                SerializationClass.CsrOrdered,
                typeof(AcceleratorQueryCapsMicroOp)
            ),
            (
                InstructionsEnum.ACCEL_SUBMIT,
                (ushort)261,
                "ACCEL_SUBMIT",
                SerializationClass.MemoryOrdered,
                typeof(AcceleratorSubmitMicroOp)
            ),
            (
                InstructionsEnum.ACCEL_POLL,
                (ushort)262,
                "ACCEL_POLL",
                SerializationClass.CsrOrdered,
                typeof(AcceleratorPollMicroOp)
            ),
            (
                InstructionsEnum.ACCEL_WAIT,
                (ushort)263,
                "ACCEL_WAIT",
                SerializationClass.FullSerial,
                typeof(AcceleratorWaitMicroOp)
            ),
            (
                InstructionsEnum.ACCEL_CANCEL,
                (ushort)264,
                "ACCEL_CANCEL",
                SerializationClass.FullSerial,
                typeof(AcceleratorCancelMicroOp)
            ),
            (
                InstructionsEnum.ACCEL_FENCE,
                (ushort)265,
                "ACCEL_FENCE",
                SerializationClass.FullSerial,
                typeof(AcceleratorFenceMicroOp)
            ),
        };

    public static ushort GetIsaOpcodeValue(InstructionsEnum opcode) => opcode switch
    {
        InstructionsEnum.ACCEL_QUERY_CAPS => IsaOpcodeValues.ACCEL_QUERY_CAPS,
        InstructionsEnum.ACCEL_SUBMIT => IsaOpcodeValues.ACCEL_SUBMIT,
        InstructionsEnum.ACCEL_POLL => IsaOpcodeValues.ACCEL_POLL,
        InstructionsEnum.ACCEL_WAIT => IsaOpcodeValues.ACCEL_WAIT,
        InstructionsEnum.ACCEL_CANCEL => IsaOpcodeValues.ACCEL_CANCEL,
        InstructionsEnum.ACCEL_FENCE => IsaOpcodeValues.ACCEL_FENCE,
        _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Not an L7-SDC opcode.")
    };

    public static VLIW_Instruction CreateNativeInstruction(InstructionsEnum opcode)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0,
            Word1 = VLIW_Instruction.PackArchRegs(
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = 0,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }
}
