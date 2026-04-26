using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Memory;
using HybridCPU_ISE.Tests.TestHelpers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using HybridCPU_ISE.Arch;

namespace HybridCPU_ISE.Tests.Phase09
{
    public sealed class RetireContractClosureTests
    {
        private sealed class ThrowingVectorFallbackMicroOp : MicroOp
        {
            public ThrowingVectorFallbackMicroOp(uint opCode)
            {
                OpCode = opCode;
                Class = MicroOpClass.Vector;
                IsMemoryOp = true;
                InstructionClass = InstructionClass.ScalarAlu;
                SerializationClass = SerializationClass.Free;
                SetClassFlexiblePlacement(SlotClass.AluClass);
            }

            public override bool Execute(ref Processor.CPU_Core core)
            {
                throw new InvalidOperationException("synthetic vector micro-op failure");
            }

            public override string GetDescription() => "Synthetic throwing vector fallback carrier";
        }

        private sealed class MainMemoryBus : IMemoryBus
        {
            public byte[] Read(ulong address, int length)
            {
                return Processor.MainMemory.ReadFromPosition(
                    byteArray_Buffer: new byte[length],
                    ioVirtualAddress: address,
                    length: (ulong)length);
            }

            public void Write(ulong address, byte[] data)
            {
                Processor.MainMemory.WriteToPosition(data, address);
            }
        }

        private static void SeedSchedulerClassTemplate(MicroOpScheduler scheduler)
        {
            var capacityState = new SlotClassCapacityState();
            capacityState.InitializeFromLaneMap();
            scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(capacityState));
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(0);
        }

        private static MicroOpScheduler PrimeReplaySchedulerForSerializingBoundary(
            ref Processor.CPU_Core core,
            ulong retiredPc,
            out long serializingEpochCountBefore)
        {
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            serializingEpochCountBefore = scheduler.SerializingEpochCount;
            Assert.True(core.GetReplayPhaseContext().IsActive);
            Assert.True(scheduler.TestGetReplayPhaseContext().IsActive);
            return scheduler;
        }

        private static void AssertReplaySerializingBoundaryPublication(
            Processor.CPU_Core core,
            MicroOpScheduler scheduler,
            long serializingEpochCountBefore)
        {
            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
        }

        private static void AssertVmTransitionReplayPublicationWithSerializingEpoch(
            Processor.CPU_Core core,
            MicroOpScheduler scheduler,
            long serializingEpochCountBefore)
        {
            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.VmTransition, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
        }

        private static void AssertTrapBoundaryReplayPublication(
            Processor.CPU_Core core,
            MicroOpScheduler scheduler,
            long serializingEpochCountBefore)
        {
            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Contains(
                scheduler.LastPhaseCertificateInvalidationReason,
                new[]
                {
                    ReplayPhaseInvalidationReason.SerializingEvent,
                    ReplayPhaseInvalidationReason.InactivePhase
                });
            Assert.Equal(AssistInvalidationReason.Trap, scheduler.LastAssistInvalidationReason);
            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
        }

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

        private static MicroOp MaterializeSingleSlotMicroOp(
            VLIW_Instruction instruction)
        {
            var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            rawSlots[0] = instruction;

            var decoder = new VliwDecoderV4();
            DecodedInstructionBundle canonicalBundle =
                decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x2400, bundleSerial: 73);

            MicroOp?[] carrierBundle =
                DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

