using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryPrefetchAssistDataLine(
                ulong memoryAddress,
                ulong domainTag,
                Core.AssistCarrierKind carrierKind,
                out Core.AssistCacheRejectKind rejectKind)
            {
                EnsureAssistDataCacheAllocated();

                if (TryFindL1DataLineIndex(memoryAddress, domainTag, out _))
                {
                    rejectKind = Core.AssistCacheRejectKind.None;
                    return true;
                }

                Core.AssistCachePartitionPolicy policy = Core.AssistCachePartitionPolicy.Default;
                int totalAssistResidentLines = CountAssistResidentLines();
                int carrierAssistResidentLines = CountAssistResidentLines(carrierKind);
                int slotIndex = FindEmptyL1DataSlot();

                if (carrierAssistResidentLines >= policy.ResolveCarrierLineBudget(carrierKind))
                {
                    slotIndex = FindAssistVictimSlot(carrierKind, allowCrossCarrierVictim: false);
                    if (slotIndex < 0)
                    {
                        rejectKind = Core.AssistCacheRejectKind.CarrierLineBudget;
                        return false;
                    }
                }
                else if (totalAssistResidentLines >= policy.TotalLineBudget)
                {
                    slotIndex = FindAssistVictimSlot(carrierKind, allowCrossCarrierVictim: true);
                    if (slotIndex < 0)
                    {
                        rejectKind = Core.AssistCacheRejectKind.TotalLineBudget;
                        return false;
                    }
                }
                else if (slotIndex < 0)
                {
                    slotIndex = FindAssistVictimSlot(carrierKind, allowCrossCarrierVictim: true);
                    if (slotIndex < 0)
                    {
                        rejectKind = Core.AssistCacheRejectKind.NoAssistVictim;
                        return false;
                    }
                }

                Cache_Data_Object prefetchedLine = GetDataByPointer(memoryAddress, domainTag);
                prefetchedLine.AssistResident = true;
                prefetchedLine.AssistCarrierKind = carrierKind;
                L1_Data[slotIndex] = prefetchedLine;
                rejectKind = Core.AssistCacheRejectKind.None;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void EnsureAssistDataCacheAllocated()
            {
                if (L1_Data == null)
                {
                    L1_Data = new Cache_Data_Object[2048];
                }

                if (L1_VLIWBundles == null)
                {
                    L1_VLIWBundles = new Cache_VLIWBundle_Object[256];
                }
            }

            private bool TryFindL1DataLineIndex(ulong memoryAddress, ulong domainTag, out int slotIndex)
            {
                EnsureAssistDataCacheAllocated();
                for (int index = 0; index < L1_Data.Length; index++)
                {
                    if (L1_Data[index].DataCache_MemoryAddress != memoryAddress)
                    {
                        continue;
                    }

                    if (domainTag != 0 &&
                        L1_Data[index].DomainTag != 0 &&
                        L1_Data[index].DomainTag != domainTag)
                    {
                        continue;
                    }

                    slotIndex = index;
                    return true;
                }

                slotIndex = -1;
                return false;
            }

            private int FindEmptyL1DataSlot()
            {
                EnsureAssistDataCacheAllocated();
                for (int index = 0; index < L1_Data.Length; index++)
                {
                    if (L1_Data[index].DataCache_MemoryAddress == 0 &&
                        L1_Data[index].DataCache_StoredValue == null)
                    {
                        return index;
                    }
                }

                return -1;
            }

            private int CountAssistResidentLines()
            {
                EnsureAssistDataCacheAllocated();
                int count = 0;
                for (int index = 0; index < L1_Data.Length; index++)
                {
                    if (L1_Data[index].AssistResident &&
                        L1_Data[index].DataCache_MemoryAddress != 0)
                    {
                        count++;
                    }
                }

                return count;
            }

            private int CountAssistResidentLines(Core.AssistCarrierKind carrierKind)
            {
                EnsureAssistDataCacheAllocated();
                int count = 0;
                for (int index = 0; index < L1_Data.Length; index++)
                {
                    if (L1_Data[index].AssistResident &&
                        L1_Data[index].AssistCarrierKind == carrierKind &&
                        L1_Data[index].DataCache_MemoryAddress != 0)
                    {
                        count++;
                    }
                }

                return count;
            }

            private int FindAssistVictimSlot(
                Core.AssistCarrierKind preferredCarrierKind,
                bool allowCrossCarrierVictim)
            {
                int preferredVictim = FindAssistVictimSlotCore(preferredCarrierKind);
                if (preferredVictim >= 0 || !allowCrossCarrierVictim)
                {
                    return preferredVictim;
                }

                return FindAssistVictimSlotCore(carrierKind: null);
            }

            private int FindAssistVictimSlotCore(Core.AssistCarrierKind? carrierKind)
            {
                EnsureAssistDataCacheAllocated();

                int selectedIndex = -1;
                ulong selectedQueryCount = ulong.MaxValue;

                for (int index = 0; index < L1_Data.Length; index++)
                {
                    Cache_Data_Object line = L1_Data[index];
                    if (!line.AssistResident || line.DataCache_MemoryAddress == 0)
                    {
                        continue;
                    }

                    if (carrierKind.HasValue && line.AssistCarrierKind != carrierKind.Value)
                    {
                        continue;
                    }

                    if (selectedIndex < 0 ||
                        line.DataCache_L1QueryCounter < selectedQueryCount)
                    {
                        selectedIndex = index;
                        selectedQueryCount = line.DataCache_L1QueryCounter;
                    }
                }

                return selectedIndex;
            }
        }
    }
}
