using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Read-only dynamic memory contour sampled by ShadowOracle/FSP.  The sample is
/// deliberately separate from <see cref="ExecutionContract"/>: it describes
/// current pressure, not an operation capability or an execution outcome.
/// </summary>
public readonly record struct Rf06MemoryShadowState
{
    private Rf06MemoryShadowState(
        byte remainingMemoryIssueBudget,
        byte remainingReadIssueBudget,
        byte remainingWriteIssueBudget,
        ImmutableArray<byte> memoryBankBudgets,
        ImmutableArray<byte> readBankBudgets,
        ImmutableArray<byte> writeBankBudgets,
        ImmutableArray<byte> consumedMemoryByBank,
        ImmutableArray<byte> consumedReadByBank,
        ImmutableArray<byte> consumedWriteByBank,
        ImmutableArray<byte> outstandingByVirtualThread,
        ImmutableArray<byte> capacityByVirtualThread,
        ushort pendingBankMask,
        ushort outstandingStoreBankMask)
    {
        RemainingMemoryIssueBudget = remainingMemoryIssueBudget;
        RemainingReadIssueBudget = remainingReadIssueBudget;
        RemainingWriteIssueBudget = remainingWriteIssueBudget;
        MemoryBankBudgets = memoryBankBudgets;
        ReadBankBudgets = readBankBudgets;
        WriteBankBudgets = writeBankBudgets;
        ConsumedMemoryByBank = consumedMemoryByBank;
        ConsumedReadByBank = consumedReadByBank;
        ConsumedWriteByBank = consumedWriteByBank;
        OutstandingByVirtualThread = outstandingByVirtualThread;
        CapacityByVirtualThread = capacityByVirtualThread;
        PendingBankMask = pendingBankMask;
        OutstandingStoreBankMask = outstandingStoreBankMask;
    }

    public byte RemainingMemoryIssueBudget { get; }
    public byte RemainingReadIssueBudget { get; }
    public byte RemainingWriteIssueBudget { get; }
    public ImmutableArray<byte> MemoryBankBudgets { get; }
    public ImmutableArray<byte> ReadBankBudgets { get; }
    public ImmutableArray<byte> WriteBankBudgets { get; }
    public ImmutableArray<byte> ConsumedMemoryByBank { get; }
    public ImmutableArray<byte> ConsumedReadByBank { get; }
    public ImmutableArray<byte> ConsumedWriteByBank { get; }
    public ImmutableArray<byte> OutstandingByVirtualThread { get; }
    public ImmutableArray<byte> CapacityByVirtualThread { get; }
    public ushort PendingBankMask { get; }
    public ushort OutstandingStoreBankMask { get; }

    public static Rf06MemoryShadowState Create(
        byte remainingMemoryIssueBudget,
        byte remainingReadIssueBudget,
        byte remainingWriteIssueBudget,
        IEnumerable<byte> memoryBankBudgets,
        IEnumerable<byte> readBankBudgets,
        IEnumerable<byte> writeBankBudgets,
        IEnumerable<byte> consumedMemoryByBank,
        IEnumerable<byte> consumedReadByBank,
        IEnumerable<byte> consumedWriteByBank,
        IEnumerable<byte> outstandingByVirtualThread,
        IEnumerable<byte> capacityByVirtualThread,
        ushort pendingBankMask = 0,
        ushort outstandingStoreBankMask = 0)
    {
        ImmutableArray<byte> memory = Freeze(memoryBankBudgets, MemoryBankId.BankCount, nameof(memoryBankBudgets));
        ImmutableArray<byte> reads = Freeze(readBankBudgets, MemoryBankId.BankCount, nameof(readBankBudgets));
        ImmutableArray<byte> writes = Freeze(writeBankBudgets, MemoryBankId.BankCount, nameof(writeBankBudgets));
        ImmutableArray<byte> consumedMemory = Freeze(consumedMemoryByBank, MemoryBankId.BankCount, nameof(consumedMemoryByBank));
        ImmutableArray<byte> consumedReads = Freeze(consumedReadByBank, MemoryBankId.BankCount, nameof(consumedReadByBank));
        ImmutableArray<byte> consumedWrites = Freeze(consumedWriteByBank, MemoryBankId.BankCount, nameof(consumedWriteByBank));
        ImmutableArray<byte> outstanding = Freeze(outstandingByVirtualThread, 4, nameof(outstandingByVirtualThread));
        ImmutableArray<byte> capacity = Freeze(capacityByVirtualThread, 4, nameof(capacityByVirtualThread));

        for (int bank = 0; bank < MemoryBankId.BankCount; bank++)
        {
            if (consumedMemory[bank] > memory[bank] ||
                consumedReads[bank] > reads[bank] ||
                consumedWrites[bank] > writes[bank])
            {
                throw new ArgumentException("A dynamic memory sample cannot exceed a bank budget.", nameof(consumedMemoryByBank));
            }
        }

        for (int vt = 0; vt < 4; vt++)
        {
            if (outstanding[vt] > capacity[vt])
            {
                throw new ArgumentException("A dynamic memory sample cannot exceed a VT scoreboard capacity.", nameof(outstandingByVirtualThread));
            }
        }

        return new Rf06MemoryShadowState(
            remainingMemoryIssueBudget,
            remainingReadIssueBudget,
            remainingWriteIssueBudget,
            memory,
            reads,
            writes,
            consumedMemory,
            consumedReads,
            consumedWrites,
            outstanding,
            capacity,
            pendingBankMask,
            outstandingStoreBankMask);
    }

    internal Rf06MemoryShadowState Consume(MemoryCapability capability, int virtualThreadId)
    {
        if (capability.Kind == MemoryCapabilityKind.NoMemory)
            return this;

        int bank = capability.Bank!.Value.Value;
        byte[] memory = ConsumedMemoryByBank.ToArray();
        byte[] reads = ConsumedReadByBank.ToArray();
        byte[] writes = ConsumedWriteByBank.ToArray();
        byte[] outstanding = OutstandingByVirtualThread.ToArray();
        memory[bank]++;
        outstanding[virtualThreadId]++;

        byte remainingRead = RemainingReadIssueBudget;
        byte remainingWrite = RemainingWriteIssueBudget;
        if (capability.Kind == MemoryCapabilityKind.Load)
        {
            reads[bank]++;
            remainingRead--;
        }
        else if (capability.Kind == MemoryCapabilityKind.Store)
        {
            writes[bank]++;
            remainingWrite--;
        }

        ushort stores = OutstandingStoreBankMask;
        if (capability.Kind == MemoryCapabilityKind.Store)
            stores |= (ushort)(1 << bank);

        return Create(
            checked((byte)(RemainingMemoryIssueBudget - 1)),
            remainingRead,
            remainingWrite,
            MemoryBankBudgets,
            ReadBankBudgets,
            WriteBankBudgets,
            memory,
            reads,
            writes,
            outstanding,
            CapacityByVirtualThread,
            PendingBankMask,
            stores);
    }

    private static ImmutableArray<byte> Freeze(IEnumerable<byte> values, int expectedLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        byte[] copy = values.ToArray();
        if (copy.Length != expectedLength)
            throw new ArgumentException($"Expected exactly {expectedLength} values.", parameterName);
        return ImmutableArray.Create(copy);
    }
}

