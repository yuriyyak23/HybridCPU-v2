using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Narrow post-phase-08 assist taxonomy.
    /// All kinds remain architecturally invisible and non-retiring.
    /// </summary>
    public enum AssistKind : byte
    {
        DonorPrefetch = 0,
        Ldsa = 1,
        Vdsa = 2
    }

    /// <summary>
    /// Concrete runtime action selected for an assist micro-op.
    /// Carrier selection remains explicit and orthogonal to the execution mode itself.
    /// </summary>
    public enum AssistExecutionMode : byte
    {
        CachePrefetch = 0,
        StreamRegisterPrefetch = 1
    }

    /// <summary>
    /// Explicit physical carrier selected for one assist micro-op.
    /// Placement remains the sole runtime authority; this enum keeps the
    /// assist model honest about which production carrier was chosen.
    /// </summary>
    public enum AssistCarrierKind : byte
    {
        LsuHosted = 0,
        Lane6Dma = 1
    }

    /// <summary>
    /// Explicit donor-source identity carried by an assist micro-op.
    /// Keeps local same-VT reuse distinct from true donor sourcing and
    /// from explicit pod-local inter-core seed transport.
    /// </summary>
    public enum AssistDonorSourceKind : byte
    {
        SameThreadSeed = 0,
        IntraCoreVtDonorVector = 1,
        InterCoreVtDonorVector = 2,
        InterCoreSameVtSeed = 3,
        InterCoreVtDonorSeed = 4,
        InterCoreSameVtVector = 5,
        InterCoreSameVtVectorWriteback = 6,
        InterCoreVtDonorVectorWriteback = 7,
        InterCoreSameVtHotLoadSeed = 8,
        InterCoreVtDonorHotLoadSeed = 9,
        InterCoreSameVtHotStoreSeed = 10,
        InterCoreVtDonorHotStoreSeed = 11,
        InterCoreSameVtColdStoreSeed = 12,
        InterCoreVtDonorColdStoreSeed = 13,
        InterCoreSameVtDefaultStoreSeed = 14,
        InterCoreVtDonorDefaultStoreSeed = 15
    }

    public static class AssistCarrierKindExtensions
    {
        public static SlotClass ResolveSlotClass(this AssistCarrierKind carrierKind) => carrierKind switch
        {
            AssistCarrierKind.LsuHosted => SlotClass.LsuClass,
            AssistCarrierKind.Lane6Dma => SlotClass.DmaStreamClass,
            _ => throw new ArgumentOutOfRangeException(nameof(carrierKind), carrierKind, "Unknown assist carrier kind.")
        };
    }

    /// <summary>
    /// Freezes the landed assist kind/execution/carrier contour.
    /// This closes the first three tuple axes:
    /// <see cref="AssistKind"/>, <see cref="AssistExecutionMode"/>, and
    /// <see cref="AssistCarrierKind"/>.
    /// This does not replace <see cref="MicroOp.AdmissionMetadata"/> or
    /// <see cref="MicroOp.Placement"/> authority; it only prevents unsupported
    /// runtime tuples from materializing implicitly through direct assist construction.
    /// </summary>
    internal static class AssistTupleSupport
    {
        public static bool IsSupportedCarrierTuple(
            AssistKind assistKind,
            AssistExecutionMode executionMode,
            AssistCarrierKind carrierKind)
        {
            return assistKind switch
            {
                AssistKind.Vdsa =>
                    executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                    carrierKind == AssistCarrierKind.Lane6Dma,
                AssistKind.Ldsa =>
                    (executionMode == AssistExecutionMode.CachePrefetch &&
                     carrierKind == AssistCarrierKind.LsuHosted) ||
                    (executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                     carrierKind == AssistCarrierKind.Lane6Dma),
                AssistKind.DonorPrefetch =>
                    (executionMode == AssistExecutionMode.CachePrefetch &&
                     carrierKind == AssistCarrierKind.LsuHosted) ||
                    (executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                     carrierKind == AssistCarrierKind.Lane6Dma),
                _ => false
            };
        }
    }

    /// <summary>
    /// Explicit donor-source descriptor for one assist micro-op.
    /// Together with <see cref="AssistTupleSupport"/>, this closes the fourth
    /// donor-source axis of the reviewer-facing assist tuple matrix.
    /// This remains assist-owned runtime metadata and does not replace slot admission authority.
    /// </summary>
    public readonly struct AssistDonorSourceDescriptor
    {
        public AssistDonorSourceDescriptor(
            AssistDonorSourceKind kind,
            int donorVirtualThreadId,
            int targetVirtualThreadId,
            int ownerContextId,
            ulong domainTag,
            int sourceCoreId = -1,
            ushort sourcePodId = 0,
            ulong sourceAssistEpochId = 0)
        {
            Kind = kind;
            DonorVirtualThreadId = donorVirtualThreadId;
            TargetVirtualThreadId = targetVirtualThreadId;
            OwnerContextId = ownerContextId;
            DomainTag = domainTag;
            SourceCoreId = sourceCoreId;
            SourcePodId = sourcePodId;
            SourceAssistEpochId = sourceAssistEpochId;
        }

        public AssistDonorSourceKind Kind { get; }

        public int DonorVirtualThreadId { get; }

        public int TargetVirtualThreadId { get; }

        public int OwnerContextId { get; }

        public ulong DomainTag { get; }

        public int SourceCoreId { get; }

        public ushort SourcePodId { get; }

        public ulong SourceAssistEpochId { get; }

        public bool HasExplicitCoreSource => SourceCoreId >= 0;

        public bool IsCrossVirtualThread => DonorVirtualThreadId != TargetVirtualThreadId;

        /// <summary>
        /// Validate the complete assist four-tuple after the carrier tuple has already
        /// been frozen by <see cref="AssistTupleSupport"/>.
        /// </summary>
        public static bool TryCreate(
            AssistKind assistKind,
            AssistExecutionMode executionMode,
            AssistCarrierKind carrierKind,
            int donorVirtualThreadId,
            int targetVirtualThreadId,
            int ownerContextId,
            ulong domainTag,
            int sourceCoreId,
            ushort sourcePodId,
            ulong sourceAssistEpochId,
            out AssistDonorSourceDescriptor donorSource)
        {
            return TryCreate(
                assistKind,
                executionMode,
                carrierKind,
                donorVirtualThreadId,
                targetVirtualThreadId,
                ownerContextId,
                domainTag,
                sourceCoreId,
                sourcePodId,
                sourceAssistEpochId,
                isWriteBackVectorSeed: false,
                isHotLoadScalarSeed: false,
                isHotStoreScalarSeed: false,
                isColdStoreScalarSeed: false,
                isDefaultStoreScalarSeed: false,
                out donorSource);
        }

        public static bool TryCreate(
            AssistKind assistKind,
            AssistExecutionMode executionMode,
            AssistCarrierKind carrierKind,
            int donorVirtualThreadId,
            int targetVirtualThreadId,
            int ownerContextId,
            ulong domainTag,
            int sourceCoreId,
            ushort sourcePodId,
            ulong sourceAssistEpochId,
            bool isWriteBackVectorSeed,
            bool isHotLoadScalarSeed,
            bool isHotStoreScalarSeed,
            bool isColdStoreScalarSeed,
            bool isDefaultStoreScalarSeed,
            out AssistDonorSourceDescriptor donorSource)
        {
            donorSource = default;

            bool isNormalizedDonor =
                donorVirtualThreadId >= 0 &&
                donorVirtualThreadId < Processor.CPU_Core.SmtWays;
            bool isNormalizedTarget =
                targetVirtualThreadId >= 0 &&
                targetVirtualThreadId < Processor.CPU_Core.SmtWays;
            if (!isNormalizedDonor || !isNormalizedTarget)
            {
                return false;
            }

            AssistDonorSourceKind kind = assistKind switch
            {
                AssistKind.Vdsa when sourceCoreId >= 0 &&
                                   isWriteBackVectorSeed &&
                                   donorVirtualThreadId == targetVirtualThreadId
                    => AssistDonorSourceKind.InterCoreSameVtVectorWriteback,
                AssistKind.Vdsa when sourceCoreId >= 0 &&
                                   isWriteBackVectorSeed
                    => AssistDonorSourceKind.InterCoreVtDonorVectorWriteback,
                AssistKind.Vdsa when sourceCoreId >= 0 &&
                                   donorVirtualThreadId == targetVirtualThreadId
                    => AssistDonorSourceKind.InterCoreSameVtVector,
                AssistKind.Vdsa when sourceCoreId >= 0
                    => AssistDonorSourceKind.InterCoreVtDonorVector,
                AssistKind.Vdsa when donorVirtualThreadId != targetVirtualThreadId
                    => AssistDonorSourceKind.IntraCoreVtDonorVector,
                AssistKind.DonorPrefetch when sourceCoreId >= 0 &&
                                              carrierKind == AssistCarrierKind.Lane6Dma &&
                                              executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                                              isHotLoadScalarSeed &&
                                              donorVirtualThreadId == targetVirtualThreadId
                    => AssistDonorSourceKind.InterCoreSameVtHotLoadSeed,
                AssistKind.DonorPrefetch when sourceCoreId >= 0 &&
                                              carrierKind == AssistCarrierKind.Lane6Dma &&
                                              executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                                              isHotLoadScalarSeed
                    => AssistDonorSourceKind.InterCoreVtDonorHotLoadSeed,
                AssistKind.DonorPrefetch when sourceCoreId >= 0 &&
                                              carrierKind == AssistCarrierKind.Lane6Dma &&
                                              executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                                              isHotStoreScalarSeed &&
                                              donorVirtualThreadId == targetVirtualThreadId
                    => AssistDonorSourceKind.InterCoreSameVtHotStoreSeed,
                AssistKind.DonorPrefetch when sourceCoreId >= 0 &&
                                              carrierKind == AssistCarrierKind.Lane6Dma &&
                                              executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                                              isHotStoreScalarSeed
                    => AssistDonorSourceKind.InterCoreVtDonorHotStoreSeed,
                AssistKind.DonorPrefetch when sourceCoreId >= 0 &&
                                              carrierKind == AssistCarrierKind.Lane6Dma &&
                                              executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                                              isDefaultStoreScalarSeed &&
                                              donorVirtualThreadId == targetVirtualThreadId
                    => AssistDonorSourceKind.InterCoreSameVtDefaultStoreSeed,
                AssistKind.DonorPrefetch when sourceCoreId >= 0 &&
                                              carrierKind == AssistCarrierKind.Lane6Dma &&
                                              executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                                              isDefaultStoreScalarSeed
                    => AssistDonorSourceKind.InterCoreVtDonorDefaultStoreSeed,
                AssistKind.Ldsa when sourceCoreId >= 0 &&
                                     carrierKind == AssistCarrierKind.Lane6Dma &&
                                     executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                                     isColdStoreScalarSeed &&
                                     donorVirtualThreadId == targetVirtualThreadId
                    => AssistDonorSourceKind.InterCoreSameVtColdStoreSeed,
                AssistKind.Ldsa when sourceCoreId >= 0 &&
                                     carrierKind == AssistCarrierKind.Lane6Dma &&
                                     executionMode == AssistExecutionMode.StreamRegisterPrefetch &&
                                     isColdStoreScalarSeed
                    => AssistDonorSourceKind.InterCoreVtDonorColdStoreSeed,
                AssistKind.DonorPrefetch or AssistKind.Ldsa when sourceCoreId >= 0 &&
                                                                     donorVirtualThreadId != targetVirtualThreadId
                    => AssistDonorSourceKind.InterCoreVtDonorSeed,
                AssistKind.DonorPrefetch or AssistKind.Ldsa when sourceCoreId >= 0
                    => AssistDonorSourceKind.InterCoreSameVtSeed,
                _ => AssistDonorSourceKind.SameThreadSeed
            };

            donorSource = new AssistDonorSourceDescriptor(
                kind,
                donorVirtualThreadId,
                targetVirtualThreadId,
                ownerContextId,
                domainTag,
                sourceCoreId,
                sourcePodId,
                sourceAssistEpochId);
            return donorSource.IsLegalFor(assistKind, executionMode, carrierKind);
        }

        public static bool TryCreate(
            AssistKind assistKind,
            AssistExecutionMode executionMode,
            AssistCarrierKind carrierKind,
            int donorVirtualThreadId,
            int targetVirtualThreadId,
            int ownerContextId,
            ulong domainTag,
            out AssistDonorSourceDescriptor donorSource)
        {
            return TryCreate(
                assistKind,
                executionMode,
                carrierKind,
                donorVirtualThreadId,
                targetVirtualThreadId,
                ownerContextId,
                domainTag,
                isWriteBackVectorSeed: false,
                isHotLoadScalarSeed: false,
                isHotStoreScalarSeed: false,
                isColdStoreScalarSeed: false,
                isDefaultStoreScalarSeed: false,
                out donorSource);
        }

        public static bool TryCreate(
            AssistKind assistKind,
            AssistExecutionMode executionMode,
            AssistCarrierKind carrierKind,
            int donorVirtualThreadId,
            int targetVirtualThreadId,
            int ownerContextId,
            ulong domainTag,
            bool isWriteBackVectorSeed,
            bool isHotLoadScalarSeed,
            bool isHotStoreScalarSeed,
            bool isColdStoreScalarSeed,
            bool isDefaultStoreScalarSeed,
            out AssistDonorSourceDescriptor donorSource)
        {
            return TryCreate(
                assistKind,
                executionMode,
                carrierKind,
                donorVirtualThreadId,
                targetVirtualThreadId,
                ownerContextId,
                domainTag,
                sourceCoreId: -1,
                sourcePodId: 0,
                sourceAssistEpochId: 0,
                isWriteBackVectorSeed,
                isHotLoadScalarSeed,
                isHotStoreScalarSeed,
                isColdStoreScalarSeed,
                isDefaultStoreScalarSeed,
                out donorSource);
        }

        public bool IsLegalFor(
            AssistKind assistKind,
            AssistExecutionMode executionMode,
            AssistCarrierKind carrierKind)
        {
            if (DonorVirtualThreadId < 0 ||
                DonorVirtualThreadId >= Processor.CPU_Core.SmtWays ||
                TargetVirtualThreadId < 0 ||
                TargetVirtualThreadId >= Processor.CPU_Core.SmtWays)
            {
                return false;
            }

            if (!AssistTupleSupport.IsSupportedCarrierTuple(
                    assistKind,
                    executionMode,
                    carrierKind))
            {
                return false;
            }

            return assistKind switch
            {
                AssistKind.Vdsa => (Kind == AssistDonorSourceKind.SameThreadSeed &&
                                    DonorVirtualThreadId == TargetVirtualThreadId &&
                                    !HasExplicitCoreSource) ||
                                   (Kind == AssistDonorSourceKind.IntraCoreVtDonorVector &&
                                    DonorVirtualThreadId != TargetVirtualThreadId &&
                                    !HasExplicitCoreSource) ||
                                   ((Kind == AssistDonorSourceKind.InterCoreSameVtVector ||
                                     Kind == AssistDonorSourceKind.InterCoreSameVtVectorWriteback) &&
                                    DonorVirtualThreadId == TargetVirtualThreadId &&
                                    HasExplicitCoreSource) ||
                                   ((Kind == AssistDonorSourceKind.InterCoreVtDonorVector ||
                                     Kind == AssistDonorSourceKind.InterCoreVtDonorVectorWriteback) &&
                                    DonorVirtualThreadId != TargetVirtualThreadId &&
                                    HasExplicitCoreSource),
                AssistKind.DonorPrefetch when carrierKind == AssistCarrierKind.LsuHosted =>
                    (Kind == AssistDonorSourceKind.SameThreadSeed &&
                     DonorVirtualThreadId == TargetVirtualThreadId &&
                     !HasExplicitCoreSource),
                AssistKind.Ldsa when carrierKind == AssistCarrierKind.LsuHosted =>
                    ((Kind == AssistDonorSourceKind.SameThreadSeed &&
                      DonorVirtualThreadId == TargetVirtualThreadId &&
                      !HasExplicitCoreSource)),
                AssistKind.DonorPrefetch when carrierKind == AssistCarrierKind.Lane6Dma =>
                    ((Kind == AssistDonorSourceKind.InterCoreSameVtSeed &&
                      DonorVirtualThreadId == TargetVirtualThreadId &&
                      HasExplicitCoreSource) ||
                     (Kind == AssistDonorSourceKind.InterCoreSameVtHotLoadSeed &&
                      DonorVirtualThreadId == TargetVirtualThreadId &&
                      HasExplicitCoreSource) ||
                     (Kind == AssistDonorSourceKind.InterCoreSameVtHotStoreSeed &&
                      DonorVirtualThreadId == TargetVirtualThreadId &&
                      HasExplicitCoreSource) ||
                     (Kind == AssistDonorSourceKind.InterCoreSameVtDefaultStoreSeed &&
                      DonorVirtualThreadId == TargetVirtualThreadId &&
                      HasExplicitCoreSource) ||
                     (Kind == AssistDonorSourceKind.InterCoreVtDonorSeed &&
                      DonorVirtualThreadId != TargetVirtualThreadId &&
                      HasExplicitCoreSource) ||
                     (Kind == AssistDonorSourceKind.InterCoreVtDonorHotLoadSeed &&
                      DonorVirtualThreadId != TargetVirtualThreadId &&
                      HasExplicitCoreSource) ||
                     (Kind == AssistDonorSourceKind.InterCoreVtDonorHotStoreSeed &&
                      DonorVirtualThreadId != TargetVirtualThreadId &&
                      HasExplicitCoreSource) ||
                     (Kind == AssistDonorSourceKind.InterCoreVtDonorDefaultStoreSeed &&
                      DonorVirtualThreadId != TargetVirtualThreadId &&
                      HasExplicitCoreSource)),
                AssistKind.Ldsa when carrierKind == AssistCarrierKind.Lane6Dma =>
                    ((Kind == AssistDonorSourceKind.InterCoreSameVtSeed &&
                      DonorVirtualThreadId == TargetVirtualThreadId &&
                      HasExplicitCoreSource) ||
                     (Kind == AssistDonorSourceKind.InterCoreSameVtColdStoreSeed &&
                      DonorVirtualThreadId == TargetVirtualThreadId &&
                      HasExplicitCoreSource) ||
                     (Kind == AssistDonorSourceKind.InterCoreVtDonorSeed &&
                      DonorVirtualThreadId != TargetVirtualThreadId &&
                      HasExplicitCoreSource) ||
                     (Kind == AssistDonorSourceKind.InterCoreVtDonorColdStoreSeed &&
                      DonorVirtualThreadId != TargetVirtualThreadId &&
                      HasExplicitCoreSource)),
                _ => false
            };
        }

        public bool MatchesScope(int ownerContextId, ulong domainTag)
        {
            return OwnerContextId == ownerContextId &&
                   DomainTag == domainTag;
        }

        public bool MatchesCoreSource(int sourceCoreId, ushort sourcePodId)
        {
            return SourceCoreId == sourceCoreId &&
                   SourcePodId == sourcePodId;
        }
    }

    /// <summary>
    /// Deterministic assist invalidation reasons.
    /// </summary>
    public enum AssistInvalidationReason : byte
    {
        None = 0,
        Replay = 1,
        Trap = 2,
        Fence = 3,
        VmTransition = 4,
        SerializingBoundary = 5,
        OwnerInvalidation = 6,
        Manual = 7,
        PipelineFlush = 8,
        InterCoreOwnerDrift = 9,
        InterCoreBoundaryDrift = 10
    }

    /// <summary>
    /// Stable replay/trace signature for landed assist ownership tuples.
    /// Keeps widened assist-owner identity explicit without making assist architecturally visible.
    /// </summary>
    public static class AssistOwnershipFingerprint
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public static ulong Compute(AssistMicroOp assistMicroOp)
        {
            ArgumentNullException.ThrowIfNull(assistMicroOp);

            ulong hash = FnvOffsetBasis;
            hash = Mix(hash, (byte)assistMicroOp.Kind);
            hash = Mix(hash, (byte)assistMicroOp.ExecutionMode);
            hash = Mix(hash, (byte)assistMicroOp.CarrierKind);
            hash = Mix(hash, (byte)assistMicroOp.CarrierSlotClass);
            hash = Mix(hash, (byte)assistMicroOp.DonorSource.Kind);
            hash = Mix(hash, assistMicroOp.CarrierVirtualThreadId);
            hash = Mix(hash, assistMicroOp.DonorVirtualThreadId);
            hash = Mix(hash, assistMicroOp.TargetVirtualThreadId);
            hash = Mix(hash, assistMicroOp.CarrierCoreId);
            hash = Mix(hash, assistMicroOp.TargetCoreId);
            hash = Mix(hash, assistMicroOp.PodId);
            hash = Mix(hash, assistMicroOp.OwnerContextId);
            hash = Mix(hash, assistMicroOp.Placement.DomainTag);
            hash = Mix(hash, assistMicroOp.DonorSource.OwnerContextId);
            hash = Mix(hash, assistMicroOp.DonorSource.DomainTag);
            hash = Mix(hash, assistMicroOp.DonorSource.SourceCoreId);
            hash = Mix(hash, assistMicroOp.DonorSource.SourcePodId);
            return hash == 0 ? 1UL : hash;
        }

        private static ulong Mix(ulong hash, byte value)
        {
            hash ^= value;
            hash *= FnvPrime;
            return hash;
        }

        private static ulong Mix(ulong hash, ushort value)
        {
            hash = Mix(hash, (byte)(value & 0xFF));
            hash = Mix(hash, (byte)((value >> 8) & 0xFF));
            return hash;
        }

        private static ulong Mix(ulong hash, int value)
        {
            unchecked
            {
                uint normalized = (uint)value;
                hash = Mix(hash, (byte)(normalized & 0xFF));
                hash = Mix(hash, (byte)((normalized >> 8) & 0xFF));
                hash = Mix(hash, (byte)((normalized >> 16) & 0xFF));
                hash = Mix(hash, (byte)((normalized >> 24) & 0xFF));
                return hash;
            }
        }

        private static ulong Mix(ulong hash, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash = Mix(hash, (byte)((value >> shift) & 0xFF));
            }

            return hash;
        }
    }

    /// <summary>
    /// Explicit assist-only quota reject taxonomy.
    /// </summary>
    public enum AssistQuotaRejectKind : byte
    {
        None = 0,
        IssueCredits = 1,
        LineCredits = 2
    }

    /// <summary>
    /// Explicit cache-side assist partition reject taxonomy.
    /// Keeps cache pressure distinct from scheduler-side issue/line credits.
    /// </summary>
    public enum AssistCacheRejectKind : byte
    {
        None = 0,
        TotalLineBudget = 1,
        CarrierLineBudget = 2,
        NoAssistVictim = 3
    }

    /// <summary>
    /// Explicit assist widened-owner backpressure taxonomy.
    /// Keeps assist pressure distinct from foreground scoreboard/hardware-budget rejects.
    /// </summary>
    public enum AssistBackpressureRejectKind : byte
    {
        None = 0,
        SharedOuterCap = 1,
        OutstandingMemory = 2,
        DmaStreamRegisterFile = 3
    }

    /// <summary>
    /// Explicit assist-only SRF resident/loading partition policy for lane6/DMA carriers.
    /// Assist may reuse existing valid/loading SRF entries, but may not evict foreground-owned registers.
    /// </summary>
    public readonly struct AssistStreamRegisterPartitionPolicy
    {
        public AssistStreamRegisterPartitionPolicy(
            byte residentRegisterBudget,
            byte loadingRegisterBudget)
        {
            ResidentRegisterBudget = residentRegisterBudget == 0 ? (byte)1 : residentRegisterBudget;
            LoadingRegisterBudget = loadingRegisterBudget == 0 ? (byte)1 : loadingRegisterBudget;
        }

        public byte ResidentRegisterBudget { get; }

        public byte LoadingRegisterBudget { get; }

        public static AssistStreamRegisterPartitionPolicy Default { get; } =
            new(residentRegisterBudget: 2, loadingRegisterBudget: 1);
    }

    /// <summary>
    /// Explicit assist-only SRF reject taxonomy.
    /// </summary>
    public enum AssistStreamRegisterRejectKind : byte
    {
        None = 0,
        ResidentBudget = 1,
        LoadingBudget = 2,
        NoAssistVictim = 3
    }

    /// <summary>
    /// Explicit VT / domain / epoch ownership for one assist micro-op.
    /// </summary>
    public readonly struct AssistOwnerBinding
    {
        public AssistOwnerBinding(
            int carrierVirtualThreadId,
            int donorVirtualThreadId,
            int targetVirtualThreadId,
            int ownerContextId,
            ulong domainTag,
            ulong replayEpochId,
            ulong assistEpochId,
            LocalityHint localityHint,
            AssistDonorSourceDescriptor donorSource,
            int carrierCoreId = -1,
            int targetCoreId = -1,
            ushort podId = 0)
        {
            CarrierVirtualThreadId = carrierVirtualThreadId;
            DonorVirtualThreadId = donorVirtualThreadId;
            TargetVirtualThreadId = targetVirtualThreadId;
            OwnerContextId = ownerContextId;
            DomainTag = domainTag;
            ReplayEpochId = replayEpochId;
            AssistEpochId = assistEpochId;
            LocalityHint = localityHint;
            DonorSource = donorSource;
            CarrierCoreId = carrierCoreId;
            TargetCoreId = targetCoreId;
            PodId = podId;
        }

        public AssistOwnerBinding(
            int carrierVirtualThreadId,
            int donorVirtualThreadId,
            int targetVirtualThreadId,
            int ownerContextId,
            ulong domainTag,
            ulong replayEpochId,
            ulong assistEpochId,
            LocalityHint localityHint)
            : this(
                carrierVirtualThreadId,
                donorVirtualThreadId,
                targetVirtualThreadId,
                ownerContextId,
                domainTag,
                replayEpochId,
                assistEpochId,
                localityHint,
                new AssistDonorSourceDescriptor(
                    AssistDonorSourceKind.SameThreadSeed,
                    donorVirtualThreadId,
                    targetVirtualThreadId,
                    ownerContextId,
                    domainTag))
        {
        }

        public int CarrierVirtualThreadId { get; }

        public int DonorVirtualThreadId { get; }

        public int TargetVirtualThreadId { get; }

        public int OwnerContextId { get; }

        public ulong DomainTag { get; }

        public ulong ReplayEpochId { get; }

        public ulong AssistEpochId { get; }

        public LocalityHint LocalityHint { get; }

        public AssistDonorSourceDescriptor DonorSource { get; }

        public int CarrierCoreId { get; }

        public int TargetCoreId { get; }

        public ushort PodId { get; }

        public bool HasExplicitCoreOwnership => CarrierCoreId >= 0 || TargetCoreId >= 0;
    }

    /// <summary>
    /// Explicit pod-local inter-core assist transport.
    /// Carries a real production seed plus donor-core identity without tunneling
    /// through the normal inter-core slot-steal nomination ports.
    /// </summary>
    public readonly struct AssistInterCoreTransport
    {
        public AssistInterCoreTransport(
            MicroOp seed,
            int donorCoreId,
            ushort donorPodId,
            ulong donorAssistEpochId)
        {
            Seed = seed ?? throw new ArgumentNullException(nameof(seed));
            if (!AssistMicroOp.IsSupportedInterCoreSeed(seed))
            {
                throw new ArgumentException(
                    "Inter-core assist transport only supports the landed current class set.",
                    nameof(seed));
            }

            DonorCoreId = donorCoreId;
            DonorPodId = donorPodId;
            DonorAssistEpochId = donorAssistEpochId;
            DonorVirtualThreadId = seed.VirtualThreadId >= 0 &&
                                   seed.VirtualThreadId < Processor.CPU_Core.SmtWays
                ? seed.VirtualThreadId
                : Math.Clamp(seed.OwnerThreadId, 0, Processor.CPU_Core.SmtWays - 1);
            DonorOwnerContextId = seed.OwnerContextId;
            DonorDomainTag = seed.Placement.DomainTag;
        }

        public MicroOp Seed { get; }

        public int DonorCoreId { get; }

        public ushort DonorPodId { get; }

        public ulong DonorAssistEpochId { get; }

        public int DonorVirtualThreadId { get; }

        public int DonorOwnerContextId { get; }

        public ulong DonorDomainTag { get; }

        public bool IsSupportedCurrentClassSet => Seed != null &&
                                                  AssistMicroOp.IsSupportedInterCoreSeed(Seed);

        public bool IsValid => Seed != null &&
                               IsSupportedCurrentClassSet &&
                               DonorCoreId >= 0 &&
                               DonorVirtualThreadId >= 0 &&
                               DonorVirtualThreadId < Processor.CPU_Core.SmtWays;
    }

    /// <summary>
    /// Dedicated assist-only memory quota authority.
    /// Foreground hardware issue budgets remain separate.
    /// </summary>
    public readonly struct AssistMemoryQuota
    {
        public AssistMemoryQuota(
            byte issueCredits,
            ushort lineCredits,
            byte hotLineCap,
            byte coldLineCap)
        {
            IssueCredits = issueCredits;
            LineCredits = lineCredits;
            HotLineCap = hotLineCap == 0 ? (byte)1 : hotLineCap;
            ColdLineCap = coldLineCap == 0 ? HotLineCap : coldLineCap;
        }

        public byte IssueCredits { get; }

        public ushort LineCredits { get; }

        public byte HotLineCap { get; }

        public byte ColdLineCap { get; }

        public byte ResolveLineCap(LocalityHint localityHint)
        {
            return localityHint == LocalityHint.Cold
                ? ColdLineCap
                : HotLineCap;
        }

        public AssistMemoryQuotaState CreateState() => new(this);

        public static AssistMemoryQuota Default { get; } =
            new(issueCredits: 1, lineCredits: 4, hotLineCap: 2, coldLineCap: 4);
    }

    /// <summary>
    /// Dedicated widened-owner assist backpressure authority.
    /// Scheduler samples shared read-side pressure but reports assist pressure through an explicit owner.
    /// </summary>
    public readonly struct AssistBackpressurePolicy
    {
        public AssistBackpressurePolicy(
            byte sharedOuterCapCredits,
            byte lsuCarrierCredits,
            byte dmaCarrierCredits,
            AssistStreamRegisterPartitionPolicy dmaSrfPartitionPolicy)
        {
            SharedOuterCapCredits = sharedOuterCapCredits == 0 ? (byte)1 : sharedOuterCapCredits;
            LsuCarrierCredits = lsuCarrierCredits == 0 ? (byte)1 : lsuCarrierCredits;
            DmaCarrierCredits = dmaCarrierCredits == 0 ? (byte)1 : dmaCarrierCredits;
            DmaSrfPartitionPolicy = dmaSrfPartitionPolicy;
        }

        public byte SharedOuterCapCredits { get; }

        public byte LsuCarrierCredits { get; }

        public byte DmaCarrierCredits { get; }

        public AssistStreamRegisterPartitionPolicy DmaSrfPartitionPolicy { get; }

        public byte ResolveCarrierCredits(AssistCarrierKind carrierKind) => carrierKind switch
        {
            AssistCarrierKind.LsuHosted => LsuCarrierCredits,
            AssistCarrierKind.Lane6Dma => DmaCarrierCredits,
            _ => SharedOuterCapCredits
        };

        public AssistBackpressureState CreateState(AssistBackpressureSnapshot snapshot) =>
            new(this, snapshot);

        public static AssistBackpressurePolicy Default { get; } =
            new(
                sharedOuterCapCredits: 1,
                lsuCarrierCredits: 1,
                dmaCarrierCredits: 1,
                dmaSrfPartitionPolicy: AssistStreamRegisterPartitionPolicy.Default);
    }

    /// <summary>
    /// Deterministic pack-pass snapshot used by assist backpressure reservation.
    /// Captures only live shared read-side pressure and projected outstanding-memory truth.
    /// </summary>
    public readonly struct AssistBackpressureSnapshot
    {
        public AssistBackpressureSnapshot(
            byte sharedOuterCapCredits,
            uint consumedSharedReadBudgetByBank,
            ushort sharedReadBudgetAtLeastOneMask,
            ushort sharedReadBudgetAtLeastTwoMask,
            byte projectedOutstandingCountVt0,
            byte projectedOutstandingCountVt1,
            byte projectedOutstandingCountVt2,
            byte projectedOutstandingCountVt3,
            byte projectedOutstandingCapacityVt0,
            byte projectedOutstandingCapacityVt1,
            byte projectedOutstandingCapacityVt2,
            byte projectedOutstandingCapacityVt3)
        {
            SharedOuterCapCredits = sharedOuterCapCredits;
            ConsumedSharedReadBudgetByBank = consumedSharedReadBudgetByBank;
            SharedReadBudgetAtLeastOneMask = sharedReadBudgetAtLeastOneMask;
            SharedReadBudgetAtLeastTwoMask = sharedReadBudgetAtLeastTwoMask;
            ProjectedOutstandingCountVt0 = projectedOutstandingCountVt0;
            ProjectedOutstandingCountVt1 = projectedOutstandingCountVt1;
            ProjectedOutstandingCountVt2 = projectedOutstandingCountVt2;
            ProjectedOutstandingCountVt3 = projectedOutstandingCountVt3;
            ProjectedOutstandingCapacityVt0 = projectedOutstandingCapacityVt0;
            ProjectedOutstandingCapacityVt1 = projectedOutstandingCapacityVt1;
            ProjectedOutstandingCapacityVt2 = projectedOutstandingCapacityVt2;
            ProjectedOutstandingCapacityVt3 = projectedOutstandingCapacityVt3;
        }

        public byte SharedOuterCapCredits { get; }

        public uint ConsumedSharedReadBudgetByBank { get; }

        public ushort SharedReadBudgetAtLeastOneMask { get; }

        public ushort SharedReadBudgetAtLeastTwoMask { get; }

        public byte ProjectedOutstandingCountVt0 { get; }

        public byte ProjectedOutstandingCountVt1 { get; }

        public byte ProjectedOutstandingCountVt2 { get; }

        public byte ProjectedOutstandingCountVt3 { get; }

        public byte ProjectedOutstandingCapacityVt0 { get; }

        public byte ProjectedOutstandingCapacityVt1 { get; }

        public byte ProjectedOutstandingCapacityVt2 { get; }

        public byte ProjectedOutstandingCapacityVt3 { get; }
    }

    /// <summary>
    /// Dedicated assist-only L1 data cache partition policy.
    /// Assist may use only a small bounded resident window and may evict only other assist-owned lines.
    /// </summary>
    public readonly struct AssistCachePartitionPolicy
    {
        public AssistCachePartitionPolicy(
            byte totalLineBudget,
            byte lsuHostedLineBudget,
            byte dmaHostedLineBudget)
        {
            TotalLineBudget = totalLineBudget == 0 ? (byte)1 : totalLineBudget;
            LsuHostedLineBudget = lsuHostedLineBudget == 0 ? TotalLineBudget : lsuHostedLineBudget;
            DmaHostedLineBudget = dmaHostedLineBudget == 0 ? (byte)1 : dmaHostedLineBudget;
        }

        public byte TotalLineBudget { get; }

        public byte LsuHostedLineBudget { get; }

        public byte DmaHostedLineBudget { get; }

        public byte ResolveCarrierLineBudget(AssistCarrierKind carrierKind) => carrierKind switch
        {
            AssistCarrierKind.LsuHosted => LsuHostedLineBudget,
            AssistCarrierKind.Lane6Dma => DmaHostedLineBudget,
            _ => TotalLineBudget
        };

        public static AssistCachePartitionPolicy Default { get; } =
            new(totalLineBudget: 8, lsuHostedLineBudget: 6, dmaHostedLineBudget: 2);
    }

    /// <summary>
    /// Mutable per-bundle assist quota state used by scheduler-side assist injection.
    /// </summary>
    public struct AssistMemoryQuotaState
    {
        private readonly AssistMemoryQuota _quota;
        private byte _remainingIssueCredits;
        private ushort _remainingLineCredits;

        public AssistMemoryQuotaState(AssistMemoryQuota quota)
        {
            _quota = quota;
            _remainingIssueCredits = quota.IssueCredits;
            _remainingLineCredits = quota.LineCredits;
        }

        public readonly byte RemainingIssueCredits => _remainingIssueCredits;

        public readonly ushort RemainingLineCredits => _remainingLineCredits;

        public bool TryReserve(
            AssistMicroOp assistMicroOp,
            out uint reservedLineDemand,
            out AssistQuotaRejectKind rejectKind)
        {
            ArgumentNullException.ThrowIfNull(assistMicroOp);

            reservedLineDemand = assistMicroOp.EstimatePrefetchLineDemand(_quota);
            rejectKind = AssistQuotaRejectKind.None;

            if (_remainingIssueCredits == 0)
            {
                rejectKind = AssistQuotaRejectKind.IssueCredits;
                return false;
            }

            if (_remainingLineCredits < reservedLineDemand)
            {
                rejectKind = AssistQuotaRejectKind.LineCredits;
                return false;
            }

            _remainingIssueCredits--;
            _remainingLineCredits -= (ushort)reservedLineDemand;
            return true;
        }
    }

    /// <summary>
    /// Mutable per-bundle assist widened-owner backpressure state.
    /// This state mirrors shared read-side pressure without borrowing foreground reject taxonomy.
    /// </summary>
    public struct AssistBackpressureState
    {
        private readonly AssistBackpressurePolicy _policy;
        private readonly ushort _sharedReadBudgetAtLeastOneMask;
        private readonly ushort _sharedReadBudgetAtLeastTwoMask;
        private readonly byte _projectedOutstandingCapacityVt0;
        private readonly byte _projectedOutstandingCapacityVt1;
        private readonly byte _projectedOutstandingCapacityVt2;
        private readonly byte _projectedOutstandingCapacityVt3;
        private byte _remainingSharedOuterCapCredits;
        private byte _remainingLsuCarrierCredits;
        private byte _remainingDmaCarrierCredits;
        private byte _projectedOutstandingCountVt0;
        private byte _projectedOutstandingCountVt1;
        private byte _projectedOutstandingCountVt2;
        private byte _projectedOutstandingCountVt3;
        private uint _consumedSharedReadBudgetByBank;

        public AssistBackpressureState(
            AssistBackpressurePolicy policy,
            AssistBackpressureSnapshot snapshot)
        {
            _policy = policy;
            _sharedReadBudgetAtLeastOneMask = snapshot.SharedReadBudgetAtLeastOneMask;
            _sharedReadBudgetAtLeastTwoMask = snapshot.SharedReadBudgetAtLeastTwoMask;
            _projectedOutstandingCapacityVt0 = snapshot.ProjectedOutstandingCapacityVt0;
            _projectedOutstandingCapacityVt1 = snapshot.ProjectedOutstandingCapacityVt1;
            _projectedOutstandingCapacityVt2 = snapshot.ProjectedOutstandingCapacityVt2;
            _projectedOutstandingCapacityVt3 = snapshot.ProjectedOutstandingCapacityVt3;
            _remainingSharedOuterCapCredits = Math.Min(
                policy.SharedOuterCapCredits,
                snapshot.SharedOuterCapCredits);
            _remainingLsuCarrierCredits = policy.LsuCarrierCredits;
            _remainingDmaCarrierCredits = policy.DmaCarrierCredits;
            _projectedOutstandingCountVt0 = snapshot.ProjectedOutstandingCountVt0;
            _projectedOutstandingCountVt1 = snapshot.ProjectedOutstandingCountVt1;
            _projectedOutstandingCountVt2 = snapshot.ProjectedOutstandingCountVt2;
            _projectedOutstandingCountVt3 = snapshot.ProjectedOutstandingCountVt3;
            _consumedSharedReadBudgetByBank = snapshot.ConsumedSharedReadBudgetByBank;
        }

        public readonly AssistStreamRegisterPartitionPolicy DmaSrfPartitionPolicy =>
            _policy.DmaSrfPartitionPolicy;

        public bool TryReserve(
            AssistMicroOp assistMicroOp,
            bool dmaSrfAvailable,
            out AssistBackpressureRejectKind rejectKind)
        {
            ArgumentNullException.ThrowIfNull(assistMicroOp);

            rejectKind = AssistBackpressureRejectKind.None;
            int targetVirtualThreadId = assistMicroOp.TargetVirtualThreadId;
            if (!HasProjectedOutstandingCapacity(targetVirtualThreadId))
            {
                rejectKind = AssistBackpressureRejectKind.OutstandingMemory;
                return false;
            }

            if (!HasSharedOuterCap(assistMicroOp.MemoryBankId) ||
                !HasCarrierCredits(assistMicroOp.CarrierKind))
            {
                rejectKind = AssistBackpressureRejectKind.SharedOuterCap;
                return false;
            }

            if (assistMicroOp.CarrierKind == AssistCarrierKind.Lane6Dma &&
                !dmaSrfAvailable)
            {
                rejectKind = AssistBackpressureRejectKind.DmaStreamRegisterFile;
                return false;
            }

            ConsumeSharedOuterCap(assistMicroOp.MemoryBankId);
            ConsumeCarrierCredits(assistMicroOp.CarrierKind);
            IncrementProjectedOutstandingCount(targetVirtualThreadId);
            return true;
        }

        private readonly bool HasProjectedOutstandingCapacity(int virtualThreadId)
        {
            return virtualThreadId switch
            {
                0 => _projectedOutstandingCountVt0 < _projectedOutstandingCapacityVt0,
                1 => _projectedOutstandingCountVt1 < _projectedOutstandingCapacityVt1,
                2 => _projectedOutstandingCountVt2 < _projectedOutstandingCapacityVt2,
                3 => _projectedOutstandingCountVt3 < _projectedOutstandingCapacityVt3,
                _ => false
            };
        }

        private readonly bool HasSharedOuterCap(int memoryBankId)
        {
            if (_remainingSharedOuterCapCredits == 0)
            {
                return false;
            }

            if ((uint)memoryBankId >= 16)
            {
                return true;
            }

            return GetConsumedSharedReadBudget(memoryBankId) <
                   GetSharedReadBudgetForBank(memoryBankId);
        }

        private readonly bool HasCarrierCredits(AssistCarrierKind carrierKind) => carrierKind switch
        {
            AssistCarrierKind.LsuHosted => _remainingLsuCarrierCredits > 0,
            AssistCarrierKind.Lane6Dma => _remainingDmaCarrierCredits > 0,
            _ => false
        };

        private void ConsumeSharedOuterCap(int memoryBankId)
        {
            if (_remainingSharedOuterCapCredits > 0)
            {
                _remainingSharedOuterCapCredits--;
            }

            _consumedSharedReadBudgetByBank =
                IncrementPackedConsumedHardwareBudget(_consumedSharedReadBudgetByBank, memoryBankId);
        }

        private void ConsumeCarrierCredits(AssistCarrierKind carrierKind)
        {
            switch (carrierKind)
            {
                case AssistCarrierKind.LsuHosted:
                    if (_remainingLsuCarrierCredits > 0)
                    {
                        _remainingLsuCarrierCredits--;
                    }
                    break;
                case AssistCarrierKind.Lane6Dma:
                    if (_remainingDmaCarrierCredits > 0)
                    {
                        _remainingDmaCarrierCredits--;
                    }
                    break;
            }
        }

        private void IncrementProjectedOutstandingCount(int virtualThreadId)
        {
            switch (virtualThreadId)
            {
                case 0:
                    if (_projectedOutstandingCountVt0 < 8)
                    {
                        _projectedOutstandingCountVt0++;
                    }
                    break;
                case 1:
                    if (_projectedOutstandingCountVt1 < 8)
                    {
                        _projectedOutstandingCountVt1++;
                    }
                    break;
                case 2:
                    if (_projectedOutstandingCountVt2 < 8)
                    {
                        _projectedOutstandingCountVt2++;
                    }
                    break;
                case 3:
                    if (_projectedOutstandingCountVt3 < 8)
                    {
                        _projectedOutstandingCountVt3++;
                    }
                    break;
            }
        }

        private readonly byte GetSharedReadBudgetForBank(int bankId)
        {
            if ((uint)bankId >= 16)
            {
                return 0;
            }

            ushort bankMask = (ushort)(1 << bankId);
            if ((_sharedReadBudgetAtLeastTwoMask & bankMask) != 0)
            {
                return 2;
            }

            return (_sharedReadBudgetAtLeastOneMask & bankMask) != 0 ? (byte)1 : (byte)0;
        }

        private readonly byte GetConsumedSharedReadBudget(int bankId)
        {
            if ((uint)bankId >= 16)
            {
                return 0;
            }

            int shift = bankId * 2;
            return (byte)((_consumedSharedReadBudgetByBank >> shift) & 0x3u);
        }

        private static uint IncrementPackedConsumedHardwareBudget(
            uint consumedPackedBudgetByBank,
            int bankId)
        {
            if ((uint)bankId >= 16)
            {
                return consumedPackedBudgetByBank;
            }

            int shift = bankId * 2;
            uint current = (consumedPackedBudgetByBank >> shift) & 0x3u;
            if (current >= 3)
            {
                return consumedPackedBudgetByBank;
            }

            uint next = current + 1u;
            return (consumedPackedBudgetByBank & ~(0x3u << shift)) | (next << shift);
        }
    }
}
