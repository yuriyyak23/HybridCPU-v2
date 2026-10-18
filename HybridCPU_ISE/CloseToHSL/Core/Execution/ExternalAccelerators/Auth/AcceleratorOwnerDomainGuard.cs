using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;

public readonly record struct AcceleratorDomainTag(ulong Value);

public readonly record struct AcceleratorMappingEpoch(ulong Value);

public readonly record struct AcceleratorIommuDomainEpoch(ulong Value);

public enum AcceleratorGuardEvidenceSource : byte
{
    GuardPlane = 0,
    RawVirtualThreadIdHint = 1,
    TokenHandle = 2,
    Telemetry = 3,
    ReplayCertificateIdentity = 4,
    RegistryMetadata = 5
}

public enum AcceleratorGuardSurface : byte
{
    DescriptorAcceptance = 0,
    CapabilityAcceptance = 1,
    SubmitAdmission = 2,
    DeviceExecution = 3,
    Commit = 4,
    ExceptionPublication = 5,
    MappingEpochValidation = 6
}

public enum AcceleratorGuardFault : byte
{
    None = 0,
    MissingGuardEvidence = 1,
    RejectedGuard = 2,
    EvidenceSourceNotAuthority = 3,
    OwnerMismatch = 4,
    DomainMismatch = 5,
    DescriptorOwnerBindingMismatch = 6,
    MappingEpochDrift = 7,
    IommuDomainEpochDrift = 8,
    CapabilityGuardMissing = 9,
    InvalidOwnerCompletion = 10
}

public enum AcceleratorInvalidOwnerCompletionDisposition : byte
{
    None = 0,
    Faulted = 1,
    Abandoned = 2
}

public sealed record AcceleratorGuardEvidence
{
    public required AcceleratorGuardEvidenceSource Source { get; init; }

    public required ushort OwnerVirtualThreadId { get; init; }

    public required uint OwnerContextId { get; init; }

    public required uint OwnerCoreId { get; init; }

    public required uint OwnerPodId { get; init; }

    public required AcceleratorDomainTag DomainTag { get; init; }

    public required ulong ActiveDomainCertificate { get; init; }

    public AcceleratorMappingEpoch MappingEpoch { get; init; }

    public AcceleratorIommuDomainEpoch IommuDomainEpoch { get; init; }

    public ulong EvidenceIdentity { get; init; }

    public string? RegistryAcceleratorId { get; init; }

    public static AcceleratorGuardEvidence FromGuardPlane(
        AcceleratorOwnerBinding ownerBinding,
        ulong activeDomainCertificate,
        AcceleratorMappingEpoch mappingEpoch = default,
        AcceleratorIommuDomainEpoch iommuDomainEpoch = default)
    {
        ArgumentNullException.ThrowIfNull(ownerBinding);
        return new AcceleratorGuardEvidence
        {
            Source = AcceleratorGuardEvidenceSource.GuardPlane,
            OwnerVirtualThreadId = ownerBinding.OwnerVirtualThreadId,
            OwnerContextId = ownerBinding.OwnerContextId,
            OwnerCoreId = ownerBinding.OwnerCoreId,
            OwnerPodId = ownerBinding.OwnerPodId,
            DomainTag = new AcceleratorDomainTag(ownerBinding.DomainTag),
            ActiveDomainCertificate = activeDomainCertificate,
            MappingEpoch = mappingEpoch,
            IommuDomainEpoch = iommuDomainEpoch
        };
    }

    public static AcceleratorGuardEvidence FromEvidencePlane(
        AcceleratorGuardEvidenceSource source,
        AcceleratorOwnerBinding ownerBinding,
        ulong evidenceIdentity = 0,
        string? registryAcceleratorId = null)
    {
        if (source == AcceleratorGuardEvidenceSource.GuardPlane)
        {
            throw new ArgumentException(
                "Use FromGuardPlane for guard-plane evidence.",
                nameof(source));
        }

        AcceleratorGuardEvidence evidence = FromGuardPlane(
            ownerBinding,
            activeDomainCertificate: ownerBinding.DomainTag);
        return evidence with
        {
            Source = source,
            EvidenceIdentity = evidenceIdentity,
            RegistryAcceleratorId = registryAcceleratorId
        };
    }

    public static AcceleratorGuardEvidence FromRawVirtualThreadIdHint(
        ushort rawVirtualThreadId,
        AcceleratorOwnerBinding ownerBinding)
    {
        return FromEvidencePlane(
            AcceleratorGuardEvidenceSource.RawVirtualThreadIdHint,
            ownerBinding) with
        {
            OwnerVirtualThreadId = rawVirtualThreadId
        };
    }
}

