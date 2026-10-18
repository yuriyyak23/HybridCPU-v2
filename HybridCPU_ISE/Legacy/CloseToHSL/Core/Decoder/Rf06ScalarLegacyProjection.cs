using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Pipeline.Scheduling;
using YAKSys_Hybrid_CPU.Core.Registers;
using OpcodeValues = YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcodeValues;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// RF-06.2 checked projection for the first non-serial scalar ALU family.
///
/// This is an additive legacy-carrier adapter. It does not route, admit, issue,
/// retire, or create replay state. The provider identity is consumed from the
/// already frozen generated binding and is never reconstructed from an opcode.
/// </summary>
internal static class Rf06ScalarLegacyProjection
{
    internal const string MaterializerIdentity = "legacy.materializer.scalar-alu";
    internal const string ProviderIdentity = "legacy.provider.scalar-alu";
    internal const string LatencyIdentity = "legacy-latency-1";

    internal static bool IsSupportedOpcode(uint opcode) => opcode is
        OpcodeValues.ADD or OpcodeValues.SUB or OpcodeValues.AND or OpcodeValues.OR or OpcodeValues.XOR;

    /// <summary>
    /// Builds the immutable RF-06 capability envelope from the one generated
    /// binding carried by canonical decode. No registry or opcode lookup occurs.
    /// </summary>
    internal static ExecutionContract CreateContract(CanonicalDecodedInstruction canonical)
    {
        ValidateCanonicalFamily(canonical, out GeneratedStaticBinding binding);

        int[] reads = { canonical.Rs1, canonical.Rs2 };
        int[] writes = { canonical.Rd };

        return ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(
                binding.RuntimeExecutionProviderId,
                payloadSchema: "scalar-reg-reg-v1"),
            InstructionClass.ScalarAlu,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            staticEffectContract: "RegisterWrite",
            MemoryCapability.None,
            reads,
            writes,
            BuildRegisterResourceMask(canonical),
            isStealable: true,
            isRetireVisible: true,
            isAssist: false);
    }

    /// <summary>
    /// Projects a checked legacy scalar carrier from an already resolved
    /// immutable contract. The returned guard must be checked before a future
    /// consumer uses the mutable carrier after any legacy handoff.
    /// </summary>
    internal static CheckedScalarLegacyProjection Project(
        CanonicalDecodedInstruction canonical,
        ExecutionContract contract)
    {
        ValidateCanonicalFamily(canonical, out GeneratedStaticBinding binding);
        ArgumentNullException.ThrowIfNull(contract);

        if (contract.GeneratedBinding != binding ||
            contract.RuntimeProvider.Id != binding.RuntimeExecutionProviderId ||
            contract.InstructionClass != InstructionClass.ScalarAlu ||
            contract.SerializationClass != SerializationClass.Free ||
            contract.Placement.RequiredSlotClass != SlotClass.AluClass ||
            contract.Placement.PinningKind != SlotPinningKind.ClassFlexible ||
            contract.Memory.Kind != MemoryCapabilityKind.NoMemory ||
            contract.StaticMemoryPlan is not null ||
            !string.Equals(contract.RuntimeProvider.PayloadSchema, "scalar-reg-reg-v1", StringComparison.Ordinal) ||
            !contract.ReadRegisters.SequenceEqual(new[] { (int)canonical.Rs1, (int)canonical.Rs2 }) ||
            !contract.WriteRegisters.SequenceEqual(new[] { (int)canonical.Rd }) ||
            contract.ResourceMask != BuildRegisterResourceMask(canonical) ||
            !contract.IsStealable ||
            !contract.IsRetireVisible ||
            contract.IsAssist ||
            !string.Equals(contract.StaticEffectContract, "RegisterWrite", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The scalar legacy projection requires the matching frozen scalar execution contract.");
        }

        ScalarALUMicroOp carrier = new()
        {
            OpCode = canonical.Opcode,
            DestRegID = canonical.Rd,
            Src1RegID = canonical.Rs1,
            Src2RegID = canonical.Rs2,
            Immediate = 0,
            UsesImmediate = false,
            WritesRegister = true,
        };
        carrier.InitializeMetadata();

        return new CheckedScalarLegacyProjection(canonical, contract, carrier);
    }

    private static ResourceBitset BuildRegisterResourceMask(CanonicalDecodedInstruction canonical)
    {
        ArchRegId rs1 = ArchRegId.FromRawValue(canonical.Rs1);
        ArchRegId rs2 = ArchRegId.FromRawValue(canonical.Rs2);
        ArchRegId rd = ArchRegId.FromRawValue(canonical.Rd);

        return ResourceMaskBuilder.ForArchitecturalRegisterRead(rs1) |
               ResourceMaskBuilder.ForArchitecturalRegisterRead(rs2) |
               ResourceMaskBuilder.ForArchitecturalRegisterWrite(rd);
    }

    private static void ValidateCanonicalFamily(
        CanonicalDecodedInstruction canonical,
        out GeneratedStaticBinding binding)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        if (!canonical.IsOccupied || !IsSupportedOpcode(canonical.Opcode) ||
            canonical.InstructionClass != InstructionClass.ScalarAlu ||
            canonical.SerializationClass != SerializationClass.Free)
        {
            throw new InvalidOperationException(
                "RF-06.2 scalar projection accepts only occupied, free, register-register scalar ALU slots.");
        }

        binding = canonical.StaticBinding ?? throw new InvalidOperationException(
            "RF-06.2 scalar projection requires the generated static binding carried by canonical decode.");
        if (binding.Opcode != canonical.Opcode ||
            !string.Equals(binding.MaterializerId.Value, MaterializerIdentity, StringComparison.Ordinal) ||
            !string.Equals(binding.RuntimeExecutionProviderId.Value, ProviderIdentity, StringComparison.Ordinal) ||
            !string.Equals(binding.LatencyModelId.Value, LatencyIdentity, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "RF-06.2 scalar projection received a generated binding outside the approved scalar family.");
        }

        if (!IsPresentArchitecturalRegister(canonical.Rd) ||
            !IsPresentArchitecturalRegister(canonical.Rs1) ||
            !IsPresentArchitecturalRegister(canonical.Rs2))
        {
            throw new InvalidOperationException(
                $"RF-06.2 scalar projection requires present rd, rs1 and rs2 architectural registers in x0..x31; received rd={canonical.Rd}, rs1={canonical.Rs1}, rs2={canonical.Rs2}.");
        }
    }

    private static bool IsPresentArchitecturalRegister(byte rawValue) =>
        ArchRegId.IsRepresentable(rawValue);
}

