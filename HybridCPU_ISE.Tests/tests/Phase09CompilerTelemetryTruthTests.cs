using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09CompilerTelemetryTruthTests
{
    [Fact]
    public void GetCompilerStateSnapshot_WhenLiveTelemetryIsUnavailable_ReturnsExplicitUnavailableState()
    {
        IseObservationService service = ObservationServiceTestFactory.CreateEmptyService();
        CompilerStateSnapshot snapshot = service.GetCompilerStateSnapshot();

        Assert.False(snapshot.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.AvailabilityReason));
        Assert.Equal(0, snapshot.TotalInstructionsCompiled);
    }

    [Fact]
    public void GetThreadCompilerState_WhenLiveTelemetryIsUnavailable_ReturnsExplicitUnavailableStateForRequestedThread()
    {
        IseObservationService service = ObservationServiceTestFactory.CreateEmptyService();
        ThreadCompilerStateSnapshot snapshot = service.GetThreadCompilerState(3);

        Assert.False(snapshot.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.AvailabilityReason));
        Assert.Equal(3, snapshot.VirtualThreadId);
    }

    [Fact]
    public void GetDependencyGraph_WhenLiveTelemetryIsUnavailable_ReturnsExplicitUnavailableState()
    {
        IseObservationService service = ObservationServiceTestFactory.CreateEmptyService();
        DependencyGraphSnapshot snapshot = service.GetDependencyGraph();

        Assert.False(snapshot.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.AvailabilityReason));
        Assert.NotNull(snapshot.DependencyMatrix);
        Assert.Equal(4, snapshot.DependencyMatrix.GetLength(0));
        Assert.Equal(4, snapshot.DependencyMatrix.GetLength(1));
    }

    [Fact]
    public void GetBarrierSchedulerState_WhenLiveTelemetryIsUnavailable_ReturnsExplicitUnavailableState()
    {
        IseObservationService service = ObservationServiceTestFactory.CreateEmptyService();
        BarrierSchedulerSnapshot snapshot = service.GetBarrierSchedulerState();

        Assert.False(snapshot.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.AvailabilityReason));
        Assert.Equal(0, snapshot.BarriersInserted);
    }
}
