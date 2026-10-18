using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class CanonicalSecurityOperationIdentityTests
{
    [Fact]
    public void DmaEnvelope_RequiresExactSchemaAndPinnedLaneSix()
    {
        Assert.True(GeneratedStaticBinding.TryFromOpcode(IsaOpcodeValues.DmaStreamCompute, out var binding));
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(binding.Opcode, out var descriptor));
        var valid = ExecutionPlacement.Create(SlotClass.DmaStreamClass, SlotPinningKind.HardPinned, 6);
        Assert.Equal("DmaCommit", Build(valid, "rf06.dma-v1").StaticEffectContract);
        Assert.Throws<ArgumentException>(() => Build(valid, "caller.schema"));
        Assert.Throws<ArgumentException>(() => Build(
            ExecutionPlacement.Create(SlotClass.DmaStreamClass, SlotPinningKind.HardPinned, 5), "rf06.dma-v1"));
        Assert.Throws<ArgumentException>(() => Build(
            ExecutionPlacement.Create(SlotClass.DmaStreamClass, SlotPinningKind.ClassFlexible), "rf06.dma-v1"));
        ExecutionContract Build(ExecutionPlacement placement, string schema) => ExecutionContract.Create(binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, schema),
            descriptor.StaticClass, descriptor.Serialization, placement, "DmaCommit", MemoryCapability.None);
    }

    [Theory]
    [InlineData(IsaOpcodeValues.VLOAD)]
    [InlineData(IsaOpcodeValues.VSTORE)]
    public void ResidualVectorMemory_HasExplicitDenyUntilTypedContractExists(ushort opcode)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(opcode, out var descriptor));
        var binding = GeneratedStaticBinding.FromDescriptor(descriptor);
        Assert.Throws<ArgumentException>(() => ExecutionContract.Create(binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "memory-v1"),
            descriptor.StaticClass, descriptor.Serialization,
            ExecutionPlacement.Create(SlotClass.LsuClass, SlotPinningKind.ClassFlexible),
            descriptor.StaticEffectContract, MemoryCapability.None));
    }

    public static IEnumerable<object[]> AllGeneratedOperations() => GeneratedIsaCatalog.Descriptors.Select(d => new object[] { d.Opcode });

    [Theory]
    [MemberData(nameof(AllGeneratedOperations))]
    public void EveryGeneratedOperation_DeniesUnregisteredEffect(uint opcode)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(opcode, out var descriptor));
        var binding = GeneratedStaticBinding.FromDescriptor(descriptor);
        Assert.Throws<ArgumentException>(() => ExecutionContract.Create(binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "caller.schema"),
            descriptor.StaticClass, descriptor.Serialization,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            "UnknownSecureEffect", MemoryCapability.None));
    }

    [Theory]
    [InlineData(IsaOpcodeValues.SB, MemoryCapabilityKind.Store, "StoreCommit")]
    [InlineData(IsaOpcodeValues.SH, MemoryCapabilityKind.Store, "StoreCommit")]
    [InlineData(IsaOpcodeValues.SW, MemoryCapabilityKind.Store, "StoreCommit")]
    [InlineData(IsaOpcodeValues.SD, MemoryCapabilityKind.Store, "StoreCommit")]
    [InlineData(IsaOpcodeValues.LR_W, MemoryCapabilityKind.Load, "AtomicCommit")]
    [InlineData(IsaOpcodeValues.LR_D, MemoryCapabilityKind.Load, "AtomicCommit")]
    [InlineData(IsaOpcodeValues.SC_W, MemoryCapabilityKind.Store, "AtomicCommit")]
    [InlineData(IsaOpcodeValues.SC_D, MemoryCapabilityKind.Store, "AtomicCommit")]
    [InlineData(IsaOpcodeValues.AMOADD_W, MemoryCapabilityKind.Atomic, "AtomicCommit")]
    [InlineData(IsaOpcodeValues.AMOADD_D, MemoryCapabilityKind.Atomic, "AtomicCommit")]
    public void StoreAtomicHandlers_RequireExactDirectionAndEffects(ushort opcode, MemoryCapabilityKind kind, string effect)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(opcode, out var descriptor));
        var binding = GeneratedStaticBinding.FromDescriptor(descriptor);
        var memory = MemoryCapability.Create(kind, new[] { new FrozenMemoryRange(0x1000, 8) }, new MemoryBankId(0));
        Assert.Equal(kind, Build(effect, memory).Memory.Kind);
        Assert.Throws<ArgumentException>(() => Build("RegisterWrite", memory));
        Assert.Throws<ArgumentException>(() => Build(effect, MemoryCapability.None));
        ExecutionContract Build(string chosenEffect, MemoryCapability capability) => ExecutionContract.Create(binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "memory-v1"),
            descriptor.StaticClass, descriptor.Serialization,
            ExecutionPlacement.Create(SlotClass.LsuClass, SlotPinningKind.ClassFlexible), chosenEffect, capability);
    }

    public static IEnumerable<object[]> SystemOperations() => GeneratedIsaCatalog.Descriptors
        .Where(d => d.StaticClass is YAKSys_Hybrid_CPU.Arch.InstructionClass.Csr or
            YAKSys_Hybrid_CPU.Arch.InstructionClass.System or YAKSys_Hybrid_CPU.Arch.InstructionClass.SmtVt or
            YAKSys_Hybrid_CPU.Arch.InstructionClass.Vmx)
        .Select(d => new object[] { d.Opcode });

    [Theory]
    [MemberData(nameof(SystemOperations))]
    public void SystemFamilies_PreserveGeneratedEnvelopeAndDenyForeignEffects(uint opcode)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(opcode, out var descriptor));
        var binding = GeneratedStaticBinding.FromDescriptor(descriptor);
        var placement = ExecutionPlacement.Create(SlotClass.SystemSingleton, SlotPinningKind.ClassFlexible);
        Assert.Equal(descriptor.StaticEffectContract, Build(descriptor.StaticEffectContract, "ordinary", MemoryCapability.None).StaticEffectContract);
        foreach (var effect in new[] { "RegisterWrite", "StoreCommit", "PcWrite", "SecureSystemEffect", "unknown" })
            Assert.Throws<ArgumentException>(() => Build(effect, "ordinary", MemoryCapability.None));
        Assert.Throws<ArgumentException>(() => Build("SerializingEffect", "caller.schema", MemoryCapability.None));
        Assert.Throws<ArgumentException>(() => Build("AcceleratorCommand", "caller.schema", MemoryCapability.None));
        var load = MemoryCapability.Create(MemoryCapabilityKind.Load,
            new[] { new FrozenMemoryRange(0x1000, 8) }, new MemoryBankId(0));
        Assert.Throws<ArgumentException>(() => Build(descriptor.StaticEffectContract, "ordinary", load));
        if (descriptor.Serialization != YAKSys_Hybrid_CPU.Arch.SerializationClass.Free)
            Assert.Equal("SerializingEffect", Build("SerializingEffect", "rf06.serializing-v1", MemoryCapability.None).StaticEffectContract);
        if (descriptor.StaticClass != YAKSys_Hybrid_CPU.Arch.InstructionClass.System)
            Assert.Throws<ArgumentException>(() => Build("AcceleratorCommand", "rf06.accelerator-v1", MemoryCapability.None));

        ExecutionContract Build(string effect, string schema, MemoryCapability memory) => ExecutionContract.Create(
            binding, new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, schema),
            descriptor.StaticClass, descriptor.Serialization, placement, effect, memory);
    }

    [Theory]
    [InlineData(IsaOpcodeValues.MTILE_LOAD, MemoryCapabilityKind.Load, "MatrixTileMemoryCommit")]
    [InlineData(IsaOpcodeValues.MTILE_STORE, MemoryCapabilityKind.Store, "MatrixTileMemoryCommit")]
    [InlineData(IsaOpcodeValues.MTILE_MACC, MemoryCapabilityKind.NoMemory, "MatrixTileCommit")]
    [InlineData(IsaOpcodeValues.MTRANSPOSE, MemoryCapabilityKind.NoMemory, "MatrixTileCommit")]
    public void MatrixTileFamily_PreservesOrdinaryEffectsAndRejectsSubstitution(
        ushort opcode, MemoryCapabilityKind kind, string effect)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(opcode, out var descriptor));
        var binding = GeneratedStaticBinding.FromDescriptor(descriptor);
        var memory = kind == MemoryCapabilityKind.NoMemory ? MemoryCapability.None :
            MemoryCapability.Create(kind, new[] { new FrozenMemoryRange(0x1000, 16) }, new MemoryBankId(0));
        Assert.Equal(effect, Build(effect, "rf06.matrix-tile-v1", memory).StaticEffectContract);
        foreach (var wrong in new[] { "RegisterWrite", "StoreCommit", "PcWrite", "SecureTileCommit", "future" })
            Assert.Throws<ArgumentException>(() => Build(wrong, "rf06.matrix-tile-v1", memory));
        Assert.Throws<ArgumentException>(() => Build(effect, "caller.schema", memory));
        var wrongMemory = kind == MemoryCapabilityKind.NoMemory ?
            MemoryCapability.Create(MemoryCapabilityKind.Load, new[] { new FrozenMemoryRange(0x2000, 8) }, new MemoryBankId(0)) :
            MemoryCapability.None;
        Assert.Throws<ArgumentException>(() => Build(effect, "rf06.matrix-tile-v1", wrongMemory));

        ExecutionContract Build(string chosenEffect, string schema, MemoryCapability capability) => ExecutionContract.Create(
            binding, new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, schema),
            descriptor.StaticClass, descriptor.Serialization,
            ExecutionPlacement.Create(SlotClass.MatrixTileStreamClass, SlotPinningKind.ClassFlexible),
            chosenEffect, capability);
    }

    [Theory]
    [InlineData(IsaOpcodeValues.JAL)]
    [InlineData(IsaOpcodeValues.JALR)]
    [InlineData(IsaOpcodeValues.BEQ)]
    [InlineData(IsaOpcodeValues.BNE)]
    [InlineData(IsaOpcodeValues.BLT)]
    [InlineData(IsaOpcodeValues.BGE)]
    [InlineData(IsaOpcodeValues.BLTU)]
    [InlineData(IsaOpcodeValues.BGEU)]
    public void ControlFlow_RejectsForeignEffectMemoryClassAndAssist(ushort opcode)
    {
        Assert.True(GeneratedStaticBinding.TryFromOpcode(opcode, out var binding));
        var provider = new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "rf06.control-flow-v1");
        var placement = ExecutionPlacement.Create(SlotClass.BranchControl, SlotPinningKind.ClassFlexible);
        foreach (var effect in new[] { "RegisterWrite", "StoreCommit", "AtomicCommit", "SecurePcWrite", "future" })
            Assert.Throws<ArgumentException>(() => Build(effect));
        var load = MemoryCapability.Create(MemoryCapabilityKind.Load,
            new[] { new FrozenMemoryRange(0x1000, 8) }, new MemoryBankId(0));
        Assert.Throws<ArgumentException>(() => Build("PcWrite", load));
        Assert.Throws<ArgumentException>(() => Build("PcWrite", assist: true));
        Assert.Throws<ArgumentException>(() => ExecutionContract.Create(binding, provider,
            YAKSys_Hybrid_CPU.Arch.InstructionClass.Memory, YAKSys_Hybrid_CPU.Arch.SerializationClass.Free,
            placement, "PcWrite", MemoryCapability.None));
        Assert.Equal("PcWrite", Build("PcWrite").StaticEffectContract);

        ExecutionContract Build(string effect, MemoryCapability? memory = null, bool assist = false) =>
            ExecutionContract.Create(binding, provider, YAKSys_Hybrid_CPU.Arch.InstructionClass.ControlFlow,
                YAKSys_Hybrid_CPU.Arch.SerializationClass.Free, placement, effect, memory ?? MemoryCapability.None,
                isAssist: assist, isRetireVisible: !assist);
    }

    [Theory]
    [InlineData(IsaOpcodeValues.LB)]
    [InlineData(IsaOpcodeValues.LBU)]
    [InlineData(IsaOpcodeValues.LH)]
    [InlineData(IsaOpcodeValues.LHU)]
    [InlineData(IsaOpcodeValues.LW)]
    [InlineData(IsaOpcodeValues.LWU)]
    [InlineData(IsaOpcodeValues.LD)]
    public void ScalarLoads_RejectForeignEffectsMissingReadAndForgedShadowSchema(ushort opcode)
    {
        Assert.True(GeneratedStaticBinding.TryFromOpcode(opcode, out var binding));
        var load = MemoryCapability.Create(MemoryCapabilityKind.Load,
            new[] { new FrozenMemoryRange(0x1000, 8) }, new MemoryBankId(0));
        var store = MemoryCapability.Create(MemoryCapabilityKind.Store,
            new[] { new FrozenMemoryRange(0x1000, 8) }, new MemoryBankId(0));
        foreach (var effect in new[] { "StoreCommit", "AtomicCommit", "PcWrite", "SecureRegisterWrite", "future" })
            Assert.Throws<ArgumentException>(() => Build(effect, "load-v1", load));
        Assert.Throws<ArgumentException>(() => Build("RegisterWrite", "load-v1", MemoryCapability.None));
        Assert.Throws<ArgumentException>(() => Build("RegisterWrite", "load-v1", store));
        Assert.Throws<ArgumentException>(() => Build("MemoryEffect", "forged-shadow", load));
        Assert.Throws<ArgumentException>(() => Build("AssistPrefetch", "rf06.assist-v1", load));
        Assert.Equal(MemoryCapabilityKind.Load, Build("RegisterWrite", "load-v1", load).Memory.Kind);

        ExecutionContract Build(string effect, string schema, MemoryCapability memory) => ExecutionContract.Create(
            binding, new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, schema),
            YAKSys_Hybrid_CPU.Arch.InstructionClass.Memory, YAKSys_Hybrid_CPU.Arch.SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.LsuClass, SlotPinningKind.ClassFlexible), effect, memory);
    }

    [Fact]
    public void RecognizedScalarOpcode_CannotAcquireForeignEffectsOrClassification()
    {
        Assert.True(GeneratedStaticBinding.TryFromOpcode(IsaOpcodeValues.ADD, out var binding));
        var provider = new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "scalar-v1");
        var placement = ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible);
        var memory = MemoryCapability.Create(MemoryCapabilityKind.Load,
            new[] { new FrozenMemoryRange(0x1000, 8) }, new MemoryBankId(0));
        Assert.Throws<ArgumentException>(() => ExecutionContract.Create(binding, provider,
            YAKSys_Hybrid_CPU.Arch.InstructionClass.ScalarAlu, YAKSys_Hybrid_CPU.Arch.SerializationClass.Free,
            placement, "RegisterWrite", memory));
        Assert.Throws<ArgumentException>(() => ExecutionContract.Create(binding, provider,
            YAKSys_Hybrid_CPU.Arch.InstructionClass.Memory, YAKSys_Hybrid_CPU.Arch.SerializationClass.Free,
            placement, "RegisterWrite", MemoryCapability.None));
        Assert.Throws<ArgumentException>(() => ExecutionContract.Create(binding, provider,
            YAKSys_Hybrid_CPU.Arch.InstructionClass.ScalarAlu, YAKSys_Hybrid_CPU.Arch.SerializationClass.MemoryOrdered,
            placement, "RegisterWrite", MemoryCapability.None));
        foreach (var effect in new[] { "StoreCommit", "AtomicCommit", "PcWrite", "SecureRegisterWrite" })
            Assert.Throws<ArgumentException>(() => ExecutionContract.Create(binding, provider,
                YAKSys_Hybrid_CPU.Arch.InstructionClass.ScalarAlu, YAKSys_Hybrid_CPU.Arch.SerializationClass.Free,
                placement, effect, MemoryCapability.None));
    }

    [Fact]
    public void ExecutionContract_RejectsUnknownStaleAndForgedBindingsEvenWithMatchingProvider()
    {
        Assert.True(GeneratedStaticBinding.TryFromOpcode(IsaOpcodeValues.ADD, out var binding));
        GeneratedStaticBinding[] invalid = [binding with { Opcode = uint.MaxValue },
            binding with { CatalogSha256 = "stale" }, binding with { CatalogVersion = "future" },
            binding with { DescriptorFingerprint = "forged" },
            binding with { RuntimeExecutionProviderId = new RuntimeExecutionProviderId("caller.provider") }];
        foreach (var forged in invalid)
        {
            var error = Assert.Throws<ArgumentException>(() => Build(forged));
            Assert.Equal("generatedBinding", error.ParamName);
        }
        Assert.Equal(binding, Build(binding).GeneratedBinding);

        static ExecutionContract Build(GeneratedStaticBinding value) => ExecutionContract.Create(
            value, new RuntimeExecutionProviderBinding(value.RuntimeExecutionProviderId, "scalar-v1"),
            YAKSys_Hybrid_CPU.Arch.InstructionClass.ScalarAlu,
            YAKSys_Hybrid_CPU.Arch.SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            "RegisterWrite", MemoryCapability.None);
    }

    [Fact]
    public void PublicProducer_RejectsSplitRawIdentityAndSubstitutedOperands()
    {
        var instruction = new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3) };
        RawSlot slot = RawSlotReader.Read(in instruction, 0);
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(slot.Opcode, out var descriptor));
        Assert.True(OperandDecoder.TryDecode(slot, descriptor, out var operands, out _));
        RawSlot[] mutations = [slot with { SlotIndex = -1 }, slot with { SlotIndex = 8 },
            slot with { Opcode = IsaOpcodeValues.SUB }, slot with { Word0 = slot.Word0 ^ 1 },
            slot with { Word1 = slot.Word1 ^ 1 }, slot with { Word2 = slot.Word2 ^ 1 },
            slot with { Word3 = slot.Word3 ^ 1 }];
        foreach (var changed in mutations)
        {
            Assert.False(CanonicalInstructionIrBuilder.TryBuild(changed, descriptor, operands, null,
                InstructionSlotMetadata.Default, out var canonical, out var failure));
            Assert.Null(canonical);
            Assert.Equal("rawIdentity", failure!.Field);
        }
        DecodedOperandFields[] forged = [operands with { Rd = 7 }, operands with { Rs1 = 7 },
            operands with { Rs2 = 7 }, operands with { Immediate = 123 }];
        foreach (var changed in forged)
        {
            Assert.False(CanonicalInstructionIrBuilder.TryBuild(slot, descriptor, changed, null,
                InstructionSlotMetadata.Default, out var canonical, out var failure));
            Assert.Null(canonical);
            Assert.Equal(DecodeFailureCode.OperandEncoding, failure!.Code);
        }
        Assert.True(CanonicalInstructionIrBuilder.TryBuild(slot, descriptor, operands, null,
            InstructionSlotMetadata.Default, out var accepted, out _));
        Assert.Equal(operands.Rd, accepted.Rd);
    }

    [Fact]
    public void PublicDecode_DerivesKnownOrdinaryAndLeavesEmptySlotsUnknown()
    {
        var raw = new VLIW_Instruction[8];
        raw[0] = new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3) };
        Assert.True(DeclarativeDecoderPipeline.TryDecodeBundle(raw, null, 0x1000, 1,
            out var decoded, out var failure), failure?.Message);
        var instruction = decoded!.CanonicalBundle.GetSlot(0);
        Assert.Equal(CanonicalSecurityOperationKind.Ordinary, instruction.SecurityOperationKind);
        Assert.Equal(CanonicalSecurityOperationKind.Unknown, decoded.CanonicalBundle.GetSlot(1).SecurityOperationKind);
        Assert.Equal(CanonicalSecurityOperationKind.Unknown, default(CanonicalSecurityOperationKind));
        Assert.NotEqual(0, (int)CanonicalSecurityOperationKind.Ordinary);

        Assert.Equal(CanonicalSecurityOperationKind.Unknown,
            (instruction with { Opcode = IsaOpcodeValues.SUB }).SecurityOperationKind);
        Assert.Equal(CanonicalSecurityOperationKind.Unknown,
            (instruction with { StaticBinding = null }).SecurityOperationKind);
        Assert.Equal(CanonicalSecurityOperationKind.Unknown,
            (instruction with { StaticBinding = instruction.StaticBinding! with { CatalogSha256 = "stale" } }).SecurityOperationKind);
    }

    [Fact]
    public void PublicCanonicalProducer_DeniesDefaultForeignAndModifiedDescriptors()
    {
        var instruction = new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD };
        RawSlot slot = RawSlotReader.Read(in instruction, 0);
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(IsaOpcodeValues.ADD, out var add));
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(IsaOpcodeValues.SUB, out var sub));
        var operands = new DecodedOperandFields(0, 0, 0, 0);
        GeneratedIsaDescriptor[] forged = [default, sub,
            add with { ProviderId = "caller.provider" },
            add with { StaticEffectContract = "SecureRegisterWrite" }];
        foreach (var descriptor in forged)
        {
            Assert.False(CanonicalInstructionIrBuilder.TryBuild(slot, descriptor, operands, null,
                InstructionSlotMetadata.Default, out var canonical, out var failure));
            Assert.Null(canonical);
            Assert.Equal(DecodeFailureCode.UnsupportedOpcode, failure!.Code);
            Assert.Equal("securityOperationIdentity", failure.Field);
        }
    }

    [Fact]
    public void LegacyPolicyConversion_IsExplicitAndCannotCreateSecureMeaning()
    {
        Assert.Equal(CanonicalSecurityOperationKind.Ordinary,
            CanonicalSecurityOperationIdentity.FromLegacyPolicyClass(SecureDomainOperationClass.Ordinary));
        foreach (var value in Enum.GetValues<SecureDomainOperationClass>().Where(v => v != SecureDomainOperationClass.Ordinary))
            Assert.Equal(CanonicalSecurityOperationKind.Unknown,
                CanonicalSecurityOperationIdentity.FromLegacyPolicyClass(value));
        Assert.Equal(CanonicalSecurityOperationKind.Unknown,
            CanonicalSecurityOperationIdentity.FromLegacyPolicyClass((SecureDomainOperationClass)255));
        Assert.Equal(CanonicalSecurityOperationKind.Unknown,
            CanonicalSecurityOperationIdentity.FromGeneratedBinding(null));
    }
}
