using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core;

public enum SecureOutputManifestEntryKind : byte
{
    RequestState = 0,
    InternalBackendResult = 1,
    InternalCompletionRecord = 2,
    GuestVisibleOutput = 3,
    RetireVisibleState = 4,
    RecomputedAfterRestoreState = 5,
}

public enum SecureOutputManifestClassificationDecision : byte
{
    EntryClassifiedForManifestOnly = 0,
    CompleteManifestClassified = 1,
    DeniedMissingManifestEntry = 2,
    DeniedOwnerPathUnclassified = 3,
    DeniedWrongPayloadClass = 4,
    DeniedPrivateMemoryContract = 5,
    DeniedHostOwnedEvidence = 6,
    DeniedDebugTraceAsGuestState = 7,
    DeniedVmcsProjectionAuthority = 8,
    DeniedCompatibilityMetadataAuthority = 9,
    DeniedRawSecret = 10,
    DeniedActiveHostPointer = 11,
    DeniedRecomputedStateRestoreProof = 12,
}

public readonly record struct SecureOutputManifestEntry(
    SecureOutputManifestEntryKind Kind,
    SecureCheckpointPayloadClass PayloadClass,
    bool OwnerPathClassified,
    bool RestoreValidationProven)
{
    public static SecureOutputManifestEntry RequestState() =>
        new(
            SecureOutputManifestEntryKind.RequestState,
            SecureCheckpointPayloadClass.SecurePolicyDescriptor,
            OwnerPathClassified: true,
            RestoreValidationProven: true);

    public static SecureOutputManifestEntry InternalBackendResult() =>
        new(
            SecureOutputManifestEntryKind.InternalBackendResult,
            SecureCheckpointPayloadClass.SecurePolicyDescriptor,
            OwnerPathClassified: true,
            RestoreValidationProven: true);

    public static SecureOutputManifestEntry InternalCompletionRecord() =>
        new(
            SecureOutputManifestEntryKind.InternalCompletionRecord,
            SecureCheckpointPayloadClass.SecurePolicyDescriptor,
            OwnerPathClassified: true,
            RestoreValidationProven: true);

    public static SecureOutputManifestEntry GuestVisibleOutput() =>
        new(
            SecureOutputManifestEntryKind.GuestVisibleOutput,
            SecureCheckpointPayloadClass.GuestVisibleState,
            OwnerPathClassified: true,
            RestoreValidationProven: true);

    public static SecureOutputManifestEntry RetireVisibleState() =>
        new(
            SecureOutputManifestEntryKind.RetireVisibleState,
            SecureCheckpointPayloadClass.GuestVisibleState,
            OwnerPathClassified: true,
            RestoreValidationProven: true);

    public static SecureOutputManifestEntry RecomputedAfterRestoreState() =>
        new(
            SecureOutputManifestEntryKind.RecomputedAfterRestoreState,
            SecureCheckpointPayloadClass.SecurePolicyDescriptor,
            OwnerPathClassified: true,
            RestoreValidationProven: true);
}

public readonly record struct SecureOutputManifestClassificationResult(
    SecureOutputManifestClassificationDecision Decision,
    SecureOutputManifestEntryKind EntryKind,
    SecureCheckpointPayloadDecision PayloadDecision,
    bool ManifestClassified,
    bool CheckpointPayloadIncluded,
    bool RestoreRevalidationRequired,
    bool CreatesRuntimeAuthority,
    bool CreatesCompletionPublicationAuthority,
    bool CreatesRetirePublicationAuthority,
    string Reason)
{
    public bool IsDenied => !ManifestClassified;

    public bool CreatesAnyRuntimeOrPublicationAuthority =>
        CreatesRuntimeAuthority ||
        CreatesCompletionPublicationAuthority ||
        CreatesRetirePublicationAuthority;

    public static SecureOutputManifestClassificationResult Classified(
        SecureOutputManifestClassificationDecision decision,
        SecureOutputManifestEntryKind entryKind,
        SecureCheckpointPayloadDecision payloadDecision,
        bool checkpointPayloadIncluded,
        bool restoreRevalidationRequired,
        string reason) =>
        new(
            decision,
            entryKind,
            payloadDecision,
            ManifestClassified: true,
            checkpointPayloadIncluded,
            restoreRevalidationRequired,
            CreatesRuntimeAuthority: false,
            CreatesCompletionPublicationAuthority: false,
            CreatesRetirePublicationAuthority: false,
            reason);

    public static SecureOutputManifestClassificationResult Denied(
        SecureOutputManifestClassificationDecision decision,
        SecureOutputManifestEntryKind entryKind,
        SecureCheckpointPayloadDecision payloadDecision,
        string reason) =>
        new(
            decision,
            entryKind,
            payloadDecision,
            ManifestClassified: false,
            CheckpointPayloadIncluded: false,
            RestoreRevalidationRequired: false,
            CreatesRuntimeAuthority: false,
            CreatesCompletionPublicationAuthority: false,
            CreatesRetirePublicationAuthority: false,
            reason);
}

