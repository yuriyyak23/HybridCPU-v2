using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Per-thread floating-point exception context for IEEE 754 compliance.
            /// Ensures FP exceptions in stolen slots don't corrupt other threads' FCSR.
            /// </summary>
            public struct FPExceptionContext
            {
                public uint FCSR;              // FP Control & Status Register
                public byte RoundingMode;      // IEEE 754 rounding mode (0-3)
                public byte ExceptionFlags;    // Accumulated exception flags
                public bool InvalidOp;         // Invalid operation flag
                public bool DivByZero;         // DIV by zero flag
                public bool Overflow;          // Overflow flag
                public bool Underflow;         // Underflow flag
                public bool Inexact;           // Inexact result flag

                /// <summary>
                /// Clear all exception flags
                /// </summary>
                public void Clear()
                {
                    FCSR = 0;
                    RoundingMode = 0;
                    ExceptionFlags = 0;
                    InvalidOp = false;
                    DivByZero = false;
                    Overflow = false;
                    Underflow = false;
                    Inexact = false;
                }

                /// <summary>
                /// Merge exception flags from another context
                /// </summary>
                public void MergeFrom(FPExceptionContext other)
                {
                    InvalidOp |= other.InvalidOp;
                    DivByZero |= other.DivByZero;
                    Overflow |= other.Overflow;
                    Underflow |= other.Underflow;
                    Inexact |= other.Inexact;

                    // Update ExceptionFlags byte
                    ExceptionFlags = 0;
                    if (InvalidOp) ExceptionFlags |= 0x01;
                    if (DivByZero) ExceptionFlags |= 0x02;
                    if (Overflow) ExceptionFlags |= 0x04;
                    if (Underflow) ExceptionFlags |= 0x08;
                    if (Inexact) ExceptionFlags |= 0x10;
                }

                /// <summary>
                /// Check if any exception flag is set
                /// </summary>
                public bool HasException()
                {
                    return InvalidOp || DivByZero || Overflow || Underflow || Inexact;
                }
            }

            /// <param name="CoreID">Zero-based hardware core index.</param>
            /// <param name="platformContext">Explicit runtime dependencies for the core.</param>
            public CPU_Core(ushort CoreID, CpuCorePlatformContext platformContext)
            {
                if (!platformContext.IsConfigured)
                {
                    throw new ArgumentException(
                        "CPU_Core requires an explicit, fully configured CpuCorePlatformContext.",
                        nameof(platformContext));
                }

                this.CoreID = CoreID;
                this._platformContext = platformContext;
                this._executionMode = platformContext.ResolveExecutionMode();
                this._interruptDispatcher = DefaultInterruptDispatcher;
                this._mainMemory = platformContext.MainMemory;
                this._atomicMemoryUnit = platformContext.CreateAtomicMemoryUnit();
                this._dmaStreamComputeTokenStore = new DmaStreamComputeTokenStore();
                this._externalAcceleratorRuntime = null;

                this.CoreFlagsRegister = new FlagsRegister(CoreID);

                this.Core_FlagsRegisters_Stack = new List<FlagsRegister>();

                this.Call_Callback_Addresses = new List<ulong>();
                this.Interrupt_Callback_Addresses = new List<ulong>();

                this.CycleCounter = 0;
                this.StageCycleCounter = 0;
                this.Stalled = false;

                this.ulong_InstructionPointer = 0;
                this.Stack = new StackMemory();
                this.ulong_MinL1Query = 0;
                this.Current_VLIWBundle_Position = 0;
                this.Current_DataObject_Position = 0;
                this._hasMaterializedVliwFetchState = false;

                // Initialize predicate registers, vector config, and scratch buffers
                InitializeVectorState();

                // Initialize per-thread FP exception contexts (4 virtual threads per core for 4-way SMT)
                this.ThreadFPContexts = new FPExceptionContext[SmtWays];
                for (int i = 0; i < SmtWays; i++)
                {
                    this.ThreadFPContexts[i] = new FPExceptionContext();
                    this.ThreadFPContexts[i].Clear();
                }

                this.VirtualThreadStalled = new bool[SmtWays];
                this.ActiveVirtualThreadId = 0;

                // Blueprint Step 1: PRF + Rename РІР‚вЂќ initialise shared physical register file,
                // per-VT rename maps, commit map and free list.
                this.PhysicalRegisters = new PhysicalRegisterFile();
                this.ArchRenameMap = new RenameMap(SmtWays);
                this.ArchCommitMap = new CommitMap(SmtWays);
                this.PhysRegFreeList = new FreeList();

                // Phase 04: unified committed architectural state now lives per VT in ArchContexts.
                this.ArchContexts = new ArchContextState[SmtWays];
                for (int i = 0; i < SmtWays; i++)
                {
                    this.ArchContexts[i] = new ArchContextState(RenameMap.ArchRegs, i);
                }

                this.RetireCoordinator = new RetireCoordinator(
                    this.PhysicalRegisters,
                    this.ArchRenameMap,
                    this.ArchCommitMap,
                    this.ArchContexts);

                // Blueprint Р’В§3.40 / Р’В§4.50: VMCS structure РІР‚вЂќ per-core VMCS manager.
                this.IsVMXRoot = false;

                // Compatibility CSR aliases remain until their projection owners are fully demoted.
                this.Csr = new CsrFile();
                this._vmxExecutionPlaneWired = true;
                this.VirtualThreadPipelineStates = new PipelineState[SmtWays];
                for (int vt = 0; vt < SmtWays; vt++)
                {
                    this.VirtualThreadPipelineStates[vt] = PipelineState.Task;
                }
                ResetCurrentDecodedBundleSlotCarrierState(0);
            }

            /// <summary>
            /// Number of simultaneous hardware threads per physical core (4-way SMT).
            /// </summary>
            public const int SmtWays = 4;

            /// <summary>
            /// Per-virtual-thread stall flags for 4-way SMT.
            /// When a virtual thread encounters a cache miss or DMA wait,
            /// only that thread's stall flag is raised; other threads continue.
            /// HLS: 4-bit register, indexed by VirtualThreadId.
            /// </summary>
            public bool[] VirtualThreadStalled;

            /// <summary>
            /// Currently active virtual thread ID (0РІР‚вЂњ3) for Round-Robin fetch.
            /// Advances each cycle to alternate instruction fetch between threads.
            /// </summary>
            public int ActiveVirtualThreadId;
            private bool _hasMaterializedVliwFetchState;

            private ulong _assistRuntimeEpoch;
            private Core.AssistInvalidationReason _lastAssistInvalidationReason;
            private long _assistLaunchCount;
            private long _assistCompletedCount;
            private long _assistKilledCount;
            private long _assistInvalidationCount;
            private ulong _testReferenceRawFallbackCount;

            /// <summary>
            /// Read the currently active virtual thread ID with checked range ownership.
            /// </summary>
            public int ReadActiveVirtualThreadId() =>
                ResolveLiveStateVtIdOrThrow(ActiveVirtualThreadId, nameof(ReadActiveVirtualThreadId));

            // РІвЂќР‚РІвЂќР‚РІвЂќР‚ Blueprint Step 1: Physical Register File + Rename РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚

            /// <summary>
            /// Shared physical register file РІР‚вЂќ 128 Р“вЂ” 64-bit entries.
            /// All virtual threads draw from this single pool via <see cref="ArchRenameMap"/>.
            /// Physical register 0 is hardwired to zero.
            /// </summary>
            public PhysicalRegisterFile PhysicalRegisters;

            /// <summary>
            /// Per-VT architecturalРІвЂ вЂ™physical register rename map.
            /// <c>ArchRenameMap[vtId][archReg]</c> gives the current physical register.
            /// Initialised to the identity mapping at reset.
            /// </summary>
            public RenameMap ArchRenameMap;

            /// <summary>
            /// Commit-point rename map snapshot for precise rollback.
            /// Updated on instruction retirement; used to restore state on
            /// branch mispredict or precise architectural fault.
            /// </summary>
            public CommitMap ArchCommitMap;

            /// <summary>
            /// Free list of physical registers available for allocation.
            /// Seeded with registers [ArchRegs РІР‚В¦ TotalPhysRegsРІв‚¬вЂ™1] at reset.
            /// </summary>
            public FreeList PhysRegFreeList;

            /// <summary>
            /// Per-VT architectural context objects introduced in phase 04.
            /// They group committed GPR state, committed PC, and privilege/context metadata.
            /// </summary>
            public ArchContextState[] ArchContexts;

            // РІвЂќР‚РІвЂќР‚РІвЂќР‚ Blueprint Р’В§3 / Checklist P0: Per-VT committed architectural register mirror РІвЂќР‚

            /// <summary>
            /// Unified retire authority for committed architectural register and PC writes.
            /// </summary>
            public RetireCoordinator RetireCoordinator;

            // РІвЂќР‚РІвЂќР‚РІвЂќР‚ Blueprint Р’В§4 / Р’В§3.40: Explicit VMX state РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚

            /// <summary>
            /// When <see langword="true"/> this core is operating in VMX root mode (host).
            /// Set by <c>VMXON</c>, cleared by <c>VMXOFF</c>.
            ///
            /// Blueprint Р’В§4 (Mandatory Structural Changes): "Р вЂќР С•Р В±Р В°Р Р†Р С‘РЎвЂљРЎРЉ Р С—Р С•Р В»Р Вµ IsVMXRoot Р Р† StateData."
            /// </summary>
            public bool IsVMXRoot;

            // РІвЂќР‚РІвЂќР‚РІвЂќР‚ Compatibility CSR aliases and transition state РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚РІвЂќР‚

            /// <summary>
            /// Per-core CSR (Control and Status Register) file.
            /// Compatibility aliases remain subject to generated projection and
            /// runtime-owned admission policy.
            /// </summary>
            public CsrFile Csr;

            /// <summary>
            /// Per-core pipeline FSM state РІР‚вЂќ tracks VMX root/non-root and exception
            /// transitions (Task, VmEntry, GuestExecution, VmExit, Halted, РІР‚В¦).
            /// Blueprint Phase 6: replaces implicit pipeline-state assumptions.
            /// </summary>
            public PipelineState[] VirtualThreadPipelineStates;

            private bool _vmxExecutionPlaneWired;

            /// <summary>
            /// Per-core retire-authoritative atomic memory plane.
            /// Backed by main memory and the shared LR/SC reservation registry.
            /// </summary>
            public Core.Memory.IAtomicMemoryUnit AtomicMemoryUnit =>
                _atomicMemoryUnit ??= CreateDefaultAtomicMemoryUnit();

            private Core.Memory.IAtomicMemoryUnit? _atomicMemoryUnit;
            private DmaStreamComputeTokenStore? _dmaStreamComputeTokenStore;
            private ExternalAcceleratorRuntime? _externalAcceleratorRuntime;

            internal DmaStreamComputeTokenStore GetDmaStreamComputeTokenStore() =>
                _dmaStreamComputeTokenStore ??= new DmaStreamComputeTokenStore();

            internal ExternalAcceleratorRuntime GetExternalAcceleratorRuntime() =>
                _externalAcceleratorRuntime ??= new ExternalAcceleratorRuntime(GetBoundMainMemory());

            private Core.Memory.IAtomicMemoryUnit CreateDefaultAtomicMemoryUnit()
            {
                if (_platformContext.IsConfigured)
                {
                    return _platformContext.CreateAtomicMemoryUnit();
                }

                return new Core.Memory.MainMemoryAtomicMemoryUnit(GetBoundMainMemory());
            }

            internal bool HasWiredVmxExecutionPlane => _vmxExecutionPlaneWired;

            internal void SetVmxExecutionPlaneWiredForTesting(bool wired)
            {
                _vmxExecutionPlaneWired = wired;
            }

            /// <summary>
            /// Per-thread FP exception contexts for 4 virtual hardware threads (4-way SMT).
            /// Ensures FP exceptions in stolen slots don't affect other threads.
            /// </summary>
            public FPExceptionContext[] ThreadFPContexts;

            public FlagsRegister CoreFlagsRegister;

            public List<FlagsRegister> Core_FlagsRegisters_Stack;

            public List<ulong> Call_Callback_Addresses;
            public List<ulong> Interrupt_Callback_Addresses;

            public uint CoreID;

            ulong ulong_InstructionPointer;

            /// <summary>
            /// Read the live frontend PC for the currently active virtual thread.
            /// This remains distinct from the committed per-VT PC stored in
            /// <see cref="ArchContexts"/> and advanced through retire authority.
            /// </summary>
            public ulong ReadActiveLivePc() => ulong_InstructionPointer;

            /// <summary>
            /// Redirect the live frontend PC for the currently active virtual thread
            /// without forcing a committed-PC retire.
            /// </summary>
            public void WriteActiveLivePc(ulong pc)
            {
                ulong_InstructionPointer = pc;
            }

            /// <summary>
            /// Read an architectural register for virtual thread <paramref name="vtId"/>.
            /// Reads the in-flight value from the Physical Register File via the rename map.
            /// Falls back to the committed architectural context when the rename map returns
            /// physical register 0 (the hardwired-zero sentinel used before first allocation).
            /// Blueprint Р’В§3 / Checklist P0: "arch РІвЂ вЂ™ rename РІвЂ вЂ™ phys" read path.
            /// </summary>
            /// <param name="vtId">Virtual thread index (0РІР‚вЂњSmtWays-1).</param>
            /// <param name="archReg">Architectural register index (0 = x0 hardwired zero).</param>
            public ulong ReadArch(int vtId, int archReg)
            {
                vtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(ReadArch));
                if (archReg == 0) return 0; // x0 hardwired zero
                if ((uint)archReg >= (uint)RenameMap.ArchRegs) return 0;
                int physReg = ArchRenameMap.Lookup(vtId, archReg);
                if (physReg == 0) // not yet renamed РІР‚вЂќ use committed mirror
                    return ArchContexts[vtId].CommittedRegs[archReg];
                return PhysicalRegisters.Read(physReg);
            }

            /// <summary>
            /// Explicit archРІвЂ вЂ™renameРІвЂ вЂ™phys read alias used by phase-04 cleanup work.
            /// </summary>
            public ulong ReadRenamed(int vtId, int archReg) => ReadArch(vtId, archReg);

            public ulong ReadCommittedPc(int vtId)
            {
                vtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(ReadCommittedPc));
                return ArchContexts[vtId].CommittedPc;
            }

            public PipelineState ReadActiveVirtualThreadPipelineState() =>
                ReadVirtualThreadPipelineState(ReadActiveVirtualThreadId());

            public PipelineState ReadVirtualThreadPipelineState(int vtId)
            {
                EnsureVirtualThreadPipelineStatesInitialized();
                vtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(ReadVirtualThreadPipelineState));
                return VirtualThreadPipelineStates[vtId];
            }

            /// <summary>
            /// Raw per-VT pipeline-state publication seam retained for reset/setup,
            /// snapshot restore, and core-owned helpers. Production transitions must
            /// flow through <see cref="ApplyVirtualThreadPipelineTransition(int, PipelineTransitionTrigger)"/>
            /// or publish an already-guarded snapshot through
            /// <see cref="PublishGuardedVirtualThreadPipelineState(int, PipelineState)"/>.
            /// </summary>
            public void WriteVirtualThreadPipelineState(int vtId, PipelineState state)
            {
                EnsureVirtualThreadPipelineStatesInitialized();
                vtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(WriteVirtualThreadPipelineState));
                VirtualThreadPipelineStates[vtId] = state;
            }

            public bool CanVirtualThreadIssueInForeground(int vtId)
            {
                return ReadVirtualThreadPipelineState(vtId) switch
                {
                    PipelineState.Task or PipelineState.GuestExecution => true,
                    _ => false
                };
            }

            public bool HasAnyVirtualThreadPipelineState(PipelineState state)
            {
                EnsureVirtualThreadPipelineStatesInitialized();
                for (int vt = 0; vt < VirtualThreadPipelineStates.Length; vt++)
                {
                    if (VirtualThreadPipelineStates[vt] == state)
                    {
                        return true;
                    }
                }

                return false;
            }

            internal bool HasActiveInterruptHandlerFrame() =>
                Interrupt_Callback_Addresses != null &&
                Interrupt_Callback_Addresses.Count != 0;

            internal void ApplyInterruptTransitionToVirtualThread(int vtId)
            {
                int normalizedVtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(ApplyInterruptTransitionToVirtualThread));
                PipelineState currentState = ReadVirtualThreadPipelineState(normalizedVtId);
                if (!PipelineFsmGuard.IsLegalTransition(currentState, PipelineTransitionTrigger.Interrupt))
                {
                    return;
                }

                ApplyVirtualThreadPipelineTransition(
                    normalizedVtId,
                    PipelineTransitionTrigger.Interrupt);
            }

            /// <summary>
            /// Commit an architectural register write for virtual thread <paramref name="vtId"/>.
            /// Emits a retire record and lets <see cref="RetireCoordinator"/> update the
            /// authoritative committed register state.
            /// </summary>
            /// <param name="vtId">Virtual thread index (0РІР‚вЂњSmtWays-1).</param>
            /// <param name="archReg">Architectural register index (0 = x0, writes silently dropped).</param>
            /// <param name="value">Value to commit.</param>
            public void WriteCommittedArch(int vtId, int archReg, ulong value)
            {
                int normalizedVtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(WriteCommittedArch));
                RetireCoordinator.Retire(RetireRecord.RegisterWrite(normalizedVtId, archReg, value));
            }

            internal void SeedCommittedArchForSetup(int vtId, int archReg, ulong value)
            {
                vtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(SeedCommittedArchForSetup));

                if ((uint)archReg >= (uint)RenameMap.ArchRegs)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(archReg),
                        archReg,
                        $"Architectural register must be in the range [0, {RenameMap.ArchRegs - 1}].");
                }

                if (archReg == 0)
                    return;

                ArchContexts[vtId].CommittedRegs[archReg] = value;

                int physReg = ArchRenameMap.Lookup(vtId, archReg);
                if (physReg != 0)
                    PhysicalRegisters.Write(physReg, value);

                ArchCommitMap.Commit(vtId, archReg, physReg == 0 ? archReg : physReg);
            }

            /// <summary>
            /// Commit a program-counter redirect for virtual thread <paramref name="vtId"/>.
            /// The committed per-VT PC is updated through the same retire path as GPR state.
            /// </summary>
            public void WriteCommittedPc(int vtId, ulong pc)
            {
                PublishVirtualThreadPcOwnership(
                    vtId,
                    pc,
                    retireWhenCommittedPcMatches: true);
            }

            internal void SeedCommittedPcForSetup(int vtId, ulong pc)
            {
                vtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(SeedCommittedPcForSetup));
                ArchContexts[vtId].CommittedPc = pc;
            }

            private void ResetFreshExecutionTransientState()
            {
                CoreFlagsRegister = new FlagsRegister(unchecked((ushort)CoreID));
                Core_FlagsRegisters_Stack?.Clear();
                Call_Callback_Addresses?.Clear();
                Interrupt_Callback_Addresses?.Clear();

                if (VirtualThreadStalled != null)
                    Array.Clear(VirtualThreadStalled, 0, VirtualThreadStalled.Length);

                if (ThreadFPContexts != null)
                {
                    for (int vt = 0; vt < ThreadFPContexts.Length; vt++)
                        ThreadFPContexts[vt].Clear();
                }

                InitializeVectorState();
                SavedVectorContext = default;
                Stalled = false;
                ResetVirtualThreadPipelineStates();
            }


            private void ResetFreshExecutionPipelineRuntimeState()
            {
                FlushPipeline();
                InitializePipeline();
                ResetCycleCounter();
            }

            /// <summary>
            /// Reset the fresh-execution transient state together with the per-VT
            /// committed and active live PC state.
            /// The active VT is normalized and all SMT lanes converge on the same entry PC.
            /// </summary>
            public void ResetExecutionStartPcState(ulong pc, int activeVtId = 0)
            {
                int resolvedActiveVtId = ResolveLiveStateVtIdOrThrow(activeVtId, nameof(ResetExecutionStartPcState));
                ResetFreshExecutionTransientState();
                ResetFreshExecutionPipelineRuntimeState();
                ActiveVirtualThreadId = resolvedActiveVtId;

                for (int vt = 0; vt < SmtWays; vt++)
                    SeedCommittedPcForSetup(vt, pc);

                WriteActiveLivePc(pc);
            }


            public void AdvanceActiveLivePc(ulong incrementValue)
            {
                ulong_InstructionPointer += incrementValue;
            }

            public void IncrementInstructionPointer(ulong incrementValue)
            {
                AdvanceActiveLivePc(incrementValue);
            }

            public void DecrementInstructionPointer(ulong decrementValue)
            {
                ulong_InstructionPointer -= decrementValue;
            }

            private int ResolveLiveStateVtIdOrThrow(int vtId, string operation) =>
                VirtualThreadIdResolver.ResolveOrThrow(vtId, SmtWays, operation);

            private void EnsureVirtualThreadPipelineStatesInitialized()
            {
                if (VirtualThreadPipelineStates != null &&
                    VirtualThreadPipelineStates.Length == SmtWays)
                {
                    return;
                }

                VirtualThreadPipelineStates = new PipelineState[SmtWays];
                for (int vt = 0; vt < SmtWays; vt++)
                {
                    VirtualThreadPipelineStates[vt] = PipelineState.Task;
                }
            }

            private void ResetVirtualThreadPipelineStates()
            {
                EnsureVirtualThreadPipelineStatesInitialized();
                for (int vt = 0; vt < VirtualThreadPipelineStates.Length; vt++)
                {
                    VirtualThreadPipelineStates[vt] = PipelineState.Task;
                }
            }

            StackMemory Stack;


            public StackMemory GetStackMemory()
            {
                return Stack;
            }

            public struct VirtualFlagsRegisters
            {
            }

            public struct ClockGen
            {
                public ulong Clock_Frequency;
            }
        }
    }
}
