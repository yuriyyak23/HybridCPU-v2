using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests;

public sealed class PhaseAuditMainMemoryAtomicMemoryUnitBindingTests
{
    [Fact]
    public void ParameterlessCtor_WhenExplicitBindingIsMissing_ThrowsTypedFault()
    {
#pragma warning disable CS0618
        MainMemoryBindingUnavailableException ex = Assert.Throws<MainMemoryBindingUnavailableException>(
            () => new MainMemoryAtomicMemoryUnit());
#pragma warning restore CS0618

        Assert.Equal(nameof(MainMemoryAtomicMemoryUnit), ex.BindingSurface);
        Assert.Contains("parameterless construction", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitCtor_WhenGlobalMainMemoryChangesAfterConstruction_UsesBoundMemory()
    {
        const ulong address = 0x200UL;
        const uint baseline = 0x1122_3344U;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        var seededMemory = new Processor.MainMemoryArea();
        seededMemory.SetLength(0x4000);
        Assert.True(seededMemory.TryWritePhysicalRange(address, BitConverter.GetBytes(baseline)));

        var replacementGlobal = new Processor.MainMemoryArea();
        replacementGlobal.SetLength(0x10);

        try
        {
            var atomicUnit = new MainMemoryAtomicMemoryUnit(seededMemory);
            Processor.MainMemory = replacementGlobal;

            int observed = atomicUnit.LoadReserved32(address);

            Assert.Equal(unchecked((int)baseline), observed);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }
}
