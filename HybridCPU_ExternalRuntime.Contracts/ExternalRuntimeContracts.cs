using System.Collections.ObjectModel;

namespace HybridCPU.ExternalRuntime.Contracts;

public readonly record struct HybridCpuExternalContractVersion(ushort Major, ushort Minor, ushort Patch)
{
    public bool IsValid => Major != 0;
    public static HybridCpuExternalContractVersion V1 { get; } = new(1, 0, 0);
    public static HybridCpuExternalContractVersion V1_1 { get; } = new(1, 1, 0);
    public static HybridCpuExternalContractVersion V1_2 { get; } = new(1, 2, 0);
    public static HybridCpuExternalContractVersion V1_3 { get; } = new(1, 3, 0);
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public enum HybridCpuExternalFeatureFamily : byte
{
    DomainLifecycle = 0,
    OwnedRegionMapping = 1,
    DeviceBinding = 2,
    MmioMapping = 3,
    InterruptBinding = 4,
    ExplicitMemoryVisibility = 5,
    DmaAdmission = 6,
    ChildDomainLifecycle = 7,
    ChildGuestMemory = 8,
    ChildEventDelivery = 9,
    ChildTrapDelivery = 10,
    ChildExecutableImage = 11,
    ChildVirtualIo = 12,
    SecureDomains = 13,
}

public enum HybridCpuExternalFeatureAvailability : byte
{
    Unavailable = 0,
    RuntimeAdmission = 1,
    Executable = 2,
    ProductionSecure = 3,
}

public readonly record struct HybridCpuExternalFeatureDescriptor(
    HybridCpuExternalFeatureFamily Family,
    HybridCpuExternalFeatureAvailability Availability,
    ushort Version)
{
    public static HybridCpuExternalFeatureDescriptor Unavailable(HybridCpuExternalFeatureFamily family) =>
        new(family, HybridCpuExternalFeatureAvailability.Unavailable, 0);
}

public sealed class HybridCpuExternalFeatureManifest
{
    private readonly ReadOnlyDictionary<HybridCpuExternalFeatureFamily, HybridCpuExternalFeatureDescriptor> features;

    public HybridCpuExternalFeatureManifest(
        HybridCpuExternalContractVersion contractVersion,
        ulong generation,
        IEnumerable<HybridCpuExternalFeatureDescriptor> features)
    {
        if (!contractVersion.IsValid) throw new ArgumentOutOfRangeException(nameof(contractVersion));
        if (generation == 0) throw new ArgumentOutOfRangeException(nameof(generation));
        ArgumentNullException.ThrowIfNull(features);

        var snapshot = new Dictionary<HybridCpuExternalFeatureFamily, HybridCpuExternalFeatureDescriptor>();
        foreach (HybridCpuExternalFeatureDescriptor feature in features)
        {
            if (!Enum.IsDefined(feature.Family)) throw new ArgumentOutOfRangeException(nameof(features), "Undefined feature family.");
            if (!Enum.IsDefined(feature.Availability)) throw new ArgumentOutOfRangeException(nameof(features), "Undefined feature availability.");
            if (feature.Availability == HybridCpuExternalFeatureAvailability.Unavailable || feature.Version == 0)
                throw new ArgumentException("Unavailable and v0 entries must be omitted from a manifest.", nameof(features));
            if (!snapshot.TryAdd(feature.Family, feature))
                throw new ArgumentException($"Duplicate feature family: {feature.Family}.", nameof(features));
        }

        ContractVersion = contractVersion;
        Generation = generation;
        this.features = new(snapshot);
        Features = Array.AsReadOnly(snapshot.Values.OrderBy(static item => item.Family).ToArray());
    }

    public HybridCpuExternalContractVersion ContractVersion { get; }
    public ulong Generation { get; }
    public IReadOnlyList<HybridCpuExternalFeatureDescriptor> Features { get; }

    public HybridCpuExternalFeatureDescriptor GetFeature(HybridCpuExternalFeatureFamily family)
    {
        if (!Enum.IsDefined(family)) throw new ArgumentOutOfRangeException(nameof(family));
        return features.TryGetValue(family, out HybridCpuExternalFeatureDescriptor feature)
            ? feature
            : HybridCpuExternalFeatureDescriptor.Unavailable(family);
    }

    public static HybridCpuExternalFeatureManifest CreateNext(
        HybridCpuExternalFeatureManifest previous,
        ulong generation,
        IEnumerable<HybridCpuExternalFeatureDescriptor> features)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (generation <= previous.Generation)
            throw new ArgumentOutOfRangeException(nameof(generation), "Manifest generation must increase monotonically.");
        return new(previous.ContractVersion, generation, features);
    }
}

public readonly record struct ExternalDomainLeaseHandle(Guid Value);
public readonly record struct ExternalDomainLeaseEpoch(ulong Value);
public readonly record struct ExternalDomainLease(ExternalDomainLeaseHandle Handle, ExternalDomainLeaseEpoch Epoch);
public readonly record struct ExternalOperationHandle(Guid Value);
public readonly record struct ExternalOperationGeneration(ulong Value);
public readonly record struct ExternalOperationIdentity(ExternalOperationHandle Handle, ExternalOperationGeneration Generation);

public enum ExternalDomainProfile : byte
{
    IsolatedDomain = 0,
}

public enum ExternalDomainTransition : byte
{
    Start = 0,
    Park = 1,
    Resume = 2,
}

public enum ExternalDomainState : byte
{
    Ready = 0,
    Running = 1,
    Parked = 2,
    Closed = 3,
}

public enum ExternalRuntimeOutcome : byte
{
    Bound = 0,
    Succeeded = 1,
    Closed = 2,
    Unsupported = 3,
    Denied = 4,
    NotFound = 5,
    Stale = 6,
    Revoked = 7,
    Faulted = 8,
    Unknown = 9,
}

public sealed record ExternalDomainBindRequest(
    Guid RequestId,
    ExternalDomainProfile Profile,
    ExternalOperationIdentity Operation);

public sealed record ExternalDomainBindReceipt(
    ExternalDomainLease Lease,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration,
    ExternalDomainState State);

public sealed record ExternalDomainTransitionReceipt(
    ExternalDomainLease Lease,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration,
    ExternalDomainTransition Transition,
    ExternalDomainState ResultingState);

public sealed record ExternalDomainCloseReceipt(
    ExternalDomainLease Lease,
    ExternalOperationIdentity Operation,
    HybridCpuExternalContractVersion ContractVersion,
    ulong ManifestGeneration,
    ExternalDomainState ResultingState,
    bool IsTerminal);

public sealed record ExternalDomainBindResult(
    ExternalRuntimeOutcome Outcome,
    ExternalDomainBindReceipt? Receipt,
    string Reason);

public sealed record ExternalDomainTransitionResult(
    ExternalRuntimeOutcome Outcome,
    ExternalDomainTransitionReceipt? Receipt,
    string Reason);

public sealed record ExternalDomainCloseResult(
    ExternalRuntimeOutcome Outcome,
    ExternalDomainCloseReceipt? Receipt,
    string Reason);

public interface IHybridCpuExternalRuntime
{
    HybridCpuExternalFeatureManifest QueryFeatures();
    ExternalDomainBindResult BindDomain(ExternalDomainBindRequest request);
    ExternalDomainTransitionResult TransitionDomain(
        ExternalDomainLease lease,
        ExternalDomainTransition transition,
        ExternalOperationIdentity operation);
    ExternalDomainCloseResult CloseDomain(
        ExternalDomainLease lease,
        ExternalOperationIdentity operation);
}
