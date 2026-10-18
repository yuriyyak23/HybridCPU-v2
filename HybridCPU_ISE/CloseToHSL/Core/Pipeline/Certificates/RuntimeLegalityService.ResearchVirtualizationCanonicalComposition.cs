#if TESTING
namespace YAKSys_Hybrid_CPU.Core;

internal sealed partial class RuntimeLegalityService
{
    internal SafetyVerifier? ResearchVirtualizationCanonicalVerifier =>
        _legalityChecker as SafetyVerifier;
}
#endif
