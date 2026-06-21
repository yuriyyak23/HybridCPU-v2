using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// FSP-specific packing helpers live in a dedicated partial so the main
            /// pipeline execution file only keeps the broader execution flow.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private System.Collections.Generic.IReadOnlyList<Core.MicroOp?> ApplyFSPPacking(
                ulong currentBundlePc,
                System.Collections.Generic.IReadOnlyList<Core.DecodedBundleSlotDescriptor> currentBundleSlots,
                Core.DecodedBundleAdmissionPrep currentAdmissionPrep,
                Core.DecodedBundleDependencySummary? currentDependencySummary,
                int currentThreadId,
                byte donorMask = 0)
            {
                if (VectorConfig.FSP_Enabled == 0)
                    return CloneDecodedBundleCarrierBundle(currentBundleSlots);

                if (CurrentCanonicalBundleHasFspBoundary(currentBundlePc) ||
                    ContainsForegroundStreamControlBoundary(currentBundleSlots))
                    return CloneDecodedBundleCarrierBundle(currentBundleSlots);

                var replayPhase = _loopBuffer.CurrentReplayPhase;
                PublishReplayPhaseContextIfNeeded(_fspScheduler, replayPhase);

                var pod = Processor.GetPodForCore((int)this.CoreID);
                if (pod == null)
                    return CloneDecodedBundleCarrierBundle(currentBundleSlots);

                Core.MicroOpScheduler podScheduler = pod.Scheduler;
                if (!object.ReferenceEquals(podScheduler, _fspScheduler))
                {
                    PublishReplayPhaseContextIfNeeded(podScheduler, replayPhase);
                }

                pod.Scheduler?.ClearSmtNominationPorts();

                Core.ResourceBitset hwResourceLocks = default;
                Core.Memory.HardwareOccupancySnapshot128 hwOccupancySnapshot =
                    Core.Memory.HardwareOccupancySnapshot128.Permissive;
                if (GetBoundMemorySubsystem() is Core.Memory.IHardwareOccupancyInput occupancyInput)
                {
                    hwOccupancySnapshot = occupancyInput.GetHardwareOccupancySnapshot128();
                    hwResourceLocks = new Core.ResourceBitset(
                        hwOccupancySnapshot.OverloadedResources.Low,
                        hwOccupancySnapshot.OverloadedResources.High);
                }

                pod.Scheduler?.SetHardwareOccupancySnapshot(hwOccupancySnapshot);

                int localCoreId = Processor.GetLocalCoreId((int)this.CoreID);
                PublishAssistOwnerSnapshot(
                    pod,
                    localCoreId,
                    currentThreadId,
                    currentBundleSlots);

                if (TryPackBundleWithIntraCoreSmt(
                    currentBundlePc,
                    currentBundleSlots,
                    currentAdmissionPrep,
                    currentDependencySummary,
                    currentThreadId,
                    pod,
                    out Core.MicroOp[] intraCorePackedBundle))
                {
                    return intraCorePackedBundle;
                }

                Core.MicroOp?[] originalBundle =
                    CloneDecodedBundleCarrierBundle(currentBundleSlots);
                ResolveBundleOwnerScope(
                    originalBundle,
                    out int bundleOwnerContextId,
                    out ulong bundleOwnerDomainTag);
                TryNominateInterCoreAssistCandidate(
                    pod,
                    localCoreId,
                    currentThreadId,
                    originalBundle);

                Core.MicroOp[] packedBundle = pod.PackBundle(
                    originalBundle: originalBundle,
                    currentThreadId: currentThreadId,
                    stealEnabled: true,
                    stealMask: VectorConfig.FSP_StealMask,
                    globalResourceLocks: hwResourceLocks,
                    donorMask: donorMask,
                    localCoreId: localCoreId,
                    bundleOwnerContextId: bundleOwnerContextId,
                    bundleOwnerDomainTag: bundleOwnerDomainTag,
                    assistRuntimeEpoch: _assistRuntimeEpoch,
                    memSub: GetBoundMemorySubsystem()
                );

                return packedBundle;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ContainsForegroundStreamControlBoundary(
                System.Collections.Generic.IReadOnlyList<Core.DecodedBundleSlotDescriptor> currentBundleSlots)
            {
                if (currentBundleSlots == null)
                    return false;

                for (int slotIndex = 0; slotIndex < currentBundleSlots.Count; slotIndex++)
                {
                    Core.DecodedBundleSlotDescriptor slotDescriptor = currentBundleSlots[slotIndex];
                    if (!slotDescriptor.IsValid || slotDescriptor.GetRuntimeExecutionIsEmptyOrNop())
                        continue;

                    if (slotDescriptor.MicroOp is Core.StreamControlMicroOp)
                        return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool CurrentCanonicalBundleHasFspBoundary(ulong currentBundlePc)
            {
                Core.DecodedBundleRuntimeState runtimeState = decodedBundleRuntimeState;
                return runtimeState.HasCanonicalDecode &&
                    runtimeState.BundlePc == currentBundlePc &&
                    runtimeState.CanonicalDecode.BundleMetadata.FspBoundary;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryPackBundleWithIntraCoreSmt(
                ulong currentBundlePc,
                System.Collections.Generic.IReadOnlyList<Core.DecodedBundleSlotDescriptor> currentBundleSlots,
                Core.DecodedBundleAdmissionPrep currentAdmissionPrep,
                Core.DecodedBundleDependencySummary? currentDependencySummary,
                int ownerVirtualThreadId,
                PodController pod,
                out Core.MicroOp[] packedBundle)
            {
                packedBundle = Array.Empty<Core.MicroOp>();
                if (currentBundleSlots == null || currentBundleSlots.Count == 0)
                    return false;

                if (!CanVirtualThreadIssueInForeground(ownerVirtualThreadId))
                    return false;

                Core.ClusterIssuePreparation clusterPreparation =
                    Core.ClusterIssuePreparation.Create(
                        currentBundlePc,
                        currentBundleSlots,
                        currentAdmissionPrep,
                        currentDependencySummary);
                if (clusterPreparation.DecodeMode != Core.DecodeMode.ClusterPreparedMode ||
                    clusterPreparation.ScalarClusterGroup.Count == 0)
                {
                    return false;
                }

                int injectableGapCount = CountInjectableGaps(currentBundleSlots);
                if (injectableGapCount == 0)
                    return false;

                if (clusterPreparation.ScalarClusterGroup.BuildVtSummary().ActiveVtCount <= 1)
                    return false;

                Core.MicroOpScheduler scheduler = pod.Scheduler;
                if (scheduler == null)
                    return false;

                scheduler.ClearSmtNominationPorts();
                scheduler.ClearAssistNominationPorts();

                var nominatedCandidates = new Core.MicroOp[4];
                var nominatedSlots = new int[] { -1, -1, -1, -1 };
                int assistNominationCount = 0;
                int nominatedCount = 0;

                Core.ScalarClusterIssueEntry[] entries = clusterPreparation.ScalarClusterGroup.Entries;
                for (int entryIndex = 0; entryIndex < entries.Length && nominatedCount < injectableGapCount; entryIndex++)
                {
                    Core.ScalarClusterIssueEntry entry = entries[entryIndex];
                    int vtId = entry.VirtualThreadId;
                    if ((uint)vtId >= 4 || vtId == ownerVirtualThreadId)
                        continue;

                    if (!CanVirtualThreadIssueInForeground(vtId))
                        continue;

                    if (nominatedCandidates[vtId] != null)
                        continue;

                    Core.MicroOp candidate = entry.MicroOp;
                    if (candidate == null)
                        continue;

                    if (!CanNominateIntraCoreSmtCandidate(candidate))
                        continue;

                    scheduler.NominateSmtCandidate(vtId, candidate);
                    nominatedCandidates[vtId] = candidate;
                    nominatedSlots[vtId] = entry.SlotIndex;
                    nominatedCount++;

                    if (TryCreateAssistCandidate(candidate, ownerVirtualThreadId, out Core.AssistMicroOp assistCandidate))
                    {
                        scheduler.NominateAssistCandidate(vtId, assistCandidate);
                        assistNominationCount++;
                    }
                }

                if (nominatedCount == 0 && assistNominationCount == 0)
                {
                    scheduler.ClearSmtNominationPorts();
                    scheduler.ClearAssistNominationPorts();
                    return false;
                }

                Core.MicroOp?[] ownerBundle =
                    CloneDecodedBundleCarrierBundle(currentBundleSlots);
                ClearForegroundSkippedOwnerSlots(
                    currentBundleSlots,
                    ownerBundle);
                ClearNominatedOwnerSlots(ownerBundle, nominatedSlots);
                packedBundle = pod.PackBundleIntraCoreSmt(
                    ownerBundle,
                    ownerVirtualThreadId,
                    (int)this.CoreID,
                    ResolveForegroundRunnableVirtualThreadMask(),
                    GetBoundMemorySubsystem());
                RestoreUnmaterializedSmtCandidates(packedBundle, nominatedCandidates, nominatedSlots);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private byte ResolveForegroundRunnableVirtualThreadMask()
            {
                byte runnableMask = 0;
                for (int vt = 0; vt < SmtWays; vt++)
                {
                    if (CanVirtualThreadIssueInForeground(vt))
                    {
                        runnableMask |= (byte)(1 << vt);
                    }
                }

                return runnableMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CountInjectableGaps(
                System.Collections.Generic.IReadOnlyList<Core.DecodedBundleSlotDescriptor> slotDescriptors)
            {
                if (slotDescriptors == null)
                    return 0;

                int gapCount = 0;
                for (int slot = 0; slot < slotDescriptors.Count; slot++)
                {
                    Core.DecodedBundleSlotDescriptor slotDescriptor = slotDescriptors[slot];
                    if (!slotDescriptor.IsValid || slotDescriptor.GetRuntimeExecutionIsEmptyOrNop())
                        gapCount++;
                }

                return gapCount;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ShouldSkipDecodedSlotForForegroundIssue(
                in Core.DecodedBundleSlotDescriptor slotDescriptor)
            {
                if (!slotDescriptor.IsValid)
                    return true;

                if (slotDescriptor.GetRuntimeExecutionIsEmptyOrNop() &&
                    !slotDescriptor.GetRuntimeAdmissionIsControlFlow())
                    return true;

                return !CanVirtualThreadIssueInForeground(slotDescriptor.GetRuntimeExecutionVirtualThreadId());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int ResolveDecodedBundleOwnerVirtualThreadId(
                System.Collections.Generic.IReadOnlyList<Core.DecodedBundleSlotDescriptor> currentBundleSlots)
            {
                if (currentBundleSlots != null)
                {
                    int fallbackOwnerVirtualThreadId = -1;
                    for (int slotIndex = 0; slotIndex < currentBundleSlots.Count; slotIndex++)
                    {
                        Core.DecodedBundleSlotDescriptor slotDescriptor = currentBundleSlots[slotIndex];
                        if (!slotDescriptor.IsValid ||
                            slotDescriptor.GetRuntimeExecutionIsEmptyOrNop())
                        {
                            continue;
                        }

                        int ownerVirtualThreadId =
                            slotDescriptor.GetRuntimeExecutionOwnerThreadId();
                        if (ownerVirtualThreadId < 0 || ownerVirtualThreadId >= SmtWays)
                        {
                            ownerVirtualThreadId =
                                slotDescriptor.GetRuntimeExecutionVirtualThreadId();
                        }

                        ownerVirtualThreadId = NormalizePipelineStateVtId(ownerVirtualThreadId);
                        if (!slotDescriptor.GetRuntimeExecutionIsFspInjected())
                        {
                            return ownerVirtualThreadId;
                        }

                        if (fallbackOwnerVirtualThreadId < 0)
                        {
                            fallbackOwnerVirtualThreadId = ownerVirtualThreadId;
                        }
                    }

                    if (fallbackOwnerVirtualThreadId >= 0)
                    {
                        return fallbackOwnerVirtualThreadId;
                    }
                }

                return ReadActiveVirtualThreadId();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool CanNominateIntraCoreSmtCandidate(Core.MicroOp candidate)
            {
                return candidate != null
                    && candidate is not Core.NopMicroOp
                    && !candidate.IsControlFlow
                    && candidate.AdmissionMetadata.IsStealable;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ClearForegroundSkippedOwnerSlots(
                System.Collections.Generic.IReadOnlyList<Core.DecodedBundleSlotDescriptor> currentBundleSlots,
                Core.MicroOp?[] ownerBundle)
            {
                if (currentBundleSlots == null || ownerBundle == null)
                    return;

                int limit = ownerBundle.Length < currentBundleSlots.Count
                    ? ownerBundle.Length
                    : currentBundleSlots.Count;
                for (int slotIndex = 0; slotIndex < limit; slotIndex++)
                {
                    if (ShouldSkipDecodedSlotForForegroundIssue(currentBundleSlots[slotIndex]))
                    {
                        ownerBundle[slotIndex] = null;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void ClearNominatedOwnerSlots(
                Core.MicroOp?[] ownerBundle,
                int[] nominatedSlots)
            {
                if (ownerBundle == null || nominatedSlots == null)
                    return;

                for (int vtId = 0; vtId < nominatedSlots.Length; vtId++)
                {
                    int slotIndex = nominatedSlots[vtId];
                    if ((uint)slotIndex >= ownerBundle.Length)
                        continue;

                    ownerBundle[slotIndex] = null;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void RestoreUnmaterializedSmtCandidates(
                Core.MicroOp[] packedBundle,
                Core.MicroOp[] nominatedCandidates,
                int[] nominatedSlots)
            {
                if (packedBundle == null)
                    return;

                for (int vtId = 0; vtId < nominatedCandidates.Length; vtId++)
                {
                    Core.MicroOp candidate = nominatedCandidates[vtId];
                    int slotIndex = nominatedSlots[vtId];
                    if (candidate == null || slotIndex < 0 || BundleContainsReference(packedBundle, candidate))
                        continue;

                    if (packedBundle[slotIndex] == null || packedBundle[slotIndex] is Core.NopMicroOp)
                    {
                        packedBundle[slotIndex] = candidate;
                        continue;
                    }

                    int fallbackSlot = FindInjectableGapSlot(packedBundle);
                    if (fallbackSlot >= 0)
                    {
                        packedBundle[fallbackSlot] = candidate;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int FindInjectableGapSlot(Core.MicroOp[] bundle)
            {
                if (bundle == null)
                    return -1;

                for (int slot = 0; slot < bundle.Length; slot++)
                {
                    if (bundle[slot] == null || bundle[slot] is Core.NopMicroOp)
                        return slot;
                }

                return -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool BundleContainsReference(Core.MicroOp[] bundle, Core.MicroOp candidate)
            {
                for (int slot = 0; slot < bundle.Length; slot++)
                {
                    if (ReferenceEquals(bundle[slot], candidate))
                        return true;
                }

                return false;
            }

            /// <summary>
            /// Refresh the current FSP-derived issue plan from the canonical/residual base bundle
            /// without republishing the packed execution layout as the canonical decode transport.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RefreshCurrentFspDerivedIssuePlan()
            {
                ReadCurrentDecodedBundleTransportFacts(
                    out Core.DecodedBundleTransportFacts currentTransportFacts);
                int currentThreadId =
                    ResolveDecodedBundleOwnerVirtualThreadId(
                        currentTransportFacts.Slots);

                System.Collections.Generic.IReadOnlyList<Core.MicroOp?> packedBundle = ApplyFSPPacking(
                    currentTransportFacts.PC,
                    currentTransportFacts.Slots,
                    currentTransportFacts.AdmissionPrep,
                    currentTransportFacts.DependencySummary,
                    currentThreadId);

                if (packedBundle != null && packedBundle.Count == 8)
                {
                    PublishCurrentFspDerivedIssuePlan(
                        currentTransportFacts.PC,
                        packedBundle);
                }
            }
        }
    }
}