public sealed class SecureOutputManifestClassificationPolicy
{
    private static readonly SecureOutputManifestEntryKind[] RequiredEntries =
    {
        SecureOutputManifestEntryKind.RequestState,
        SecureOutputManifestEntryKind.InternalBackendResult,
        SecureOutputManifestEntryKind.InternalCompletionRecord,
        SecureOutputManifestEntryKind.GuestVisibleOutput,
        SecureOutputManifestEntryKind.RetireVisibleState,
        SecureOutputManifestEntryKind.RecomputedAfterRestoreState,
    };

    public static SecureOutputManifestClassificationPolicy FailClosed { get; } = new();

    public static IReadOnlyList<SecureOutputManifestEntryKind> RequiredPositivePathEntries { get; } =
        Array.AsReadOnly(RequiredEntries);

    public SecureOutputManifestClassificationResult ClassifyEntry(
        SecureOutputManifestEntry entry,
        SecurePrivateMemorySealedPayloadContract? privateMemoryContract = null)
    {
        if (!entry.OwnerPathClassified)
        {
            return Deny(
                SecureOutputManifestClassificationDecision.DeniedOwnerPathUnclassified,
                entry.Kind,
                SecureCheckpointPayloadDecision.Allowed,
                "Secure output manifest entries require owner/path/reachability classification.");
        }

        SecureCheckpointPayloadDecision payloadDecision =
            ClassifyPayload(entry.PayloadClass, privateMemoryContract);
        if (payloadDecision != SecureCheckpointPayloadDecision.Allowed)
        {
            return Deny(MapPayloadDenial(entry.PayloadClass, payloadDecision), entry.Kind, payloadDecision, PayloadReason(payloadDecision));
        }

        return entry.Kind switch
        {
            SecureOutputManifestEntryKind.RequestState =>
                ClassifyRequestState(entry),

            SecureOutputManifestEntryKind.InternalBackendResult =>
                ClassifiedManifestOnly(
                    entry.Kind,
                    payloadDecision,
                    "Internal backend result is manifest coverage only; it is not checkpoint authority."),

            SecureOutputManifestEntryKind.InternalCompletionRecord =>
                ClassifiedManifestOnly(
                    entry.Kind,
                    payloadDecision,
                    "Internal completion record is manifest coverage only; it is not checkpoint or restore authority."),

            SecureOutputManifestEntryKind.GuestVisibleOutput =>
                ClassifyGuestVisibleOutput(entry, payloadDecision),

            SecureOutputManifestEntryKind.RetireVisibleState =>
                ClassifyRetireVisibleState(entry, payloadDecision),

            SecureOutputManifestEntryKind.RecomputedAfterRestoreState =>
                ClassifyRecomputedAfterRestore(entry, payloadDecision),

            _ => Deny(
                SecureOutputManifestClassificationDecision.DeniedWrongPayloadClass,
                entry.Kind,
                payloadDecision,
                "Secure output manifest entry kind is not classified."),
        };
    }

    public SecureOutputManifestClassificationResult ClassifyManifest(
        IReadOnlyList<SecureOutputManifestEntry> entries,
        SecurePrivateMemorySealedPayloadContract? privateMemoryContract = null)
    {
        foreach (SecureOutputManifestEntryKind required in RequiredEntries)
        {
            if (!ContainsEntry(entries, required))
            {
                return Deny(
                    SecureOutputManifestClassificationDecision.DeniedMissingManifestEntry,
                    required,
                    SecureCheckpointPayloadDecision.Allowed,
                    "Secure output manifest is missing a required positive-path entry.");
            }
        }

        foreach (SecureOutputManifestEntry entry in entries)
        {
            SecureOutputManifestClassificationResult result =
                ClassifyEntry(entry, privateMemoryContract);
            if (result.IsDenied)
            {
                return result;
            }
        }

        return SecureOutputManifestClassificationResult.Classified(
            SecureOutputManifestClassificationDecision.CompleteManifestClassified,
            SecureOutputManifestEntryKind.RecomputedAfterRestoreState,
            SecureCheckpointPayloadDecision.Allowed,
            checkpointPayloadIncluded: false,
            restoreRevalidationRequired: true,
            "Secure output manifest coverage is complete for classification evidence only.");
    }

