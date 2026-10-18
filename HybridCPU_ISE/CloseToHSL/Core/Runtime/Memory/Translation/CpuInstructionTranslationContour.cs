using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core.Memory;

public enum CpuInstructionTranslationMode : byte
{
    Identity = 0,
    BoundedRegions = 1,
    TwoStageBoundedRegions = 2,
}

public enum CpuInstructionTranslationAccessKind : byte
{
    None = 0,
    InstructionFetch = 1,
    ScalarLoad = 2,
    ScalarStore = 3,
}

public enum CpuInstructionTranslationFaultReason : byte
{
    None = 0,
    UnmappedAddress = 1,
    AccessDenied = 2,
    AddressOverflow = 3,
    OwnerScopeMismatch = 4,
    SecondStageViolation = 5,
    SecondStageMisconfiguration = 6,
    SecondStageSourceStale = 7,
}

public enum CpuInstructionTranslationFaultStage : byte
{
    StageOne = 1,
    SecondStage = 2,
}

public readonly record struct CpuInstructionTranslationScope(
    ulong DomainId,
    int ContextId,
    int VirtualThreadId)
{
    public bool IsValid =>
        DomainId != 0 && ContextId > 0 &&
        VirtualThreadId is >= 0 and < Processor.CPU_Core.SmtWays;
}

public readonly record struct CpuInstructionTranslationRegion(
    ulong VirtualBase,
    ulong PhysicalBase,
    ulong Length,
    bool Readable,
    bool Writable,
    bool Executable)
{
    internal bool IsValid =>
        Length != 0 &&
        VirtualBase <= ulong.MaxValue - (Length - 1) &&
        PhysicalBase <= ulong.MaxValue - (Length - 1) &&
        (Readable || Writable || Executable);
}

/// <summary>
/// Explicit stage-one VA-to-GPA mapping. This type is deliberately distinct
/// from <see cref="CpuInstructionTranslationRegion"/> so a Phase 54 physical
/// result can never be reinterpreted as a guest-physical address.
/// </summary>
public readonly record struct CpuInstructionStageOneRegion(
    ulong VirtualBase,
    ulong GuestPhysicalBase,
    ulong Length,
    bool Readable,
    bool Writable,
    bool Executable)
{
    internal bool IsValid =>
        Length != 0 &&
        VirtualBase <= ulong.MaxValue - (Length - 1) &&
        GuestPhysicalBase <= ulong.MaxValue - (Length - 1) &&
        (Readable || Writable || Executable);
}

/// <summary>
/// Immutable CPU-owned address-translation policy. Identity is the production
/// default; bounded translation is enabled only by explicitly supplying a
/// mapped policy when constructing a core platform context.
/// </summary>
public sealed class CpuInstructionTranslationPolicy
{
    private CpuInstructionTranslationPolicy(
        CpuInstructionTranslationMode mode,
        CpuInstructionTranslationScope scope,
        ImmutableArray<CpuInstructionTranslationRegion> regions,
        ImmutableArray<CpuInstructionStageOneRegion> stageOneRegions = default,
        Core.MemoryDomainRuntime.CpuSecondStageTranslationBinding? secondStageBinding = null)
    {
        Mode = mode;
        Scope = scope;
        Regions = regions;
        StageOneRegions = stageOneRegions.IsDefault
            ? ImmutableArray<CpuInstructionStageOneRegion>.Empty
            : stageOneRegions;
        SecondStageBinding = secondStageBinding;
    }

    public CpuInstructionTranslationMode Mode { get; }

    public CpuInstructionTranslationScope Scope { get; }

    public ImmutableArray<CpuInstructionTranslationRegion> Regions { get; }

    public ImmutableArray<CpuInstructionStageOneRegion> StageOneRegions { get; }

    internal Core.MemoryDomainRuntime.CpuSecondStageTranslationBinding? SecondStageBinding { get; }

    public static CpuInstructionTranslationPolicy Identity { get; } =
        new(CpuInstructionTranslationMode.Identity, default, ImmutableArray<CpuInstructionTranslationRegion>.Empty);

