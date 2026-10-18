namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Memory locality hint for load/store instructions in a VLIW slot.
    /// Replaces the removed <c>HINT_STREAM</c> / <c>HINT_HOT</c> / <c>HINT_COLD</c>
    /// / <c>HINT_REUSE</c> opcodes.
    /// Consumed by the L1/L2 prefetch and eviction policy — not an architectural observable.
    /// </summary>
    public enum LocalityHint : byte
    {
        /// <summary>
        /// No hint — use the normal cache hierarchy.
        /// Default for all load/store slots.
        /// </summary>
        None = 0,

        /// <summary>
        /// Hot access — data is likely to be reused soon.
        /// Scheduler may prioritise this slot and retain the line in L1.
        /// Replaces <c>HINT_HOT</c> / <c>HINT_REUSE</c>.
        /// </summary>
        Hot = 1,

        /// <summary>
        /// Cold/streaming access — data is unlikely to be reused.
        /// Hardware may use a bypass or non-temporal path.
        /// Replaces <c>HINT_COLD</c> / <c>HINT_STREAM</c>.
        /// </summary>
        Cold = 2,
    }
}
