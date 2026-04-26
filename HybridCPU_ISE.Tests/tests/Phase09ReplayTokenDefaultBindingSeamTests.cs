using System;
using Xunit;
using HybridCPU_ISE.Core;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ReplayTokenDefaultBindingSeamTests
{
    [Fact]
    public void ReplayTokenCtor_WhenMainMemoryBindingIsOmitted_ThenRollbackFailsClosed()
    {
        const ulong address = 0x180UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        byte[] rollbackBytes = { 0x10, 0x20, 0x30, 0x40 };

        try
        {
            Processor.MainMemory = replacementMemory;
            var token = new ReplayToken();
            token.PreExecutionMemoryState.Add((address, rollbackBytes));

            var core = new Processor.CPU_Core(0);
            MainMemoryBindingUnavailableException ex = Assert.Throws<MainMemoryBindingUnavailableException>(
                () => token.Rollback(ref core));

            Assert.Equal("ReplayToken", ex.BindingSurface);
            Assert.Contains("ReplayToken.Rollback()", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }

    [Fact]
    public void ReplayTokenFromJson_WhenMainMemoryBindingIsOmitted_ThenRollbackFailsClosed()
    {
        const ulong address = 0x280UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        byte[] rollbackBytes = { 0x55, 0x66, 0x77, 0x88 };

        try
        {
            Processor.MainMemory = replacementMemory;
            var originalToken = new ReplayToken();
            string json = originalToken.ToJson();

            ReplayToken restoredToken = ReplayToken.FromJson(json);
            restoredToken.PreExecutionMemoryState.Add((address, rollbackBytes));

            var core = new Processor.CPU_Core(0);
            MainMemoryBindingUnavailableException ex = Assert.Throws<MainMemoryBindingUnavailableException>(
                () => restoredToken.Rollback(ref core));

            Assert.Equal("ReplayToken", ex.BindingSurface);
            Assert.Contains("ReplayToken.Rollback()", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }
}
