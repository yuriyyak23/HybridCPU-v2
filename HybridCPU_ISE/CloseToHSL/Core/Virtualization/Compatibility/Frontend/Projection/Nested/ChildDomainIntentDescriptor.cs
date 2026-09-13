using System;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.Core.Nested;

public static class ChildDomainIntentFieldIds
{
    public const ushort GuestProgramCounter = 0;
    public const ushort GuestStackPointer = 1;
    public const ushort GuestFlags = 2;
    public const ushort GuestControl0 = 3;
    public const ushort GuestPageTableRoot = 4;
    public const ushort GuestControl4 = 5;
    public const ushort HostProgramCounter = 32;
    public const ushort HostStackPointer = 33;
    public const ushort HostFlags = 34;
    public const ushort HostControl0 = 35;
    public const ushort HostPageTableRoot = 36;
    public const ushort PinExecutionControls = 64;
    public const ushort ProcessorExecutionControls = 65;
    public const ushort ExitControls = 66;
    public const ushort EntryControls = 67;
    public const ushort SecondStageRootPointer = 80;
    public const ushort AddressSpaceTag = 81;
    public const ushort SecondaryExecutionControls = 82;
    public const ushort GuestPageTableTargetCount = 83;
    public const ushort ExitReason = 96;
    public const ushort ExitQualification = 97;
    public const ushort GuestPhysicalAddress = 112;
    public const ushort SecondStageViolationQualification = 113;
}

public enum ChildDomainIntentAccessDisposition : byte
{
    Allowed = 0,
    VmFail = 1,
    VmExitToL0 = 2,
}

public readonly record struct ChildDomainIntentAccessResult(
    bool Succeeded,
    ChildDomainIntentAccessDisposition Disposition,
    ushort FieldId,
    long Value,
    VmExitReason ExitReason,
    string Message)
{
    public static ChildDomainIntentAccessResult Allowed(ushort fieldId, long value) =>
        new(true, ChildDomainIntentAccessDisposition.Allowed, fieldId, value, VmExitReason.None, string.Empty);

    public static ChildDomainIntentAccessResult VmFail(ushort fieldId, string message) =>
        new(false, ChildDomainIntentAccessDisposition.VmFail, fieldId, 0, VmExitReason.None, message);

    public static ChildDomainIntentAccessResult L0Exit(ushort fieldId, VmExitReason reason, string message) =>
        new(false, ChildDomainIntentAccessDisposition.VmExitToL0, fieldId, 0, reason, message);
}

public sealed class ChildDomainIntentAccessPolicy
{
    private static readonly ushort[] DefaultL1VisibleFields =
    {
        ChildDomainIntentFieldIds.GuestProgramCounter,
        ChildDomainIntentFieldIds.GuestStackPointer,
        ChildDomainIntentFieldIds.GuestFlags,
        ChildDomainIntentFieldIds.GuestControl0,
        ChildDomainIntentFieldIds.GuestPageTableRoot,
        ChildDomainIntentFieldIds.GuestControl4,
        ChildDomainIntentFieldIds.HostProgramCounter,
        ChildDomainIntentFieldIds.HostStackPointer,
        ChildDomainIntentFieldIds.HostFlags,
        ChildDomainIntentFieldIds.HostControl0,
        ChildDomainIntentFieldIds.HostPageTableRoot,
        ChildDomainIntentFieldIds.PinExecutionControls,
        ChildDomainIntentFieldIds.ProcessorExecutionControls,
        ChildDomainIntentFieldIds.ExitControls,
        ChildDomainIntentFieldIds.EntryControls,
        ChildDomainIntentFieldIds.SecondStageRootPointer,
        ChildDomainIntentFieldIds.AddressSpaceTag,
        ChildDomainIntentFieldIds.SecondaryExecutionControls,
        ChildDomainIntentFieldIds.GuestPageTableTargetCount,
        ChildDomainIntentFieldIds.ExitReason,
        ChildDomainIntentFieldIds.ExitQualification,
        ChildDomainIntentFieldIds.GuestPhysicalAddress,
        ChildDomainIntentFieldIds.SecondStageViolationQualification,
    };

    private readonly ushort[] _readAllow;
    private readonly ushort[] _writeAllow;
    private readonly ushort[] _readDeny;
    private readonly ushort[] _writeDeny;
    private readonly ushort[] _readExit;
    private readonly ushort[] _writeExit;

