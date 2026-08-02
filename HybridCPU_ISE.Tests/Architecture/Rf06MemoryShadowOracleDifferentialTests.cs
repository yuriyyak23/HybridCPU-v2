using System;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf06MemoryShadowOracleDifferentialTests
{
    [Fact]
    public void LegacyLoadProjection_MatchesImmutableContract()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () =>
            {
                LoadMicroOp carrier = CreateLoad(1, bank: 3);
                MemoryCapability memory = MemoryCapability.Create(
                    MemoryCapabilityKind.Load,
                    new[] { new FrozenMemoryRange(carrier.Address, carrier.Size) },
                    new MemoryBankId(carrier.MemoryBankId));
                AdmissionRecord admission = CreateAdmission(memory, 1);

                Rf06MemoryDifferentialResult result = Rf06MemoryShadowOracle.Compare(
                    admission,
                    carrier,
                    CreateState());

                Assert.True(result.IsEquivalent);
                Assert.True(result.StaticEquivalent);
                Assert.True(result.DynamicEquivalent);
                Assert.True(result.ContractDecision.IsEligible);
            });
    }

    [Fact]
    public void LegacyStoreAndAtomicProjection_PreserveDirectionalShadowContour()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () =>
            {
                StoreMicroOp store = CreateStore(1, bank: 5);
                MemoryCapability storeMemory = MemoryCapability.Create(
                    MemoryCapabilityKind.Store,
                    new[] { new FrozenMemoryRange(store.Address, store.Size) },
                    new MemoryBankId(store.MemoryBankId));
                Rf06MemoryDifferentialResult storeResult = Rf06MemoryShadowOracle.Compare(
                    CreateAdmission(storeMemory, 1), store, CreateState());

                AtomicMicroOp atomic = new()
                {
                    VirtualThreadId = 2,
                    Address = (ulong)(7 * 64 + 8),
                    Size = 8,
                    BaseRegID = 1,
                    SrcRegID = 2,
                    WritesRegister = true,
                    DestRegID = 3,
                };
                atomic.InitializeMetadata();
                MemoryCapability atomicMemory = MemoryCapability.Create(
                    MemoryCapabilityKind.Atomic,
                    new[] { new FrozenMemoryRange(atomic.Address, (ulong)Math.Max(atomic.Size, (byte)4)) },
                    new MemoryBankId(atomic.MemoryBankId));
                Rf06MemoryDifferentialResult atomicResult = Rf06MemoryShadowOracle.Compare(
                    CreateAdmission(atomicMemory, 2), atomic, CreateState());

                Assert.True(storeResult.IsEquivalent);
                Assert.True(atomicResult.IsEquivalent);
            });
    }

    [Theory]
    [InlineData((ushort)0x0008, (byte)0, Rf06MemoryShadowRejectReason.PendingBank)]
    [InlineData((ushort)0, (byte)0, Rf06MemoryShadowRejectReason.None)]
    public void ContractAndLegacyDecisionsRemainEqualAcrossDynamicPressure(
        ushort pendingBankMask,
        byte consumedBank,
        Rf06MemoryShadowRejectReason expected)
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () =>
            {
                LoadMicroOp carrier = CreateLoad(1, bank: 3);
                MemoryCapability memory = MemoryCapability.Create(
                    MemoryCapabilityKind.Load,
                    new[] { new FrozenMemoryRange(carrier.Address, carrier.Size) },
                    new MemoryBankId(carrier.MemoryBankId));
                Rf06MemoryShadowState state = CreateState(
                    pendingBankMask: pendingBankMask,
                    consumedMemoryBank: consumedBank);
                Rf06MemoryDifferentialResult result = Rf06MemoryShadowOracle.Compare(
                    CreateAdmission(memory, 1), carrier, state);

                Assert.True(result.IsEquivalent);
                Assert.Equal(expected, result.ContractDecision.RejectReason);
                Assert.Equal(result.LegacyDecision, result.ContractDecision);
            });
    }

    [Fact]
    public void FourWayFspNomination_UsesSameMemoryDecisionForVtZeroThroughThree()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () =>
            {
                var candidates = new[]
                {
                    CreateFspCandidate(1, 1),
                    CreateFspCandidate(2, 2),
                    CreateFspCandidate(3, 3),
                };

                Rf06MemoryFspDifferentialResult result = Rf06MemoryFspDifferential.Evaluate(
                    candidates,
                    ownerVirtualThreadId: 0,
                    readyVirtualThreadMask: 0b_1111,
                    CreateState());

                Assert.True(result.AreEquivalent);
                Assert.Equal(3, result.LegacyPacked);
                Assert.Equal(3, result.ContractPacked);
                Assert.Empty(result.DivergentVirtualThreads);
            });
    }

    [Fact]
    public void FourWayFspOwnerAndReadyMaskAreNotMemoryDivergences()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () =>
            {
                var candidates = new[]
                {
                    CreateFspCandidate(0, 0),
                    CreateFspCandidate(1, 1),
                    CreateFspCandidate(2, 2),
                    CreateFspCandidate(3, 3),
                };

                Rf06MemoryFspDifferentialResult result = Rf06MemoryFspDifferential.Evaluate(
                    candidates,
                    ownerVirtualThreadId: 0,
                    readyVirtualThreadMask: 0b_1011,
                    CreateState());

                Assert.True(result.AreEquivalent);
                Assert.Equal(2, result.ContractPacked);
                Assert.DoesNotContain(0, result.DivergentVirtualThreads);
            });
    }

    [Fact]
    public void DynamicShadowState_FreezesCallerArrays()
    {
        byte[] budgets = Enumerable.Repeat((byte)2, MemoryBankId.BankCount).ToArray();
        byte[] outstanding = new byte[4];
        Rf06MemoryShadowState state = Rf06MemoryShadowState.Create(
            4,
            2,
            2,
            budgets,
            budgets,
            budgets,
            new byte[MemoryBankId.BankCount],
            new byte[MemoryBankId.BankCount],
            new byte[MemoryBankId.BankCount],
            outstanding,
            new byte[] { 2, 2, 2, 2 });

        budgets[0] = 0;
        outstanding[0] = 2;

        Assert.Equal((byte)2, state.MemoryBankBudgets[0]);
        Assert.Equal((byte)0, state.OutstandingByVirtualThread[0]);
    }

    [Fact]
    public void ContractMismatch_IsReportedBeforeDynamicComparison()
    {
        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () =>
            {
                LoadMicroOp carrier = CreateLoad(1, bank: 3);
                MemoryCapability wrongMemory = MemoryCapability.Create(
                    MemoryCapabilityKind.Load,
                    new[] { new FrozenMemoryRange(carrier.Address, carrier.Size) },
                    new MemoryBankId(4));

                Rf06MemoryDifferentialResult result = Rf06MemoryShadowOracle.Compare(
                    CreateAdmission(wrongMemory, 1), carrier, CreateState());

                Assert.False(result.IsEquivalent);
                Assert.False(result.StaticEquivalent);
                Assert.Equal("MemoryCapability", result.MismatchField);
            });
    }

    [Fact]
    public void DifferentialContourHasNoSchedulerOrExecutionStateCreation()
    {
        DirectoryInfo root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root.FullName,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "Scheduling",
            "Rf06MemoryShadowOracleDifferential.cs"));

        Assert.DoesNotContain("MemorySubsystem", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcome", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationAttempt", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new MicroOp", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionRegistry", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FspStage2_UsesTheSameContractProjectionSeam()
    {
        DirectoryInfo root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root.FullName,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "Scheduling",
            "Fsp",
            "MicroOpScheduler.FSPPipeline.cs"));

        Assert.Contains("AssertRf06MemoryContractProjection", source, StringComparison.Ordinal);
        Assert.Contains("SCHED2", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new MicroOpScheduler", source, StringComparison.Ordinal);
    }

    private static Rf06MemoryFspCandidate CreateFspCandidate(int virtualThreadId, int bank)
    {
        LoadMicroOp carrier = CreateLoad(virtualThreadId, bank);
        MemoryCapability memory = MemoryCapability.Create(
            MemoryCapabilityKind.Load,
            new[] { new FrozenMemoryRange(carrier.Address, carrier.Size) },
            new MemoryBankId(carrier.MemoryBankId));
        return new Rf06MemoryFspCandidate(CreateAdmission(memory, virtualThreadId), carrier);
    }

    private static LoadMicroOp CreateLoad(int virtualThreadId, int bank)
    {
        var load = new LoadMicroOp
        {
            VirtualThreadId = virtualThreadId,
            DestRegID = (ushort)(virtualThreadId + 1),
            WritesRegister = true,
            Address = (ulong)(bank * 64 + 8),
            Size = 8,
        };
        load.InitializeMetadata();
        return load;
    }

    private static StoreMicroOp CreateStore(int virtualThreadId, int bank)
    {
        var store = new StoreMicroOp
        {
            VirtualThreadId = virtualThreadId,
            SrcRegID = (ushort)(virtualThreadId + 1),
            BaseRegID = 2,
            Address = (ulong)(bank * 64 + 8),
            Size = 8,
        };
        store.InitializeMetadata();
        return store;
    }

    private static Rf06MemoryShadowState CreateState(
        ushort pendingBankMask = 0,
        byte consumedMemoryBank = 0)
    {
        byte[] budgets = Enumerable.Repeat((byte)2, MemoryBankId.BankCount).ToArray();
        byte[] consumed = new byte[MemoryBankId.BankCount];
        consumed[3] = consumedMemoryBank;
        return Rf06MemoryShadowState.Create(
            8,
            8,
            8,
            budgets,
            budgets,
            budgets,
            consumed,
            new byte[MemoryBankId.BankCount],
            new byte[MemoryBankId.BankCount],
            new byte[4],
            new byte[] { 8, 8, 8, 8 },
            pendingBankMask);
    }

    private static AdmissionRecord CreateAdmission(MemoryCapability memory, int virtualThreadId)
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
            virtualThreadId,
            bundle.BundleSerial,
            SlotId.Create(canonical.SlotIndex),
            fetchEpoch: 1);
        GeneratedStaticBinding binding = new(
            canonical.Opcode,
            new MaterializerId("rf06.shadow.materializer"),
            new RuntimeExecutionProviderId("rf06.shadow.provider"),
            new LatencyModelId("rf06.shadow.latency"),
            "test",
            "test-catalog",
            "test-descriptor");
        ExecutionContract contract = ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "memory-v1"),
            InstructionClass.Memory,
            SerializationClass.MemoryOrdered,
            ExecutionPlacement.Create(SlotClass.LsuClass, SlotPinningKind.ClassFlexible),
            "MemoryEffect",
            memory,
            readRegisters: new[] { 1 },
            writeRegisters: Array.Empty<int>());
        return AdmissionRecord.Create(provenance, contract, virtualThreadId, 0, 0);
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