    public static CpuInstructionTranslationPolicy CreateBoundedRegions(
        CpuInstructionTranslationScope scope,
        IEnumerable<CpuInstructionTranslationRegion> regions)
    {
        if (!scope.IsValid)
            throw new ArgumentOutOfRangeException(nameof(scope));
        ArgumentNullException.ThrowIfNull(regions);

        ImmutableArray<CpuInstructionTranslationRegion> ordered = regions
            .OrderBy(region => region.VirtualBase)
            .ToImmutableArray();
        if (ordered.IsDefaultOrEmpty)
            throw new ArgumentException("Mapped CPU translation requires at least one region.", nameof(regions));

        ulong previousEnd = 0;
        for (int index = 0; index < ordered.Length; index++)
        {
            CpuInstructionTranslationRegion region = ordered[index];
            if (!region.IsValid)
                throw new ArgumentException("CPU translation regions must be non-empty, non-overflowing and accessible.", nameof(regions));

            if (index != 0 && region.VirtualBase <= previousEnd)
                throw new ArgumentException("CPU translation regions must not overlap in virtual address space.", nameof(regions));
            previousEnd = region.VirtualBase + region.Length - 1;
        }

        return new(CpuInstructionTranslationMode.BoundedRegions, scope, ordered);
    }

    public static CpuInstructionTranslationPolicy CreateTwoStageBoundedRegions(
        CpuInstructionTranslationScope scope,
        IEnumerable<CpuInstructionStageOneRegion> stageOneRegions,
        Core.MemoryDomainRuntime memoryDomainRuntime,
        Core.MemoryDomainDescriptor memoryDomainDescriptor,
        ulong runtimeAddressSpaceIdentity)
    {
        if (!scope.IsValid)
            throw new ArgumentOutOfRangeException(nameof(scope));
        ArgumentNullException.ThrowIfNull(stageOneRegions);
        ArgumentNullException.ThrowIfNull(memoryDomainRuntime);
        ArgumentNullException.ThrowIfNull(memoryDomainDescriptor);

        ImmutableArray<CpuInstructionStageOneRegion> ordered = stageOneRegions
            .OrderBy(region => region.VirtualBase)
            .ToImmutableArray();
        if (ordered.IsDefaultOrEmpty)
            throw new ArgumentException("Two-stage CPU translation requires an explicit stage-one mapping.", nameof(stageOneRegions));

        ulong previousEnd = 0;
        for (int index = 0; index < ordered.Length; index++)
        {
            CpuInstructionStageOneRegion region = ordered[index];
            if (!region.IsValid)
                throw new ArgumentException("Stage-one mappings must be non-empty, non-overflowing and accessible.", nameof(stageOneRegions));
            if (index != 0 && region.VirtualBase <= previousEnd)
                throw new ArgumentException("Stage-one mappings must not overlap in virtual address space.", nameof(stageOneRegions));
            previousEnd = region.VirtualBase + region.Length - 1;
        }

        Core.CpuSecondStageSourceBindResult bound =
            memoryDomainRuntime.BindCanonicalCpuSecondStageTranslation(
                memoryDomainDescriptor,
                runtimeAddressSpaceIdentity,
                scope.DomainId);
        if (!bound.IsBound)
            throw new InvalidOperationException(bound.Reason);

        return new(
            CpuInstructionTranslationMode.TwoStageBoundedRegions,
            scope,
            ImmutableArray<CpuInstructionTranslationRegion>.Empty,
            ordered,
            bound.Binding);
    }
}

public readonly record struct CpuInstructionTranslationOperationIdentity(
    ulong AttemptId,
    ulong EventId,
    ulong MemoryOperationId)
{
    public bool IsValid => AttemptId != 0 && EventId != 0 && MemoryOperationId != 0;
}

public readonly record struct CpuInstructionTranslationRequest(
    CpuInstructionTranslationScope Scope,
    CpuInstructionTranslationAccessKind AccessKind,
    ulong VirtualAddress,
    ushort AccessSize,
    CpuInstructionTranslationOperationIdentity OperationIdentity)
{
    internal bool HasOperationIdentity =>
        AccessKind != CpuInstructionTranslationAccessKind.None &&
        AccessSize != 0 && OperationIdentity.IsValid;

    public bool IsValid =>
        Scope.IsValid && HasOperationIdentity;
}

