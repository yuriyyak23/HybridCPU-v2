namespace YAKSys_Hybrid_CPU.Core;

public enum VmxCompatProjectionDecision : byte
{
    Allowed = 0,
    UnknownAlias = 1,
    UnfrozenAbiAlias = 2,
    DescriptorValidationDenied = 3,
    EvidenceValidationDenied = 4,
    AuthoritativeMutationDenied = 5,
}

public readonly record struct VmxCompatProjectionRequest(
    CompatAliasSourceKind SourceKind,
    string SourceName,
    bool DescriptorValidated,
    bool EvidenceValidated,
    bool AttemptsAuthoritativeMutation);

public readonly record struct VmxCompatProjectionResult(
    VmxCompatProjectionDecision Decision,
    CompatAliasMapEntry Alias,
    string Reason)
{
    public bool IsAllowed => Decision == VmxCompatProjectionDecision.Allowed;

    public static VmxCompatProjectionResult Allowed(CompatAliasMapEntry alias) =>
        new(VmxCompatProjectionDecision.Allowed, alias, string.Empty);

    public static VmxCompatProjectionResult Denied(
        VmxCompatProjectionDecision decision,
        string reason) =>
        new(decision, default, reason);
}

public sealed partial class VmxCompatProjectionService
{
    private readonly CompatAliasMap _aliasMap;

    public VmxCompatProjectionService()
        : this(new CompatAliasMap())
    {
    }

    public VmxCompatProjectionService(CompatAliasMap aliasMap)
    {
        _aliasMap = aliasMap;
    }

    public VmxCompatProjectionResult ValidateProjection(VmxCompatProjectionRequest request)
    {
        if (!_aliasMap.TryGetEntry(request.SourceKind, request.SourceName, out var alias))
        {
            return VmxCompatProjectionResult.Denied(
                VmxCompatProjectionDecision.UnknownAlias,
                "Compatibility projection source is not declared in the alias map.");
        }

        if (!alias.IsFrozenAbi)
        {
            return VmxCompatProjectionResult.Denied(
                VmxCompatProjectionDecision.UnfrozenAbiAlias,
                "Compatibility projection source is not part of the frozen ABI.");
        }

        if (!request.DescriptorValidated)
        {
            return VmxCompatProjectionResult.Denied(
                VmxCompatProjectionDecision.DescriptorValidationDenied,
                "Compatibility projection requires descriptor validation.");
        }

        if (!request.EvidenceValidated)
        {
            return VmxCompatProjectionResult.Denied(
                VmxCompatProjectionDecision.EvidenceValidationDenied,
                "Compatibility projection requires evidence validation.");
        }

        if (request.AttemptsAuthoritativeMutation)
        {
            return VmxCompatProjectionResult.Denied(
                VmxCompatProjectionDecision.AuthoritativeMutationDenied,
                "Compatibility projection cannot mutate authoritative substrate state directly.");
        }

        return VmxCompatProjectionResult.Allowed(alias);
    }
}
