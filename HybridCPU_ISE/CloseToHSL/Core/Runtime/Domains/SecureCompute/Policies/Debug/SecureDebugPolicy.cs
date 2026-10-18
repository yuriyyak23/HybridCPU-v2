namespace YAKSys_Hybrid_CPU.Core;

public enum SecureDebugMode : byte
{
    Denied = 0,
    MeasuredDebugOnly = 1,
    ExplicitRuntimeDebug = 2,
}

public sealed partial class SecureDebugPolicy
{
    public SecureDebugPolicy()
        : this(SecureDebugMode.Denied, changesMeasurementClass: false)
    {
    }

    public SecureDebugPolicy(
        SecureDebugMode mode,
        bool changesMeasurementClass)
    {
        Mode = mode;
        ChangesMeasurementClass = changesMeasurementClass;
    }

    public static SecureDebugPolicy Denied { get; } = new();

    public SecureDebugMode Mode { get; }

    public bool ChangesMeasurementClass { get; }

    public bool AllowsDebug =>
        Mode != SecureDebugMode.Denied;
}
