using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    public interface IIoVirtualizationBackend
    {
        ulong LastIotlbInvalidationEpoch { get; }

        IommuDomainBinding BindDomain(IommuDomainBinding binding);

        void UnbindDomain(ushort ioDomainTag, uint domainId, uint deviceId);

        int InvalidateIotlbAll();

        int InvalidateIotlbByIoDomainTag(ushort ioDomainTag);

        int InvalidateIotlbByIoDomain(ushort ioDomainTag, uint domainId);

        int InvalidateIotlbByDevice(ushort ioDomainTag, uint domainId, uint deviceId);
    }

    public sealed class IoVirtualizationBlock
    {
        private readonly Dictionary<(ushort IoDomainTag, ulong DomainTag, uint DeviceId), IommuDomainBinding> _bindings = new();
        private readonly IIoVirtualizationBackend _backend;

        public IoVirtualizationBlock()
            : this(IoVirtualizationHostBackend.Instance)
        {
        }

        public IoVirtualizationBlock(IIoVirtualizationBackend backend)
        {
            _backend = backend ?? throw new System.ArgumentNullException(nameof(backend));
        }

        public ulong OwnershipEpoch { get; private set; }

        public ulong PolicyEpoch { get; private set; }

        public ulong LastIotlbInvalidationEpoch => _backend.LastIotlbInvalidationEpoch;

        public IommuDomainBinding BindDomain(IommuDomainBinding binding)
        {
            IommuDomainBinding effective = _backend.BindDomain(binding);
            if (!effective.IsValid)
            {
                return default;
            }

            _bindings[(effective.IoDomainTag, effective.DomainTag, effective.DeviceId)] = effective;
            AdvanceOwnershipEpoch();
            return effective;
        }

        public bool UnbindDomain(ushort ioDomainTag, ulong domainTag, uint deviceId)
        {
            if (!_bindings.TryGetValue((ioDomainTag, domainTag, deviceId), out IommuDomainBinding binding))
            {
                return false;
            }

            bool removed = _bindings.Remove((ioDomainTag, domainTag, deviceId));
            _backend.UnbindDomain(binding.IoDomainTag, binding.DomainId, binding.DeviceId);
            if (removed)
            {
                AdvanceOwnershipEpoch();
            }

            return removed;
        }

        public bool TryResolveBinding(
            ushort ioDomainTag,
            ulong domainTag,
            uint deviceId,
            out IommuDomainBinding binding) =>
            _bindings.TryGetValue((ioDomainTag, domainTag, deviceId), out binding);

        public bool TryResolveDescriptorBinding(
            ushort ioDomainTag,
            DmaStreamComputeOwnerBinding ownerBinding,
            out IommuDomainBinding binding)
        {
            return TryResolveBinding(
                ioDomainTag,
                ownerBinding.OwnerDomainTag,
                ownerBinding.DeviceId,
                out binding);
        }

        public int InvalidateIotlbAll()
        {
            int invalidated = _backend.InvalidateIotlbAll();
            if (invalidated != 0)
            {
                AdvancePolicyEpoch();
            }

            return invalidated;
        }

        public int InvalidateIotlbByIoDomainTag(ushort ioDomainTag)
        {
            int invalidated = _backend.InvalidateIotlbByIoDomainTag(ioDomainTag);
            if (invalidated != 0)
            {
                AdvancePolicyEpoch();
            }

            return invalidated;
        }

        public int InvalidateIotlbByIoDomain(ushort ioDomainTag, uint domainId)
        {
            int invalidated = _backend.InvalidateIotlbByIoDomain(ioDomainTag, domainId);
            if (invalidated != 0)
            {
                AdvancePolicyEpoch();
            }

            return invalidated;
        }

        public int InvalidateIotlbByDevice(ushort ioDomainTag, uint domainId, uint deviceId)
        {
            int invalidated = _backend.InvalidateIotlbByDevice(ioDomainTag, domainId, deviceId);
            if (invalidated != 0)
            {
                AdvancePolicyEpoch();
            }

            return invalidated;
        }

        private void AdvanceOwnershipEpoch()
        {
            unchecked
            {
                OwnershipEpoch++;
                if (OwnershipEpoch == 0)
                {
                    OwnershipEpoch = 1;
                }
            }
        }

        private void AdvancePolicyEpoch()
        {
            unchecked
            {
                PolicyEpoch++;
                if (PolicyEpoch == 0)
                {
                    PolicyEpoch = 1;
                }
            }
        }
    }
}