public readonly record struct AcceleratorGuardDecision
{
    private AcceleratorGuardDecision(
        AcceleratorGuardSurface surface,
        AcceleratorGuardFault fault,
        AcceleratorOwnerBinding? descriptorOwnerBinding,
        AcceleratorGuardEvidence? evidence,
        LegalityDecision legalityDecision,
        string message)
    {
        Surface = surface;
        Fault = fault;
        DescriptorOwnerBinding = descriptorOwnerBinding;
        Evidence = evidence;
        LegalityDecision = legalityDecision;
        Message = message;
    }

    public AcceleratorGuardSurface Surface { get; }

    public AcceleratorGuardFault Fault { get; }

    public AcceleratorOwnerBinding? DescriptorOwnerBinding { get; }

    public AcceleratorGuardEvidence? Evidence { get; }

    public LegalityDecision LegalityDecision { get; }

    public string Message { get; }

    public AcceleratorMappingEpoch MappingEpoch => Evidence?.MappingEpoch ?? default;

    public AcceleratorIommuDomainEpoch IommuDomainEpoch => Evidence?.IommuDomainEpoch ?? default;

    public bool IsAllowed =>
        Fault == AcceleratorGuardFault.None &&
        LegalityDecision.IsAllowed &&
        Evidence?.Source == AcceleratorGuardEvidenceSource.GuardPlane;

    public static AcceleratorGuardDecision Allow(
        AcceleratorGuardSurface surface,
        AcceleratorOwnerBinding descriptorOwnerBinding,
        AcceleratorGuardEvidence evidence,
        string message)
    {
        ArgumentNullException.ThrowIfNull(descriptorOwnerBinding);
        ArgumentNullException.ThrowIfNull(evidence);
        return new AcceleratorGuardDecision(
            surface,
            AcceleratorGuardFault.None,
            descriptorOwnerBinding,
            evidence,
            LegalityDecision.Allow(
                LegalityAuthoritySource.GuardPlane,
                attemptedReplayCertificateReuse: false),
            message);
    }

    public static AcceleratorGuardDecision Reject(
        AcceleratorGuardSurface surface,
        AcceleratorGuardFault fault,
        AcceleratorOwnerBinding? descriptorOwnerBinding,
        AcceleratorGuardEvidence? evidence,
        RejectKind rejectKind,
        string message)
    {
        if (fault == AcceleratorGuardFault.None)
        {
            throw new ArgumentException(
                "Use Allow for successful guard decisions.",
                nameof(fault));
        }

        bool attemptedReplayCertificateAuthority =
            evidence?.Source == AcceleratorGuardEvidenceSource.ReplayCertificateIdentity;
        return new AcceleratorGuardDecision(
            surface,
            fault,
            descriptorOwnerBinding,
            evidence,
            LegalityDecision.Reject(
                rejectKind,
                CertificateRejectDetail.None,
                LegalityAuthoritySource.GuardPlane,
                attemptedReplayCertificateAuthority),
            message);
    }
}

public sealed record AcceleratorInvalidOwnerCompletionResult
{
    private AcceleratorInvalidOwnerCompletionResult(
        AcceleratorGuardDecision guardDecision,
        AcceleratorInvalidOwnerCompletionDisposition disposition,
        bool privilegedDiagnosticRecorded,
        string message)
    {
        GuardDecision = guardDecision;
        Disposition = disposition;
        PrivilegedDiagnosticRecorded = privilegedDiagnosticRecorded;
        Message = message;
    }

    public AcceleratorGuardDecision GuardDecision { get; }

    public AcceleratorInvalidOwnerCompletionDisposition Disposition { get; }

    public bool UserVisiblePublicationAllowed => false;

    public bool PrivilegedDiagnosticRecorded { get; }

    public string Message { get; }

    public static AcceleratorInvalidOwnerCompletionResult GuardStillValid(
        AcceleratorGuardDecision guardDecision)
    {
        return new AcceleratorInvalidOwnerCompletionResult(
            guardDecision,
            AcceleratorInvalidOwnerCompletionDisposition.None,
            privilegedDiagnosticRecorded: false,
            "Owner/domain guard is still valid; Phase 05 still has no user-visible commit publication path.");
    }

