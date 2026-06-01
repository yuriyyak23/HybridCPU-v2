using System;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Lane6 queue/control carrier for the Phase 07 DSC_STATUS contour.
    /// It captures a store-owned token status snapshot at execute and publishes
    /// the scalar status word only through the retire writeback packet.
    /// </summary>
    public sealed class DmaStreamComputeStatusMicroOp : MicroOp
    {
        private const uint Lane6QueueControlOwnerPodId = 0;

        private DmaStreamComputeStatusQueryResult? _lastStatusResult;
        private DmaStreamComputeOwnerBinding? _lastOwnerBinding;
        private ulong _lastRequestedTokenId;
        private ulong _capturedStatusWord;

        public DmaStreamComputeStatusMicroOp(
            ushort destinationRegister,
            ushort tokenRegister)
        {
            if (!TryNormalizeFlatArchRegId(destinationRegister, out int destRegId))
            {
                throw new DecodeProjectionFaultException(
                    "DSC_STATUS requires a flat architectural rd register id.");
            }

            if (!TryNormalizeFlatArchRegId(tokenRegister, out int tokenRegId))
            {
                throw new DecodeProjectionFaultException(
                    "DSC_STATUS requires a flat architectural rs1 token-id register id.");
            }

            OpCode = Processor.CPU_Core.IsaOpcodeValues.DSC_STATUS;
            DestinationRegister = (ushort)destRegId;
            TokenRegister = (ushort)tokenRegId;
            DestRegID = DestinationRegister;

            IsMemoryOp = false;
            HasSideEffects = true;
            Class = MicroOpClass.Dma;
            InstructionClass = Arch.InstructionClass.Memory;
            SerializationClass = Arch.SerializationClass.MemoryOrdered;
            WritesRegister = DestinationRegister != 0;

            SetHardPinnedPlacement(SlotClass.DmaStreamClass, 6);
            RefreshOwnerDependentMetadata();
        }

        public ushort DestinationRegister { get; }

        public ushort TokenRegister { get; }

        public DmaStreamComputeStatusQueryResult? LastStatusResult => _lastStatusResult;

        public DmaStreamComputeOwnerBinding? LastOwnerBinding => _lastOwnerBinding;

        public ulong LastRequestedTokenId => _lastRequestedTokenId;

        public DmaStreamComputeStatusReplayEvidence LastStatusReplayEvidence { get; private set; }

        public bool UsedStreamEngineFallback => false;

        public bool UsedDmaControllerFallback => false;

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public override bool Execute(ref Processor.CPU_Core core)
        {
            RefreshOwnerDependentMetadata();

            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            _lastRequestedTokenId = ReadUnifiedScalarSourceOperand(ref core, vtId, TokenRegister);
            _lastOwnerBinding = BuildOwnerBinding(ref core, vtId);

            _lastStatusResult =
                core.GetDmaStreamComputeTokenStore().QueryStatusByTokenId(
                    _lastRequestedTokenId,
                    _lastOwnerBinding);
            _capturedStatusWord =
                _lastStatusResult.Snapshot?.EncodedStatusWord ?? 0UL;
            LastStatusReplayEvidence = DmaStreamComputeStatusReplayEvidence.Capture(
                _lastRequestedTokenId,
                _lastOwnerBinding,
                _lastStatusResult,
                DmaStreamComputeLanePlacementEvidence.MaterializedLane6(
                    selectedLane: 6,
                    freeLaneMask: SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass),
                    stableDonorMask: 0,
                    replayActive: false));

            return true;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            DmaStreamComputeStatusQueryResult result =
                _lastStatusResult ?? throw new InvalidOperationException(
                    "DSC_STATUS retire requires a prior execute/capture result.");

            if (!result.IsAccepted)
            {
                throw CreateRetireException(result);
            }

            if (!WritesRegister)
            {
                return;
            }

            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            AppendWriteBackRetireRecord(
                retireRecords,
                ref retireRecordCount,
                RetireRecord.RegisterWrite(vtId, DestinationRegister, _capturedStatusWord));
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _capturedStatusWord;
            return _lastStatusResult?.IsAccepted == true && WritesRegister;
        }

        public override void CapturePrimaryWriteBackResult(ulong value) =>
            _capturedStatusWord = value;

        public override void RefreshWriteMetadata() => RefreshOwnerDependentMetadata();

        public void RefreshOwnerDependentMetadata()
        {
            ReadRegisters = TokenRegister == 0
                ? Array.Empty<int>()
                : new[] { (int)TokenRegister };
            WriteRegisters = WritesRegister
                ? new[] { (int)DestinationRegister }
                : Array.Empty<int>();
            ReadMemoryRanges = Array.Empty<(ulong Address, ulong Length)>();
            WriteMemoryRanges = Array.Empty<(ulong Address, ulong Length)>();
            ResourceMask = BuildResourceMask(Placement.DomainTag);
            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override string GetDescription() =>
            $"DSC_STATUS: rd=x{DestinationRegister}, token=x{TokenRegister}";

        private DmaStreamComputeOwnerBinding BuildOwnerBinding(
            ref Processor.CPU_Core core,
            int vtId)
        {
            if (OwnerContextId < 0)
            {
                throw new InvalidOperationException(
                    "DSC_STATUS owner context id is not materialized.");
            }

            return new DmaStreamComputeOwnerBinding
            {
                OwnerVirtualThreadId = (ushort)vtId,
                OwnerContextId = (uint)OwnerContextId,
                OwnerCoreId = core.CoreID,
                OwnerPodId = Lane6QueueControlOwnerPodId,
                OwnerDomainTag = Placement.DomainTag,
                DeviceId = DmaStreamComputeDescriptor.CanonicalLane6DeviceId
            };
        }

        private Exception CreateRetireException(
            DmaStreamComputeStatusQueryResult result)
        {
            if (result.RejectKind == DmaStreamComputeStatusQueryRejectKind.OwnerDomainMismatch &&
                _lastOwnerBinding is not null)
            {
                return new DomainFaultException(
                    NormalizeExecutionVtId(OwnerThreadId),
                    pc: 0,
                    _lastOwnerBinding.OwnerDomainTag,
                    _lastOwnerBinding.OwnerDomainTag);
            }

            return new InvalidOperationException(result.Message);
        }

        private static ResourceBitset BuildResourceMask(ulong ownerDomainTag)
        {
            int resourceDomainBucket = (int)(ownerDomainTag & 0xFUL);

            return ResourceMaskBuilder.ForDMAChannel(0)
                   | ResourceMaskBuilder.ForStreamEngine(0)
                   | ResourceMaskBuilder.ForMemoryDomain(resourceDomainBucket);
        }
    }
}
