using YAKSys_Hybrid_CPU.Core.Nested;

namespace YAKSys_Hybrid_CPU.Core;

public enum NestedDomainAuthority : byte
{
    Runtime = 0,
    DomainComposition = 1,
    CompatibilityProjection = 2,
}

public sealed partial class NestedDomainDescriptor
{
    public NestedDomainDescriptor()
        : this(
            authority: NestedDomainAuthority.Runtime,
            parentDomainId: 0,
            childDomainId: 0,
            capabilities: NestedCapabilityGrantMask.None,
            domainCompositionEnabled: false,
            allowsCompatibilityProjection: false,
            hostEvidenceExcluded: true,
            lanePassthroughBlocked: true)
    {
    }

    public NestedDomainDescriptor(
        NestedDomainAuthority authority,
        ulong parentDomainId,
        ulong childDomainId,
        NestedCapabilityGrantMask capabilities,
        bool domainCompositionEnabled,
        bool allowsCompatibilityProjection,
        bool hostEvidenceExcluded,
        bool lanePassthroughBlocked)
    {
        Authority = authority;
        ParentDomainId = parentDomainId;
        ChildDomainId = childDomainId;
        Capabilities = capabilities;
        DomainCompositionEnabled = domainCompositionEnabled;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
        HostEvidenceExcluded = hostEvidenceExcluded;
        LanePassthroughBlocked = lanePassthroughBlocked;
    }

    public NestedDomainAuthority Authority { get; }

    public ulong ParentDomainId { get; }

    public ulong ChildDomainId { get; }

    public NestedCapabilityGrantMask Capabilities { get; }

    public bool DomainCompositionEnabled { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool HostEvidenceExcluded { get; }

    public bool LanePassthroughBlocked { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == NestedDomainAuthority.Runtime;

    public bool HasParentChildBinding =>
        ParentDomainId != 0 &&
        ChildDomainId != 0 &&
        ParentDomainId != ChildDomainId;

    public bool HasCapability(NestedCapabilityGrantMask capability) =>
        capability == NestedCapabilityGrantMask.None ||
        (Capabilities & capability) == capability;

    public bool HasRequiredSecurityGates =>
        HostEvidenceExcluded &&
        LanePassthroughBlocked;

    public bool CanComposeDomain =>
        IsRuntimeAuthoritative &&
        DomainCompositionEnabled &&
        HasParentChildBinding &&
        HasRequiredSecurityGates;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        CanComposeDomain;

    public NestedDomainDescriptor WithDomainBinding(
        ulong parentDomainId,
        ulong childDomainId) =>
        new(
            Authority,
            parentDomainId,
            childDomainId,
            Capabilities,
            DomainCompositionEnabled,
            AllowsCompatibilityProjection,
            HostEvidenceExcluded,
            LanePassthroughBlocked);
}
