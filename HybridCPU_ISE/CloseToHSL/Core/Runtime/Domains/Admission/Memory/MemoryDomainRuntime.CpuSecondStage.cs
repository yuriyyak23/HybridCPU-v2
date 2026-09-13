using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core;

internal enum CpuSecondStageSourceBindDecision : byte
{
    Bound = 0,
    AdmissionDenied = 1,
    SourceBindDenied = 2,
    MissingSecondStageRoot = 3,
    ScopeMismatch = 4,
}

internal readonly record struct CpuSecondStageSourceBindResult(
    CpuSecondStageSourceBindDecision Decision,
    MemoryDomainRuntime.CpuSecondStageTranslationBinding? Binding,
    string Reason)
{
    internal bool IsBound =>
        Decision == CpuSecondStageSourceBindDecision.Bound && Binding is not null;
}

internal readonly record struct CpuSecondStageTranslationSnapshot(
    MemoryDomainDescriptor SourceOwner,
    ulong SecondStageRoot,
    ulong DomainIdentity,
    ulong AddressSpaceIdentity,
    ulong MemoryDomainOwnerEpoch,
    ulong AddressSpaceGeneration,
    ulong BindingGeneration,
    AddressSpaceDescriptor AddressSpace)
{
    internal bool IsValid =>
        SecondStageRoot != 0 && DomainIdentity != 0 && AddressSpaceIdentity != 0 &&
        MemoryDomainOwnerEpoch != 0 && AddressSpaceGeneration != 0 &&
        BindingGeneration != 0 && AddressSpace.IsRuntimeAuthoritative;
}

public sealed partial class MemoryDomainRuntime
{
    internal sealed class CpuSecondStageTranslationBinding
    {
        private readonly object _issuerSeal;

        internal CpuSecondStageTranslationBinding(
            object issuerSeal,
            MemoryDomainRuntime issuer,
            MemoryDomainDescriptor sourceOwner,
            ulong runtimeAddressSpaceIdentity,
            ulong addressSpaceGeneration,
            ulong bindingGeneration)
        {
            _issuerSeal = issuerSeal;
            Issuer = issuer;
            SourceOwner = sourceOwner;
            RuntimeAddressSpaceIdentity = runtimeAddressSpaceIdentity;
            AddressSpaceGeneration = addressSpaceGeneration;
            BindingGeneration = bindingGeneration;
        }

        internal MemoryDomainRuntime Issuer { get; }
        internal MemoryDomainDescriptor SourceOwner { get; }
        internal ulong RuntimeAddressSpaceIdentity { get; }
        internal ulong AddressSpaceGeneration { get; }
        internal ulong BindingGeneration { get; }
        internal bool WasIssuedBy(object seal) => ReferenceEquals(_issuerSeal, seal);
    }

    private readonly object _cpuSecondStageIssuerSeal = new();
    private readonly List<Action> _cpuSecondStageLifecycleInvalidations = new();

