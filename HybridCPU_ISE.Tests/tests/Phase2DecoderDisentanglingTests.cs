// V5 Phase 2: Decoder Disentangling — YAKSys_Hybrid_CPU.Core.Legality.ILegalityChecker, ISlotMetadataBuilder, IInternalOpBuilder
//
// Covers:
//   [T2-01] LegalityChecker: Legal ALU instruction in Machine mode
//   [T2-02] LegalityChecker: Stall when RAW hazard on Rs1
//   [T2-03] LegalityChecker: Stall when RAW hazard on Rs2
//   [T2-04] LegalityChecker: Stall when execution unit unavailable
//   [T2-05] LegalityChecker: PrivilegeFault for M-mode instruction in S-mode
//   [T2-06] SlotMetadataBuilder: returns default SlotMetadata when annotation is null
//   [T2-07] SlotMetadataBuilder: BranchHint from compiler annotation
//   [T2-08] SlotMetadataBuilder: full annotation passthrough
//   [T2-09] InternalOpBuilder: maps scalar ALU opcodes to correct InternalOpKind
//   [T2-10] InternalOpBuilder: maps memory opcodes to Load/Store
//   [T2-11] InternalOpBuilder: maps control-flow opcodes to Jal/Jalr/Branch
//   [T2-12] InternalOpBuilder: maps CSR opcodes to CsrRead* kinds
//   [T2-13] InstructionIR: DataType defaults to DWord
//   [T2-14] InstructionIR: CsrAddress is null for non-CSR ops
//   [T2-15] SafetyMask removed from InstructionIR (V5 Phase 5)

