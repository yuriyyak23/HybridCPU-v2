namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            // --- Cycle-Accurate FSM Infrastructure (Phase 4.1) ---

            /// <summary>
            /// Global cycle counter for deterministic timing.
            /// Increments on each live pipeline execution cycle, never resets during execution.
            /// Used for cycle-accurate simulation and performance profiling.
            /// </summary>
            public ulong CycleCounter;

            /// <summary>
            /// Per-stage latency constants (in cycles).
            /// Based on simplified 5-stage pipeline model for HLS synthesis.
            /// </summary>
            public const int FETCH_LATENCY = 1;         // 1 cycle to fetch VLIW bundle from cache
            public const int DECODE_LATENCY = 1;        // 1 cycle to decode 8 instructions in parallel
            public const int EXECUTE_LATENCY_MIN = 1;   // 1 cycle for scalar ALU operations
            public const int EXECUTE_LATENCY_MAX = 10;  // 10 cycles for FP divide/sqrt
            public const int MEMORY_LATENCY = 4;        // 4 cycles for L1 cache hit (AXI4 burst)
            public const int WRITEBACK_LATENCY = 1;     // 1 cycle to commit results

            /// <summary>
            /// Cycle counter for current stage operation.
            /// Tracks how many cycles have been spent in the current pipeline stage.
            /// Reset when transitioning to a new stage.
            /// </summary>
            public int StageCycleCounter;

            /// <summary>
            /// Pipeline stall flag.
            /// Set to true when pipeline must wait (e.g., memory access, data dependency).
            /// Prevents state transitions until cleared.
            /// </summary>
            public bool Stalled;

            // --- Power State Enumeration (ACPI-like C-states and P-states) ---
            public enum CorePowerState : uint
            {
                C0_Active = 0,      // Operating state, fully powered and executing
                C1_Halt = 1,        // Halted, clock gated but powered, fast wake-up
                C2_StopClock = 2,   // Clocks gated to more components, longer wake-up than C1
                C3_Sleep = 3,       // Deeper sleep, caches may be flushed, longer wake-up
                C6_DeepPowerDown = 6, // Core voltage reduced or off, significant wake-up latency
                // P-states (Performance States) - can be combined or managed separately
                P0_MaxPerformance = 100, // Max frequency and voltage
                P1_HighPerformance = 101,
                Pn_MinPerformance = 115, // Min frequency and voltage for power saving
                ErrorState = 0xFFFFFFFF // Indicates an issue with power state transition
            }

            /// <summary>
            /// Reset the cycle counter (for testing or new execution runs).
            /// </summary>
            public void ResetCycleCounter()
            {
                CycleCounter = 0;
                StageCycleCounter = 0;
            }
        }
    }
}
