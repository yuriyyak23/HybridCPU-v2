using System;
using System.Collections.Generic;
using System.Linq;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

/// <summary>
/// Metadata-only registry for future L7-SDC external accelerator capability queries.
/// </summary>
public sealed class AcceleratorCapabilityRegistry
{
    private readonly Dictionary<string, AcceleratorCapabilityDescriptor> _descriptors =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly AcceleratorTelemetry? _telemetry;

    public AcceleratorCapabilityRegistry(AcceleratorTelemetry? telemetry = null)
    {
        _telemetry = telemetry;
    }

    public int Count => _descriptors.Count;

    public void RegisterProvider(IAcceleratorCapabilityProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        IReadOnlyList<AcceleratorCapabilityDescriptor> capabilities = provider.GetCapabilities();
        ArgumentNullException.ThrowIfNull(capabilities);

        foreach (AcceleratorCapabilityDescriptor descriptor in capabilities)
        {
            RegisterDescriptor(descriptor);
        }
    }

    public void RegisterDescriptor(AcceleratorCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ValidateMetadataOnlyDescriptor(descriptor);

        if (!_descriptors.TryAdd(descriptor.AcceleratorId, descriptor))
        {
            throw new InvalidOperationException(
                $"Accelerator capability metadata for '{descriptor.AcceleratorId}' is already registered.");
        }
    }

    public bool TryGetDescriptor(
        string acceleratorId,
        out AcceleratorCapabilityDescriptor? descriptor)
    {
        descriptor = null;
        if (string.IsNullOrWhiteSpace(acceleratorId))
        {
            return false;
        }

        return _descriptors.TryGetValue(acceleratorId, out descriptor);
    }

    public IReadOnlyList<AcceleratorCapabilityDescriptor> GetAllDescriptors()
    {
        return _descriptors.Values.ToArray();
    }

    public AcceleratorCapabilityQueryResult Query(
        string acceleratorId,
        AcceleratorCapabilityAdoptionMode adoptionMode =
            AcceleratorCapabilityAdoptionMode.MetadataOnly,
        AcceleratorCompatibilityMode compatibilityMode =
            AcceleratorCompatibilityMode.MetadataOnly)
    {
        if (!IsKnownMetadataOnlyMode(adoptionMode))
        {
            return RecordQueryResult(
                AcceleratorCapabilityQueryResult.Reject(
                    $"Unknown or unsupported accelerator capability adoption mode '{(int)adoptionMode}'."));
        }

        if (!IsKnownMetadataOnlyMode(compatibilityMode))
        {
            return RecordQueryResult(
                AcceleratorCapabilityQueryResult.Reject(
                    $"Unknown or unsupported accelerator compatibility mode '{(int)compatibilityMode}'."));
        }

        if (!TryGetDescriptor(acceleratorId, out AcceleratorCapabilityDescriptor? descriptor))
        {
            return RecordQueryResult(
                AcceleratorCapabilityQueryResult.Reject(
                    $"Unknown accelerator id '{acceleratorId}'."));
        }

        return RecordQueryResult(
            AcceleratorCapabilityQueryResult.MetadataAvailable(descriptor!));
    }

    public AcceleratorCapabilityAcceptanceResult AcceptCapability(
        string acceleratorId,
        AcceleratorOwnerBinding descriptorOwnerBinding,
        AcceleratorCapabilityAdoptionMode adoptionMode =
            AcceleratorCapabilityAdoptionMode.MetadataOnly,
        AcceleratorCompatibilityMode compatibilityMode =
            AcceleratorCompatibilityMode.MetadataOnly)
    {
        ArgumentNullException.ThrowIfNull(descriptorOwnerBinding);
        AcceleratorCapabilityQueryResult query = Query(
            acceleratorId,
            adoptionMode,
            compatibilityMode);
        return AcceleratorCapabilityAcceptanceResult.Reject(
            query,
            AcceleratorGuardDecision.Reject(
                AcceleratorGuardSurface.CapabilityAcceptance,
                AcceleratorGuardFault.CapabilityGuardMissing,
                descriptorOwnerBinding,
                evidence: null,
                YAKSys_Hybrid_CPU.Core.RejectKind.OwnerMismatch,
                "L7-SDC capability acceptance requires guard-plane owner/domain evidence; registry metadata is not authority."));
    }

    public AcceleratorCapabilityAcceptanceResult AcceptCapability(
        string acceleratorId,
        AcceleratorOwnerBinding descriptorOwnerBinding,
        AcceleratorGuardDecision descriptorGuardDecision,
        AcceleratorCapabilityAdoptionMode adoptionMode =
            AcceleratorCapabilityAdoptionMode.MetadataOnly,
        AcceleratorCompatibilityMode compatibilityMode =
            AcceleratorCompatibilityMode.MetadataOnly)
    {
        ArgumentNullException.ThrowIfNull(descriptorOwnerBinding);

        AcceleratorGuardDecision capabilityGuard =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeCapabilityAcceptance(
                descriptorOwnerBinding,
                descriptorGuardDecision);
        AcceleratorCapabilityQueryResult query = Query(
            acceleratorId,
            adoptionMode,
            compatibilityMode);

        if (!capabilityGuard.IsAllowed)
        {
            return AcceleratorCapabilityAcceptanceResult.Reject(query, capabilityGuard);
        }

        return query.IsMetadataAvailable
            ? AcceleratorCapabilityAcceptanceResult.Accepted(query, capabilityGuard)
            : AcceleratorCapabilityAcceptanceResult.Reject(query, capabilityGuard);
    }

