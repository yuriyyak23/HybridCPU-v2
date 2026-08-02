// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v7 — Domain Tag Manager
// Blueprint §6.100 (New Types): DomainTagManager
// ─────────────────────────────────────────────────────────────────────────────

using System.Threading;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Issues unique per-transaction domain tags for FSP security and ordering.
    /// <para>
    /// Each <c>DomainTag</c> is a monotonically increasing <see cref="ulong"/> token
    /// stamped on a <see cref="Pipeline.MicroOps.MicroOp"/> at decode time.
    /// The FSP scheduler uses these tags to detect cross-domain hazards and to
    /// prevent stolen micro-ops from one security domain from committing in the
    /// context of another.
    /// </para>
    /// <para>
    /// <b>Thread safety:</b> <see cref="Issue"/> uses a lock-free atomic increment
    /// so that multiple hardware threads can obtain tags in parallel without
    /// contention.  Tag zero (0) is reserved as "untagged" / "no domain" and is
    /// never returned by <see cref="Issue"/>.
    /// </para>
    /// Blueprint §6.100: "DomainTagManager: (для безопасности) класс для выдачи
    /// DomainTag уникальных при транзакциях."
    /// </summary>
    public sealed class DomainTagManager
    {
        // ── Singleton ──────────────────────────────────────────────────────────

        /// <summary>Global singleton for designs that share one tag space.</summary>
        public static readonly DomainTagManager Global = new DomainTagManager();

        // ── Tag counter — starts at 1 so 0 is always "no tag" ─────────────────

        private long _counter = 0;

        // ── API ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reserved sentinel value meaning "this micro-op carries no domain tag".
        /// Never returned by <see cref="Issue"/>.
        /// </summary>
        public const ulong NoTag = 0UL;

        /// <summary>
        /// Issues the next unique domain tag.
        /// </summary>
        /// <returns>
        /// A non-zero <see cref="ulong"/> that uniquely identifies the current
        /// transaction / instruction-decode window.
        /// </returns>
        public ulong Issue()
        {
            // Atomic post-increment. Wrap-around at ulong.MaxValue+1 would produce 0,
            // which is the reserved NoTag sentinel. Skip it to maintain the invariant
            // that Issue() never returns 0.
            ulong tag;
            do
            {
                tag = (ulong)Interlocked.Increment(ref _counter);
            }
            while (tag == NoTag);
            return tag;
        }

        /// <summary>
        /// Resets the tag counter back to zero (useful for deterministic tests).
        /// Must not be called while instruction decode is in progress.
        /// </summary>
        public void Reset() => Interlocked.Exchange(ref _counter, 0);
    }
}
