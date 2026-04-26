using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Stream Register File for HybridCPU.
    /// Provides a local buffer pool for prefetching stream data.
    /// </summary>
    public partial class StreamRegisterFile
    {
        /// <summary>
        /// Maximum vector length (elements per register)
        /// </summary>
        private const int VLMAX = 32;

        /// <summary>
        /// Maximum element size (8 bytes for FLOAT64/INT64)
        /// </summary>
        private const int MAX_ELEMENT_SIZE = 8;

        /// <summary>
        /// Size of each stream register in bytes
        /// </summary>
        private const int REGISTER_SIZE = VLMAX * MAX_ELEMENT_SIZE;

        /// <summary>
        /// Number of stream registers
        /// </summary>
        private readonly int registerCount;

        /// <summary>
        /// Stream register state
        /// </summary>
        public enum RegisterState : byte
        {
            Invalid = 0,
            Loading = 1,
            Valid = 2,
            Dirty = 3
        }

        /// <summary>
        /// Stream register entry
        /// </summary>
        private struct RegisterEntry
        {
            public byte[] Data;
            public RegisterState State;
            public ulong SourceAddress;
            public uint ValidBytes;
            public ulong LastAccessTime;
            public byte ElementSize;
            public uint ElementCount;
            public bool AssistOwned;
        }

        private RegisterEntry[] registers;
        private ulong accessCounter;
        private ulong totalHits;
        private ulong totalMisses;
        private ulong totalEvictions;

        /// <summary>
        /// Counts actual SRF-served reads that bypassed the live memory backend.
        /// </summary>
        private ulong _l1BypassHits;

        /// <summary>
        /// Initialize Stream Register File
        /// </summary>
        public StreamRegisterFile(int numRegisters = 8)
        {
            registerCount = numRegisters;
            registers = new RegisterEntry[numRegisters];
            accessCounter = 0;
            totalHits = 0;
            totalMisses = 0;
            totalEvictions = 0;

            for (int i = 0; i < numRegisters; i++)
            {
                registers[i].Data = new byte[REGISTER_SIZE];
                registers[i].State = RegisterState.Invalid;
                registers[i].SourceAddress = 0;
                registers[i].ValidBytes = 0;
                registers[i].LastAccessTime = 0;
                registers[i].ElementSize = 0;
                registers[i].ElementCount = 0;
                registers[i].AssistOwned = false;
            }
        }

        /// <summary>
        /// Allocate a stream register for a memory region.
        /// Returns a cache hit only when the existing valid register already
        /// contains the full requested packed window.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AllocateRegister(ulong sourceAddr, byte elementSize, uint elementCount)
        {
            uint requestedBytes = ComputeRequestedByteCount(elementSize, elementCount);
            int matchingIndexNeedingReload = -1;

            for (int i = 0; i < registerCount; i++)
            {
                if (registers[i].State == RegisterState.Valid &&
                    registers[i].SourceAddress == sourceAddr &&
                    registers[i].ElementSize == elementSize)
                {
                    if (registers[i].ValidBytes < requestedBytes)
                    {
                        matchingIndexNeedingReload = i;
                        break;
                    }

                    registers[i].LastAccessTime = ++accessCounter;
                    registers[i].AssistOwned = false;
                    totalHits++;
                    return i;
                }
            }

            totalMisses++;
            int victimIndex = matchingIndexNeedingReload >= 0
                ? matchingIndexNeedingReload
                : FindVictimRegister();

            if (victimIndex == -1)
            {
                return -1;
            }

            if (registers[victimIndex].State == RegisterState.Dirty)
            {
                totalEvictions++;
            }

            registers[victimIndex].State = RegisterState.Invalid;
            registers[victimIndex].SourceAddress = sourceAddr;
            registers[victimIndex].ElementSize = elementSize;
            registers[victimIndex].ElementCount = elementCount;
            registers[victimIndex].ValidBytes = 0;
            registers[victimIndex].LastAccessTime = ++accessCounter;
            registers[victimIndex].AssistOwned = false;

            return victimIndex;
        }

        /// <summary>
        /// Find victim register for eviction (LRU policy)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindVictimRegister()
        {
            int victimIndex = -1;
            ulong oldestTime = ulong.MaxValue;

            for (int i = 0; i < registerCount; i++)
            {
                if (registers[i].State == RegisterState.Invalid)
                {
                    return i;
                }
            }

            for (int i = 0; i < registerCount; i++)
            {
                if (registers[i].State == RegisterState.Loading)
                {
                    continue;
                }

                if (registers[i].LastAccessTime < oldestTime)
                {
                    oldestTime = registers[i].LastAccessTime;
                    victimIndex = i;
                }
            }

            return victimIndex;
        }

        /// <summary>
        /// Load data into a stream register from a full memory image.
        /// Compatibility path for existing tests.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LoadRegister(int regIndex, byte[] memory)
        {
            if (regIndex < 0 || regIndex >= registerCount)
            {
                return false;
            }

            ref RegisterEntry reg = ref registers[regIndex];
            uint bytesToLoad = ComputeRequestedByteCount(reg.ElementSize, reg.ElementCount);
            if (reg.SourceAddress + bytesToLoad > (ulong)memory.Length)
            {
                return false;
            }

            return LoadRegister(
                regIndex,
                memory.AsSpan((int)reg.SourceAddress, (int)bytesToLoad));
        }

        /// <summary>
        /// Load already-materialized bytes into a stream register.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LoadRegister(int regIndex, ReadOnlySpan<byte> sourceData)
        {
            if (regIndex < 0 || regIndex >= registerCount)
            {
                return false;
            }

            ref RegisterEntry reg = ref registers[regIndex];
            uint bytesToLoad = ComputeRequestedByteCount(reg.ElementSize, reg.ElementCount);
            if (bytesToLoad == 0 || bytesToLoad > sourceData.Length)
            {
                return false;
            }

            reg.State = RegisterState.Loading;
            sourceData.Slice(0, (int)bytesToLoad).CopyTo(reg.Data);
            reg.State = RegisterState.Valid;
            reg.ValidBytes = bytesToLoad;
            reg.LastAccessTime = ++accessCounter;
            return true;
        }

        /// <summary>
        /// Read data from a stream register.
        /// Successful reads count as real SRF bypasses.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadRegister(int regIndex, Span<byte> destination, uint byteCount)
        {
            if (regIndex < 0 || regIndex >= registerCount)
            {
                return false;
            }

            ref RegisterEntry reg = ref registers[regIndex];
            if (reg.State != RegisterState.Valid)
            {
                return false;
            }

            if (byteCount > reg.ValidBytes || byteCount > destination.Length)
            {
                return false;
            }

            reg.Data.AsSpan(0, (int)byteCount).CopyTo(destination);
            reg.LastAccessTime = ++accessCounter;
            RecordBypassHit(reg.AssistOwned);
            return true;
        }

        /// <summary>
        /// Try to serve an exact packed contiguous chunk from SRF.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadPrefetchedChunk(
            ulong sourceAddr,
            byte elementSize,
            uint elementCount,
            Span<byte> destination)
        {
            uint requestedBytes = ComputeRequestedByteCount(elementSize, elementCount);
            if (requestedBytes == 0 || requestedBytes > destination.Length)
            {
                return false;
            }

            for (int i = 0; i < registerCount; i++)
            {
                ref RegisterEntry reg = ref registers[i];
                if (reg.State != RegisterState.Valid ||
                    reg.SourceAddress != sourceAddr ||
                    reg.ElementSize != elementSize ||
                    reg.ValidBytes < requestedBytes)
                {
                    continue;
                }

                return ReadRegister(i, destination, requestedBytes);
            }

            return false;
        }

        /// <summary>
        /// Write data to a stream register (marks as dirty)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteRegister(int regIndex, ReadOnlySpan<byte> source, uint byteCount)
        {
            if (regIndex < 0 || regIndex >= registerCount)
            {
                return false;
            }

            ref RegisterEntry reg = ref registers[regIndex];
            if (byteCount > REGISTER_SIZE || byteCount > source.Length)
            {
                return false;
            }

            source.Slice(0, (int)byteCount).CopyTo(reg.Data);
            reg.State = RegisterState.Dirty;
            reg.ValidBytes = byteCount;
            reg.AssistOwned = false;
            reg.LastAccessTime = ++accessCounter;
            return true;
        }

        /// <summary>
        /// Get register state
        /// </summary>
        public RegisterState GetRegisterState(int regIndex)
        {
            if (regIndex < 0 || regIndex >= registerCount)
            {
                return RegisterState.Invalid;
            }

            return registers[regIndex].State;
        }

        /// <summary>
        /// Invalidate a register
        /// </summary>
        public void InvalidateRegister(int regIndex)
        {
            if (regIndex < 0 || regIndex >= registerCount)
            {
                return;
            }

            registers[regIndex].State = RegisterState.Invalid;
            registers[regIndex].ValidBytes = 0;
            registers[regIndex].AssistOwned = false;
        }

        /// <summary>
        /// Invalidate all registers
        /// </summary>
        public void InvalidateAll()
        {
            for (int i = 0; i < registerCount; i++)
            {
                registers[i].State = RegisterState.Invalid;
                registers[i].ValidBytes = 0;
                registers[i].AssistOwned = false;
            }
        }

        /// <summary>
        /// Mark a register as Loading.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkLoading(int regIndex)
        {
            if (regIndex < 0 || regIndex >= registerCount)
            {
                return;
            }

            if (registers[regIndex].State == RegisterState.Invalid)
            {
                registers[regIndex].State = RegisterState.Loading;
            }
        }

        /// <summary>
        /// Invalidate any SRF window that overlaps the written address range.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvalidateOverlappingRange(ulong address, uint byteCount)
        {
            if (byteCount == 0)
            {
                return;
            }

            for (int i = 0; i < registerCount; i++)
            {
                ref RegisterEntry reg = ref registers[i];
                if (reg.State == RegisterState.Invalid)
                {
                    continue;
                }

                uint trackedBytes = GetTrackedByteCount(reg);
                if (trackedBytes == 0)
                {
                    continue;
                }

                if (RangesOverlap(address, byteCount, reg.SourceAddress, trackedBytes))
                {
                    InvalidateRegister(i);
                }
            }
        }

        /// <summary>
        /// Get performance statistics
        /// </summary>
        public (ulong hits, ulong misses, ulong evictions, double hitRate, ulong l1BypassHits) GetStatistics()
        {
            ulong total = totalHits + totalMisses;
            double hitRate = total > 0 ? (double)totalHits / total : 0.0;
            return (totalHits, totalMisses, totalEvictions, hitRate, _l1BypassHits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ComputeRequestedByteCount(byte elementSize, uint elementCount)
        {
            if (elementSize == 0 || elementCount == 0)
            {
                return 0;
            }

            ulong requestedBytes = (ulong)elementSize * elementCount;
            return (uint)Math.Min((ulong)REGISTER_SIZE, requestedBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetTrackedByteCount(in RegisterEntry reg)
        {
            return reg.State == RegisterState.Valid
                ? reg.ValidBytes
                : ComputeRequestedByteCount(reg.ElementSize, reg.ElementCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RangesOverlap(
            ulong addressA,
            uint byteCountA,
            ulong addressB,
            uint byteCountB)
        {
            ulong endA = addressA + byteCountA;
            ulong endB = addressB + byteCountB;
            return addressA < endB && addressB < endA;
        }
    }
}
