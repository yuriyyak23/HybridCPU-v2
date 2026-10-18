// Description: Generic host-side translation invalidation entrypoint used after runtime memory-domain policy has admitted a request.
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Memory;

public static partial class IOMMU
{
    public static int ApplyTranslationInvalidation(
        TranslationInvalidationScope scope,
        ulong descriptor,
        ulong guestPhysicalAddress,
        bool isSecondStageRoot,
        bool epochWrapped = false) =>
        ApplyIoDomainInvalidation(
            scope,
            descriptor,
            isSecondStageRoot,
            epochWrapped);
}
