using System.Threading;

namespace YAKSys_Hybrid_CPU.Core;

internal sealed class PinBasedControlsVmReadScalarDeliveryCanonicalComposition :
    IVmReadScalarResultReceiptOwner
{
    internal const string DecisionId =
        "D2-HV-VMREAD-PIN-BASED-CONTROLS-SCALAR-0006";

    private readonly object _sync = new();
    private readonly CompatibilityControlPolicyOwner _policyOwner;
    private int _enabled;
    private ulong _generation = 1;

    internal PinBasedControlsVmReadScalarDeliveryCanonicalComposition(
        CompatibilityControlPolicyOwner policyOwner,
        bool enabled)
    {
        _policyOwner = policyOwner ?? throw new ArgumentNullException(nameof(policyOwner));
        _enabled = enabled ? 1 : 0;
    }

    internal bool IsEnabled => Volatile.Read(ref _enabled) != 0;

    internal bool Disable()
    {
        lock (_sync)
        {
            _enabled = 0;
            AdvanceGeneration();
            return true;
        }
    }

    internal VmReadScalarDeliveryResult Prepare(
        ReplayPhaseContext replayPhase,
        VmxMicroOp carrier,
        VmReadScalarAttemptBinding attempt,
        ulong fieldSelector,
        ulong restoreGeneration)
    {
        lock (_sync)
        {
            if (!IsEnabled)
                return Deny(VmReadScalarDeliveryDecision.Disabled,
                    "Exact PinBasedControls scalar-delivery profile is default-disabled.");
            if (!replayPhase.IsActive || attempt.Operation != VmxOperationKind.VmRead ||
                attempt.AttemptId == 0 || attempt.BundleIdentity == 0 ||
                attempt.ReplayEpoch != replayPhase.EpochId ||
                carrier.VirtualizationAdmission != attempt.Certificate)
                return Deny(VmReadScalarDeliveryDecision.AdmissionDenied,
                    "Live canonical VMREAD E1 attempt is required.");
            if (fieldSelector > ushort.MaxValue ||
                unchecked((VmcsField)(ushort)fieldSelector) != VmcsField.PinBasedControls)
                return Deny(VmReadScalarDeliveryDecision.FieldDenied,
                    "Only exact VmcsField.PinBasedControls is admitted.");
            if (carrier.Rd is 0 or > 31 || carrier.Rs2 != 0)
                return Deny(VmReadScalarDeliveryDecision.DestinationDenied,
                    "VMREAD scalar delivery requires x1-x31 and reserved x0 Rs2.");

            CompatibilityControlPolicySnapshot snapshot = _policyOwner.CaptureCurrent();
            if (snapshot.Identity.DomainIdentity != attempt.DomainTag)
                return Deny(VmReadScalarDeliveryDecision.SourceDenied,
                    "Owner-issued compatibility-control policy does not match the VMREAD domain.");
            PinBasedControlsProjectionResult projection =
                Phase64PinBasedControlsVmReadProjectionD2.Project(_policyOwner, snapshot);
            if (!projection.IsGranted || !projection.Value.HasValue)
                return Deny(VmReadScalarDeliveryDecision.ProjectionDenied, projection.Reason);

            var receipt = new VmReadScalarResultReceipt(
                this,
                attempt,
                DecisionId,
                _policyOwner,
                snapshot.Identity.PolicyGeneration,
                snapshot,
                _generation,
                attempt.AttemptId,
                attempt.IssuerGeneration,
                attempt.BundleIdentity,
                attempt.ReplayEpoch,
                restoreGeneration,
                attempt.DomainTag,
                VmcsField.PinBasedControls,
                carrier.Rd,
                projection.Value.Value);
            return new(VmReadScalarDeliveryDecision.Prepared, receipt,
                "Exact owner-issued PinBasedControls projection captured for compatibility-only scalar delivery.");
        }
    }

    public bool ValidateLive(
        VmReadScalarResultReceipt receipt,
        ulong? currentRestoreGeneration = null)
    {
        lock (_sync)
            return Validate(receipt, currentRestoreGeneration) && !receipt.IsConsumed;
    }

    public bool ValidateConsumedBinding(
        VmReadScalarResultReceipt receipt,
        ulong currentRestoreGeneration)
    {
        lock (_sync)
            return Validate(receipt, currentRestoreGeneration);
    }

    private bool Validate(
        VmReadScalarResultReceipt receipt,
        ulong? currentRestoreGeneration)
    {
        return IsEnabled &&
            receipt.DecisionId == DecisionId &&
            receipt.ProfileGeneration == _generation &&
            receipt.CompatibilityControlCapture is { } capture &&
            ReferenceEquals(receipt.SourceOwner, _policyOwner) &&
            _policyOwner.IsCurrent(capture) &&
            receipt.SourceEpoch == capture.Identity.PolicyGeneration &&
            receipt.DomainTag == capture.Identity.DomainIdentity &&
            receipt.Field == VmcsField.PinBasedControls &&
            receipt.AttemptId != 0 && receipt.BundleIdentity != 0 &&
            (!currentRestoreGeneration.HasValue ||
                receipt.RestoreGeneration == currentRestoreGeneration.Value);
    }

    private void AdvanceGeneration()
    {
        checked { _generation++; }
        if (_generation == 0)
            throw new InvalidOperationException("PinBasedControls delivery generation exhausted.");
    }

    private static VmReadScalarDeliveryResult Deny(
        VmReadScalarDeliveryDecision decision,
        string reason) => new(decision, null, reason);
}
