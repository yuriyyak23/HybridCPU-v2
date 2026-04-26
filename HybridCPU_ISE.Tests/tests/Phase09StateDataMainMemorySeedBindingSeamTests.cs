using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// Proves that <see cref="Processor.CPU_Core"/> constructed with an explicit
/// <c>mainMemory</c> parameter (D3-J) seeds both the bound main-memory surface
/// and the <see cref="MainMemoryAtomicMemoryUnit"/> from that instance, not from
/// the mutable global <see cref="Processor.MainMemory"/>.
/// </summary>
public sealed class Phase09StateDataMainMemorySeedBindingSeamTests
{
    /// <summary>
    /// Constructs a CPU_Core with an explicit mainMemory parameter, then swaps
    /// the global to a different (tiny) memory. Verifies that
    /// <see cref="Processor.CPU_Core.GetBoundMainMemoryLength"/> returns the
    /// length of the seeded memory, not the replacement global.
    /// </summary>
    [Fact]
    public void Constructor_WhenExplicitMainMemoryProvided_BindsToSeededMemoryNotGlobal()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;

        var seededMemory = new Processor.MainMemoryArea();
        seededMemory.SetLength(0x8000); // 32 KB

        var replacementGlobal = new Processor.MainMemoryArea();
        replacementGlobal.SetLength(0x100); // 256 bytes — deliberately different

        try
        {
            // Set global to something other than seeded so we can distinguish
            Processor.MainMemory = replacementGlobal;

            var core = new Processor.CPU_Core(0, mainMemory: seededMemory);

            // Bound memory should be the explicitly-passed seeded instance
            Assert.Equal((ulong)0x8000, core.GetBoundMainMemoryLength());
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }

    /// <summary>
    /// Constructs a CPU_Core with an explicit mainMemory parameter, then swaps
    /// the global to a tiny replacement. Verifies that
    /// <see cref="Processor.CPU_Core.AtomicMemoryUnit"/> operates against the
    /// seeded memory (can access an address within seeded range but beyond
    /// replacement range).
    /// </summary>
    [Fact]
    public void Constructor_WhenExplicitMainMemoryProvided_AtomicMemoryUnitUsesSeededMemory()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;

        const ulong testAddress = 0x200;
        const uint initialWord = 0xDEAD_BEEFU;

        var seededMemory = new Processor.MainMemoryArea();
        seededMemory.SetLength(0x8000);

        // Write initial data at test address in seeded memory
        seededMemory.TryWritePhysicalRange(testAddress, BitConverter.GetBytes(initialWord));

        // Replacement global is too small to hold testAddress
        var replacementGlobal = new Processor.MainMemoryArea();
        replacementGlobal.SetLength(0x10);

        try
        {
            Processor.MainMemory = replacementGlobal;

            var core = new Processor.CPU_Core(0, mainMemory: seededMemory);

            // AtomicMemoryUnit should be backed by seededMemory, so LR_W at
            // testAddress (0x200) should succeed — it would fail if backed by
            // replacementGlobal (only 0x10 bytes).
            int loadedValue = core.AtomicMemoryUnit.LoadReserved32(testAddress);

            Assert.Equal(unchecked((int)initialWord), loadedValue);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }

    /// <summary>
    /// Constructs a CPU_Core without an explicit mainMemory parameter (null default).
    /// Verifies it falls back to the current global <see cref="Processor.MainMemory"/>.
    /// </summary>
    [Fact]
    public void Constructor_WhenMainMemoryIsNull_FallsBackToGlobalMainMemory()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;

        var globalMemory = new Processor.MainMemoryArea();
        globalMemory.SetLength(0x6000);

        try
        {
            Processor.MainMemory = globalMemory;

            var core = new Processor.CPU_Core(0); // no explicit mainMemory

            Assert.Equal((ulong)0x6000, core.GetBoundMainMemoryLength());
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }
}
