using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core;

internal enum VirtualizationAdmissionIssueDecision : byte
{
    IssuedForFaultOnlyTransport = 0,
    NotVmxMicroOp = 1,
    UnknownFrozenOpcode = 2,
    InvalidVirtualThread = 3,
    OwnerVirtualThreadMismatch = 4,
    OwnerContextMismatch = 5,
    DomainMismatch = 6,
    InvalidSourceSlot = 7,
    WorkingSlotMismatch = 8,
    DuplicateAttempt = 9,
}

internal enum VirtualizationAdmissionValidationDecision : byte
{
    ValidForFaultOnlyTransport = 0,
    MissingCertificate = 1,
    IssuerMismatch = 2,
    IssuanceNotLive = 3,
    IssuerGenerationMismatch = 4,
    OpcodeOrOperationMismatch = 5,
    VirtualThreadMismatch = 6,
    OwnerContextMismatch = 7,
    DomainMismatch = 8,
    SourceOrWorkingSlotMismatch = 9,
    BundleOrReplayMismatch = 10,
}

internal readonly record struct VirtualizationAdmissionIssueResult(
    VirtualizationAdmissionIssueDecision Decision,
    SafetyVerifier.VirtualizationAdmissionCertificate? Certificate,
    string Reason)
{
    internal bool IsIssued =>
        Decision == VirtualizationAdmissionIssueDecision.IssuedForFaultOnlyTransport &&
        Certificate is not null;
}

internal readonly record struct VirtualizationAdmissionValidationResult(
    VirtualizationAdmissionValidationDecision Decision,
    string Reason)
{
    internal bool IsValidForFaultOnlyTransport =>
        Decision == VirtualizationAdmissionValidationDecision.ValidForFaultOnlyTransport;
}

public partial class SafetyVerifier
{
    /// <summary>
    /// Opaque E1 admission issued only by one live SafetyVerifier instance after the
    /// existing legality decision and successful Stage-B lane materialization.
    /// It is deliberately incapable of authorizing backend, completion or retire.
    /// </summary>
    internal sealed class VirtualizationAdmissionCertificate
    {
        private readonly object _issuerSeal;

        internal VirtualizationAdmissionCertificate(
            object issuerSeal,
            ulong issuerGeneration,
            ulong attemptId,
            ushort opcode,
            VmxOperationKind operation,
            int virtualThreadId,
            int ownerContextId,
            ulong domainTag,
            int sourceSlotId,
            int workingSlotId,
            ulong bundleIdentity,
            ulong replayEpoch,
            ulong carrierIdentityDigest)
        {
            _issuerSeal = issuerSeal;
            IssuerGeneration = issuerGeneration;
            AttemptId = attemptId;
            Opcode = opcode;
            Operation = operation;
            VirtualThreadId = virtualThreadId;
            OwnerContextId = ownerContextId;
            DomainTag = domainTag;
            SourceSlotId = sourceSlotId;
            WorkingSlotId = workingSlotId;
            BundleIdentity = bundleIdentity;
            ReplayEpoch = replayEpoch;
            CarrierIdentityDigest = carrierIdentityDigest;
        }

        internal ulong IssuerGeneration { get; }
        internal uint SchemaVersion => 1;
        internal ulong AttemptId { get; }
        internal ushort Opcode { get; }
        internal VmxOperationKind Operation { get; }
        internal int VirtualThreadId { get; }
        internal int OwnerContextId { get; }
        internal ulong DomainTag { get; }
        internal int SourceSlotId { get; }
        internal int WorkingSlotId { get; }
        internal ulong BundleIdentity { get; }
        internal ulong ReplayEpoch { get; }
        internal ulong AttemptEpoch => ReplayEpoch;
        internal ulong ReplayGeneration => IssuerGeneration;
        internal ulong CarrierIdentityDigest { get; }