    public static AcceleratorInvalidOwnerCompletionResult InvalidOwner(
        AcceleratorGuardDecision guardDecision,
        bool privilegedDiagnosticRecorded)
    {
        AcceleratorInvalidOwnerCompletionDisposition disposition =
            guardDecision.Fault is AcceleratorGuardFault.OwnerMismatch
                or AcceleratorGuardFault.DomainMismatch
                or AcceleratorGuardFault.MappingEpochDrift
                or AcceleratorGuardFault.IommuDomainEpochDrift
                    ? AcceleratorInvalidOwnerCompletionDisposition.Abandoned
                    : AcceleratorInvalidOwnerCompletionDisposition.Faulted;

        return new AcceleratorInvalidOwnerCompletionResult(
            guardDecision,
            disposition,
            privilegedDiagnosticRecorded,
            "Invalid-owner completion is model evidence only; user-visible architectural publication is forbidden.");
    }
}

public sealed class AcceleratorOwnerDomainGuard
{
    public static AcceleratorOwnerDomainGuard Default { get; } = new();

    public AcceleratorGuardDecision EnsureBeforeDescriptorAcceptance(
        AcceleratorOwnerBinding descriptorOwnerBinding,
        AcceleratorGuardEvidence? guardEvidence)
    {
        return EvaluateGuard(
            AcceleratorGuardSurface.DescriptorAcceptance,
            descriptorOwnerBinding,
            guardEvidence,
            priorAcceptedDecision: null,
            validateEpochs: false);
    }

    public AcceleratorGuardDecision EnsureBeforeCapabilityAcceptance(
        AcceleratorOwnerBinding descriptorOwnerBinding,
        AcceleratorGuardEvidence? guardEvidence)
    {
        return EvaluateGuard(
            AcceleratorGuardSurface.CapabilityAcceptance,
            descriptorOwnerBinding,
            guardEvidence,
            priorAcceptedDecision: null,
            validateEpochs: false);
    }

    public AcceleratorGuardDecision EnsureBeforeCapabilityAcceptance(
        AcceleratorOwnerBinding descriptorOwnerBinding,
        AcceleratorGuardDecision descriptorGuardDecision)
    {
        return EvaluateGuard(
            AcceleratorGuardSurface.CapabilityAcceptance,
            descriptorOwnerBinding,
            descriptorGuardDecision.Evidence,
            descriptorGuardDecision,
            validateEpochs: false);
    }

