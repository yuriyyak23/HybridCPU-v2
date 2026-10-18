using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// Immutable Stage-A memory admission envelope. It contains only static bank,
/// direction and address-footprint legality. Dynamic pressure, timing, MSHR
/// allocation and completion remain outside RF-06.4a.
/// </summary>
public sealed record MemoryAdmissionPolicy
{
    private MemoryAdmissionPolicy(
        ImmutableArray<MemoryBankId> allowedBanks,
        MemoryAccessDirection allowedDirections,
        ImmutableArray<FrozenMemoryRange> allowedFootprint)
    {
        AllowedBanks = allowedBanks;
        AllowedDirections = allowedDirections;
        AllowedFootprint = allowedFootprint;
    }

    public ImmutableArray<MemoryBankId> AllowedBanks { get; }
    public MemoryAccessDirection AllowedDirections { get; }
    public ImmutableArray<FrozenMemoryRange> AllowedFootprint { get; }

    public static MemoryAdmissionPolicy Create(
        IEnumerable<MemoryBankId> allowedBanks,
        MemoryAccessDirection allowedDirections,
        IEnumerable<FrozenMemoryRange> allowedFootprint)
    {
        ArgumentNullException.ThrowIfNull(allowedBanks);
        ArgumentNullException.ThrowIfNull(allowedFootprint);
        if (allowedDirections == MemoryAccessDirection.None)
        {
            throw new ArgumentException(
                "A memory admission policy must allow at least one direction.",
                nameof(allowedDirections));
        }

        MemoryBankId[] banks = allowedBanks
            .Distinct()
            .OrderBy(bank => bank.Value)
            .ToArray();
        if (banks.Length == 0)
        {
            throw new ArgumentException(
                "A memory admission policy must allow at least one bank.",
                nameof(allowedBanks));
        }

        ImmutableArray<FrozenMemoryRange> footprint = NormalizeFootprint(allowedFootprint);
        if (footprint.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "A memory admission policy must declare at least one allowed range.",
                nameof(allowedFootprint));
        }

        return new MemoryAdmissionPolicy(
            ImmutableArray.Create(banks),
            allowedDirections,
            footprint);
    }

    private static ImmutableArray<FrozenMemoryRange> NormalizeFootprint(
        IEnumerable<FrozenMemoryRange> ranges)
    {
        FrozenMemoryRange[] ordered = ranges
            .OrderBy(range => range.Address)
            .ThenBy(range => range.Length)
            .ToArray();

        for (int index = 1; index < ordered.Length; index++)
        {
            FrozenMemoryRange previous = ordered[index - 1];
            FrozenMemoryRange current = ordered[index];
            if (current.Address <= previous.LastAddress)
            {
                throw new ArgumentException(
                    "A memory admission policy cannot contain overlapping allowed ranges.",
                    nameof(ranges));
            }
        }

        return ImmutableArray.Create(ordered);
    }
}

internal enum Rf06MemoryAdmissionRejectReason : byte
{
    None = 0,
    NotMemoryCapability = 1,
    BankNotAllowed = 2,
    DirectionNotAllowed = 3,
    FootprintOutsidePolicy = 4,
}

/// <summary>
/// Result of the RF-06.4a static memory Stage-A check. It intentionally has no
/// carrier, lane, operation ID, replay entry or mutable pressure state.
/// </summary>
internal sealed record Rf06MemoryAdmissionResult(
    bool IsAdmitted,
    Rf06MemoryAdmissionRejectReason RejectReason,
    AdmissionRecord Admission)
{
    internal static Rf06MemoryAdmissionResult Accepted(AdmissionRecord admission) =>
        new(true, Rf06MemoryAdmissionRejectReason.None, admission);

    internal static Rf06MemoryAdmissionResult Rejected(
        AdmissionRecord admission,
        Rf06MemoryAdmissionRejectReason reason) =>
        new(false, reason, admission);
}

/// <summary>
/// RF-06.4a checked memory capability admission. This is a static Stage-A
/// contract check; the existing scheduler remains responsible for dynamic bank
/// pressure and the later physical-lane path.
/// </summary>
internal static class Rf06MemoryCapabilityAdmission
{
    internal static Rf06MemoryAdmissionResult AdmitStageA(
        AdmissionRecord admission,
        MemoryAdmissionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(policy);

        MemoryCapability memory = admission.ExecutionContract.Memory;
        if (memory.Kind == MemoryCapabilityKind.NoMemory ||
            !memory.Bank.HasValue ||
            memory.Direction == MemoryAccessDirection.None ||
            memory.Footprint.IsDefaultOrEmpty)
        {
            return Rf06MemoryAdmissionResult.Rejected(
                admission,
                Rf06MemoryAdmissionRejectReason.NotMemoryCapability);
        }

        if (!policy.AllowedBanks.Contains(memory.Bank.Value))
        {
            return Rf06MemoryAdmissionResult.Rejected(
                admission,
                Rf06MemoryAdmissionRejectReason.BankNotAllowed);
        }

        if ((memory.Direction & ~policy.AllowedDirections) != MemoryAccessDirection.None)
        {
            return Rf06MemoryAdmissionResult.Rejected(
                admission,
                Rf06MemoryAdmissionRejectReason.DirectionNotAllowed);
        }

        if (memory.Footprint.Any(requested =>
                !policy.AllowedFootprint.Any(allowed => Contains(allowed, requested))))
        {
            return Rf06MemoryAdmissionResult.Rejected(
                admission,
                Rf06MemoryAdmissionRejectReason.FootprintOutsidePolicy);
        }

        return Rf06MemoryAdmissionResult.Accepted(admission);
    }

    private static bool Contains(FrozenMemoryRange allowed, FrozenMemoryRange requested) =>
        requested.Address >= allowed.Address &&
        requested.LastAddress <= allowed.LastAddress;
}
