using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Memory
{
    public static partial class NestedPageWalker
    {
        public static bool TranslateNested(
            ulong addressSpaceRoot,
            ulong secondStageRoot,
            ulong guestVirtualAddress,
            out ulong hostPhysicalAddress,
            out byte permissions,
            Processor.MainMemoryArea? mainMemory = null)
        {
            var domainControl = new MemoryDomainTranslationControl(
                TranslationEnabled: true,
                AddressSpaceTaggingEnabled: false,
                AddressSpaceRoot: addressSpaceRoot,
                SecondStageRoot: secondStageRoot,
                DomainTag: 0,
                AddressSpaceTag: 0,
                AddressSpaceGeneration: 0,
                DefaultMemoryType: MemoryDomainTranslationControl.WriteBackMemoryType);

            NestedTranslationResult result = TranslateNestedDetailed(
                domainControl,
                guestVirtualAddress,
                NestedMemoryAccessType.Read,
                secondStageEpoch: 0,
                addressSpaceTagEpoch: 0,
                mainMemory);

            hostPhysicalAddress = result.HostPhysicalAddress;
            permissions = result.Permissions;
            return result.Succeeded;
        }

        public static NestedTranslationResult TranslateNestedDetailed(
            MemoryDomainTranslationControl control,
            ulong guestVirtualAddress,
            NestedMemoryAccessType accessType,
            ulong secondStageEpoch,
            ulong addressSpaceTagEpoch,
            Processor.MainMemoryArea? mainMemory = null)
        {
            Processor.MainMemoryArea resolvedMemory = mainMemory ?? Processor.MainMemory;

            if (!control.TranslationEnabled)
            {
                return NestedTranslationResult.SingleStage(
                    guestVirtualAddress,
                    guestVirtualAddress,
                    accessType,
                    ReadPermission | WritePermission | ExecutePermission);
            }

            if (!control.IsValid)
            {
                return NestedTranslationResult.SecondStageMisconfiguration(
                    guestVirtualAddress,
                    control.SecondStageRoot,
                    accessType,
                    pageWalkLevel: 0,
                    causedByPageWalk: false);
            }

            if (!WalkGuestPageTable(
                    control,
                    guestVirtualAddress,
                    accessType,
                    resolvedMemory,
                    out ulong guestPhysicalAddress,
                    out byte guestPerms,
                    out NestedTranslationResult fault))
            {
                return fault;
            }

            if (!WalkSecondStage(
                    control,
                    guestPhysicalAddress,
                    guestVirtualAddress,
                    accessType,
                    causedByPageWalk: false,
                    pageWalkLevel: 0,
                    resolvedMemory,
                    out ulong hostPhysicalAddress,
                    out byte secondStagePerms,
                    out byte memoryType,
                    out fault))
            {
                return fault;
            }

            byte permissions = (byte)(guestPerms & secondStagePerms);
            AddressSpaceId addressSpace = control.ToAddressSpaceId(
                secondStageEpoch,
                addressSpaceTagEpoch);
            NestedTlbTag tag = NestedTlbTag.Create(
                guestVirtualAddress,
                addressSpace,
                permissions,
                memoryType);

            return NestedTranslationResult.Success(
                guestVirtualAddress,
                guestPhysicalAddress,
                hostPhysicalAddress,
                accessType,
                permissions,
                memoryType,
                tag);
        }
    }
}