public enum Rf06MemoryShadowRejectReason : byte
{
    None = 0,
    NotMemoryCapability,
    InvalidVirtualThread,
    NoIssueBudget,
    ScoreboardCapacity,
    PendingBank,
    OutstandingStoreBank,
    BankBudget,
    DirectionBudget,
    LegacyCarrierNotRepresentable,
}

public readonly record struct Rf06MemoryShadowDecision(
    bool IsEligible,
    Rf06MemoryShadowRejectReason RejectReason);

/// <summary>
/// Immutable memory-contract evaluator used by the RF-06.4b differential
/// contour. It mirrors only the existing ShadowOracle memory gates. It does
/// not allocate a scheduler operation, mutate a carrier, or own timing/MSHR
/// state.
/// </summary>
public static class Rf06MemoryShadowOracle
{
    public static Rf06MemoryShadowDecision EvaluateContract(
        AdmissionRecord admission,
        Rf06MemoryShadowState state)
    {
        ArgumentNullException.ThrowIfNull(admission);
        return EvaluateContract(admission.ExecutionContract.Memory, admission.VirtualThreadId, state);
    }

    public static Rf06MemoryShadowDecision EvaluateContract(
        MemoryCapability capability,
        int virtualThreadId,
        Rf06MemoryShadowState state)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (capability.Kind == MemoryCapabilityKind.NoMemory)
            return Reject(Rf06MemoryShadowRejectReason.NotMemoryCapability);
        if ((uint)virtualThreadId >= 4)
            return Reject(Rf06MemoryShadowRejectReason.InvalidVirtualThread);
        if (state.RemainingMemoryIssueBudget == 0)
            return Reject(Rf06MemoryShadowRejectReason.NoIssueBudget);
        if (state.OutstandingByVirtualThread[virtualThreadId] >= state.CapacityByVirtualThread[virtualThreadId])
            return Reject(Rf06MemoryShadowRejectReason.ScoreboardCapacity);

