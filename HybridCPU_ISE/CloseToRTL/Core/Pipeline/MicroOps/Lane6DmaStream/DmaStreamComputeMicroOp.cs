using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Canonical lane6 typed-slot carrier for descriptor-backed memory-memory compute.
    /// Phase 06 opens the current DSC1 production contour only: lane6 token-owned
    /// runtime execution, staged writes, and retire-owned commit.
    /// </summary>
    public sealed class DmaStreamComputeMicroOp : MicroOp
    {
        private DmaStreamComputeTokenStore _tokenStore = new();
        private DmaStreamComputeExecutionResult? _lastExecutionResult;
        private DmaStreamComputeCommitResult? _lastRetireCommitResult;

        public DmaStreamComputeMicroOp(DmaStreamComputeDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            EnsureOwnerDomainGuardAccepted(descriptor);
            EnsureMandatoryFootprints(descriptor);

            Descriptor = descriptor;
            DescriptorReference = descriptor.DescriptorReference;
            DescriptorIdentityHash = descriptor.DescriptorIdentityHash;
            CertificateInputHash = descriptor.CertificateInputHash;
            NormalizedFootprintHash = descriptor.NormalizedFootprintHash;
            Operation = descriptor.Operation;
            ElementType = descriptor.ElementType;
            Shape = descriptor.Shape;
            RangeEncoding = descriptor.RangeEncoding;
            PartialCompletionPolicy = descriptor.PartialCompletionPolicy;
            OwnerBinding = descriptor.OwnerBinding;
            ReplayEvidence = DmaStreamComputeReplayEvidence.CreateForDescriptor(descriptor);
            LastExecutionReplayEvidence = ReplayEvidence;

            IsMemoryOp = true;
            HasSideEffects = true;
            Class = MicroOpClass.Dma;
            InstructionClass = Arch.InstructionClass.Memory;
            SerializationClass = Arch.SerializationClass.MemoryOrdered;
            WritesRegister = false;

            OwnerThreadId = descriptor.OwnerBinding.OwnerVirtualThreadId;
            VirtualThreadId = descriptor.OwnerBinding.OwnerVirtualThreadId;
            OwnerContextId = ConvertOwnerContextId(descriptor.OwnerBinding.OwnerContextId);

            SetHardPinnedPlacement(SlotClass.DmaStreamClass, 6);
            Placement = Placement with { DomainTag = descriptor.OwnerBinding.OwnerDomainTag };

            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ReadMemoryRanges = ConvertRanges(descriptor.NormalizedReadMemoryRanges);
            WriteMemoryRanges = ConvertRanges(descriptor.NormalizedWriteMemoryRanges);

            ResourceMask = BuildResourceMask(descriptor.OwnerBinding.OwnerDomainTag);
            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public DmaStreamComputeDescriptor Descriptor { get; }

        public DmaStreamComputeDescriptorReference DescriptorReference { get; }

        public ulong DescriptorIdentityHash { get; }

        public ulong CertificateInputHash { get; }

        public ulong NormalizedFootprintHash { get; }

        public DmaStreamComputeOperationKind Operation { get; }

        public DmaStreamComputeElementType ElementType { get; }

        public DmaStreamComputeShapeKind Shape { get; }

        public DmaStreamComputeRangeEncoding RangeEncoding { get; }

        public DmaStreamComputePartialCompletionPolicy PartialCompletionPolicy { get; }

        public DmaStreamComputeOwnerBinding OwnerBinding { get; }

        public DmaStreamComputeReplayEvidence ReplayEvidence { get; }

        public DmaStreamComputeExecutionResult? LastExecutionResult => _lastExecutionResult;

        public DmaStreamComputeCommitResult? LastRetireCommitResult => _lastRetireCommitResult;

        public DmaStreamComputeToken? LastExecutionToken => _lastExecutionResult?.Token;

        public DmaStreamComputeTokenHandle LastExecutionTokenHandle =>
            _lastExecutionResult?.TokenHandle ?? default;

        public int ActiveTokenCount => _tokenStore.ActiveTokenCount;

        public DmaStreamComputeReplayEvidence LastExecutionReplayEvidence { get; private set; }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public DmaStreamComputeReplayEvidence ExportReplayEvidence(
            DmaStreamComputeTokenLifecycleEvidence tokenLifecycleEvidence = default,
            DmaStreamComputeLanePlacementEvidence lanePlacementEvidence = default) =>
            DmaStreamComputeReplayEvidence.CreateForDescriptor(
                Descriptor,
                tokenLifecycleEvidence: tokenLifecycleEvidence,
                lanePlacementEvidence: lanePlacementEvidence);

        public override bool Execute(ref Processor.CPU_Core core)
        {
            EnsurePhase06ProductionContour(Descriptor);
            EnsureNoUnretiredExecution();
            RejectGuestCompatibilityExecution(ref core);
            _tokenStore = core.GetDmaStreamComputeTokenStore();

            DmaStreamComputeExecutionResult execution =
                DmaStreamComputeRuntime.ExecuteMaterializedMicroOpToCommitPending(
                    this,
                    _tokenStore,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6());

            _lastExecutionResult = execution;
            LastExecutionReplayEvidence = ExportReplayEvidence(
                execution.Token.ExportLifecycleEvidence(),
                DmaStreamComputeLanePlacementEvidence.MaterializedLane6(
                    selectedLane: 6,
                    freeLaneMask: SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass),
                    stableDonorMask: 0,
                    replayActive: false));
            return execution.IsCommitPending || execution.RequiresRetireExceptionPublication;
        }

        public override void Commit(ref Processor.CPU_Core core)
        {
            if (_lastExecutionResult is null)
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp retire commit requires a prior successful lane6 Execute capture.");
            }

            _lastRetireCommitResult =
                core.ApplyDmaStreamComputeRetireCommit(
                    _lastExecutionResult.Token,
                    Descriptor.OwnerGuardDecision);
            LastExecutionReplayEvidence = ExportReplayEvidence(
                _lastExecutionResult.Token.ExportLifecycleEvidence(),
                DmaStreamComputeLanePlacementEvidence.MaterializedLane6(
                    selectedLane: 6,
                    freeLaneMask: SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass),
                    stableDonorMask: 0,
                    replayActive: false));
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            Commit(ref core);
        }

        public override string GetDescription() =>
            $"DmaStreamCompute: Op={Operation}, Type={ElementType}, Shape={Shape}, " +
            $"Descriptor=0x{DescriptorIdentityHash:X16}, Footprint=0x{NormalizedFootprintHash:X16}";

        private static void EnsureMandatoryFootprints(DmaStreamComputeDescriptor descriptor)
        {
            if (descriptor.ReadMemoryRanges is null ||
                descriptor.ReadMemoryRanges.Count == 0 ||
                descriptor.NormalizedReadMemoryRanges is null ||
                descriptor.NormalizedReadMemoryRanges.Count == 0)
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp requires non-empty ReadMemoryRanges before typed-slot admission.");
            }

            if (IsMemoryWritingOperation(descriptor.Operation) &&
                (descriptor.WriteMemoryRanges is null ||
                 descriptor.WriteMemoryRanges.Count == 0 ||
                 descriptor.NormalizedWriteMemoryRanges is null ||
                 descriptor.NormalizedWriteMemoryRanges.Count == 0))
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp requires non-empty WriteMemoryRanges for memory-writing operations before typed-slot admission.");
            }
        }

        private static void EnsureOwnerDomainGuardAccepted(DmaStreamComputeDescriptor descriptor)
        {
            if (!descriptor.OwnerGuardDecision.IsAllowed)
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp requires an accepted owner/domain guard decision before typed-slot admission.");
            }

            if (descriptor.OwnerGuardDecision.DescriptorOwnerBinding is null ||
                !descriptor.OwnerGuardDecision.DescriptorOwnerBinding.Equals(descriptor.OwnerBinding))
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp owner/domain guard decision does not match the descriptor owner binding.");
            }
        }

        private static bool IsMemoryWritingOperation(DmaStreamComputeOperationKind operation) =>
            operation is DmaStreamComputeOperationKind.Copy
                or DmaStreamComputeOperationKind.Add
                or DmaStreamComputeOperationKind.Mul
                or DmaStreamComputeOperationKind.Fma
                or DmaStreamComputeOperationKind.Reduce;

        private static void EnsurePhase06ProductionContour(DmaStreamComputeDescriptor descriptor)
        {
            if (!IsMemoryWritingOperation(descriptor.Operation) ||
                ResolveElementSize(descriptor.ElementType) == 0 ||
                !IsPhase06Shape(descriptor.Operation, descriptor.Shape) ||
                descriptor.RangeEncoding != DmaStreamComputeRangeEncoding.InlineContiguous ||
                descriptor.PartialCompletionPolicy != DmaStreamComputePartialCompletionPolicy.AllOrNone)
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp production execution rejects descriptors outside the Phase 06 DSC1 contour and fails closed without StreamEngine or DMAController fallback.");
            }
        }

        private void EnsureNoUnretiredExecution()
        {
            if (_lastExecutionResult?.Token.State is
                DmaStreamComputeTokenState.CommitPending or
                DmaStreamComputeTokenState.Faulted)
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp already has a captured token that must retire, fault, or be canceled before re-execution.");
            }
        }

        private void RejectGuestCompatibilityExecution(ref Processor.CPU_Core core)
        {
            if (core.Csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine) == 0)
            {
                return;
            }

            bool compatibilityGuestExecution;
            try
            {
                compatibilityGuestExecution =
                    core.ReadVirtualThreadPipelineState(OwnerThreadId) == PipelineState.GuestExecution;
            }
            catch (ArgumentOutOfRangeException)
            {
                compatibilityGuestExecution =
                    core.HasAnyVirtualThreadPipelineState(PipelineState.GuestExecution);
            }

            if (compatibilityGuestExecution)
            {
                throw new InvalidOperationException(
                    "Guest Lane6 compatibility execution is fail-closed: no runtime-owned DMA domain binding is admitted for the frozen VMX frontend.");
            }
        }

        private static bool IsPhase06Shape(
            DmaStreamComputeOperationKind operation,
            DmaStreamComputeShapeKind shape) =>
            shape == DmaStreamComputeShapeKind.Contiguous1D ||
            (operation == DmaStreamComputeOperationKind.Reduce &&
             shape == DmaStreamComputeShapeKind.FixedReduce);

        private static int ResolveElementSize(DmaStreamComputeElementType elementType) =>
            elementType switch
            {
                DmaStreamComputeElementType.UInt8 => 1,
                DmaStreamComputeElementType.UInt16 => 2,
                DmaStreamComputeElementType.UInt32 => 4,
                DmaStreamComputeElementType.UInt64 => 8,
                DmaStreamComputeElementType.Float32 => 4,
                DmaStreamComputeElementType.Float64 => 8,
                _ => 0
            };

        private static IReadOnlyList<(ulong Address, ulong Length)> ConvertRanges(
            IReadOnlyList<DmaStreamComputeMemoryRange> ranges)
        {
            if (ranges is null || ranges.Count == 0)
            {
                return Array.Empty<(ulong Address, ulong Length)>();
            }

            var converted = new (ulong Address, ulong Length)[ranges.Count];
            for (int i = 0; i < ranges.Count; i++)
            {
                converted[i] = (ranges[i].Address, ranges[i].Length);
            }

            return converted;
        }

        private static ResourceBitset BuildResourceMask(ulong ownerDomainTag)
        {
            int resourceDomainBucket = (int)(ownerDomainTag & 0xFUL);

            return ResourceMaskBuilder.ForDMAChannel(0)
                   | ResourceMaskBuilder.ForStreamEngine(0)
                   | ResourceMaskBuilder.ForLoad()
                   | ResourceMaskBuilder.ForStore()
                   | ResourceMaskBuilder.ForMemoryDomain(resourceDomainBucket);
        }

        private static int ConvertOwnerContextId(uint ownerContextId)
        {
            if (ownerContextId > int.MaxValue)
            {
                throw new InvalidOperationException(
                    "DmaStreamCompute owner context id exceeds the current runtime owner-context field width.");
            }

            return (int)ownerContextId;
        }
    }
}
