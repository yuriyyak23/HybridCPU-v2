using System;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        /// <summary>
        /// Public diagnostics/model helper for typed-slot rejection classification.
        /// Combines certificate-level and class-level reject information into a
        /// <see cref="TypedSlotRejectClassification"/> that bridges compiler/runtime vocabulary.
        /// Scheduler runtime paths use <see cref="IRuntimeLegalityService"/> instead of
        /// calling this helper on a concrete verifier.
        /// Compiler-only preflight categories such as `TypedSlotFactsInvalid` and
        /// `TypedSlotAliasedLaneConflict` do not have runtime reject twins and remain
        /// outside this classifier.
        /// <para>
        /// HLS: pure combinational - 3 comparators, no flip-flops, no timing impact.
        /// </para>
        /// </summary>
        public static TypedSlotRejectClassification ClassifyReject(
            TypedSlotRejectReason admissionReject,
            CertificateRejectDetail certDetail,
            SlotClass candidateClass,
            SlotPinningKind pinningKind)
        {
            return new TypedSlotRejectClassification(
                admissionReject,
                certDetail,
                candidateClass,
                pinningKind,
                isPinnedConflict: admissionReject == TypedSlotRejectReason.PinnedLaneConflict,
                isClassCapacityIssue: admissionReject is TypedSlotRejectReason.StaticClassOvercommit
                                   or TypedSlotRejectReason.DynamicClassExhaustion,
                isStaticOvercommit: admissionReject == TypedSlotRejectReason.StaticClassOvercommit,
                isDynamicExhaustion: admissionReject == TypedSlotRejectReason.DynamicClassExhaustion,
                isDynamicStateIssue: admissionReject is TypedSlotRejectReason.ScoreboardReject
                                   or TypedSlotRejectReason.BankPendingReject
                                   or TypedSlotRejectReason.HardwareBudgetReject
                                   or TypedSlotRejectReason.AssistQuotaReject
                                   or TypedSlotRejectReason.AssistBackpressureReject
                                   or TypedSlotRejectReason.SpeculationBudgetReject
                                   or TypedSlotRejectReason.DynamicClassExhaustion);
        }

        TypedSlotRejectClassification ILegalityChecker.ClassifyReject(
            TypedSlotRejectReason admissionReject,
            CertificateRejectDetail certDetail,
            SlotClass candidateClass,
            SlotPinningKind pinningKind)
        {
            return ClassifyReject(admissionReject, certDetail, candidateClass, pinningKind);
        }

        /// <summary>
        /// Validate typed-slot facts emitted by the compiler against a live bundle.
        /// Called at decode time if validation is enabled (diagnostic-only, not on critical path).
        /// Current staged status is <see cref="TypedSlotFactMode.ValidationOnly"/> via
        /// <see cref="TypedSlotFactStaging.CurrentMode"/>: empty/default facts remain
        /// legal, and canonical runtime execution must stay correct without them.
        /// <para>
        /// Check 1: per-slot class matches <see cref="MicroOp.Placement"/>.<see cref="SlotPlacementMetadata.RequiredSlotClass"/> for non-null ops.
        /// Check 2: per-class counts match actual ops in bundle.
        /// Check 3: no duplicate <see cref="MicroOp.Placement"/>.<see cref="SlotPlacementMetadata.PinnedLaneId"/> among hard-pinned ops.
        /// Check 4: class counts within <see cref="SlotClassLaneMap.GetClassCapacity"/> bounds.
        /// Check 5: PinnedOpCount + FlexibleOpCount == total non-null ops.
        /// </para>
        /// </summary>
        /// <param name="facts">Compiler-emitted typed-slot facts.</param>
        /// <param name="bundle">The VLIW bundle (nullable slots).</param>
        /// <param name="bundleWidth">Number of slots to validate (default 8).</param>
        /// <returns><see langword="true"/> if all checks pass.</returns>
        public static bool ValidateTypedSlotFacts(
            TypedSlotBundleFacts facts,
            MicroOp?[] bundle,
            int bundleWidth = 8)
        {
            ArgumentNullException.ThrowIfNull(bundle);

            // Current ValidationOnly mainline: empty/default facts mean the producer
            // did not supply typed-slot facts for this bundle, so runtime legality
            // must continue without treating that absence as an admission failure.
            if (facts.IsEmpty)
                return true;

            int limit = bundle.Length < bundleWidth ? bundle.Length : bundleWidth;

            byte actualAlu = 0, actualLsu = 0, actualDma = 0, actualBranch = 0, actualSys = 0;
            int actualFlexible = 0, actualPinned = 0;
            int totalNonNull = 0;

            // Track pinned lane IDs for duplicate detection (8-bit occupancy for lanes 0-7)
            byte pinnedLaneOccupancy = 0;

            for (int i = 0; i < limit; i++)
            {
                if (bundle[i] is not { } op)
                    continue;

                totalNonNull++;
                SlotPlacementMetadata placement = op.Placement;

                // Check 1: per-slot class match
                if (placement.RequiredSlotClass != facts.GetSlotClass(i))
                    return false;

                switch (placement.RequiredSlotClass)
                {
                    case SlotClass.AluClass:        actualAlu++;    break;
                    case SlotClass.LsuClass:        actualLsu++;    break;
                    case SlotClass.DmaStreamClass:  actualDma++;    break;
                    case SlotClass.BranchControl:   actualBranch++; break;
                    case SlotClass.SystemSingleton: actualSys++;    break;
                }

                if (placement.PinningKind == SlotPinningKind.HardPinned)
                {
                    actualPinned++;
                    byte laneBit = (byte)(1 << placement.PinnedLaneId);
                    if ((pinnedLaneOccupancy & laneBit) != 0)
                        return false;

                    pinnedLaneOccupancy |= laneBit;
                }
                else
                {
                    actualFlexible++;
                }
            }

            if (facts.AluCount != actualAlu
                || facts.LsuCount != actualLsu
                || facts.DmaStreamCount != actualDma
                || facts.BranchControlCount != actualBranch
                || facts.SystemSingletonCount != actualSys)
            {
                return false;
            }

            if (facts.AluCount > SlotClassLaneMap.GetClassCapacity(SlotClass.AluClass)
                || facts.LsuCount > SlotClassLaneMap.GetClassCapacity(SlotClass.LsuClass)
                || facts.DmaStreamCount > SlotClassLaneMap.GetClassCapacity(SlotClass.DmaStreamClass)
                || facts.BranchControlCount > SlotClassLaneMap.GetClassCapacity(SlotClass.BranchControl)
                || facts.SystemSingletonCount > SlotClassLaneMap.GetClassCapacity(SlotClass.SystemSingleton))
            {
                return false;
            }

            if (facts.PinnedOpCount + facts.FlexibleOpCount != totalNonNull)
                return false;

            if (facts.PinnedOpCount != actualPinned || facts.FlexibleOpCount != actualFlexible)
                return false;

            return true;
        }

        /// <summary>
        /// Verify that compiler typed-slot facts are consistent with runtime capacity state.
        /// Pure diagnostic agreement check under the current
        /// <see cref="TypedSlotFactMode.ValidationOnly"/> staging. This does not yet
        /// make facts mandatory for runtime admission.
        /// <para>
        /// HLS: pure combinational - 5 comparators, no flip-flops, no timing impact.
        /// </para>
        /// </summary>
        /// <param name="compilerFacts">Compiler-emitted typed-slot facts.</param>
        /// <param name="runtimeCapacity">Current runtime class-capacity state.</param>
        /// <returns><see langword="true"/> if compiler class counts fit runtime capacity.</returns>
        public static bool VerifyTypedSlotAgreement(
            TypedSlotBundleFacts compilerFacts,
            SlotClassCapacityState runtimeCapacity)
        {
            return compilerFacts.AluCount <= runtimeCapacity.AluTotal
                && compilerFacts.LsuCount <= runtimeCapacity.LsuTotal
                && compilerFacts.DmaStreamCount <= runtimeCapacity.DmaStreamTotal
                && compilerFacts.BranchControlCount <= runtimeCapacity.BranchControlTotal
                && compilerFacts.SystemSingletonCount <= runtimeCapacity.SystemSingletonTotal;
        }
    }
}
