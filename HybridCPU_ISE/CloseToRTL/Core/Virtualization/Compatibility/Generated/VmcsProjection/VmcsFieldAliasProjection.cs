using System;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmcsFieldAliasAccess : byte
{
    Read = 0,
    Write = 1,
}

public enum VmcsFieldAliasDecision : byte
{
    Allowed = 0,
    UndefinedField = 1,
    MissingGeneratedAlias = 2,
    DescriptorValidationDenied = 3,
    EvidencePolicyDenied = 4,
    HostOwnedEvidenceDenied = 5,
    WriteDenied = 6,
}

public readonly record struct VmcsFieldAliasRequest(
    VmcsField Field,
    VmcsFieldAliasAccess Access,
    EvidenceVisibilityClass EvidenceClass,
    bool GeneratedAliasDeclared,
    bool DescriptorValidated,
    bool AllowWrite);

public readonly record struct VmcsFieldAliasResult(
    VmcsFieldAliasDecision Decision,
    VmcsField Field,
    string Reason)
{
    public bool IsAllowed => Decision == VmcsFieldAliasDecision.Allowed;

    public static VmcsFieldAliasResult Allowed(VmcsField field) =>
        new(VmcsFieldAliasDecision.Allowed, field, string.Empty);

    public static VmcsFieldAliasResult Denied(
        VmcsFieldAliasDecision decision,
        VmcsField field,
        string reason) =>
        new(decision, field, reason);
}

public sealed partial class VmcsFieldAliasProjection
{
    public VmcsFieldAliasResult ValidateAccess(
        VmcsFieldAliasRequest request,
        EvidencePolicyDescriptor evidencePolicy)
    {
        if (!Enum.IsDefined(typeof(VmcsField), request.Field))
        {
            return VmcsFieldAliasResult.Denied(
                VmcsFieldAliasDecision.UndefinedField,
                request.Field,
                "VMCS field is not part of the frozen field alias ABI.");
        }

        if (!request.GeneratedAliasDeclared)
        {
            return VmcsFieldAliasResult.Denied(
                VmcsFieldAliasDecision.MissingGeneratedAlias,
                request.Field,
                "VMCS field does not have a generated descriptor alias.");
        }

        if (!request.DescriptorValidated)
        {
            return VmcsFieldAliasResult.Denied(
                VmcsFieldAliasDecision.DescriptorValidationDenied,
                request.Field,
                "VMCS field alias access requires descriptor validation.");
        }

        if (evidencePolicy.MustRecomputeAfterRestore(request.EvidenceClass))
        {
            return VmcsFieldAliasResult.Denied(
                VmcsFieldAliasDecision.HostOwnedEvidenceDenied,
                request.Field,
                "VMCS field alias access cannot expose host-owned evidence.");
        }

        if (!evidencePolicy.CanExposeToGuest(request.EvidenceClass))
        {
            return VmcsFieldAliasResult.Denied(
                VmcsFieldAliasDecision.EvidencePolicyDenied,
                request.Field,
                "Evidence policy does not permit guest-visible VMCS alias access.");
        }

        if (request.Access == VmcsFieldAliasAccess.Write)
        {
            return VmcsFieldAliasResult.Denied(
                VmcsFieldAliasDecision.WriteDenied,
                request.Field,
                "VMCS field alias write authority was removed; compatibility projection is read-only.");
        }

        return VmcsFieldAliasResult.Allowed(request.Field);
    }
}