        int bank = capability.Bank!.Value.Value;
        if ((state.PendingBankMask & (1 << bank)) != 0)
            return Reject(Rf06MemoryShadowRejectReason.PendingBank);
        if (capability.Kind == MemoryCapabilityKind.Store &&
            (state.OutstandingStoreBankMask & (1 << bank)) != 0)
        {
            return Reject(Rf06MemoryShadowRejectReason.OutstandingStoreBank);
        }

        if (state.ConsumedMemoryByBank[bank] >= state.MemoryBankBudgets[bank])
            return Reject(Rf06MemoryShadowRejectReason.BankBudget);

        // The current legacy oracle applies directional budgets to Load and
        // Store, while Atomic follows the existing general-bank contour only.
        // Keeping this distinction is required for behavior-preserving shadow
        // migration; Atomic direction remains ReadWrite in the immutable ISA
        // capability and is not reinterpreted as mutable state here.
        if (capability.Kind == MemoryCapabilityKind.Load &&
            (state.RemainingReadIssueBudget == 0 ||
             state.ConsumedReadByBank[bank] >= state.ReadBankBudgets[bank]))
        {
            return Reject(Rf06MemoryShadowRejectReason.DirectionBudget);
        }
        if (capability.Kind == MemoryCapabilityKind.Store &&
            (state.RemainingWriteIssueBudget == 0 ||
             state.ConsumedWriteByBank[bank] >= state.WriteBankBudgets[bank]))
        {
            return Reject(Rf06MemoryShadowRejectReason.DirectionBudget);
        }

