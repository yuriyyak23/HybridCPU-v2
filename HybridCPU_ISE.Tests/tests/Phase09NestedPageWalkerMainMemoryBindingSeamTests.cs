using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// Proves that <see cref="NestedPageWalker.TranslateNested"/> reads physical words
/// through the explicitly-passed <c>mainMemory</c> parameter (D3-H binding seam)
/// instead of the mutable global <see cref="Processor.MainMemory"/>.
///
/// Strategy: seed a <see cref="Processor.MainMemoryArea"/> with valid 2-level EPT and
/// guest page table structures, swap the global to an empty memory after setup, then
/// call <c>TranslateNested</c> with the seeded memory as the explicit parameter and
/// verify that translation succeeds using the seeded memory.
/// </summary>
public sealed class Phase09NestedPageWalkerMainMemoryBindingSeamTests
{
    private const ulong PPN_MASK = 0xFFFFFFFFFFFFF000UL;
    private const ulong PRESENT_BIT = 0x1UL;
    private const ulong READ_BIT = 0x2UL;
    private const ulong WRITE_BIT = 0x4UL;

    /// <summary>
    /// Write a 64-bit word at a physical address in the given memory area.
    /// </summary>
    private static void WritePhysicalWord(Processor.MainMemoryArea memory, ulong address, ulong value)
    {
        byte[] buf = BitConverter.GetBytes(value);
        memory.TryWritePhysicalRange(address, buf);
    }

    /// <summary>
    /// Build a minimal 2-level EPT + 2-level guest page table structure in memory.
    ///
    /// Layout (all addresses are HPAs, EPT identity-maps the needed GPA pages):
    ///   EPT root            @ 0x0000  (eptPointer)
    ///   EPT page table      @ 0x1000
    ///   Guest page directory @ 0x2000  (guest CR3 GPA = 0x2000, identity-mapped)
    ///   Guest page table    @ 0x3000  (GPA = 0x3000, identity-mapped)
    ///   Final data page     GPA 0x4000 → HPA 0x5000  (non-identity to prove EPT translation)
    ///
    /// GVA 0x00000ABC should translate to HPA 0x5ABC with R/W permissions.
    /// </summary>
    private static Processor.MainMemoryArea BuildSeededMemoryWithPageTables()
    {
        var memory = new Processor.MainMemoryArea();
        memory.SetLength(0x10000); // 64 KB — enough for all structures

        // EPT page directory entry [0]: points to EPT page table at HPA 0x1000
        WritePhysicalWord(memory, 0x0000, 0x1000UL | PRESENT_BIT);

        // EPT page table entries (identity-map GPA pages 2, 3; remap page 4 → HPA 0x5000)
        WritePhysicalWord(memory, 0x1000 + 2 * 8, 0x2000UL | PRESENT_BIT | READ_BIT | WRITE_BIT); // GPA 0x2xxx → HPA 0x2xxx
        WritePhysicalWord(memory, 0x1000 + 3 * 8, 0x3000UL | PRESENT_BIT | READ_BIT | WRITE_BIT); // GPA 0x3xxx → HPA 0x3xxx
        WritePhysicalWord(memory, 0x1000 + 4 * 8, 0x5000UL | PRESENT_BIT | READ_BIT | WRITE_BIT); // GPA 0x4xxx → HPA 0x5xxx

        // Guest page directory entry [0]: guest page table base GPA = 0x3000
        WritePhysicalWord(memory, 0x2000, 0x3000UL | PRESENT_BIT | READ_BIT | WRITE_BIT);

        // Guest page table entry [0]: final data page GPA = 0x4000
        WritePhysicalWord(memory, 0x3000, 0x4000UL | PRESENT_BIT | READ_BIT | WRITE_BIT);

        return memory;
    }

    /// <summary>
    /// Proves that TranslateNested reads physical words through the explicitly-passed
    /// mainMemory parameter, not the mutable global Processor.MainMemory.
    ///
    /// Strategy: seed memory with valid page tables, swap global to empty memory,
    /// call TranslateNested with the seeded memory as explicit parameter, verify success.
    /// </summary>
    [Fact]
    public void TranslateNested_WhenGlobalIsSwappedAfterSetup_UsesExplicitMainMemory()
    {
        const ulong eptPointer = 0x0000UL;
        const ulong guestCR3 = 0x2000UL;
        const ulong gva = 0x00000ABCUL;
        const ulong expectedHpa = 0x5ABCUL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;

        Processor.MainMemoryArea seededMemory = BuildSeededMemoryWithPageTables();
        var emptyMemory = new Processor.MainMemoryArea();
        emptyMemory.SetLength(0x10000);

        try
        {
            // Swap global to empty memory — no valid page tables there
            Processor.MainMemory = emptyMemory;

            bool result = NestedPageWalker.TranslateNested(
                guestCR3, eptPointer, gva,
                out ulong hostPhysicalAddress,
                out byte permissions,
                mainMemory: seededMemory);

            Assert.True(result,
                "TranslateNested should succeed when the explicit mainMemory parameter contains " +
                "valid page tables, even though the global Processor.MainMemory is empty.");
            Assert.Equal(expectedHpa, hostPhysicalAddress);
            Assert.NotEqual((byte)0, permissions);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }

    /// <summary>
    /// Proves that TranslateNested with null mainMemory (default) falls back to the
    /// global Processor.MainMemory and successfully translates when the global has
    /// valid page tables.
    /// </summary>
    [Fact]
    public void TranslateNested_WhenMainMemoryIsNull_FallsBackToGlobalProcessorMainMemory()
    {
        const ulong eptPointer = 0x0000UL;
        const ulong guestCR3 = 0x2000UL;
        const ulong gva = 0x00000ABCUL;
        const ulong expectedHpa = 0x5ABCUL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;

        Processor.MainMemoryArea seededMemory = BuildSeededMemoryWithPageTables();

        try
        {
            // Set global to the seeded memory with valid page tables
            Processor.MainMemory = seededMemory;

            bool result = NestedPageWalker.TranslateNested(
                guestCR3, eptPointer, gva,
                out ulong hostPhysicalAddress,
                out byte permissions);

            Assert.True(result,
                "TranslateNested should succeed with null mainMemory (default) when the global " +
                "Processor.MainMemory contains valid page tables.");
            Assert.Equal(expectedHpa, hostPhysicalAddress);
            Assert.NotEqual((byte)0, permissions);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }
}
