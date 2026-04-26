using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU.Memory
{
    public partial class MemorySubsystem
    {
        #region Helper Methods

        /// <summary>
        /// Compute which memory bank an address belongs to
        /// </summary>
        private int ComputeBankId(ulong address)
        {
            return Core.Memory.MemoryBankRouting.ResolveBankId(address, BankWidthBytes, NumBanks);
        }

        private bool RemoveQueuedBankRequest(ulong requestID, int bankId)
        {
            if ((uint)bankId >= (uint)NumBanks || bankQueues[bankId].Count == 0)
                return false;

            Queue<BankRequest> retainedRequests = new(bankQueues[bankId].Count);
            bool removed = false;
            while (bankQueues[bankId].Count > 0)
            {
                BankRequest request = bankQueues[bankId].Dequeue();
                if (!removed && request.RequestID == requestID)
                {
                    removed = true;
                    continue;
                }

                retainedRequests.Enqueue(request);
            }

            while (retainedRequests.Count > 0)
            {
                bankQueues[bankId].Enqueue(retainedRequests.Dequeue());
            }

            return removed;
        }

        /// <summary>
        /// Calculate adaptive stall cycles based on queue depth and transfer size
        /// </summary>
        /// <param name="queueDepth">Current queue depth for the bank</param>
        /// <param name="transferSize">Size of the transfer in bytes</param>
        /// <returns>Number of stall cycles</returns>
        private int CalculateAdaptiveStall(int queueDepth, int transferSize)
        {
            // Base stall increases with queue depth
            int baseStall = queueDepth * 2;

            // Add transfer-dependent latency based on bandwidth
            // Bandwidth in bytes/cycle = (BankBandwidthGBps * 1e9) / (frequency in Hz)
            // Assuming 1 GHz frequency for simplicity: 1 cycle = 1 ns
            double bytesPerCycle = BankBandwidthGBps; // GB/s ≈ bytes/cycle at 1 GHz
            int transferLatency = (int)Math.Ceiling(transferSize / bytesPerCycle);

            return baseStall + transferLatency;
        }

        /// <summary>
        /// Select next bank for processing based on arbitration policy
        /// </summary>
        /// <returns>Bank ID to process, or -1 if no banks have pending requests</returns>
        private int SelectNextBank()
        {
            switch (ArbitrationPolicy)
            {
                case BankArbitrationPolicy.RoundRobin:
                    return SelectBankRoundRobin();

                case BankArbitrationPolicy.WeightedFair:
                    return SelectBankWeightedFair();

                case BankArbitrationPolicy.Priority:
                    return SelectBankPriority();

                case BankArbitrationPolicy.FixedStatic:
                    // ProcessBankQueues handles FixedStatic directly to enable multiple ports
                    return -1;

                default:
                    return SelectBankRoundRobin();
            }
        }

        /// <summary>
        /// Round-robin bank selection
        /// </summary>
        private int SelectBankRoundRobin()
        {
            int startIndex = roundRobinIndex;
            do
            {
                if (!bankOccupied[roundRobinIndex] && bankQueues[roundRobinIndex].Count > 0)
                {
                    int selected = roundRobinIndex;
                    roundRobinIndex = (roundRobinIndex + 1) % NumBanks;
                    return selected;
                }
                roundRobinIndex = (roundRobinIndex + 1) % NumBanks;
            } while (roundRobinIndex != startIndex);

            return -1; // No available banks
        }

        /// <summary>
        /// Weighted fair queueing bank selection
        /// Prioritizes banks with longer queues but considers occupancy
        /// </summary>
        private int SelectBankWeightedFair()
        {
            int selectedBank = -1;
            int maxWeight = -1;

            for (int i = 0; i < NumBanks; i++)
            {
                if (!bankOccupied[i] && bankQueues[i].Count > 0)
                {
                    // Weight = queue depth + average priority
                    int queueDepth = bankQueues[i].Count;
                    int avgPriority = 0;
                    foreach (var req in bankQueues[i])
                    {
                        avgPriority += req.Priority;
                    }
                    avgPriority = queueDepth > 0 ? avgPriority / queueDepth : 0;

                    int weight = queueDepth * 10 + avgPriority;
                    if (weight > maxWeight)
                    {
                        maxWeight = weight;
                        selectedBank = i;
                    }
                }
            }

            return selectedBank;
        }

        /// <summary>
        /// Priority-based bank selection
        /// Selects bank with highest priority request
        /// </summary>
        private int SelectBankPriority()
        {
            int selectedBank = -1;
            int highestPriority = -1;

            for (int i = 0; i < NumBanks; i++)
            {
                if (!bankOccupied[i] && bankQueues[i].Count > 0)
                {
                    var request = bankQueues[i].Peek();
                    if (request.Priority > highestPriority)
                    {
                        highestPriority = request.Priority;
                        selectedBank = i;
                    }
                }
            }

            return selectedBank;
        }

        /// <summary>
        /// Process queued requests for available banks
        /// </summary>
        private void ProcessBankQueues()
        {
            if (ArbitrationPolicy == BankArbitrationPolicy.FixedStatic)
            {
                // Strict TDM scheduling: bank selection depends purely on the cycle counter (Cycle-Determinism)
                for (int p = 0; p < NumMemoryPorts; p++)
                {
                    int targetBank = (int)((currentCycle + p) % NumBanks);
                    if (!bankOccupied[targetBank] && bankQueues[targetBank].Count > 0)
                    {
                        var request = bankQueues[targetBank].Dequeue();
                        ExecuteBankRequest(targetBank, request);
                    }
                }
                return;
            }

            // Process up to NumMemoryPorts banks concurrently using dynamic arbitration
            int portsUsed = 0;
            int maxAttempts = NumBanks; 
            int attempts = 0;

            while (portsUsed < NumMemoryPorts && attempts < maxAttempts)
            {
                int selectedBank = SelectNextBank();
                if (selectedBank == -1) break; // No available banks according to policy

                var request = bankQueues[selectedBank].Dequeue();
                ExecuteBankRequest(selectedBank, request);
                portsUsed++;
                attempts++;
            }
        }

        /// <summary>
        /// Execute a bank request immediately
        /// </summary>
        private void ExecuteBankRequest(int bankId, BankRequest request)
        {
            long waitCycles = currentCycle - request.EnqueueCycle;
            TotalWaitCycles += waitCycles;
            int portId = AllocatePortForBank(bankId, request.IsRead);

            // Phase 3: Calculate accurate burst timing using BurstPlanner
            var timing = BurstPlanner.CalculateBurstTiming(
                request.Address, request.Length, request.IsRead);

            // Track unaligned accesses
            if (timing.AlignmentPenalty > 0)
            {
                UnalignedAccessCount++;
                TotalAlignmentPenalty += timing.AlignmentPenalty;
            }

            // Track burst efficiency
            if (timing.TotalCycles > 0)
            {
                int theoreticalCycles = timing.DataCycles;
                int actualCycles = timing.TotalCycles;
                double burstEff = theoreticalCycles / (double)actualCycles;

                // Running average of burst efficiency
                if (TotalBursts > 0)
                {
                    BurstEfficiency = (BurstEfficiency * 0.95) + (burstEff * 0.05);
                }
                else
                {
                    BurstEfficiency = burstEff;
                }
            }

            // Accumulate burst timing cycles
            TotalBurstTimingCycles += timing.TotalCycles;

            // Mark bank as occupied
            bankOccupied[bankId] = true;
            bankLastAccessCycle[bankId] = currentCycle;

            // Execute the request via IOMMU or DMA
            bool success = false;
            if (request.IsRead)
            {
                // Perform read operation
                if (request.Length >= DmaThresholdBytes && dma != null)
                {
                    success = ReadViaDMA(request.DeviceID, request.Address, request.Buffer.AsSpan(0, request.Length));
                }
                else
                {
                    success = IOMMU.ReadBurst(request.DeviceID, request.Address, request.Buffer.AsSpan(0, request.Length));
                }
            }
            else
            {
                // Retire-deferred stores still consume queue/bank timing here, but
                // the backing-memory side effect remains blocked until WB retire.
                if (request.DefersPhysicalWriteUntilRetire)
                {
                    success = true;
                }
                else if (request.Length >= DmaThresholdBytes && dma != null)
                {
                    success = WriteViaDMA(request.DeviceID, request.Address, request.Buffer.AsSpan(0, request.Length));
                }
                else
                {
                    success = IOMMU.WriteBurst(request.DeviceID, request.Address, request.Buffer.AsSpan(0, request.Length));
                }
            }

            // Find and complete the associated memory request token
            lock (pendingRequests)
            {
                if (pendingRequests.TryGetValue(request.RequestID, out MemoryRequestToken? matchingToken) &&
                    !matchingToken.IsComplete)
                {
                    matchingToken.IsComplete = true;
                    matchingToken.Succeeded = success;
                    matchingToken.CompleteCycle = currentCycle;
                    matchingToken.FailureReason = success
                        ? null
                        : $"MemorySubsystem {(request.IsRead ? "read" : "write")} request failed after bank admission at 0x{request.Address:X} for {request.Length} byte(s).";
                }
            }

            // Update statistics
            if (success)
            {
                TotalBursts++;
                TotalBytesTransferred += request.Length;
            }

            // Release bank after operation
            bankOccupied[bankId] = false;
            ReleasePortAfterOperation(portId, bankId, request.IsRead);
        }

        /// <summary>
        /// Reset all performance statistics
        /// </summary>
        public void ResetStatistics()
        {
            TotalBursts = 0;
            TotalBytesTransferred = 0;
            BankConflicts = 0;
            StallCycles = 0;
            DmaTransfers = 0;
            TotalWaitCycles = 0;
            MaxQueueDepth = 0;

            // Phase 3: Reset burst timing metrics
            UnalignedAccessCount = 0;
            TotalAlignmentPenalty = 0;
            BurstEfficiency = 0.0;
            TotalBurstTimingCycles = 0;

            // Clear all bank queues
            for (int i = 0; i < NumBanks; i++)
            {
                bankQueues[i].Clear();
            }
        }

        /// <summary>
        /// Advance the internal cycle counter (for simulation)
        /// </summary>
        public void AdvanceCycles(long cycles)
        {
            currentCycle += cycles;

            // Advance L3 cache cycle counter
            L3Cache.AdvanceCycle();

            // === TRB Issue Phase: find next transaction whose bank is free ===
            ReadOnlySpan<bool> bankBusySpan = bankOccupied.AsSpan();
            int issuableIdx = TRB.FindNextIssuable(bankBusySpan);
            if (issuableIdx >= 0)
            {
                var entry = TRB.GetEntry(issuableIdx);
                if (entry.TargetBank >= 0 && entry.TargetBank < NumBanks)
                    bankOccupied[entry.TargetBank] = true;

                // Mark complete immediately (single-cycle model)
                TRB.Complete(issuableIdx, (ulong)currentCycle);
            }

            // === TRB Retire Phase: in-order commit ===
            while (TRB.TryRetire(out var retired))
            {
                if (retired.TargetBank >= 0 && retired.TargetBank < NumBanks)
                    bankOccupied[retired.TargetBank] = false;
            }

            // Process any queued requests
            ProcessBankQueues();

            // Release banks that have been idle for sufficient time
            for (int i = 0; i < NumBanks; i++)
            {
                if (bankOccupied[i] && (currentCycle - bankLastAccessCycle[i]) > 100)
                {
                    bankOccupied[i] = false;
                }
            }

            AdvanceSamplingEpoch();
        }

        #endregion

        #region Event Handlers

        protected virtual void OnBurstStarted(ulong address, int length, bool isRead, int bankId)
        {
            BurstStarted?.Invoke(this, new BurstEventArgs
            {
                Address = address,
                Length = length,
                IsRead = isRead,
                BankId = bankId,
                Timestamp = currentCycle
            });
        }

        protected virtual void OnBurstCompleted(ulong address, int length, bool isRead, int bankId)
        {
            BurstCompleted?.Invoke(this, new BurstEventArgs
            {
                Address = address,
                Length = length,
                IsRead = isRead,
                BankId = bankId,
                Timestamp = currentCycle
            });
        }

        #endregion

        #region Phase 2: Microarchitecture Fidelity

        /// <summary>
        /// Calculate port switching penalty when a port needs to switch from one bank to another.
        /// (Phase 2: Requirement 2.2)
        /// </summary>
        /// <param name="portId">Port ID to use</param>
        /// <param name="newBankId">Bank ID to switch to</param>
        /// <returns>Number of penalty cycles (0 if no switch needed)</returns>
        private int CalculatePortSwitchPenalty(int portId, int newBankId)
        {
            if (portId < 0 || portId >= _portStates.Length)
                return 0;

            ref var port = ref _portStates[portId];

            // If port is switching to a different bank, apply penalty
            if (port.CurrentBankId != -1 && port.CurrentBankId != newBankId)
            {
                return PortSwitchingPenalty;
            }

            return 0; // No penalty if same bank or port was idle
        }

        /// <summary>
        /// Calculate memory latency based on row buffer state.
        /// (Phase 2: Requirement 2.2)
        /// </summary>
        /// <param name="address">Memory address to access</param>
        /// <param name="size">Size of access in bytes</param>
        /// <returns>Latency in cycles</returns>
        private int CalculateMemoryLatency(ulong address, int size)
        {
            // Calculate row address
            ulong rowAddress = address / (ulong)RowBufferSize;

            int bankId = ComputeBankId(address);
            ulong lastRowAddress = bankLastAccessCycle[bankId] > 0
                ? (ulong)bankLastAccessCycle[bankId] / (ulong)RowBufferSize
                : ulong.MaxValue;

            // Row buffer hit if accessing same row
            if (rowAddress == lastRowAddress)
            {
                return RowBufferHitLatency;
            }

            // Row buffer miss - need to load new row
            return RowBufferMissLatency;
        }

        /// <summary>
        /// Allocate a port for accessing a bank, updating port state.
        /// (Phase 2: Requirement 2.2)
        /// </summary>
        /// <param name="bankId">Bank ID to access</param>
        /// <returns>Port ID that was allocated</returns>
        private int AllocatePortForBank(int bankId)
        {
            return AllocatePortForBank(bankId, isRead: true);
        }

        /// <summary>
        /// Allocate a port for accessing a bank with direction-aware turnaround.
        /// </summary>
        private int AllocatePortForBank(int bankId, bool isRead)
        {
            // Prefer idle ports that are already ready for the requested direction.
            for (int i = 0; i < _portStates.Length; i++)
            {
                if (!_portStates[i].Busy && IsPortDirectionReady(i, isRead))
                {
                    _portStates[i].Busy = true;
                    _portStates[i].CurrentBankId = bankId;
                    _portStates[i].LastSwitchCycle = currentCycle;
                    return i;
                }
            }

            // Then fall back to any idle port if the caller forces the issue.
            for (int i = 0; i < _portStates.Length; i++)
            {
                if (!_portStates[i].Busy)
                {
                    _portStates[i].Busy = true;
                    _portStates[i].CurrentBankId = bankId;
                    _portStates[i].LastSwitchCycle = currentCycle;
                    return i;
                }
            }

            int portId = bankId % _portStates.Length;
            _portStates[portId].CurrentBankId = bankId;
            _portStates[portId].LastSwitchCycle = currentCycle;
            return portId;
        }

        /// <summary>
        /// Release a port after completing an operation.
        /// (Phase 2: Requirement 2.2)
        /// </summary>
        /// <param name="portId">Port ID to release</param>
        private void ReleasePortAfterOperation(int portId)
        {
            ReleasePortAfterOperation(portId, bankId: -1, isRead: true);
        }

        /// <summary>
        /// Release a port after completing an operation and update the
        /// direction-ready timestamp for the next sampled occupancy snapshot.
        /// </summary>
        private void ReleasePortAfterOperation(int portId, int bankId, bool isRead)
        {
            if (portId >= 0 && portId < _portStates.Length)
            {
                _portStates[portId].Busy = false;
                if (bankId >= 0)
                {
                    _portStates[portId].CurrentBankId = bankId;
                }

                _portStates[portId].LastSwitchCycle = currentCycle;
                _portStates[portId].HasLastDirection = true;
                _portStates[portId].LastWasRead = isRead;
                _portStates[portId].DirectionReadyCycle = currentCycle + ReadWriteTurnaroundPenalty;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool IsPortDirectionReady(int portId, bool isRead)
        {
            if ((uint)portId >= (uint)_portStates.Length)
            {
                return false;
            }

            ref readonly PortState port = ref _portStates[portId];
            if (!port.HasLastDirection || port.LastWasRead == isRead)
            {
                return true;
            }

            return currentCycle >= port.DirectionReadyCycle;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int CountDirectionallyReadyPorts(bool isRead)
        {
            int readyPorts = 0;
            for (int i = 0; i < _portStates.Length; i++)
            {
                if (IsPortDirectionReady(i, isRead))
                {
                    readyPorts++;
                }
            }

            return readyPorts;
        }

        #endregion

        #region Phase 4: Accelerator DMA Support (fail-closed)

        /// <summary>
        /// Register accelerator device.
        /// </summary>
        public void RegisterAcceleratorDevice(ulong deviceId, YAKSys_Hybrid_CPU.Execution.AcceleratorDMACapabilities capabilities)
        {
            AcceleratorRuntimeFailClosed.ThrowRegistrationNotSupported();
        }

        /// <summary>
        /// Initiate accelerator DMA transfer.
        /// </summary>
        public YAKSys_Hybrid_CPU.Execution.DMATransferToken InitiateAcceleratorDMA(ulong deviceId, ulong srcAddr, ulong dstAddr, int size)
        {
            return AcceleratorRuntimeFailClosed.ThrowTransferNotSupported();
        }

        #endregion
    }
}
