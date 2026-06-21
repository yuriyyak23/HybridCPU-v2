using System;
using System.Collections.Generic;
using System.Numerics;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Quality metrics for one materialized Stage 6 slot placement.
    /// </summary>
    public sealed record IrBundlePlacementQuality(
        int OccupiedSlotCount,
        int OccupiedSlotSpan,
        int InternalGapCount,
        int LargestInternalGap,
        int OrderInversionCount,
        int LeadingEmptySlotCount,
        int TrailingEmptySlotCount,
        int SlotIndexSum,
        int ConstrainedSlotDisplacementCost)
    {
        /// <summary>
        /// Gets the quality metrics for an empty assignment.
        /// </summary>
        public static IrBundlePlacementQuality Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Computes quality metrics for one assigned physical slot set.
        /// </summary>
        public static IrBundlePlacementQuality Create(IReadOnlyList<int> assignedSlots, int slotCount)
        {
            ArgumentNullException.ThrowIfNull(assignedSlots);

            var legalSlots = new IrIssueSlotMask[assignedSlots.Count];
            Array.Fill(legalSlots, IrIssueSlotMask.All);
            return Create(assignedSlots, legalSlots, slotCount);
        }

        /// <summary>
        /// Computes quality metrics for one assigned physical slot set against the instructions' legal-slot masks.
        /// </summary>
        public static IrBundlePlacementQuality Create(IReadOnlyList<int> assignedSlots, IReadOnlyList<IrIssueSlotMask> legalSlots, int slotCount)
        {
            ArgumentNullException.ThrowIfNull(assignedSlots);
            ArgumentNullException.ThrowIfNull(legalSlots);
            if (slotCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount));
            }

            if (assignedSlots.Count != legalSlots.Count)
            {
                throw new ArgumentException("Assigned slot count must match legal-slot count.", nameof(legalSlots));
            }

            int occupiedSlotCount = 0;
            int minSlot = slotCount;
            int maxSlot = -1;
            int slotIndexSum = 0;
            int orderInversionCount = 0;
            int constrainedSlotDisplacementCost = 0;
            var occupied = new bool[slotCount];

            for (int instructionIndex = 0; instructionIndex < assignedSlots.Count; instructionIndex++)
            {
                int slot = assignedSlots[instructionIndex];
                if (slot < 0)
                {
                    continue;
                }

                occupiedSlotCount++;
                occupied[slot] = true;
                slotIndexSum += slot;
                constrainedSlotDisplacementCost += GetConstrainedSlotDisplacementCost(legalSlots[instructionIndex], slot, slotCount);
                if (slot < minSlot)
                {
                    minSlot = slot;
                }

                if (slot > maxSlot)
                {
                    maxSlot = slot;
                }
            }

            if (occupiedSlotCount == 0)
            {
                return Empty;
            }

            int occupiedSlotSpan = (maxSlot - minSlot) + 1;
            int internalGapCount = 0;
            int largestInternalGap = 0;
            int currentGapLength = 0;
            for (int slot = minSlot; slot <= maxSlot; slot++)
            {
                if (!occupied[slot])
                {
                    internalGapCount++;
                    currentGapLength++;
                    if (currentGapLength > largestInternalGap)
                    {
                        largestInternalGap = currentGapLength;
                    }
                }
                else
                {
                    currentGapLength = 0;
                }
            }

            for (int leftIndex = 0; leftIndex < assignedSlots.Count; leftIndex++)
            {
                int leftSlot = assignedSlots[leftIndex];
                if (leftSlot < 0)
                {
                    continue;
                }

                for (int rightIndex = leftIndex + 1; rightIndex < assignedSlots.Count; rightIndex++)
                {
                    int rightSlot = assignedSlots[rightIndex];
                    if (rightSlot >= 0 && leftSlot > rightSlot)
                    {
                        orderInversionCount++;
                    }
                }
            }

            return new IrBundlePlacementQuality(
                occupiedSlotCount,
                occupiedSlotSpan,
                internalGapCount,
                largestInternalGap,
                orderInversionCount,
                minSlot,
                (slotCount - 1) - maxSlot,
                slotIndexSum,
                constrainedSlotDisplacementCost);
        }

        private static int GetConstrainedSlotDisplacementCost(IrIssueSlotMask legalSlots, int assignedSlot, int slotCount)
        {
            if (assignedSlot <= 0)
            {
                return 0;
            }

            int legalSlotCount = BitOperations.PopCount((uint)legalSlots);
            if (legalSlotCount <= 0)
            {
                return 0;
            }

            uint lowerSlotMask = ((uint)1 << assignedSlot) - 1u;
            int lowerLegalSlotCount = BitOperations.PopCount((uint)legalSlots & lowerSlotMask);
            int constraintWeight = Math.Max(1, (slotCount - legalSlotCount) + 1);
            return lowerLegalSlotCount * constraintWeight;
        }
    }
}
