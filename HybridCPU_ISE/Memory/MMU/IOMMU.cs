using System;
using System.Collections.Generic;
using System.Text;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU
{
    /// <summary>
    /// Access permissions for IOMMU mappings.
    /// </summary>
    [Flags]
    public enum IOMMUAccessPermissions
    {
        None = 0,
        Read = 1,
        Write = 2,
        ReadWrite = Read | Write
    }
}

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Input-Output Memory Management Unit (IOMMU).
    /// Provides address translation and access control for DMA devices.
    ///
    /// Design principles:
    /// - Two-level page table (1024 directories x 1024 pages)
    /// - 4KB page size (matching typical system MMU)
    /// - TLB-style entry format: PPN | Permissions | Present
    /// - HLS-compatible: fixed-size arrays, no Dictionary
    ///
    /// Phase 5 additions:
    /// - SIP-style memory domains for thread isolation
    /// - Domain allocation and enforcement
    /// - Formal isolation proof generation
    ///
    /// Memory model:
    /// - Devices access memory via IO Virtual Addresses (IOVA)
    /// - IOMMU translates IOVA → Physical Address
    /// - Enforces read/write permissions per mapping
    /// - Per-thread domains ensure Singularity-style isolation
    /// </summary>
    public static class IOMMU
    {
        // Two-level page table: 1024 directories x 1024 pages
        private static ulong[][]? PageDirectory;
        private const ulong PAGE_SIZE = 4096;
        private const int PAGE_DIR_ENTRIES = 1024;
        private const int PAGE_TABLE_ENTRIES = 1024;

        // Page table entry bits: [63:12] Physical Page Number | [3:2] Permissions | [0] Present
        private const ulong PTE_PRESENT_BIT = 0x1UL;
        private const ulong PTE_READ_BIT = 0x2UL;    // Corresponds to IOMMUAccessPermissions.Read (1) shifted left 1
        private const ulong PTE_WRITE_BIT = 0x4UL;   // Corresponds to IOMMUAccessPermissions.Write (2) shifted left 1
        private const ulong PTE_PPN_MASK = 0xFFFFFFFFFFFFF000UL;

        // HLS-compatible TLB cache (16-entry CAM, single-cycle hit)
        private static TLB _tlb;

        // Hardware Page Table Walker (Plan 09) — autonomous FSM for TLB miss handling
        private static PageTableWalker _ptw;

        // Phase 5: SIP memory domains for thread isolation
        private static Dictionary<int, MemoryDomain>? _threadDomains;
        private static ulong _nextFreeAddress = 0x10000000UL; // Start allocating from 256MB

        /// <summary>
        /// Phase §2: Null-domain guard. When enabled, burst operations from threads
        /// without an allocated memory domain are rejected even if checkDomain=false.
        /// This prevents uninitialized or null-domain threads from direct (unmasked)
        /// bypass to user-level memory, closing a side-channel vector.
        /// HLS: single AND gate on the burst transaction arbiter.
        /// </summary>
        public static bool NullDomainGuardEnabled { get; set; } = false;

        public static void Initialize()
        {
            PageDirectory = new ulong[PAGE_DIR_ENTRIES][];
            _threadDomains = new Dictionary<int, MemoryDomain>();
            _nextFreeAddress = 0x10000000UL;
            _tlb.FlushAll();
            _ptw.Reset();
        }

        public static void RegisterDevice(ulong deviceID)
        {
            // Device registration placeholder for future per-device page tables
        }

        public static void UnregisterDevice(ulong deviceID)
        {
            // Simplified: in a full implementation, each device would have its own page table
            // For now, clearing is a no-op since we use a shared page table
        }

        /// <summary>
        /// Maps IO virtual addresses to physical addresses using two-level page table.
        /// </summary>
        public static bool Map(ulong deviceID, ulong ioVirtualAddress, ulong physicalAddress, ulong size, IOMMUAccessPermissions permissions)
        {
            if (size == 0) return false;
            if (PageDirectory == null) Initialize();

            ulong numPages = (size + PAGE_SIZE - 1) / PAGE_SIZE;
            ulong permBits = 0;
            if ((permissions & IOMMUAccessPermissions.Read) != 0) permBits |= PTE_READ_BIT;
            if ((permissions & IOMMUAccessPermissions.Write) != 0) permBits |= PTE_WRITE_BIT;

            for (ulong i = 0; i < numPages; i++)
            {
                ulong currentVA = ioVirtualAddress + (i * PAGE_SIZE);
                ulong currentPA = physicalAddress + (i * PAGE_SIZE);

                uint dirIndex = (uint)((currentVA >> 22) & 0x3FF);
                uint tableIndex = (uint)((currentVA >> 12) & 0x3FF);

                if (PageDirectory![dirIndex] == null)
                {
                    PageDirectory[dirIndex] = new ulong[PAGE_TABLE_ENTRIES];
                }

                // Encode PPN + permissions + present bit into 64-bit entry (TLB-style)
                ulong entry = (currentPA & PTE_PPN_MASK) | permBits | PTE_PRESENT_BIT;
                PageDirectory[dirIndex][tableIndex] = entry;
            }

            InvalidateTranslationWarmStateOnMappingMutation();
            return true;
        }

        /// <summary>
        /// Unmaps IO virtual address range from the page table.
        /// </summary>
        public static bool Unmap(ulong deviceID, ulong ioVirtualAddress, ulong size)
        {
            if (size == 0 || PageDirectory == null) return false;

            ulong numPages = (size + PAGE_SIZE - 1) / PAGE_SIZE;
            bool anyUnmapped = false;

            for (ulong i = 0; i < numPages; i++)
            {
                ulong currentVA = ioVirtualAddress + (i * PAGE_SIZE);
                uint dirIndex = (uint)((currentVA >> 22) & 0x3FF);
                uint tableIndex = (uint)((currentVA >> 12) & 0x3FF);

                if (PageDirectory[dirIndex] != null && (PageDirectory[dirIndex][tableIndex] & PTE_PRESENT_BIT) != 0)
                {
                    PageDirectory[dirIndex][tableIndex] = 0; // Clear entry
                    anyUnmapped = true;
                }
            }

            if (anyUnmapped)
            {
                InvalidateTranslationWarmStateOnMappingMutation();
            }

            return anyUnmapped;
        }

        /// <summary>
        /// Translates IOVA to physical address.
        /// Fast path: TLB lookup (1 cycle in HW).
        /// Slow path: 2-level page walk (MISS_PENALTY_CYCLES).
        /// </summary>
        public static bool TranslateAndValidateAccess(ulong deviceID, ulong ioVirtualAddress, ulong accessSize, IOMMUAccessPermissions requestedPermissions, out ulong physicalAddress)
        {
            physicalAddress = 0;
            if (accessSize == 0 || PageDirectory == null) return false;

            // === Fast path: TLB lookup (1 cycle in HW) ===
            if (_tlb.TryTranslate(ioVirtualAddress, (int)deviceID, out physicalAddress, out byte cachedPerms))
            {
                if (!HasRequestedPermissions(cachedPerms, requestedPermissions))
                    return false;

                return true;
            }

            // === Slow path: full 2-level page walk (MISS_PENALTY_CYCLES) ===
            if (!TryResolveMappedPage(ioVirtualAddress, requestedPermissions, out physicalAddress, out byte permBits))
                return false;

            _tlb.Insert(ioVirtualAddress, physicalAddress, permBits, (int)deviceID);

            return true;
        }

        /// <summary>
        /// Warm translation state for an assist/replay-discardable range without
        /// materializing an architectural memory access.
        /// </summary>
        public static bool TryWarmTranslationForAssistRange(
            ulong deviceID,
            ulong ioVirtualAddress,
            ulong accessSize,
            IOMMUAccessPermissions requestedPermissions)
        {
            return TryWarmTranslationForAssistRange(
                unchecked((int)deviceID),
                ioVirtualAddress,
                accessSize,
                requestedPermissions,
                checkDomain: false);
        }

        /// <summary>
        /// Thread-aware translation warm path.
        /// Domain/permission failures stay suppressed on the warm path.
        /// </summary>
        public static bool TryWarmTranslationForAssistRange(
            int threadId,
            ulong ioVirtualAddress,
            ulong accessSize,
            IOMMUAccessPermissions requestedPermissions,
            bool checkDomain)
        {
            if (accessSize == 0)
            {
                return true;
            }

            if (PageDirectory == null)
            {
                return false;
            }

            if (checkDomain)
            {
                var (allowed, _) = CheckDomainAccess(
                    threadId,
                    ioVirtualAddress,
                    accessSize,
                    isWrite: requestedPermissions.HasFlag(IOMMUAccessPermissions.Write));

                if (!allowed)
                {
                    return false;
                }
            }
            else if (NullDomainGuardEnabled &&
                     _threadDomains != null &&
                     !_threadDomains.ContainsKey(threadId))
            {
                return false;
            }

            ulong remaining = accessSize;
            ulong currentVA = ioVirtualAddress;
            while (remaining > 0)
            {
                ulong pageOffset = currentVA & 0xFFF;
                ulong bytesInPage = PAGE_SIZE - pageOffset;
                ulong chunkSize = Math.Min(bytesInPage, remaining);

                if (_tlb.TryTranslate(currentVA, threadId, out _, out byte cachedPerms))
                {
                    if (!HasRequestedPermissions(cachedPerms, requestedPermissions))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!TryResolveMappedPage(currentVA, requestedPermissions, out ulong physicalAddress, out byte permissionBits))
                    {
                        return false;
                    }

                    _tlb.Insert(currentVA, physicalAddress, permissionBits, threadId);
                }

                currentVA += chunkSize;
                remaining -= chunkSize;
            }

            return true;
        }

        /// <summary>
        /// VMX-aware translation overload.
        /// When VMX guest mode is active, performs nested translation (GVA → GPA → HPA)
        /// via the <see cref="NestedPageWalker"/>. Otherwise falls through to standard path.
        /// </summary>
        /// <param name="deviceID">Device or thread ID.</param>
        /// <param name="ioVirtualAddress">Guest virtual address (when VMX active) or IO virtual address.</param>
        /// <param name="accessSize">Access size in bytes.</param>
        /// <param name="requestedPermissions">Required permissions.</param>
        /// <param name="physicalAddress">Resulting host physical address.</param>
        /// <param name="vmxActive">True when in VMX non-root (guest) mode.</param>
        /// <param name="guestCR3">GPA of guest page table root (from VMCS).</param>
        /// <param name="eptPointer">HPA of EPT root (from VMCS).</param>
        /// <returns>True if translation succeeded.</returns>
        public static bool TranslateAndValidateAccess(ulong deviceID, ulong ioVirtualAddress,
            ulong accessSize, IOMMUAccessPermissions requestedPermissions,
            out ulong physicalAddress, bool vmxActive,
            ulong guestCR3, ulong eptPointer)
        {
            if (vmxActive && eptPointer != 0)
            {
                return NestedPageWalker.TranslateNested(
                    guestCR3, eptPointer, ioVirtualAddress,
                    out physicalAddress, out _);
            }

            return TranslateAndValidateAccess(deviceID, ioVirtualAddress, accessSize,
                                              requestedPermissions, out physicalAddress);
        }

        /// <summary>
        /// DMA Read through IOMMU (device reads from system memory).
        /// </summary>
        public static ulong DMARead(ulong deviceID, ulong ioVirtualAddress, byte[] buffer, ulong offset, ulong count)
        {
            Processor.MainMemoryArea mainMemory = CaptureCurrentMainMemory();
            ulong physicalAddress;
            if (TranslateAndValidateAccess(deviceID, ioVirtualAddress, count, IOMMUAccessPermissions.Read, out physicalAddress))
            {
                try
                {
                    return TryReadCapturedMainMemory(
                        mainMemory,
                        physicalAddress,
                        buffer.AsSpan((int)offset, (int)count))
                        ? count
                        : 0;
                }
                catch
                {
                    return 0;
                }
            }
            return 0;
        }

        /// <summary>
        /// DMA Write through IOMMU (device writes to system memory).
        /// </summary>
        public static ulong DMAWrite(ulong deviceID, ulong ioVirtualAddress, byte[] buffer, ulong offset, ulong count)
        {
            Processor.MainMemoryArea mainMemory = CaptureCurrentMainMemory();
            ulong physicalAddress;
            if (TranslateAndValidateAccess(deviceID, ioVirtualAddress, count, IOMMUAccessPermissions.Write, out physicalAddress))
            {
                try
                {
                    return TryWriteCapturedMainMemory(
                        mainMemory,
                        physicalAddress,
                        buffer.AsSpan((int)offset, (int)count))
                        ? count
                        : 0;
                }
                catch
                {
                }
            }
            return 0;
        }

        /// <summary>
        /// Burst Read - HLS-compatible batch memory read for vector operations.
        /// Handles multi-page bursts by splitting at 4KB boundaries.
        ///
        /// Architecture contract:
        /// - Splits burst into page-aligned chunks
        /// - Translates each chunk separately via page table walk
        /// - Ensures no single read operation crosses page boundary
        /// - Returns false if any page translation fails
        /// </summary>
        public static bool ReadBurst(ulong deviceID, ulong ioVirtualAddress, Span<byte> buffer)
        {
            // Phase §2: Null-domain guard — reject if the thread has no allocated domain
            if (NullDomainGuardEnabled && _threadDomains != null && !_threadDomains.ContainsKey((int)deviceID))
            {
                return false;
            }

            // Call thread-aware version with deviceID as threadID (backward compatibility)
            // For CPU operations (deviceID=0), this maps to thread 0
            return ReadBurst((int)deviceID, ioVirtualAddress, buffer, checkDomain: false);
        }

        /// <summary>
        /// Burst Read with thread domain checking (Phase 5).
        /// Thread-aware version that validates memory access against thread's domain.
        ///
        /// Architecture contract:
        /// - Validates entire burst range is within thread's memory domain
        /// - Splits burst into page-aligned chunks
        /// - Translates each chunk separately via page table walk
        /// - Ensures no single read operation crosses page boundary
        /// - Returns false if domain check or page translation fails
        /// </summary>
        /// <param name="threadId">Hardware thread ID (0-15) performing the read</param>
        /// <param name="ioVirtualAddress">Starting virtual address</param>
        /// <param name="buffer">Destination buffer for read data</param>
        /// <param name="checkDomain">Whether to enforce domain boundary checking</param>
        /// <returns>True if read succeeded, false on domain violation or translation failure</returns>
        public static bool ReadBurst(int threadId, ulong ioVirtualAddress, Span<byte> buffer, bool checkDomain = true)
        {
            if (buffer.Length == 0 || PageDirectory == null) return false;
            Processor.MainMemoryArea mainMemory = CaptureCurrentMainMemory();

            // Phase 5: Check memory domain if enabled
            if (checkDomain)
            {
                var (allowed, reason) = CheckDomainAccess(threadId, ioVirtualAddress, (ulong)buffer.Length, isWrite: false);
                if (!allowed)
                {
                    // Domain violation - return false
                    return false;
                }
            }

            int totalBytes = buffer.Length;
            int offset = 0;
            ulong currentVA = ioVirtualAddress;

            while (offset < totalBytes)
            {
                // Calculate bytes remaining in current page
                ulong pageOffset = currentVA & 0xFFF; // Offset within 4KB page
                int bytesInPage = (int)(PAGE_SIZE - pageOffset);
                int chunkSize = Math.Min(bytesInPage, totalBytes - offset);

                // Translate current page
                ulong physicalAddress;
                if (!TranslateAndValidateAccess((ulong)threadId, currentVA, (ulong)chunkSize, IOMMUAccessPermissions.Read, out physicalAddress))
                {
                    return false;
                }

                // Read chunk from physical memory
                if (!TryReadCapturedMainMemory(
                    mainMemory,
                    physicalAddress,
                    buffer.Slice(offset, chunkSize)))
                {
                    return false;
                }

                // Move to next chunk
                offset += chunkSize;
                currentVA += (ulong)chunkSize;
            }

            return true;
        }

        /// <summary>
        /// Burst Write - HLS-compatible batch memory write for vector operations.
        /// Handles multi-page bursts by splitting at 4KB boundaries.
        ///
        /// Architecture contract:
        /// - Splits burst into page-aligned chunks
        /// - Translates each chunk separately via page table walk
        /// - Ensures no single write operation crosses page boundary
        /// - Returns false if any page translation fails
        /// </summary>
        public static bool WriteBurst(ulong deviceID, ulong ioVirtualAddress, ReadOnlySpan<byte> buffer)
        {
            // Phase §2: Null-domain guard — reject if the thread has no allocated domain
            if (NullDomainGuardEnabled && _threadDomains != null && !_threadDomains.ContainsKey((int)deviceID))
            {
                return false;
            }

            // Call thread-aware version with deviceID as threadID (backward compatibility)
            // For CPU operations (deviceID=0), this maps to thread 0
            return WriteBurst((int)deviceID, ioVirtualAddress, buffer, checkDomain: false);
        }

        /// <summary>
        /// Burst Write with thread domain checking (Phase 5).
        /// Thread-aware version that validates memory access against thread's domain.
        ///
        /// Architecture contract:
        /// - Validates entire burst range is within thread's memory domain
        /// - Splits burst into page-aligned chunks
        /// - Translates each chunk separately via page table walk
        /// - Ensures no single write operation crosses page boundary
        /// - Returns false if domain check or page translation fails
        /// </summary>
        /// <param name="threadId">Hardware thread ID (0-15) performing the write</param>
        /// <param name="ioVirtualAddress">Starting virtual address</param>
        /// <param name="buffer">Source buffer with data to write</param>
        /// <param name="checkDomain">Whether to enforce domain boundary checking</param>
        /// <returns>True if write succeeded, false on domain violation or translation failure</returns>
        public static bool WriteBurst(int threadId, ulong ioVirtualAddress, ReadOnlySpan<byte> buffer, bool checkDomain = true)
        {
            if (buffer.Length == 0 || PageDirectory == null) return false;
            Processor.MainMemoryArea mainMemory = CaptureCurrentMainMemory();

            // Phase 5: Check memory domain if enabled
            if (checkDomain)
            {
                var (allowed, reason) = CheckDomainAccess(threadId, ioVirtualAddress, (ulong)buffer.Length, isWrite: true);
                if (!allowed)
                {
                    // Domain violation - return false
                    return false;
                }
            }

            int totalBytes = buffer.Length;
            int offset = 0;
            ulong currentVA = ioVirtualAddress;

            while (offset < totalBytes)
            {
                // Calculate bytes remaining in current page
                ulong pageOffset = currentVA & 0xFFF; // Offset within 4KB page
                int bytesInPage = (int)(PAGE_SIZE - pageOffset);
                int chunkSize = Math.Min(bytesInPage, totalBytes - offset);

                // Translate current page
                ulong physicalAddress;
                if (!TranslateAndValidateAccess((ulong)threadId, currentVA, (ulong)chunkSize, IOMMUAccessPermissions.Write, out physicalAddress))
                {
                    return false;
                }

                // Write chunk to physical memory
                if (!TryWriteCapturedMainMemory(
                    mainMemory,
                    physicalAddress,
                    buffer.Slice(offset, chunkSize)))
                {
                    return false;
                }

                // Move to next chunk
                offset += chunkSize;
                currentVA += (ulong)chunkSize;
            }

            return true;
        }

        private static Processor.MainMemoryArea CaptureCurrentMainMemory() => Processor.MainMemory;

        private static bool TryReadCapturedMainMemory(
            Processor.MainMemoryArea mainMemory,
            ulong physicalAddress,
            Span<byte> buffer)
        {
            return mainMemory.TryReadPhysicalRange(physicalAddress, buffer);
        }

        private static bool TryWriteCapturedMainMemory(
            Processor.MainMemoryArea mainMemory,
            ulong physicalAddress,
            ReadOnlySpan<byte> buffer)
        {
            return mainMemory.TryWritePhysicalRange(physicalAddress, buffer);
        }

        // ========== Hardware PTW Integration (Plan 09) ==========

        /// <summary>
        /// Submit a TLB miss to the hardware PTW for asynchronous walk.
        /// Called internally by <see cref="TranslateAndValidateAccess"/> slow path
        /// or externally when a streaming/burst operation encounters a TLB miss.
        ///
        /// <para>The requesting thread should be stalled (STALL_PTW_WALK state)
        /// until <see cref="AdvancePTW"/> returns a completed walk result.</para>
        /// </summary>
        /// <param name="virtualAddress">Virtual address that missed TLB.</param>
        /// <param name="threadId">Hardware thread ID (0–15).</param>
        /// <param name="domainId">Domain / ASID for page table root selection.</param>
        /// <param name="isWrite">True if original access was a write.</param>
        /// <returns>True if accepted (started or queued), false if PTW queue full.</returns>
        public static bool SubmitPTWalk(ulong virtualAddress, int threadId, int domainId, bool isWrite)
        {
            return _ptw.SubmitWalk(virtualAddress, threadId, domainId, isWrite);
        }

        /// <summary>
        /// Advance the hardware PTW FSM by one cycle and process results.
        /// Called from MemorySubsystem.AdvanceCycles() once per clock tick.
        ///
        /// <para>On successful walk: auto-fills TLB with the translated mapping.
        /// On fault: returns fault indication for the core FSM to handle.</para>
        /// </summary>
        /// <returns>Walk result for this cycle (check <c>Done</c> flag).</returns>
        public static PageTableWalker.WalkResult AdvancePTW()
        {
            var result = _ptw.AdvanceCycle(PageDirectory);

            // On successful walk, auto-fill TLB
            if (result.Done && !result.Faulted)
            {
                byte permBits = result.Permissions;
                _tlb.Insert(result.VirtualAddress, result.PhysicalAddress, permBits, result.DomainId);
            }

            return result;
        }

        /// <summary>True if PTW is currently busy with a walk.</summary>
        public static bool IsPTWBusy => _ptw.IsBusy;

        /// <summary>Thread ID stalled by PTW, or -1 if idle.</summary>
        public static int PTWStalledThreadId => _ptw.StalledThreadId;

        /// <summary>PTW walk statistics: (completed, faulted, totalCycles).</summary>
        public static (ulong Completed, ulong Faulted, ulong TotalCycles) GetPTWStatistics()
        {
            return (_ptw.WalksCompleted, _ptw.WalksFaulted, _ptw.TotalWalkCycles);
        }

        // ========== Phase 5: SIP Memory Domain Management ==========

        /// <summary>
        /// Allocate memory domain for thread (SIP-style) (Phase 5)
        /// </summary>
        public static bool AllocateDomain(int threadId, ulong size, MemoryDomainFlags flags)
        {
            if (_threadDomains == null)
            {
                Initialize();
            }

            // Check if thread already has a domain
            if (_threadDomains!.ContainsKey(threadId))
            {
                return false; // Domain already allocated
            }

            // Find free region
            ulong baseAddr = FindFreeRegion(size);
            if (baseAddr == 0)
                return false;

            // Check no overlap with existing domains
            foreach (var domain in _threadDomains.Values)
            {
                if (domain.Overlaps(baseAddr, size))
                    return false;
            }

            // Create domain
            _threadDomains[threadId] = new MemoryDomain
            {
                ThreadId = threadId,
                BaseAddress = baseAddr,
                Size = size,
                Flags = flags
            };

            return true;
        }

        /// <summary>
        /// Find free memory region of requested size (Phase 5)
        /// </summary>
        private static ulong FindFreeRegion(ulong size)
        {
            // Simple bump allocator
            // In production, would need proper free list management
            ulong candidate = _nextFreeAddress;

            // Align to page boundary
            if (candidate % PAGE_SIZE != 0)
            {
                candidate = (candidate / PAGE_SIZE + 1) * PAGE_SIZE;
            }

            _nextFreeAddress = candidate + size;

            // Check if we've exceeded reasonable address space (1GB for now)
            if (_nextFreeAddress > 0x40000000UL)
            {
                return 0; // Out of address space
            }

            return candidate;
        }

        /// <summary>
        /// Verify memory access respects domain boundaries (Phase 5)
        /// </summary>
        public static (bool Allowed, string Reason) CheckDomainAccess(
            int threadId, ulong address, ulong size, bool isWrite)
        {
            if (_threadDomains == null || !_threadDomains.ContainsKey(threadId))
            {
                return (false, $"Thread {threadId} has no memory domain");
            }

            var domain = _threadDomains[threadId];

            if (!domain.Contains(address, size))
            {
                return (false, $"Access 0x{address:X}+{size} outside domain [0x{domain.BaseAddress:X}+{domain.Size}]");
            }

            if (isWrite && !domain.Flags.HasFlag(MemoryDomainFlags.Write))
            {
                return (false, $"Write access denied to read-only domain");
            }

            if (!isWrite && !domain.Flags.HasFlag(MemoryDomainFlags.Read))
            {
                return (false, $"Read access denied to write-only domain");
            }

            return (true, "");
        }

        /// <summary>
        /// Get memory domain for thread (Phase 5)
        /// </summary>
        public static bool GetDomain(int threadId, out MemoryDomain domain)
        {
            domain = default;

            if (_threadDomains == null || !_threadDomains.ContainsKey(threadId))
            {
                return false;
            }

            domain = _threadDomains[threadId];
            return true;
        }

        /// <summary>
        /// Free memory domain for thread (Phase 5)
        /// </summary>
        public static bool FreeDomain(int threadId)
        {
            if (_threadDomains == null || !_threadDomains.ContainsKey(threadId))
            {
                return false;
            }

            _threadDomains.Remove(threadId);
            InvalidateTranslationWarmStateOnDomainChange(threadId);
            return true;
        }

        /// <summary>
        /// Generate formal proof of domain isolation (Phase 5)
        /// Exports SMT-LIB2 format proof that can be checked by Z3 or other SMT solvers
        /// </summary>
        public static string GenerateIsolationProof()
        {
            if (_threadDomains == null || _threadDomains.Count == 0)
            {
                return "; No domains allocated";
            }

            var sb = new StringBuilder();
            sb.AppendLine("; SMT-LIB2 format proof of memory domain isolation");
            sb.AppendLine("(set-logic QF_BV)");
            sb.AppendLine();

            // Declare domain constants
            sb.AppendLine("; Domain declarations");
            foreach (var domain in _threadDomains.Values)
            {
                sb.AppendLine($"(declare-const domain_{domain.ThreadId}_base (_ BitVec 64))");
                sb.AppendLine($"(declare-const domain_{domain.ThreadId}_size (_ BitVec 64))");
                sb.AppendLine($"(assert (= domain_{domain.ThreadId}_base #x{domain.BaseAddress:X16}))");
                sb.AppendLine($"(assert (= domain_{domain.ThreadId}_size #x{domain.Size:X16}))");
                sb.AppendLine();
            }

            // Assert non-overlap for all pairs
            sb.AppendLine("; Non-overlap assertions");
            var threadIds = new List<int>(_threadDomains.Keys);
            for (int i = 0; i < threadIds.Count; i++)
            {
                for (int j = i + 1; j < threadIds.Count; j++)
                {
                    int tid1 = threadIds[i];
                    int tid2 = threadIds[j];

                    sb.AppendLine($"; Domains {tid1} and {tid2} do not overlap");
                    sb.AppendLine("(assert (or");
                    sb.AppendLine($"  (bvuge domain_{tid1}_base (bvadd domain_{tid2}_base domain_{tid2}_size))");
                    sb.AppendLine($"  (bvuge domain_{tid2}_base (bvadd domain_{tid1}_base domain_{tid1}_size))))");
                    sb.AppendLine();
                }
            }

            // Check satisfiability
            sb.AppendLine("(check-sat)");
            sb.AppendLine("; Expected result: sat (domains are mutually isolated)");

            return sb.ToString();
        }

        /// <summary>
        /// Verify all domains are non-overlapping (Phase 5)
        /// </summary>
        public static bool VerifyDomainIsolation()
        {
            if (_threadDomains == null || _threadDomains.Count == 0)
            {
                return true; // No domains = trivially isolated
            }

            var domains = new List<MemoryDomain>(_threadDomains.Values);

            for (int i = 0; i < domains.Count; i++)
            {
                for (int j = i + 1; j < domains.Count; j++)
                {
                    if (domains[i].Overlaps(domains[j].BaseAddress, domains[j].Size))
                    {
                        return false; // Overlap detected
                    }
                }
            }

            return true; // All domains are isolated
        }

        private static bool HasRequestedPermissions(byte permissionBits, IOMMUAccessPermissions requestedPermissions)
        {
            if ((requestedPermissions & IOMMUAccessPermissions.Read) != 0 && (permissionBits & 0x02) == 0)
                return false;

            if ((requestedPermissions & IOMMUAccessPermissions.Write) != 0 && (permissionBits & 0x04) == 0)
                return false;

            return true;
        }

        private static bool TryResolveMappedPage(
            ulong ioVirtualAddress,
            IOMMUAccessPermissions requestedPermissions,
            out ulong physicalAddress,
            out byte permissionBits)
        {
            physicalAddress = 0;
            permissionBits = 0;

            if (PageDirectory == null)
            {
                return false;
            }

            uint dirIndex = (uint)((ioVirtualAddress >> 22) & 0x3FF);
            uint tableIndex = (uint)((ioVirtualAddress >> 12) & 0x3FF);
            ulong offset = ioVirtualAddress & 0xFFF;

            if (PageDirectory[dirIndex] == null)
            {
                return false;
            }

            ulong entry = PageDirectory[dirIndex][tableIndex];
            if ((entry & PTE_PRESENT_BIT) == 0)
            {
                return false;
            }

            if ((entry & PTE_READ_BIT) != 0) permissionBits |= 0x02;
            if ((entry & PTE_WRITE_BIT) != 0) permissionBits |= 0x04;

            if (!HasRequestedPermissions(permissionBits, requestedPermissions))
            {
                permissionBits = 0;
                return false;
            }

            physicalAddress = (entry & PTE_PPN_MASK) + offset;
            return true;
        }

        private static void InvalidateTranslationWarmStateOnMappingMutation()
        {
            _tlb.FlushAll();
        }

        private static void InvalidateTranslationWarmStateOnDomainChange(int threadId)
        {
            _tlb.FlushDomain(threadId);
        }

        /// <summary>
        /// Get domain statistics (Phase 5)
        /// </summary>
        public static (int TotalDomains, ulong TotalAllocated, ulong LargestDomain) GetDomainStatistics()
        {
            if (_threadDomains == null || _threadDomains.Count == 0)
            {
                return (0, 0, 0);
            }

            int count = _threadDomains.Count;
            ulong total = 0;
            ulong largest = 0;

            foreach (var domain in _threadDomains.Values)
            {
                total += domain.Size;
                if (domain.Size > largest)
                {
                    largest = domain.Size;
                }
            }

            return (count, total, largest);
        }

        /// <summary>
        /// Resize existing memory domain for thread (Phase 5 extension)
        /// This allows dynamic adjustment of domain sizes for different thread workloads.
        ///
        /// Important notes:
        /// - Cannot resize if new size causes overlap with other domains
        /// - Preserves existing flags and base address when shrinking
        /// - May relocate domain if growing requires more space
        /// - All existing mappings remain valid after resize (caller must handle remapping)
        /// </summary>
        /// <param name="threadId">Thread ID of domain to resize</param>
        /// <param name="newSize">New size in bytes (must be page-aligned)</param>
        /// <returns>True if resize succeeded, false if failed (overlap or no domain)</returns>
        public static bool ResizeDomain(int threadId, ulong newSize)
        {
            if (_threadDomains == null || !_threadDomains.ContainsKey(threadId))
            {
                return false; // Domain doesn't exist
            }

            var currentDomain = _threadDomains[threadId];

            // If shrinking, no overlap check needed
            if (newSize <= currentDomain.Size)
            {
                _threadDomains[threadId] = new MemoryDomain
                {
                    ThreadId = threadId,
                    BaseAddress = currentDomain.BaseAddress,
                    Size = newSize,
                    Flags = currentDomain.Flags
                };
                InvalidateTranslationWarmStateOnDomainChange(threadId);
                return true;
            }

            // Growing: check if new size would overlap with other domains
            ulong growthSize = newSize - currentDomain.Size;
            ulong newEnd = currentDomain.BaseAddress + newSize;

            foreach (var kvp in _threadDomains)
            {
                if (kvp.Key == threadId) continue; // Skip self

                var otherDomain = kvp.Value;

                // Check if growing would overlap with this domain
                if (currentDomain.BaseAddress < (otherDomain.BaseAddress + otherDomain.Size) &&
                    newEnd > otherDomain.BaseAddress)
                {
                    // Would overlap - try to relocate instead
                    return RelocateDomain(threadId, newSize);
                }
            }

            // No overlap, can grow in place
            _threadDomains[threadId] = new MemoryDomain
            {
                ThreadId = threadId,
                BaseAddress = currentDomain.BaseAddress,
                Size = newSize,
                Flags = currentDomain.Flags
            };

            InvalidateTranslationWarmStateOnDomainChange(threadId);
            return true;
        }

        /// <summary>
        /// Relocate domain to new address space with new size (Phase 5 extension)
        /// Used internally by ResizeDomain when growing in place isn't possible.
        /// </summary>
        private static bool RelocateDomain(int threadId, ulong newSize)
        {
            if (_threadDomains == null || !_threadDomains.ContainsKey(threadId))
            {
                return false;
            }

            var currentDomain = _threadDomains[threadId];

            // Find new free region for relocated domain
            ulong newBaseAddr = FindFreeRegion(newSize);
            if (newBaseAddr == 0)
                return false;

            // Check no overlap with existing domains (including the one we're relocating)
            foreach (var kvp in _threadDomains)
            {
                if (kvp.Key == threadId) continue; // Skip domain being relocated

                var otherDomain = kvp.Value;
                if (otherDomain.Overlaps(newBaseAddr, newSize))
                    return false;
            }

            // Relocate domain
            _threadDomains[threadId] = new MemoryDomain
            {
                ThreadId = threadId,
                BaseAddress = newBaseAddr,
                Size = newSize,
                Flags = currentDomain.Flags
            };

            InvalidateTranslationWarmStateOnDomainChange(threadId);
            return true;
        }

        /// <summary>
        /// Allocate shared memory region accessible by multiple threads (Phase 5 extension)
        /// Shared regions require explicit synchronization (mutex/locks) by the caller.
        ///
        /// Architecture notes:
        /// - Shared regions are separate from per-thread domains
        /// - All threads accessing shared region must use same flags
        /// - No automatic synchronization - caller must implement locking
        /// - Singularity-style: shared memory is explicit and controlled
        /// </summary>
        /// <param name="sharedRegionId">Unique ID for shared region (user-defined)</param>
        /// <param name="size">Size in bytes</param>
        /// <param name="threadIds">Array of thread IDs that can access this region</param>
        /// <param name="flags">Access flags (must include Shared flag)</param>
        /// <returns>Base address of shared region, or 0 if allocation failed</returns>
        public static ulong AllocateSharedRegion(int sharedRegionId, ulong size, int[] threadIds, MemoryDomainFlags flags)
        {
            if (_threadDomains == null)
            {
                Initialize();
            }

            // Enforce Shared flag
            if (!flags.HasFlag(MemoryDomainFlags.Shared))
            {
                return 0; // Must have Shared flag
            }

            // Find free region
            ulong baseAddr = FindFreeRegion(size);
            if (baseAddr == 0)
                return 0;

            // Check no overlap with existing domains
            foreach (var domain in _threadDomains!.Values)
            {
                if (domain.Overlaps(baseAddr, size))
                    return 0;
            }

            // Store shared region mapping for each thread
            // Note: In full implementation, would use separate _sharedRegions dictionary
            // For now, using negative thread IDs to distinguish shared regions
            int sharedThreadId = -(sharedRegionId + 1); // -1, -2, -3, etc.

            _threadDomains[sharedThreadId] = new MemoryDomain
            {
                ThreadId = sharedThreadId,
                BaseAddress = baseAddr,
                Size = size,
                Flags = flags
            };

            return baseAddr;
        }

        /// <summary>
        /// Check if address is in shared memory region (Phase 5 extension)
        /// </summary>
        public static bool IsSharedMemory(ulong address)
        {
            if (_threadDomains == null) return false;

            foreach (var kvp in _threadDomains)
            {
                // Shared regions have negative thread IDs
                if (kvp.Key < 0)
                {
                    var domain = kvp.Value;
                    if (domain.Contains(address, 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
