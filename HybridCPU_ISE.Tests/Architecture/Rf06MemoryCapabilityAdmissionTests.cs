using System;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf06MemoryCapabilityAdmissionTests
{
    [Fact]
    public void MemoryCapability_FreezesBankDirectionAndFootprint()
    {
        var source = new[] { new FrozenMemoryRange(0x1100, 8) };
        MemoryCapability capability = MemoryCapability.Create(
            MemoryCapabilityKind.Load,
            source,
            bank: new MemoryBankId(3));
        source[0] = new FrozenMemoryRange(0x2200, 8);

        Assert.Equal(new MemoryBankId(3), capability.Bank);
        Assert.Equal(MemoryAccessDirection.Read, capability.Direction);
        Assert.Equal((ulong)0x1100, capability.Footprint[0].Address);
        Assert.Equal((ulong)8, capability.Footprint[0].Length);
    }

    [Fact]
    public void MemoryCapability_RejectsInvalidCombinations()
    {
        Assert.Throws<ArgumentException>(() => MemoryCapability.Create(
            MemoryCapabilityKind.NoMemory,
            bank: new MemoryBankId(0)));
        Assert.Throws<ArgumentNullException>(() => MemoryCapability.Create(
            MemoryCapabilityKind.Load,
            new[] { new FrozenMemoryRange(0x1000, 4) }));
        Assert.Throws<ArgumentException>(() => MemoryCapability.Create(
            MemoryCapabilityKind.Store,
            new[] { new FrozenMemoryRange(0x1000, 4) },
            bank: new MemoryBankId(0),
            direction: MemoryAccessDirection.Read));
        Assert.Throws<ArgumentException>(() => MemoryCapability.Create(
            MemoryCapabilityKind.Load,
            new[]
            {
                new FrozenMemoryRange(0x1000, 8),
                new FrozenMemoryRange(0x1004, 8),
            },
            bank: new MemoryBankId(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrozenMemoryRange(ulong.MaxValue, 2));
    }

    [Fact]
    public void MemoryAdmissionPolicy_FreezesAndNormalizesStaticEnvelope()
    {
        var banks = new[] { new MemoryBankId(3), new MemoryBankId(1), new MemoryBankId(3) };
        var ranges = new[]
        {
            new FrozenMemoryRange(0x2000, 0x10),
            new FrozenMemoryRange(0x1000, 0x10),
        };

        MemoryAdmissionPolicy policy = MemoryAdmissionPolicy.Create(
            banks,
            MemoryAccessDirection.ReadWrite,
            ranges);
        banks[0] = new MemoryBankId(7);
        ranges[0] = new FrozenMemoryRange(0x3000, 0x10);

        Assert.Equal(new[] { 1, 3 }, policy.AllowedBanks.Select(bank => bank.Value));
        Assert.Equal((ulong)0x1000, policy.AllowedFootprint[0].Address);
        Assert.Equal((ulong)0x2000, policy.AllowedFootprint[1].Address);
    }

    [Fact]
    public void StageA_AcceptsContainedBankAndDirection()
    {
        MemoryCapability capability = MemoryCapability.Create(
            MemoryCapabilityKind.Load,
            new[] { new FrozenMemoryRange(0x1010, 8) },
            bank: new MemoryBankId(3));
        AdmissionRecord admission = CreateAdmission(capability);
        MemoryAdmissionPolicy policy = CreatePolicy(
            MemoryAccessDirection.Read,
            new FrozenMemoryRange(0x1000, 0x100));

        Rf06MemoryAdmissionResult result = Rf06MemoryCapabilityAdmission.AdmitStageA(admission, policy);

        Assert.True(result.IsAdmitted);
        Assert.Equal(Rf06MemoryAdmissionRejectReason.None, result.RejectReason);
        Assert.Same(admission, result.Admission);
    }

    [Theory]
    [InlineData((byte)Rf06MemoryAdmissionRejectReason.BankNotAllowed)]
    [InlineData((byte)Rf06MemoryAdmissionRejectReason.DirectionNotAllowed)]
    [InlineData((byte)Rf06MemoryAdmissionRejectReason.FootprintOutsidePolicy)]
    public void StageA_RejectsStaticMemoryCapabilityMismatch(
        byte expectedCode)
    {
        Rf06MemoryAdmissionRejectReason expected = (Rf06MemoryAdmissionRejectReason)expectedCode;
        MemoryCapability capability = expected switch
        {
            Rf06MemoryAdmissionRejectReason.BankNotAllowed => MemoryCapability.Create(
                MemoryCapabilityKind.Load,
                new[] { new FrozenMemoryRange(0x1010, 8) },
                bank: new MemoryBankId(7)),
            Rf06MemoryAdmissionRejectReason.DirectionNotAllowed => MemoryCapability.Create(
                MemoryCapabilityKind.Store,
                new[] { new FrozenMemoryRange(0x1010, 8) },
                bank: new MemoryBankId(3)),
            _ => MemoryCapability.Create(
                MemoryCapabilityKind.Load,
                new[] { new FrozenMemoryRange(0x2010, 8) },
                bank: new MemoryBankId(3)),
        };
        MemoryAdmissionPolicy policy = CreatePolicy(
            MemoryAccessDirection.Read,
            new FrozenMemoryRange(0x1000, 0x100));

        Rf06MemoryAdmissionResult result = Rf06MemoryCapabilityAdmission.AdmitStageA(
            CreateAdmission(capability),
            policy);

        Assert.False(result.IsAdmitted);
        Assert.Equal(expected, result.RejectReason);
    }

    [Fact]
    public void StageA_NonMemoryCapabilityHasNoAdmissionIdentity()
    {
        MemoryAdmissionPolicy policy = CreatePolicy(
            MemoryAccessDirection.ReadWrite,
            new FrozenMemoryRange(0x1000, 0x100));
        Rf06MemoryAdmissionResult result = Rf06MemoryCapabilityAdmission.AdmitStageA(
            CreateAdmission(MemoryCapability.None),
            policy);

        Assert.False(result.IsAdmitted);
        Assert.Equal(Rf06MemoryAdmissionRejectReason.NotMemoryCapability, result.RejectReason);
        Assert.DoesNotContain("OperationId", result.GetType().GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void MemoryAdmissionSourceHasNoTimingOrOperationState()
    {
        DirectoryInfo root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root.FullName,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Decoder",
            "Rf06MemoryCapabilityAdmission.cs"));

        Assert.DoesNotContain("MemorySubsystem", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResourceToken", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcome", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationAttempt", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new MicroOp", source, StringComparison.Ordinal);
    }

    private static MemoryAdmissionPolicy CreatePolicy(
        MemoryAccessDirection direction,
        FrozenMemoryRange allowedRange) =>
        MemoryAdmissionPolicy.Create(
            new[] { new MemoryBankId(3) },
            direction,
            new[] { allowedRange });

    private static AdmissionRecord CreateAdmission(MemoryCapability memory)
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(3, 1, 2),
        };
        CanonicalBundle bundle = Assert.IsType<CanonicalBundle>(
            new VliwDecoderV4().DecodeInstructionBundle(raw, 0x5000, 10).CanonicalBundle);
        CanonicalDecodedInstruction canonical = bundle.GetSlot(0);
        SourceOperationProvenance provenance = new(
            bundle.SemanticKey,
            sourceVirtualThreadId: 1,
            bundle.BundleSerial,
            sourceSlotId: SlotId.Create(canonical.SlotIndex),
            fetchEpoch: 1);

        GeneratedStaticBinding binding = new(
            Opcode: canonical.Opcode,
            MaterializerId: new MaterializerId("rf06.test.memory.materializer"),
            RuntimeExecutionProviderId: new RuntimeExecutionProviderId("rf06.test.memory.provider"),
            LatencyModelId: new LatencyModelId("rf06.test.memory.latency"),
            CatalogVersion: "test",
            CatalogSha256: "test-catalog",
            DescriptorFingerprint: "test-descriptor");
        ExecutionContract contract = ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "memory-v1"),
            InstructionClass.Memory,
            SerializationClass.MemoryOrdered,
            ExecutionPlacement.Create(SlotClass.LsuClass, SlotPinningKind.ClassFlexible),
            staticEffectContract: "MemoryEffect",
            memory,
            readRegisters: new[] { 1 },
            writeRegisters: Array.Empty<int>(),
            isStealable: true,
            isRetireVisible: true,
            isAssist: false);
        return AdmissionRecord.Create(provenance, contract, 1, 0, 0);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current;
            }
        }

        throw new DirectoryNotFoundException("HybridCPU ISE repository root was not found.");
    }
}