    public AcceleratorGuardDecision EnsureBeforeSubmit(
        AcceleratorCommandDescriptor descriptor,
        AcceleratorGuardEvidence? guardEvidence)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return EvaluateGuard(
            AcceleratorGuardSurface.SubmitAdmission,
            descriptor.OwnerBinding,
            guardEvidence,
            descriptor.OwnerGuardDecision,
            validateEpochs: true);
    }

    public AcceleratorGuardDecision EnsureBeforeDeviceExecution(
        AcceleratorCommandDescriptor descriptor,
        AcceleratorGuardEvidence? guardEvidence)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return EvaluateGuard(
            AcceleratorGuardSurface.DeviceExecution,
            descriptor.OwnerBinding,
            guardEvidence,
            descriptor.OwnerGuardDecision,
            validateEpochs: true);
    }

    public AcceleratorGuardDecision EnsureBeforeCommit(
        AcceleratorCommandDescriptor descriptor,
        AcceleratorGuardEvidence? guardEvidence)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return EvaluateGuard(
            AcceleratorGuardSurface.Commit,
            descriptor.OwnerBinding,
            guardEvidence,
            descriptor.OwnerGuardDecision,
            validateEpochs: true);
    }

    public AcceleratorGuardDecision EnsureBeforeExceptionPublication(
        AcceleratorCommandDescriptor descriptor,
        AcceleratorGuardEvidence? guardEvidence)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return EvaluateGuard(
            AcceleratorGuardSurface.ExceptionPublication,
            descriptor.OwnerBinding,
            guardEvidence,
            descriptor.OwnerGuardDecision,
            validateEpochs: true);
    }

    public AcceleratorMappingEpoch CaptureMappingEpoch(
        AcceleratorGuardEvidence guardEvidence)
    {
        ArgumentNullException.ThrowIfNull(guardEvidence);
        if (guardEvidence.Source != AcceleratorGuardEvidenceSource.GuardPlane)
        {
            throw new InvalidOperationException(
                "L7-SDC mapping epoch evidence must come from the guard plane.");
        }

        return guardEvidence.MappingEpoch;
    }

    public AcceleratorGuardDecision ValidateMappingEpoch(
        AcceleratorGuardDecision acceptedGuardDecision,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        if (acceptedGuardDecision.DescriptorOwnerBinding is null)
        {
            return AcceleratorGuardDecision.Reject(
                AcceleratorGuardSurface.MappingEpochValidation,
                AcceleratorGuardFault.MissingGuardEvidence,
                descriptorOwnerBinding: null,
                currentGuardEvidence,
                RejectKind.OwnerMismatch,
                "L7-SDC mapping epoch validation requires a prior guard-backed descriptor decision.");
        }

        return EvaluateGuard(
            AcceleratorGuardSurface.MappingEpochValidation,
            acceptedGuardDecision.DescriptorOwnerBinding,
            currentGuardEvidence,
            acceptedGuardDecision,
            validateEpochs: true);
    }

    public AcceleratorInvalidOwnerCompletionResult MarkAbandonedOnInvalidOwner(
        AcceleratorCommandDescriptor descriptor,
        AcceleratorGuardEvidence? completionEvidence,
        bool recordPrivilegedDiagnostic = true)
    {
        AcceleratorGuardDecision completionGuard =
            EnsureBeforeCommit(descriptor, completionEvidence);
        return completionGuard.IsAllowed
            ? AcceleratorInvalidOwnerCompletionResult.GuardStillValid(completionGuard)
            : AcceleratorInvalidOwnerCompletionResult.InvalidOwner(
                completionGuard,
                recordPrivilegedDiagnostic);
    }

    public bool IsDescriptorGuardBacked(
        AcceleratorCommandDescriptor descriptor,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        AcceleratorGuardDecision decision = descriptor.OwnerGuardDecision;
        if (!decision.IsAllowed)
        {
            message = string.IsNullOrWhiteSpace(decision.Message)
                ? "L7-SDC descriptor sideband lacks an accepted owner/domain guard decision."
                : decision.Message;
            return false;
        }

        if (decision.DescriptorOwnerBinding is null ||
            !decision.DescriptorOwnerBinding.Equals(descriptor.OwnerBinding))
        {
            message = "L7-SDC descriptor guard decision does not match descriptor owner binding.";
            return false;
        }

        if (decision.Evidence?.Source != AcceleratorGuardEvidenceSource.GuardPlane ||
            decision.LegalityDecision.AuthoritySource != LegalityAuthoritySource.GuardPlane ||
            decision.LegalityDecision.AttemptedReplayCertificateReuse)
        {
            message = "L7-SDC descriptor acceptance requires guard-plane authority; evidence-plane identity cannot authorize sideband admission.";
            return false;
        }

        message = "L7-SDC descriptor sideband is guard-backed.";
        return true;
    }

    private static AcceleratorGuardDecision EvaluateGuard(
        AcceleratorGuardSurface surface,
        AcceleratorOwnerBinding descriptorOwnerBinding,
        AcceleratorGuardEvidence? guardEvidence,
        AcceleratorGuardDecision? priorAcceptedDecision,
        bool validateEpochs)
    {
        ArgumentNullException.ThrowIfNull(descriptorOwnerBinding);

        if (guardEvidence is null)
        {
            return Reject(
                surface,
                AcceleratorGuardFault.MissingGuardEvidence,
                descriptorOwnerBinding,
                guardEvidence,
                RejectKind.OwnerMismatch,
                $"{surface}: L7-SDC owner/domain guard evidence is required before authority acceptance.");
        }

        if (guardEvidence.Source != AcceleratorGuardEvidenceSource.GuardPlane)
        {
            return Reject(
                surface,
                AcceleratorGuardFault.EvidenceSourceNotAuthority,
                descriptorOwnerBinding,
                guardEvidence,
                RejectKind.OwnerMismatch,
                $"{surface}: {guardEvidence.Source} is evidence only and cannot authorize L7-SDC owner/domain acceptance.");
        }

        if (priorAcceptedDecision.HasValue)
        {
            AcceleratorGuardDecision prior = priorAcceptedDecision.Value;
            if (!prior.IsAllowed)
            {
                return Reject(
                    surface,
                    AcceleratorGuardFault.RejectedGuard,
                    descriptorOwnerBinding,
                    guardEvidence,
                    prior.LegalityDecision.RejectKind == RejectKind.None
                        ? RejectKind.OwnerMismatch
                        : prior.LegalityDecision.RejectKind,
                    $"{surface}: prior L7-SDC guard decision was not accepted.");
            }

            if (prior.DescriptorOwnerBinding is null ||
                !prior.DescriptorOwnerBinding.Equals(descriptorOwnerBinding))
            {
                return Reject(
                    surface,
                    AcceleratorGuardFault.DescriptorOwnerBindingMismatch,
                    descriptorOwnerBinding,
                    guardEvidence,
                    RejectKind.OwnerMismatch,
                    $"{surface}: prior L7-SDC guard decision does not match descriptor owner binding.");
            }

            if (prior.Evidence?.Source != AcceleratorGuardEvidenceSource.GuardPlane ||
                prior.LegalityDecision.AuthoritySource != LegalityAuthoritySource.GuardPlane ||
                prior.LegalityDecision.AttemptedReplayCertificateReuse)
            {
                return Reject(
                    surface,
                    AcceleratorGuardFault.EvidenceSourceNotAuthority,
                    descriptorOwnerBinding,
                    guardEvidence,
                    RejectKind.OwnerMismatch,
                    $"{surface}: prior L7-SDC guard decision was not produced by the guard plane.");
            }

            if (validateEpochs)
            {
                if (!prior.MappingEpoch.Equals(guardEvidence.MappingEpoch))
                {
                    return Reject(
                        surface,
                        AcceleratorGuardFault.MappingEpochDrift,
                        descriptorOwnerBinding,
                        guardEvidence,
                        RejectKind.EpochMismatch,
                        $"{surface}: L7-SDC mapping epoch drift prevents authority reuse.");
                }

                if (!prior.IommuDomainEpoch.Equals(guardEvidence.IommuDomainEpoch))
                {
                    return Reject(
                        surface,
                        AcceleratorGuardFault.IommuDomainEpochDrift,
                        descriptorOwnerBinding,
                        guardEvidence,
                        RejectKind.EpochMismatch,
                        $"{surface}: L7-SDC IOMMU-domain epoch drift prevents authority reuse.");
                }
            }
        }

        if (descriptorOwnerBinding.OwnerVirtualThreadId != guardEvidence.OwnerVirtualThreadId ||
            descriptorOwnerBinding.OwnerContextId != guardEvidence.OwnerContextId ||
            descriptorOwnerBinding.OwnerCoreId != guardEvidence.OwnerCoreId ||
            descriptorOwnerBinding.OwnerPodId != guardEvidence.OwnerPodId)
        {
            return Reject(
                surface,
                AcceleratorGuardFault.OwnerMismatch,
                descriptorOwnerBinding,
                guardEvidence,
                RejectKind.OwnerMismatch,
                $"{surface}: L7-SDC descriptor owner binding does not match guard-plane owner evidence.");
        }

        if (descriptorOwnerBinding.DomainTag != guardEvidence.DomainTag.Value)
        {
            return Reject(
                surface,
                AcceleratorGuardFault.DomainMismatch,
                descriptorOwnerBinding,
                guardEvidence,
                RejectKind.DomainMismatch,
                $"{surface}: L7-SDC descriptor domain tag does not match guard-plane domain evidence.");
        }

        if (!IsDomainCoveredByCertificate(
                guardEvidence.DomainTag.Value,
                guardEvidence.ActiveDomainCertificate))
        {
            return Reject(
                surface,
                AcceleratorGuardFault.DomainMismatch,
                descriptorOwnerBinding,
                guardEvidence,
                RejectKind.DomainMismatch,
                $"{surface}: L7-SDC descriptor domain is not covered by the active guard-plane domain certificate.");
        }

        return AcceleratorGuardDecision.Allow(
            surface,
            descriptorOwnerBinding,
            guardEvidence,
            $"{surface}: L7-SDC owner/domain guard succeeded.");
    }

    private static bool IsDomainCoveredByCertificate(
        ulong ownerDomainTag,
        ulong activeDomainCertificate)
    {
        if (ownerDomainTag == 0)
        {
            return activeDomainCertificate == 0;
        }

        return activeDomainCertificate == 0 ||
               (ownerDomainTag & activeDomainCertificate) != 0;
    }

    private static AcceleratorGuardDecision Reject(
        AcceleratorGuardSurface surface,
        AcceleratorGuardFault fault,
        AcceleratorOwnerBinding? descriptorOwnerBinding,
        AcceleratorGuardEvidence? guardEvidence,
        RejectKind rejectKind,
        string message)
    {
        return AcceleratorGuardDecision.Reject(
            surface,
            fault,
            descriptorOwnerBinding,
            guardEvidence,
            rejectKind,
            message);
    }
}
