using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Compat;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Materialises a VT-scoped live adapter from the current core state.
            /// The active VT observes the live front-end instruction pointer; inactive
            /// VTs fall back to their committed per-VT PC snapshot.
            /// </summary>
            public LiveCpuStateAdapter CreateLiveCpuStateAdapter(int vtId)
            {
                int normalizedVtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(CreateLiveCpuStateAdapter));
                ulong instructionPointer = normalizedVtId == ReadActiveVirtualThreadId()
                    ? ReadActiveLivePc()
                    : ReadCommittedPc(normalizedVtId);

                return new LiveCpuStateAdapter(
                    ArchContexts,
                    PhysicalRegisters,
                    ArchRenameMap,
                    RetireCoordinator,
                    normalizedVtId,
                    CoreID,
                    instructionPointer,
                    ReadVirtualThreadPipelineState(normalizedVtId),
                    VectorConfig,
                    ExceptionStatus,
                    GetAllPredicateRegisters(),
                    pipeCtrl.CycleCount,
                    pipeCtrl.InstructionsRetired);
            }

            /// <summary>
            /// Explicit compat-only adapter that binds legacy non-VT calls to a selected VT.
            /// Production code should continue to use <see cref="CreateLiveCpuStateAdapter(int)"/>.
            /// </summary>
            public LegacyCpuStateAdapter CreateLegacyCpuStateAdapter(int vtId)
            {
                int normalizedVtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(CreateLegacyCpuStateAdapter));
                return new LegacyCpuStateAdapter(CreateLiveCpuStateAdapter(normalizedVtId), (byte)normalizedVtId);
            }

            /// <summary>
            /// Scoped live-state adapter over <see cref="CPU_Core"/> that provides the
            /// canonical VT-scoped state surface expected by execution helpers.
            ///
            /// Architectural register and committed-PC writes retire through
            /// <see cref="RetireCoordinator"/>, while transient core-owned state
            /// (live frontend PC/FSM/vector/predicate state) is written back
            /// explicitly via <see cref="ApplyTo(ref CPU_Core)"/>.
            /// </summary>
            public class LiveCpuStateAdapter : ICanonicalCpuState
            {
                private const int PredicateRegisterCount = 16;

                private readonly ArchContextState[]   _archContexts;
                private readonly PhysicalRegisterFile _physicalRegisters;
                private readonly RenameMap            _renameMap;
                private readonly RetireCoordinator    _retireCoordinator;
                private readonly int                  _vtId;
                private readonly ushort               _coreId;
                private readonly ulong                _cycleCount;
                private readonly ulong                _instructionsRetired;
                private readonly ulong[]              _predicateRegisters;

                private ulong                 _instructionPointer;
                private RVV_Config            _vectorConfig;
                private VectorExceptionStatus _exceptionStatus;
                private bool                  _hasExplicitPcWrite;
                private int                   _pcWriteVtId;
                private ulong                 _pcWriteValue;

                internal LiveCpuStateAdapter(
                    ArchContextState[] archContexts,
                    PhysicalRegisterFile physicalRegisters,
                    RenameMap renameMap,
                    RetireCoordinator retireCoordinator,
                    int vtId,
                    uint coreId,
                    ulong instructionPointer,
                    PipelineState pipelineState,
                    RVV_Config? vectorConfig = null,
                    VectorExceptionStatus? exceptionStatus = null,
                    ulong[]? predicateRegisters = null,
                    ulong cycleCount = 0,
                    ulong instructionsRetired = 0)
                {
                    _archContexts = archContexts ?? throw new ArgumentNullException(nameof(archContexts));
                    _physicalRegisters = physicalRegisters ?? throw new ArgumentNullException(nameof(physicalRegisters));
                    _renameMap = renameMap ?? throw new ArgumentNullException(nameof(renameMap));
                    _retireCoordinator = retireCoordinator ?? throw new ArgumentNullException(nameof(retireCoordinator));
                    _vtId = ResolveVtIdOrThrow(vtId, nameof(LiveCpuStateAdapter));
                    _coreId = unchecked((ushort)coreId);
                    _instructionPointer = instructionPointer;
                    PipelineState = pipelineState;
                    _vectorConfig = vectorConfig ?? default;
                    _exceptionStatus = exceptionStatus ?? default;
                    _predicateRegisters = ClonePredicateRegisters(predicateRegisters);
                    _cycleCount = cycleCount;
                    _instructionsRetired = instructionsRetired;
                    _pcWriteVtId = _vtId;
                }

                public PipelineState PipelineState { get; set; }

                /// <summary>
                /// Applies deferred core-owned state updates back into the live core.
                /// Architectural register writes and committed-PC publication retire
                /// through <see cref="RetireCoordinator"/>; only transient core-owned
                /// state is written directly here.
                /// </summary>
                public void ApplyTo(ref CPU_Core core)
                {
                    core.PublishGuardedVirtualThreadPipelineState(_vtId, PipelineState);
                    core.VectorConfig = _vectorConfig;
                    core.ExceptionStatus = _exceptionStatus;

                    for (int i = 0; i < PredicateRegisterCount; i++)
                        core.SetPredicateRegister(i, _predicateRegisters[i]);

                    PublishExplicitPcWrite(ref core);
                }

                public long ReadRegister(byte vtId, int regId) =>
                    unchecked((long)ReadArchitecturalRegister(vtId, regId));

                public void WriteRegister(byte vtId, int regId, ulong value)
                {
                    if (regId == 0)
                        return;

                    int normalizedVtId = ResolveVtIdOrThrow(vtId, nameof(WriteRegister));
                    _retireCoordinator.Retire(
                        RetireRecord.RegisterWrite(normalizedVtId, unchecked((ushort)regId), value));
                }

                public ulong ReadPc(byte vtId)
                {
                    int normalizedVtId = ResolveVtIdOrThrow(vtId, nameof(ReadPc));
                    if (_hasExplicitPcWrite && normalizedVtId == _pcWriteVtId)
                    {
                        return _pcWriteValue;
                    }

                    return normalizedVtId == _vtId
                        ? _instructionPointer
                        : ReadCommittedPcSnapshot(normalizedVtId);
                }

                public void WritePc(byte vtId, ulong pc)
                {
                    int normalizedVtId = ResolveVtIdOrThrow(vtId, nameof(WritePc));
                    _hasExplicitPcWrite = true;
                    _pcWriteVtId = normalizedVtId;
                    _pcWriteValue = pc;
                    if (normalizedVtId == _vtId)
                    {
                        _instructionPointer = pc;
                    }
                }

                public ushort GetCoreID() => _coreId;

                public ulong GetCycleCount() => _cycleCount;

                public ulong GetInstructionsRetired() => _instructionsRetired;

                public double GetIPC() =>
                    _cycleCount == 0 ? 0.0 : (double)_instructionsRetired / _cycleCount;

                public PipelineState GetCurrentPipelineState() => PipelineState;

                public void SetCurrentPipelineState(PipelineState state) => PipelineState = state;

                public void TransitionPipelineState(PipelineTransitionTrigger trigger) =>
                    PipelineState = PipelineFsmGuard.Transition(PipelineState, trigger);

                public ulong GetVL() => _vectorConfig.VL;

                public void SetVL(ulong vl) => _vectorConfig.VL = vl;

                public ulong GetVLMAX() => RVV_Config.VLMAX;

                public byte GetSEW() => (byte)((_vectorConfig.VTYPE >> 3) & 0x7);

                public void SetSEW(byte sew) =>
                    _vectorConfig.VTYPE = (_vectorConfig.VTYPE & ~(0x7UL << 3)) | (((ulong)sew & 0x7) << 3);

                public byte GetLMUL() => (byte)(_vectorConfig.VTYPE & 0x7);

                public void SetLMUL(byte lmul) =>
                    _vectorConfig.VTYPE = (_vectorConfig.VTYPE & ~0x7UL) | ((ulong)lmul & 0x7);

                public bool GetTailAgnostic() => _vectorConfig.TailAgnostic != 0;

                public void SetTailAgnostic(bool agnostic) =>
                    _vectorConfig.TailAgnostic = agnostic ? (byte)1 : (byte)0;

                public bool GetMaskAgnostic() => _vectorConfig.MaskAgnostic != 0;

                public void SetMaskAgnostic(bool agnostic) =>
                    _vectorConfig.MaskAgnostic = agnostic ? (byte)1 : (byte)0;

                public uint GetExceptionMask() => _exceptionStatus.GetMask();

                public void SetExceptionMask(uint mask) => _exceptionStatus.SetMask((byte)mask);

                public uint GetExceptionPriority()
                {
                    uint packed = 0;
                    for (int i = 0; i < 5; i++)
                        packed |= (uint)(_exceptionStatus.GetPriority(i) & 0x7) << (i * 3);

                    return packed;
                }

                public void SetExceptionPriority(uint priority)
                {
                    for (int i = 0; i < 5; i++)
                        _exceptionStatus.SetPriority(i, (byte)((priority >> (i * 3)) & 0x7));
                }

                public byte GetRoundingMode() => _exceptionStatus.RoundingMode;

                public void SetRoundingMode(byte mode) => _exceptionStatus.SetRoundingMode(mode);

                public ulong GetOverflowCount() => _exceptionStatus.OverflowCount;

                public ulong GetUnderflowCount() => _exceptionStatus.UnderflowCount;

                public ulong GetDivByZeroCount() => _exceptionStatus.DivByZeroCount;

                public ulong GetInvalidOpCount() => _exceptionStatus.InvalidOpCount;

                public ulong GetInexactCount() => _exceptionStatus.InexactCount;

                public void ClearExceptionCounters() => _exceptionStatus.ClearExceptionCounters();

                public bool GetVectorDirty() => _exceptionStatus.VectorDirty != 0;

                public void SetVectorDirty(bool dirty) =>
                    _exceptionStatus.VectorDirty = dirty ? (byte)1 : (byte)0;

                public bool GetVectorEnabled() => _exceptionStatus.VectorEnabled != 0;

                public void SetVectorEnabled(bool enabled) =>
                    _exceptionStatus.VectorEnabled = enabled ? (byte)1 : (byte)0;

                public ushort GetPredicateMask(ushort maskID)
                {
                    if ((uint)maskID >= (uint)_predicateRegisters.Length)
                        return 0;

                    return unchecked((ushort)(_predicateRegisters[maskID] & 0xFFFF));
                }

                public void SetPredicateMask(ushort maskID, ushort mask)
                {
                    if ((uint)maskID >= (uint)_predicateRegisters.Length)
                        return;

                    _predicateRegisters[maskID] = (_predicateRegisters[maskID] & ~0xFFFFUL) | mask;
                }

                private ulong ReadArchitecturalRegister(int vtId, int regId)
                {
                    int normalizedVtId = ResolveVtIdOrThrow(vtId, nameof(ReadRegister));
                    if (regId == 0)
                        return 0;

                    if ((uint)regId >= (uint)RenameMap.ArchRegs)
                        return 0;

                    int physReg = _renameMap.Lookup(normalizedVtId, regId);
                    if (physReg != 0)
                        return _physicalRegisters.Read(physReg);

                    return _archContexts[normalizedVtId].CommittedRegs[regId];
                }

                private ulong ReadCommittedPcSnapshot(int vtId)
                {
                    return _archContexts[ResolveVtIdOrThrow(vtId, nameof(ReadPc))].CommittedPc;
                }

                private int ResolveVtIdOrThrow(int vtId, string operation) =>
                    VirtualThreadIdResolver.ResolveOrThrow(vtId, _archContexts.Length, operation);

                private static ulong[] ClonePredicateRegisters(ulong[]? predicateRegisters)
                {
                    ulong[] snapshot = CreateDefaultPredicateRegisters();
                    if (predicateRegisters == null)
                        return snapshot;

                    int copyLength = Math.Min(snapshot.Length, predicateRegisters.Length);
                    Array.Copy(predicateRegisters, snapshot, copyLength);
                    return snapshot;
                }

                private static ulong[] CreateDefaultPredicateRegisters()
                {
                    ulong[] registers = new ulong[PredicateRegisterCount];
                    Array.Fill(registers, ulong.MaxValue);
                    return registers;
                }

                private void PublishExplicitPcWrite(ref CPU_Core core)
                {
                    if (!_hasExplicitPcWrite)
                    {
                        return;
                    }

                    core.PublishLiveAdapterPcWriteback(_pcWriteVtId, _pcWriteValue);
                    _hasExplicitPcWrite = false;
                }
            }

        }
    }
}
