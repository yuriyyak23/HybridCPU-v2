using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests
{
    public sealed class Phase08AtomicRetireSemanticsTests
    {
        private static void InitializeCpuMainMemoryIdentityMap()
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
        }

        private static void InitializeMemorySubsystem()
        {
            Processor proc = default;
            Processor.Memory = new MemorySubsystem(ref proc);
        }

        private static void InitializeMemory()
        {
            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
        }

        private static InstructionIR CreateInstructionIr(
            InstructionsEnum opcode,
            byte rd = 0,
            byte rs1 = 0,
            byte rs2 = 0)
        {
            return new InstructionIR
            {
                CanonicalOpcode = opcode,
                Class = InstructionClassifier.GetClass(opcode),
                SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
                Rd = rd,
                Rs1 = rs1,
                Rs2 = rs2,
                Imm = 0
            };
        }

        private static RetireWindowCaptureSnapshot CaptureAtomicRetireWindowPublication(
            ExecutionDispatcherV4 dispatcher,
            InstructionIR instruction,
            ICanonicalCpuState state,
            byte vtId)
        {
            return RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 0xA08,
                vtId);
        }

        private static RetireWindowCaptureSnapshot CaptureAndApplyAtomicRetireWindowPublication(
            ref Processor.CPU_Core core,
            ExecutionDispatcherV4 dispatcher,
            InstructionIR instruction,
            ICanonicalCpuState state,
            byte vtId)
        {
            return RetireWindowCaptureTestHelper.CaptureAndApplyExecutionDispatcherRetireWindowPublications(
                ref core,
                dispatcher,
                instruction,
                state,
                bundleSerial: 0xA09,
                vtId);
        }

        private static uint ReadWord(ulong address) =>
            BitConverter.ToUInt32(Processor.MainMemory.ReadFromPosition(new byte[4], address, 4), 0);

        private static ulong ReadDoubleword(ulong address) =>
            BitConverter.ToUInt64(Processor.MainMemory.ReadFromPosition(new byte[8], address, 8), 0);

        private static ulong SignExtendWordToUlong(uint value) =>
            unchecked((ulong)(long)(int)value);

        private static uint ComputeExpectedWordAtomicWrite(
            InstructionsEnum opcode,
            uint previousValue,
            uint sourceValue)
        {
            return opcode switch
            {
                InstructionsEnum.AMOSWAP_W => sourceValue,
                InstructionsEnum.AMOADD_W => unchecked(previousValue + sourceValue),
                InstructionsEnum.AMOXOR_W => previousValue ^ sourceValue,
                InstructionsEnum.AMOAND_W => previousValue & sourceValue,
                InstructionsEnum.AMOOR_W => previousValue | sourceValue,
                InstructionsEnum.AMOMIN_W => unchecked((uint)Math.Min((int)previousValue, (int)sourceValue)),
                InstructionsEnum.AMOMAX_W => unchecked((uint)Math.Max((int)previousValue, (int)sourceValue)),
                InstructionsEnum.AMOMINU_W => Math.Min(previousValue, sourceValue),
                InstructionsEnum.AMOMAXU_W => Math.Max(previousValue, sourceValue),
                _ => throw new InvalidOperationException($"Unexpected word atomic opcode {opcode}.")
            };
        }

        private static ulong ComputeExpectedDwordAtomicWrite(
            InstructionsEnum opcode,
            ulong previousValue,
            ulong sourceValue)
        {
            return opcode switch
            {
                InstructionsEnum.AMOSWAP_D => sourceValue,
                InstructionsEnum.AMOADD_D => unchecked(previousValue + sourceValue),
                InstructionsEnum.AMOXOR_D => previousValue ^ sourceValue,
                InstructionsEnum.AMOAND_D => previousValue & sourceValue,
                InstructionsEnum.AMOOR_D => previousValue | sourceValue,
                InstructionsEnum.AMOMIN_D => unchecked((ulong)Math.Min((long)previousValue, (long)sourceValue)),
                InstructionsEnum.AMOMAX_D => unchecked((ulong)Math.Max((long)previousValue, (long)sourceValue)),
                InstructionsEnum.AMOMINU_D => Math.Min(previousValue, sourceValue),
                InstructionsEnum.AMOMAXU_D => Math.Max(previousValue, sourceValue),
                _ => throw new InvalidOperationException($"Unexpected doubleword atomic opcode {opcode}.")
            };
        }

        [Theory]
        [InlineData(InstructionsEnum.LR_W, InstructionsEnum.SC_W, 4)]
        [InlineData(InstructionsEnum.LR_D, InstructionsEnum.SC_D, 8)]
        public void LrScRetireApply_SucceedsOnlyForMatchingReservation(
            InstructionsEnum lrOpcode,
            InstructionsEnum scOpcode,
            byte accessSize)
        {
            const byte vtId = 0;
            const ulong address = 0x1_0000;
            const ulong initialValue = 0xFFFF_FFFF_8765_4321UL;
            const ulong storeValue = 0xCAFE_BABE_1020_3040UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, storeValue);
            Processor.MainMemory.WriteToPosition(
                accessSize == 4
                    ? BitConverter.GetBytes(unchecked((uint)initialValue))
                    : BitConverter.GetBytes(initialValue),
                address);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(lrOpcode, rd: 5, rs1: 1),
                state,
                vtId);
            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(scOpcode, rd: 6, rs1: 1, rs2: 2),
                state,
                vtId);

            ulong expectedLoadValue = accessSize == 4
                ? SignExtendWordToUlong(unchecked((uint)initialValue))
                : initialValue;
            Assert.Equal(expectedLoadValue, core.ReadArch(vtId, 5));
            Assert.Equal(0UL, core.ReadArch(vtId, 6));

            if (accessSize == 4)
            {
                Assert.Equal(unchecked((uint)storeValue), ReadWord(address));
            }
            else
            {
                Assert.Equal(storeValue, ReadDoubleword(address));
            }
        }

        [Fact]
        public void LrScReservation_IsScopedByCoreAndVirtualThread()
        {
            const byte reservingVt = 0;
            const byte otherVt = 1;
            const ulong address = 0x1_0100;
            const uint initialWord = 0x7FFF_FFF0U;
            const uint storeValue = 0x2222_3333U;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(reservingVt, 1, address);
            core.WriteCommittedArch(reservingVt, 2, storeValue);
            core.WriteCommittedArch(otherVt, 1, address);
            core.WriteCommittedArch(otherVt, 2, storeValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            var dispatcher = new ExecutionDispatcherV4();
            var reservingState = core.CreateLiveCpuStateAdapter(reservingVt);
            var otherState = core.CreateLiveCpuStateAdapter(otherVt);

            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.LR_W, rd: 5, rs1: 1),
                reservingState,
                reservingVt);
            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.SC_W, rd: 6, rs1: 1, rs2: 2),
                otherState,
                otherVt);

            Assert.Equal(1UL, core.ReadArch(otherVt, 6));
            Assert.Equal(initialWord, ReadWord(address));

            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.SC_W, rd: 7, rs1: 1, rs2: 2),
                reservingState,
                reservingVt);

            Assert.Equal(0UL, core.ReadArch(reservingVt, 7));
            Assert.Equal(storeValue, ReadWord(address));
        }

        [Fact]
        public void LrScReservation_IsInvalidatedByOverlappingPhysicalWrite()
        {
            const byte vtId = 0;
            const ulong address = 0x1_0200;
            const ulong initialValue = 0x1020_3040_5060_7080UL;
            const uint interveningValue = 0xA0B0_C0D0U;
            const ulong storeValue = 0xFFFF_0000_1111_2222UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, storeValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialValue), address);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.LR_D, rd: 5, rs1: 1),
                state,
                vtId);

            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(interveningValue), address + 4);

            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.SC_D, rd: 6, rs1: 1, rs2: 2),
                state,
                vtId);

            Assert.Equal(1UL, core.ReadArch(vtId, 6));
            Assert.Equal(initialValue & 0x0000_0000_FFFF_FFFFUL, ReadDoubleword(address) & 0x0000_0000_FFFF_FFFFUL);
            Assert.Equal(interveningValue, (uint)(ReadDoubleword(address) >> 32));
        }

        [Fact]
        public void FailedStoreConditional_ConsumesMatchingReservation()
        {
            const byte vtId = 0;
            const ulong reservedAddress = 0x1_0300;
            const ulong wrongAddress = 0x1_0320;
            const uint initialWord = 0x1122_3344U;
            const uint storeValue = 0x5566_7788U;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, reservedAddress);
            core.WriteCommittedArch(vtId, 2, storeValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), reservedAddress);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), wrongAddress);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.LR_W, rd: 5, rs1: 1),
                state,
                vtId);

            core.WriteCommittedArch(vtId, 1, wrongAddress);
            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.SC_W, rd: 6, rs1: 1, rs2: 2),
                state,
                vtId);

            core.WriteCommittedArch(vtId, 1, reservedAddress);
            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.SC_W, rd: 7, rs1: 1, rs2: 2),
                state,
                vtId);

            Assert.Equal(1UL, core.ReadArch(vtId, 6));
            Assert.Equal(1UL, core.ReadArch(vtId, 7));
            Assert.Equal(initialWord, ReadWord(reservedAddress));
            Assert.Equal(initialWord, ReadWord(wrongAddress));
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOSWAP_W)]
        [InlineData(InstructionsEnum.AMOADD_W)]
        [InlineData(InstructionsEnum.AMOXOR_W)]
        [InlineData(InstructionsEnum.AMOAND_W)]
        [InlineData(InstructionsEnum.AMOOR_W)]
        [InlineData(InstructionsEnum.AMOMIN_W)]
        [InlineData(InstructionsEnum.AMOMAX_W)]
        [InlineData(InstructionsEnum.AMOMINU_W)]
        [InlineData(InstructionsEnum.AMOMAXU_W)]
        public void AmoWordRetireApply_PublishesSignExtendedOldValueAndRmwWrite(
            InstructionsEnum opcode)
        {
            const byte vtId = 0;
            const ulong address = 0x1_0400;
            const uint initialWord = 0xFFFF_FFF0U;
            const uint sourceWord = 0x0000_0010U;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, sourceWord);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            RetireWindowCaptureSnapshot transaction =
                CaptureAndApplyAtomicRetireWindowPublication(
                    ref core,
                    dispatcher,
                    CreateInstructionIr(opcode, rd: 9, rs1: 1, rs2: 2),
                    state,
                    vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Atomic, transaction.TypedEffectKind);
            Assert.Equal((byte)4, transaction.AtomicEffect.AccessSize);
            Assert.Equal(SignExtendWordToUlong(initialWord), core.ReadArch(vtId, 9));
            Assert.Equal(ComputeExpectedWordAtomicWrite(opcode, initialWord, sourceWord), ReadWord(address));
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOSWAP_D)]
        [InlineData(InstructionsEnum.AMOADD_D)]
        [InlineData(InstructionsEnum.AMOXOR_D)]
        [InlineData(InstructionsEnum.AMOAND_D)]
        [InlineData(InstructionsEnum.AMOOR_D)]
        [InlineData(InstructionsEnum.AMOMIN_D)]
        [InlineData(InstructionsEnum.AMOMAX_D)]
        [InlineData(InstructionsEnum.AMOMINU_D)]
        [InlineData(InstructionsEnum.AMOMAXU_D)]
        public void AmoDoublewordRetireApply_PublishesOldValueAndRmwWrite(
            InstructionsEnum opcode)
        {
            const byte vtId = 0;
            const ulong address = 0x1_0500;
            const ulong initialValue = 0xFFFF_FFFF_FFFF_FFF0UL;
            const ulong sourceValue = 0x0000_0000_0000_0010UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, sourceValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialValue), address);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            RetireWindowCaptureSnapshot transaction =
                CaptureAndApplyAtomicRetireWindowPublication(
                    ref core,
                    dispatcher,
                    CreateInstructionIr(opcode, rd: 9, rs1: 1, rs2: 2),
                    state,
                    vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Atomic, transaction.TypedEffectKind);
            Assert.Equal((byte)8, transaction.AtomicEffect.AccessSize);
            Assert.Equal(initialValue, core.ReadArch(vtId, 9));
            Assert.Equal(ComputeExpectedDwordAtomicWrite(opcode, initialValue, sourceValue), ReadDoubleword(address));
        }

        [Fact]
        public void AtomicRetireWindowCapture_DoesNotPublishMemoryOrRegisterStateUntilApply()
        {
            const byte vtId = 0;
            const ulong address = 0x1_0600;
            const uint initialWord = 0xFFFF_FFF0U;
            const uint sourceWord = 0x0000_0010U;
            const ulong originalDestination = 0xCAFE_BABE_DEAD_BEEFUL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, sourceWord);
            core.WriteCommittedArch(vtId, 9, originalDestination);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);
            InstructionIR instruction = CreateInstructionIr(InstructionsEnum.AMOADD_W, rd: 9, rs1: 1, rs2: 2);

            RetireWindowCaptureSnapshot captured =
                CaptureAtomicRetireWindowPublication(
                    dispatcher,
                    instruction,
                    state,
                    vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Atomic, captured.TypedEffectKind);
            Assert.True(captured.HasAtomicEffect);
            Assert.False(captured.HasSerializingBoundaryEffect);
            Assert.Equal(0, captured.RetireRecordCount);
            Assert.Equal(originalDestination, core.ReadArch(vtId, 9));
            Assert.Equal(initialWord, ReadWord(address));

            CaptureAndApplyAtomicRetireWindowPublication(
                ref core,
                dispatcher,
                instruction,
                state,
                vtId);

            Assert.Equal(SignExtendWordToUlong(initialWord), core.ReadArch(vtId, 9));
            Assert.Equal(
                ComputeExpectedWordAtomicWrite(InstructionsEnum.AMOADD_W, initialWord, sourceWord),
                ReadWord(address));
        }
    }
}