using System;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.Metadata;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.V5Phase2
{
    // ─────────────────────────────────────────────────────────────────────────
    // Test stubs
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configurable <see cref="IHazardState"/> stub for Phase 2 tests.
    /// </summary>
    internal sealed class StubHazardState : IHazardState
    {
        public bool RawHazardResult { get; set; }

        public bool HasRawHazard(int sourceReg) => RawHazardResult;
        public bool HasWawHazard(int destReg1, int destReg2) => false;
        public bool HasWarHazard(int sourceReg, int destReg) => false;
    }

    /// <summary>
    /// Configurable <see cref="IResourceState"/> stub for Phase 2 tests.
    /// </summary>
    internal sealed class StubResourceState : IResourceState
    {
        public bool IsAvailableResult { get; set; } = true;

        public bool IsAvailable(InstructionClass instructionClass) => IsAvailableResult;
        public int AvailableCount(InstructionClass instructionClass) => IsAvailableResult ? 4 : 0;
    }

    /// <summary>Builds a minimal <see cref="InstructionIR"/> for a given opcode and class.</summary>
    internal static class IrFactory
    {
        public static InstructionIR Alu(InstructionsEnum opcode = InstructionsEnum.Addition,
                                        byte rd = 1, byte rs1 = 2, byte rs2 = 3) =>
            new InstructionIR
            {
                CanonicalOpcode    = opcode,
                Class              = InstructionClass.ScalarAlu,
                SerializationClass = SerializationClass.Free,
                Rd                 = rd,
                Rs1                = rs1,
                Rs2                = rs2,
                Imm                = 0,
            };

        public static InstructionIR Privileged(InstructionsEnum opcode = InstructionsEnum.MRET) =>
            new InstructionIR
            {
                CanonicalOpcode    = opcode,
                Class              = InstructionClass.System,
                SerializationClass = SerializationClass.FullSerial,
                Rd                 = 0,
                Rs1                = 0,
                Rs2                = 0,
                Imm                = 0,
            };

        public static InstructionIR Memory(InstructionsEnum opcode = InstructionsEnum.LD,
                                           byte rd = 1, byte rs1 = 2) =>
            new InstructionIR
            {
                CanonicalOpcode    = opcode,
                Class              = InstructionClass.Memory,
                SerializationClass = SerializationClass.Free,
                Rd                 = rd,
                Rs1                = rs1,
                Rs2                = 0,
                Imm                = 0,
            };

        public static InstructionIR ControlFlow(InstructionsEnum opcode = InstructionsEnum.JAL) =>
            new InstructionIR
            {
                CanonicalOpcode    = opcode,
                Class              = InstructionClass.ControlFlow,
                SerializationClass = SerializationClass.FullSerial,
                Rd                 = 1,
                Rs1                = 0,
                Rs2                = 0,
                Imm                = 4,
            };

        public static InstructionIR Csr(InstructionsEnum opcode = InstructionsEnum.CSRRW,
                                        byte rd = 1, byte rs1 = 2) =>
            new InstructionIR
            {
                CanonicalOpcode    = opcode,
                Class              = InstructionClass.Csr,
                SerializationClass = SerializationClass.FullSerial,
                Rd                 = rd,
                Rs1                = rs1,
                Rs2                = 0,
                Imm                = 0,
                CsrAddress         = YAKSys_Hybrid_CPU.Arch.CsrAddresses.Mscratch,
            };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // T2-01 to T2-05 — LegalityChecker
    // ─────────────────────────────────────────────────────────────────────────

    public class Phase2LegalityCheckerTests
    {
        private readonly YAKSys_Hybrid_CPU.Core.Legality.ILegalityChecker _checker = new YAKSys_Hybrid_CPU.Core.Legality.LegalityChecker();

        [Fact]
        public void T2_01_LegalAluInstruction_InMachineMode_ReturnsLegal()
        {
            // Arrange
            InstructionIR ir = IrFactory.Alu();
            var hazards   = new StubHazardState  { RawHazardResult    = false };
            var resources = new StubResourceState{ IsAvailableResult  = true  };

            // Act
            LegalityResult result = _checker.Check(ir, hazards, resources, PrivilegeLevel.Machine);

            // Assert
            Assert.Equal(LegalityResult.Legal, result);
        }

        [Fact]
        public void T2_02_LegalAluInstruction_InUserMode_ReturnsLegal()
        {
            // Arrange — ALU instructions are not privileged; user mode is fine
            InstructionIR ir = IrFactory.Alu();
            var hazards   = new StubHazardState  { RawHazardResult   = false };
            var resources = new StubResourceState{ IsAvailableResult = true  };

            // Act
            LegalityResult result = _checker.Check(ir, hazards, resources, PrivilegeLevel.User);

            // Assert
            Assert.Equal(LegalityResult.Legal, result);
        }

        [Fact]
        public void T2_03_WhenRawHazardOnRs1_ThenStall()
        {
            // Arrange
            InstructionIR ir = IrFactory.Alu(rd: 5, rs1: 3, rs2: 4);
            var hazards   = new StubHazardState  { RawHazardResult   = true };   // Rs1 has RAW
            var resources = new StubResourceState{ IsAvailableResult = true };

            // Act
            LegalityResult result = _checker.Check(ir, hazards, resources, PrivilegeLevel.Machine);

            // Assert
            Assert.Equal(LegalityResult.Stall, result);
        }

        [Fact]
        public void T2_04_WhenExecutionUnitUnavailable_ThenStall()
        {
            // Arrange
            InstructionIR ir = IrFactory.Alu();
            var hazards   = new StubHazardState  { RawHazardResult   = false };
            var resources = new StubResourceState{ IsAvailableResult = false };  // no ALU unit free

            // Act
            LegalityResult result = _checker.Check(ir, hazards, resources, PrivilegeLevel.Machine);

            // Assert
            Assert.Equal(LegalityResult.Stall, result);
        }

        [Fact]
        public void T2_05_WhenMRetInSupervisorMode_ThenPrivilegeFault()
        {
            // Arrange — MRET requires Machine mode, Supervisor mode is insufficient
            InstructionIR ir = IrFactory.Privileged(InstructionsEnum.MRET);
            var hazards   = new StubHazardState  { RawHazardResult   = false };
            var resources = new StubResourceState{ IsAvailableResult = true  };

            // Act
            LegalityResult result = _checker.Check(ir, hazards, resources, PrivilegeLevel.Supervisor);

            // Assert
            Assert.Equal(LegalityResult.PrivilegeFault, result);
        }

        [Fact]
        public void T2_05b_WhenMRetInMachineMode_ThenLegal()
        {
            // Arrange — MRET in Machine mode is legal
            InstructionIR ir = IrFactory.Privileged(InstructionsEnum.MRET);
            var hazards   = new StubHazardState  { RawHazardResult   = false };
            var resources = new StubResourceState{ IsAvailableResult = true  };

            // Act
            LegalityResult result = _checker.Check(ir, hazards, resources, PrivilegeLevel.Machine);

            // Assert
            Assert.Equal(LegalityResult.Legal, result);
        }

        [Fact]
        public void T2_05c_WhenVmxonInUserMode_ThenPrivilegeFault()
        {
            // Arrange — VMXON requires Machine mode
            InstructionIR ir = IrFactory.Privileged(InstructionsEnum.VMXON);
            var hazards   = new StubHazardState  { RawHazardResult   = false };
            var resources = new StubResourceState{ IsAvailableResult = true  };

            // Act
            LegalityResult result = _checker.Check(ir, hazards, resources, PrivilegeLevel.User);

            // Assert
            Assert.Equal(LegalityResult.PrivilegeFault, result);
        }

        [Fact]
        public void T2_PrivilegeCheckPreemptsHazardCheck()
        {
            // Arrange — MRET in User mode AND a RAW hazard: privilege check fires first
            InstructionIR ir = IrFactory.Privileged(InstructionsEnum.MRET);
            var hazards   = new StubHazardState  { RawHazardResult   = true };  // RAW hazard also present
            var resources = new StubResourceState{ IsAvailableResult = true };

            // Act
            LegalityResult result = _checker.Check(ir, hazards, resources, PrivilegeLevel.User);

            // Assert — PrivilegeFault takes precedence over Stall
            Assert.Equal(LegalityResult.PrivilegeFault, result);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // T2-06 to T2-08 — SlotMetadataBuilder
    // ─────────────────────────────────────────────────────────────────────────

    public class Phase2SlotMetadataBuilderTests
    {
        private readonly ISlotMetadataBuilder _builder = new SlotMetadataBuilder();

        [Fact]
        public void T2_06_WhenAnnotationIsNull_ThenDefaultSlotMetadata()
        {
            // Arrange
            InstructionIR ir = IrFactory.Alu();

            // Act
            SlotMetadata metadata = _builder.Build(ir, annotation: null);

            // Assert — all fields take SlotMetadata defaults
            Assert.Equal(BranchHint.None,             metadata.BranchHint);
            Assert.Equal(StealabilityPolicy.Stealable, metadata.StealabilityPolicy);
            Assert.Equal(0xFF,                         metadata.DonorVtHint);
            Assert.Equal(LocalityHint.None,            metadata.LocalityHint);
            Assert.Equal(0xFF,                         metadata.PreferredVt);
            Assert.Equal(ThermalHint.None,             metadata.ThermalHint);
        }

        [Fact]
        public void T2_07_WhenAnnotationHasBranchHintTaken_ThenMetadataHasBranchHintTaken()
        {
            // Arrange
            InstructionIR ir = IrFactory.ControlFlow();
            var annotation = new CompilerAnnotation
            {
                BranchHint = BranchHint.Likely,
            };

            // Act
            SlotMetadata metadata = _builder.Build(ir, annotation);

            // Assert
            Assert.Equal(BranchHint.Likely, metadata.BranchHint);
        }

        [Fact]
        public void T2_08_WhenAnnotationIsFullyPopulated_ThenAllFieldsPassThrough()
        {
            // Arrange
            InstructionIR ir = IrFactory.Alu();
            var annotation = new CompilerAnnotation
            {
                BranchHint         = BranchHint.Unlikely,
                StealabilityPolicy = StealabilityPolicy.NotStealable,
                DonorVtHint        = 2,
                LocalityHint       = LocalityHint.Hot,
                PreferredVt        = 1,
                ThermalHint        = ThermalHint.Boost,
            };

            // Act
            SlotMetadata metadata = _builder.Build(ir, annotation);

            // Assert — all fields forwarded verbatim
            Assert.Equal(BranchHint.Unlikely,  metadata.BranchHint);
            Assert.Equal(StealabilityPolicy.NotStealable, metadata.StealabilityPolicy);
            Assert.Equal((byte)2,                     metadata.DonorVtHint);
            Assert.Equal(LocalityHint.Hot,             metadata.LocalityHint);
            Assert.Equal((byte)1,                     metadata.PreferredVt);
            Assert.Equal(ThermalHint.Boost,             metadata.ThermalHint);
        }

        [Fact]
        public void T2_NullAnnotationReturnsSameAsFreshSlotMetadata()
        {
            // Arrange
            InstructionIR ir = IrFactory.Alu();

            // Act
            SlotMetadata viaBuilder = _builder.Build(ir, annotation: null);
            SlotMetadata viaDefault = new SlotMetadata();

            // Assert — structurally identical
            Assert.Equal(viaDefault, viaBuilder);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // T2-09 to T2-12 — InternalOpBuilder
    // ─────────────────────────────────────────────────────────────────────────

    public class Phase2InternalOpBuilderTests
    {
        private readonly IInternalOpBuilder _builder = new InternalOpBuilder();

        [Theory]
        [InlineData(InstructionsEnum.Addition,    InternalOpKind.Add)]
        [InlineData(InstructionsEnum.Subtraction, InternalOpKind.Sub)]
        [InlineData(InstructionsEnum.Multiplication, InternalOpKind.Mul)]
        [InlineData(InstructionsEnum.XOR,         InternalOpKind.Xor)]
        [InlineData(InstructionsEnum.OR,          InternalOpKind.Or)]
        [InlineData(InstructionsEnum.AND,         InternalOpKind.And)]
        [InlineData(InstructionsEnum.ADDI,        InternalOpKind.AddI)]
        [InlineData(InstructionsEnum.ANDI,        InternalOpKind.AndI)]
        [InlineData(InstructionsEnum.LUI,         InternalOpKind.Lui)]
        [InlineData(InstructionsEnum.AUIPC,       InternalOpKind.Auipc)]
        [InlineData(InstructionsEnum.SLT,         InternalOpKind.Slt)]
        [InlineData(InstructionsEnum.SLTU,        InternalOpKind.Sltu)]
        [InlineData(InstructionsEnum.MULH,        InternalOpKind.MulH)]
        [InlineData(InstructionsEnum.MULHU,       InternalOpKind.MulHu)]
        [InlineData(InstructionsEnum.MULHSU,      InternalOpKind.MulHsu)]
        [InlineData(InstructionsEnum.DIVU,        InternalOpKind.Divu)]
        [InlineData(InstructionsEnum.REMU,        InternalOpKind.Remu)]
        public void T2_09_ScalarAluOpcodes_MapToCorrectInternalOpKind(
            InstructionsEnum opcode, InternalOpKind expectedKind)
        {
            // Arrange
            InstructionIR ir = IrFactory.Alu(opcode);

            // Act
            InternalOp op = _builder.Build(ir);

            // Assert
            Assert.Equal(expectedKind, op.Kind);
        }

        [Theory]
        [InlineData(InstructionsEnum.LB,  InternalOpKind.Load)]
        [InlineData(InstructionsEnum.LBU, InternalOpKind.Load)]
        [InlineData(InstructionsEnum.LH,  InternalOpKind.Load)]
        [InlineData(InstructionsEnum.LHU, InternalOpKind.Load)]
        [InlineData(InstructionsEnum.LW,  InternalOpKind.Load)]
        [InlineData(InstructionsEnum.LWU, InternalOpKind.Load)]
        [InlineData(InstructionsEnum.LD,  InternalOpKind.Load)]
        [InlineData(InstructionsEnum.SB,  InternalOpKind.Store)]
        [InlineData(InstructionsEnum.SH,  InternalOpKind.Store)]
        [InlineData(InstructionsEnum.SW,  InternalOpKind.Store)]
        [InlineData(InstructionsEnum.SD,  InternalOpKind.Store)]
        public void T2_10_MemoryOpcodes_MapToLoadOrStore(
            InstructionsEnum opcode, InternalOpKind expectedKind)
        {
            // Arrange
            InstructionIR ir = IrFactory.Memory(opcode);

            // Act
            InternalOp op = _builder.Build(ir);

            // Assert
            Assert.Equal(expectedKind, op.Kind);
        }

        [Theory]
        [InlineData(InstructionsEnum.JAL,  InternalOpKind.Jal)]
        [InlineData(InstructionsEnum.JALR, InternalOpKind.Jalr)]
        [InlineData(InstructionsEnum.BEQ,  InternalOpKind.Branch)]
        [InlineData(InstructionsEnum.BNE,  InternalOpKind.Branch)]
        [InlineData(InstructionsEnum.BLT,  InternalOpKind.Branch)]
        [InlineData(InstructionsEnum.BGE,  InternalOpKind.Branch)]
        [InlineData(InstructionsEnum.BLTU, InternalOpKind.Branch)]
        [InlineData(InstructionsEnum.BGEU, InternalOpKind.Branch)]
        [InlineData(InstructionsEnum.JumpIfEqual, InternalOpKind.Branch)]
        public void T2_11_ControlFlowOpcodes_MapToJalJalrBranch(
            InstructionsEnum opcode, InternalOpKind expectedKind)
        {
            // Arrange
            InstructionIR ir = IrFactory.ControlFlow(opcode);

            // Act
            InternalOp op = _builder.Build(ir);

            // Assert
            Assert.Equal(expectedKind, op.Kind);
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRW,  InternalOpKind.CsrReadWrite)]
        [InlineData(InstructionsEnum.CSRRS,  InternalOpKind.CsrReadSet)]
        [InlineData(InstructionsEnum.CSRRC,  InternalOpKind.CsrReadClear)]
        [InlineData(InstructionsEnum.CSRRWI, InternalOpKind.CsrReadWrite)]
        [InlineData(InstructionsEnum.CSRRSI, InternalOpKind.CsrReadSet)]
        [InlineData(InstructionsEnum.CSRRCI, InternalOpKind.CsrReadClear)]
        [InlineData(InstructionsEnum.CSR_CLEAR, InternalOpKind.CsrClear)]
        public void T2_12_CsrOpcodes_MapToCsrReadKinds(
            InstructionsEnum opcode, InternalOpKind expectedKind)
        {
            // Arrange
            InstructionIR ir = IrFactory.Csr(opcode);

            // Act
            InternalOp op = _builder.Build(ir);

            // Assert
            Assert.Equal(expectedKind, op.Kind);
        }

        [Theory]
        [InlineData(InstructionsEnum.FENCE, InternalOpKind.Fence)]
        [InlineData(InstructionsEnum.FENCE_I, InternalOpKind.FenceI)]
        [InlineData(InstructionsEnum.ECALL, InternalOpKind.Ecall)]
        [InlineData(InstructionsEnum.EBREAK, InternalOpKind.Ebreak)]
        [InlineData(InstructionsEnum.MRET, InternalOpKind.Mret)]
        [InlineData(InstructionsEnum.SRET, InternalOpKind.Sret)]
        [InlineData(InstructionsEnum.WFI, InternalOpKind.Wfi)]
        public void T2_12b_SystemOpcodes_MapToExplicitSystemKinds(
            InstructionsEnum opcode, InternalOpKind expectedKind)
        {
            InstructionIR ir = IrFactory.ControlFlow(opcode);

            InternalOp op = _builder.Build(ir);

            Assert.Equal(expectedKind, op.Kind);
        }

        [Fact]
        public void T2_RegisterFields_PassedThrough_ToInternalOp()
        {
            // Arrange
            InstructionIR ir = IrFactory.Alu(InstructionsEnum.Addition, rd: 5, rs1: 3, rs2: 7);

            // Act
            InternalOp op = _builder.Build(ir);

            // Assert
            Assert.Equal(5, op.Rd);
            Assert.Equal(3, op.Rs1);
            Assert.Equal(7, op.Rs2);
        }

        [Fact]
        public void T2_Immediate_PassedThrough_ToInternalOp()
        {
            // Arrange — ADDI with immediate 42
            var ir = new InstructionIR
            {
                CanonicalOpcode    = InstructionsEnum.ADDI,
                Class              = InstructionClass.ScalarAlu,
                SerializationClass = SerializationClass.Free,
                Rd                 = 1,
                Rs1                = 2,
                Rs2                = 0,
                Imm                = 42L,
            };

            // Act
            InternalOp op = _builder.Build(ir);

            // Assert
            Assert.Equal(42L, op.Immediate);
        }

        [Fact]
        public void T2_InternalOpBuilder_ConsumesCanonicalOpcodeIdentity()
        {
            var ir = new InstructionIR
            {
                CanonicalOpcode    = InstructionsEnum.CSRRS,
                Class              = InstructionClass.Csr,
                SerializationClass = SerializationClass.CsrOrdered,
                Rd                 = 1,
                Rs1                = 2,
                Rs2                = 0,
                Imm                = 0,
                CsrAddress         = CsrAddresses.Mscratch,
            };

            InternalOp op = _builder.Build(ir);

            Assert.Equal(InternalOpKind.CsrReadSet, op.Kind);
        }

        [Fact]
        public void T2_MapToKind_IsStateless_SamOpcodeAlwaysSameKind()
        {
            // Act + Assert — calling twice gives identical result
            InternalOpKind first  = InternalOpBuilder.MapToKind((ushort)InstructionsEnum.Addition);
            InternalOpKind second = InternalOpBuilder.MapToKind((ushort)InstructionsEnum.Addition);

            Assert.Equal(InternalOpKind.Add, first);
            Assert.Equal(first, second);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // T2-13 to T2-15 — InstructionIR Phase 2 additions
    // ─────────────────────────────────────────────────────────────────────────

    public class Phase2InstructionIrExtensionTests
    {
        [Fact]
        public void T2_13_DataType_DefaultsToDWord()
        {
            // Arrange + Act
            InstructionIR ir = IrFactory.Alu();

            // Assert
            Assert.Equal(InternalOpDataType.DWord, ir.DataType);
        }

        [Fact]
        public void T2_14_CsrAddress_IsNullForNonCsrOp()
        {
            // Arrange + Act
            InstructionIR ir = IrFactory.Alu();

            // Assert
            Assert.Null(ir.CsrAddress);
        }

        [Fact]
        public void T2_14b_CsrAddress_CanBeSetForCsrOp()
        {
            // Arrange
            InstructionIR ir = IrFactory.Csr(InstructionsEnum.CSRRW);

            // Assert
            Assert.NotNull(ir.CsrAddress);
            Assert.Equal(YAKSys_Hybrid_CPU.Arch.CsrAddresses.Mscratch, ir.CsrAddress!.Value);
        }

        [Fact]
        public void T2_15_SafetyMask_RemovedFromInstructionIR()
        {
            // V5 Phase 5: InstructionIR.SafetyMask was marked [Obsolete] in Phase 2.
            // It has now been fully removed. Verify the field no longer exists.
            PropertyInfo? prop = typeof(InstructionIR).GetProperty("SafetyMask");
            Assert.Null(prop);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Architecture boundary contracts
    // ─────────────────────────────────────────────────────────────────────────

    public class Phase2ArchitectureBoundaryTests
    {
        [Fact]
        public void ILegalityChecker_IsDefinedInLegalityNamespace()
        {
            Assert.Equal("YAKSys_Hybrid_CPU.Core.Legality", typeof(YAKSys_Hybrid_CPU.Core.Legality.ILegalityChecker).Namespace);
        }

        [Fact]
        public void ISlotMetadataBuilder_IsDefinedInMetadataNamespace()
        {
            Assert.Equal("YAKSys_Hybrid_CPU.Core.Pipeline.Metadata", typeof(ISlotMetadataBuilder).Namespace);
        }

        [Fact]
        public void IInternalOpBuilder_IsDefinedInPipelineNamespace()
        {
            Assert.Equal("YAKSys_Hybrid_CPU.Core.Pipeline", typeof(IInternalOpBuilder).Namespace);
        }

        [Fact]
        public void CompilerAnnotation_IsSealedRecord()
        {
            Type t = typeof(CompilerAnnotation);
            Assert.True(t.IsSealed, "CompilerAnnotation must be sealed");
            Assert.True(t.IsClass,  "CompilerAnnotation must be a record (class)");
        }

        [Fact]
        public void LegalityResult_HasExpectedValues()
        {
            Assert.Equal(0, (int)LegalityResult.Legal);
            Assert.Equal(1, (int)LegalityResult.Stall);
            Assert.Equal(2, (int)LegalityResult.Flush);
            Assert.Equal(3, (int)LegalityResult.PrivilegeFault);
        }

        [Fact]
        public void InternalOpBuilder_IsConcreteAndImplementsInterface()
        {
            Type t = typeof(InternalOpBuilder);
            Assert.True(typeof(IInternalOpBuilder).IsAssignableFrom(t));
            Assert.False(t.IsAbstract);
        }

        [Fact]
        public void LegalityChecker_IsConcreteAndImplementsInterface()
        {
            Type t = typeof(LegalityChecker);
            Assert.True(typeof(YAKSys_Hybrid_CPU.Core.Legality.ILegalityChecker).IsAssignableFrom(t));
            Assert.False(t.IsAbstract);
        }

        [Fact]
        public void SlotMetadataBuilder_IsConcreteAndImplementsInterface()
        {
            Type t = typeof(SlotMetadataBuilder);
            Assert.True(typeof(ISlotMetadataBuilder).IsAssignableFrom(t));
            Assert.False(t.IsAbstract);
        }
    }
}
