using System;
using System.Linq;
using System.Reflection;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests;

public sealed class VmxMigrationEvidenceRecomputedCompatibilityFieldTests
{
    private const BindingFlags InstanceAnyVisibility =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    [Fact]
    public void CompletionOwnedVmreadFields_AreRecomputedCompatibilityProjectionNotMigratableState()
    {
        foreach (VmcsField field in CompletionOwnedFields())
        {
            Assert.True(VmcsFieldProjectionSchema.TryGet(field, out VmcsFieldProjectionSchemaEntry entry));
            Assert.Equal(VmcsFieldProjectionOwner.CompletionRecord, entry.Owner);
            Assert.Equal(EvidenceVisibilityClass.CompatibilityAlias, entry.EvidenceClass);
            Assert.Equal(VmcsFieldProjectionAccessPolicy.ReadOnly, entry.AccessPolicy);
            Assert.Equal(VmcsFieldProjectionMigrationPolicy.RecomputedCompletion, entry.MigrationPolicy);
            Assert.True(VmcsFieldProjectionSchema.CanRead(entry));
            Assert.False(VmcsFieldProjectionSchema.CanWrite(entry));
        }

        VmcsFieldProjectionSchemaEntry[] recomputedEntries =
            VmcsFieldProjectionSchema.Entries
                .ToArray()
                .Where(entry => entry.MigrationPolicy == VmcsFieldProjectionMigrationPolicy.RecomputedCompletion)
                .ToArray();

        Assert.Equal(
            CompletionOwnedFields().OrderBy(field => field).ToArray(),
            recomputedEntries.Select(entry => entry.Field).OrderBy(field => field).ToArray());
        Assert.All(
            recomputedEntries,
            entry => Assert.Equal(VmcsFieldProjectionOwner.CompletionRecord, entry.Owner));
    }

    [Fact]
    public void ProjectedCompletionValues_DoNotBecomeCheckpointPayloadClasses()
    {
        CompletionRecord completion = new(
            CompletionRecordClass.CompatibilityExit,
            reasonCode: (uint)VmExitReason.VmCall,
            qualification: 0x1234UL,
            faultAddress: 0xABCD_EF00UL,
            faultAux: 0x55UL);
        VmxCompletionProjection projection = new CompletionProjectionService().ProjectToVmx(completion);

        Assert.Equal(VmExitReason.VmCall, projection.ExitReason);
        Assert.Equal(0x1234UL, projection.ExitQualification);
        Assert.Equal(0xABCD_EF00UL, projection.GuestPhysicalAddress);
        Assert.Equal(0x55UL, projection.EptViolationQualification);

        string[] payloadNames = Enum.GetNames<MigrationPayloadClass>();
        foreach (string forbiddenPayloadName in new[]
                 {
                     nameof(VmcsField.ExitReason),
                     nameof(VmcsField.ExitQualification),
                     nameof(VmcsField.GuestPhysicalAddress),
                     nameof(VmcsField.EptViolationQualification),
                     nameof(CompletionRecord),
                     nameof(VmxCompletionProjection),
                     "VmExitReason",
                     "VmcsField",
                 })
        {
            Assert.DoesNotContain(forbiddenPayloadName, payloadNames);
            Assert.Null(typeof(DomainCheckpointImage).GetProperty(
                forbiddenPayloadName,
                InstanceAnyVisibility));
            Assert.Null(typeof(DomainCheckpointImage).GetField(
                forbiddenPayloadName,
                InstanceAnyVisibility));
        }
    }

