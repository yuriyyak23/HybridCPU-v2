using HybridCPU_ISE;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.PhaseAudit;

public sealed class PhaseAuditObservationSourceFailClosedTests
{
    [Fact]
    public void NullMachineStateSource_WhenDiagnosticsRequested_ThenReportsUnavailableInsteadOfZeroMetrics()
    {
        IIseMachineStateSource source = NullMachineStateSource.Instance;
        Assert.Equal(MachineStateSourceProvenance.Null, source.SourceProvenance);

        AssertUnavailable(
            Assert.Throws<MachineStateSourceUnavailableException>(() => source.GetPerformanceReport()),
            nameof(IIseMachineStateSource.GetPerformanceReport));
        AssertUnavailable(
            Assert.Throws<MachineStateSourceUnavailableException>(() => source.GetReplayPhaseMetrics(0)),
            nameof(IIseMachineStateSource.GetReplayPhaseMetrics));
        AssertUnavailable(
            Assert.Throws<MachineStateSourceUnavailableException>(() => source.GetSchedulerPhaseMetrics(0)),
            nameof(IIseMachineStateSource.GetSchedulerPhaseMetrics));
        AssertUnavailable(
            Assert.Throws<MachineStateSourceUnavailableException>(() => source.GetTypedSlotTelemetryProfile(0, "program")),
            nameof(IIseMachineStateSource.GetTypedSlotTelemetryProfile));
        AssertUnavailable(
            Assert.Throws<MachineStateSourceUnavailableException>(() => source.GetReplayToken()),
            nameof(IIseMachineStateSource.GetReplayToken));
    }

    [Fact]
    public void ExplicitTestMachineStateSource_WhenMetricsAreZero_ThenDoesNotUseNullUnavailableSemantics()
    {
        var core = new Processor.CPU_Core(0);
        var source = new ObservationTestMachineStateSource(cores: new[] { core });

        Assert.NotNull(source.GetPerformanceReport());
        _ = source.GetReplayPhaseMetrics(0);
        _ = source.GetSchedulerPhaseMetrics(0);
        Assert.Null(source.GetTypedSlotTelemetryProfile(0, "program"));
        Assert.Equal(string.Empty, source.GetReplayToken());
    }

    private static void AssertUnavailable(
        MachineStateSourceUnavailableException exception,
        string expectedOperation)
    {
        Assert.Equal("Null", exception.SourceKind);
        Assert.Equal(expectedOperation, exception.Operation);
        Assert.Contains(expectedOperation, exception.Message);
        Assert.Contains("unavailable", exception.Message);
    }
}