public readonly record struct CpuInstructionTranslationFault(
    CpuInstructionTranslationFaultReason Reason,
    CpuInstructionTranslationRequest Request,
    ulong OwnerEpoch,
    ulong Qualification,
    ulong Auxiliary,
    CpuInstructionTranslationFaultStage FaultStage = CpuInstructionTranslationFaultStage.StageOne,
    bool HasGuestPhysicalAddress = false,
    ulong GuestPhysicalAddress = 0,
    ulong MemoryDomainOwnerEpoch = 0,
    ulong AddressSpaceGeneration = 0,
    ulong AddressSpaceIdentity = 0)
{
    public bool IsValid =>
        Reason != CpuInstructionTranslationFaultReason.None &&
        Request.IsValid && OwnerEpoch != 0 &&
        (FaultStage != CpuInstructionTranslationFaultStage.SecondStage ||
            (HasGuestPhysicalAddress && MemoryDomainOwnerEpoch != 0 &&
             AddressSpaceGeneration != 0 && AddressSpaceIdentity != 0));
}

public readonly record struct CpuInstructionTranslationResult(
    bool Succeeded,
    ulong PhysicalAddress,
    ulong OwnerEpoch,
    CpuInstructionTranslationFault? Fault,
    bool HasGuestPhysicalAddress = false,
    ulong GuestPhysicalAddress = 0)
{
    public static CpuInstructionTranslationResult Success(ulong physicalAddress, ulong ownerEpoch) =>
        new(true, physicalAddress, ownerEpoch, null);

    public static CpuInstructionTranslationResult Failed(in CpuInstructionTranslationFault fault) =>
        new(false, 0, fault.OwnerEpoch, fault);
}

public sealed class CpuInstructionTranslationFaultException : Exception
{
    public CpuInstructionTranslationFaultException(CpuInstructionTranslationFault fault)
        : base($"CPU {fault.Request.AccessKind} translation fault ({fault.Reason}) at virtual address 0x{fault.Request.VirtualAddress:X}.")
    {
        if (!fault.IsValid)
            throw new ArgumentOutOfRangeException(nameof(fault));
        Fault = fault;
    }

    public CpuInstructionTranslationFault Fault { get; }
}

public sealed class CpuInstructionTranslationSourceStaleException : Exception
{
    public CpuInstructionTranslationSourceStaleException(CpuInstructionTranslationFault fault)
        : base("CPU second-stage translation source changed; the in-flight operation must be replayed.")
    {
        Fault = fault;
    }

    public CpuInstructionTranslationFault Fault { get; }
}

internal sealed class CpuInstructionTranslationOwner
{
    private readonly CpuInstructionTranslationPolicy _policy;
    private readonly Processor.MainMemoryArea _physicalMemory;
    private ulong _nextFetchIdentity;
    private ulong _nextFallbackMemoryIdentity;

