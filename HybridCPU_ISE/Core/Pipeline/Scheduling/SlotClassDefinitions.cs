using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Physical lane class taxonomy for typed-slot scheduling.
    /// This enum is the canonical repository-facing description of the fixed W=8 topology:
    /// ALU lanes 0..3, LSU lanes 4..5, DMA/stream lane 6, and the aliased branch/system lane 7.
    /// Each value denotes a functional class of execution lanes in that typed bundle model.
    /// <para>
    /// HLS design note: encoded as 3-bit field (byte backing for C# convenience).
    /// Synthesizes to a 3-bit register / comparator — 3 flip-flops, 1-LUT decode depth.
    /// </para>
    /// </summary>
    public enum SlotClass : byte
    {
        /// <summary>Lanes 0–3: scalar/vector ALU-capable lanes.</summary>
        AluClass = 0,

        /// <summary>Lanes 4–5: load/store unit lanes.</summary>
        LsuClass = 1,

        /// <summary>Lane 6: DMA/stream engine lane.</summary>
        DmaStreamClass = 2,

        /// <summary>Lane 7 (aliased): branch/control-flow lane.</summary>
        BranchControl = 3,

        /// <summary>Lane 7 (aliased): system-singleton lane (CSR, Halt, PortIO).</summary>
        SystemSingleton = 4,

        /// <summary>No specific lane affinity (NOP, generic, or uninitialized).</summary>
        Unclassified = 7
    }

    /// <summary>
    /// Indicates whether a micro-operation is pinned to a specific physical lane
    /// or is flexible within its <see cref="SlotClass"/> lane set.
    /// <para>
    /// HLS design note: 1-bit field. Zero additional LUT when stored as register bit.
    /// </para>
    /// </summary>
    public enum SlotPinningKind : byte
    {
        /// <summary>
        /// Operation may be placed in any lane belonging to its <see cref="SlotClass"/>.
        /// Scheduler picks a free lane from the class lane mask.
        /// </summary>
        ClassFlexible = 0,

        /// <summary>
        /// Operation is pinned to <see cref="MicroOp.Placement"/>.<see cref="SlotPlacementMetadata.PinnedLaneId"/>.
        /// Scheduler must place it in that exact physical lane.
        /// </summary>
        HardPinned = 1
    }

    /// <summary>
    /// Structured reject reasons for the two-stage typed-slot admission pipeline.
    /// Stage A (Class Admission) and Stage B (Lane Materialization) each produce
    /// distinct reject codes for telemetry (Phase 08) and diagnostics.
    /// <para>
    /// HLS design note: 5-bit enum (byte backing). Stored in diagnostic register,
    /// not on critical timing path.
    /// </para>
    /// </summary>
    public enum TypedSlotRejectReason : byte
    {
        /// <summary>No rejection — candidate was admitted and placed.</summary>
        None = 0,

        // ── Stage A rejects ──────────────────────────────────────────

        /// <summary>
        /// Compiler's original bundle already exceeds class capacity.
        /// This is a structural/compiler fault — the bundle was packed
        /// with more ops of this class than physical lanes exist.
        /// Detected when class capacity is exhausted before any FSP inject
        /// in the current scheduling pass (<c>currentPassInjections == 0</c>).
        /// </summary>
        StaticClassOvercommit = 1,

        /// <summary>
        /// Class capacity was exhausted dynamically during intra-cycle FSP
        /// injection. Earlier injected ops in the same scheduling pass
        /// consumed remaining class capacity. This is normal runtime
        /// behavior, NOT a compiler bug.
        /// </summary>
        DynamicClassExhaustion = 2,

        /// <summary>
        /// Certificate / CanInject() safety-mask conflict.
        /// Shared resource overlap detected by <see cref="BundleResourceCertificate4Way"/>.
        /// </summary>
        ResourceConflict = 3,

        /// <summary>
        /// Domain-tag isolation violation (DomainTag &amp; CsrMemDomainCert mismatch).
        /// </summary>
        DomainReject = 4,

        /// <summary>
        /// Scoreboard conflict — SuppressLsu memory-wall gate blocked a memory op.
        /// </summary>
        ScoreboardReject = 5,

        /// <summary>
        /// Bank-pending scoreboard conflict — outstanding MSHR entry on the same
        /// memory bank for this virtual thread.
        /// </summary>
        BankPendingReject = 6,

        /// <summary>
        /// Sampled hardware load/store budget exhausted for the live widened lane4..5 LSU subset,
        /// including the direction-aware read/write turnaround split.
        /// </summary>
        HardwareBudgetReject = 7,

        /// <summary>
        /// Speculation budget exhausted — cross-thread memory op blocked
        /// because the budget counter reached zero.
        /// </summary>
        SpeculationBudgetReject = 8,

        /// <summary>
        /// Dedicated assist-only quota rejected an architecturally invisible memory assist.
        /// </summary>
        AssistQuotaReject = 9,

        /// <summary>
        /// Explicit assist widened-owner backpressure rejected an architecturally invisible
        /// memory assist before it could borrow shared outer-cap/MSHR/SRF pressure.
        /// </summary>
        AssistBackpressureReject = 10,

        // ── Stage B rejects ──────────────────────────────────────────

        /// <summary>
        /// Hard-pinned lane already occupied by another operation.
        /// </summary>
        PinnedLaneConflict = 11,

        /// <summary>
        /// All class-eligible lanes are occupied — no free lane available
        /// for a <see cref="SlotPinningKind.ClassFlexible"/> candidate.
        /// </summary>
        LateBindingConflict = 12,

        // ── Policy defers (not hard rejects) ─────────────────────────

        /// <summary>
        /// Candidate deferred by fairness-credit policy (not a hard reject).
        /// </summary>
        FairnessDeferred = 20
    }

    /// <summary>
    /// Maps <see cref="SlotClass"/> values to physical lane bitmasks for the canonical W=8 topology.
    /// The fixed lane map is ALU[0..3], LSU[4..5], DMA/stream[6], Branch/System[7].
    /// Provides capacity queries and aliased-lane detection (BranchControl / SystemSingleton share lane 7).
    /// <para>
    /// HLS design note: all methods are pure combinational (ROM-table or mux). No dynamic allocation.
    /// GetLaneMask → 8-bit ROM indexed by 3-bit SlotClass → 8 × 3 = 24 LUT1 equivalent.
    /// PopCount(8-bit) → 4-LUT chain, 2 depth.
    /// </para>
    /// </summary>
    public static class SlotClassLaneMap
    {
        // Lane mask constants for W=8 default topology.
        // Bit N set → physical lane N can host ops of that class.
        private const byte AluMask          = 0b_0000_1111; // lanes 0-3
        private const byte LsuMask          = 0b_0011_0000; // lanes 4-5
        private const byte DmaStreamMask    = 0b_0100_0000; // lane 6
        private const byte BranchMask       = 0b_1000_0000; // lane 7
        private const byte SystemMask       = 0b_1000_0000; // lane 7 (aliased with Branch)
        private const byte UnclassifiedMask = 0b_1111_1111; // any lane (placeholder)

        /// <summary>
        /// Returns the physical lane bitmask for the given <see cref="SlotClass"/>.
        /// Each set bit indicates a lane that can host operations of that class.
        /// </summary>
        /// <param name="slotClass">The slot class to query.</param>
        /// <returns>8-bit lane mask (bit N → lane N is eligible).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetLaneMask(SlotClass slotClass) => slotClass switch
        {
            SlotClass.AluClass        => AluMask,
            SlotClass.LsuClass        => LsuMask,
            SlotClass.DmaStreamClass  => DmaStreamMask,
            SlotClass.BranchControl   => BranchMask,
            SlotClass.SystemSingleton => SystemMask,
            SlotClass.Unclassified    => UnclassifiedMask,
            _ => throw new ArgumentOutOfRangeException(nameof(slotClass), slotClass, "Unknown SlotClass value.")
        };

        /// <summary>
        /// Returns the number of physical lanes available for the given <see cref="SlotClass"/>.
        /// Equivalent to PopCount of <see cref="GetLaneMask"/>.
        /// </summary>
        /// <param name="slotClass">The slot class to query.</param>
        /// <returns>Lane count (1–8).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetClassCapacity(SlotClass slotClass)
        {
            return BitOperations.PopCount(GetLaneMask(slotClass));
        }

        /// <summary>
        /// Returns the set of <see cref="SlotClass"/> values that share at least one physical lane
        /// with the given class (i.e., are aliased on the same hardware lane).
        /// For the default W=8 topology, BranchControl and SystemSingleton both map to lane 7.
        /// </summary>
        /// <param name="slotClass">The slot class to query.</param>
        /// <returns>
        /// A read-only span of co-aliased classes (excluding <paramref name="slotClass"/> itself).
        /// Empty if no aliasing exists.
        /// </returns>
        public static ReadOnlySpan<SlotClass> GetAliasedClasses(SlotClass slotClass) => slotClass switch
        {
            SlotClass.BranchControl   => [SlotClass.SystemSingleton],
            SlotClass.SystemSingleton => [SlotClass.BranchControl],
            _ => []
        };

        /// <summary>
        /// Returns <see langword="true"/> if the given <see cref="SlotClass"/> shares physical lanes
        /// with another class (aliased-lane condition).
        /// </summary>
        /// <param name="slotClass">The slot class to query.</param>
        /// <returns><see langword="true"/> for BranchControl and SystemSingleton; otherwise <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAliasedLanes(SlotClass slotClass) => slotClass switch
        {
            SlotClass.BranchControl   => true,
            SlotClass.SystemSingleton => true,
            _ => false
        };
    }

    /// <summary>
    /// Typed-slot facts communicated between compiler and runtime.
    /// Compiler sets these during bundle formation (Phase 8);
    /// runtime reads them during class-admission and lane-binding.
    /// <para>
    /// HLS design note: fixed-size — 8 × 3-bit SlotClass + 8-bit pinning mask +
    /// 2 × 32-bit counts + 5 × 8-bit class counts ≈ 80 flip-flops, no dynamic allocation.
    /// </para>
    /// </summary>
    public readonly struct TypedSlotBundleFacts
    {
        /// <summary>Required slot class for bundle slot 0.</summary>
        public SlotClass Slot0Class { get; init; }

        /// <summary>Required slot class for bundle slot 1.</summary>
        public SlotClass Slot1Class { get; init; }

        /// <summary>Required slot class for bundle slot 2.</summary>
        public SlotClass Slot2Class { get; init; }

        /// <summary>Required slot class for bundle slot 3.</summary>
        public SlotClass Slot3Class { get; init; }

        /// <summary>Required slot class for bundle slot 4.</summary>
        public SlotClass Slot4Class { get; init; }

        /// <summary>Required slot class for bundle slot 5.</summary>
        public SlotClass Slot5Class { get; init; }

        /// <summary>Required slot class for bundle slot 6.</summary>
        public SlotClass Slot6Class { get; init; }

        /// <summary>Required slot class for bundle slot 7.</summary>
        public SlotClass Slot7Class { get; init; }

        /// <summary>
        /// Per-slot pinning kind mask (bit per slot: 0 = flexible, 1 = pinned).
        /// Bit N corresponds to slot N.
        /// </summary>
        public byte PinningKindMask { get; init; }

        /// <summary>Total class-flexible ops in this bundle.</summary>
        public int FlexibleOpCount { get; init; }

        /// <summary>Total hard-pinned ops in this bundle.</summary>
        public int PinnedOpCount { get; init; }

        /// <summary>Number of ALU-class ops in the bundle (compiler structural budget).</summary>
        public byte AluCount { get; init; }

        /// <summary>Number of LSU-class ops in the bundle (compiler structural budget).</summary>
        public byte LsuCount { get; init; }

        /// <summary>Number of DMA/Stream-class ops in the bundle (compiler structural budget).</summary>
        public byte DmaStreamCount { get; init; }

        /// <summary>Number of BranchControl-class ops in the bundle (compiler structural budget).</summary>
        public byte BranchControlCount { get; init; }

        /// <summary>Number of SystemSingleton-class ops in the bundle (compiler structural budget).</summary>
        public byte SystemSingletonCount { get; init; }

        /// <summary>
        /// Returns <see langword="true"/> when all counts are zero,
        /// indicating no typed-slot facts were provided (legacy / pre-Phase 8 bundle).
        /// <c>default(TypedSlotBundleFacts).IsEmpty == true</c>.
        /// </summary>
        public bool IsEmpty =>
            FlexibleOpCount == 0
            && PinnedOpCount == 0
            && AluCount == 0
            && LsuCount == 0
            && DmaStreamCount == 0
            && BranchControlCount == 0
            && SystemSingletonCount == 0;

        /// <summary>
        /// Gets the per-slot <see cref="SlotClass"/> by index (0–7).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SlotClass GetSlotClass(int index) => index switch
        {
            0 => Slot0Class,
            1 => Slot1Class,
            2 => Slot2Class,
            3 => Slot3Class,
            4 => Slot4Class,
            5 => Slot5Class,
            6 => Slot6Class,
            7 => Slot7Class,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Slot index must be 0–7.")
        };

        /// <summary>
        /// Returns whether slot at <paramref name="index"/> is hard-pinned (bit set in <see cref="PinningKindMask"/>).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSlotPinned(int index) => (PinningKindMask & (1 << index)) != 0;

        /// <summary>
        /// Builds <see cref="TypedSlotBundleFacts"/> from a live VLIW bundle.
        /// Pure function — scans the array, counts per-class ops, and encodes pinning mask.
        /// </summary>
        /// <param name="bundle">The VLIW bundle (nullable slots, up to W=8).</param>
        /// <param name="bundleWidth">Number of slots to scan (default 8).</param>
        /// <returns>Fully populated facts struct.</returns>
        public static TypedSlotBundleFacts FromBundle(MicroOp?[] bundle, int bundleWidth = 8)
        {
            ArgumentNullException.ThrowIfNull(bundle);

            int limit = bundle.Length < bundleWidth ? bundle.Length : bundleWidth;

            Span<SlotClass> slotClasses = stackalloc SlotClass[8];
            byte pinningMask = 0;
            int flexibleCount = 0;
            int pinnedCount = 0;
            byte aluCount = 0, lsuCount = 0, dmaCount = 0, branchCount = 0, sysCount = 0;

            for (int i = 0; i < 8; i++)
            {
                if (i < limit && bundle[i] is { } op)
                {
                    SlotPlacementMetadata placement = op.Placement;
                    slotClasses[i] = placement.RequiredSlotClass;

                    if (placement.PinningKind == SlotPinningKind.HardPinned)
                    {
                        pinningMask |= (byte)(1 << i);
                        pinnedCount++;
                    }
                    else
                    {
                        flexibleCount++;
                    }

                    switch (placement.RequiredSlotClass)
                    {
                        case SlotClass.AluClass:        aluCount++;    break;
                        case SlotClass.LsuClass:        lsuCount++;    break;
                        case SlotClass.DmaStreamClass:  dmaCount++;    break;
                        case SlotClass.BranchControl:   branchCount++; break;
                        case SlotClass.SystemSingleton: sysCount++;    break;
                    }
                }
                else
                {
                    slotClasses[i] = SlotClass.Unclassified;
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
    }

    /// <summary>
    /// Cross-validation report generated when runtime rejects an op
    /// that compiler declared structurally admissible.
    /// Distinguishes structural mismatches (compiler bugs) from
    /// dynamic rejects (expected runtime behavior).
    /// <para>
    /// HLS design note: diagnostic-only struct, not on critical timing path.
    /// </para>
    /// </summary>
    public readonly struct AgreementViolationReport
    {
        /// <summary>What compiler declared about the bundle.</summary>
        public TypedSlotBundleFacts CompilerFacts { get; init; }

        /// <summary>What runtime rejected and why.</summary>
        public TypedSlotRejectReason RuntimeReject { get; init; }

        /// <summary>
        /// <see langword="true"/> if this is a structural mismatch (compiler bug):
        /// <see cref="TypedSlotRejectReason.StaticClassOvercommit"/> or
        /// <see cref="TypedSlotRejectReason.PinnedLaneConflict"/>.
        /// <see langword="false"/> for dynamic rejects (expected runtime behavior).
        /// </summary>
        public bool IsStructuralMismatch { get; init; }

        /// <summary>
        /// Classifies a runtime reject reason as structural (compiler bug) or dynamic (expected).
        /// </summary>
        public static bool IsStructuralReject(TypedSlotRejectReason reason) =>
            reason is TypedSlotRejectReason.StaticClassOvercommit
                   or TypedSlotRejectReason.PinnedLaneConflict;

        /// <summary>
        /// Classifies a runtime reject reason as dynamic (expected runtime behavior).
        /// </summary>
        public static bool IsDynamicReject(TypedSlotRejectReason reason) =>
            reason is TypedSlotRejectReason.ScoreboardReject
                   or TypedSlotRejectReason.BankPendingReject
                   or TypedSlotRejectReason.HardwareBudgetReject
                   or TypedSlotRejectReason.AssistQuotaReject
                   or TypedSlotRejectReason.AssistBackpressureReject
                   or TypedSlotRejectReason.SpeculationBudgetReject
                   or TypedSlotRejectReason.DomainReject
                   or TypedSlotRejectReason.DynamicClassExhaustion;
    }
}
