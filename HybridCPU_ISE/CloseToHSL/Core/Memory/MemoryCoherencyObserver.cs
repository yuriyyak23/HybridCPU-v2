using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core.Memory
{
    public enum MemoryCoherencyWriteSourceKind : byte
    {
        Unknown = 0,
        CpuPhysicalWrite = 1,
        AtomicPhysicalWrite = 2,
        DmaControllerWrite = 3,
        DmaStreamComputeCommit = 4,
        StreamEngineWrite = 5,
        L7AcceleratorCommit = 6,
        PhysicalBurstBackendWrite = 7,
        ModelOrchestration = 8
    }

    public readonly record struct MemoryCoherencyWriteNotification(
        ulong Address,
        ulong Length,
        ulong DomainTag,
        MemoryCoherencyWriteSourceKind SourceKind)
    {
        public bool IsWellFormed =>
            MemoryRangeOverlap.IsNonEmptyWellFormed(Address, Length);
    }

    public readonly record struct DataCacheRangeInvalidationResult(
        bool RangeWellFormed,
        int L1LinesInvalidated,
        int L2LinesInvalidated,
        int AssistResidentLinesInvalidated)
    {
        public int TotalLinesInvalidated =>
            L1LinesInvalidated + L2LinesInvalidated;

        public bool Succeeded => RangeWellFormed;

        public static DataCacheRangeInvalidationResult Empty { get; } =
            new(true, 0, 0, 0);
    }

    public readonly record struct DataCacheRangeFlushResult(
        bool RangeWellFormed,
        bool IsNoOpProof,
        bool RequiresFutureDirtyWriteback,
        int CleanLinesObserved,
        int L1DirtyLines,
        int L2DirtyLines)
    {
        public int DirtyLinesObserved => L1DirtyLines + L2DirtyLines;

        public bool Succeeded =>
            RangeWellFormed && !RequiresFutureDirtyWriteback;

        public static DataCacheRangeFlushResult EmptyNoOpProof { get; } =
            new(true, true, false, 0, 0, 0);

        public DataCacheRangeFlushResult Merge(DataCacheRangeFlushResult other) =>
            new(
                RangeWellFormed && other.RangeWellFormed,
                IsNoOpProof && other.IsNoOpProof,
                RequiresFutureDirtyWriteback || other.RequiresFutureDirtyWriteback,
                CleanLinesObserved + other.CleanLinesObserved,
                L1DirtyLines + other.L1DirtyLines,
                L2DirtyLines + other.L2DirtyLines);
    }

    public readonly record struct MemoryCoherencyObserverResult(
        bool NotificationWellFormed,
        int DataCacheLinesInvalidated,
        int AssistResidentLinesInvalidated,
        int SrfWindowsInvalidated)
    {
        public bool Succeeded => NotificationWellFormed;

        public static MemoryCoherencyObserverResult Empty { get; } =
            new(true, 0, 0, 0);
    }

    public readonly struct MemoryCoherencyCacheParticipant
    {
        private readonly Processor.CPU_Core.Cache_Data_Object[]? _l1Data;
        private readonly Processor.CPU_Core.Cache_Data_Object[]? _l2Data;

        public MemoryCoherencyCacheParticipant(
            Processor.CPU_Core.Cache_Data_Object[]? l1Data,
            Processor.CPU_Core.Cache_Data_Object[]? l2Data)
        {
            _l1Data = l1Data;
            _l2Data = l2Data;
        }

        public DataCacheRangeInvalidationResult InvalidateRange(
            ulong address,
            ulong length,
            ulong domainTag = 0) =>
            InvalidateArrays(_l1Data, _l2Data, address, length, domainTag);

        public DataCacheRangeFlushResult FlushRange(
            ulong address,
            ulong length,
            ulong domainTag = 0) =>
            FlushArrays(_l1Data, _l2Data, address, length, domainTag);

        public static DataCacheRangeInvalidationResult InvalidateArrays(
            Processor.CPU_Core.Cache_Data_Object[]? l1Data,
            Processor.CPU_Core.Cache_Data_Object[]? l2Data,
            ulong address,
            ulong length,
            ulong domainTag = 0)
        {
            if (!MemoryRangeOverlap.IsNonEmptyWellFormed(address, length))
            {
                return new DataCacheRangeInvalidationResult(false, 0, 0, 0);
            }

            int assistInvalidated = 0;
            int l1Invalidated = InvalidateArray(
                l1Data,
                address,
                length,
                domainTag,
                ref assistInvalidated);
            int l2Invalidated = InvalidateArray(
                l2Data,
                address,
                length,
                domainTag,
                ref assistInvalidated);

            return new DataCacheRangeInvalidationResult(
                true,
                l1Invalidated,
                l2Invalidated,
                assistInvalidated);
        }

        public static DataCacheRangeFlushResult FlushArrays(
            Processor.CPU_Core.Cache_Data_Object[]? l1Data,
            Processor.CPU_Core.Cache_Data_Object[]? l2Data,
            ulong address,
            ulong length,
            ulong domainTag = 0)
        {
            if (!MemoryRangeOverlap.IsNonEmptyWellFormed(address, length))
            {
                return new DataCacheRangeFlushResult(
                    false,
                    false,
                    false,
                    0,
                    0,
                    0);
            }

            int cleanLines = 0;
            int l1Dirty = CountOverlappingDirtyLines(
                l1Data,
                address,
                length,
                domainTag,
                ref cleanLines);
            int l2Dirty = CountOverlappingDirtyLines(
                l2Data,
                address,
                length,
                domainTag,
                ref cleanLines);
            bool hasDirtyLines = l1Dirty != 0 || l2Dirty != 0;

            return new DataCacheRangeFlushResult(
                true,
                !hasDirtyLines,
                hasDirtyLines,
                cleanLines,
                l1Dirty,
                l2Dirty);
        }

        private static int InvalidateArray(
            Processor.CPU_Core.Cache_Data_Object[]? cache,
            ulong address,
            ulong length,
            ulong domainTag,
            ref int assistInvalidated)
        {
            if (cache is null)
            {
                return 0;
            }

            int invalidated = 0;
            for (int index = 0; index < cache.Length; index++)
            {
                Processor.CPU_Core.Cache_Data_Object line = cache[index];
                if (!IsLiveLine(line) ||
                    !DomainMatches(line.DomainTag, domainTag) ||
                    !MemoryRangeOverlap.RangesOverlap(
                        address,
                        length,
                        line.DataCache_MemoryAddress,
                        line.DataCache_DataLenght))
                {
                    continue;
                }

                if (line.AssistResident)
                {
                    assistInvalidated++;
                }

                cache[index] = default;
                invalidated++;
            }

            return invalidated;
        }

        private static int CountOverlappingDirtyLines(
            Processor.CPU_Core.Cache_Data_Object[]? cache,
            ulong address,
            ulong length,
            ulong domainTag,
            ref int cleanLines)
        {
            if (cache is null)
            {
                return 0;
            }

            int dirtyLines = 0;
            for (int index = 0; index < cache.Length; index++)
            {
                Processor.CPU_Core.Cache_Data_Object line = cache[index];
                if (!IsLiveLine(line) ||
                    !DomainMatches(line.DomainTag, domainTag) ||
                    !MemoryRangeOverlap.RangesOverlap(
                        address,
                        length,
                        line.DataCache_MemoryAddress,
                        line.DataCache_DataLenght))
                {
                    continue;
                }

                if (line.DataCache_IsDirty)
                {
                    dirtyLines++;
                }
                else
                {
                    cleanLines++;
                }
            }

            return dirtyLines;
        }

        private static bool IsLiveLine(
            Processor.CPU_Core.Cache_Data_Object line) =>
            line.DataCache_DataLenght != 0 &&
            line.DataCache_StoredValue is { Length: > 0 };

        private static bool DomainMatches(ulong lineDomainTag, ulong notificationDomainTag) =>
            notificationDomainTag == 0 ||
            lineDomainTag == 0 ||
            lineDomainTag == notificationDomainTag;
    }

    /// <summary>
    /// Explicit non-coherent publication observer for modeled external writes.
    /// Callers must route write notifications through this object; it is not a
    /// coherent DMA/cache hierarchy, snoop fabric, or automatic CPU memory authority.
    /// </summary>
    public sealed class MemoryCoherencyObserver
    {
        private readonly List<MemoryCoherencyCacheParticipant> _cacheParticipants = new();
        private readonly List<StreamRegisterFile> _streamRegisterFiles = new();

        public MemoryCoherencyObserver RegisterDataCache(
            Processor.CPU_Core core)
        {
            _cacheParticipants.Add(
                new MemoryCoherencyCacheParticipant(core.L1_Data, core.L2_Data));
            return this;
        }

        public MemoryCoherencyObserver RegisterDataCache(
            Processor.CPU_Core.Cache_Data_Object[]? l1Data,
            Processor.CPU_Core.Cache_Data_Object[]? l2Data)
        {
            _cacheParticipants.Add(
                new MemoryCoherencyCacheParticipant(l1Data, l2Data));
            return this;
        }

        public MemoryCoherencyObserver RegisterStreamRegisterFile(
            StreamRegisterFile streamRegisterFile)
        {
            ArgumentNullException.ThrowIfNull(streamRegisterFile);
            _streamRegisterFiles.Add(streamRegisterFile);
            return this;
        }

        public MemoryCoherencyObserverResult NotifyWrite(
            MemoryCoherencyWriteNotification notification)
        {
            if (!notification.IsWellFormed)
            {
                return new MemoryCoherencyObserverResult(false, 0, 0, 0);
            }

            int dataInvalidated = 0;
            int assistInvalidated = 0;
            for (int index = 0; index < _cacheParticipants.Count; index++)
            {
                DataCacheRangeInvalidationResult result =
                    _cacheParticipants[index].InvalidateRange(
                        notification.Address,
                        notification.Length,
                        notification.DomainTag);
                dataInvalidated += result.TotalLinesInvalidated;
                assistInvalidated += result.AssistResidentLinesInvalidated;
            }

            int srfInvalidated = 0;
            for (int index = 0; index < _streamRegisterFiles.Count; index++)
            {
                srfInvalidated += _streamRegisterFiles[index]
                    .InvalidateOverlappingRangeAndCount(
                        notification.Address,
                        notification.Length);
            }

            return new MemoryCoherencyObserverResult(
                true,
                dataInvalidated,
                assistInvalidated,
                srfInvalidated);
        }

        public DataCacheRangeFlushResult FlushBeforeExternalRead(
            ulong address,
            ulong length,
            ulong domainTag = 0)
        {
            if (!MemoryRangeOverlap.IsNonEmptyWellFormed(address, length))
            {
                return new DataCacheRangeFlushResult(
                    false,
                    false,
                    false,
                    0,
                    0,
                    0);
            }

            DataCacheRangeFlushResult aggregate =
                DataCacheRangeFlushResult.EmptyNoOpProof;
            for (int index = 0; index < _cacheParticipants.Count; index++)
            {
                aggregate = aggregate.Merge(
                    _cacheParticipants[index].FlushRange(
                        address,
                        length,
                        domainTag));
            }

            return aggregate;
        }
    }
}
