namespace YAKSys_Hybrid_CPU.Core;

public partial class MicroOpScheduler
{
    private VmReadScalarDeliveryCanonicalComposition? _exactVmReadScalarDelivery;
    private GuestPcSpFlagsVmReadScalarDeliveryCanonicalComposition?
        _exactGuestPcSpFlagsVmReadScalarDelivery;
    private MemoryOwnedVmReadScalarDeliveryCanonicalComposition?
        _exactMemoryOwnedVmReadScalarDelivery;

    internal VmReadScalarDeliveryResult? LastVmReadScalarDeliveryResult { get; private set; }
    internal bool HasExactVmReadScalarDelivery => _exactVmReadScalarDelivery is not null;
    internal bool HasExactGuestPcSpFlagsVmReadScalarDelivery =>
        _exactGuestPcSpFlagsVmReadScalarDelivery is not null;
    internal bool HasExactMemoryOwnedVmReadScalarDelivery =>
        _exactMemoryOwnedVmReadScalarDelivery is not null;

    internal void ConfigureExactVmReadScalarDelivery(
        VmReadScalarDeliveryCanonicalComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        if (_exactVmReadScalarDelivery is not null)
            throw new InvalidOperationException("Exact VMREAD scalar delivery is already configured.");
        if (!composition.IsEnabled)
            throw new InvalidOperationException("Exact VMREAD scalar delivery must be enabled before scheduler binding.");
        composition.ObserveReplayPhase(_currentReplayPhase);
        _exactVmReadScalarDelivery = composition;
        LastVmReadScalarDeliveryResult = null;
    }

    internal void DisableExactVmReadScalarDelivery()
    {
        _exactVmReadScalarDelivery?.Disable();
        _exactVmReadScalarDelivery = null;
        LastVmReadScalarDeliveryResult = null;
    }

    internal void ConfigureExactGuestPcSpFlagsVmReadScalarDelivery(
        GuestPcSpFlagsVmReadScalarDeliveryCanonicalComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        if (_exactGuestPcSpFlagsVmReadScalarDelivery is not null)
            throw new InvalidOperationException(
                "Exact GuestPc/GuestSp/GuestFlags VMREAD scalar delivery is already configured.");
        if (!composition.IsEnabled)
            throw new InvalidOperationException(
                "Exact GuestPc/GuestSp/GuestFlags VMREAD scalar delivery must be enabled before scheduler binding.");
        composition.ObserveReplayPhase(_currentReplayPhase);
        _exactGuestPcSpFlagsVmReadScalarDelivery = composition;
        LastVmReadScalarDeliveryResult = null;
    }

    internal void DisableExactGuestPcSpFlagsVmReadScalarDelivery()
    {
        _exactGuestPcSpFlagsVmReadScalarDelivery?.Disable();
        _exactGuestPcSpFlagsVmReadScalarDelivery = null;
        LastVmReadScalarDeliveryResult = null;
    }

    internal void ConfigureExactMemoryOwnedVmReadScalarDelivery(
        MemoryOwnedVmReadScalarDeliveryCanonicalComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        if (_exactMemoryOwnedVmReadScalarDelivery is not null)
            throw new InvalidOperationException(
                "Exact memory-owned VMREAD scalar delivery is already configured.");
        if (!composition.IsEnabled)
            throw new InvalidOperationException(
                "Exact memory-owned VMREAD scalar delivery must be enabled before scheduler binding.");
        composition.ObserveReplayPhase(_currentReplayPhase);
        _exactMemoryOwnedVmReadScalarDelivery = composition;
        LastVmReadScalarDeliveryResult = null;
    }

    internal void DisableExactMemoryOwnedVmReadScalarDelivery()
    {
        _exactMemoryOwnedVmReadScalarDelivery?.Disable();
        _exactMemoryOwnedVmReadScalarDelivery = null;
        LastVmReadScalarDeliveryResult = null;
    }

