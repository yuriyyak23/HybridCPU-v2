namespace YAKSys_Hybrid_CPU.Core;

internal enum MemoryDomainSourceBindDecision : byte
{
    Bound = 0,
    MissingDescriptor = 1,
    InvalidTranslationControl = 2,
    TranslationDisabled = 3,
    MissingDomainIdentity = 4,
    MissingAddressSpaceIdentity = 5,
}

internal readonly record struct MemoryDomainSourceBindResult(
    MemoryDomainSourceBindDecision Decision,
    MemoryDomainDescriptor? Descriptor,
    ulong AddressSpaceGeneration,
    string Reason)
{
    internal bool IsBound => Decision == MemoryDomainSourceBindDecision.Bound &&
        Descriptor is not null && AddressSpaceGeneration != 0;
}

internal enum MemoryDomainSourceCaptureDecision : byte
{
    Captured = 0,
    SourceUnbound = 1,
    StaleOrForeignDescriptor = 2,
    DomainMismatch = 3,
    AddressSpaceMismatch = 4,
    GenerationUnmaterialized = 5,
    GenerationMismatch = 6,
    FieldDenied = 7,
    SecondStageDenied = 8,
    TaggingDenied = 9,
    TargetCountDenied = 10,
}

internal readonly record struct MemoryDomainSourceCaptureResult(
    MemoryDomainSourceCaptureDecision Decision,
    MemoryDomainRuntime.SourceCapture? Capture,
    string Reason)
{
    internal bool IsCaptured => Decision == MemoryDomainSourceCaptureDecision.Captured && Capture is not null;
}

public sealed partial class MemoryDomainRuntime
{
    internal sealed class SourceCapture
    {
        private readonly object _issuerSeal;

        internal SourceCapture(
            object issuerSeal,
            MemoryDomainDescriptor sourceOwner,
            ulong addressSpaceGeneration,
            ulong bindingGeneration,
            ulong domainTag,
            ulong runtimeAddressSpaceTag,
            VmcsField field,
            ulong value)
        {
            _issuerSeal = issuerSeal;
            SourceOwner = sourceOwner;
            AddressSpaceGeneration = addressSpaceGeneration;
            BindingGeneration = bindingGeneration;
            DomainTag = domainTag;
            RuntimeAddressSpaceTag = runtimeAddressSpaceTag;
            Field = field;
            Value = value;
        }

        internal MemoryDomainDescriptor SourceOwner { get; }
        internal ulong AddressSpaceGeneration { get; }
        internal ulong BindingGeneration { get; }
        internal ulong DomainTag { get; }
        internal ulong RuntimeAddressSpaceTag { get; }
        internal VmcsField Field { get; }
        internal ulong Value { get; }
        internal bool RuntimeAuthorityGranted => false;
        internal bool IsReceipt => false;
        internal bool WasIssuedBy(object seal) => ReferenceEquals(_issuerSeal, seal);
    }

    private readonly object _translationSourceSync = new();
    private readonly object _translationCaptureIssuerSeal = new();
    private MemoryDomainDescriptor? _currentTranslationSource;
    private ulong _currentAddressSpaceGeneration;
    private ulong _addressSpaceGenerationCounter;
    private ulong _translationBindingGeneration;
    private ulong _currentRuntimeAddressSpaceTag;

    internal MemoryDomainDescriptor? CurrentTranslationSource
    {
        get { lock (_translationSourceSync) return _currentTranslationSource; }
    }

    internal ulong CurrentAddressSpaceGeneration
    {
        get { lock (_translationSourceSync) return _currentAddressSpaceGeneration; }
    }

    internal MemoryDomainSourceBindResult BindAuthoritativeTranslationView(
        MemoryDomainDescriptor? descriptor,
        ulong runtimeAddressSpaceTag) => BindSource(descriptor, runtimeAddressSpaceTag, "bound");