        return new Rf06MemoryShadowDecision(true, Rf06MemoryShadowRejectReason.None);
    }

    /// <summary>
    /// Compatibility-boundary projection of the existing carrier facts. This
    /// is the only place where ShadowOracle/FSP reads legacy memory carrier
    /// type/address/size/bank fields during RF-06.4b.
    /// </summary>
    public static bool TryProjectLegacyCarrier(
        LoadStoreMicroOp carrier,
        out MemoryCapability capability)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        capability = MemoryCapability.None;

        int bank = carrier.MemoryBankId;
        if ((uint)bank >= MemoryBankId.BankCount)
        {
            MemoryBankResolution nonResolvedBank =
                Processor.Memory is null
                    ? MemoryBankResolution.UnavailableTopology
                    : MemoryBankResolution.InvalidGeometry;
            if (!nonResolvedBank.IsResolved)
                return false;
        }

        MemoryCapabilityKind kind;
        ulong length;
        if (carrier is LoadMicroOp load)
        {
            kind = MemoryCapabilityKind.Load;
            length = load.Size;
        }
        else if (carrier is StoreMicroOp store)
        {
            kind = MemoryCapabilityKind.Store;
            length = store.Size;
        }
        else if (carrier is AtomicMicroOp atomic)
        {
            kind = MemoryCapabilityKind.Atomic;
            length = Math.Max(atomic.Size, (byte)4);
        }
        else
        {
            return false;
        }

        if (length == 0)
            return false;
        MemoryBankResolution bankResolution =
            MemoryBankResolution.Resolved(new MemoryBankId(bank));

        capability = MemoryCapability.Create(
            kind,
            new[] { new FrozenMemoryRange(carrier.MemoryAddress, length) },
            bankResolution.Bank!.Value);
        return true;
    }

    public static Rf06MemoryShadowDecision EvaluateLegacyCarrier(
        LoadStoreMicroOp carrier,
        Rf06MemoryShadowState state)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        if (!TryProjectLegacyCarrier(carrier, out MemoryCapability capability))
            return Reject(Rf06MemoryShadowRejectReason.LegacyCarrierNotRepresentable);

        return EvaluateLegacyFacts(carrier, capability.Kind, capability.Bank!.Value.Value, state);
    }

    public static Rf06MemoryDifferentialResult Compare(
        AdmissionRecord admission,
        LoadStoreMicroOp carrier,
        Rf06MemoryShadowState state)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(carrier);
        if (!TryProjectLegacyCarrier(carrier, out MemoryCapability projected))
        {
            return new Rf06MemoryDifferentialResult(
                false,
                false,
                false,
                "LegacyCarrierNotRepresentable",
                new Rf06MemoryShadowDecision(false, Rf06MemoryShadowRejectReason.LegacyCarrierNotRepresentable),
                EvaluateContract(admission, state));
        }

        bool staticEquivalent = AreEquivalent(projected, admission.ExecutionContract.Memory);
        Rf06MemoryShadowDecision legacy = EvaluateLegacyFacts(
            carrier,
            projected.Kind,
            projected.Bank!.Value.Value,
            state);
        Rf06MemoryShadowDecision contract = EvaluateContract(admission, state);
        bool dynamicEquivalent = legacy == contract;
        return new Rf06MemoryDifferentialResult(
            staticEquivalent && dynamicEquivalent,
            staticEquivalent,
            dynamicEquivalent,
            staticEquivalent ? (dynamicEquivalent ? string.Empty : "DynamicDecision") : "MemoryCapability",
            legacy,
            contract);
    }

    private static Rf06MemoryShadowDecision EvaluateLegacyFacts(
        LoadStoreMicroOp carrier,
        MemoryCapabilityKind kind,
        int bank,
        Rf06MemoryShadowState state)
    {
        if ((uint)carrier.VirtualThreadId >= 4)
            return Reject(Rf06MemoryShadowRejectReason.InvalidVirtualThread);
        if (state.RemainingMemoryIssueBudget == 0)
            return Reject(Rf06MemoryShadowRejectReason.NoIssueBudget);
        if (state.OutstandingByVirtualThread[carrier.VirtualThreadId] >= state.CapacityByVirtualThread[carrier.VirtualThreadId])
            return Reject(Rf06MemoryShadowRejectReason.ScoreboardCapacity);
        if ((state.PendingBankMask & (1 << bank)) != 0)
            return Reject(Rf06MemoryShadowRejectReason.PendingBank);
        if (carrier is StoreMicroOp && (state.OutstandingStoreBankMask & (1 << bank)) != 0)
            return Reject(Rf06MemoryShadowRejectReason.OutstandingStoreBank);
        if (state.ConsumedMemoryByBank[bank] >= state.MemoryBankBudgets[bank])
            return Reject(Rf06MemoryShadowRejectReason.BankBudget);
        if (carrier is LoadMicroOp &&
            (state.RemainingReadIssueBudget == 0 || state.ConsumedReadByBank[bank] >= state.ReadBankBudgets[bank]))
        {
            return Reject(Rf06MemoryShadowRejectReason.DirectionBudget);
        }
        if (carrier is StoreMicroOp &&
            (state.RemainingWriteIssueBudget == 0 || state.ConsumedWriteByBank[bank] >= state.WriteBankBudgets[bank]))
        {
            return Reject(Rf06MemoryShadowRejectReason.DirectionBudget);
        }

        return new Rf06MemoryShadowDecision(true, Rf06MemoryShadowRejectReason.None);
    }

    private static bool AreEquivalent(MemoryCapability left, MemoryCapability right)
    {
        if (left.Kind != right.Kind || left.Direction != right.Direction || left.Bank != right.Bank ||
            left.Footprint.Length != right.Footprint.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Footprint.Length; index++)
        {
            if (left.Footprint[index] != right.Footprint[index])
                return false;
        }

        return true;
    }

    private static Rf06MemoryShadowDecision Reject(Rf06MemoryShadowRejectReason reason) =>
        new(false, reason);
}