        // E1 binds explicit absence for identities that require later neutral owners.
        internal bool HasAcceptedNumericLeaf => false;
        internal bool HasMaterializedAddressSpaceIdentity => false;
        internal ulong AddressSpaceTag => 0;
        internal bool HasMaterializedDescriptorIdentity => false;
        internal ulong DescriptorIdentityDigest => 0;
        internal ulong DescriptorEpoch => 0;
        internal bool HasCapabilityGrantIdentity => false;
        internal ulong CapabilityGrantIdentity => 0;
        internal ulong CapabilityRevocationEpoch => 0;
        internal bool HasEvidencePolicyIdentity => false;
        internal ulong EvidencePolicyDigest => 0;
        internal ulong EvidenceEpoch => 0;
        internal bool HasRestoreGeneration => false;
        internal ulong RestoreGeneration => 0;
        internal bool BackendExecutionAuthorized => false;
        internal bool CompletionPublicationAuthorized => false;
        internal bool RetirePublicationAuthorized => false;

        internal bool WasIssuedBy(object issuerSeal) =>
            ReferenceEquals(_issuerSeal, issuerSeal);
    }

    private readonly object _virtualizationAdmissionIssuerSeal = new();
    private sealed record LiveVirtualizationAdmission(
        ulong AttemptId,
        ulong IssuerGeneration);

    private readonly ConditionalWeakTable<
        VirtualizationAdmissionCertificate,
        LiveVirtualizationAdmission> _liveVirtualizationAdmissions = new();
    private ulong _virtualizationAdmissionIssuerGeneration = 1;
    private ulong _nextVirtualizationAdmissionAttemptId = 1;

    internal VirtualizationAdmissionIssueResult IssueVirtualizationAdmissionAfterStageB(
        ReplayPhaseContext replayPhase,
        SmtBundleMetadata4Way bundleMetadata,
        MicroOp candidate,
        int sourceSlotId,
        int selectedLane)
    {
        if (candidate is not VmxMicroOp vmx)
        {
            return DenyIssue(
                VirtualizationAdmissionIssueDecision.NotVmxMicroOp,
                "Only a canonical VmxMicroOp may receive E1 virtualization admission.");
        }

        if (!vmx.TryResolveFrozenOperation(out VmxOperationKind operation))
        {
            return DenyIssue(
                VirtualizationAdmissionIssueDecision.UnknownFrozenOpcode,
                "The candidate opcode is not in the frozen VMX compatibility vocabulary.");
        }

        if (!VtId.TryCreate(vmx.VirtualThreadId, out _))
        {
            return DenyIssue(
                VirtualizationAdmissionIssueDecision.InvalidVirtualThread,
                "The VMX candidate has no valid canonical virtual-thread identity.");
        }

        if (vmx.OwnerThreadId != vmx.VirtualThreadId ||
            bundleMetadata.OwnerVirtualThreadId != vmx.VirtualThreadId)
        {
            return DenyIssue(
                VirtualizationAdmissionIssueDecision.OwnerVirtualThreadMismatch,
                "Owner, candidate and bundle virtual-thread identities must match.");
        }

        if (bundleMetadata.HasKnownOwnerContext &&
            bundleMetadata.OwnerContextId != vmx.OwnerContextId)
        {
            return DenyIssue(
                VirtualizationAdmissionIssueDecision.OwnerContextMismatch,
                "The live bundle owner context does not match the VMX candidate.");
        }

        if (bundleMetadata.HasDomainRestriction &&
            bundleMetadata.OwnerDomainTag != vmx.Placement.DomainTag)
        {
            return DenyIssue(
                VirtualizationAdmissionIssueDecision.DomainMismatch,
                "The live bundle domain does not match the VMX candidate.");
        }

        SlotPlacementMetadata placement = vmx.Placement;
        if (placement.PinningKind != SlotPinningKind.HardPinned ||
            placement.RequiredSlotClass != SlotClass.SystemSingleton ||
            placement.PinnedLaneId != 7 ||
            sourceSlotId != 7)
        {
            return DenyIssue(
                VirtualizationAdmissionIssueDecision.InvalidSourceSlot,
                "E1 recognizes only the frozen system-singleton VMX source slot.");
        }

        if (selectedLane != placement.PinnedLaneId)
        {
            return DenyIssue(
                VirtualizationAdmissionIssueDecision.WorkingSlotMismatch,
                "The Stage-B working lane does not match the frozen VMX source slot.");
        }

        if (vmx.VirtualizationAdmission is not null)
        {
            return DenyIssue(
                VirtualizationAdmissionIssueDecision.DuplicateAttempt,
                "A VMX carrier cannot receive a second E1 admission certificate.");
        }

        ulong attemptId = AllocateVirtualizationAdmissionAttemptId();
        ushort opcode = unchecked((ushort)vmx.OpCode);
        var certificate = new VirtualizationAdmissionCertificate(
            _virtualizationAdmissionIssuerSeal,
            _virtualizationAdmissionIssuerGeneration,
            attemptId,
            opcode,
            operation,
            vmx.VirtualThreadId,
            vmx.OwnerContextId,
            vmx.Placement.DomainTag,
            sourceSlotId,
            selectedLane,
            ComputeBundleIdentity(replayPhase, bundleMetadata),
            replayPhase.EpochId,
            ComputeCarrierIdentityDigest(vmx));
        _liveVirtualizationAdmissions.Add(
            certificate,
            new LiveVirtualizationAdmission(
                attemptId,
                _virtualizationAdmissionIssuerGeneration));

        return new VirtualizationAdmissionIssueResult(
            VirtualizationAdmissionIssueDecision.IssuedForFaultOnlyTransport,
            certificate,
            "SafetyVerifier issued an E1 certificate for fault-only canonical transport.");
    }

