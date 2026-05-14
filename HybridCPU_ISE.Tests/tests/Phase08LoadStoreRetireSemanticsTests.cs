using System;
using System.Collections.Generic;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests
{
    public sealed class Phase08LoadStoreRetireSemanticsTests
    {
        public static IEnumerable<object[]> LoadCases()
        {
            yield return new object[] { InstructionsEnum.LB, (byte)1, 0x80UL, 0xFFFF_FFFF_FFFF_FF80UL };
            yield return new object[] { InstructionsEnum.LH, (byte)2, 0x8001UL, 0xFFFF_FFFF_FFFF_8001UL };
            yield return new object[] { InstructionsEnum.LW, (byte)4, 0x8000_0001UL, 0xFFFF_FFFF_8000_0001UL };
            yield return new object[] { InstructionsEnum.LBU, (byte)1, 0xFFUL, 0xFFUL };
            yield return new object[] { InstructionsEnum.LHU, (byte)2, 0xFFFFUL, 0xFFFFUL };
            yield return new object[] { InstructionsEnum.LWU, (byte)4, 0xFFFF_FFFFUL, 0xFFFF_FFFFUL };
            yield return new object[] { InstructionsEnum.LD, (byte)8, 0x8877_6655_4433_2211UL, 0x8877_6655_4433_2211UL };
        }

        public static IEnumerable<object[]> StoreCases()
        {
            const ulong value = 0x1122_3344_5566_7788UL;
            yield return new object[] { InstructionsEnum.SB, (byte)1, value, new byte[] { 0x88, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC } };
            yield return new object[] { InstructionsEnum.SH, (byte)2, value, new byte[] { 0x88, 0x77, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC } };
            yield return new object[] { InstructionsEnum.SW, (byte)4, value, new byte[] { 0x88, 0x77, 0x66, 0x55, 0xCC, 0xCC, 0xCC, 0xCC } };
            yield return new object[] { InstructionsEnum.SD, (byte)8, value, new byte[] { 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11 } };
        }

        [Theory]
        [MemberData(nameof(LoadCases))]
        public void LoadMicroOp_DecodesPublishedSignAndZeroExtension(
            InstructionsEnum opcode,
            byte accessSize,
            ulong memoryImage,
            ulong expectedValue)
        {
            const ulong address = 0x1_1000UL;
            const ushort destinationRegister = 7;

            InitializeMemory();
            WriteBytes(address, CreateStoreImage(memoryImage, accessSize));

            var core = new Processor.CPU_Core(0);
            var load = new LoadMicroOp
            {
                OpCode = (uint)opcode,
                Address = address,
                Size = accessSize,
                BaseRegID = 1,
                DestRegID = destinationRegister,
                WritesRegister = true
            };
            load.InitializeMetadata();

            Assert.False(load.Execute(ref core));
            Processor.Memory!.AdvanceCycles(1);
            Assert.True(load.Execute(ref core));

            Assert.True(load.TryGetPrimaryWriteBackResult(out ulong loadedValue));
            Assert.Equal(expectedValue, loadedValue);
            Assert.Equal((address, (ulong)accessSize), Assert.Single(load.ReadMemoryRanges));
            Assert.Equal((address, (ulong)accessSize), Assert.Single(load.AdmissionMetadata.ReadMemoryRanges));
            Assert.Empty(load.WriteMemoryRanges);
        }

        [Theory]
        [MemberData(nameof(LoadCases))]
        public void ExplicitPacketLoadRetire_DecodesPublishedSignAndZeroExtension(
            InstructionsEnum opcode,
            byte accessSize,
            ulong memoryImage,
            ulong expectedValue)
        {
            const int vtId = 1;
            const ulong pc = 0xA810UL;
            const ulong address = 0x1_2000UL;
            const ushort destinationRegister = 9;
            const ulong originalDestinationValue = 0xCAFE_BABE_DEAD_BEEFUL;

            InitializeMemory();
            WriteBytes(address, CreateStoreImage(memoryImage, accessSize));

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, pc);
            core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

            core.TestPrepareExplicitPacketLoadForWriteBack(
                laneIndex: 4,
                pc,
                address,
                destinationRegister,
                accessSize,
                vtId,
                opcode);

            Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

            core.TestRunWriteBackStage();

            Assert.Equal(expectedValue, core.ReadArch(vtId, destinationRegister));
        }

        [Theory]
        [MemberData(nameof(LoadCases))]
        public void SingleLaneMemoryStage_LoadDecodeUsesOpcodeAndAccessSize(
            InstructionsEnum opcode,
            byte accessSize,
            ulong memoryImage,
            ulong expectedValue)
        {
            const ulong pc = 0xA820UL;
            const ulong address = 0x1_3000UL;
            const ushort destinationRegister = 11;

            InitializeMemory();
            WriteBytes(address, CreateStoreImage(memoryImage, accessSize));

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.TestSeedSingleLaneExecuteForMemoryFollowThrough(
                isMemoryOp: true,
                isLoad: true,
                writesRegister: true,
                destRegId: destinationRegister,
                memoryAddress: address,
                memoryAccessSize: accessSize,
                pc: pc,
                opCode: (uint)opcode);

            core.TestRunMemoryStageFromCurrentExecuteState();

            var memoryStage = core.GetMemoryStage();
            Assert.True(memoryStage.Valid);
            Assert.True(memoryStage.ResultReady);
            Assert.Equal(expectedValue, memoryStage.ResultValue);
        }

        [Theory]
        [MemberData(nameof(StoreCases))]
        public void ExplicitPacketStoreRetire_DefersPhysicalWriteAndTruncatesByPublishedSize(
            InstructionsEnum opcode,
            byte accessSize,
            ulong storeValue,
            byte[] expectedMemoryAfterRetire)
        {
            const int vtId = 2;
            const ulong pc = 0xA830UL;
            const ulong address = 0x1_4000UL;
            byte[] baseline = { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC };

            InitializeMemory();
            WriteBytes(address, baseline);

            var core = new Processor.CPU_Core(0);
            core.TestPrepareExplicitPacketStoreForWriteBack(
                laneIndex: 5,
                pc,
                address,
                storeValue,
                accessSize,
                vtId,
                opcode);

            Assert.Equal(baseline, ReadBytes(address, baseline.Length));

            core.TestRunWriteBackStage();

            Assert.Equal(expectedMemoryAfterRetire, ReadBytes(address, expectedMemoryAfterRetire.Length));
        }

        [Theory]
        [InlineData(InstructionsEnum.LB, 1)]
        [InlineData(InstructionsEnum.LBU, 1)]
        [InlineData(InstructionsEnum.LH, 2)]
        [InlineData(InstructionsEnum.LHU, 2)]
        [InlineData(InstructionsEnum.LW, 4)]
        [InlineData(InstructionsEnum.LWU, 4)]
        [InlineData(InstructionsEnum.LD, 8)]
        public void TypedLoadFactory_PublishesExactMemoryFootprint(
            InstructionsEnum opcode,
            byte accessSize)
        {
            const ulong address = 0x1_5000UL;
            var context = new DecoderContext
            {
                OpCode = (uint)opcode,
                Reg1ID = 12,
                Reg2ID = 4,
                Reg3ID = VLIW_Instruction.NoReg,
                MemoryAddress = address,
                HasMemoryAddress = true
            };

            LoadMicroOp load = Assert.IsType<LoadMicroOp>(
                InstructionRegistry.CreateMicroOp((uint)opcode, context));

            Assert.Equal((uint)opcode, load.OpCode);
            Assert.Equal(accessSize, load.Size);
            Assert.True(load.WritesRegister);
            Assert.Equal((address, (ulong)accessSize), Assert.Single(load.ReadMemoryRanges));
            Assert.Equal((address, (ulong)accessSize), Assert.Single(load.AdmissionMetadata.ReadMemoryRanges));
            Assert.Empty(load.WriteMemoryRanges);
            Assert.Empty(load.AdmissionMetadata.WriteMemoryRanges);
        }

        [Theory]
        [InlineData(InstructionsEnum.SB, 1)]
        [InlineData(InstructionsEnum.SH, 2)]
        [InlineData(InstructionsEnum.SW, 4)]
        [InlineData(InstructionsEnum.SD, 8)]
        public void TypedStoreFactory_PublishesExactMemoryFootprint(
            InstructionsEnum opcode,
            byte accessSize)
        {
            const ulong address = 0x1_6000UL;
            var context = new DecoderContext
            {
                OpCode = (uint)opcode,
                Reg1ID = VLIW_Instruction.NoReg,
                Reg2ID = 4,
                Reg3ID = 13,
                MemoryAddress = address,
                HasMemoryAddress = true
            };

            StoreMicroOp store = Assert.IsType<StoreMicroOp>(
                InstructionRegistry.CreateMicroOp((uint)opcode, context));

            Assert.Equal((uint)opcode, store.OpCode);
            Assert.Equal(accessSize, store.Size);
            Assert.False(store.WritesRegister);
            Assert.Empty(store.ReadMemoryRanges);
            Assert.Empty(store.AdmissionMetadata.ReadMemoryRanges);
            Assert.Equal((address, (ulong)accessSize), Assert.Single(store.WriteMemoryRanges));
            Assert.Equal((address, (ulong)accessSize), Assert.Single(store.AdmissionMetadata.WriteMemoryRanges));
        }

        private static void InitializeMemory()
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x100000000UL,
                permissions: IOMMUAccessPermissions.ReadWrite);

            Processor proc = default;
            Processor.Memory = new MemorySubsystem(ref proc);
        }

        private static byte[] CreateStoreImage(ulong value, byte accessSize)
        {
            return accessSize switch
            {
                1 => new[] { (byte)value },
                2 => BitConverter.GetBytes((ushort)value),
                4 => BitConverter.GetBytes((uint)value),
                8 => BitConverter.GetBytes(value),
                _ => throw new ArgumentOutOfRangeException(nameof(accessSize), accessSize, "Unsupported test access size.")
            };
        }

        private static byte[] ReadBytes(ulong address, int length)
        {
            return Processor.MainMemory.ReadFromPosition(new byte[length], address, (ulong)length);
        }

        private static void WriteBytes(ulong address, byte[] bytes)
        {
            Processor.MainMemory.WriteToPosition(bytes, address);
        }
    }
}
