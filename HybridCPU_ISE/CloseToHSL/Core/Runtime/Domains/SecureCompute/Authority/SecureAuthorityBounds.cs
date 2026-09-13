namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class SecureAuthorityBounds
{
    public SecureAuthorityBounds()
        : this(
            allowsPrivateMemory: false,
            allowsSharedMemory: false,
            allowsIo: false,
            allowsDma: false,
            allowsHypercalls: false,
            allowsDebug: false,
            allowsMigration: false,
            allowsCompatibilityProjection: false)
    {
    }

    public SecureAuthorityBounds(
        bool allowsPrivateMemory,
        bool allowsSharedMemory,
        bool allowsIo,
        bool allowsDma,
        bool allowsHypercalls,
        bool allowsDebug,
        bool allowsMigration,
        bool allowsCompatibilityProjection)
    {
        AllowsPrivateMemory = allowsPrivateMemory;
        AllowsSharedMemory = allowsSharedMemory;
        AllowsIo = allowsIo;
        AllowsDma = allowsDma;
        AllowsHypercalls = allowsHypercalls;
        AllowsDebug = allowsDebug;
        AllowsMigration = allowsMigration;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public static SecureAuthorityBounds None { get; } = new();

    public bool AllowsPrivateMemory { get; }

    public bool AllowsSharedMemory { get; }

    public bool AllowsIo { get; }

    public bool AllowsDma { get; }

    public bool AllowsHypercalls { get; }

    public bool AllowsDebug { get; }

    public bool AllowsMigration { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsSubsetOf(SecureAuthorityBounds parent) =>
        (!AllowsPrivateMemory || parent.AllowsPrivateMemory) &&
        (!AllowsSharedMemory || parent.AllowsSharedMemory) &&
        (!AllowsIo || parent.AllowsIo) &&
        (!AllowsDma || parent.AllowsDma) &&
        (!AllowsHypercalls || parent.AllowsHypercalls) &&
        (!AllowsDebug || parent.AllowsDebug) &&
        (!AllowsMigration || parent.AllowsMigration) &&
        (!AllowsCompatibilityProjection || parent.AllowsCompatibilityProjection);
}
