namespace YAKSys_Hybrid_CPU.Core;

public enum MemoryTranslationAuthority : byte
{
    RuntimeOwned = 0,
    CompatibilityProjection = 1,
}

[System.Flags]
public enum TranslationInvalidationPermission : byte
{
    None = 0,
    Address = 1 << 0,
    AddressSpace = 1 << 1,
    Global = 1 << 2,
}

public sealed partial class MemoryTranslationPolicy
{
    public MemoryTranslationPolicy()
        : this(
            authority: MemoryTranslationAuthority.RuntimeOwned,
            invalidationPermissions: TranslationInvalidationPermission.None,
            requireFenceBeforeInvalidation: true,
            allowCompatibilityProjection: false)
    {
    }

    public MemoryTranslationPolicy(
        MemoryTranslationAuthority authority,
        TranslationInvalidationPermission invalidationPermissions,
        bool requireFenceBeforeInvalidation,
        bool allowCompatibilityProjection)
    {
        Authority = authority;
        InvalidationPermissions = invalidationPermissions;
        RequireFenceBeforeInvalidation = requireFenceBeforeInvalidation;
        AllowCompatibilityProjection = allowCompatibilityProjection;
    }

    public MemoryTranslationAuthority Authority { get; }

    public TranslationInvalidationPermission InvalidationPermissions { get; }

    public bool RequireFenceBeforeInvalidation { get; }

    public bool AllowCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative => Authority == MemoryTranslationAuthority.RuntimeOwned;

    public bool AllowsInvalidation(TranslationInvalidationPermission requestedPermission) =>
        IsRuntimeAuthoritative &&
        requestedPermission != TranslationInvalidationPermission.None &&
        (InvalidationPermissions & requestedPermission) == requestedPermission;

    public MemoryTranslationPolicy WithInvalidationPermissions(
        TranslationInvalidationPermission permissions) =>
        new(Authority, permissions, RequireFenceBeforeInvalidation, AllowCompatibilityProjection);
}
