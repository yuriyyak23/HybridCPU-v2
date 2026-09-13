namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Canonical VT-scoped architectural state surface used by execution, pipeline, and VMX paths.
    /// All register and PC reads/writes must flow through the VT-aware members on this contract.
    /// </summary>
    public interface ICanonicalCpuState
    {
        ulong GetVL();
        void SetVL(ulong vl);

        ulong GetVLMAX();

        byte GetSEW();
        void SetSEW(byte sew);

        byte GetLMUL();
        void SetLMUL(byte lmul);

        bool GetTailAgnostic();
        void SetTailAgnostic(bool agnostic);

        bool GetMaskAgnostic();
        void SetMaskAgnostic(bool agnostic);

        uint GetExceptionMask();
        void SetExceptionMask(uint mask);

        uint GetExceptionPriority();
        void SetExceptionPriority(uint priority);

        byte GetRoundingMode();
        void SetRoundingMode(byte mode);

        ulong GetOverflowCount();
        ulong GetUnderflowCount();
        ulong GetDivByZeroCount();
        ulong GetInvalidOpCount();
        ulong GetInexactCount();

        void ClearExceptionCounters();

        bool GetVectorDirty();
        void SetVectorDirty(bool dirty);

        bool GetVectorEnabled();
        void SetVectorEnabled(bool enabled);

        long ReadRegister(byte vtId, int regId);
        void WriteRegister(byte vtId, int regId, ulong value);

        ushort GetPredicateMask(ushort maskID);
        void SetPredicateMask(ushort maskID, ushort mask);

        ulong ReadPc(byte vtId);
        void WritePc(byte vtId, ulong pc);

        ushort GetCoreID();

        ulong GetCycleCount();
        ulong GetInstructionsRetired();
        double GetIPC();

        PipelineState GetCurrentPipelineState();
        void SetCurrentPipelineState(PipelineState state);
        void TransitionPipelineState(PipelineTransitionTrigger trigger);
    }

}
