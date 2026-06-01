namespace YAKSys_Hybrid_CPU.Core;

public enum DomainBindingAuthority : byte
{
    Runtime = 0,
    DescriptorSet = 1,
    CompatibilityProjection = 2,
}

public enum DomainBindingDecision : byte
{
    Allowed = 0,
    MissingBinding = 1,
    RuntimeAuthorityRequired = 2,
    MissingRuntimeContext = 3,
    MissingRequiredDomains = 4,
    DomainIdMismatch = 5,
    CompatibilityProjectionDenied = 6,
}

public readonly record struct DomainBindingEntry(
    DomainBindingAuthority Authority,
    ulong DomainId,
    DomainRuntimeContext Context,
    bool AllowsCompatibilityProjection)
{
    public bool IsRuntimeAuthoritative =>
        Authority == DomainBindingAuthority.Runtime;

    public bool HasDomainId =>
        DomainId != 0;

    public bool HasRuntimeContext =>
        Context is not null;

    public bool HasRequiredDomains =>
        HasRuntimeContext &&
        Context.HasRequiredDomains;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative &&
        HasDomainId;
}

public readonly record struct DomainBindingRequest(
    DomainBindingEntry? Binding,
    ulong ExpectedDomainId,
    bool RequiresCompatibilityProjection);

public readonly record struct DomainBindingResult(
    DomainBindingDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == DomainBindingDecision.Allowed;

    public static DomainBindingResult Allowed { get; } =
        new(DomainBindingDecision.Allowed, "Domain binding allowed.");

    public static DomainBindingResult Denied(
        DomainBindingDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class DomainBindingTable
{
    public DomainBindingResult Validate(DomainBindingRequest request)
    {
        if (request.Binding is null)
        {
            return DomainBindingResult.Denied(
                DomainBindingDecision.MissingBinding,
                "Domain binding requires a binding entry.");
        }

        DomainBindingEntry binding = request.Binding.Value;
        if (!binding.IsRuntimeAuthoritative)
        {
            return DomainBindingResult.Denied(
                DomainBindingDecision.RuntimeAuthorityRequired,
                "Domain binding requires runtime authority.");
        }

        if (!binding.HasRuntimeContext)
        {
            return DomainBindingResult.Denied(
                DomainBindingDecision.MissingRuntimeContext,
                "Domain binding requires a runtime context.");
        }

        if (!binding.HasRequiredDomains)
        {
            return DomainBindingResult.Denied(
                DomainBindingDecision.MissingRequiredDomains,
                "Domain binding requires execution, memory, and I/O domain descriptors.");
        }

        if (request.ExpectedDomainId != 0 &&
            binding.DomainId != request.ExpectedDomainId)
        {
            return DomainBindingResult.Denied(
                DomainBindingDecision.DomainIdMismatch,
                "Domain binding did not match the expected domain id.");
        }

        if (request.RequiresCompatibilityProjection &&
            !binding.CanProjectToCompatibilityFrontend)
        {
            return DomainBindingResult.Denied(
                DomainBindingDecision.CompatibilityProjectionDenied,
                "Domain binding denies compatibility projection.");
        }

        return DomainBindingResult.Allowed;
    }

    public bool CanBind(DomainBindingRequest request) =>
        Validate(request).IsAllowed;
}
