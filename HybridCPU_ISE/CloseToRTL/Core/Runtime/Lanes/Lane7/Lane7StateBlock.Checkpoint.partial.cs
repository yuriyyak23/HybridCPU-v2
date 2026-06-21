using System;

namespace YAKSys_Hybrid_CPU.Core
{
    public sealed partial class Lane7StateBlock
    {
        public Lane7Checkpoint CreateCheckpoint()
        {
            return new Lane7Checkpoint(
                ExecutionDomainTag,
                AddressSpaceTag,
                VirtualizationEnabled,
                Array.AsReadOnly([.. _handlesByValue.Values]),
                Array.AsReadOnly([.. _tokensByVirtualValue.Values]),
                OwnershipEpoch,
                TokenEpoch,
                CompletionEpoch);
        }

        public void RestoreCheckpoint(Lane7Checkpoint checkpoint)
        {
            ArgumentNullException.ThrowIfNull(checkpoint);

            _handlesByValue.Clear();
            _handleByOwner.Clear();
            _tokensByVirtualValue.Clear();
            HostEvidence.PrepareForRestore(EvidencePolicyDescriptor.FailClosed);

            ExecutionDomainTag = checkpoint.ExecutionDomainTag;
            AddressSpaceTag = checkpoint.AddressSpaceTag;
            VirtualizationEnabled = checkpoint.VirtualizationEnabled;
            OwnershipEpoch = checkpoint.OwnershipEpoch;
            TokenEpoch = checkpoint.TokenEpoch;
            CompletionEpoch = checkpoint.CompletionEpoch;
            ulong nextHandle = 0x7000_0000_0000_0001UL;
            for (int index = 0; index < checkpoint.VirtualHandles.Count; index++)
            {
                Lane7VirtualHandle handle = checkpoint.VirtualHandles[index];
                if (!handle.IsValid)
                {
                    continue;
                }

                _handlesByValue[handle.Value] = handle;
                _handleByOwner[(handle.ExecutionDomainTag, handle.OwnerVirtualThreadId, handle.AcceleratorId)] =
                    handle.Value;
                if (handle.Value >= nextHandle)
                {
                    nextHandle = handle.Value + 1;
                }
            }

            ulong nextToken = 0x7100_0000_0000_0001UL;
            for (int index = 0; index < checkpoint.VirtualTokens.Count; index++)
            {
                Lane7VirtualToken token = checkpoint.VirtualTokens[index];
                if (!token.IsValid)
                {
                    continue;
                }

                _tokensByVirtualValue[token.VirtualTokenId] = token;
                if (token.VirtualTokenId >= nextToken)
                {
                    nextToken = token.VirtualTokenId + 1;
                }
            }

            _nextVirtualHandle = nextHandle;
            _nextVirtualToken = nextToken;
        }
    }
}