    internal VirtualizationAdmissionValidationResult ValidateVirtualizationAdmission(
        ReplayPhaseContext replayPhase,
        SmtBundleMetadata4Way bundleMetadata,
        MicroOp candidate,
        int sourceSlotId,
        int selectedLane,
        VirtualizationAdmissionCertificate? certificate)
    {
        if (certificate is null)
            return DenyValidation(VirtualizationAdmissionValidationDecision.MissingCertificate, "Certificate is missing.");

        if (!certificate.WasIssuedBy(_virtualizationAdmissionIssuerSeal))
            return DenyValidation(VirtualizationAdmissionValidationDecision.IssuerMismatch, "Certificate issuer mismatch.");

        if (!_liveVirtualizationAdmissions.TryGetValue(
                certificate,
                out LiveVirtualizationAdmission? live) ||
            live.AttemptId != certificate.AttemptId)
        {
            return DenyValidation(VirtualizationAdmissionValidationDecision.IssuanceNotLive, "Certificate issuance is not live.");
        }

        if (certificate.IssuerGeneration != _virtualizationAdmissionIssuerGeneration ||
            live.IssuerGeneration != _virtualizationAdmissionIssuerGeneration)
        {
            return DenyValidation(
                VirtualizationAdmissionValidationDecision.IssuerGenerationMismatch,
                "Certificate was invalidated by an issuer-generation change.");
        }

        if (candidate is not VmxMicroOp vmx ||
            !vmx.TryResolveFrozenOperation(out VmxOperationKind operation) ||
            certificate.Opcode != unchecked((ushort)vmx.OpCode) ||
            certificate.Operation != operation ||
            certificate.CarrierIdentityDigest != ComputeCarrierIdentityDigest(vmx))
        {
            return DenyValidation(
                VirtualizationAdmissionValidationDecision.OpcodeOrOperationMismatch,
                "Opcode, operation or carrier identity changed after issuance.");
        }

        if (certificate.VirtualThreadId != vmx.VirtualThreadId ||
            vmx.OwnerThreadId != vmx.VirtualThreadId ||
            bundleMetadata.OwnerVirtualThreadId != vmx.VirtualThreadId)
        {
            return DenyValidation(VirtualizationAdmissionValidationDecision.VirtualThreadMismatch, "Virtual-thread identity mismatch.");
        }

        if (certificate.OwnerContextId != vmx.OwnerContextId ||
            (bundleMetadata.HasKnownOwnerContext && bundleMetadata.OwnerContextId != vmx.OwnerContextId))
        {
            return DenyValidation(VirtualizationAdmissionValidationDecision.OwnerContextMismatch, "Owner context mismatch.");
        }

        if (certificate.DomainTag != vmx.Placement.DomainTag ||
            (bundleMetadata.HasDomainRestriction && bundleMetadata.OwnerDomainTag != vmx.Placement.DomainTag))
        {
            return DenyValidation(VirtualizationAdmissionValidationDecision.DomainMismatch, "Domain identity mismatch.");
        }

        if (certificate.SourceSlotId != sourceSlotId ||
            sourceSlotId != vmx.Placement.PinnedLaneId ||
            certificate.WorkingSlotId != selectedLane ||
            selectedLane != vmx.Placement.PinnedLaneId)
        {
            return DenyValidation(
                VirtualizationAdmissionValidationDecision.SourceOrWorkingSlotMismatch,
                "Source or working slot identity mismatch.");
        }

        if (certificate.BundleIdentity != ComputeBundleIdentity(replayPhase, bundleMetadata) ||
            certificate.ReplayEpoch != replayPhase.EpochId)
        {
            return DenyValidation(
                VirtualizationAdmissionValidationDecision.BundleOrReplayMismatch,
                "Bundle or replay identity mismatch.");
        }

        return new VirtualizationAdmissionValidationResult(
            VirtualizationAdmissionValidationDecision.ValidForFaultOnlyTransport,
            "Certificate is valid only for fault-only canonical transport.");
    }

