// Phase 12: Final ISA Integration вЂ” ISA v4 Freeze and End-to-End Trace Wiring
// Covers:
//   - IsaV4Surface freeze constants: IsaVersion, IsaMandatoryOpcodeCount, FrozenDate
//   - ExecutionDispatcherV4: IV4TraceEventSink + TelemetryCounters constructor params
//   - ExecutionDispatcherV4.Execute(instr, state, bundleSerial, vtId): emits V4TraceEvent
//   - TraceEventKind emitted for each supported eager direct surface
//       (ALU, Memory, ControlFlow, CSR)
//   - Atomic, System-event, SmtVt-event, stream-control, and VMX eager direct surfaces are explicitly rejected and do not emit trace/telemetry
//   - Trace event bundleSerial / vtId stamping
//   - TelemetryCounters incremented via ApplyTraceEvent
//   - NullV4TraceEventSink used when no sink provided (backward-compatible)
//   - CSR address payload in CsrRead / CsrWrite events

using System;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase12
{
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Test helpers
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>Minimal ICanonicalCpuState stub for Phase 12 integration tests.</summary>
    internal sealed class P12CpuState : ICanonicalCpuState
    {
        private readonly Dictionary<ushort, ulong> _regs = new();
        private ulong _pc;
        private PipelineState _pipelineState = PipelineState.Task;

        public ulong ReadIntRegister(ushort id)  => _regs.TryGetValue(id, out var v) ? v : 0UL;
        public void WriteIntRegister(ushort id, ulong value)
        {
            if (id != 0) _regs[id] = value;
        }
        public void SetReg(ushort id, ulong value) => _regs[id] = value;

        public long ReadRegister(byte vtId, int regId) =>
            unchecked((long)ReadIntRegister((ushort)regId));

        public void WriteRegister(byte vtId, int regId, ulong value) =>
            WriteIntRegister((ushort)regId, value);

        public ulong GetInstructionPointer()            => _pc;
        public void  SetInstructionPointer(ulong ip)    => _pc = ip;
        public ushort GetCoreID()                       => 0;

        public ulong ReadPc(byte vtId) => GetInstructionPointer();
        public void WritePc(byte vtId, ulong pc) => SetInstructionPointer(pc);

        public ulong  GetVL()                           => 0;
        public void   SetVL(ulong vl)                   { }
        public ulong  GetVLMAX()                        => 0;
        public byte   GetSEW()                          => 0;
        public void   SetSEW(byte s)                    { }
        public byte   GetLMUL()                         => 0;
        public void   SetLMUL(byte l)                   { }
        public bool   GetTailAgnostic()                 => false;
        public void   SetTailAgnostic(bool a)           { }
        public bool   GetMaskAgnostic()                 => false;
        public void   SetMaskAgnostic(bool a)           { }
        public uint   GetExceptionMask()                => 0;
        public void   SetExceptionMask(uint m)          { }
        public uint   GetExceptionPriority()            => 0;
        public void   SetExceptionPriority(uint p)      { }
        public byte   GetRoundingMode()                 => 0;
        public void   SetRoundingMode(byte m)           { }
        public ulong  GetOverflowCount()                => 0;
        public ulong  GetUnderflowCount()               => 0;
        public ulong  GetDivByZeroCount()               => 0;
        public ulong  GetInvalidOpCount()               => 0;
        public ulong  GetInexactCount()                 => 0;
        public void   ClearExceptionCounters()          { }
        public bool   GetVectorDirty()                  => false;
        public void   SetVectorDirty(bool d)            { }
        public bool   GetVectorEnabled()                => false;
        public void   SetVectorEnabled(bool e)          { }
        public ushort GetPredicateMask(ushort id)       => 0;
        public void   SetPredicateMask(ushort id, ushort m) { }
        public ulong  GetCycleCount()                   => 0;
        public ulong  GetInstructionsRetired()          => 0;
        public double GetIPC()                          => 0;

        public PipelineState GetCurrentPipelineState()                   => _pipelineState;
        public void SetCurrentPipelineState(PipelineState state)         => _pipelineState = state;
        public void TransitionPipelineState(PipelineTransitionTrigger t) =>
            _pipelineState = PipelineFsmGuard.Transition(_pipelineState, t);
    }

    /// <summary>
    /// Builds InstructionIR records for testing.
    /// </summary>
    internal static class P12Ir
    {
        public static InstructionIR Make(
            InstructionsEnum opcode,
            byte rs1 = 1, byte rs2 = 2, byte rd = 3, long imm = 0)
            => new InstructionIR
            {
                CanonicalOpcode   = opcode,
                Class             = InstructionClassifier.GetClass(opcode),
                SerializationClass= InstructionClassifier.GetSerializationClass(opcode),
                Rs1 = rs1, Rs2 = rs2, Rd = rd, Imm = imm,
            };
    }

    /// <summary>
    /// Capturing IV4TraceEventSink for test assertions.
    /// </summary>
    internal sealed class CapturingSink : IV4TraceEventSink
    {
        public List<V4TraceEvent> Events { get; } = new();
        public void RecordV4Event(V4TraceEvent evt) => Events.Add(evt);
    }

    internal sealed class CapturingEventQueue : IPipelineEventQueue
    {
        public List<PipelineEvent> Events { get; } = new();
        public void Enqueue(PipelineEvent evt) => Events.Add(evt);
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // ISA v4 Freeze Constants
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class IsaFreezeConstantsTests
    {
        [Fact]
        public void IsaVersion_Is4()
        {
            Assert.Equal(4, IsaV4Surface.IsaVersion);
        }

        [Fact]
        public void IsaMandatoryOpcodeCount_Is97()
        {
            Assert.Equal(97, IsaV4Surface.IsaMandatoryOpcodeCount);
        }

        [Fact]
        public void IsaMandatoryOpcodeCount_MatchesMandatoryCoreOpcodesSetSize()
        {
            Assert.Equal(IsaV4Surface.IsaMandatoryOpcodeCount, IsaV4Surface.MandatoryCoreOpcodes.Count);
        }

        [Fact]
        public void FrozenDate_Is2026()
        {
            Assert.Equal(2026, IsaV4Surface.FrozenDate.Year);
        }

        [Fact]
        public void FrozenDate_IsNotDefault()
        {
            Assert.NotEqual(default(DateOnly), IsaV4Surface.FrozenDate);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // ExecutionDispatcherV4 вЂ” backward compatibility (no trace sink)
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class DispatcherBackwardCompatTests
    {
        [Fact]
        public void Execute_WithoutTraceSink_DoesNotThrow()
        {
            // Dispatcher with no trace sink defaults to NullV4TraceEventSink
            var dispatcher = new ExecutionDispatcherV4();
            var state = new P12CpuState();
            state.SetReg(1, 10);
            state.SetReg(2, 3);

            var result = dispatcher.Execute(P12Ir.Make(InstructionsEnum.Addition), state);

            Assert.Equal(13UL, result.Value);
        }

        [Fact]
        public void Execute_WithoutBundleSerial_UsesZeroSerial()
        {
            var sink = new CapturingSink();
            var dispatcher = new ExecutionDispatcherV4(traceSink: sink);
            var state = new P12CpuState();
            state.SetReg(1, 5);
            state.SetReg(2, 5);

            dispatcher.Execute(P12Ir.Make(InstructionsEnum.Addition), state);

            Assert.Single(sink.Events);
            Assert.Equal(0UL, sink.Events[0].BundleSerial);
            Assert.Equal(0, sink.Events[0].VtId);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // ExecutionDispatcherV4 вЂ” trace event stamping
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class DispatcherTraceStampingTests
    {
        private readonly CapturingSink _sink = new();
        private readonly ExecutionDispatcherV4 _dispatcher;
        private readonly P12CpuState _state = new();

        public DispatcherTraceStampingTests()
        {
            _dispatcher = new ExecutionDispatcherV4(traceSink: _sink);
        }

        [Fact]
        public void Execute_WithBundleSerial_StampsEvent()
        {
            _state.SetReg(1, 1);
            _state.SetReg(2, 1);

            _dispatcher.Execute(P12Ir.Make(InstructionsEnum.Addition), _state, bundleSerial: 42, vtId: 1);

            Assert.Single(_sink.Events);
            Assert.Equal(42UL, _sink.Events[0].BundleSerial);
            Assert.Equal(1, _sink.Events[0].VtId);
        }

        [Fact]
        public void Execute_StampsPipelineState()
        {
            _state.SetCurrentPipelineState(PipelineState.Task);
            _state.SetReg(1, 1);
            _state.SetReg(2, 1);

            _dispatcher.Execute(P12Ir.Make(InstructionsEnum.Addition), _state, bundleSerial: 1, vtId: 0);

            Assert.Equal(PipelineState.Task, _sink.Events[0].FsmState);
        }

        [Fact]
        public void Execute_MultipleInstructions_EmitsOneEventEach()
        {
            _state.SetReg(1, 1); _state.SetReg(2, 1);

            _dispatcher.Execute(P12Ir.Make(InstructionsEnum.Addition), _state, 1, 0);
            _dispatcher.Execute(P12Ir.Make(InstructionsEnum.Subtraction), _state, 2, 0);
            _dispatcher.Execute(P12Ir.Make(InstructionsEnum.AND), _state, 3, 0);

            Assert.Equal(3, _sink.Events.Count);
            Assert.Equal(1UL, _sink.Events[0].BundleSerial);
            Assert.Equal(2UL, _sink.Events[1].BundleSerial);
            Assert.Equal(3UL, _sink.Events[2].BundleSerial);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // ExecutionDispatcherV4 вЂ” TraceEventKind classification per instruction class
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class DispatcherTraceKindClassificationTests
    {
        private static (CapturingSink sink, ExecutionDispatcherV4 dispatcher) Build(bool wireCsrFile = false)
        {
            var sink = new CapturingSink();
            return (sink, new ExecutionDispatcherV4(
                csrFile: wireCsrFile ? new CsrFile() : null,
                traceSink: sink,
                pipelineEventQueue: new CapturingEventQueue()));
        }

        [Theory]
        [InlineData(InstructionsEnum.Addition)]       // ADD
        [InlineData(InstructionsEnum.Subtraction)]    // SUB
        [InlineData(InstructionsEnum.Multiplication)] // MUL
        [InlineData(InstructionsEnum.MULH)]
        [InlineData(InstructionsEnum.MULHU)]
        [InlineData(InstructionsEnum.MULHSU)]
        [InlineData(InstructionsEnum.Division)]       // DIV
        [InlineData(InstructionsEnum.DIVU)]
        [InlineData(InstructionsEnum.Modulus)]        // REM
        [InlineData(InstructionsEnum.REMU)]
        [InlineData(InstructionsEnum.ADDI)]
        [InlineData(InstructionsEnum.LUI)]
        public void ScalarAlu_EmitsAluExecuted(InstructionsEnum opcode)
        {
            var (sink, dispatcher) = Build();
            var state = new P12CpuState();
            state.SetReg(1, 4); state.SetReg(2, 2);

            dispatcher.Execute(P12Ir.Make(opcode), state, 1, 0);

            Assert.Equal(TraceEventKind.AluExecuted, sink.Events[0].Kind);
        }

        [Theory]
        [InlineData(InstructionsEnum.LB)]
        [InlineData(InstructionsEnum.LBU)]
        [InlineData(InstructionsEnum.LH)]
        [InlineData(InstructionsEnum.LHU)]
        [InlineData(InstructionsEnum.LW)]
        [InlineData(InstructionsEnum.LWU)]
        [InlineData(InstructionsEnum.LD)]
        public void Memory_Load_EmitsLoadExecuted(InstructionsEnum opcode)
        {
            var (sink, dispatcher) = Build();
            var state = new P12CpuState();

            // No real memory bus вЂ” dispatcher will throw MemoryAccessException on actual access.
            // We expect LoadExecuted in the event kind; wrap the execution to survive.
            try { dispatcher.Execute(P12Ir.Make(opcode, rs1: 0, rs2: 0, rd: 3, imm: 0), state, 1, 0); }
            catch { /* memory access with no bus will throw; we still verify event kind */ }

            Assert.Single(sink.Events);
            Assert.Equal(TraceEventKind.LoadExecuted, sink.Events[0].Kind);
        }

        [Theory]
        [InlineData(InstructionsEnum.SB)]
        [InlineData(InstructionsEnum.SH)]
        [InlineData(InstructionsEnum.SW)]
        [InlineData(InstructionsEnum.SD)]
        public void Memory_Store_EmitsStoreExecuted(InstructionsEnum opcode)
        {
            var (sink, dispatcher) = Build();
            var state = new P12CpuState();

            try { dispatcher.Execute(P12Ir.Make(opcode), state, 1, 0); }
            catch { }

            Assert.Single(sink.Events);
            Assert.Equal(TraceEventKind.StoreExecuted, sink.Events[0].Kind);
        }

        [Theory]
        [InlineData(InstructionsEnum.JAL)]
        [InlineData(InstructionsEnum.JALR)]
        public void ControlFlow_Jump_EmitsJumpExecuted(InstructionsEnum opcode)
        {
            var (sink, dispatcher) = Build();
            var state = new P12CpuState();

            dispatcher.Execute(P12Ir.Make(opcode, rs1: 0, rs2: 0, rd: 0, imm: 0), state, 1, 0);

            Assert.Equal(TraceEventKind.JumpExecuted, sink.Events[0].Kind);
        }

        [Theory]
        [InlineData(InstructionsEnum.BEQ, true,  0, 0)]  // 0 == 0 в†’ BEQ taken  в†’ BranchTaken
        [InlineData(InstructionsEnum.BNE, false, 5, 5)]  // 5 == 5 в†’ BNE not taken в†’ BranchNotTaken
        public void ControlFlow_Branch_EmitsTakenOrNotTaken(
            InstructionsEnum opcode, bool expectTaken, ulong rs1Val, ulong rs2Val)
        {
            var (sink, dispatcher) = Build();
            var state = new P12CpuState();
            state.SetReg(1, rs1Val); state.SetReg(2, rs2Val);
            dispatcher.Execute(P12Ir.Make(opcode, rs1: 1, rs2: 2, imm: 4), state, 1, 0);

            var expected = expectTaken ? TraceEventKind.BranchTaken : TraceEventKind.BranchNotTaken;
            Assert.Equal(expected, sink.Events[0].Kind);
        }

        [Theory]
        [InlineData(InstructionsEnum.LR_W)]
        [InlineData(InstructionsEnum.LR_D)]
        [InlineData(InstructionsEnum.SC_W)]
        [InlineData(InstructionsEnum.SC_D)]
        [InlineData(InstructionsEnum.AMOSWAP_W)]
        [InlineData(InstructionsEnum.AMOADD_W)]
        [InlineData(InstructionsEnum.AMOXOR_W)]
        [InlineData(InstructionsEnum.AMOAND_W)]
        [InlineData(InstructionsEnum.AMOOR_W)]
        [InlineData(InstructionsEnum.AMOMIN_W)]
        [InlineData(InstructionsEnum.AMOMAX_W)]
        [InlineData(InstructionsEnum.AMOMINU_W)]
        [InlineData(InstructionsEnum.AMOMAXU_W)]
        [InlineData(InstructionsEnum.AMOSWAP_D)]
        [InlineData(InstructionsEnum.AMOADD_D)]
        [InlineData(InstructionsEnum.AMOXOR_D)]
        [InlineData(InstructionsEnum.AMOAND_D)]
        [InlineData(InstructionsEnum.AMOOR_D)]
        [InlineData(InstructionsEnum.AMOMIN_D)]
        [InlineData(InstructionsEnum.AMOMAX_D)]
        [InlineData(InstructionsEnum.AMOMINU_D)]
        [InlineData(InstructionsEnum.AMOMAXU_D)]
        public void Atomic_DirectSurface_IsRejectedBeforeTracePublication(InstructionsEnum opcode)
        {
            var (sink, dispatcher) = Build();
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode, rs1: 0, rs2: 1, rd: 3), state, 1, 0));

            Assert.Contains("Atomic opcode", ex.Message, StringComparison.Ordinal);
            Assert.Empty(sink.Events);
        }

        [Theory]
        [InlineData(InstructionsEnum.STREAM_SETUP, "stub success/trace")]
        [InlineData(InstructionsEnum.STREAM_START, "stub success/trace")]
        [InlineData(InstructionsEnum.STREAM_WAIT, "CaptureRetireWindowPublications")]
        public void StreamControl_DirectExecuteSurface_IsRejectedBeforeTracePublication(
            InstructionsEnum opcode,
            string expectedMessageToken)
        {
            var (sink, dispatcher) = Build();
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode, rs1: 0, rs2: 1, rd: 3), state, 1, 0));

            Assert.Contains(expectedMessageToken, ex.Message, StringComparison.Ordinal);
            Assert.Empty(sink.Events);
        }

        [Theory]
        [InlineData(InstructionsEnum.ECALL)]
        [InlineData(InstructionsEnum.EBREAK)]
        [InlineData(InstructionsEnum.MRET)]
        [InlineData(InstructionsEnum.SRET)]
        [InlineData(InstructionsEnum.WFI)]
        [InlineData(InstructionsEnum.FENCE)]
        [InlineData(InstructionsEnum.FENCE_I)]
        public void System_EagerExecuteRejectsWithoutTracePublication(InstructionsEnum opcode)
        {
            var (sink, dispatcher) = Build();
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode), state, 1, 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Empty(sink.Events);
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRW)]
        [InlineData(InstructionsEnum.CSRRWI)]
        public void Csr_Write_EmitsCsrWrite(InstructionsEnum opcode)
        {
            var (sink, dispatcher) = Build(wireCsrFile: true);
            var state = new P12CpuState();

            dispatcher.Execute(P12Ir.Make(opcode, rs1: 1, imm: CsrAddresses.Mstatus), state, 1, 0);

            Assert.Equal(TraceEventKind.CsrWrite, sink.Events[0].Kind);
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRS)]
        [InlineData(InstructionsEnum.CSRRC)]
        [InlineData(InstructionsEnum.CSRRSI)]
        [InlineData(InstructionsEnum.CSRRCI)]
        public void Csr_ReadSet_EmitsCsrRead(InstructionsEnum opcode)
        {
            var (sink, dispatcher) = Build(wireCsrFile: true);
            var state = new P12CpuState();

            dispatcher.Execute(P12Ir.Make(opcode, rs1: 1, imm: CsrAddresses.Mstatus), state, 1, 0);

            Assert.Equal(TraceEventKind.CsrRead, sink.Events[0].Kind);
        }

        [Fact]
        public void Csr_AddressEncodedInPayload()
        {
            var (sink, dispatcher) = Build(wireCsrFile: true);
            var state = new P12CpuState();
            const ushort csrAddr = CsrAddresses.Mscratch;

            dispatcher.Execute(P12Ir.Make(InstructionsEnum.CSRRW, rs1: 1, imm: csrAddr), state, 1, 0);

            Assert.Equal((ulong)csrAddr, sink.Events[0].Payload);
        }

        [Fact]
        public void Csr_Clear_EmitsCsrWriteWithZeroPayload()
        {
            var (sink, dispatcher) = Build(wireCsrFile: true);
            var state = new P12CpuState();

            dispatcher.Execute(P12Ir.Make(InstructionsEnum.CSR_CLEAR), state, 1, 0);

            Assert.Equal(TraceEventKind.CsrWrite, sink.Events[0].Kind);
            Assert.Equal(0UL, sink.Events[0].Payload);
        }

        [Fact]
        public void Csr_Write_ToMachinePowerState_UpdatesCanonicalCsrFile()
        {
            var sink = new CapturingSink();
            var csr = new CsrFile();
            var dispatcher = new ExecutionDispatcherV4(csrFile: csr, traceSink: sink);
            var state = new P12CpuState();
            state.SetReg(1, PowerControlCsr.EncodeState(CorePowerState.C1_Halt));

            dispatcher.Execute(P12Ir.Make(InstructionsEnum.CSRRW, rs1: 1, imm: CsrAddresses.MpowerState), state, 1, 0);

            Assert.Equal(
                PowerControlCsr.EncodeState(CorePowerState.C1_Halt),
                csr.Read(CsrAddresses.MpowerState, PrivilegeLevel.Machine));
            Assert.Equal(TraceEventKind.CsrWrite, sink.Events[0].Kind);
            Assert.Equal((ulong)CsrAddresses.MpowerState, sink.Events[0].Payload);
        }

        [Fact]
        public void Csr_Write_ToMachinePerformanceLevel_UpdatesCanonicalCsrFile()
        {
            var sink = new CapturingSink();
            var csr = new CsrFile();
            var dispatcher = new ExecutionDispatcherV4(csrFile: csr, traceSink: sink);
            var state = new P12CpuState();
            state.SetReg(1, PowerControlCsr.EncodeState(CorePowerState.P1_HighPerformance));

            dispatcher.Execute(P12Ir.Make(InstructionsEnum.CSRRW, rs1: 1, imm: CsrAddresses.MperfLevel), state, 1, 0);

            Assert.Equal(
                PowerControlCsr.EncodeState(CorePowerState.P1_HighPerformance),
                csr.Read(CsrAddresses.MperfLevel, PrivilegeLevel.Machine));
            Assert.Equal(TraceEventKind.CsrWrite, sink.Events[0].Kind);
            Assert.Equal((ulong)CsrAddresses.MperfLevel, sink.Events[0].Payload);
        }

        [Theory]
        [InlineData(InstructionsEnum.YIELD)]
        [InlineData(InstructionsEnum.WFE)]
        [InlineData(InstructionsEnum.SEV)]
        [InlineData(InstructionsEnum.POD_BARRIER)]
        [InlineData(InstructionsEnum.VT_BARRIER)]
        public void SmtVt_EagerExecuteRejectsWithoutTracePublication(InstructionsEnum opcode)
        {
            var (sink, dispatcher) = Build();
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode), state, 1, 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Empty(sink.Events);
        }

        [Theory]
        [InlineData(InstructionsEnum.VMXON)]
        [InlineData(InstructionsEnum.VMXOFF)]
        [InlineData(InstructionsEnum.VMLAUNCH)]
        [InlineData(InstructionsEnum.VMRESUME)]
        [InlineData(InstructionsEnum.VMREAD)]
        [InlineData(InstructionsEnum.VMWRITE)]
        [InlineData(InstructionsEnum.VMCLEAR)]
        [InlineData(InstructionsEnum.VMPTRLD)]
        public void Vmx_EagerExecuteRejectsWithoutTracePublication(InstructionsEnum opcode)
        {
            var sink = new CapturingSink();
            var csr = new CsrFile();
            var vmcs = new VmcsManager();
            var dispatcher = new ExecutionDispatcherV4(
                vmxUnit: new VmxExecutionUnit(csr, vmcs),
                traceSink: sink);
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode), state, 1, 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Empty(sink.Events);
        }

        [Theory]
        [InlineData(InstructionsEnum.VMLAUNCH)]
        [InlineData(InstructionsEnum.VMRESUME)]
        public void Vmx_LaunchResume_WithoutWiredUnit_Throws(InstructionsEnum opcode)
        {
            var (_, dispatcher) = Build();
            var state = new P12CpuState();
            state.SetCurrentPipelineState(PipelineState.Task);

            Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode), state, 1, 0));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // ExecutionDispatcherV4 вЂ” TelemetryCounters integration
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class DispatcherTelemetryIntegrationTests
    {
        [Fact]
        public void Execute_WithTelemetry_IncrementsAluInstrCount()
        {
            var telemetry  = new TelemetryCounters();
            var dispatcher = new ExecutionDispatcherV4(telemetry: telemetry);
            var state = new P12CpuState();
            state.SetReg(1, 3); state.SetReg(2, 4);

            dispatcher.Execute(P12Ir.Make(InstructionsEnum.Addition), state, 1, vtId: 0);

            Assert.Equal(1UL, telemetry.GetInstrCountForVt(0));
        }

        [Fact]
        public void Execute_WithTelemetry_IncrementsBundleRetiredOnBundleRetiredEvent()
        {
            // TelemetryCounters.ApplyTraceEvent increments BundleRetiredCount for BundleRetired events.
            // The dispatcher itself doesn't emit BundleRetired (that's a pipeline concern),
            // but we can verify the ApplyTraceEvent dispatch path works when a BundleRetired
            // event is manually fed through the pipeline.
            var telemetry = new TelemetryCounters();
            var evt = V4TraceEvent.Create(1, 0, PipelineState.Task, TraceEventKind.BundleRetired, 0);
            telemetry.ApplyTraceEvent(evt);

            Assert.Equal(1UL, telemetry.BundleRetiredCount);
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOADD_D)]
        [InlineData(InstructionsEnum.LR_W)]
        public void Execute_AtomicSurfaceRejected_DoesNotIncrementTelemetry(InstructionsEnum opcode)
        {
            var telemetry  = new TelemetryCounters();
            var dispatcher = new ExecutionDispatcherV4(telemetry: telemetry);
            var state = new P12CpuState();

            Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode, rs1: 0, rs2: 1, rd: 3), state, 1, vtId: 0));

            Assert.Equal(0UL, telemetry.GetInstrCountForVt(0));
            Assert.Equal(0UL, telemetry.GetAmoDwordCount(0));
            Assert.Equal(0UL, telemetry.GetLrCount(0));
        }

        [Fact]
        public void Execute_RejectedAtomicSurface_LeavesLrTelemetryAtZero()
        {
            var telemetry  = new TelemetryCounters();
            var dispatcher = new ExecutionDispatcherV4(telemetry: telemetry);
            var state = new P12CpuState();

            Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(InstructionsEnum.LR_W, rs1: 0, rd: 3), state, 1, vtId: 0));
            Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(InstructionsEnum.LR_D, rs1: 0, rd: 3), state, 2, vtId: 0));

            Assert.Equal(0UL, telemetry.GetLrCount(0));
        }

        [Theory]
        [InlineData(InstructionsEnum.STREAM_SETUP)]
        [InlineData(InstructionsEnum.STREAM_WAIT)]
        public void Execute_RejectedStreamControlDirectSurface_LeavesTelemetryAtZero(InstructionsEnum opcode)
        {
            var telemetry = new TelemetryCounters();
            var dispatcher = new ExecutionDispatcherV4(telemetry: telemetry);
            var state = new P12CpuState();

            Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode, rs1: 0, rs2: 1, rd: 3), state, 1, vtId: 0));

            Assert.Equal(0UL, telemetry.GetInstrCountForVt(0));
        }

        [Theory]
        [InlineData(InstructionsEnum.ECALL)]
        [InlineData(InstructionsEnum.EBREAK)]
        [InlineData(InstructionsEnum.MRET)]
        [InlineData(InstructionsEnum.SRET)]
        [InlineData(InstructionsEnum.WFI)]
        [InlineData(InstructionsEnum.FENCE)]
        [InlineData(InstructionsEnum.FENCE_I)]
        public void Execute_SystemEagerSurfaceRejectedWithQueue_DoesNotPublishTraceOrTelemetry(
            InstructionsEnum opcode)
        {
            var sink = new CapturingSink();
            var telemetry = new TelemetryCounters();
            var queue = new CapturingEventQueue();
            var dispatcher = new ExecutionDispatcherV4(
                traceSink: sink,
                telemetry: telemetry,
                pipelineEventQueue: queue);
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode, rs1: 0, rs2: 1, rd: 3), state, 1, vtId: 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.Empty(sink.Events);
            Assert.Equal(0UL, telemetry.GetInstrCountForVt(0));
        }

        [Theory]
        [InlineData(InstructionsEnum.YIELD)]
        [InlineData(InstructionsEnum.WFE)]
        [InlineData(InstructionsEnum.SEV)]
        [InlineData(InstructionsEnum.POD_BARRIER)]
        [InlineData(InstructionsEnum.VT_BARRIER)]
        public void Execute_SmtVtEagerSurfaceRejectedWithQueue_DoesNotPublishTraceOrTelemetry(
            InstructionsEnum opcode)
        {
            var sink = new CapturingSink();
            var telemetry = new TelemetryCounters();
            var queue = new CapturingEventQueue();
            var dispatcher = new ExecutionDispatcherV4(
                traceSink: sink,
                telemetry: telemetry,
                pipelineEventQueue: queue);
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode, rs1: 0, rs2: 1, rd: 3), state, 1, vtId: 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.Empty(sink.Events);
            Assert.Equal(0UL, telemetry.GetInstrCountForVt(0));
        }

        [Theory]
        [InlineData(InstructionsEnum.VMXON)]
        [InlineData(InstructionsEnum.VMXOFF)]
        [InlineData(InstructionsEnum.VMLAUNCH)]
        [InlineData(InstructionsEnum.VMRESUME)]
        [InlineData(InstructionsEnum.VMREAD)]
        [InlineData(InstructionsEnum.VMWRITE)]
        [InlineData(InstructionsEnum.VMCLEAR)]
        [InlineData(InstructionsEnum.VMPTRLD)]
        public void Execute_VmxEagerSurfaceRejected_DoesNotPublishTraceOrTelemetry(
            InstructionsEnum opcode)
        {
            var sink = new CapturingSink();
            var telemetry = new TelemetryCounters();
            var csr = new CsrFile();
            var vmcs = new VmcsManager();
            var dispatcher = new ExecutionDispatcherV4(
                vmxUnit: new VmxExecutionUnit(csr, vmcs),
                traceSink: sink,
                telemetry: telemetry);
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode), state, 1, vtId: 0));

            Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
            Assert.Empty(sink.Events);
            Assert.Equal(0UL, telemetry.GetInstrCountForVt(0));
        }

        [Theory]
        [InlineData(InstructionsEnum.VGATHER)]
        [InlineData(InstructionsEnum.VSCATTER)]
        [InlineData(InstructionsEnum.MTILE_LOAD)]
        [InlineData(InstructionsEnum.MTILE_STORE)]
        public void Execute_NonScalarMemoryEagerSurfaceRejected_DoesNotPublishTraceOrTelemetry(
            InstructionsEnum opcode)
        {
            var sink = new CapturingSink();
            var telemetry = new TelemetryCounters();
            var dispatcher = new ExecutionDispatcherV4(
                traceSink: sink,
                telemetry: telemetry);
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode, rs1: 0, rs2: 1, rd: 3), state, 1, vtId: 0));

            Assert.Contains("pipeline execution remains the supported path", ex.Message, StringComparison.Ordinal);
            Assert.Empty(sink.Events);
            Assert.Equal(0UL, telemetry.GetInstrCountForVt(0));
        }

        [Fact]
        public void Execute_WithTelemetry_SinkAndTelemetryBothReceiveEvent()
        {
            var sink       = new CapturingSink();
            var telemetry  = new TelemetryCounters();
            var dispatcher = new ExecutionDispatcherV4(traceSink: sink, telemetry: telemetry);
            var state = new P12CpuState();
            state.SetReg(1, 1); state.SetReg(2, 2);

            dispatcher.Execute(P12Ir.Make(InstructionsEnum.Addition), state, 7, vtId: 2);

            Assert.Single(sink.Events);
            Assert.Equal(7UL, sink.Events[0].BundleSerial);
            Assert.Equal(1UL, telemetry.GetInstrCountForVt(2));
        }

        [Fact]
        public void Execute_WithoutTelemetry_DoesNotThrow()
        {
            var dispatcher = new ExecutionDispatcherV4();
            var state = new P12CpuState();
            state.SetReg(1, 10); state.SetReg(2, 5);

            // Should not throw вЂ” telemetry is optional
            var result = dispatcher.Execute(P12Ir.Make(InstructionsEnum.Addition), state);
            Assert.Equal(15UL, result.Value);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // ISA v4 Instruction Coverage - all mandatory core opcodes
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class IsaV4InstructionCoverageTests
    {
        [Fact]
        public void MandatoryCoreOpcodes_ContainsAllScalarAluOpcodes()
        {
            // IsaV4Surface uses mnemonic strings; the InstructionsEnum uses legacy names
            // for legacy opcodes (Addition, Subtraction, ...) and v4 names for new ones.
            var expected = new[]
            {
                "ADD", "SUB", "AND", "OR", "XOR", "SLL", "SRL", "SRA", "SLT", "SLTU",
                "MUL", "MULH", "MULHU", "MULHSU", "DIV", "DIVU", "REM", "REMU",
                "ADDI", "ANDI", "ORI", "XORI", "SLLI", "SRLI", "SRAI", "SLTI", "SLTIU",
                "LUI", "AUIPC",
            };
            foreach (var op in expected)
                Assert.Contains(op, IsaV4Surface.MandatoryCoreOpcodes);
        }

        [Fact]
        public void MandatoryCoreOpcodes_ContainsAllMemoryOpcodes()
        {
            var expected = new[]
            {
                "LB", "LBU", "LH", "LHU", "LW", "LWU", "LD",
                "SB", "SH", "SW", "SD",
            };
            foreach (var op in expected)
                Assert.Contains(op, IsaV4Surface.MandatoryCoreOpcodes);
        }

        [Fact]
        public void MandatoryCoreOpcodes_ContainsAllControlFlowOpcodes()
        {
            var expected = new[]
            {
                "JAL", "JALR", "BEQ", "BNE", "BLT", "BGE", "BLTU", "BGEU",
            };
            foreach (var op in expected)
                Assert.Contains(op, IsaV4Surface.MandatoryCoreOpcodes);
        }

        [Fact]
        public void MandatoryCoreOpcodes_ContainsFullAtomicPlane()
        {
            var expected = new[]
            {
                "LR_W", "SC_W", "LR_D", "SC_D",
                "AMOSWAP_W", "AMOADD_W", "AMOXOR_W", "AMOAND_W", "AMOOR_W",
                "AMOMIN_W",  "AMOMAX_W",  "AMOMINU_W",  "AMOMAXU_W",
                "AMOSWAP_D", "AMOADD_D", "AMOXOR_D", "AMOAND_D", "AMOOR_D",
                "AMOMIN_D",  "AMOMAX_D",  "AMOMINU_D",  "AMOMAXU_D",
            };
            foreach (var op in expected)
                Assert.Contains(op, IsaV4Surface.MandatoryCoreOpcodes);
        }

        [Fact]
        public void MandatoryCoreOpcodes_ContainsAllSystemOpcodes()
        {
            var expected = new[]
            {
                "FENCE", "FENCE_I", "ECALL", "EBREAK", "MRET", "SRET", "WFI",
            };
            foreach (var op in expected)
                Assert.Contains(op, IsaV4Surface.MandatoryCoreOpcodes);
        }

        [Fact]
        public void MandatoryCoreOpcodes_ContainsAllCsrOpcodes()
        {
            var expected = new[] { "CSRRW", "CSRRS", "CSRRC", "CSRRWI", "CSRRSI", "CSRRCI" };
            foreach (var op in expected)
                Assert.Contains(op, IsaV4Surface.MandatoryCoreOpcodes);
        }

        [Fact]
        public void MandatoryCoreOpcodes_ContainsAllSmtVtOpcodes()
        {
            var expected = new[] { "YIELD", "WFE", "SEV", "POD_BARRIER", "VT_BARRIER" };
            foreach (var op in expected)
                Assert.Contains(op, IsaV4Surface.MandatoryCoreOpcodes);
        }

        [Fact]
        public void MandatoryCoreOpcodes_ContainsAllVmxOpcodes()
        {
            var expected = new[]
            {
                "VMXON", "VMXOFF", "VMLAUNCH", "VMRESUME",
                "VMREAD", "VMWRITE", "VMCLEAR", "VMPTRLD",
            };
            foreach (var op in expected)
                Assert.Contains(op, IsaV4Surface.MandatoryCoreOpcodes);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Prohibited opcode rejection
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class ProhibitedOpcodeRejectionTests
    {
        private static ExecutionDispatcherV4 Dispatcher() => new();

        [Fact]
        public void ProhibitedOpcodes_AreNotInMandatoryCore()
        {
            foreach (var op in IsaV4Surface.ProhibitedOpcodes)
                Assert.DoesNotContain(op, IsaV4Surface.MandatoryCoreOpcodes);
        }

        [Theory]
        [InlineData("RDVTID")]
        [InlineData("RDVTMASK")]
        [InlineData("FSP_FENCE")]
        [InlineData("CSR_READ")]
        [InlineData("CSR_WRITE")]
        [InlineData("HINT_LIKELY")]
        [InlineData("HINT_UNLIKELY")]
        [InlineData("HINT_HOT")]
        [InlineData("HINT_COLD")]
        [InlineData("NOP")]
        [InlineData("LI")]
        [InlineData("MV")]
        [InlineData("CALL")]
        [InlineData("RET")]
        [InlineData("JMP")]
        public void ProhibitedOpcodes_AreInProhibitedSet(string opcode)
        {
            Assert.Contains(opcode, IsaV4Surface.ProhibitedOpcodes);
        }
    }
}

