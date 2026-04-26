namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Canonical CSR routing helpers for core-local power-control state.
    /// </summary>
    public static class PowerControlCsr
    {
        public static bool IsPerformanceState(Processor.CPU_Core.CorePowerState state) =>
            state >= Processor.CPU_Core.CorePowerState.P0_MaxPerformance &&
            state <= Processor.CPU_Core.CorePowerState.Pn_MinPerformance;

        public static ushort ResolveTargetCsr(Processor.CPU_Core.CorePowerState state) =>
            IsPerformanceState(state)
                ? CsrAddresses.MperfLevel
                : CsrAddresses.MpowerState;

        public static ulong EncodeState(Processor.CPU_Core.CorePowerState state) =>
            (ulong)(uint)state;

        public static Processor.CPU_Core.CorePowerState DecodePowerState(ulong rawValue)
        {
            return rawValue switch
            {
                (ulong)Processor.CPU_Core.CorePowerState.C0_Active => Processor.CPU_Core.CorePowerState.C0_Active,
                (ulong)Processor.CPU_Core.CorePowerState.C1_Halt => Processor.CPU_Core.CorePowerState.C1_Halt,
                (ulong)Processor.CPU_Core.CorePowerState.C2_StopClock => Processor.CPU_Core.CorePowerState.C2_StopClock,
                (ulong)Processor.CPU_Core.CorePowerState.C3_Sleep => Processor.CPU_Core.CorePowerState.C3_Sleep,
                (ulong)Processor.CPU_Core.CorePowerState.C6_DeepPowerDown => Processor.CPU_Core.CorePowerState.C6_DeepPowerDown,
                _ => Processor.CPU_Core.CorePowerState.ErrorState
            };
        }

        public static uint DecodePerformanceLevel(ulong rawValue) => unchecked((uint)rawValue);
    }
}
