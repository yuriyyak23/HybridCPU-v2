using System;
using System.Threading;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        /// <summary>
        /// Synchronization primitives for multi-processor execution (Iteration 7)
        ///
        /// Goals:
        /// - Enable coordination between multiple CPU cores
        /// - Support barrier synchronization for parallel algorithms
        /// - Provide atomic operations for lock-free data structures
        /// - Minimize synchronization overhead
        ///
        /// Primitives:
        /// - Barriers: Wait for all cores to reach sync point
        /// - Semaphores: Counting synchronization
        /// - Atomic operations: Compare-and-swap, fetch-and-add
        /// - Memory fences: Ordering guarantees
        /// </summary>
        public static class SyncPrimitives
        {
            /// <summary>
            /// Global barrier for all CPU cores
            /// Used for synchronizing parallel phases (e.g., map-reduce)
            /// </summary>
            private static Barrier? GlobalBarrier = null;

            /// <summary>
            /// Barrier sense (alternate between 0 and 1 to avoid reuse issues)
            /// </summary>
            private static int BarrierSense = 0;

            /// <summary>
            /// Initialize synchronization primitives
            /// </summary>
            /// <param name="coreCount">Number of CPU cores to synchronize</param>
            public static void Initialize(int coreCount)
            {
                if (coreCount <= 0)
                    throw new ArgumentException("Core count must be positive", nameof(coreCount));

                GlobalBarrier?.Dispose();
                GlobalBarrier = new Barrier(coreCount);
                BarrierSense = 0;
            }

            /// <summary>
            /// Barrier synchronization - wait for all cores to reach this point
            /// </summary>
            /// <param name="coreID">ID of calling core (for logging/debugging)</param>
            /// <returns>True if this is the last core to arrive (coordinator)</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool BarrierWait(int coreID)
            {
                if (GlobalBarrier == null)
                    throw new InvalidOperationException("Barrier not initialized");

                // Signal and wait for all cores
                GlobalBarrier.SignalAndWait();

                // Toggle sense to prepare for next use
                int currentSense = Interlocked.Exchange(ref BarrierSense, 1 - BarrierSense);

                // Last core to arrive (coordinator) returns true
                return GlobalBarrier.CurrentPhaseNumber > 0 &&
                       GlobalBarrier.ParticipantsRemaining == GlobalBarrier.ParticipantCount;
            }

            /// <summary>
            /// Reset barrier for reuse
            /// Should only be called when no cores are waiting
            /// </summary>
            public static void ResetBarrier()
            {
                if (GlobalBarrier == null) return;

                int coreCount = GlobalBarrier.ParticipantCount;
                GlobalBarrier.Dispose();
                GlobalBarrier = new Barrier(coreCount);
                BarrierSense = 0;
            }

            /// <summary>
            /// Atomic compare-and-swap (CAS) operation
            /// Atomically: if (*ptr == expected) { *ptr = newValue; return true; } else { return false; }
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool CompareAndSwap(ref ulong ptr, ulong expected, ulong newValue)
            {
                ulong original = Interlocked.CompareExchange(ref ptr, newValue, expected);
                return original == expected;
            }

            /// <summary>
            /// Atomic fetch-and-add operation
            /// Atomically: old = *ptr; *ptr += value; return old;
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong FetchAndAdd(ref ulong ptr, ulong value)
            {
                // .NET doesn't have native Interlocked.Add for ulong, implement with CAS loop
                ulong current, newValue;
                do
                {
                    current = ptr;
                    newValue = current + value;
                } while (!CompareAndSwap(ref ptr, current, newValue));

                return current;
            }

            /// <summary>
            /// Atomic fetch-and-increment
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong FetchAndIncrement(ref ulong ptr)
            {
                return FetchAndAdd(ref ptr, 1);
            }

            /// <summary>
            /// Atomic load with acquire semantics
            /// Ensures all subsequent reads see values after this load
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong AtomicLoadAcquire(ref ulong ptr)
            {
                ulong value = Volatile.Read(ref ptr);
                Thread.MemoryBarrier(); // Full fence for acquire
                return value;
            }

            /// <summary>
            /// Atomic store with release semantics
            /// Ensures all previous writes are visible before this store
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void AtomicStoreRelease(ref ulong ptr, ulong value)
            {
                Thread.MemoryBarrier(); // Full fence for release
                Volatile.Write(ref ptr, value);
            }

            /// <summary>
            /// Memory fence - ensure all memory operations complete
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void MemoryFence()
            {
                Thread.MemoryBarrier();
            }

            /// <summary>
            /// Spin-wait for a condition with exponential backoff
            /// More efficient than busy-spin for longer waits
            /// </summary>
            /// <param name="condition">Condition to wait for</param>
            /// <param name="maxSpins">Maximum spin iterations before yielding</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void SpinWait(Func<bool> condition, int maxSpins = 1000)
            {
                int spinCount = 0;
                while (!condition())
                {
                    if (spinCount < maxSpins)
                    {
                        // Spin with exponential backoff
                        Thread.SpinWait(1 << Math.Min(spinCount / 10, 10));
                        spinCount++;
                    }
                    else
                    {
                        // Yield to OS scheduler after too many spins
                        Thread.Yield();
                        spinCount = 0;
                    }
                }
            }

            /// <summary>
            /// Cleanup synchronization primitives
            /// </summary>
            public static void Cleanup()
            {
                GlobalBarrier?.Dispose();
                GlobalBarrier = null;
            }
        }

        /// <summary>
        /// Per-core synchronization state
        /// </summary>
        public partial struct CPU_Core
        {
            /// <summary>
            /// Core-local synchronization counter
            /// Used for tracking synchronization events
            /// </summary>
            private ulong syncCounter;

            /// <summary>
            /// Increment synchronization counter atomically
            /// </summary>
            public void IncrementSyncCounter()
            {
                SyncPrimitives.FetchAndIncrement(ref syncCounter);
            }

            /// <summary>
            /// Get current synchronization counter value
            /// </summary>
            public ulong GetSyncCounter()
            {
                return SyncPrimitives.AtomicLoadAcquire(ref syncCounter);
            }

            /// <summary>
            /// Reset synchronization counter
            /// </summary>
            public void ResetSyncCounter()
            {
                SyncPrimitives.AtomicStoreRelease(ref syncCounter, 0);
            }

            /// <summary>
            /// Barrier synchronization - wait for all cores
            /// </summary>
            /// <returns>True if this core is the coordinator (last to arrive)</returns>
            public bool SyncBarrier()
            {
                IncrementSyncCounter();
                return SyncPrimitives.BarrierWait((int)this.CoreID);
            }
        }
    }
}
