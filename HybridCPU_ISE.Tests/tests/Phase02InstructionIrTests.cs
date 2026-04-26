using System;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase02
{
    /// <summary>
    /// Phase 02: Instruction IR — classifier and MicroOp classification tests.
    /// </summary>
    public sealed class Phase02InstructionIrTests
    {
        // ─── InstructionClassifier.GetClass ──────────────────────────────────────────

        [Fact]
        public void GetClass_ScalarAluOpcodes_ReturnScalarAlu()
        {
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.Addition));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.Subtraction));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.Multiplication));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.Division));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.Modulus));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.ShiftLeft));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.ShiftRight));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.XOR));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.OR));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.AND));
        }

        [Fact]
        public void GetClass_ImmediateAluOpcodes_ReturnScalarAlu()
        {
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.ADDI));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.ANDI));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.ORI));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.XORI));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.SLTI));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.SLTIU));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.SLLI));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.SRLI));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.SRAI));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.LUI));
            Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.AUIPC));
        }

        [Fact]
        public void GetClass_LoadOpcodes_ReturnMemory()
        {
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.LB));
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.LBU));
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.LH));
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.LHU));
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.LW));
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.LWU));
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.LD));
        }

        [Fact]
        public void GetClass_StoreOpcodes_ReturnMemory()
        {
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.SB));
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.SH));
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.SW));
            Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.SD));
        }

        [Fact]
        public void GetClass_BranchAndJumpOpcodes_ReturnControlFlow()
        {
            Assert.Equal(InstructionClass.ControlFlow, InstructionClassifier.GetClass(InstructionsEnum.JAL));
            Assert.Equal(InstructionClass.ControlFlow, InstructionClassifier.GetClass(InstructionsEnum.JALR));
            Assert.Equal(InstructionClass.ControlFlow, InstructionClassifier.GetClass(InstructionsEnum.BEQ));
            Assert.Equal(InstructionClass.ControlFlow, InstructionClassifier.GetClass(InstructionsEnum.BNE));
            Assert.Equal(InstructionClass.ControlFlow, InstructionClassifier.GetClass(InstructionsEnum.BLT));
            Assert.Equal(InstructionClass.ControlFlow, InstructionClassifier.GetClass(InstructionsEnum.BGE));
            Assert.Equal(InstructionClass.ControlFlow, InstructionClassifier.GetClass(InstructionsEnum.BLTU));
            Assert.Equal(InstructionClass.ControlFlow, InstructionClassifier.GetClass(InstructionsEnum.BGEU));
        }

        [Fact]
        public void GetClass_AtomicOpcodes_ReturnAtomic()
        {
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.LR_W));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.SC_W));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.LR_D));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.SC_D));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.AMOADD_W));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.AMOSWAP_W));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.AMOOR_W));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.AMOAND_W));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.AMOXOR_W));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.AMOMIN_W));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.AMOMAX_W));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.AMOMINU_W));
            Assert.Equal(InstructionClass.Atomic, InstructionClassifier.GetClass(InstructionsEnum.AMOMAXU_W));
        }

        [Fact]
        public void GetClass_SystemOpcodes_ReturnSystem()
        {
            Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(InstructionsEnum.FENCE));
            Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(InstructionsEnum.FENCE_I));
            Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(InstructionsEnum.ECALL));
            Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(InstructionsEnum.EBREAK));
            Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(InstructionsEnum.MRET));
            Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(InstructionsEnum.SRET));
            Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(InstructionsEnum.WFI));
            Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(InstructionsEnum.Interrupt));
            Assert.Equal(InstructionClass.System, InstructionClassifier.GetClass(InstructionsEnum.InterruptReturn));
        }

        [Fact]
        public void GetClass_CsrOpcodes_ReturnCsr()
        {
            Assert.Equal(InstructionClass.Csr, InstructionClassifier.GetClass(InstructionsEnum.CSRRW));
            Assert.Equal(InstructionClass.Csr, InstructionClassifier.GetClass(InstructionsEnum.CSRRS));
            Assert.Equal(InstructionClass.Csr, InstructionClassifier.GetClass(InstructionsEnum.CSRRC));
            Assert.Equal(InstructionClass.Csr, InstructionClassifier.GetClass(InstructionsEnum.CSRRWI));
            Assert.Equal(InstructionClass.Csr, InstructionClassifier.GetClass(InstructionsEnum.CSRRSI));
            Assert.Equal(InstructionClass.Csr, InstructionClassifier.GetClass(InstructionsEnum.CSRRCI));
            Assert.Equal(InstructionClass.Csr, InstructionClassifier.GetClass(InstructionsEnum.CSR_CLEAR));
        }

        [Fact]
        public void GetClass_StreamWait_ReturnSmtVt()
        {
            Assert.Equal(InstructionClass.SmtVt, InstructionClassifier.GetClass(InstructionsEnum.STREAM_WAIT));
        }

        // ─── InstructionClassifier.GetSerializationClass ─────────────────────────────

        [Fact]
        public void GetSerializationClass_AluAndLoads_ReturnFree()
        {
            Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.ADDI));
            Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.LW));
            Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.JAL));
            Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.BEQ));
        }

        [Fact]
        public void GetSerializationClass_StoreOpcodes_ReturnMemoryOrdered()
        {
            Assert.Equal(SerializationClass.MemoryOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.SB));
            Assert.Equal(SerializationClass.MemoryOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.SH));
            Assert.Equal(SerializationClass.MemoryOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.SW));
            Assert.Equal(SerializationClass.MemoryOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.SD));
            Assert.Equal(SerializationClass.MemoryOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.FENCE));
        }

        [Fact]
        public void GetSerializationClass_AtomicOpcodes_ReturnAtomicSerial()
        {
            Assert.Equal(SerializationClass.AtomicSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.LR_W));
            Assert.Equal(SerializationClass.AtomicSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.SC_W));
            Assert.Equal(SerializationClass.AtomicSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.AMOADD_W));
            Assert.Equal(SerializationClass.AtomicSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.AMOSWAP_W));
        }

        [Fact]
        public void GetSerializationClass_CsrOpcodes_ReturnCsrOrdered()
        {
            Assert.Equal(SerializationClass.CsrOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.CSRRW));
            Assert.Equal(SerializationClass.CsrOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.CSRRS));
            Assert.Equal(SerializationClass.CsrOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.CSRRC));
            Assert.Equal(SerializationClass.CsrOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.CSR_CLEAR));
        }

        [Fact]
        public void GetSerializationClass_TrapOpcodes_ReturnFullSerial()
        {
            Assert.Equal(SerializationClass.FullSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.ECALL));
            Assert.Equal(SerializationClass.FullSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.EBREAK));
            Assert.Equal(SerializationClass.FullSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.MRET));
            Assert.Equal(SerializationClass.FullSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.SRET));
            Assert.Equal(SerializationClass.FullSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.WFI));
            Assert.Equal(SerializationClass.FullSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.FENCE_I));
            Assert.Equal(SerializationClass.FullSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.STREAM_WAIT));
            Assert.Equal(SerializationClass.FullSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.VSETVEXCPMASK));
            Assert.Equal(SerializationClass.FullSerial, InstructionClassifier.GetSerializationClass(InstructionsEnum.VSETVEXCPPRI));
        }

        // ─── InstructionClassifier.Classify (pair method) ────────────────────────────

        [Fact]
        public void Classify_ReturnsBothValues_Consistently()
        {
            var (cls, ser) = InstructionClassifier.Classify(InstructionsEnum.AMOADD_W);
            Assert.Equal(InstructionClass.Atomic, cls);
            Assert.Equal(SerializationClass.AtomicSerial, ser);
        }

        [Fact]
        public void Classify_SW_MemoryOrderedAndMemoryClass()
        {
            var (cls, ser) = InstructionClassifier.Classify(InstructionsEnum.SW);
            Assert.Equal(InstructionClass.Memory, cls);
            Assert.Equal(SerializationClass.MemoryOrdered, ser);
        }

        // ─── MicroOp subclass InstructionClass defaults ───────────────────────────────

        [Fact]
        public void ScalarALUMicroOp_HasScalarAluClassAndFreeSerial()
        {
            var op = new ScalarALUMicroOp();
            Assert.Equal(InstructionClass.ScalarAlu, op.InstructionClass);
            Assert.Equal(SerializationClass.Free, op.SerializationClass);
        }

        [Fact]
        public void LoadMicroOp_HasMemoryClassAndFreeSerial()
        {
            var op = new LoadMicroOp();
            Assert.Equal(InstructionClass.Memory, op.InstructionClass);
            Assert.Equal(SerializationClass.Free, op.SerializationClass);
        }

        [Fact]
        public void StoreMicroOp_HasMemoryClassAndMemoryOrderedSerial()
        {
            var op = new StoreMicroOp();
            Assert.Equal(InstructionClass.Memory, op.InstructionClass);
            Assert.Equal(SerializationClass.MemoryOrdered, op.SerializationClass);
        }

        [Fact]
        public void BranchMicroOp_HasControlFlowClassAndFreeSerial()
        {
            var op = new BranchMicroOp();
            Assert.Equal(InstructionClass.ControlFlow, op.InstructionClass);
            Assert.Equal(SerializationClass.Free, op.SerializationClass);
        }

        [Fact]
        public void CsrReadWriteMicroOp_HasCsrClassAndCsrOrderedSerial()
        {
            var op = new CsrReadWriteMicroOp();
            Assert.Equal(InstructionClass.Csr, op.InstructionClass);
            Assert.Equal(SerializationClass.CsrOrdered, op.SerializationClass);
        }

        [Fact]
        public void HaltMicroOp_HasSystemClassAndFullSerialSerial()
        {
            var op = new HaltMicroOp();
            Assert.Equal(InstructionClass.System, op.InstructionClass);
            Assert.Equal(SerializationClass.FullSerial, op.SerializationClass);
        }

        [Fact]
        public void SysEventMicroOp_HasSystemClassAndFullSerialSerial()
        {
            var op = new SysEventMicroOp { EventKind = SystemEventKind.Fence };
            Assert.Equal(InstructionClass.System, op.InstructionClass);
            Assert.Equal(SerializationClass.FullSerial, op.SerializationClass);
        }

        [Theory]
        [InlineData(InstructionsEnum.FENCE)]
        [InlineData(InstructionsEnum.YIELD)]
        [InlineData(InstructionsEnum.VT_BARRIER)]
        public void SysEventFactoryHelpers_PublishCanonicalOpcodeIdentity(
            InstructionsEnum opcode)
        {
            SysEventMicroOp op = opcode switch
            {
                InstructionsEnum.FENCE => SysEventMicroOp.ForFence(),
                InstructionsEnum.YIELD => SysEventMicroOp.ForYield(),
                InstructionsEnum.VT_BARRIER => SysEventMicroOp.ForVtBarrier(),
                _ => throw new InvalidOperationException($"Unexpected opcode {opcode}."),
            };

            Assert.Equal((uint)opcode, op.OpCode);
            Assert.True(
                OpcodeRegistry.TryGetPublishedSemantics(
                    opcode,
                    out InstructionClass publishedClass,
                    out SerializationClass publishedSerialization));
            Assert.Equal(publishedClass, op.InstructionClass);
            Assert.Equal(publishedSerialization, op.SerializationClass);
        }

        [Fact]
        public void MoveMicroOp_HasScalarAluClassAndFreeSerial()
        {
            var op = new MoveMicroOp();
            Assert.Equal(InstructionClass.ScalarAlu, op.InstructionClass);
            Assert.Equal(SerializationClass.Free, op.SerializationClass);
        }

        [Fact]
        public void IncrDecrMicroOp_HasScalarAluClassAndFreeSerial()
        {
            var op = new IncrDecrMicroOp();
            Assert.Equal(InstructionClass.ScalarAlu, op.InstructionClass);
            Assert.Equal(SerializationClass.Free, op.SerializationClass);
        }

        // ─── InstructionIR record creation ────────────────────────────────────────────

        [Fact]
        public void InstructionIR_CanBeConstructedWithRequiredFields()
        {
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.ADDI,
                Class = InstructionClass.ScalarAlu,
                SerializationClass = SerializationClass.Free,
                Rd = 1,
                Rs1 = 2,
                Rs2 = 0,
                Imm = 42,
            };

            Assert.Equal(InstructionsEnum.ADDI, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
            Assert.Equal(SerializationClass.Free, ir.SerializationClass);
            Assert.Equal(42L, ir.Imm);
        }

        [Fact]
        public void InstructionIR_CanonicalOpcode_SeparatesStorageFromLegacyBridge()
        {
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.ADDI,
                Class = InstructionClass.ScalarAlu,
                SerializationClass = SerializationClass.Free,
                Rd = 1,
                Rs1 = 2,
                Rs2 = 0,
                Imm = 42,
            };

            Assert.Equal(typeof(IsaOpcode), ir.CanonicalOpcode.GetType());
            Assert.Equal(InstructionsEnum.ADDI, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal((uint)InstructionsEnum.ADDI, (uint)ir.CanonicalOpcode);
        }

        [Fact]
        public void InstructionIR_IsImmutable()
        {
            var ir1 = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.ADDI,
                Class = InstructionClass.ScalarAlu,
                SerializationClass = SerializationClass.Free,
                Rd = 1,
                Rs1 = 2,
                Rs2 = 0,
                Imm = 0,
            };
            var ir2 = ir1 with { Imm = 99 };

            Assert.Equal(0L, ir1.Imm);
            Assert.Equal(99L, ir2.Imm);
            Assert.Equal(ir1.CanonicalOpcode, ir2.CanonicalOpcode);
        }
    }
}
