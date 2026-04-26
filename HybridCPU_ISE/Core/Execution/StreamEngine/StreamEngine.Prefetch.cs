
using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Memory;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static partial class StreamEngine
    {
        private const uint MaxStreamRegisterBytes = 32u * 8u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MemorySubsystem? ResolveActiveMemorySubsystem(
            MemorySubsystem? memSub)
        {
            return memSub ?? Processor.Memory;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResolveSrfResidentChunkBudget(int elementSize, ulong requestedElementCount)
        {
            if (elementSize <= 0 || requestedElementCount == 0)
            {
                return 0;
            }

            ulong maxResidentElements = (ulong)MaxStreamRegisterBytes / (ulong)elementSize;
            if (maxResidentElements == 0)
            {
                return 0;
            }

            ulong boundedElementCount = Math.Min(requestedElementCount, maxResidentElements);
            return (uint)Math.Min(boundedElementCount, (ulong)uint.MaxValue);
        }

        /// <summary>
        /// Prefetch the next strip-mined chunk into the SRF using the active
        /// runtime memory backend, so the later BurstRead can truthfully bypass
        /// backend traffic instead of relying on a loading marker only.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PrefetchToStreamRegister(
            ulong sourceAddr,
            byte elemSize,
            uint elemCount,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            MemorySubsystem? activeMemorySubsystem = ResolveActiveMemorySubsystem(memSub);
            StreamRegisterFile? srfNullable = activeMemorySubsystem?.StreamRegisters;
            if (srfNullable == null || elemCount == 0 || elemSize == 0)
            {
                return;
            }

            uint residentChunkBudget = ResolveSrfResidentChunkBudget(elemSize, elemCount);
            if (residentChunkBudget == 0 || residentChunkBudget != elemCount)
            {
                return;
            }

            StreamRegisterFile srf = srfNullable;
            int regIdx = srf.AllocateRegister(sourceAddr, elemSize, residentChunkBudget);
            if (regIdx < 0)
            {
                return;
            }

            TryWarmNextStreamChunk(
                srf,
                regIdx,
                sourceAddr,
                elemSize,
                residentChunkBudget,
                assistOwned: false,
                memSub: activeMemorySubsystem);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryPrefetchToAssistStreamRegister(
            ulong sourceAddr,
            byte elemSize,
            uint elemCount,
            YAKSys_Hybrid_CPU.Core.AssistStreamRegisterPartitionPolicy partitionPolicy,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            MemorySubsystem? activeMemorySubsystem = ResolveActiveMemorySubsystem(memSub);
            StreamRegisterFile? srfNullable = activeMemorySubsystem?.StreamRegisters;
            if (srfNullable == null || elemCount == 0 || elemSize == 0)
            {
                return false;
            }

            uint residentChunkBudget = ResolveSrfResidentChunkBudget(elemSize, elemCount);
            if (residentChunkBudget == 0 || residentChunkBudget != elemCount)
            {
                return false;
            }

            StreamRegisterFile srf = srfNullable;
            if (!srf.TryAllocateAssistRegister(
                sourceAddr,
                elemSize,
                residentChunkBudget,
                partitionPolicy,
                out int regIdx,
                out YAKSys_Hybrid_CPU.Core.AssistStreamRegisterRejectKind rejectKind))
            {
                srf.RecordAssistScheduleReject(rejectKind);
                return false;
            }

            return TryWarmNextStreamChunk(
                srf,
                regIdx,
                sourceAddr,
                elemSize,
                residentChunkBudget,
                assistOwned: true,
                memSub: activeMemorySubsystem);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryWarmNextStreamChunk(
            StreamRegisterFile srf,
            int registerIndex,
            ulong sourceAddr,
            byte elemSize,
            uint elemCount,
            bool assistOwned,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (registerIndex < 0 || elemSize == 0 || elemCount == 0)
            {
                return false;
            }

            MemorySubsystem? activeMemorySubsystem = ResolveActiveMemorySubsystem(memSub);

            srf.RecordWarmAttempt(assistOwned);
            if (srf.GetRegisterState(registerIndex) == StreamRegisterFile.RegisterState.Valid)
            {
                srf.RecordWarmReuse(assistOwned);
                return true;
            }

            if (assistOwned)
            {
                srf.MarkAssistLoading(registerIndex);
            }
            else
            {
                srf.MarkLoading(registerIndex);
            }

            uint byteCount = (uint)Math.Min((ulong)elemSize * elemCount, (ulong)MaxStreamRegisterBytes);
            if (byteCount == 0)
            {
                srf.InvalidateRegister(registerIndex);
                return false;
            }

            if (!IOMMU.TryWarmTranslationForAssistRange(
                    deviceID: 0,
                    ioVirtualAddress: sourceAddr,
                    accessSize: byteCount,
                    requestedPermissions: IOMMUAccessPermissions.Read))
            {
                srf.RecordWarmTranslationReject();
                srf.InvalidateRegister(registerIndex);
                return false;
            }

            byte[] ingress = new byte[byteCount];
            if (!BurstIO.TryReadThroughActiveBackend(sourceAddr, ingress.AsSpan(0, (int)byteCount), activeMemorySubsystem) ||
                !srf.LoadRegister(registerIndex, ingress.AsSpan(0, (int)byteCount)))
            {
                srf.RecordWarmBackendReject();
                srf.InvalidateRegister(registerIndex);
                return false;
            }

            srf.RecordWarmSuccess(assistOwned);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryWarmPlanned2DChunk(
            ulong baseAddr,
            int elementSize,
            ulong elementCount,
            uint rowLength,
            ushort rowStride,
            ushort colStride,
            ulong startOffset,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (elementCount == 0 ||
                elementSize <= 0 ||
                rowLength == 0 ||
                colStride != elementSize)
            {
                return;
            }

            foreach (BurstSegment segment in BurstPlanner.Plan2D(
                         baseAddr + (startOffset * colStride),
                         elementCount,
                         elementSize,
                         rowLength,
                         rowStride,
                         colStride))
            {
                if (segment.Length <= 0 || segment.Length % elementSize != 0)
                {
                    continue;
                }

                uint segmentElements = (uint)(segment.Length / elementSize);
                if (segmentElements == 0)
                {
                    continue;
                }

                PrefetchToStreamRegister(segment.Address, (byte)elementSize, segmentElements, memSub);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryWarmIndexedIngressChunk(
            ulong destBase,
            ulong indexBase,
            int elementSize,
            int indexElementSize,
            ushort indexStride,
            ulong startElement,
            ulong elementCount,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (elementCount == 0 || elementCount > uint.MaxValue || elementSize <= 0)
            {
                return;
            }

            uint chunkElements = (uint)elementCount;
            PrefetchToStreamRegister(
                destBase + (startElement * (ulong)elementSize),
                (byte)elementSize,
                chunkElements,
                memSub);

            if (indexElementSize > 0 &&
                indexStride == indexElementSize)
            {
                PrefetchToStreamRegister(
                    indexBase + (startElement * (ulong)indexStride),
                    (byte)indexElementSize,
                    chunkElements,
                    memSub);
            }
        }

        /// <summary>
        /// Lane6/DMA-backed assist seam for landed stream-register-prefetch assists.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ScheduleLane6AssistPrefetch(
            ulong sourceAddr,
            byte elemSize,
            uint elemCount,
            YAKSys_Hybrid_CPU.Core.AssistStreamRegisterPartitionPolicy partitionPolicy,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (elemCount == 0 || elemSize == 0)
            {
                return false;
            }

            return TryPrefetchToAssistStreamRegister(
                sourceAddr,
                elemSize,
                elemCount,
                partitionPolicy,
                memSub);
        }

        /// <summary>
        /// Compatibility alias for older narrow assist callsites.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ScheduleAssistPrefetch(
            ulong sourceAddr,
            byte elemSize,
            uint elemCount,
            YAKSys_Hybrid_CPU.Core.AssistStreamRegisterPartitionPolicy partitionPolicy,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            return ScheduleLane6AssistPrefetch(
                sourceAddr,
                elemSize,
                elemCount,
                partitionPolicy,
                memSub);
        }
    }
}
