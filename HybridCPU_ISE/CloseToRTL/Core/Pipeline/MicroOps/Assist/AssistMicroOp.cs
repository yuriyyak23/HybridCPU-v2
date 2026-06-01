
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline.Metadata;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Architecturally invisible assist carrier for donor-prefetch / LDSA / VDSA.
    /// The assist plane is legality-bound via <see cref="AdmissionMetadata"/>,
    /// placement-bound via <see cref="Placement"/>, non-retiring, and replay-discardable.
    /// Assist does not publish retire-visible architectural state, but its carrier-memory
    /// effects plus replay and telemetry evidence remain intentionally observable.
    /// </summary>
    public sealed class AssistMicroOp : MicroOp
    {
        private const ulong CacheLineSize = 32;

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public AssistMicroOp(
            AssistKind kind,
            AssistExecutionMode executionMode,
            AssistCarrierKind carrierKind,
            ulong baseAddress,
            ulong prefetchLength,
            byte elementSize,
            uint elementCount,
            AssistOwnerBinding ownerBinding)
        {
            ValidateSupportedTuple(kind, executionMode, carrierKind, ownerBinding);

            Kind = kind;
            ExecutionMode = executionMode;
            CarrierKind = carrierKind;
            BaseAddress = baseAddress;
            PrefetchLength = prefetchLength == 0 ? CacheLineSize : prefetchLength;
            ElementSize = elementSize == 0 ? (byte)1 : elementSize;
            ElementCount = elementCount == 0 ? 1u : elementCount;
            CarrierVirtualThreadId = ownerBinding.CarrierVirtualThreadId;
            DonorVirtualThreadId = ownerBinding.DonorVirtualThreadId;
            TargetVirtualThreadId = ownerBinding.TargetVirtualThreadId;
            CarrierCoreId = ownerBinding.CarrierCoreId;
            TargetCoreId = ownerBinding.TargetCoreId;
            PodId = ownerBinding.PodId;
            ReplayEpochId = ownerBinding.ReplayEpochId;
            AssistEpochId = ownerBinding.AssistEpochId;
            LocalityHint = ownerBinding.LocalityHint;
            MemoryLocalityHint = ownerBinding.LocalityHint;
            DonorSource = ownerBinding.DonorSource;
            MemoryBankId = ResolveMemoryBankId(BaseAddress);

            OpCode = Processor.CPU_Core.IsaOpcodeValues.Nope;
            Latency = 1;
            IsMemoryOp = true;
            WritesRegister = false;
            HasSideEffects = false;
            SlotClass carrierSlotClass = carrierKind.ResolveSlotClass();
            Class = carrierSlotClass == SlotClass.DmaStreamClass
                ? MicroOpClass.Dma
                : MicroOpClass.Lsu;
            InstructionClass = InstructionClass.Memory;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(carrierSlotClass);
            Placement = Placement with { DomainTag = ownerBinding.DomainTag };
            OwnerThreadId = ownerBinding.TargetVirtualThreadId;
            OwnerContextId = ownerBinding.OwnerContextId;
            VirtualThreadId = ownerBinding.TargetVirtualThreadId;

            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ReadMemoryRanges = new[] { (BaseAddress, PrefetchLength) };
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();

            if (carrierSlotClass == SlotClass.DmaStreamClass)
            {
                ResourceMask = ResourceMaskBuilder.ForDMAChannel(0);
                SafetyMask = ResourceMaskBuilder.ForDMAChannel128(0);
                ResourceMask |= ResourceMaskBuilder.ForStreamEngine(0);
                SafetyMask |= ResourceMaskBuilder.ForStreamEngine128(0);
            }
            else
            {
                ResourceMask = ResourceMaskBuilder.ForLoad();
                SafetyMask = ResourceMaskBuilder.ForLoad128();
            }

            if (MemoryBankRouting.IsResolvedSchedulerVisibleBankId(MemoryBankId))
            {
                ResourceMask |= ResourceMaskBuilder.ForMemoryBank(MemoryBankId);
                SafetyMask |= ResourceMaskBuilder.ForMemoryBank128(MemoryBankId);
            }

            OriginalResourceMask = ResourceMask;
            RefreshAdmissionMetadata();
        }

        public AssistKind Kind { get; }

        public AssistExecutionMode ExecutionMode { get; }

        public AssistCarrierKind CarrierKind { get; }

        public ulong BaseAddress { get; }

        public ulong PrefetchLength { get; }

        public byte ElementSize { get; }

        public uint ElementCount { get; }

        public int CarrierVirtualThreadId { get; }

        public int DonorVirtualThreadId { get; }

        public int TargetVirtualThreadId { get; }

        public int CarrierCoreId { get; }

        public int TargetCoreId { get; }

        public ushort PodId { get; }

        public ulong ReplayEpochId { get; }

        public ulong AssistEpochId { get; }

        public LocalityHint LocalityHint { get; }

        public AssistDonorSourceDescriptor DonorSource { get; }

        public int MemoryBankId { get; }

        public bool HasResolvedMemoryBankId =>
            MemoryBankRouting.IsResolvedSchedulerVisibleBankId(MemoryBankId);

        public SlotClass CarrierSlotClass => Placement.RequiredSlotClass;

        public bool HasExplicitCoreOwnership => CarrierCoreId >= 0 || TargetCoreId >= 0;

        public uint ReservedPrefetchLines { get; private set; }

        public override bool IsAssist => true;

        public override bool IsRetireVisible => false;

        public override bool IsReplayDiscardable => true;

        public override bool SuppressesArchitecturalFaults => true;

        public override bool Execute(ref Processor.CPU_Core core) => core.ExecuteAssistMicroOp(this);

        public override string GetDescription()
        {
            string coreScope = HasExplicitCoreOwnership
                ? $", CarrierCore={CarrierCoreId}, TargetCore={TargetCoreId}, Pod={PodId}"
                : string.Empty;
            return $"Assist[{Kind}/{CarrierSlotClass}/{DonorSource.Kind}] Base=0x{BaseAddress:X}, Len={PrefetchLength}, CarrierVT={CarrierVirtualThreadId}, DonorVT={DonorVirtualThreadId}, TargetVT={TargetVirtualThreadId}{coreScope}";
        }

        public uint EstimatePrefetchLineDemand(AssistMemoryQuota quota)
        {
            ulong requestedLines = Math.Max(
                1UL,
                (PrefetchLength + CacheLineSize - 1UL) / CacheLineSize);
            ulong cappedLines = Math.Min(
                requestedLines,
                quota.ResolveLineCap(LocalityHint));
            return (uint)cappedLines;
        }

        public uint ResolvePrefetchLineBudget(AssistMemoryQuota quota)
        {
            return ReservedPrefetchLines == 0
                ? EstimatePrefetchLineDemand(quota)
                : ReservedPrefetchLines;
        }

        internal void BindReservedPrefetchLines(uint reservedPrefetchLines)
        {
            ReservedPrefetchLines = Math.Max(1u, reservedPrefetchLines);
        }

        public static bool TryCreateFromSeed(
            MicroOp seed,
            int carrierVirtualThreadId,
            ulong replayEpochId,
            ulong assistEpochId,
            out AssistMicroOp assistMicroOp)
        {
            assistMicroOp = null!;
            if (seed == null)
                return false;

            int donorVirtualThreadId =
                NormalizeAssistVirtualThreadId(seed.VirtualThreadId, seed.OwnerThreadId);
            return TryCreateFromSeed(
                seed,
                carrierVirtualThreadId,
                donorVirtualThreadId,
                replayEpochId,
                assistEpochId,
                out assistMicroOp);
        }

        public static bool TryCreateFromSeed(
            MicroOp seed,
            int carrierVirtualThreadId,
            int targetVirtualThreadId,
            ulong replayEpochId,
            ulong assistEpochId,
            out AssistMicroOp assistMicroOp)
        {
            assistMicroOp = null!;
            if (!IsSupportedAssistSeed(seed, allowVectorWriteBackSeed: false))
                return false;

            if (!TryClassifySeed(
                seed,
                widenInterCoreScalarLoadAssistToLane6: false,
                allowInterCoreScalarStoreAssist: false,
                allowInterCoreVectorWriteBackAssist: false,
                out AssistKind kind,
                out AssistExecutionMode executionMode,
                out AssistCarrierKind carrierKind,
                out ulong baseAddress,
                out ulong prefetchLength,
                out byte elementSize,
                out uint elementCount,
                out LocalityHint localityHint,
                out _,
                out _,
                out _,
                out _,
                out _))
            {
                return false;
            }

            int donorVirtualThreadId = NormalizeAssistVirtualThreadId(seed.VirtualThreadId, seed.OwnerThreadId);
            targetVirtualThreadId = NormalizeAssistVirtualThreadId(targetVirtualThreadId, donorVirtualThreadId);
            if (!AssistDonorSourceDescriptor.TryCreate(
                kind,
                executionMode,
                carrierKind,
                donorVirtualThreadId,
                targetVirtualThreadId,
                seed.OwnerContextId,
                seed.Placement.DomainTag,
                out AssistDonorSourceDescriptor donorSource))
            {
                return false;
            }

            var binding = new AssistOwnerBinding(
                carrierVirtualThreadId,
                donorVirtualThreadId,
                targetVirtualThreadId,
                seed.OwnerContextId,
                seed.Placement.DomainTag,
                replayEpochId,
                assistEpochId,
                localityHint,
                donorSource);

            assistMicroOp = new AssistMicroOp(
                kind,
                executionMode,
                carrierKind,
                baseAddress,
                prefetchLength,
                elementSize,
                elementCount,
                binding);
            return true;
        }

        public static bool TryCreateInterCoreTransportFromSeed(
            MicroOp seed,
            int donorCoreId,
            ushort donorPodId,
            ulong donorAssistEpochId,
            out AssistInterCoreTransport transport)
        {
            transport = default;
            if (!IsSupportedInterCoreSeed(seed))
                return false;

            transport = new AssistInterCoreTransport(seed, donorCoreId, donorPodId, donorAssistEpochId);
            return transport.IsValid;
        }

        public static bool TryCreateInterCoreTransportFromSeed(
            MicroOp seed,
            int donorCoreId,
            ushort donorPodId,
            out AssistInterCoreTransport transport)
        {
            return TryCreateInterCoreTransportFromSeed(
                seed,
                donorCoreId,
                donorPodId,
                donorAssistEpochId: 0,
                out transport);
        }

        public static bool TryCreateFromInterCoreTransport(
            AssistInterCoreTransport transport,
            ushort podId,
            int carrierCoreId,
            int carrierVirtualThreadId,
            int targetCoreId,
            int targetVirtualThreadId,
            int targetOwnerContextId,
            ulong targetDomainTag,
            ulong replayEpochId,
            ulong assistEpochId,
            out AssistMicroOp assistMicroOp)
        {
            assistMicroOp = null!;
            if (!transport.IsValid)
                return false;

            MicroOp seed = transport.Seed;
            if (!TryClassifyInterCoreSeed(
                    seed,
                    out AssistKind kind,
                    out AssistExecutionMode executionMode,
                    out AssistCarrierKind carrierKind,
                    out ulong baseAddress,
                    out ulong prefetchLength,
                    out byte elementSize,
                    out uint elementCount,
                    out LocalityHint localityHint,
                    out bool isWriteBackVectorSeed,
                    out bool isHotLoadScalarSeed,
                    out bool isHotStoreScalarSeed,
                    out bool isColdStoreScalarSeed,
                    out bool isDefaultStoreScalarSeed))
            {
                return false;
            }

            if (!IsInterCoreTransportEligible(
                    kind,
                    executionMode,
                    carrierKind))
            {
                return false;
            }

            int donorVirtualThreadId = NormalizeAssistVirtualThreadId(
                transport.DonorVirtualThreadId,
                transport.DonorVirtualThreadId);
            targetVirtualThreadId = NormalizeAssistVirtualThreadId(
                targetVirtualThreadId,
                donorVirtualThreadId);

            if (!AssistDonorSourceDescriptor.TryCreate(
                    kind,
                    executionMode,
                carrierKind,
                donorVirtualThreadId,
                targetVirtualThreadId,
                transport.DonorOwnerContextId,
                transport.DonorDomainTag,
                transport.DonorCoreId,
                transport.DonorPodId,
                transport.DonorAssistEpochId,
                isWriteBackVectorSeed,
                isHotLoadScalarSeed,
                isHotStoreScalarSeed,
                isColdStoreScalarSeed,
                isDefaultStoreScalarSeed,
                out AssistDonorSourceDescriptor donorSource))
            {
                return false;
            }

            var binding = new AssistOwnerBinding(
                carrierVirtualThreadId,
                donorVirtualThreadId,
                targetVirtualThreadId,
                targetOwnerContextId,
                targetDomainTag,
                replayEpochId,
                assistEpochId,
                localityHint,
                donorSource,
                carrierCoreId,
                targetCoreId,
                podId);

            assistMicroOp = new AssistMicroOp(
                kind,
                executionMode,
                carrierKind,
                baseAddress,
                prefetchLength,
                elementSize,
                elementCount,
                binding);
            return true;
        }

        private static bool IsInterCoreTransportEligible(
            AssistKind kind,
            AssistExecutionMode executionMode,
            AssistCarrierKind carrierKind)
        {
            if (!AssistTupleSupport.IsSupportedCarrierTuple(
                    kind,
                    executionMode,
                    carrierKind))
            {
                return false;
            }

            return kind switch
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

        internal static bool IsSupportedInterCoreSeed(MicroOp seed)
        {
            if (!IsSupportedAssistSeed(seed, allowVectorWriteBackSeed: true))
            {
                return false;
            }

            if (!TryClassifyInterCoreSeed(
                    seed,
                    out AssistKind kind,
                    out AssistExecutionMode executionMode,
                    out AssistCarrierKind carrierKind,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _))
            {
                return false;
            }

            return IsInterCoreTransportEligible(
                kind,
                executionMode,
                carrierKind);
        }

        private static bool TryClassifyInterCoreSeed(
            MicroOp seed,
            out AssistKind kind,
            out AssistExecutionMode executionMode,
            out AssistCarrierKind carrierKind,
            out ulong baseAddress,
            out ulong prefetchLength,
            out byte elementSize,
            out uint elementCount,
            out LocalityHint localityHint,
            out bool isWriteBackVectorSeed,
            out bool isHotLoadScalarSeed,
            out bool isHotStoreScalarSeed,
            out bool isColdStoreScalarSeed,
            out bool isDefaultStoreScalarSeed)
        {
            return TryClassifySeed(
                seed,
                widenInterCoreScalarLoadAssistToLane6: true,
                allowInterCoreScalarStoreAssist: true,
                allowInterCoreVectorWriteBackAssist: true,
                out kind,
                out executionMode,
                out carrierKind,
                out baseAddress,
                out prefetchLength,
                out elementSize,
                out elementCount,
                out localityHint,
                out isWriteBackVectorSeed,
                out isHotLoadScalarSeed,
                out isHotStoreScalarSeed,
                out isColdStoreScalarSeed,
                out isDefaultStoreScalarSeed);
        }

        private static bool TryClassifySeed(
            MicroOp seed,
            bool widenInterCoreScalarLoadAssistToLane6,
            bool allowInterCoreScalarStoreAssist,
            bool allowInterCoreVectorWriteBackAssist,
            out AssistKind kind,
            out AssistExecutionMode executionMode,
            out AssistCarrierKind carrierKind,
            out ulong baseAddress,
            out ulong prefetchLength,
            out byte elementSize,
            out uint elementCount,
            out LocalityHint localityHint,
            out bool isWriteBackVectorSeed,
            out bool isHotLoadScalarSeed,
            out bool isHotStoreScalarSeed,
            out bool isColdStoreScalarSeed,
            out bool isDefaultStoreScalarSeed)
        {
            kind = AssistKind.DonorPrefetch;
            executionMode = AssistExecutionMode.CachePrefetch;
            carrierKind = AssistCarrierKind.LsuHosted;
            baseAddress = 0;
            prefetchLength = 0;
            elementSize = 1;
            elementCount = 1;
            localityHint = seed.MemoryLocalityHint;
            isWriteBackVectorSeed = false;
            isHotLoadScalarSeed = false;
            isHotStoreScalarSeed = false;
            isColdStoreScalarSeed = false;
            isDefaultStoreScalarSeed = false;

            if (!TryResolvePrimaryAssistWindow(
                    seed,
                    allowInterCoreScalarStoreAssist,
                    allowInterCoreVectorWriteBackAssist,
                    out baseAddress,
                    out prefetchLength,
                    out isWriteBackVectorSeed))
            return false;

            if (seed is VectorMicroOp vectorMicroOp)
            {
                kind = AssistKind.Vdsa;
                executionMode = AssistExecutionMode.StreamRegisterPrefetch;
                carrierKind = AssistCarrierKind.Lane6Dma;

                int vectorElementSize = DataTypeUtils.SizeOf(vectorMicroOp.Instruction.DataTypeValue);
                elementSize = (byte)Math.Clamp(vectorElementSize, 1, 32);
                ulong streamLength = vectorMicroOp.Instruction.StreamLength == 0
                    ? 1
                    : vectorMicroOp.Instruction.StreamLength;
                elementCount = (uint)Math.Min(streamLength, 256UL);
                prefetchLength = Math.Max(prefetchLength, (ulong)elementSize * elementCount);
                return true;
            }

            if (seed is StoreMicroOp storeMicroOp)
            {
                elementSize = storeMicroOp.Size == 0 ? (byte)1 : storeMicroOp.Size;
                kind = allowInterCoreScalarStoreAssist &&
                       localityHint == LocalityHint.Cold
                    ? AssistKind.Ldsa
                    : AssistKind.DonorPrefetch;
                if (allowInterCoreScalarStoreAssist &&
                    localityHint == LocalityHint.Cold)
                {
                    executionMode = AssistExecutionMode.StreamRegisterPrefetch;
                    carrierKind = AssistCarrierKind.Lane6Dma;
                    isColdStoreScalarSeed = true;
                    prefetchLength = Math.Max(prefetchLength, CacheLineSize);
                    elementCount = ResolveLane6ScalarElementCount(prefetchLength, elementSize);
                    return true;
                }

                if (allowInterCoreScalarStoreAssist &&
                    localityHint == LocalityHint.Hot)
                {
                    executionMode = AssistExecutionMode.StreamRegisterPrefetch;
                    carrierKind = AssistCarrierKind.Lane6Dma;
                    isHotStoreScalarSeed = true;
                    prefetchLength = Math.Max(prefetchLength, CacheLineSize);
                    elementCount = ResolveLane6ScalarElementCount(prefetchLength, elementSize);
                    return true;
                }

                if (allowInterCoreScalarStoreAssist)
                {
                    executionMode = AssistExecutionMode.StreamRegisterPrefetch;
                    carrierKind = AssistCarrierKind.Lane6Dma;
                    isDefaultStoreScalarSeed = true;
                    prefetchLength = Math.Max(prefetchLength, CacheLineSize);
                    elementCount = ResolveLane6ScalarElementCount(prefetchLength, elementSize);
                    return true;
                }

                elementCount = 1;
                prefetchLength = Math.Max(prefetchLength, (ulong)elementSize);
                return true;
            }

            if (seed is LoadMicroOp loadMicroOp)
            {
                elementSize = loadMicroOp.Size == 0 ? (byte)1 : loadMicroOp.Size;
                kind = localityHint == LocalityHint.Cold ? AssistKind.Ldsa : AssistKind.DonorPrefetch;
                if (widenInterCoreScalarLoadAssistToLane6 &&
                    localityHint == LocalityHint.Hot)
                {
                    executionMode = AssistExecutionMode.StreamRegisterPrefetch;
                    carrierKind = AssistCarrierKind.Lane6Dma;
                    isHotLoadScalarSeed = true;
                    prefetchLength = Math.Max(prefetchLength, CacheLineSize);
                    elementCount = ResolveLane6ScalarElementCount(prefetchLength, elementSize);
                    return true;
                }

                if (widenInterCoreScalarLoadAssistToLane6)
                {
                    executionMode = AssistExecutionMode.StreamRegisterPrefetch;
                    carrierKind = AssistCarrierKind.Lane6Dma;
                    prefetchLength = Math.Max(prefetchLength, CacheLineSize);
                    elementCount = ResolveLane6ScalarElementCount(prefetchLength, elementSize);
                    return true;
                }

                elementCount = 1;
                prefetchLength = Math.Max(prefetchLength, (ulong)elementSize);
                return true;
            }

            if (seed is LoadStoreMicroOp)
            {
                kind = localityHint == LocalityHint.Cold ? AssistKind.Ldsa : AssistKind.DonorPrefetch;
                elementSize = (byte)Math.Clamp((int)Math.Min(prefetchLength, CacheLineSize), 1, 32);
                elementCount = 1;
                prefetchLength = Math.Max(prefetchLength, (ulong)elementSize);
                return true;
            }

            return false;
        }

        private static bool IsSupportedAssistSeed(MicroOp seed, bool allowVectorWriteBackSeed)
        {
            if (seed == null || seed.IsAssist)
            {
                return false;
            }

            if (seed.IsMemoryOp)
            {
                return true;
            }

            if (seed is not VectorMicroOp)
            {
                return false;
            }

            if (seed.ReadMemoryRanges != null && seed.ReadMemoryRanges.Count > 0)
            {
                return true;
            }

            return allowVectorWriteBackSeed &&
                   seed.WriteMemoryRanges != null &&
                   seed.WriteMemoryRanges.Count > 0;
        }

        private static uint ResolveLane6ScalarElementCount(
            ulong prefetchLength,
            byte elementSize)
        {
            ulong normalizedElementSize = elementSize == 0 ? 1UL : elementSize;
            ulong normalizedPrefetchLength = Math.Max(prefetchLength, CacheLineSize);
            ulong elementCount = Math.Max(1UL, normalizedPrefetchLength / normalizedElementSize);
            return (uint)Math.Min(elementCount, 256UL);
        }

        private static bool TryResolvePrimaryAssistWindow(
            MicroOp seed,
            bool allowInterCoreScalarStoreAssist,
            bool allowInterCoreVectorWriteBackAssist,
            out ulong baseAddress,
            out ulong prefetchLength,
            out bool isWriteBackVectorSeed)
        {
            baseAddress = 0;
            prefetchLength = 0;
            isWriteBackVectorSeed = false;

            if (seed.ReadMemoryRanges != null && seed.ReadMemoryRanges.Count > 0)
            {
                AssistCoalescingDescriptor assistCoalescingDescriptor =
                    seed.AdmissionMetadata.AssistCoalescingDescriptor;
                if (assistCoalescingDescriptor.IsValid)
                {
                    baseAddress = assistCoalescingDescriptor.BaseAddress;
                    prefetchLength = assistCoalescingDescriptor.PrefetchLength;
                }
                else
                {
                    IReadOnlyList<(ulong Address, ulong Length)> normalizedReadRanges =
                        seed.AdmissionMetadata.NormalizedReadMemoryRanges;
                    IReadOnlyList<(ulong Address, ulong Length)> resolvedReadRanges =
                        normalizedReadRanges != null && normalizedReadRanges.Count > 0
                            ? normalizedReadRanges
                            : seed.ReadMemoryRanges;
                    (baseAddress, prefetchLength) = resolvedReadRanges[0];
                }

                prefetchLength = prefetchLength == 0 ? CacheLineSize : prefetchLength;
                return true;
            }

            if (allowInterCoreScalarStoreAssist &&
                seed is StoreMicroOp &&
                seed.WriteMemoryRanges != null &&
                seed.WriteMemoryRanges.Count > 0)
            {
                (baseAddress, prefetchLength) = seed.WriteMemoryRanges[0];
                prefetchLength = prefetchLength == 0 ? CacheLineSize : prefetchLength;
                return true;
            }

            if (allowInterCoreVectorWriteBackAssist &&
                seed is VectorMicroOp &&
                (seed.ReadMemoryRanges == null || seed.ReadMemoryRanges.Count == 0) &&
                seed.WriteMemoryRanges != null &&
                seed.WriteMemoryRanges.Count > 0)
            {
                (baseAddress, prefetchLength) = seed.WriteMemoryRanges[0];
                prefetchLength = prefetchLength == 0 ? CacheLineSize : prefetchLength;
                isWriteBackVectorSeed = true;
                return true;
            }

            return false;
        }

        private static void ValidateSupportedTuple(
            AssistKind kind,
            AssistExecutionMode executionMode,
            AssistCarrierKind carrierKind,
            AssistOwnerBinding ownerBinding)
        {
            if (!AssistTupleSupport.IsSupportedCarrierTuple(
                    kind,
                    executionMode,
                    carrierKind))
            {
                throw new ArgumentException(
                    "Assist kind/execution/carrier tuple is not supported by the landed runtime.",
                    nameof(carrierKind));
            }

            ValidateVirtualThreadId(ownerBinding.CarrierVirtualThreadId, nameof(ownerBinding));
            ValidateVirtualThreadId(ownerBinding.DonorVirtualThreadId, nameof(ownerBinding));
            ValidateVirtualThreadId(ownerBinding.TargetVirtualThreadId, nameof(ownerBinding));

            if (ownerBinding.DonorSource.DonorVirtualThreadId != ownerBinding.DonorVirtualThreadId ||
                ownerBinding.DonorSource.TargetVirtualThreadId != ownerBinding.TargetVirtualThreadId)
            {
                throw new ArgumentException(
                    "Assist owner binding must match donor-source virtual-thread identity.",
                    nameof(ownerBinding));
            }

            if (!ownerBinding.DonorSource.IsLegalFor(kind, executionMode, carrierKind))
            {
                throw new ArgumentException(
                    "Assist donor-source tuple is not supported by the landed runtime.",
                    nameof(ownerBinding));
            }
        }

        private static int NormalizeAssistVirtualThreadId(int virtualThreadId, int ownerThreadId)
        {
            int resolved = virtualThreadId >= 0 && virtualThreadId < Processor.CPU_Core.SmtWays
                ? virtualThreadId
                : ownerThreadId;
            return Math.Clamp(resolved, 0, Processor.CPU_Core.SmtWays - 1);
        }

        private static int ResolveMemoryBankId(ulong address)
        {
            return MemoryBankRouting.ResolveSchedulerVisibleBankId(address);
        }

        private static void ValidateVirtualThreadId(int virtualThreadId, string paramName)
        {
            if (virtualThreadId < 0 || virtualThreadId >= Processor.CPU_Core.SmtWays)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    virtualThreadId,
                    "Assist virtual-thread ownership must stay within the landed SMT contour.");
            }
        }
    }
}
