using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Vmcs.V2;

public sealed class VmcsV2BlockDirectory
{
    public const ushort GuestIntegerRegisterFileFieldId = 0x1000;
    public const ushort VmxRootControlBlockFieldId = 0x1100;
    public const ushort VirtualCpuBlockFieldId = 0x1101;
    public const ushort BundleExecutionBlockFieldId = 0x1102;
    public const ushort VmxPreemptionTimerBlockFieldId = 0x1103;
    public const ushort VirtualInterruptFabricBlockFieldId = 0x1104;
    public const ushort VectorStreamStateBlockFieldId = 0x1105;
    public const ushort SecurityIsolationBlockFieldId = 0x1106;
    public const ushort CapabilityNegotiationBlockFieldId = 0x1107;
    public const ushort NptBlockFieldId = 0x1108;
    public const ushort ExitInfoBlockFieldId = 0x1109;
    public const ushort InterceptBitmapBlockFieldId = 0x110A;
    public const ushort EventInjectionBlockFieldId = 0x110B;
    public const ushort LaneCompletionRoutingBlockFieldId = 0x110C;
    public const ushort IoVirtBlockFieldId = 0x110D;
    public const ushort Lane6StateBlockFieldId = 0x110E;
    public const ushort Lane7StateBlockFieldId = 0x110F;
    public const ushort ShadowVmcsBlockFieldId = 0x1110;
    public const ushort DirtyLogBlockFieldId = 0x1111;
    public const ushort DebugTraceBlockFieldId = 0x1112;

    private readonly VmcsV2FieldDescriptor[] _fields;

    public VmcsV2BlockDirectory(IEnumerable<VmcsV2FieldDescriptor> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _fields = [..fields];
    }

    public IReadOnlyList<VmcsV2FieldDescriptor> Fields => _fields;

    public static VmcsV2BlockDirectory CreateDefault()
    {
        List<VmcsV2FieldDescriptor> fields = new();
        AddLegacyVmcsAliases(fields);
        AddDescriptorFoundationFields(fields);
        return new VmcsV2BlockDirectory(fields);
    }

    public bool TryGetField(ushort fieldId, out VmcsV2FieldDescriptor descriptor)
    {
        foreach (VmcsV2FieldDescriptor candidate in _fields)
        {
            if (candidate.FieldId == fieldId)
            {
                descriptor = candidate;
                return true;
            }
        }

        descriptor = default;
        return false;
    }

    public bool TryGetLegacyAlias(VmcsField field, out VmcsV2FieldDescriptor descriptor)
    {
        if (!Enum.IsDefined(typeof(VmcsField), field))
        {
            descriptor = default;
            return false;
        }

        return TryGetField((ushort)field, out descriptor);
    }

    private static void AddLegacyVmcsAliases(List<VmcsV2FieldDescriptor> fields)
    {
        foreach (VmcsField field in Enum.GetValues<VmcsField>())
        {
            fields.Add(new VmcsV2FieldDescriptor(
                (ushort)field,
                field.ToString(),
                ResolveBlock(field),
                VmcsV2FieldValueType.UInt64,
                ResolveAccessPolicy(field),
                VmcsV2ValidationPolicy.None,
                ResolveMigrationPolicy(field),
                InvalidationEpoch: 0));
        }
    }

