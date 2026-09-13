using System.Collections.Immutable;
using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.Scheduling;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf06ContractFreezeTests
{
    [Fact]
    public void CanonicalDecode_CarriesOneFrozenGeneratedBinding()
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(3, 1, 2),
        };

        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(raw, 0x4000, 9);
        CanonicalBundle canonical = Assert.IsType<CanonicalBundle>(decoded.CanonicalBundle);
        CanonicalDecodedInstruction slot = canonical.GetSlot(0);

        GeneratedStaticBinding binding = Assert.IsType<GeneratedStaticBinding>(slot.StaticBinding);
        Assert.Equal((uint)IsaOpcodeValues.ADD, binding.Opcode);
        Assert.Equal("legacy.materializer.scalar-alu", binding.MaterializerId.Value);
        Assert.Equal("legacy.provider.scalar-alu", binding.RuntimeExecutionProviderId.Value);
        Assert.Equal("legacy-latency-1", binding.LatencyModelId.Value);
        Assert.Equal(GeneratedIsaCatalog.CatalogSha256, binding.CatalogSha256);
        Assert.Equal(binding, slot.StaticBinding);
    }

    [Fact]
    public void ExecutionContract_FreezesCallerCollectionsAndExcludesLiveState()
    {
        GeneratedStaticBinding binding = CreateBinding();
        var reads = new List<int> { 1 };
        var writes = new List<int> { 3 };
        ExecutionContract contract = CreateContract(binding, reads, writes);
        reads.Add(7);
        writes.Clear();

        Assert.Equal(1, Assert.Single(contract.ReadRegisters));
        Assert.Equal(3, Assert.Single(contract.WriteRegisters));
        Assert.DoesNotContain(typeof(ExecutionContract).GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name is "ReadyState" or "CompletionState" or "ExecutionOutcome" or "ResultValue" or "Fault" or
            "RemainingLatency" or "ResourceToken" or "MshrSlot");
    }

    [Fact]
    public void ExecutionContract_CannotCaptureOperandValuesOrResolvedDynamicAddress()
    {
        Type contract = typeof(ExecutionContract);
        Assert.DoesNotContain(contract.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name is "OperandSnapshot" or "CapturedOperand" or "SourceValue" or
            "ForwardingEvidence" or "ResolvedDynamicAddress" or "ResolvedAddress");

        DirectoryInfo? root = FindRepositoryRoot();
        Assert.NotNull(root);
        string source = File.ReadAllText(Path.Combine(
            root!.FullName,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06ExecutionContracts.cs"));

        Assert.Contains("FreezeRegisters", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalRegisterFile", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterFile.Read", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadArchitecturalRegister", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionContract_RejectsIncoherentProviderAndAssistCombinations()
    {
        GeneratedStaticBinding binding = CreateBinding();
        RuntimeExecutionProviderBinding wrongProvider = new(
            new RuntimeExecutionProviderId("different.provider"),
            "scalar-v1");

        Assert.Throws<ArgumentException>(() => ExecutionContract.Create(
            binding,
            wrongProvider,
            InstructionClass.ScalarAlu,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            "RegisterWrite",
            MemoryCapability.None));

        Assert.Throws<ArgumentException>(() => ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "assist-v1"),
            InstructionClass.ScalarAlu,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            "RegisterWrite",
            MemoryCapability.None,
            isRetireVisible: true,
            isAssist: true));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.HardPinned, pinnedLaneId: 8));
    }

    [Fact]
    public void LegacyProjectionGuard_RejectsSchedulingRelevantMutation()
    {
        LegacyAdmissionProjection original = CreateProjection(isStealable: true);
        AdmissionProjectionFingerprint fingerprint = LegacyProjectionGuard.Capture(original);
        LegacyAdmissionProjection mutated = original with { IsStealable = false };

        Assert.Throws<InvalidOperationException>(() =>
            LegacyProjectionGuard.EnsureCurrent(fingerprint, mutated));
    }

    [Fact]
    public void AdmissionRecord_IsLaneLessAndOperationIdLess()
    {
        AdmissionRecord admission = CreateAdmission(CreateContract(CreateBinding()));
        Assert.DoesNotContain(typeof(AdmissionRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.PropertyType == typeof(VliwOperationId) ||
            property.Name.Contains("Lane", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("OperationId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ScheduledOperation_AllocatesOnlyAfterStageBAndReplayGetsFreshAttempt()
    {
        AdmissionRecord admission = CreateAdmission(CreateContract(CreateBinding()));
        var issuer = new OperationAttemptIssuer();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScheduledOperation.CreateAfterStageB(admission, 10, 0, physicalLane: 8, issuer));

        ScheduledOperation first = ScheduledOperation.CreateAfterStageB(admission, 10, 0, 0, issuer);
        ScheduledOperation replay = ScheduledOperation.CreateAfterStageB(admission, 10, 0, 0, issuer);

        Assert.Equal((ulong)1, first.OperationId.OperationAttempt);
        Assert.Equal((ulong)2, replay.OperationId.OperationAttempt);
        Assert.NotEqual(first.OperationId, replay.OperationId);
        Assert.Same(admission.ExecutionContract.RuntimeProvider, first.RuntimeProvider);
    }

    [Fact]
    public void EqualContractsDoNotRequireConcreteCarrierIdentity()
    {
        ExecutionContract contract = CreateContract(CreateBinding());
        AdmissionRecord first = CreateAdmission(contract, CreateSemanticKey([1]));
        AdmissionRecord substitute = CreateAdmission(contract, CreateSemanticKey([2]));

        Assert.True(first.HasEquivalentAdmissionFacts(substitute));
    }

    [Fact]
    public void CompatibilityProjector_DoesNotCreateRuntimeWorkOrReplayEntries()
    {
        DirectoryInfo? root = FindRepositoryRoot();
        Assert.NotNull(root);
        string source = File.ReadAllText(Path.Combine(
            root!.FullName,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "CanonicalInstructionIrCompatibilityProjector.cs"));

        Assert.DoesNotContain("CreateMicroOp(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AdmissionRecord", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayEntry", source, StringComparison.Ordinal);
    }

    private static DirectoryInfo? FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current;
            }
        }

        return null;
    }

    private static GeneratedStaticBinding CreateBinding()
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor((uint)IsaOpcodeValues.ADD, out GeneratedIsaDescriptor descriptor));
        return GeneratedStaticBinding.FromDescriptor(in descriptor);
    }

    private static ExecutionContract CreateContract(
        GeneratedStaticBinding binding,
        IEnumerable<int>? reads = null,
        IEnumerable<int>? writes = null) =>
        ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "scalar-v1"),
            InstructionClass.ScalarAlu,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            "RegisterWrite",
            MemoryCapability.None,
            reads ?? [1],
            writes ?? [3],
            isStealable: true,
            isRetireVisible: true,
            isAssist: false);

    private static AdmissionRecord CreateAdmission(
        ExecutionContract contract,
        SemanticInstructionKey? semanticKey = null) =>
        AdmissionRecord.Create(
            new SourceOperationProvenance(
                semanticKey ?? CreateSemanticKey([0]),
                sourceVirtualThreadId: 0,
                sourceBundleSerial: 4,
                sourceSlotId: SlotId.Zero,
                fetchEpoch: 1),
            contract,
            virtualThreadId: 0,
            ownerContextId: 2,
            domainTag: 3);

    private static SemanticInstructionKey CreateSemanticKey(byte[] bytes) =>
        SemanticInstructionKey.Create(bytes, "rf06-test", CanonicalDecodeContext.Unbound);

    private static LegacyAdmissionProjection CreateProjection(bool isStealable) =>
        new(
            VirtualThreadId: 0,
            OwnerContextId: 2,
            DomainTag: 3,
            IsStealable: isStealable,
            ReadRegisters: ImmutableArray.Create(1),
            WriteRegisters: ImmutableArray.Create(3),
            Placement: ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            ResourceMask: ResourceBitset.Zero);
}