    internal MemoryDomainSourceBindResult ReplaceAuthoritativeTranslationView(
        MemoryDomainDescriptor? descriptor,
        ulong runtimeAddressSpaceTag) => BindSource(descriptor, runtimeAddressSpaceTag, "replaced");

    internal MemoryDomainSourceBindResult RebindAuthoritativeTranslationViewAfterRestore(
        MemoryDomainDescriptor? descriptor,
        ulong runtimeAddressSpaceTag) => BindSource(descriptor, runtimeAddressSpaceTag, "rebound after restore");

    internal void UnbindAuthoritativeTranslationView()
    {
        lock (_translationSourceSync)
        {
            AdvanceAddressSpaceGeneration();
            AdvanceBindingGeneration();
            _currentTranslationSource = null;
            _currentAddressSpaceGeneration = 0;
            _currentRuntimeAddressSpaceTag = 0;
        }
    }

    internal MemoryDomainSourceCaptureResult CaptureVmReadScalarSource(
        MemoryDomainDescriptor? presentedDescriptor,
        ulong runtimeDomainTag,
        ulong runtimeAddressSpaceTag,
        VmcsField field)
    {
        lock (_translationSourceSync)
        {
            MemoryDomainDescriptor? current = _currentTranslationSource;
            if (current is null)
                return DenyCapture(MemoryDomainSourceCaptureDecision.SourceUnbound,
                    "Memory-domain translation source is not bound.");
            if (!ReferenceEquals(presentedDescriptor, current))
                return DenyCapture(MemoryDomainSourceCaptureDecision.StaleOrForeignDescriptor,
                    "Presented memory-domain descriptor is stale or foreign.");
            MemoryDomainTranslationControl control = current.TranslationControl;
            if (runtimeDomainTag == 0 || control.DomainTag != runtimeDomainTag)
                return DenyCapture(MemoryDomainSourceCaptureDecision.DomainMismatch,
                    "Memory source and runtime domain identities must be non-zero and equal.");
            if (runtimeAddressSpaceTag == 0 || runtimeAddressSpaceTag != _currentRuntimeAddressSpaceTag)
                return DenyCapture(MemoryDomainSourceCaptureDecision.AddressSpaceMismatch,
                    "Memory source and runtime address-space identities must be non-zero and equal.");
            if (_currentAddressSpaceGeneration == 0)
                return DenyCapture(MemoryDomainSourceCaptureDecision.GenerationUnmaterialized,
                    "Runtime-owned address-space generation is not materialized.");
            if (control.AddressSpaceGeneration != _currentAddressSpaceGeneration)
                return DenyCapture(MemoryDomainSourceCaptureDecision.GenerationMismatch,
                    "Translation view generation does not match the live MemoryDomain generation.");

            ulong value;
            MemoryDomainSourceCaptureDecision denied;
            string reason;
            switch (field)
            {
                case VmcsField.GuestCr3:
                    value = control.AddressSpaceRoot;
                    break;
                case VmcsField.EptPointer when current.OwnsSecondStageTranslation && control.SecondStageRoot != 0:
                    value = control.SecondStageRoot;
                    break;
                case VmcsField.EptPointer:
                    denied = MemoryDomainSourceCaptureDecision.SecondStageDenied;
                    reason = "EptPointer requires owned valid second-stage translation.";
                    return DenyCapture(denied, reason);
                case VmcsField.Vpid when control.AddressSpaceTaggingEnabled && control.AddressSpaceTag != 0:
                    value = control.AddressSpaceTag;
                    break;
                case VmcsField.Vpid:
                    return DenyCapture(MemoryDomainSourceCaptureDecision.TaggingDenied,
                        "Vpid requires enabled tagging and a non-zero tag.");
                case VmcsField.Cr3TargetCount when
                    control.AddressSpaceTargetCount <= MemoryDomainTranslationControl.MaxAddressSpaceTargetCount:
                    value = control.AddressSpaceTargetCount;
                    break;
                case VmcsField.Cr3TargetCount:
                    return DenyCapture(MemoryDomainSourceCaptureDecision.TargetCountDenied,
                        "Cr3TargetCount exceeds the canonical target-count bound.");
                default:
                    return DenyCapture(MemoryDomainSourceCaptureDecision.FieldDenied,
                        $"Memory-domain VMREAD source capture denies field '{field}'.");
            }

            return new(MemoryDomainSourceCaptureDecision.Captured,
                new SourceCapture(_translationCaptureIssuerSeal, current,
                    _currentAddressSpaceGeneration, _translationBindingGeneration,
                    runtimeDomainTag, runtimeAddressSpaceTag, field, value),
                "Exact memory-domain scalar source was captured atomically with owner and generation.");
        }
    }

