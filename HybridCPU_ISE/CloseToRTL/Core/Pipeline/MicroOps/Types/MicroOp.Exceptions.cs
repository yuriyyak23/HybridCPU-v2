
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;


namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Exception thrown when a memory access encounters a page fault or MMU error.
    /// Used in speculative FSP to suppress interrupts for stolen memory operations.
    /// </summary>
    public class PageFaultException : Exception
    {
        /// <summary>
        /// Address that caused the page fault
        /// </summary>
        public ulong FaultAddress { get; }

        /// <summary>
        /// Whether the fault occurred during a read or write operation
        /// </summary>
        public bool IsWrite { get; }

        public PageFaultException(ulong address, bool isWrite)
            : base($"Page fault at address 0x{address:X} during {(isWrite ? "write" : "read")}")
        {
            FaultAddress = address;
            IsWrite = isWrite;
        }

        public PageFaultException(string message, ulong address, bool isWrite)
            : base(message)
        {
            FaultAddress = address;
            IsWrite = isWrite;
        }

        public PageFaultException(string message, Exception innerException, ulong address, bool isWrite)
            : base(message, innerException)
        {
            FaultAddress = address;
            IsWrite = isWrite;
        }
    }

    /// <summary>
    /// Precise architectural exception raised when a non-speculative micro-operation
    /// (issued by its owning VT in program order) fails the DomainTag capability check.
    /// Corresponds to FAULT_DOMAIN_VT[i] in the architectural spec (В§5.4).
    /// FSP-injected ops that fail domain checks are silently squashed to NOP instead.
    /// </summary>
    public sealed class DomainFaultException : Exception
    {
        /// <summary>Virtual thread index that caused the fault.</summary>
        public int VirtualThreadId { get; }

        /// <summary>Program counter of the faulting instruction.</summary>
        public ulong FaultingPC { get; }

        /// <summary>DomainTag of the faulting micro-operation.</summary>
        public ulong OperationDomainTag { get; }

        /// <summary>Active domain certificate at fault time (CsrMemDomainCert).</summary>
        public ulong ActiveCert { get; }

        public DomainFaultException(int vtId, ulong pc, ulong opTag, ulong cert)
            : base($"FAULT_DOMAIN_VT[{vtId}]: domain tag 0x{opTag:X} rejected by cert 0x{cert:X} at PC=0x{pc:X}")
        {
            VirtualThreadId = vtId;
            FaultingPC = pc;
            OperationDomainTag = opTag;
            ActiveCert = cert;
        }
    }

    /// <summary>
    /// Abstract base class for micro-operations.
    ///
    /// Purpose: Decouple decoding from execution by introducing an intermediate representation.
    /// Benefits:
    /// - Easier to add new instructions without modifying pipeline logic
    /// - Clear separation of concerns (decode, execute, commit)
    /// - Facilitates instruction scheduling and optimization
    /// - Simplifies testing (can test MicroOps independently)
    ///
    /// Design pattern: Decode в†’ MicroOp в†’ Execute в†’ Commit
}
