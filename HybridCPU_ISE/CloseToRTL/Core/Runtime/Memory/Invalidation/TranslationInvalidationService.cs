using HybridCPU_ISE.CloseToRTL.Memory.MMU;

namespace YAKSys_Hybrid_CPU.Core;

public enum TranslationInvalidationScope : byte
{
    Address = 0,
    AddressSpace = 1,
    Global = 2,
}

public enum TranslationInvalidationDecision : byte
{
    Allowed = 0,
    MissingMemoryDomain = 1,
    MissingAddressSpace = 2,
    MissingTranslationPolicy = 3,
    RuntimeAuthorityMissing = 4,
    PermissionDenied = 5,
    RangeDenied = 6,
    FenceRequired = 7,
}

public readonly record struct TranslationInvalidationResult(
    TranslationInvalidationDecision Decision,
    int InvalidatedEntries,
    string Message)
{
    public bool IsAllowed => Decision == TranslationInvalidationDecision.Allowed;

    public static TranslationInvalidationResult Allowed(int invalidatedEntries) =>
        new(TranslationInvalidationDecision.Allowed, invalidatedEntries, string.Empty);

    public static TranslationInvalidationResult Denied(
        TranslationInvalidationDecision decision,
        string message) =>
        new(decision, 0, message);
}

public readonly record struct TranslationInvalidationBackendRequest(
    TranslationInvalidationScope Scope,
    ulong Descriptor,
    ulong GuestPhysicalAddress,
    ulong SizeBytes,
    bool IsSecondStageRoot);

public interface ITranslationInvalidationBackend
{
    int Invalidate(TranslationInvalidationBackendRequest request);
}

public sealed partial class TranslationInvalidationService
{
    private readonly ITranslationInvalidationBackend _backend;

    public TranslationInvalidationService()
        : this(TranslationInvalidationHostBackend.Instance)
    {
    }

    public TranslationInvalidationService(ITranslationInvalidationBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public TranslationInvalidationResult Invalidate(
        DomainRuntimeContext context,
        TranslationInvalidationScope scope,
        ulong descriptor,
        ulong guestPhysicalAddress = 0,
        ulong sizeBytes = 0,
        bool isSecondStageRoot = true,
        bool fenceSatisfied = false)
    {
        MemoryDomainDescriptor? memory = context.Memory;
        if (memory is null)
        {
            return Deny(
                TranslationInvalidationDecision.MissingMemoryDomain,
                "Translation invalidation requires a memory-domain descriptor.");
        }

        if (!memory.IsAuthoritativeMemoryStateOwner || !memory.OwnsSecondStageTranslation)
        {
            return Deny(
                TranslationInvalidationDecision.RuntimeAuthorityMissing,
                "Compatibility projection cannot own translation invalidation authority.");
        }

        AddressSpaceDescriptor? addressSpace = memory.AddressSpace;
        if (addressSpace is null)
        {
            return Deny(
                TranslationInvalidationDecision.MissingAddressSpace,
                "Translation invalidation requires a descriptor-owned address space.");
        }

        if (!addressSpace.IsRuntimeAuthoritative)
        {
            return Deny(
                TranslationInvalidationDecision.RuntimeAuthorityMissing,
                "Address-space projection cannot own invalidation authority.");
        }

        MemoryTranslationPolicy? policy = memory.TranslationPolicy;
        if (policy is null)
        {
            return Deny(
                TranslationInvalidationDecision.MissingTranslationPolicy,
                "Translation invalidation requires an explicit memory translation policy.");
        }

        TranslationInvalidationPermission permission = ToPermission(scope);
        if (!policy.AllowsInvalidation(permission))
        {
            return Deny(
                TranslationInvalidationDecision.PermissionDenied,
                "Memory translation policy denies the requested invalidation scope.");
        }

        if (policy.RequireFenceBeforeInvalidation && !fenceSatisfied)
        {
            return Deny(
                TranslationInvalidationDecision.FenceRequired,
                "Translation invalidation requires a satisfied fence.");
        }

        if (scope == TranslationInvalidationScope.Address &&
            !addressSpace.AllowsRange(guestPhysicalAddress, sizeBytes))
        {
            return Deny(
                TranslationInvalidationDecision.RangeDenied,
                "Address-space descriptor denies the requested invalidation range.");
        }

        int invalidated = _backend.Invalidate(
            new TranslationInvalidationBackendRequest(
                scope,
                descriptor,
                guestPhysicalAddress,
                sizeBytes,
                isSecondStageRoot));
        return TranslationInvalidationResult.Allowed(invalidated);
    }

    private static TranslationInvalidationResult Deny(
        TranslationInvalidationDecision decision,
        string message) =>
        TranslationInvalidationResult.Denied(decision, message);

    private static TranslationInvalidationPermission ToPermission(
        TranslationInvalidationScope scope) =>
        scope switch
        {
            TranslationInvalidationScope.Address => TranslationInvalidationPermission.Address,
            TranslationInvalidationScope.AddressSpace => TranslationInvalidationPermission.AddressSpace,
            TranslationInvalidationScope.Global => TranslationInvalidationPermission.Global,
            _ => TranslationInvalidationPermission.None,
        };

}