public readonly record struct Rf06MemoryDifferentialResult(
    bool IsEquivalent,
    bool StaticEquivalent,
    bool DynamicEquivalent,
    string MismatchField,
    Rf06MemoryShadowDecision LegacyDecision,
    Rf06MemoryShadowDecision ContractDecision);

public readonly record struct Rf06MemoryFspCandidate(
    AdmissionRecord Admission,
    LoadStoreMicroOp LegacyCarrier);

public readonly record struct Rf06MemoryFspDifferentialResult(
    int LegacyPacked,
    int ContractPacked,
    bool AreEquivalent,
    ImmutableArray<int> DivergentVirtualThreads);

/// <summary>
/// Four-way SMT/FSP differential contour. It models the existing SCHED2
/// ascending VT nomination order and owner exclusion. It intentionally does not
/// issue operation IDs or alter the scheduler's nomination ports.
/// </summary>
public static class Rf06MemoryFspDifferential
{
    public static Rf06MemoryFspDifferentialResult Evaluate(
        IReadOnlyList<Rf06MemoryFspCandidate> candidates,
        int ownerVirtualThreadId,
        byte readyVirtualThreadMask,
        Rf06MemoryShadowState state)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if ((uint)ownerVirtualThreadId >= 4)
            throw new ArgumentOutOfRangeException(nameof(ownerVirtualThreadId));

        int legacyPacked = 0;
        int contractPacked = 0;
        List<int> divergent = new();
        Rf06MemoryShadowState legacyState = state;
        Rf06MemoryShadowState contractState = state;

        for (int vt = 0; vt < 4; vt++)
        {
            if (vt == ownerVirtualThreadId || (readyVirtualThreadMask & (1 << vt)) == 0)
                continue;

            Rf06MemoryFspCandidate? candidate = candidates
                .FirstOrDefault(item => item.Admission.VirtualThreadId == vt);
            if (candidate is null)
                continue;

            Rf06MemoryFspCandidate selected = candidate.Value;
            Rf06MemoryShadowDecision legacy = Rf06MemoryShadowOracle.EvaluateLegacyCarrier(
                selected.LegacyCarrier,
                legacyState);
            Rf06MemoryShadowDecision contract = Rf06MemoryShadowOracle.EvaluateContract(
                selected.Admission,
                contractState);
            if (legacy != contract)
                divergent.Add(vt);

            if (legacy.IsEligible)
            {
                legacyPacked++;
                if (Rf06MemoryShadowOracle.TryProjectLegacyCarrier(selected.LegacyCarrier, out MemoryCapability legacyCapability))
                    legacyState = legacyState.Consume(legacyCapability, vt);
            }

            if (contract.IsEligible)
            {
                contractPacked++;
                contractState = contractState.Consume(
                    selected.Admission.ExecutionContract.Memory,
                    selected.Admission.VirtualThreadId);
            }
        }

        return new Rf06MemoryFspDifferentialResult(
            legacyPacked,
            contractPacked,
            divergent.Count == 0 && legacyPacked == contractPacked,
            divergent.ToImmutableArray());
    }
}
