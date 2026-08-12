namespace YAKSys_Hybrid_CPU.Core;

internal sealed partial class RuntimeLegalityService
{
    internal SafetyVerifier? CanonicalVirtualizationVerifier =>
        _legalityChecker as SafetyVerifier;
}
