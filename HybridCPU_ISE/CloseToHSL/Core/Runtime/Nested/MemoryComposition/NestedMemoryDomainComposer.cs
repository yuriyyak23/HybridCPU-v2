using YAKSys_Hybrid_CPU.Core.Nested;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core;

public enum NestedMemoryDomainCompositionDecision : byte
{
    Allowed = 0,
    MissingNestedDescriptor = 1,
    RuntimeAuthorityRequired = 2,
    DomainCompositionDenied = 3,
    CapabilityDenied = 4,
    MissingChildMemoryDomain = 5,
    MissingHostMemoryDomain = 6,
    ChildTranslationAuthorityDenied = 7,
    HostTranslationAuthorityDenied = 8,
    InvalidCompositionContext = 9,
    CompatibilityProjectionDenied = 10,
}

public readonly record struct NestedMemoryDomainCompositionRequest(
    NestedDomainDescriptor? NestedDescriptor,
    MemoryDomainDescriptor? ChildMemory,
    MemoryDomainDescriptor? HostMemory,
    NestedMemoryCompositionContext Context,
    bool RequiresCompatibilityProjection);

public readonly record struct NestedMemoryDomainCompositionResult(
    NestedMemoryDomainCompositionDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == NestedMemoryDomainCompositionDecision.Allowed;

    public static NestedMemoryDomainCompositionResult Allowed { get; } =
        new(NestedMemoryDomainCompositionDecision.Allowed, "Nested memory domain composition allowed.");

    public static NestedMemoryDomainCompositionResult Denied(
        NestedMemoryDomainCompositionDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class NestedMemoryDomainComposer
{
    public NestedMemoryDomainCompositionResult Validate(
        NestedMemoryDomainCompositionRequest request)
    {
        if (request.NestedDescriptor is null)
        {
            return NestedMemoryDomainCompositionResult.Denied(
                NestedMemoryDomainCompositionDecision.MissingNestedDescriptor,
                "Nested memory composition requires a nested domain descriptor.");
        }

        if (!request.NestedDescriptor.IsRuntimeAuthoritative)
        {
            return NestedMemoryDomainCompositionResult.Denied(
                NestedMemoryDomainCompositionDecision.RuntimeAuthorityRequired,
                "Nested memory composition requires runtime-authoritative descriptors.");
        }

        if (!request.NestedDescriptor.CanComposeDomain)
        {
            return NestedMemoryDomainCompositionResult.Denied(
                NestedMemoryDomainCompositionDecision.DomainCompositionDenied,
                "Nested memory composition requires validated domain composition.");
        }

        if (!request.NestedDescriptor.HasCapability(NestedCapabilityGrantMask.NestedMemoryComposition))
        {
            return NestedMemoryDomainCompositionResult.Denied(
                NestedMemoryDomainCompositionDecision.CapabilityDenied,
                "Nested memory composition requires the nested memory-composition capability grant.");
        }

        if (request.ChildMemory is null)
        {
            return NestedMemoryDomainCompositionResult.Denied(
                NestedMemoryDomainCompositionDecision.MissingChildMemoryDomain,
                "Nested memory composition requires a child memory-domain descriptor.");
        }

        if (request.HostMemory is null)
        {
            return NestedMemoryDomainCompositionResult.Denied(
                NestedMemoryDomainCompositionDecision.MissingHostMemoryDomain,
                "Nested memory composition requires a host memory-domain descriptor.");
        }

        if (!request.ChildMemory.OwnsSecondStageTranslation ||
            !request.ChildMemory.HasValidTranslationControl)
        {
            return NestedMemoryDomainCompositionResult.Denied(
                NestedMemoryDomainCompositionDecision.ChildTranslationAuthorityDenied,
                "Child memory-domain descriptor does not own valid second-stage translation.");
        }

        if (!request.HostMemory.OwnsSecondStageTranslation ||
            !request.HostMemory.HasValidTranslationControl)
        {
            return NestedMemoryDomainCompositionResult.Denied(
                NestedMemoryDomainCompositionDecision.HostTranslationAuthorityDenied,
                "Host memory-domain descriptor does not own valid second-stage translation.");
        }

        if (!request.Context.IsValid)
        {
            return NestedMemoryDomainCompositionResult.Denied(
                NestedMemoryDomainCompositionDecision.InvalidCompositionContext,
                "Nested memory composition context is invalid.");
        }

        if (request.RequiresCompatibilityProjection &&
            !request.NestedDescriptor.CanProjectToCompatibilityFrontend)
        {
            return NestedMemoryDomainCompositionResult.Denied(
                NestedMemoryDomainCompositionDecision.CompatibilityProjectionDenied,
                "Nested memory composition descriptor denies compatibility projection.");
        }

        return NestedMemoryDomainCompositionResult.Allowed;
    }

    public bool CanCompose(NestedMemoryDomainCompositionRequest request) =>
        Validate(request).IsAllowed;
}
