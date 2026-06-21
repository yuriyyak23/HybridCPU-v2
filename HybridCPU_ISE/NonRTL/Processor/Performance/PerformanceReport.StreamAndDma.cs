using HybridCPU_ISE.Core;
using System.Text;

namespace YAKSys_Hybrid_CPU
{
    public partial class PerformanceReport
    {
        #region Stream Register File Statistics (Q1 Review §5)

        /// <summary>
        /// L1 bypass hits: stream data served from SRF (RegisterState.Valid)
        /// without consuming a memory port. Proves memory ports are not saturated
        /// thanks to the internal prefetch buffer.
        /// </summary>
        public long L1BypassHits { get; set; }

        /// <summary>
        /// Foreground (non-assist) ingress warm attempts issued through the SRF seam.
        /// </summary>
        public long ForegroundWarmAttempts { get; set; }

        /// <summary>
        /// Foreground ingress warm attempts that successfully left reusable SRF state behind.
        /// </summary>
        public long ForegroundWarmSuccesses { get; set; }

        /// <summary>
        /// Foreground warm attempts that reused an already-valid SRF window.
        /// </summary>
        public long ForegroundWarmReuseHits { get; set; }

        /// <summary>
        /// Foreground execution reads that were served from SRF rather than backend memory.
        /// </summary>
        public long ForegroundBypassHits { get; set; }

        /// <summary>
        /// Assist-driven lane6/DMA ingress warm attempts issued through the SRF seam.
        /// </summary>
        public long AssistWarmAttempts { get; set; }

        /// <summary>
        /// Assist-driven ingress warm attempts that successfully published reusable SRF state.
        /// </summary>
        public long AssistWarmSuccesses { get; set; }

        /// <summary>
        /// Assist-driven warm attempts that found an already-valid SRF window.
        /// </summary>
        public long AssistWarmReuseHits { get; set; }

        /// <summary>
        /// Assist-originated execution reads that were served from SRF rather than backend memory.
        /// </summary>
        public long AssistBypassHits { get; set; }

        /// <summary>
        /// Warm attempts rejected by translation/domain/permission truth before backend ingress.
        /// </summary>
        public long StreamWarmTranslationRejects { get; set; }

        /// <summary>
        /// Warm attempts that passed translation but could not materialize ingress/backend state.
        /// </summary>
        public long StreamWarmBackendRejects { get; set; }

        /// <summary>
        /// Assist warm attempts rejected because the assist SRF resident budget was exhausted.
        /// </summary>
        public long AssistWarmResidentBudgetRejects { get; set; }

        /// <summary>
        /// Assist warm attempts rejected because the assist SRF loading budget was exhausted.
        /// </summary>
        public long AssistWarmLoadingBudgetRejects { get; set; }

        /// <summary>
        /// Assist warm attempts rejected because no assist-owned SRF victim was available.
        /// </summary>
        public long AssistWarmNoVictimRejects { get; set; }

        #endregion

        #region DMA Controller Statistics

        /// <summary>
        /// Active DMA transfers
        /// </summary>
        public int ActiveDmaTransfers { get; set; }

        /// <summary>
        /// Completed DMA transfers
        /// </summary>
        public long CompletedDmaTransfers { get; set; }

        /// <summary>
        /// Total DMA transfer latency in cycles
        /// </summary>
        public long TotalDmaLatency { get; set; }

        /// <summary>
        /// DMA errors
        /// </summary>
        public long DmaErrors { get; set; }

        /// <summary>
        /// DMA bytes transferred
        /// </summary>
        public long DmaBytesTransferred { get; set; }

        #endregion
    }
}
