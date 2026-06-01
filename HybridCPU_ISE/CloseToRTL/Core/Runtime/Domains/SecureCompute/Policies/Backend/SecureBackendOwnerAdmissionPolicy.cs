namespace YAKSys_Hybrid_CPU.Core;

public enum SecureBackendOwnerAdmissionDecision : byte
{
    AllowedProofOnlyNoExecution = 0,
    DeniedMissingNeutralOwner = 1,
    DeniedNonNeutralAuthoritySource = 2,
    DeniedMissingRfcAdrApproval = 3,
    DeniedMissingProofChain = 4,
    DeniedStaleEpoch = 5,
    DeniedMissingNegativeTests = 6,
    DeniedBackendExecutionClosed = 7,
}

public readonly record struct SecureBackendOwnerAdmissionRequest(
    SecureBackendOwnerDescriptor? Owner,
    SecureBackendRfcAdrState RfcAdrState,
    SecureRevocationEpoch CurrentEpoch,
    bool RequestsBackendExecution);

public readonly record struct SecureBackendOwnerAdmissionResult(
    SecureBackendOwnerAdmissionDecision Decision,
    bool ProofChainAccepted,
    bool BackendExecutionAuthorized,
    string Reason)
{
    public bool IsAllowed =>
        Decision == SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution;

    public static SecureBackendOwnerAdmissionResult AllowedProofOnly { get; } = new(
        SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution,
        ProofChainAccepted: true,
        BackendExecutionAuthorized: false,
        Reason: "Neutral backend owner proof chain is accepted only as policy evidence; secure backend execution remains closed.");

    public static SecureBackendOwnerAdmissionResult Denied(
        SecureBackendOwnerAdmissionDecision decision,
        string reason) =>
        new(
            decision,
            ProofChainAccepted: false,
            BackendExecutionAuthorized: false,
            Reason: reason);
}

public sealed partial class SecureBackendOwnerAdmissionPolicy
{
    public static SecureBackendOwnerAdmissionPolicy Default { get; } = new();

    public SecureBackendOwnerAdmissionResult Admit(SecureBackendOwnerAdmissionRequest request)
    {
        SecureBackendOwnerDescriptor? owner = request.Owner;
        if (owner is null ||
            !owner.Value.Materialized ||
            !owner.Value.HasIdentity)
        {
            return Deny(
                SecureBackendOwnerAdmissionDecision.DeniedMissingNeutralOwner,
                "Secure backend owner proof requires a materialized neutral runtime owner descriptor.");
        }

        if (!owner.Value.IsNeutralSource)
        {
            return Deny(
                SecureBackendOwnerAdmissionDecision.DeniedNonNeutralAuthoritySource,
                "Secure backend owner proof cannot be sourced from compatibility projection, VMX, VMCS or VmxCaps vocabulary.");
        }

        if (request.RfcAdrState != SecureBackendRfcAdrState.Approved)
        {
            return Deny(
                SecureBackendOwnerAdmissionDecision.DeniedMissingRfcAdrApproval,
                "Secure backend owner proof requires a separate approved RFC/ADR gate.");
        }

        if (!owner.Value.HasProofChain)
        {
            return Deny(
                SecureBackendOwnerAdmissionDecision.DeniedMissingProofChain,
                "Secure backend owner proof requires grant, evidence, completion and retire proof chain validation.");
        }

        if (!owner.Value.Epoch.Equals(request.CurrentEpoch))
        {
            return Deny(
                SecureBackendOwnerAdmissionDecision.DeniedStaleEpoch,
                "Secure backend owner proof epoch must match the current policy epoch.");
        }

        if (!owner.Value.NegativeTestsPresent)
        {
            return Deny(
                SecureBackendOwnerAdmissionDecision.DeniedMissingNegativeTests,
                "Secure backend owner proof requires matching negative conformance tests before any runtime-execution claim.");
        }

        if (request.RequestsBackendExecution)
        {
            return Deny(
                SecureBackendOwnerAdmissionDecision.DeniedBackendExecutionClosed,
                "Secure backend execution remains closed; this gate accepts owner proof only.");
        }

        return SecureBackendOwnerAdmissionResult.AllowedProofOnly;
    }

    private static SecureBackendOwnerAdmissionResult Deny(
        SecureBackendOwnerAdmissionDecision decision,
        string reason) =>
        SecureBackendOwnerAdmissionResult.Denied(decision, reason);
}
