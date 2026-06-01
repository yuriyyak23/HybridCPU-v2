using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    internal readonly struct IssuePacketLane
    {
        public IssuePacketLane(
            byte physicalLaneIndex,
            bool isOccupied,
            byte slotIndex,
            int virtualThreadId,
            int ownerThreadId,
            uint opCode,
            MicroOp microOp,
            SlotClass requiredSlotClass,
            SlotPinningKind pinningKind,
            bool countsTowardScalarProjection)
        {
            PhysicalLaneIndex = physicalLaneIndex;
            IsOccupied = isOccupied;
            SlotIndex = slotIndex;
            VirtualThreadId = virtualThreadId;
            OwnerThreadId = ownerThreadId;
            OpCode = opCode;
            MicroOp = microOp;
            RequiredSlotClass = requiredSlotClass;
            PinningKind = pinningKind;
            CountsTowardScalarProjection = countsTowardScalarProjection;
        }

        public byte PhysicalLaneIndex { get; }
        public byte LaneIndex => PhysicalLaneIndex;
        public bool IsOccupied { get; }
        public byte SlotIndex { get; }
        public int VirtualThreadId { get; }
        public int OwnerThreadId { get; }
        public uint OpCode { get; }
        public MicroOp MicroOp { get; }
        public SlotClass RequiredSlotClass { get; }
        public SlotPinningKind PinningKind { get; }
        public bool CountsTowardScalarProjection { get; }
        public bool IsNonScalarSelection => IsOccupied && !CountsTowardScalarProjection;

        public static IssuePacketLane Create(byte physicalLaneIndex, ScalarClusterIssueEntry entry)
        {
            MicroOp microOp = entry.MicroOp
                ?? throw new InvalidOperationException("Prepared scalar-cluster entry must carry a live MicroOp.");
            SlotPlacementMetadata placement = microOp.AdmissionMetadata.Placement;

            return new IssuePacketLane(
                physicalLaneIndex,
                isOccupied: true,
                entry.SlotIndex,
                entry.VirtualThreadId,
                entry.OwnerThreadId,
                entry.OpCode,
                microOp,
                placement.RequiredSlotClass,
                placement.PinningKind,
                countsTowardScalarProjection: true);
        }

        public static IssuePacketLane Create(
            byte physicalLaneIndex,
            DecodedBundleSlotDescriptor slot,
            bool countsTowardScalarProjection)
        {
            SlotPlacementMetadata placement = slot.GetRuntimeAdmissionPlacement();

            return new IssuePacketLane(
                physicalLaneIndex,
                isOccupied: true,
                slot.SlotIndex,
                slot.GetRuntimeExecutionVirtualThreadId(),
                slot.GetRuntimeExecutionOwnerThreadId(),
                slot.GetRuntimeExecutionOpCode(),
                slot.MicroOp,
                placement.RequiredSlotClass,
                placement.PinningKind,
                countsTowardScalarProjection);
        }

        public static IssuePacketLane CreateEmpty(byte physicalLaneIndex)
        {
            return new IssuePacketLane(
                physicalLaneIndex,
                isOccupied: false,
                slotIndex: 0,
                virtualThreadId: 0,
                ownerThreadId: 0,
                opCode: 0,
                microOp: null,
                requiredSlotClass: SlotClass.Unclassified,
                pinningKind: SlotPinningKind.ClassFlexible,
                countsTowardScalarProjection: false);
        }
    }
}
