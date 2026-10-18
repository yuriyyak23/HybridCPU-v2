namespace YAKSys_Hybrid_CPU.Core;

public enum DomainTrapRecordAuthority : byte
{
    Runtime = 0,
    CompatibilityProjection = 1,
}

public sealed partial class DomainTrapRecord
{
    public DomainTrapRecord()
        : this(
            authority: DomainTrapRecordAuthority.Runtime,
            sequence: 0,
            domainId: 0,
            shouldExit: false,
            descriptorEpoch: 0,
            evidenceHash: 0,
            allowsCompatibilityProjection: true)
    {
    }

    public DomainTrapRecord(
        DomainTrapRecordAuthority authority,
        ulong sequence,
        ulong domainId,
        bool shouldExit,
        ulong descriptorEpoch,
        ulong evidenceHash,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        Sequence = sequence;
        DomainId = domainId;
        ShouldExit = shouldExit;
        DescriptorEpoch = descriptorEpoch;
        EvidenceHash = evidenceHash;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public DomainTrapRecordAuthority Authority { get; }

    public ulong Sequence { get; }

    public ulong DomainId { get; }

    public bool ShouldExit { get; }

    public ulong DescriptorEpoch { get; }

    public ulong EvidenceHash { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == DomainTrapRecordAuthority.Runtime;

    public bool HasDomainBinding =>
        DomainId != 0;

    public bool HasEvidenceHash =>
        EvidenceHash != 0;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative;

    public DomainTrapRecord WithSequence(ulong sequence) =>
        new(
            Authority,
            sequence,
            DomainId,
            ShouldExit,
            DescriptorEpoch,
            EvidenceHash,
            AllowsCompatibilityProjection);
}