    internal void RegisterCpuSecondStageCompletionInvalidation(
        CpuSecondStageTranslationBinding binding,
        Action invalidation)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(invalidation);
        lock (_translationSourceSync)
        {
            if (!ReferenceEquals(binding.Issuer, this) ||
                !binding.WasIssuedBy(_cpuSecondStageIssuerSeal) ||
                !ReferenceEquals(binding.SourceOwner, _currentTranslationSource) ||
                binding.BindingGeneration != _translationBindingGeneration)
            {
                throw new InvalidOperationException(
                    "CPU second-stage lifecycle registration requires the exact live MemoryDomain binding.");
            }
            _cpuSecondStageLifecycleInvalidations.Add(invalidation);
        }
    }

    private void InvalidateCpuSecondStageLifecycleBeforeSourceChange()
    {
        Action[] invalidations;
        lock (_translationSourceSync)
        {
            invalidations = _cpuSecondStageLifecycleInvalidations.ToArray();
            _cpuSecondStageLifecycleInvalidations.Clear();
        }
        for (int index = 0; index < invalidations.Length; index++)
            invalidations[index]();
    }

    internal CpuSecondStageSourceBindResult BindCanonicalCpuSecondStageTranslation(
        MemoryDomainDescriptor? descriptor,
        ulong runtimeAddressSpaceIdentity,
        ulong expectedDomainIdentity)
    {
        MemoryDomainRuntimeResult admission = Validate(new MemoryDomainRuntimeRequest(
            descriptor,
            RequiresAddressSpace: true,
            RequiresTranslationPolicy: true,
            RequiresSecondStageTranslation: true,
            RequiresDirtyTracking: false));
        if (!admission.IsAllowed)
        {
            return new(
                CpuSecondStageSourceBindDecision.AdmissionDenied,
                null,
                admission.Reason);
        }

        if (descriptor!.TranslationPolicy is not { IsRuntimeAuthoritative: true } ||
            descriptor.AddressSpace is not { IsRuntimeAuthoritative: true })
        {
            return new(
                CpuSecondStageSourceBindDecision.AdmissionDenied,
                null,
                "CPU second-stage translation requires runtime-owned memory policy and address space.");
        }

        MemoryDomainTranslationControl control = descriptor.TranslationControl;
        if (control.SecondStageRoot == 0)
        {
            return new(
                CpuSecondStageSourceBindDecision.MissingSecondStageRoot,
                null,
                "CPU second-stage translation requires a non-zero canonical MemoryDomain second-stage root.");
        }

        if (expectedDomainIdentity == 0 || control.DomainTag != expectedDomainIdentity)
        {
            return new(
                CpuSecondStageSourceBindDecision.ScopeMismatch,
                null,
                "CPU translation scope must match the canonical MemoryDomain identity.");
        }

        MemoryDomainSourceBindResult bound =
            BindAuthoritativeTranslationView(descriptor, runtimeAddressSpaceIdentity);
        if (!bound.IsBound)
        {
            return new(
                CpuSecondStageSourceBindDecision.SourceBindDenied,
                null,
                bound.Reason);
        }

        lock (_translationSourceSync)
        {
            MemoryDomainDescriptor canonical = bound.Descriptor!;
            var binding = new CpuSecondStageTranslationBinding(
                _cpuSecondStageIssuerSeal,
                this,
                canonical,
                runtimeAddressSpaceIdentity,
                _currentAddressSpaceGeneration,
                _translationBindingGeneration);
            return new(
                CpuSecondStageSourceBindDecision.Bound,
                binding,
                "Canonical MemoryDomainRuntime issued an exact CPU second-stage source binding.");
        }
    }

    internal bool TryCaptureCanonicalCpuSecondStageTranslation(
        CpuSecondStageTranslationBinding? binding,
        ulong expectedDomainIdentity,
        out CpuSecondStageTranslationSnapshot snapshot)
    {
        lock (_translationSourceSync)
        {
            snapshot = default;
            if (binding is null || !ReferenceEquals(binding.Issuer, this) ||
                !binding.WasIssuedBy(_cpuSecondStageIssuerSeal) ||
                !ReferenceEquals(binding.SourceOwner, _currentTranslationSource) ||
                binding.AddressSpaceGeneration != _currentAddressSpaceGeneration ||
                binding.BindingGeneration != _translationBindingGeneration ||
                binding.RuntimeAddressSpaceIdentity != _currentRuntimeAddressSpaceTag)
            {
                return false;
            }

            MemoryDomainDescriptor source = binding.SourceOwner;
            MemoryDomainTranslationControl control = source.TranslationControl;
            AddressSpaceDescriptor? addressSpace = source.AddressSpace;
            if (!source.OwnsSecondStageTranslation ||
                source.TranslationPolicy is not { IsRuntimeAuthoritative: true } ||
                addressSpace is not { IsRuntimeAuthoritative: true } ||
                !control.TranslationEnabled || control.SecondStageRoot == 0 ||
                control.DomainTag == 0 || control.DomainTag != expectedDomainIdentity ||
                control.AddressSpaceGeneration != _currentAddressSpaceGeneration)
            {
                return false;
            }

            snapshot = new CpuSecondStageTranslationSnapshot(
                source,
                control.SecondStageRoot,
                control.DomainTag,
                binding.RuntimeAddressSpaceIdentity,
                binding.BindingGeneration,
                _currentAddressSpaceGeneration,
                binding.BindingGeneration,
                addressSpace);
            return snapshot.IsValid;
        }
    }

    internal bool IsCurrentCanonicalCpuSecondStageTranslation(
        CpuSecondStageTranslationBinding? binding,
        in CpuSecondStageTranslationSnapshot snapshot)
    {
        lock (_translationSourceSync)
        {
            return binding is not null && snapshot.IsValid &&
                ReferenceEquals(binding.Issuer, this) &&
                binding.WasIssuedBy(_cpuSecondStageIssuerSeal) &&
                ReferenceEquals(binding.SourceOwner, snapshot.SourceOwner) &&
                ReferenceEquals(_currentTranslationSource, snapshot.SourceOwner) &&
                binding.AddressSpaceGeneration == snapshot.AddressSpaceGeneration &&
                binding.BindingGeneration == snapshot.BindingGeneration &&
                _currentAddressSpaceGeneration == snapshot.AddressSpaceGeneration &&
                _translationBindingGeneration == snapshot.BindingGeneration &&
                _currentRuntimeAddressSpaceTag == snapshot.AddressSpaceIdentity;
        }
    }
}