/// <summary>
/// Mutable legacy carrier plus an immutable fingerprint of the facts that made
/// the projection valid. RF-07 outcome/completion state is intentionally absent.
/// </summary>
internal sealed class CheckedScalarLegacyProjection
{
    private readonly ScalarLegacyProjectionFingerprint _fingerprint;

    internal CheckedScalarLegacyProjection(
        CanonicalDecodedInstruction canonical,
        ExecutionContract contract,
        ScalarALUMicroOp carrier)
    {
        CanonicalInstruction = canonical;
        Contract = contract;
        Carrier = carrier;
        _fingerprint = ScalarLegacyProjectionFingerprint.Capture(contract, carrier);
    }

    internal CanonicalDecodedInstruction CanonicalInstruction { get; }
    internal ExecutionContract Contract { get; }
    internal ScalarALUMicroOp Carrier { get; }

    internal void EnsureCurrent() =>
        ScalarLegacyProjectionFingerprint.EnsureCurrent(_fingerprint, Contract, Carrier);
}

internal readonly record struct ScalarLegacyProjectionFingerprint(string Value)
{
    internal static ScalarLegacyProjectionFingerprint Capture(
        ExecutionContract contract,
        ScalarALUMicroOp carrier)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(carrier);

        string canonical = string.Join(
            "|",
            contract.GeneratedBinding.Opcode,
            contract.GeneratedBinding.MaterializerId.Value,
            contract.GeneratedBinding.RuntimeExecutionProviderId.Value,
            contract.GeneratedBinding.LatencyModelId.Value,
            contract.GeneratedBinding.DescriptorFingerprint,
            contract.RuntimeProvider.Id.Value,
            contract.RuntimeProvider.PayloadSchema,
            contract.InstructionClass,
            contract.SerializationClass,
            contract.Placement.RequiredSlotClass,
            contract.Placement.PinningKind,
            contract.Placement.PinnedLaneId,
            contract.Placement.DomainTag,
            contract.StaticEffectContract,
            contract.Memory.Kind,
            contract.StaticMemoryPlan is not null,
            string.Join(",", contract.ReadRegisters),
            string.Join(",", contract.WriteRegisters),
            contract.ResourceMask.Low,
            contract.ResourceMask.High,
            carrier.OpCode,
            carrier.DestRegID,
            carrier.Src1RegID,
            carrier.Src2RegID,
            carrier.Immediate,
            carrier.UsesImmediate,
            carrier.WritesRegister,
            carrier.IsMemoryOp,
            carrier.Latency,
            carrier.InstructionClass,
            carrier.SerializationClass,
            string.Join(",", carrier.ReadRegisters),
            string.Join(",", carrier.WriteRegisters),
            carrier.ResourceMask.Low,
            carrier.ResourceMask.High);

        return new ScalarLegacyProjectionFingerprint(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant());
    }

    internal static void EnsureCurrent(
        ScalarLegacyProjectionFingerprint expected,
        ExecutionContract contract,
        ScalarALUMicroOp carrier)
    {
        ScalarLegacyProjectionFingerprint actual = Capture(contract, carrier);
        if (actual != expected)
        {
            throw new InvalidOperationException(
                "RF-06.2 scalar legacy projection is stale after carrier mutation.");
        }
    }
}
