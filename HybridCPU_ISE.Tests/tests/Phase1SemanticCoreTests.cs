// V5 Phase 1: Semantic Core вЂ” InternalOp, PipelineEvent contracts
//
// Covers:
//   [T1-01] InternalOp: sealed record, required Kind, Rs1/Rs2/Rd default -1, Immediate default 0
//   [T1-02] InternalOp: DataType defaults to DWord
//   [T1-03] InternalOp: CsrTarget is null for non-CSR ops
//   [T1-04] InternalOp: Flags defaults to None
//   [T1-05] PipelineEvent hierarchy: all concrete event types are records
//   [T1-06] PipelineEvent: EcallEvent captures EcallCode
//   [T1-07] PipelineEvent: FenceEvent distinguishes FENCE vs FENCE.I
//   [T1-08] IPipelineEventQueue: NullPipelineEventQueue is singleton, Enqueue is no-op
//   [T1-09] ExecutionResult: remains value-only; system events live on the queue/lane seams
//   [T1-10] ExecutionDispatcherV4: system instructions reject eager execute in favor of direct compat retire transactions
//   [T1-11] LegacyExecutionShim: REMOVED in V5 Phase 5 вЂ” InvokeAlu inlined, class deleted
//   [T1-12] ExecutionDispatcherV4: direct compat system transactions keep typed events explicit without eager queue-side success

