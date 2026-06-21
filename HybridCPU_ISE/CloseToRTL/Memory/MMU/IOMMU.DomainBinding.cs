using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using System;
// Description: Generic host-side I/O-domain binding, DMA translation, and IOTLB state for IOMMU.
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Memory
{
    public static partial class IOMMU
    {
        private readonly record struct IoDomainKey(
            ushort IoDomainTag,
            uint DomainId,
            uint DeviceId);

        private readonly record struct IotlbEntry(
            IotlbTag Tag,
            ulong HostPhysicalPageNumber,
            byte PermissionBits);

        private static Dictionary<IoDomainKey, IommuDomainBinding>? _ioDomainBindings;
        private static Dictionary<IotlbTag, IotlbEntry>? _iotlb;
        private static ulong _nextIoDomainEpoch = 1;
        private static ulong _iotlbMappingEpoch = 1;
        private static ulong _iotlbInvalidationEpoch = 1;

        public static ulong IotlbMappingEpoch => _iotlbMappingEpoch;

        public static ulong IotlbInvalidationEpoch => _iotlbInvalidationEpoch;

        public static ulong IotlbHits { get; private set; }

        public static ulong IotlbMisses { get; private set; }

        internal static void InitializeIoDomainDmaState()
        {
            _ioDomainBindings = new Dictionary<IoDomainKey, IommuDomainBinding>();
            _iotlb = new Dictionary<IotlbTag, IotlbEntry>();
            _nextIoDomainEpoch = 1;
            _iotlbMappingEpoch = 1;
            _iotlbInvalidationEpoch = 1;
            IotlbHits = 0;
            IotlbMisses = 0;
        }

        public static IommuDomainBinding BindIoDomain(IommuDomainBinding binding)
        {
            EnsureIoDomainDmaState();
            IommuDomainBinding effective = binding.DomainEpoch == 0
                ? binding.WithEpoch(AllocateIoDomainEpoch())
                : binding;

            if (!effective.IsValid)
            {
                return default;
            }

            _ioDomainBindings![new IoDomainKey(
                effective.IoDomainTag,
                effective.DomainId,
                effective.DeviceId)] = effective;
            InvalidateIotlbByDevice(effective.IoDomainTag, effective.DomainId, effective.DeviceId);
            return effective;
        }

        public static bool UnbindIoDomain(ushort ioDomainTag, uint domainId, uint deviceId)
        {
            EnsureIoDomainDmaState();
            bool removed = _ioDomainBindings!.Remove(new IoDomainKey(ioDomainTag, domainId, deviceId));
            if (removed)
            {
                InvalidateIotlbByDevice(ioDomainTag, domainId, deviceId);
            }

            return removed;
        }

        public static bool TryGetIoDomainBinding(
            ushort ioDomainTag,
            uint domainId,
            uint deviceId,
            out IommuDomainBinding binding)
        {
            EnsureIoDomainDmaState();
            return _ioDomainBindings!.TryGetValue(
                new IoDomainKey(ioDomainTag, domainId, deviceId),
                out binding);
        }

        public static bool TryTranslateDma(
            IommuDomainBinding binding,
            ulong ioVirtualAddress,
            ulong accessSize,
            IOMMUAccessPermissions requestedPermissions,
            out DmaTranslationResult result)
        {
            EnsureIoDomainDmaState();

            if (!binding.IsValid ||
                !TryGetIoDomainBinding(binding.IoDomainTag, binding.DomainId, binding.DeviceId, out IommuDomainBinding current) ||
                current.DomainTag != binding.DomainTag ||
                current.DomainEpoch != binding.DomainEpoch)
            {
                result = DmaTranslationResult.Fail(
                    ioVirtualAddress,
                    accessSize,
                    requestedPermissions,
                    DmaFault.Abort(
                        DmaFaultKind.MissingDomainBinding,
                        ioVirtualAddress,
                        (requestedPermissions & IOMMUAccessPermissions.Write) != 0,
                        "DMA translation requires an active I/O-domain tag/domain/device binding."));
                return false;
            }

            if (accessSize == 0 ||
                ioVirtualAddress > ulong.MaxValue - (accessSize - 1))
            {
                result = DmaTranslationResult.Fail(
                    ioVirtualAddress,
                    accessSize,
                    requestedPermissions,
                    DmaFault.Abort(
                        DmaFaultKind.DescriptorFault,
                        ioVirtualAddress,
                        (requestedPermissions & IOMMUAccessPermissions.Write) != 0,
                        "DMA descriptor range is empty or overflows the IOVA space."));
                return false;
            }

            bool isWrite = (requestedPermissions & IOMMUAccessPermissions.Write) != 0;
            if (!current.Allows(requestedPermissions))
            {
                result = DmaTranslationResult.Fail(
                    ioVirtualAddress,
                    accessSize,
                    requestedPermissions,
                    DmaFault.Abort(
                        DmaFaultKind.PermissionFault,
                        ioVirtualAddress,
                        isWrite,
                        "DMA I/O-domain permissions reject the requested descriptor access."));
                return false;
            }

            IotlbTag tag = IotlbTag.Create(current, ioVirtualAddress, requestedPermissions, _iotlbMappingEpoch);
            ulong offset = ioVirtualAddress & 0xFFFUL;
            if (_iotlb!.TryGetValue(tag, out IotlbEntry entry) &&
                tag.Matches(current, ioVirtualAddress, requestedPermissions, _iotlbMappingEpoch))
            {
                IotlbHits++;
                ulong hostPhysicalAddress = (entry.HostPhysicalPageNumber << 12) | offset;
                result = DmaTranslationResult.Success(
                    iotlbHit: true,
                    ioVirtualAddress,
                    hostPhysicalAddress,
                    accessSize,
                    requestedPermissions,
                    tag);
                return true;
            }

            IotlbMisses++;
            if (!TranslateAndValidateAccess(
                    current.DeviceId,
                    ioVirtualAddress,
                    accessSize,
                    requestedPermissions,
                    out ulong physicalAddress))
            {
                result = DmaTranslationResult.Fail(
                    ioVirtualAddress,
                    accessSize,
                    requestedPermissions,
                    DmaFault.Abort(
                        DmaFaultKind.IommuTranslationFault,
                        ioVirtualAddress,
                        isWrite,
                        "DMA IOTLB miss could not be resolved by the IOMMU page tables."));
                return false;
            }

            byte permissionBits = ToIotlbPermissionBits(requestedPermissions);
            _iotlb[tag] = new IotlbEntry(
                tag,
                physicalAddress >> 12,
                permissionBits);

            result = DmaTranslationResult.Success(
                iotlbHit: false,
                ioVirtualAddress,
                physicalAddress,
                accessSize,
                requestedPermissions,
                tag);
            return true;
        }

        public static int CountIotlbEntries()
        {
            EnsureIoDomainDmaState();
            return _iotlb!.Count;
        }

        public static int InvalidateIotlbAll()
        {
            EnsureIoDomainDmaState();
            int count = _iotlb!.Count;
            _iotlb.Clear();
            AdvanceIotlbInvalidationEpoch();
            return count;
        }

        public static int InvalidateIotlbByIoDomainTag(ushort ioDomainTag)
        {
            EnsureIoDomainDmaState();
            return InvalidateIotlbWhere(tag => tag.IoDomainTag == ioDomainTag);
        }

        public static int InvalidateIotlbByIoDomain(ushort ioDomainTag, uint domainId)
        {
            EnsureIoDomainDmaState();
            return InvalidateIotlbWhere(tag => tag.IoDomainTag == ioDomainTag && tag.DomainId == domainId);
        }

        public static int InvalidateIotlbByDevice(ushort ioDomainTag, uint domainId, uint deviceId)
        {
            EnsureIoDomainDmaState();
            return InvalidateIotlbWhere(
                tag => tag.IoDomainTag == ioDomainTag &&
                       tag.DomainId == domainId &&
                       tag.DeviceId == deviceId);
        }

        public static int InvalidateIotlbByEpoch(
            ushort ioDomainTag,
            uint domainId,
            uint deviceId,
            ulong domainEpoch)
        {
            EnsureIoDomainDmaState();
            return InvalidateIotlbWhere(
                tag => tag.IoDomainTag == ioDomainTag &&
                       tag.DomainId == domainId &&
                       tag.DeviceId == deviceId &&
                       tag.DomainEpoch == domainEpoch);
        }

        public static bool TranslateGuestAccess(
            ulong deviceID,
            ulong guestVirtualAddress,
            ulong accessSize,
            NestedMemoryAccessType accessType,
            MemoryDomainTranslationControl domainControl,
            out ulong physicalAddress,
            out NestedTranslationResult translation,
            ulong secondStageEpoch = 0,
            ulong addressSpaceTagEpoch = 0,
            Processor.MainMemoryArea? mainMemory = null)
        {
            physicalAddress = 0;
            if (accessSize == 0)
            {
                translation = NestedTranslationResult.GuestPageFault(
                    guestVirtualAddress,
                    0,
                    accessType,
                    pageWalkLevel: 0);
                return false;
            }

            if (!domainControl.TranslationEnabled)
            {
                IOMMUAccessPermissions requestedPermissions = ToIommuPermissions(accessType);
                bool singleStage = TranslateAndValidateAccess(
                    deviceID,
                    guestVirtualAddress,
                    accessSize,
                    requestedPermissions,
                    out physicalAddress);

                translation = singleStage
                    ? NestedTranslationResult.SingleStage(
                        guestVirtualAddress,
                        physicalAddress,
                        accessType,
                        ToNestedPermissionBits(requestedPermissions))
                    : NestedTranslationResult.GuestPageFault(
                        guestVirtualAddress,
                        0,
                        accessType,
                        pageWalkLevel: 0);
                return singleStage;
            }

            if (!domainControl.IsValid)
            {
                translation = NestedTranslationResult.SecondStageMisconfiguration(
                    guestVirtualAddress,
                    domainControl.SecondStageRoot,
                    accessType,
                    pageWalkLevel: 0,
                    causedByPageWalk: false);
                return false;
            }

            AddressSpaceId addressSpace = domainControl.ToAddressSpaceId(secondStageEpoch, addressSpaceTagEpoch);
            if (_tlb.TryTranslateNested(
                    guestVirtualAddress,
                    addressSpace,
                    out physicalAddress,
                    out ulong guestPhysicalAddress,
                    out byte cachedPermissions,
                    out byte cachedMemoryType,
                    out NestedTlbTag cachedTag))
            {
                if (NestedPageWalker.HasAccessPermission(cachedPermissions, accessType))
                {
                    translation = NestedTranslationResult.Success(
                        guestVirtualAddress,
                        guestPhysicalAddress,
                        physicalAddress,
                        accessType,
                        cachedPermissions,
                        cachedMemoryType,
                        cachedTag);
                    return true;
                }

                physicalAddress = 0;
                translation = NestedTranslationResult.SecondStageViolation(
                    guestVirtualAddress,
                    guestPhysicalAddress,
                    accessType,
                    pageWalkLevel: 0,
                    causedByPageWalk: false);
                return false;
            }

            translation = NestedPageWalker.TranslateNestedDetailed(
                domainControl,
                guestVirtualAddress,
                accessType,
                secondStageEpoch,
                addressSpaceTagEpoch,
                mainMemory);

            if (!translation.Succeeded)
            {
                physicalAddress = 0;
                return false;
            }

            physicalAddress = translation.HostPhysicalAddress;
            _tlb.InsertNested(
                guestVirtualAddress,
                translation.GuestPhysicalAddress,
                translation.HostPhysicalAddress,
                translation.Permissions,
                translation.MemoryType,
                addressSpace);
            return true;
        }

        public static bool TranslateAndValidateAccess(
            ulong deviceID,
            ulong ioVirtualAddress,
            ulong accessSize,
            IOMMUAccessPermissions requestedPermissions,
            MemoryDomainTranslationControl domainControl,
            out ulong physicalAddress,
            out NestedTranslationResult translation,
            ulong secondStageEpoch = 0,
            ulong addressSpaceTagEpoch = 0,
            Processor.MainMemoryArea? mainMemory = null)
        {
            NestedMemoryAccessType accessType =
                (requestedPermissions & IOMMUAccessPermissions.Write) != 0
                    ? NestedMemoryAccessType.Write
                    : NestedMemoryAccessType.Read;

            return TranslateGuestAccess(
                deviceID,
                ioVirtualAddress,
                accessSize,
                accessType,
                domainControl,
                out physicalAddress,
                out translation,
                secondStageEpoch,
                addressSpaceTagEpoch,
                mainMemory);
        }

        public static int ApplyIoDomainInvalidation(
            TranslationInvalidationScope scope,
            ulong descriptor,
            bool isSecondStageRoot,
            bool epochWrapped = false)
        {
            if (epochWrapped || scope == TranslationInvalidationScope.Global)
            {
                InvalidateIotlbAll();
                return _tlb.FlushNestedAll();
            }

            if (scope == TranslationInvalidationScope.AddressSpace)
            {
                if (!isSecondStageRoot)
                {
                    InvalidateIotlbByIoDomainTag(unchecked((ushort)descriptor));
                }

                return isSecondStageRoot
                    ? _tlb.FlushNestedBySecondStageRoot(descriptor)
                    : _tlb.FlushNestedByAddressSpaceTag(unchecked((ushort)descriptor));
            }

            if (scope == TranslationInvalidationScope.Address)
            {
                return isSecondStageRoot
                    ? _tlb.FlushNestedBySecondStageRoot(descriptor)
                    : _tlb.FlushNestedByAddressSpaceTag(unchecked((ushort)descriptor));
            }

            return 0;
        }

        public static int CountNestedTlbEntries() => _tlb.CountNestedEntries();

        private static void EnsureIoDomainDmaState()
        {
            if (_ioDomainBindings is null || _iotlb is null)
            {
                InitializeIoDomainDmaState();
            }
        }

        private static ulong AllocateIoDomainEpoch()
        {
            ulong epoch = _nextIoDomainEpoch++;
            if (_nextIoDomainEpoch == 0)
            {
                _nextIoDomainEpoch = 1;
            }

            return epoch == 0 ? 1 : epoch;
        }

        private static int InvalidateIotlbWhere(Func<IotlbTag, bool> predicate)
        {
            List<IotlbTag> doomed = new();
            foreach (IotlbTag tag in _iotlb!.Keys)
            {
                if (predicate(tag))
                {
                    doomed.Add(tag);
                }
            }

            for (int i = 0; i < doomed.Count; i++)
            {
                _iotlb.Remove(doomed[i]);
            }

            if (doomed.Count != 0)
            {
                AdvanceIotlbInvalidationEpoch();
            }

            return doomed.Count;
        }

        private static void AdvanceIotlbInvalidationEpoch()
        {
            unchecked
            {
                _iotlbInvalidationEpoch++;
                _iotlbMappingEpoch++;
                if (_iotlbInvalidationEpoch == 0)
                {
                    _iotlbInvalidationEpoch = 1;
                }

                if (_iotlbMappingEpoch == 0)
                {
                    _iotlbMappingEpoch = 1;
                }
            }
        }

        private static byte ToIotlbPermissionBits(IOMMUAccessPermissions permissions)
        {
            byte bits = 0;
            if ((permissions & IOMMUAccessPermissions.Read) != 0)
            {
                bits |= 0x02;
            }

            if ((permissions & IOMMUAccessPermissions.Write) != 0)
            {
                bits |= 0x04;
            }

            return bits;
        }

        private static IOMMUAccessPermissions ToIommuPermissions(NestedMemoryAccessType accessType) =>
            accessType == NestedMemoryAccessType.Write
                ? IOMMUAccessPermissions.Write
                : IOMMUAccessPermissions.Read;

        private static byte ToNestedPermissionBits(IOMMUAccessPermissions permissions)
        {
            byte bits = 0;
            if ((permissions & IOMMUAccessPermissions.Read) != 0)
            {
                bits |= NestedPageWalker.ReadPermission;
            }

            if ((permissions & IOMMUAccessPermissions.Write) != 0)
            {
                bits |= NestedPageWalker.WritePermission;
            }

            return bits;
        }
    }
}
