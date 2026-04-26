using HybridCPU_ISE.Arch;
using System;
using System.IO;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            ulong ulong_MinL1Query;

            public Cache_VLIWBundle_Object[] L1_VLIWBundles;
            public Cache_Data_Object[] L1_Data; // 2048*32bytes=65536 data items. Only for MemPtr (Memory Array)

            public void PrefetchRegisterData(ulong MemoryAddress, ulong domainTag = 0) // Only for MemPtr (Memory Array) // only 32bytes (256bit - FP and VectorRegisters)
            {
                for (int int_CycleCount = 0; int_CycleCount != L1_Data.Length; int_CycleCount++)
                {
                    if (L1_Data[int_CycleCount].DataCache_MemoryAddress == MemoryAddress)
                    {
                        // Phase §2: Domain isolation — treat cross-domain hit as miss
                        if (domainTag != 0 && L1_Data[int_CycleCount].DomainTag != 0 &&
                            L1_Data[int_CycleCount].DomainTag != domainTag)
                        {
                            continue;
                        }

                        L1_Data[int_CycleCount].AssistResident = false;
                        return;
                    }
                }

                Current_DataObject_Position++;

                L1_Data[Current_DataObject_Position] = GetDataByPointer(MemoryAddress, domainTag);
            }

            public void PrefetchVLIWBundle(ulong MemoryAddress)
            {
                _hasMaterializedVliwFetchState = true;
                Current_VLIWBundle_Position++;

                L1_VLIWBundles[Current_VLIWBundle_Position] = GetVLIWBundleByPointer(MemoryAddress);
            }

            public Cache_Data_Object GetDataByMemPtr(ulong MemoryAddress, ulong domainTag = 0) // Only for MemPtr (Memory Array) // only 32bytes (256bit - FP and VectorRegisters)
            {
                for (int int_CycleCount = 0; int_CycleCount != L1_Data.Length; int_CycleCount++)
                {
                    if (L1_Data[int_CycleCount].DataCache_MemoryAddress == MemoryAddress)
                    {
                        // Phase §2: Domain isolation — treat cross-domain hit as miss
                        if (domainTag != 0 && L1_Data[int_CycleCount].DomainTag != 0 &&
                            L1_Data[int_CycleCount].DomainTag != domainTag)
                        {
                            continue;
                        }

                        L1_Data[int_CycleCount].AssistResident = false;
                        return L1_Data[int_CycleCount];
                    }
                }

                Current_DataObject_Position++;

                L1_Data[Current_DataObject_Position] = GetDataByPointer(MemoryAddress, domainTag);

                return L1_Data[Current_DataObject_Position];
            }

            ulong Current_VLIWBundle_Position, Current_DataObject_Position;
            public Cache_VLIWBundle_Object GetVLIWBundleByPointer(ulong MemoryAddress)
            {
                _hasMaterializedVliwFetchState = true;

                if (L1_VLIWBundles == null || L1_Data == null)
                {
                    L1_VLIWBundles = new Cache_VLIWBundle_Object[256];

                    L1_Data = new Cache_Data_Object[2048]; // 2048*32bytes=65536 data items. Only for MemPtr (Memory Array)
                }

                {
                    for (int int_CycleCount = 0; int_CycleCount != L1_VLIWBundles.Length; int_CycleCount++)
                    {
                        if (L1_VLIWBundles[int_CycleCount].VLIWCache_MemoryAddress == MemoryAddress && MemoryAddress > 0)
                        {
                            L1_VLIWBundles[int_CycleCount].VLIWCache_L1QueryCounter++;

                            return L1_VLIWBundles[int_CycleCount];
                        }
                    }

                    Current_VLIWBundle_Position++;

                    ulong_MinL1Query = ulong.MaxValue;

                    for (int int_CycleCount = 0; int_CycleCount != L1_VLIWBundles.Length; int_CycleCount++)
                    {
                        if (L1_VLIWBundles[int_CycleCount].VLIWCache_L1QueryCounter < ulong_MinL1Query)
                        {
                            ulong_MinL1Query = L1_VLIWBundles[int_CycleCount].VLIWCache_L1QueryCounter;

                            int_CycleCount = 0;

                            Current_VLIWBundle_Position = (ulong)int_CycleCount;

                            continue;
                        }
                    }

                    L1_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_L1QueryCounter++;

                    L1_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_MemoryAddress = MemoryAddress;

                    Cache_VLIWBundle_Object fetchedBundle =
                        GetL2_VLIWBundleByPointer(
                            L1_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_MemoryAddress);
                    L1_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_VLIWBundle =
                        fetchedBundle.VLIWCache_VLIWBundle;
                    L1_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_BundleAnnotations =
                        fetchedBundle.VLIWCache_BundleAnnotations;
                    L1_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_HasAnnotationCarrier =
                        fetchedBundle.VLIWCache_HasAnnotationCarrier;
                    LoadTo_Cache_L2(L1_VLIWBundles[Current_VLIWBundle_Position]);

                    return L1_VLIWBundles[Current_VLIWBundle_Position];
                }
            }

            /// <summary>
            /// Invalidates fetch-side state for one emitted VLIW bundle address.
            /// This flushes stale bundle cache lines and any replayed loop-buffer state.
            /// </summary>
            public void InvalidateVliwFetchState(ulong memoryAddress)
            {
                if (!_hasMaterializedVliwFetchState)
                {
                    _loopBuffer.Invalidate(Core.ReplayPhaseInvalidationReason.CertificateMutation);
                    PublishReplayPhaseContextIfNeeded(_fspScheduler, _loopBuffer.CurrentReplayPhase);
                    return;
                }

                InvalidateVliwBundleCacheLine(L1_VLIWBundles, memoryAddress);
                InvalidateVliwBundleCacheLine(L2_VLIWBundles, memoryAddress);
                _loopBuffer.Invalidate(Core.ReplayPhaseInvalidationReason.CertificateMutation);
                PublishReplayPhaseContextIfNeeded(_fspScheduler, _loopBuffer.CurrentReplayPhase);
            }

            private static void InvalidateVliwBundleCacheLine(Cache_VLIWBundle_Object[]? cache, ulong memoryAddress)
            {
                if (cache == null)
                {
                    return;
                }

                for (int index = 0; index < cache.Length; index++)
                {
                    if (cache[index].VLIWCache_MemoryAddress == memoryAddress)
                    {
                        cache[index] = default;
                    }
                }
            }

            public struct Cache_VLIWBundle_Object
            {
                public ulong VLIWCache_MemoryAddress, VLIWCache_L1QueryCounter, VLIWCache_L2QueryCounter;
                public byte[] VLIWCache_VLIWBundle;
                public VliwBundleAnnotations? VLIWCache_BundleAnnotations;
                public bool VLIWCache_HasAnnotationCarrier;
            }
            public struct Cache_Data_Object
            {
                public ulong DataCache_MemoryAddress, DataCache_DataLenght, DataCache_L1QueryCounter, DataCache_L2QueryCounter;
                public byte[] DataCache_StoredValue;

                /// <summary>
                /// Domain tag for Singularity-style cache isolation (Phase §2 Side-Channel hardening).
                /// Prevents cross-domain eviction side-channels: a lookup must match both address and domain.
                /// Zero = kernel / unrestricted.
                /// </summary>
                public ulong DomainTag;

                /// <summary>
                /// True when this L1 line currently belongs to the assist-only resident partition.
                /// Foreground hits clear this bit and reclaim the line.
                /// </summary>
                public bool AssistResident;

                /// <summary>
                /// Physical assist carrier that inserted this line.
                /// Structural-only bookkeeping for assist cache partitioning.
                /// </summary>
                public Core.AssistCarrierKind AssistCarrierKind;
            }

            public Cache_VLIWBundle_Object[] L2_VLIWBundles = new Cache_VLIWBundle_Object[65536];
            public Cache_Data_Object[] L2_Data = new Cache_Data_Object[65536];

            ulong ulong_MinL2Query;

            public void SyncSMPChache()
            {

            }
            public Cache_Data_Object GetDataByPointer(ulong MemoryAddress, ulong domainTag = 0)
            {
                for (int int_CycleCount = 0; int_CycleCount != L2_Data.Length; int_CycleCount++)
                {
                    if (L2_Data[int_CycleCount].DataCache_MemoryAddress == MemoryAddress)
                    {
                        // Phase §2: Domain isolation — treat cross-domain hit as miss
                        if (domainTag != 0 && L2_Data[int_CycleCount].DomainTag != 0 &&
                            L2_Data[int_CycleCount].DomainTag != domainTag)
                        {
                            continue;
                        }
                        return L2_Data[int_CycleCount];
                    }
                }

                Current_DataObject_Position++;

                L2_Data[Current_DataObject_Position] =
                    MaterializeCacheDataLine(MemoryAddress, domainTag);

                return L2_Data[Current_DataObject_Position];
            }

            private Cache_Data_Object MaterializeCacheDataLine(
                ulong memoryAddress,
                ulong domainTag)
            {
                const int cacheLineBytes = 32;

                var cacheLine = new Cache_Data_Object
                {
                    DataCache_MemoryAddress = memoryAddress,
                    DataCache_DataLenght = cacheLineBytes,
                    DataCache_StoredValue = new byte[cacheLineBytes],
                    DomainTag = domainTag
                };

                // Cache and assist-prefetch surfaces may observe sparse main-memory holes
                // as zero-filled cache lines. Exact execution surfaces still go through the
                // fail-closed bound-memory helpers instead of this cache materializer.
                if (!HasExactBoundMainMemoryRange(memoryAddress, cacheLineBytes))
                {
                    return cacheLine;
                }

                try
                {
                    GetBoundMainMemory().ReadFromPosition(
                        cacheLine.DataCache_StoredValue,
                        memoryAddress,
                        cacheLineBytes);
                }
                catch (IOException)
                {
                    Array.Clear(
                        cacheLine.DataCache_StoredValue,
                        0,
                        cacheLine.DataCache_StoredValue.Length);
                }

                return cacheLine;
            }

            public void LoadTo_Cache_L2(Cache_VLIWBundle_Object Cached_VLIWBundle)
            {
                _hasMaterializedVliwFetchState = true;
                ulong_MinL2Query = ulong.MaxValue;

                for (int int_CycleCount = 0; int_CycleCount != L2_VLIWBundles.Length; int_CycleCount++)
                {
                    if (L2_VLIWBundles[int_CycleCount].VLIWCache_L2QueryCounter < ulong_MinL2Query)
                    {
                        ulong_MinL2Query = L2_VLIWBundles[int_CycleCount].VLIWCache_L2QueryCounter;

                        int_CycleCount = 0;

                        Current_VLIWBundle_Position = (ulong)int_CycleCount;

                        continue;
                    }
                }

                if (L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_VLIWBundle == null || L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_VLIWBundle.Length != 256)
                {
                    L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_VLIWBundle = new byte[256];
                }

                Cached_VLIWBundle.VLIWCache_VLIWBundle.CopyTo(L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_VLIWBundle, 0);

                L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_MemoryAddress = Cached_VLIWBundle.VLIWCache_MemoryAddress;
                L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_BundleAnnotations = Cached_VLIWBundle.VLIWCache_BundleAnnotations;
                L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_HasAnnotationCarrier = Cached_VLIWBundle.VLIWCache_HasAnnotationCarrier;

            }
            public Cache_VLIWBundle_Object GetL2_VLIWBundleByPointer(ulong MemoryAddress)
            {
                _hasMaterializedVliwFetchState = true;
                for (int int_CycleCount = 0; int_CycleCount != L2_VLIWBundles.Length; int_CycleCount++)
                {
                    if (L2_VLIWBundles[int_CycleCount].VLIWCache_MemoryAddress == MemoryAddress && MemoryAddress > 0)
                    {
                        L2_VLIWBundles[int_CycleCount].VLIWCache_L2QueryCounter++;

                        return L2_VLIWBundles[int_CycleCount];
                    }
                }

                Current_VLIWBundle_Position++;

                ulong ulong_MinL2Query = ulong.MaxValue;

                for (int int_CycleCount = 0; int_CycleCount != L2_VLIWBundles.Length; int_CycleCount++)
                {
                    if (L2_VLIWBundles[int_CycleCount].VLIWCache_L2QueryCounter < ulong_MinL2Query)
                    {
                        ulong_MinL2Query = L2_VLIWBundles[int_CycleCount].VLIWCache_L2QueryCounter;

                        int_CycleCount = 0;

                        Current_VLIWBundle_Position = (ulong)int_CycleCount;

                        continue;
                    }
                }

                L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_MemoryAddress = MemoryAddress;
                L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_VLIWBundle = new byte[256];

                MainMemory.ReadFromPosition(L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_VLIWBundle, L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_MemoryAddress, 256);
                if (MainMemory.TryReadVliwBundleAnnotations(MemoryAddress, out VliwBundleAnnotations? bundleAnnotations))
                {
                    L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_BundleAnnotations = bundleAnnotations;
                    L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_HasAnnotationCarrier = true;
                }
                else
                {
                    L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_BundleAnnotations = null;
                    L2_VLIWBundles[Current_VLIWBundle_Position].VLIWCache_HasAnnotationCarrier = false;
                }

                return L2_VLIWBundles[Current_VLIWBundle_Position];
            }

            /// <summary>
            /// Invalidate all L1 and L2 data cache lines belonging to a specific domain.
            /// Called on domain revocation, context switch, or SIP termination.
            /// HLS: single-cycle parallel AND-mask scan (small array).
            /// Phase §2: Side-channel hardening — prevents stale cross-domain residues.
            /// </summary>
            /// <param name="domainTag">Domain tag to flush (0 = flush kernel lines)</param>
            public void FlushDomainFromDataCache(ulong domainTag)
            {
                if (L1_Data != null)
                {
                    for (int i = 0; i < L1_Data.Length; i++)
                    {
                        if (L1_Data[i].DomainTag == domainTag)
                        {
                            L1_Data[i].DataCache_MemoryAddress = 0;
                            L1_Data[i].DomainTag = 0;
                            L1_Data[i].AssistResident = false;
                        }
                    }
                }

                for (int i = 0; i < L2_Data.Length; i++)
                {
                    if (L2_Data[i].DomainTag == domainTag)
                    {
                        L2_Data[i].DataCache_MemoryAddress = 0;
                        L2_Data[i].DomainTag = 0;
                        L2_Data[i].AssistResident = false;
                    }
                }
            }
        }
    }
}
