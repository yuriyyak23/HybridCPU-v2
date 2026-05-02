using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.MemoryOrdering;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class GlobalMemoryConflictServicePhase05Tests
{
    [Fact]
    public void Phase05_AbsentServicePreservesScalarDscAndL7CurrentBehavior()
    {
        var service = GlobalMemoryConflictService.CreateAbsent();
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        GlobalMemoryFootprint footprint =
            GlobalMemoryFootprint.FromDmaStreamCompute(descriptor, tokenId: 0x505);

        GlobalMemoryConflictObservation absentObservation =
            service.RegisterActive(footprint);

        Assert.Equal(GlobalMemoryConflictServiceMode.Absent, service.Mode);
        Assert.Equal(GlobalMemoryConflictDecisionKind.Accept, absentObservation.DecisionKind);
        Assert.Equal(GlobalMemoryConflictClass.ServiceAbsent, absentObservation.ConflictClass);
        Assert.False(absentObservation.ChangesArchitecturalMemoryResults);
        Assert.Equal(0, service.ActiveFootprintCount);
        Assert.Empty(service.Observations);

        var bus = new FakeMemoryBus();
        var state = new FakeCpuState();
        state.WriteRegister(vtId: 0, regId: 1, value: 0x120);
        state.WriteRegister(vtId: 0, regId: 2, value: 0x1122_3344);
        var memoryUnit = new MemoryUnit(bus);
        ulong effectiveAddress = memoryUnit.Execute(
            new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.SW,
                Class = InstructionClass.Memory,
                SerializationClass = SerializationClass.Free,
                Rd = 0,
                Rs1 = 1,
                Rs2 = 2,
                Imm = 0
            },
            state);
        Assert.Equal(0x120UL, effectiveAddress);
        Assert.Equal(0x1122_3344U, BitConverter.ToUInt32(bus.Read(0x120, 4), 0));

        var core = new Processor.CPU_Core(0);
        var dscCarrier = new DmaStreamComputeMicroOp(descriptor);
        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);
        Assert.Throws<InvalidOperationException>(() => dscCarrier.Execute(ref core));

        foreach (SystemDeviceCommandMicroOp carrier in CreateL7Carriers())
        {
            Assert.False(carrier.WritesRegister);
            Assert.Throws<InvalidOperationException>(() => carrier.Execute(ref core));
        }
    }

    [Fact]
    public void Phase05_PresentPassiveReportsConflictsWithoutChangingMemoryResults()
    {
        var service = GlobalMemoryConflictService.CreatePresentPassive();
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        GlobalMemoryFootprint dscFootprint =
            GlobalMemoryFootprint.FromDmaStreamCompute(descriptor, tokenId: 0x506);
        Assert.Equal(
            GlobalMemoryConflictDecisionKind.Accept,
            service.RegisterActive(dscFootprint).DecisionKind);

        GlobalMemoryFootprint cpuLoad = GlobalMemoryFootprint.CpuScalarLoad(
            descriptor.OwnerBinding.OwnerVirtualThreadId,
            descriptor.OwnerBinding.OwnerContextId,
            descriptor.OwnerBinding.OwnerCoreId,
            descriptor.OwnerBinding.OwnerPodId,
            descriptor.OwnerBinding.OwnerDomainTag,
            address: 0x9008,
            length: 4);

        GlobalMemoryConflictObservation observation = service.ObserveAccess(cpuLoad);

        Assert.Equal(GlobalMemoryConflictServiceMode.PresentPassive, observation.ServiceMode);
        Assert.Equal(GlobalMemoryConflictClass.ReadAfterWriteOverlap, observation.ConflictClass);
        Assert.Equal(GlobalMemoryConflictDecisionKind.Serialize, observation.DecisionKind);
        Assert.True(observation.IsPassiveOnly);
        Assert.True(observation.FutureEnforcingDecisionWouldBlock);
        Assert.False(observation.IsCurrentArchitecturalEffect);
        Assert.False(observation.ChangesArchitecturalMemoryResults);
        Assert.False(observation.CanPublishArchitecturalMemory);

        var bus = new FakeMemoryBus();
        bus.Write(0x9008, BitConverter.GetBytes(0xAABB_CCDDU));
        var state = new FakeCpuState();
        state.WriteRegister(vtId: 0, regId: 1, value: 0x9008);
        var memoryUnit = new MemoryUnit(bus);
        memoryUnit.Execute(
            new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.LWU,
                Class = InstructionClass.Memory,
                SerializationClass = SerializationClass.Free,
                Rd = 3,
                Rs1 = 1,
                Rs2 = 0,
                Imm = 0
            },
            state);

        Assert.Equal(0xAABB_CCDDUL, unchecked((ulong)state.ReadRegister(vtId: 0, regId: 3)));
    }

    [Fact]
    public void Phase05_OverlappingDscL7AndCpuFootprintsClassifyDeterministically()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();

        var dscVsCpu = GlobalMemoryConflictService.CreatePresentPassive();
        dscVsCpu.RegisterActive(
            GlobalMemoryFootprint.FromDmaStreamCompute(descriptor, tokenId: 0x507));
        GlobalMemoryConflictObservation cpuLoadOverlap = dscVsCpu.ObserveAccess(
            GlobalMemoryFootprint.CpuScalarLoad(
                descriptor.OwnerBinding.OwnerVirtualThreadId,
                descriptor.OwnerBinding.OwnerContextId,
                descriptor.OwnerBinding.OwnerCoreId,
                descriptor.OwnerBinding.OwnerPodId,
                descriptor.OwnerBinding.OwnerDomainTag,
                address: 0x9000,
                length: 8));
        Assert.Equal(GlobalMemoryConflictClass.ReadAfterWriteOverlap, cpuLoadOverlap.ConflictClass);

        var cpuVsDsc = GlobalMemoryConflictService.CreatePresentPassive();
        cpuVsDsc.RegisterActive(
            GlobalMemoryFootprint.CpuScalarStore(
                descriptor.OwnerBinding.OwnerVirtualThreadId,
                descriptor.OwnerBinding.OwnerContextId,
                descriptor.OwnerBinding.OwnerCoreId,
                descriptor.OwnerBinding.OwnerPodId,
                descriptor.OwnerBinding.OwnerDomainTag,
                address: 0x1000,
                length: 8));
        GlobalMemoryConflictObservation dscReadOverlap = cpuVsDsc.ObserveAccess(
            GlobalMemoryFootprint.FromDmaStreamCompute(descriptor, tokenId: 0x508));
        Assert.Equal(GlobalMemoryConflictClass.ReadAfterWriteOverlap, dscReadOverlap.ConflictClass);

        AcceleratorCommandDescriptor l7Descriptor = CreateL7Descriptor(
            sourceRanges: new[] { new AcceleratorMemoryRange(0x4000, 0x20) },
            destinationRanges: new[] { new AcceleratorMemoryRange(0x9000, 0x40) });
        var l7VsL7 = GlobalMemoryConflictService.CreatePresentPassive();
        l7VsL7.RegisterActive(
            GlobalMemoryFootprint.FromL7Accelerator(l7Descriptor, tokenId: 0x701));
        GlobalMemoryConflictObservation l7WriteOverlap = l7VsL7.ObserveAccess(
            GlobalMemoryFootprint.FromL7Accelerator(l7Descriptor, tokenId: 0x702));
        Assert.Equal(GlobalMemoryConflictClass.WriteWriteOverlap, l7WriteOverlap.ConflictClass);

        var dscVsL7 = GlobalMemoryConflictService.CreatePresentPassive();
        dscVsL7.RegisterActive(
            GlobalMemoryFootprint.FromDmaStreamCompute(descriptor, tokenId: 0x509));
        GlobalMemoryConflictObservation addressSpaceBoundary = dscVsL7.ObserveAccess(
            GlobalMemoryFootprint.FromL7Accelerator(l7Descriptor, tokenId: 0x703));
        Assert.Equal(
            GlobalMemoryConflictClass.AddressSpaceMismatchRequiresPolicy,
            addressSpaceBoundary.ConflictClass);
    }

    [Fact]
    public void Phase05_NonOverlappingFootprintsDoNotCreateFalseConflicts()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var service = GlobalMemoryConflictService.CreatePresentPassive();
        service.RegisterActive(
            GlobalMemoryFootprint.FromDmaStreamCompute(descriptor, tokenId: 0x50A));

        GlobalMemoryConflictObservation cpuNonOverlap = service.ObserveAccess(
            GlobalMemoryFootprint.CpuScalarStore(
                descriptor.OwnerBinding.OwnerVirtualThreadId,
                descriptor.OwnerBinding.OwnerContextId,
                descriptor.OwnerBinding.OwnerCoreId,
                descriptor.OwnerBinding.OwnerPodId,
                descriptor.OwnerBinding.OwnerDomainTag,
                address: 0xA000,
                length: 0x20));

        Assert.Equal(GlobalMemoryConflictDecisionKind.Accept, cpuNonOverlap.DecisionKind);
        Assert.Equal(GlobalMemoryConflictClass.None, cpuNonOverlap.ConflictClass);
        Assert.False(cpuNonOverlap.IsConflict);

        AcceleratorCommandDescriptor l7Descriptor = CreateL7Descriptor(
            sourceRanges: new[] { new AcceleratorMemoryRange(0xB000, 0x20) },
            destinationRanges: new[] { new AcceleratorMemoryRange(0xC000, 0x40) });
        GlobalMemoryConflictObservation l7NonOverlap = service.ObserveAccess(
            GlobalMemoryFootprint.FromL7Accelerator(l7Descriptor, tokenId: 0x704));

        Assert.Equal(GlobalMemoryConflictDecisionKind.Accept, l7NonOverlap.DecisionKind);
        Assert.Equal(GlobalMemoryConflictClass.None, l7NonOverlap.ConflictClass);
    }

    [Fact]
    public void Phase05_ParticipantIdentityCarriesDomainDeviceTokenAndMappingEpochRequirements()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        DmaStreamComputeActiveTokenEntry entry = AllocateDscEntry(descriptor);
        GlobalMemoryFootprint dscFootprint = GlobalMemoryFootprint.FromDmaStreamCompute(entry);

        Assert.Equal(GlobalMemoryParticipantKind.DmaStreamComputeToken, dscFootprint.ParticipantKind);
        Assert.Equal(entry.Handle.TokenId, dscFootprint.Identity.TokenId);
        Assert.False(dscFootprint.Identity.RequiresTokenIdentityForEnforcingMode);
        Assert.Equal(descriptor.OwnerBinding.OwnerDomainTag, dscFootprint.Identity.MemoryDomainTag);
        Assert.Equal(descriptor.OwnerBinding.DeviceId, dscFootprint.Identity.DeviceId);
        Assert.Equal(descriptor.DescriptorIdentityHash, dscFootprint.Identity.DescriptorIdentityHash);
        Assert.Equal(descriptor.NormalizedFootprintHash, dscFootprint.Identity.NormalizedFootprintHash);
        Assert.False(dscFootprint.Identity.MappingEpochKnown);
        Assert.True(dscFootprint.Identity.RequiresMappingEpochForEnforcingMode);

        AcceleratorCommandDescriptor l7Descriptor = CreateL7Descriptor(
            sourceRanges: new[] { new AcceleratorMemoryRange(0x1000, 0x20) },
            destinationRanges: new[] { new AcceleratorMemoryRange(0x9000, 0x40) },
            mappingEpoch: 0x1234);
        GlobalMemoryFootprint l7Footprint =
            GlobalMemoryFootprint.FromL7Accelerator(
                l7Descriptor,
                tokenId: 0x705,
                submissionSequence: 0x22);

        Assert.Equal(GlobalMemoryParticipantKind.L7Accelerator, l7Footprint.ParticipantKind);
        Assert.Equal(0x705UL, l7Footprint.Identity.TokenId);
        Assert.Equal(0x22UL, l7Footprint.Identity.SubmissionSequence);
        Assert.Equal(l7Descriptor.OwnerBinding.DomainTag, l7Footprint.Identity.MemoryDomainTag);
        Assert.Equal((uint)l7Descriptor.AcceleratorId, l7Footprint.Identity.DeviceId);
        Assert.True(l7Footprint.Identity.MappingEpochKnown);
        Assert.Equal(0x1234UL, l7Footprint.Identity.MappingEpoch);
    }

    [Fact]
    public void Phase05_FenceWaitAndPollRemainFutureGatedAndNeverPublishMemory()
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        byte[] original = DmaStreamComputeTelemetryTests.Fill(0x11, 16);
        DmaStreamComputeTelemetryTests.WriteMemory(0x9000, original);
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var token = new DmaStreamComputeToken(descriptor, tokenId: 0x5060);
        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(0x9000, DmaStreamComputeTelemetryTests.Fill(0xA5, 16));
        Assert.False(token.MarkComputeComplete().RequiresRetireExceptionPublication);

        var service = GlobalMemoryConflictService.CreatePresentPassive();
        GlobalMemoryParticipantIdentity identity = GlobalMemoryParticipantIdentity.FromCpu(
            descriptor.OwnerBinding.OwnerVirtualThreadId,
            descriptor.OwnerBinding.OwnerContextId,
            descriptor.OwnerBinding.OwnerCoreId,
            descriptor.OwnerBinding.OwnerPodId,
            descriptor.OwnerBinding.OwnerDomainTag);

        GlobalMemoryConflictObservation poll = service.ObserveAccess(
            GlobalMemoryFootprint.FutureBoundary(GlobalMemoryOperationKind.Poll, identity));
        GlobalMemoryConflictObservation wait = service.ObserveAccess(
            GlobalMemoryFootprint.FutureBoundary(GlobalMemoryOperationKind.Wait, identity));
        GlobalMemoryConflictObservation fence = service.ObserveAccess(
            GlobalMemoryFootprint.FutureBoundary(GlobalMemoryOperationKind.Fence, identity));

        Assert.Equal(GlobalMemoryConflictDecisionKind.Accept, poll.DecisionKind);
        Assert.Equal(GlobalMemoryConflictDecisionKind.Stall, wait.DecisionKind);
        Assert.Equal(GlobalMemoryConflictDecisionKind.Serialize, fence.DecisionKind);
        Assert.All(
            new[] { poll, wait, fence },
            observation =>
            {
                Assert.Equal(
                    GlobalMemoryConflictClass.FenceWaitPollFutureGated,
                    observation.ConflictClass);
                Assert.False(observation.CanPublishArchitecturalMemory);
                Assert.False(observation.ChangesArchitecturalMemoryResults);
            });
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, token.State);
        Assert.Equal(original, DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));
    }

    [Fact]
    public void Phase05_NoCpuAtomicDmaStreamOrCompilerProductionHookIsMandatory()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] unexpectedConflictServiceHooks =
            CompatFreezeScanner.ScanProductionFilesForPatterns(
                repoRoot,
                new[] { "GlobalMemoryConflictService", "GlobalMemoryFootprint" },
                new[]
                {
                    Path.Combine(
                        "HybridCPU_ISE",
                        "Core",
                        "Execution",
                        "MemoryOrdering",
                        "GlobalMemoryConflictService.cs")
                },
                new[]
                {
                    Path.Combine("HybridCPU_ISE", "Core", "Memory", "MemoryUnit.cs"),
                    Path.Combine("HybridCPU_ISE", "Core", "Memory", "AtomicMemoryUnit.cs"),
                    Path.Combine("HybridCPU_ISE", "Memory", "DMA", "DMAController.cs"),
                    Path.Combine("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamEngine.BurstIO.cs")
                });
        Assert.Empty(unexpectedConflictServiceHooks);

        string compilerText = ReadAllSourceText(Path.Combine(repoRoot, "HybridCPU_Compiler"));
        Assert.DoesNotContain("GlobalMemoryConflictService", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("GlobalMemoryFootprint", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeRuntime.ExecuteToCommitPending", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalAcceleratorConflictManager", compilerText, StringComparison.Ordinal);

        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var core = new Processor.CPU_Core(0);
        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);
        Assert.Throws<InvalidOperationException>(
            () => new DmaStreamComputeMicroOp(descriptor).Execute(ref core));
        foreach (SystemDeviceCommandMicroOp carrier in CreateL7Carriers())
        {
            Assert.False(carrier.WritesRegister);
            Assert.Throws<InvalidOperationException>(() => carrier.Execute(ref core));
        }
    }

    private static DmaStreamComputeActiveTokenEntry AllocateDscEntry(
        DmaStreamComputeDescriptor descriptor)
    {
        var store = new DmaStreamComputeTokenStore();
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        DmaStreamComputeIssueAdmissionResult admission =
            store.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(
                        issuingPc: 0x5000,
                        bundleId: 0x55,
                        issueCycle: 0x33,
                        replayEpoch: 0x7)));
        Assert.True(admission.IsAccepted, admission.Message);
        return admission.Entry!;
    }

    private static AcceleratorCommandDescriptor CreateL7Descriptor(
        IReadOnlyList<AcceleratorMemoryRange> sourceRanges,
        IReadOnlyList<AcceleratorMemoryRange> destinationRanges,
        ulong mappingEpoch = 0)
    {
        byte[] bytes = L7SdcTestDescriptorFactory.BuildDescriptor(
            sourceRanges: sourceRanges,
            destinationRanges: destinationRanges);
        AcceleratorOwnerBinding owner = L7SdcTestDescriptorFactory.ReadOwnerBinding(bytes);
        AcceleratorGuardEvidence evidence = L7SdcTestDescriptorFactory.CreateGuardEvidence(
            owner,
            mappingEpoch: new AcceleratorMappingEpoch(mappingEpoch));
        AcceleratorDescriptorValidationResult validation =
            L7SdcTestDescriptorFactory.ParseWithGuard(bytes, evidence);
        Assert.True(validation.IsValid, validation.Message);
        return validation.RequireDescriptor();
    }

    private static SystemDeviceCommandMicroOp[] CreateL7Carriers() =>
        new SystemDeviceCommandMicroOp[]
        {
            new AcceleratorQueryCapsMicroOp(),
            new AcceleratorSubmitMicroOp(),
            new AcceleratorPollMicroOp(),
            new AcceleratorWaitMicroOp(),
            new AcceleratorCancelMicroOp(),
            new AcceleratorFenceMicroOp()
        };

    private static string ReadAllSourceText(string root)
    {
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file => !CompatFreezeScanner.IsGeneratedPath(file))
                .Select(File.ReadAllText));
    }

    private sealed class FakeMemoryBus : IMemoryBus
    {
        private readonly byte[] _memory = new byte[65536];

        public byte[] Read(ulong address, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(_memory, checked((int)address), result, 0, length);
            return result;
        }

        public void Write(ulong address, byte[] data)
        {
            Array.Copy(data, 0, _memory, checked((int)address), data.Length);
        }
    }

    private sealed class FakeCpuState : ICanonicalCpuState
    {
        private readonly Dictionary<(byte VtId, int Register), long> _registers = new();

        public ulong GetVL() => 0;
        public void SetVL(ulong vl) { }
        public ulong GetVLMAX() => 0;
        public byte GetSEW() => 0;
        public void SetSEW(byte sew) { }
        public byte GetLMUL() => 0;
        public void SetLMUL(byte lmul) { }
        public bool GetTailAgnostic() => false;
        public void SetTailAgnostic(bool agnostic) { }
        public bool GetMaskAgnostic() => false;
        public void SetMaskAgnostic(bool agnostic) { }
        public uint GetExceptionMask() => 0;
        public void SetExceptionMask(uint mask) { }
        public uint GetExceptionPriority() => 0;
        public void SetExceptionPriority(uint priority) { }
        public byte GetRoundingMode() => 0;
        public void SetRoundingMode(byte mode) { }
        public ulong GetOverflowCount() => 0;
        public ulong GetUnderflowCount() => 0;
        public ulong GetDivByZeroCount() => 0;
        public ulong GetInvalidOpCount() => 0;
        public ulong GetInexactCount() => 0;
        public void ClearExceptionCounters() { }
        public bool GetVectorDirty() => false;
        public void SetVectorDirty(bool dirty) { }
        public bool GetVectorEnabled() => false;
        public void SetVectorEnabled(bool enabled) { }

        public long ReadRegister(byte vtId, int regId) =>
            _registers.TryGetValue((vtId, regId), out long value) ? value : 0;

        public void WriteRegister(byte vtId, int regId, ulong value)
        {
            _registers[(vtId, regId)] = unchecked((long)value);
        }

        public ushort GetPredicateMask(ushort maskID) => 0;
        public void SetPredicateMask(ushort maskID, ushort mask) { }
        public ulong ReadPc(byte vtId) => 0;
        public void WritePc(byte vtId, ulong pc) { }
        public ushort GetCoreID() => 0;
        public ulong GetCycleCount() => 0;
        public ulong GetInstructionsRetired() => 0;
        public double GetIPC() => 0;
        public PipelineState GetCurrentPipelineState() => PipelineState.Task;
        public void SetCurrentPipelineState(PipelineState state) { }
        public void TransitionPipelineState(PipelineTransitionTrigger trigger) { }
    }
}
