using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU.Memory
{
    public partial class MemorySubsystem
    {
        #region Asynchronous Memory Operations

        /// <summary>
        /// Enqueue an asynchronous read request
        /// Returns a token that can be polled for completion
        /// </summary>
        /// <param name="deviceID">Device initiating the request</param>
        /// <param name="address">Memory address to read from</param>
        /// <param name="size">Number of bytes to read</param>
        /// <param name="buffer">Buffer to store the read data</param>
        /// <returns>Token for tracking request completion</returns>
        public MemoryRequestToken EnqueueRead(ulong deviceID, ulong address, int size, byte[] buffer)
        {
            if (buffer == null || buffer.Length < size)
            {
                throw new ArgumentException("Buffer is null or too small");
            }

            // Create request token
            ulong requestID = nextRequestID++;
            var token = new MemoryRequestToken(requestID, deviceID, address, size, buffer, true, currentCycle);

            // Store in pending requests
            lock (pendingRequests)
            {
                pendingRequests[requestID] = token;
            }

            // Enqueue to appropriate bank
            int bankId = ComputeBankId(address);
            var bankRequest = new BankRequest
            {
                RequestID = requestID,
                DeviceID = deviceID,
                Address = address,
                Length = size,
                IsRead = true,
                Priority = 5, // Default priority
                EnqueueCycle = currentCycle,
                Buffer = buffer
            };

            bankQueues[bankId].Enqueue(bankRequest);

            return token;
        }

        /// <summary>
        /// Enqueue an asynchronous write request
        /// Returns a token that can be polled for completion
        /// </summary>
        /// <param name="deviceID">Device initiating the request</param>
        /// <param name="address">Memory address to write to</param>
        /// <param name="size">Number of bytes to write</param>
        /// <param name="buffer">Buffer containing the data to write</param>
        /// <returns>Token for tracking request completion</returns>
        public MemoryRequestToken EnqueueWrite(
            ulong deviceID,
            ulong address,
            int size,
            byte[] buffer,
            bool deferPhysicalWriteUntilRetire = false)
        {
            if (buffer == null || buffer.Length < size)
            {
                throw new ArgumentException("Buffer is null or too small");
            }

            // Create request token
            ulong requestID = nextRequestID++;
            var token = new MemoryRequestToken(
                requestID,
                deviceID,
                address,
                size,
                buffer,
                isRead: false,
                enqueueCycle: currentCycle,
                defersPhysicalWriteUntilRetire: deferPhysicalWriteUntilRetire);

            // Store in pending requests
            lock (pendingRequests)
            {
                pendingRequests[requestID] = token;
            }

            // Enqueue to appropriate bank
            int bankId = ComputeBankId(address);
            var bankRequest = new BankRequest
            {
                RequestID = requestID,
                DeviceID = deviceID,
                Address = address,
                Length = size,
                IsRead = false,
                DefersPhysicalWriteUntilRetire = deferPhysicalWriteUntilRetire,
                Priority = 5, // Default priority
                EnqueueCycle = currentCycle,
                Buffer = buffer
            };

            bankQueues[bankId].Enqueue(bankRequest);

            return token;
        }

        public bool CancelPendingRequest(MemoryRequestToken? token)
        {
            return token != null && CancelPendingRequest(token.RequestID);
        }

        public bool CancelPendingRequest(ulong requestID)
        {
            if (requestID == 0)
                return false;

            MemoryRequestToken? token;
            lock (pendingRequests)
            {
                if (!pendingRequests.TryGetValue(requestID, out token) || token.IsComplete)
                    return false;

                pendingRequests.Remove(requestID);
            }

            return RemoveQueuedBankRequest(requestID, ComputeBankId(token.Address));
        }

        #endregion

        #region DMA Routing

        private bool ReadViaDMA(ulong deviceID, ulong address, Span<byte> buffer)
        {
            if (dma == null) return false;

            // Allocate temporary buffer for DMA
            byte[] tempBuffer = new byte[buffer.Length];

            // Create DMA transfer descriptor
            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = address,
                DestAddress = 0, // Will write to buffer directly
                TransferSize = (uint)buffer.Length,
                SourceStride = 0, // Contiguous
                DestStride = 0,   // Contiguous
                ElementSize = 1,
                UseIOMMU = true,
                NextDescriptor = 0,
                ChannelID = 0, // Auto-assign channel 0 for reads
                Priority = 128 // Medium priority
            };

            // Track this DMA operation
            int bankId = ComputeBankId(address);
            var pendingOp = new PendingDmaOperation
            {
                BankId = bankId,
                Address = address,
                Length = buffer.Length,
                IsRead = true
            };

            lock (pendingDmaOps)
            {
                pendingDmaOps[descriptor.ChannelID] = pendingOp;
            }

            // Configure and start DMA transfer
            if (!dma.ConfigureTransfer(descriptor))
            {
                // Channel busy, fall back to IOMMU
                lock (pendingDmaOps)
                {
                    pendingDmaOps.Remove(descriptor.ChannelID);
                }
                return IOMMU.ReadBurst(deviceID, address, buffer);
            }

            if (!dma.StartTransfer(descriptor.ChannelID))
            {
                lock (pendingDmaOps)
                {
                    pendingDmaOps.Remove(descriptor.ChannelID);
                }
                dma.ResetChannel(descriptor.ChannelID);
                return IOMMU.ReadBurst(deviceID, address, buffer);
            }

            // Execute DMA cycles to completion
            // In a real hardware implementation, CPU would continue other work
            int maxCycles = 10000;
            int cycles = 0;
            while (dma.GetChannelState(descriptor.ChannelID) == DMAController.ChannelState.Active && cycles < maxCycles)
            {
                dma.ExecuteCycle();
                cycles++;
            }

            // Check completion
            bool success = dma.GetChannelState(descriptor.ChannelID) == DMAController.ChannelState.Completed;

            if (success)
            {
                // Read the data via IOMMU (DMA has completed the transfer in background)
                success = IOMMU.ReadBurst(deviceID, address, buffer);
            }

            // Cleanup
            dma.ResetChannel(descriptor.ChannelID);
            lock (pendingDmaOps)
            {
                pendingDmaOps.Remove(descriptor.ChannelID);
            }

            return success;
        }

        private bool WriteViaDMA(ulong deviceID, ulong address, ReadOnlySpan<byte> buffer)
        {
            if (dma == null) return false;

            // Copy to temporary buffer
            byte[] tempBuffer = new byte[buffer.Length];
            buffer.CopyTo(tempBuffer);

            // Create DMA transfer descriptor
            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = 0, // Will read from buffer directly
                DestAddress = address,
                TransferSize = (uint)buffer.Length,
                SourceStride = 0, // Contiguous
                DestStride = 0,   // Contiguous
                ElementSize = 1,
                UseIOMMU = true,
                NextDescriptor = 0,
                ChannelID = 1, // Auto-assign channel 1 for writes
                Priority = 128 // Medium priority
            };

            // Track this DMA operation
            int bankId = ComputeBankId(address);
            var pendingOp = new PendingDmaOperation
            {
                BankId = bankId,
                Address = address,
                Length = buffer.Length,
                IsRead = false
            };

            lock (pendingDmaOps)
            {
                pendingDmaOps[descriptor.ChannelID] = pendingOp;
            }

            // Configure and start DMA transfer
            if (!dma.ConfigureTransfer(descriptor))
            {
                // Channel busy, fall back to IOMMU
                lock (pendingDmaOps)
                {
                    pendingDmaOps.Remove(descriptor.ChannelID);
                }
                return IOMMU.WriteBurst(deviceID, address, buffer);
            }

            if (!dma.StartTransfer(descriptor.ChannelID))
            {
                lock (pendingDmaOps)
                {
                    pendingDmaOps.Remove(descriptor.ChannelID);
                }
                dma.ResetChannel(descriptor.ChannelID);
                return IOMMU.WriteBurst(deviceID, address, buffer);
            }

            // Execute DMA cycles to completion
            int maxCycles = 10000;
            int cycles = 0;
            while (dma.GetChannelState(descriptor.ChannelID) == DMAController.ChannelState.Active && cycles < maxCycles)
            {
                dma.ExecuteCycle();
                cycles++;
            }

            // Check completion
            bool success = dma.GetChannelState(descriptor.ChannelID) == DMAController.ChannelState.Completed;

            // Cleanup
            dma.ResetChannel(descriptor.ChannelID);
            lock (pendingDmaOps)
            {
                pendingDmaOps.Remove(descriptor.ChannelID);
            }

            return success;
        }

        /// <summary>
        /// Handle DMA transfer completion event
        /// </summary>
        private void OnDmaTransferCompleted(object? sender, DMAController.TransferCompletedEventArgs e)
        {
            PendingDmaOperation op;
            lock (pendingDmaOps)
            {
                if (!pendingDmaOps.TryGetValue(e.ChannelID, out op))
                    return;

                pendingDmaOps.Remove(e.ChannelID);
            }

            // Release the bank that was occupied by this DMA operation
            if (op.BankId >= 0 && op.BankId < NumBanks)
            {
                bankOccupied[op.BankId] = false;
            }

            // Fire burst completed event
            OnBurstCompleted(op.Address, op.Length, op.IsRead, op.BankId);
        }

        #endregion
    }
}