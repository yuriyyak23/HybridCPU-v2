namespace YAKSys_Hybrid_CPU.Core;

public enum NeutralHypercallBackendOwnerSource : byte
{
    None = 0,
    NeutralRuntimeOwner = 1,
    CompatibilityProjection = 2,
    CompatibilityStateVocabulary = 3,
    CapabilityProjection = 4,
    MigrationCheckpoint = 5,
    LaneStreamBoundary = 6,
    SecureComputeBoundary = 7,
    CompilerEmission = 8,
}

public enum NeutralHypercallBackendOwnerRfcAdrState : byte
{
    Missing = 0,
    DraftOnly = 1,
}

public enum NeutralHypercallBackendOperationClass : byte
{
    Unknown = 0,
    NoStateNoPayloadDomainLocal = 1,
}

public enum NeutralHypercallBackendLeafSelection : byte
{
    Missing = 0,
    CandidateOnlyNoNumericLeaf = 1,
}

public readonly record struct NeutralHypercallBackendOwnerDescriptor(
    ulong OwnerId,
    NeutralHypercallBackendOwnerSource Source,
    string RfcAdrId,
    NeutralHypercallBackendOwnerRfcAdrState RfcAdrState,
    NeutralHypercallBackendOperationClass OperationClass,
    NeutralHypercallBackendLeafSelection LeafSelection,
    bool NoPayloadOnly,
    bool DomainLocalOnly,
    bool NegativeTestsPresent)
{
    public const string CandidateRfcAdrId =
        "RFC-HV-VMCALL-NO-STATE-OWNER-0001";

    public static NeutralHypercallBackendOwnerDescriptor Missing { get; } =
        new(
            OwnerId: 0,
            Source: NeutralHypercallBackendOwnerSource.None,
            RfcAdrId: string.Empty,
            RfcAdrState: NeutralHypercallBackendOwnerRfcAdrState.Missing,
            OperationClass: NeutralHypercallBackendOperationClass.Unknown,
            LeafSelection: NeutralHypercallBackendLeafSelection.Missing,
            NoPayloadOnly: false,
            DomainLocalOnly: false,
            NegativeTestsPresent: false);

    public static NeutralHypercallBackendOwnerDescriptor DraftNoStateCandidate(
        ulong ownerId,
        bool negativeTestsPresent = true) =>
        new(
            ownerId,
            NeutralHypercallBackendOwnerSource.NeutralRuntimeOwner,
            CandidateRfcAdrId,
            NeutralHypercallBackendOwnerRfcAdrState.DraftOnly,
            NeutralHypercallBackendOperationClass.NoStateNoPayloadDomainLocal,
            NeutralHypercallBackendLeafSelection.CandidateOnlyNoNumericLeaf,
            NoPayloadOnly: true,
            DomainLocalOnly: true,
            negativeTestsPresent);

    public bool IsMaterialized =>
        OwnerId != 0 &&
        Source != NeutralHypercallBackendOwnerSource.None &&
        !string.IsNullOrWhiteSpace(RfcAdrId);

    public bool IsNeutralRuntimeOwner =>
        Source == NeutralHypercallBackendOwnerSource.NeutralRuntimeOwner;

    public bool IsCandidateDraft =>
        RfcAdrId == CandidateRfcAdrId &&
        RfcAdrState == NeutralHypercallBackendOwnerRfcAdrState.DraftOnly;

    public bool HasNoStateCandidateShape =>
        OperationClass ==
            NeutralHypercallBackendOperationClass.NoStateNoPayloadDomainLocal &&
        LeafSelection ==
            NeutralHypercallBackendLeafSelection.CandidateOnlyNoNumericLeaf &&
        NoPayloadOnly &&
        DomainLocalOnly;
}
