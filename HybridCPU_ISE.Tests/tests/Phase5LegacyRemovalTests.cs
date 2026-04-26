// V5 Phase 5 + V6 Phase 7: Legacy Removal — LegacyExecutionShim deleted, InstructionIR.SafetyMask removed,
//             MicroOp.CanBeStolen removed, CompilerContract.Version = 6 (bumped in V6 Phase 7)
//
// Covers:
//   [T5-01] CompilerContract.Version == 6
//   [T5-02] LegacyExecutionShim type does not exist
//   [T5-03] InstructionIR has no SafetyMask property
//   [T5-04] MicroOp has no CanBeStolen property
//   [T5-05] SlotMetadata.NotStealable singleton is not null
//   [T5-06] SlotMetadata.NotStealable.StealabilityPolicy == NotStealable
//   [T5-07] MicroOp has no SlotMeta property
//   [T5-08] MicroOp has no legacy stealability wrapper method
//   [T5-09] BranchMicroOp.AdmissionMetadata.IsStealable == false
//   [T5-10] CSRMicroOp.AdmissionMetadata.IsStealable == false
//   [T5-11] ScalarALUMicroOp.AdmissionMetadata.IsStealable == true
//   [T5-12] VectorALUMicroOp.AdmissionMetadata.IsStealable == true
//   [T5-13] LoadMicroOp (normal address) — AdmissionMetadata.IsStealable == true
//   [T5-14] LoadMicroOp (MMIO address) — AdmissionMetadata.IsStealable == false
//   [T5-15] StoreMicroOp (normal address) — AdmissionMetadata.IsStealable == true
//   [T5-16] StoreMicroOp (MMIO address) — AdmissionMetadata.IsStealable == false
//   [T5-17] SysEventMicroOp.AdmissionMetadata.IsStealable == false
//   [T5-18] PortIOMicroOp.AdmissionMetadata.IsStealable == false
//   [T5-19] GenericMicroOp.AdmissionMetadata.IsStealable == false (conservative)
//   [T5-20] NopMicroOp.AdmissionMetadata defaults to stealable
//   [T5-22] VliwDecoderV4 decoded IR has no SafetyMask property
//   [T5-21] SlotMetadata.Default.StealabilityPolicy == Stealable

