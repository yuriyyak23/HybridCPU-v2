// V6 Phase 3: ISA Table-Driven Decode (B7–B11, A5)
//
// Covers:
//   [T6P3-01] OpcodeInfo.IsMathOrVector is true for Scalar category opcodes
//   [T6P3-02] OpcodeInfo.IsMathOrVector is true for Vector category opcodes
//   [T6P3-03] OpcodeInfo.IsMathOrVector is false for ControlFlow / System / Memory opcodes
//   [T6P3-04] OpcodeRegistry.IsMathOrVectorOp returns correct results
//   [T6P3-05] OpcodeRegistry.IsControlFlowOp returns correct results
//   [T6P3-06] VLIW_Instruction no longer has IsControlFlow or IsMathOrVector properties
//   [T6P3-07] MicroOpDescriptor no longer has IsControlFlow property
//   [T6P3-08] SysEventMicroOp exists and has SystemEventKind property
//   [T6P3-09] SysEventMicroOp materializes a typed PipelineEvent for every SystemEventKind
//   [T6P3-10] StreamControlMicroOp exists and no longer self-classifies as memory
//   [T6P3-11] InstructionRegistry: FENCE/ECALL/EBREAK/MRET/SRET/WFI produce SysEventMicroOp
//   [T6P3-12] InstructionRegistry: STREAM_SETUP/START/WAIT produce StreamControlMicroOp
//   [T6P3-13] MicroOp base class does not expose GeneratedEvent state
//   [T6P3-14] SysEventMicroOp does not persist GeneratedEvent state
//   [T6P3-15] Decoder fallback uses OpcodeRegistry.IsControlFlowOp (no direct VLIW_Instruction call)