    internal void InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason reason)
    {
        _ = reason;
        unchecked
        {
            _virtualizationAdmissionIssuerGeneration++;
            if (_virtualizationAdmissionIssuerGeneration == 0)
                _virtualizationAdmissionIssuerGeneration = 1;
        }
    }

    private ulong AllocateVirtualizationAdmissionAttemptId()
    {
        ulong attemptId = _nextVirtualizationAdmissionAttemptId++;
        if (attemptId == 0)
        {
            attemptId = _nextVirtualizationAdmissionAttemptId++;
        }

        return attemptId;
    }

    private static ulong ComputeBundleIdentity(
        ReplayPhaseContext replayPhase,
        SmtBundleMetadata4Way bundleMetadata)
    {
        ulong hash = 1469598103934665603UL;
        Mix(ref hash, replayPhase.EpochId);
        Mix(ref hash, replayPhase.CachedPc);
        Mix(ref hash, replayPhase.CompletedReplays);
        Mix(ref hash, unchecked((ulong)(uint)bundleMetadata.OwnerVirtualThreadId));
        Mix(ref hash, unchecked((ulong)(uint)bundleMetadata.OwnerContextId));
        Mix(ref hash, bundleMetadata.OwnerDomainTag);
        Mix(ref hash, bundleMetadata.BundleDomainShapeId);
        Mix(ref hash, unchecked((ulong)(uint)bundleMetadata.OperationCount));
        return hash;
    }

    private static ulong ComputeCarrierIdentityDigest(VmxMicroOp vmx)
    {
        ulong hash = 1469598103934665603UL;
        Mix(ref hash, unchecked((ushort)vmx.OpCode));
        Mix(ref hash, vmx.Rd);
        Mix(ref hash, vmx.Rs1);
        Mix(ref hash, vmx.Rs2);
        Mix(ref hash, unchecked((ulong)(uint)vmx.Placement.PinnedLaneId));
        return hash;
    }

    private static void Mix(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }

    private static VirtualizationAdmissionIssueResult DenyIssue(
        VirtualizationAdmissionIssueDecision decision,
        string reason) =>
        new(decision, null, reason);

    private static VirtualizationAdmissionValidationResult DenyValidation(
        VirtualizationAdmissionValidationDecision decision,
        string reason) =>
        new(decision, reason);
}