    [Fact]
    public void CheckpointRestore_AllowsNeutralGuestStateButRejectsCompatibilityProjectionMetadata()
    {
        var restoreService = new RestoreValidationService();
        MigrationValidationPolicy policy = CreateGuestStateMigrationPolicy();

        RestoreValidationResult guestStateRestore = restoreService.ValidateRestore(
            CreateGuestStateCheckpoint(epoch: 31),
            policy,
            EvidenceRestorePolicy.PreserveGuestArchitecturalState,
            expectedCheckpointEpoch: 31);
        Assert.True(guestStateRestore.IsAllowed);

        DomainCheckpointImage compatibilityMetadata = new DomainCheckpointImage(
            DomainCheckpointAuthority.DomainDescriptor,
            checkpointEpoch: 31,
            payloadMask: 0,
            evidenceMask: 0,
            containsCompatibilityProjectionMetadata: false)
            .WithPayload(MigrationPayloadClass.CompatibilityProjectionMetadata);
        RestoreValidationResult metadataRestore = restoreService.ValidateRestore(
            compatibilityMetadata,
            CreateCompatibilityMetadataAllowedButRejectedPolicy(),
            EvidenceRestorePolicy.PreserveGuestArchitecturalState,
            expectedCheckpointEpoch: 31);

        Assert.False(metadataRestore.IsAllowed);
        Assert.Equal(
            RestoreValidationDecision.CompatibilityProjectionDenied,
            metadataRestore.Decision);

        DomainCheckpointImage compatibilityAuthority = new DomainCheckpointImage(
            DomainCheckpointAuthority.CompatibilityProjection,
            checkpointEpoch: 31,
            payloadMask: 0,
            evidenceMask: 0,
            containsCompatibilityProjectionMetadata: false)
            .WithPayload(MigrationPayloadClass.GuestArchitecturalState);
        RestoreValidationResult compatibilityAuthorityRestore = restoreService.ValidateRestore(
            compatibilityAuthority,
            policy,
            EvidenceRestorePolicy.PreserveGuestArchitecturalState,
            expectedCheckpointEpoch: 31);

        Assert.False(compatibilityAuthorityRestore.IsAllowed);
        Assert.Equal(
            RestoreValidationDecision.CompatibilityProjectionDenied,
            compatibilityAuthorityRestore.Decision);
    }

    [Fact]
    public void HostOwnedEvidence_IsRecomputeOnlyAndRejectedFromCheckpointRestore()
    {
        var migration = new MigrationDescriptor(
            allowGuestArchitecturalState: true,
            allowDomainDescriptorState: true,
            allowCompatibilityProjectionMetadata: false);
        var evidencePolicy = new EvidencePolicyDescriptor(
            allowCompatibilityAliases: false,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: true);
        var boundary = new HostOwnedEvidenceBoundary();

        foreach ((MigrationPayloadClass Payload, EvidenceVisibilityClass Evidence) in HostOwnedPayloads())
        {
            Assert.True(migration.MustRecomputeAfterRestore(Payload));
            Assert.True(evidencePolicy.MustRecomputeAfterRestore(Evidence));

            HostOwnedEvidenceBoundaryResult boundaryResult = boundary.ValidateRestore(
                evidencePolicy,
                Evidence,
                EvidenceRestorePolicy.RecomputeAfterRestore);
            Assert.Equal(
                HostOwnedEvidenceBoundaryDecision.RestoreRequiresRecompute,
                boundaryResult.Decision);

            DomainCheckpointImage checkpoint = CreateGuestStateCheckpoint(epoch: 41)
                .WithPayload(Payload);
            RestoreValidationResult restore = new RestoreValidationService().ValidateRestore(
                checkpoint,
                CreateGuestStateMigrationPolicy(),
                EvidenceRestorePolicy.PreserveGuestArchitecturalState,
                expectedCheckpointEpoch: 41);

            Assert.False(restore.IsAllowed);
            Assert.Equal(RestoreValidationDecision.MigrationPolicyDenied, restore.Decision);
            Assert.Equal(
                MigrationValidationDecision.HostOwnedEvidenceRejected,
                restore.MigrationResult.Decision);
        }
    }

    [Fact]
    public void MigrationAndCheckpointSources_DoNotDependOnCompletionOrVmcsProjectionState()
    {
        string migrationSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Migration/Checkpoint/DomainCheckpointImage.cs",
            "CloseToRTL/Core/Runtime/Migration/Format/MigrationDescriptor.cs",
            "CloseToRTL/Core/Runtime/Migration/Restore/RestoreValidationService.cs",
            "CloseToRTL/Core/Runtime/Migration/Validation/MigrationValidationPolicy.cs",
            "CloseToRTL/Core/Runtime/Evidence/HostOwned/HostOwnedEvidenceBoundary.cs");

        foreach (string required in new[]
                 {
                     "MigrationPayloadClass.GuestArchitecturalState",
                     "MigrationPayloadClass.DomainDescriptorState",
                     "MigrationPayloadClass.CompatibilityProjectionMetadata",
                     "ContainsCompatibilityProjectionMetadata",
                     "CompatibilityProjectionDenied",
                     "MustRecomputeAfterRestore",
                     "ContainsHostOwnedEvidence",
                     "EvidenceRestorePolicy.PreserveGuestArchitecturalState",
                 })
        {
            Assert.Contains(required, migrationSource);
        }

