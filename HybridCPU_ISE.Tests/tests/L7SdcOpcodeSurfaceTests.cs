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
    public void L7SdcOpcodeSurface_CurrentCommandsPublishLane7ControlPlaneMetadata(
        InstructionsEnum opcode,
        ushort _,
        string mnemonic,
        SerializationClass _2,
        Type _3)
    {
        DecoderContext context = L7SdcPhase03TestCases.CreateRegisterResultContext(opcode);
        MicroOp carrier = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            context);
        SystemDeviceCommandMicroOp command =
            Assert.IsAssignableFrom<SystemDeviceCommandMicroOp>(carrier);

        Assert.Contains(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode), StringComparison.Ordinal);
        Assert.Equal(SlotClass.SystemSingleton, command.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, command.Placement.PinningKind);
        Assert.Equal(7, command.Placement.PinnedLaneId);
        Assert.True(command.HasSideEffects);
        Assert.False(command.IsMemoryOp);
        Assert.False(command.IsControlFlow);
        Assert.True(command.WritesRegister);
        Assert.Equal(new[] { 5 }, command.WriteRegisters);
        Assert.False(command.UsedLegacyCustomAcceleratorFallback);
        Assert.False(command.UsedArithmeticExecutionPlane);

        if (command.CommandKind is SystemDeviceCommandKind.Poll
            or SystemDeviceCommandKind.Status
            or SystemDeviceCommandKind.Wait
            or SystemDeviceCommandKind.Cancel
            or SystemDeviceCommandKind.Fence)
        {
            Assert.Equal(new[] { 4 }, command.ReadRegisters);
        }
        else
        {
            Assert.Empty(command.ReadRegisters);
        }

        if (command.CommandKind == SystemDeviceCommandKind.Submit)
        {
            var core = new Processor.CPU_Core(0);
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => command.Execute(ref core));
            Assert.Contains("descriptorless raw factory execution remains fail-closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void L7SdcOpcodeSurface_DedicatedSurfaceDoesNotMutateFrozenMandatoryCore()
    {
        Assert.Equal(111, IsaV4Surface.IsaMandatoryOpcodeCount);
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
        {
            InstructionsEnum.ACCEL_STATUS,
            266,
            "ACCEL_STATUS",
            SerializationClass.CsrOrdered,
            typeof(AcceleratorStatusMicroOp)
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
            (
                InstructionsEnum.ACCEL_STATUS,
                (ushort)266,
                "ACCEL_STATUS",
                SerializationClass.CsrOrdered,
                typeof(AcceleratorStatusMicroOp)
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
        InstructionsEnum.ACCEL_STATUS => IsaOpcodeValues.ACCEL_STATUS,
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

    public static DecoderContext CreateRegisterResultContext(InstructionsEnum opcode)
    {
        DecoderContext context = new()
        {
            OpCode = (uint)opcode,
            Reg1ID = 5,
            Reg2ID = opcode is InstructionsEnum.ACCEL_POLL
                or InstructionsEnum.ACCEL_STATUS
                or InstructionsEnum.ACCEL_WAIT
                or InstructionsEnum.ACCEL_CANCEL
                or InstructionsEnum.ACCEL_FENCE
                    ? (ushort)4
                    : (ushort)0,
            Reg3ID = 0
        };
        return context;
    }
}
