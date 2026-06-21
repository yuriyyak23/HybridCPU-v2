namespace YAKSys_Hybrid_CPU.Core;

public enum SecureHostInspectionMode : byte
{
    Denied = 0,
    MetadataOnly = 1,
    ExplicitSharedOnly = 2,
    DebugOnly = 3,
}

public sealed partial class SecureHostInspectionPolicy
{
    public SecureHostInspectionPolicy()
        : this(SecureHostInspectionMode.Denied, allowPrivateMemoryInspection: false)
    {
    }

    public SecureHostInspectionPolicy(
        SecureHostInspectionMode mode,
        bool allowPrivateMemoryInspection)
    {
        Mode = mode;
        AllowPrivateMemoryInspection = allowPrivateMemoryInspection;
    }

    public static SecureHostInspectionPolicy DenyAll { get; } = new();

    public SecureHostInspectionMode Mode { get; }

    public bool AllowPrivateMemoryInspection { get; }

    public bool CanInspectPrivateMemory =>
        Mode != SecureHostInspectionMode.Denied && AllowPrivateMemoryInspection;
}
