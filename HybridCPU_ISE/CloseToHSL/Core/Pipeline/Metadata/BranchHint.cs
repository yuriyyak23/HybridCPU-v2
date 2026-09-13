namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Branch prediction hint for control-flow instructions in a VLIW slot.
    /// Replaces the removed <c>HINT_LIKELY</c> / <c>HINT_UNLIKELY</c> opcodes.
    /// Consumed by the branch predictor frontend — not an architectural observable.
    /// </summary>
    public enum BranchHint : byte
    {
        /// <summary>
        /// No hint — branch predictor uses history tables only.
        /// Default for all control-flow slots.
        /// </summary>
        None = 0,

        /// <summary>
        /// Branch is likely taken.
        /// Frontend biases speculative fetch toward the taken path.
        /// </summary>
        Likely = 1,

        /// <summary>
        /// Branch is unlikely taken.
        /// Frontend biases speculative fetch toward the fall-through path.
        /// </summary>
        Unlikely = 2,
    }
}
