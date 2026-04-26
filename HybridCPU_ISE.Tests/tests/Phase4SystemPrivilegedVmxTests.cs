// V5 Phase 4: System/Privileged Layer Rewrite + VMX FSM Groundwork
//
// Covers:
//   [T4-01] ECALL: generates EcallEvent в†’ FSM handles trap в†’ mepc/mcause updated в†’ PC redirected
//   [T4-02] EBREAK: generates EbreakEvent в†’ FSM handles debug trap в†’ mepc/mcause(3) в†’ PC redirected
//   [T4-03] MRET: generates MretEvent в†’ privilege restored в†’ mepc restored to PC
//   [T4-04] SRET: generates SretEvent в†’ privilege restored в†’ sepc restored to PC
//   [T4-05] FENCE: memory queue drained for affected VT before continuing (FSM state unchanged)
//   [T4-06] FENCE.I: instruction cache flush event в†’ FSM state unchanged
//   [T4-07] WFI: VT enters Halted FSM state в†’ resumes on interrupt
//   [T4-08] VMXON: explicit VMX executor enables VMX CSR state; requires M-mode
//   [T4-09] VMLAUNCH: FSM transitions Taskв†’VmEntryв†’GuestExecution; guest state loaded
//   [T4-10] VM_EXIT: typed VMX retire saves guest state and restores host context
//   [T4-11] VMREAD/VMWRITE: dispatched as InternalOpKind.VmRead/VmWrite, not as privileged events
//   [T4-12] FspAdmissionPolicy: NoSteal slot not pilfered
//   [T4-13] FspAdmissionPolicy: FspBoundary=true prevents cross-boundary pilfering
//   [T4-14] FspAdmissionPolicy: method signature uses only BundleMetadata/SlotMetadata (no MicroOp.CanBeStolen)
//   [T4-15] Regression: WFE в†’ VT suspended; SEV в†’ all VTs woken

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Pipeline.Scheduling;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.V5Phase4
{
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Test helpers
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Minimal ICanonicalCpuState stub for Phase 4 tests.
    /// Tracks a mutable instruction pointer; all other members are no-ops.
    /// </summary>
    internal sealed class P4CpuState : ICanonicalCpuState
    {
        private readonly Dictionary<ushort, ulong> _intRegs = new();
        private readonly Dictionary<byte, ulong> _vtPcs = new();
        private ulong _pc;
        private PipelineState _state = PipelineState.Task;

        public P4CpuState(ulong initialPc = 0) => _pc = initialPc;

        public byte? LastReadPcVtId { get; private set; }

        public void SetVtPc(byte vtId, ulong pc) => _vtPcs[vtId] = pc;
        public void SetReg(ushort regId, ulong value)
        {
            if (regId != 0)
                _intRegs[regId] = value;
        }

        public ulong GetInstructionPointer() => _pc;
        public void  SetInstructionPointer(ulong ip) => _pc = ip;
        public ulong ReadPc(byte vtId)
        {
            LastReadPcVtId = vtId;
            return _vtPcs.TryGetValue(vtId, out var pc) ? pc : _pc;
        }

        public void WritePc(byte vtId, ulong pc)
        {
            _vtPcs[vtId] = pc;
            _pc = pc;
        }

        public PipelineState GetCurrentPipelineState() => _state;
        public void SetCurrentPipelineState(PipelineState s) => _state = s;
        public void TransitionPipelineState(PipelineTransitionTrigger trigger)
            => _state = PipelineFsmGuard.Transition(_state, trigger);

        // в”Ђв”Ђ Unused ICanonicalCpuState members в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        public ulong GetVL() => 0;
        public void  SetVL(ulong vl) { }
        public ulong GetVLMAX() => 0;
        public byte  GetSEW() => 0;
        public void  SetSEW(byte sew) { }
        public byte  GetLMUL() => 0;
        public void  SetLMUL(byte lmul) { }
        public bool  GetTailAgnostic() => false;
        public void  SetTailAgnostic(bool agnostic) { }
        public bool  GetMaskAgnostic() => false;
        public void  SetMaskAgnostic(bool agnostic) { }
        public uint  GetExceptionMask() => 0;
        public void  SetExceptionMask(uint mask) { }
        public uint  GetExceptionPriority() => 0;
        public void  SetExceptionPriority(uint priority) { }
        public byte  GetRoundingMode() => 0;
        public void  SetRoundingMode(byte mode) { }
        public ulong GetOverflowCount() => 0;
        public ulong GetUnderflowCount() => 0;
        public ulong GetDivByZeroCount() => 0;
        public ulong GetInvalidOpCount() => 0;
        public ulong GetInexactCount() => 0;
        public void  ClearExceptionCounters() { }
        public bool  GetVectorDirty() => false;
        public void  SetVectorDirty(bool dirty) { }
        public bool  GetVectorEnabled() => false;
        public void  SetVectorEnabled(bool enabled) { }
        public ulong ReadIntRegister(ushort regID) => _intRegs.TryGetValue(regID, out var value) ? value : 0;
        public void  WriteIntRegister(ushort regID, ulong value)
        {
            if (regID != 0)
                _intRegs[regID] = value;
        }

        public long ReadRegister(byte vtId, int regId) =>
            unchecked((long)ReadIntRegister((ushort)regId));

        public void WriteRegister(byte vtId, int regId, ulong value) =>
            WriteIntRegister((ushort)regId, value);
        public ushort GetPredicateMask(ushort maskID) => 0;
        public void  SetPredicateMask(ushort maskID, ushort mask) { }
        public ushort GetCoreID() => 0;
        public ulong GetCycleCount() => 0;
        public ulong GetInstructionsRetired() => 0;
        public double GetIPC() => 0;
    }

    /// <summary>
    /// IPipelineEventQueue that captures enqueued events for assertion.
    /// </summary>
    internal sealed class CapturingEventQueue : IPipelineEventQueue
    {
        private readonly List<PipelineEvent> _captured = new();
        public IReadOnlyList<PipelineEvent> Captured => _captured;
        public void Enqueue(PipelineEvent evt) => _captured.Add(evt);
    }

    internal sealed class P4RecordingVmxEventSink : IVmxEventSink
    {
        public List<(VmxEventKind Kind, ushort CoreId, VmExitReason Reason)> Events { get; } = new();

        public void RecordVmxEvent(VmxEventKind kind, ushort vtId, VmExitReason exitReason = VmExitReason.None)
        {
            Events.Add((kind, vtId, exitReason));
        }
    }

    internal static class P4VmxIrHelper
    {
        public static InstructionIR MakeVmx(
            InstructionsEnum opcode,
            byte rd = 0,
            byte rs1 = 0,
            byte rs2 = 0,
            long imm = 0)
        {
            return new InstructionIR
            {
                CanonicalOpcode = opcode,
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = rd,
                Rs1 = rs1,
                Rs2 = rs2,
                Imm = imm,
            };
        }
    }

    /// <summary>
    /// IVmcsManager stub: always loads successfully, records SaveGuestState /
    /// RestoreHostState calls for assertion.
    /// </summary>
    internal sealed class TrackingVmcsManager : IVmcsManager
    {
        public bool HasActiveVmcs { get; private set; }
        public bool HasLaunchedVmcs { get; private set; }
        public int  SaveGuestStateCallCount   { get; private set; }
        public int  RestoreHostStateCallCount { get; private set; }
        public ulong LastLoadedAddress { get; private set; }
        public byte? LastSaveGuestStateVtId { get; private set; }
        public byte? LastRestoreHostStateVtId { get; private set; }

        public VmcsPointerResult LoadPointer(ulong vmcsPhysicalAddress)
        {
            Load(vmcsPhysicalAddress);
            return new VmcsPointerResult(vmcsPhysicalAddress, HasActiveVmcs, HasLaunchedVmcs);
        }

        public VmcsPointerResult ClearPointer(ulong vmcsPhysicalAddress)
        {
            Clear(vmcsPhysicalAddress);
            return new VmcsPointerResult(vmcsPhysicalAddress, HasActiveVmcs, HasLaunchedVmcs);
        }

        public VmcsFieldReadResult ReadFieldValue(VmcsField field) =>
            new(field, 0, HasActiveVmcs);

        public VmcsFieldWriteResult WriteFieldValue(VmcsField field, long value) =>
            new(field, value, HasActiveVmcs);

        public VmEntryTransitionResult BeginVmEntry(bool markLaunchedOnSuccess)
        {
            if (markLaunchedOnSuccess)
                HasLaunchedVmcs = true;

            return new VmEntryTransitionResult(
                Success: true,
                FailureReason: VmExitReason.None,
                GuestPc: null,
                GuestSp: null,
                HasActiveVmcs: HasActiveVmcs,
                HasLaunchedVmcs: HasLaunchedVmcs);
        }

        public VmExitTransitionResult CompleteVmExit(ICanonicalCpuState state, byte vtId, VmExitReason reason)
        {
            LastSaveGuestStateVtId = vtId;
            LastRestoreHostStateVtId = vtId;
            SaveGuestStateCallCount++;
            RestoreHostStateCallCount++;
            return new VmExitTransitionResult(
                Success: true,
                ExitReason: reason,
                SavedGuestPc: 0,
                SavedGuestSp: 0,
                HostPc: null,
                HostSp: null,
                HasActiveVmcs: HasActiveVmcs,
                HasLaunchedVmcs: HasLaunchedVmcs);
        }

        public void Load(ulong vmcsPhysicalAddress)
        {
            HasActiveVmcs     = true;
            LastLoadedAddress = vmcsPhysicalAddress;
        }

        public void Clear(ulong vmcsPhysicalAddress)
        {
            HasActiveVmcs = false;
        }

        public long ReadField(VmcsField field) => 0;
        public void WriteField(VmcsField field, long value) { }

        public void SaveGuestState(ICanonicalCpuState state)  => SaveGuestStateCallCount++;
        public void SaveGuestState(ICanonicalCpuState state, byte vtId)
        {
            LastSaveGuestStateVtId = vtId;
            SaveGuestStateCallCount++;
        }
        public void RestoreGuestState(ICanonicalCpuState state) { }
        public void SaveHostState(ICanonicalCpuState state)    { }
        public void RestoreHostState(ICanonicalCpuState state) => RestoreHostStateCallCount++;
        public void RestoreHostState(ICanonicalCpuState state, byte vtId)
        {
            LastRestoreHostStateVtId = vtId;
            RestoreHostStateCallCount++;
        }
        public void MarkLaunched() => HasLaunchedVmcs = true;
    }

    /// <summary>
    /// IVmcsManager stub that always throws on Load (simulates entry failure).
    /// </summary>
    internal sealed class FailingVmcsManager : IVmcsManager
    {
        public bool HasActiveVmcs  => false;
        public bool HasLaunchedVmcs => false;
        public VmcsPointerResult LoadPointer(ulong addr)
        {
            Load(addr);
            return new VmcsPointerResult(addr, HasActiveVmcs, HasLaunchedVmcs);
        }
        public VmcsPointerResult ClearPointer(ulong addr) => new(addr, HasActiveVmcs, HasLaunchedVmcs);
        public VmcsFieldReadResult ReadFieldValue(VmcsField field) => new(field, 0, HasActiveVmcs);
        public VmcsFieldWriteResult WriteFieldValue(VmcsField field, long value) => new(field, value, HasActiveVmcs);
        public VmEntryTransitionResult BeginVmEntry(bool markLaunchedOnSuccess) =>
            new(false, VmExitReason.InvalidGuestState, null, null, HasActiveVmcs, HasLaunchedVmcs);
        public VmExitTransitionResult CompleteVmExit(ICanonicalCpuState state, byte vtId, VmExitReason reason) =>
            new(false, reason, 0, 0, null, null, HasActiveVmcs, HasLaunchedVmcs);
        public void Load(ulong addr)  => throw new InvalidOperationException("VMCS load failed");
        public void Clear(ulong addr) { }
        public long ReadField(VmcsField field) => 0;
        public void WriteField(VmcsField field, long value) { }
        public void SaveGuestState(ICanonicalCpuState state)  { }
        public void SaveGuestState(ICanonicalCpuState state, byte vtId) { }
        public void RestoreGuestState(ICanonicalCpuState state) { }
        public void SaveHostState(ICanonicalCpuState state)    { }
        public void RestoreHostState(ICanonicalCpuState state) { }
        public void RestoreHostState(ICanonicalCpuState state, byte vtId) { }
        public void MarkLaunched() { }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-01 вЂ” ECALL
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class EcallHandlerTests
    {
        [Fact]
        public void T4_01_Ecall_MachineMode_UpdatesMepcMcauseAndRedirectsPC()
        {
            // Arrange
            var csr      = new CsrFile();
            var handler  = new PipelineFsmEventHandler(csr);
            var cpuState = new P4CpuState(initialPc: 0x200);

            ulong trapHandlerAddr = 0x1000;
            csr.Write(CsrAddresses.Mtvec, trapHandlerAddr, PrivilegeLevel.Machine);

            var evt = new EcallEvent
            {
                VtId = 0, BundleSerial = 1, EcallCode = 93 /* exit syscall */
            };

            // Act
            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.Machine);

            // Assert
            Assert.Equal(PipelineState.Task, next);                // no FSM state change
            Assert.Equal(0x200UL, csr.DirectRead(CsrAddresses.Mepc));   // mepc = faulting PC
            Assert.Equal(11UL,    csr.DirectRead(CsrAddresses.Mcause));  // M-mode ecall cause
            Assert.Equal(trapHandlerAddr, cpuState.GetInstructionPointer()); // PC в†’ mtvec
        }

        [Fact]
        public void T4_01c_Ecall_UsesVtScopedPcRead()
        {
            var csr      = new CsrFile();
            var handler  = new PipelineFsmEventHandler(csr);
            var cpuState = new P4CpuState(initialPc: 0x111);
            cpuState.SetVtPc(2, 0x222);

            csr.Write(CsrAddresses.Mtvec, 0x1000UL, PrivilegeLevel.Machine);

            handler.Handle(
                new EcallEvent { VtId = 2, BundleSerial = 99, EcallCode = 0 },
                PipelineState.Task,
                cpuState,
                PrivilegeLevel.Machine);

            Assert.Equal((byte)2, cpuState.LastReadPcVtId);
            Assert.Equal(0x222UL, csr.DirectRead(CsrAddresses.Mepc));
        }

        [Theory]
        [InlineData(PrivilegeLevel.Supervisor, 9UL)]   // S-mode ecall cause = 9
        [InlineData(PrivilegeLevel.User,       8UL)]   // U-mode ecall cause = 8
        public void T4_01b_Ecall_PrivilegeCauseCodeVariants(PrivilegeLevel priv, ulong expectedCause)
        {
            var csr     = new CsrFile();
            var handler = new PipelineFsmEventHandler(csr);
            csr.Write(CsrAddresses.Mtvec, 0x4000UL, PrivilegeLevel.Machine);

            var evt = new EcallEvent { VtId = 0, BundleSerial = 1, EcallCode = 1 };
            handler.Handle(evt, PipelineState.Task, new P4CpuState(), priv);

            Assert.Equal(expectedCause, csr.DirectRead(CsrAddresses.Mcause));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-02 вЂ” EBREAK
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class EbreakHandlerTests
    {
        [Fact]
        public void T4_02_Ebreak_WritesMepcMcause3AndRedirectsPC()
        {
            var csr      = new CsrFile();
            var handler  = new PipelineFsmEventHandler(csr);
            var cpuState = new P4CpuState(initialPc: 0x300);

            csr.Write(CsrAddresses.Mtvec, 0x2000UL, PrivilegeLevel.Machine);

            var evt = new EbreakEvent { VtId = 0, BundleSerial = 2 };

            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.Machine);

            Assert.Equal(PipelineState.Task, next);
            Assert.Equal(0x300UL, csr.DirectRead(CsrAddresses.Mepc));
            Assert.Equal(3UL,     csr.DirectRead(CsrAddresses.Mcause));     // breakpoint
            Assert.Equal(0x2000UL, cpuState.GetInstructionPointer());
        }

        [Fact]
        public void T4_02b_TrapEntry_WritesMepcMcauseMtvalAndRedirectsPC()
        {
            var csr = new CsrFile();
            var handler = new PipelineFsmEventHandler(csr);
            var cpuState = new P4CpuState(initialPc: 0x360);

            csr.Write(CsrAddresses.Mtvec, 0x2400UL, PrivilegeLevel.Machine);

            var evt = new TrapEntryEvent
            {
                VtId = 0,
                BundleSerial = 3,
                CauseCode = 2,
                FaultAddress = 0xDEAD
            };

            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.Machine);

            Assert.Equal(PipelineState.Task, next);
            Assert.Equal(0x360UL, csr.DirectRead(CsrAddresses.Mepc));
            Assert.Equal(2UL, csr.DirectRead(CsrAddresses.Mcause));
            Assert.Equal(0xDEADUL, csr.DirectRead(CsrAddresses.Mtval));
            Assert.Equal(0x2400UL, cpuState.GetInstructionPointer());
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-03 вЂ” MRET
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class MretHandlerTests
    {
        [Fact]
        public void T4_03_Mret_RestoresMepcToPC_FsmStateUnchanged()
        {
            var csr = new CsrFile();
            var handler = new PipelineFsmEventHandler(csr);

            // Simulate a previous trap that saved the return address into mepc.
            ulong returnAddr = 0x400;
            csr.Write(CsrAddresses.Mepc, returnAddr, PrivilegeLevel.Machine);

            var cpuState = new P4CpuState(initialPc: 0x1000); // inside trap handler
            var evt      = new MretEvent { VtId = 0, BundleSerial = 3 };

            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.Machine);

            Assert.Equal(PipelineState.Task, next);
            Assert.Equal(returnAddr, cpuState.GetInstructionPointer());
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-04 вЂ” SRET
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class SretHandlerTests
    {
        [Fact]
        public void T4_04_Sret_RestoresSepcToPC_FsmStateUnchanged()
        {
            var csr     = new CsrFile();
            var handler = new PipelineFsmEventHandler(csr);

            ulong returnAddr = 0x500;
            csr.Write(CsrAddresses.Sepc, returnAddr, PrivilegeLevel.Supervisor);

            var cpuState = new P4CpuState(initialPc: 0x2000);
            var evt      = new SretEvent { VtId = 1, BundleSerial = 4 };

            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.Supervisor);

            Assert.Equal(PipelineState.Task, next);
            Assert.Equal(returnAddr, cpuState.GetInstructionPointer());
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-05 вЂ” FENCE (data memory ordering)
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class FenceHandlerTests
    {
        [Fact]
        public void T4_05_Fence_DataFence_FsmStateUnchanged()
        {
            var handler  = new PipelineFsmEventHandler(new CsrFile());
            var cpuState = new P4CpuState();

            var evt = new FenceEvent { VtId = 0, BundleSerial = 5, IsInstructionFence = false };
            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.User);

            Assert.Equal(PipelineState.Task, next);
        }

        // в”Ђв”Ђ T4-06 в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T4_06_FenceI_InstructionCacheFence_FsmStateUnchanged()
        {
            var handler  = new PipelineFsmEventHandler(new CsrFile());
            var cpuState = new P4CpuState();

            var evt = new FenceEvent { VtId = 0, BundleSerial = 6, IsInstructionFence = true };
            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.User);

            Assert.Equal(PipelineState.Task, next);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-07 вЂ” WFI (Wait-For-Interrupt)
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class WfiHandlerTests
    {
        [Fact]
        public void T4_07_Wfi_TransitionsTaskToHalted()
        {
            var handler  = new PipelineFsmEventHandler(new CsrFile());
            var cpuState = new P4CpuState();

            var evt  = new WfiEvent { VtId = 0, BundleSerial = 7 };
            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.Machine);

            Assert.Equal(PipelineState.Halted, next);
        }

        [Fact]
        public void T4_07b_Wfi_WhenNotInTask_Throws_IllegalFsmTransition()
        {
            var handler  = new PipelineFsmEventHandler(new CsrFile());
            var cpuState = new P4CpuState();

            var evt = new WfiEvent { VtId = 0, BundleSerial = 8 };

            // Issuing WFI from GuestExecution is illegal in the FSM table.
            Assert.Throws<IllegalFsmTransitionException>(
                () => handler.Handle(evt, PipelineState.GuestExecution, cpuState, PrivilegeLevel.Machine));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-08 вЂ” VMXON
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class VmxOnHandlerTests
    {
        [Fact]
        public void T4_08_VmxOn_MachineMode_SetsVmxEnableCsr()
        {
            var csr     = new CsrFile();
            var vmx     = new VmxExecutionUnit(csr, new TrackingVmcsManager());
            var state   = new P4CpuState();

            var result = vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), state, PrivilegeLevel.Machine);

            Assert.False(result.VmxFaulted);
            Assert.Equal(1UL, csr.DirectRead(CsrAddresses.VmxEnable));
            Assert.Equal(PipelineState.Task, state.GetCurrentPipelineState());
        }

        [Fact]
        public void T4_08b_VmxOn_SupervisorMode_ThrowsPrivilegeViolation()
        {
            var vmx = new VmxExecutionUnit(new CsrFile(), new TrackingVmcsManager());

            Assert.Throws<VmxPrivilegeViolationException>(
                () => vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), new P4CpuState(), PrivilegeLevel.Supervisor));
        }

        [Fact]
        public void T4_08c_VmxOn_UserMode_ThrowsPrivilegeViolation()
        {
            var vmx = new VmxExecutionUnit(new CsrFile(), new TrackingVmcsManager());

            Assert.Throws<VmxPrivilegeViolationException>(
                () => vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), new P4CpuState(), PrivilegeLevel.User));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-09 вЂ” VMLAUNCH (typed VM-entry path)
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class VmEntryHandlerTests
    {
        private static (VmxExecutionUnit vmx, CsrFile csr) MakeVmxEnabledUnit(
            IVmcsManager vmcs,
            IVmxEventSink? sink = null)
        {
            var csr = new CsrFile();
            var vmx = new VmxExecutionUnit(csr, vmcs, sink);
            vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), new P4CpuState(), PrivilegeLevel.Machine);
            return (vmx, csr);
        }

        [Fact]
        public void T4_09_VmLaunch_TaskToGuestExecution_VmcsLoaded()
        {
            var vmcs    = new TrackingVmcsManager();
            var sink    = new P4RecordingVmxEventSink();
            var state   = new P4CpuState();
            var (vmx, _) = MakeVmxEnabledUnit(vmcs, sink);

            state.SetReg(1, 0xABC000UL);
            vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMPTRLD, rs1: 1), state, PrivilegeLevel.Machine);
            var result = vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), state, PrivilegeLevel.Machine);

            Assert.False(result.VmxFaulted);
            Assert.Equal(PipelineState.GuestExecution, state.GetCurrentPipelineState());
            Assert.True(vmcs.HasActiveVmcs);
            Assert.Equal(0xABC000UL, vmcs.LastLoadedAddress);
            Assert.Contains(sink.Events, e => e.Kind == VmxEventKind.VmEntry);
        }

        [Fact]
        public void T4_09b_VmLaunch_WithoutVmxEnabled_ReturnsVmxFault()
        {
            var vmx = new VmxExecutionUnit(new CsrFile(), new TrackingVmcsManager());
            var state = new P4CpuState();

            var result = vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), state, PrivilegeLevel.Machine);

            Assert.True(result.VmxFaulted);
            Assert.Equal(PipelineState.Task, state.GetCurrentPipelineState());
        }

        [Fact]
        public void T4_09c_VmLaunch_WithoutActiveVmcs_ReturnsVmxFault()
        {
            var (vmx, _) = MakeVmxEnabledUnit(new FailingVmcsManager());
            var state = new P4CpuState();

            var result = vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), state, PrivilegeLevel.Machine);

            Assert.True(result.VmxFaulted);
            Assert.Equal(PipelineState.Task, state.GetCurrentPipelineState());
        }

        [Fact]
        public void T4_09d_VmLaunch_MachinePrivilegeRequired()
        {
            var (vmx, _) = MakeVmxEnabledUnit(new TrackingVmcsManager());

            Assert.Throws<VmxPrivilegeViolationException>(
                () => vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMLAUNCH), new P4CpuState(), PrivilegeLevel.Supervisor));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-10 вЂ” VM_EXIT (typed VM-exit path)
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class VmExitHandlerTests
    {
        [Fact]
        public void T4_10_VmExit_FromGuestExecution_SavesGuestRestoresHost_AndRecordsDiagnostics()
        {
            var csr = new CsrFile();
            var vmcs = new VmcsManager();
            var sink = new P4RecordingVmxEventSink();
            var vmx = new VmxExecutionUnit(csr, vmcs, sink);

            var state = new P4CpuState(initialPc: 0x1200);

            vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), state, PrivilegeLevel.Machine);
            state.SetReg(1, 0xABC000UL);
            vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMPTRLD, rs1: 1), state, PrivilegeLevel.Machine);

            vmcs.WriteFieldValue(VmcsField.HostPc, 0x5000);
            vmcs.WriteFieldValue(VmcsField.HostSp, 0x5100);
            vmcs.MarkLaunched();
            state.SetCurrentPipelineState(PipelineState.GuestExecution);
            state.SetReg(2, 0x3300);

            var result = vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), state, PrivilegeLevel.Machine);

            Assert.False(result.VmxFaulted);
            Assert.Equal(PipelineState.Task, state.GetCurrentPipelineState());
            Assert.Equal(0x5000UL, state.GetInstructionPointer());
            Assert.Equal(0x5100UL, state.ReadIntRegister(2));
            Assert.Equal(0UL, csr.DirectRead(CsrAddresses.VmxEnable));
            Assert.Equal((ulong)VmExitReason.VmxOff, csr.DirectRead(CsrAddresses.VmxExitReason));
            Assert.Equal(0x1200L, vmcs.ReadFieldValue(VmcsField.GuestPc).Value);
            Assert.Equal(0x3300L, vmcs.ReadFieldValue(VmcsField.GuestSp).Value);
            Assert.Contains(sink.Events, e => e.Kind == VmxEventKind.VmExit && e.Reason == VmExitReason.VmxOff);

            // FSM: GuestExecution в†’ VmExit в†’ Task
        }

        [Fact]
        public void T4_10b_VmExit_UsesVtScopedGuestStateTransfer()
        {
            var vmcs = new TrackingVmcsManager();
            var csr = new CsrFile();
            var vmx = new VmxExecutionUnit(csr, vmcs);
            var state = new P4CpuState();

            vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), state, PrivilegeLevel.Machine);
            vmcs.LoadPointer(0xABC000UL);
            vmcs.MarkLaunched();
            state.SetCurrentPipelineState(PipelineState.GuestExecution);
            state.SetVtPc(3, 0x8800UL);
            state.SetReg(2, 0x9900UL);

            VmxRetireEffect effect = vmx.Resolve(
                P4VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF),
                state,
                PrivilegeLevel.Machine,
                virtualThreadId: 3);

            VmxRetireOutcome outcome = vmx.RetireEffect(effect, state, virtualThreadId: 3);

            Assert.Equal((byte)3, vmcs.LastSaveGuestStateVtId);
            Assert.Equal((byte)3, vmcs.LastRestoreHostStateVtId);
            Assert.False(outcome.Faulted);
            Assert.Equal((ulong)VmExitReason.VmxOff, csr.DirectRead(CsrAddresses.VmxExitReason));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-11 вЂ” VMREAD / VMWRITE are InternalOpKind, not PipelineEvent subtypes
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class VmReadWriteAreInternalOpsTests
    {
        [Fact]
        public void T4_11_VmRead_IsInternalOpKind_NotPipelineEvent()
        {
            // VmRead/VmWrite must be values in the InternalOpKind enum.
            Assert.True(Enum.IsDefined(typeof(InternalOpKind), InternalOpKind.VmRead));
            Assert.True(Enum.IsDefined(typeof(InternalOpKind), InternalOpKind.VmWrite));
        }

        [Fact]
        public void T4_11b_VmRead_HasNoCorrespondingPipelineEventSubtype()
        {
            // Verify that no PipelineEvent subtype is named VmRead or VmWrite.
            var pipelineEventType = typeof(PipelineEvent);
            var subtypes = Assembly.GetAssembly(pipelineEventType)!
                .GetTypes()
                .Where(t => t.IsSubclassOf(pipelineEventType) || t == pipelineEventType);

            Assert.DoesNotContain(subtypes, t =>
                t.Name.IndexOf("VmRead",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.Name.IndexOf("VmWrite", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-12 / T4-13 вЂ” FspAdmissionPolicy
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class FspAdmissionPolicyTests
    {
        private static InstructionIR MakeIR() =>
            new InstructionIR
            {
                CanonicalOpcode    = InstructionsEnum.Nope,
                Class              = InstructionClass.ScalarAlu,
                SerializationClass = SerializationClass.Free,
                Rd                 = 0,
                Rs1                = 0,
                Rs2                = 0,
                Imm                = 0,
            };

        [Fact]
        public void T4_12_FspAdmission_NotStealableSlot_NotAdmitted()
        {
            var policy = new FspAdmissionPolicy();

            // Slot 0 is free (null) but marked NotStealable.
            var bundle = new InstructionIR?[] { null, MakeIR(), null, null, null, null, null, null };

            var notSteal = new SlotMetadata { StealabilityPolicy = StealabilityPolicy.NotStealable };
            var stealable = new SlotMetadata { StealabilityPolicy = StealabilityPolicy.Stealable };
            var slotMetas = new[]
            {
                notSteal,  // slot 0: free but not stealable
                stealable, // slot 1: occupied вЂ” irrelevant
                stealable, // slot 2: free and stealable
                stealable, stealable, stealable, stealable, stealable,
            };

            var meta   = new BundleMetadata { SlotMetadata = slotMetas, FspBoundary = false };
            var result = policy.Evaluate(bundle, meta, donorVtId: 1);

            // Slot 0 must NOT be in the result (not stealable).
            Assert.DoesNotContain(result.Entries, e => e.SlotIndex == 0);

            // Slot 2 IS free and stealable вЂ” must be admitted.
            Assert.Contains(result.Entries, e => e.SlotIndex == 2 && e.DonorVtId == 1);
        }

        [Fact]
        public void T4_13_FspAdmission_FspBoundaryTrue_NoSlotsAdmitted()
        {
            var policy = new FspAdmissionPolicy();

            // All slots free and stealable, but bundle has FspBoundary=true.
            var bundle   = new InstructionIR?[8];  // all null = all free
            var meta     = BundleMetadata.CreateFspFence(); // FspBoundary=true, all NotStealable
            var result   = policy.Evaluate(bundle, meta, donorVtId: 0);

            Assert.Equal(SlotAssignment.Empty, result);
            Assert.False(result.HasAssignments);
        }

        [Fact]
        public void T4_13b_FspAdmission_FspBoundaryFalse_AllFreeStealable_AllAdmitted()
        {
            var policy   = new FspAdmissionPolicy();
            var bundle   = new InstructionIR?[4]; // 4 free slots, no fixed size needed
            var meta     = BundleMetadata.CreateAllStealable();
            // CreateAllStealable gives 8 slot-metas; our 4-slot bundle uses slots 0-3.
            var result   = policy.Evaluate(bundle, meta, donorVtId: 2);

            Assert.Equal(4, result.Count);
            for (int i = 0; i < 4; i++)
                Assert.Contains(result.Entries, e => e.SlotIndex == i && e.DonorVtId == 2);
        }

        [Fact]
        public void T4_13c_FspAdmission_OccupiedSlotsNeverAdmitted()
        {
            var policy = new FspAdmissionPolicy();

            // Only slot 2 is free; the rest are occupied.
            var bundle = new InstructionIR?[]
            {
                MakeIR(), MakeIR(), null, MakeIR(), MakeIR(), MakeIR(), MakeIR(), MakeIR()
            };
            var meta   = BundleMetadata.CreateAllStealable();
            var result = policy.Evaluate(bundle, meta, donorVtId: 3);

            Assert.Equal(1, result.Count);
            Assert.Equal(2, result.Entries[0].SlotIndex);
        }

        // в”Ђв”Ђ T4-14: FspAdmissionPolicy method signature uses only metadata в”Ђв”Ђв”Ђв”Ђв”Ђ
        [Fact]
        public void T4_14_FspAdmissionPolicy_Evaluate_ParameterTypesUseOnlyMetadata()
        {
            // The Evaluate method must NOT accept a MicroOp parameter.
            // It must accept BundleMetadata and InstructionIR (V5 IR), not legacy MicroOp.
            var method = typeof(FspAdmissionPolicy)
                .GetMethod(nameof(FspAdmissionPolicy.Evaluate),
                           BindingFlags.Public | BindingFlags.Instance);

            Assert.NotNull(method);

            var paramTypes = method!.GetParameters();

            // No parameter should be of type MicroOp or any subclass.
            foreach (var p in paramTypes)
            {
                Assert.False(
                    typeof(MicroOp).IsAssignableFrom(p.ParameterType) ||
                    (p.ParameterType.IsGenericType &&
                     p.ParameterType.GenericTypeArguments.Any(
                         a => typeof(MicroOp).IsAssignableFrom(a))),
                    $"Parameter '{p.Name}' of type '{p.ParameterType}' is or contains MicroOp");
            }

            // Must accept BundleMetadata.
            Assert.Contains(paramTypes, p => p.ParameterType == typeof(BundleMetadata));
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // T4-15 вЂ” WFE / SEV вЂ” regression for FSM-owned wait/wake transitions
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class WfeSevHandlerTests
    {
        [Fact]
        public void T4_15_Wfe_TransitionsToWaitForEvent_SevResumesWaitingState()
        {
            var handler = new PipelineFsmEventHandler(
                new CsrFile(), vtCount: 4);

            var cpuState = new P4CpuState();

            // VT 1 issues WFE.
            var wfeEvt = new WfeEvent { VtId = 1, BundleSerial = 20 };
            var waitingState = handler.Handle(wfeEvt, PipelineState.Task, cpuState, PrivilegeLevel.User);

            Assert.Equal(PipelineState.WaitForEvent, waitingState);

            // VT 0 issues SEV вЂ” wakes the waiting FSM state.
            var sevEvt = new SevEvent { VtId = 0, BundleSerial = 21 };
            var resumedState = handler.Handle(sevEvt, waitingState, cpuState, PrivilegeLevel.User);

            Assert.Equal(PipelineState.Task, resumedState);
        }

        [Fact]
        public void T4_15aa_LiveCpuStateAdapter_UsesPerVirtualThreadPipelineState()
        {
            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.WriteVirtualThreadPipelineState(1, PipelineState.WaitForEvent);

            var vt0State = core.CreateLiveCpuStateAdapter(0);
            var vt1State = core.CreateLiveCpuStateAdapter(1);

            Assert.Equal(PipelineState.Task, vt0State.GetCurrentPipelineState());
            Assert.Equal(PipelineState.WaitForEvent, vt1State.GetCurrentPipelineState());
            Assert.True(core.CanVirtualThreadIssueInForeground(0));
            Assert.False(core.CanVirtualThreadIssueInForeground(1));
        }

        [Fact]
        public void T4_15ab_SevEvent_WakesAllWaitingVirtualThreads_ThroughRetireFsmPlane()
        {
            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.WriteVirtualThreadPipelineState(1, PipelineState.WaitForEvent);
            core.WriteVirtualThreadPipelineState(2, PipelineState.WaitForEvent);

            core.ApplyRetiredSystemEventForTesting(
                new SevEvent { VtId = 0, BundleSerial = 31 },
                virtualThreadId: 0,
                retiredPc: 0x100);

            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(1));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(2));
        }

        [Fact]
        public void T4_15b_Sev_DoesNotInventWakeState_WhenThreadIsRunnable()
        {
            var handler  = new PipelineFsmEventHandler(new CsrFile());
            var cpuState = new P4CpuState();

            var evt  = new SevEvent { VtId = 0, BundleSerial = 22 };
            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.User);

            Assert.Equal(PipelineState.Task, next);
        }

        [Fact]
        public void T4_15c_Yield_FsmStateUnchanged()
        {
            var handler  = new PipelineFsmEventHandler(new CsrFile());
            var cpuState = new P4CpuState();

            var evt  = new YieldEvent { VtId = 0, BundleSerial = 23 };
            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.User);

            Assert.Equal(PipelineState.Task, next);
        }

        [Fact]
        public void T4_15d_PodBarrier_FsmStateUnchanged()
        {
            var handler  = new PipelineFsmEventHandler(new CsrFile());
            var cpuState = new P4CpuState();

            var evt  = new PodBarrierEvent { VtId = 0, BundleSerial = 24 };
            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.User);

            Assert.Equal(PipelineState.Task, next);
        }

        [Fact]
        public void T4_15e_VtBarrier_FsmStateUnchanged()
        {
            var handler  = new PipelineFsmEventHandler(new CsrFile());
            var cpuState = new P4CpuState();

            var evt  = new VtBarrierEvent { VtId = 2, BundleSerial = 25 };
            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.User);

            Assert.Equal(PipelineState.Task, next);
        }

        [Fact]
        public void T4_15f_VmxOff_WhenInTask_FsmStateUnchanged()
        {
            // VMXOFF from Task вЂ” the FSM table has no GuestExecutionв†’VmExit trigger
            // for VmxOff from Task state; PipelineFsmGuard.Advance returns current.
            var csr     = new CsrFile();
            var vmx = new VmxExecutionUnit(csr, new TrackingVmcsManager());
            var state = new P4CpuState();

            // First enable VMX so we can turn it off.
            vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMXON), state, PrivilegeLevel.Machine);

            var result = vmx.Execute(P4VmxIrHelper.MakeVmx(InstructionsEnum.VMXOFF), state, PrivilegeLevel.Machine);

            Assert.False(result.VmxFaulted);
            Assert.Equal(0UL, csr.DirectRead(CsrAddresses.VmxEnable));
            // VMXOFF from Task disables VMX without leaving host Task state.
            Assert.Equal(PipelineState.Task, state.GetCurrentPipelineState());
        }

        [Fact]
        public void T4_15g_UnknownEvent_PassThrough()
        {
            // An event type that PipelineFsmEventHandler doesn't know about
            // must return the current state unchanged (defensive pass-through).
            var handler  = new PipelineFsmEventHandler(new CsrFile());
            var cpuState = new P4CpuState();

            // ResetEvent is a valid PipelineEvent but not in the handler's dispatch table.
            var evt  = new ResetEvent { VtId = 0, BundleSerial = 27 };
            var next = handler.Handle(evt, PipelineState.Task, cpuState, PrivilegeLevel.Machine);

            Assert.Equal(PipelineState.Task, next);
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // Guard: ArgumentNullException on null inputs
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public sealed class PipelineFsmEventHandlerNullGuardTests
    {
        [Fact]
        public void NullCsr_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PipelineFsmEventHandler(null!));
        }

        [Fact]
        public void ZeroVtCount_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PipelineFsmEventHandler(new CsrFile(), vtCount: 0));
        }

        [Fact]
        public void NullEvent_Handle_ThrowsArgumentNullException()
        {
            var handler = new PipelineFsmEventHandler(new CsrFile());
            Assert.Throws<ArgumentNullException>(() =>
                handler.Handle(null!, PipelineState.Task, new P4CpuState(), PrivilegeLevel.Machine));
        }

        [Fact]
        public void NullCpuState_Handle_ThrowsArgumentNullException()
        {
            var handler = new PipelineFsmEventHandler(new CsrFile());
            var evt     = new EcallEvent { VtId = 0, BundleSerial = 0, EcallCode = 0 };
            Assert.Throws<ArgumentNullException>(() =>
                handler.Handle(evt, PipelineState.Task, null!, PrivilegeLevel.Machine));
        }
    }
}


