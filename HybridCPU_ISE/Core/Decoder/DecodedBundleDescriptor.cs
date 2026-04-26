using System;
using System.Collections.Generic;
using System.Numerics;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Decode-structural severity classification for a dependency pair within a bundle.
    /// Determined solely from register lists, slot class, and memory bank intent вЂ”
    /// never from runtime state, pipeline occupancy, or scheduling policy.
    /// This is a triage hint for the runtime, not a final admission decision.
    /// </summary>
    public enum HazardTriageClass : byte
    {
        /// The slot pair has no bundle-local dependency that would prevent parallel issue.
        Safe = 0,

        /// Decode sees a potential conflict, but runtime may resolve it
        /// (e.g., WAR between scalar slots, cross-class memory bank divergence).
        NeedsRuntimeCheck = 1,

        /// Decode is certain that parallel issue is impossible for this pair
        /// (e.g., RAW or WAW between two scalar-cluster-internal slots).
        HardReject = 2
    }

    /// <summary>
    /// Dominant effect type for a decoded bundle-local hazard.
    /// This is additive metadata for compiler-facing analysis and does not alter runtime admission.
    /// </summary>
    public enum HazardEffectKind : byte
    {
        RegisterData = 0,
        MemoryBank = 1,
        ControlFlow = 2,
        SystemBarrier = 3,
        PinnedLane = 4
    }

    /// <summary>
    /// Typed classification for a slot's relationship to the scalar cluster target.
    /// Provides explicit contract for downstream consumers without policy leakage.
    /// </summary>
    public enum ScalarLaneTargetClass : byte
    {
        /// Slot is not eligible for the scalar cluster (memory, vector, control, system, DMA, or empty/NOP).
        NotEligible = 0,

        /// <summary>
        /// Slot is structurally eligible for the scalar cluster but excluded from the current
        /// prepared group due to bundle-local hazards or the 4-way ceiling.
        /// </summary>
        Eligible = 1,

        /// <summary>
        /// Slot is eligible and admitted into the prepared scalar issue group (within the 4-way ceiling).
        /// </summary>
        Prepared = 2
    }

    [Flags]
    public enum DecodedBundleFlags : ushort
    {
        None = 0,
        HasValidSlots = 1 << 0,
        HasEmptyOrNopSlots = 1 << 1,
        HasControlFlow = 1 << 2,
        HasMemoryOps = 1 << 3,
        HasVectorOps = 1 << 4,
        HasRegisterWrites = 1 << 5,
        HasPinnedOps = 1 << 6,
        HasCrossThreadSpan = 1 << 7
    }

    public enum DecodedBundleStateKind : byte
    {
        Empty = 0,
        Canonical = 1,
        ForegroundMutated = 2,
        Replay = 3,
        DecodeFault = 4
    }

    public enum DecodedBundleStateOrigin : byte
    {
        None = 0,
        Reset = 1,
        CanonicalDecode = 2,
        DecodeFallbackTrap = 3,
        ReplayBundleLoad = 4,
        ForegroundBundlePublication = 5,
        SingleSlotMutation = 7,
        ClearMaskMutation = 8,
        FspPacking = 9
    }

    public enum DecodedBundleStateOwnerKind : byte
    {
        None = 0,
        BaseRuntimePublication = 1,
        DerivedIssuePlanPublication = 2
    }

    public readonly struct DecodedBundleSlotDescriptor
    {
        private static readonly SlotPlacementMetadata EmptySlotPlacement = new SlotPlacementMetadata
        {
            RequiredSlotClass = SlotClass.Unclassified,
            PinningKind = SlotPinningKind.HardPinned,
            PinnedLaneId = 0,
            DomainTag = 0
        };

        public static DecodedBundleSlotDescriptor Create(
            byte slotIndex,
            MicroOp? microOp,
            bool? isVectorOpOverride = null)
        {
            bool isEmptyOrNop = microOp == null || microOp is NopMicroOp;
            MicroOpAdmissionMetadata admission = microOp?.AdmissionMetadata ?? MicroOpAdmissionMetadata.Default;
            bool isMemoryOp = microOp != null && admission.IsMemoryOp;
            bool isControlFlow = microOp != null && admission.IsControlFlow;
            bool writesRegister = microOp != null && admission.WritesRegister;
            bool isVectorOp = isVectorOpOverride ?? (microOp is VectorMicroOp || microOp?.Class == MicroOpClass.Vector);
            SlotPlacementMetadata placement = microOp != null ? admission.Placement : EmptySlotPlacement;

            return new DecodedBundleSlotDescriptor(
                microOp: microOp!,
                slotIndex: slotIndex,
                virtualThreadId: microOp?.VirtualThreadId ?? 0,
                ownerThreadId: microOp?.OwnerThreadId ?? 0,
                opCode: microOp?.OpCode ?? 0,
                readRegisters: microOp != null ? admission.ReadRegisters : Array.Empty<int>(),
                writeRegisters: NormalizeWriteRegisters(microOp!, admission),
                writesRegister: writesRegister,
                isMemoryOp: isMemoryOp,
                isControlFlow: isControlFlow,
                placement: placement,
                memoryBankIntent: microOp is TrapMicroOp trapMicroOp && isMemoryOp
                    ? trapMicroOp.ProjectedMemoryBankIntent
                    : microOp is LoadStoreMicroOp loadStoreMicroOp
                        ? loadStoreMicroOp.MemoryBankId
                        : -1,
                isFspInjected: microOp?.IsFspInjected ?? false,
                isEmptyOrNop: isEmptyOrNop,
                isVectorOp: isVectorOp);
        }

        private static IReadOnlyList<int> NormalizeWriteRegisters(MicroOp microOp, MicroOpAdmissionMetadata admission)
        {
            if (admission.WriteRegisters != null && admission.WriteRegisters.Count > 0)
                return admission.WriteRegisters;

            if (!admission.WritesRegister || microOp == null)
                return Array.Empty<int>();

            microOp.ValidatePublishedWriteRegisterContract(
                "DecodedBundleSlotDescriptor.Create",
                admission);
            throw new InvalidOperationException(
                $"DecodedBundleSlotDescriptor.Create observed {microOp.GetType().Name} " +
                $"(opcode 0x{microOp.OpCode:X}) with WritesRegister=true but an empty canonical " +
                "AdmissionMetadata.WriteRegisters list. DestRegID fallback is retired.");
        }

        public DecodedBundleSlotDescriptor(
            MicroOp microOp,
            byte slotIndex,
            int virtualThreadId,
            int ownerThreadId,
            uint opCode,
            IReadOnlyList<int> readRegisters,
            IReadOnlyList<int> writeRegisters,
            bool writesRegister,
            bool isMemoryOp,
            bool isControlFlow,
            SlotPlacementMetadata placement,
            int memoryBankIntent,
            bool isFspInjected,
            bool isEmptyOrNop,
            bool isVectorOp = false)
        {
            MicroOp = microOp;
            SlotIndex = slotIndex;
            VirtualThreadId = virtualThreadId;
            OwnerThreadId = ownerThreadId;
            OpCode = opCode;
            ReadRegisters = readRegisters;
            WriteRegisters = writeRegisters;
            WritesRegister = writesRegister;
            IsMemoryOp = isMemoryOp;
            IsControlFlow = isControlFlow;
            Placement = placement;
            MemoryBankIntent = memoryBankIntent;
            IsFspInjected = isFspInjected;
            IsEmptyOrNop = isEmptyOrNop;
            IsVectorOp = isVectorOp;
        }

        public MicroOp MicroOp { get; }
        public byte SlotIndex { get; }
        public int VirtualThreadId { get; }
        public int OwnerThreadId { get; }
        public uint OpCode { get; }
        public IReadOnlyList<int> ReadRegisters { get; }
        public IReadOnlyList<int> WriteRegisters { get; }
        public bool WritesRegister { get; }
        public bool IsMemoryOp { get; }
        public bool IsControlFlow { get; }
        public SlotPlacementMetadata Placement { get; }
        public int MemoryBankIntent { get; }
        public bool IsFspInjected { get; }
        public bool IsEmptyOrNop { get; }
        public bool IsVectorOp { get; }
        public bool IsValid => MicroOp != null;

        internal SlotPlacementMetadata GetRuntimeAdmissionPlacement()
        {
            if (MicroOp == null || MicroOp is TrapMicroOp)
                return Placement;

            return MicroOp.AdmissionMetadata.Placement;
        }

        internal bool GetRuntimeAdmissionIsMemoryOp()
        {
            if (MicroOp == null || MicroOp is TrapMicroOp)
                return IsMemoryOp;

            return MicroOp.AdmissionMetadata.IsMemoryOp;
        }

        internal bool GetRuntimeAdmissionIsControlFlow()
        {
            if (MicroOp == null || MicroOp is TrapMicroOp)
                return IsControlFlow;

            return MicroOp.AdmissionMetadata.IsControlFlow;
        }

        internal bool GetRuntimeAdmissionWritesRegister()
        {
            if (MicroOp == null || MicroOp is TrapMicroOp)
                return WritesRegister;

            return MicroOp.AdmissionMetadata.WritesRegister;
        }

        internal IReadOnlyList<int> GetRuntimeAdmissionReadRegisters()
        {
            if (MicroOp == null || MicroOp is TrapMicroOp)
                return ReadRegisters;

            return MicroOp.AdmissionMetadata.ReadRegisters;
        }

        internal IReadOnlyList<int> GetRuntimeAdmissionWriteRegisters()
        {
            if (MicroOp == null || MicroOp is TrapMicroOp)
                return WriteRegisters;

            return MicroOp.AdmissionMetadata.WriteRegisters;
        }

        internal int GetRuntimeExecutionVirtualThreadId()
        {
            if (MicroOp != null)
                return MicroOp.VirtualThreadId;

            return VirtualThreadId;
        }

        internal int GetRuntimeExecutionOwnerThreadId()
        {
            if (MicroOp != null)
                return MicroOp.OwnerThreadId;

            return OwnerThreadId;
        }

        internal uint GetRuntimeExecutionOpCode()
        {
            if (MicroOp != null)
                return MicroOp.OpCode;

            return OpCode;
        }

        internal ulong GetRuntimeAdmissionDomainTag()
        {
            if (MicroOp == null || MicroOp is TrapMicroOp)
                return Placement.DomainTag;

            return MicroOp.AdmissionMetadata.DomainTag;
        }

        internal bool GetRuntimeExecutionIsFspInjected()
        {
            if (MicroOp == null)
                return IsFspInjected;

            return MicroOp.IsFspInjected;
        }

        internal bool GetRuntimeExecutionIsEmptyOrNop()
        {
            if (MicroOp == null)
                return true;

            return MicroOp is NopMicroOp;
        }

        internal int GetRuntimeExecutionMemoryBankIntent()
        {
            if (MicroOp is LoadStoreMicroOp loadStoreMicroOp)
                return loadStoreMicroOp.MemoryBankId;

            return MemoryBankIntent;
        }

        /// <summary>
        /// Whether this slot is structurally eligible for the 4-way scalar cluster from decode facts alone.
        /// Mirrors the decode-side eligibility rule: non-empty, non-memory, non-control-flow, non-vector, AluClass.
        /// This is a preparatory hint, not a final admission decision.
        /// </summary>
        public bool CanTargetScalarCluster =>
            !IsEmptyOrNop
            && !IsMemoryOp
            && !IsControlFlow
            && !IsVectorOp
            && Placement.RequiredSlotClass == SlotClass.AluClass;

        /// <summary>
        /// Classify this slot's relationship to the scalar cluster target given the bundle-wide prepared mask.
        /// Returns <see cref="ScalarLaneTargetClass.NotEligible"/> if the slot cannot target the scalar cluster,
        /// <see cref="ScalarLaneTargetClass.Prepared"/> if it was admitted into the prepared group,
        /// or <see cref="ScalarLaneTargetClass.Eligible"/> if it is structurally eligible but excluded
        /// from the current prepared group (due to hazards or the 4-way ceiling).
        /// </summary>
        public ScalarLaneTargetClass ClassifyScalarLaneTarget(byte preparedMask)
        {
            if (!CanTargetScalarCluster)
                return ScalarLaneTargetClass.NotEligible;

            byte slotBit = (byte)(1 << SlotIndex);
            return (preparedMask & slotBit) != 0
                ? ScalarLaneTargetClass.Prepared
                : ScalarLaneTargetClass.Eligible;
        }

        /// <summary>
        /// Returns <see langword="true"/> when the two register sets share at least one register ID
        /// and both slots belong to the same virtual thread.
        /// <para>
        /// Blueprint В§7: "РЎРєРѕСЂСЂРµРєС‚РёСЂРѕРІР°С‚СЊ HasRegisterIntersection: РІРѕР·РјРѕР¶РЅРѕ, РґРµР»Р°С‚СЊ РµРіРѕ С‡Р°СЃС‚СЊСЋ
        /// SlotDescriptor, Р° РЅРµ СЃС‚Р°С‚РёС‡РµСЃРєРёРј РїСЂРёРІР°С‚РЅС‹Рј РјРµС‚РѕРґРѕРј."
        /// </para>
        /// </summary>
        /// <param name="left">First slot (provides its VT for thread-isolation check).</param>
        /// <param name="right">Second slot (provides its VT for thread-isolation check).</param>
        /// <param name="leftRegs">Register list from <paramref name="left"/> to inspect.</param>
        /// <param name="rightRegs">Register list from <paramref name="right"/> to inspect.</param>
        /// <returns><see langword="true"/> if the two slots are in the same VT and share at least one register.</returns>
        public static bool HasRegisterIntersection(
            in DecodedBundleSlotDescriptor left,
            in DecodedBundleSlotDescriptor right,
            IReadOnlyList<int> leftRegs,
            IReadOnlyList<int> rightRegs)
        {
            if (left.VirtualThreadId != right.VirtualThreadId)
                return false;

            if (leftRegs == null || rightRegs == null || leftRegs.Count == 0 || rightRegs.Count == 0)
                return false;

            for (int i = 0; i < leftRegs.Count; i++)
            {
                int registerId = leftRegs[i];
                for (int j = 0; j < rightRegs.Count; j++)
                {
                    if (registerId == rightRegs[j])
                        return true;
                }
            }

            return false;
        }
    }

    public readonly struct DecodedBundleTypedSlotFacts
    {
        public DecodedBundleTypedSlotFacts(
            byte aluClassMask,
            byte lsuClassMask,
            byte dmaStreamClassMask,
            byte branchControlMask,
            byte systemSingletonMask,
            byte unclassifiedMask,
            byte pinnedSlotMask,
            byte flexibleSlotMask)
        {
            AluClassMask = aluClassMask;
            LsuClassMask = lsuClassMask;
            DmaStreamClassMask = dmaStreamClassMask;
            BranchControlMask = branchControlMask;
            SystemSingletonMask = systemSingletonMask;
            UnclassifiedMask = unclassifiedMask;
            PinnedSlotMask = pinnedSlotMask;
            FlexibleSlotMask = flexibleSlotMask;
        }

        public byte AluClassMask { get; }
        public byte LsuClassMask { get; }
        public byte DmaStreamClassMask { get; }
        public byte BranchControlMask { get; }
        public byte SystemSingletonMask { get; }
        public byte UnclassifiedMask { get; }
        public byte PinnedSlotMask { get; }
        public byte FlexibleSlotMask { get; }

        public int AluClassCount => BitOperations.PopCount((uint)AluClassMask);
        public int LsuClassCount => BitOperations.PopCount((uint)LsuClassMask);
        public int DmaStreamClassCount => BitOperations.PopCount((uint)DmaStreamClassMask);
        public int BranchControlCount => BitOperations.PopCount((uint)BranchControlMask);
        public int SystemSingletonCount => BitOperations.PopCount((uint)SystemSingletonMask);
        public int UnclassifiedCount => BitOperations.PopCount((uint)UnclassifiedMask);
    }

    public readonly struct DecodedBundleDependencySummary
    {
        public DecodedBundleDependencySummary(
            ulong readRegisterMask,
            ulong writeRegisterMask,
            ResourceBitset aggregateResourceMask,
            ulong rawDependencyMask,
            ulong wawDependencyMask,
            ulong warDependencyMask,
            ulong controlConflictMask,
            ulong memoryConflictMask,
            byte scalarClusterEligibleMask,
            ulong systemBarrierConflictMask = 0,
            ulong pinnedLaneConflictMask = 0)
        {
            ReadRegisterMask = readRegisterMask;
            WriteRegisterMask = writeRegisterMask;
            AggregateResourceMask = aggregateResourceMask;
            RawDependencyMask = rawDependencyMask;
            WawDependencyMask = wawDependencyMask;
            WarDependencyMask = warDependencyMask;
            ControlConflictMask = controlConflictMask;
            MemoryConflictMask = memoryConflictMask;
            ScalarClusterEligibleMask = scalarClusterEligibleMask;
            _systemBarrierConflictMask = systemBarrierConflictMask;
            _pinnedLaneConflictMask = pinnedLaneConflictMask;
        }

        private readonly ulong _systemBarrierConflictMask;
        private readonly ulong _pinnedLaneConflictMask;

        public ulong ReadRegisterMask { get; }
        public ulong WriteRegisterMask { get; }
        public ResourceBitset AggregateResourceMask { get; }

        /// <summary>
        /// Directed 8x8 RAW matrix encoded in a 64-bit mask.
        /// Bit <c>(sourceSlot * 8 + targetSlot)</c> is set when an earlier slot writes
        /// a register read by a later slot.
        /// </summary>
        public ulong RawDependencyMask { get; }

        /// <summary>
        /// Directed 8x8 WAW matrix encoded in a 64-bit mask.
        /// Bit <c>(sourceSlot * 8 + targetSlot)</c> is set when two slots target the
        /// same architectural destination in bundle order.
        /// </summary>
        public ulong WawDependencyMask { get; }

        /// <summary>
        /// Directed 8x8 WAR matrix encoded in a 64-bit mask.
        /// Bit <c>(sourceSlot * 8 + targetSlot)</c> is set when an earlier slot reads
        /// a register written by a later slot.
        /// </summary>
        public ulong WarDependencyMask { get; }

        /// <summary>
        /// Pairwise singleton conflict matrix for branch/system lane aliasing.
        /// Bit <c>(sourceSlot * 8 + targetSlot)</c> is set when both slots require the
        /// aliased singleton control lane.
        /// </summary>
        public ulong ControlConflictMask { get; }

        /// <summary>
        /// Pairwise coarse memory-ordering conflict matrix.
        /// Bit <c>(sourceSlot * 8 + targetSlot)</c> is set when two bundle slots are both
        /// memory operations and decode-side facts indicate they should stay ordered.
        /// </summary>
        public ulong MemoryConflictMask { get; }

        /// <summary>
        /// Mask of slots that are obvious scalar-cluster candidates from decode facts alone.
        /// This is a preparatory hint for later wide scalar admission, not a final policy decision.
        /// </summary>
        public byte ScalarClusterEligibleMask { get; }

        /// <summary>
        /// Directed per-slot hazard query: for a given slot within a scalar group,
        /// returns which peer slots are HardReject and which NeedsRuntimeCheck.
        /// Projects from the existing 8x8 dependency matrices using slot class context.
        /// </summary>
        /// <param name="slotIndex">The slot to query (0..7).</param>
        /// <param name="scalarGroupMask">Mask of slots in the scalar cluster group being evaluated.</param>
        public SlotHazardQueryResult QuerySlotHazards(byte slotIndex, byte scalarGroupMask)
        {
            if (slotIndex >= 8)
                return default;

            byte hardRejectPeers = 0;
            byte needsCheckPeers = 0;
            HazardEffectKind dominantEffectKind = HazardEffectKind.RegisterData;
            bool dominantEffectAssigned = false;

            for (int peer = 0; peer < 8; peer++)
            {
                if (peer == slotIndex)
                    continue;

                byte peerBit = (byte)(1 << peer);
                HazardTriageClass triage = QueryPairHazard(
                    slotIndex,
                    (byte)peer,
                    scalarGroupMask,
                    out HazardEffectKind effectKind);

                switch (triage)
                {
                    case HazardTriageClass.HardReject:
                        hardRejectPeers |= peerBit;
                        break;
                    case HazardTriageClass.NeedsRuntimeCheck:
                        needsCheckPeers |= peerBit;
                        break;
                }

                if (!dominantEffectAssigned && triage != HazardTriageClass.Safe)
                {
                    dominantEffectKind = effectKind;
                    dominantEffectAssigned = true;
                }
            }

            return new SlotHazardQueryResult(hardRejectPeers, needsCheckPeers, dominantEffectKind);
        }

        internal HazardTriageClass QueryPairHazard(
            byte slotIndex,
            byte peerIndex,
            byte scalarGroupMask,
            out HazardEffectKind dominantEffectKind)
        {
            dominantEffectKind = HazardEffectKind.RegisterData;

            if (slotIndex >= 8 || peerIndex >= 8 || slotIndex == peerIndex)
                return HazardTriageClass.Safe;

            byte slotBit = (byte)(1 << slotIndex);
            byte peerBit = (byte)(1 << peerIndex);
            bool slotIsScalar = (scalarGroupMask & slotBit) != 0;
            bool peerIsScalar = (scalarGroupMask & peerBit) != 0;

            ulong forwardBit = 1UL << (slotIndex * 8 + peerIndex);
            ulong reverseBit = 1UL << (peerIndex * 8 + slotIndex);
            ulong pairBits = forwardBit | reverseBit;

            bool hasRaw = (RawDependencyMask & pairBits) != 0;
            bool hasWaw = (WawDependencyMask & pairBits) != 0;
            bool hasWar = (WarDependencyMask & pairBits) != 0;
            bool hasSystemBarrier = (_systemBarrierConflictMask & pairBits) != 0;
            bool hasControlFlow = (ControlConflictMask & pairBits) != 0 && !hasSystemBarrier;
            bool hasMemory = (MemoryConflictMask & pairBits) != 0;
            bool hasPinnedLane = (_pinnedLaneConflictMask & pairBits) != 0;

            return ClassifyPairHazard(
                slotIsScalar,
                peerIsScalar,
                hasRaw,
                hasWaw,
                hasWar,
                hasMemory,
                hasControlFlow,
                hasSystemBarrier,
                hasPinnedLane,
                out dominantEffectKind);
        }

        /// <summary>
        /// Compute a refined scalar-cluster readiness mask that blocks a slot only when it has
        /// a <see cref="HazardTriageClass.HardReject"/> peer within the scalar group.
        /// This replaces the overly conservative flat blocking used by <see cref="DecodedBundleAdmissionPrep.WideReadyScalarMask"/>.
        /// </summary>
        /// <param name="scalarCandidateMask">Scalar-eligible slots from decode facts.</param>
        public byte ComputeRefinedWideReadyScalarMask(byte scalarCandidateMask)
        {
            byte refinedMask = 0;

            for (int slotIndex = 0; slotIndex < 8; slotIndex++)
            {
                byte slotBit = (byte)(1 << slotIndex);
                if ((scalarCandidateMask & slotBit) == 0)
                    continue;

                SlotHazardQueryResult query = QuerySlotHazards((byte)slotIndex, scalarCandidateMask);

                // Slot is wide-ready only if no HardReject peer exists within the scalar group
                byte scalarHardReject = (byte)(query.HardRejectPeers & scalarCandidateMask);
                if (scalarHardReject == 0)
                {
                    refinedMask |= slotBit;
                }
            }

            return refinedMask;
        }

        /// <summary>
        /// Classify the hazard severity for a dependency pair based on decode-structural facts.
        /// Per Phase 03 В§4.1 triage table.
        /// </summary>
        private static HazardTriageClass ClassifyPairHazard(
            bool slotIsScalar, bool peerIsScalar,
            bool hasRaw, bool hasWaw, bool hasWar,
            bool hasMemory,
            bool hasControlFlow,
            bool hasSystemBarrier,
            bool hasPinnedLane,
            out HazardEffectKind dominantEffectKind)
        {
            dominantEffectKind = HazardEffectKind.RegisterData;
            bool bothScalar = slotIsScalar && peerIsScalar;

            if (hasRaw || hasWaw || hasWar)
            {
                dominantEffectKind = HazardEffectKind.RegisterData;

                if (hasRaw || hasWaw)
                {
                    if (bothScalar)
                        return HazardTriageClass.HardReject;

                    if (slotIsScalar || peerIsScalar)
                        return HazardTriageClass.NeedsRuntimeCheck;

                    return HazardTriageClass.HardReject;
                }

                return HazardTriageClass.NeedsRuntimeCheck;
            }

            if (hasMemory)
            {
                dominantEffectKind = HazardEffectKind.MemoryBank;
                return HazardTriageClass.HardReject;
            }

            if (hasControlFlow)
            {
                dominantEffectKind = HazardEffectKind.ControlFlow;
                return HazardTriageClass.HardReject;
            }

            if (hasSystemBarrier)
            {
                dominantEffectKind = HazardEffectKind.SystemBarrier;
                return HazardTriageClass.HardReject;
            }

            if (hasPinnedLane)
            {
                dominantEffectKind = HazardEffectKind.PinnedLane;
                return HazardTriageClass.NeedsRuntimeCheck;
            }

            return HazardTriageClass.Safe;
        }
    }

    /// <summary>
    /// Result of a directed per-slot hazard query within the scalar cluster context.
    /// Contains peer masks classified by hazard severity.
    /// </summary>
    public readonly struct SlotHazardQueryResult
    {
        public SlotHazardQueryResult(
            byte hardRejectPeers,
            byte needsCheckPeers,
            HazardEffectKind dominantEffectKind = HazardEffectKind.RegisterData)
        {
            HardRejectPeers = hardRejectPeers;
            NeedsCheckPeers = needsCheckPeers;
            DominantEffectKind = dominantEffectKind;
        }

        /// <summary>
        /// Mask of peer slots with HardReject severity вЂ” parallel issue is impossible.
        /// </summary>
        public byte HardRejectPeers { get; }

        /// <summary>
        /// Mask of peer slots with NeedsRuntimeCheck severity вЂ” runtime may resolve the conflict.
        /// </summary>
        public byte NeedsCheckPeers { get; }

        /// <summary>
        /// Dominant hazard effect kind observed for this query.
        /// Defaults to <see cref="HazardEffectKind.RegisterData"/> when no hazard was classified.
        /// </summary>
        public HazardEffectKind DominantEffectKind { get; }

        /// <summary>
        /// True when the queried slot has no HardReject peers and can potentially proceed to wide path.
        /// </summary>
        public bool IsWideCandidate => HardRejectPeers == 0;
    }

    [Flags]
    public enum DecodedBundleAdmissionFlags : ushort
    {
        None = 0,
        HasScalarClusterCandidates = 1 << 0,
        HasWideReadyScalarCandidates = 1 << 1,
        HasBundleLocalDependencies = 1 << 2,
        HasControlConflicts = 1 << 3,
        HasMemoryOrderingConstraints = 1 << 4,
        HasAuxiliaryClusterOps = 1 << 5,
        SuggestNarrowFallback = 1 << 6
    }

    public readonly struct DecodedBundleAdmissionPrep
    {
        public DecodedBundleAdmissionPrep(
            byte scalarCandidateMask,
            byte wideReadyScalarMask,
            byte auxiliaryOpMask,
            byte narrowOnlyMask,
            DecodedBundleAdmissionFlags flags)
        {
            ScalarCandidateMask = scalarCandidateMask;
            WideReadyScalarMask = wideReadyScalarMask;
            AuxiliaryOpMask = auxiliaryOpMask;
            NarrowOnlyMask = narrowOnlyMask;
            Flags = flags;
        }

        /// <summary>
        /// Scalar operations that can target the 4-way scalar cluster from decode facts alone.
        /// </summary>
        public byte ScalarCandidateMask { get; }

        /// <summary>
        /// Scalar candidates that currently have no bundle-local blockers in the decode-side legality substrate.
        /// </summary>
        public byte WideReadyScalarMask { get; }

        /// <summary>
        /// Non-empty operations that belong to auxiliary paths (LSU, DMA, vector, branch/system)
        /// rather than the scalar-cluster target.
        /// </summary>
        public byte AuxiliaryOpMask { get; }

        /// <summary>
        /// Non-empty operations that currently require the narrow/reference path.
        /// Includes auxiliary operations and scalar candidates blocked by bundle-local hazards.
        /// </summary>
        public byte NarrowOnlyMask { get; }

        /// <summary>
        /// Advisory decode-side flags for narrow-vs-wide readiness.
        /// </summary>
        public DecodedBundleAdmissionFlags Flags { get; }

        public int ScalarCandidateCount => BitOperations.PopCount((uint)ScalarCandidateMask);
        public int WideReadyScalarCount => BitOperations.PopCount((uint)WideReadyScalarMask);
        public bool SuggestNarrowFallback => (Flags & DecodedBundleAdmissionFlags.SuggestNarrowFallback) != 0;
    }

    internal readonly struct DecodedBundleTransportFacts
    {
        internal DecodedBundleTransportFacts(
            ulong pc,
            DecodedBundleSlotDescriptor[] slots,
            byte validMask,
            byte nopMask,
            DecodedBundleDependencySummary? dependencySummary,
            DecodedBundleAdmissionPrep admissionPrep,
            DecodedBundleStateKind stateKind,
            DecodedBundleStateOrigin stateOrigin)
        {
            ArgumentNullException.ThrowIfNull(slots);

            PC = pc;
            Slots = slots;
            ValidMask = validMask;
            NopMask = nopMask;
            DependencySummary = dependencySummary;
            AdmissionPrep = admissionPrep;
            StateKind = stateKind;
            StateOrigin = stateOrigin;
        }

        public ulong PC { get; }
        public DecodedBundleSlotDescriptor[] Slots { get; }
        public byte ValidMask { get; }
        public byte NopMask { get; }
        public byte ValidNonEmptyMask => (byte)(ValidMask & ~NopMask);
        public DecodedBundleDependencySummary? DependencySummary { get; }
        public DecodedBundleAdmissionPrep AdmissionPrep { get; }
        public DecodedBundleStateKind StateKind { get; }
        public DecodedBundleStateOrigin StateOrigin { get; }
    }

    internal readonly struct BundleProgressState
    {
        internal const byte TerminalSlotIndex = 8;

        internal BundleProgressState(
            ulong bundlePc,
            byte remainingMask,
            byte nextSlotIndex)
        {
            BundlePc = bundlePc;
            RemainingMask = remainingMask;
            NextSlotIndex = nextSlotIndex;
        }

        public ulong BundlePc { get; }
        public byte RemainingMask { get; }
        public byte NextSlotIndex { get; }
        public bool HasRemaining => RemainingMask != 0 && NextSlotIndex < TerminalSlotIndex;

        internal bool Contains(byte slotIndex)
        {
            return slotIndex < TerminalSlotIndex &&
                (RemainingMask & (1 << slotIndex)) != 0;
        }

        internal static BundleProgressState CreateEmpty(ulong bundlePc = 0)
        {
            return new BundleProgressState(
                bundlePc,
                remainingMask: 0,
                nextSlotIndex: TerminalSlotIndex);
        }

        internal static BundleProgressState CreateForCursor(
            ulong bundlePc,
            byte remainingMask,
            byte currentSlotIndex)
        {
            byte nextSlotIndex = ResolveNextSlotIndexForCursor(
                remainingMask,
                currentSlotIndex);
            return new BundleProgressState(
                bundlePc,
                remainingMask,
                nextSlotIndex);
        }

        internal BundleProgressState ConsumeSlot(byte slotIndex)
        {
            if (slotIndex >= TerminalSlotIndex)
                return this;

            byte remainingMask = (byte)(RemainingMask & ~(1 << slotIndex));
            return CreateForCursor(
                BundlePc,
                remainingMask,
                slotIndex);
        }

        internal BundleProgressState ConsumeMask(
            byte consumedSlotMask,
            byte currentSlotIndex)
        {
            byte remainingMask = (byte)(RemainingMask & ~consumedSlotMask);
            return CreateForCursor(
                BundlePc,
                remainingMask,
                currentSlotIndex);
        }

        private static byte ResolveNextSlotIndexForCursor(
            byte remainingMask,
            byte currentSlotIndex)
        {
            if (remainingMask == 0)
                return TerminalSlotIndex;

            if (currentSlotIndex < TerminalSlotIndex &&
                (remainingMask & (1 << currentSlotIndex)) != 0)
            {
                return currentSlotIndex;
            }

            if (currentSlotIndex < TerminalSlotIndex)
            {
                for (byte slotIndex = (byte)(currentSlotIndex + 1);
                    slotIndex < TerminalSlotIndex;
                    slotIndex++)
                {
                    if ((remainingMask & (1 << slotIndex)) != 0)
                        return slotIndex;
                }
            }

            for (byte slotIndex = 0; slotIndex < TerminalSlotIndex; slotIndex++)
            {
                if ((remainingMask & (1 << slotIndex)) != 0)
                    return slotIndex;
            }

            return TerminalSlotIndex;
        }
    }

    internal enum DecodedBundleCanonicalValidity : byte
    {
        None = 0,
        Canonical = 1,
        DecodeFault = 2
    }

    [Flags]
    internal enum DecodedBundleTransformKind : ushort
    {
        None = 0,
        Reset = 1 << 0,
        CanonicalDecode = 1 << 1,
        DecodeFallbackTrap = 1 << 2,
        ReplayBundleLoad = 1 << 3,
        ForegroundBundlePublication = 1 << 4,
        SingleSlotMutation = 1 << 6,
        ClearMaskMutation = 1 << 7,
        FspPacking = 1 << 8
    }

    internal readonly struct DecodedBundleTransformHistory
    {
        internal DecodedBundleTransformHistory(
            DecodedBundleTransformKind appliedTransforms,
            byte mutationDepth,
            DecodedBundleStateOrigin latestOrigin)
        {
            AppliedTransforms = appliedTransforms;
            MutationDepth = mutationDepth;
            LatestOrigin = latestOrigin;
        }

        public DecodedBundleTransformKind AppliedTransforms { get; }
        public byte MutationDepth { get; }
        public DecodedBundleStateOrigin LatestOrigin { get; }

        internal bool Contains(DecodedBundleTransformKind transformKind)
        {
            return (AppliedTransforms & transformKind) == transformKind;
        }

        internal static DecodedBundleTransformHistory CreateBase(
            DecodedBundleStateOrigin origin)
        {
            return new DecodedBundleTransformHistory(
                ResolveTransformKind(origin),
                CountsAsMutation(origin) ? (byte)1 : (byte)0,
                origin);
        }

        internal DecodedBundleTransformHistory Append(
            DecodedBundleStateOrigin origin)
        {
            byte mutationDepth = MutationDepth;
            if (CountsAsMutation(origin) && mutationDepth < byte.MaxValue)
            {
                mutationDepth++;
            }

            return new DecodedBundleTransformHistory(
                AppliedTransforms | ResolveTransformKind(origin),
                mutationDepth,
                origin);
        }

        private static bool CountsAsMutation(
            DecodedBundleStateOrigin origin)
        {
            return origin == DecodedBundleStateOrigin.ReplayBundleLoad ||
                origin == DecodedBundleStateOrigin.ForegroundBundlePublication ||
                origin == DecodedBundleStateOrigin.SingleSlotMutation ||
                origin == DecodedBundleStateOrigin.ClearMaskMutation ||
                origin == DecodedBundleStateOrigin.FspPacking;
        }

        private static DecodedBundleTransformKind ResolveTransformKind(
            DecodedBundleStateOrigin origin)
        {
            return origin switch
            {
                DecodedBundleStateOrigin.None => DecodedBundleTransformKind.None,
                DecodedBundleStateOrigin.Reset => DecodedBundleTransformKind.Reset,
                DecodedBundleStateOrigin.CanonicalDecode => DecodedBundleTransformKind.CanonicalDecode,
                DecodedBundleStateOrigin.DecodeFallbackTrap => DecodedBundleTransformKind.DecodeFallbackTrap,
                DecodedBundleStateOrigin.ReplayBundleLoad => DecodedBundleTransformKind.ReplayBundleLoad,
                DecodedBundleStateOrigin.ForegroundBundlePublication => DecodedBundleTransformKind.ForegroundBundlePublication,
                DecodedBundleStateOrigin.SingleSlotMutation => DecodedBundleTransformKind.SingleSlotMutation,
                DecodedBundleStateOrigin.ClearMaskMutation => DecodedBundleTransformKind.ClearMaskMutation,
                DecodedBundleStateOrigin.FspPacking => DecodedBundleTransformKind.FspPacking,
                _ => DecodedBundleTransformKind.None
            };
        }
    }

    internal readonly struct DecodedBundleRuntimeState
    {
        internal DecodedBundleRuntimeState(
            DecodedBundleTransportFacts transportFacts,
            Decoder.DecodedInstructionBundle canonicalDecode,
            Legality.BundleLegalityDescriptor legalityDescriptor,
            DecodedBundleTransformHistory transformHistory,
            DecodedBundleStateOwnerKind stateOwnerKind = DecodedBundleStateOwnerKind.None,
            ulong stateEpoch = 0,
            ulong stateVersion = 0,
            ulong lineageStateVersion = 0)
        {
            ArgumentNullException.ThrowIfNull(canonicalDecode);
            ArgumentNullException.ThrowIfNull(legalityDescriptor);

            ValidateBundleIdentity(
                transportFacts.PC,
                canonicalDecode,
                legalityDescriptor);

            TransportFacts = transportFacts;
            CanonicalDecode = canonicalDecode;
            LegalityDescriptor = legalityDescriptor;
            CanonicalValidity = ResolveCanonicalValidity(
                canonicalDecode,
                legalityDescriptor);
            TransformHistory = transformHistory;
            ValidatePublicationIdentity(
                stateOwnerKind,
                stateEpoch,
                stateVersion,
                lineageStateVersion);
            StateOwnerKind = stateOwnerKind;
            StateEpoch = stateEpoch;
            StateVersion = stateVersion;
            LineageStateVersion = lineageStateVersion;
        }

        public DecodedBundleTransportFacts TransportFacts { get; }
        public Decoder.DecodedInstructionBundle CanonicalDecode { get; }
        public Legality.BundleLegalityDescriptor LegalityDescriptor { get; }
        public DecodedBundleCanonicalValidity CanonicalValidity { get; }
        public DecodedBundleTransformHistory TransformHistory { get; }
        public DecodedBundleStateOwnerKind StateOwnerKind { get; }
        public ulong StateEpoch { get; }
        public ulong StateVersion { get; }
        public ulong LineageStateVersion { get; }
        public ulong BundlePc => TransportFacts.PC;
        public ulong BundleSerial => CanonicalDecode.BundleSerial;
        public DecodedBundleStateKind StateKind => TransportFacts.StateKind;
        public DecodedBundleStateOrigin StateOrigin => TransportFacts.StateOrigin;
        public bool HasCanonicalDecode => CanonicalValidity == DecodedBundleCanonicalValidity.Canonical;
        public bool HasCanonicalLegality => CanonicalValidity == DecodedBundleCanonicalValidity.Canonical;
        public bool HasDecodeFault =>
            StateKind == DecodedBundleStateKind.DecodeFault ||
            CanonicalValidity == DecodedBundleCanonicalValidity.DecodeFault;

        internal bool CanPreserveCanonicalForPc(ulong pc)
        {
            return CanonicalValidity == DecodedBundleCanonicalValidity.Canonical &&
                CanonicalDecode.BundleAddress == pc &&
                LegalityDescriptor.BundleAddress == pc;
        }

        internal DecodedBundleRuntimeState RepublishTransport(
            in DecodedBundleTransportFacts transportFacts)
        {
            if (CanPreserveCanonicalForPc(transportFacts.PC))
            {
                DecodedBundleTransformHistory history = TransformHistory;
                if (transportFacts.StateKind != DecodedBundleStateKind.Canonical ||
                    transportFacts.StateOrigin != DecodedBundleStateOrigin.CanonicalDecode)
                {
                    history = history.Append(transportFacts.StateOrigin);
                }

                return new DecodedBundleRuntimeState(
                    transportFacts,
                    CanonicalDecode,
                    LegalityDescriptor,
                    history,
                    StateOwnerKind,
                    StateEpoch,
                    StateVersion,
                    LineageStateVersion);
            }

            return CreateTransportOnly(
                transportFacts,
                bundleSerial: 0);
        }

        internal DecodedBundleRuntimeState ApplyFspPacking(
            in DecodedBundleTransportFacts transportFacts)
        {
            ValidateExpectedOrigin(
                transportFacts,
                DecodedBundleStateOrigin.FspPacking);
            return ApplyTransform(
                transportFacts,
                DecodedBundleStateOrigin.FspPacking);
        }

        internal DecodedBundleRuntimeState ApplySingleSlotMutation(
            in DecodedBundleTransportFacts transportFacts)
        {
            ValidateExpectedOrigin(
                transportFacts,
                DecodedBundleStateOrigin.SingleSlotMutation);
            return ApplyTransform(
                transportFacts,
                DecodedBundleStateOrigin.SingleSlotMutation);
        }

        internal DecodedBundleRuntimeState ApplyProgressProjection(
            in DecodedBundleTransportFacts transportFacts)
        {
            if (TransportFacts.ValidMask == transportFacts.ValidMask &&
                TransportFacts.NopMask == transportFacts.NopMask &&
                TransportFacts.StateKind == transportFacts.StateKind &&
                TransportFacts.StateOrigin == transportFacts.StateOrigin &&
                ReferenceEquals(TransportFacts.Slots, transportFacts.Slots))
            {
                return this;
            }

            return ApplyTransform(
                transportFacts,
                DecodedBundleStateOrigin.ClearMaskMutation);
        }

        internal DecodedBundleRuntimeState WithPublicationIdentity(
            DecodedBundleStateOwnerKind stateOwnerKind,
            ulong stateEpoch,
            ulong stateVersion,
            ulong lineageStateVersion)
        {
            if (StateOwnerKind == stateOwnerKind &&
                StateEpoch == stateEpoch &&
                StateVersion == stateVersion &&
                LineageStateVersion == lineageStateVersion)
            {
                return this;
            }

            return new DecodedBundleRuntimeState(
                TransportFacts,
                CanonicalDecode,
                LegalityDescriptor,
                TransformHistory,
                stateOwnerKind,
                stateEpoch,
                stateVersion,
                lineageStateVersion);
        }

        internal static DecodedBundleRuntimeState CreateCanonical(
            Decoder.DecodedInstructionBundle canonicalDecode,
            Legality.BundleLegalityDescriptor legalityDescriptor,
            in DecodedBundleTransportFacts transportFacts)
        {
            return new DecodedBundleRuntimeState(
                transportFacts,
                canonicalDecode,
                legalityDescriptor,
                DecodedBundleTransformHistory.CreateBase(
                    DecodedBundleStateOrigin.CanonicalDecode));
        }

        internal static DecodedBundleRuntimeState CreateFallback(
            ulong pc,
            in DecodedBundleTransportFacts transportFacts,
            ulong bundleSerial = 0)
        {
            if (transportFacts.PC != pc)
            {
                throw new ArgumentException(
                    $"Fallback transport PC 0x{transportFacts.PC:X} must match requested bundle PC 0x{pc:X}.",
                    nameof(transportFacts));
            }

            return new DecodedBundleRuntimeState(
                transportFacts,
                Decoder.DecodedInstructionBundle.CreateDecodeFault(pc, bundleSerial),
                Legality.BundleLegalityDescriptor.CreateDecodeFault(pc, bundleSerial),
                DecodedBundleTransformHistory.CreateBase(
                    DecodedBundleStateOrigin.DecodeFallbackTrap));
        }

        internal static DecodedBundleRuntimeState CreateReplay(
            in DecodedBundleTransportFacts transportFacts,
            ulong bundleSerial = 0)
        {
            ValidateExpectedOrigin(
                transportFacts,
                DecodedBundleStateOrigin.ReplayBundleLoad);
            return CreateTransportOnly(
                transportFacts,
                bundleSerial,
                DecodedBundleTransformHistory.CreateBase(
                    DecodedBundleStateOrigin.ReplayBundleLoad));
        }

        internal static DecodedBundleRuntimeState CreateTransportOnly(
            in DecodedBundleTransportFacts transportFacts,
            ulong bundleSerial = 0,
            DecodedBundleTransformHistory? transformHistory = null)
        {
            return new DecodedBundleRuntimeState(
                transportFacts,
                Decoder.DecodedInstructionBundle.CreateEmpty(
                    transportFacts.PC,
                    bundleSerial),
                Legality.BundleLegalityDescriptor.CreateEmpty(
                    transportFacts.PC,
                    bundleSerial),
                transformHistory ?? DecodedBundleTransformHistory.CreateBase(
                    transportFacts.StateOrigin));
        }

        internal static DecodedBundleRuntimeState CreateEmpty(
            ulong pc,
            ulong bundleSerial = 0)
        {
            DecodedBundleTransportFacts transportFacts =
                DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                    pc,
                    Array.Empty<MicroOp?>(),
                    DecodedBundleStateKind.Empty,
                    DecodedBundleStateOrigin.Reset);

            return CreateTransportOnly(
                transportFacts,
                bundleSerial);
        }

        private static DecodedBundleCanonicalValidity ResolveCanonicalValidity(
            Decoder.DecodedInstructionBundle canonicalDecode,
            Legality.BundleLegalityDescriptor legalityDescriptor)
        {
            bool hasDecodeFault =
                canonicalDecode.HasDecodeFault ||
                legalityDescriptor.HasDecodeFault;
            if (hasDecodeFault)
            {
                return DecodedBundleCanonicalValidity.DecodeFault;
            }

            return canonicalDecode.IsEmpty
                ? DecodedBundleCanonicalValidity.None
                : DecodedBundleCanonicalValidity.Canonical;
        }

        private static void ValidateBundleIdentity(
            ulong transportPc,
            Decoder.DecodedInstructionBundle canonicalDecode,
            Legality.BundleLegalityDescriptor legalityDescriptor)
        {
            if (canonicalDecode.BundleAddress != legalityDescriptor.BundleAddress)
            {
                throw new ArgumentException(
                    $"Canonical decode PC 0x{canonicalDecode.BundleAddress:X} must match legality PC 0x{legalityDescriptor.BundleAddress:X}.");
            }

            if (canonicalDecode.BundleSerial != legalityDescriptor.BundleSerial)
            {
                throw new ArgumentException(
                    $"Canonical decode serial {canonicalDecode.BundleSerial} must match legality serial {legalityDescriptor.BundleSerial}.");
            }

            if (canonicalDecode.BundleAddress != transportPc)
            {
                throw new ArgumentException(
                    $"Transport PC 0x{transportPc:X} must match canonical decode PC 0x{canonicalDecode.BundleAddress:X}.");
            }

            bool canonicalHasPayload = !canonicalDecode.IsEmpty;
            bool legalityHasPayload = !legalityDescriptor.IsEmpty;
            if (canonicalHasPayload != legalityHasPayload)
            {
                throw new ArgumentException(
                    "Canonical decode and legality descriptors must either both carry payload or both be empty.");
            }

            if (canonicalDecode.HasDecodeFault != legalityDescriptor.HasDecodeFault)
            {
                throw new ArgumentException(
                    "Canonical decode and legality descriptors must agree on decode-fault publication.");
            }
        }

        private DecodedBundleRuntimeState ApplyTransform(
            in DecodedBundleTransportFacts transportFacts,
            DecodedBundleStateOrigin transformOrigin)
        {
            if (transportFacts.PC != BundlePc)
            {
                throw new ArgumentException(
                    $"Transport PC 0x{transportFacts.PC:X} must stay aligned with runtime-state PC 0x{BundlePc:X}.",
                    nameof(transportFacts));
            }

            return new DecodedBundleRuntimeState(
                transportFacts,
                CanonicalDecode,
                LegalityDescriptor,
                TransformHistory.Append(transformOrigin),
                StateOwnerKind,
                StateEpoch,
                StateVersion,
                LineageStateVersion);
        }

        private static void ValidatePublicationIdentity(
            DecodedBundleStateOwnerKind stateOwnerKind,
            ulong stateEpoch,
            ulong stateVersion,
            ulong lineageStateVersion)
        {
            if (stateOwnerKind == DecodedBundleStateOwnerKind.None)
            {
                if (stateEpoch != 0 || stateVersion != 0 || lineageStateVersion != 0)
                {
                    throw new ArgumentException(
                        "Unpublished decoded-bundle runtime state must not carry owner/epoch/version identity.");
                }

                return;
            }

            if (stateEpoch == 0)
            {
                throw new ArgumentException(
                    "Published decoded-bundle runtime state must carry a non-zero StateEpoch.");
            }

            if (stateVersion == 0)
            {
                throw new ArgumentException(
                    "Published decoded-bundle runtime state must carry a non-zero StateVersion.");
            }

            if (lineageStateVersion == 0)
            {
                throw new ArgumentException(
                    "Published decoded-bundle runtime state must carry a non-zero lineage state version.");
            }
        }

        private static void ValidateExpectedOrigin(
            in DecodedBundleTransportFacts transportFacts,
            DecodedBundleStateOrigin expectedOrigin)
        {
            if (transportFacts.StateOrigin != expectedOrigin)
            {
                throw new ArgumentException(
                    $"Transport origin {transportFacts.StateOrigin} must match expected runtime transform {expectedOrigin}.",
                    nameof(transportFacts));
            }
        }
    }

    internal readonly struct DecodedBundleDerivedIssuePlanState
    {
        internal DecodedBundleDerivedIssuePlanState(
            bool isActive,
            DecodedBundleRuntimeState runtimeState)
        {
            IsActive = isActive;
            RuntimeState = runtimeState;
        }

        public bool IsActive { get; }
        public DecodedBundleRuntimeState RuntimeState { get; }
        public DecodedBundleTransportFacts TransportFacts => RuntimeState.TransportFacts;
        public ulong BundlePc => RuntimeState.BundlePc;
        public ulong BundleSerial => RuntimeState.BundleSerial;
        public ulong StateEpoch => RuntimeState.StateEpoch;
        public ulong StateVersion => RuntimeState.StateVersion;
        public ulong LineageStateVersion => RuntimeState.LineageStateVersion;

        internal bool MatchesBundlePc(ulong pc)
        {
            return IsActive && RuntimeState.BundlePc == pc;
        }

        internal void ValidateAgainstBaseRuntimeState(
            in DecodedBundleRuntimeState baseRuntimeState,
            string caller)
        {
            if (!IsActive)
            {
                return;
            }

            if (RuntimeState.StateOwnerKind != DecodedBundleStateOwnerKind.DerivedIssuePlanPublication)
            {
                throw new InvalidOperationException(
                    $"{caller} observed an active derived issue-plan state without DerivedIssuePlanPublication ownership.");
            }

            if (RuntimeState.BundlePc != baseRuntimeState.BundlePc)
            {
                throw new InvalidOperationException(
                    $"{caller} observed derived bundle PC 0x{RuntimeState.BundlePc:X} that diverges from base bundle PC 0x{baseRuntimeState.BundlePc:X}.");
            }

            if (RuntimeState.BundleSerial != baseRuntimeState.BundleSerial)
            {
                throw new InvalidOperationException(
                    $"{caller} observed derived bundle serial {RuntimeState.BundleSerial} that diverges from base bundle serial {baseRuntimeState.BundleSerial}.");
            }

            if (RuntimeState.StateEpoch != baseRuntimeState.StateEpoch)
            {
                throw new InvalidOperationException(
                    $"{caller} observed derived state epoch {RuntimeState.StateEpoch} that diverges from base state epoch {baseRuntimeState.StateEpoch}.");
            }

            if (RuntimeState.LineageStateVersion != baseRuntimeState.StateVersion)
            {
                throw new InvalidOperationException(
                    $"{caller} observed derived lineage state version {RuntimeState.LineageStateVersion} that does not match base state version {baseRuntimeState.StateVersion}.");
            }
        }

        internal static DecodedBundleDerivedIssuePlanState CreateEmpty(ulong bundlePc = 0)
        {
            DecodedBundleTransportFacts transportFacts =
                DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                    bundlePc,
                    Array.Empty<MicroOp?>(),
                    DecodedBundleStateKind.Empty,
                    DecodedBundleStateOrigin.None);
            return new DecodedBundleDerivedIssuePlanState(
                isActive: false,
                runtimeState: DecodedBundleRuntimeState.CreateTransportOnly(
                    transportFacts));
        }

        internal static DecodedBundleDerivedIssuePlanState CreateActive(
            in DecodedBundleRuntimeState runtimeState)
        {
            if (runtimeState.StateOwnerKind != DecodedBundleStateOwnerKind.DerivedIssuePlanPublication)
            {
                throw new ArgumentException(
                    "Derived issue-plan publication must stamp runtime state ownership before activation.",
                    nameof(runtimeState));
            }

            return new DecodedBundleDerivedIssuePlanState(
                isActive: true,
                runtimeState: runtimeState);
        }
    }

    internal static class DecodedBundleSlotCarrierBuilder
    {
        private const int BundleWidth = 8;

        internal static DecodedBundleTransportFacts BuildTransportFacts(
            ulong pc,
            IReadOnlyList<MicroOp?> carrierBundle,
            DecodedBundleStateKind stateKind = DecodedBundleStateKind.ForegroundMutated,
            DecodedBundleStateOrigin stateOrigin = DecodedBundleStateOrigin.ForegroundBundlePublication)
        {
            ArgumentNullException.ThrowIfNull(carrierBundle);

            ResolveTransportFacts(
                carrierBundle,
                out DecodedBundleSlotDescriptor[] slots,
                out byte validMask,
                out byte nopMask,
                out DecodedBundleDependencySummary? dependencySummary,
                out DecodedBundleAdmissionPrep admissionPrep);

            return new DecodedBundleTransportFacts(
                pc,
                slots,
                validMask,
                nopMask,
                dependencySummary,
                admissionPrep,
                stateKind,
                stateOrigin);
        }

        internal static DecodedBundleTransportFacts BuildTransportFacts(
            ulong pc,
            IReadOnlyList<MicroOp?> carrierBundle,
            DecodedBundleDependencySummary? dependencySummary,
            DecodedBundleStateKind stateKind = DecodedBundleStateKind.ForegroundMutated,
            DecodedBundleStateOrigin stateOrigin = DecodedBundleStateOrigin.ForegroundBundlePublication)
        {
            ArgumentNullException.ThrowIfNull(carrierBundle);

            ResolveCarrierTransportFactsShape(
                carrierBundle,
                out DecodedBundleSlotDescriptor[] slots,
                out byte validMask,
                out byte nopMask);

            DecodedBundleAdmissionPrep admissionPrep = dependencySummary.HasValue
                ? BuildAdmissionPrep(slots, validMask, nopMask, dependencySummary.Value)
                : default;

            return new DecodedBundleTransportFacts(
                pc,
                slots,
                validMask,
                nopMask,
                dependencySummary,
                admissionPrep,
                stateKind,
                stateOrigin);
        }

        internal static DecodedBundleTransportFacts BuildTransportFacts(
            ulong pc,
            IReadOnlyList<MicroOp?> carrierBundle,
            Decoder.DecodedInstructionBundle canonicalBundle,
            DecodedBundleDependencySummary? dependencySummary)
        {
            ArgumentNullException.ThrowIfNull(carrierBundle);
            ArgumentNullException.ThrowIfNull(canonicalBundle);

            ResolveCanonicalCarrierTransportFactsShape(
                carrierBundle,
                canonicalBundle,
                out DecodedBundleSlotDescriptor[] slots,
                out byte validMask,
                out byte nopMask);

            DecodedBundleAdmissionPrep admissionPrep = dependencySummary.HasValue
                ? BuildAdmissionPrep(slots, validMask, nopMask, dependencySummary.Value)
                : default;

            return new DecodedBundleTransportFacts(
                pc,
                slots,
                validMask,
                nopMask,
                dependencySummary,
                admissionPrep,
                DecodedBundleStateKind.Canonical,
                DecodedBundleStateOrigin.CanonicalDecode);
        }

        internal static DecodedBundleTransportFacts BuildTransportFactsFromSingleSlotMutationContour(
            ulong pc,
            IReadOnlyList<DecodedBundleSlotDescriptor> slotDescriptors,
            byte slotIndex,
            MicroOp? microOp)
        {
            ArgumentNullException.ThrowIfNull(slotDescriptors);

            ResolveTransportFactsShapeWithSingleSlotMutation(
                slotDescriptors,
                slotIndex,
                DecodedBundleSlotDescriptor.Create(slotIndex, microOp),
                out DecodedBundleSlotDescriptor[] slots,
                out byte validMask,
                out byte nopMask);

            ResolveTransportFactSummary(
                slots,
                validMask,
                nopMask,
                out DecodedBundleDependencySummary? dependencySummary,
                out DecodedBundleAdmissionPrep admissionPrep);

            return new DecodedBundleTransportFacts(
                pc,
                slots,
                validMask,
                nopMask,
                dependencySummary,
                admissionPrep,
                DecodedBundleStateKind.ForegroundMutated,
                DecodedBundleStateOrigin.SingleSlotMutation);
        }

        internal static DecodedBundleTransportFacts BuildTransportFactsFromClearMaskMutationContour(
            ulong pc,
            IReadOnlyList<DecodedBundleSlotDescriptor> slotDescriptors,
            byte clearSlotMask)
        {
            ArgumentNullException.ThrowIfNull(slotDescriptors);

            ResolveTransportFactsShapeWithClearMaskMutation(
                slotDescriptors,
                clearSlotMask,
                out DecodedBundleSlotDescriptor[] slots,
                out byte validMask,
                out byte nopMask);

            ResolveTransportFactSummary(
                slots,
                validMask,
                nopMask,
                out DecodedBundleDependencySummary? dependencySummary,
                out DecodedBundleAdmissionPrep admissionPrep);

            return new DecodedBundleTransportFacts(
                pc,
                slots,
                validMask,
                nopMask,
                dependencySummary,
                admissionPrep,
                DecodedBundleStateKind.ForegroundMutated,
                DecodedBundleStateOrigin.ClearMaskMutation);
        }

        internal static DecodedBundleTransportFacts BuildTransportFactsFromSlotDescriptorProjection(
            ulong pc,
            IReadOnlyList<DecodedBundleSlotDescriptor> slotDescriptors,
            DecodedBundleStateKind stateKind = DecodedBundleStateKind.ForegroundMutated,
            DecodedBundleStateOrigin stateOrigin = DecodedBundleStateOrigin.ForegroundBundlePublication)
        {
            ArgumentNullException.ThrowIfNull(slotDescriptors);

            ResolveTransportFacts(
                slotDescriptors,
                out DecodedBundleSlotDescriptor[] slots,
                out byte validMask,
                out byte nopMask,
                out DecodedBundleDependencySummary? dependencySummary,
                out DecodedBundleAdmissionPrep admissionPrep);

            return new DecodedBundleTransportFacts(
                pc,
                slots,
                validMask,
                nopMask,
                dependencySummary,
                admissionPrep,
                stateKind,
                stateOrigin);
        }

        internal static DecodedBundleTransportFacts BuildTransportFactsFromProgressProjection(
            in DecodedBundleTransportFacts baseTransportFacts,
            byte remainingMask)
        {
            byte effectiveRemainingMask =
                (byte)(remainingMask & baseTransportFacts.ValidNonEmptyMask);
            byte clearSlotMask =
                (byte)(baseTransportFacts.ValidNonEmptyMask & ~effectiveRemainingMask);

            if (clearSlotMask == 0)
                return baseTransportFacts;

            ResolveTransportFactsShapeWithClearMaskMutation(
                baseTransportFacts.Slots,
                clearSlotMask,
                out DecodedBundleSlotDescriptor[] slots,
                out byte validMask,
                out byte nopMask);

            ResolveTransportFactSummary(
                slots,
                validMask,
                nopMask,
                out DecodedBundleDependencySummary? dependencySummary,
                out DecodedBundleAdmissionPrep admissionPrep);

            return new DecodedBundleTransportFacts(
                baseTransportFacts.PC,
                slots,
                validMask,
                nopMask,
                dependencySummary,
                admissionPrep,
                baseTransportFacts.StateKind,
                baseTransportFacts.StateOrigin);
        }

        private static void ResolveTransportFacts(
            IReadOnlyList<MicroOp?> carrierBundle,
            out DecodedBundleSlotDescriptor[] slots,
            out byte validMask,
            out byte nopMask,
            out DecodedBundleDependencySummary? dependencySummary,
            out DecodedBundleAdmissionPrep admissionPrep)
        {
            ResolveCarrierTransportFactsShape(
                carrierBundle,
                out slots,
                out validMask,
                out nopMask);

            ResolveTransportFactSummary(
                slots,
                validMask,
                nopMask,
                out dependencySummary,
                out admissionPrep);
        }

        private static void ResolveTransportFacts(
            IReadOnlyList<DecodedBundleSlotDescriptor> slotDescriptors,
            out DecodedBundleSlotDescriptor[] slots,
            out byte validMask,
            out byte nopMask,
            out DecodedBundleDependencySummary? dependencySummary,
            out DecodedBundleAdmissionPrep admissionPrep)
        {
            ResolveTransportFactsShape(
                slotDescriptors,
                out slots,
                out validMask,
                out nopMask);

            ResolveTransportFactSummary(
                slots,
                validMask,
                nopMask,
                out dependencySummary,
                out admissionPrep);
        }

        private static void ResolveTransportFactSummary(
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            byte validMask,
            byte nopMask,
            out DecodedBundleDependencySummary? dependencySummary,
            out DecodedBundleAdmissionPrep admissionPrep)
        {
            ulong readRegisterMask = 0;
            ulong writeRegisterMask = 0;
            ResourceBitset aggregateResourceMask = ResourceBitset.Zero;

            for (byte slotIndex = 0; slotIndex < BundleWidth; slotIndex++)
            {
                DecodedBundleSlotDescriptor slot = slots[slotIndex];
                if (slot.IsEmptyOrNop)
                    continue;

                if (slot.ReadRegisters != null)
                {
                    for (int i = 0; i < slot.ReadRegisters.Count; i++)
                    {
                        int registerId = slot.ReadRegisters[i];
                        readRegisterMask |= EncodeDependencyRegisterMaskBit(registerId, slotIndex, "read");
                    }
                }

                if (slot.WriteRegisters != null)
                {
                    for (int i = 0; i < slot.WriteRegisters.Count; i++)
                    {
                        int registerId = slot.WriteRegisters[i];
                        writeRegisterMask |= EncodeDependencyRegisterMaskBit(registerId, slotIndex, "write");
                    }
                }

                aggregateResourceMask |= slot.MicroOp?.ResourceMask ?? ResourceBitset.Zero;
            }

            dependencySummary = validMask != 0
                ? Legality.BundleLegalityAnalyzer.BuildDependencySummary(
                    slots,
                    readRegisterMask,
                    writeRegisterMask,
                    aggregateResourceMask)
                : null;
            admissionPrep = dependencySummary.HasValue
                ? BuildAdmissionPrep(slots, validMask, nopMask, dependencySummary.Value)
                : default;
        }

        private static ulong EncodeDependencyRegisterMaskBit(
            int registerId,
            byte slotIndex,
            string accessKind)
        {
            if ((uint)registerId >= ArchRegId.DependencyMaskBitCount)
            {
                throw new InvalidOperationException(
                    $"DecodedBundleSlotCarrierBuilder.ResolveTransportFactSummary observed slot {slotIndex} " +
                    $"{accessKind} register id {registerId}, but dependency masks are limited to " +
                    $"architectural register ids in [0, {ArchRegId.DependencyMaskBitCount - 1}]. " +
                    "Migrate DecodedBundleDependencySummary to a wider bitset before publishing larger register ids.");
            }

            return 1UL << registerId;
        }

        private static void ResolveCarrierTransportFactsShape(
            IReadOnlyList<MicroOp?> carrierBundle,
            out DecodedBundleSlotDescriptor[] slots,
            out byte validMask,
            out byte nopMask)
        {
            slots = new DecodedBundleSlotDescriptor[BundleWidth];
            validMask = 0;
            nopMask = 0;

            for (byte slotIndex = 0; slotIndex < BundleWidth; slotIndex++)
            {
                MicroOp? microOp = slotIndex < carrierBundle.Count ? carrierBundle[slotIndex] : null;
                DecodedBundleSlotDescriptor slot = DecodedBundleSlotDescriptor.Create(slotIndex, microOp);
                slots[slotIndex] = slot;

                byte slotBit = (byte)(1 << slotIndex);
                if (slot.IsValid)
                    validMask |= slotBit;

                if (slot.IsEmptyOrNop)
                    nopMask |= slotBit;
            }
        }

        private static void ResolveCanonicalCarrierTransportFactsShape(
            IReadOnlyList<MicroOp?> carrierBundle,
            Decoder.DecodedInstructionBundle canonicalBundle,
            out DecodedBundleSlotDescriptor[] slots,
            out byte validMask,
            out byte nopMask)
        {
            slots = new DecodedBundleSlotDescriptor[BundleWidth];
            validMask = 0;
            nopMask = 0;

            for (byte slotIndex = 0; slotIndex < BundleWidth; slotIndex++)
            {
                MicroOp? microOp = slotIndex < carrierBundle.Count ? carrierBundle[slotIndex] : null;
                DecodedBundleSlotDescriptor slot = ResolveCanonicalCarrierTransportSlotDescriptor(
                    slotIndex,
                    microOp,
                    canonicalBundle);
                slots[slotIndex] = slot;

                byte slotBit = (byte)(1 << slotIndex);
                if (slot.IsValid)
                    validMask |= slotBit;

                if (slot.IsEmptyOrNop)
                    nopMask |= slotBit;
            }
        }

        private static DecodedBundleSlotDescriptor ResolveCanonicalCarrierTransportSlotDescriptor(
            byte slotIndex,
            MicroOp? microOp,
            Decoder.DecodedInstructionBundle canonicalBundle)
        {
            bool isVectorOp = ResolveCanonicalExecutionVectorClassification(
                slotIndex,
                microOp,
                canonicalBundle);

            if (microOp is TrapMicroOp trapMicroOp
                && slotIndex < canonicalBundle.SlotCount)
            {
                Decoder.DecodedInstruction decodedSlot = canonicalBundle.GetDecodedSlot(slotIndex);
                if (decodedSlot.IsOccupied)
                {
                    return BuildCanonicalTrapTransportSlotDescriptor(
                        slotIndex,
                        trapMicroOp,
                        decodedSlot,
                        isVectorOp);
                }
            }

            return DecodedBundleSlotDescriptor.Create(
                slotIndex,
                microOp,
                isVectorOpOverride: isVectorOp);
        }

        private static DecodedBundleSlotDescriptor BuildCanonicalTrapTransportSlotDescriptor(
            byte slotIndex,
            TrapMicroOp trapMicroOp,
            Decoder.DecodedInstruction decodedSlot,
            bool isVectorOp)
        {
            Pipeline.MicroOps.InstructionIR instruction = decodedSlot.RequireInstruction();
            bool writesRegister = Legality.BundleLegalityAnalyzer.MayWriteArchitecturalRegister(instruction);
            bool isMemoryOp = Legality.BundleLegalityAnalyzer.IsMemoryLikeClass(instruction.Class);
            bool isControlFlow = instruction.Class == Arch.InstructionClass.ControlFlow;
            int virtualThreadId = trapMicroOp.VirtualThreadId != 0
                ? trapMicroOp.VirtualThreadId
                : (int)decodedSlot.SlotMetadata.VirtualThreadId.Value;
            int ownerThreadId = trapMicroOp.OwnerThreadId != 0
                ? trapMicroOp.OwnerThreadId
                : virtualThreadId;
            SlotPlacementMetadata placement = Legality.BundleLegalityAnalyzer.BuildCanonicalPlacement(
                instruction.Class,
                domainTag: ResolveCanonicalTransportDomainTag(trapMicroOp, decodedSlot));

            return new DecodedBundleSlotDescriptor(
                microOp: trapMicroOp,
                slotIndex: slotIndex,
                virtualThreadId: virtualThreadId,
                ownerThreadId: ownerThreadId,
                opCode: (uint)instruction.CanonicalOpcode,
                readRegisters: Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction),
                writeRegisters: Legality.BundleLegalityAnalyzer.GetCanonicalWriteRegisters(instruction, writesRegister),
                writesRegister: writesRegister,
                isMemoryOp: isMemoryOp,
                isControlFlow: isControlFlow,
                placement: placement,
                memoryBankIntent: isMemoryOp
                    ? trapMicroOp.ProjectedMemoryBankIntent
                    : -1,
                isFspInjected: trapMicroOp.IsFspInjected,
                isEmptyOrNop: false,
                isVectorOp: isVectorOp);
        }

        private static ulong ResolveCanonicalTransportDomainTag(
            MicroOp microOp,
            Decoder.DecodedInstruction decodedSlot)
        {
            ulong domainTag = microOp.AdmissionMetadata.DomainTag;
            if (domainTag != 0)
                return domainTag;

            domainTag = microOp.Placement.DomainTag;
            if (domainTag != 0)
                return domainTag;

            MicroOpAdmissionMetadata canonicalAdmissionMetadata =
                decodedSlot.SlotMetadata.SlotMetadata.AdmissionMetadata;
            if (canonicalAdmissionMetadata.DomainTag != 0)
                return canonicalAdmissionMetadata.DomainTag;

            return canonicalAdmissionMetadata.Placement.DomainTag;
        }

        private static bool ResolveCanonicalExecutionVectorClassification(
            byte slotIndex,
            MicroOp? microOp,
            Decoder.DecodedInstructionBundle canonicalBundle)
        {
            if (slotIndex < canonicalBundle.SlotCount)
            {
                Decoder.DecodedInstruction decodedSlot = canonicalBundle.GetDecodedSlot(slotIndex);
                if (decodedSlot.IsOccupied)
                {
                    return Arch.OpcodeRegistry.IsVectorOp((uint)decodedSlot.RequireInstruction().CanonicalOpcode);
                }
            }

            return microOp is VectorMicroOp || microOp?.Class == MicroOpClass.Vector;
        }

        private static void ResolveTransportFactsShape(
            IReadOnlyList<DecodedBundleSlotDescriptor> slotDescriptors,
            out DecodedBundleSlotDescriptor[] slots,
            out byte validMask,
            out byte nopMask)
        {
            slots = new DecodedBundleSlotDescriptor[BundleWidth];
            validMask = 0;
            nopMask = 0;

            for (byte slotIndex = 0; slotIndex < BundleWidth; slotIndex++)
            {
                DecodedBundleSlotDescriptor slot = NormalizeSlotDescriptor(slotIndex, slotDescriptors);
                slots[slotIndex] = slot;

                byte slotBit = (byte)(1 << slotIndex);
                if (slot.IsValid)
                    validMask |= slotBit;

                if (slot.IsEmptyOrNop)
                {
                    nopMask |= slotBit;
                }
            }
        }

        private static void ResolveTransportFactsShapeWithSingleSlotMutation(
            IReadOnlyList<DecodedBundleSlotDescriptor> slotDescriptors,
            byte replacementSlotIndex,
            in DecodedBundleSlotDescriptor replacementSlot,
            out DecodedBundleSlotDescriptor[] slots,
            out byte validMask,
            out byte nopMask)
        {
            slots = new DecodedBundleSlotDescriptor[BundleWidth];
            validMask = 0;
            nopMask = 0;

            for (byte slotIndex = 0; slotIndex < BundleWidth; slotIndex++)
            {
                DecodedBundleSlotDescriptor slot = slotIndex == replacementSlotIndex
                    ? replacementSlot
                    : NormalizeSlotDescriptor(slotIndex, slotDescriptors);
                slots[slotIndex] = slot;

                byte slotBit = (byte)(1 << slotIndex);
                if (slot.IsValid)
                    validMask |= slotBit;

                if (slot.IsEmptyOrNop)
                {
                    nopMask |= slotBit;
                }
            }
        }

        private static void ResolveTransportFactsShapeWithClearMaskMutation(
            IReadOnlyList<DecodedBundleSlotDescriptor> slotDescriptors,
            byte clearSlotMask,
            out DecodedBundleSlotDescriptor[] slots,
            out byte validMask,
            out byte nopMask)
        {
            slots = new DecodedBundleSlotDescriptor[BundleWidth];
            validMask = 0;
            nopMask = 0;

            for (byte slotIndex = 0; slotIndex < BundleWidth; slotIndex++)
            {
                DecodedBundleSlotDescriptor slot = (clearSlotMask & (1 << slotIndex)) != 0
                    ? DecodedBundleSlotDescriptor.Create(slotIndex, null)
                    : NormalizeSlotDescriptor(slotIndex, slotDescriptors);
                slots[slotIndex] = slot;

                byte slotBit = (byte)(1 << slotIndex);
                if (slot.IsValid)
                    validMask |= slotBit;

                if (slot.IsEmptyOrNop)
                {
                    nopMask |= slotBit;
                }
            }
        }

        private static DecodedBundleSlotDescriptor NormalizeSlotDescriptor(
            byte slotIndex,
            IReadOnlyList<DecodedBundleSlotDescriptor> slotDescriptors)
        {
            if (slotIndex >= slotDescriptors.Count)
                return DecodedBundleSlotDescriptor.Create(slotIndex, null);

            DecodedBundleSlotDescriptor slot = slotDescriptors[slotIndex];
            if (slot.MicroOp != null)
                return DecodedBundleSlotDescriptor.Create(slotIndex, slot.MicroOp);

            if (!slot.IsValid && !slot.IsEmptyOrNop)
                return DecodedBundleSlotDescriptor.Create(slotIndex, null);

            return slot;
        }

        private static DecodedBundleAdmissionPrep BuildAdmissionPrep(
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            byte validMask,
            byte nopMask,
            DecodedBundleDependencySummary dependencySummary)
        {
            byte nonEmptyMask = (byte)(validMask & ~nopMask);
            byte scalarCandidateMask = ResolveAuthoritativeScalarCandidateMask(
                slots,
                dependencySummary.ScalarClusterEligibleMask);
            ulong blockerPairMask = dependencySummary.RawDependencyMask
                | dependencySummary.WawDependencyMask
                | dependencySummary.WarDependencyMask
                | dependencySummary.ControlConflictMask
                | dependencySummary.MemoryConflictMask;
            byte blockedSlotMask = ExtractBlockedTargetSlotMask(blockerPairMask);
            byte wideReadyScalarMask = (byte)(scalarCandidateMask & ~blockedSlotMask);
            byte auxiliaryOpMask = (byte)(nonEmptyMask & ~scalarCandidateMask);
            byte narrowOnlyMask = (byte)(nonEmptyMask & ~wideReadyScalarMask);

            DecodedBundleAdmissionFlags flags = DecodedBundleAdmissionFlags.None;
            if (scalarCandidateMask != 0)
                flags |= DecodedBundleAdmissionFlags.HasScalarClusterCandidates;
            if (wideReadyScalarMask != 0)
                flags |= DecodedBundleAdmissionFlags.HasWideReadyScalarCandidates;
            if ((dependencySummary.RawDependencyMask | dependencySummary.WawDependencyMask | dependencySummary.WarDependencyMask) != 0)
                flags |= DecodedBundleAdmissionFlags.HasBundleLocalDependencies;
            if (dependencySummary.ControlConflictMask != 0)
                flags |= DecodedBundleAdmissionFlags.HasControlConflicts;
            if (dependencySummary.MemoryConflictMask != 0)
                flags |= DecodedBundleAdmissionFlags.HasMemoryOrderingConstraints;
            if (auxiliaryOpMask != 0)
                flags |= DecodedBundleAdmissionFlags.HasAuxiliaryClusterOps;

            int partialWideReadyCount = BitOperations.PopCount((uint)wideReadyScalarMask);
            if (nonEmptyMask != 0 && partialWideReadyCount <= 1)
                flags |= DecodedBundleAdmissionFlags.SuggestNarrowFallback;

            return new DecodedBundleAdmissionPrep(
                scalarCandidateMask,
                wideReadyScalarMask,
                auxiliaryOpMask,
                narrowOnlyMask,
                flags);
        }

        private static byte ResolveAuthoritativeScalarCandidateMask(
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            byte scalarClusterEligibleMask)
        {
            byte sanitizedMask = 0;
            int slotCount = Math.Min(BundleWidth, slots.Count);

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                byte slotBit = (byte)(1 << slotIndex);
                if ((scalarClusterEligibleMask & slotBit) == 0)
                    continue;

                if (IsAuthoritativeScalarAdmissionCandidate(slots[slotIndex]))
                {
                    sanitizedMask |= slotBit;
                }
            }

            return sanitizedMask;
        }

        private static bool IsAuthoritativeScalarAdmissionCandidate(
            in DecodedBundleSlotDescriptor slot)
        {
            if (slot.GetRuntimeExecutionIsEmptyOrNop()
                || slot.GetRuntimeAdmissionIsMemoryOp()
                || slot.GetRuntimeAdmissionIsControlFlow()
                || slot.IsVectorOp)
            {
                return false;
            }

            SlotPlacementMetadata placement = slot.GetRuntimeAdmissionPlacement();
            return placement.RequiredSlotClass == SlotClass.AluClass
                && placement.PinningKind == SlotPinningKind.ClassFlexible;
        }

        private static ulong EncodeSlotPair(int sourceSlotIndex, int targetSlotIndex)
        {
            return 1UL << ((sourceSlotIndex * BundleWidth) + targetSlotIndex);
        }

        private static byte ExtractBlockedTargetSlotMask(ulong pairMask)
        {
            byte slotMask = 0;

            for (int sourceSlotIndex = 0; sourceSlotIndex < BundleWidth; sourceSlotIndex++)
            {
                for (int targetSlotIndex = sourceSlotIndex + 1; targetSlotIndex < BundleWidth; targetSlotIndex++)
                {
                    if ((pairMask & EncodeSlotPair(sourceSlotIndex, targetSlotIndex)) == 0)
                        continue;

                    slotMask |= (byte)(1 << targetSlotIndex);
                }
            }

            return slotMask;
        }
    }


}