    internal CpuInstructionTranslationOwner(
        CpuInstructionTranslationPolicy policy,
        Processor.MainMemoryArea physicalMemory)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _physicalMemory = physicalMemory ?? throw new ArgumentNullException(nameof(physicalMemory));
        OwnerEpoch = 1;
    }

    internal ulong OwnerEpoch { get; }

    internal bool IsIdentity => _policy.Mode == CpuInstructionTranslationMode.Identity;

    internal CpuInstructionTranslationScope ConfiguredScope => _policy.Scope;

    internal void BindCompletionLifecycle(Core.ArchitecturalCompletionCommitOwner completionOwner)
    {
        ArgumentNullException.ThrowIfNull(completionOwner);
        Core.MemoryDomainRuntime.CpuSecondStageTranslationBinding? binding =
            _policy.SecondStageBinding;
        if (binding is null)
            return;

        var scope = new Core.CompletionObservationScope(
            _policy.Scope.DomainId,
            _policy.Scope.ContextId,
            _policy.Scope.VirtualThreadId);
        binding.Issuer.RegisterCpuSecondStageCompletionInvalidation(
            binding,
            () => completionOwner.InvalidateAfterMemoryDomainRebind(scope));
    }

    internal CpuInstructionTranslationOperationIdentity IssueFetchIdentity(ulong virtualAddress)
    {
        ulong sequence = checked(++_nextFetchIdentity);
        return new(
            sequence,
            MixNonZero(sequence, virtualAddress ^ 0x464554434845564EUL),
            MixNonZero(sequence, virtualAddress ^ 0x46455443484F5049UL));
    }

    internal CpuInstructionTranslationOperationIdentity IssueFallbackMemoryIdentity(
        CpuInstructionTranslationAccessKind accessKind,
        ulong virtualAddress)
    {
        ulong sequence = checked(++_nextFallbackMemoryIdentity);
        return new(
            sequence,
            MixNonZero(sequence, virtualAddress ^ ((ulong)(byte)accessKind << 56)),
            MixNonZero(sequence, virtualAddress ^ 0x4D454D4F5046414CUL));
    }

    internal CpuInstructionTranslationResult Translate(in CpuInstructionTranslationRequest request)
    {
        if (!request.HasOperationIdentity)
            throw new ArgumentOutOfRangeException(nameof(request));

        if (_policy.Mode == CpuInstructionTranslationMode.Identity)
            return CpuInstructionTranslationResult.Success(request.VirtualAddress, OwnerEpoch);

        if (!request.Scope.IsValid || request.Scope != _policy.Scope)
            return Fault(CpuInstructionTranslationFaultReason.OwnerScopeMismatch, request);

        ulong accessSize = request.AccessSize;
        if (request.VirtualAddress > ulong.MaxValue - (accessSize - 1))
            return Fault(CpuInstructionTranslationFaultReason.AddressOverflow, request);

        ulong requestEnd = request.VirtualAddress + accessSize - 1;
        if (_policy.Mode == CpuInstructionTranslationMode.TwoStageBoundedRegions)
            return TranslateTwoStage(request, requestEnd);

        foreach (CpuInstructionTranslationRegion region in _policy.Regions)
        {
            ulong regionEnd = region.VirtualBase + region.Length - 1;
            if (request.VirtualAddress < region.VirtualBase || requestEnd > regionEnd)
                continue;

            bool allowed = request.AccessKind switch
            {
                CpuInstructionTranslationAccessKind.InstructionFetch => region.Executable,
                CpuInstructionTranslationAccessKind.ScalarLoad => region.Readable,
                CpuInstructionTranslationAccessKind.ScalarStore => region.Writable,
                _ => false,
            };
            if (!allowed)
                return Fault(CpuInstructionTranslationFaultReason.AccessDenied, request);

            ulong offset = request.VirtualAddress - region.VirtualBase;
            return CpuInstructionTranslationResult.Success(region.PhysicalBase + offset, OwnerEpoch);
        }

        return Fault(CpuInstructionTranslationFaultReason.UnmappedAddress, request);
    }

    private CpuInstructionTranslationResult TranslateTwoStage(
        in CpuInstructionTranslationRequest request,
        ulong requestEnd)
    {
        foreach (CpuInstructionStageOneRegion region in _policy.StageOneRegions)
        {
            ulong regionEnd = region.VirtualBase + region.Length - 1;
            if (request.VirtualAddress < region.VirtualBase || requestEnd > regionEnd)
                continue;

            if (!Allows(region, request.AccessKind))
                return Fault(CpuInstructionTranslationFaultReason.AccessDenied, request);

            ulong guestPhysicalAddress = region.GuestPhysicalBase +
                (request.VirtualAddress - region.VirtualBase);
            Core.MemoryDomainRuntime.CpuSecondStageTranslationBinding? binding =
                _policy.SecondStageBinding;
            if (binding is null ||
                !binding.Issuer.TryCaptureCanonicalCpuSecondStageTranslation(
                    binding,
                    request.Scope.DomainId,
                    out Core.CpuSecondStageTranslationSnapshot snapshot))
            {
                if (binding is null)
                    throw new InvalidOperationException("Two-stage policy lacks its MemoryDomainRuntime-issued binding.");
                Core.MemoryDomainTranslationControl staleControl =
                    binding.SourceOwner.TranslationControl;
                snapshot = new Core.CpuSecondStageTranslationSnapshot(
                    binding.SourceOwner,
                    staleControl.SecondStageRoot,
                    staleControl.DomainTag,
                    binding.RuntimeAddressSpaceIdentity,
                    binding.BindingGeneration,
                    binding.AddressSpaceGeneration,
                    binding.BindingGeneration,
                    binding.SourceOwner.AddressSpace!);
                return SecondStageFault(
                    CpuInstructionTranslationFaultReason.SecondStageSourceStale,
                    request,
                    guestPhysicalAddress,
                    snapshot,
                    CpuSecondStageNeutralAuxiliary.Encode(
                        request.AccessKind,
                        request.AccessSize,
                        CpuSecondStageFaultKind.SourceStale,
                        pageWalkLevel: 1));
            }

            CpuSecondStageTranslationResult translated =
                CpuSecondStageTranslationMechanism.Translate(
                    snapshot,
                    guestPhysicalAddress,
                    request.AccessKind,
                    request.AccessSize,
                    _physicalMemory);

            if (!binding.Issuer.IsCurrentCanonicalCpuSecondStageTranslation(binding, snapshot))
            {
                return SecondStageFault(
                    CpuInstructionTranslationFaultReason.SecondStageSourceStale,
                    request,
                    guestPhysicalAddress,
                    snapshot,
                    CpuSecondStageNeutralAuxiliary.Encode(
                        request.AccessKind,
                        request.AccessSize,
                        CpuSecondStageFaultKind.SourceStale,
                        pageWalkLevel: 0));
            }

            if (!translated.Succeeded)
            {
                CpuInstructionTranslationFaultReason reason = translated.FaultKind switch
                {
                    CpuSecondStageFaultKind.Misconfiguration =>
                        CpuInstructionTranslationFaultReason.SecondStageMisconfiguration,
                    _ => CpuInstructionTranslationFaultReason.SecondStageViolation,
                };
                return SecondStageFault(
                    reason,
                    request,
                    guestPhysicalAddress,
                    snapshot,
                    translated.NeutralAuxiliary);
            }

            return new CpuInstructionTranslationResult(
                true,
                translated.CpuPhysicalAddress,
                OwnerEpoch,
                null,
                HasGuestPhysicalAddress: true,
                GuestPhysicalAddress: guestPhysicalAddress);
        }

        return Fault(CpuInstructionTranslationFaultReason.UnmappedAddress, request);
    }

    private CpuInstructionTranslationResult SecondStageFault(
        CpuInstructionTranslationFaultReason reason,
        in CpuInstructionTranslationRequest request,
        ulong guestPhysicalAddress,
        in Core.CpuSecondStageTranslationSnapshot snapshot,
        ulong auxiliary)
    {
        ulong qualification =
            ((ulong)(byte)reason << 56) |
            ((ulong)(byte)request.AccessKind << 48) |
            ((ulong)request.AccessSize << 32);
        var fault = new CpuInstructionTranslationFault(
            reason,
            request,
            OwnerEpoch,
            qualification,
            auxiliary,
            CpuInstructionTranslationFaultStage.SecondStage,
            HasGuestPhysicalAddress: true,
            GuestPhysicalAddress: guestPhysicalAddress,
            MemoryDomainOwnerEpoch: snapshot.MemoryDomainOwnerEpoch,
            AddressSpaceGeneration: snapshot.AddressSpaceGeneration,
            AddressSpaceIdentity: snapshot.AddressSpaceIdentity);
        return CpuInstructionTranslationResult.Failed(fault);
    }

    private static bool Allows(
        in CpuInstructionStageOneRegion region,
        CpuInstructionTranslationAccessKind accessKind) => accessKind switch
        {
            CpuInstructionTranslationAccessKind.InstructionFetch => region.Executable,
            CpuInstructionTranslationAccessKind.ScalarLoad => region.Readable,
            CpuInstructionTranslationAccessKind.ScalarStore => region.Writable,
            _ => false,
        };

    private CpuInstructionTranslationResult Fault(
        CpuInstructionTranslationFaultReason reason,
        in CpuInstructionTranslationRequest request)
    {
        ulong qualification =
            ((ulong)(byte)reason << 56) |
            ((ulong)(byte)request.AccessKind << 48) |
            ((ulong)request.AccessSize << 32);
        var fault = new CpuInstructionTranslationFault(
            reason,
            request,
            OwnerEpoch,
            qualification,
            OwnerEpoch);
        return CpuInstructionTranslationResult.Failed(fault);
    }

    internal static ulong CreateMemoryOperationIdentity(
        ulong attemptId,
        ulong eventId,
        CpuInstructionTranslationAccessKind accessKind,
        ulong virtualAddress)
    {
        ulong value = attemptId ^ RotateLeft(eventId, 17) ^
            ((ulong)(byte)accessKind << 56) ^ virtualAddress;
        return MixNonZero(value, 0x4350554D454D4F50UL);
    }

    private static ulong MixNonZero(ulong left, ulong right)
    {
        ulong value = unchecked((left * 0x9E3779B185EBCA87UL) ^ right);
        value ^= value >> 29;
        value *= 0xD6E8FEB86659FD93UL;
        return value == 0 ? 1UL : value;
    }

    private static ulong RotateLeft(ulong value, int count) =>
        (value << count) | (value >> (64 - count));
}
