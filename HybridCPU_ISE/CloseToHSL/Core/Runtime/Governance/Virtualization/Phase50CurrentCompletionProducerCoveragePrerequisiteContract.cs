using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal enum CurrentCompletionCoverageContourDisposition : byte
{
    ExistingCanonicalProducerInsufficient = 0,
    UncalledNeutralHelperNotArchitecturalCompletion = 1,
    CompatibilityMapperNotAuthority = 2,
    ForbiddenExpansionRequired = 3,
}

internal readonly record struct CurrentCompletionCoverageContourEntry(
    string Contour,
    CurrentCompletionCoverageContourDisposition Disposition,
    string Finding);

internal static class Phase50CurrentCompletionProducerCoveragePrerequisiteContract
{
    internal const string CandidateDecisionId =
        "D2-HV-VMREAD-SCALAR-DELIVERY-V1-CURRENT-COMPLETION-0004";
    internal const string BlockerCode =
        "NoCanonicalCpuTranslationFaultCompletionProducerAndExpansionNotAuthorized";

    internal static ImmutableArray<CurrentCompletionCoverageContourEntry> Contours { get; } =
    [
        new(
            "CanonicalPipelineTrapEntryProducer",
            CurrentCompletionCoverageContourDisposition.ExistingCanonicalProducerInsufficient,
            "Only neutral mcause reason is committed; qualification is absent, address semantic is virtual, and auxiliary is absent."),
        new(
            "IOMMU.TranslateGuestAccess/NestedPageWalker",
            CurrentCompletionCoverageContourDisposition.UncalledNeutralHelperNotArchitecturalCompletion,
            "Typed GPA and translation qualification exist, but no CPU pipeline/retire caller commits them as architectural completion."),
        new(
            "NestedExitMapper",
            CurrentCompletionCoverageContourDisposition.CompatibilityMapperNotAuthority,
            "The only qualification consumer maps to VMX vocabulary inside the compatibility frontend."),
        new(
            "ProspectiveCpuTranslationFaultCompletionProducer",
            CurrentCompletionCoverageContourDisposition.ForbiddenExpansionRequired,
            "Creating and integrating this producer would expand memory/IOMMU/nested completion scope and requires separate authorization."),
    ];

    internal static ImmutableArray<VmReadScalarDeliveryE0FindingV2> Findings { get; } =
    [
        new(1, "ProductionRegistration", "ExactlyOneCanonicalPipelineTrapEntryProducerIsRegistered"),
        new(2, "CpuRetireReachability", "NoCpuPipelineOrRetireCallerCarriesNestedTranslationResultToArchitecturalCompletionCommit"),
        new(3, "TranslationHelper", "TypedGpaAndQualificationExistOnlyAsUncommittedMemoryTranslationResults"),
        new(4, "CompatibilityConsumer", "NestedExitMapperIsTheOnlyQualificationConsumerAndCannotBecomeAuthority"),
        new(5, "ReasonMapping", "RiscVMcauseToVmExitReasonOwnerApprovedMappingDoesNotExist"),
        new(6, "RequiredOwner", "ASeparateNeutralCpuTranslationFaultCompletionProducerAndPolicyDecisionWouldBeRequired"),
        new(7, "AuthorizationBoundary", "MemoryIommuNestedAndVmReadProductionExpansionAreNotAuthorized"),
        new(8, "Disposition", "BlockedPrerequisiteWithoutSurrogateProducerOrCompatibilityAuthority"),
    ];

    internal static bool ExistingProducerCoverageComplete => false;
    internal static bool CpuTranslationFaultProducerExists => false;
    internal static bool CpuRetireReachabilityExists => false;
    internal static bool OwnerApprovedReasonMappingExists => false;
    internal static bool ExpansionAuthorized => false;
    internal static bool D2MayOpen => false;
    internal static bool RuntimeAuthorityGranted => false;
}
