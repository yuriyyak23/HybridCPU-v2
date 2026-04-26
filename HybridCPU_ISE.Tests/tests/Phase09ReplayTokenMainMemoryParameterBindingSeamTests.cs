using System;
using HybridCPU_ISE.Core;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// Proves that <see cref="ReplayToken"/> constructed with an explicit
/// <c>mainMemory</c> parameter (D3-L) seeds the bound memory surface from
/// that instance, not from the mutable global <see cref="Processor.MainMemory"/>.
/// </summary>
public sealed class Phase09ReplayTokenMainMemoryParameterBindingSeamTests
{
    /// <summary>
    /// Constructs a ReplayToken with an explicit mainMemory parameter, swaps
    /// the global to a tiny replacement, then verifies CaptureMemoryState
    /// reads from the seeded memory.
    /// </summary>
    [Fact]
    public void Constructor_WhenExplicitMainMemoryProvided_CaptureMemoryStateUsesSeededMemory()
    {
        const ulong address = 0x200UL;
        byte[] baseline = { 0xAA, 0xBB, 0xCC, 0xDD };

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;

        var seededMemory = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        var replacementGlobal = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x10UL);

        try
        {
            // Write baseline data into seeded memory
            Assert.True(seededMemory.TryWritePhysicalRange(address, baseline));

            // Set global to something too small to hold address 0x200
            Processor.MainMemory = replacementGlobal;

            // Construct with explicit seeded memory — bypasses global
            var token = new ReplayToken(mainMemory: seededMemory);

            // CaptureMemoryState should read from seeded memory (address 0x200
            // is within seeded range but beyond replacementGlobal's 0x10 range)
            token.CaptureMemoryState(address, baseline.Length);

            Assert.Single(token.PreExecutionMemoryState);
            Assert.Equal(baseline, token.PreExecutionMemoryState[0].Data);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }

    /// <summary>
    /// Constructs a ReplayToken without an explicit mainMemory parameter
    /// and verifies replay-side memory capture now fails closed until a caller
    /// supplies an explicit memory binding.
    /// </summary>
    [Fact]
    public void Constructor_WhenMainMemoryIsNull_RemainsUnboundUntilExplicitBinding()
    {
        const ulong address = 0x100UL;
        byte[] baseline = { 0x11, 0x22, 0x33, 0x44 };

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        var globalMemory = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);

        try
        {
            Processor.MainMemory = globalMemory;
            Assert.True(globalMemory.TryWritePhysicalRange(address, baseline));

            var token = new ReplayToken();

            MainMemoryBindingUnavailableException ex = Assert.Throws<MainMemoryBindingUnavailableException>(
                () => token.CaptureMemoryState(address, baseline.Length));
            Assert.Equal("ReplayToken", ex.BindingSurface);
            Assert.Contains("ReplayToken.CaptureMemoryState()", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }

    /// <summary>
    /// Deserializes a ReplayToken via <see cref="ReplayToken.FromJson"/> with an
    /// explicit mainMemory parameter, swaps global, verifies Rollback writes to
    /// the seeded memory.
    /// </summary>
    [Fact]
    public void FromJson_WhenExplicitMainMemoryProvided_RollbackUsesSeededMemory()
    {
        const ulong address = 0x300UL;
        byte[] rollbackBytes = { 0x55, 0x66, 0x77, 0x88 };
        byte[] mutatedBytes = { 0xFE, 0xED, 0xFA, 0xCE };

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;

        var seededMemory = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        var replacementGlobal = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x10UL);

        try
        {
            // Serialize a config-only token; memory binding is supplied explicitly on restore.
            Processor.MainMemory = seededMemory;
            var originalToken = new ReplayToken();
            string json = originalToken.ToJson();

            // Now set global to tiny replacement
            Processor.MainMemory = replacementGlobal;

            // Deserialize with explicit seeded memory
            ReplayToken restoredToken = ReplayToken.FromJson(json, mainMemory: seededMemory);
            restoredToken.PreExecutionMemoryState.Add((address, rollbackBytes));

            // Write mutated data so we can verify rollback overwrites it
            Assert.True(seededMemory.TryWritePhysicalRange(address, mutatedBytes));

            var core = new Processor.CPU_Core(0);
            restoredToken.Rollback(ref core);

            byte[] observed = new byte[rollbackBytes.Length];
            Assert.True(seededMemory.TryReadPhysicalRange(address, observed));
            Assert.Equal(rollbackBytes, observed);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }
}
