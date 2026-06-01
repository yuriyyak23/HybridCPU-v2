using System;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmcsReadOnlyValueProjectionDecision : byte
{
    NotEvaluated = 0,
    ReadOnlyValueProjected = 1,
    RuntimeAdmissionDenied = 2,
    UnknownFieldDenied = 3,
    SchemaReadDenied = 4,
    AliasAccessDenied = 5,
    NeutralOwnerValueSourceMissing = 6,
    CompletionSourceMissing = 7,
    CompletionSourceDenied = 8,
    MemorySourceMissing = 9,
    MemorySourceDenied = 10,
    CompatibilityControlValueProjectionDenied = 11,
    HostAddressSpaceOwnerMissing = 12,
    ExecutionSourceMissing = 13,
    ExecutionSourceDenied = 14,
    PrivilegedExecutionStateProjectionDenied = 15,
    HostExecutionStateOwnerMissing = 16,
}

public readonly record struct VmcsReadOnlyValueProjectionRequest(
    ushort FieldId,
    RuntimeBoundaryAdmissionResult RuntimeAdmission,
    EvidencePolicyDescriptor? EvidencePolicy,
    bool DescriptorValidated,
    ExecutionDomainDescriptor? Execution,
    MemoryDomainDescriptor? Memory,
    CompletionRecord? Completion);

public readonly record struct VmcsReadOnlyValueProjectionResult(
    VmcsReadOnlyValueProjectionDecision Decision,
    VmcsField Field,
    VmcsFieldProjectionSchemaEntry SchemaEntry,
    VmcsFieldAliasResult AliasAccess,
    VmcsV2ValidationResult Validation,
    long Value,
    string Reason)
{
    public bool IsProjected =>
        Decision == VmcsReadOnlyValueProjectionDecision.ReadOnlyValueProjected &&
        Validation.Succeeded;
}

public sealed class VmcsReadOnlyValueProjectionService
{
    private readonly VmcsFieldAliasProjection _aliasProjection;
    private readonly CompletionProjectionService _completionProjection;

    public VmcsReadOnlyValueProjectionService()
        : this(new VmcsFieldAliasProjection(), new CompletionProjectionService())
    {
    }

    public VmcsReadOnlyValueProjectionService(
        VmcsFieldAliasProjection aliasProjection,
        CompletionProjectionService completionProjection)
    {
        _aliasProjection = aliasProjection ?? throw new ArgumentNullException(nameof(aliasProjection));
        _completionProjection = completionProjection ?? throw new ArgumentNullException(nameof(completionProjection));
    }

