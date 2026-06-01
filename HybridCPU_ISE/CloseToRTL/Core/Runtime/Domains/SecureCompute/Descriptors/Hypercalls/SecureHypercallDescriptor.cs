using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core;

public enum SecureHypercallArgumentClass : byte
{
    Immediate = 0,
    ExplicitSharedBuffer = 1,
    OpaqueHandle = 2,
    RawPrivatePointerDenied = 3,
}

public readonly record struct SecureHypercallArgumentDescriptor(
    byte Index,
    SecureHypercallArgumentClass ArgumentClass,
    ulong SharedBufferId,
    SecureGrantHandle Grant)
{
    public bool RequiresSharedBuffer =>
        ArgumentClass == SecureHypercallArgumentClass.ExplicitSharedBuffer;

    public bool IsDeniedRawPrivatePointer =>
        ArgumentClass == SecureHypercallArgumentClass.RawPrivatePointerDenied;
}

public sealed partial class SecureHypercallDescriptor
{
    public SecureHypercallDescriptor()
        : this(
            neutralBackendOwnerRequired: true,
            allowBackendExecution: false,
            allowedHypercallIds: System.Array.Empty<ulong>(),
            requiredGrant: SecureGrantHandle.None,
            arguments: System.Array.Empty<SecureHypercallArgumentDescriptor>(),
            requireEvidenceApproval: true,
            requireCompletionFence: true,
            requireRetirePublicationRule: true)
    {
    }

    public SecureHypercallDescriptor(
        bool neutralBackendOwnerRequired,
        bool allowBackendExecution,
        SecureGrantHandle requiredGrant,
        IReadOnlyList<SecureHypercallArgumentDescriptor> arguments,
        bool requireEvidenceApproval,
        bool requireCompletionFence,
        bool requireRetirePublicationRule)
        : this(
            neutralBackendOwnerRequired,
            allowBackendExecution,
            System.Array.Empty<ulong>(),
            requiredGrant,
            arguments,
            requireEvidenceApproval,
            requireCompletionFence,
            requireRetirePublicationRule)
    {
    }

    public SecureHypercallDescriptor(
        bool neutralBackendOwnerRequired,
        bool allowBackendExecution,
        IReadOnlyList<ulong> allowedHypercallIds,
        SecureGrantHandle requiredGrant,
        IReadOnlyList<SecureHypercallArgumentDescriptor> arguments,
        bool requireEvidenceApproval,
        bool requireCompletionFence,
        bool requireRetirePublicationRule)
    {
        NeutralBackendOwnerRequired = neutralBackendOwnerRequired;
        AllowBackendExecution = allowBackendExecution;
        AllowedHypercallIds = allowedHypercallIds;
        RequiredGrant = requiredGrant;
        Arguments = arguments;
        RequireEvidenceApproval = requireEvidenceApproval;
        RequireCompletionFence = requireCompletionFence;
        RequireRetirePublicationRule = requireRetirePublicationRule;
    }

    public static SecureHypercallDescriptor Disabled { get; } = new();

    public bool NeutralBackendOwnerRequired { get; }

    public bool AllowBackendExecution { get; }

    public IReadOnlyList<ulong> AllowedHypercallIds { get; }

    public SecureGrantHandle RequiredGrant { get; }

    public IReadOnlyList<SecureHypercallArgumentDescriptor> Arguments { get; }

    public bool RequireEvidenceApproval { get; }

    public bool RequireCompletionFence { get; }

    public bool RequireRetirePublicationRule { get; }

    public bool IsDenialOnly =>
        !AllowBackendExecution;

    public bool HasPolicy =>
        AllowedHypercallIds.Count != 0;

    public bool AllowsHypercallId(ulong hypercallId)
    {
        foreach (ulong allowed in AllowedHypercallIds)
        {
            if (allowed == hypercallId)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasDeniedRawPrivatePointer()
    {
        foreach (var argument in Arguments)
        {
            if (argument.IsDeniedRawPrivatePointer)
            {
                return true;
            }
        }

        return false;
    }
}
