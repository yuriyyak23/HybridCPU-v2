namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// FSP stealability policy for a slot in a VLIW bundle.
    /// Replaces the deprecated <c>MicroOp.CanBeStolen</c> field.
    /// Consumed exclusively by the scheduler and FSP — not an architectural observable.
    /// </summary>
    public enum StealabilityPolicy : byte
    {
        /// <summary>
        /// FSP may pilfer this slot if it is free.
        /// Default for all normal ALU, LSU, and non-privileged operations.
        /// </summary>
        Stealable = 0,

        /// <summary>
        /// FSP must not pilfer this slot.
        /// Required for privileged operations, control flow, and atomic sequences.
        /// </summary>
        NotStealable = 1,
    }
}