    private static void AddDescriptorFoundationFields(List<VmcsV2FieldDescriptor> fields)
    {
        fields.Add(new VmcsV2FieldDescriptor(
            GuestIntegerRegisterFileFieldId,
            "GuestIntegerRegisterFile",
            VmcsV2BlockKind.VirtualCpu,
            VmcsV2FieldValueType.GuestIntegerRegisterFile,
            VmcsV2AccessPolicy.GuestState,
            VmcsV2ValidationPolicy.GuestGprPersistenceRequired,
            VmcsV2MigrationPolicy.MigratableGuestState,
            InvalidationEpoch: 0));

        AddBlockDescriptor(fields, VmxRootControlBlockFieldId, "VmxRootControlBlock", VmcsV2BlockKind.VmxRootControl);
        AddBlockDescriptor(fields, VirtualCpuBlockFieldId, "VirtualCpuBlock", VmcsV2BlockKind.VirtualCpu);
        AddBlockDescriptor(fields, BundleExecutionBlockFieldId, "BundleExecutionBlock", VmcsV2BlockKind.BundleExecution);
        AddBlockDescriptor(fields, VmxPreemptionTimerBlockFieldId, "VmxPreemptionTimerBlock", VmcsV2BlockKind.SchedulingBudgetTimer);
        AddBlockDescriptor(fields, VirtualInterruptFabricBlockFieldId, "VirtualInterruptFabricBlock", VmcsV2BlockKind.VirtualInterruptFabric);
        AddBlockDescriptor(fields, VectorStreamStateBlockFieldId, "VectorStreamStateBlock", VmcsV2BlockKind.VectorStreamState);
        AddBlockDescriptor(fields, SecurityIsolationBlockFieldId, "SecurityIsolationBlock", VmcsV2BlockKind.SecurityIsolation);
        AddBlockDescriptor(fields, CapabilityNegotiationBlockFieldId, "CapabilityNegotiationBlock", VmcsV2BlockKind.CapabilityNegotiation);
        AddBlockDescriptor(fields, NptBlockFieldId, "NptBlock", VmcsV2BlockKind.NestedTranslation);
        AddBlockDescriptor(fields, ExitInfoBlockFieldId, "ExitInfoBlock", VmcsV2BlockKind.ExitInformation);
        AddBlockDescriptor(fields, InterceptBitmapBlockFieldId, "InterceptBitmapBlock", VmcsV2BlockKind.InterceptBitmap);
        AddBlockDescriptor(fields, EventInjectionBlockFieldId, "EventInjectionBlock", VmcsV2BlockKind.EventInjection);
        AddBlockDescriptor(fields, LaneCompletionRoutingBlockFieldId, "LaneCompletionRoutingBlock", VmcsV2BlockKind.LaneCompletionRouting);
        AddBlockDescriptor(fields, IoVirtBlockFieldId, "IoVirtBlock", VmcsV2BlockKind.IoVirt);
        AddBlockDescriptor(fields, Lane6StateBlockFieldId, "Lane6StateBlock", VmcsV2BlockKind.Lane6State);
        AddBlockDescriptor(fields, Lane7StateBlockFieldId, "Lane7StateBlock", VmcsV2BlockKind.Lane7State);
        AddBlockDescriptor(fields, ShadowVmcsBlockFieldId, "ShadowVmcsBlock", VmcsV2BlockKind.ShadowVmcs);
        AddBlockDescriptor(fields, DirtyLogBlockFieldId, "DirtyLogBlock", VmcsV2BlockKind.DirtyLog);
        AddBlockDescriptor(fields, DebugTraceBlockFieldId, "DebugTraceBlock", VmcsV2BlockKind.DebugTrace);
    }

    private static void AddBlockDescriptor(
        List<VmcsV2FieldDescriptor> fields,
        ushort fieldId,
        string name,
        VmcsV2BlockKind block)
    {
        fields.Add(new VmcsV2FieldDescriptor(
            fieldId,
            name,
            block,
            VmcsV2FieldValueType.DescriptorReference,
            VmcsV2AccessPolicy.HostEvidence,
            VmcsV2ValidationPolicy.HostEvidenceForbidden,
            VmcsV2MigrationPolicy.ReconstructableRuntimeState,
            InvalidationEpoch: 0));
    }

    private static VmcsV2BlockKind ResolveBlock(VmcsField field) =>
        (ushort)field switch
        {
            < 0x20 => VmcsV2BlockKind.VirtualCpu,
            >= 0x20 and < 0x40 => VmcsV2BlockKind.VmxRootControl,
            >= 0x40 and < 0x60 => VmcsV2BlockKind.VmxRootControl,
            >= 0x60 and < 0x70 => VmcsV2BlockKind.ExitInformation,
            >= 0x70 and < 0x80 => VmcsV2BlockKind.ExitInformation,
            _ => VmcsV2BlockKind.CapabilityNegotiation,
        };

    private static VmcsV2AccessPolicy ResolveAccessPolicy(VmcsField field) =>
        (ushort)field switch
        {
            < 0x20 => VmcsV2AccessPolicy.GuestState,
            >= 0x20 and < 0x40 => VmcsV2AccessPolicy.HostState,
            >= 0x60 and < 0x80 => VmcsV2AccessPolicy.ExitInfo,
            _ => VmcsV2AccessPolicy.RootControl,
        };

    private static VmcsV2MigrationPolicy ResolveMigrationPolicy(VmcsField field) =>
        (ushort)field switch
        {
            < 0x20 => VmcsV2MigrationPolicy.MigratableGuestState,
            >= 0x20 and < 0x40 => VmcsV2MigrationPolicy.RootLocalState,
            _ => VmcsV2MigrationPolicy.ReconstructableRuntimeState,
        };
}
