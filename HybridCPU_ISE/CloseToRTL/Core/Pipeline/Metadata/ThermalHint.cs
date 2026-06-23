namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Thermal / power hint for an entire VLIW bundle.
    /// Consumed by the power management unit to guide frequency scaling decisions.
    /// Not an architectural observable — has no effect on instruction semantics.
    /// </summary>
    public enum ThermalHint : byte
    {
        /// <summary>
        /// No thermal hint — run at nominal frequency.
        /// Default for all bundles.
        /// </summary>
        None = 0,

        /// <summary>
        /// High-intensity bundle — may request a temporary frequency boost.
        /// </summary>
        Boost = 1,

        /// <summary>
        /// Low-intensity bundle — may allow a frequency reduction to save power.
        /// </summary>
        Throttle = 2,
    }
}
