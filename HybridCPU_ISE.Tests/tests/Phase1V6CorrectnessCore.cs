// V6 Phase 1–7: Correctness Core — OwnerContextId separation, BranchMicroOp V6-final class,
//              IsControlFlow/IsMathOrVector deprecation (removed in Phase 3)
//
// Covers:
//   [T6-01] MicroOp.OwnerContextId property exists and is independent of VirtualThreadId
//   [T6-02] MicroOp.OwnerContextId defaults to 0
//   [T6-03] MicroOp.OwnerContextId and VirtualThreadId can differ
//   [T6-04] BranchMicroOp (V6-final) does NOT carry [Obsolete] attribute
//   [T6-05] VLIW_Instruction.IsControlFlow carries [Obsolete] attribute
//   [T6-06] VLIW_Instruction.IsMathOrVector carries [Obsolete] attribute
//   [T6-07] InstructionRegistry unknown opcode throws, never returns NOP
//   [T6-08] MicroOp.OwnerContextId survives pipeline copy independently of VirtualThreadId
//   [T6-09] ScalarALUMicroOp.OwnerContextId is settable and readable
//   [T6-10] BranchMicroOp.OwnerContextId is settable and readable

using HybridCPU_ISE.Arch;
using System;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

#pragma warning disable CS0618 // Intentionally testing [Obsolete] members
namespace HybridCPU_ISE.Tests.V6Phase1
{
    // ─────────────────────────────────────────────────────────────────────────
    // [T6-01..T6-03] OwnerContextId identity model separation
    // ─────────────────────────────────────────────────────────────────────────

    public class OwnerContextIdTests
    {
        [Fact]
        public void T6_01_MicroOp_OwnerContextId_PropertyExists()
        {
            var prop = typeof(MicroOp).GetProperty(
                "OwnerContextId",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(prop);
        }

        [Fact]
        public void T6_02_MicroOp_OwnerContextId_DefaultsToZero()
        {
            var op = new ScalarALUMicroOp();
            Assert.Equal(0, op.OwnerContextId);
        }

        [Fact]
        public void T6_03_OwnerContextId_And_VirtualThreadId_AreIndependent()
        {
            var op = new ScalarALUMicroOp
            {
                OwnerContextId = 2,
                VirtualThreadId = 1
            };
            Assert.Equal(2, op.OwnerContextId);
            Assert.Equal(1, op.VirtualThreadId);
        }

        [Fact]
        public void T6_08_OwnerContextId_DoesNotAlias_VirtualThreadId()
        {
            var op = new ScalarALUMicroOp
            {
                OwnerContextId = 3,
                VirtualThreadId = 0
            };
            // Changing VirtualThreadId must not affect OwnerContextId
            op.VirtualThreadId = 2;
            Assert.Equal(3, op.OwnerContextId);
            // Changing OwnerContextId must not affect VirtualThreadId
            op.OwnerContextId = 7;
            Assert.Equal(2, op.VirtualThreadId);
        }

        [Fact]
        public void T6_09_ScalarALUMicroOp_OwnerContextId_IsSettableAndReadable()
        {
            var op = new ScalarALUMicroOp { OwnerContextId = 42 };
            Assert.Equal(42, op.OwnerContextId);
        }

        [Fact]
        public void T6_10_BranchMicroOp_OwnerContextId_IsSettableAndReadable()
        {
            var op = new BranchMicroOp { OwnerContextId = 99 };
            Assert.Equal(99, op.OwnerContextId);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [T6-04..T6-06] Phase 1 obsolete annotations / Phase 3 removal verification
    // ─────────────────────────────────────────────────────────────────────────

    public class V6ObsoleteAnnotationTests
    {
        [Fact]
        public void T6_04_BranchMicroOp_HasNoObsoleteAttribute()
        {
            // Phase 7 (Final Freeze): BranchMicroOp is the V6-final name; [Obsolete] is gone.
            var attr = typeof(BranchMicroOp).GetCustomAttribute<ObsoleteAttribute>();
            Assert.Null(attr);
        }

        // T6-05 / T6-06: In Phase 3 (ISA_V6_MIGRATE B7/B8/B10) VLIW_Instruction.IsControlFlow
        // and IsMathOrVector were removed. The architectural table-driven replacements are
        // OpcodeRegistry.IsControlFlowOp() and OpcodeRegistry.IsMathOrVectorOp().

        [Fact]
        public void T6_05_VLIW_Instruction_IsControlFlow_RemovedInPhase3()
        {
            var prop = typeof(VLIW_Instruction)
                .GetProperty("IsControlFlow");
            Assert.Null(prop); // Fully removed in Phase 3 — table-driven via OpcodeRegistry.IsControlFlowOp()
        }

        [Fact]
        public void T6_06_VLIW_Instruction_IsMathOrVector_RemovedInPhase3()
        {
            var prop = typeof(VLIW_Instruction)
                .GetProperty("IsMathOrVector");
            Assert.Null(prop); // Fully removed in Phase 3 — table-driven via OpcodeRegistry.IsMathOrVectorOp()
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [T6-07] Unknown opcode throws, never returns NOP
    // ─────────────────────────────────────────────────────────────────────────

    public class InstructionRegistryFallbackTests
    {
        [Fact]
        public void T6_07_UnknownOpcode_Throws_NotReturnsNop()
        {
            // 0xDEADBEEF is guaranteed to be unregistered
            const uint unknownOpcode = 0xDEADBEEF;
            var ctx = new YAKSys_Hybrid_CPU.Core.DecoderContext
            {
                OpCode      = unknownOpcode,
                Reg1ID      = 0,
                Reg2ID      = 0
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => YAKSys_Hybrid_CPU.Core.InstructionRegistry.CreateMicroOp(unknownOpcode, ctx));

            // The exception message must identify the opcode — confirms throw path, not NOP return
            Assert.Contains("0xDEADBEEF", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#pragma warning restore CS0618

