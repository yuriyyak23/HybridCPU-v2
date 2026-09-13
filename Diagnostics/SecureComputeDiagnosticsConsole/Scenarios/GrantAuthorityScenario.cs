using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal sealed class GrantAuthorityScenario : ISecureComputeScenario
{
    public string Id => "grant-authority";
    public string Description => "Exercises grant-handle validation and separates it from mint/revoke ledger ownership.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.PolicyClassifier;
    public string AuthorityCeiling => "Caller-supplied handle validation only; no ledger-backed mint, lookup or revoke proof.";

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var policy = SecureGrantAuthorityPolicy.Default;
        var epoch = new SecureRevocationEpoch(7);
        var handle = new SecureGrantHandle(SecureGrantHandleKind.DomainIdentity, 1, 0xA5, 7);
        SecureGrantEpochSet epochs = SecureGrantEpochSet.Single(epoch);

        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SecureGrantAuthorityResult accepted = policy.Validate(
                handle, SecureGrantMaterializationSource.NeutralRuntimeOwner,
                SecureAuthorityBounds.None, SecureAuthorityBounds.None, epochs,
                runtimeOwnerMaterialized: true);
            SecureGrantAuthorityResult guest = policy.Validate(
                handle, SecureGrantMaterializationSource.GuestArchitecturalState,
                SecureAuthorityBounds.None, SecureAuthorityBounds.None, epochs,
                runtimeOwnerMaterialized: true);
            SecureGrantAuthorityResult revoked = policy.Validate(
                handle, SecureGrantMaterializationSource.NeutralRuntimeOwner,
                SecureAuthorityBounds.None, SecureAuthorityBounds.None, epochs,
                runtimeOwnerMaterialized: true, grantRevoked: true);
            SecureGrantAuthorityResult stale = policy.Validate(
                handle, SecureGrantMaterializationSource.NeutralRuntimeOwner,
                SecureAuthorityBounds.None, SecureAuthorityBounds.None,
                SecureGrantEpochSet.Single(new SecureRevocationEpoch(8)),
                runtimeOwnerMaterialized: true);

            context.Check(accepted.IsAllowed, "well-shaped caller-supplied handle passes policy validation");
            context.Check(guest.Decision == SecureGrantAuthorityDecision.DeniedGuestScalarMaterialization, "guest scalar source is denied");
            context.Check(revoked.Decision == SecureGrantAuthorityDecision.DeniedRevokedGrant, "revoked input flag is denied");
            context.Check(stale.Decision == SecureGrantAuthorityDecision.DeniedStaleEpoch, "stale epoch is denied");
            context.Count("caller_handle_policy_allowed");
            context.Count("guest_source_denied");
            context.Count("caller_revoked_flag_denied");
            context.Count("stale_epoch_denied");
            context.Trace("grant-policy", ("epoch", epoch.Current), ("localId", handle.LocalId));
            context.CompleteIteration("Grant policy matrix completed.");
        }

        context.Finding(
            "C0-GRANT-LEDGER",
            DiagnosticSeverity.Blocker,
            "Validation is not grant lifecycle ownership",
            "The policy consumes caller-supplied provenance, epochs, runtimeOwnerMaterialized and grantRevoked values. This scenario found no minted ledger entry or authoritative revoke lookup in this path.");
        return Task.CompletedTask;
    }
}