    public ChildDomainIntentAccessPolicy()
        : this(
            Array.Empty<ushort>(),
            Array.Empty<ushort>(),
            Array.Empty<ushort>(),
            Array.Empty<ushort>(),
            Array.Empty<ushort>(),
            Array.Empty<ushort>())
    {
    }

    private ChildDomainIntentAccessPolicy(
        ushort[] readAllow,
        ushort[] writeAllow,
        ushort[] readDeny,
        ushort[] writeDeny,
        ushort[] readExit,
        ushort[] writeExit)
    {
        _readAllow = readAllow;
        _writeAllow = writeAllow;
        _readDeny = readDeny;
        _writeDeny = writeDeny;
        _readExit = readExit;
        _writeExit = writeExit;
    }

    public static ChildDomainIntentAccessPolicy DefaultNestedL1Visible()
    {
        return new(
            DefaultL1VisibleFields,
            DefaultL1VisibleFields,
            Array.Empty<ushort>(),
            Array.Empty<ushort>(),
            Array.Empty<ushort>(),
            Array.Empty<ushort>());
    }

    public ChildDomainIntentAccessDisposition EvaluateRead(ushort fieldId) =>
        Evaluate(fieldId, _readAllow, _readDeny, _readExit);

    public ChildDomainIntentAccessDisposition EvaluateWrite(ushort fieldId) =>
        Evaluate(fieldId, _writeAllow, _writeDeny, _writeExit);

    private static ChildDomainIntentAccessDisposition Evaluate(
        ushort fieldId,
        ushort[] allow,
        ushort[] deny,
        ushort[] exit)
    {
        if (Array.IndexOf(exit, fieldId) >= 0)
        {
            return ChildDomainIntentAccessDisposition.VmExitToL0;
        }

        if (Array.IndexOf(deny, fieldId) >= 0 ||
            Array.IndexOf(allow, fieldId) < 0)
        {
            return ChildDomainIntentAccessDisposition.VmFail;
        }

        return ChildDomainIntentAccessDisposition.Allowed;
    }
}

public sealed class ChildDomainIntentDescriptor
{
    public ChildDomainIntentDescriptor(ulong childIntentPointer, ChildDomainIntentAccessPolicy? bitmaps = null)
    {
        ChildIntentPointer = childIntentPointer;
        AccessBitmaps = bitmaps ?? ChildDomainIntentAccessPolicy.DefaultNestedL1Visible();
    }

    public ushort Version => 1;

    public ulong ChildIntentPointer { get; }

    public ChildDomainIntentAccessPolicy AccessBitmaps { get; }

    public bool IsReadOnlyCompatibilityProjection => true;

    public bool TryReadIntentField(
        VmcsV2BlockDirectory directory,
        ushort fieldId,
        out long value,
        out ChildDomainIntentAccessResult result)
    {
        value = 0;
        ChildDomainIntentAccessDisposition disposition = AccessBitmaps.EvaluateRead(fieldId);
        if (disposition == ChildDomainIntentAccessDisposition.VmExitToL0)
        {
            result = ChildDomainIntentAccessResult.L0Exit(
                fieldId,
                VmExitReason.SecurityPolicyViolation,
                "Child-domain intent field read is configured to exit to L0.");
            return false;
        }

        if (disposition == ChildDomainIntentAccessDisposition.VmFail)
        {
            result = ChildDomainIntentAccessResult.VmFail(
                fieldId,
                "Child-domain intent field read is denied by the nested access bitmap.");
            return false;
        }

        if (!IsL1VisibleField(directory, fieldId, out string message))
        {
            result = ChildDomainIntentAccessResult.VmFail(fieldId, message);
            return false;
        }

        result = ChildDomainIntentAccessResult.VmFail(
            fieldId,
            "Child-domain intent field read requires neutral runtime-owned nested intent state.");
        return false;
    }

    public bool ContainsHostEvidence(VmcsV2HostEvidenceKind evidence) => false;

    private static bool IsL1VisibleField(
        VmcsV2BlockDirectory directory,
        ushort fieldId,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!directory.TryGetField(fieldId, out VmcsV2FieldDescriptor descriptor))
        {
            message = "Child-domain intent field is not present in the VMCSv2 field directory.";
            return false;
        }

        if (descriptor.AccessPolicy.HostRuntimeEvidence ||
            descriptor.ValueType == VmcsV2FieldValueType.DescriptorReference)
        {
            message = "Host-owned VMCSv2 descriptor/evidence fields are not visible in child-domain intent projection.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

public sealed record ChildDomainIntentSnapshot(
    ushort Version,
    ulong ChildIntentPointer);
