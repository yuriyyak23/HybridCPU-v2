using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.IR;

/// <summary>
/// Emits <see cref="TypedSlotBundleFacts"/> from a compiler-side materialized bundle.
/// Uses <see cref="IrInstructionAnnotation.RequiredSlotClass"/> (ISE vocabulary)
/// and <see cref="IrSlotClassMapping.ToRuntimePinningKind"/> for coordinate translation.
/// Compiler preflight emits facts eagerly even though the current runtime staging
/// surface still reports <see cref="TypedSlotFactMode.ValidationOnly"/>.
/// </summary>
/// <remarks>
/// <c>DmaStreamClass</c> instructions are reported as <c>ClassFlexible</c> in the pinning mask
/// (not pinned) because <see cref="IrSlotBindingKind.SingletonClass"/> maps to
/// <see cref="SlotPinningKind.ClassFlexible"/>. The singleton constraint is enforced by
/// <see cref="SlotClassLaneMap"/> topology (lane 6 only, capacity=1), not by pinning metadata.
/// </remarks>
public static class HybridCpuTypedSlotFactsEmitter
{
    /// <summary>
    /// Emit typed-slot facts from a materialized bundle.
    /// Works on both legacy and class-first bundler paths.
    /// </summary>
    public static TypedSlotBundleFacts EmitFacts(IrMaterializedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        Span<SlotClass> slotClasses = stackalloc SlotClass[8];
        byte pinningMask = 0;
        int flexibleCount = 0;
        int pinnedCount = 0;
        byte aluCount = 0, lsuCount = 0, dmaCount = 0, branchCount = 0, sysCount = 0;

        foreach (IrMaterializedBundleSlot slot in bundle.Slots)
        {
            int i = slot.SlotIndex;

            if (slot.Instruction is not { } instruction)
            {
                slotClasses[i] = SlotClass.Unclassified;
                continue;
            }

            SlotClass slotClass = instruction.Annotation.RequiredSlotClass;
            slotClasses[i] = slotClass;

            SlotPinningKind pinningKind = IrSlotClassMapping.ToRuntimePinningKind(instruction.Annotation.BindingKind);
            if (pinningKind == SlotPinningKind.HardPinned)
            {
                pinningMask |= (byte)(1 << i);
                pinnedCount++;
            }
            else
            {
                flexibleCount++;
            }

            switch (slotClass)
            {
                case SlotClass.AluClass:        aluCount++;    break;
                case SlotClass.LsuClass:        lsuCount++;    break;
                case SlotClass.DmaStreamClass:  dmaCount++;    break;
                case SlotClass.BranchControl:   branchCount++; break;
                case SlotClass.SystemSingleton: sysCount++;    break;
            }
        }

        return new TypedSlotBundleFacts
        {
            Slot0Class = slotClasses[0],
            Slot1Class = slotClasses[1],
            Slot2Class = slotClasses[2],
            Slot3Class = slotClasses[3],
            Slot4Class = slotClasses[4],
            Slot5Class = slotClasses[5],
            Slot6Class = slotClasses[6],
            Slot7Class = slotClasses[7],
            PinningKindMask = pinningMask,
            FlexibleOpCount = flexibleCount,
            PinnedOpCount = pinnedCount,
            AluCount = aluCount,
            LsuCount = lsuCount,
            DmaStreamCount = dmaCount,
            BranchControlCount = branchCount,
            SystemSingletonCount = sysCount
        };
    }

    /// <summary>
    /// Compiler-side validation of emitted facts (subset of ISE checks).
    /// Does not require <c>MicroOp[]</c> — validates internal consistency only.
    /// Under the current <see cref="TypedSlotFactMode.ValidationOnly"/> staging,
    /// empty facts still remain acceptable for canonical runtime execution.
    /// </summary>
    public static bool ValidateEmittedFacts(TypedSlotBundleFacts facts)
    {
        // Current ValidationOnly mainline keeps missing facts as an allowed state.
        if (facts.IsEmpty)
            return true;

        // Check 1: per-class counts within capacity bounds
        if (facts.AluCount > SlotClassLaneMap.GetClassCapacity(SlotClass.AluClass)
            || facts.LsuCount > SlotClassLaneMap.GetClassCapacity(SlotClass.LsuClass)
            || facts.DmaStreamCount > SlotClassLaneMap.GetClassCapacity(SlotClass.DmaStreamClass)
            || facts.BranchControlCount > SlotClassLaneMap.GetClassCapacity(SlotClass.BranchControl)
            || facts.SystemSingletonCount > SlotClassLaneMap.GetClassCapacity(SlotClass.SystemSingleton))
        {
            return false;
        }

        // Check 2: total ops within bundle width
        if (facts.PinnedOpCount + facts.FlexibleOpCount > 8)
            return false;

        // Check 3: aliased-lane constraint — BranchControl and SystemSingleton share lane 7
        if (facts.BranchControlCount > 0 && facts.SystemSingletonCount > 0 && SlotClassLaneMap.HasAliasedLanes(SlotClass.BranchControl))
            return false;

        // Check 4: singleton constraint — DmaStream has capacity 1
        if (facts.DmaStreamCount > SlotClassLaneMap.GetClassCapacity(SlotClass.DmaStreamClass))
            return false;

        // Check 5: class count totals match pinned + flexible totals
        int classTotal = facts.AluCount + facts.LsuCount + facts.DmaStreamCount
                         + facts.BranchControlCount + facts.SystemSingletonCount;
        if (classTotal != facts.PinnedOpCount + facts.FlexibleOpCount)
            return false;

        return true;
    }
}