    private static SecureOutputManifestClassificationResult ClassifyRequestState(
        SecureOutputManifestEntry entry)
    {
        if (entry.PayloadClass is not SecureCheckpointPayloadClass.SecurePolicyDescriptor
            and not SecureCheckpointPayloadClass.GuestVisibleState)
        {
            return DenyWrongPayload(entry);
        }

        return SecureOutputManifestClassificationResult.Classified(
            SecureOutputManifestClassificationDecision.EntryClassifiedForManifestOnly,
            entry.Kind,
            SecureCheckpointPayloadDecision.Allowed,
            checkpointPayloadIncluded: true,
            restoreRevalidationRequired: true,
            "Request state is classified as descriptor or guest-visible state.");
    }

    private static SecureOutputManifestClassificationResult ClassifyGuestVisibleOutput(
        SecureOutputManifestEntry entry,
        SecureCheckpointPayloadDecision payloadDecision)
    {
        if (entry.PayloadClass is not SecureCheckpointPayloadClass.GuestVisibleState
            and not SecureCheckpointPayloadClass.SecureSharedMemory)
        {
            return DenyWrongPayload(entry);
        }

        return SecureOutputManifestClassificationResult.Classified(
            SecureOutputManifestClassificationDecision.EntryClassifiedForManifestOnly,
            entry.Kind,
            payloadDecision,
            checkpointPayloadIncluded: true,
            restoreRevalidationRequired: true,
            "Guest-visible output is classified as guest-visible/shared state only.");
    }

    private static SecureOutputManifestClassificationResult ClassifyRetireVisibleState(
        SecureOutputManifestEntry entry,
        SecureCheckpointPayloadDecision payloadDecision)
    {
        if (entry.PayloadClass != SecureCheckpointPayloadClass.GuestVisibleState)
        {
            return DenyWrongPayload(entry);
        }

        return SecureOutputManifestClassificationResult.Classified(
            SecureOutputManifestClassificationDecision.EntryClassifiedForManifestOnly,
            entry.Kind,
            payloadDecision,
            checkpointPayloadIncluded: true,
            restoreRevalidationRequired: true,
            "Retire-visible state is classified only as guest-visible architectural state.");
    }

    private static SecureOutputManifestClassificationResult ClassifyRecomputedAfterRestore(
        SecureOutputManifestEntry entry,
        SecureCheckpointPayloadDecision payloadDecision)
    {
        if (!entry.RestoreValidationProven)
        {
            return Deny(
                SecureOutputManifestClassificationDecision.DeniedRecomputedStateRestoreProof,
                entry.Kind,
                payloadDecision,
                "Recomputed-after-restore state requires restore validation proof.");
        }

        return SecureOutputManifestClassificationResult.Classified(
            SecureOutputManifestClassificationDecision.EntryClassifiedForManifestOnly,
            entry.Kind,
            payloadDecision,
            checkpointPayloadIncluded: false,
            restoreRevalidationRequired: true,
            "Recomputed-after-restore state is classified as rebuild-only manifest coverage.");
    }

    private static SecureOutputManifestClassificationResult ClassifiedManifestOnly(
        SecureOutputManifestEntryKind entryKind,
        SecureCheckpointPayloadDecision payloadDecision,
        string reason) =>
        SecureOutputManifestClassificationResult.Classified(
            SecureOutputManifestClassificationDecision.EntryClassifiedForManifestOnly,
            entryKind,
            payloadDecision,
            checkpointPayloadIncluded: false,
            restoreRevalidationRequired: true,
            reason);

