// V5 Phase 3: Engine ISA-Agnostic Restructuring + unified retire authority + CommitUnit
//
// Covers:
//   [T3-01] ExecutionEngine: dispatch all InternalOpKind values via IExecutionUnit registry
//   [T3-02] ExecutionEngine: throws InvalidInternalOpException for unregistered kind
//   [T3-03] New execution-engine files contain no reference to IsaV4Opcode / InstructionsEnum
//   [T3-04] PipelineFsmGuard.Advance exists and is the single FSM state-advance entry-point
//   [T3-05] CommitUnit: write to VT0 register file does not affect VT1 register file
//   [T3-06] CommitUnit: commits in program order (FIFO within VT)
//   [T3-07] VT register isolation: 4 VTs each with independent register state, no cross-VT leakage
//   [T3-08] ICanonicalCpuState.ReadRegister always requires vtId parameter вЂ” no overload without vtId
//   [T3-09] VT register bank: x0 always reads as 0 regardless of write attempts
//   [T3-10] PipelineFsmGuard.Advance: HaltEvent drives Taskв†’Halted, ResetEvent drives Resetв†’Task

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.V5Phase3
{
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Test helpers
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Minimal ICanonicalCpuState stub - provides only what Phase 3 execution tests need.
    /// Implements the VT-scoped ReadRegister/ReadPc surface explicitly.
    /// </summary>
    internal sealed class P3CpuState : ICanonicalCpuState
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
        public void TransitionPipelineState(PipelineTransitionTrigger trigger)
            => _pipelineState = PipelineFsmGuard.Transition(_pipelineState, trigger);
    }

    /// <summary>
    /// Stub IExecutionUnit that returns a configurable ExecutionResult.
    /// </summary>
    internal sealed class StubExecutionUnit : IExecutionUnit
    {
        private readonly ExecutionResult _result;
        public bool WasCalled { get; private set; }

        public StubExecutionUnit(ExecutionResult result = default) => _result = result;

        public ExecutionResult Execute(InternalOp op, ICanonicalCpuState state)
        {
            WasCalled = true;
            return _result;
        }
    }

    /// <summary>
    /// Builds a full registry with a no-op stub unit for every InternalOpKind value.
    /// </summary>
    internal static class FullRegistryFactory
    {
        public static Dictionary<InternalOpKind, IExecutionUnit> Build()
        {
            var dict = new Dictionary<InternalOpKind, IExecutionUnit>();
            foreach (InternalOpKind kind in Enum.GetValues<InternalOpKind>())
                dict[kind] = new StubExecutionUnit(ExecutionResult.Ok(0));
            return dict;
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T3-01 / T3-02 вЂ” ExecutionEngine dispatch
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class ExecutionEngineDispatchTests
    {
        // в”Ђв”Ђ T3-01a в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_01_ExecutionEngine_WhenRegistryContainsAllKinds_ThenAllKindsDispatchSuccessfully()
        {
            var registry = FullRegistryFactory.Build();
            var engine = new ExecutionEngine(registry);
            var state = new P3CpuState();

            foreach (InternalOpKind kind in Enum.GetValues<InternalOpKind>())
            {
                var op = new InternalOp { Kind = kind };
                var result = engine.Execute(op, state);
                Assert.Equal(0UL, result.Value);
            }
        }

        // в”Ђв”Ђ T3-01b: each stub was invoked в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_01b_ExecutionEngine_WhenKindDispatched_ThenCorrectUnitIsCalled()
        {
            var stub = new StubExecutionUnit(ExecutionResult.Ok(42));
            var registry = FullRegistryFactory.Build();
            registry[InternalOpKind.Add] = stub;

            var engine = new ExecutionEngine(registry);
            var op = new InternalOp { Kind = InternalOpKind.Add };
            var result = engine.Execute(op, new P3CpuState());

            Assert.True(stub.WasCalled);
            Assert.Equal(42UL, result.Value);
        }

        // в”Ђв”Ђ T3-01c: registry covers every enum member в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_01c_ExecutionEngine_FullRegistry_HasEntryForEveryInternalOpKind()
        {
            var kinds = Enum.GetValues<InternalOpKind>();
            var registry = FullRegistryFactory.Build();

            foreach (var kind in kinds)
                Assert.True(registry.ContainsKey(kind), $"Missing unit for {kind}");
        }

        // в”Ђв”Ђ T3-02: unregistered kind throws в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_02_ExecutionEngine_WhenKindHasNoUnit_ThenThrowsInvalidInternalOpException()
        {
            var engine = new ExecutionEngine(new Dictionary<InternalOpKind, IExecutionUnit>());
            var op = new InternalOp { Kind = InternalOpKind.Add };

            var ex = Assert.Throws<InvalidInternalOpException>(() =>
                engine.Execute(op, new P3CpuState()));

            Assert.Equal(InternalOpKind.Add, ex.Kind);
        }

        [Fact]
        public void T3_02b_InvalidInternalOpException_WhenConstructed_ThenMessageContainsKindName()
        {
            var ex = new InvalidInternalOpException(InternalOpKind.Load);
            Assert.Contains("Load", ex.Message);
        }

        [Fact]
        public void T3_02c_ExecutionEngine_WhenNullRegistry_ThenThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ExecutionEngine(null!));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T3-03 вЂ” ISA opcode isolation (engine types must not depend on ISA opcodes)
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class IsaOpcodeIsolationTests
    {
        // Returns all types reachable from method signatures of the given type
        private static IEnumerable<Type> GetSignatureTypes(Type t)
        {
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                           BindingFlags.Static | BindingFlags.NonPublic))
            {
                yield return m.ReturnType;
                foreach (var p in m.GetParameters())
                    yield return p.ParameterType;
            }
        }

        // в”Ђв”Ђ T3-03a: IExecutionUnit.Execute only uses InternalOp + ICanonicalCpuState в”Ђв”Ђ
        [Fact]
        public void T3_03_IExecutionUnit_ExecuteSignature_HasNoIsaOpcodeTypeInParameters()
        {
            var method = typeof(IExecutionUnit).GetMethod("Execute")!;
            foreach (var p in method.GetParameters())
            {
                Assert.DoesNotContain("IsaV4Opcode", p.ParameterType.Name);
                Assert.DoesNotContain("InstructionsEnum", p.ParameterType.Name);
            }
        }

        // в”Ђв”Ђ T3-03b: ExecutionEngine has no ISA opcode type in any method signature в”Ђв”Ђ
        [Fact]
        public void T3_03b_ExecutionEngine_MethodSignatures_ContainNoIsaOpcodeType()
        {
            foreach (var t in GetSignatureTypes(typeof(ExecutionEngine)))
            {
                Assert.DoesNotContain("IsaV4Opcode", t.Name);
                Assert.DoesNotContain("InstructionsEnum", t.Name);
            }
        }

        // в”Ђв”Ђ T3-03c: InvalidInternalOpException carries InternalOpKind, not ISA enum в”Ђв”Ђ
        [Fact]
        public void T3_03c_InvalidInternalOpException_CarriesInternalOpKindNotIsaEnum()
        {
            var prop = typeof(InvalidInternalOpException).GetProperty("Kind")!;
            Assert.Equal(typeof(InternalOpKind), prop.PropertyType);
        }

        [Fact]
        public void T3_03d_IExecutionUnit_InterfaceHasNoIsaTypeDependency()
        {
            var type = typeof(IExecutionUnit);
            var method = type.GetMethod("Execute")!;
            var paramTypes = method.GetParameters().Select(p => p.ParameterType.Name).ToArray();

            // Parameters must be InternalOp and ICanonicalCpuState вЂ” no ISA-specific types
            Assert.Contains("InternalOp", paramTypes);
            Assert.Contains("ICanonicalCpuState", paramTypes);
            Assert.Equal(2, paramTypes.Length);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T3-04 / T3-10 вЂ” PipelineFsmGuard single authority + Advance method
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class PipelineFsmGuardAdvanceTests
    {
        // в”Ђв”Ђ T3-04: PipelineFsmGuard.Advance is a public static method в”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_04_PipelineFsmGuard_HasPublicStaticAdvanceMethod()
        {
            var method = typeof(PipelineFsmGuard).GetMethod(
                "Advance",
                BindingFlags.Public | BindingFlags.Static);

            Assert.NotNull(method);
        }

        [Fact]
        public void T3_04b_PipelineFsmGuard_AdvanceMethod_TakesPipelineStateAndPipelineEvent()
        {
            var method = typeof(PipelineFsmGuard).GetMethod(
                "Advance",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(PipelineState), typeof(PipelineEvent) },
                null);

            Assert.NotNull(method);
        }

        [Fact]
        public void T3_04c_PipelineFsmGuard_AdvanceMethod_ReturnsPipelineState()
        {
            var method = typeof(PipelineFsmGuard).GetMethod(
                "Advance",
                BindingFlags.Public | BindingFlags.Static);

            Assert.NotNull(method);
            Assert.Equal(typeof(PipelineState), method!.ReturnType);
        }

        // в”Ђв”Ђ T3-10: HaltEvent drives Taskв†’Halted в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_10_PipelineFsmGuard_WhenHaltEventFromTask_ThenTransitionsToHalted()
        {
            var evt = new HaltEvent { VtId = 0, BundleSerial = 1, Reason = "all-WFI" };
            var next = PipelineFsmGuard.Advance(PipelineState.Task, evt);
            Assert.Equal(PipelineState.Halted, next);
        }

        [Fact]
        public void T3_10b_PipelineFsmGuard_WhenResetEventFromReset_ThenTransitionsToTask()
        {
            var evt = new ResetEvent { VtId = 0, BundleSerial = 0 };
            var next = PipelineFsmGuard.Advance(PipelineState.Reset, evt);
            Assert.Equal(PipelineState.Task, next);
        }

        [Fact]
        public void T3_10c_PipelineFsmGuard_WhenWfiEventFromTask_ThenTransitionsToHalted()
        {
            var evt = new WfiEvent { VtId = 0, BundleSerial = 2 };
            var next = PipelineFsmGuard.Advance(PipelineState.Task, evt);
            Assert.Equal(PipelineState.Halted, next);
        }

        [Fact]
        public void T3_10d_PipelineFsmGuard_WhenVmLaunchTriggerFromTask_ThenTransitionsToVmEntry()
        {
            var next = PipelineFsmGuard.Transition(PipelineState.Task, PipelineTransitionTrigger.VmLaunch);
            Assert.Equal(PipelineState.VmEntry, next);
        }

        [Fact]
        public void T3_10e_PipelineFsmGuard_WhenVmExitTriggerFromGuestExecution_ThenTransitionsToVmExit()
        {
            var next = PipelineFsmGuard.Transition(PipelineState.GuestExecution, PipelineTransitionTrigger.VmExitCond);
            Assert.Equal(PipelineState.VmExit, next);
        }

        // Non-FSM events must leave state unchanged
        [Fact]
        public void T3_10f_PipelineFsmGuard_WhenNonFsmEvent_ThenStateUnchanged()
        {
            var evt = new EcallEvent { VtId = 0, BundleSerial = 5, EcallCode = 9 };
            var next = PipelineFsmGuard.Advance(PipelineState.Task, evt);
            Assert.Equal(PipelineState.Task, next);
        }

        [Fact]
        public void T3_10g_PipelineFsmGuard_WhenFenceEventFromTask_ThenStateUnchanged()
        {
            var evt = new FenceEvent { VtId = 0, BundleSerial = 6, IsInstructionFence = false };
            var next = PipelineFsmGuard.Advance(PipelineState.Task, evt);
            Assert.Equal(PipelineState.Task, next);
        }

        [Fact]
        public void T3_10h_PipelineFsmGuard_WhenMretEventFromTask_ThenThrowsIllegalTransition()
        {
            // C15: MretEvent maps to TrapReturn trigger.
            // Task has no TrapReturn transition вЂ” MRET outside a trap handler is illegal.
            var evt = new MretEvent { VtId = 0, BundleSerial = 7 };
            Assert.Throws<IllegalFsmTransitionException>(() =>
                PipelineFsmGuard.Advance(PipelineState.Task, evt));
        }

        [Fact]
        public void T3_10i_PipelineFsmGuard_WhenNullEvent_ThenThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PipelineFsmGuard.Advance(PipelineState.Task, null!));
        }

        // Advance must ultimately call Transition в†’ illegal transitions still throw
        [Fact]
        public void T3_10j_PipelineFsmGuard_WhenHaltFromAlreadyHalted_ThenThrowsIllegalTransition()
        {
            var evt = new HaltEvent { VtId = 0, BundleSerial = 8, Reason = "nested" };
            Assert.Throws<IllegalFsmTransitionException>(() =>
                PipelineFsmGuard.Advance(PipelineState.Halted, evt));
        }

        [Fact]
        public void T3_10k_PipelineFsmGuard_WhenVmxOffTriggerFromGuestExecution_ThenTransitionsToVmExit()
        {
            var next = PipelineFsmGuard.Transition(PipelineState.GuestExecution, PipelineTransitionTrigger.VmxOff);
            Assert.Equal(PipelineState.VmExit, next);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T3-05 / T3-06 / T3-07 вЂ” CommitUnit + VT isolation
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class CommitUnitTests
    {
        internal static (CommitUnit unit, RetireCoordinator retireCoordinator, ArchContextState[] contexts, PhysicalRegisterFile physicalRegisters, RenameMap renameMap, CommitMap commitMap) BuildFourVtForUnifiedState()
        {
            var contexts = Enumerable.Range(0, SmtWays)
                .Select(i => new ArchContextState(RenameMap.ArchRegs, i))
                .ToArray();
            var physicalRegisters = new PhysicalRegisterFile();
            var renameMap = new RenameMap(SmtWays);
            var commitMap = new CommitMap(SmtWays);

            for (int vt = 0; vt < SmtWays; vt++)
            {
                commitMap.Commit(vt, 0, 0);

                for (int archReg = 1; archReg < RenameMap.ArchRegs; archReg++)
                {
                    int physReg = GetDedicatedPhysRegIdForTest(vt, archReg);
                    renameMap.Remap(vt, archReg, physReg);
                    commitMap.Commit(vt, archReg, physReg);
                }
            }

            var retireCoordinator = new RetireCoordinator(physicalRegisters, renameMap, commitMap, contexts);
            var unit = CommitUnit.FromRetireCoordinator(retireCoordinator);
            return (unit, retireCoordinator, contexts, physicalRegisters, renameMap, commitMap);
        }

        internal static int GetDedicatedPhysRegIdForTest(int vtId, int archReg)
        {
            if (archReg == 0)
                return 0;

            return 1 + (vtId * (RenameMap.ArchRegs - 1)) + (archReg - 1);
        }

        // в”Ђв”Ђ T3-05: VT isolation вЂ” write to VT0 does not affect VT1 в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_05_CommitUnit_WhenWriteToVt0_ThenVt1RegisterUnchanged()
        {
            var (unit, _, contexts, physicalRegisters, _, commitMap) = BuildFourVtForUnifiedState();

            unit.Commit(
                new[]
                {
                    RetireRecord.RegisterWrite(0, 5, 42UL)
                },
                vtId: 0);

            Assert.Equal(42UL, contexts[0].CommittedRegs[5]);
            Assert.Equal(0UL, contexts[1].CommittedRegs[5]);
            Assert.Equal(42UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(0, 5)));
            Assert.Equal(0UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(1, 5)));
            Assert.Equal(GetDedicatedPhysRegIdForTest(0, 5), commitMap.Lookup(0, 5));
        }

        [Fact]
        public void T3_05b_CommitUnit_WhenWriteToVt2_ThenVt0Vt1Vt3Unchanged()
        {
            var (unit, _, contexts, physicalRegisters, _, commitMap) = BuildFourVtForUnifiedState();

            unit.Commit(
                new[]
                {
                    RetireRecord.RegisterWrite(2, 10, 99UL)
                },
                vtId: 2);

            Assert.Equal(0UL, contexts[0].CommittedRegs[10]);
            Assert.Equal(0UL, contexts[1].CommittedRegs[10]);
            Assert.Equal(99UL, contexts[2].CommittedRegs[10]);
            Assert.Equal(0UL, contexts[3].CommittedRegs[10]);
            Assert.Equal(99UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(2, 10)));
            Assert.Equal(GetDedicatedPhysRegIdForTest(2, 10), commitMap.Lookup(2, 10));
        }

        // в”Ђв”Ђ T3-06: program order вЂ” FIFO within VT в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_06_CommitUnit_WhenMultipleRetiresSameVt_ThenLastWriteWins()
        {
            var (unit, _, contexts, physicalRegisters, _, commitMap) = BuildFourVtForUnifiedState();

            unit.Commit(new[] { RetireRecord.RegisterWrite(0, 1, 10UL) }, vtId: 0);
            unit.Commit(new[] { RetireRecord.RegisterWrite(0, 1, 20UL) }, vtId: 0);
            unit.Commit(new[] { RetireRecord.RegisterWrite(0, 1, 30UL) }, vtId: 0);

            Assert.Equal(30UL, contexts[0].CommittedRegs[1]);
            Assert.Equal(30UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(0, 1)));
            Assert.Equal(GetDedicatedPhysRegIdForTest(0, 1), commitMap.Lookup(0, 1));
        }

        [Fact]
        public void T3_06b_CommitUnit_WhenInterleavedVtRetires_ThenEachVtHasOwnResult()
        {
            var (unit, _, contexts, physicalRegisters, _, _) = BuildFourVtForUnifiedState();

            unit.Commit(new[] { RetireRecord.RegisterWrite(0, 7, 100UL) }, vtId: 0);
            unit.Commit(new[] { RetireRecord.RegisterWrite(1, 7, 200UL) }, vtId: 1);
            unit.Commit(new[] { RetireRecord.RegisterWrite(2, 7, 300UL) }, vtId: 2);
            unit.Commit(new[] { RetireRecord.RegisterWrite(3, 7, 400UL) }, vtId: 3);

            Assert.Equal(100UL, contexts[0].CommittedRegs[7]);
            Assert.Equal(200UL, contexts[1].CommittedRegs[7]);
            Assert.Equal(300UL, contexts[2].CommittedRegs[7]);
            Assert.Equal(400UL, contexts[3].CommittedRegs[7]);
            Assert.Equal(100UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(0, 7)));
            Assert.Equal(200UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(1, 7)));
            Assert.Equal(300UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(2, 7)));
            Assert.Equal(400UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(3, 7)));
        }

        // в”Ђв”Ђ T3-06c: PC redirect is committed в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_06c_CommitUnit_WhenPcRedirected_ThenVtPcIsUpdated()
        {
            var (unit, _, contexts, _, _, _) = BuildFourVtForUnifiedState();

            unit.Commit(
                new[]
                {
                    RetireRecord.RegisterWrite(1, 1, 0x1004UL),
                    RetireRecord.PcWrite(1, 0x8000_0000UL)
                },
                vtId: 1);

            Assert.Equal(0x8000_0000UL, contexts[1].CommittedPc);
            Assert.Equal(0UL, contexts[0].CommittedPc);
        }

        // в”Ђв”Ђ T3-06d: Rd=0 write is silently discarded в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_06d_CommitUnit_WhenBatchWritesX0_ThenNoRegisterWritten()
        {
            var (unit, _, contexts, physicalRegisters, _, commitMap) = BuildFourVtForUnifiedState();

            unit.Commit(
                new[]
                {
                    RetireRecord.RegisterWrite(0, 0, 99UL)
                },
                vtId: 0);

            Assert.Equal(0UL, contexts[0].CommittedRegs[0]);
            Assert.Equal(0UL, physicalRegisters.Read(0));
            Assert.Equal(0, commitMap.Lookup(0, 0));
        }

        // в”Ђв”Ђ T3-07: 4-VT full isolation в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_06da_CommitUnit_WhenBatchCommitWithinSingleVt_ThenAppliesRecordsInOrder()
        {
            var (unit, _, contexts, physicalRegisters, _, _) = BuildFourVtForUnifiedState();

            unit.Commit(
                new[]
                {
                    RetireRecord.RegisterWrite(2, 5, 11UL),
                    RetireRecord.PcWrite(2, 0x2200UL),
                    RetireRecord.RegisterWrite(2, 5, 22UL)
                },
                vtId: 2);

            Assert.Equal(22UL, contexts[2].CommittedRegs[5]);
            Assert.Equal(0x2200UL, contexts[2].CommittedPc);
            Assert.Equal(22UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(2, 5)));
        }

        [Fact]
        public void T3_06db_CommitUnit_WhenSeparateBatchCommitsUseDifferentVts_ThenStateRemainsIsolated()
        {
            var (unit, _, contexts, physicalRegisters, _, _) = BuildFourVtForUnifiedState();

            unit.Commit(
                new[]
                {
                    RetireRecord.RegisterWrite(0, 3, 101UL),
                    RetireRecord.PcWrite(0, 0x1000UL)
                },
                vtId: 0);
            unit.Commit(
                new[]
                {
                    RetireRecord.RegisterWrite(3, 7, 303UL),
                    RetireRecord.PcWrite(3, 0x3000UL)
                },
                vtId: 3);

            Assert.Equal(101UL, contexts[0].CommittedRegs[3]);
            Assert.Equal(0x1000UL, contexts[0].CommittedPc);
            Assert.Equal(303UL, contexts[3].CommittedRegs[7]);
            Assert.Equal(0x3000UL, contexts[3].CommittedPc);
            Assert.Equal(0UL, contexts[1].CommittedPc);
            Assert.Equal(0UL, contexts[2].CommittedPc);
            Assert.Equal(101UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(0, 3)));
            Assert.Equal(303UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(3, 7)));
        }

        [Fact]
        public void T3_06e_CommitUnit_WhenBatchContainsMixedVirtualThreadOwnership_ThenThrowsWithoutCommitting()
        {
            var (unit, _, contexts, physicalRegisters, _, _) = BuildFourVtForUnifiedState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                unit.Commit(
                    new[]
                    {
                        RetireRecord.RegisterWrite(0, 5, 123UL),
                        RetireRecord.PcWrite(1, 0x4400UL)
                    },
                    vtId: 0));

            Assert.Contains("record[1]", ex.Message, StringComparison.Ordinal);
            Assert.Equal(0UL, contexts[0].CommittedPc);
            Assert.Equal(0UL, contexts[0].CommittedRegs[5]);
            Assert.Equal(0UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(0, 5)));
        }

        [Fact]
        public void T3_06ea_CommitUnit_WhenSingleRecordBelongsToDifferentVt_ThenThrowsWithoutCommitting()
        {
            var (unit, _, contexts, physicalRegisters, _, _) = BuildFourVtForUnifiedState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                unit.Commit(
                    new[]
                    {
                        RetireRecord.RegisterWrite(2, 5, 77UL)
                    },
                    vtId: 0));

            Assert.Contains("record[0]", ex.Message, StringComparison.Ordinal);
            Assert.Equal(0UL, contexts[0].CommittedRegs[5]);
            Assert.Equal(0UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(0, 5)));
            Assert.Equal(0UL, contexts[0].CommittedPc);
        }

        [Fact]
        public void T3_06f_CommitUnit_WhenBatchIsEmpty_ThenLeavesStateUnchanged()
        {
            var (unit, _, contexts, physicalRegisters, _, _) = BuildFourVtForUnifiedState();

            unit.Commit(Array.Empty<RetireRecord>(), vtId: 0);

            Assert.Equal(0UL, contexts[0].CommittedRegs[5]);
            Assert.Equal(0UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(0, 5)));
            Assert.Equal(0UL, contexts[0].CommittedPc);
        }

        [Fact]
        public void T3_06g_CommitUnit_WhenBatchOnlyUpdatesPc_ThenRegisterFileRemainsUntouched()
        {
            var (unit, _, contexts, physicalRegisters, _, _) = BuildFourVtForUnifiedState();

            unit.Commit(
                new[]
                {
                    RetireRecord.PcWrite(0, 0x7700UL)
                },
                vtId: 0);

            Assert.Equal(0x7700UL, contexts[0].CommittedPc);
            Assert.Equal(0UL, contexts[0].CommittedRegs[5]);
            Assert.Equal(0UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(0, 5)));
        }

        [Fact]
        public void T3_07_CommitUnit_FourVts_EachMaintainIndependentState()
        {
            var (unit, _, contexts, physicalRegisters, _, _) = BuildFourVtForUnifiedState();

            for (byte vt = 0; vt < 4; vt++)
            {
                unit.Commit(
                    new[]
                    {
                        RetireRecord.RegisterWrite(vt, (byte)(vt + 1), (ulong)(vt * 100))
                    },
                    vtId: vt);
            }

            Assert.Equal(0UL, contexts[0].CommittedRegs[1]);
            Assert.Equal(100UL, contexts[1].CommittedRegs[2]);
            Assert.Equal(200UL, contexts[2].CommittedRegs[3]);
            Assert.Equal(300UL, contexts[3].CommittedRegs[4]);
            Assert.Equal(0UL, contexts[0].CommittedRegs[2]);
            Assert.Equal(300UL, physicalRegisters.Read(GetDedicatedPhysRegIdForTest(3, 4)));
        }

        // в”Ђв”Ђ Error cases в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_05c_CommitUnit_WhenVtIdOutOfRange_ThenThrowsArgumentOutOfRange()
        {
            var (unit, _, _, _, _, _) = BuildFourVtForUnifiedState();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                unit.Commit(
                    new[]
                    {
                        RetireRecord.RegisterWrite(0, 1, 1UL)
                    },
                    vtId: 99));
        }

        [Fact]
        public void T3_05d_CommitUnit_WhenNullRetireCoordinator_ThenThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                CommitUnit.FromRetireCoordinator(null!));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T3-08 / T3-09 вЂ” ICanonicalCpuState VT-scoped + VtRegisterBank x0 contract
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class UnifiedRetireStateTests
    {
        // в”Ђв”Ђ T3-08: ICanonicalCpuState.ReadRegister requires vtId в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_08_ICpuState_ReadRegisterMethod_RequiresVtIdParameter()
        {
            var methods = typeof(ICanonicalCpuState).GetMethods()
                .Where(m => m.Name == "ReadRegister")
                .ToArray();

            Assert.True(methods.Length > 0, "ICanonicalCpuState must declare ReadRegister");

            // All ReadRegister overloads must have a vtId parameter
            foreach (var method in methods)
            {
                var paramNames = method.GetParameters().Select(p => p.Name).ToArray();
                Assert.Contains("vtId", paramNames);
            }
        }

        [Fact]
        public void T3_08b_ICpuState_ReadPcMethod_RequiresVtIdParameter()
        {
            var methods = typeof(ICanonicalCpuState).GetMethods()
                .Where(m => m.Name == "ReadPc")
                .ToArray();

            Assert.True(methods.Length > 0, "ICanonicalCpuState must declare ReadPc");

            foreach (var method in methods)
            {
                var paramNames = method.GetParameters().Select(p => p.Name).ToArray();
                Assert.Contains("vtId", paramNames);
            }
        }

        // в”Ђв”Ђ T3-09: VtRegisterBank x0 contract в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T3_09_RetireCoordinator_X0_AlwaysReadsAsZero_Regardless_Of_WriteAttempt()
        {
            var (_, retireCoordinator, contexts, physicalRegisters, _, commitMap) = CommitUnitTests.BuildFourVtForUnifiedState();

            retireCoordinator.Retire(RetireRecord.RegisterWrite(0, 0, 999UL));

            Assert.Equal(0UL, contexts[0].CommittedRegs[0]);
            Assert.Equal(0UL, physicalRegisters.Read(0));
            Assert.Equal(0, commitMap.Lookup(0, 0));
        }

        [Fact]
        public void T3_09b_RetireCoordinator_PcWrite_OnlyUpdatesTargetVt()
        {
            var (_, retireCoordinator, contexts, _, _, _) = CommitUnitTests.BuildFourVtForUnifiedState();

            retireCoordinator.Retire(RetireRecord.PcWrite(2, 0xDEAD_BEEF_0000_0000UL));

            Assert.Equal(0UL, contexts[0].CommittedPc);
            Assert.Equal(0UL, contexts[1].CommittedPc);
            Assert.Equal(0xDEAD_BEEF_0000_0000UL, contexts[2].CommittedPc);
            Assert.Equal(0UL, contexts[3].CommittedPc);
        }

        [Fact]
        public void T3_09c_RetireCoordinator_FourVts_AreCompletelyIndependent()
        {
            var (_, retireCoordinator, contexts, physicalRegisters, _, _) = CommitUnitTests.BuildFourVtForUnifiedState();

            for (int vt = 0; vt < SmtWays; vt++)
                retireCoordinator.Retire(RetireRecord.RegisterWrite(vt, 5, (ulong)(vt * 1000 + 1)));

            for (int vt = 0; vt < SmtWays; vt++)
            {
                Assert.Equal((ulong)(vt * 1000 + 1), contexts[vt].CommittedRegs[5]);
                Assert.Equal((ulong)(vt * 1000 + 1), physicalRegisters.Read(CommitUnitTests.GetDedicatedPhysRegIdForTest(vt, 5)));
            }
        }

        [Fact]
        public void T3_09d_RetireCoordinator_WhenRegisterWriteVtIdOutOfRange_ThenThrowsArgumentOutOfRange()
        {
            var (_, retireCoordinator, contexts, _, _, _) = CommitUnitTests.BuildFourVtForUnifiedState();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                retireCoordinator.Retire(RetireRecord.RegisterWrite(contexts.Length, 5, 123UL)));
        }

        [Fact]
        public void T3_09e_RetireCoordinator_WhenPcWriteVtIdOutOfRange_ThenThrowsArgumentOutOfRange()
        {
            var (_, retireCoordinator, contexts, _, _, _) = CommitUnitTests.BuildFourVtForUnifiedState();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                retireCoordinator.Retire(RetireRecord.PcWrite(contexts.Length, 0x1000UL)));
        }

        [Fact]
        public void T3_09f_RetireCoordinator_WhenArchRegOutOfRange_ThenThrowsArgumentOutOfRange()
        {
            var (_, retireCoordinator, _, _, _, _) = CommitUnitTests.BuildFourVtForUnifiedState();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                retireCoordinator.Retire(RetireRecord.RegisterWrite(0, RenameMap.ArchRegs, 123UL)));
        }



        // в”Ђв”Ђ T3-07: 4-VT isolation via VtRegisterBank directly в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        // в”Ђв”Ђ ICanonicalCpuState.ReadRegister default implementation delegating properly в”Ђв”Ђ\r\n}

        // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        // Architecture boundary: IExecutionUnit must not know about ISA types
        // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        public sealed class ExecutionEngineArchitectureBoundaryTests
        {
            [Fact]
            public void T3_Arch_ExecutionEngine_AssemblyContainsNoIsaV4OpcodeType()
            {
                var asm = typeof(ExecutionEngine).Assembly;
                var typeNames = asm.GetTypes().Select(t => t.Name).ToArray();

                Assert.DoesNotContain("IsaV4Opcode", typeNames);
            }

            [Fact]
            public void T3_Arch_IExecutionUnit_SignatureUsesOnlyInternalOpAndICpuState()
            {
                var executeMethod = typeof(IExecutionUnit).GetMethod("Execute")!;
                var paramTypes = executeMethod.GetParameters()
                    .Select(p => p.ParameterType)
                    .ToArray();

                Assert.Equal(2, paramTypes.Length);
                Assert.Equal(typeof(InternalOp), paramTypes[0]);
                Assert.Equal(typeof(ICanonicalCpuState), paramTypes[1]);
            }

            [Fact]
            public void T3_Arch_ExecutionEngine_IsSealed()
            {
                Assert.True(typeof(ExecutionEngine).IsSealed);
            }

            [Fact]
            public void T3_Arch_CommitUnit_IsSealed()
            {
                Assert.True(typeof(CommitUnit).IsSealed);
            }

            [Fact]
            public void T3_Arch_RetireCoordinator_IsSealed()
            {
                Assert.True(typeof(RetireCoordinator).IsSealed);
            }

            [Fact]
            public void T3_Arch_InvalidInternalOpException_IsSealed()
            {
                Assert.True(typeof(InvalidInternalOpException).IsSealed);
            }

            [Fact]
            public void T3_Arch_CommitUnit_ExposesRetireCoordinatorFactory()
            {
                var factory = typeof(CommitUnit).GetMethod(
                    "FromRetireCoordinator",
                    BindingFlags.Public | BindingFlags.Static);

                Assert.NotNull(factory);
                Assert.Equal(typeof(CommitUnit), factory!.ReturnType);
                Assert.Equal(typeof(RetireCoordinator), factory.GetParameters().Single().ParameterType);
            }

            [Fact]
            public void T3_Arch_CommitUnit_HasNoPublicConstructors()
            {
                Assert.Empty(typeof(CommitUnit).GetConstructors());
            }

            [Fact]
            public void T3_Arch_CommitUnit_OnlyExposesBatchCommitSurface()
            {
                var publicInstanceMethods = typeof(CommitUnit)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                var commitMethods = publicInstanceMethods
                    .Where(m => m.Name == "Commit")
                    .ToArray();

                Assert.Single(commitMethods);
                Assert.DoesNotContain(publicInstanceMethods, m => m.Name == "Retire");

                var parameters = commitMethods[0].GetParameters();
                Assert.Equal(typeof(ReadOnlySpan<RetireRecord>), parameters[0].ParameterType);
                Assert.Equal(typeof(byte), parameters[1].ParameterType);
            }
        }
    }
}


