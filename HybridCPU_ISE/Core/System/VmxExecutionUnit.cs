using System;
using System.Diagnostics;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Thrown when a VMX instruction is executed from a privilege level
    /// below Machine mode.
    /// </summary>
    public sealed class VmxPrivilegeViolationException : Exception
    {
        public VmxPrivilegeViolationException(string message) : base(message) { }
    }

    /// <summary>
    /// VMX instruction plane execution unit.
    /// Production pipeline users resolve a typed <see cref="VmxRetireEffect"/>
    /// during execution and retire it explicitly later through
    /// <see cref="RetireEffect"/>.
    /// </summary>
    public sealed class VmxExecutionUnit
    {
        private const ushort StackPointerArchRegId = 2;

        private readonly CsrFile _csr;
        private readonly IVmcsManager _vmcs;
        private readonly IVmxEventSink _trace;

        public VmxExecutionUnit(
            CsrFile csrFile,
            IVmcsManager vmcsManager)
            : this(csrFile, vmcsManager, eventSink: null)
        {
        }

        internal VmxExecutionUnit(
            CsrFile csrFile,
            IVmcsManager vmcsManager,
            IVmxEventSink? eventSink)
        {
            _csr = csrFile ?? throw new ArgumentNullException(nameof(csrFile));
            _vmcs = vmcsManager ?? throw new ArgumentNullException(nameof(vmcsManager));
            _trace = eventSink ?? NullVmxEventSink.Instance;
        }

        /// <summary>
        /// Semantic-compatibility wrapper for direct execution tests.
        /// Authoritative direct retire follow-through should prefer <see cref="Resolve"/>
        /// plus the core-owned direct compat retire transaction apply path, while
        /// production pipeline code continues to retire the resolved effect explicitly.
        /// </summary>
        public ExecutionResult Execute(
            InstructionIR instr,
            ICanonicalCpuState state,
            PrivilegeLevel privilege,
            byte virtualThreadId = 0)
        {
            VmxRetireEffect effect = Resolve(instr, state, privilege, virtualThreadId);
            VmxRetireOutcome outcome = RetireEffect(effect, state, virtualThreadId);

            if (outcome.HasRegisterWriteback)
            {
                state.WriteRegister(virtualThreadId, outcome.RegisterDestination, outcome.RegisterWritebackValue);
            }

            if (outcome.RestoredStackPointer.HasValue)
            {
                state.WriteRegister(virtualThreadId, StackPointerArchRegId, outcome.RestoredStackPointer.Value);
            }

            if (outcome.RedirectTargetPc.HasValue)
            {
                state.WritePc(virtualThreadId, outcome.RedirectTargetPc.Value);
            }

            state.SetCurrentPipelineState(
                ResolveFinalPipelineState(
                    state.GetCurrentPipelineState(),
                    effect,
                    outcome));

            return outcome.Faulted
                ? ExecutionResult.VmxFault()
                : ExecutionResult.Ok(outcome.HasRegisterWriteback ? outcome.RegisterWritebackValue : 0);
        }

        public VmxRetireEffect Resolve(
            InstructionIR instr,
            ICanonicalCpuState state,
            PrivilegeLevel privilege,
            byte virtualThreadId)
        {
            ushort opcode = instr.CanonicalOpcode.Value;
            string opcodeName = OpcodeRegistry.GetMnemonicOrHex(opcode);

            if (privilege < PrivilegeLevel.Machine)
            {
                throw new VmxPrivilegeViolationException(
                    $"VMX instruction {opcodeName} requires Machine-mode privilege; current level is {privilege}");
            }

            return opcode switch
            {
                IsaOpcodeValues.VMXON => ResolveVmxOn(),
                IsaOpcodeValues.VMXOFF => ResolveVmxOff(state),
                IsaOpcodeValues.VMLAUNCH => ResolveVmLaunch(state),
                IsaOpcodeValues.VMRESUME => ResolveVmResume(state),
                IsaOpcodeValues.VMREAD => ResolveVmRead(instr, state, virtualThreadId),
                IsaOpcodeValues.VMWRITE => ResolveVmWrite(instr, state, virtualThreadId),
                IsaOpcodeValues.VMCLEAR => ResolveVmClear(instr, state, virtualThreadId),
                IsaOpcodeValues.VMPTRLD => ResolveVmPtrLd(instr, state, virtualThreadId),
                _ => throw new UnreachableException($"Unknown VMX opcode: {opcodeName}")
            };
        }

        public VmxRetireOutcome RetireEffect(
            in VmxRetireEffect effect,
            ICanonicalCpuState state,
            byte virtualThreadId)
        {
            ushort coreId = state.GetCoreID();
            if (!effect.IsValid)
            {
                return VmxRetireOutcome.NoOp();
            }

            if (effect.IsFaulted)
            {
                if (effect.FailureReason != VmExitReason.None)
                {
                    _csr.HardwareWrite(CsrAddresses.VmxExitReason, (ulong)effect.FailureReason);
                }

                return VmxRetireOutcome.Fault(effect.FailureReason);
            }

            return effect.Operation switch
            {
                VmxOperationKind.VmxOn => ApplyVmxOn(coreId),
                VmxOperationKind.VmxOff => ApplyVmxOff(effect, state, virtualThreadId, coreId),
                VmxOperationKind.VmLaunch => ApplyVmEntry(effect, markLaunchedOnSuccess: true, coreId),
                VmxOperationKind.VmResume => ApplyVmEntry(effect, markLaunchedOnSuccess: false, coreId),
                VmxOperationKind.VmRead => ApplyVmRead(effect, coreId),
                VmxOperationKind.VmWrite => ApplyVmWrite(effect, coreId),
                VmxOperationKind.VmClear => ApplyVmClear(effect, coreId),
                VmxOperationKind.VmPtrLd => ApplyVmPtrLd(effect, coreId),
                _ => throw new UnreachableException($"Unsupported VMX retire operation: {effect.Operation}")
            };
        }

        public static PipelineState ResolveFinalPipelineState(
            PipelineState current,
            in VmxRetireEffect effect,
            in VmxRetireOutcome outcome)
        {
            if (!effect.IsValid)
            {
                return current;
            }

            switch (effect.Operation)
            {
                case VmxOperationKind.VmLaunch:
                {
                    if (effect.IsFaulted)
                        return current;

                    PipelineState afterEntry = PipelineFsmGuard.Transition(current, PipelineTransitionTrigger.VmLaunch);
                    return PipelineFsmGuard.Transition(
                        afterEntry,
                        outcome.Faulted ? PipelineTransitionTrigger.EntryFail : PipelineTransitionTrigger.EntryOk);
                }

                case VmxOperationKind.VmResume:
                {
                    if (effect.IsFaulted)
                        return current;

                    PipelineState afterEntry = PipelineFsmGuard.Transition(current, PipelineTransitionTrigger.VmResume);
                    return PipelineFsmGuard.Transition(
                        afterEntry,
                        outcome.Faulted ? PipelineTransitionTrigger.EntryFail : PipelineTransitionTrigger.EntryOk);
                }

                case VmxOperationKind.VmxOff:
                {
                    if (!effect.ExitGuestContextOnRetire)
                        return current;

                    PipelineState afterExit = PipelineFsmGuard.Transition(current, PipelineTransitionTrigger.VmxOff);
                    return PipelineFsmGuard.Transition(afterExit, PipelineTransitionTrigger.ExitComplete);
                }

                default:
                    return current;
            }
        }

        private VmxRetireEffect ResolveVmxOn()
        {
            return _csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine) != 0
                ? VmxRetireEffect.Fault(VmxOperationKind.VmxOn)
                : VmxRetireEffect.Control(VmxOperationKind.VmxOn);
        }

        private VmxRetireEffect ResolveVmxOff(ICanonicalCpuState state)
        {
            if (_csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine) == 0)
            {
                return VmxRetireEffect.Fault(VmxOperationKind.VmxOff);
            }

            return VmxRetireEffect.Control(
                VmxOperationKind.VmxOff,
                exitGuestContextOnRetire: state.GetCurrentPipelineState() == PipelineState.GuestExecution);
        }

        private VmxRetireEffect ResolveVmLaunch(ICanonicalCpuState state)
        {
            if (_csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine) == 0 ||
                state.GetCurrentPipelineState() != PipelineState.Task ||
                !_vmcs.HasActiveVmcs ||
                _vmcs.HasLaunchedVmcs)
            {
                return VmxRetireEffect.Fault(VmxOperationKind.VmLaunch);
            }

            return VmxRetireEffect.Control(VmxOperationKind.VmLaunch);
        }

        private VmxRetireEffect ResolveVmResume(ICanonicalCpuState state)
        {
            if (_csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine) == 0 ||
                state.GetCurrentPipelineState() != PipelineState.Task ||
                !_vmcs.HasLaunchedVmcs)
            {
                return VmxRetireEffect.Fault(VmxOperationKind.VmResume);
            }

            return VmxRetireEffect.Control(VmxOperationKind.VmResume);
        }

        private VmxRetireEffect ResolveVmRead(
            InstructionIR instr,
            ICanonicalCpuState state,
            byte virtualThreadId)
        {
            if (_csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine) == 0 ||
                !_vmcs.HasActiveVmcs)
            {
                return VmxRetireEffect.Fault(VmxOperationKind.VmRead);
            }

            VmcsField field = (VmcsField)ReadVmxSourceOperand(state, virtualThreadId, instr.Rs1);
            bool hasRegisterDestination = instr.Rd != 0;
            return VmxRetireEffect.VmcsRead(field, instr.Rd, hasRegisterDestination);
        }

        private VmxRetireEffect ResolveVmWrite(
            InstructionIR instr,
            ICanonicalCpuState state,
            byte virtualThreadId)
        {
            if (_csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine) == 0 ||
                !_vmcs.HasActiveVmcs)
            {
                return VmxRetireEffect.Fault(VmxOperationKind.VmWrite);
            }

            VmcsField field = (VmcsField)ReadVmxSourceOperand(state, virtualThreadId, instr.Rs1);
            long value = unchecked((long)ReadVmxSourceOperand(state, virtualThreadId, instr.Rs2));
            return VmxRetireEffect.VmcsWrite(field, value);
        }

        private VmxRetireEffect ResolveVmClear(
            InstructionIR instr,
            ICanonicalCpuState state,
            byte virtualThreadId)
        {
            if (_csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine) == 0)
            {
                return VmxRetireEffect.Fault(VmxOperationKind.VmClear);
            }

            ulong vmcsAddress = ReadVmxSourceOperand(state, virtualThreadId, instr.Rs1);
            return VmxRetireEffect.VmcsPointerEffect(VmxOperationKind.VmClear, vmcsAddress);
        }

        private VmxRetireEffect ResolveVmPtrLd(
            InstructionIR instr,
            ICanonicalCpuState state,
            byte virtualThreadId)
        {
            if (_csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine) == 0)
            {
                return VmxRetireEffect.Fault(VmxOperationKind.VmPtrLd);
            }

            ulong vmcsAddress = ReadVmxSourceOperand(state, virtualThreadId, instr.Rs1);
            return VmxRetireEffect.VmcsPointerEffect(VmxOperationKind.VmPtrLd, vmcsAddress);
        }

        private VmxRetireOutcome ApplyVmxOn(ushort coreId)
        {
            _csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            _trace.RecordVmxEvent(VmxEventKind.VmxOn, coreId);
            return VmxRetireOutcome.NoOp();
        }

        private VmxRetireOutcome ApplyVmxOff(
            in VmxRetireEffect effect,
            ICanonicalCpuState state,
            byte virtualThreadId,
            ushort coreId)
        {
            if (!effect.ExitGuestContextOnRetire)
            {
                _csr.Write(CsrAddresses.VmxEnable, 0, PrivilegeLevel.Machine);
                _trace.RecordVmxEvent(VmxEventKind.VmxOff, coreId);
                return VmxRetireOutcome.NoOp();
            }

            VmExitTransitionResult exitResult = _vmcs.CompleteVmExit(state, virtualThreadId, VmExitReason.VmxOff);
            _csr.HardwareWrite(CsrAddresses.VmxExitReason, (ulong)exitResult.ExitReason);
            ulong exitCount = _csr.DirectRead(CsrAddresses.VmExitCnt);
            _csr.HardwareWrite(CsrAddresses.VmExitCnt, exitCount + 1);
            _csr.Write(CsrAddresses.VmxEnable, 0, PrivilegeLevel.Machine);
            _trace.RecordVmxEvent(VmxEventKind.VmExit, coreId, exitResult.ExitReason);
            _trace.RecordVmxEvent(VmxEventKind.VmxOff, coreId);

            return new VmxRetireOutcome(
                Faulted: false,
                FailureReason: VmExitReason.None,
                HasRegisterWriteback: false,
                RegisterDestination: 0,
                RegisterWritebackValue: 0,
                RedirectTargetPc: exitResult.HostPc,
                RestoredStackPointer: exitResult.HostSp,
                FlushesPipeline: exitResult.HostPc.HasValue);
        }

        private VmxRetireOutcome ApplyVmEntry(
            in VmxRetireEffect effect,
            bool markLaunchedOnSuccess,
            ushort coreId)
        {
            VmEntryTransitionResult entryResult = _vmcs.BeginVmEntry(markLaunchedOnSuccess);
            if (!entryResult.Success)
            {
                if (entryResult.FailureReason != VmExitReason.None)
                {
                    _csr.HardwareWrite(CsrAddresses.VmxExitReason, (ulong)entryResult.FailureReason);
                }

                _trace.RecordVmxEvent(
                    effect.Operation == VmxOperationKind.VmResume ? VmxEventKind.VmResume : VmxEventKind.VmEntry,
                    coreId,
                    entryResult.FailureReason);
                return VmxRetireOutcome.Fault(entryResult.FailureReason);
            }

            _trace.RecordVmxEvent(
                effect.Operation == VmxOperationKind.VmResume ? VmxEventKind.VmResume : VmxEventKind.VmEntry,
                coreId);
            return new VmxRetireOutcome(
                Faulted: false,
                FailureReason: VmExitReason.None,
                HasRegisterWriteback: false,
                RegisterDestination: 0,
                RegisterWritebackValue: 0,
                RedirectTargetPc: entryResult.GuestPc,
                RestoredStackPointer: entryResult.GuestSp,
                FlushesPipeline: entryResult.GuestPc.HasValue);
        }

        private VmxRetireOutcome ApplyVmRead(in VmxRetireEffect effect, ushort coreId)
        {
            VmcsFieldReadResult readResult = _vmcs.ReadFieldValue(effect.VmcsField);
            _trace.RecordVmxEvent(VmxEventKind.VmRead, coreId);

            return new VmxRetireOutcome(
                Faulted: false,
                FailureReason: VmExitReason.None,
                HasRegisterWriteback: effect.HasRegisterDestination,
                RegisterDestination: effect.RegisterDestination,
                RegisterWritebackValue: unchecked((ulong)readResult.Value),
                RedirectTargetPc: null,
                RestoredStackPointer: null,
                FlushesPipeline: false);
        }

        private VmxRetireOutcome ApplyVmWrite(in VmxRetireEffect effect, ushort coreId)
        {
            _vmcs.WriteFieldValue(effect.VmcsField, effect.VmcsValue);
            _trace.RecordVmxEvent(VmxEventKind.VmWrite, coreId);
            return VmxRetireOutcome.NoOp();
        }

        private VmxRetireOutcome ApplyVmClear(in VmxRetireEffect effect, ushort coreId)
        {
            _vmcs.ClearPointer(effect.VmcsPointer);
            _trace.RecordVmxEvent(VmxEventKind.VmClear, coreId);
            return VmxRetireOutcome.NoOp();
        }

        private VmxRetireOutcome ApplyVmPtrLd(in VmxRetireEffect effect, ushort coreId)
        {
            _vmcs.LoadPointer(effect.VmcsPointer);
            _trace.RecordVmxEvent(VmxEventKind.VmPtrLd, coreId);
            return VmxRetireOutcome.NoOp();
        }

        private static ulong ReadVmxSourceOperand(ICanonicalCpuState state, byte virtualThreadId, ushort regId)
        {
            if (regId == 0)
            {
                return 0;
            }

            return unchecked((ulong)state.ReadRegister(virtualThreadId, regId));
        }
    }
}