    private static void ValidateMetadataOnlyDescriptor(AcceleratorCapabilityDescriptor descriptor)
    {
        if (!IsKnownMetadataOnlyMode(descriptor.AdoptionMode))
        {
            throw new ArgumentException(
                $"Unknown or unsupported accelerator capability adoption mode '{(int)descriptor.AdoptionMode}'.",
                nameof(descriptor));
        }

        if (!IsKnownMetadataOnlyMode(descriptor.CompatibilityMode))
        {
            throw new ArgumentException(
                $"Unknown or unsupported accelerator compatibility mode '{(int)descriptor.CompatibilityMode}'.",
                nameof(descriptor));
        }
    }

    private static bool IsKnownMetadataOnlyMode(AcceleratorCapabilityAdoptionMode mode) =>
        mode == AcceleratorCapabilityAdoptionMode.MetadataOnly &&
        Enum.IsDefined(typeof(AcceleratorCapabilityAdoptionMode), mode);

    private static bool IsKnownMetadataOnlyMode(AcceleratorCompatibilityMode mode) =>
        mode == AcceleratorCompatibilityMode.MetadataOnly &&
        Enum.IsDefined(typeof(AcceleratorCompatibilityMode), mode);

    private AcceleratorCapabilityQueryResult RecordQueryResult(
        AcceleratorCapabilityQueryResult result)
    {
        _telemetry?.RecordCapabilityQuery(
            result.IsMetadataAvailable,
            result.IsMetadataAvailable
                ? "L7-SDC capability metadata query succeeded."
                : result.RejectReason);
        return result;
    }
}

public sealed class AcceleratorCapabilityAcceptanceResult
{
    private AcceleratorCapabilityAcceptanceResult(
        bool isAccepted,
        AcceleratorCapabilityQueryResult queryResult,
        AcceleratorGuardDecision guardDecision,
        string rejectReason)
    {
        IsAccepted = isAccepted;
        QueryResult = queryResult;
        GuardDecision = guardDecision;
        RejectReason = rejectReason;
    }

    public bool IsAccepted { get; }

    public bool IsRejected => !IsAccepted;

    public AcceleratorCapabilityQueryResult QueryResult { get; }

    public AcceleratorGuardDecision GuardDecision { get; }

    public string RejectReason { get; }

    public AcceleratorCapabilityDescriptor? Descriptor => QueryResult.Descriptor;

    public bool GrantsDecodeAuthority => false;

    public bool GrantsCommandSubmissionAuthority => false;

    public bool GrantsExecutionAuthority => false;

    public bool GrantsCommitAuthority => false;

    public static AcceleratorCapabilityAcceptanceResult Accepted(
        AcceleratorCapabilityQueryResult queryResult,
        AcceleratorGuardDecision guardDecision)
    {
        ArgumentNullException.ThrowIfNull(queryResult);
        if (!queryResult.IsMetadataAvailable)
        {
            throw new ArgumentException(
                "Accepted capability results require metadata to be available.",
                nameof(queryResult));
        }

        if (!guardDecision.IsAllowed)
        {
            throw new ArgumentException(
                "Accepted capability results require an allowed guard decision.",
                nameof(guardDecision));
        }

        return new AcceleratorCapabilityAcceptanceResult(
            isAccepted: true,
            queryResult,
            guardDecision,
            rejectReason: string.Empty);
    }

    public static AcceleratorCapabilityAcceptanceResult Reject(
        AcceleratorCapabilityQueryResult queryResult,
        AcceleratorGuardDecision guardDecision)
    {
        ArgumentNullException.ThrowIfNull(queryResult);
        string rejectReason = !string.IsNullOrWhiteSpace(queryResult.RejectReason)
            ? queryResult.RejectReason
            : guardDecision.Message;
        return new AcceleratorCapabilityAcceptanceResult(
            isAccepted: false,
            queryResult,
            guardDecision,
            rejectReason);
    }
}

public sealed class AcceleratorCapabilityQueryResult
{
    private AcceleratorCapabilityQueryResult(
        bool isMetadataAvailable,
        AcceleratorCapabilityDescriptor? descriptor,
        string rejectReason)
    {
        IsMetadataAvailable = isMetadataAvailable;
        Descriptor = descriptor;
        RejectReason = rejectReason;
    }

    public bool IsMetadataAvailable { get; }

    public bool IsRejected => !IsMetadataAvailable;

    public AcceleratorCapabilityDescriptor? Descriptor { get; }

    public string RejectReason { get; }

    public bool GrantsDecodeAuthority => false;

    public bool GrantsCommandSubmissionAuthority => false;

    public bool GrantsExecutionAuthority => false;

    public bool GrantsCommitAuthority => false;

    public static AcceleratorCapabilityQueryResult MetadataAvailable(
        AcceleratorCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new AcceleratorCapabilityQueryResult(
            isMetadataAvailable: true,
            descriptor,
            rejectReason: string.Empty);
    }

    public static AcceleratorCapabilityQueryResult Reject(string rejectReason)
    {
        if (string.IsNullOrWhiteSpace(rejectReason))
        {
            throw new ArgumentException("Reject reason is required.", nameof(rejectReason));
        }

        return new AcceleratorCapabilityQueryResult(
            isMetadataAvailable: false,
            descriptor: null,
            rejectReason);
    }
}