        foreach (string forbidden in new[]
                 {
                     "CompletionRecord",
                     "CompletionProjectionService",
                     "VmxCompletionProjection",
                     "VmcsReadOnlyValueProjectionService",
                     "AdmitVmReadProjection",
                     "ReadOnlyValueProjected",
                     "VmcsV2Descriptor",
                     "VmcsField.",
                     "VmExitReason",
                     "ExitReason",
                     "ExitQualification",
                     "GuestPhysicalAddress",
                     "EptViolationQualification",
                     "ReadFieldValue(",
                     "WriteFieldValue(",
                     "HardwareWrite(",
                     "DirectWrite(",
                     "VmxExecutionUnit",
                     "VmcsManager",
                     "IVmcsManager",
                 })
        {
            Assert.DoesNotContain(forbidden, migrationSource);
        }
    }

    [Fact]
    public void CompletionProjectionSources_DoNotExposeMigrationOrCheckpointAuthority()
    {
        string completionProjectionSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Completion/Records/CompletionRecord.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Completion/CompletionRecordCompatibilityProjection.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Completion/CompletionProjectionService.cs");

        Assert.Contains("CompletionRecordClass.CompatibilityExit", completionProjectionSource);
        Assert.Contains("CanProjectToVmx", completionProjectionSource);
        Assert.Contains("ProjectToVmx", completionProjectionSource);

        foreach (string forbidden in new[]
                 {
                     "DomainCheckpointImage",
                     "MigrationDescriptor",
                     "MigrationValidationPolicy",
                     "EvidenceRestorePolicy",
                     "MigrationPayloadClass",
                     "ValidateRestore",
                     "ValidateImport",
                     "WithPayload",
                     "CanSerialize",
                     "CanRestore",
                     "ContainsHostOwnedEvidence",
                     "MustRecomputeAfterRestore",
                     "VmcsManager",
                     "IVmcsManager",
                     "VmxExecutionUnit",
                     "ReadFieldValue(",
                     "WriteFieldValue(",
                 })
        {
            Assert.DoesNotContain(forbidden, completionProjectionSource);
        }
    }

    private static VmcsField[] CompletionOwnedFields() =>
        new[]
        {
            VmcsField.ExitReason,
            VmcsField.ExitQualification,
            VmcsField.GuestPhysicalAddress,
            VmcsField.EptViolationQualification,
        };

    private static (MigrationPayloadClass Payload, EvidenceVisibilityClass Evidence)[] HostOwnedPayloads() =>
        new[]
        {
            (
                MigrationPayloadClass.HostOwnedRuntimeEvidence,
                EvidenceVisibilityClass.HostOwnedRuntimeEvidence),
            (
                MigrationPayloadClass.SchedulerEvidence,
                EvidenceVisibilityClass.SchedulerEvidence),
            (
                MigrationPayloadClass.BackendBindingEvidence,
                EvidenceVisibilityClass.BackendBindingEvidence),
            (
                MigrationPayloadClass.NativeTokenEvidence,
                EvidenceVisibilityClass.NativeTokenEvidence),
        };

    private static DomainCheckpointImage CreateGuestStateCheckpoint(ulong epoch) =>
        new DomainCheckpointImage(
            DomainCheckpointAuthority.DomainDescriptor,
            checkpointEpoch: epoch,
            payloadMask: 0,
            evidenceMask: 0,
            containsCompatibilityProjectionMetadata: false)
            .WithPayload(MigrationPayloadClass.GuestArchitecturalState);

    private static MigrationValidationPolicy CreateGuestStateMigrationPolicy() =>
        new(
            new MigrationDescriptor(
                allowGuestArchitecturalState: true,
                allowDomainDescriptorState: true,
                allowCompatibilityProjectionMetadata: false),
            new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: true),
            rejectCompatibilityProjectionMetadata: true,
            requireGuestStatePreservePolicy: true);

    private static MigrationValidationPolicy CreateCompatibilityMetadataAllowedButRejectedPolicy() =>
        new(
            new MigrationDescriptor(
                allowGuestArchitecturalState: true,
                allowDomainDescriptorState: true,
                allowCompatibilityProjectionMetadata: true),
            new EvidencePolicyDescriptor(
                allowCompatibilityAliases: true,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: true),
            rejectCompatibilityProjectionMetadata: true,
            requireGuestStatePreservePolicy: true);
}