    internal bool IsAuthenticCapture(SourceCapture? capture) => capture is not null &&
        capture.AddressSpaceGeneration != 0 && capture.BindingGeneration != 0 &&
        capture.WasIssuedBy(_translationCaptureIssuerSeal);

    private MemoryDomainSourceBindResult BindSource(
        MemoryDomainDescriptor? descriptor,
        ulong runtimeAddressSpaceTag,
        string action)
    {
        lock (_translationSourceSync)
        {
            if (descriptor is null)
                return DenyBind(MemoryDomainSourceBindDecision.MissingDescriptor,
                    "Memory-domain source binding requires a descriptor.");
            if (!descriptor.HasValidTranslationControl)
                return DenyBind(MemoryDomainSourceBindDecision.InvalidTranslationControl,
                    "Memory-domain source binding requires valid translation control.");
            if (!descriptor.TranslationControl.TranslationEnabled)
                return DenyBind(MemoryDomainSourceBindDecision.TranslationDisabled,
                    "Memory-domain source binding requires enabled translation.");
            if (descriptor.TranslationControl.DomainTag == 0)
                return DenyBind(MemoryDomainSourceBindDecision.MissingDomainIdentity,
                    "Memory-domain source binding requires a non-zero domain identity.");
            if (runtimeAddressSpaceTag == 0)
                return DenyBind(MemoryDomainSourceBindDecision.MissingAddressSpaceIdentity,
                    "Memory-domain source binding requires a non-zero runtime address-space identity.");

            ulong generation = AdvanceAddressSpaceGeneration();
            ulong bindingGeneration = AdvanceBindingGeneration();
            MemoryDomainTranslationControl canonicalControl = descriptor.TranslationControl with
            {
                AddressSpaceGeneration = generation,
            };
            MemoryDomainDescriptor canonicalDescriptor = descriptor.WithTranslationControl(canonicalControl);
            _currentTranslationSource = canonicalDescriptor;
            _currentAddressSpaceGeneration = generation;
            _currentRuntimeAddressSpaceTag = runtimeAddressSpaceTag;
            return new(MemoryDomainSourceBindDecision.Bound, canonicalDescriptor, generation,
                $"Authoritative memory-domain translation source was {action} at generation {generation} and binding {bindingGeneration}.");
        }
    }

    private ulong AdvanceAddressSpaceGeneration()
    {
        unchecked { _addressSpaceGenerationCounter++; }
        if (_addressSpaceGenerationCounter == 0) _addressSpaceGenerationCounter = 1;
        return _addressSpaceGenerationCounter;
    }

    private ulong AdvanceBindingGeneration()
    {
        unchecked { _translationBindingGeneration++; }
        if (_translationBindingGeneration == 0) _translationBindingGeneration = 1;
        return _translationBindingGeneration;
    }

    private static MemoryDomainSourceBindResult DenyBind(
        MemoryDomainSourceBindDecision decision,
        string reason) => new(decision, null, 0, reason);

    private static MemoryDomainSourceCaptureResult DenyCapture(
        MemoryDomainSourceCaptureDecision decision,
        string reason) => new(decision, null, reason);
}
