// Description: Host memory backend that performs generic translation invalidation after descriptor/policy admission has already succeeded.
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.CloseToRTL.Memory.MMU;

public sealed class TranslationInvalidationHostBackend : ITranslationInvalidationBackend
{
    public static TranslationInvalidationHostBackend Instance { get; } = new();

    private TranslationInvalidationHostBackend()
    {
    }

    public int Invalidate(TranslationInvalidationBackendRequest request) =>
        IOMMU.ApplyTranslationInvalidation(
            request.Scope,
            request.Descriptor,
            request.GuestPhysicalAddress,
            request.IsSecondStageRoot);
}