using System;
using System.Reflection;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.V6Phase3
{
    // ─────────────────────────────────────────────────────────────────────────
    // [T6P3-01..T6P3-05]  OpcodeInfo.IsMathOrVector and OpcodeRegistry helpers
    // ─────────────────────────────────────────────────────────────────────────

    public class OpcodeInfoIsMathOrVectorTests
    {
        [Fact]
        public void T6P3_00_OpcodeRegistry_SplitAggregate_InitializesAllCollections()
        {
            Assert.NotEmpty(OpcodeRegistry.Opcodes);
            Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.Addition));
            Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.VADD));
            Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.LR_W));
            Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.FENCE));
            Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.VDOT_FP8));
        }

        [Fact]
        public void T6P3_00a_OpcodeRegistry_DoesNotPublishProhibitedCsrWrapperOpcodes()
        {
            Assert.Null(OpcodeRegistry.GetInfo(147u));
            Assert.Null(OpcodeRegistry.GetInfo(148u));
        }

        [Fact]
        public void T6P3_00b_OpcodeInfo_SystemCategoryDefaultsToSystemRuntimeClass()
        {
            var info = new OpcodeInfo(
                opCode: 0xF0F0u,
                mnemonic: "TESTSYS",
                category: OpcodeCategory.System,
                operandCount: 0,
                flags: InstructionFlags.None,
                executionLatency: 1,
                memoryBandwidth: 0);

            Assert.Equal(InstructionClass.System, info.InstructionClass);
            Assert.Equal(SerializationClass.Free, info.SerializationClass);
        }

        [Theory]
        [InlineData(InstructionsEnum.CSR_CLEAR, InstructionClass.Csr, SerializationClass.CsrOrdered)]
        [InlineData(InstructionsEnum.VMXON, InstructionClass.Vmx, SerializationClass.VmxSerial)]
        [InlineData(InstructionsEnum.STREAM_WAIT, InstructionClass.SmtVt, SerializationClass.FullSerial)]
        [InlineData(InstructionsEnum.VSETVL, InstructionClass.System, SerializationClass.FullSerial)]
        [InlineData(InstructionsEnum.VGATHER, InstructionClass.Memory, SerializationClass.Free)]
        [InlineData(InstructionsEnum.VSCATTER, InstructionClass.Memory, SerializationClass.MemoryOrdered)]
        public void T6P3_00c_PublishedSemantics_AgreeAcrossRegistryDescriptorAndClassifier(
            InstructionsEnum opcode,
            InstructionClass expectedClass,
            SerializationClass expectedSerialization)
        {
            Assert.True(
                OpcodeRegistry.TryGetPublishedSemantics(
                    opcode,
                    out InstructionClass publishedClass,
                    out SerializationClass publishedSerialization));

            OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
            Assert.True(info.HasValue);
            Assert.Equal(expectedClass, info.Value.InstructionClass);
            Assert.Equal(expectedSerialization, info.Value.SerializationClass);
            Assert.Equal(expectedClass, publishedClass);
            Assert.Equal(expectedSerialization, publishedSerialization);
            Assert.Equal(expectedClass, InstructionClassifier.GetClass(opcode));
            Assert.Equal(expectedSerialization, InstructionClassifier.GetSerializationClass(opcode));
        }

        [Theory]
        [InlineData(InstructionsEnum.Interrupt, InstructionClass.System, SerializationClass.FullSerial)]
        [InlineData(InstructionsEnum.InterruptReturn, InstructionClass.System, SerializationClass.FullSerial)]
        public void T6P3_00d_UnpublishedContours_StillUseLegacyClassifierFallback(
            InstructionsEnum opcode,
            InstructionClass expectedClass,
            SerializationClass expectedSerialization)
        {
            Assert.False(
                OpcodeRegistry.TryGetPublishedSemantics(
                    opcode,
                    out _,
                    out _));
            Assert.Null(OpcodeRegistry.GetInfo((uint)opcode));
            Assert.Equal(expectedClass, InstructionClassifier.GetClass(opcode));
            Assert.Equal(expectedSerialization, InstructionClassifier.GetSerializationClass(opcode));
        }

        [Theory]
        [InlineData(InstructionsEnum.VSETVEXCPMASK, InstructionClass.Csr, SerializationClass.FullSerial)]
        [InlineData(InstructionsEnum.VSETVEXCPPRI, InstructionClass.Csr, SerializationClass.FullSerial)]
        public void T6P3_00e_PublishedCsrContours_UsePublishedSemantics(
            InstructionsEnum opcode,
            InstructionClass expectedClass,
            SerializationClass expectedSerialization)
        {
            Assert.True(
                OpcodeRegistry.TryGetPublishedSemantics(
                    opcode,
                    out InstructionClass publishedClass,
                    out SerializationClass publishedSerialization));

            OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
            Assert.True(info.HasValue);
            Assert.Equal(expectedClass, info.Value.InstructionClass);
            Assert.Equal(expectedSerialization, info.Value.SerializationClass);
            Assert.Equal(expectedClass, publishedClass);
            Assert.Equal(expectedSerialization, publishedSerialization);
            Assert.Equal(expectedClass, InstructionClassifier.GetClass(opcode));
            Assert.Equal(expectedSerialization, InstructionClassifier.GetSerializationClass(opcode));
        }

        [Theory]
        [InlineData((uint)InstructionsEnum.Addition)]
        [InlineData((uint)InstructionsEnum.Subtraction)]
        [InlineData((uint)InstructionsEnum.Multiplication)]
        [InlineData((uint)InstructionsEnum.ADDI)]
        [InlineData((uint)InstructionsEnum.SLT)]
        [InlineData((uint)InstructionsEnum.SLTU)]
        public void T6P3_01_IsMathOrVector_TrueForScalarOpcodes(uint opCode)
        {
            Assert.True(OpcodeRegistry.IsMathOrVectorOp(opCode),
                $"Expected IsMathOrVectorOp=true for opcode 0x{opCode:X}");
        }

        [Theory]
        [InlineData((uint)InstructionsEnum.VADD)]
        [InlineData((uint)InstructionsEnum.VSUB)]
        [InlineData((uint)InstructionsEnum.VMUL)]
        [InlineData((uint)InstructionsEnum.VDOT_FP8)]
        public void T6P3_02_IsMathOrVector_TrueForVectorOpcodes(uint opCode)
        {
            Assert.True(OpcodeRegistry.IsMathOrVectorOp(opCode),
                $"Expected IsMathOrVectorOp=true for opcode 0x{opCode:X}");
        }

        [Theory]
        [InlineData((uint)InstructionsEnum.JAL)]
        [InlineData((uint)InstructionsEnum.JALR)]
        [InlineData((uint)InstructionsEnum.BEQ)]
        [InlineData((uint)InstructionsEnum.ECALL)]
        [InlineData((uint)InstructionsEnum.FENCE)]
        [InlineData((uint)InstructionsEnum.LD)]
        public void T6P3_03_IsMathOrVector_FalseForNonMathOpcodes(uint opCode)
        {
            Assert.False(OpcodeRegistry.IsMathOrVectorOp(opCode),
                $"Expected IsMathOrVectorOp=false for opcode 0x{opCode:X}");
        }

        [Theory]
        [InlineData((uint)InstructionsEnum.Addition, true)]
        [InlineData((uint)InstructionsEnum.VADD, true)]
        [InlineData((uint)InstructionsEnum.VDOT_FP8, true)]
        [InlineData((uint)InstructionsEnum.JAL, false)]
        [InlineData((uint)InstructionsEnum.ECALL, false)]
        [InlineData((uint)InstructionsEnum.FENCE, false)]
        public void T6P3_04_OpcodeRegistry_IsMathOrVectorOp(uint opCode, bool expected)
        {
            Assert.Equal(expected, OpcodeRegistry.IsMathOrVectorOp(opCode));
        }

        [Theory]
        [InlineData((uint)InstructionsEnum.JAL, true)]
        [InlineData((uint)InstructionsEnum.JALR, true)]
        [InlineData((uint)InstructionsEnum.BEQ, true)]
        [InlineData((uint)InstructionsEnum.Addition, false)]
        [InlineData((uint)InstructionsEnum.ECALL, false)]
        [InlineData((uint)InstructionsEnum.FENCE, false)]
        public void T6P3_05_OpcodeRegistry_IsControlFlowOp(uint opCode, bool expected)
        {
            Assert.Equal(expected, OpcodeRegistry.IsControlFlowOp(opCode));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [T6P3-06..T6P3-07]  VLIW_Instruction and MicroOpDescriptor cleanup
    // ─────────────────────────────────────────────────────────────────────────

    public class Phase3CleanupTests
    {
        [Fact]
        public void T6P3_06a_VLIW_Instruction_HasNo_IsControlFlow_Property()
        {
            var prop = typeof(VLIW_Instruction).GetProperty("IsControlFlow");
            Assert.Null(prop); // Removed in Phase 3 — use OpcodeRegistry.IsControlFlowOp()
        }

        [Fact]
        public void T6P3_06b_VLIW_Instruction_HasNo_IsMathOrVector_Property()
        {
            var prop = typeof(VLIW_Instruction).GetProperty("IsMathOrVector");
            Assert.Null(prop); // Removed in Phase 3 — use OpcodeRegistry.IsMathOrVectorOp()
        }

        [Fact]
        public void T6P3_07_MicroOpDescriptor_HasNo_IsControlFlow_Property()
        {
            var prop = typeof(MicroOpDescriptor).GetProperty("IsControlFlow");
            Assert.Null(prop); // Removed in Phase 3 — architectural class lives in OpcodeInfo
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [T6P3-08..T6P3-10]  New MicroOp types
    // ─────────────────────────────────────────────────────────────────────────

    public class SysEventMicroOpTests
    {
        [Fact]
        public void T6P3_08_SysEventMicroOp_Exists_WithEventKindProperty()
        {
            var t = typeof(SysEventMicroOp);
            Assert.NotNull(t);
            var prop = t.GetProperty("EventKind");
            Assert.NotNull(prop);
            Assert.Equal(typeof(SystemEventKind), prop!.PropertyType);
        }

        [Theory]
        [InlineData(SystemEventKind.Fence,  typeof(FenceEvent))]
        [InlineData(SystemEventKind.FenceI, typeof(FenceEvent))]
        [InlineData(SystemEventKind.Ebreak, typeof(EbreakEvent))]
        [InlineData(SystemEventKind.Mret,   typeof(MretEvent))]
        [InlineData(SystemEventKind.Sret,   typeof(SretEvent))]
        [InlineData(SystemEventKind.Wfi,    typeof(WfiEvent))]
        [InlineData(SystemEventKind.Wfe,    typeof(WfeEvent))]
        [InlineData(SystemEventKind.Sev,    typeof(SevEvent))]
        [InlineData(SystemEventKind.Yield,  typeof(YieldEvent))]
        [InlineData(SystemEventKind.PodBarrier, typeof(PodBarrierEvent))]
        [InlineData(SystemEventKind.VtBarrier,  typeof(VtBarrierEvent))]
        public void T6P3_09_SysEventMicroOp_CreatePipelineEvent_ReturnsTypedEvent(
            SystemEventKind kind, Type expectedEventType)
        {
            var op = new SysEventMicroOp { EventKind = kind };
            var core = default(YAKSys_Hybrid_CPU.Processor.CPU_Core);
            var evt = op.CreatePipelineEvent(ref core);

            Assert.NotNull(evt);
            Assert.IsType(expectedEventType, evt);
        }

        [Fact]
        public void T6P3_09b_SysEventMicroOp_CreatePipelineEvent_EcallWithAuthoritativeA7_ReturnsEcallEvent()
        {
            var op = new SysEventMicroOp { EventKind = SystemEventKind.Ecall };
            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x2600, activeVtId: 0);
            core.WriteCommittedArch(0, 17, 93UL);
            var evt = op.CreatePipelineEvent(ref core);

            var typedEvent = Assert.IsType<EcallEvent>(evt);
            Assert.NotNull(typedEvent);
            Assert.Equal(93L, typedEvent.EcallCode);
        }

        [Fact]
        public void T6P3_09c_SysEventMicroOp_CreatePipelineEvent_EcallWithoutAuthoritativeA7_FailsClosed()
        {
            var op = new SysEventMicroOp { EventKind = SystemEventKind.Ecall };
            var core = default(YAKSys_Hybrid_CPU.Processor.CPU_Core);

            InvalidOperationException ex = Assert.ThrowsAny<InvalidOperationException>(
                () => op.CreatePipelineEvent(ref core));

            Assert.Contains("authoritative a7/x17", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void T6P3_10_StreamControlMicroOp_IsMemoryOp_False()
        {
            var op = new StreamControlMicroOp();
            Assert.False(op.IsMemoryOp);
            Assert.True(op.HasSideEffects);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [T6P3-11..T6P3-12]  InstructionRegistry factory verification
    // ─────────────────────────────────────────────────────────────────────────

    public class InstructionRegistryPhase3Tests
    {
        private static MicroOp Create(InstructionsEnum opcode)
        {
            var ctx = new DecoderContext { OpCode = (uint)opcode };
            return InstructionRegistry.CreateMicroOp((uint)opcode, ctx);
        }

        private static MicroOp Create(uint opcode, VLIW_Instruction instruction)
        {
            VLIW_Instruction.TryUnpackArchRegs(
                instruction.Word1,
                out byte reg1,
                out byte reg2,
                out byte reg3);

            var ctx = new DecoderContext
            {
                OpCode = opcode,
                Immediate = instruction.Immediate,
                HasImmediate = true,
                Reg1ID = reg1,
                Reg2ID = reg2,
                Reg3ID = reg3,
            };

            return InstructionRegistry.CreateMicroOp(opcode, ctx);
        }

        [Theory]
        [InlineData(InstructionsEnum.FENCE,  SystemEventKind.Fence)]
        [InlineData(InstructionsEnum.FENCE_I, SystemEventKind.FenceI)]
        [InlineData(InstructionsEnum.ECALL,  SystemEventKind.Ecall)]
        [InlineData(InstructionsEnum.EBREAK, SystemEventKind.Ebreak)]
        [InlineData(InstructionsEnum.MRET,   SystemEventKind.Mret)]
        [InlineData(InstructionsEnum.SRET,   SystemEventKind.Sret)]
        [InlineData(InstructionsEnum.WFI,    SystemEventKind.Wfi)]
        [InlineData(InstructionsEnum.WFE,    SystemEventKind.Wfe)]
        [InlineData(InstructionsEnum.SEV,    SystemEventKind.Sev)]
        [InlineData(InstructionsEnum.YIELD,  SystemEventKind.Yield)]
        [InlineData(InstructionsEnum.POD_BARRIER, SystemEventKind.PodBarrier)]
        [InlineData(InstructionsEnum.VT_BARRIER,  SystemEventKind.VtBarrier)]
        public void T6P3_11_SystemOpcodes_ProduceSysEventMicroOp(
            InstructionsEnum opcode, SystemEventKind expectedKind)
        {
            var op = Create(opcode);
            var sysOp = Assert.IsType<SysEventMicroOp>(op);
            Assert.Equal(expectedKind, sysOp.EventKind);
        }

        [Theory]
        [InlineData(InstructionsEnum.STREAM_SETUP)]
        [InlineData(InstructionsEnum.STREAM_START)]
        [InlineData(InstructionsEnum.STREAM_WAIT)]
        public void T6P3_12_StreamOpcodes_ProduceStreamControlMicroOp(InstructionsEnum opcode)
        {
            var op = Create(opcode);
            Assert.IsType<StreamControlMicroOp>(op);
        }

        [Theory]
        [InlineData(InstructionsEnum.STREAM_SETUP, InstructionClass.System)]
        [InlineData(InstructionsEnum.STREAM_START, InstructionClass.System)]
        [InlineData(InstructionsEnum.STREAM_WAIT, InstructionClass.SmtVt)]
        public void T6P3_12a_StreamControlRegistryFactory_PublishesCanonicalSingletonClassification(
            InstructionsEnum opcode,
            InstructionClass expectedClass)
        {
            StreamControlMicroOp op = Assert.IsType<StreamControlMicroOp>(Create(opcode));

            Assert.Equal(expectedClass, op.InstructionClass);
            Assert.Equal(InstructionClassifier.GetSerializationClass(opcode), op.SerializationClass);
            Assert.Equal(SlotClass.SystemSingleton, op.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, op.AdmissionMetadata.Placement.PinnedLaneId);
            Assert.False(op.AdmissionMetadata.IsMemoryOp);
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRW, typeof(CsrReadWriteMicroOp))]
        [InlineData(InstructionsEnum.CSRRS, typeof(CsrReadSetMicroOp))]
        [InlineData(InstructionsEnum.CSRRC, typeof(CsrReadClearMicroOp))]
        [InlineData(InstructionsEnum.CSRRWI, typeof(CsrReadWriteImmediateMicroOp))]
        [InlineData(InstructionsEnum.CSRRSI, typeof(CsrReadSetImmediateMicroOp))]
        [InlineData(InstructionsEnum.CSRRCI, typeof(CsrReadClearImmediateMicroOp))]
        [InlineData(InstructionsEnum.CSR_CLEAR, typeof(CsrClearMicroOp))]
        public void T6P3_12c_CsrOpcodes_ProduceExplicitCsrCarrier(InstructionsEnum opcode, Type expectedType)
        {
            var ctx = new DecoderContext
            {
                OpCode = (uint)opcode,
                Immediate = 0x300,
                HasImmediate = true,
                Reg1ID = 2,
                Reg2ID = 1,
            };

            MicroOp op = InstructionRegistry.CreateMicroOp((uint)opcode, ctx);

            Assert.IsType(expectedType, op);
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRW, typeof(CsrReadWriteMicroOp), true)]
        [InlineData(InstructionsEnum.CSRRWI, typeof(CsrReadWriteImmediateMicroOp), false)]
        public void T6P3_12c_CsrRegistryFactory_NoDestinationNoArchReg_DoesNotPublishPhantomWriteback(
            InstructionsEnum opcode,
            Type expectedType,
            bool readsSourceRegister)
        {
            const byte sourceRegister = 4;
            const byte immediateValue = 7;

            var ctx = new DecoderContext
            {
                OpCode = (uint)opcode,
                Immediate = CsrAddresses.Mstatus,
                HasImmediate = true,
                Reg1ID = VLIW_Instruction.NoArchReg,
                Reg2ID = readsSourceRegister ? sourceRegister : immediateValue,
            };

            CSRMicroOp op = Assert.IsAssignableFrom<CSRMicroOp>(
                InstructionRegistry.CreateMicroOp((uint)opcode, ctx));
            Assert.IsType(expectedType, op);

            Assert.Equal(VLIW_Instruction.NoReg, op.DestRegID);
            Assert.False(op.WritesRegister);
            Assert.False(op.AdmissionMetadata.WritesRegister);
            Assert.Empty(op.WriteRegisters);
            Assert.Empty(op.AdmissionMetadata.WriteRegisters);

            if (readsSourceRegister)
            {
                Assert.Equal(new[] { (int)sourceRegister }, op.ReadRegisters);
                Assert.Equal(new[] { (int)sourceRegister }, op.AdmissionMetadata.ReadRegisters);
            }
            else
            {
                Assert.Empty(op.ReadRegisters);
                Assert.Empty(op.AdmissionMetadata.ReadRegisters);
            }
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRW)]
        [InlineData(InstructionsEnum.CSRRS)]
        [InlineData(InstructionsEnum.CSRRC)]
        public void T6P3_12c_CsrRegistryFactory_NoArchRegSource_RejectsNonCanonicalManualPublication(
            InstructionsEnum opcode)
        {
            var ctx = new DecoderContext
            {
                OpCode = (uint)opcode,
                Immediate = CsrAddresses.Mstatus,
                HasImmediate = true,
                Reg1ID = 9,
                Reg2ID = VLIW_Instruction.NoArchReg,
            };

            InvalidOperationException ex = Assert.ThrowsAny<InvalidOperationException>(
                () => InstructionRegistry.CreateMicroOp((uint)opcode, ctx));

            Assert.Contains("non-canonical rs1 register encoding", ex.Message, StringComparison.Ordinal);
            Assert.Contains("runtime follow-through", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRWI)]
        [InlineData(InstructionsEnum.CSRRSI)]
        [InlineData(InstructionsEnum.CSRRCI)]
        public void T6P3_12c_CsrRegistryFactory_NonCanonicalZimm_RejectsCompatibilityMasking(
            InstructionsEnum opcode)
        {
            var ctx = new DecoderContext
            {
                OpCode = (uint)opcode,
                Immediate = CsrAddresses.Mstatus,
                HasImmediate = true,
                Reg1ID = 9,
                Reg2ID = VLIW_Instruction.NoArchReg,
            };

            InvalidOperationException ex = Assert.ThrowsAny<InvalidOperationException>(
                () => InstructionRegistry.CreateMicroOp((uint)opcode, ctx));

            Assert.Contains("non-canonical zimm immediate encoding", ex.Message, StringComparison.Ordinal);
            Assert.Contains("compatibility masking", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(InstructionsEnum.VMREAD, (byte)7, (byte)3, (byte)0, new[] { 3 }, new[] { 7 })]
        [InlineData(InstructionsEnum.VMWRITE, (byte)0, (byte)2, (byte)9, new[] { 2, 9 }, new int[0])]
        [InlineData(InstructionsEnum.VMPTRLD, (byte)0, (byte)11, (byte)0, new[] { 11 }, new int[0])]
        [InlineData(InstructionsEnum.VMCLEAR, (byte)0, (byte)12, (byte)0, new[] { 12 }, new int[0])]
        public void T6P3_12d_VmxRegistryFactory_PublishesCanonicalRegisterFacts(
            InstructionsEnum opcode,
            byte rd,
            byte rs1,
            byte rs2,
            int[] expectedReads,
            int[] expectedWrites)
        {
            var ctx = new DecoderContext
            {
                OpCode = (uint)opcode,
                Reg1ID = rd,
                Reg2ID = rs1,
                Reg3ID = rs2
            };

            VmxMicroOp op = Assert.IsType<VmxMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, ctx));

            Assert.Equal(expectedReads, op.ReadRegisters);
            Assert.Equal(expectedWrites, op.WriteRegisters);
            Assert.Equal(expectedReads, op.AdmissionMetadata.ReadRegisters);
            Assert.Equal(expectedWrites, op.AdmissionMetadata.WriteRegisters);
            Assert.Equal(InstructionClass.Vmx, op.InstructionClass);
            Assert.Equal(SerializationClass.VmxSerial, op.SerializationClass);
            Assert.Equal(SlotClass.SystemSingleton, op.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, op.AdmissionMetadata.Placement.PinnedLaneId);
        }

        [Fact]
        public void T6P3_12d_VmxRegistryFactory_NoDestinationNoReg_DoesNotPublishPhantomWriteback()
        {
            const byte fieldSelectorRegister = 3;

            var ctx = new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.VMREAD,
                Reg1ID = VLIW_Instruction.NoReg,
                Reg2ID = fieldSelectorRegister,
                Reg3ID = VLIW_Instruction.NoReg
            };

            VmxMicroOp op = Assert.IsType<VmxMicroOp>(
                InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VMREAD, ctx));

            Assert.Equal(VLIW_Instruction.NoArchReg, op.Rd);
            Assert.Equal(VLIW_Instruction.NoReg, op.DestRegID);
            Assert.False(op.WritesRegister);
            Assert.False(op.AdmissionMetadata.WritesRegister);
            Assert.Equal(new[] { (int)fieldSelectorRegister }, op.ReadRegisters);
            Assert.Empty(op.WriteRegisters);
            Assert.Equal(new[] { (int)fieldSelectorRegister }, op.AdmissionMetadata.ReadRegisters);
            Assert.Empty(op.AdmissionMetadata.WriteRegisters);
        }

        [Theory]
        [InlineData(InstructionsEnum.VLOAD)]
        [InlineData(InstructionsEnum.VSTORE)]
        public void T6P3_12b_VectorTransferOpcodes_ProduceVectorTransferMicroOp(InstructionsEnum opcode)
        {
            var instruction = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                DestSrc1Pointer = 0x1000,
                Src2Pointer = 0x2000,
                StreamLength = 8,
                Stride = 4,
            };

            var ctx = new DecoderContext
            {
                OpCode = (uint)opcode,
                DataType = instruction.DataType,
                HasDataType = true,
                IndexedAddressing = instruction.Indexed,
                Is2DAddressing = instruction.Is2D,
                HasVectorAddressingContour = true,
                VectorPrimaryPointer = instruction.DestSrc1Pointer,
                VectorSecondaryPointer = instruction.Src2Pointer,
                VectorStreamLength = instruction.StreamLength,
                VectorStride = instruction.Stride,
                VectorRowStride = instruction.RowStride,
                TailAgnostic = instruction.TailAgnostic,
                MaskAgnostic = instruction.MaskAgnostic,
                HasVectorPayload = true,
                PredicateMask = instruction.PredicateMask,
            };
            var op = InstructionRegistry.CreateMicroOp((uint)opcode, ctx);

            var transfer = Assert.IsType<VectorTransferMicroOp>(op);
            switch (opcode)
            {
                case InstructionsEnum.VLOAD:
                    Assert.Equal((ulong)0x2000, Assert.Single(transfer.ReadMemoryRanges).Address);
                    Assert.Equal((ulong)0x1000, Assert.Single(transfer.WriteMemoryRanges).Address);
                    break;

                case InstructionsEnum.VSTORE:
                    Assert.Equal((ulong)0x1000, Assert.Single(transfer.ReadMemoryRanges).Address);
                    Assert.Equal((ulong)0x2000, Assert.Single(transfer.WriteMemoryRanges).Address);
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected vector transfer opcode {opcode}.");
            }
        }

        [Fact]
        public void T6P3_12e_CsrReadHelper_LowersToCanonicalCsrReadCarrier()
        {
            const ushort destinationRegister = 6;

            VLIW_Instruction instruction =
                InstructionEncoder.EncodeCSRRead(CsrAddresses.Mstatus, destinationRegister);

            Assert.Equal((uint)InstructionsEnum.CSRRS, instruction.OpCode);
            Assert.True(VLIW_Instruction.TryUnpackArchRegs(
                instruction.Word1,
                out byte rd,
                out byte rs1,
                out byte rs2));
            Assert.Equal(destinationRegister, rd);
            Assert.Equal(0, rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, rs2);

            CSRMicroOp op = Assert.IsType<CsrReadSetMicroOp>(Create((uint)InstructionsEnum.CSRRS, instruction));

            Assert.True(op.WritesRegister);
            Assert.Empty(op.ReadRegisters);
            Assert.Equal(new[] { (int)destinationRegister }, op.WriteRegisters);
            Assert.Empty(op.AdmissionMetadata.ReadRegisters);
            Assert.Equal(new[] { (int)destinationRegister }, op.AdmissionMetadata.WriteRegisters);
            Assert.Equal(InstructionClass.Csr, op.InstructionClass);
            Assert.Equal(SerializationClass.CsrOrdered, op.SerializationClass);
            Assert.Equal(SlotClass.SystemSingleton, op.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, op.AdmissionMetadata.Placement.PinnedLaneId);
        }

        [Fact]
        public void T6P3_12e_InstructionRegistry_DoesNotRegisterRawCsrReadWrapperOpcode()
        {
            Assert.False(InstructionRegistry.IsRegistered(147u));

            InvalidOperationException ex = Assert.ThrowsAny<InvalidOperationException>(
                () => InstructionRegistry.CreateMicroOp(147u, new DecoderContext { OpCode = 147u }));

            Assert.Contains("Unsupported instruction opcode: 0x93", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void T6P3_12f_CsrWriteHelper_LowersToCanonicalCsrWriteCarrier()
        {
            const ushort sourceRegister = 5;

            VLIW_Instruction instruction =
                InstructionEncoder.EncodeCSRWrite(CsrAddresses.Mstatus, sourceRegister);

            Assert.Equal((uint)InstructionsEnum.CSRRW, instruction.OpCode);
            Assert.True(VLIW_Instruction.TryUnpackArchRegs(
                instruction.Word1,
                out byte rd,
                out byte rs1,
                out byte rs2));
            Assert.Equal(VLIW_Instruction.NoArchReg, rd);
            Assert.Equal(sourceRegister, rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, rs2);

            CSRMicroOp op = Assert.IsType<CsrReadWriteMicroOp>(Create((uint)InstructionsEnum.CSRRW, instruction));

            Assert.False(op.WritesRegister);
            Assert.Equal(sourceRegister, op.SrcRegID);
            Assert.Equal(new[] { (int)sourceRegister }, op.ReadRegisters);
            Assert.Empty(op.WriteRegisters);
            Assert.Equal(new[] { (int)sourceRegister }, op.AdmissionMetadata.ReadRegisters);
            Assert.Empty(op.AdmissionMetadata.WriteRegisters);
            Assert.Equal(InstructionClass.Csr, op.InstructionClass);
            Assert.Equal(SerializationClass.CsrOrdered, op.SerializationClass);
            Assert.Equal(SlotClass.SystemSingleton, op.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, op.AdmissionMetadata.Placement.PinnedLaneId);
        }

        [Fact]
        public void T6P3_12f_InstructionRegistry_DoesNotRegisterRawCsrWriteWrapperOpcode()
        {
            Assert.False(InstructionRegistry.IsRegistered(148u));

            InvalidOperationException ex = Assert.ThrowsAny<InvalidOperationException>(
                () => InstructionRegistry.CreateMicroOp(148u, new DecoderContext { OpCode = 148u }));

            Assert.Contains("Unsupported instruction opcode: 0x94", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void T6P3_12g_LegacyCsrClearRegistryFactory_PublishesCanonicalSingletonClassification()
        {
            VLIW_Instruction instruction =
                InstructionEncoder.EncodeClearExceptionCounters();

            CSRMicroOp op = Assert.IsType<CsrClearMicroOp>(
                Create((uint)InstructionsEnum.CSR_CLEAR, instruction));

            Assert.False(op.WritesRegister);
            Assert.Empty(op.ReadRegisters);
            Assert.Empty(op.WriteRegisters);
            Assert.Empty(op.AdmissionMetadata.ReadRegisters);
            Assert.Empty(op.AdmissionMetadata.WriteRegisters);
            Assert.Equal(InstructionClass.Csr, op.InstructionClass);
            Assert.Equal(SerializationClass.CsrOrdered, op.SerializationClass);
            Assert.Equal(SlotClass.SystemSingleton, op.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.AdmissionMetadata.Placement.PinningKind);
            Assert.Equal(7, op.AdmissionMetadata.Placement.PinnedLaneId);
        }
    }

    public class PlatformAsmFacadeCsrLoweringTests
    {
        [Fact]
        public void T6P3_12h_PlatformAsmFacade_CsrRead_LowersToCanonicalCsrrs()
        {
            var context = new HybridCpuThreadCompilerContext(0);
            var facade = new PlatformAsmFacade(0, context);

            facade.CsrRead(new AsmRegister(6), CsrAddresses.Mstatus);

            Assert.Equal(1, context.InstructionCount);
            VLIW_Instruction instruction = context.GetCompiledInstructions()[0];

            Assert.Equal((uint)InstructionsEnum.CSRRS, instruction.OpCode);
            Assert.Equal(CsrAddresses.Mstatus, instruction.Immediate);
            Assert.True(VLIW_Instruction.TryUnpackArchRegs(
                instruction.DestSrc1Pointer,
                out byte rd,
                out byte rs1,
                out byte rs2));
            Assert.Equal(6, rd);
            Assert.Equal(0, rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, rs2);
        }

        [Fact]
        public void T6P3_12i_PlatformAsmFacade_CsrWrite_LowersToCanonicalCsrrw()
        {
            var context = new HybridCpuThreadCompilerContext(0);
            var facade = new PlatformAsmFacade(0, context);

            facade.CsrWrite(CsrAddresses.Mstatus, new AsmRegister(5));

            Assert.Equal(1, context.InstructionCount);
            VLIW_Instruction instruction = context.GetCompiledInstructions()[0];

            Assert.Equal((uint)InstructionsEnum.CSRRW, instruction.OpCode);
            Assert.Equal(CsrAddresses.Mstatus, instruction.Immediate);
            Assert.True(VLIW_Instruction.TryUnpackArchRegs(
                instruction.DestSrc1Pointer,
                out byte rd,
                out byte rs1,
                out byte rs2));
            Assert.Equal(VLIW_Instruction.NoArchReg, rd);
            Assert.Equal(5, rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, rs2);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [T6P3-13..T6P3-14]  GeneratedEvent moved from MicroOp state into lane state
    // ─────────────────────────────────────────────────────────────────────────

    public class GeneratedEventPropertyTests
    {
        [Fact]
        public void T6P3_13_MicroOp_DoesNotExposeGeneratedEvent_Property()
        {
            var prop = typeof(MicroOp).GetProperty("GeneratedEvent");
            Assert.Null(prop);
        }

        [Fact]
        public void T6P3_14_SysEventMicroOp_DoesNotPersistGeneratedEvent_State()
        {
            var op = new SysEventMicroOp { EventKind = SystemEventKind.Fence };
            Assert.Null(typeof(SysEventMicroOp).GetProperty("GeneratedEvent"));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [T6P3-16..T6P3-19]  B9 — OpcodeRegistry table-driven StreamEngine helpers
    // ─────────────────────────────────────────────────────────────────────────

    public class OpcodeRegistryB9HelperTests
    {
        // --- IsComparisonOp ---

        [Theory]
        [InlineData((uint)InstructionsEnum.VCMPEQ, true)]
        [InlineData((uint)InstructionsEnum.VCMPNE, true)]
        [InlineData((uint)InstructionsEnum.VCMPLT, true)]
        [InlineData((uint)InstructionsEnum.VCMPLE, true)]
        [InlineData((uint)InstructionsEnum.VCMPGT, true)]
        [InlineData((uint)InstructionsEnum.VCMPGE, true)]
        // mask-manip ops share Comparison category but must NOT be classified as comparison
        [InlineData((uint)InstructionsEnum.VMAND,  false)]
        [InlineData((uint)InstructionsEnum.VMOR,   false)]
        [InlineData((uint)InstructionsEnum.VMXOR,  false)]
        [InlineData((uint)InstructionsEnum.VMNOT,  false)]
        [InlineData((uint)InstructionsEnum.VPOPC,  false)]
        [InlineData((uint)InstructionsEnum.VADD,   false)]
        [InlineData((uint)InstructionsEnum.FENCE,  false)]
        public void T6P3_16_IsComparisonOp(uint opCode, bool expected)
        {
            Assert.Equal(expected, OpcodeRegistry.IsComparisonOp(opCode));
        }

        // --- IsMaskManipOp ---

        [Theory]
        [InlineData((uint)InstructionsEnum.VMAND,  true)]
        [InlineData((uint)InstructionsEnum.VMOR,   true)]
        [InlineData((uint)InstructionsEnum.VMXOR,  true)]
        [InlineData((uint)InstructionsEnum.VMNOT,  true)]
        [InlineData((uint)InstructionsEnum.VPOPC,  true)]
        [InlineData((uint)InstructionsEnum.VCMPEQ, false)]
        [InlineData((uint)InstructionsEnum.VCMPGE, false)]
        [InlineData((uint)InstructionsEnum.VADD,   false)]
        [InlineData((uint)InstructionsEnum.VREDSUM, false)]
        public void T6P3_17_IsMaskManipOp(uint opCode, bool expected)
        {
            Assert.Equal(expected, OpcodeRegistry.IsMaskManipOp(opCode));
        }

        // --- IsFmaOp ---

        [Theory]
        [InlineData((uint)InstructionsEnum.VFMADD,  true)]
        [InlineData((uint)InstructionsEnum.VFMSUB,  true)]
        [InlineData((uint)InstructionsEnum.VFNMADD, true)]
        [InlineData((uint)InstructionsEnum.VFNMSUB, true)]
        [InlineData((uint)InstructionsEnum.VADD,    false)]
        [InlineData((uint)InstructionsEnum.VMUL,    false)]
        [InlineData((uint)InstructionsEnum.VCMPEQ,  false)]
        [InlineData((uint)InstructionsEnum.VREDSUM, false)]
        public void T6P3_18_IsFmaOp(uint opCode, bool expected)
        {
            Assert.Equal(expected, OpcodeRegistry.IsFmaOp(opCode));
        }

        // --- IsReductionOp ---

        [Theory]
        [InlineData((uint)InstructionsEnum.VREDSUM, true)]
        [InlineData((uint)InstructionsEnum.VREDMAX, true)]
        [InlineData((uint)InstructionsEnum.VREDMIN, true)]
        [InlineData((uint)InstructionsEnum.VREDAND, true)]
        [InlineData((uint)InstructionsEnum.VREDOR,  true)]
        [InlineData((uint)InstructionsEnum.VREDXOR, true)]
        [InlineData((uint)InstructionsEnum.VDOT, true)]
        [InlineData((uint)InstructionsEnum.VDOTU, true)]
        [InlineData((uint)InstructionsEnum.VDOTF, true)]
        [InlineData((uint)InstructionsEnum.VDOT_FP8, true)]
        // VPOPC has Reduction flag but is a MaskManip op — must NOT appear here
        [InlineData((uint)InstructionsEnum.VPOPC,   false)]
        [InlineData((uint)InstructionsEnum.VADD,    false)]
        [InlineData((uint)InstructionsEnum.VCMPEQ,  false)]
        [InlineData((uint)InstructionsEnum.FENCE,   false)]
        public void T6P3_19_IsReductionOp(uint opCode, bool expected)
        {
            Assert.Equal(expected, OpcodeRegistry.IsReductionOp(opCode));
        }
    }
}

