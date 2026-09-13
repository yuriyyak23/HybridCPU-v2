using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core;

public readonly struct ExecutionDomainStateEpoch : IEquatable<ExecutionDomainStateEpoch>
{
    internal ExecutionDomainStateEpoch(ulong value) => Value = value;

    public ulong Value { get; }
    public bool IsMaterialized => Value != 0;

    public bool Equals(ExecutionDomainStateEpoch other) => Value == other.Value;
    public override bool Equals(object? obj) =>
        obj is ExecutionDomainStateEpoch other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(ExecutionDomainStateEpoch left, ExecutionDomainStateEpoch right) =>
        left.Equals(right);
    public static bool operator !=(ExecutionDomainStateEpoch left, ExecutionDomainStateEpoch right) =>
        !left.Equals(right);
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

internal enum ExecutionDomainSourceBindDecision : byte
{
    Bound = 0,
    MissingDescriptor = 1,
    DescriptorAuthorityDenied = 2,
    CompatibilityProjectionDenied = 3,
    MissingDomainIdentity = 4,
    MissingAddressSpaceIdentity = 5,
    IncompleteSourceState = 6,
}

internal readonly record struct ExecutionDomainSourceBindResult(
    ExecutionDomainSourceBindDecision Decision,
    ExecutionDomainDescriptor? Descriptor,
    ExecutionDomainStateEpoch Epoch,
    string Reason)
{
    internal bool IsBound =>
        Decision == ExecutionDomainSourceBindDecision.Bound &&
        Descriptor is not null && Epoch.IsMaterialized;
}

internal enum ExecutionDomainSourceCaptureDecision : byte
{
    Captured = 0,
    SourceUnbound = 1,
    StaleOrForeignDescriptor = 2,
    DomainMismatch = 3,
    AddressSpaceMismatch = 4,
    SourceEpochUnmaterialized = 5,
    SourceEpochMismatch = 6,
    FieldDenied = 7,
    FieldUnmaterialized = 8,
}

internal readonly record struct ExecutionDomainSourceCaptureResult(
    ExecutionDomainSourceCaptureDecision Decision,
    ExecutionDomainRuntime.SourceCapture? Capture,
    string Reason)
{
    internal bool IsCaptured =>
        Decision == ExecutionDomainSourceCaptureDecision.Captured && Capture is not null;
}

public sealed partial class ExecutionDomainRuntime
{
    /// <summary>
    /// Opaque immutable capture issued only while the existing execution-domain runtime
    /// owns the matching descriptor, epoch, domain and address-space binding. It is a
    /// freshness proof for one source read, not a capability or continuing source lease.
    /// </summary>
    internal sealed class SourceCapture
    {
        private readonly object _issuerSeal;

        internal SourceCapture(
            object issuerSeal,
            ExecutionDomainDescriptor sourceOwner,
            ExecutionDomainStateEpoch sourceEpoch,
            ulong bindingGeneration,
            ulong domainTag,
            ulong addressSpaceTag,
            VmcsField field,
            ulong value)
        {
            _issuerSeal = issuerSeal;
            SourceOwner = sourceOwner;
            SourceEpoch = sourceEpoch;
            BindingGeneration = bindingGeneration;
            DomainTag = domainTag;
            AddressSpaceTag = addressSpaceTag;
            Field = field;
            Value = value;
        }

        internal ExecutionDomainDescriptor SourceOwner { get; }
        internal ExecutionDomainStateEpoch SourceEpoch { get; }
        internal ulong BindingGeneration { get; }
        internal ulong DomainTag { get; }
        internal ulong AddressSpaceTag { get; }
        internal VmcsField Field { get; }
        internal ulong Value { get; }
        internal bool IsMaterialized =>
            SourceEpoch.IsMaterialized && BindingGeneration != 0 &&
            DomainTag != 0 && AddressSpaceTag != 0;
        internal bool RuntimeAuthorityGranted => false;
        internal bool IsCapability => false;
        internal bool IsReceipt => false;

        internal bool WasIssuedBy(object issuerSeal) => ReferenceEquals(_issuerSeal, issuerSeal);
    }

    private readonly object _sourceStateSync = new();
    private readonly object _sourceCaptureIssuerSeal = new();
    private ExecutionDomainDescriptor? _currentSourceDescriptor;
    private ExecutionDomainStateEpoch _currentSourceEpoch;
    private ulong _sourceEpochCounter;
    private ulong _sourceBindingGeneration;
    private ulong _currentSourceAddressSpaceTag;

    internal ExecutionDomainStateEpoch CurrentSourceEpoch
    {
        get { lock (_sourceStateSync) return _currentSourceEpoch; }
    }

    internal ExecutionDomainDescriptor? CurrentSourceDescriptor
    {
        get { lock (_sourceStateSync) return _currentSourceDescriptor; }
    }

    internal ExecutionDomainSourceBindResult BindAuthoritativeReadOnlyState(
        ExecutionDomainDescriptor? descriptor,
        ulong addressSpaceTag) => BindSource(descriptor, addressSpaceTag, "bound");

    internal ExecutionDomainSourceBindResult ReplaceAuthoritativeReadOnlyState(
        ExecutionDomainDescriptor? descriptor,
        ulong addressSpaceTag) => BindSource(descriptor, addressSpaceTag, "replaced");

    internal ExecutionDomainSourceBindResult RebindAuthoritativeReadOnlyStateAfterRestore(
        ExecutionDomainDescriptor? descriptor,
        ulong addressSpaceTag) => BindSource(descriptor, addressSpaceTag, "rebound after restore");

    internal void UnbindAuthoritativeReadOnlyState()
    {
        lock (_sourceStateSync)
        {
            AdvanceSourceEpoch();
            AdvanceBindingGeneration();
            _currentSourceDescriptor = null;
            _currentSourceEpoch = default;
            _currentSourceAddressSpaceTag = 0;
        }
    }

    internal ExecutionDomainSourceCaptureResult CaptureVmReadScalarSource(
        ExecutionDomainDescriptor? presentedDescriptor,
        ulong runtimeDomainTag,
        ulong runtimeAddressSpaceTag,
        VmcsField field)
    {
        lock (_sourceStateSync)
        {
            ExecutionDomainDescriptor? current = _currentSourceDescriptor;
            if (current is null)
                return DenyCapture(ExecutionDomainSourceCaptureDecision.SourceUnbound,
                    "Execution-domain read-only source is not bound.");
            if (!ReferenceEquals(presentedDescriptor, current))
                return DenyCapture(ExecutionDomainSourceCaptureDecision.StaleOrForeignDescriptor,
                    "Presented execution-domain descriptor is stale or belongs to another runtime binding.");
            if (runtimeDomainTag == 0 || current.DomainTag != runtimeDomainTag)
                return DenyCapture(ExecutionDomainSourceCaptureDecision.DomainMismatch,
                    "Execution-domain source and runtime domain identities must be non-zero and equal.");
            if (runtimeAddressSpaceTag == 0 ||
                _currentSourceAddressSpaceTag != runtimeAddressSpaceTag)
                return DenyCapture(ExecutionDomainSourceCaptureDecision.AddressSpaceMismatch,
                    "Execution-domain source and runtime address-space identities must be non-zero and equal.");
            if (!_currentSourceEpoch.IsMaterialized)
                return DenyCapture(ExecutionDomainSourceCaptureDecision.SourceEpochUnmaterialized,
                    "Authoritative execution-domain source epoch is not materialized.");

            ExecutionDomainReadOnlyStateView view = current.ReadOnlyState;
            if (view.StateEpoch == 0 || view.StateEpoch != _currentSourceEpoch.Value)
                return DenyCapture(ExecutionDomainSourceCaptureDecision.SourceEpochMismatch,
                    "Execution-domain view epoch does not match the live authoritative source epoch.");
            if (!TryReadExactField(view, field, out ulong value, out bool knownField))
                return DenyCapture(
                    knownField
                        ? ExecutionDomainSourceCaptureDecision.FieldUnmaterialized
                        : ExecutionDomainSourceCaptureDecision.FieldDenied,
                    knownField
                        ? $"Execution-domain source does not materialize '{field}'."
                        : $"Execution-domain source capture denies field '{field}'.");

            var capture = new SourceCapture(
                _sourceCaptureIssuerSeal,
                current,
                _currentSourceEpoch,
                _sourceBindingGeneration,
                runtimeDomainTag,
                runtimeAddressSpaceTag,
                field,
                value);
            return new(ExecutionDomainSourceCaptureDecision.Captured, capture,
                "Exact execution-domain scalar source was captured atomically with its owner and epoch.");
        }
    }

    internal bool IsAuthenticCapture(SourceCapture? capture) =>
        capture is not null && capture.IsMaterialized &&
        capture.WasIssuedBy(_sourceCaptureIssuerSeal);

    private ExecutionDomainSourceBindResult BindSource(
        ExecutionDomainDescriptor? descriptor,
        ulong addressSpaceTag,
        string action)
    {
        lock (_sourceStateSync)
        {
            if (descriptor is null)
                return DenyBind(ExecutionDomainSourceBindDecision.MissingDescriptor,
                    "Execution-domain source binding requires a descriptor.");
            if (!descriptor.IsAuthoritativeExecutionStateOwner)
                return DenyBind(ExecutionDomainSourceBindDecision.DescriptorAuthorityDenied,
                    "Execution-domain descriptor must retain canonical execution-state ownership.");
            if (!descriptor.CompatibilityProjectionEnabled)
                return DenyBind(ExecutionDomainSourceBindDecision.CompatibilityProjectionDenied,
                    "Execution-domain descriptor disables compatibility projection.");
            if (descriptor.DomainTag == 0)
                return DenyBind(ExecutionDomainSourceBindDecision.MissingDomainIdentity,
                    "Execution-domain source binding requires a non-zero descriptor domain tag.");
            if (addressSpaceTag == 0)
                return DenyBind(ExecutionDomainSourceBindDecision.MissingAddressSpaceIdentity,
                    "Execution-domain source binding requires a non-zero address-space tag.");
            if (!descriptor.ReadOnlyState.HasCompleteGuestPcSpFlags)
                return DenyBind(ExecutionDomainSourceBindDecision.IncompleteSourceState,
                    "Execution-domain source binding requires complete materialized GuestPc/GuestSp/GuestFlags state.");

            ExecutionDomainStateEpoch epoch = AdvanceSourceEpoch();
            ulong bindingGeneration = AdvanceBindingGeneration();
            ExecutionDomainReadOnlyStateView canonicalView = descriptor.ReadOnlyState with
            {
                StateEpoch = epoch.Value,
            };
            ExecutionDomainDescriptor canonicalDescriptor = descriptor.WithReadOnlyState(canonicalView);
            _currentSourceDescriptor = canonicalDescriptor;
            _currentSourceEpoch = epoch;
            _currentSourceAddressSpaceTag = addressSpaceTag;
            return new(ExecutionDomainSourceBindDecision.Bound, canonicalDescriptor, epoch,
                $"Authoritative execution-domain read-only source was {action} at runtime epoch {epoch.Value} and binding generation {bindingGeneration}.");
        }
    }

    private ExecutionDomainStateEpoch AdvanceSourceEpoch()
    {
        unchecked { _sourceEpochCounter++; }
        if (_sourceEpochCounter == 0) _sourceEpochCounter = 1;
        return new ExecutionDomainStateEpoch(_sourceEpochCounter);
    }

    private ulong AdvanceBindingGeneration()
    {
        unchecked { _sourceBindingGeneration++; }
        if (_sourceBindingGeneration == 0) _sourceBindingGeneration = 1;
        return _sourceBindingGeneration;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadExactField(
        ExecutionDomainReadOnlyStateView view,
        VmcsField field,
        out ulong value,
        out bool knownField)
    {
        knownField = true;
        switch (field)
        {
            case VmcsField.GuestPc when view.HasMaterializedGuestPc:
                value = view.GuestPc;
                return true;
            case VmcsField.GuestSp when view.HasMaterializedGuestSp:
                value = view.GuestSp;
                return true;
            case VmcsField.GuestFlags when view.HasMaterializedGuestFlags:
                value = view.GuestFlags;
                return true;
            case VmcsField.GuestPc:
            case VmcsField.GuestSp:
            case VmcsField.GuestFlags:
                value = 0;
                return false;
            default:
                knownField = false;
                value = 0;
                return false;
        }
    }

    private static ExecutionDomainSourceBindResult DenyBind(
        ExecutionDomainSourceBindDecision decision,
        string reason) => new(decision, null, default, reason);

    private static ExecutionDomainSourceCaptureResult DenyCapture(
        ExecutionDomainSourceCaptureDecision decision,
        string reason) => new(decision, null, reason);
}
