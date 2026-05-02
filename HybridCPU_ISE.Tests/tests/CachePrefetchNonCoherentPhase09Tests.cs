using System;
using System.IO;
using System.Linq;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

[CollectionDefinition("Phase09 Cache NonCoherent", DisableParallelization = true)]
public sealed class Phase09CacheNonCoherentCollection;

[Collection("Phase09 Cache NonCoherent")]
public sealed class CachePrefetchNonCoherentPhase09Tests
{
    [Fact]
    public void Phase09_ExplicitObserverInvalidatesOnlyOverlappingDataCacheLines()
    {
        Processor.CPU_Core core = CreateCoreWithCaches();
        core.L1_Data[0] = DataLine(0x9000, 0x11, domainTag: 0xD0A11);
        core.L1_Data[1] = DataLine(0x9040, 0x22, domainTag: 0xD0A11);
        core.L2_Data[0] = DataLine(0x9000, 0x33, domainTag: 0xD0A11);
        core.L2_Data[1] = DataLine(0x9080, 0x44, domainTag: 0xD0A11);

        var observer = new MemoryCoherencyObserver()
            .RegisterDataCache(core);

        MemoryCoherencyObserverResult result =
            observer.NotifyWrite(
                new MemoryCoherencyWriteNotification(
                    0x9008,
                    8,
                    0xD0A11,
                    MemoryCoherencyWriteSourceKind.DmaStreamComputeCommit));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.DataCacheLinesInvalidated);
        Assert.Equal(0UL, core.L1_Data[0].DataCache_DataLenght);
        Assert.Equal(0x9040UL, core.L1_Data[1].DataCache_MemoryAddress);
        Assert.Equal(0UL, core.L2_Data[0].DataCache_DataLenght);
        Assert.Equal(0x9080UL, core.L2_Data[1].DataCache_MemoryAddress);
    }

    [Fact]
    public void Phase09_DomainFlushBehaviorRemainsDataCacheScoped()
    {
        Processor.CPU_Core core = CreateCoreWithCaches();
        core.L1_Data[0] = DataLine(0x9000, 0x11, domainTag: 0xA);
        core.L1_Data[1] = DataLine(0x9040, 0x22, domainTag: 0xB);
        core.L2_Data[0] = DataLine(0xA000, 0x33, domainTag: 0xA);
        core.L2_Data[1] = DataLine(0xA040, 0x44, domainTag: 0xB);

        core.FlushDomainFromDataCache(0xA);

        Assert.Equal(0UL, core.L1_Data[0].DataCache_DataLenght);
        Assert.Equal(0x9040UL, core.L1_Data[1].DataCache_MemoryAddress);
        Assert.Equal(0UL, core.L2_Data[0].DataCache_DataLenght);
        Assert.Equal(0xA040UL, core.L2_Data[1].DataCache_MemoryAddress);
    }

    [Fact]
    public void Phase09_AssistResidentAndSrfOverlappingWindowsInvalidateThroughObserver()
    {
        Processor.CPU_Core core = CreateCoreWithCaches();
        core.L1_Data[0] = DataLine(
            0x9000,
            0x51,
            domainTag: 0xD0A11,
            assistResident: true);
        var srf = new StreamRegisterFile();
        int register = srf.AllocateRegister(0x9000, elementSize: 1, elementCount: 0x20);
        Assert.True(register >= 0);
        Assert.True(srf.LoadRegister(register, Fill(0x51, 0x20).AsSpan()));

        var observer = new MemoryCoherencyObserver()
            .RegisterDataCache(core)
            .RegisterStreamRegisterFile(srf);

        MemoryCoherencyObserverResult result =
            observer.NotifyWrite(
                new MemoryCoherencyWriteNotification(
                    0x9010,
                    4,
                    0xD0A11,
                    MemoryCoherencyWriteSourceKind.L7AcceleratorCommit));

        Assert.Equal(1, result.AssistResidentLinesInvalidated);
        Assert.Equal(1, result.SrfWindowsInvalidated);
        Assert.Equal(StreamRegisterFile.RegisterState.Invalid, srf.GetRegisterState(register));
        Assert.Equal(0UL, core.L1_Data[0].DataCache_DataLenght);
    }

    [Fact]
    public void Phase09_DataOnlyObserverDoesNotInvalidateVliwFetchState()
    {
        Processor.CPU_Core core = CreateCoreWithCaches();
        core.L1_VLIWBundles[0] = VliwLine(0x2000, 0x61);
        core.L2_VLIWBundles[0] = VliwLine(0x2000, 0x62);

        var observer = new MemoryCoherencyObserver()
            .RegisterDataCache(core);

        MemoryCoherencyObserverResult result =
            observer.NotifyWrite(
                new MemoryCoherencyWriteNotification(
                    0x2000,
                    0x20,
                    0,
                    MemoryCoherencyWriteSourceKind.ModelOrchestration));

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.DataCacheLinesInvalidated);
        Assert.Equal(0x2000UL, core.L1_VLIWBundles[0].VLIWCache_MemoryAddress);
        Assert.Equal(0x2000UL, core.L2_VLIWBundles[0].VLIWCache_MemoryAddress);
    }

    [Fact]
    public void Phase09_CodeBundleWriteRequiresExplicitVliwInvalidationPath()
    {
        Processor.CPU_Core core = CreateCoreWithCaches();
        core.L1_VLIWBundles[0] = VliwLine(0x2000, 0x71);
        core.L2_VLIWBundles[0] = VliwLine(0x2000, 0x72);
        core.TestMarkVliwFetchStateMaterializedForPhase09();

        core.InvalidateVliwFetchState(0x2000);

        Assert.Equal(0UL, core.L1_VLIWBundles[0].VLIWCache_MemoryAddress);
        Assert.Equal(0UL, core.L2_VLIWBundles[0].VLIWCache_MemoryAddress);
    }

    [Fact]
    public void Phase09_FlushBeforeExternalReadIsNoOpProofForReadMaterializedNonDirtyCache()
    {
        Processor.CPU_Core core = CreateCoreWithCaches();
        core.L1_Data[0] = DataLine(0x9000, 0x81, domainTag: 0xD0A11);
        core.L2_Data[0] = DataLine(0x9000, 0x82, domainTag: 0xD0A11);

        DataCacheRangeFlushResult cleanFlush =
            core.FlushDataCacheRange(0x9008, 8, domainTag: 0xD0A11);

        Assert.True(cleanFlush.Succeeded);
        Assert.True(cleanFlush.IsNoOpProof);
        Assert.Equal(0, cleanFlush.DirtyLinesObserved);
        Assert.Equal(2, cleanFlush.CleanLinesObserved);
        Assert.Equal(0x9000UL, core.L1_Data[0].DataCache_MemoryAddress);

        core.L1_Data[0] = DataLine(
            0x9000,
            0x83,
            domainTag: 0xD0A11,
            dirty: true);
        DataCacheRangeFlushResult dirtyFlush =
            core.FlushDataCacheRange(0x9008, 8, domainTag: 0xD0A11);

        Assert.False(dirtyFlush.Succeeded);
        Assert.True(dirtyFlush.RequiresFutureDirtyWriteback);
        Assert.Equal(1, dirtyFlush.L1DirtyLines);
    }

    [Fact]
    public void Phase09_DmaStreamCommitObserverInvalidatesDataCacheAndKeepsExecutionGatesClosed()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            InitializeMainMemory(0x10000);
            WriteMemory(0x9000, Fill(0x91, 16));
            DmaStreamComputeDescriptor descriptor =
                CreateDmaDescriptor(new DmaStreamComputeMemoryRange(0x9000, 16));
            DmaStreamComputeToken token = CreateDmaCommitPendingToken(
                descriptor,
                0x9000,
                Fill(0xA1, 16));
            Processor.CPU_Core core = CreateCoreWithCaches();
            core.L1_Data[0] = DataLine(0x9000, 0x91, descriptor.OwnerBinding.OwnerDomainTag);
            var observer = new MemoryCoherencyObserver()
                .RegisterDataCache(core);

            DmaStreamComputeCommitResult result =
                token.Commit(Processor.MainMemory, descriptor.OwnerGuardDecision, observer);

            Assert.True(result.Succeeded);
            Assert.Equal(DmaStreamComputeTokenState.Committed, token.State);
            Assert.Equal(0UL, core.L1_Data[0].DataCache_DataLenght);
            Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);

            var executeCore = new Processor.CPU_Core(0);
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => new DmaStreamComputeMicroOp(descriptor).Execute(ref executeCore));
            Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void Phase09_L7CommitObserverInvalidatesDataCacheAndCarriersRemainFailClosed()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x9000,
                L7SdcPhase07TestFactory.Fill(0xB1, 0x40));
            var staging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(fixture, staging, 0xB2);
            Processor.CPU_Core core = CreateCoreWithCaches();
            core.L1_Data[0] = DataLine(
                0x9000,
                0xB1,
                fixture.Descriptor.OwnerBinding.DomainTag);
            var observer = new MemoryCoherencyObserver()
                .RegisterDataCache(core);

            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true,
                    coherencyObserver: observer);

            Assert.True(commit.Succeeded, commit.Message);
            Assert.Equal(AcceleratorTokenState.Committed, fixture.Token.State);
            Assert.Equal(0UL, core.L1_Data[0].DataCache_DataLenght);

            var executeCore = new Processor.CPU_Core(0);
            SystemDeviceCommandMicroOp[] carriers =
            {
                new AcceleratorQueryCapsMicroOp(),
                new AcceleratorSubmitMicroOp(),
                new AcceleratorPollMicroOp(),
                new AcceleratorWaitMicroOp(),
                new AcceleratorCancelMicroOp(),
                new AcceleratorFenceMicroOp()
            };

            foreach (SystemDeviceCommandMicroOp carrier in carriers)
            {
                Assert.False(carrier.WritesRegister);
                Assert.Empty(carrier.WriteRegisters);
                Assert.Throws<InvalidOperationException>(() => carrier.Execute(ref executeCore));
            }
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void Phase09_DmaControllerAndPhysicalBurstBackendInvalidateOnlyWhenObserverIsExplicitlyRouted()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            InitializeMainMemory(0x10000);
            WriteMemory(0x1000, Fill(0xC1, 0x10));
            Processor.CPU_Core core = CreateCoreWithCaches();
            core.L1_Data[0] = DataLine(0x9000, 0xC2);
            core.L1_Data[1] = DataLine(0x9040, 0xC3);
            var observer = new MemoryCoherencyObserver()
                .RegisterDataCache(core);
            Processor processor = default;
            var dma = new DMAController(
                ref processor,
                interruptDispatch: static (_, _, _) => { },
                coherencyObserver: observer);

            Assert.True(
                dma.ConfigureTransfer(
                    new DMAController.TransferDescriptor
                    {
                        SourceAddress = 0x1000,
                        DestAddress = 0x9000,
                        TransferSize = 0x10,
                        ElementSize = 1,
                        UseIOMMU = false,
                        ChannelID = 0,
                        Priority = 1
                    }));
            Assert.True(dma.StartTransfer(0));

            dma.ExecuteCycle();

            Assert.Equal(0UL, core.L1_Data[0].DataCache_DataLenght);
            Assert.Equal(0x9040UL, core.L1_Data[1].DataCache_MemoryAddress);

            core.L1_Data[1] = DataLine(0x9040, 0xC4);
            var backend = new PhysicalMainMemoryBurstBackend(Processor.MainMemory, observer);
            Assert.True(backend.Write(0, 0x9040, Fill(0xC5, 8)));

            Assert.Equal(0UL, core.L1_Data[1].DataCache_DataLenght);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void Phase09_AtomicPhysicalWriteNotificationPolicyInvalidatesReservationsNotDataCache()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            InitializeMainMemory(0x10000);
            WriteMemory(0x200, BitConverter.GetBytes(0x11223344));
            var atomicUnit = new MainMemoryAtomicMemoryUnit(Processor.MainMemory);
            Processor.CPU_Core core = CreateCoreWithCaches();
            core.L1_Data[0] = DataLine(0x200, 0xD1);

            Assert.Equal(0x11223344, atomicUnit.LoadReserved32(0x200));
            Assert.True(Processor.MainMemory.TryWritePhysicalRange(
                0x202,
                new byte[] { 0xAA }));

            Assert.Equal(1, atomicUnit.StoreConditional32(0x200, 0x55667788));
            Assert.Equal(0x200UL, core.L1_Data[0].DataCache_MemoryAddress);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void Phase09_CodeAndCompilerSurfacesDoNotClaimCoherentDmaOrAutomaticCoherency()
    {
        string root = L7SdcPhase07TestFactory.ResolveRepoRoot();
        string cacheSource = File.ReadAllText(
            Path.Combine(root, "HybridCPU_ISE", "Core", "Cache", "CPU_Core.Cache.cs"));
        string observerSource = File.ReadAllText(
            Path.Combine(root, "HybridCPU_ISE", "Core", "Memory", "MemoryCoherencyObserver.cs"));
        string compilerSource = File.ReadAllText(
            Path.Combine(root, "HybridCPU_Compiler", "API", "Threading", "HybridCpuThreadCompilerContext.cs"));
        string combined = cacheSource + observerSource + compilerSource;

        Assert.Contains(
            "does not install cache coherency",
            cacheSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "it is not a",
            observerSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "coherent DMA/cache hierarchy",
            observerSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "CoherentDmaEnabled",
            combined,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "AutomaticCoherency",
            combined,
            StringComparison.OrdinalIgnoreCase);

        var dscContext = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        dscContext.CompileDmaStreamCompute(
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor());
        Assert.Equal(1, dscContext.InstructionCount);
        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);

        var l7Context = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        AcceleratorCommandDescriptor l7Descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        l7Context.CompileAcceleratorSubmit(
            IrAcceleratorIntent.ForMatMul(l7Descriptor),
            CompilerAcceleratorCapabilityModel.ReferenceMatMul);
        Assert.Equal(1, l7Context.InstructionCount);
    }

    private static Processor.CPU_Core CreateCoreWithCaches()
    {
        var core = new Processor.CPU_Core(0)
        {
            L1_Data = new Processor.CPU_Core.Cache_Data_Object[8],
            L2_Data = new Processor.CPU_Core.Cache_Data_Object[8],
            L1_VLIWBundles = new Processor.CPU_Core.Cache_VLIWBundle_Object[4],
            L2_VLIWBundles = new Processor.CPU_Core.Cache_VLIWBundle_Object[4]
        };
        return core;
    }

    private static Processor.CPU_Core.Cache_Data_Object DataLine(
        ulong address,
        byte fill,
        ulong domainTag = 0,
        bool assistResident = false,
        bool dirty = false) =>
        new()
        {
            DataCache_MemoryAddress = address,
            DataCache_DataLenght = 0x20,
            DataCache_StoredValue = Fill(fill, 0x20),
            DomainTag = domainTag,
            AssistResident = assistResident,
            DataCache_IsDirty = dirty
        };

    private static Processor.CPU_Core.Cache_VLIWBundle_Object VliwLine(
        ulong address,
        byte fill) =>
        new()
        {
            VLIWCache_MemoryAddress = address,
            VLIWCache_VLIWBundle = Fill(fill, 256)
        };

    private static DmaStreamComputeDescriptor CreateDmaDescriptor(
        params DmaStreamComputeMemoryRange[] writeRanges)
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        DmaStreamComputeMemoryRange[] normalizedWrites =
            writeRanges.Length == 0
                ? descriptor.NormalizedWriteMemoryRanges.ToArray()
                : writeRanges;
        return descriptor with
        {
            WriteMemoryRanges = normalizedWrites,
            NormalizedWriteMemoryRanges = normalizedWrites,
            NormalizedFootprintHash = 0x9909UL
        };
    }

    private static DmaStreamComputeToken CreateDmaCommitPendingToken(
        DmaStreamComputeDescriptor descriptor,
        ulong address,
        byte[] staged)
    {
        var token = new DmaStreamComputeToken(
            descriptor,
            descriptor.DescriptorIdentityHash);
        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(address, staged);
        DmaStreamComputeCommitResult pending = token.MarkComputeComplete();
        Assert.False(pending.Succeeded);
        Assert.False(pending.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, token.State);
        return token;
    }

    private static void StageAndCompleteDefault(
        L7SdcPhase07Fixture fixture,
        AcceleratorStagingBuffer staging,
        byte value)
    {
        Assert.True(fixture.Token.MarkValidated(fixture.Evidence).Succeeded);
        Assert.True(fixture.Token.MarkQueued(fixture.Evidence).Succeeded);
        Assert.True(fixture.Token.MarkRunning(fixture.Evidence).Succeeded);
        AcceleratorStagingResult staged =
            staging.StageWrite(
                fixture.Token,
                new AcceleratorMemoryRange(0x9000, 0x40),
                L7SdcPhase07TestFactory.Fill(value, 0x40),
                fixture.Evidence);
        Assert.True(staged.IsAccepted, staged.Message);
        Assert.True(fixture.Token.MarkDeviceComplete(fixture.Evidence).Succeeded);
    }

    private static void InitializeMainMemory(ulong bytes)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, bytes);
        Processor.Memory = null;
    }

    private static void WriteMemory(ulong address, byte[] bytes) =>
        Assert.True(Processor.MainMemory.TryWritePhysicalRange(address, bytes));

    private static byte[] Fill(byte value, int count)
    {
        byte[] bytes = new byte[count];
        Array.Fill(bytes, value);
        return bytes;
    }
}
