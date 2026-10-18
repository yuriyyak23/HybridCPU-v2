using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    internal readonly struct AuxiliaryIssueReservationSet
    {
        public AuxiliaryIssueReservationSet(
            byte vectorMask,
            byte memoryMask,
            byte dmaStreamMask,
            byte controlMask,
            byte systemMask,
            byte otherMask)
        {
            VectorMask = vectorMask;
            MemoryMask = memoryMask;
            DmaStreamMask = dmaStreamMask;
            ControlMask = controlMask;
            SystemMask = systemMask;
            OtherMask = otherMask;
        }

        public byte VectorMask { get; }
        public byte MemoryMask { get; }
        public byte DmaStreamMask { get; }
        public byte ControlMask { get; }
        public byte SystemMask { get; }
        public byte OtherMask { get; }
        public byte AggregateMask => (byte)(VectorMask | MemoryMask | DmaStreamMask | ControlMask | SystemMask | OtherMask);

        public static AuxiliaryIssueReservationSet Create(AuxiliaryClusterReservation[] auxiliaryReservations)
        {
            byte vectorMask = 0;
            byte memoryMask = 0;
            byte dmaStreamMask = 0;
            byte controlMask = 0;
            byte systemMask = 0;
            byte otherMask = 0;

            for (int i = 0; i < auxiliaryReservations.Length; i++)
            {
                AuxiliaryClusterReservation reservation = auxiliaryReservations[i];
                switch (reservation.Kind)
                {
                    case AuxiliaryClusterKind.Vector:
                        vectorMask |= reservation.SlotMask;
                        break;
                    case AuxiliaryClusterKind.Memory:
                        memoryMask |= reservation.SlotMask;
                        break;
                    case AuxiliaryClusterKind.DmaStream:
                        dmaStreamMask |= reservation.SlotMask;
                        break;
                    case AuxiliaryClusterKind.Control:
                        controlMask |= reservation.SlotMask;
                        break;
                    case AuxiliaryClusterKind.System:
                        systemMask |= reservation.SlotMask;
                        break;
                    default:
                        otherMask |= reservation.SlotMask;
                        break;
                }
            }

            return new AuxiliaryIssueReservationSet(
                vectorMask,
                memoryMask,
                dmaStreamMask,
                controlMask,
                systemMask,
                otherMask);
        }

        public static AuxiliaryIssueReservationSet CreateEmpty()
        {
            return new AuxiliaryIssueReservationSet(
                vectorMask: 0,
                memoryMask: 0,
                dmaStreamMask: 0,
                controlMask: 0,
                systemMask: 0,
                otherMask: 0);
        }
    }
}