using System;
using System.Collections.Generic;
using Xunit;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.V5Phase1
{
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Test helpers
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>Minimal ICanonicalCpuState stub for Phase 1 tests.</summary>
    internal sealed class P1CpuState : ICanonicalCpuState
    {
        private readonly ulong[] _regs = new ulong[32];
        private PipelineState _pipelineState = PipelineState.Task;
        private ulong _pc;

        public void SetReg(int idx, ulong value) => _regs[idx] = value;

        public ulong GetVL() => 0;
        public void SetVL(ulong vl) { }
        public ulong GetVLMAX() => 0;
        public byte GetSEW() => 0;
        public void SetSEW(byte sew) { }
        public byte GetLMUL() => 0;
        public void SetLMUL(byte lmul) { }
        public bool GetTailAgnostic() => false;
        public void SetTailAgnostic(bool agnostic) { }
        public bool GetMaskAgnostic() => false;
        public void SetMaskAgnostic(bool agnostic) { }
        public uint GetExceptionMask() => 0;
        public void SetExceptionMask(uint mask) { }
        public uint GetExceptionPriority() => 0;
        public void SetExceptionPriority(uint priority) { }
        public byte GetRoundingMode() => 0;
        public void SetRoundingMode(byte mode) { }
        public ulong GetOverflowCount() => 0;
        public ulong GetUnderflowCount() => 0;
        public ulong GetDivByZeroCount() => 0;
        public ulong GetInvalidOpCount() => 0;
        public ulong GetInexactCount() => 0;
        public void ClearExceptionCounters() { }
        public bool GetVectorDirty() => false;
        public void SetVectorDirty(bool dirty) { }
        public bool GetVectorEnabled() => false;
        public void SetVectorEnabled(bool enabled) { }
        public long ReadRegister(byte vtId, int regId) => unchecked((long)_regs[regId]);

        public void WriteRegister(byte vtId, int regId, ulong value)
        {
            if (regId == 0)
                return;

            _regs[regId] = value;
        }
        public ushort GetPredicateMask(ushort maskID) => 0;
        public void SetPredicateMask(ushort maskID, ushort mask) { }
        public ulong ReadPc(byte vtId) => _pc;
        public void WritePc(byte vtId, ulong pc) => _pc = pc;
        public ushort GetCoreID() => 0;
        public ulong GetCycleCount() => 0;
        public ulong GetInstructionsRetired() => 0;
        public double GetIPC() => 0;
        public PipelineState GetCurrentPipelineState() => _pipelineState;
        public void SetCurrentPipelineState(PipelineState state) => _pipelineState = state;
        public void TransitionPipelineState(YAKSys_Hybrid_CPU.Core.PipelineTransitionTrigger trigger)
            => _pipelineState = YAKSys_Hybrid_CPU.Core.PipelineFsmGuard.Transition(_pipelineState, trigger);
    }

    /// <summary>Recording IPipelineEventQueue for test assertions.</summary>
    internal sealed class RecordingEventQueue : IPipelineEventQueue
    {
        public List<PipelineEvent> Events { get; } = new();
        public void Enqueue(PipelineEvent evt) => Events.Add(evt);
    }

    /// <summary>Minimal InstructionIR builder for Phase 1 tests.</summary>
    internal static class P1Ir
    {
        public static YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps.InstructionIR Make(InstructionsEnum opcode) =>
            new()
            {
                CanonicalOpcode = opcode,
                Class  = YAKSys_Hybrid_CPU.Arch.InstructionClassifier.GetClass(opcode),
                SerializationClass = YAKSys_Hybrid_CPU.Arch.InstructionClassifier.GetSerializationClass(opcode),
                Rd = 0, Rs1 = 0, Rs2 = 0, Imm = 0,
            };
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // [T1-01] InternalOp contract вЂ” defaults
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class InternalOpDefaultsTests
    {
        [Fact]
        public void InternalOp_Rs1_DefaultsToNegativeOne()
        {
            var op = new InternalOp { Kind = InternalOpKind.Add };
            Assert.Equal(-1, op.Rs1);
        }

        [Fact]
        public void InternalOp_Rs2_DefaultsToNegativeOne()
        {
            var op = new InternalOp { Kind = InternalOpKind.Add };
            Assert.Equal(-1, op.Rs2);
        }

        [Fact]
        public void InternalOp_Rd_DefaultsToNegativeOne()
        {
            var op = new InternalOp { Kind = InternalOpKind.Add };
            Assert.Equal(-1, op.Rd);
        }

        [Fact]
        public void InternalOp_Immediate_DefaultsToZero()
        {
            var op = new InternalOp { Kind = InternalOpKind.Add };
            Assert.Equal(0L, op.Immediate);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // [T1-02] InternalOp вЂ” DataType defaults
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class InternalOpDataTypeTests
    {
        [Fact]
        public void InternalOp_DataType_DefaultsDWord()
        {
            var op = new InternalOp { Kind = InternalOpKind.Load };
            Assert.Equal(InternalOpDataType.DWord, op.DataType);
        }

        [Theory]
        [InlineData(InternalOpDataType.Byte)]
        [InlineData(InternalOpDataType.Half)]
        [InlineData(InternalOpDataType.Word)]
        [InlineData(InternalOpDataType.DWord)]
        public void InternalOp_DataType_RoundTrips(InternalOpDataType dt)
        {
            var op = new InternalOp { Kind = InternalOpKind.Load, DataType = dt };
            Assert.Equal(dt, op.DataType);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // [T1-03] InternalOp вЂ” CsrTarget
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class InternalOpCsrTargetTests
    {
        [Fact]
        public void InternalOp_CsrTarget_DefaultsNull()
        {
            var op = new InternalOp { Kind = InternalOpKind.Add };
            Assert.Null(op.CsrTarget);
        }

        [Fact]
        public void InternalOp_CsrTarget_RoundTrips()
        {
            ushort csrAddr = 0x300; // mstatus
            var op = new InternalOp { Kind = InternalOpKind.CsrReadWrite, CsrTarget = csrAddr };
            Assert.Equal(csrAddr, op.CsrTarget);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // [T1-04] InternalOp вЂ” Flags defaults
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class InternalOpFlagsTests
    {
        [Fact]
        public void InternalOp_Flags_DefaultsNone()
        {
            var op = new InternalOp { Kind = InternalOpKind.Add };
            Assert.Equal(InternalOpFlags.None, op.Flags);
        }

        [Fact]
        public void InternalOp_Flags_SignedRoundTrips()
        {
            var op = new InternalOp { Kind = InternalOpKind.Div, Flags = InternalOpFlags.Signed };
            Assert.True((op.Flags & InternalOpFlags.Signed) != 0);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // [T1-05] PipelineEvent hierarchy вЂ” type taxonomy
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class PipelineEventHierarchyTests
    {
        [Fact]
        public void EcallEvent_IsA_PipelineEvent()
            => Assert.IsAssignableFrom<PipelineEvent>(new EcallEvent { VtId = 0, BundleSerial = 0, EcallCode = 0 });

        [Fact]
        public void EbreakEvent_IsA_PipelineEvent()
            => Assert.IsAssignableFrom<PipelineEvent>(new EbreakEvent { VtId = 0, BundleSerial = 0 });

        [Fact]
        public void MretEvent_IsA_PipelineEvent()
            => Assert.IsAssignableFrom<PipelineEvent>(new MretEvent { VtId = 0, BundleSerial = 0 });

        [Fact]
        public void SretEvent_IsA_PipelineEvent()
            => Assert.IsAssignableFrom<PipelineEvent>(new SretEvent { VtId = 0, BundleSerial = 0 });

        [Fact]
        public void FenceEvent_IsA_PipelineEvent()
            => Assert.IsAssignableFrom<PipelineEvent>(new FenceEvent { VtId = 0, BundleSerial = 0, IsInstructionFence = false });

        [Fact]
        public void WfiEvent_IsA_PipelineEvent()
            => Assert.IsAssignableFrom<PipelineEvent>(new WfiEvent { VtId = 0, BundleSerial = 0 });

        [Fact]
        public void YieldEvent_IsA_PipelineEvent()
            => Assert.IsAssignableFrom<PipelineEvent>(new YieldEvent { VtId = 0, BundleSerial = 0 });
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // [T1-06] EcallEvent вЂ” captures EcallCode
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class EcallEventTests
    {
        [Fact]
        public void EcallEvent_Captures_EcallCode()
        {
            var evt = new EcallEvent { VtId = 1, BundleSerial = 42, EcallCode = 93 };
            Assert.Equal(93L, evt.EcallCode);
            Assert.Equal((byte)1, evt.VtId);
            Assert.Equal(42UL, evt.BundleSerial);
        }

        [Fact]
        public void EcallEvent_RecordEquality_MatchesByValue()
        {
            var a = new EcallEvent { VtId = 0, BundleSerial = 1, EcallCode = 42 };
            var b = new EcallEvent { VtId = 0, BundleSerial = 1, EcallCode = 42 };
            Assert.Equal(a, b);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // [T1-07] FenceEvent вЂ” distinguishes FENCE vs FENCE.I
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class FenceEventTests
    {
        [Fact]
        public void FenceEvent_DataFence_IsInstructionFence_IsFalse()
        {
            var evt = new FenceEvent { VtId = 0, BundleSerial = 0, IsInstructionFence = false };
            Assert.False(evt.IsInstructionFence);
        }

        [Fact]
        public void FenceEvent_InstructionFence_IsInstructionFence_IsTrue()
        {
            var evt = new FenceEvent { VtId = 0, BundleSerial = 0, IsInstructionFence = true };
            Assert.True(evt.IsInstructionFence);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // [T1-08] IPipelineEventQueue вЂ” NullPipelineEventQueue
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class NullPipelineEventQueueTests
    {
        [Fact]
        public void NullPipelineEventQueue_IsSingleton()
        {
            Assert.Same(NullPipelineEventQueue.Instance, NullPipelineEventQueue.Instance);
        }

        [Fact]
        public void NullPipelineEventQueue_Enqueue_DoesNotThrow()
        {
            var queue = NullPipelineEventQueue.Instance;
            var evt = new WfiEvent { VtId = 0, BundleSerial = 0 };
            queue.Enqueue(evt); // must not throw
        }

        [Fact]
        public void NullPipelineEventQueue_Implements_IPipelineEventQueue()
            => Assert.IsAssignableFrom<IPipelineEventQueue>(NullPipelineEventQueue.Instance);
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // [T1-09] ExecutionResult stays value-only; event authority lives on explicit queue/lane seams
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class ExecutionResultValueTests
    {
        [Fact]
        public void Ok_PreservesValue()
        {
            var result = ExecutionResult.Ok(42);
            Assert.Equal(42UL, result.Value);
        }

        [Fact]
        public void Ok_TrapRaised_IsFalse()
        {
            var result = ExecutionResult.Ok();
            Assert.False(result.TrapRaised);
        }

        [Fact]
        public void Trap_SetsTrapRaised()
        {
            var result = ExecutionResult.Trap();
            Assert.True(result.TrapRaised);
        }

        [Fact]
        public void Redirect_SetsNewPc()
        {
            var result = ExecutionResult.Redirect(0x200, rdValue: 0x40);
            Assert.True(result.PcRedirected);
            Assert.Equal(0x200UL, result.NewPc);
            Assert.Equal(0x40UL, result.Value);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // [T1-10] ExecutionDispatcherV4 вЂ” system instructions reject eager execute and resolve through direct compat retire
    // [T1-12] direct compat system transactions keep typed events explicit without eager queue-side success
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class DispatcherSystemInstructionTests
    {
        private static (RecordingEventQueue queue, ExecutionDispatcherV4 dispatcher) Build()
        {
            var queue = new RecordingEventQueue();
            var dispatcher = new ExecutionDispatcherV4(pipelineEventQueue: queue);
            return (queue, dispatcher);
        }

        private static RetireWindowCaptureSnapshot Resolve(
            InstructionsEnum opcode,
            P1CpuState? state = null,
            ulong bundleSerial = 1,
            byte vtId = 0)
        {
            var dispatcher = new ExecutionDispatcherV4();
            state ??= new P1CpuState();
            return RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                P1Ir.Make(opcode),
                state,
                bundleSerial,
                vtId);
        }

        [Theory]
        [InlineData(InstructionsEnum.ECALL)]
        [InlineData(InstructionsEnum.EBREAK)]
        [InlineData(InstructionsEnum.MRET)]
        [InlineData(InstructionsEnum.SRET)]
        [InlineData(InstructionsEnum.WFI)]
        [InlineData(InstructionsEnum.FENCE)]
        [InlineData(InstructionsEnum.FENCE_I)]
        public void System_Instructions_EagerExecuteIsRejectedInFavorOfDirectCompatRetire(InstructionsEnum opcode)
        {
            var (queue, dispatcher) = Build();
            var state = new P1CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P1Ir.Make(opcode), state, bundleSerial: 1, vtId: 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.True(Resolve(opcode, state).HasPipelineEvent);
        }

        [Theory]
        [InlineData(InstructionsEnum.ECALL)]
        [InlineData(InstructionsEnum.EBREAK)]
        [InlineData(InstructionsEnum.MRET)]
        [InlineData(InstructionsEnum.SRET)]
        [InlineData(InstructionsEnum.WFI)]
        [InlineData(InstructionsEnum.FENCE)]
        [InlineData(InstructionsEnum.FENCE_I)]
        public void System_Instructions_ResolveTypedEvents(InstructionsEnum opcode)
        {
            var state = new P1CpuState();
            var transaction = Resolve(opcode, state, bundleSerial: 1, vtId: 0);

            Assert.Equal(RetireWindowCaptureEffectKind.System, transaction.TypedEffectKind);
            Assert.True(transaction.HasPipelineEvent);
        }

        [Fact]
        public void Ecall_Event_CapturesA7Register()
        {
            var state = new P1CpuState();
            state.SetReg(17, 93); // a7 = 93 (exit syscall)
            var ecallEvt = Assert.IsType<EcallEvent>(
                Resolve(InstructionsEnum.ECALL, state, bundleSerial: 2, vtId: 1).PipelineEvent);
            Assert.Equal(93L, ecallEvt.EcallCode);
        }

        [Fact]
        public void Ecall_Event_StampsVtId()
        {
            var state = new P1CpuState();
            var ecallEvt = Assert.IsType<EcallEvent>(
                Resolve(InstructionsEnum.ECALL, state, bundleSerial: 7, vtId: 3).PipelineEvent);
            Assert.Equal((byte)3, ecallEvt.VtId);
            Assert.Equal(7UL, ecallEvt.BundleSerial);
        }

        [Fact]
        public void Fence_Event_IsDataFenceForFENCE()
        {
            var state = new P1CpuState();
            var fenceEvt = Assert.IsType<FenceEvent>(
                Resolve(InstructionsEnum.FENCE, state, bundleSerial: 1, vtId: 0).PipelineEvent);
            Assert.False(fenceEvt.IsInstructionFence);
        }

        [Fact]
        public void Fence_Event_IsInstructionFenceForFENCE_I()
        {
            var state = new P1CpuState();
            var fenceEvt = Assert.IsType<FenceEvent>(
                Resolve(InstructionsEnum.FENCE_I, state, bundleSerial: 1, vtId: 0).PipelineEvent);
            Assert.True(fenceEvt.IsInstructionFence);
        }

        [Fact]
        public void Wfi_Event_IsWfiEvent()
        {
            var state = new P1CpuState();
            Assert.IsType<WfiEvent>(Resolve(InstructionsEnum.WFI, state, bundleSerial: 1, vtId: 0).PipelineEvent);
        }

        [Fact]
        public void Mret_Event_IsMretEvent()
        {
            var state = new P1CpuState();
            Assert.IsType<MretEvent>(Resolve(InstructionsEnum.MRET, state, bundleSerial: 1, vtId: 0).PipelineEvent);
        }

        [Fact]
        public void Sret_Event_IsSretEvent()
        {
            var state = new P1CpuState();
            Assert.IsType<SretEvent>(Resolve(InstructionsEnum.SRET, state, bundleSerial: 1, vtId: 0).PipelineEvent);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // SmtVt instructions resolve PipelineEvents through direct compat retire
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public class DispatcherSmtVtInstructionTests
    {
        private static (RecordingEventQueue queue, ExecutionDispatcherV4 dispatcher) Build()
        {
            var queue = new RecordingEventQueue();
            var dispatcher = new ExecutionDispatcherV4(pipelineEventQueue: queue);
            return (queue, dispatcher);
        }

        private static RetireWindowCaptureSnapshot Resolve(
            InstructionsEnum opcode,
            ulong bundleSerial = 1,
            byte vtId = 0)
        {
            var dispatcher = new ExecutionDispatcherV4();
            var state = new P1CpuState();
            return RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                P1Ir.Make(opcode),
                state,
                bundleSerial,
                vtId);
        }

        [Theory]
        [InlineData(InstructionsEnum.YIELD)]
        [InlineData(InstructionsEnum.WFE)]
        [InlineData(InstructionsEnum.SEV)]
        [InlineData(InstructionsEnum.POD_BARRIER)]
        [InlineData(InstructionsEnum.VT_BARRIER)]
        public void SmtVt_Instructions_EagerExecuteIsRejectedInFavorOfDirectCompatRetire(InstructionsEnum opcode)
        {
            var (queue, dispatcher) = Build();
            var state = new P1CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P1Ir.Make(opcode), state, bundleSerial: 1, vtId: 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.True(Resolve(opcode).HasPipelineEvent);
        }

        [Fact]
        public void Yield_Resolves_YieldEvent()
        {
            Assert.IsType<YieldEvent>(Resolve(InstructionsEnum.YIELD).PipelineEvent);
        }

        [Fact]
        public void Wfe_Resolves_WfeEvent()
        {
            Assert.IsType<WfeEvent>(Resolve(InstructionsEnum.WFE).PipelineEvent);
        }

        [Fact]
        public void Sev_Resolves_SevEvent()
        {
            Assert.IsType<SevEvent>(Resolve(InstructionsEnum.SEV).PipelineEvent);
        }

        [Fact]
        public void PodBarrier_Resolves_PodBarrierEvent()
        {
            Assert.IsType<PodBarrierEvent>(Resolve(InstructionsEnum.POD_BARRIER).PipelineEvent);
        }

        [Fact]
        public void VtBarrier_Resolves_VtBarrierEvent()
        {
            Assert.IsType<VtBarrierEvent>(Resolve(InstructionsEnum.VT_BARRIER).PipelineEvent);
        }
    }
}

