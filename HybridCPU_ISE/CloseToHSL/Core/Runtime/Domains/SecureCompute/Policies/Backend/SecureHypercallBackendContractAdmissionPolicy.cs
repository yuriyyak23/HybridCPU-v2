namespace YAKSys_Hybrid_CPU.Core;

public enum SecureHypercallBackendContractDecision : byte
{
    AllowedProofOnlyNoExecution = 0,
    DeniedUnresolvedContract = 1,
    DeniedUnknownServiceId = 2,
    DeniedDecodedLeafMismatch = 3,
    DeniedUnsupportedContractVersion = 4,
    DeniedMissingOwner = 5,
    DeniedWrongOwner = 6,
    DeniedOwnerEpochMismatch = 7,
    DeniedMissingGrant = 8,
    DeniedMissingEvidence = 9,
    DeniedStaleEvidence = 10,
    DeniedInvalidSharedBuffer = 11,
    DeniedRawPointerRepresentation = 12,
    DeniedInvalidOpaqueHandle = 13,
    DeniedReplayViolation = 14,
    DeniedCancelledBeforeExecution = 15,
    DeniedTransportOpcodeMismatch = 16,
}

public readonly record struct SecureHypercallBackendContractAdmissionResult(
    SecureHypercallBackendContractDecision Decision,
    bool ContractProofAccepted,
    bool BackendExecutionAuthorized,
    bool CompletionPublicationAuthorized,
    bool RetirePublicationAuthorized,
    string Reason)
{
    public bool IsProofOnly =>
        Decision == SecureHypercallBackendContractDecision.AllowedProofOnlyNoExecution &&
        ContractProofAccepted &&
        !BackendExecutionAuthorized &&
        !CompletionPublicationAuthorized &&
        !RetirePublicationAuthorized;

    public static SecureHypercallBackendContractAdmissionResult AllowedProofOnly() =>
        new(
            SecureHypercallBackendContractDecision.AllowedProofOnlyNoExecution,
            ContractProofAccepted: true,
            BackendExecutionAuthorized: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            "Secure hypercall owner/service contract proof accepted; execution and publication remain closed.");

    public static SecureHypercallBackendContractAdmissionResult Denied(
        SecureHypercallBackendContractDecision decision,
        string reason) =>
        new(
            decision,
            ContractProofAccepted: false,
            BackendExecutionAuthorized: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            reason);
}

public sealed class SecureHypercallBackendContractAdmissionPolicy
{
    public static SecureHypercallBackendContractAdmissionPolicy Default { get; } = new();

    public SecureHypercallBackendContractAdmissionResult Admit(
        SecureHypercallBackendContractDescriptor? contract,
        SecureHypercallBackendContractRequest request)
    {
        if (contract?.IsMaterialized != true)
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedUnresolvedContract,
                "Secure hypercall contract requires an exact decoded leaf, service ID, owner ID, owner epoch and version.");
        }

        if (request.TransportOpcode != SecureHypercallBackendOwnerAbiRegistry.TransportOpcode)
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedTransportOpcodeMismatch,
                "Secure hypercall backend owner contract requires the reviewed transport opcode.");
        }

        if (request.ServiceId != contract.ServiceId)
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedUnknownServiceId,
                "Secure hypercall service ID is not present in the accepted owner contract.");
        }

        if (request.DecodedLeaf != contract.DecodedLeaf)
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedDecodedLeafMismatch,
                "Decoded transport leaf does not match the separately typed contract leaf.");
        }

        if (request.ContractVersion != contract.Version)
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedUnsupportedContractVersion,
                "Secure hypercall contract version is unsupported.");
        }

        if (request.Owner is not { } owner ||
            !owner.Materialized ||
            !owner.HasIdentity ||
            !owner.IsNeutralSource)
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedMissingOwner,
                "Secure hypercall contract requires a materialized neutral backend owner descriptor.");
        }

        if (owner.OwnerId != contract.OwnerId.Value)
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedWrongOwner,
                "Secure hypercall backend owner does not match the accepted service contract.");
        }

        if (!owner.Epoch.Equals(contract.OwnerEpoch) ||
            !owner.Epoch.Equals(request.CurrentEpoch))
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedOwnerEpochMismatch,
                "Secure hypercall backend owner epoch is stale or does not match the contract epoch.");
        }

        if (request.PresentedGrant != contract.RequiredGrant ||
            !request.PresentedGrant.MatchesEpoch(request.CurrentEpoch))
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedMissingGrant,
                "Secure hypercall contract requires the exact current typed grant.");
        }

        if (!request.EvidenceValidated)
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedMissingEvidence,
                "Secure hypercall contract requires validated neutral evidence.");
        }

        if (!request.EvidenceEpoch.Equals(request.CurrentEpoch))
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedStaleEvidence,
                "Secure hypercall evidence epoch is stale.");
        }

        foreach (SecureHypercallBackendArgument argument in request.Arguments)
        {
            SecureHypercallBackendContractAdmissionResult argumentResult =
                AdmitArgument(argument, request);
            if (!argumentResult.IsProofOnly)
            {
                return argumentResult;
            }
        }

        if (request.IsReplay &&
            (contract.ReplayPolicy == SecureHypercallReplayPolicy.DenyReplay ||
             !request.IdempotentRetry ||
             !request.ReplayTokenMatches))
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedReplayViolation,
                "Secure hypercall replay is denied unless the contract permits an idempotent retry with a matching replay token.");
        }

        if (request.CancellationRequested)
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedCancelledBeforeExecution,
                "Secure hypercall cancellation is accepted only as a pre-execution denial.");
        }

        return SecureHypercallBackendContractAdmissionResult.AllowedProofOnly();
    }

    private static SecureHypercallBackendContractAdmissionResult AdmitArgument(
        SecureHypercallBackendArgument argument,
        SecureHypercallBackendContractRequest request)
    {
        if (argument.IsRawPointer)
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedRawPointerRepresentation,
                "Raw host or guest pointer representation is forbidden by the secure hypercall contract.");
        }

        if (argument.IsOpaqueHandle)
        {
            return argument.Value != 0 &&
                argument.Grant.MatchesEpoch(request.CurrentEpoch)
                    ? SecureHypercallBackendContractAdmissionResult.AllowedProofOnly()
                    : Deny(
                        SecureHypercallBackendContractDecision.DeniedInvalidOpaqueHandle,
                        "Opaque handles require a nonzero value plus current runtime provenance.");
        }

        if (!argument.IsSharedBuffer)
        {
            return SecureHypercallBackendContractAdmissionResult.AllowedProofOnly();
        }

        if (request.IoPolicy?.TryFindCurrentSharedBuffer(
                argument.Value,
                request.ValidatedDomainTag,
                request.CurrentEpoch,
                out SecureSharedBufferDescriptor buffer) != true ||
            argument.Length == 0 ||
            argument.Length > buffer.Length ||
            !argument.Grant.MatchesEpoch(request.CurrentEpoch))
        {
            return Deny(
                SecureHypercallBackendContractDecision.DeniedInvalidSharedBuffer,
                "Shared-buffer arguments require current owner, lifetime, evidence, bounds and typed-grant validation.");
        }

        return SecureHypercallBackendContractAdmissionResult.AllowedProofOnly();
    }

    private static SecureHypercallBackendContractAdmissionResult Deny(
        SecureHypercallBackendContractDecision decision,
        string reason) =>
        SecureHypercallBackendContractAdmissionResult.Denied(decision, reason);
}
