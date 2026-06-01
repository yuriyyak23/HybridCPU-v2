using System;

namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class VectorStreamSnapshot
{
    public ushort[] SnapshotPredicateMasks()
    {
        ushort[] snapshot = new ushort[PredicateRegisterCount];
        Array.Copy(_predicateMasks, snapshot, snapshot.Length);
        return snapshot;
    }

    public void RestoreTo(ICanonicalCpuState state) => RestoreTo(state, SaveMask);

    public void RestoreTo(ICanonicalCpuState state, VectorStreamSaveMask restoreMask)
    {
        ArgumentNullException.ThrowIfNull(state);

        VectorStreamSaveMask effectiveMask = SaveMask & restoreMask;
        if ((effectiveMask & VectorStreamSaveMask.VectorConfig) != 0)
        {
            state.SetVL(VL);
            state.SetSEW(SEW);
            state.SetLMUL(LMUL);
            state.SetTailAgnostic(TailAgnostic);
            state.SetMaskAgnostic(MaskAgnostic);
            state.SetRoundingMode(RoundingMode);
            state.SetVectorEnabled(VectorEnabled);
        }

        if ((effectiveMask & VectorStreamSaveMask.ExceptionPolicy) != 0)
        {
            state.SetExceptionMask(ExceptionMask);
            state.SetExceptionPriority(ExceptionPriority);
        }

        if ((effectiveMask & VectorStreamSaveMask.ExceptionCounters) != 0)
        {
            state.ClearExceptionCounters();
            if (state is IVectorExceptionCounterState counterState)
            {
                counterState.SetVectorExceptionCounters(
                    OverflowCount,
                    UnderflowCount,
                    DivByZeroCount,
                    InvalidOpCount,
                    InexactCount);
            }
        }

        if ((effectiveMask & VectorStreamSaveMask.DirtyState) != 0)
        {
            state.SetVectorDirty(VectorDirty);
        }

        if ((effectiveMask & VectorStreamSaveMask.PredicateRegisters) != 0)
        {
            for (ushort index = 0; index < PredicateRegisterCount; index++)
            {
                state.SetPredicateMask(index, _predicateMasks[index]);
            }
        }
    }

}
