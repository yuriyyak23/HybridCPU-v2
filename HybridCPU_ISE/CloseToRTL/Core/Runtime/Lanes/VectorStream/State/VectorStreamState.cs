using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core;

[Flags]
public enum VectorStreamSaveMask : ulong
{
    None = 0,
    VectorConfig = 1UL << 0,
    ExceptionPolicy = 1UL << 1,
    ExceptionCounters = 1UL << 2,
    PredicateRegisters = 1UL << 3,
    DirtyState = 1UL << 4,
    StreamPolicy = 1UL << 5,
    Architectural =
        VectorConfig |
        ExceptionPolicy |
        ExceptionCounters |
        PredicateRegisters |
        DirtyState,
    All = Architectural | StreamPolicy,
}

public enum VectorExceptionAction : byte
{
    Accumulate = 0,
    Inject = 1,
    ReflectAsCompatibilityExit = 2,
}

public enum StreamDescriptorFaultKind : byte
{
    None = 0,
    VirtualizationDisabled = 1,
    DescriptorTableUnbound = 2,
    AddressOutOfRange = 3,
    InvalidLength = 4,
    UnsupportedOpcode = 5,
    StreamLengthTooLarge = 6,
    ValidationEvidenceMissing = 7,
    ValidationEvidenceStale = 8,
    DescriptorDecodeFault = 9,
    DescriptorMemoryReadFailed = 10,
    OwnerMismatch = 11,
}

public enum StreamReplayReason : byte
{
    None = 0,
    StreamReplayEpochChanged = 1,
    DescriptorValidationStale = 2,
    DirtyStateRequiresSave = 3,
}

public interface IVectorExceptionCounterState
{
    void SetVectorExceptionCounters(
        ulong overflowCount,
        ulong underflowCount,
        ulong divByZeroCount,
        ulong invalidOpCount,
        ulong inexactCount);
}

public sealed partial class VectorStreamSnapshot
{
    public const int PredicateRegisterCount = 16;

    private readonly ushort[] _predicateMasks;

    public VectorStreamSnapshot(
        ulong vl,
        byte sew,
        byte lmul,
        bool tailAgnostic,
        bool maskAgnostic,
        uint exceptionMask,
        uint exceptionPriority,
        byte roundingMode,
        ulong overflowCount,
        ulong underflowCount,
        ulong divByZeroCount,
        ulong invalidOpCount,
        ulong inexactCount,
        bool vectorDirty,
        bool vectorEnabled,
        ushort[] predicateMasks,
        ulong streamDescriptorTableBase,
        ulong streamDescriptorTableLimit,
        ulong streamReplayEpoch,
        ulong streamQueueEpoch,
        ulong streamCompletionEpoch,
        VectorStreamSaveMask saveMask,
        ulong architecturalEpoch)
    {
        ArgumentNullException.ThrowIfNull(predicateMasks);
        if (predicateMasks.Length != PredicateRegisterCount)
        {
            throw new ArgumentException(
                "Vector/stream snapshot requires exactly 16 predicate masks.",
                nameof(predicateMasks));
        }

        VL = vl;
        SEW = sew;
        LMUL = lmul;
        TailAgnostic = tailAgnostic;
        MaskAgnostic = maskAgnostic;
        ExceptionMask = exceptionMask;
        ExceptionPriority = exceptionPriority;
        RoundingMode = roundingMode;
        OverflowCount = overflowCount;
        UnderflowCount = underflowCount;
        DivByZeroCount = divByZeroCount;
        InvalidOpCount = invalidOpCount;
        InexactCount = inexactCount;
        VectorDirty = vectorDirty;
        VectorEnabled = vectorEnabled;
        _predicateMasks = new ushort[PredicateRegisterCount];
        Array.Copy(predicateMasks, _predicateMasks, PredicateRegisterCount);
        StreamDescriptorTableBase = streamDescriptorTableBase;
        StreamDescriptorTableLimit = streamDescriptorTableLimit;
        StreamReplayEpoch = streamReplayEpoch;
        StreamQueueEpoch = streamQueueEpoch;
        StreamCompletionEpoch = streamCompletionEpoch;
        SaveMask = saveMask;
        ArchitecturalEpoch = architecturalEpoch;
    }

    public ulong VL { get; }

    public byte SEW { get; }

    public byte LMUL { get; }

    public bool TailAgnostic { get; }

    public bool MaskAgnostic { get; }

    public uint ExceptionMask { get; }

    public uint ExceptionPriority { get; }

    public byte RoundingMode { get; }

    public ulong OverflowCount { get; }

    public ulong UnderflowCount { get; }

    public ulong DivByZeroCount { get; }

    public ulong InvalidOpCount { get; }

    public ulong InexactCount { get; }

    public bool VectorDirty { get; }

    public bool VectorEnabled { get; }

    public IReadOnlyList<ushort> PredicateMasks => _predicateMasks;

    public ulong StreamDescriptorTableBase { get; }

    public ulong StreamDescriptorTableLimit { get; }

    public ulong StreamReplayEpoch { get; }

    public ulong StreamQueueEpoch { get; }

    public ulong StreamCompletionEpoch { get; }

    public VectorStreamSaveMask SaveMask { get; }

    public ulong ArchitecturalEpoch { get; }

    public static VectorStreamSnapshot Capture(
        ICanonicalCpuState state,
        ulong streamDescriptorTableBase,
        ulong streamDescriptorTableLimit,
        ulong streamReplayEpoch,
        ulong streamQueueEpoch,
        ulong streamCompletionEpoch,
        VectorStreamSaveMask saveMask,
        ulong architecturalEpoch)
    {
        ArgumentNullException.ThrowIfNull(state);

        ushort[] predicateMasks = new ushort[PredicateRegisterCount];
        for (ushort index = 0; index < PredicateRegisterCount; index++)
        {
            predicateMasks[index] = state.GetPredicateMask(index);
        }

        return new VectorStreamSnapshot(
            state.GetVL(),
            state.GetSEW(),
            state.GetLMUL(),
            state.GetTailAgnostic(),
            state.GetMaskAgnostic(),
            state.GetExceptionMask(),
            state.GetExceptionPriority(),
            state.GetRoundingMode(),
            state.GetOverflowCount(),
            state.GetUnderflowCount(),
            state.GetDivByZeroCount(),
            state.GetInvalidOpCount(),
            state.GetInexactCount(),
            state.GetVectorDirty(),
            state.GetVectorEnabled(),
            predicateMasks,
            streamDescriptorTableBase,
            streamDescriptorTableLimit,
            streamReplayEpoch,
            streamQueueEpoch,
            streamCompletionEpoch,
            saveMask,
            architecturalEpoch);
    }

    public override string ToString() =>
        $"Vector/stream snapshot: vl={VL}, sew={SEW}, lmul={LMUL}, predicates={_predicateMasks.Length}, streamEpoch={StreamReplayEpoch}";
}