    public VmcsReadOnlyValueProjectionResult Project(
        VmcsReadOnlyValueProjectionRequest request)
    {
        VmcsField field = unchecked((VmcsField)request.FieldId);

        if (!request.RuntimeAdmission.IsAllowed)
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.RuntimeAdmissionDenied,
                field,
                default,
                default,
                request.FieldId,
                "Generated read-only VMREAD value projection requires runtime boundary admission first.");
        }

        if (!VmcsFieldProjectionSchema.TryGet(field, out VmcsFieldProjectionSchemaEntry entry))
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.UnknownFieldDenied,
                field,
                default,
                default,
                request.FieldId,
                "VMREAD field is not part of the generated VMCS field projection schema.");
        }

        if (!VmcsFieldProjectionSchema.CanRead(entry))
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.SchemaReadDenied,
                field,
                entry,
                default,
                request.FieldId,
                "Generated VMCS field projection schema denies read access for this field.");
        }

        if (entry.Owner == VmcsFieldProjectionOwner.CompatibilityControlDescriptor)
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.CompatibilityControlValueProjectionDenied,
                field,
                entry,
                default,
                request.FieldId,
                "Compatibility-control VMREAD values are intentionally denied: the neutral CompatibilityControlDescriptor exposes fail-closed semantics, but no frozen VMX control-bit value projection contract is admitted in this slice.");
        }

        if (entry.Owner is not (
            VmcsFieldProjectionOwner.CompletionRecord or
            VmcsFieldProjectionOwner.ExecutionDomainDescriptor or
            VmcsFieldProjectionOwner.MemoryDomainDescriptor))
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.NeutralOwnerValueSourceMissing,
                field,
                entry,
                default,
                request.FieldId,
                $"Generated VMREAD field '{entry.Field}' owner '{entry.Owner}' has no admitted neutral read-only value source in this slice.");
        }

        VmcsFieldAliasResult alias = _aliasProjection.ValidateAccess(
            new VmcsFieldAliasRequest(
                entry.Field,
                VmcsFieldAliasAccess.Read,
                entry.EvidenceClass,
                entry.IsGeneratedAlias,
                request.DescriptorValidated,
                AllowWrite: false),
            request.EvidencePolicy ?? EvidencePolicyDescriptor.FailClosed);

        if (!alias.IsAllowed)
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.AliasAccessDenied,
                field,
                entry,
                alias,
                request.FieldId,
                alias.Reason);
        }

        return entry.Owner switch
        {
            VmcsFieldProjectionOwner.CompletionRecord =>
                ProjectCompletionOwnedValue(request, field, entry, alias),
            VmcsFieldProjectionOwner.ExecutionDomainDescriptor =>
                ProjectExecutionOwnedValue(request, field, entry, alias),
            VmcsFieldProjectionOwner.MemoryDomainDescriptor =>
                ProjectMemoryOwnedValue(request, field, entry, alias),
            _ => Denied(
                VmcsReadOnlyValueProjectionDecision.NeutralOwnerValueSourceMissing,
                field,
                entry,
                alias,
                request.FieldId,
                $"Generated VMREAD field '{entry.Field}' owner '{entry.Owner}' has no admitted neutral read-only value source in this slice."),
        };
    }

    private VmcsReadOnlyValueProjectionResult ProjectCompletionOwnedValue(
        VmcsReadOnlyValueProjectionRequest request,
        VmcsField field,
        VmcsFieldProjectionSchemaEntry entry,
        VmcsFieldAliasResult alias)
    {
        if (request.Completion is null)
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.CompletionSourceMissing,
                field,
                entry,
                alias,
                request.FieldId,
                "Completion-owned VMREAD value projection requires a neutral CompletionRecord source.");
        }

        if (!_completionProjection.CanProjectToVmx(request.Completion))
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.CompletionSourceDenied,
                field,
                entry,
                alias,
                request.FieldId,
                "CompletionRecord is not an admitted compatibility projection source for VMREAD value projection.");
        }

        VmxCompletionProjection projection = _completionProjection.ProjectToVmx(request.Completion);
        long value = entry.Field switch
        {
            VmcsField.ExitReason => (long)projection.ExitReason,
            VmcsField.ExitQualification => unchecked((long)projection.ExitQualification),
            VmcsField.GuestPhysicalAddress => unchecked((long)projection.GuestPhysicalAddress),
            VmcsField.EptViolationQualification => unchecked((long)projection.EptViolationQualification),
            _ => 0,
        };

        if (entry.Field is not (
            VmcsField.ExitReason or
            VmcsField.ExitQualification or
            VmcsField.GuestPhysicalAddress or
            VmcsField.EptViolationQualification))
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.NeutralOwnerValueSourceMissing,
                field,
                entry,
                alias,
                request.FieldId,
                $"CompletionRecord does not define a VMREAD value projection for field '{entry.Field}'.");
        }

        return new VmcsReadOnlyValueProjectionResult(
            VmcsReadOnlyValueProjectionDecision.ReadOnlyValueProjected,
            field,
            entry,
            alias,
            VmcsV2ValidationResult.Success(request.FieldId),
            value,
            "Generated read-only VMREAD value came from the neutral CompletionRecord owner.");
    }

    private static VmcsReadOnlyValueProjectionResult ProjectMemoryOwnedValue(
        VmcsReadOnlyValueProjectionRequest request,
        VmcsField field,
        VmcsFieldProjectionSchemaEntry entry,
        VmcsFieldAliasResult alias)
    {
        if (entry.Field == VmcsField.HostCr3)
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.HostAddressSpaceOwnerMissing,
                field,
                entry,
                alias,
                request.FieldId,
                "HostCr3 compatibility projection remains denied: no neutral host-address-space owner exposes a read-only address-space root source.");
        }

        if (request.Memory is null)
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.MemorySourceMissing,
                field,
                entry,
                alias,
                request.FieldId,
                "Memory-owned VMREAD value projection requires a neutral MemoryDomainDescriptor source.");
        }

        if (!request.Memory.TryCreateReadOnlyTranslationView(
                out MemoryDomainReadOnlyTranslationView view,
                out string viewReason))
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.MemorySourceDenied,
                field,
                entry,
                alias,
                request.FieldId,
                viewReason);
        }

        if (entry.Field == VmcsField.Vpid &&
            (!view.AddressSpaceTaggingEnabled || view.AddressSpaceTag == 0))
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.MemorySourceDenied,
                field,
                entry,
                alias,
                request.FieldId,
                "VPID compatibility projection requires neutral address-space tagging with a non-zero AddressSpaceTag.");
        }

        if (!TryProjectMemoryField(entry.Field, view, out long value))
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.NeutralOwnerValueSourceMissing,
                field,
                entry,
                alias,
                request.FieldId,
                $"MemoryDomainDescriptor does not define a VMREAD value projection for field '{entry.Field}'.");
        }

        return new VmcsReadOnlyValueProjectionResult(
            VmcsReadOnlyValueProjectionDecision.ReadOnlyValueProjected,
            field,
            entry,
            alias,
            VmcsV2ValidationResult.Success(request.FieldId),
            value,
            "Generated read-only VMREAD value came from the neutral MemoryDomainDescriptor translation view.");
    }

    private static VmcsReadOnlyValueProjectionResult ProjectExecutionOwnedValue(
        VmcsReadOnlyValueProjectionRequest request,
        VmcsField field,
        VmcsFieldProjectionSchemaEntry entry,
        VmcsFieldAliasResult alias)
    {
        if (entry.Field is VmcsField.HostPc or VmcsField.HostSp or VmcsField.HostFlags or VmcsField.HostCr0)
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.HostExecutionStateOwnerMissing,
                field,
                entry,
                alias,
                request.FieldId,
                "Host execution-state VMREAD projection remains denied: no neutral host-execution owner exposes read-only host PC/SP/flags/control-register sources.");
        }

        if (entry.Field is VmcsField.GuestCr0 or VmcsField.GuestCr4)
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.PrivilegedExecutionStateProjectionDenied,
                field,
                entry,
                alias,
                request.FieldId,
                "Guest control-register VMREAD projection remains denied until neutral privileged execution-state semantics are materialized.");
        }

        if (request.Execution is null)
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.ExecutionSourceMissing,
                field,
                entry,
                alias,
                request.FieldId,
                "Execution-owned VMREAD value projection requires a neutral ExecutionDomainDescriptor source.");
        }

        if (!request.Execution.TryCreateReadOnlyStateView(
                out ExecutionDomainReadOnlyStateView view,
                out string viewReason))
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.ExecutionSourceMissing,
                field,
                entry,
                alias,
                request.FieldId,
                viewReason);
        }

        if (!TryProjectExecutionField(entry.Field, view, out long value, out string missingReason))
        {
            return Denied(
                VmcsReadOnlyValueProjectionDecision.ExecutionSourceDenied,
                field,
                entry,
                alias,
                request.FieldId,
                missingReason);
        }

        return new VmcsReadOnlyValueProjectionResult(
            VmcsReadOnlyValueProjectionDecision.ReadOnlyValueProjected,
            field,
            entry,
            alias,
            VmcsV2ValidationResult.Success(request.FieldId),
            value,
            "Generated read-only VMREAD value came from the neutral ExecutionDomainDescriptor read-only state view.");
    }

    private static bool TryProjectExecutionField(
        VmcsField field,
        ExecutionDomainReadOnlyStateView view,
        out long value,
        out string missingReason)
    {
        switch (field)
        {
            case VmcsField.GuestPc when view.HasMaterializedGuestPc:
                value = unchecked((long)view.GuestPc);
                missingReason = string.Empty;
                return true;
            case VmcsField.GuestSp when view.HasMaterializedGuestSp:
                value = unchecked((long)view.GuestSp);
                missingReason = string.Empty;
                return true;
            case VmcsField.GuestFlags when view.HasMaterializedGuestFlags:
                value = unchecked((long)view.GuestFlags);
                missingReason = string.Empty;
                return true;
            case VmcsField.GuestPc:
            case VmcsField.GuestSp:
            case VmcsField.GuestFlags:
                value = 0;
                missingReason = $"ExecutionDomainDescriptor read-only state view does not materialize field '{field}'.";
                return false;
            default:
                value = 0;
                missingReason = $"ExecutionDomainDescriptor does not define a VMREAD value projection for field '{field}'.";
                return false;
        }
    }

    private static bool TryProjectMemoryField(
        VmcsField field,
        MemoryDomainReadOnlyTranslationView view,
        out long value)
    {
        switch (field)
        {
            case VmcsField.GuestCr3:
                value = unchecked((long)view.AddressSpaceRoot);
                return true;
            case VmcsField.EptPointer when view.OwnsSecondStageTranslation:
                value = unchecked((long)view.SecondStageRoot);
                return true;
            case VmcsField.Vpid:
                value = view.AddressSpaceTag;
                return true;
            case VmcsField.Cr3TargetCount:
                value = view.AddressSpaceTargetCount;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static VmcsReadOnlyValueProjectionResult Denied(
        VmcsReadOnlyValueProjectionDecision decision,
        VmcsField field,
        VmcsFieldProjectionSchemaEntry entry,
        VmcsFieldAliasResult alias,
        ushort fieldId,
        string reason) =>
        new(
            decision,
            field,
            entry,
            alias,
            VmcsV2ValidationResult.Fail(VmcsV2ValidationCode.AccessDenied, fieldId, reason),
            Value: 0,
            reason);
}
