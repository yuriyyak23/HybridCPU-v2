using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072tExplicitPacketCompletedTokenFailureTests
{
    [Theory]
    [InlineData(0x540UL, false)]
    [InlineData(0x548UL, true)]
    public void ExplicitPacketCompletedTokenFailure_ProjectsExactMemArchitecturalFault(
        ulong address,
        bool isWrite)
    {
        var failure = new PageFaultException(
            "completed MemorySubsystem request failed",
            address,
            isWrite);

        ExecutionOutcome outcome =
            Processor.CPU_Core.ProjectExplicitPacketCompletedMemoryRequestFailureOutcome(failure);

        Assert.Equal(ExecutionOutcomeKind.ArchitecturalFault, outcome.Kind);
        Assert.Equal(ExecutionDiagnosticCode.PageFault, outcome.Diagnostic!.Code);
        Assert.Equal(address, outcome.Diagnostic.FaultAddress);
        Assert.Equal(isWrite, outcome.Diagnostic.FaultIsWrite);
        Assert.Null(outcome.Result);
        Assert.False(outcome.HasArchitecturalEffects);
    }

    [Fact]
    public void ExplicitPacketCompletedTokenFailure_MissingFaultCarrierFailsClosed()
    {
        Assert.Throws<ArgumentNullException>(
            () => Processor.CPU_Core.ProjectExplicitPacketCompletedMemoryRequestFailureOutcome(
                null!));
    }

    [Fact]
    public void ExplicitPacketMemLoad_WhenAsyncTokenCompletesUnsuccessfully_ThenDeliversTypedFaultWithoutRetire()
    {
        const ulong pc = 0x6A00UL;
        const ulong address = 0x1800UL;
        const ushort destinationRegister = 13;
        const ulong originalDestinationValue = 0xAABB_CCDD_EEFF_0011UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        try
        {
            InitializeFailedAsyncTokenEnvironment();
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc, activeVtId: 0);
            core.WriteCommittedArch(0, destinationRegister, originalDestinationValue);

            PageFaultException fault = Assert.Throws<PageFaultException>(
                () => core.TestPrepareExplicitPacketLoadForWriteBack(
                    laneIndex: 4,
                    pc,
                    address,
                    destinationRegister,
                    accessSize: 8));

            Assert.Equal(address, fault.FaultAddress);
            Assert.False(fault.IsWrite);
            Assert.Contains("failed completed controller request", fault.Message, StringComparison.Ordinal);
            Assert.Equal(originalDestinationValue, core.ReadArch(0, destinationRegister));
            Assert.Equal(0UL, core.GetPipelineControl().InstructionsRetired);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    [Fact]
    public void ExplicitPacketMemStore_WhenAsyncBackendIsUnavailable_ThenRetainsDeferredWriteOwnership()
    {
        const ulong pc = 0x6A08UL;
        const ulong address = 0x1800UL;
        const ulong storedValue = 0x1122_3344_5566_7788UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        try
        {
            InitializeFailedAsyncTokenEnvironment();
            var core = new Processor.CPU_Core(0);

            core.TestPrepareExplicitPacketStoreForWriteBack(
                laneIndex: 5,
                pc,
                address,
                storedValue,
                accessSize: 8);

            // A deferred store queue completion is intentionally successful: its
            // physical write is owned by WB-retire. It therefore cannot be
            // fabricated into the load-only completed-token fault contour.
            Assert.Equal(0UL, core.GetPipelineControl().InstructionsRetired);
            byte[] observed = new byte[8];
            Assert.True(Processor.MainMemory.TryReadPhysicalRange(address, observed));
            Assert.Equal(new byte[8], observed);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    private static void InitializeFailedAsyncTokenEnvironment()
    {
        // The core binds this 0x2000-byte memory before the test lane is built.
        // The IOMMU map deliberately ends at 0x1000, so a 0x1800 request passes
        // MEM's bound-memory range check but completes as a real unsuccessful
        // asynchronous request in MemorySubsystem.
        Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x2000UL);
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x1000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);
        Processor processor = default;
        Processor.Memory = new MemorySubsystem(ref processor);
    }
}