    internal bool TryPrepareVmReadScalarAfterCanonicalValueRead(
        BundleIssuePacket issuePacket,
        IssuePacketLane issueLane,
        ulong fieldSelector,
        ulong restoreGeneration)
    {
        _ = issuePacket;
        if (issueLane.MicroOp is not VmxMicroOp carrier ||
            carrier.VirtualizationAdmission is null ||
            !carrier.TryResolveFrozenOperation(out VmxOperationKind operation) ||
            operation != VmxOperationKind.VmRead ||
            !issueLane.IsOccupied || issueLane.PhysicalLaneIndex != 7)
            return false;

        VmcsField? selectedField = fieldSelector <= ushort.MaxValue
            ? unchecked((VmcsField)(ushort)fieldSelector)
            : null;
        var attempt = new VmReadScalarAttemptBinding(carrier.VirtualizationAdmission);
        if (selectedField is VmcsField.GuestCr3 or VmcsField.EptPointer or
            VmcsField.Vpid or VmcsField.Cr3TargetCount)
        {
            if (_exactMemoryOwnedVmReadScalarDelivery is not null)
                LastVmReadScalarDeliveryResult = _exactMemoryOwnedVmReadScalarDelivery.Prepare(
                    _currentReplayPhase, carrier, attempt,
                    fieldSelector, restoreGeneration);
            else if (_exactGuestPcSpFlagsVmReadScalarDelivery is not null)
                LastVmReadScalarDeliveryResult = _exactGuestPcSpFlagsVmReadScalarDelivery.Prepare(
                    _currentReplayPhase, carrier, attempt,
                    fieldSelector, restoreGeneration);
            else if (_exactVmReadScalarDelivery is not null)
                LastVmReadScalarDeliveryResult = _exactVmReadScalarDelivery.Prepare(
                    _currentReplayPhase, carrier, carrier.VirtualizationAdmission,
                    fieldSelector, restoreGeneration);
            else
                return false;
        }
        else if (selectedField is VmcsField.GuestPc or VmcsField.GuestSp or VmcsField.GuestFlags)
        {
            if (_exactGuestPcSpFlagsVmReadScalarDelivery is not null)
                LastVmReadScalarDeliveryResult = _exactGuestPcSpFlagsVmReadScalarDelivery.Prepare(
                    _currentReplayPhase, carrier, attempt,
                    fieldSelector, restoreGeneration);
            else if (_exactVmReadScalarDelivery is not null)
                LastVmReadScalarDeliveryResult = _exactVmReadScalarDelivery.Prepare(
                    _currentReplayPhase, carrier, carrier.VirtualizationAdmission,
                    fieldSelector, restoreGeneration);
            else if (_exactMemoryOwnedVmReadScalarDelivery is not null)
                LastVmReadScalarDeliveryResult = _exactMemoryOwnedVmReadScalarDelivery.Prepare(
                    _currentReplayPhase, carrier, attempt,
                    fieldSelector, restoreGeneration);
            else
                return false;
        }
        else if (selectedField is VmcsField.GuestCr0 or VmcsField.GuestCr4)
        {
            if (_exactVmReadScalarDelivery is not null)
                LastVmReadScalarDeliveryResult = _exactVmReadScalarDelivery.Prepare(
                    _currentReplayPhase, carrier, carrier.VirtualizationAdmission,
                    fieldSelector, restoreGeneration);
            else if (_exactGuestPcSpFlagsVmReadScalarDelivery is not null)
                LastVmReadScalarDeliveryResult = _exactGuestPcSpFlagsVmReadScalarDelivery.Prepare(
                    _currentReplayPhase, carrier, attempt,
                    fieldSelector, restoreGeneration);
            else if (_exactMemoryOwnedVmReadScalarDelivery is not null)
                LastVmReadScalarDeliveryResult = _exactMemoryOwnedVmReadScalarDelivery.Prepare(
                    _currentReplayPhase, carrier, attempt,
                    fieldSelector, restoreGeneration);
            else
                return false;
        }
        else if (_exactMemoryOwnedVmReadScalarDelivery is not null)
        {
            LastVmReadScalarDeliveryResult = _exactMemoryOwnedVmReadScalarDelivery.Prepare(
                _currentReplayPhase, carrier, attempt,
                fieldSelector, restoreGeneration);
        }
        else if (_exactGuestPcSpFlagsVmReadScalarDelivery is not null)
        {
            LastVmReadScalarDeliveryResult = _exactGuestPcSpFlagsVmReadScalarDelivery.Prepare(
                _currentReplayPhase, carrier, attempt,
                fieldSelector, restoreGeneration);
        }
        else if (_exactVmReadScalarDelivery is not null)
        {
            LastVmReadScalarDeliveryResult = _exactVmReadScalarDelivery.Prepare(
                _currentReplayPhase, carrier, carrier.VirtualizationAdmission,
                fieldSelector, restoreGeneration);
        }
        else
        {
            return false;
        }
        if (LastVmReadScalarDeliveryResult is not { IsPrepared: true, Receipt: not null } result)
            return false;
        carrier.AttachVmReadScalarResultReceipt(result.Receipt, fieldSelector);
        return true;
    }
}
