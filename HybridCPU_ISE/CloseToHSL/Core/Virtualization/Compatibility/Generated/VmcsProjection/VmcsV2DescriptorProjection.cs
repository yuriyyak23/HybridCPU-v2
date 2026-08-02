using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmcsV2DescriptorProjectionDecision : byte
{
    Allowed = 0,
    MissingGeneratedAliasMap = 1,
    DescriptorValidationDenied = 2,
    EvidenceValidationDenied = 3,
    WritableProjectionDenied = 4,
    AuthoritativeMutationDenied = 5,
}

public readonly record struct VmcsV2DescriptorProjectionRequest(
    bool GeneratedAliasMapDeclared,
    bool DescriptorValidated,
    bool EvidenceValidated,
    bool ReadOnlyProjection,
    bool AttemptsAuthoritativeMutation);

public readonly record struct VmcsV2DescriptorProjectionResult(
    VmcsV2DescriptorProjectionDecision Decision,
    uint RevisionId,
    uint SizeBytes,
    string Baseline,
    string Reason)
{
    public bool IsAllowed => Decision == VmcsV2DescriptorProjectionDecision.Allowed;

    public static VmcsV2DescriptorProjectionResult Allowed() =>
        new(
            VmcsV2DescriptorProjectionDecision.Allowed,
            VmcsV2Header.CurrentRevisionId,
            VmcsV2Header.CurrentSizeBytes,
            VmcsV2Header.CompatibilityBaseline,
            string.Empty);

    public static VmcsV2DescriptorProjectionResult Denied(
        VmcsV2DescriptorProjectionDecision decision,
        string reason) =>
        new(decision, 0, 0, string.Empty, reason);
}

public sealed partial class VmcsV2DescriptorProjection
{
    public VmcsV2DescriptorProjectionResult ValidateProjection(
        VmcsV2DescriptorProjectionRequest request)
    {
        if (!request.GeneratedAliasMapDeclared)
        {
            return VmcsV2DescriptorProjectionResult.Denied(
                VmcsV2DescriptorProjectionDecision.MissingGeneratedAliasMap,
                "VMCSv2 descriptor projection requires a generated alias map.");
        }

        if (!request.DescriptorValidated)
        {
            return VmcsV2DescriptorProjectionResult.Denied(
                VmcsV2DescriptorProjectionDecision.DescriptorValidationDenied,
                "VMCSv2 descriptor projection requires descriptor validation.");
        }

        if (!request.EvidenceValidated)
        {
            return VmcsV2DescriptorProjectionResult.Denied(
                VmcsV2DescriptorProjectionDecision.EvidenceValidationDenied,
                "VMCSv2 descriptor projection requires evidence validation.");
        }

        if (!request.ReadOnlyProjection)
        {
            return VmcsV2DescriptorProjectionResult.Denied(
                VmcsV2DescriptorProjectionDecision.WritableProjectionDenied,
                "VMCSv2 descriptor projection must be read-only.");
        }

        if (request.AttemptsAuthoritativeMutation)
        {
            return VmcsV2DescriptorProjectionResult.Denied(
                VmcsV2DescriptorProjectionDecision.AuthoritativeMutationDenied,
                "VMCSv2 descriptor projection cannot own or mutate authoritative substrate state.");
        }

        return VmcsV2DescriptorProjectionResult.Allowed();
    }
}
