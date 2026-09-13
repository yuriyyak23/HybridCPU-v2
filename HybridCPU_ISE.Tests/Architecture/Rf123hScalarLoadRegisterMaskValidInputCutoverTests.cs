using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123hScalarLoadRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void ProjectionUsesIndependentCheckedPathsAndRetainsBothRawFallbacks()
    {
        string root = FindRepositoryRoot();
        string projection = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Decoder",
            "Rf08ScalarLoadContractProjection.cs"));

        Assert.Equal(2, Count(projection, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(projection,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(baseRegisterId)"));
        Assert.Equal(1, Count(projection,
            "ResourceMaskBuilder.ForRegisterRead(plan.BaseRegisterId)"));
        Assert.Equal(1, Count(projection,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite(destinationRegisterId)"));
        Assert.Equal(1, Count(projection,
            "ResourceMaskBuilder.ForRegisterWrite(plan.DestinationRegisterId)"));
        Assert.DoesNotContain("ArchRegId.Create(", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("&&", projection, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDestinationAndBaseBytePairMatchesTheFrozenRawMask()
    {
        for (int destinationValue = byte.MinValue;
             destinationValue <= byte.MaxValue;
             destinationValue++)
        {
            for (int baseValue = byte.MinValue; baseValue <= byte.MaxValue; baseValue++)
            {
                byte destinationRegisterId = (byte)destinationValue;
                byte baseRegisterId = (byte)baseValue;
                CanonicalDecodedInstruction slot = CreateCanonicalLoad(
                    destinationRegisterId,
                    baseRegisterId);
                Assert.True(CanonicalScalarLoadAddressPlan.TryCreate(
                    slot,
                    out CanonicalScalarLoadAddressPlan? plan));
                plan = Assert.IsType<CanonicalScalarLoadAddressPlan>(plan);
                CanonicalDecodedInstruction canonical = slot with
                {
                    ScalarLoadAddressPlan = plan,
                };

                ExecutionContract contract =
                    Rf08ScalarLoadContractProjection.CreateContract(canonical);
                ResourceBitset expected =
                    ResourceMaskBuilder.ForRegisterRead(baseRegisterId) |
                    ResourceMaskBuilder.ForRegisterWrite(destinationRegisterId) |
                    ResourceMaskBuilder.ForLoad();

                Assert.Equal(expected, contract.ResourceMask);
                Assert.Equal(new[] { (int)baseRegisterId }, contract.ReadRegisters);
                Assert.Equal(new[] { (int)destinationRegisterId }, contract.WriteRegisters);
            }
        }
    }

    private static CanonicalDecodedInstruction CreateCanonicalLoad(
        byte destinationRegisterId,
        byte baseRegisterId)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(
            IsaOpcodeValues.LD,
            out GeneratedIsaDescriptor descriptor));
        GeneratedStaticBinding binding = GeneratedStaticBinding.FromDescriptor(in descriptor);
        return new CanonicalDecodedInstruction(
            SlotIndex: 0,
            IsOccupied: true,
            Opcode: IsaOpcodeValues.LD,
            InstructionClass: InstructionClass.Memory,
            SerializationClass: SerializationClass.Free,
            Rd: destinationRegisterId,
            Rs1: baseRegisterId,
            Rs2: VLIW_Instruction.NoArchReg,
            Immediate: 16,
            CsrAddress: null,
            AcquireOrdering: false,
            ReleaseOrdering: false,
            RawSlot: CanonicalPayloadSnapshot.FromBytes("rf12.3h", [1]),
            InstructionPayload: CanonicalPayloadSnapshot.FromBytes("rf12.3h", [2]),
            SlotSideband: CanonicalPayloadSnapshot.FromBytes("rf12.3h", [3]))
        {
            StaticBinding = binding,
        };
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
