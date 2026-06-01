using System;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Lane6 capability query for the Phase 07A DSC_QUERY_CAPS contour.
    /// It captures a bounded runtime-owned capability word at execute and
    /// publishes the scalar result only through retire/writeback.
    /// </summary>
    public sealed class DmaStreamComputeQueryCapsMicroOp : MicroOp
    {
        private const uint Lane6QueryOwnerPodId = 0;

        private DmaStreamComputeCapabilityQueryResult? _lastQueryResult;
        private DmaStreamComputeOwnerBinding? _lastOwnerBinding;
        private ulong _capturedCapabilityWord;

        public DmaStreamComputeQueryCapsMicroOp(ushort destinationRegister)
        {
            if (!TryNormalizeFlatArchRegId(destinationRegister, out int destRegId))
            {
                throw new DecodeProjectionFaultException(
                    "DSC_QUERY_CAPS requires a flat architectural rd register id.");
            }

            OpCode = Processor.CPU_Core.IsaOpcodeValues.DSC_QUERY_CAPS;
            DestinationRegister = (ushort)destRegId;
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

        public DmaStreamComputeCapabilityQueryResult? LastQueryResult => _lastQueryResult;

        public DmaStreamComputeOwnerBinding? LastOwnerBinding => _lastOwnerBinding;

        public DmaStreamComputeCapabilityQueryReplayEvidence LastQueryReplayEvidence { get; private set; }

        public bool UsedStreamEngineFallback => false;

        public bool UsedDmaControllerFallback => false;

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public override bool Execute(ref Processor.CPU_Core core)
        {
            RefreshOwnerDependentMetadata();

            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            _lastOwnerBinding = BuildOwnerBinding(ref core, vtId);
            _lastQueryResult = DmaStreamComputeCapabilityQueryResult.Capture(_lastOwnerBinding);
            _capturedCapabilityWord = _lastQueryResult.EncodedCapabilityWord;
            LastQueryReplayEvidence = DmaStreamComputeCapabilityQueryReplayEvidence.Capture(
                _lastOwnerBinding,
                _lastQueryResult,
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
            _ = _lastQueryResult ?? throw new InvalidOperationException(
                "DSC_QUERY_CAPS retire requires a prior execute/capture result.");

            if (!WritesRegister)
            {
                return;
            }

            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            AppendWriteBackRetireRecord(
                retireRecords,
                ref retireRecordCount,
                RetireRecord.RegisterWrite(vtId, DestinationRegister, _capturedCapabilityWord));
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _capturedCapabilityWord;
            return _lastQueryResult?.IsAccepted == true && WritesRegister;
        }

        public override void CapturePrimaryWriteBackResult(ulong value) =>
            _capturedCapabilityWord = value;

        public override void RefreshWriteMetadata() => RefreshOwnerDependentMetadata();

        public void RefreshOwnerDependentMetadata()
        {
            ReadRegisters = Array.Empty<int>();
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
            $"DSC_QUERY_CAPS: rd=x{DestinationRegister}";

        private DmaStreamComputeOwnerBinding BuildOwnerBinding(
            ref Processor.CPU_Core core,
            int vtId)
        {
            if (OwnerContextId < 0)
            {
                throw new InvalidOperationException(
                    "DSC_QUERY_CAPS owner context id is not materialized.");
            }

            return new DmaStreamComputeOwnerBinding
            {
                OwnerVirtualThreadId = (ushort)vtId,
                OwnerContextId = (uint)OwnerContextId,
                OwnerCoreId = core.CoreID,
                OwnerPodId = Lane6QueryOwnerPodId,
                OwnerDomainTag = Placement.DomainTag,
                DeviceId = DmaStreamComputeDescriptor.CanonicalLane6DeviceId
            };
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
