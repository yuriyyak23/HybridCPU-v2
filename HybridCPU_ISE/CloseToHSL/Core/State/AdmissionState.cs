namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Reference-owned containment for the current decode-coupled admission chain.
/// It does not own scheduler legality, execution or publication authority.
/// </summary>
internal sealed class AdmissionState
{
    internal RuntimeClusterAdmissionPreparation Preparation;
    internal RuntimeClusterAdmissionCandidateView CandidateView;
    internal RuntimeClusterAdmissionDecisionDraft DecisionDraft;
    internal RuntimeClusterAdmissionHandoff Handoff;
}
