using System;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using HybridCPU_ISE.Tests.TestHelpers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase04
{
    public sealed class Phase04_DirectCompatRetireTransactionTests
    {
        private sealed class FakeMemoryBus : IMemoryBus
        {
            private readonly byte[] _memory = new byte[65536];

            public byte[] Read(ulong address, int length)
            {
                byte[] result = new byte[length];
                Array.Copy(_memory, (int)address, result, 0, length);
                return result;
            }

            public void Write(ulong address, byte[] data)
            {
                Array.Copy(data, 0, _memory, (int)address, data.Length);
            }

            public void StoreDword(ulong address, ulong value)
            {
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Copy(bytes, 0, _memory, (int)address, bytes.Length);
            }
        }

        private static RetireWindowCaptureSnapshot Capture(
            ExecutionDispatcherV4 dispatcher,
            InstructionIR instruction,
            ICanonicalCpuState state,
            ulong bundleSerial = 0,
            byte vtId = 0)
        {
            return RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial,
                vtId);
        }

        [Fact]
        public void ScalarAluTransaction_EmitsRegisterWriteRetireRecord()
        {
            var state = new FakeCpuState();
            state.SetReg(1, 11UL);
            state.SetReg(2, 7UL);

            var dispatcher = new ExecutionDispatcherV4();
            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.Addition,
                rd: 9,
                rs1: 1,
                rs2: 2);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 2);

            Assert.Equal(RetireWindowCaptureEffectKind.None, transaction.TypedEffectKind);
            Assert.Equal(1, transaction.RetireRecordCount);

            RetireRecord retireRecord = transaction.GetRetireRecord(0);
            Assert.True(retireRecord.IsRegisterWrite);
            Assert.Equal(2, retireRecord.VtId);
            Assert.Equal(9, retireRecord.ArchReg);
            Assert.Equal(18UL, retireRecord.Value);
        }

        [Fact]
        public void JalTransaction_EmitsLinkWriteAndRedirectPcRecords()
        {
            const ulong pc = 0x2000;

            var state = new FakeCpuState();
            state.SetInstructionPointer(pc);

            var dispatcher = new ExecutionDispatcherV4();
            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.JAL,
                rd: 5,
                imm: 0x20);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 1);

            Assert.Equal(RetireWindowCaptureEffectKind.None, transaction.TypedEffectKind);
            Assert.Equal(2, transaction.RetireRecordCount);

            RetireRecord linkWrite = transaction.GetRetireRecord(0);
            Assert.True(linkWrite.IsRegisterWrite);
            Assert.Equal(1, linkWrite.VtId);
            Assert.Equal(5, linkWrite.ArchReg);
            Assert.Equal(pc + 4, linkWrite.Value);

            RetireRecord redirectedPc = transaction.GetRetireRecord(1);
            Assert.True(redirectedPc.IsPcWrite);
            Assert.Equal(1, redirectedPc.VtId);
            Assert.Equal(pc + 0x20, redirectedPc.Value);
        }

        [Fact]
        public void TypedLoadTransaction_EmitsRegisterWriteRetireRecord()
        {
            const ulong baseAddress = 0x200;
            const ulong loadedValue = 0x8877_6655_4433_2211UL;

            var bus = new FakeMemoryBus();
            bus.StoreDword(baseAddress + 0x10, loadedValue);

            var state = new FakeCpuState();
            state.SetReg(1, baseAddress);

            var dispatcher = new ExecutionDispatcherV4(memoryUnit: new MemoryUnit(bus));
            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.LD,
                rd: 9,
                rs1: 1,
                imm: 0x10);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 2);

            Assert.Equal(RetireWindowCaptureEffectKind.None, transaction.TypedEffectKind);
            Assert.Equal(1, transaction.RetireRecordCount);

            RetireRecord retireRecord = transaction.GetRetireRecord(0);
            Assert.True(retireRecord.IsRegisterWrite);
            Assert.Equal(2, retireRecord.VtId);
            Assert.Equal(9, retireRecord.ArchReg);
            Assert.Equal(loadedValue, retireRecord.Value);
        }

        [Fact]
        public void TypedLoadDirectExecute_LeavesRegisterMutationForRetireWindow()
        {
            const ulong baseAddress = 0x240;
            const ulong loadedValue = 0x0102_0304_0506_0708UL;
            const ulong originalDestinationValue = 0xABCD_EF01_2345_6789UL;

            var bus = new FakeMemoryBus();
            bus.StoreDword(baseAddress + 0x10, loadedValue);

            var state = new FakeCpuState();
            state.SetReg(1, baseAddress);
            state.SetReg(9, originalDestinationValue);

            var dispatcher = new ExecutionDispatcherV4(memoryUnit: new MemoryUnit(bus));
            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.LD,
                rd: 9,
                rs1: 1,
                imm: 0x10);

            ExecutionResult result = dispatcher.Execute(ir, state, bundleSerial: 7, vtId: 2);

            Assert.Equal(baseAddress + 0x10, result.Value);
            Assert.Equal(originalDestinationValue, state.ReadIntRegister(9));
        }

        [Fact]
        public void TypedStoreTransaction_CarriesScalarMemoryStoreEffect()
        {
            const ulong baseAddress = 0x300;
            const ulong storeValue = 0xDEAD_BEEF_5566_7788UL;

            var state = new FakeCpuState();
            state.SetReg(1, baseAddress);
            state.SetReg(2, storeValue);

            var dispatcher = new ExecutionDispatcherV4(memoryUnit: new MemoryUnit(new FakeMemoryBus()));
            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.SW,
                rs1: 1,
                rs2: 2,
                imm: 0x18);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 1);

            Assert.Equal(RetireWindowCaptureEffectKind.ScalarMemoryStore, transaction.TypedEffectKind);
            Assert.Equal(0, transaction.RetireRecordCount);
            Assert.Equal(baseAddress + 0x18, transaction.MemoryAddress);
            Assert.Equal(storeValue, transaction.MemoryData);
            Assert.Equal(4, transaction.MemoryAccessSize);
        }

        [Fact]
        public void TypedStoreDirectExecute_LeavesMemoryMutationForRetireWindow()
        {
            const ulong baseAddress = 0x340;
            const ulong storeValue = 0x8899_AABB_CCDD_EEFFUL;

            var bus = new FakeMemoryBus();
            var state = new FakeCpuState();
            state.SetReg(1, baseAddress);
            state.SetReg(2, storeValue);

            var dispatcher = new ExecutionDispatcherV4(memoryUnit: new MemoryUnit(bus));
            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.SW,
                rs1: 1,
                rs2: 2,
                imm: 0x08);

            ExecutionResult result = dispatcher.Execute(ir, state, bundleSerial: 9, vtId: 1);

            Assert.Equal(baseAddress + 0x08, result.Value);
            Assert.Equal(new byte[4], bus.Read(baseAddress + 0x08, 4));
        }

        [Theory]
        [InlineData(InstructionsEnum.Load)]
        [InlineData(InstructionsEnum.Store)]
        public void LegacyAbsoluteMemoryTransaction_UsesDirectCompatRetireSurface(InstructionsEnum opcode)
        {
            var state = new FakeCpuState();
            state.SetReg(1, 0x300UL);
            state.SetReg(2, 0x1122_3344_5566_7788UL);

            var dispatcher = new ExecutionDispatcherV4(memoryUnit: new MemoryUnit(new FakeMemoryBus()));
            InstructionIR ir = IrBuilder.Make(
                opcode,
                rd: 9,
                rs1: 1,
                rs2: 2,
                imm: 0x18);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 1);

            if (opcode == InstructionsEnum.Load)
            {
                Assert.Equal(RetireWindowCaptureEffectKind.None, transaction.TypedEffectKind);
                Assert.Equal(1, transaction.RetireRecordCount);

                RetireRecord retireRecord = transaction.GetRetireRecord(0);
                Assert.True(retireRecord.IsRegisterWrite);
                Assert.Equal(1, retireRecord.VtId);
                Assert.Equal(9, retireRecord.ArchReg);
                Assert.Equal(0UL, retireRecord.Value);
                return;
            }

            Assert.Equal(RetireWindowCaptureEffectKind.ScalarMemoryStore, transaction.TypedEffectKind);
            Assert.Equal(0, transaction.RetireRecordCount);
            Assert.Equal(0x318UL, transaction.MemoryAddress);
            Assert.Equal(0x1122_3344_5566_7788UL, transaction.MemoryData);
            Assert.Equal(8, transaction.MemoryAccessSize);
        }

        [Fact]
        public void AtomicTransaction_CarriesTypedAtomicEffect()
        {
            const ulong address = 0x380;
            const ulong sourceValue = 0x10;

            var state = new FakeCpuState();
            state.SetReg(1, address);
            state.SetReg(2, sourceValue);

            var dispatcher = new ExecutionDispatcherV4();
            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.AMOADD_W,
                rd: 9,
                rs1: 1,
                rs2: 2);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 3);

            Assert.Equal(RetireWindowCaptureEffectKind.Atomic, transaction.TypedEffectKind);
            Assert.Equal(0, transaction.RetireRecordCount);
            Assert.True(transaction.HasAtomicEffect);
            Assert.Equal(InstructionsEnum.AMOADD_W, (InstructionsEnum)transaction.AtomicEffect.Opcode);
            Assert.Equal((byte)4, transaction.AtomicEffect.AccessSize);
            Assert.Equal(address, transaction.AtomicEffect.Address);
            Assert.Equal(sourceValue, transaction.AtomicEffect.SourceValue);
            Assert.Equal((ushort)9, transaction.AtomicEffect.DestinationRegister);
            Assert.Equal(3, transaction.AtomicEffect.VirtualThreadId);
        }

        [Fact]
        public void CsrTransaction_StripsRegisterWriteIntoRetireRecordAndKeepsTypedEffect()
        {
            const ulong oldCsrValue = 0x55UL;
            const ulong newCsrValue = 0xABUL;

            var csr = new CsrFile();
            csr.Write(CsrAddresses.VmxEnable, oldCsrValue, PrivilegeLevel.Machine);

            var state = new FakeCpuState();
            state.SetReg(1, newCsrValue);

            var dispatcher = new ExecutionDispatcherV4(csrFile: csr);
            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.CSRRW,
                rd: 9,
                rs1: 1,
                imm: (long)CsrAddresses.VmxEnable);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 3);

            Assert.Equal(RetireWindowCaptureEffectKind.Csr, transaction.TypedEffectKind);
            Assert.Equal(1, transaction.RetireRecordCount);

            RetireRecord retireRecord = transaction.GetRetireRecord(0);
            Assert.True(retireRecord.IsRegisterWrite);
            Assert.Equal(3, retireRecord.VtId);
            Assert.Equal(9, retireRecord.ArchReg);
            Assert.Equal(oldCsrValue, retireRecord.Value);

            CsrRetireEffect effect = transaction.CsrEffect;
            Assert.False(effect.HasRegisterWriteback);
            Assert.Equal(CsrStorageSurface.WiredCsrFile, effect.StorageSurface);
            Assert.True(effect.HasCsrWrite);
            Assert.Equal((ushort)CsrAddresses.VmxEnable, effect.CsrAddress);
            Assert.Equal(oldCsrValue, effect.ReadValue);
            Assert.Equal(newCsrValue, effect.CsrWriteValue);
        }

        [Fact]
        public void CsrTransaction_OnNonCsrFileBackedVectorCsr_ThrowsExplicitPipelineAuthorityError()
        {
            var dispatcher = new ExecutionDispatcherV4(csrFile: new CsrFile());
            var state = new FakeCpuState();
            state.SetReg(1, 1UL);

            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.CSRRW,
                rd: 9,
                rs1: 1,
                imm: (long)VectorCSR.VLIW_STEAL_ENABLE);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => Capture(dispatcher, ir, state));

            Assert.Contains("non-CsrFile-backed CSR surfaces", ex.Message, StringComparison.Ordinal);
            Assert.Contains("pipeline execution", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(InstructionsEnum.VSETVEXCPMASK)]
        [InlineData(InstructionsEnum.VSETVEXCPPRI)]
        public void VectorExceptionControlCsrTransaction_RejectsDirectCompatContour(
            InstructionsEnum opcode)
        {
            var dispatcher = new ExecutionDispatcherV4(csrFile: new CsrFile());
            var state = new FakeCpuState();
            state.SetReg(1, 0x1234UL);

            InstructionIR ir = IrBuilder.Make(opcode, rs1: 1);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 2));

            Assert.Contains("canonical mainline retire path", ex.Message, StringComparison.Ordinal);
            Assert.Contains("serializing-boundary follow-through", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(InstructionsEnum.Interrupt)]
        [InlineData(InstructionsEnum.InterruptReturn)]
        public void RetainedInterruptSystemTransaction_RejectsDirectCompatContour(
            InstructionsEnum opcode)
        {
            var dispatcher = new ExecutionDispatcherV4();
            var state = new FakeCpuState();

            InstructionIR ir = IrBuilder.Make(opcode);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 2));

            Assert.Contains("Retained system opcode", ex.Message, StringComparison.Ordinal);
            Assert.Contains("retire-window publication contour", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(InstructionsEnum.VSETVL)]
        [InlineData(InstructionsEnum.VSETVLI)]
        [InlineData(InstructionsEnum.VSETIVLI)]
        public void VectorConfigSystemTransaction_RejectsDirectCompatContour(
            InstructionsEnum opcode)
        {
            var dispatcher = new ExecutionDispatcherV4();
            var state = new FakeCpuState();
            state.SetReg(5, 21UL);
            state.SetReg(6, 0x47UL);

            InstructionIR ir = IrBuilder.Make(opcode, rd: 4, rs1: 5, rs2: 6, imm: 13);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 2));

            Assert.Contains("Vector-config opcode", ex.Message, StringComparison.Ordinal);
            Assert.Contains("deferred VectorConfig state publication", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void CsrTransaction_WithoutCsrFile_ThrowsExplicitAuthorityError()
        {
            var dispatcher = new ExecutionDispatcherV4();
            var state = new FakeCpuState();
            state.SetReg(1, 0xABUL);

            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.CSRRW,
                rd: 9,
                rs1: 1,
                imm: (long)CsrAddresses.Mstatus);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => Capture(dispatcher, ir, state));

            Assert.Contains("wired CsrFile", ex.Message, StringComparison.Ordinal);
            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Contains("mainline retire path", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void MretTransaction_CarriesTypedSystemEventWithRetiredPc()
        {
            const ulong retiredPc = 0x9000;
            const ulong bundleSerial = 41;

            var state = new FakeCpuState();
            state.SetInstructionPointer(retiredPc);

            var dispatcher = new ExecutionDispatcherV4();
            InstructionIR ir = IrBuilder.Make(InstructionsEnum.MRET);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial, vtId: 2);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(0, transaction.RetireRecordCount);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, transaction.PipelineEventOrderGuarantee);
            Assert.Equal(retiredPc, transaction.PipelineEventRetiredPc);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);

            MretEvent systemEvent = Assert.IsType<MretEvent>(transaction.PipelineEvent);
            Assert.Equal((byte)2, systemEvent.VtId);
            Assert.Equal(bundleSerial, systemEvent.BundleSerial);
        }

        [Theory]
        [InlineData(InstructionsEnum.ECALL, typeof(EcallEvent))]
        [InlineData(InstructionsEnum.EBREAK, typeof(EbreakEvent))]
        [InlineData(InstructionsEnum.SRET, typeof(SretEvent))]
        public void TrapBoundarySystemTransactions_CarryTypedPipelineEventAndSerializingFollowThrough(
            InstructionsEnum opcode,
            Type expectedEventType)
        {
            const ulong retiredPc = 0x4550;
            const ulong bundleSerial = 17;

            var state = new FakeCpuState();
            state.SetInstructionPointer(retiredPc);
            state.SetReg(17, 93);

            var dispatcher = new ExecutionDispatcherV4();
            InstructionIR ir = IrBuilder.Make(opcode);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial, vtId: 1);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, transaction.PipelineEventOrderGuarantee);
            Assert.Equal(retiredPc, transaction.PipelineEventRetiredPc);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);

            PipelineEvent pipelineEvent =
                Assert.IsAssignableFrom<PipelineEvent>(transaction.PipelineEvent);
            Assert.IsType(expectedEventType, pipelineEvent);
            Assert.Equal((byte)1, pipelineEvent.VtId);
            Assert.Equal(bundleSerial, pipelineEvent.BundleSerial);

            if (pipelineEvent is EcallEvent ecallEvent)
            {
                Assert.Equal(93L, ecallEvent.EcallCode);
            }
        }

        [Theory]
        [InlineData(InstructionsEnum.FENCE, false, SystemEventOrderGuarantee.DrainMemory, false)]
        [InlineData(InstructionsEnum.FENCE_I, true, SystemEventOrderGuarantee.FlushPipeline, true)]
        public void FenceTransactions_CarryTypedPipelineEventWithExpectedOrderingContour(
            InstructionsEnum opcode,
            bool expectInstructionFence,
            SystemEventOrderGuarantee expectedOrderGuarantee,
            bool expectSerializingBoundary)
        {
            const ulong retiredPc = 0x4890;
            const ulong bundleSerial = 21;

            var state = new FakeCpuState();
            state.SetInstructionPointer(retiredPc);

            var dispatcher = new ExecutionDispatcherV4();
            InstructionIR ir = IrBuilder.Make(opcode);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial, vtId: 2);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(expectedOrderGuarantee, transaction.PipelineEventOrderGuarantee);
            Assert.Equal(retiredPc, transaction.PipelineEventRetiredPc);
            Assert.Equal(expectSerializingBoundary, transaction.HasSerializingBoundaryEffect);
            Assert.Equal(expectSerializingBoundary, transaction.HasPipelineEventSerializingBoundaryFollowThrough);

            FenceEvent fenceEvent = Assert.IsType<FenceEvent>(transaction.PipelineEvent);
            Assert.Equal(expectInstructionFence, fenceEvent.IsInstructionFence);
            Assert.Equal((byte)2, fenceEvent.VtId);
            Assert.Equal(bundleSerial, fenceEvent.BundleSerial);
        }

        [Theory]
        [InlineData(InstructionsEnum.WFE, typeof(WfeEvent))]
        [InlineData(InstructionsEnum.SEV, typeof(SevEvent))]
        public void DrainMemorySmtVtTransactions_CarryTypedPipelineEventWithFollowThrough(
            InstructionsEnum opcode,
            Type expectedEventType)
        {
            const ulong retiredPc = 0x8123;
            const ulong bundleSerial = 19;

            var state = new FakeCpuState();
            state.SetInstructionPointer(retiredPc);

            var dispatcher = new ExecutionDispatcherV4();
            InstructionIR ir = IrBuilder.Make(opcode);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial, vtId: 1);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, transaction.PipelineEventOrderGuarantee);
            Assert.Equal(retiredPc, transaction.PipelineEventRetiredPc);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);

            PipelineEvent pipelineEvent =
                Assert.IsAssignableFrom<PipelineEvent>(transaction.PipelineEvent);
            Assert.IsType(expectedEventType, pipelineEvent);
            Assert.Equal((byte)1, pipelineEvent.VtId);
            Assert.Equal(bundleSerial, pipelineEvent.BundleSerial);
        }

        [Theory]
        [InlineData(InstructionsEnum.WFI, typeof(WfiEvent), SystemEventOrderGuarantee.DrainMemory)]
        [InlineData(InstructionsEnum.POD_BARRIER, typeof(PodBarrierEvent), SystemEventOrderGuarantee.None)]
        [InlineData(InstructionsEnum.VT_BARRIER, typeof(VtBarrierEvent), SystemEventOrderGuarantee.None)]
        public void FullSerialEventTransactions_CarryTypedEventAndSerializingBoundaryFollowThrough(
            InstructionsEnum opcode,
            Type expectedEventType,
            SystemEventOrderGuarantee expectedOrderGuarantee)
        {
            const ulong retiredPc = 0x8A10;
            const ulong bundleSerial = 27;

            var state = new FakeCpuState();
            state.SetInstructionPointer(retiredPc);

            var dispatcher = new ExecutionDispatcherV4();
            InstructionIR ir = IrBuilder.Make(opcode);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial, vtId: 2);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(expectedOrderGuarantee, transaction.PipelineEventOrderGuarantee);
            Assert.Equal(retiredPc, transaction.PipelineEventRetiredPc);
            Assert.True(transaction.HasSerializingBoundaryEffect);
            Assert.True(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.IsType(expectedEventType, transaction.PipelineEvent);
        }

        [Fact]
        public void YieldTransaction_CarriesTypedPipelineEventWithoutBoundaryGuarantee()
        {
            const ulong retiredPc = 0x4444;

            var state = new FakeCpuState();
            state.SetInstructionPointer(retiredPc);

            var dispatcher = new ExecutionDispatcherV4();
            InstructionIR ir = IrBuilder.Make(InstructionsEnum.YIELD);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial: 7, vtId: 3);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.Equal(SystemEventOrderGuarantee.None, transaction.PipelineEventOrderGuarantee);
            Assert.Equal(retiredPc, transaction.PipelineEventRetiredPc);
            Assert.False(transaction.HasPipelineEventSerializingBoundaryFollowThrough);
            Assert.False(transaction.HasSerializingBoundaryEffect);
            Assert.IsType<YieldEvent>(transaction.PipelineEvent);
        }

        [Fact]
        public void StreamWaitTransaction_CarriesSerializingBoundaryEffect()
        {
            var dispatcher = new ExecutionDispatcherV4();
            var state = new FakeCpuState();
            state.SetInstructionPointer(0x5000);

            RetireWindowCaptureSnapshot transaction =
                Capture(
                    dispatcher,
                    IrBuilder.Make(InstructionsEnum.STREAM_WAIT),
                    state,
                    bundleSerial: 13,
                    vtId: 2);

            Assert.Equal(RetireWindowCaptureEffectKind.SerializingBoundary, transaction.TypedEffectKind);
            Assert.Equal(0, transaction.RetireRecordCount);
            Assert.True(transaction.HasSerializingBoundaryEffect);
        }

        [Fact]
        public void VmreadTransaction_CarriesTypedVmxEffect()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.VmxEnable, 1UL, PrivilegeLevel.Machine);

            var vmcs = new VmcsManager();
            vmcs.LoadPointer(0x1000);

            var state = new FakeCpuState();
            state.SetReg(1, (ulong)VmcsField.HostPc);

            var dispatcher = new ExecutionDispatcherV4(
                csrFile: csr,
                vmxUnit: new VmxExecutionUnit(csr, vmcs));
            InstructionIR ir = IrBuilder.Make(
                InstructionsEnum.VMREAD,
                rd: 5,
                rs1: 1);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 1);

            Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);
            Assert.Equal(0, transaction.RetireRecordCount);
            Assert.Equal(VmxOperationKind.VmRead, transaction.VmxEffect.Operation);
            Assert.True(transaction.VmxEffect.HasRegisterDestination);
            Assert.Equal((ushort)5, transaction.VmxEffect.RegisterDestination);
            Assert.Equal(VmcsField.HostPc, transaction.VmxEffect.VmcsField);
        }

        [Fact]
        public void VmxOffTransaction_CarriesTypedExitEffect()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.VmxEnable, 1UL, PrivilegeLevel.Machine);

            var vmcs = new VmcsManager();
            var state = new FakeCpuState();
            state.SetCurrentPipelineState(PipelineState.GuestExecution);

            var dispatcher = new ExecutionDispatcherV4(
                csrFile: csr,
                vmxUnit: new VmxExecutionUnit(csr, vmcs));
            InstructionIR ir = IrBuilder.Make(InstructionsEnum.VMXOFF);

            RetireWindowCaptureSnapshot transaction =
                Capture(dispatcher, ir, state, bundleSerial: 0, vtId: 0);

            Assert.Equal(RetireWindowCaptureEffectKind.Vmx, transaction.TypedEffectKind);
            Assert.Equal(VmxOperationKind.VmxOff, transaction.VmxEffect.Operation);
            Assert.True(transaction.VmxEffect.ExitGuestContextOnRetire);
        }

        [Theory]
        [InlineData(InstructionsEnum.STREAM_SETUP, "System opcode")]
        [InlineData(InstructionsEnum.STREAM_START, "System opcode")]
        [InlineData(InstructionsEnum.VSCATTER, "authoritative path")]
        [InlineData(InstructionsEnum.MTILE_LOAD, "authoritative path")]
        public void UnsupportedDirectCompatSurfaces_ThrowExplicitPipelineAuthorityError(
            InstructionsEnum opcode,
            string expectedMessageToken)
        {
            var dispatcher = new ExecutionDispatcherV4();
            var state = new FakeCpuState();
            InstructionIR ir = IrBuilder.Make(opcode);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => Capture(dispatcher, ir, state));

            Assert.Contains(expectedMessageToken, ex.Message, StringComparison.Ordinal);
            Assert.Contains("pipeline execution", ex.Message, StringComparison.Ordinal);
        }
    }
}

