using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests
{
    public sealed class Phase10FenceOrderingAndVisibilityTests
    {
        private const byte VtId = 0;

        private static void InitializeCpuMainMemoryIdentityMap(ulong size = 0x4000000UL)
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, size);
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

        private static VLIW_Instruction CreateCanonicalFenceInstruction(InstructionsEnum opcode)
        {
            return new VLIW_Instruction
            {
                OpCode = (uint)opcode
            };
        }

        private static InstructionIR CreateSystemInstructionIr(InstructionsEnum opcode)
        {
            return new InstructionIR
            {
                CanonicalOpcode = opcode,
                Class = InstructionClassifier.GetClass(opcode),
                SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0
            };
        }

        private static ulong ReadDoubleword(ulong address) =>
            BitConverter.ToUInt64(Processor.MainMemory.ReadFromPosition(new byte[8], address, 8), 0);

        private static void WriteBytes(ulong address, byte value, int length)
        {
            byte[] data = new byte[length];
            Array.Fill(data, value);
            Processor.MainMemory.WriteToPosition(data, address);
        }

        private static Processor.CPU_Core CreateCoreWithFetchCaches()
        {
            return new Processor.CPU_Core(0)
            {
                L1_Data = new Processor.CPU_Core.Cache_Data_Object[8],
                L2_Data = new Processor.CPU_Core.Cache_Data_Object[8],
                L1_VLIWBundles = new Processor.CPU_Core.Cache_VLIWBundle_Object[4],
                L2_VLIWBundles = new Processor.CPU_Core.Cache_VLIWBundle_Object[4]
            };
        }

        private static bool ContainsVliwLine(
            Processor.CPU_Core.Cache_VLIWBundle_Object[] cache,
            ulong address)
        {
            return Array.Exists(cache, line => line.VLIWCache_MemoryAddress == address);
        }

        [Fact]
        public void CanonicalFence_DecodesAndCapturesDrainMemoryWithoutSerializingPromotion()
        {
            VLIW_Instruction raw = CreateCanonicalFenceInstruction(InstructionsEnum.FENCE);
            InstructionIR instruction = new VliwDecoderV4().Decode(in raw, slotIndex: 7);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x2000, VtId);
            core.WriteCommittedPc(VtId, 0x2000);
            ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(VtId);

            RetireWindowCaptureSnapshot snapshot =
                RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                    new ExecutionDispatcherV4(),
                    instruction,
                    state,
                    bundleSerial: 0xF10,
                    vtId: VtId);

            FenceEvent fenceEvent = Assert.IsType<FenceEvent>(snapshot.PipelineEvent);
            Assert.False(fenceEvent.IsInstructionFence);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, snapshot.PipelineEventOrderGuarantee);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.False(instruction.AcquireOrdering);
            Assert.False(instruction.ReleaseOrdering);
            Assert.Equal(0, instruction.Imm);
        }

        [Fact]
        public void CanonicalFenceI_DecodesAndCapturesFlushPipelineBoundary()
        {
            VLIW_Instruction raw = CreateCanonicalFenceInstruction(InstructionsEnum.FENCE_I);
            InstructionIR instruction = new VliwDecoderV4().Decode(in raw, slotIndex: 7);

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x2040, VtId);
            core.WriteCommittedPc(VtId, 0x2040);
            ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(VtId);

            RetireWindowCaptureSnapshot snapshot =
                RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                    new ExecutionDispatcherV4(),
                    instruction,
                    state,
                    bundleSerial: 0xF11,
                    vtId: VtId);

            FenceEvent fenceEvent = Assert.IsType<FenceEvent>(snapshot.PipelineEvent);
            Assert.True(fenceEvent.IsInstructionFence);
            Assert.Equal(SystemEventOrderGuarantee.FlushPipeline, snapshot.PipelineEventOrderGuarantee);
            Assert.True(snapshot.HasSerializingBoundaryEffect);
            Assert.False(instruction.AcquireOrdering);
            Assert.False(instruction.ReleaseOrdering);
            Assert.Equal(0, instruction.Imm);
        }

        [Theory]
        [InlineData(InstructionsEnum.FENCE, "immediate")]
        [InlineData(InstructionsEnum.FENCE, "predicate")]
        [InlineData(InstructionsEnum.FENCE, "flags")]
        [InlineData(InstructionsEnum.FENCE, "word1")]
        [InlineData(InstructionsEnum.FENCE, "word2")]
        [InlineData(InstructionsEnum.FENCE, "word3")]
        [InlineData(InstructionsEnum.FENCE_I, "immediate")]
        [InlineData(InstructionsEnum.FENCE_I, "predicate")]
        [InlineData(InstructionsEnum.FENCE_I, "flags")]
        [InlineData(InstructionsEnum.FENCE_I, "word1")]
        [InlineData(InstructionsEnum.FENCE_I, "word2")]
        [InlineData(InstructionsEnum.FENCE_I, "word3")]
        public void UnsupportedFencePayload_DecodeFailsClosed(
            InstructionsEnum opcode,
            string payloadKind)
        {
            VLIW_Instruction raw = CreateCanonicalFenceInstruction(opcode);
            switch (payloadKind)
            {
                case "immediate":
                    raw.Immediate = 1;
                    break;
                case "predicate":
                    raw.PredicateMask = 1;
                    break;
                case "flags":
                    raw.Acquire = true;
                    break;
                case "word1":
                    raw.Word1 = 1;
                    break;
                case "word2":
                    raw.Word2 = 1;
                    break;
                case "word3":
                    raw.StreamLength = 1;
                    break;
            }

            void Decode() => new VliwDecoderV4().Decode(in raw, slotIndex: 7);

            Assert.Throws<InvalidOpcodeException>(Decode);
        }

        [Theory]
        [InlineData(InstructionsEnum.FENCE)]
        [InlineData(InstructionsEnum.FENCE_I)]
        public void FenceDecoder_AllowsVirtualThreadTransportHintOnly(
            InstructionsEnum opcode)
        {
            VLIW_Instruction raw = CreateCanonicalFenceInstruction(opcode);
            raw.VirtualThreadId = 3;

            InstructionIR instruction = new VliwDecoderV4().Decode(in raw, slotIndex: 7);

            Assert.Equal((ushort)opcode, instruction.CanonicalOpcode.Value);
            Assert.Equal(0, instruction.Imm);
        }

        [Theory]
        [InlineData(InstructionsEnum.FENCE)]
        [InlineData(InstructionsEnum.FENCE_I)]
        public void FenceMaterializer_RejectsPayloadContextAsOrderingAuthority(
            InstructionsEnum opcode)
        {
            var context = new DecoderContext
            {
                OpCode = (uint)opcode,
                Immediate = 1,
                HasImmediate = true
            };

            Assert.Throws<DecodeProjectionFaultException>(
                () => InstructionRegistry.CreateMicroOp((uint)opcode, context));
        }

        [Theory]
        [InlineData(InstructionsEnum.FENCE)]
        [InlineData(InstructionsEnum.FENCE_I)]
        public void FenceDispatcher_RejectsIrAqRlBitsAsOrderingAuthority(
            InstructionsEnum opcode)
        {
            InstructionIR instruction = CreateSystemInstructionIr(opcode) with
            {
                AcquireOrdering = true,
                ReleaseOrdering = true
            };
            var core = new Processor.CPU_Core(0);
            ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(VtId);

            Span<YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord> retireRecords =
                stackalloc YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord[
                    Processor.CPU_Core.DirectRetirePublicationRetireRecordCapacity];
            Span<Processor.CPU_Core.RetireWindowEffect> retireEffects =
                stackalloc Processor.CPU_Core.RetireWindowEffect[3];
            PipelineEvent?[] pipelineEvents = new PipelineEvent?[1];
            var retireBatch = new Processor.CPU_Core.RetireWindowBatch(
                retireRecords,
                retireEffects,
                pipelineEvents);

            InvalidOperationException? exception = null;
            try
            {
                new ExecutionDispatcherV4().CaptureRetireWindowPublications(
                    instruction,
                    state,
                    ref retireBatch,
                    bundleSerial: 0xF12,
                    vtId: VtId);
            }
            catch (InvalidOperationException caught)
            {
                exception = caught;
            }

            Assert.NotNull(exception);
        }

        [Fact]
        public void FenceRetireBoundary_OrdersStoresThroughSeparateSingletonBatches()
        {
            const ulong firstAddress = 0x2_1000;
            const ulong secondAddress = 0x2_1008;
            const ulong firstValue = 0x1111_2222_3333_4444UL;
            const ulong secondValue = 0x5555_6666_7777_8888UL;

            InitializeMemory();
            var core = new Processor.CPU_Core(0);

            Span<YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord> retireRecords =
                stackalloc YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord[8];
            Span<Processor.CPU_Core.RetireWindowEffect> retireEffects =
                stackalloc Processor.CPU_Core.RetireWindowEffect[8];
            PipelineEvent?[] pipelineEvents = new PipelineEvent?[2];
            var priorStoreBatch = new Processor.CPU_Core.RetireWindowBatch(
                retireRecords,
                retireEffects,
                pipelineEvents);

            priorStoreBatch.CaptureRetireWindowScalarMemoryStore(firstAddress, firstValue, 8);
            Assert.Equal(0UL, ReadDoubleword(firstAddress));
            core.ApplyCapturedRetireWindowBatch(ref priorStoreBatch);
            Assert.Equal(firstValue, ReadDoubleword(firstAddress));

            retireRecords = stackalloc YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord[8];
            retireEffects = stackalloc Processor.CPU_Core.RetireWindowEffect[8];
            pipelineEvents = new PipelineEvent?[2];
            var fenceBatch = new Processor.CPU_Core.RetireWindowBatch(
                retireRecords,
                retireEffects,
                pipelineEvents);

            fenceBatch.CaptureRetireWindowPipelineEvent(
                new FenceEvent
                {
                    VtId = VtId,
                    BundleSerial = 0xF13,
                    IsInstructionFence = false
                },
                SystemEventOrderGuarantee.DrainMemory,
                retiredPc: 0x2100,
                virtualThreadId: VtId,
                serializingBoundaryFollowThrough: false);

            Processor.CPU_Core.RetireWindowEffect[] capturedEffects = fenceBatch.Effects.ToArray();
            Assert.Equal(Processor.CPU_Core.RetireWindowEffectKind.PipelineEvent, capturedEffects[0].Kind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, capturedEffects[0].SystemEventOrderGuarantee);
            Assert.Equal(0UL, ReadDoubleword(secondAddress));
            core.ApplyCapturedRetireWindowBatch(ref fenceBatch);

            retireRecords = stackalloc YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord[8];
            retireEffects = stackalloc Processor.CPU_Core.RetireWindowEffect[8];
            pipelineEvents = new PipelineEvent?[2];
            var laterStoreBatch = new Processor.CPU_Core.RetireWindowBatch(
                retireRecords,
                retireEffects,
                pipelineEvents);

            laterStoreBatch.CaptureRetireWindowScalarMemoryStore(secondAddress, secondValue, 8);
            Assert.Equal(0UL, ReadDoubleword(secondAddress));
            core.ApplyCapturedRetireWindowBatch(ref laterStoreBatch);
            Assert.Equal(secondValue, ReadDoubleword(secondAddress));
        }

        [Fact]
        public void FenceI_RetireInvalidatesFetchStateAfterPriorCodeWrite()
        {
            const ulong bundleAddress = 0x3000;

            InitializeCpuMainMemoryIdentityMap();
            WriteBytes(bundleAddress, 0x11, 256);
            var core = CreateCoreWithFetchCaches();
            Processor.CPU_Core.Cache_VLIWBundle_Object beforeFence =
                core.GetVLIWBundleByPointer(bundleAddress);
            Assert.Equal(0x11, beforeFence.VLIWCache_VLIWBundle[0]);

            WriteBytes(bundleAddress, 0x22, 256);

            Span<YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord> retireRecords =
                stackalloc YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord[4];
            Span<Processor.CPU_Core.RetireWindowEffect> retireEffects =
                stackalloc Processor.CPU_Core.RetireWindowEffect[4];
            PipelineEvent?[] pipelineEvents = new PipelineEvent?[1];
            var retireBatch = new Processor.CPU_Core.RetireWindowBatch(
                retireRecords,
                retireEffects,
                pipelineEvents);

            retireBatch.CaptureRetireWindowPipelineEvent(
                new FenceEvent
                {
                    VtId = VtId,
                    BundleSerial = 0xF14,
                    IsInstructionFence = true
                },
                SystemEventOrderGuarantee.FlushPipeline,
                retiredPc: 0x3000,
                virtualThreadId: VtId,
                serializingBoundaryFollowThrough: true);

            Assert.True(ContainsVliwLine(core.L1_VLIWBundles, bundleAddress));
            Assert.True(ContainsVliwLine(core.L2_VLIWBundles, bundleAddress));

            core.ApplyCapturedRetireWindowBatch(ref retireBatch);

            Assert.False(ContainsVliwLine(core.L1_VLIWBundles, bundleAddress));
            Assert.False(ContainsVliwLine(core.L2_VLIWBundles, bundleAddress));
            Processor.CPU_Core.Cache_VLIWBundle_Object afterFence =
                core.GetVLIWBundleByPointer(bundleAddress);
            Assert.Equal(0x22, afterFence.VLIWCache_VLIWBundle[0]);
        }

        [Fact]
        public void FenceIRolledBackBatch_DoesNotInvalidateFetchStateBeforeApply()
        {
            var core = CreateCoreWithFetchCaches();
            core.L1_VLIWBundles[0] = new Processor.CPU_Core.Cache_VLIWBundle_Object
            {
                VLIWCache_MemoryAddress = 0x3400,
                VLIWCache_VLIWBundle = new byte[256]
            };
            core.L2_VLIWBundles[0] = new Processor.CPU_Core.Cache_VLIWBundle_Object
            {
                VLIWCache_MemoryAddress = 0x3400,
                VLIWCache_VLIWBundle = new byte[256]
            };
            core.TestMarkVliwFetchStateMaterializedForPhase09();

            Span<YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord> retireRecords =
                stackalloc YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord[4];
            Span<Processor.CPU_Core.RetireWindowEffect> retireEffects =
                stackalloc Processor.CPU_Core.RetireWindowEffect[4];
            PipelineEvent?[] pipelineEvents = new PipelineEvent?[1];
            var retireBatch = new Processor.CPU_Core.RetireWindowBatch(
                retireRecords,
                retireEffects,
                pipelineEvents);

            retireBatch.CaptureRetireWindowPipelineEvent(
                new FenceEvent
                {
                    VtId = VtId,
                    BundleSerial = 0xF15,
                    IsInstructionFence = true
                },
                SystemEventOrderGuarantee.FlushPipeline,
                retiredPc: 0x3400,
                virtualThreadId: VtId,
                serializingBoundaryFollowThrough: true);

            Assert.Equal(0x3400UL, core.L1_VLIWBundles[0].VLIWCache_MemoryAddress);
            Assert.Equal(0x3400UL, core.L2_VLIWBundles[0].VLIWCache_MemoryAddress);
        }

        [Theory]
        [InlineData(InstructionsEnum.FENCE, SystemEventKind.Fence, SystemEventOrderGuarantee.DrainMemory)]
        [InlineData(InstructionsEnum.FENCE_I, SystemEventKind.FenceI, SystemEventOrderGuarantee.FlushPipeline)]
        public void FenceMicroOps_RemainLane7SystemSingletonCarriers(
            InstructionsEnum opcode,
            SystemEventKind expectedKind,
            SystemEventOrderGuarantee expectedGuarantee)
        {
            SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(
                InstructionRegistry.CreateMicroOp(
                    (uint)opcode,
                    new DecoderContext { OpCode = (uint)opcode }));

            Assert.Equal(expectedKind, microOp.EventKind);
            Assert.Equal(expectedGuarantee, microOp.OrderGuarantee);
            Assert.Equal(SlotClass.SystemSingleton, microOp.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, microOp.Placement.PinningKind);
            Assert.Equal((byte)7, microOp.Placement.PinnedLaneId);
        }
    }
}