            return Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]);
        }

        private static MicroOp CreateRegistryMicroOp(
            VLIW_Instruction instruction)
        {
            VLIW_Instruction.TryUnpackArchRegs(
                instruction.Word1,
                out byte reg1,
                out byte reg2,
                out byte reg3);

            var context = new DecoderContext
            {
                OpCode = instruction.OpCode,
                Immediate = instruction.Immediate,
                HasImmediate = true,
                Reg1ID = reg1,
                Reg2ID = reg2,
                Reg3ID = reg3,
            };

            return InstructionRegistry.CreateMicroOp(instruction.OpCode, context);
        }

        private static VLIW_Instruction CreateVectorInstruction(
            InstructionsEnum opcode,
            ulong destSrc1Pointer = 0,
            ulong src2Pointer = 0,
            ushort immediate = 0,
            uint streamLength = 1,
            ushort stride = 0,
            byte predicateMask = 0xFF,
            DataTypeEnum dataType = DataTypeEnum.INT32)
        {
            return new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = predicateMask,
                DestSrc1Pointer = destSrc1Pointer,
                Src2Pointer = src2Pointer,
                Immediate = immediate,
                StreamLength = streamLength,
                Stride = stride
            };
        }

        private static VLIW_Instruction CreateVectorTransferInstruction(
            InstructionsEnum opcode,
            ulong destSrc1Pointer = 0x280UL,
            ulong src2Pointer = 0x380UL,
            uint streamLength = 4,
            ushort stride = 4,
            DataTypeEnum dataType = DataTypeEnum.INT32) =>
            CreateVectorInstruction(
                opcode,
                destSrc1Pointer: destSrc1Pointer,
                src2Pointer: src2Pointer,
                streamLength: streamLength,
                stride: stride,
                dataType: dataType);

        private static VectorTransferMicroOp CreateVectorTransferMicroOp(
            InstructionsEnum opcode,
            VLIW_Instruction instruction,
            byte ownerThreadId = 1,
            byte ownerContextId = 1)
        {
            return new VectorTransferMicroOp
            {
                OpCode = (uint)opcode,
                OwnerThreadId = ownerThreadId,
                VirtualThreadId = ownerThreadId,
                OwnerContextId = ownerContextId,
                Instruction = instruction
            };
        }

        private static VLIW_Instruction CreateScalarInstruction(
            InstructionsEnum opcode,
            byte rd = 0,
            byte rs1 = 0,
            byte rs2 = 0,
            ushort immediate = 0)
        {
            return new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
                Src2Pointer = immediate,
                Immediate = immediate,
                StreamLength = 0,
                Stride = 0
            };
        }

        private static VLIW_Instruction CreateVectorConfigInstruction(
            InstructionsEnum opcode,
            byte rd,
            byte rs1 = 0,
            byte rs2 = 0,
            ushort immediate = 0,
            DataTypeEnum dataType = DataTypeEnum.INT32,
            uint streamLength = 0)
        {
            return new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
                Immediate = immediate,
                StreamLength = streamLength,
                Stride = 0
            };
        }

        private static void SeedVectorWordMemory(ulong address, params uint[] values)
        {
            byte[] data = new byte[values.Length * sizeof(uint)];
            for (int i = 0; i < values.Length; i++)
            {
                BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(uint));
            }

            Processor.MainMemory.WriteToPosition(data, address);
        }

        private static void SeedVectorByteMemory(ulong address, params byte[] values)
        {
            Processor.MainMemory.WriteToPosition(values, address);
        }

        private static byte[] ReadMainMemoryBytes(ulong address, int length)
        {
            return Processor.MainMemory.ReadFromPosition(
                new byte[length],
                address,
                (ulong)length);
        }

        private static ulong ResolveVectorTransferReadPointer(
            InstructionsEnum opcode,
            ulong destSrc1Pointer,
            ulong src2Pointer)
        {
            return opcode switch
            {
                InstructionsEnum.VLOAD => src2Pointer,
                InstructionsEnum.VSTORE => destSrc1Pointer,
                _ => throw new InvalidOperationException($"Unexpected vector transfer opcode {opcode}.")
            };
        }

        private static ulong ResolveVectorTransferWritePointer(
            InstructionsEnum opcode,
            ulong destSrc1Pointer,
            ulong src2Pointer)
        {
            return opcode switch
            {
                InstructionsEnum.VLOAD => destSrc1Pointer,
                InstructionsEnum.VSTORE => src2Pointer,
                _ => throw new InvalidOperationException($"Unexpected vector transfer opcode {opcode}.")
            };
        }

        private static void SeedVectorTransferMemory(
            InstructionsEnum opcode,
            ulong destSrc1Pointer,
            ulong src2Pointer,
            byte[] sourceBytes,
            byte[] destinationSeed)
        {
            if (sourceBytes.Length != destinationSeed.Length)
            {
                throw new ArgumentException("Vector transfer source and destination buffers must have the same length.");
            }

            Processor.MainMemory.WriteToPosition(
                sourceBytes,
                ResolveVectorTransferReadPointer(opcode, destSrc1Pointer, src2Pointer));
            Processor.MainMemory.WriteToPosition(
                destinationSeed,
                ResolveVectorTransferWritePointer(opcode, destSrc1Pointer, src2Pointer));
        }

        private static InstructionIR CreateInstructionIr(
            InstructionsEnum opcode,
            byte rd = 0,
            byte rs1 = 0,
            byte rs2 = 0,
            long imm = 0)
        {
            return new InstructionIR
            {
                CanonicalOpcode = opcode,
                Class = InstructionClassifier.GetClass(opcode),
                SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
                Rd = rd,
                Rs1 = rs1,
                Rs2 = rs2,
                Imm = imm
            };
        }

        private static RetireWindowCaptureSnapshot ResolveAndApplyDispatcherRetireWindowPublication(
            ref Processor.CPU_Core core,
            ExecutionDispatcherV4 dispatcher,
            InstructionIR instruction,
            ICanonicalCpuState state,
            ulong bundleSerial,
            byte vtId)
        {
            return RetireWindowCaptureTestHelper.CaptureAndApplyExecutionDispatcherRetireWindowPublications(
                ref core,
                dispatcher,
                instruction,
                state,
                bundleSerial,
                vtId);
        }

        private static RetireWindowCaptureSnapshot ResolveDispatcherRetireWindowPublication(
            ExecutionDispatcherV4 dispatcher,
            InstructionIR instruction,
            ICanonicalCpuState state,
            ulong bundleSerial,
            byte vtId)
        {
            return RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial,
                vtId);
        }

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

        [Fact]
        public void StreamVectorCompute_DoesNotSynthesizeScalarRegisterWriteBackCarrier()
        {
            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(2, 1, 7UL);
            core.WriteCommittedArch(2, 2, 5UL);

            var microOp = new VectorALUMicroOp
            {
                OpCode = (uint)InstructionsEnum.VADD,
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)InstructionsEnum.VADD,
                    DataTypeValue = DataTypeEnum.INT32,
                    PredicateMask = 0xFF,
                    DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 2),
                    StreamLength = 1
                }
            };
            microOp.InitializeMetadata();

            core.TestRetireExplicitPacketLaneMicroOp(
                laneIndex: 0,
                microOp,
                pc: 0x3400,
                vtId: 2);

            Assert.Equal(0UL, core.ReadArch(2, 9));

            var control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void StreamPopCountScalarWrite_RetiresThroughWriteBackCarrier()
        {
            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(3, 0b1011_0101UL);

            var microOp = new VectorALUMicroOp
            {
                OpCode = (uint)InstructionsEnum.VPOPC,
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)InstructionsEnum.VPOPC,
                    DataTypeValue = DataTypeEnum.INT32,
                    PredicateMask = 0xFF,
                    Immediate = (ushort)(3 | (6 << 8)),
                    StreamLength = 8
                }
            };
            microOp.InitializeMetadata();

            core.TestRetireExplicitPacketLaneMicroOp(
                laneIndex: 0,
                microOp,
                pc: 0x3600,
                vtId: 2);

            Assert.Equal(5UL, core.ReadArch(2, 6));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
        }

        [Fact]
        public void WriteBackWindow_WhenDeferredStoreAndCsrEffectShareRetireWindow_ThenBothApplyThroughGeneralizedBatch()
        {
            const int vtId = 0;
            const ulong storeAddress = 0x180UL;
            const ulong storeData = 0x8877_6655_4433_2211UL;
            const ulong csrWriteValue = 1UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, csrWriteValue);

            var storeOp = new StoreMicroOp
            {
                OwnerThreadId = vtId,
                VirtualThreadId = vtId,
                OwnerContextId = vtId,
                Address = storeAddress,
                Value = storeData,
                Size = 8,
                SrcRegID = 1
            };
            storeOp.InitializeMetadata();

            var csrOp = new CsrReadWriteMicroOp
            {
                OpCode = (uint)InstructionsEnum.CSRRW,
                CSRAddress = (ulong)VectorCSR.VLIW_STEAL_ENABLE,
                SrcRegID = 1,
                DestRegID = 9,
                WritesRegister = true,
                OwnerThreadId = vtId,
                VirtualThreadId = vtId,
                OwnerContextId = vtId
            };
            csrOp.InitializeMetadata();

            WriteBackStage writeBack = new();
            writeBack.Clear();
            writeBack.Valid = true;
            writeBack.ActiveLaneIndex = 7;
            writeBack.UsesExplicitPacketLanes = true;
            writeBack.MaterializedPhysicalLaneCount = 2;

            ScalarWriteBackLaneState lane4 = new();
            lane4.Clear(4);
            lane4.IsOccupied = true;
            lane4.PC = 0x4100;
            lane4.OpCode = (uint)InstructionsEnum.Store;
            lane4.ResultValue = storeData;
            lane4.IsMemoryOp = true;
            lane4.MemoryAddress = storeAddress;
            lane4.MemoryData = storeData;
            lane4.IsLoad = false;
            lane4.MemoryAccessSize = 8;
            lane4.WritesRegister = false;
            lane4.MicroOp = storeOp;
            lane4.OwnerThreadId = vtId;
            lane4.VirtualThreadId = vtId;
            lane4.OwnerContextId = vtId;
            lane4.DefersStoreCommitToWriteBack = true;
            writeBack.Lane4 = lane4;

            ScalarWriteBackLaneState lane7 = new();
            lane7.Clear(7);
            lane7.IsOccupied = true;
            lane7.PC = 0x4200;
            lane7.OpCode = (uint)InstructionsEnum.CSRRW;
            lane7.ResultValue = 0;
            lane7.WritesRegister = true;
            lane7.DestRegID = 9;
            lane7.MicroOp = csrOp;
            lane7.OwnerThreadId = vtId;
            lane7.VirtualThreadId = vtId;
            lane7.OwnerContextId = vtId;
            lane7.GeneratedCsrEffect = CsrRetireEffect.Create(
                CsrStorageSurface.VectorPodPlane,
                (ushort)VectorCSR.VLIW_STEAL_ENABLE,
                readValue: 0,
                hasRegisterWriteback: true,
                destRegId: 9,
                hasCsrWrite: true,
                csrWriteValue: csrWriteValue);
            writeBack.Lane7 = lane7;

            core.TestSetWriteBackStage(writeBack);
            core.TestRunWriteBackStage();

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[8], storeAddress, 8);

            Assert.Equal(storeData, BitConverter.ToUInt64(buffer, 0));
            Assert.Equal(0UL, core.ReadArch(vtId, 9));
            Assert.Equal((byte)1, core.VectorConfig.FSP_Enabled);

            var control = core.GetPipelineControl();
            Assert.Equal(2UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(2UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void WriteBackWindow_WhenLanePublishesMultipleSingletonTypedEffects_ThenThrows()
        {
            var core = new Processor.CPU_Core(0);
            var systemOp = SysEventMicroOp.ForFence();

            WriteBackStage writeBack = new();
            writeBack.Clear();
            writeBack.Valid = true;
            writeBack.ActiveLaneIndex = 7;
            writeBack.UsesExplicitPacketLanes = true;
            writeBack.MaterializedPhysicalLaneCount = 1;

            ScalarWriteBackLaneState lane7 = new();
            lane7.Clear(7);
            lane7.IsOccupied = true;
            lane7.PC = 0x5000;
            lane7.OpCode = (uint)InstructionsEnum.FENCE;
            lane7.MicroOp = systemOp;
            lane7.OwnerThreadId = 0;
            lane7.VirtualThreadId = 0;
            lane7.OwnerContextId = 0;
            lane7.GeneratedEvent = new FenceEvent
            {
                VtId = 0,
                BundleSerial = 1,
                IsInstructionFence = false
            };
            lane7.GeneratedVmxEffect = VmxRetireEffect.Control(
                VmxOperationKind.VmxOff,
                exitGuestContextOnRetire: true);
            writeBack.Lane7 = lane7;

            core.TestSetWriteBackStage(writeBack);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunWriteBackStage());

            Assert.Contains("multiple singleton typed effects", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void VmxRead_RetiresRegisterWriteThroughWriteBackCarrier()
        {
            const int vtId = 2;
            const byte fieldSelectorRegister = 1;
            const byte destinationRegister = 5;
            const ulong vmreadValue = 0x8877_6655_4433_2211UL;

            var core = new Processor.CPU_Core(0);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x1000);
            core.Vmcs.WriteFieldValue(VmcsField.HostPc, unchecked((long)vmreadValue));
            core.WriteCommittedArch(vtId, fieldSelectorRegister, (ulong)VmcsField.HostPc);

            var microOp = new VmxMicroOp
            {
                OpCode = (uint)InstructionsEnum.VMREAD,
                Rd = destinationRegister,
                Rs1 = fieldSelectorRegister,
                WritesRegister = true,
                DestRegID = destinationRegister
            };
            microOp.RefreshWriteMetadata();

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: 0x6400,
                vtId);

            Assert.Equal(vmreadValue, core.ReadArch(vtId, destinationRegister));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void VmxOff_RetiresHostRestoreAndRedirectThroughWriteBackCarrier()
        {
            const int vtId = 2;
            const ulong guestPc = 0x2200;
            const ulong guestSp = 0x3300;
            const ulong hostPc = 0x6600;
            const ulong hostSp = 0x7700;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(guestPc, vtId);
            core.WriteCommittedPc(vtId, guestPc);
            core.WriteCommittedArch(vtId, 2, guestSp);
            core.WriteVirtualThreadPipelineState(vtId, PipelineState.GuestExecution);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x2000);
            core.Vmcs.MarkLaunched();
            core.Vmcs.WriteFieldValue(VmcsField.HostPc, unchecked((long)hostPc));
            core.Vmcs.WriteFieldValue(VmcsField.HostSp, unchecked((long)hostSp));

            var microOp = new VmxMicroOp
            {
                OpCode = (uint)InstructionsEnum.VMXOFF
            };
            microOp.RefreshWriteMetadata();

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                guestPc,
                vtId);

            Assert.Equal(hostPc, core.ReadCommittedPc(vtId));
            Assert.Equal(hostSp, core.ReadArch(vtId, 2));
            Assert.Equal(hostPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(0UL, core.Csr.DirectRead(CsrAddresses.VmxEnable));
            Assert.Equal((ulong)VmExitReason.VmxOff, core.Csr.DirectRead(CsrAddresses.VmxExitReason));
            Assert.Equal(1UL, core.Csr.DirectRead(CsrAddresses.VmExitCnt));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmxOnCarrier_WhenReplayPhaseIsActive_PublishesSerializingBoundaryAndEnablesVmx()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x6440;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.VMXON)));
            Assert.Equal(SerializationClass.VmxSerial, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(1UL, core.Csr.DirectRead(CsrAddresses.VmxEnable));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmxReadCarrier_WhenReplayPhaseIsActive_PublishesSerializingBoundaryAndWritesRegister()
        {
            const int vtId = 1;
            const byte fieldSelectorRegister = 1;
            const byte destinationRegister = 5;
            const ulong retiredPc = 0x6460;
            const ulong vmreadValue = 0x8877_6655_4433_2211UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x1000);
            core.Vmcs.WriteFieldValue(VmcsField.HostPc, unchecked((long)vmreadValue));
            core.WriteCommittedArch(vtId, fieldSelectorRegister, (ulong)VmcsField.HostPc);

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(
                    CreateScalarInstruction(
                        InstructionsEnum.VMREAD,
                        rd: destinationRegister,
                        rs1: fieldSelectorRegister)));

            Assert.True(microOp.WritesRegister);
            Assert.True(microOp.AdmissionMetadata.WritesRegister);
            Assert.Equal(new[] { (int)fieldSelectorRegister }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Equal(new[] { (int)destinationRegister }, microOp.AdmissionMetadata.WriteRegisters);
            Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(vmreadValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmxReadCarrier_NoDestinationRegister_WhenReplayPhaseIsActive_PublishesSerializingBoundaryWithoutWriteBack()
        {
            const int vtId = 1;
            const byte fieldSelectorRegister = 1;
            const byte untouchedProbeRegister = 5;
            const ulong retiredPc = 0x6470;
            const ulong untouchedProbeValue = 0x1234_5678_9ABC_DEF0UL;
            const ulong vmreadValue = 0x8877_6655_4433_2211UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x1000);
            core.Vmcs.WriteFieldValue(VmcsField.HostPc, unchecked((long)vmreadValue));
            core.WriteCommittedArch(vtId, fieldSelectorRegister, (ulong)VmcsField.HostPc);
            core.WriteCommittedArch(vtId, untouchedProbeRegister, untouchedProbeValue);

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(
                    CreateScalarInstruction(
                        InstructionsEnum.VMREAD,
                        rd: VLIW_Instruction.NoArchReg,
                        rs1: fieldSelectorRegister)));

            Assert.False(microOp.WritesRegister);
            Assert.False(microOp.AdmissionMetadata.WritesRegister);
            Assert.Equal(new[] { (int)fieldSelectorRegister }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
            Assert.Equal(VLIW_Instruction.NoArchReg, microOp.Rd);

            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;

            Assert.True(microOp.Execute(ref core));
            VmxRetireEffect retireEffect = microOp.CreateRetireEffect();
            Assert.True(retireEffect.IsValid);
            Assert.False(retireEffect.HasRegisterDestination);
            Assert.Equal((ushort)0, retireEffect.RegisterDestination);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(untouchedProbeValue, core.ReadArch(vtId, untouchedProbeRegister));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmWriteCarrier_WhenReplayPhaseIsActive_PublishesSerializingBoundaryAndWritesVmcs()
        {
            const int vtId = 1;
            const byte fieldSelectorRegister = 1;
            const byte valueRegister = 2;
            const ulong retiredPc = 0x6480;
            const ulong vmwriteValue = 0x1122_3344_5566_7788UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x1100);
            core.WriteCommittedArch(vtId, fieldSelectorRegister, (ulong)VmcsField.HostPc);
            core.WriteCommittedArch(vtId, valueRegister, vmwriteValue);

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(
                    CreateScalarInstruction(
                        InstructionsEnum.VMWRITE,
                        rs1: fieldSelectorRegister,
                        rs2: valueRegister)));

            Assert.False(microOp.WritesRegister);
            Assert.False(microOp.AdmissionMetadata.WritesRegister);
            Assert.Equal(
                new[] { (int)fieldSelectorRegister, (int)valueRegister },
                microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
            Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(unchecked((long)vmwriteValue), core.Vmcs.ReadFieldValue(VmcsField.HostPc).Value);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmPtrLdCarrier_WhenReplayPhaseIsActive_PublishesSerializingBoundaryAndLoadsVmcs()
        {
            const int vtId = 0;
            const byte pointerRegister = 1;
            const ulong retiredPc = 0x64A0;
            const ulong vmcsPointer = 0x2000;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.WriteCommittedArch(vtId, pointerRegister, vmcsPointer);

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(
                    CreateScalarInstruction(
                        InstructionsEnum.VMPTRLD,
                        rs1: pointerRegister)));

            Assert.False(microOp.WritesRegister);
            Assert.False(microOp.AdmissionMetadata.WritesRegister);
            Assert.Equal(new[] { (int)pointerRegister }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
            Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.True(core.Vmcs.HasActiveVmcs);
            Assert.False(core.Vmcs.HasLaunchedVmcs);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmClearCarrier_WhenReplayPhaseIsActive_PublishesSerializingBoundaryAndClearsVmcs()
        {
            const int vtId = 0;
            const byte pointerRegister = 1;
            const ulong retiredPc = 0x64C0;
            const ulong vmcsPointer = 0x2100;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(vmcsPointer);
            core.Vmcs.MarkLaunched();
            core.WriteCommittedArch(vtId, pointerRegister, vmcsPointer);

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(
                    CreateScalarInstruction(
                        InstructionsEnum.VMCLEAR,
                        rs1: pointerRegister)));

            Assert.False(microOp.WritesRegister);
            Assert.False(microOp.AdmissionMetadata.WritesRegister);
            Assert.Equal(new[] { (int)pointerRegister }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
            Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.False(core.Vmcs.HasActiveVmcs);
            Assert.False(core.Vmcs.HasLaunchedVmcs);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmLaunchCarrier_WhenReplayPhaseIsActive_EntersGuestExecutionAndPublishesVmTransitionSerializingBoundary()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x64E0;
            const ulong guestPc = 0x9200;
            const ulong guestSp = 0x9300;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x2200);
            core.Vmcs.WriteFieldValue(VmcsField.GuestPc, unchecked((long)guestPc));
            core.Vmcs.WriteFieldValue(VmcsField.GuestSp, unchecked((long)guestSp));

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.VMLAUNCH)));

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertVmTransitionReplayPublicationWithSerializingEpoch(
                core,
                scheduler,
                serializingEpochCountBefore);
            Assert.True(core.Vmcs.HasLaunchedVmcs);
            Assert.Equal(PipelineState.GuestExecution, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(guestPc, core.ReadCommittedPc(vtId));
            Assert.Equal(guestPc, core.ReadActiveLivePc());
            Assert.Equal(guestSp, core.ReadArch(vtId, 2));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmResumeCarrier_WhenReplayPhaseIsActive_EntersGuestExecutionAndPublishesVmTransitionSerializingBoundary()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x6500;
            const ulong guestPc = 0x9400;
            const ulong guestSp = 0x9500;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x2300);
            core.Vmcs.MarkLaunched();
            core.Vmcs.WriteFieldValue(VmcsField.GuestPc, unchecked((long)guestPc));
            core.Vmcs.WriteFieldValue(VmcsField.GuestSp, unchecked((long)guestSp));

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.VMRESUME)));

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertVmTransitionReplayPublicationWithSerializingEpoch(
                core,
                scheduler,
                serializingEpochCountBefore);
            Assert.True(core.Vmcs.HasLaunchedVmcs);
            Assert.Equal(PipelineState.GuestExecution, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(guestPc, core.ReadCommittedPc(vtId));
            Assert.Equal(guestPc, core.ReadActiveLivePc());
            Assert.Equal(guestSp, core.ReadArch(vtId, 2));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmxOffCarrier_WhenReplayPhaseIsActive_RestoresHostStateAndPublishesVmTransitionSerializingBoundary()
        {
            const int vtId = 2;
            const ulong guestPc = 0x2200;
            const ulong guestSp = 0x3300;
            const ulong retiredPc = guestPc;
            const ulong hostPc = 0x6600;
            const ulong hostSp = 0x7700;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(guestPc, vtId);
            core.WriteCommittedPc(vtId, guestPc);
            core.WriteCommittedArch(vtId, 2, guestSp);
            core.WriteVirtualThreadPipelineState(vtId, PipelineState.GuestExecution);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x2000);
            core.Vmcs.MarkLaunched();
            core.Vmcs.WriteFieldValue(VmcsField.HostPc, unchecked((long)hostPc));
            core.Vmcs.WriteFieldValue(VmcsField.HostSp, unchecked((long)hostSp));

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.VMXOFF)));

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertVmTransitionReplayPublicationWithSerializingEpoch(
                core,
                scheduler,
                serializingEpochCountBefore);
            Assert.Equal(hostPc, core.ReadCommittedPc(vtId));
            Assert.Equal(hostSp, core.ReadArch(vtId, 2));
            Assert.Equal(hostPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(0UL, core.Csr.DirectRead(CsrAddresses.VmxEnable));
            Assert.Equal((ulong)VmExitReason.VmxOff, core.Csr.DirectRead(CsrAddresses.VmxExitReason));
            Assert.Equal(1UL, core.Csr.DirectRead(CsrAddresses.VmExitCnt));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmxOnCarrier_WhenAlreadyEnabled_PublishesSerializingBoundaryWithoutStateDrift()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x6450;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.VMXON)));

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(
                core,
                scheduler,
                serializingEpochCountBefore);
            Assert.Equal(1UL, core.Csr.DirectRead(CsrAddresses.VmxEnable));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVmReadCarrier_WithoutActiveVmcs_PublishesSerializingBoundaryWithoutRegisterDrift()
        {
            const int vtId = 1;
            const byte fieldSelectorRegister = 1;
            const byte destinationRegister = 5;
            const ulong retiredPc = 0x6470;
            const ulong originalDestinationValue = 0xABCD_EF01_2345_6789UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);
            core.WriteCommittedArch(vtId, fieldSelectorRegister, (ulong)VmcsField.HostPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);

            VmxMicroOp microOp = Assert.IsType<VmxMicroOp>(
                MaterializeSingleSlotMicroOp(
                    CreateScalarInstruction(
                        InstructionsEnum.VMREAD,
                        rd: destinationRegister,
                        rs1: fieldSelectorRegister)));

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(
                core,
                scheduler,
                serializingEpochCountBefore);
            Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
            Assert.False(core.Vmcs.HasActiveVmcs);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void Mret_RetiresCommittedPcThroughWriteBackCarrier()
        {
            const int vtId = 0;
            const ulong trapHandlerPc = 0x9000;
            const ulong returnPc = 0x4320;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(trapHandlerPc, vtId);
            core.WriteCommittedPc(vtId, trapHandlerPc);
            core.Csr.Write(CsrAddresses.Mepc, returnPc, PrivilegeLevel.Machine);

            var microOp = SysEventMicroOp.ForMret();
            microOp.OpCode = (uint)InstructionsEnum.MRET;

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                trapHandlerPc,
                vtId);

            Assert.Equal(returnPc, core.ReadCommittedPc(vtId));
            Assert.Equal(returnPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void ApplyRetiredSystemEventForTesting_MretOnInactiveVt_UpdatesCommittedPcWithoutTouchingActiveFrontend()
        {
            const int activeVtId = 0;
            const int retiredVtId = 2;
            const ulong activePc = 0x1800;
            const ulong retiredPc = 0x2200;
            const ulong returnPc = 0x5560;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(activePc, activeVtId);
            core.WriteCommittedPc(retiredVtId, retiredPc);
            core.Csr.Write(CsrAddresses.Mepc, returnPc, PrivilegeLevel.Machine);
            ulong activeLivePcBefore = core.ReadActiveLivePc();

            core.ApplyRetiredSystemEventForTesting(
                new MretEvent { VtId = (byte)retiredVtId, BundleSerial = 41 },
                virtualThreadId: retiredVtId,
                retiredPc: retiredPc);

            Assert.Equal(returnPc, core.ReadCommittedPc(retiredVtId));
            Assert.Equal(activeLivePcBefore, core.ReadActiveLivePc());
            Assert.Equal(activePc, core.ReadCommittedPc(activeVtId));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(retiredVtId));
        }

        [Theory]
        [InlineData(InstructionsEnum.YIELD, SystemEventKind.Yield)]
        [InlineData(InstructionsEnum.POD_BARRIER, SystemEventKind.PodBarrier)]
        [InlineData(InstructionsEnum.VT_BARRIER, SystemEventKind.VtBarrier)]
        public void MainlineSmtVtEventCarrier_RetiresThroughLane7SingletonWriteBack(
            InstructionsEnum opcode,
            SystemEventKind expectedKind)
        {
            const int vtId = 2;
            const ulong retiredPc = 0x5A00;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);

            var context = new DecoderContext
            {
                OpCode = (uint)opcode
            };

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                InstructionRegistry.CreateMicroOp((uint)opcode, context));
            Assert.Equal(expectedKind, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.None, microOp.OrderGuarantee);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineYieldCarrier_WhenReplayPhaseIsActive_DoesNotPromoteBoundaryOrChurnScheduler()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x5A20;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction instruction = new()
            {
                OpCode = (uint)InstructionsEnum.YIELD
            };

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(instruction));
            Assert.Equal(SystemEventKind.Yield, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.None, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.Free, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineExplicitPacketLoadCarrier_WhenReplayPhaseIsActive_DoesNotPromoteBoundaryOrChurnScheduler()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x5A28;
            const ulong address = 0x1980;
            const ushort destinationRegister = 9;
            const ulong originalDestinationValue = 0xCAFE_BABE_DEAD_BEEFUL;
            const ulong loadedValue = 0x0102_0304_0506_0708UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(loadedValue), address);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var microOp = new LoadMicroOp
            {
                OpCode = (uint)InstructionsEnum.LD,
                OwnerThreadId = vtId,
                VirtualThreadId = vtId,
                OwnerContextId = vtId,
                Address = address,
                Size = 8,
                BaseRegID = 1,
                DestRegID = destinationRegister,
                WritesRegister = true
            };
            microOp.InitializeMetadata();

            Assert.True(microOp.IsMemoryOp);
            Assert.Equal(InstructionClass.Memory, microOp.InstructionClass);
            Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
            Assert.Equal(SlotClass.LsuClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);

            core.TestPrepareExplicitPacketLoadForWriteBack(
                laneIndex: 4,
                retiredPc,
                address,
                destinationRegister,
                accessSize: 8,
                vtId);

            Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

            core.TestRunWriteBackStage();

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(loadedValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineExplicitPacketStoreCarrier_WhenReplayPhaseIsActive_DoesNotPromoteBoundaryOrChurnScheduler()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x5A30;
            const ulong address = 0x19C0;
            const ulong storeValue = 0x8877_6655_4433_2211UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            byte[] baseline = { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC };
            Processor.MainMemory.WriteToPosition(baseline, address);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var microOp = new StoreMicroOp
            {
                OwnerThreadId = vtId,
                VirtualThreadId = vtId,
                OwnerContextId = vtId,
                Address = address,
                Value = storeValue,
                Size = 8,
                SrcRegID = 2,
                BaseRegID = 1
            };
            microOp.InitializeMetadata();

            Assert.True(microOp.IsMemoryOp);
            Assert.Equal(InstructionClass.Memory, microOp.InstructionClass);
            Assert.Equal(SerializationClass.MemoryOrdered, microOp.SerializationClass);
            Assert.Equal(SlotClass.LsuClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);

            core.TestPrepareExplicitPacketStoreForWriteBack(
                laneIndex: 4,
                retiredPc,
                address,
                storeValue,
                accessSize: 8,
                vtId);

            Assert.Equal(baseline, Processor.MainMemory.ReadFromPosition(new byte[8], address, 8));

            core.TestRunWriteBackStage();

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(
                storeValue,
                BitConverter.ToUInt64(Processor.MainMemory.ReadFromPosition(new byte[8], address, 8), 0));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineAtomicCarrier_WhenReplayPhaseIsActive_DoesNotPromoteBoundaryOrChurnScheduler()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x5A34;
            const ulong address = 0x1A40;
            const uint initialWord = 0xFFFF_FFF0U;
            const uint sourceWord = 0x0000_0010U;
            const ushort destinationRegister = 9;
            const ulong originalDestinationValue = 0xCAFE_BABE_DEAD_BEEFUL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, sourceWord);
            core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var microOp = new AtomicMicroOp
            {
                OpCode = (uint)InstructionsEnum.AMOADD_W,
                OwnerThreadId = vtId,
                VirtualThreadId = vtId,
                OwnerContextId = vtId,
                DestRegID = destinationRegister,
                BaseRegID = 1,
                SrcRegID = 2,
                Size = 4,
                WritesRegister = true
            };
            microOp.InitializeMetadata();

            Assert.True(microOp.IsMemoryOp);
            Assert.Equal(InstructionClass.Atomic, microOp.InstructionClass);
            Assert.Equal(SerializationClass.AtomicSerial, microOp.SerializationClass);
            Assert.Equal(SlotClass.LsuClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);

            core.TestPrepareExplicitPacketAtomicForWriteBack(
                laneIndex: 4,
                microOp,
                pc: retiredPc,
                vtId);

            Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

            core.TestRunWriteBackStage();

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(SignExtendWordToUlong(initialWord), core.ReadArch(vtId, destinationRegister));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[4], address, 4);
            Assert.Equal(
                ComputeExpectedWordAtomicWrite(InstructionsEnum.AMOADD_W, initialWord, sourceWord),
                BitConverter.ToUInt32(buffer, 0));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Theory]
        [InlineData(InstructionsEnum.POD_BARRIER, SystemEventKind.PodBarrier)]
        [InlineData(InstructionsEnum.VT_BARRIER, SystemEventKind.VtBarrier)]
        public void MainlineSerializingSmtVtBarrierCarrier_WhenReplayPhaseIsActive_PublishesSerializingBoundary(
            InstructionsEnum opcode,
            SystemEventKind expectedKind)
        {
            const int vtId = 2;
            const ulong retiredPc = 0x5A40;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            VLIW_Instruction instruction = new()
            {
                OpCode = (uint)opcode
            };

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(instruction));
            Assert.Equal(expectedKind, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.None, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineWfeCarrier_WhenReplayPhaseIsActive_TransitionsToWaitForEventAndPublishesSerializingBoundary()
        {
            const int vtId = 1;
            const ulong retiredPc = 0x5A50;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.WFE)));
            Assert.Equal(SystemEventKind.Wfe, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.WaitForEvent, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineSevCarrier_WhenReplayPhaseIsActive_WakesWaitingVirtualThreadAndPublishesSerializingBoundary()
        {
            const int issuerVtId = 0;
            const int waitingVtId = 1;
            const ulong issuerPc = 0x5A58;
            const ulong waitingPc = 0x5A5C;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(issuerPc, issuerVtId);
            core.WriteCommittedPc(issuerVtId, issuerPc);
            core.WriteCommittedPc(waitingVtId, waitingPc);
            core.WriteVirtualThreadPipelineState(waitingVtId, PipelineState.WaitForEvent);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                issuerPc,
                out long serializingEpochCountBefore);

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.SEV)));
            Assert.Equal(SystemEventKind.Sev, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: issuerPc,
                issuerVtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(issuerVtId));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(waitingVtId));
            Assert.Equal(issuerPc, core.ReadCommittedPc(issuerVtId));
            Assert.Equal(waitingPc, core.ReadCommittedPc(waitingVtId));
            Assert.Equal(issuerPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineFenceCarrier_WhenReplayPhaseIsActive_DrainsMemoryWithoutBoundaryPromotion()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5A60;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.FENCE)));
            Assert.Equal(SystemEventKind.Fence, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.MemoryOrdered, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(AssistInvalidationReason.Fence, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineFenceICarrier_WhenReplayPhaseIsActive_FlushesPipelineAndPublishesFenceBoundary()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5A80;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.FENCE_I)));
            Assert.Equal(SystemEventKind.FenceI, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.FlushPipeline, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.Fence, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineWfiCarrier_WhenReplayPhaseIsActive_HaltsVirtualThreadAndPublishesSerializingBoundary()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5AA0;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.WFI)));
            Assert.Equal(SystemEventKind.Wfi, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Halted, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVectorExceptionMaskCarrier_WhenReplayPhaseIsActive_UpdatesMaskAndPublishesSerializingBoundary()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5AB0;
            const byte sourceRegister = 6;
            const ulong newMask = 0x13UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.ExceptionStatus.SetMask(0x1C);
            core.WriteCommittedArch(vtId, sourceRegister, newMask);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            CSRMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    InstructionsEnum.VSETVEXCPMASK,
                    rd: VLIW_Instruction.NoArchReg,
                    rs1: sourceRegister,
                    rs2: VLIW_Instruction.NoArchReg)));
            Assert.Equal(InstructionClass.Csr, microOp.InstructionClass);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
            Assert.Equal((ulong)CsrAddresses.VexcpMask, microOp.CSRAddress);
            Assert.Equal(new[] { (int)sourceRegister }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
            Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal((byte)(newMask & 0x1F), core.ExceptionStatus.GetMask());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVectorExceptionPriorityCarrier_WhenReplayPhaseIsActive_UpdatesPackedPrioritiesAndPublishesSerializingBoundary()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5AB8;
            const byte sourceRegister = 7;
            const ulong packedPriorities = 1UL | (4UL << 3) | (2UL << 6) | (7UL << 9) | (3UL << 12);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.ExceptionStatus.SetPriority(0, 0);
            core.ExceptionStatus.SetPriority(1, 0);
            core.ExceptionStatus.SetPriority(2, 0);
            core.ExceptionStatus.SetPriority(3, 0);
            core.ExceptionStatus.SetPriority(4, 0);
            core.WriteCommittedArch(vtId, sourceRegister, packedPriorities);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            CSRMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    InstructionsEnum.VSETVEXCPPRI,
                    rd: VLIW_Instruction.NoArchReg,
                    rs1: sourceRegister,
                    rs2: VLIW_Instruction.NoArchReg)));
            Assert.Equal(InstructionClass.Csr, microOp.InstructionClass);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
            Assert.Equal((ulong)CsrAddresses.VexcpPri, microOp.CSRAddress);
            Assert.Equal(new[] { (int)sourceRegister }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal((byte)1, core.ExceptionStatus.GetPriority(0));
            Assert.Equal((byte)4, core.ExceptionStatus.GetPriority(1));
            Assert.Equal((byte)2, core.ExceptionStatus.GetPriority(2));
            Assert.Equal((byte)7, core.ExceptionStatus.GetPriority(3));
            Assert.Equal((byte)3, core.ExceptionStatus.GetPriority(4));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVsetvlCarrier_WhenReplayPhaseIsActive_UpdatesVectorConfigAndPublishesSerializingBoundary()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5ABC;
            const byte destinationRegister = 4;
            const byte avlRegister = 5;
            const byte vtypeRegister = 6;
            const ulong requestedVl = 19UL;
            const ulong vtype = 0xC1UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.VectorConfig.VL = 1;
            core.VectorConfig.VTYPE = 0;
            core.VectorConfig.TailAgnostic = 0;
            core.VectorConfig.MaskAgnostic = 0;
            core.WriteCommittedArch(vtId, avlRegister, requestedVl);
            core.WriteCommittedArch(vtId, vtypeRegister, vtype);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            VConfigMicroOp microOp = Assert.IsType<VConfigMicroOp>(
                MaterializeSingleSlotMicroOp(CreateVectorConfigInstruction(
                    InstructionsEnum.VSETVL,
                    destinationRegister,
                    avlRegister,
                    vtypeRegister)));
            Assert.Equal(InstructionClass.System, microOp.InstructionClass);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
            Assert.Equal(new[] { (int)avlRegister, (int)vtypeRegister }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Equal(new[] { (int)destinationRegister }, microOp.AdmissionMetadata.WriteRegisters);
            Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(requestedVl, core.VectorConfig.VL);
            Assert.Equal(vtype, core.VectorConfig.VTYPE);
            Assert.Equal((byte)1, core.VectorConfig.TailAgnostic);
            Assert.Equal((byte)1, core.VectorConfig.MaskAgnostic);
            Assert.Equal(requestedVl, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVsetvliCarrier_WhenReplayPhaseIsActive_UsesCurrentImmediateVtypeContourAndPublishesSerializingBoundary()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5ABE;
            const byte destinationRegister = 7;
            const byte avlRegister = 8;
            const ulong requestedVl = 48UL;
            DataTypeEnum encodedVType = DataTypeEnum.UINT16;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.VectorConfig.VL = 2;
            core.VectorConfig.VTYPE = 0;
            core.VectorConfig.TailAgnostic = 1;
            core.VectorConfig.MaskAgnostic = 1;
            core.WriteCommittedArch(vtId, avlRegister, requestedVl);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            VConfigMicroOp microOp = Assert.IsType<VConfigMicroOp>(
                MaterializeSingleSlotMicroOp(CreateVectorConfigInstruction(
                    InstructionsEnum.VSETVLI,
                    destinationRegister,
                    avlRegister,
                    VLIW_Instruction.NoArchReg,
                    dataType: encodedVType,
                    streamLength: 1)));
            Assert.Equal(InstructionClass.System, microOp.InstructionClass);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
            Assert.Equal(new[] { (int)avlRegister }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Equal(new[] { (int)destinationRegister }, microOp.AdmissionMetadata.WriteRegisters);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(Processor.CPU_Core.RVV_Config.VLMAX, core.VectorConfig.VL);
            Assert.Equal((ulong)encodedVType, core.VectorConfig.VTYPE);
            Assert.Equal((byte)0, core.VectorConfig.TailAgnostic);
            Assert.Equal((byte)0, core.VectorConfig.MaskAgnostic);
            Assert.Equal(Processor.CPU_Core.RVV_Config.VLMAX, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineVsetivliCarrier_WhenReplayPhaseIsActive_UsesImmediateAvlContourAndPublishesSerializingBoundary()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5AC2;
            const byte destinationRegister = 9;
            const ushort avlImmediate = 13;
            DataTypeEnum encodedVType = DataTypeEnum.INT16;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.VectorConfig.VL = 3;
            core.VectorConfig.VTYPE = 0xFFFF;
            core.VectorConfig.TailAgnostic = 1;
            core.VectorConfig.MaskAgnostic = 1;
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            VConfigMicroOp microOp = Assert.IsType<VConfigMicroOp>(
                MaterializeSingleSlotMicroOp(CreateVectorConfigInstruction(
                    InstructionsEnum.VSETIVLI,
                    destinationRegister,
                    immediate: avlImmediate,
                    dataType: encodedVType)));
            Assert.Equal(InstructionClass.System, microOp.InstructionClass);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
            Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
            Assert.Equal(new[] { (int)destinationRegister }, microOp.AdmissionMetadata.WriteRegisters);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal((ulong)avlImmediate, core.VectorConfig.VL);
            Assert.Equal((ulong)encodedVType, core.VectorConfig.VTYPE);
            Assert.Equal((byte)0, core.VectorConfig.TailAgnostic);
            Assert.Equal((byte)0, core.VectorConfig.MaskAgnostic);
            Assert.Equal((ulong)avlImmediate, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineCsrCarrier_WhenReplayPhaseIsActive_PreservesReplayStateWithoutBoundaryPromotion()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5AC0;
            const byte sourceRegister = 1;
            const byte destinationRegister = 9;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.VectorConfig.FSP_Enabled = 1;
            core.WriteCommittedArch(vtId, sourceRegister, 0);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            CSRMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    InstructionsEnum.CSRRW,
                    rd: destinationRegister,
                    rs1: sourceRegister,
                    immediate: (ushort)VectorCSR.VLIW_STEAL_ENABLE)));
            Assert.Equal(InstructionClass.Csr, microOp.InstructionClass);
            Assert.Equal(SerializationClass.CsrOrdered, microOp.SerializationClass);
            Assert.Equal(SlotClass.SystemSingleton, microOp.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.Placement.PinningKind);
            Assert.Equal(7, microOp.Placement.PinnedLaneId);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(1UL, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(0, core.VectorConfig.FSP_Enabled);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineCsrCarrier_OnWiredCsrFileSurface_WhenReplayPhaseIsActive_PreservesReplayStateWithoutBoundaryPromotion()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5AD0;
            const byte sourceRegister = 1;
            const byte destinationRegister = 9;
            const ulong oldCsrValue = 0x55UL;
            const ulong newCsrValue = 0xABUL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.Csr.Write(CsrAddresses.Mstatus, oldCsrValue, PrivilegeLevel.Machine);
            core.WriteCommittedArch(vtId, sourceRegister, newCsrValue);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            CSRMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    InstructionsEnum.CSRRW,
                    rd: destinationRegister,
                    rs1: sourceRegister,
                    immediate: CsrAddresses.Mstatus)));

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(oldCsrValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(newCsrValue, core.Csr.DirectRead(CsrAddresses.Mstatus));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void CsrWriteHelperCarrier_OnWiredCsrFileSurface_WritesEncodedSourceWithoutBoundaryPromotion()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5AD4;
            const byte sourceRegister = 4;
            const ulong oldCsrValue = 0x55UL;
            const ulong newCsrValue = 0xABUL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.Csr.Write(CsrAddresses.Mstatus, oldCsrValue, PrivilegeLevel.Machine);
            core.WriteCommittedArch(vtId, sourceRegister, newCsrValue);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction instruction =
                InstructionEncoder.EncodeCSRWrite(CsrAddresses.Mstatus, sourceRegister);

            Assert.Equal((uint)InstructionsEnum.CSRRW, instruction.OpCode);

            CSRMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(
                CreateRegistryMicroOp(instruction));
            Assert.Equal(sourceRegister, microOp.SrcRegID);
            Assert.Equal(new[] { (int)sourceRegister }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
            Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));

            CsrRetireEffect effect = microOp.CreateRetireEffect(ref core);
            Assert.False(effect.HasRegisterWriteback);
            Assert.True(effect.HasCsrWrite);
            Assert.Equal(oldCsrValue, effect.ReadValue);
            Assert.Equal(newCsrValue, effect.CsrWriteValue);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(newCsrValue, core.Csr.DirectRead(CsrAddresses.Mstatus));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void DirectFactoryCsrrsReadOnly_NoDestinationNoArchReg_PreservesReplayWithoutWriteBack()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5AD7;
            const byte untouchedProbeRegister = 9;
            const ulong untouchedProbeValue = 0x8899_AABB_CCDD_EEFFUL;
            const ulong oldCsrValue = 0x5AUL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, untouchedProbeRegister, untouchedProbeValue);
            core.Csr.Write(CsrAddresses.Mstatus, oldCsrValue, PrivilegeLevel.Machine);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var context = new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.CSRRS,
                Immediate = CsrAddresses.Mstatus,
                HasImmediate = true,
                Reg1ID = VLIW_Instruction.NoArchReg,
                Reg2ID = 0,
            };

            CSRMicroOp microOp = Assert.IsType<CsrReadSetMicroOp>(
                InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.CSRRS, context));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            Assert.Equal(VLIW_Instruction.NoReg, microOp.DestRegID);
            Assert.False(microOp.WritesRegister);
            Assert.False(microOp.AdmissionMetadata.WritesRegister);
            Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
            Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));

            CsrRetireEffect effect = microOp.CreateRetireEffect(ref core);
            Assert.False(effect.HasRegisterWriteback);
            Assert.False(effect.HasCsrWrite);
            Assert.Equal(oldCsrValue, effect.ReadValue);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(untouchedProbeValue, core.ReadArch(vtId, untouchedProbeRegister));
            Assert.Equal(oldCsrValue, core.Csr.DirectRead(CsrAddresses.Mstatus));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void MainlineCsrrwiImmediateCarrier_OnVectorPodSurface_WritesImmediateValueWithoutBoundaryPromotion()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5AD8;
            const byte destinationRegister = 9;
            const byte immediateValue = 1;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.VectorConfig.FSP_Enabled = 0;
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            CSRMicroOp microOp = Assert.IsType<CsrReadWriteImmediateMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    InstructionsEnum.CSRRWI,
                    rd: destinationRegister,
                    rs1: immediateValue,
                    immediate: (ushort)VectorCSR.VLIW_STEAL_ENABLE)));

            Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
            Assert.Equal(new[] { (int)destinationRegister }, microOp.AdmissionMetadata.WriteRegisters);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(0UL, core.ReadArch(vtId, destinationRegister));
            Assert.Equal((byte)immediateValue, core.VectorConfig.FSP_Enabled);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRS, typeof(CsrReadSetMicroOp))]
        [InlineData(InstructionsEnum.CSRRC, typeof(CsrReadClearMicroOp))]
        public void MainlineCsrOptionalSourceCarrier_WhenRegisterSourceIsZero_PreservesReplayStateWithoutPhantomWrite(
            InstructionsEnum opcode,
            Type expectedMicroOpType)
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5ADA;
            const byte destinationRegister = 9;
            const ulong oldCsrValue = 1UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.VectorConfig.FSP_Enabled = (byte)oldCsrValue;
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            CSRMicroOp microOp = Assert.IsAssignableFrom<CSRMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    opcode,
                    rd: destinationRegister,
                    rs1: 0,
                    immediate: (ushort)VectorCSR.VLIW_STEAL_ENABLE)));
            Assert.IsType(expectedMicroOpType, microOp);
            Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);

            CsrRetireEffect effect = microOp.CreateRetireEffect(ref core);
            Assert.False(effect.HasCsrWrite);
            Assert.Equal(oldCsrValue, effect.ReadValue);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(oldCsrValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal((byte)oldCsrValue, core.VectorConfig.FSP_Enabled);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void MainlineCsrrwCarrier_WhenSourceRegisterIsX0_WritesZeroWithoutPhantomRegisterReadOrBoundaryPromotion()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5ADB;
            const byte destinationRegister = 9;
            const ulong oldCsrValue = 0x55UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.Csr.Write(CsrAddresses.Mstatus, oldCsrValue, PrivilegeLevel.Machine);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            CSRMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    InstructionsEnum.CSRRW,
                    rd: destinationRegister,
                    rs1: 0,
                    immediate: CsrAddresses.Mstatus)));

            Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
            Assert.True(microOp.AdmissionMetadata.WritesRegister);
            Assert.Equal(new[] { (int)destinationRegister }, microOp.AdmissionMetadata.WriteRegisters);

            CsrRetireEffect effect = microOp.CreateRetireEffect(ref core);
            Assert.True(effect.HasRegisterWriteback);
            Assert.True(effect.HasCsrWrite);
            Assert.Equal(0UL, effect.CsrWriteValue);
            Assert.Equal(oldCsrValue, effect.ReadValue);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(oldCsrValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(0UL, core.Csr.DirectRead(CsrAddresses.Mstatus));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectFactoryCsrrwCarrier_NoDestinationNoArchReg_PreservesReplayWithoutWriteBack()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5ADB1;
            const byte sourceRegister = 4;
            const byte untouchedProbeRegister = 9;
            const ulong sourceValue = 0xCAFE_BABE_1234_5678UL;
            const ulong untouchedProbeValue = 0x1122_3344_5566_7788UL;
            const ulong oldCsrValue = 0x55UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
            core.WriteCommittedArch(vtId, untouchedProbeRegister, untouchedProbeValue);
            core.Csr.Write(CsrAddresses.Mstatus, oldCsrValue, PrivilegeLevel.Machine);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var context = new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.CSRRW,
                Immediate = CsrAddresses.Mstatus,
                HasImmediate = true,
                Reg1ID = VLIW_Instruction.NoArchReg,
                Reg2ID = sourceRegister,
            };

            CSRMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(
                InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.CSRRW, context));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            Assert.Equal(VLIW_Instruction.NoReg, microOp.DestRegID);
            Assert.False(microOp.WritesRegister);
            Assert.False(microOp.AdmissionMetadata.WritesRegister);
            Assert.Equal(new[] { (int)sourceRegister }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);

            CsrRetireEffect effect = microOp.CreateRetireEffect(ref core);
            Assert.False(effect.HasRegisterWriteback);
            Assert.True(effect.HasCsrWrite);
            Assert.Equal(oldCsrValue, effect.ReadValue);
            Assert.Equal(sourceValue, effect.CsrWriteValue);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(untouchedProbeValue, core.ReadArch(vtId, untouchedProbeRegister));
            Assert.Equal(sourceValue, core.Csr.DirectRead(CsrAddresses.Mstatus));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRW, 4, 0x5UL, 0x7UL, typeof(CsrReadWriteMicroOp))]
        [InlineData(InstructionsEnum.CSRRWI, 1, 0x0UL, 0x1UL, typeof(CsrReadWriteImmediateMicroOp))]
        public void MainlineCsrCarrier_WhenDestinationIsX0_DoesNotPublishPhantomWritebackOrBoundaryPromotion(
            InstructionsEnum opcode,
            byte sourceOrImmediate,
            ulong oldCsrValue,
            ulong expectedCsrValue,
            Type expectedMicroOpType)
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5ADC;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            if (opcode == InstructionsEnum.CSRRW)
            {
                core.CreateLiveCpuStateAdapter(vtId).WriteRegister((byte)vtId, sourceOrImmediate, expectedCsrValue);
            }

            core.Csr.Write(CsrAddresses.Mstatus, oldCsrValue, PrivilegeLevel.Machine);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            CSRMicroOp microOp = Assert.IsAssignableFrom<CSRMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    opcode,
                    rd: 0,
                    rs1: sourceOrImmediate,
                    immediate: CsrAddresses.Mstatus)));
            Assert.IsType(expectedMicroOpType, microOp);

            Assert.False(microOp.AdmissionMetadata.WritesRegister);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
            Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));

            CsrRetireEffect effect = microOp.CreateRetireEffect(ref core);
            Assert.False(effect.HasRegisterWriteback);
            Assert.True(effect.HasCsrWrite);
            Assert.Equal(expectedCsrValue, effect.CsrWriteValue);
            Assert.Equal(oldCsrValue, effect.ReadValue);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(0UL, core.ReadArch(vtId, 0));
            Assert.Equal(expectedCsrValue, core.Csr.DirectRead(CsrAddresses.Mstatus));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRSI, 0x50UL, 0x5UL, 0x55UL, typeof(CsrReadSetImmediateMicroOp))]
        [InlineData(InstructionsEnum.CSRRCI, 0x55UL, 0x5UL, 0x50UL, typeof(CsrReadClearImmediateMicroOp))]
        public void MainlineCsrImmediateCarrier_OnWiredCsrFileSurface_PreservesReplayStateWithoutBoundaryPromotion(
            InstructionsEnum opcode,
            ulong oldCsrValue,
            ulong immediateMask,
            ulong expectedCsrValue,
            Type expectedMicroOpType)
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5ADC;
            const byte destinationRegister = 9;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.Csr.Write(CsrAddresses.Mstatus, oldCsrValue, PrivilegeLevel.Machine);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            CSRMicroOp microOp = Assert.IsAssignableFrom<CSRMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    opcode,
                    rd: destinationRegister,
                    rs1: (byte)immediateMask,
                    immediate: CsrAddresses.Mstatus)));
            Assert.IsType(expectedMicroOpType, microOp);
            Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
            Assert.Equal(new[] { (int)destinationRegister }, microOp.AdmissionMetadata.WriteRegisters);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(oldCsrValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(expectedCsrValue, core.Csr.DirectRead(CsrAddresses.Mstatus));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRSI, typeof(CsrReadSetImmediateMicroOp))]
        [InlineData(InstructionsEnum.CSRRCI, typeof(CsrReadClearImmediateMicroOp))]
        public void MainlineCsrImmediateCarrier_WhenImmediateMaskIsZero_PreservesReplayStateWithoutPhantomWrite(
            InstructionsEnum opcode,
            Type expectedMicroOpType)
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5ADE;
            const byte destinationRegister = 9;
            const ulong oldCsrValue = 0x55UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.Csr.Write(CsrAddresses.Mstatus, oldCsrValue, PrivilegeLevel.Machine);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            CSRMicroOp microOp = Assert.IsAssignableFrom<CSRMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    opcode,
                    rd: destinationRegister,
                    rs1: 0,
                    immediate: CsrAddresses.Mstatus)));
            Assert.IsType(expectedMicroOpType, microOp);
            Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);

            CsrRetireEffect effect = microOp.CreateRetireEffect(ref core);
            Assert.False(effect.HasCsrWrite);
            Assert.Equal(oldCsrValue, effect.ReadValue);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                pc: retiredPc,
                vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(oldCsrValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(oldCsrValue, core.Csr.DirectRead(CsrAddresses.Mstatus));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void MainlineCsrCarrier_WhenAddressHasNoAuthoritativeStorageSurface_ThenExecuteRejectsHiddenNoOpContour()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5AE0;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, 1, 0xABUL);

            CSRMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(
                    InstructionsEnum.CSRRW,
                    rd: 9,
                    rs1: 1,
                    immediate: 0xFFF)));
            Assert.Equal(0xFFFUL, microOp.CSRAddress);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => microOp.Execute(ref core));

            Assert.Contains("authoritative CSR storage surface", ex.Message, StringComparison.Ordinal);
            Assert.Contains("hidden success/no-op", ex.Message, StringComparison.Ordinal);
            Assert.Equal(0UL, core.ReadArch(vtId, 9));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
        }

        [Fact]
        public void MainlineMretCarrier_WhenReplayPhaseIsActive_RestoresCommittedPcAndPublishesTrapBoundary()
        {
            const int vtId = 0;
            const ulong trapHandlerPc = 0x5AC0;
            const ulong returnPc = 0x4340;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(trapHandlerPc, vtId);
            core.WriteCommittedPc(vtId, trapHandlerPc);
            core.Csr.Write(CsrAddresses.Mepc, returnPc, PrivilegeLevel.Machine);

            MicroOpScheduler scheduler =
                PrimeReplaySchedulerForSerializingBoundary(
                    ref core,
                    trapHandlerPc,
                    out long serializingEpochCountBefore);

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.MRET)));
            Assert.Equal(SystemEventKind.Mret, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                trapHandlerPc,
                vtId);

            AssertTrapBoundaryReplayPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(returnPc, core.ReadCommittedPc(vtId));
            Assert.Equal(returnPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineSretCarrier_WhenReplayPhaseIsActive_RestoresSupervisorReturnPcAndPublishesTrapBoundary()
        {
            const int vtId = 1;
            const ulong trapHandlerPc = 0x5AE0;
            const ulong returnPc = 0x4380;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(trapHandlerPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, trapHandlerPc);
            core.ArchContexts[vtId].CurrentPrivilege = PrivilegeLevel.Supervisor;
            core.Csr.Write(CsrAddresses.Sepc, returnPc, PrivilegeLevel.Supervisor);

            MicroOpScheduler scheduler =
                PrimeReplaySchedulerForSerializingBoundary(
                    ref core,
                    trapHandlerPc,
                    out long serializingEpochCountBefore);

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.SRET)));
            Assert.Equal(SystemEventKind.Sret, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                trapHandlerPc,
                vtId);

            AssertTrapBoundaryReplayPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(returnPc, core.ReadCommittedPc(vtId));
            Assert.Equal(returnPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineEcallCarrier_WhenReplayPhaseIsActive_EntersMachineTrapAndPublishesTrapBoundary()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5B00;
            const ulong trapHandlerPc = 0x1200;
            const long ecallCode = 93;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, 17, (ulong)ecallCode);
            core.Csr.Write(CsrAddresses.Mtvec, trapHandlerPc, PrivilegeLevel.Machine);

            MicroOpScheduler scheduler =
                PrimeReplaySchedulerForSerializingBoundary(
                    ref core,
                    retiredPc,
                    out long serializingEpochCountBefore);

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.ECALL)));
            Assert.Equal(SystemEventKind.Ecall, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
            Assert.Equal(new[] { 17 }, microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
            Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                retiredPc,
                vtId);

            AssertTrapBoundaryReplayPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(retiredPc, core.Csr.DirectRead(CsrAddresses.Mepc));
            Assert.Equal(11UL, core.Csr.DirectRead(CsrAddresses.Mcause));
            Assert.Equal(trapHandlerPc, core.ReadCommittedPc(vtId));
            Assert.Equal(trapHandlerPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void MainlineEbreakCarrier_WhenReplayPhaseIsActive_EntersBreakpointTrapAndPublishesTrapBoundary()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x5B20;
            const ulong trapHandlerPc = 0x1400;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.Csr.Write(CsrAddresses.Mtvec, trapHandlerPc, PrivilegeLevel.Machine);

            MicroOpScheduler scheduler =
                PrimeReplaySchedulerForSerializingBoundary(
                    ref core,
                    retiredPc,
                    out long serializingEpochCountBefore);

            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                MaterializeSingleSlotMicroOp(CreateScalarInstruction(InstructionsEnum.EBREAK)));
            Assert.Equal(SystemEventKind.Ebreak, microOp.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, microOp.OrderGuarantee);
            Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);

            core.TestRetireExplicitLane7SingletonMicroOp(
                microOp,
                retiredPc,
                vtId);

            AssertTrapBoundaryReplayPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(retiredPc, core.Csr.DirectRead(CsrAddresses.Mepc));
            Assert.Equal(3UL, core.Csr.DirectRead(CsrAddresses.Mcause));
            Assert.Equal(trapHandlerPc, core.ReadCommittedPc(vtId));
            Assert.Equal(trapHandlerPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void ResetExecutionStartPcState_SeedsCommittedPcForAllVtsAndActiveLivePc()
        {
            const ulong seededPc = 0x7A00;
            const int activeVtId = 2;

            var core = new Processor.CPU_Core(0);
            core.ResetExecutionStartPcState(seededPc, activeVtId);

            for (int vt = 0; vt < Processor.CPU_Core.SmtWays; vt++)
            {
                Assert.Equal(seededPc, core.ReadCommittedPc(vt));
            }

            Assert.Equal(seededPc, core.ReadActiveLivePc());
            Assert.Equal(activeVtId, core.ReadActiveVirtualThreadId());
        }

        [Fact]
        public void DirectCompatCsrTransaction_AppliesOnRealCore()
        {
            const int vtId = 0;

            var core = new Processor.CPU_Core(0);
            core.ExceptionStatus.OverflowCount = 9;
            core.ExceptionStatus.DivByZeroCount = 4;

            var dispatcher = new ExecutionDispatcherV4(csrFile: core.Csr);
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.CSR_CLEAR,
                Class = InstructionClassifier.GetClass(InstructionsEnum.CSR_CLEAR),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.CSR_CLEAR),
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 17,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Csr, transaction.TypedEffectKind);
            Assert.Equal(0, transaction.RetireRecordCount);
            Assert.True(transaction.CsrEffect.ClearsArchitecturalExceptionState);

            Assert.Equal(0U, core.ExceptionStatus.OverflowCount);
            Assert.Equal(0U, core.ExceptionStatus.DivByZeroCount);
        }

        [Fact]
        public void DirectCompatCsrTransaction_WhenReplayPhaseIsActive_PreservesReplayStateWithoutBoundaryPromotion()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x6A40;
            const ulong oldCsrValue = 0x55UL;
            const ulong newCsrValue = 0xABUL;
            const byte sourceRegister = 1;
            const byte destinationRegister = 9;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.Csr.Write(CsrAddresses.Mstatus, oldCsrValue, PrivilegeLevel.Machine);
            core.WriteCommittedArch(vtId, sourceRegister, newCsrValue);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var dispatcher = new ExecutionDispatcherV4(csrFile: core.Csr);
            ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.CSRRW,
                Class = InstructionClassifier.GetClass(InstructionsEnum.CSRRW),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.CSRRW),
                Rd = destinationRegister,
                Rs1 = sourceRegister,
                Rs2 = 0,
                Imm = CsrAddresses.Mstatus
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 19,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Csr, transaction.TypedEffectKind);
            Assert.False(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.True(transaction.HasCsrEffect);
            Assert.Equal(oldCsrValue, transaction.GetRetireRecord(0).Value);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(oldCsrValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(newCsrValue, core.Csr.DirectRead(CsrAddresses.Mstatus));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectCompatVmxTransaction_AppliesOnRealCore()
        {
            const int vtId = 2;
            const ulong guestPc = 0x2200;
            const ulong guestSp = 0x3300;
            const ulong hostPc = 0x6600;
            const ulong hostSp = 0x7700;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(guestPc, vtId);
            core.WriteCommittedPc(vtId, guestPc);
            core.WriteCommittedArch(vtId, 2, guestSp);
            core.WriteVirtualThreadPipelineState(vtId, PipelineState.GuestExecution);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: guestPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(vtId, destReg: 1, src1Reg: 2, src2Reg: 3));
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x2000);
            core.Vmcs.MarkLaunched();
            core.Vmcs.WriteFieldValue(VmcsField.HostPc, unchecked((long)hostPc));
            core.Vmcs.WriteFieldValue(VmcsField.HostSp, unchecked((long)hostSp));

            var dispatcher = new ExecutionDispatcherV4(
                csrFile: core.Csr,
                vmxUnit: new VmxExecutionUnit(core.Csr, core.Vmcs));
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.VMXOFF,
                Class = InstructionClassifier.GetClass(InstructionsEnum.VMXOFF),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.VMXOFF),
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 23,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);

            Assert.Equal(hostPc, core.ReadCommittedPc(vtId));
            Assert.Equal(hostSp, core.ReadArch(vtId, 2));
            Assert.Equal(hostPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(0UL, core.Csr.DirectRead(CsrAddresses.VmxEnable));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();
            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(AssistInvalidationReason.VmTransition, scheduler.LastAssistInvalidationReason);
        }

        [Fact]
        public void DirectCompatVmxOnTransaction_PublishesSerializingBoundaryAndEnablesVmxOnRealCore()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x72EC;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(vtId, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;

            var dispatcher = new ExecutionDispatcherV4(
                csrFile: core.Csr,
                vmxUnit: new VmxExecutionUnit(core.Csr, core.Vmcs));
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.VMXON),
                state,
                bundleSerial: 30,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(1UL, core.Csr.DirectRead(CsrAddresses.VmxEnable));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectCompatVmLaunchTransaction_WithoutActiveVmcs_PublishesSerializingBoundaryWithoutVmStateDrift()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x72ED;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(vtId, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);

            var dispatcher = new ExecutionDispatcherV4(
                csrFile: core.Csr,
                vmxUnit: new VmxExecutionUnit(core.Csr, core.Vmcs));
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.VMLAUNCH),
                state,
                bundleSerial: 33,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);
            Assert.True(transaction.VmxEffect.IsFaulted);
            Assert.Equal(VmxOperationKind.VmLaunch, transaction.VmxEffect.Operation);

            AssertReplaySerializingBoundaryPublication(
                core,
                scheduler,
                serializingEpochCountBefore);
            Assert.False(core.Vmcs.HasActiveVmcs);
            Assert.False(core.Vmcs.HasLaunchedVmcs);
            Assert.Equal(1UL, core.Csr.DirectRead(CsrAddresses.VmxEnable));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectCompatVmLaunchTransaction_EntersGuestExecutionAndPublishesVmTransitionSerializingBoundaryOnRealCore()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x72EE;
            const ulong guestPc = 0x9200;
            const ulong guestSp = 0x9300;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(vtId, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x2200);
            core.Vmcs.WriteFieldValue(VmcsField.GuestPc, unchecked((long)guestPc));
            core.Vmcs.WriteFieldValue(VmcsField.GuestSp, unchecked((long)guestSp));

            var dispatcher = new ExecutionDispatcherV4(
                csrFile: core.Csr,
                vmxUnit: new VmxExecutionUnit(core.Csr, core.Vmcs));
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.VMLAUNCH),
                state,
                bundleSerial: 34,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);

            AssertVmTransitionReplayPublicationWithSerializingEpoch(
                core,
                scheduler,
                serializingEpochCountBefore);
            Assert.True(core.Vmcs.HasLaunchedVmcs);
            Assert.Equal(PipelineState.GuestExecution, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(guestPc, core.ReadCommittedPc(vtId));
            Assert.Equal(guestPc, core.ReadActiveLivePc());
            Assert.Equal(guestSp, core.ReadArch(vtId, 2));
        }

        [Fact]
        public void DirectCompatVmResumeTransaction_EntersGuestExecutionAndPublishesVmTransitionSerializingBoundaryOnRealCore()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x72F2;
            const ulong guestPc = 0x9400;
            const ulong guestSp = 0x9500;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(vtId, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x2300);
            core.Vmcs.MarkLaunched();
            core.Vmcs.WriteFieldValue(VmcsField.GuestPc, unchecked((long)guestPc));
            core.Vmcs.WriteFieldValue(VmcsField.GuestSp, unchecked((long)guestSp));

            var dispatcher = new ExecutionDispatcherV4(
                csrFile: core.Csr,
                vmxUnit: new VmxExecutionUnit(core.Csr, core.Vmcs));
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.VMRESUME),
                state,
                bundleSerial: 35,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);

            AssertVmTransitionReplayPublicationWithSerializingEpoch(
                core,
                scheduler,
                serializingEpochCountBefore);
            Assert.True(core.Vmcs.HasLaunchedVmcs);
            Assert.Equal(PipelineState.GuestExecution, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(guestPc, core.ReadCommittedPc(vtId));
            Assert.Equal(guestPc, core.ReadActiveLivePc());
            Assert.Equal(guestSp, core.ReadArch(vtId, 2));
        }

        [Fact]
        public void DirectCompatVmReadTransaction_WritesDestinationAndPublishesSerializingBoundaryOnRealCore()
        {
            const int vtId = 1;
            const ulong retiredPc = 0x72F0;
            const byte fieldSelectorRegister = 1;
            const byte destinationRegister = 5;
            const ulong vmreadValue = 0x8877_6655_4433_2211UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(vtId, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x1000);
            core.Vmcs.WriteFieldValue(VmcsField.HostPc, unchecked((long)vmreadValue));
            core.WriteCommittedArch(vtId, fieldSelectorRegister, (ulong)VmcsField.HostPc);

            var dispatcher = new ExecutionDispatcherV4(
                csrFile: core.Csr,
                vmxUnit: new VmxExecutionUnit(core.Csr, core.Vmcs));
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.VMREAD, rd: destinationRegister, rs1: fieldSelectorRegister),
                state,
                bundleSerial: 31,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(vmreadValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectCompatVmWriteTransaction_WritesVmcsAndPublishesSerializingBoundaryOnRealCore()
        {
            const int vtId = 1;
            const ulong retiredPc = 0x72F4;
            const byte fieldSelectorRegister = 1;
            const byte valueRegister = 2;
            const ulong vmwriteValue = 0x1122_3344_5566_7788UL;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(vtId, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x1100);
            core.WriteCommittedArch(vtId, fieldSelectorRegister, (ulong)VmcsField.HostPc);
            core.WriteCommittedArch(vtId, valueRegister, vmwriteValue);

            var dispatcher = new ExecutionDispatcherV4(
                csrFile: core.Csr,
                vmxUnit: new VmxExecutionUnit(core.Csr, core.Vmcs));
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.VMWRITE, rs1: fieldSelectorRegister, rs2: valueRegister),
                state,
                bundleSerial: 32,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(unchecked((long)vmwriteValue), core.Vmcs.ReadFieldValue(VmcsField.HostPc).Value);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Theory]
        [InlineData(InstructionsEnum.VMPTRLD, false)]
        [InlineData(InstructionsEnum.VMCLEAR, true)]
        public void DirectCompatVmxPointerTransaction_PublishesSerializingBoundaryOnRealCore(
            InstructionsEnum opcode,
            bool startWithActiveVmcs)
        {
            const int vtId = 0;
            const ulong retiredPc = 0x72F8;
            const byte pointerRegister = 1;
            const ulong vmcsPointer = 0x2000;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(vtId, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            if (startWithActiveVmcs)
            {
                core.Vmcs.LoadPointer(vmcsPointer);
                core.Vmcs.MarkLaunched();
            }

            core.WriteCommittedArch(vtId, pointerRegister, vmcsPointer);

            var dispatcher = new ExecutionDispatcherV4(
                csrFile: core.Csr,
                vmxUnit: new VmxExecutionUnit(core.Csr, core.Vmcs));
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(opcode, rs1: pointerRegister),
                state,
                bundleSerial: 33,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);

            AssertReplaySerializingBoundaryPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            if (opcode == InstructionsEnum.VMPTRLD)
            {
                Assert.True(core.Vmcs.HasActiveVmcs);
                Assert.False(core.Vmcs.HasLaunchedVmcs);
            }
            else
            {
                Assert.False(core.Vmcs.HasActiveVmcs);
                Assert.False(core.Vmcs.HasLaunchedVmcs);
            }
        }

        [Fact]
        public void DirectCompatSystemTransaction_AppliesMretOnRealCore()
        {
            const int vtId = 0;
            const ulong trapHandlerPc = 0x9000;
            const ulong returnPc = 0x4320;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(trapHandlerPc, vtId);
            core.WriteCommittedPc(vtId, trapHandlerPc);
            core.Csr.Write(CsrAddresses.Mepc, returnPc, PrivilegeLevel.Machine);
            MicroOpScheduler scheduler =
                PrimeReplaySchedulerForSerializingBoundary(
                    ref core,
                    trapHandlerPc,
                    out long serializingEpochCountBefore);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.MRET,
                Class = InstructionClassifier.GetClass(InstructionsEnum.MRET),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.MRET),
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 29,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, transaction.PipelineEventOrderGuarantee);
            Assert.Equal(trapHandlerPc, transaction.PipelineEventRetiredPc);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.IsType<MretEvent>(transaction.PipelineEvent);

            AssertTrapBoundaryReplayPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(returnPc, core.ReadCommittedPc(vtId));
            Assert.Equal(returnPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        }

        [Fact]
        public void DirectCompatSretTransaction_RestoresSupervisorReturnPcAndPublishesTrapBoundaryOnRealCore()
        {
            const int vtId = 1;
            const ulong trapHandlerPc = 0x9040;
            const ulong returnPc = 0x4360;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(trapHandlerPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, trapHandlerPc);
            core.ArchContexts[vtId].CurrentPrivilege = PrivilegeLevel.Supervisor;
            core.Csr.Write(CsrAddresses.Sepc, returnPc, PrivilegeLevel.Supervisor);

            MicroOpScheduler scheduler =
                PrimeReplaySchedulerForSerializingBoundary(
                    ref core,
                    trapHandlerPc,
                    out long serializingEpochCountBefore);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.SRET),
                state,
                bundleSerial: 30,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, transaction.PipelineEventOrderGuarantee);
            Assert.Equal(trapHandlerPc, transaction.PipelineEventRetiredPc);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.IsType<SretEvent>(transaction.PipelineEvent);

            AssertTrapBoundaryReplayPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(returnPc, core.ReadCommittedPc(vtId));
            Assert.Equal(returnPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        }

        [Fact]
        public void DirectCompatEcallTransaction_EntersMachineTrapAndPublishesTrapBoundaryOnRealCore()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x9080;
            const ulong trapHandlerPc = 0x1200;
            const long ecallCode = 93;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, 17, (ulong)ecallCode);
            core.Csr.Write(CsrAddresses.Mtvec, trapHandlerPc, PrivilegeLevel.Machine);

            MicroOpScheduler scheduler =
                PrimeReplaySchedulerForSerializingBoundary(
                    ref core,
                    retiredPc,
                    out long serializingEpochCountBefore);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.ECALL),
                state,
                bundleSerial: 31,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, transaction.PipelineEventOrderGuarantee);
            Assert.Equal(retiredPc, transaction.PipelineEventRetiredPc);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            EcallEvent ecallEvent = Assert.IsType<EcallEvent>(transaction.PipelineEvent);
            Assert.Equal(ecallCode, ecallEvent.EcallCode);

            AssertTrapBoundaryReplayPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(retiredPc, core.Csr.DirectRead(CsrAddresses.Mepc));
            Assert.Equal(11UL, core.Csr.DirectRead(CsrAddresses.Mcause));
            Assert.Equal(trapHandlerPc, core.ReadCommittedPc(vtId));
            Assert.Equal(trapHandlerPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        }

        [Fact]
        public void DirectCompatEbreakTransaction_EntersBreakpointTrapAndPublishesTrapBoundaryOnRealCore()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x90C0;
            const ulong trapHandlerPc = 0x1400;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.Csr.Write(CsrAddresses.Mtvec, trapHandlerPc, PrivilegeLevel.Machine);

            MicroOpScheduler scheduler =
                PrimeReplaySchedulerForSerializingBoundary(
                    ref core,
                    retiredPc,
                    out long serializingEpochCountBefore);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.EBREAK),
                state,
                bundleSerial: 32,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, transaction.PipelineEventOrderGuarantee);
            Assert.Equal(retiredPc, transaction.PipelineEventRetiredPc);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.IsType<EbreakEvent>(transaction.PipelineEvent);

            AssertTrapBoundaryReplayPublication(core, scheduler, serializingEpochCountBefore);
            Assert.Equal(retiredPc, core.Csr.DirectRead(CsrAddresses.Mepc));
            Assert.Equal(3UL, core.Csr.DirectRead(CsrAddresses.Mcause));
            Assert.Equal(trapHandlerPc, core.ReadCommittedPc(vtId));
            Assert.Equal(trapHandlerPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        }

        [Fact]
        public void DirectCompatLoadTransaction_AppliesCommittedRegisterWriteOnRealCore()
        {
            const int vtId = 1;
            const ulong baseAddress = 0x280;
            const ulong loadedValue = 0x0102_0304_0506_0708UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, baseAddress);

            byte[] bytes = BitConverter.GetBytes(loadedValue);
            Processor.MainMemory.WriteToPosition(bytes, baseAddress + 8);

            var dispatcher = new ExecutionDispatcherV4(memoryUnit: new MemoryUnit(new MainMemoryBus()));
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.LD,
                Class = InstructionClassifier.GetClass(InstructionsEnum.LD),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.LD),
                Rd = 9,
                Rs1 = 1,
                Rs2 = 0,
                Imm = 8
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 43,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.None, transaction.TypedEffectKind);
            Assert.False(transaction.HasTypedEffect);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.Equal(1, transaction.RetireRecordCount);
            RetireRecord retireRecord = transaction.GetRetireRecord(0);
            Assert.True(retireRecord.IsRegisterWrite);
            Assert.Equal(vtId, retireRecord.VtId);
            Assert.Equal(9, retireRecord.ArchReg);
            Assert.Equal(loadedValue, retireRecord.Value);

            Assert.Equal(loadedValue, core.ReadArch(vtId, 9));
        }

        [Fact]
        public void DirectCompatLoadTransaction_WhenReplayPhaseIsActive_PreservesReplayAndSchedulerStateWithoutBoundaryPromotion()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x72E2;
            const ulong baseAddress = 0x2A0;
            const ulong loadedValue = 0x8877_6655_4433_2211UL;
            const ulong originalDestinationValue = 0x1234_5678_9ABC_DEF0UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, 1, baseAddress);
            core.WriteCommittedArch(vtId, 9, originalDestinationValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(loadedValue), baseAddress + 8);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var dispatcher = new ExecutionDispatcherV4(memoryUnit: new MemoryUnit(new MainMemoryBus()));
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.LD,
                Class = InstructionClassifier.GetClass(InstructionsEnum.LD),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.LD),
                Rd = 9,
                Rs1 = 1,
                Rs2 = 0,
                Imm = 8
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 45,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.None, transaction.TypedEffectKind);
            Assert.False(transaction.HasTypedEffect);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.Equal(1, transaction.RetireRecordCount);
            RetireRecord retireRecord = transaction.GetRetireRecord(0);
            Assert.True(retireRecord.IsRegisterWrite);
            Assert.Equal(vtId, retireRecord.VtId);
            Assert.Equal(9, retireRecord.ArchReg);
            Assert.Equal(loadedValue, retireRecord.Value);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(loadedValue, core.ReadArch(vtId, 9));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectCompatStoreTransaction_AppliesRetiredStoreCommitOnRealCore()
        {
            const int vtId = 0;
            const ulong storeAddress = 0x340;
            const ulong storeValue = 0x8877_6655_4433_2211UL;
            byte[] baseline = new byte[8];

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            Processor.MainMemory.WriteToPosition(baseline, storeAddress);

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, storeAddress);
            core.WriteCommittedArch(vtId, 2, storeValue);

            var dispatcher = new ExecutionDispatcherV4(memoryUnit: new MemoryUnit(new MainMemoryBus()));
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.SD,
                Class = InstructionClassifier.GetClass(InstructionsEnum.SD),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.SD),
                Rd = 0,
                Rs1 = 1,
                Rs2 = 2,
                Imm = 0
            };

            var transaction = ResolveDispatcherRetireWindowPublication(
                dispatcher,
                ir,
                state,
                bundleSerial: 47,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.ScalarMemoryStore, transaction.TypedEffectKind);

            byte[] bufferBeforeApply = Processor.MainMemory.ReadFromPosition(new byte[8], storeAddress, 8);
            Assert.Equal(baseline, bufferBeforeApply);

            RetireWindowCaptureTestHelper.ApplyExecutionDispatcherRetireWindowPublications(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 47,
                vtId: (byte)vtId);

            byte[] bufferAfterApply = Processor.MainMemory.ReadFromPosition(new byte[8], storeAddress, 8);
            Assert.Equal(storeValue, BitConverter.ToUInt64(bufferAfterApply, 0));
        }

        [Fact]
        public void DirectCompatStoreTransaction_WhenReplayPhaseIsActive_PreservesReplayAndSchedulerStateWithoutBoundaryPromotion()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x72E8;
            const ulong baseAddress = 0x320;
            const ulong storeValue = 0x1122_3344_5566_7788UL;
            const ulong targetAddress = baseAddress + 8;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            byte[] baseline = { 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };
            Processor.MainMemory.WriteToPosition(baseline, targetAddress);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, 1, baseAddress);
            core.WriteCommittedArch(vtId, 2, storeValue);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var dispatcher = new ExecutionDispatcherV4(memoryUnit: new MemoryUnit(new MainMemoryBus()));
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.SD,
                Class = InstructionClassifier.GetClass(InstructionsEnum.SD),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.SD),
                Rd = 0,
                Rs1 = 1,
                Rs2 = 2,
                Imm = 8
            };

            var transaction = ResolveDispatcherRetireWindowPublication(
                dispatcher,
                ir,
                state,
                bundleSerial: 47,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.ScalarMemoryStore, transaction.TypedEffectKind);
            Assert.True(transaction.HasTypedEffect);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.Equal(0, transaction.RetireRecordCount);
            Assert.Equal(baseline, Processor.MainMemory.ReadFromPosition(new byte[8], targetAddress, 8));

            RetireWindowCaptureTestHelper.ApplyExecutionDispatcherRetireWindowPublications(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 47,
                vtId: (byte)vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(
                storeValue,
                BitConverter.ToUInt64(Processor.MainMemory.ReadFromPosition(new byte[8], targetAddress, 8), 0));
        }

        [Fact]
        public void DirectCompatJalrTransaction_WhenReplayPhaseIsActive_PublishesRetireOwnedRedirectAndInvalidatesReplay()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x72F0;
            const byte destinationRegister = 7;
            const byte baseRegister = 6;
            const ulong baseValue = 0x5003;
            const ulong originalLinkValue = 0xDEAD_BEEF_0123_4567UL;
            const long immediate = 0x24;
            ulong targetPc = (baseValue + (ulong)immediate) & ~1UL;
            ulong linkValue = retiredPc + 4;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, destinationRegister, originalLinkValue);
            core.WriteCommittedArch(vtId, baseRegister, baseValue);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.JALR,
                Class = InstructionClassifier.GetClass(InstructionsEnum.JALR),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.JALR),
                Rd = destinationRegister,
                Rs1 = baseRegister,
                Rs2 = 0,
                Imm = immediate
            };

            var transaction = ResolveDispatcherRetireWindowPublication(
                dispatcher,
                ir,
                state,
                bundleSerial: 49,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.None, transaction.TypedEffectKind);
            Assert.False(transaction.HasTypedEffect);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.Equal(2, transaction.RetireRecordCount);
            RetireRecord linkWriteRecord = transaction.GetRetireRecord(0);
            Assert.True(linkWriteRecord.IsRegisterWrite);
            Assert.Equal(vtId, linkWriteRecord.VtId);
            Assert.Equal(destinationRegister, linkWriteRecord.ArchReg);
            Assert.Equal(linkValue, linkWriteRecord.Value);
            RetireRecord pcWriteRecord = transaction.GetRetireRecord(1);
            Assert.True(pcWriteRecord.IsPcWrite);
            Assert.Equal(vtId, pcWriteRecord.VtId);
            Assert.Equal(targetPc, pcWriteRecord.Value);

            Assert.Equal(originalLinkValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            RetireWindowCaptureTestHelper.ApplyExecutionDispatcherRetireWindowPublications(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 49,
                vtId: (byte)vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.PipelineFlush, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(linkValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(targetPc, core.ReadCommittedPc(vtId));
            Assert.Equal(targetPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void DirectCompatJalTransaction_WhenReplayPhaseIsActive_PublishesRetireOwnedRedirectAndInvalidatesReplay()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x7310;
            const byte destinationRegister = 5;
            const ulong originalLinkValue = 0xCAFE_BABE_1234_5678UL;
            const long immediate = 0x28;
            ulong targetPc = retiredPc + (ulong)immediate;
            ulong linkValue = retiredPc + 4;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, destinationRegister, originalLinkValue);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.JAL,
                Class = InstructionClassifier.GetClass(InstructionsEnum.JAL),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.JAL),
                Rd = destinationRegister,
                Rs1 = 0,
                Rs2 = 0,
                Imm = immediate
            };

            var transaction = ResolveDispatcherRetireWindowPublication(
                dispatcher,
                ir,
                state,
                bundleSerial: 50,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.None, transaction.TypedEffectKind);
            Assert.False(transaction.HasTypedEffect);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.Equal(2, transaction.RetireRecordCount);
            RetireRecord linkWriteRecord = transaction.GetRetireRecord(0);
            Assert.True(linkWriteRecord.IsRegisterWrite);
            Assert.Equal(vtId, linkWriteRecord.VtId);
            Assert.Equal(destinationRegister, linkWriteRecord.ArchReg);
            Assert.Equal(linkValue, linkWriteRecord.Value);
            RetireRecord pcWriteRecord = transaction.GetRetireRecord(1);
            Assert.True(pcWriteRecord.IsPcWrite);
            Assert.Equal(vtId, pcWriteRecord.VtId);
            Assert.Equal(targetPc, pcWriteRecord.Value);

            Assert.Equal(originalLinkValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            RetireWindowCaptureTestHelper.ApplyExecutionDispatcherRetireWindowPublications(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 50,
                vtId: (byte)vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.PipelineFlush, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(linkValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(targetPc, core.ReadCommittedPc(vtId));
            Assert.Equal(targetPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void DirectCompatBeqTransaction_WhenReplayPhaseIsActive_PublishesRetireOwnedRedirectAndInvalidatesReplay()
        {
            const int vtId = 0;
            const ulong retiredPc = 0x72F4;
            const byte leftRegister = 3;
            const byte rightRegister = 4;
            const ulong compareValue = 0x4455_6677_8899_AABBUL;
            const long immediate = 0x40;
            ulong targetPc = retiredPc + (ulong)immediate;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, leftRegister, compareValue);
            core.WriteCommittedArch(vtId, rightRegister, compareValue);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.BEQ,
                Class = InstructionClassifier.GetClass(InstructionsEnum.BEQ),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.BEQ),
                Rd = 0,
                Rs1 = leftRegister,
                Rs2 = rightRegister,
                Imm = immediate
            };

            var transaction = ResolveDispatcherRetireWindowPublication(
                dispatcher,
                ir,
                state,
                bundleSerial: 51,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.None, transaction.TypedEffectKind);
            Assert.False(transaction.HasTypedEffect);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.Equal(1, transaction.RetireRecordCount);
            RetireRecord pcWriteRecord = transaction.GetRetireRecord(0);
            Assert.True(pcWriteRecord.IsPcWrite);
            Assert.Equal(vtId, pcWriteRecord.VtId);
            Assert.Equal(targetPc, pcWriteRecord.Value);

            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            RetireWindowCaptureTestHelper.ApplyExecutionDispatcherRetireWindowPublications(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 51,
                vtId: (byte)vtId);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.PipelineFlush, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(targetPc, core.ReadCommittedPc(vtId));
            Assert.Equal(targetPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Theory]
        [InlineData(InstructionsEnum.BEQ, 0x1010UL, 0x2020UL)]
        [InlineData(InstructionsEnum.BNE, 0x3030UL, 0x3030UL)]
        [InlineData(InstructionsEnum.BLTU, ulong.MaxValue, 0UL)]
        [InlineData(InstructionsEnum.BGEU, 0UL, ulong.MaxValue)]
        public void DirectCompatConditionalBranchNotTakenTransaction_WhenReplayPhaseIsActive_PreservesReplayWithoutBoundaryPromotion(
            InstructionsEnum opcode,
            ulong leftValue,
            ulong rightValue)
        {
            const int vtId = 0;
            const ulong retiredPc = 0x7330;
            const byte leftRegister = 8;
            const byte rightRegister = 9;
            const long immediate = 0x44;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, leftRegister, leftValue);
            core.WriteCommittedArch(vtId, rightRegister, rightValue);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = opcode,
                Class = InstructionClassifier.GetClass(opcode),
                SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
                Rd = 0,
                Rs1 = leftRegister,
                Rs2 = rightRegister,
                Imm = immediate
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 52,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.None, transaction.TypedEffectKind);
            Assert.False(transaction.HasTypedEffect);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.Equal(0, transaction.RetireRecordCount);
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
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
        public void DirectCompatAtomicWordTransaction_AppliesRetiredAtomicEffectOnRealCore(
            InstructionsEnum opcode)
        {
            const int vtId = 0;
            const ulong address = 0x380;
            const uint initialWord = 0xFFFF_FFF0U;
            const uint sourceWord = 0x0000_0010U;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, sourceWord);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(opcode, rd: 9, rs1: 1, rs2: 2),
                state,
                bundleSerial: 59,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Atomic, transaction.TypedEffectKind);
            Assert.Equal((byte)4, transaction.AtomicEffect.AccessSize);

            Assert.Equal(SignExtendWordToUlong(initialWord), core.ReadArch(vtId, 9));

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[4], address, 4);
            Assert.Equal(
                ComputeExpectedWordAtomicWrite(opcode, initialWord, sourceWord),
                BitConverter.ToUInt32(buffer, 0));
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
        public void DirectCompatAtomicDoublewordTransaction_AppliesRetiredAtomicEffectOnRealCore(
            InstructionsEnum opcode)
        {
            const int vtId = 0;
            const ulong address = 0x3C0;
            const ulong initialValue = 0xFFFF_FFFF_FFFF_FFF0UL;
            const ulong sourceValue = 0x0000_0000_0000_0010UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, sourceValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialValue), address);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(opcode, rd: 9, rs1: 1, rs2: 2),
                state,
                bundleSerial: 61,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Atomic, transaction.TypedEffectKind);
            Assert.Equal((byte)8, transaction.AtomicEffect.AccessSize);

            Assert.Equal(initialValue, core.ReadArch(vtId, 9));

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[8], address, 8);
            Assert.Equal(
                ComputeExpectedDwordAtomicWrite(opcode, initialValue, sourceValue),
                BitConverter.ToUInt64(buffer, 0));
        }

        [Fact]
        public void DirectCompatAtomicLrScTransactions_SucceedWithoutInterveningWrite()
        {
            const int vtId = 0;
            const ulong address = 0x400;
            const uint initialWord = 0x8765_4321U;
            const uint storeValue = 0xCAFE_BABEU;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, storeValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var lrTransaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.LR_W, rd: 5, rs1: 1),
                state,
                bundleSerial: 63,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Atomic, lrTransaction.TypedEffectKind);
            Assert.Equal(SignExtendWordToUlong(initialWord), core.ReadArch(vtId, 5));

            var scTransaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.SC_W, rd: 6, rs1: 1, rs2: 2),
                state,
                bundleSerial: 65,
                vtId: (byte)vtId);

            Assert.Equal(0UL, core.ReadArch(vtId, 6));

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[4], address, 4);
            Assert.Equal(storeValue, BitConverter.ToUInt32(buffer, 0));
        }

        [Fact]
        public void DirectCompatAtomicTransaction_WhenReplayPhaseIsActive_PreservesReplayAndSchedulerStateWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x72E8;
            const ulong address = 0x470;
            const uint initialWord = 0xFFFF_FFF0U;
            const uint sourceWord = 0x0000_0010U;
            const ulong originalDestinationValue = 0x1234_5678_9ABC_DEF0UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, sourceWord);
            core.WriteCommittedArch(vtId, 9, originalDestinationValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.AMOADD_W, rd: 9, rs1: 1, rs2: 2),
                state,
                bundleSerial: 71,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Atomic, transaction.TypedEffectKind);
            Assert.True(transaction.HasTypedEffect);
            Assert.True(transaction.HasAtomicEffect);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.Equal(vtId, transaction.AtomicEffect.VirtualThreadId);
            Assert.Equal(address, transaction.AtomicEffect.Address);
            Assert.Equal(sourceWord, transaction.AtomicEffect.SourceValue);
            Assert.Equal((ushort)9, transaction.AtomicEffect.DestinationRegister);
            Assert.Equal((byte)4, transaction.AtomicEffect.AccessSize);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(SignExtendWordToUlong(initialWord), core.ReadArch(vtId, 9));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[4], address, 4);
            Assert.Equal(
                ComputeExpectedWordAtomicWrite(InstructionsEnum.AMOADD_W, initialWord, sourceWord),
                BitConverter.ToUInt32(buffer, 0));
        }

        [Fact]
        public void DirectCompatAtomicScTransaction_FailsAfterOverlappingPhysicalWriteInvalidatesReservation()
        {
            const int vtId = 0;
            const ulong address = 0x440;
            const uint initialWord = 0x1122_3344U;
            const uint interveningValue = 0x5566_7788U;
            const uint storeValue = 0xCAFE_BABEU;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, storeValue);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);

            var lrTransaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.LR_W, rd: 5, rs1: 1),
                state,
                bundleSerial: 67,
                vtId: (byte)vtId);

            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(interveningValue), address);

            var scTransaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.SC_W, rd: 6, rs1: 1, rs2: 2),
                state,
                bundleSerial: 69,
                vtId: (byte)vtId);

            Assert.Equal(1UL, core.ReadArch(vtId, 6));

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[4], address, 4);
            Assert.Equal(interveningValue, BitConverter.ToUInt32(buffer, 0));
        }

        [Fact]
        public void AtomicMicroOp_Execute_ResolvesRetireEffectWithoutMutatingMemory()
        {
            const ulong address = 0x480;
            const uint initialWord = 0xFFFF_FFF0U;
            const uint sourceWord = 0x0000_0010U;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(0, 1, address);
            core.WriteCommittedArch(0, 2, sourceWord);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            var microOp = new AtomicMicroOp
            {
                OpCode = (uint)InstructionsEnum.AMOADD_W,
                DestRegID = 9,
                BaseRegID = 1,
                SrcRegID = 2,
                Size = 4,
                WritesRegister = true
            };
            microOp.InitializeMetadata();

            Assert.True(microOp.Execute(ref core));

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[4], address, 4);
            Assert.Equal(initialWord, BitConverter.ToUInt32(buffer, 0));

            AtomicRetireEffect retireEffect = microOp.CreateRetireEffect();
            Assert.True(retireEffect.IsValid);
            Assert.Equal(InstructionsEnum.AMOADD_W, (InstructionsEnum)retireEffect.Opcode);
            Assert.Equal(address, retireEffect.Address);
            Assert.Equal(sourceWord, retireEffect.SourceValue);
            Assert.Equal((ushort)9, retireEffect.DestinationRegister);
        }

        [Fact]
        public void AtomicMicroOp_RetiresThroughExplicitPacketLaneOnRealCore()
        {
            const ulong address = 0x4C0;
            const uint initialWord = 0xFFFF_FFF0U;
            const uint sourceWord = 0x0000_0010U;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(0, 1, address);
            core.WriteCommittedArch(0, 2, sourceWord);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            var microOp = new AtomicMicroOp
            {
                OpCode = (uint)InstructionsEnum.AMOADD_W,
                DestRegID = 9,
                BaseRegID = 1,
                SrcRegID = 2,
                Size = 4,
                WritesRegister = true
            };
            microOp.InitializeMetadata();

            core.TestPrepareExplicitPacketAtomicForWriteBack(
                laneIndex: 4,
                microOp,
                pc: 0x7600,
                vtId: 0);
            core.TestRunWriteBackStage();

            Assert.Equal(SignExtendWordToUlong(initialWord), core.ReadArch(0, 9));

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[4], address, 4);
            Assert.Equal(
                ComputeExpectedWordAtomicWrite(InstructionsEnum.AMOADD_W, initialWord, sourceWord),
                BitConverter.ToUInt32(buffer, 0));

            PipelineControl control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void DirectCompatWfeTransaction_TransitionsToWaitForEventOnRealCore()
        {
            const int vtId = 1;
            const ulong retiredPc = 0x7000;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(vtId);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.WFE,
                Class = InstructionClassifier.GetClass(InstructionsEnum.WFE),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.WFE),
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 31,
                vtId: (byte)vtId);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, transaction.PipelineEventOrderGuarantee);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.IsType<WfeEvent>(transaction.PipelineEvent);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.WaitForEvent, core.ReadVirtualThreadPipelineState(vtId));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectCompatSevTransaction_WakesWaitingVirtualThreadOnRealCore()
        {
            const ulong issuerPc = 0x7100;
            const ulong waitingPc = 0x7200;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(issuerPc, activeVtId: 0);
            core.WriteCommittedPc(0, issuerPc);
            core.WriteCommittedPc(1, waitingPc);
            core.WriteVirtualThreadPipelineState(1, PipelineState.WaitForEvent);
            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                issuerPc,
                out long serializingEpochCountBefore);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(0);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.SEV,
                Class = InstructionClassifier.GetClass(InstructionsEnum.SEV),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.SEV),
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 37,
                vtId: 0);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, transaction.PipelineEventOrderGuarantee);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.IsType<SevEvent>(transaction.PipelineEvent);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(1));
            Assert.Equal(issuerPc, core.ReadCommittedPc(0));
            Assert.Equal(waitingPc, core.ReadCommittedPc(1));
        }

        [Fact]
        public void DirectCompatPodBarrierTransaction_PublishesSerializingBoundaryOnRealCore()
        {
            const ulong retiredPc = 0x72C0;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: 0);
            core.WriteCommittedPc(0, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(0);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.POD_BARRIER,
                Class = InstructionClassifier.GetClass(InstructionsEnum.POD_BARRIER),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.POD_BARRIER),
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 49,
                vtId: 0);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.None, transaction.PipelineEventOrderGuarantee);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.IsType<PodBarrierEvent>(transaction.PipelineEvent);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(retiredPc, core.ReadCommittedPc(0));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectCompatVtBarrierTransaction_PublishesSerializingBoundaryOnRealCore()
        {
            const ulong retiredPc = 0x72E0;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: 0);
            core.WriteCommittedPc(0, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(0);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.VT_BARRIER),
                state,
                bundleSerial: 51,
                vtId: 0);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.None, transaction.PipelineEventOrderGuarantee);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.IsType<VtBarrierEvent>(transaction.PipelineEvent);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(retiredPc, core.ReadCommittedPc(0));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectCompatYieldTransaction_PreservesReplayAndSchedulerStateWithoutBoundaryPromotion()
        {
            const ulong retiredPc = 0x72E4;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: 0);
            core.WriteCommittedPc(0, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(0);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.YIELD),
                state,
                bundleSerial: 47,
                vtId: 0);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.None, transaction.PipelineEventOrderGuarantee);
            Assert.False(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.IsType<YieldEvent>(transaction.PipelineEvent);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(retiredPc, core.ReadCommittedPc(0));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectCompatFenceTransaction_DrainsMemoryWithoutPublishingSerializingReplayBoundary()
        {
            const ulong retiredPc = 0x72E8;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: 0);
            core.WriteCommittedPc(0, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(0);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.FENCE),
                state,
                bundleSerial: 50,
                vtId: 0);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, transaction.PipelineEventOrderGuarantee);
            Assert.False(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            FenceEvent fenceEvent = Assert.IsType<FenceEvent>(transaction.PipelineEvent);
            Assert.False(fenceEvent.IsInstructionFence);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(AssistInvalidationReason.Fence, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(retiredPc, core.ReadCommittedPc(0));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Fact]
        public void DirectCompatFenceITransaction_FlushesPipelineAndPreservesFenceSpecificAssistInvalidation()
        {
            const ulong retiredPc = 0x72F0;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: 0);
            core.WriteCommittedPc(0, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(0);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.FENCE_I),
                state,
                bundleSerial: 52,
                vtId: 0);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.FlushPipeline, transaction.PipelineEventOrderGuarantee);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            FenceEvent fenceEvent = Assert.IsType<FenceEvent>(transaction.PipelineEvent);
            Assert.True(fenceEvent.IsInstructionFence);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.Fence, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(retiredPc, core.ReadCommittedPc(0));
        }

        [Fact]
        public void DirectCompatWfiTransaction_TransitionsToHaltedAndPublishesSerializingReplayBoundary()
        {
            const ulong retiredPc = 0x72F8;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: 0);
            core.WriteCommittedPc(0, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(0);

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                CreateInstructionIr(InstructionsEnum.WFI),
                state,
                bundleSerial: 54,
                vtId: 0);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, transaction.PipelineEventOrderGuarantee);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.IsType<WfiEvent>(transaction.PipelineEvent);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(PipelineState.Halted, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(retiredPc, core.ReadCommittedPc(0));
        }

        [Fact]
        public void DirectCompatStreamWaitTransaction_PublishesSerializingBoundaryOnRealCore()
        {
            const ulong retiredPc = 0x7300;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, activeVtId: 0);
            core.WriteCommittedPc(0, retiredPc);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: retiredPc,
                totalIterations: 8,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(0);
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.STREAM_WAIT,
                Class = InstructionClassifier.GetClass(InstructionsEnum.STREAM_WAIT),
                SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.STREAM_WAIT),
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0
            };

            var transaction = ResolveAndApplyDispatcherRetireWindowPublication(
                ref core,
                dispatcher,
                ir,
                state,
                bundleSerial: 53,
                vtId: 0);

            Assert.Equal(RetireWindowCaptureEffectKind.SerializingBoundary, transaction.TypedEffectKind);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(retiredPc, core.ReadCommittedPc(0));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
        }

        [Theory]
        [InlineData(InstructionsEnum.VADD)]
        [InlineData(InstructionsEnum.VSQRT)]
        public void ExecuteStreamInstruction_WhenScalarizedVectorOpcodeReachesDirectHelperSurface_ThenRejectsSyntheticGprRetireTruth(
            InstructionsEnum opcode)
        {
            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(0, 1, 7UL);
            core.WriteCommittedArch(0, 2, 5UL);
            core.WriteCommittedArch(0, 9, 0xABCDUL);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 2),
                StreamLength = 1
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.ExecuteDirectStreamCompat(inst));

            Assert.Contains("streamLength == 1 / IsScalar", ex.Message, StringComparison.Ordinal);
            Assert.Contains("vector compute opcodes must use the canonical vector carrier path", ex.Message, StringComparison.Ordinal);
            Assert.Equal(0xABCDUL, core.ReadArch(0, 9));
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenVpopcWouldWriteScalar_ThenPublishesThroughRetireWindowWithoutCompatApply()
        {
            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(3, 0b1011_0101UL);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VPOPC,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                Immediate = (ushort)(3 | (6 << 8)),
                StreamLength = 8
            };

            core.ExecuteDirectStreamCompat(inst);

            Assert.Equal(5UL, core.ReadArch(0, 6));
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenSingleElementVpopcWouldWriteScalar_ThenPublishesThroughRetireWindowWithoutCompatApply()
        {
            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(3, 0b1011_0101UL);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VPOPC,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                Immediate = (ushort)(3 | (6 << 8)),
                StreamLength = 1
            };

            core.ExecuteDirectStreamCompat(inst);

            Assert.Equal(1UL, core.ReadArch(0, 6));
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenZeroLengthVpopcReachesDirectCompatContour_ThenRejectsSilentNoOpFallback()
        {
            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(3, 0b1011_0101UL);
            core.WriteCommittedArch(0, 6, 17UL);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VPOPC,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                Immediate = (ushort)(3 | (6 << 8)),
                StreamLength = 0
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.ExecuteDirectStreamCompat(inst));

            Assert.Contains("StreamLength == 0", ex.Message, StringComparison.Ordinal);
            Assert.Contains("implicit success/no-op", ex.Message, StringComparison.Ordinal);
            Assert.Equal(17UL, core.ReadArch(0, 6));
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenScalarShapedMemoryOpcodeReachesDirectCompatContour_ThenRejectsZeroWriteFallback()
        {
            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(0, 9, 17UL);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.LW,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 0),
                Src2Pointer = 0x40,
                StreamLength = 1
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.ExecuteDirectStreamCompat(inst));

            Assert.Contains("scalar direct stream helper contour", ex.Message, StringComparison.Ordinal);
            Assert.Contains("authoritative scalar retire/apply contract", ex.Message, StringComparison.Ordinal);
            Assert.Contains("must not synthesize scalar retire truth", ex.Message, StringComparison.Ordinal);
            Assert.Equal(17UL, core.ReadArch(0, 9));
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenScalarShapedControlOpcodeReachesDirectCompatContour_ThenRejectsZeroWriteFallback()
        {
            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(0, 7, 23UL);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.JALR,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(7, 1, 0),
                Src2Pointer = 0,
                StreamLength = 1
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.ExecuteDirectStreamCompat(inst));

            Assert.Contains("scalar direct stream helper contour", ex.Message, StringComparison.Ordinal);
            Assert.Contains("authoritative scalar retire/apply contract", ex.Message, StringComparison.Ordinal);
            Assert.Contains("must not synthesize scalar retire truth", ex.Message, StringComparison.Ordinal);
            Assert.Equal(23UL, core.ReadArch(0, 7));
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenComparisonWouldWritePredicateState_ThenPublishesThroughRetireWindowWithoutCompatApply()
        {
            const ulong lhsAddress = 0x200UL;
            const ulong rhsAddress = 0x300UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(lhsAddress, 3U, 7U);
            SeedVectorWordMemory(rhsAddress, 1U, 7U);

            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(5, 0xA5UL);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VCMPEQ,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = lhsAddress,
                Src2Pointer = rhsAddress,
                Immediate = 5,
                StreamLength = 2
            };

            core.ExecuteDirectStreamCompat(inst);

            Assert.Equal(0b10UL, core.GetPredicateRegister(5));
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenMaskManipWouldWritePredicateState_ThenPublishesThroughRetireWindowWithoutCompatApply()
        {
            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(1, 0b1010UL);
            core.SetPredicateRegister(2, 0b1100UL);
            core.SetPredicateRegister(3, 0x5AUL);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VMAND,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                Immediate = (ushort)(1 | (2 << 4) | (3 << 8)),
                StreamLength = 8
            };

            core.ExecuteDirectStreamCompat(inst);

            Assert.Equal(0b1000UL, core.GetPredicateRegister(3));
            Assert.Equal(0b1010UL, core.GetPredicateRegister(1));
            Assert.Equal(0b1100UL, core.GetPredicateRegister(2));
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenPureVectorMemoryOpNeedsMemoryVisibleCarrier_ThenRejectsDirectSurface()
        {
            const ulong baseAddress = 0x200UL;
            uint first = 0x0000_00FFU;
            uint second = 0x0F0F_0F0FU;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);

            byte[] seed = new byte[8];
            Array.Copy(BitConverter.GetBytes(first), 0, seed, 0, 4);
            Array.Copy(BitConverter.GetBytes(second), 0, seed, 4, 4);
            Processor.MainMemory.WriteToPosition(seed, baseAddress);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VNOT,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = baseAddress,
                StreamLength = 2,
                Stride = 4
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.ExecuteDirectStreamCompat(inst));

            Assert.Contains("memory-visible publication", ex.Message, StringComparison.Ordinal);

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[8], baseAddress, 8);

            Assert.Equal(first, BitConverter.ToUInt32(buffer, 0));
            Assert.Equal(second, BitConverter.ToUInt32(buffer, 4));
        }

        [Theory]
        [InlineData(InstructionsEnum.VGATHER)]
        [InlineData(InstructionsEnum.VSCATTER)]
        public void ExecuteStreamInstruction_WhenIndexedGatherScatterNeedsMemoryVisibleCarrier_ThenRejectsDirectSurface(
            InstructionsEnum opcode)
        {
            var core = new Processor.CPU_Core(0);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x240UL,
                Src2Pointer = 0x340UL,
                StreamLength = 2,
                Stride = 4,
                Indexed = true
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.ExecuteDirectStreamCompat(inst));

            Assert.Contains("memory-visible publication", ex.Message, StringComparison.Ordinal);
            Assert.Contains("pipeline execution remains the authoritative path", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenZeroLengthVectorMemoryOpReachesHelperSurface_ThenRejectsSilentNoOpFallback()
        {
            const ulong baseAddress = 0x280UL;
            uint first = 0x0000_00AAU;
            uint second = 0xF0F0_0F0FU;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);

            byte[] seed = new byte[8];
            Array.Copy(BitConverter.GetBytes(first), 0, seed, 0, 4);
            Array.Copy(BitConverter.GetBytes(second), 0, seed, 4, 4);
            Processor.MainMemory.WriteToPosition(seed, baseAddress);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VNOT,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = baseAddress,
                StreamLength = 0,
                Stride = 4
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.ExecuteDirectStreamCompat(inst));

            Assert.Contains("zero-length helper request", ex.Message, StringComparison.Ordinal);
            Assert.Contains("StreamEngine.Execute", ex.Message, StringComparison.Ordinal);

            byte[] buffer = Processor.MainMemory.ReadFromPosition(new byte[8], baseAddress, 8);

            Assert.Equal(first, BitConverter.ToUInt32(buffer, 0));
            Assert.Equal(second, BitConverter.ToUInt32(buffer, 4));
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenZeroLengthPredicateVectorOpReachesHelperSurface_ThenRejectsSilentNoOpFallback()
        {
            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(5, 0xA5UL);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VCMPEQ,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x200UL,
                Src2Pointer = 0x300UL,
                Immediate = 5,
                StreamLength = 0
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.ExecuteDirectStreamCompat(inst));

            Assert.Contains("zero-length helper request", ex.Message, StringComparison.Ordinal);
            Assert.Contains("StreamEngine.Execute", ex.Message, StringComparison.Ordinal);
            Assert.Equal(0xA5UL, core.GetPredicateRegister(5));
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorComparisonMicroOpCompletes_ThenPublishesPredicateRegisterWithoutMemoryWrite()
        {
            const ulong lhsAddress = 0x200UL;
            const ulong rhsAddress = 0x300UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(lhsAddress, 3U, 7U);
            SeedVectorWordMemory(rhsAddress, 1U, 7U);

            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(5, 0xA5UL);

            var inst = CreateVectorInstruction(
                InstructionsEnum.VCMPEQ,
                destSrc1Pointer: lhsAddress,
                src2Pointer: rhsAddress,
                immediate: 5,
                streamLength: 2,
                stride: 4);

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<VectorComparisonMicroOp>(microOp);

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: 0x1E00);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);
            Assert.Equal(0b10UL, core.GetPredicateRegister(5));

            byte[] lhsBytes = Processor.MainMemory.ReadFromPosition(new byte[8], lhsAddress, 8);
            Assert.Equal(3U, BitConverter.ToUInt32(lhsBytes, 0));
            Assert.Equal(7U, BitConverter.ToUInt32(lhsBytes, 4));
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorComparisonUsesPredicateOnlyContour_ThenRetiresWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x1E40;
            const ulong lhsAddress = 0x200UL;
            const ulong rhsAddress = 0x300UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(lhsAddress, 3U, 7U);
            SeedVectorWordMemory(rhsAddress, 1U, 7U);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.SetPredicateRegister(5, 0xA5UL);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VCMPEQ,
                destSrc1Pointer: lhsAddress,
                src2Pointer: rhsAddress,
                immediate: 5,
                streamLength: 2,
                stride: 4);

            VectorComparisonMicroOp microOp = Assert.IsType<VectorComparisonMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            Assert.Equal(0b10UL, core.GetPredicateRegister(5));
            byte[] lhsBytes = Processor.MainMemory.ReadFromPosition(new byte[8], lhsAddress, 8);
            Assert.Equal(3U, BitConverter.ToUInt32(lhsBytes, 0));
            Assert.Equal(7U, BitConverter.ToUInt32(lhsBytes, 4));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorMaskMicroOpCompletes_ThenPublishesPredicateRegisterWithoutMemoryShape()
        {
            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(1, 0b1010UL);
            core.SetPredicateRegister(2, 0b1100UL);
            core.SetPredicateRegister(3, 0x5AUL);

            var inst = CreateVectorInstruction(
                InstructionsEnum.VMAND,
                immediate: (ushort)(1 | (2 << 4) | (3 << 8)),
                streamLength: 8);

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            VectorMaskOpMicroOp maskMicroOp = Assert.IsType<VectorMaskOpMicroOp>(microOp);
            Assert.False(maskMicroOp.IsMemoryOp);
            Assert.False(maskMicroOp.AdmissionMetadata.IsMemoryOp);

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                maskMicroOp,
                isVectorOp: true,
                pc: 0x1F00);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);
            Assert.Equal(0b1000UL, core.GetPredicateRegister(3));
            Assert.Equal(0b1010UL, core.GetPredicateRegister(1));
            Assert.Equal(0b1100UL, core.GetPredicateRegister(2));
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorMaskUsesPredicateOnlyContour_ThenRetiresWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x1F40;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.SetPredicateRegister(1, 0b1010UL);
            core.SetPredicateRegister(2, 0b1100UL);
            core.SetPredicateRegister(3, 0x5AUL);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VMAND,
                immediate: (ushort)(1 | (2 << 4) | (3 << 8)),
                streamLength: 8);

            VectorMaskOpMicroOp microOp = Assert.IsType<VectorMaskOpMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            Assert.Equal(0b1000UL, core.GetPredicateRegister(3));
            Assert.Equal(0b1010UL, core.GetPredicateRegister(1));
            Assert.Equal(0b1100UL, core.GetPredicateRegister(2));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorComparisonMicroOpUsesUnsupportedElementDataType_ThenRejectsPredicateNoOpContour()
        {
            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(5, 0xA5UL);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VCMPEQ,
                destSrc1Pointer: 0x200UL,
                src2Pointer: 0x300UL,
                immediate: 5,
                streamLength: 2,
                stride: 4);
            inst.DataType = 0xFE;

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            TrapMicroOp trap = Assert.IsType<TrapMicroOp>(microOp);
            Assert.NotNull(trap.TrapReason);

            Assert.Equal(0xA5UL, core.GetPredicateRegister(5));
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorBinaryMicroOpUsesUnsupportedElementDataType_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VADD,
                destSrc1Pointer: 0x220UL,
                src2Pointer: 0x320UL,
                streamLength: 4,
                stride: 4);
            inst.DataType = 0xFE;

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<TrapMicroOp>(microOp);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorBinaryMicroOpUsesZeroLengthStream_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                    InstructionsEnum.VADD,
                    destSrc1Pointer: 0x220UL,
                    src2Pointer: 0x320UL,
                    streamLength: 0,
                    stride: 4);

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<VectorBinaryOpMicroOp>(microOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    inst,
                    microOp,
                    isVectorOp: true,
                    isMemoryOp: false,
                    pc: 0x2090));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("StreamLength == 0", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("vector compute contour", ex.InnerException.Message, StringComparison.Ordinal);
            Assert.Contains("hidden success/no-op", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorBinaryUsesMaskedUndisturbedLaneContour_ThenRetiresPreservedDestinationWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x2098;
            const ulong lhsAddress = 0x220;
            const ulong rhsAddress = 0x320;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(lhsAddress, 10U, 20U, 30U, 40U);
            SeedVectorWordMemory(rhsAddress, 1U, 2U, 3U, 4U);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.SetPredicateRegister(5, 0b1101UL);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VADD,
                destSrc1Pointer: lhsAddress,
                src2Pointer: rhsAddress,
                streamLength: 4,
                stride: 4,
                predicateMask: 5,
                dataType: DataTypeEnum.UINT32);

            VectorBinaryOpMicroOp microOp = Assert.IsType<VectorBinaryOpMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], lhsAddress, 16);
            Assert.Equal(11U, BitConverter.ToUInt32(resultBytes, 0));
            Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 4));
            Assert.Equal(33U, BitConverter.ToUInt32(resultBytes, 8));
            Assert.Equal(44U, BitConverter.ToUInt32(resultBytes, 12));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
        }

        [Theory]
        [InlineData(InstructionsEnum.VCOMPRESS)]
        [InlineData(InstructionsEnum.VEXPAND)]
        public void ExecuteStage_WhenMaterializedVectorPredicativeMovementMicroOpRunsOnRepresentable1DContour_ThenRetiresMemoryVisibleResultWithoutBoundaryPromotion(
            InstructionsEnum opcode)
        {
            const int vtId = 2;
            const ulong retiredPc = 0x20A0;
            const ulong vectorAddress = 0x260UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(vectorAddress, 10U, 20U, 30U, 40U);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.SetPredicateRegister(5, 0b1101UL);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                opcode,
                destSrc1Pointer: vectorAddress,
                streamLength: 4,
                stride: 4,
                predicateMask: 5,
                dataType: DataTypeEnum.UINT32);

            VectorPredicativeMovementMicroOp microOp = Assert.IsType<VectorPredicativeMovementMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
            if (opcode == InstructionsEnum.VCOMPRESS)
            {
                Assert.Equal(10U, BitConverter.ToUInt32(resultBytes, 0));
                Assert.Equal(30U, BitConverter.ToUInt32(resultBytes, 4));
                Assert.Equal(40U, BitConverter.ToUInt32(resultBytes, 8));
                Assert.Equal(40U, BitConverter.ToUInt32(resultBytes, 12));
            }
            else
            {
                Assert.Equal(10U, BitConverter.ToUInt32(resultBytes, 0));
                Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 4));
                Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 8));
                Assert.Equal(30U, BitConverter.ToUInt32(resultBytes, 12));
            }

            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
        }

        [Theory]
        [InlineData(InstructionsEnum.VCOMPRESS)]
        [InlineData(InstructionsEnum.VEXPAND)]
        public void ExecuteStage_WhenMaterializedVectorPredicativeMovementMicroOpExceedsScratchBackedFootprint_ThenFailsClosedBeforePublication(
            InstructionsEnum opcode)
        {
            var core = new Processor.CPU_Core(0);
            int maxRepresentableElements = Math.Min(
                core.GetScratchA().Length / sizeof(uint),
                core.GetScratchDst().Length / sizeof(uint));

            VLIW_Instruction inst = CreateVectorInstruction(
                opcode,
                destSrc1Pointer: 0x260UL,
                streamLength: (uint)(maxRepresentableElements + 1),
                stride: 4,
                predicateMask: 5,
                dataType: DataTypeEnum.UINT32);

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<VectorPredicativeMovementMicroOp>(microOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    inst,
                    microOp,
                    isVectorOp: true,
                    isMemoryOp: false,
                    pc: 0x20A0));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("authoritative mainline 1D single-surface contour", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("must fail closed instead of publishing partial compaction/expansion truth", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorUnaryMicroOpUsesUnsupportedElementDataType_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VSQRT,
                destSrc1Pointer: 0x240UL,
                streamLength: 4,
                stride: 4);
            inst.DataType = 0xFE;

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<TrapMicroOp>(microOp);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorUnaryMicroOpUsesZeroLengthStream_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VSQRT,
                destSrc1Pointer: 0x240UL,
                streamLength: 0,
                stride: 4);

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<VectorUnaryOpMicroOp>(microOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    inst,
                    microOp,
                    isVectorOp: true,
                    isMemoryOp: false,
                    pc: 0x20B0));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("StreamLength == 0", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("vector compute contour", ex.InnerException.Message, StringComparison.Ordinal);
            Assert.Contains("hidden success/no-op", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorUnaryUsesMaskedUndisturbedLaneContour_ThenRetiresPreservedDestinationWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x20B8;
            const ulong vectorAddress = 0x240;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(vectorAddress, 0x0000_000FU, 0x1234_5678U, 0xAAAA_AAAAU, 0x0000_FFFFU);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.SetPredicateRegister(5, 0b0101UL);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VNOT,
                destSrc1Pointer: vectorAddress,
                streamLength: 4,
                stride: 4,
                predicateMask: 5,
                dataType: DataTypeEnum.UINT32);

            VectorUnaryOpMicroOp microOp = Assert.IsType<VectorUnaryOpMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
            Assert.Equal(0xFFFF_FFF0U, BitConverter.ToUInt32(resultBytes, 0));
            Assert.Equal(0x1234_5678U, BitConverter.ToUInt32(resultBytes, 4));
            Assert.Equal(0x5555_5555U, BitConverter.ToUInt32(resultBytes, 8));
            Assert.Equal(0x0000_FFFFU, BitConverter.ToUInt32(resultBytes, 12));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
        }

        [Fact]
        public void Materialization_WhenVectorFmaMicroOpUsesDescriptorLessContour_ThenFailsClosedBeforeRuntime()
        {
            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VFMADD,
                destSrc1Pointer: 0x260UL,
                src2Pointer: 0UL,
                streamLength: 4,
                stride: 4);

            TrapMicroOp trap = Assert.IsType<TrapMicroOp>(MaterializeSingleSlotMicroOp(inst));
            Assert.Equal((uint)InstructionsEnum.VFMADD, trap.OpCode);
            Assert.False(trap.IsMemoryOp);
            Assert.False(trap.AdmissionMetadata.IsMemoryOp);
            Assert.Contains("descriptor-less tri-operand FMA publication", trap.TrapReason, StringComparison.Ordinal);
            Assert.Contains("authoritative descriptor-backed contour", trap.TrapReason, StringComparison.Ordinal);
            Assert.Contains("TriOpDesc", trap.TrapReason, StringComparison.Ordinal);
        }

        [Fact]
        public void Materialization_WhenVectorFmaMicroOpUsesUnsupportedElementDataType_ThenFailsClosedBeforeRuntime()
        {
            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VFMADD,
                destSrc1Pointer: 0x260UL,
                src2Pointer: 0x360UL,
                streamLength: 4,
                stride: 4);
            inst.DataType = 0xFE;

            TrapMicroOp trap = Assert.IsType<TrapMicroOp>(MaterializeSingleSlotMicroOp(inst));
            Assert.Equal((uint)InstructionsEnum.VFMADD, trap.OpCode);
            Assert.False(trap.IsMemoryOp);
            Assert.False(trap.AdmissionMetadata.IsMemoryOp);
            Assert.Contains("unsupported element DataType", trap.TrapReason, StringComparison.Ordinal);
            Assert.Contains("authoritative mainline vector publication contour", trap.TrapReason, StringComparison.Ordinal);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorFmaMicroOpUsesZeroLengthStream_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VFMADD,
                destSrc1Pointer: 0x260UL,
                src2Pointer: 0x360UL,
                streamLength: 0,
                stride: 4);

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<VectorFmaMicroOp>(microOp);
            Assert.False(microOp.IsMemoryOp);
            Assert.False(microOp.AdmissionMetadata.IsMemoryOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    inst,
                    microOp,
                    isVectorOp: true,
                    isMemoryOp: false,
                    pc: 0x20D0));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("StreamLength == 0", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("vector compute contour", ex.InnerException.Message, StringComparison.Ordinal);
            Assert.Contains("hidden success/no-op", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorReductionMicroOpUsesUnsupportedElementDataType_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VREDSUM,
                destSrc1Pointer: 0x280UL,
                streamLength: 4,
                stride: 4);
            inst.DataType = 0xFE;

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<TrapMicroOp>(microOp);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorReductionMicroOpUsesZeroLengthStream_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VREDSUM,
                destSrc1Pointer: 0x280UL,
                streamLength: 0,
                stride: 4);

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<VectorReductionMicroOp>(microOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    inst,
                    microOp,
                    isVectorOp: true,
                    isMemoryOp: true,
                    pc: 0x20F0));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("StreamLength == 0", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("vector compute contour", ex.InnerException.Message, StringComparison.Ordinal);
            Assert.Contains("hidden success/no-op", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Theory]
        [InlineData(InstructionsEnum.VREDMAX, 9U)]
        [InlineData(InstructionsEnum.VREDMIN, 2U)]
        [InlineData(InstructionsEnum.VREDMAXU, 9U)]
        [InlineData(InstructionsEnum.VREDMINU, 2U)]
        public void ExecuteStage_WhenMaterializedUnsignedVectorReductionUsesExtremumContour_ThenRetiresCorrectScalarResultWithoutBoundaryPromotion(
            InstructionsEnum opcode,
            uint expectedScalarResult)
        {
            const int vtId = 2;
            const ulong retiredPc = 0x2200;
            const ulong vectorAddress = 0x360;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(vectorAddress, 5U, 9U, 2U, 7U);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                opcode,
                destSrc1Pointer: vectorAddress,
                streamLength: 4,
                stride: 4,
                predicateMask: 0xFF,
                dataType: DataTypeEnum.UINT32);

            VectorReductionMicroOp microOp = Assert.IsType<VectorReductionMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
            Assert.Equal(expectedScalarResult, BitConverter.ToUInt32(resultBytes, 0));
            Assert.Equal(9U, BitConverter.ToUInt32(resultBytes, 4));
            Assert.Equal(2U, BitConverter.ToUInt32(resultBytes, 8));
            Assert.Equal(7U, BitConverter.ToUInt32(resultBytes, 12));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
        }

        [Theory]
        [InlineData(InstructionsEnum.VREDAND, 0x00000000U)]
        [InlineData(InstructionsEnum.VREDOR, 0x000000FFU)]
        [InlineData(InstructionsEnum.VREDXOR, 0x00000099U)]
        public void ExecuteStage_WhenMaterializedBitwiseVectorReductionUsesScalarResultContour_ThenRetiresCorrectScalarResultWithoutBoundaryPromotion(
            InstructionsEnum opcode,
            uint expectedScalarResult)
        {
            const int vtId = 2;
            const ulong retiredPc = 0x2280;
            const ulong vectorAddress = 0x3A0;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(vectorAddress, 0x000000F0U, 0x000000CCU, 0x000000AAU, 0x0000000FU);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                opcode,
                destSrc1Pointer: vectorAddress,
                streamLength: 4,
                stride: 4,
                predicateMask: 0xFF,
                dataType: DataTypeEnum.UINT32);

            VectorReductionMicroOp microOp = Assert.IsType<VectorReductionMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: true,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
            Assert.Equal(expectedScalarResult, BitConverter.ToUInt32(resultBytes, 0));
            Assert.Equal(0x000000CCU, BitConverter.ToUInt32(resultBytes, 4));
            Assert.Equal(0x000000AAU, BitConverter.ToUInt32(resultBytes, 8));
            Assert.Equal(0x0000000FU, BitConverter.ToUInt32(resultBytes, 12));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
        }

        [Theory]
        [InlineData(InstructionsEnum.VPERMUTE)]
        [InlineData(InstructionsEnum.VRGATHER)]
        public void ExecuteStage_WhenMaterializedVectorPermutationUsesMaskedUndisturbedLaneContour_ThenRetiresPreservedDestinationWithoutBoundaryPromotion(
            InstructionsEnum opcode)
        {
            const int vtId = 2;
            const ulong retiredPc = 0x2108;
            const ulong vectorAddress = 0x2A0;
            const ulong indexAddress = 0x3A0;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(vectorAddress, 10U, 20U, 30U, 40U);
            SeedVectorWordMemory(indexAddress, 2U, 0U, 3U, 2U);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.SetPredicateRegister(5, 0b1101UL);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                opcode,
                destSrc1Pointer: vectorAddress,
                src2Pointer: indexAddress,
                streamLength: 4,
                stride: 4,
                predicateMask: 5,
                dataType: DataTypeEnum.UINT32);

            VectorPermutationMicroOp microOp = Assert.IsType<VectorPermutationMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
            Assert.Equal(30U, BitConverter.ToUInt32(resultBytes, 0));
            Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 4));
            Assert.Equal(40U, BitConverter.ToUInt32(resultBytes, 8));
            Assert.Equal(30U, BitConverter.ToUInt32(resultBytes, 12));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorPermutationMicroOpUsesUnsupportedElementDataType_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VPERMUTE,
                destSrc1Pointer: 0x2A0UL,
                src2Pointer: 0x3A0UL,
                streamLength: 4,
                stride: 4);
            inst.DataType = 0xFE;

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<TrapMicroOp>(microOp);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorPermutationMicroOpUsesZeroLengthStream_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VPERMUTE,
                destSrc1Pointer: 0x2A0UL,
                src2Pointer: 0x3A0UL,
                streamLength: 0,
                stride: 4);

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<VectorPermutationMicroOp>(microOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    inst,
                    microOp,
                    isVectorOp: true,
                    isMemoryOp: true,
                    pc: 0x2110));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("StreamLength == 0", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("vector compute contour", ex.InnerException.Message, StringComparison.Ordinal);
            Assert.Contains("hidden success/no-op", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorSlideUpUsesUndisturbedLowerLaneContour_ThenRetiresShiftedResultWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x2138;
            const ulong vectorAddress = 0x2C0;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(vectorAddress, 10U, 20U, 30U, 40U);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VSLIDEUP,
                destSrc1Pointer: vectorAddress,
                immediate: 1,
                streamLength: 4,
                stride: 4,
                predicateMask: 0,
                dataType: DataTypeEnum.UINT32);

            VectorSlideMicroOp microOp = Assert.IsType<VectorSlideMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
            Assert.Equal(10U, BitConverter.ToUInt32(resultBytes, 0));
            Assert.Equal(10U, BitConverter.ToUInt32(resultBytes, 4));
            Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 8));
            Assert.Equal(30U, BitConverter.ToUInt32(resultBytes, 12));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorSlideDownUsesMaskedUndisturbedLaneContour_ThenRetiresPreservedDestinationWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x2140;
            const ulong vectorAddress = 0x2E0;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(vectorAddress, 10U, 20U, 30U, 40U);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.SetPredicateRegister(5, 0b1101UL);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VSLIDEDOWN,
                destSrc1Pointer: vectorAddress,
                immediate: 1,
                streamLength: 4,
                stride: 4,
                predicateMask: 5,
                dataType: DataTypeEnum.UINT32);

            VectorSlideMicroOp microOp = Assert.IsType<VectorSlideMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
            Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 0));
            Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 4));
            Assert.Equal(40U, BitConverter.ToUInt32(resultBytes, 8));
            Assert.Equal(0U, BitConverter.ToUInt32(resultBytes, 12));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorSlideMicroOpUsesUnsupportedElementDataType_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VSLIDEUP,
                destSrc1Pointer: 0x2C0UL,
                immediate: 1,
                streamLength: 4,
                stride: 4);
            inst.DataType = 0xFE;

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<TrapMicroOp>(microOp);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorSlideMicroOpUsesZeroLengthStream_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VSLIDEUP,
                destSrc1Pointer: 0x2C0UL,
                immediate: 1,
                streamLength: 0,
                stride: 4);

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<VectorSlideMicroOp>(microOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    inst,
                    microOp,
                    isVectorOp: true,
                    isMemoryOp: true,
                    pc: 0x2130));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("StreamLength == 0", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("vector compute contour", ex.InnerException.Message, StringComparison.Ordinal);
            Assert.Contains("hidden success/no-op", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorDotProductMicroOpUsesUnsupportedElementDataType_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VDOT,
                destSrc1Pointer: 0x2E0UL,
                src2Pointer: 0x3E0UL,
                streamLength: 4,
                stride: 4);
            inst.DataType = 0xFE;

            TrapMicroOp trap = Assert.IsType<TrapMicroOp>(MaterializeSingleSlotMicroOp(inst));
            Assert.Contains("unsupported element DataType", trap.TrapReason, StringComparison.Ordinal);
            Assert.Contains("authoritative mainline vector publication contour", trap.TrapReason, StringComparison.Ordinal);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVectorDotProductMicroOpUsesZeroLengthStream_ThenRejectsComputeNoOpContour()
        {
            var core = new Processor.CPU_Core(0);

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VDOT,
                destSrc1Pointer: 0x2E0UL,
                src2Pointer: 0x3E0UL,
                streamLength: 0,
                stride: 4);

            MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
            Assert.IsType<VectorDotProductMicroOp>(microOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    inst,
                    microOp,
                    isVectorOp: true,
                    isMemoryOp: true,
                    pc: 0x2150));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("StreamLength == 0", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("vector compute contour", ex.InnerException.Message, StringComparison.Ordinal);
            Assert.Contains("hidden success/no-op", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVpopcUsesDedicatedScalarResultMicroOp_ThenRetiresThroughWriteBackWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x2100;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);
            core.SetPredicateRegister(1, 0b1011UL);
            core.WriteCommittedArch(vtId, 6, 17UL);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VPOPC,
                immediate: (ushort)(1 | (6 << 8)),
                streamLength: 4);

            VectorMaskPopCountMicroOp microOp = Assert.IsType<VectorMaskPopCountMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);
            Assert.Equal(17UL, core.ReadArch(vtId, 6));

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            Assert.Equal(3UL, core.ReadArch(vtId, 6));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVdotUsesIntegerDotContour_ThenRetiresWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x2180;
            const ulong lhsAddress = 0x2E0;
            const ulong rhsAddress = 0x3E0;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorWordMemory(lhsAddress, 3U, 7U);
            SeedVectorWordMemory(rhsAddress, 2U, 5U);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VDOT,
                destSrc1Pointer: lhsAddress,
                src2Pointer: rhsAddress,
                streamLength: 2,
                stride: 4);

            VectorDotProductMicroOp microOp = Assert.IsType<VectorDotProductMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: true,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[8], lhsAddress, 8);
            Assert.Equal(41U, BitConverter.ToUInt32(resultBytes, 0));
            Assert.Equal(7U, BitConverter.ToUInt32(resultBytes, 4));
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Theory]
        [InlineData(InstructionsEnum.VDOTU)]
        [InlineData(InstructionsEnum.VDOTF)]
        public void ExecuteStage_WhenMaterializedSecondaryVdotUsesNonWideningDotContour_ThenRetiresWithoutBoundaryPromotion(
            InstructionsEnum opcode)
        {
            const int vtId = 2;
            const ulong retiredPc = 0x21A0;
            const ulong lhsAddress = 0x300;
            const ulong rhsAddress = 0x380;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            if (opcode == InstructionsEnum.VDOTF)
            {
                byte[] lhsSeed = new byte[8];
                byte[] rhsSeed = new byte[8];
                Array.Copy(BitConverter.GetBytes(1.0f), 0, lhsSeed, 0, 4);
                Array.Copy(BitConverter.GetBytes(2.0f), 0, lhsSeed, 4, 4);
                Array.Copy(BitConverter.GetBytes(2.0f), 0, rhsSeed, 0, 4);
                Array.Copy(BitConverter.GetBytes(4.0f), 0, rhsSeed, 4, 4);
                Processor.MainMemory.WriteToPosition(lhsSeed, lhsAddress);
                Processor.MainMemory.WriteToPosition(rhsSeed, rhsAddress);
            }
            else
            {
                SeedVectorWordMemory(lhsAddress, 3U, 7U);
                SeedVectorWordMemory(rhsAddress, 2U, 5U);
            }

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                opcode,
                destSrc1Pointer: lhsAddress,
                src2Pointer: rhsAddress,
                streamLength: 2,
                stride: 4,
                dataType: opcode == InstructionsEnum.VDOTF
                    ? DataTypeEnum.FLOAT32
                    : DataTypeEnum.UINT32);

            VectorDotProductMicroOp microOp = Assert.IsType<VectorDotProductMicroOp>(
                MaterializeSingleSlotMicroOp(inst));
            microOp.OwnerThreadId = vtId;
            microOp.VirtualThreadId = vtId;
            microOp.OwnerContextId = vtId;
            microOp.RefreshAdmissionMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: true,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[8], lhsAddress, 8);
            if (opcode == InstructionsEnum.VDOTF)
            {
                Assert.Equal(10.0f, BitConverter.ToSingle(resultBytes, 0));
                Assert.Equal(2.0f, BitConverter.ToSingle(resultBytes, 4));
            }
            else
            {
                Assert.Equal(41U, BitConverter.ToUInt32(resultBytes, 0));
                Assert.Equal(7U, BitConverter.ToUInt32(resultBytes, 4));
            }

            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(retiredPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void ExecuteStage_WhenMaterializedVdotFp8UsesWideningDotContour_ThenRetiresFp32ScalarResultWithoutBoundaryPromotion()
        {
            const int vtId = 2;
            const ulong retiredPc = 0x21C0;
            const ulong nextFetchPc = retiredPc + 0x100;
            const ulong lhsAddress = 0x320;
            const ulong rhsAddress = 0x3A0;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
            SeedVectorByteMemory(lhsAddress, 0x38, 0x40, 0xAA, 0xBB, 0x11, 0x22, 0x33, 0x44);
            SeedVectorByteMemory(rhsAddress, 0x38, 0x40, 0xCC, 0xDD);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);

            MicroOpScheduler scheduler = PrimeReplaySchedulerForSerializingBoundary(
                ref core,
                retiredPc,
                out long serializingEpochCountBefore);
            long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
            ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
            ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
                scheduler.LastPhaseCertificateInvalidationReason;
            AssistInvalidationReason assistInvalidationReasonBefore =
                scheduler.LastAssistInvalidationReason;

            VLIW_Instruction inst = CreateVectorInstruction(
                InstructionsEnum.VDOT_FP8,
                destSrc1Pointer: lhsAddress,
                src2Pointer: rhsAddress,
                streamLength: 2,
                stride: 1,
                predicateMask: 0x0F,
                dataType: DataTypeEnum.FLOAT8_E4M3);
            inst.VirtualThreadId = (byte)vtId;

            core.TestRunDecodeStageWithFetchedBundle(
                new[] { inst },
                retiredPc);

            var decodeStage = core.TestReadDecodeStageStatus();
            Assert.True(decodeStage.Valid);
            Assert.Equal((uint)InstructionsEnum.VDOT_FP8, decodeStage.OpCode);
            Assert.True(decodeStage.IsVectorOp);
            Assert.False(decodeStage.IsMemoryOp);

            core.TestRunExecuteStageFromCurrentDecodeState();

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.Valid);
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[8], lhsAddress, 8);
            Assert.Equal(5.0f, BitConverter.ToSingle(resultBytes, 0));
            Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, resultBytes[4..8]);
            Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
            Assert.Equal(nextFetchPc, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhase.IsActive);
            Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
            Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
            Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.True(schedulerPhase.IsActive);
            Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
            Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
            Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
            Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
            Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void ExecuteStreamInstruction_WhenStreamWaitReachesLegacyHelperSurface_ThenRejectsMissingSerializingBoundaryFollowThrough()
        {
            var core = new Processor.CPU_Core(0);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.STREAM_WAIT
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.ExecuteDirectStreamCompat(inst));

            Assert.Contains("serializing-boundary retire/apply follow-through", ex.Message, StringComparison.Ordinal);
            Assert.Contains("ExecutionDispatcherV4.CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(InstructionsEnum.STREAM_SETUP)]
        [InlineData(InstructionsEnum.STREAM_START)]
        public void ExecuteStreamInstruction_WhenUnsupportedStreamControlReachesLegacyHelperSurface_ThenRejectsNoOpFallback(
            InstructionsEnum opcode)
        {
            var core = new Processor.CPU_Core(0);

            var inst = new VLIW_Instruction
            {
                OpCode = (uint)opcode
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.ExecuteDirectStreamCompat(inst));

            Assert.Contains("without an authoritative retire/apply contour", ex.Message, StringComparison.Ordinal);
            Assert.Contains("stream execution/no-op behavior", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ExecuteStage_WhenVectorDecodeHasNoAuthoritativeMicroOp_ThenRejectsReferenceRawFallback()
        {
            var core = new Processor.CPU_Core(0);
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VADD,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x200UL,
                Src2Pointer = 0x300UL,
                StreamLength = 8,
                Stride = 4
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    inst,
                    isVectorOp: true));

            Assert.Contains("test-only", ex.Message, StringComparison.Ordinal);
            Assert.Contains("without an authoritative MicroOp", ex.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void ExecuteStage_WhenVectorMicroOpThrows_ThenRejectsReferenceRawFallback()
        {
            var core = new Processor.CPU_Core(0);
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VADD,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x200UL,
                Src2Pointer = 0x300UL,
                StreamLength = 8,
                Stride = 4
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    inst,
                    new ThrowingVectorFallbackMicroOp(inst.OpCode),
                    isVectorOp: true));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.Contains("MicroOp-owned runtime contour", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("synthetic vector micro-op failure", ex.InnerException!.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void ExecuteStage_WhenVectorMicroOpUsesZeroLengthStream_ThenRejectsInnerStreamNoOpContour()
        {
            var core = new Processor.CPU_Core(0);
            var microOp = new VectorALUMicroOp
            {
                OpCode = (uint)InstructionsEnum.VADD,
                OwnerThreadId = 2,
                VirtualThreadId = 2,
                OwnerContextId = 2,
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)InstructionsEnum.VADD,
                    DataTypeValue = DataTypeEnum.INT32,
                    PredicateMask = 0xFF,
                    DestSrc1Pointer = 0x200UL,
                    Src2Pointer = 0x300UL,
                    StreamLength = 0,
                    Stride = 4
                }
            };
            microOp.InitializeMetadata();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    microOp.Instruction,
                    microOp,
                    isVectorOp: true));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("StreamEngine.Execute(...)", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("StreamLength == 0", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void ExecuteStage_WhenVectorMicroOpUsesUnsupportedElementDataType_ThenRejectsInnerStreamNoOpContour()
        {
            var core = new Processor.CPU_Core(0);
            var microOp = new VectorALUMicroOp
            {
                OpCode = (uint)InstructionsEnum.VADD,
                OwnerThreadId = 2,
                VirtualThreadId = 2,
                OwnerContextId = 2,
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)InstructionsEnum.VADD,
                    DataType = 0xFE,
                    PredicateMask = 0xFF,
                    DestSrc1Pointer = 0x200UL,
                    Src2Pointer = 0x300UL,
                    StreamLength = 2,
                    Stride = 4
                }
            };
            microOp.InitializeMetadata();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    microOp.Instruction,
                    microOp,
                    isVectorOp: true));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("unsupported element DataType", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("hidden success/no-op", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Theory]
        [InlineData(InstructionsEnum.VLOAD)]
        [InlineData(InstructionsEnum.VSTORE)]
        public void Materialization_WhenVectorTransferUsesUnsupportedElementDataType_ThenFailsClosedBeforeRuntime(
            InstructionsEnum opcode)
        {
            VLIW_Instruction inst = CreateVectorInstruction(
                opcode,
                destSrc1Pointer: 0x280UL,
                src2Pointer: 0x380UL,
                streamLength: 4,
                stride: 4);
            inst.DataType = 0xFE;

            TrapMicroOp trap = Assert.IsType<TrapMicroOp>(MaterializeSingleSlotMicroOp(inst));
            Assert.Equal((uint)opcode, trap.OpCode);
            Assert.Contains("unsupported element DataType", trap.TrapReason, StringComparison.Ordinal);
            Assert.Contains("authoritative mainline vector publication contour", trap.TrapReason, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(InstructionsEnum.VLOAD)]
        [InlineData(InstructionsEnum.VSTORE)]
        public void VectorTransferMicroOp_WhenStreamLengthIsZero_ThenExecuteRejectsCarrierNoOpContour(
            InstructionsEnum opcode)
        {
            var core = new Processor.CPU_Core(0);
            var microOp = new VectorTransferMicroOp
            {
                OpCode = (uint)opcode,
                OwnerThreadId = 1,
                VirtualThreadId = 1,
                OwnerContextId = 1,
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)opcode,
                    DataTypeValue = DataTypeEnum.INT32,
                    PredicateMask = 0xFF,
                    DestSrc1Pointer = 0x280UL,
                    Src2Pointer = 0x380UL,
                    StreamLength = 0,
                    Stride = 4
                }
            };
            microOp.InitializeMetadata();
            Assert.False(microOp.IsMemoryOp);
            Assert.False(microOp.AdmissionMetadata.IsMemoryOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => microOp.Execute(ref core));

            Assert.Contains("VectorTransferMicroOp.Execute()", ex.Message, StringComparison.Ordinal);
            Assert.Contains("StreamLength == 0", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(InstructionsEnum.VLOAD)]
        [InlineData(InstructionsEnum.VSTORE)]
        public void ExecuteStage_WhenVectorTransferMicroOpUsesZeroLengthStream_ThenRejectsCarrierNoOpContour(
            InstructionsEnum opcode)
        {
            var core = new Processor.CPU_Core(0);
            var instruction = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x280UL,
                Src2Pointer = 0x380UL,
                StreamLength = 0,
                Stride = 4
            };

            var microOp = new VectorTransferMicroOp
            {
                OpCode = (uint)opcode,
                OwnerThreadId = 1,
                VirtualThreadId = 1,
                OwnerContextId = 1,
                Instruction = instruction
            };
            microOp.InitializeMetadata();
            Assert.False(microOp.IsMemoryOp);
            Assert.False(microOp.AdmissionMetadata.IsMemoryOp);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    instruction,
                    microOp,
                    isVectorOp: true,
                    isMemoryOp: false));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("VectorTransferMicroOp.Execute()", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("StreamLength == 0", ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Theory]
        [InlineData(InstructionsEnum.VLOAD)]
        [InlineData(InstructionsEnum.VSTORE)]
        public void VectorTransferMicroOp_WhenStreamLengthIsNonZero_ThenExecutes1DTransferThroughPublishedReadWriteRanges(
            InstructionsEnum opcode)
        {
            const ulong destSrc1Pointer = 0x280UL;
            const ulong src2Pointer = 0x380UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            VLIW_Instruction instruction =
                CreateVectorTransferInstruction(opcode, destSrc1Pointer, src2Pointer);
            var microOp = CreateVectorTransferMicroOp(opcode, instruction);
            microOp.InitializeMetadata();

            byte[] sourceBytes =
            {
                0x10, 0x00, 0x00, 0x00,
                0x20, 0x00, 0x00, 0x00,
                0x30, 0x00, 0x00, 0x00,
                0x40, 0x00, 0x00, 0x00
            };
            byte[] destinationSeed =
            {
                0xCC, 0xCC, 0xCC, 0xCC,
                0xCC, 0xCC, 0xCC, 0xCC,
                0xCC, 0xCC, 0xCC, 0xCC,
                0xCC, 0xCC, 0xCC, 0xCC
            };
            SeedVectorTransferMemory(opcode, destSrc1Pointer, src2Pointer, sourceBytes, destinationSeed);

            Assert.False(microOp.IsMemoryOp);
            Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
            Assert.Equal(
                (ResolveVectorTransferReadPointer(opcode, destSrc1Pointer, src2Pointer), 16UL),
                Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges));
            Assert.Equal(
                (ResolveVectorTransferWritePointer(opcode, destSrc1Pointer, src2Pointer), 16UL),
                Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges));

            Assert.True(microOp.Execute(ref core));

            Assert.Equal(
                sourceBytes,
                ReadMainMemoryBytes(
                    ResolveVectorTransferReadPointer(opcode, destSrc1Pointer, src2Pointer),
                    sourceBytes.Length));
            Assert.Equal(
                sourceBytes,
                ReadMainMemoryBytes(
                    ResolveVectorTransferWritePointer(opcode, destSrc1Pointer, src2Pointer),
                    sourceBytes.Length));
        }

        [Theory]
        [InlineData(InstructionsEnum.VLOAD)]
        [InlineData(InstructionsEnum.VSTORE)]
        public void ExecuteStage_WhenVectorTransferMicroOpUsesNonZeroStream_ThenCompletesAuthoritativeTransferWithoutLegacyFallback(
            InstructionsEnum opcode)
        {
            const byte vtId = 1;
            const ulong retiredPc = 0x4600UL;
            const ulong destSrc1Pointer = 0x280UL;
            const ulong src2Pointer = 0x380UL;

            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(retiredPc, vtId);
            core.WriteCommittedPc(vtId, retiredPc);

            VLIW_Instruction instruction =
                CreateVectorTransferInstruction(opcode, destSrc1Pointer, src2Pointer);
            var microOp = CreateVectorTransferMicroOp(opcode, instruction, vtId);
            microOp.InitializeMetadata();

            byte[] sourceBytes =
            {
                0xAA, 0x00, 0x00, 0x00,
                0xBB, 0x00, 0x00, 0x00,
                0xCC, 0x00, 0x00, 0x00,
                0xDD, 0x00, 0x00, 0x00
            };
            byte[] destinationSeed =
            {
                0x11, 0x11, 0x11, 0x11,
                0x22, 0x22, 0x22, 0x22,
                0x33, 0x33, 0x33, 0x33,
                0x44, 0x44, 0x44, 0x44
            };
            SeedVectorTransferMemory(opcode, destSrc1Pointer, src2Pointer, sourceBytes, destinationSeed);

            Assert.False(microOp.IsMemoryOp);
            Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
            Assert.Equal(
                (ResolveVectorTransferReadPointer(opcode, destSrc1Pointer, src2Pointer), 16UL),
                Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges));
            Assert.Equal(
                (ResolveVectorTransferWritePointer(opcode, destSrc1Pointer, src2Pointer), 16UL),
                Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges));

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: retiredPc);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.True(executeStage.ResultReady);
            Assert.True(executeStage.VectorComplete);

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            Assert.Equal(
                sourceBytes,
                ReadMainMemoryBytes(
                    ResolveVectorTransferReadPointer(opcode, destSrc1Pointer, src2Pointer),
                    sourceBytes.Length));
            Assert.Equal(
                sourceBytes,
                ReadMainMemoryBytes(
                    ResolveVectorTransferWritePointer(opcode, destSrc1Pointer, src2Pointer),
                    sourceBytes.Length));
        }

        [Fact]
        public void ExecuteStage_WhenIndexedVectorMicroOpCannotReadDescriptor_ThenRejectsInnerStreamNoOpContour()
        {
            const ulong unreadableDescriptorAddr = 0x1_0000_1000UL;

            IOMMU.Initialize();
            var core = new Processor.CPU_Core(0);
            var microOp = new VectorALUMicroOp
            {
                OpCode = (uint)InstructionsEnum.VADD,
                OwnerThreadId = 2,
                VirtualThreadId = 2,
                OwnerContextId = 2,
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)InstructionsEnum.VADD,
                    DataTypeValue = DataTypeEnum.INT32,
                    PredicateMask = 0xFF,
                    DestSrc1Pointer = 0x200UL,
                    Src2Pointer = unreadableDescriptorAddr,
                    StreamLength = 4,
                    Stride = 4,
                    Indexed = true
                }
            };
            microOp.InitializeMetadata();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    microOp.Instruction,
                    microOp,
                    isVectorOp: true));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("StreamEngine", ex.InnerException!.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Theory]
        [InlineData(InstructionsEnum.VGATHER)]
        [InlineData(InstructionsEnum.VSCATTER)]
        public void ExecuteStage_WhenRawIndexedVectorMicroOpUsesNonRepresentableGatherScatterOpcode_ThenRejectsCompatSuccessContour(
            InstructionsEnum opcode)
        {
            var core = new Processor.CPU_Core(0);
            var microOp = new VectorALUMicroOp
            {
                OpCode = (uint)opcode,
                OwnerThreadId = 2,
                VirtualThreadId = 2,
                OwnerContextId = 2,
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)opcode,
                    DataTypeValue = DataTypeEnum.INT32,
                    PredicateMask = 0xFF,
                    DestSrc1Pointer = 0x200UL,
                    Src2Pointer = 0x300UL,
                    StreamLength = 4,
                    Stride = 4,
                    Indexed = true
                }
            };
            microOp.InitializeMetadata();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    microOp.Instruction,
                    microOp,
                    isVectorOp: true));

            Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("indexed StreamEngine.Execute(...)", ex.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains("non-representable raw gather/scatter contour", ex.InnerException.Message, StringComparison.Ordinal);
            Assert.Contains(opcode.ToString(), ex.InnerException.Message, StringComparison.Ordinal);

            var executeStage = core.TestReadExecuteStageStatus();
            Assert.False(executeStage.ResultReady);
            Assert.False(executeStage.VectorComplete);
        }

        [Fact]
        public void VectorMaskMicroOp_WhenElementDataTypeIsUnsupported_ThenPredicateFollowThroughStillExecutes()
        {
            var core = new Processor.CPU_Core(0);
            core.SetPredicateRegister(1, 0b1010UL);
            core.SetPredicateRegister(2, 0b1100UL);
            core.SetPredicateRegister(3, 0x5AUL);

            var microOp = new VectorALUMicroOp
            {
                OpCode = (uint)InstructionsEnum.VMAND,
                OwnerThreadId = 1,
                VirtualThreadId = 1,
                OwnerContextId = 1,
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)InstructionsEnum.VMAND,
                    DataType = 0xFE,
                    Immediate = (ushort)(1 | (2 << 4) | (3 << 8)),
                    StreamLength = 8
                }
            };
            microOp.InitializeMetadata();

            bool success = microOp.Execute(ref core);

            Assert.True(success);
            Assert.Equal(0b1000UL, core.GetPredicateRegister(3));
        }

        [Fact]
        public void LegacySingleLaneVectorMicroOp_WhenRawFallbackWouldBeRequired_ThenFailClosed()
        {
            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(2, 1, 7UL);
            core.WriteCommittedArch(2, 2, 5UL);

            var microOp = new VectorALUMicroOp
            {
                OpCode = (uint)InstructionsEnum.VADD,
                OwnerThreadId = 2,
                VirtualThreadId = 2,
                OwnerContextId = 2,
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)InstructionsEnum.VADD,
                    DataTypeValue = DataTypeEnum.INT32,
                    PredicateMask = 0xFF,
                    DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 2),
                    StreamLength = 1
                }
            };
            microOp.InitializeMetadata();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => core.TestRunExecuteStageWithDecodedInstruction(
                    microOp.Instruction,
                    microOp,
                    isVectorOp: true,
                    isMemoryOp: true,
                    writesRegister: true,
                    reg1Id: 9,
                    pc: 0x1C00));

            Assert.Contains("reference raw execute fallback after MicroOp failure", ex.Message);
        }

        [Fact]
        public void ExplicitPacketVectorMicroOp_DoesNotSynthesizeScalarRegisterWriteBack()
        {
            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(2, 1, 7UL);
            core.WriteCommittedArch(2, 2, 5UL);

            var microOp = new VectorALUMicroOp
            {
                OpCode = (uint)InstructionsEnum.VADD,
                OwnerThreadId = 2,
                VirtualThreadId = 2,
                OwnerContextId = 2,
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)InstructionsEnum.VADD,
                    DataTypeValue = DataTypeEnum.INT32,
                    PredicateMask = 0xFF,
                    DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 2),
                    StreamLength = 1
                }
            };
            microOp.InitializeMetadata();

            core.TestRetireExplicitPacketLaneMicroOp(
                laneIndex: 0,
                microOp,
                pc: 0x1D00,
                vtId: 2);

            Assert.Equal(0UL, core.ReadArch(2, 9));

            var control = core.GetPipelineControl();
            Assert.Equal(0UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(0UL, control.NonScalarLanesRetired);
            Assert.Equal(0UL, control.MemoryStalls);
        }
    }
}



