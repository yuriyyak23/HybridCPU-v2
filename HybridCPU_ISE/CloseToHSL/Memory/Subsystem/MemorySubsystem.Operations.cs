using HybridCPU_ISE.CloseToHSL.Memory.DMA;
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

            lock (geometryLifecycleGate)
            {
                ulong requestID = nextRequestID++;
                PhysicalMemoryBankIndex bankIndex = ComputeBankId(address);
                PhysicalMemoryBankBinding physicalBankBinding =
                    PhysicalMemoryBankBinding.Create(
                        bankIndex,
                        _publishedPhysicalBankGeometry.Generation);
                var token = new MemoryRequestToken(
                    requestID,
                    deviceID,
                    address,
                    size,
                    buffer,
                    isRead: true,
                    enqueueCycle: currentCycle,
                    defersPhysicalWriteUntilRetire: false,
                    physicalBankBinding: physicalBankBinding);

                // Store in pending requests
                lock (pendingRequests)
                {
                    pendingRequests[requestID] = token;
                }

                // Enqueue to appropriate bank
                var bankRequest = new BankRequest
                {
                    RequestID = requestID,
                    DeviceID = deviceID,
                    Address = address,
                    Length = size,
                    IsRead = true,
                    Priority = 5, // Default priority
                    EnqueueCycle = currentCycle,
                    Buffer = buffer,
                    PhysicalBankBinding = physicalBankBinding
                };

                bankQueues[physicalBankBinding.BankIndex.Value]
                    .Enqueue(bankRequest);

                return token;
            }
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

            lock (geometryLifecycleGate)
            {
                ulong requestID = nextRequestID++;
                PhysicalMemoryBankIndex bankIndex = ComputeBankId(address);
                PhysicalMemoryBankBinding physicalBankBinding =
                    PhysicalMemoryBankBinding.Create(
                        bankIndex,
                        _publishedPhysicalBankGeometry.Generation);
                var token = new MemoryRequestToken(
                    requestID,
                    deviceID,
                    address,
                    size,
                    buffer,
                    isRead: false,
                    enqueueCycle: currentCycle,
                    defersPhysicalWriteUntilRetire: deferPhysicalWriteUntilRetire,
                    physicalBankBinding: physicalBankBinding);

                // Store in pending requests
                lock (pendingRequests)
                {
                    pendingRequests[requestID] = token;
                }

                // Enqueue to appropriate bank
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
                    Buffer = buffer,
                    PhysicalBankBinding = physicalBankBinding
                };

                bankQueues[physicalBankBinding.BankIndex.Value]
                    .Enqueue(bankRequest);

                return token;
            }
        }

        public bool CancelPendingRequest(MemoryRequestToken? token)
        {
            return token != null && CancelPendingRequest(token.RequestID);
        }

        public bool CancelPendingRequest(ulong requestID)
        {
            if (requestID == 0)
                return false;

            lock (geometryLifecycleGate)
            {
                MemoryRequestToken? token;
                PhysicalMemoryBankGeometry geometry =
                    _publishedPhysicalBankGeometry;
                PhysicalMemoryBankBinding physicalBankBinding;
                lock (pendingRequests)
                {
                    if (!pendingRequests.TryGetValue(requestID, out token) || token.IsComplete)
                        return false;

                    physicalBankBinding =
                        token.GetPhysicalBankBindingForOwner();
                    if (!physicalBankBinding.IsWellFormed ||
                        physicalBankBinding.Generation != geometry.Generation ||
                        physicalBankBinding.BankIndex.Value >= geometry.BankCount)
                    {
                        return false;
                    }

                    pendingRequests.Remove(requestID);
                }

                return RemoveQueuedBankRequest(
                    requestID,
                    physicalBankBinding.BankIndex.Value);
            }
        }

        #endregion

    }
}
