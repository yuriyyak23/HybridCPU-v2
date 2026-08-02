using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class AddressSpaceCanonicalIdentityAuthorityRemovalContract
{
    public const string AddressSpaceIdentityPath =
        "Core/Runtime/Memory/AddressSpaces/AddressSpaceId.cs";

    public const string NestedTlbTagPath =
        "Core/Runtime/Memory/Translation/NestedTlbTag.cs";

    public const string NestedCompositionPath =
        "Core/Runtime/Nested/MemoryComposition/NestedMemoryCompositionService.cs";

    public const string MemoryDomainDescriptorPath =
        "Core/Runtime/Domains/Descriptors/MemoryDomain/MemoryDomainDescriptor.cs";

    public const string CompatibilityTranslationPath =
        "Core/VMX/Compatibility/Frontend/Projection/Memory/MemoryTranslationControl.cs";

    public const string TlbPath = "Memory/MMU/TLB.cs";

    public const string GenericIommuPath = "Memory/MMU/IOMMU.cs";

    public const string DomainBoundIommuPath = "Memory/MMU/IOMMU.DomainBinding.cs";

    public const string PageWalkerPath =
        "Core/Runtime/Memory/Translation/NestedPageWalker.cs";

    public const string PageWalkerTranslatePath =
        "Core/Runtime/Memory/Translation/NestedPageWalker.Translate.partial.cs";

    public static string[] ForbiddenCanonicalIdentityMarkers { get; } =
    {
        "Vmid",
        "Vpid",
        "NptRootIdentity",
        "EptEpoch",
        "VpidEpoch",
        "VmcsIdentity",
        "CompatibilityProjectionIdentity",
        "HostOwned",
        "MatchesEptContext",
        "MatchesVpid",
        "FlushNestedByNptRoot",
        "FlushNestedByVpid",
        "FlushNestedByVmid",
    };

    public static string[] ForbiddenNestedCompositionMarkers { get; } =
    {
        "L2Vpid",
        "InvalidateByNestedVpid",
        "InvalidateByCompositeRoot",
        "TryWalkRawNpt",
        "VpidEpochOrZero",
    };

    public bool IsNeutralRuntimeIdentity(
        MemoryDomainTranslationControl control,
        ulong secondStageEpoch,
        ulong addressSpaceTagEpoch,
        AddressSpaceId identity) =>
        identity == control.ToAddressSpaceId(secondStageEpoch, addressSpaceTagEpoch) &&
        identity.DomainTag == control.DomainTag &&
        identity.AddressSpaceTag == control.AddressSpaceTag &&
        identity.SecondStageRootIdentity == control.SecondStageRoot &&
        identity.SecondStageEpoch == secondStageEpoch &&
        identity.AddressSpaceTagEpoch == addressSpaceTagEpoch &&
        identity.AddressSpaceGeneration == control.AddressSpaceGeneration;
}
