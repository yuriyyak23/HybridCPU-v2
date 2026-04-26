using HybridCPU_ISE.Core;
using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ReplayTokenRollbackBoundaryTests
{
    [Fact]
    public void CanSafelyRollback_WhenOnlyRegisterStateIsCaptured_ReturnsTrue()
    {
        var token = new ReplayToken
        {
            HasSideEffects = true
        };
        token.PreExecutionRegisterState.Add(5, 0x1234UL);

        Assert.True(token.CanSafelyRollback());
    }

    [Fact]
    public void CanSafelyRollback_WhenMemoryStateExistsWithoutBinding_ReturnsFalse()
    {
        var token = new ReplayToken
        {
            HasSideEffects = true
        };
        token.PreExecutionMemoryState.Add((0x180UL, new byte[] { 0x10, 0x20, 0x30, 0x40 }));

        Assert.False(token.CanSafelyRollback());
    }

    [Fact]
    public void CanSafelyRollback_WhenMemoryStateFallsOutsideBoundSurface_ReturnsFalse()
    {
        var token = new ReplayToken(
            mainMemory: new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x10UL))
        {
            HasSideEffects = true
        };
        token.PreExecutionMemoryState.Add((0x180UL, new byte[] { 0x10, 0x20, 0x30, 0x40 }));

        Assert.False(token.CanSafelyRollback());
    }

    [Fact]
    public void CanSafelyRollback_WhenMemoryStateIsFullyBound_ReturnsTrue()
    {
        var token = new ReplayToken(
            mainMemory: new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL))
        {
            HasSideEffects = true
        };
        token.PreExecutionMemoryState.Add((0x180UL, new byte[] { 0x10, 0x20, 0x30, 0x40 }));

        Assert.True(token.CanSafelyRollback());
    }
}
