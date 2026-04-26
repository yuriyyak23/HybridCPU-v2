using System;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Nested Page Walker for VMX virtualization.
    /// Performs two-level translation: GVA → GPA → HPA.
    ///
    /// <para><b>Translation flow:</b></para>
    /// <list type="number">
    ///   <item>Walk guest page table (rooted at GuestCR3, which is a GPA) to obtain GPA.</item>
    ///   <item>Each intermediate GPA in the guest walk must itself be EPT-translated to HPA.</item>
    ///   <item>Walk EPT (rooted at EPTPointer, which is an HPA) to translate final GPA → HPA.</item>
    /// </list>
    ///
    /// <para><b>HLS characteristics:</b></para>
    /// <list type="bullet">
    ///   <item>Fixed-depth walk: 2-level guest + 2-level EPT = 4 memory accesses worst case.</item>
    ///   <item>Deterministic latency: <see cref="NESTED_WALK_LATENCY_CYCLES"/> = 32 cycles.</item>
    ///   <item>Zero heap allocation — stack-only temporaries.</item>
    ///   <item>In HLS: each ReadPhysicalWord maps to AXI4-Lite single-beat read.</item>
    /// </list>
    /// </summary>
    public static class NestedPageWalker
    {
        /// <summary>
        /// Worst-case latency: 2 guest walks + 2 EPT walks × memory latency per step.
        /// </summary>
        public const int NESTED_WALK_LATENCY_CYCLES = 32;

        private const ulong PPN_MASK = 0xFFFFFFFFFFFFF000UL;
        private const ulong PRESENT_BIT = 0x1UL;
        private const ulong READ_BIT = 0x2UL;
        private const ulong WRITE_BIT = 0x4UL;

        /// <summary>
        /// Translate GVA to HPA through two-level nesting.
        /// </summary>
        /// <param name="guestCR3">GPA of guest page table root.</param>
        /// <param name="eptPointer">HPA of EPT root.</param>
        /// <param name="guestVirtualAddress">Guest virtual address to translate.</param>
        /// <param name="hostPhysicalAddress">Resulting host physical address.</param>
        /// <param name="permissions">Intersection of guest and EPT permissions (R/W bits).</param>
        /// <param name="mainMemory">Optional main-memory surface for physical word reads.
        /// When null, falls back to the global <see cref="Processor.MainMemory"/>.</param>
        /// <returns>True if translation succeeded, false on guest page fault or EPT violation.</returns>
        public static bool TranslateNested(
            ulong guestCR3, ulong eptPointer,
            ulong guestVirtualAddress,
            out ulong hostPhysicalAddress,
            out byte permissions,
            Processor.MainMemoryArea? mainMemory = null)
        {
            hostPhysicalAddress = 0;
            permissions = 0;

            Processor.MainMemoryArea resolvedMemory = mainMemory ?? Processor.MainMemory;

            // Step 1: Walk guest page table (GVA → GPA)
            if (!WalkGuestPageTable(guestCR3, eptPointer, guestVirtualAddress,
                                    out ulong guestPhysicalAddress, out byte guestPerms,
                                    resolvedMemory))
            {
                return false;
            }

            // Step 2: Walk EPT (GPA → HPA)
            if (!WalkEPT(eptPointer, guestPhysicalAddress,
                         out hostPhysicalAddress, out byte eptPerms,
                         resolvedMemory))
            {
                return false;
            }

            // Permissions = intersection of guest and EPT
            permissions = (byte)(guestPerms & eptPerms);
            return true;
        }

        /// <summary>
        /// Walk guest page table. Each intermediate GPA is EPT-translated
        /// before reading the guest PTE — this is the "nested" part.
        /// </summary>
        private static bool WalkGuestPageTable(
            ulong guestCR3_GPA, ulong eptPointer,
            ulong gva, out ulong gpa, out byte permissions,
            Processor.MainMemoryArea resolvedMemory)
        {
            gpa = 0;
            permissions = 0;

            // Translate GuestCR3 (GPA) → HPA to access guest page directory
            if (!WalkEPT(eptPointer, guestCR3_GPA, out ulong cr3Hpa, out _, resolvedMemory))
                return false;

            // Guest level 1: page directory entry
            uint guestDirIndex = (uint)((gva >> 22) & 0x3FF);
            ulong pdeAddrHpa = cr3Hpa + guestDirIndex * 8;
            ulong pde = resolvedMemory.ReadPhysicalWord(pdeAddrHpa);
            if ((pde & PRESENT_BIT) == 0)
                return false;

            // Guest level 2: page table entry (PDE contains GPA of page table)
            ulong ptBaseGpa = pde & PPN_MASK;
            if (!WalkEPT(eptPointer, ptBaseGpa, out ulong ptBaseHpa, out _, resolvedMemory))
                return false;

            uint guestTableIndex = (uint)((gva >> 12) & 0x3FF);
            ulong pteAddrHpa = ptBaseHpa + guestTableIndex * 8;
            ulong pte = resolvedMemory.ReadPhysicalWord(pteAddrHpa);
            if ((pte & PRESENT_BIT) == 0)
                return false;

            gpa = (pte & PPN_MASK) | (gva & 0xFFF);
            permissions = (byte)((pte >> 1) & 0x06); // R/W bits
            return true;
        }

        /// <summary>
        /// Walk Extended Page Table: GPA → HPA.
        /// Same 2-level structure as IOMMU page table.
        /// All accesses use HPA (EPTPointer is already an HPA).
        /// </summary>
        private static bool WalkEPT(ulong eptPointer, ulong gpa,
                                    out ulong hpa, out byte permissions,
                                    Processor.MainMemoryArea resolvedMemory)
        {
            hpa = 0;
            permissions = 0;

            // EPT level 1: EPT page directory entry
            uint eptDirIndex = (uint)((gpa >> 22) & 0x3FF);
            ulong epdeAddr = eptPointer + eptDirIndex * 8;
            ulong epde = resolvedMemory.ReadPhysicalWord(epdeAddr);
            if ((epde & PRESENT_BIT) == 0)
                return false;

            // EPT level 2: EPT page table entry
            ulong eptBase = epde & PPN_MASK;
            uint eptTableIndex = (uint)((gpa >> 12) & 0x3FF);
            ulong epteAddr = eptBase + eptTableIndex * 8;
            ulong epte = resolvedMemory.ReadPhysicalWord(epteAddr);
            if ((epte & PRESENT_BIT) == 0)
                return false;

            hpa = (epte & PPN_MASK) | (gpa & 0xFFF);
            permissions = (byte)((epte >> 1) & 0x06);
            return true;
        }
    }
}
