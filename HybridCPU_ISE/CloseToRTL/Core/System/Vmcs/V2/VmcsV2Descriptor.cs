using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core.Vmcs.V2;

public sealed class VmcsV2Descriptor
{
    public VmcsV2Descriptor(VmcsV2Header header, VmcsV2BlockDirectory directory)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    public VmcsV2Header Header { get; }

    public VmcsV2BlockDirectory Directory { get; }

    public VmxRootControlBlock RootControl { get; } = new();

    public VirtualCpuBlock VirtualCpu { get; } = new();

    public BundleExecutionBlock BundleExecution { get; } = new();

    public VmxPreemptionTimerBlock PreemptionTimer { get; } = new();

    public VirtualInterruptFabricBlock InterruptFabric { get; } = new();

    public VectorStreamStateBlock VectorStreamState { get; } = new();

    public SecurityIsolationBlock SecurityIsolation { get; } = new();

    public CapabilityNegotiationBlock CapabilityNegotiation { get; } = new();

    public VmxNptBlock NptBlock { get; } = new();

    public ExitInfoBlock ExitInfo { get; } = new();

    public InterceptBitmapBlock InterceptBitmap { get; } = new();

    public EventInjectionBlock EventInjection { get; } = new();

    public LaneCompletionRoutingBlock LaneCompletionRouting { get; } = new();

    public IoVirtualizationBlock IoVirt { get; } = new();

    public Lane6StateBlock Lane6State { get; } = new();

    public Lane7StateBlock Lane7State { get; } = new();

    public DirtyLogBlock DirtyLog { get; } = new();

    public DebugTraceBlock DebugTrace { get; } = new();

    public static VmcsV2Descriptor CreateDefault() =>
        new(new VmcsV2Header(), VmcsV2BlockDirectory.CreateDefault());

    public bool TryReadScalarField(
        ushort fieldId,
        out long value,
        out VmcsV2ValidationResult validation)
    {
        if (!Directory.TryGetField(fieldId, out VmcsV2FieldDescriptor descriptor))
        {
            value = 0;
            validation = UnknownField(fieldId);
            return false;
        }

        if (!descriptor.AccessPolicy.CanReadViaVmRead)
        {
            value = 0;
            validation = AccessDenied(descriptor, "VMREAD is not permitted for this VMCSv2 field.");
            return false;
        }

        if (descriptor.ValueType != VmcsV2FieldValueType.UInt64 &&
            descriptor.ValueType != VmcsV2FieldValueType.Epoch &&
            descriptor.ValueType != VmcsV2FieldValueType.Boolean)
        {
            value = 0;
            validation = VmcsV2ValidationResult.Fail(
                VmcsV2ValidationCode.TypeMismatch,
                fieldId,
                "The field is not a scalar VMREAD payload.");
            return false;
        }

        value = 0;
        validation = AccessDenied(
            descriptor,
            "VMCSv2 scalar projection cache was removed; scalar VMREAD requires generated read-only projection over neutral descriptors.");
        return false;
    }

    public VmcsV2ValidationResult ValidateMigrationReadiness() =>
        VirtualCpu.GprPersistence != VmcsV2GprPersistenceKind.Complete
            ? VmcsV2ValidationResult.Fail(
                VmcsV2ValidationCode.GuestGprPersistenceIncomplete,
                VmcsV2BlockDirectory.GuestIntegerRegisterFileFieldId,
                "Migration requires PC, SP, and all 32 guest integer registers.")
            : DirtyLog.Overflowed
                ? VmcsV2ValidationResult.Fail(
                    VmcsV2ValidationCode.DirtyLogOverflow,
                    VmcsV2BlockDirectory.DirtyLogBlockFieldId,
                    "Migration is blocked because dirty logging overflowed.")
            : !VectorStreamState.IsMigrationReady
                ? VmcsV2ValidationResult.Fail(
                    VmcsV2ValidationCode.VectorStreamStateIncomplete,
                    VmcsV2BlockDirectory.VectorStreamStateBlockFieldId,
                    "Migration requires materialized vector/stream guest state when vector/stream virtualization is enabled.")
                : VmcsV2ValidationResult.Success(VmcsV2BlockDirectory.GuestIntegerRegisterFileFieldId);

    public VmcsV2ValidationResult ValidateNestedEnablementReadiness()
    {
        if (VirtualCpu.GprPersistence != VmcsV2GprPersistenceKind.Complete)
        {
            return VmcsV2ValidationResult.Fail(
                VmcsV2ValidationCode.GuestGprPersistenceIncomplete,
                VmcsV2BlockDirectory.GuestIntegerRegisterFileFieldId,
                "Nested enablement is blocked until the guest GPR file is materialized.");
        }

        return VmcsV2ValidationResult.Fail(
            VmcsV2ValidationCode.NestedVmxDisabled,
            VmcsV2BlockDirectory.NptBlockFieldId,
            "Legacy Shadow VMCS block was removed without replacement; nested enablement must use a generic nested-domain projection service.");
    }

    public void RecordVectorExceptionExit(VectorStreamExceptionInfo info)
    {
        if (!info.RequiresCompatibilityExitProjection)
        {
            return;
        }

        ExitInfo.RecordVectorException(info);
    }

    public void RecordStreamDescriptorFaultExit(VectorStreamDescriptorFaultInfo info)
    {
        if (!info.IsFaulted)
        {
            return;
        }

        ExitInfo.RecordStreamDescriptorFault(info);
    }

    public void RecordStreamReplayRequiredExit(
        ushort ownerVirtualThreadId,
        ushort addressSpaceTag,
        ulong streamReplayEpoch)
    {
        ExitInfo.RecordStreamReplayRequired(ownerVirtualThreadId, addressSpaceTag, streamReplayEpoch);
    }

    private static VmcsV2ValidationResult UnknownField(ushort fieldId) =>
        VmcsV2ValidationResult.Fail(
            VmcsV2ValidationCode.UnknownField,
            fieldId,
            "The VMCSv2 field is not present in the typed block directory.");

    private static VmcsV2ValidationResult AccessDenied(
        VmcsV2FieldDescriptor descriptor,
        string message) =>
        VmcsV2ValidationResult.Fail(
            VmcsV2ValidationCode.AccessDenied,
            descriptor.FieldId,
            message);
}
