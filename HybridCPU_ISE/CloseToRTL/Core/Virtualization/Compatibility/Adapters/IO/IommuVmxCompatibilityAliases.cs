// Description: Frozen VMX I/O aliases retained as denied compatibility vocabulary only.
using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Memory
{
    public static partial class IOMMU
    {
        public static bool VmxCompatibilityIoAliasesAreReadOnlyDenied => true;

        internal static void InitializeVmxDmaState()
        {
        }

        public static IommuDomainBinding BindVmxDomain(IommuDomainBinding binding) =>
            default;

        public static bool UnbindVmxDomain(ushort vmid, uint domainId, uint deviceId) =>
            false;

        public static bool TryGetVmxDomainBinding(
            ushort vmid,
            uint domainId,
            uint deviceId,
            out IommuDomainBinding binding)
        {
            binding = default;
            return false;
        }

        public static bool TryTranslateVmxDma(
            IommuDomainBinding binding,
            ulong ioVirtualAddress,
            ulong accessSize,
            IOMMUAccessPermissions requestedPermissions,
            out DmaTranslationResult result)
        {
            result = default;
            return false;
        }

        public static int CountVmxIotlbEntries() =>
            0;

        public static int InvalidateVmxIotlbAll() =>
            0;

        public static int InvalidateVmxIotlbByVmid(ushort vmid) =>
            0;

        public static int InvalidateVmxIotlbByDomain(ushort vmid, uint domainId) =>
            0;

        public static int InvalidateVmxIotlbByDevice(ushort vmid, uint domainId, uint deviceId) =>
            0;

        public static int InvalidateVmxIotlbByEpoch(
            ushort vmid,
            uint domainId,
            uint deviceId,
            ulong domainEpoch) =>
            0;

        public static bool TryAccountNptWriteProtectDirty(
            NestedTranslationResult translation,
            ulong accessSize,
            out VmxDirtyLogStatus status)
        {
            status = default;
            return false;
        }

        public static int ApplyVmxInvalidation(
            VmxInvalidationScope scope,
            ulong descriptor,
            bool isEpt,
            bool epochWrapped = false)
        {
            return 0;
        }
    }
}
