using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core;

public enum SecureIoDmaPolicy : byte
{
    Denied = 0,
    ExplicitSharedBuffersOnly = 1,
}

public enum SecureSharedBufferDirection : byte
{
    None = 0,
    DomainToDevice = 1,
    DeviceToDomain = 2,
    Bidirectional = 3,
}

public readonly record struct SecureSharedBufferDescriptor(
    ulong BufferId,
    ulong Start,
    ulong Length,
    SecureSharedBufferDirection Direction,
    SecureGrantHandle Grant,
    SecureEvidenceVisibilityClass EvidenceClass,
    ulong OwnerDomainTag,
    ulong LifetimeEpoch)
{
    public bool IsMaterialized =>
        BufferId != 0 &&
        Length != 0 &&
        Direction != SecureSharedBufferDirection.None &&
        Grant.IsMaterialized &&
        EvidenceClass != SecureEvidenceVisibilityClass.Denied &&
        OwnerDomainTag != 0 &&
        LifetimeEpoch != 0;

    public bool Contains(ulong address, ulong length)
    {
        if (Length == 0 || length == 0 || address < Start)
        {
            return false;
        }

        ulong offset = address - Start;
        return offset < Length && length <= Length - offset;
    }

    public bool AllowsDirection(SecureSharedBufferDirection requiredDirection) =>
        requiredDirection != SecureSharedBufferDirection.None &&
        (Direction == SecureSharedBufferDirection.Bidirectional ||
         Direction == requiredDirection);

    public bool IsOwnedBy(ulong domainTag) =>
        OwnerDomainTag != 0 && OwnerDomainTag == domainTag;

    public bool HasCurrentLifetime(SecureRevocationEpoch policyEpoch) =>
        policyEpoch.IsCurrent(LifetimeEpoch);
}

public sealed partial class SecureIoDomainDescriptor
{
    public SecureIoDomainDescriptor()
        : this(
            SecureIoDmaPolicy.Denied,
            System.Array.Empty<SecureSharedBufferDescriptor>(),
            requireCompletionFence: true,
            neutralIoOwnerMaterialized: false)
    {
    }

    public SecureIoDomainDescriptor(
        SecureIoDmaPolicy dmaPolicy,
        IReadOnlyList<SecureSharedBufferDescriptor> sharedBuffers,
        bool requireCompletionFence)
        : this(
            dmaPolicy,
            sharedBuffers,
            requireCompletionFence,
            neutralIoOwnerMaterialized: false)
    {
    }

    public SecureIoDomainDescriptor(
        SecureIoDmaPolicy dmaPolicy,
        IReadOnlyList<SecureSharedBufferDescriptor> sharedBuffers,
        bool requireCompletionFence,
        bool neutralIoOwnerMaterialized)
    {
        DmaPolicy = dmaPolicy;
        SharedBuffers = sharedBuffers;
        RequireCompletionFence = requireCompletionFence;
        NeutralIoOwnerMaterialized = neutralIoOwnerMaterialized;
    }

    public static SecureIoDomainDescriptor Disabled { get; } = new();

    public SecureIoDmaPolicy DmaPolicy { get; }

    public IReadOnlyList<SecureSharedBufferDescriptor> SharedBuffers { get; }

    public bool RequireCompletionFence { get; }

    public bool NeutralIoOwnerMaterialized { get; }

    public bool AllowsDma =>
        DmaPolicy == SecureIoDmaPolicy.ExplicitSharedBuffersOnly &&
        SharedBuffers.Count != 0;

    public bool AllowsSharedBuffer(ulong bufferId)
    {
        foreach (var buffer in SharedBuffers)
        {
            if (buffer.BufferId == bufferId && buffer.IsMaterialized)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryFindSharedBuffer(
        ulong address,
        ulong length,
        SecureSharedBufferDirection requiredDirection,
        ulong ownerDomainTag,
        SecureRevocationEpoch policyEpoch,
        out SecureSharedBufferDescriptor buffer)
    {
        if (DmaPolicy != SecureIoDmaPolicy.ExplicitSharedBuffersOnly)
        {
            buffer = default;
            return false;
        }

        foreach (var candidate in SharedBuffers)
        {
            if (candidate.IsMaterialized &&
                candidate.Contains(address, length) &&
                candidate.AllowsDirection(requiredDirection) &&
                candidate.IsOwnedBy(ownerDomainTag) &&
                candidate.HasCurrentLifetime(policyEpoch))
            {
                buffer = candidate;
                return true;
            }
        }

        buffer = default;
        return false;
    }
}
