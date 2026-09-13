namespace YAKSys_Hybrid_CPU.Core.Nested;

public enum NestedValidationCode : byte
{
    Success = 0,
    ProjectionDenied = 1,
    CompatibilityProjectionFailed = 2,
}

public readonly record struct NestedValidationResult(
    bool Succeeded,
    NestedValidationCode Code,
    string Message)
{
    public static NestedValidationResult Success() =>
        new(true, NestedValidationCode.Success, string.Empty);

    public static NestedValidationResult Fail(
        NestedValidationCode code,
        string message) =>
        new(false, code, message);
}
