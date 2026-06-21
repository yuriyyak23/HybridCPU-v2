using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Global Resource Lock Bitset (GRLB) 
            /// Each bit represents a resource that is currently locked by an executing operation.
            /// When a bit is set to 1, the corresponding resource is occupied and cannot be used
            /// by other operations until it is released.
            ///
            /// Extended to 128 bits to support more resources:
            /// Low 64 bits (0-63):
            /// - Bits 0-15:  Register read groups (16 groups of 4 registers each)
            /// - Bits 16-31: Register write groups (16 groups of 4 registers each)
            /// - Bits 32-47: Memory domain IDs (16 possible domains)
            /// - Bit 48:     Load operation (LSU read channel)
            /// - Bit 49:     Store operation (LSU write channel)
            /// - Bit 50:     Atomic operation (LSU atomic channel)
            /// - Bits 51-54: DMA channels 0-3 (4 channels)
            /// - Bits 55-58: Stream engines 0-3 (4 engines)
            /// - Bits 59-62: Custom accelerators 0-3 (4 accelerators)
            /// - Bit 63:     Reserved
            ///
            /// High 64 bits (64-127):
            /// - Bits 64-67: DMA channels 4-7 (4 additional channels)
            /// - Bits 68-71: Stream engines 4-7 (4 additional engines)
            /// - Bits 72-75: Custom accelerators 4-7 (4 additional accelerators)
            /// - Bits 76-79: Additional LSU channels
            /// - Bits 80-95: Extended memory domains (16 additional domains)
            /// - Bits 96-127: Reserved for future resource types
            ///
            /// Hardware-Agnostic Design:
            /// This is implemented as a ResourceBitset struct (two ulongs) which maps directly to
            /// two 64-bit hardware registers in FPGA synthesis. The bitwise operations (AND, OR, NOT)
            /// are single-cycle combinational logic in hardware (two parallel gates).
            /// </summary>
            private Core.ResourceBitset globalResourceLocks;

            /// <summary>
            /// Token generation counter for ABA problem prevention (Phase 8 enhancement).
            /// Each resource acquisition increments this counter and stores the token
            /// with the resource bits. When releasing, we verify the token matches
            /// to prevent releasing a resource that was already released and re-acquired.
            /// </summary>
            private ulong tokenGeneration;

            /// <summary>
            /// Resource token storage - maps each bit position to its acquisition token.
            /// This prevents the ABA problem where a resource is released, re-acquired,
            /// and then incorrectly released again by a stale operation.
            /// Array size is 128 (one per bit in 128-bit globalResourceLocks).
            /// </summary>
            private ulong[] resourceTokens = new ulong[128];

            /// <summary>
            /// Performance counter: Number of cycles stalled due to structural hazards (resource conflicts).
            /// Phase 8: GRLB
            /// </summary>
            public ulong StructuralStalls { get; private set; }

            /// <summary>
            /// Resource usage statistics - counts how many times each resource was acquired.
            /// Index corresponds to bit position in globalResourceLocks (0-127).
            /// Phase 8 Extended: GRLB monitoring and debugging.
            /// </summary>
            private ulong[] resourceUsageCounts = new ulong[128];

            /// <summary>
            /// Resource contention statistics - counts how many times acquisition failed for each resource.
            /// Index corresponds to bit position in globalResourceLocks (0-127).
            /// Phase 8 Extended: GRLB monitoring and debugging.
            /// </summary>
            private ulong[] resourceContentionCounts = new ulong[128];

            // ===== Banked GRLB (Plan 06): 4 × 32-bit independent banks =====

            /// <summary>
            /// Array of reference counters for register read groups (bits 0-15).
            /// Implements Scoreboard Reference Counting to safely handle multiple
            /// concurrent readers without premature AND-NOT deallocation (WAR hazard prevention).
            /// </summary>
            private byte[] _readCounters = new byte[16];

            /// <summary>
            /// Banked GRLB: 4 × 32-bit banks with independent acquire/release.
            /// Each bank serves a subset of resources, reducing Fan-In/Fan-Out
            /// from 128 to 32 wires per bank.
            /// HLS: 4 independent 32-bit registers with local arbitration logic.
            ///
            /// Bank layout:
            ///   Bank 0 [bits  0–31]:  Register read/write groups
            ///   Bank 1 [bits 32–63]:  Memory domains + LSU + DMA 0–3
            ///   Bank 2 [bits 64–95]:  Extended GRLB channels (DMA 4–7, Stream, Accel)
            ///   Bank 3 [bits 96–127]: Reserved / extended domains
            /// </summary>
            private uint[] _grlbBanks = new uint[4];

            /// <summary>
            /// Per-bank contention counters for diagnostics.
            /// </summary>
            private ulong[] _bankContentionCounts = new ulong[4];

            /// <summary>
            /// Attempt to acquire resources for a micro-operation with token tracking.
            /// Uses banked GRLB (Plan 06): only checks/sets relevant 32-bit banks,
            /// reducing Fan-In/Fan-Out from 128 to 32 wires per bank.
            ///
            /// Algorithm:
            /// 1. Determine which banks are touched by the mask (ActiveBanks).
            /// 2. For each active bank: check (bank &amp; mask_slice) for conflict.
            /// 3. If conflict: update contention stats and return false.
            /// 4. If no conflict: set bits in active banks, sync globalResourceLocks,
            ///    store tokens, return true.
            ///
            /// HLS: independent logic per bank → minimal cross-bank wiring.
            /// </summary>
            /// <param name="mask">Resource mask from the MicroOp (ResourceMask property)</param>
            /// <param name="token">Output: assigned token for this acquisition (0 if failed)</param>
            /// <returns>True if resources acquired successfully, false if conflict detected</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AcquireResourcesWithToken(Core.ResourceBitset mask, out ulong token)
            {
                byte activeBanks = mask.ActiveBanks();

                // Phase 1: Check all active banks for conflicts (parallel in HW)
                for (int b = 0; b < 4; b++)
                {
                    if ((activeBanks & (1 << b)) == 0) continue;
                    uint bankMask = mask.GetBank(b);

                    bool hasConflict = false;
                    if (b == 0)
                    {
                        // Bank 0 contains Register Reads (0-15) and Register Writes (16-31)
                        // Data Hazard resolution: RAR is safe, but RAW/WAR/WAW are conflicts.
                        uint myReads = bankMask & 0xFFFF;
                        uint myWrites = (bankMask >> 16) & 0xFFFF;
                        uint theirReads = _grlbBanks[0] & 0xFFFF;
                        uint theirWrites = (_grlbBanks[0] >> 16) & 0xFFFF;

                        // Conflict rule: (myReads & theirWrites) | (myWrites & theirReads) | (myWrites & theirWrites)
                        if ((myReads & theirWrites) != 0 || (myWrites & theirReads) != 0 || (myWrites & theirWrites) != 0)
                        {
                            hasConflict = true;
                        }
                    }
                    else
                    {
                        // For non-register banks, strict overlap check
                        if ((_grlbBanks[b] & bankMask) != 0)
                        {
                            hasConflict = true;
                        }
                    }

                    if (hasConflict)
                    {
                        // Conflict detected in bank b
                        token = 0;
                        _bankContentionCounts[b]++;
                        StructuralStalls++;

                        // Update per-bit contention statistics... (skip detailed exact bit check for simplicity or keep existing)
                        for (int bit = 0; bit < 64; bit++)
                        {
                            if ((mask.Low & (1UL << bit)) != 0 && (globalResourceLocks.Low & (1UL << bit)) != 0)
                                resourceContentionCounts[bit]++;
                        }
                        for (int bit = 0; bit < 64; bit++)
                        {
                            if ((mask.High & (1UL << bit)) != 0 && (globalResourceLocks.High & (1UL << bit)) != 0)
                                resourceContentionCounts[64 + bit]++;
                        }

                        return false;
                    }
                }

                // Phase 2: No conflict — acquire in all active banks (parallel in HW)
                token = ++tokenGeneration;

                for (int b = 0; b < 4; b++)
                {
                    if ((activeBanks & (1 << b)) == 0) continue;

                    if (b == 0)
                    {
                        uint bankMask = mask.GetBank(0);
                        // Increment read counters for read bits (0-15)
                        for (int i = 0; i < 16; i++)
                        {
                            if ((bankMask & (1U << i)) != 0)
                            {
                                _readCounters[i]++;
                            }
                        }
                        // Set the actual bits in _grlbBanks
                        _grlbBanks[0] |= bankMask;
                    }
                    else
                    {
                        _grlbBanks[b] |= mask.GetBank(b);
                    }
                }

                // Sync unified register from banks
                globalResourceLocks = new Core.ResourceBitset(
                    (ulong)_grlbBanks[0] | ((ulong)_grlbBanks[1] << 32),
                    (ulong)_grlbBanks[2] | ((ulong)_grlbBanks[3] << 32));

                // Store token for each bit position and update usage statistics
                for (int bit = 0; bit < 64; bit++)
                {
                    if ((mask.Low & (1UL << bit)) != 0)
                    {
                        resourceTokens[bit] = token;
                        resourceUsageCounts[bit]++;
                    }
                }
                for (int bit = 0; bit < 64; bit++)
                {
                    if ((mask.High & (1UL << bit)) != 0)
                    {
                        resourceTokens[64 + bit] = token;
                        resourceUsageCounts[64 + bit]++;
                    }
                }

                return true;
            }

            /// <summary>
            /// Attempt to acquire resources for a micro-operation (legacy method without token).
            /// Returns true if all resources were successfully acquired, false if any conflict exists.
            /// This method is kept for backward compatibility but internally uses token tracking.
            /// </summary>
            /// <param name="mask">Resource mask from the MicroOp (ResourceMask property)</param>
            /// <returns>True if resources acquired successfully, false if conflict detected</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AcquireResources(Core.ResourceBitset mask)
            {
                ulong token;
                return AcquireResourcesWithToken(mask, out token);
            }

            /// <summary>
            /// Release resources held by a micro-operation with token verification.
            /// This clears the bits corresponding to the resources that were locked,
            /// but only if the token matches the current token for each bit.
            ///
            /// This prevents the ABA problem: if a resource was released and re-acquired
            /// by another operation, the old operation's token won't match and the
            /// release will be ignored for that bit.
            ///
            /// In hardware, this is a loop over bit positions, checking tokens and
            /// conditionally clearing bits.
            ///
            /// Algorithm:
            /// 1. For each bit in the mask (both Low and High):
            ///    a. Check if the stored token matches the provided token
            ///    b. If match, clear the bit: globalResourceLocks &= ~(1 << bit)
            ///    c. If no match, skip (resource was already released and re-acquired)
            ///
            /// Note: This operation is safe - releasing already-released resources is harmless.
            /// </summary>
            /// <param name="mask">Resource mask from the MicroOp (ResourceMask property)</param>
            /// <param name="token">Token that was assigned when resources were acquired</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ReleaseResourcesWithToken(Core.ResourceBitset mask, ulong token)
            {
                // For each bit in the Low component, check token and release if it matches
                for (int bit = 0; bit < 64; bit++)
                {
                    ulong bitMask = 1UL << bit;
                    if ((mask.Low & bitMask) != 0)
                    {
                        if (bit >= 0 && bit < 16)
                        {
                            // Register Read bit (Scoreboard RefCount)
                            if (_readCounters[bit] > 0)
                            {
                                _readCounters[bit]--;
                            }

                            // Only clear the bit in the global lock if no more readers
                            if (_readCounters[bit] == 0)
                            {
                                globalResourceLocks.Low &= ~bitMask;
                            }
                        }
                        else
                        {
                            // Non-read bits: Only release if token matches (prevents ABA problem)
                            if (resourceTokens[bit] == token)
                            {
                                globalResourceLocks.Low &= ~bitMask;
                            }
                        }
                    }
                }
                // For each bit in the High component, check token and release if it matches
                for (int bit = 0; bit < 64; bit++)
                {
                    ulong bitMask = 1UL << bit;
                    if ((mask.High & bitMask) != 0)
                    {
                        // Only release if token matches (prevents ABA problem)
                        if (resourceTokens[64 + bit] == token)
                        {
                            globalResourceLocks.High &= ~bitMask;
                        }
                    }
                }

                // Sync banked registers from unified register
                SyncBanksFromUnified();
            }

            /// <summary>
            /// Release resources held by a micro-operation (legacy method without token check).
            /// This clears the bits corresponding to the resources that were locked.
            /// Note: This version does NOT protect against ABA problem. Use ReleaseResourcesWithToken instead.
            ///
            /// Algorithm:
            /// 1. Clear the locked bits: globalResourceLocks &= ~mask
            ///
            /// Note: This operation is idempotent - releasing already-released resources is safe.
            /// </summary>
            /// <param name="mask">Resource mask from the MicroOp (ResourceMask property)</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ReleaseResources(Core.ResourceBitset mask)
            {
                // Clear the resource lock bits using bitwise AND with inverted mask
                // Special handling for read groups (0-15)
                for (int i = 0; i < 16; i++)
                {
                    if ((mask.Low & (1UL << i)) != 0)
                    {
                        if (_readCounters[i] > 0)
                            _readCounters[i]--;

                        if (_readCounters[i] == 0)
                        {
                            globalResourceLocks.Low &= ~(1UL << i);
                        }
                    }
                }

                ulong nonReadMaskLow = mask.Low & ~0xFFFFUL;
                globalResourceLocks.Low &= ~nonReadMaskLow;
                globalResourceLocks.High &= ~mask.High;

                // Sync banked registers from unified register
                SyncBanksFromUnified();
            }

            /// <summary>
            /// Check if specific resources are currently locked (for debugging/monitoring).
            /// Returns true if ANY of the requested resources are locked.
            /// </summary>
            /// <param name="mask">Resource mask to check</param>
            /// <returns>True if any resource in the mask is locked</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AreResourcesLocked(Core.ResourceBitset mask)
            {
                return (globalResourceLocks.Low & mask.Low) != 0 || (globalResourceLocks.High & mask.High) != 0;
            }

            /// <summary>
            /// Get the current global resource lock state (for debugging/monitoring).
            /// Returns a copy of the globalResourceLocks bitset.
            /// </summary>
            public Core.ResourceBitset GetGlobalResourceLocks()
            {
                return globalResourceLocks;
            }

            /// <summary>
            /// Clear all global resource locks.
            /// Called during pipeline flush, context switch, or reset.
            /// </summary>
            public void ClearAllResourceLocks()
            {
                globalResourceLocks = Core.ResourceBitset.Zero;
                _grlbBanks[0] = 0;
                _grlbBanks[1] = 0;
                _grlbBanks[2] = 0;
                _grlbBanks[3] = 0;
                for (int i = 0; i < 16; i++)
                {
                    _readCounters[i] = 0;
                }
            }

            /// <summary>
            /// Sync banked registers from the unified globalResourceLocks register.
            /// Called after operations that modify globalResourceLocks directly (legacy release paths).
            /// HLS: simple wire assignment, zero logic.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SyncBanksFromUnified()
            {
                _grlbBanks[0] = (uint)(globalResourceLocks.Low & 0xFFFFFFFFUL);
                _grlbBanks[1] = (uint)(globalResourceLocks.Low >> 32);
                _grlbBanks[2] = (uint)(globalResourceLocks.High & 0xFFFFFFFFUL);
                _grlbBanks[3] = (uint)(globalResourceLocks.High >> 32);
            }

            /// <summary>
            /// Get per-bank contention count for diagnostics.
            /// </summary>
            /// <param name="bankIndex">Bank index (0–3).</param>
            /// <returns>Number of contention events on this bank.</returns>
            public ulong GetBankContentionCount(int bankIndex)
            {
                if ((uint)bankIndex >= 4) return 0;
                return _bankContentionCounts[bankIndex];
            }

            /// <summary>
            /// Get current GRLB bank states for diagnostics and GUI display.
            /// Returns a copy of the 4 × 32-bit bank array.
            /// </summary>
            /// <returns>Array of 4 uint values representing the 4 GRLB banks</returns>
            public uint[] GetGrlbBanks()
            {
                return new uint[]
                {
                    _grlbBanks[0],
                    _grlbBanks[1],
                    _grlbBanks[2],
                    _grlbBanks[3]
                };
            }

            /// <summary>
            /// Increment the structural stall counter.
            /// Called when a MicroOp must wait due to resource conflicts.
            /// Phase 8 Extended: GRLB performance monitoring.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IncrementStructuralStalls()
            {
                StructuralStalls++;
            }

            /// <summary>
            /// Reset GRLB performance counters.
            /// </summary>
            public void ResetGRLBCounters()
            {
                StructuralStalls = 0;
                for (int i = 0; i < 128; i++)
                {
                    resourceUsageCounts[i] = 0;
                    resourceContentionCounts[i] = 0;
                }
                for (int i = 0; i < 4; i++)
                    _bankContentionCounts[i] = 0;
            }

            /// <summary>
            /// Get resource usage statistics for a specific resource bit.
            /// Returns the number of times the resource was successfully acquired.
            /// Phase 8 Extended: GRLB monitoring and debugging.
            /// </summary>
            /// <param name="bitIndex">Resource bit index (0-127)</param>
            /// <returns>Number of times the resource was acquired</returns>
            public ulong GetResourceUsageCount(int bitIndex)
            {
                if (bitIndex < 0 || bitIndex >= 128) return 0;
                return resourceUsageCounts[bitIndex];
            }

            /// <summary>
            /// Get resource contention statistics for a specific resource bit.
            /// Returns the number of times acquisition failed due to conflicts.
            /// Phase 8 Extended: GRLB monitoring and debugging.
            /// </summary>
            /// <param name="bitIndex">Resource bit index (0-127)</param>
            /// <returns>Number of times acquisition failed</returns>
            public ulong GetResourceContentionCount(int bitIndex)
            {
                if (bitIndex < 0 || bitIndex >= 128) return 0;
                return resourceContentionCounts[bitIndex];
            }

            /// <summary>
            /// Get all resource usage statistics.
            /// Returns a copy of the usage counts array.
            /// Phase 8 Extended: GRLB monitoring and debugging.
            /// </summary>
            /// <returns>Array of 128 usage counts</returns>
            public ulong[] GetAllResourceUsageCounts()
            {
                ulong[] copy = new ulong[128];
                Array.Copy(resourceUsageCounts, copy, 128);
                return copy;
            }

            /// <summary>
            /// Get all resource contention statistics.
            /// Returns a copy of the contention counts array.
            /// Phase 8 Extended: GRLB monitoring and debugging.
            /// </summary>
            /// <returns>Array of 128 contention counts</returns>
            public ulong[] GetAllResourceContentionCounts()
            {
                ulong[] copy = new ulong[128];
                Array.Copy(resourceContentionCounts, copy, 128);
                return copy;
            }

            // ===== Refactoring Pt. 1: Dynamic Hardware Occupancy Integration =====

            /// <summary>
            /// Get the effective resource lock set that includes both GRLB-held locks
            /// and dynamic hardware occupancy from the memory subsystem.
            ///
            /// This replaces the ad-hoc SuppressLsu boolean with a mathematically
            /// correct mask merge: if memory banks/channels are congested, the
            /// corresponding Load/Store/MemoryBank bits are set, causing
            /// AcquireResources to detect a structural hazard.
            ///
            /// HLS: single-cycle OR gate between GRLB register and hardware status wires.
            /// </summary>
            /// <param name="hardwareOccupancy">Occupancy mask from MemorySubsystem.GetHardwareOccupancyMask128()</param>
            /// <returns>Merged resource bitset (GRLB ∪ hardware occupancy)</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Core.ResourceBitset GetEffectiveLocks(Core.SafetyMask128 hardwareOccupancy)
            {
                return new Core.ResourceBitset(
                    globalResourceLocks.Low | hardwareOccupancy.Low,
                    globalResourceLocks.High | hardwareOccupancy.High);
            }

            /// <summary>
            /// Check if a resource mask conflicts with the effective locks
            /// (GRLB + hardware occupancy) without acquiring resources.
            ///
            /// Used by the FSP scheduler to pre-check whether an injection would
            /// stall on structural hazards before committing to it.
            ///
            /// HLS: combinational path — OR + AND + compare-to-zero.
            /// </summary>
            /// <param name="mask">Resource mask to check</param>
            /// <param name="hardwareOccupancy">Dynamic hardware occupancy mask</param>
            /// <returns>True if a conflict exists, false if resources are free</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool HasConflictWithHardware(Core.ResourceBitset mask, Core.SafetyMask128 hardwareOccupancy)
            {
                ulong effectiveLow = globalResourceLocks.Low | hardwareOccupancy.Low;
                ulong effectiveHigh = globalResourceLocks.High | hardwareOccupancy.High;
                return ((effectiveLow & mask.Low) != 0) || ((effectiveHigh & mask.High) != 0);
            }
        }
    }
}
