using HybridCPU_ISE;
using HybridCPU_ISE.Legacy;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.PhaseAudit;

public sealed class PhaseAuditLegacyObservationBridgeIsolationTests
{
    [Fact]
    public void LegacyProcessorMachineStateSource_WhenConstructorsAreReflected_HasNoPublicCtors()
    {
        Assert.Empty(typeof(LegacyProcessorMachineStateSource).GetConstructors());
    }

    [Fact]
    public void LegacyObservationFactory_WhenCompatSourceIsRequested_ReportsLegacyGlobalProvenance()
    {
        IseObservationService service = LegacyObservationServiceFactory.CreateLegacyGlobalCompat(new object());

        Assert.Equal(MachineStateSourceProvenance.LegacyGlobal, service.SourceProvenance);
    }

    [Fact]
    public void ObservationTestFactory_WhenSingleCoreServiceIsCreated_ReportsInstanceBoundLiveCoreProvenance()
    {
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(
            new Processor.CPU_Core(0));

        Assert.Equal(MachineStateSourceProvenance.LiveCore, service.SourceProvenance);
    }
}
