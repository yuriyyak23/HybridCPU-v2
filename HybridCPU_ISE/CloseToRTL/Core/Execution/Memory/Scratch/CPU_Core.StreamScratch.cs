namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Pre-allocated scratch buffers for stream operations.
            /// Avoids dynamic allocation in execution loop (HLS requirement).
            ///
            /// Design:
            /// - Size = VLMAX * MaxElementSize = 32 * 8 = 256 bytes each
            /// - ScratchA: first operand buffer
            /// - ScratchB: second operand buffer
            /// - ScratchDst: result buffer
            /// - Allocated once during core initialization
            /// </summary>

            /// <summary>
            /// Maximum vector length per strip-mine iteration.
            /// Matches RVV_Config.VLMAX constant.
            /// </summary>
            private const int VLMAX = 32;

            /// <summary>
            /// Maximum element size in bytes (FLOAT64/INT64/UINT64 = 8 bytes).
            /// </summary>
            private const int MAX_ELEMENT_SIZE = 8;

            /// <summary>
            /// Scratch buffer size: VLMAX * MAX_ELEMENT_SIZE = 256 bytes.
            /// </summary>
            private const int SCRATCH_BUFFER_SIZE = VLMAX * MAX_ELEMENT_SIZE;

            /// <summary>
            /// Scratch buffer for first source operand.
            /// Used in read-compute-write cycles.
            /// </summary>
            private byte[] ScratchA;

            /// <summary>
            /// Scratch buffer for second source operand.
            /// Used in binary operations.
            /// </summary>
            private byte[] ScratchB;

            /// <summary>
            /// Scratch buffer for destination/result.
            /// Used to accumulate results before writeback.
            /// </summary>
            private byte[] ScratchDst;

            // ===== Banked Scratch Controllers (Plan 07: Crossbar Banking) =====

            /// <summary>
            /// Banked scratch controller for operand A (4-bank interleaved, crossbar interconnect).
            /// Provides cycle-accurate bank conflict detection for HLS timing simulation.
            /// </summary>
            private Core.ScratchBankController BankedScratchA;

            /// <summary>
            /// Banked scratch controller for operand B.
            /// </summary>
            private Core.ScratchBankController BankedScratchB;

            /// <summary>
            /// Banked scratch controller for destination/result.
            /// </summary>
            private Core.ScratchBankController BankedScratchDst;

            /// <summary>
            /// Scratch buffer for index array.
            /// Used in indexed/gather-scatter operations.
            /// Maximum size: VLMAX * 8 (for uint64 indices) = 256 bytes.
            /// </summary>
            private byte[] ScratchIndex;

            // Double buffering support (Iteration 6)
            // Allows overlapping memory access with computation
            // While one buffer is being computed, the other can be loaded/stored

            /// <summary>
            /// Double buffer set 0 for ScratchA/B (active during even iterations)
            /// </summary>
            private byte[] ScratchA_DB0, ScratchB_DB0;

            /// <summary>
            /// Double buffer set 1 for ScratchA/B (active during odd iterations)
            /// </summary>
            private byte[] ScratchA_DB1, ScratchB_DB1;

            /// <summary>
            /// Double buffer sets for ScratchDst (result buffer)
            /// </summary>
            private byte[] ScratchDst_DB0, ScratchDst_DB1;

            /// <summary>
            /// Current active buffer set (0 or 1)
            /// Toggles between iterations to enable double buffering
            /// </summary>
            private int ActiveBufferSet = 0;

            /// <summary>
            /// Initialize scratch buffers during core initialization.
            /// Called from CPU_Core constructor/init path.
            /// </summary>
            private void InitializeScratchBuffers()
            {
                ActiveBufferSet = 0;

                // Single buffers (for non-pipelined operations)
                ScratchA = new byte[SCRATCH_BUFFER_SIZE];
                ScratchB = new byte[SCRATCH_BUFFER_SIZE];
                ScratchDst = new byte[SCRATCH_BUFFER_SIZE];
                ScratchIndex = new byte[SCRATCH_BUFFER_SIZE]; // For uint64 indices

                // Double buffers (for pipelined operations)
                ScratchA_DB0 = new byte[SCRATCH_BUFFER_SIZE];
                ScratchB_DB0 = new byte[SCRATCH_BUFFER_SIZE];
                ScratchA_DB1 = new byte[SCRATCH_BUFFER_SIZE];
                ScratchB_DB1 = new byte[SCRATCH_BUFFER_SIZE];
                ScratchDst_DB0 = new byte[SCRATCH_BUFFER_SIZE];
                ScratchDst_DB1 = new byte[SCRATCH_BUFFER_SIZE];

                // Banked controllers (Plan 07: crossbar banking)
                BankedScratchA.Initialize();
                BankedScratchB.Initialize();
                BankedScratchDst.Initialize();
            }

            /// <summary>
            /// Get scratch buffer A as Span for zero-copy operations.
            /// </summary>
            public System.Span<byte> GetScratchA() => ScratchA;

            /// <summary>
            /// Get scratch buffer B as Span for zero-copy operations.
            /// </summary>
            public System.Span<byte> GetScratchB() => ScratchB;

            /// <summary>
            /// Get scratch buffer Dst as Span for zero-copy operations.
            /// </summary>
            public System.Span<byte> GetScratchDst() => ScratchDst;

            /// <summary>
            /// Get scratch buffer Index as Span for zero-copy operations.
            /// Used for gather/scatter index arrays.
            /// </summary>
            public System.Span<byte> GetScratchIndex() => ScratchIndex;

            /// <summary>
            /// Toggle active buffer set for double buffering.
            /// Should be called after completing one iteration of pipelined stream operation.
            /// </summary>
            public void ToggleDoubleBuffer()
            {
                ActiveBufferSet = 1 - ActiveBufferSet; // Toggle between 0 and 1
            }

            /// <summary>
            /// Get active scratch buffer A for double buffering.
            /// Returns DB0 if ActiveBufferSet=0, DB1 if ActiveBufferSet=1.
            /// </summary>
            public System.Span<byte> GetActiveScratchA()
            {
                return ActiveBufferSet == 0 ? ScratchA_DB0 : ScratchA_DB1;
            }

            /// <summary>
            /// Get active scratch buffer B for double buffering.
            /// Returns DB0 if ActiveBufferSet=0, DB1 if ActiveBufferSet=1.
            /// </summary>
            public System.Span<byte> GetActiveScratchB()
            {
                return ActiveBufferSet == 0 ? ScratchB_DB0 : ScratchB_DB1;
            }

            /// <summary>
            /// Get inactive scratch buffer A for double buffering.
            /// While computation uses active buffer, memory can prefetch into inactive buffer.
            /// </summary>
            public System.Span<byte> GetInactiveScratchA()
            {
                return ActiveBufferSet == 0 ? ScratchA_DB1 : ScratchA_DB0;
            }

            /// <summary>
            /// Get inactive scratch buffer B for double buffering.
            /// While computation uses active buffer, memory can prefetch into inactive buffer.
            /// </summary>
            public System.Span<byte> GetInactiveScratchB()
            {
                return ActiveBufferSet == 0 ? ScratchB_DB1 : ScratchB_DB0;
            }

            /// <summary>
            /// Get active scratch buffer Dst for double buffering.
            /// </summary>
            public System.Span<byte> GetActiveScratchDst()
            {
                return ActiveBufferSet == 0 ? ScratchDst_DB0 : ScratchDst_DB1;
            }

            /// <summary>
            /// Get inactive scratch buffer Dst for double buffering.
            /// </summary>
            public System.Span<byte> GetInactiveScratchDst()
            {
                return ActiveBufferSet == 0 ? ScratchDst_DB1 : ScratchDst_DB0;
            }

            /// <summary>
            /// PHASE 1: Public accessor for UI
            /// Get current active buffer set (0 or 1)
            /// </summary>
            public int GetActiveBufferSet()
            {
                return ActiveBufferSet;
            }

            /// <summary>
            /// Get banked scratch controller for operand A (Plan 07).
            /// </summary>
            public Core.ScratchBankController GetBankedScratchA() => BankedScratchA;

            /// <summary>
            /// Get banked scratch controller for operand B (Plan 07).
            /// </summary>
            public Core.ScratchBankController GetBankedScratchB() => BankedScratchB;

            /// <summary>
            /// Get banked scratch controller for destination (Plan 07).
            /// </summary>
            public Core.ScratchBankController GetBankedScratchDst() => BankedScratchDst;

            /// <summary>
            /// Check for crossbar bank conflicts in a vector triple-access (2R+1W).
            /// Returns stall penalty cycles. Updates conflict counters on BankedScratchA.
            /// </summary>
            /// <param name="readAElement">Element index for read from scratch A.</param>
            /// <param name="readBElement">Element index for read from scratch B.</param>
            /// <param name="writeDstElement">Element index for write to scratch Dst.</param>
            /// <returns>Stall cycles (0 = no conflict, 1 = one-cycle stall).</returns>
            public int CheckScratchBankConflict(int readAElement, int readBElement, int writeDstElement)
            {
                return BankedScratchA.CheckConflict(readAElement, readBElement, writeDstElement);
            }
        }
    }
}