using System;
using System.Reflection;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using HybridCPU_ISE.Tests.TestHelpers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.V5Phase5
{
    // ─────────────────────────────────────────────────────────────────────────
    // [T5-01..T5-04] Static contract assertions
    // ─────────────────────────────────────────────────────────────────────────

    public class Phase5ContractTests
    {
        [Fact]
        public void T5_01_CompilerContractVersion_Is6()
        {
            Assert.Equal(6, CompilerContract.Version);
        }

        [Fact]
        public void T5_02_LegacyExecutionShim_TypeDoesNotExist()
        {
            // LegacyExecutionShim was deleted in V5 Phase 5.
            // It should no longer be present in the production assembly.
            var asm = typeof(InternalOp).Assembly;
            var t = asm.GetType("YAKSys_Hybrid_CPU.Core.Pipeline.LegacyExecutionShim");
            Assert.Null(t);
        }

        [Fact]
        public void T5_03_InstructionIR_HasNo_SafetyMask_Property()
        {
            PropertyInfo? prop = typeof(InstructionIR).GetProperty("SafetyMask");
            Assert.Null(prop);
        }

        [Fact]
        public void T5_04_MicroOp_HasNo_CanBeStolen_Property()
        {
            PropertyInfo? prop = typeof(MicroOp).GetProperty(
                "CanBeStolen",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.Null(prop);
        }

        [Fact]
        public void T5_04b_IrInstruction_HasNo_CanBeStolen_Property()
        {
            PropertyInfo? prop = typeof(IrInstruction).GetProperty(
                "CanBeStolen",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.Null(prop);
        }

        [Fact]
        public void T5_04c_MicroOp_HasNo_SlotMeta_Property()
        {
            PropertyInfo? prop = typeof(MicroOp).GetProperty(
                "SlotMeta",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.Null(prop);
        }

        [Fact]
        public void T5_04d_MicroOp_HasNo_IsMetadataStealable_Method()
        {
            MethodInfo? method = typeof(MicroOp).GetMethod(
                "IsMetadataStealable",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.Null(method);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [T5-05..T5-06] SlotMetadata.NotStealable singleton
    // ─────────────────────────────────────────────────────────────────────────

    public class SlotMetadataNotStealableTests
    {
        [Fact]
        public void T5_05_NotStealable_Singleton_IsNotNull()
        {
            Assert.NotNull(SlotMetadata.NotStealable);
        }

        [Fact]
        public void T5_06_NotStealable_Singleton_PolicyIsNotStealable()
        {
            Assert.Equal(StealabilityPolicy.NotStealable, SlotMetadata.NotStealable.StealabilityPolicy);
        }

        [Fact]
        public void T5_21_Default_Singleton_PolicyIsStealable()
        {
            Assert.Equal(StealabilityPolicy.Stealable, SlotMetadata.Default.StealabilityPolicy);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [T5-07..T5-17] MicroOp stealability after CanBeStolen removal
    // ─────────────────────────────────────────────────────────────────────────

    public class MicroOpStealabilityAfterRemovalTests
    {
        private static BranchMicroOp CreateBranchMicroOpForAdmissionTest()
        {
            var op = new BranchMicroOp
            {
                OpCode = (uint)InstructionsEnum.BEQ,
                IsConditional = true,
                Reg1ID = 1,
                Reg2ID = 2,
                OwnerThreadId = 0,
                VirtualThreadId = 0
            };
            op.InitializeMetadata();
            return op;
        }

        private static CsrReadWriteMicroOp CreateCsrReadWriteMicroOpForAdmissionTest()
        {
            var op = new CsrReadWriteMicroOp
            {
                OpCode = (uint)InstructionsEnum.CSRRW,
                CSRAddress = 0x300,
                DestRegID = 1,
                SrcRegID = 2,
                WritesRegister = true,
                OwnerThreadId = 0,
                VirtualThreadId = 0
            };
            op.InitializeMetadata();
            return op;
        }

        private static VectorALUMicroOp CreateVectorAluMicroOpForAdmissionTest()
        {
            var op = new VectorALUMicroOp
            {
                OpCode = (uint)InstructionsEnum.VADD,
                OwnerThreadId = 0,
                VirtualThreadId = 0,
                Instruction = new VLIW_Instruction
                {
                    StreamLength = 4,
                    PredicateMask = 0xFF,
                    DataTypeValue = DataTypeEnum.INT32
                }
            };
            op.InitializeMetadata();
            return op;
        }

        private static SysEventMicroOp CreateFenceSystemEventMicroOpForAdmissionTest()
        {
            var op = new SysEventMicroOp
            {
                OpCode = (uint)InstructionsEnum.FENCE,
                EventKind = SystemEventKind.Fence,
                OrderGuarantee = SystemEventOrderGuarantee.DrainMemory,
                OwnerThreadId = 0,
                VirtualThreadId = 0
            };
            op.InitializeMetadata();
            return op;
        }

        [Fact]
        public void T5_09_BranchMicroOp_IsNotStealable()
        {
            var op = CreateBranchMicroOpForAdmissionTest();
            Assert.False(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_10_CsrReadWriteMicroOp_IsNotStealable()
        {
            var op = CreateCsrReadWriteMicroOpForAdmissionTest();
            Assert.False(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_11_ScalarALUMicroOp_IsStealable()
        {
            var op = new ScalarALUMicroOp();
            Assert.True(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_12_VectorALUMicroOp_IsStealable()
        {
            var op = CreateVectorAluMicroOpForAdmissionTest();
            Assert.True(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_13_LoadMicroOp_NormalAddress_IsStealable()
        {
            var op = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0, destReg: 1, address: 0x1000, domainTag: 0);
            Assert.True(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_14_LoadMicroOp_MmioAddress_IsNotStealable()
        {
            var op = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0, destReg: 1, address: 0xFFFF000000001000UL, domainTag: 0);
            Assert.False(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_15_StoreMicroOp_NormalAddress_IsStealable()
        {
            var op = MicroOpTestHelper.CreateStore(
                virtualThreadId: 0, srcReg: 2, address: 0x2000, domainTag: 0);
            Assert.True(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_16_StoreMicroOp_MmioAddress_IsNotStealable()
        {
            var op = MicroOpTestHelper.CreateStore(
                virtualThreadId: 0, srcReg: 2, address: 0xFFFF000000002000UL, domainTag: 0);
            Assert.False(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_17_SysEventMicroOp_IsNotStealable()
        {
            var op = CreateFenceSystemEventMicroOpForAdmissionTest();
            Assert.False(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_18_PortIOMicroOp_IsNotStealable()
        {
            var op = new PortIOMicroOp();
            Assert.False(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_19_GenericMicroOp_IsNotStealable()
        {
            var op = new GenericMicroOp();
            Assert.False(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_20_MicroOp_DefaultAdmissionMetadata_IsStealable()
        {
            var op = new NopMicroOp();
            Assert.True(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void T5_22_InstructionIR_BuiltWithoutSafetyMask()
        {
            // Verify that InstructionIR can be created without SafetyMask
            // (it no longer exists as a property)
            var ir = new InstructionIR
            {
                CanonicalOpcode    = InstructionsEnum.ADDI,
                Class              = InstructionClass.ScalarAlu,
                SerializationClass = SerializationClass.Free,
                Rd = 1, Rs1 = 2, Rs2 = 0, Imm = 10,
            };
            Assert.Equal(InstructionsEnum.ADDI, ir.CanonicalOpcode.ToInstructionsEnum());
        }
    }
}