    private static SecureCheckpointPayloadDecision ClassifyPayload(
        SecureCheckpointPayloadClass payloadClass,
        SecurePrivateMemorySealedPayloadContract? privateMemoryContract)
    {
        if (payloadClass == SecureCheckpointPayloadClass.SecurePrivateMemory)
        {
            return privateMemoryContract?.IsComplete == true
                ? new SecureCheckpointPayloadPolicy(allowPrivateSealedPayload: true).Classify(payloadClass)
                : SecureCheckpointPayloadDecision.DeniedPrivateMemoryWithoutSealedPayload;
        }

        return SecureCheckpointPayloadPolicy.FailClosed.Classify(payloadClass);
    }

    private static SecureOutputManifestClassificationDecision MapPayloadDenial(
        SecureCheckpointPayloadClass payloadClass,
        SecureCheckpointPayloadDecision payloadDecision) =>
        payloadDecision switch
        {
            SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence =>
                SecureOutputManifestClassificationDecision.DeniedHostOwnedEvidence,
            SecureCheckpointPayloadDecision.DeniedDebugTraceAsGuestState =>
                SecureOutputManifestClassificationDecision.DeniedDebugTraceAsGuestState,
            SecureCheckpointPayloadDecision.DeniedRawMeasurementSecret
                or SecureCheckpointPayloadDecision.DeniedRawSealingKey =>
                SecureOutputManifestClassificationDecision.DeniedRawSecret,
            SecureCheckpointPayloadDecision.DeniedActiveHostPointer =>
                SecureOutputManifestClassificationDecision.DeniedActiveHostPointer,
            SecureCheckpointPayloadDecision.DeniedPrivateMemoryWithoutSealedPayload =>
                SecureOutputManifestClassificationDecision.DeniedPrivateMemoryContract,
            SecureCheckpointPayloadDecision.DeniedCompatibilityProjectionAuthority
                when payloadClass == SecureCheckpointPayloadClass.VmcsProjectionMetadata =>
                SecureOutputManifestClassificationDecision.DeniedVmcsProjectionAuthority,
            SecureCheckpointPayloadDecision.DeniedCompatibilityProjectionAuthority =>
                SecureOutputManifestClassificationDecision.DeniedCompatibilityMetadataAuthority,
            _ => SecureOutputManifestClassificationDecision.DeniedWrongPayloadClass,
        };

    private static string PayloadReason(SecureCheckpointPayloadDecision payloadDecision) =>
        payloadDecision switch
        {
            SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence =>
                "Host-owned evidence, scheduler evidence, backend binding evidence and native tokens are not secure output manifest payloads.",
            SecureCheckpointPayloadDecision.DeniedCompatibilityProjectionAuthority =>
                "VMCS and compatibility projection metadata cannot be secure output manifest authority.",
            SecureCheckpointPayloadDecision.DeniedDebugTraceAsGuestState =>
                "Debug traces cannot be restored as secure guest state.",
            SecureCheckpointPayloadDecision.DeniedRawMeasurementSecret
                or SecureCheckpointPayloadDecision.DeniedRawSealingKey =>
                "Raw measurement secrets and raw sealing keys are not manifest payloads.",
            SecureCheckpointPayloadDecision.DeniedActiveHostPointer =>
                "Active host pointers are not portable manifest payloads.",
            SecureCheckpointPayloadDecision.DeniedPrivateMemoryWithoutSealedPayload =>
                "Private memory requires a complete sealed/encrypted payload contract before classification.",
            _ => "Secure output manifest payload class is not classified.",
        };

    private static SecureOutputManifestClassificationResult DenyWrongPayload(
        SecureOutputManifestEntry entry) =>
        Deny(
            SecureOutputManifestClassificationDecision.DeniedWrongPayloadClass,
            entry.Kind,
            SecureCheckpointPayloadDecision.Allowed,
            "Secure output manifest entry uses the wrong payload class for its entry kind.");

    private static SecureOutputManifestClassificationResult Deny(
        SecureOutputManifestClassificationDecision decision,
        SecureOutputManifestEntryKind entryKind,
        SecureCheckpointPayloadDecision payloadDecision,
        string reason) =>
        SecureOutputManifestClassificationResult.Denied(decision, entryKind, payloadDecision, reason);

    private static bool ContainsEntry(
        IReadOnlyList<SecureOutputManifestEntry> entries,
        SecureOutputManifestEntryKind kind)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].Kind == kind)
            {
                return true;
            }
        }

        return false;
    }
}
