using System;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    [Flags]
    public enum DmaStreamComputeCapabilityFlags : ulong
    {
        None = 0,
        Lane6DmaStreamBackend = 1UL << 0,
        CurrentDsc1Production = 1UL << 1,
        DscStatusQuery = 1UL << 2,
        Dsc2ParserFootprintFoundation = 1UL << 3,
        DscQueryCaps = 1UL << 4
    }

    public sealed record DmaStreamComputeCapabilityQueryResult
    {
        private DmaStreamComputeCapabilityQueryResult(
            DmaStreamComputeOwnerBinding ownerBinding,
            DmaStreamComputeCapabilityFlags capabilityFlags,
            string message)
        {
            OwnerBinding = ownerBinding;
            CapabilityFlags = capabilityFlags;
            Message = message;
        }

        public DmaStreamComputeOwnerBinding OwnerBinding { get; }

        public DmaStreamComputeCapabilityFlags CapabilityFlags { get; }

        public ulong EncodedCapabilityWord => (ulong)CapabilityFlags;

        public string Message { get; }

        public bool IsAccepted => true;

        public bool CanIssueToken => false;

        public bool CanPublishMemory => false;

        public bool CanProductionLower => false;

        public static DmaStreamComputeCapabilityQueryResult Capture(
            DmaStreamComputeOwnerBinding ownerBinding)
        {
            ArgumentNullException.ThrowIfNull(ownerBinding);
            return new DmaStreamComputeCapabilityQueryResult(
                ownerBinding,
                DmaStreamComputeCapabilityFlags.Lane6DmaStreamBackend |
                DmaStreamComputeCapabilityFlags.CurrentDsc1Production |
                DmaStreamComputeCapabilityFlags.DscStatusQuery |
                DmaStreamComputeCapabilityFlags.Dsc2ParserFootprintFoundation |
                DmaStreamComputeCapabilityFlags.DscQueryCaps,
                "DSC_QUERY_CAPS captured the current lane6 DMA/stream capability word; execution authority, token issue, memory publication, and compiler lowering are unchanged.");
        }
    }

    public readonly record struct DmaStreamComputeCapabilityQueryReplayEvidence
    {
        public DmaStreamComputeCapabilityQueryReplayEvidence(
            uint opcode,
            DmaStreamComputeOwnerBinding ownerBinding,
            ulong encodedCapabilityWord,
            bool ownerBindingRevalidated,
            DmaStreamComputeLanePlacementEvidence lanePlacementEvidence,
            ulong evidenceHash)
        {
            Opcode = opcode;
            OwnerBinding = ownerBinding;
            EncodedCapabilityWord = encodedCapabilityWord;
            OwnerBindingRevalidated = ownerBindingRevalidated;
            LanePlacementEvidence = lanePlacementEvidence;
            EvidenceHash = evidenceHash;
        }

        public uint Opcode { get; }

        public DmaStreamComputeOwnerBinding OwnerBinding { get; }

        public ulong EncodedCapabilityWord { get; }

        public bool OwnerBindingRevalidated { get; }

        public DmaStreamComputeLanePlacementEvidence LanePlacementEvidence { get; }

        public ulong EvidenceHash { get; }

        public bool IsComplete =>
            Opcode == Processor.CPU_Core.IsaOpcodeValues.DSC_QUERY_CAPS &&
            EncodedCapabilityWord != 0 &&
            OwnerBindingRevalidated &&
            LanePlacementEvidence.IsComplete &&
            EvidenceHash != 0;

        public static DmaStreamComputeCapabilityQueryReplayEvidence Capture(
            DmaStreamComputeOwnerBinding ownerBinding,
            DmaStreamComputeCapabilityQueryResult result,
            DmaStreamComputeLanePlacementEvidence lanePlacementEvidence)
        {
            ArgumentNullException.ThrowIfNull(ownerBinding);
            ArgumentNullException.ThrowIfNull(result);

            bool ownerBindingRevalidated =
                result.OwnerBinding.Equals(ownerBinding) &&
                ownerBinding.DeviceId == DmaStreamComputeDescriptor.CanonicalLane6DeviceId;
            ulong encodedCapabilityWord = result.EncodedCapabilityWord;

            ulong hash = 14695981039346656037UL;
            Add(Processor.CPU_Core.IsaOpcodeValues.DSC_QUERY_CAPS);
            Add(encodedCapabilityWord);
            Add(ownerBinding.OwnerVirtualThreadId);
            Add(ownerBinding.OwnerContextId);
            Add(ownerBinding.OwnerCoreId);
            Add(ownerBinding.OwnerPodId);
            Add(ownerBinding.OwnerDomainTag);
            Add(ownerBinding.DeviceId);
            Add(ownerBindingRevalidated ? 1UL : 0UL);
            Add(lanePlacementEvidence.EvidenceHash);

            return new DmaStreamComputeCapabilityQueryReplayEvidence(
                Processor.CPU_Core.IsaOpcodeValues.DSC_QUERY_CAPS,
                ownerBinding,
                encodedCapabilityWord,
                ownerBindingRevalidated,
                lanePlacementEvidence,
                hash == 0 ? 0xD5C0_07A0UL : hash);

            void Add(ulong value)
            {
                unchecked
                {
                    hash ^= value;
                    hash *= 1099511628211UL;
                }
            }
        }
    }
}
