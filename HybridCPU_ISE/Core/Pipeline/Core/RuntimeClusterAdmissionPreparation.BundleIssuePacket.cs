using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    internal readonly struct BundleIssuePacket
    {
        public BundleIssuePacket(
            ulong pc,
            DecodeMode decodeMode,
            byte validNonEmptyMask,
            byte scalarCandidateMask,
            byte scalarIssueMask,
            byte selectedSlotMask,
            byte unmappedSelectedSlotMask,
            byte preparedScalarMask,
            byte refinedPreparedScalarMask,
            int advisoryScalarIssueWidth,
            int refinedAdvisoryScalarIssueWidth,
            RuntimeClusterAdmissionExecutionMode executionMode,
            bool shouldProbeClusterPath,
            bool usesIssuePacketAsExecutionSource,
            bool retainsReferenceSequentialPath,
            IssuePacketLane lane0,
            IssuePacketLane lane1,
            IssuePacketLane lane2,
            IssuePacketLane lane3,
            IssuePacketLane lane4,
            IssuePacketLane lane5,
            IssuePacketLane lane6,
            IssuePacketLane lane7,
            BundleIssueFallbackInfo fallbackInfo)
        {
            PC = pc;
            DecodeMode = decodeMode;
            ValidNonEmptyMask = validNonEmptyMask;
            ScalarCandidateMask = scalarCandidateMask;
            ScalarIssueMask = scalarIssueMask;
            SelectedSlotMask = selectedSlotMask;
            UnmappedSelectedSlotMask = unmappedSelectedSlotMask;
            PreparedScalarMask = preparedScalarMask;
            RefinedPreparedScalarMask = refinedPreparedScalarMask;
            AdvisoryScalarIssueWidth = advisoryScalarIssueWidth;
            RefinedAdvisoryScalarIssueWidth = refinedAdvisoryScalarIssueWidth;
            ExecutionMode = executionMode;
            ShouldProbeClusterPath = shouldProbeClusterPath;
            UsesIssuePacketAsExecutionSource = usesIssuePacketAsExecutionSource;
            RetainsReferenceSequentialPath = retainsReferenceSequentialPath;
            Lane0 = lane0;
            Lane1 = lane1;
            Lane2 = lane2;
            Lane3 = lane3;
            Lane4 = lane4;
            Lane5 = lane5;
            Lane6 = lane6;
            Lane7 = lane7;
            FallbackInfo = fallbackInfo;
        }

        public ulong PC { get; }
        public DecodeMode DecodeMode { get; }
        public byte ValidNonEmptyMask { get; }
        public byte ScalarCandidateMask { get; }
        public byte ScalarIssueMask { get; }
        public byte SelectedSlotMask { get; }
        public byte UnmappedSelectedSlotMask { get; }
        public byte PreparedScalarMask { get; }
        public byte RefinedPreparedScalarMask { get; }
        public int AdvisoryScalarIssueWidth { get; }
        public int RefinedAdvisoryScalarIssueWidth { get; }
        public RuntimeClusterAdmissionExecutionMode ExecutionMode { get; }

        /// <summary>
        /// Diagnostic flag indicating whether this bundle stays interesting for cluster-path probing.
        /// This does not, by itself, select the packet as the live execution source.
        /// </summary>
        public bool ShouldProbeClusterPath { get; }

        /// <summary>
        /// True when execute-stage materialization should consume the packet lanes as the live source.
        /// False means the packet remains metadata while execution stays on the retained reference path.
        /// </summary>
        public bool UsesIssuePacketAsExecutionSource { get; }

        /// <summary>
        /// True when the decode/execute handoff still carries the reference sequential path alongside
        /// any packetized admission metadata. This may be false in cluster-prepared execution modes.
        /// </summary>
        public bool RetainsReferenceSequentialPath { get; }
        public IssuePacketLane Lane0 { get; }
        public IssuePacketLane Lane1 { get; }
        public IssuePacketLane Lane2 { get; }
        public IssuePacketLane Lane3 { get; }
        public IssuePacketLane Lane4 { get; }
        public IssuePacketLane Lane5 { get; }
        public IssuePacketLane Lane6 { get; }
        public IssuePacketLane Lane7 { get; }
        public BundleIssueFallbackInfo FallbackInfo { get; }
        public byte SelectedNonScalarSlotMask => (byte)(SelectedSlotMask & ~ScalarIssueMask);
        public bool HasUnmappedSelectedSlots => UnmappedSelectedSlotMask != 0;
        public int PreparedScalarLaneCount => GetPreparedScalarLaneCount();
        public int PreparedPhysicalLaneCount => GetPreparedPhysicalLaneCount();

        public IssuePacketLane GetPhysicalLane(byte laneIndex) => laneIndex switch
        {
            0 => Lane0,
            1 => Lane1,
            2 => Lane2,
            3 => Lane3,
            4 => Lane4,
            5 => Lane5,
            6 => Lane6,
            7 => Lane7,
            _ => IssuePacketLane.CreateEmpty(laneIndex)
        };

        public static BundleIssuePacket Create(
            ClusterIssuePreparation clusterPreparation,
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            RuntimeClusterAdmissionCandidateView candidateView,
            RuntimeClusterAdmissionDecisionDraft decisionDraft)
        {
            ArgumentNullException.ThrowIfNull(slots);

            IssuePacketLane lane0 = IssuePacketLane.CreateEmpty(0);
            IssuePacketLane lane1 = IssuePacketLane.CreateEmpty(1);
            IssuePacketLane lane2 = IssuePacketLane.CreateEmpty(2);
            IssuePacketLane lane3 = IssuePacketLane.CreateEmpty(3);
            IssuePacketLane lane4 = IssuePacketLane.CreateEmpty(4);
            IssuePacketLane lane5 = IssuePacketLane.CreateEmpty(5);
            IssuePacketLane lane6 = IssuePacketLane.CreateEmpty(6);
            IssuePacketLane lane7 = IssuePacketLane.CreateEmpty(7);

            ScalarClusterIssueEntry[] entries = clusterPreparation.ScalarClusterGroup.Entries;
            byte scalarIssueMask = decisionDraft.ScalarIssueMask;
            byte auxiliarySlotMask = decisionDraft.AuxiliaryReservationMask;
            byte selectedSlotMask = (byte)(scalarIssueMask | auxiliarySlotMask);
            byte occupiedPhysicalLaneMask = 0;
            byte unmappedSelectedSlotMask = 0;

            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                ScalarClusterIssueEntry entry = entries[entryIndex];
                byte slotBit = (byte)(1 << entry.SlotIndex);
                if ((scalarIssueMask & slotBit) == 0)
                    continue;

                if (!TryAssignToPacket(
                    entry,
                    ref occupiedPhysicalLaneMask,
                    ref lane0,
                    ref lane1,
                    ref lane2,
                    ref lane3,
                    ref lane4,
                    ref lane5,
                    ref lane6,
                    ref lane7))
                {
                    unmappedSelectedSlotMask |= slotBit;
                }
            }

            for (byte slotIndex = 0; slotIndex < 8; slotIndex++)
            {
                byte slotBit = (byte)(1 << slotIndex);
                if ((auxiliarySlotMask & slotBit) == 0)
                    continue;

                if (slotIndex >= slots.Count)
                {
                    unmappedSelectedSlotMask |= slotBit;
                    continue;
                }

                DecodedBundleSlotDescriptor slot = slots[slotIndex];
                if (!slot.IsValid)
                {
                    unmappedSelectedSlotMask |= slotBit;
                    continue;
                }

                if (!TryAssignToPacket(
                    slot,
                    countsTowardScalarProjection: false,
                    ref occupiedPhysicalLaneMask,
                    ref lane0,
                    ref lane1,
                    ref lane2,
                    ref lane3,
                    ref lane4,
                    ref lane5,
                    ref lane6,
                    ref lane7))
                {
                    unmappedSelectedSlotMask |= slotBit;
                }
            }

            return new BundleIssuePacket(
                candidateView.PC,
                candidateView.DecodeMode,
                candidateView.ValidNonEmptyMask,
                candidateView.ScalarCandidateMask,
                scalarIssueMask,
                selectedSlotMask,
                unmappedSelectedSlotMask,
                candidateView.PreparedScalarMask,
                candidateView.RefinedPreparedScalarMask,
                candidateView.AdvisoryScalarIssueWidth,
                candidateView.RefinedAdvisoryScalarIssueWidth,
                decisionDraft.ExecutionMode,
                candidateView.ShouldConsiderClusterAdmission && decisionDraft.ShouldProbeClusterPath,
                decisionDraft.UsesIssuePacketAsExecutionSource,
                decisionDraft.RetainsReferenceSequentialPath,
                lane0,
                lane1,
                lane2,
                lane3,
                lane4,
                lane5,
                lane6,
                lane7,
                BundleIssueFallbackInfo.Create(candidateView, decisionDraft));
        }

        public static BundleIssuePacket CreateEmpty(ulong pc = 0)
        {
            return new BundleIssuePacket(
                pc,
                DecodeMode.ReferenceSequentialMode,
                validNonEmptyMask: 0,
                scalarCandidateMask: 0,
                scalarIssueMask: 0,
                selectedSlotMask: 0,
                unmappedSelectedSlotMask: 0,
                preparedScalarMask: 0,
                refinedPreparedScalarMask: 0,
                advisoryScalarIssueWidth: 0,
                refinedAdvisoryScalarIssueWidth: 0,
                executionMode: RuntimeClusterAdmissionExecutionMode.Empty,
                shouldProbeClusterPath: false,
                usesIssuePacketAsExecutionSource: false,
                retainsReferenceSequentialPath: true,
                IssuePacketLane.CreateEmpty(0),
                IssuePacketLane.CreateEmpty(1),
                IssuePacketLane.CreateEmpty(2),
                IssuePacketLane.CreateEmpty(3),
                IssuePacketLane.CreateEmpty(4),
                IssuePacketLane.CreateEmpty(5),
                IssuePacketLane.CreateEmpty(6),
                IssuePacketLane.CreateEmpty(7),
                BundleIssueFallbackInfo.CreateEmpty());
        }

        private static bool TryAssignToPacket(
            ScalarClusterIssueEntry entry,
            ref byte occupiedPhysicalLaneMask,
            ref IssuePacketLane lane0,
            ref IssuePacketLane lane1,
            ref IssuePacketLane lane2,
            ref IssuePacketLane lane3,
            ref IssuePacketLane lane4,
            ref IssuePacketLane lane5,
            ref IssuePacketLane lane6,
            ref IssuePacketLane lane7)
        {
            if (!TryResolvePacketLane(entry, occupiedPhysicalLaneMask, out byte physicalLaneIndex))
                return false;

            IssuePacketLane lane = IssuePacketLane.Create(physicalLaneIndex, entry);
            SetLane(
                physicalLaneIndex,
                lane,
                ref lane0,
                ref lane1,
                ref lane2,
                ref lane3,
                ref lane4,
                ref lane5,
                ref lane6,
                ref lane7);
            occupiedPhysicalLaneMask |= (byte)(1 << physicalLaneIndex);
            return true;
        }

        private static bool TryAssignToPacket(
            DecodedBundleSlotDescriptor slot,
            bool countsTowardScalarProjection,
            ref byte occupiedPhysicalLaneMask,
            ref IssuePacketLane lane0,
            ref IssuePacketLane lane1,
            ref IssuePacketLane lane2,
            ref IssuePacketLane lane3,
            ref IssuePacketLane lane4,
            ref IssuePacketLane lane5,
            ref IssuePacketLane lane6,
            ref IssuePacketLane lane7)
        {
            if (!TryResolvePacketLane(slot, occupiedPhysicalLaneMask, out byte physicalLaneIndex))
                return false;

            IssuePacketLane lane = IssuePacketLane.Create(physicalLaneIndex, slot, countsTowardScalarProjection);
            SetLane(
                physicalLaneIndex,
                lane,
                ref lane0,
                ref lane1,
                ref lane2,
                ref lane3,
                ref lane4,
                ref lane5,
                ref lane6,
                ref lane7);
            occupiedPhysicalLaneMask |= (byte)(1 << physicalLaneIndex);
            return true;
        }

        private static bool TryResolvePacketLane(
            ScalarClusterIssueEntry entry,
            byte occupiedPhysicalLaneMask,
            out byte physicalLaneIndex)
        {
            physicalLaneIndex = byte.MaxValue;
            MicroOp microOp = entry.MicroOp
                ?? throw new InvalidOperationException("Prepared scalar-cluster entry must carry a live MicroOp.");
            SlotPlacementMetadata placement = microOp.AdmissionMetadata.Placement;

            if (placement.PinningKind == SlotPinningKind.HardPinned)
            {
                byte pinnedLaneId = placement.PinnedLaneId;
                if (pinnedLaneId >= 8)
                    return false;

                byte pinnedLaneBit = (byte)(1 << pinnedLaneId);
                if ((occupiedPhysicalLaneMask & pinnedLaneBit) != 0)
                    return false;

                physicalLaneIndex = pinnedLaneId;
                return true;
            }

            if (placement.RequiredSlotClass == SlotClass.Unclassified)
                return false;

            byte eligibleLaneMask = SlotClassLaneMap.GetLaneMask(placement.RequiredSlotClass);
            if (eligibleLaneMask == 0)
                return false;

            for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
            {
                byte laneBit = (byte)(1 << laneIndex);
                if ((eligibleLaneMask & laneBit) == 0 || (occupiedPhysicalLaneMask & laneBit) != 0)
                    continue;

                physicalLaneIndex = laneIndex;
                return true;
            }

            return false;
        }

        private static bool TryResolvePacketLane(
            DecodedBundleSlotDescriptor slot,
            byte occupiedPhysicalLaneMask,
            out byte physicalLaneIndex)
        {
            physicalLaneIndex = byte.MaxValue;
            SlotPlacementMetadata placement = slot.GetRuntimeAdmissionPlacement();

            if (placement.PinningKind == SlotPinningKind.HardPinned)
            {
                byte pinnedLaneId = placement.PinnedLaneId;
                if (pinnedLaneId >= 8)
                    return false;

                byte pinnedLaneBit = (byte)(1 << pinnedLaneId);
                if ((occupiedPhysicalLaneMask & pinnedLaneBit) != 0)
                    return false;

                physicalLaneIndex = pinnedLaneId;
                return true;
            }

            if (placement.RequiredSlotClass == SlotClass.Unclassified)
                return false;

            byte eligibleLaneMask = SlotClassLaneMap.GetLaneMask(placement.RequiredSlotClass);
            if (eligibleLaneMask == 0)
                return false;

            for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
            {
                byte laneBit = (byte)(1 << laneIndex);
                if ((eligibleLaneMask & laneBit) == 0 || (occupiedPhysicalLaneMask & laneBit) != 0)
                    continue;

                physicalLaneIndex = laneIndex;
                return true;
            }

            return false;
        }

        private static void SetLane(
            byte laneIndex,
            IssuePacketLane lane,
            ref IssuePacketLane lane0,
            ref IssuePacketLane lane1,
            ref IssuePacketLane lane2,
            ref IssuePacketLane lane3,
            ref IssuePacketLane lane4,
            ref IssuePacketLane lane5,
            ref IssuePacketLane lane6,
            ref IssuePacketLane lane7)
        {
            switch (laneIndex)
            {
                case 0:
                    lane0 = lane;
                    break;
                case 1:
                    lane1 = lane;
                    break;
                case 2:
                    lane2 = lane;
                    break;
                case 3:
                    lane3 = lane;
                    break;
                case 4:
                    lane4 = lane;
                    break;
                case 5:
                    lane5 = lane;
                    break;
                case 6:
                    lane6 = lane;
                    break;
                case 7:
                    lane7 = lane;
                    break;
            }
        }

        private int GetPreparedScalarLaneCount()
        {
            int laneCount = 0;
            if (Lane0.IsOccupied && Lane0.CountsTowardScalarProjection) laneCount++;
            if (Lane1.IsOccupied && Lane1.CountsTowardScalarProjection) laneCount++;
            if (Lane2.IsOccupied && Lane2.CountsTowardScalarProjection) laneCount++;
            if (Lane3.IsOccupied && Lane3.CountsTowardScalarProjection) laneCount++;

            return laneCount;
        }

        private int GetPreparedPhysicalLaneCount()
        {
            int laneCount = 0;
            if (Lane0.IsOccupied) laneCount++;
            if (Lane1.IsOccupied) laneCount++;
            if (Lane2.IsOccupied) laneCount++;
            if (Lane3.IsOccupied) laneCount++;
            if (Lane4.IsOccupied) laneCount++;
            if (Lane5.IsOccupied) laneCount++;
            if (Lane6.IsOccupied) laneCount++;
            if (Lane7.IsOccupied) laneCount++;

            return laneCount;
        }
    }

}
