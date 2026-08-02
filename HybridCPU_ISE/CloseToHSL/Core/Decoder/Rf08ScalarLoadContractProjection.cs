using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// RF-08.3l creates only an immutable scalar-load contract source. It does not
/// create an admission, schedule a lane, or observe dynamic memory state.
/// </summary>
internal static class Rf08ScalarLoadContractProjection
{
    internal const string PayloadSchema = "scalar-load-unresolved-address-plan-v1";

    internal static ExecutionContract CreateContract(CanonicalDecodedInstruction canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        CanonicalScalarLoadAddressPlan plan = canonical.ScalarLoadAddressPlan ??
            throw new InvalidOperationException(
                "RF-08 scalar-load contract projection requires the canonical unresolved address plan.");
        plan.EnsureMatches(canonical);

        GeneratedStaticBinding binding = canonical.StaticBinding ??
            throw new InvalidOperationException(
                "RF-08 scalar-load contract projection requires the canonical generated binding.");
        if (!ReferenceEquals(binding, plan.GeneratedBinding) ||
            canonical.InstructionClass != InstructionClass.Memory ||
            canonical.SerializationClass != SerializationClass.Free)
        {
            throw new InvalidOperationException(
                "RF-08 scalar-load contract projection received a mismatched canonical load plan or binding.");
        }

        ResourceBitset resourceMask = ResourceMaskBuilder.ForLoad();
        resourceMask |= ArchRegId.TryCreate(plan.BaseRegisterId, out ArchRegId baseRegisterId)
            ? ResourceMaskBuilder.ForArchitecturalRegisterRead(baseRegisterId)
            : ResourceMaskBuilder.ForRegisterRead(plan.BaseRegisterId);
        resourceMask |= ArchRegId.TryCreate(
                plan.DestinationRegisterId,
                out ArchRegId destinationRegisterId)
            ? ResourceMaskBuilder.ForArchitecturalRegisterWrite(destinationRegisterId)
            : ResourceMaskBuilder.ForRegisterWrite(plan.DestinationRegisterId);

        return ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, PayloadSchema),
            InstructionClass.Memory,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.LsuClass, SlotPinningKind.ClassFlexible),
            staticEffectContract: "RegisterWrite",
            memory: MemoryCapability.None,
            readRegisters: [plan.BaseRegisterId],
            writeRegisters: [plan.DestinationRegisterId],
            resourceMask: resourceMask,
            isStealable: true,
            isRetireVisible: true,
            isAssist: false,
            staticMemoryPlan: StaticMemoryAccessPlan.UnresolvedScalarLoad(plan));
    }
}
