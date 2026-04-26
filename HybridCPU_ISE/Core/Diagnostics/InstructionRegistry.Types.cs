
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.Metadata;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Context information passed to MicroOp factories during decoding
    /// </summary>
    public struct DecoderContext
    {
        public uint OpCode;
        public ushort Immediate;
        public bool HasImmediate;
        public byte DataType;
        public bool HasDataType;
        public bool IndexedAddressing;
        public bool Is2DAddressing;
        public bool HasVectorAddressingContour;
        public ulong VectorPrimaryPointer;
        public ulong VectorSecondaryPointer;
        public uint VectorStreamLength;
        public ushort VectorStride;
        public ushort VectorRowStride;
        public bool TailAgnostic;
        public bool MaskAgnostic;
        public bool HasVectorPayload;
        public ulong MemoryAddress;
        public bool HasMemoryAddress;
        public ulong PackedRegisterTriplet;
        public bool HasPackedRegisterTriplet;
        public ushort Reg1ID;
        public ushort Reg2ID;
        public ushort Reg3ID;
        public ulong AuxData;
        public ushort PredicateMask;
        public int OwnerThreadId;
        public int MemoryDomainId;
    }

    /// <summary>
    /// Safety mask generation context
    /// </summary>
    public struct SafetyMaskContext
    {
        public IReadOnlyList<int> ReadRegisters;
        public IReadOnlyList<int> WriteRegisters;
        public IReadOnlyList<(ulong Address, ulong Length)> ReadMemoryRanges;
        public IReadOnlyList<(ulong Address, ulong Length)> NormalizedReadMemoryRanges;
        public IReadOnlyList<(ulong Address, ulong Length)> WriteMemoryRanges;
        public AssistCoalescingDescriptor AssistCoalescingDescriptor;
        public int MemoryDomainId;
        public bool IsMemoryOp;
        public bool IsLoad;
        public bool IsStore;
        public bool IsAtomic;
        /// Virtual thread that owns this operation.
        /// Used by <see cref="InstructionRegistry.DefaultSafetyMask128Generator"/> to keep
        /// 128-bit masks aware of per-VT resource ownership.
        /// Blueprint section 2: generate 128-bit masks with per-VT awareness.
        /// </summary>
        public int VirtualThreadId;
    }

    /// <summary>
    /// Resource footprint for accelerator operations (Phase 4)
    /// </summary>
    public struct AcceleratorResourceFootprint
    {
        public int MemoryBandwidthMBps;      // MB/s required
        public int RegisterFileAccesses;     // Number of register reads/writes
        public bool RequiresExclusiveAccess; // Can't share with other ops
    }

    /// <summary>
    /// Interface for HLS-generated custom accelerators (Phase 4).
    /// Allows integration of C# HLS output (e.g., Microsoft KiWi) into HybridCPU ISE.
    ///
    /// Design philosophy:
    /// - Hardware-agnostic: suitable for HLS synthesis
    /// - Deterministic: predictable timing and behavior
    /// - Safe: integrates with FSP scheduler and SafetyVerifier
    /// - Efficient: supports pipelining and data-dependent latency
    /// </summary>
    public interface ICustomAccelerator
    {
        /// Accelerator name/identifier
        /// </summary>
        string Name { get; }

        /// Supported opcodes handled by this accelerator
        /// </summary>
        IReadOnlyList<uint> SupportedOpcodes { get; }

        /// Execute operation with given operands
        /// </summary>
        /// <param name="opcode">Operation code</param>
        /// <param name="operands">Input operands</param>
        /// <param name="config">Accelerator-specific configuration</param>
        /// <returns>Result values</returns>
        ulong[] Execute(uint opcode, ulong[] operands, byte[] config);

        /// Query execution latency for operation (may be data-dependent)
        /// </summary>
        /// <param name="opcode">Operation code</param>
        /// <param name="operands">Input operands (for data-dependent latency)</param>
        /// <returns>Estimated latency in cycles</returns>
        int GetLatency(uint opcode, ulong[] operands);

        /// <summary>
        /// Get resource requirements (for FSP scheduling)
        /// </summary>
        /// <param name="opcode">Operation code</param>
        /// <returns>Resource footprint (memory, registers, etc.)</returns>
        AcceleratorResourceFootprint GetResourceFootprint(uint opcode);

        /// <summary>
        /// Check if operation can be pipelined
        /// </summary>
        bool IsPipelined(uint opcode);

        /// <summary>
        /// Reset accelerator state (for deterministic execution)
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Factory function type for creating MicroOps from decoded instructions
    /// </summary>
    public delegate MicroOp MicroOpFactory(DecoderContext context);

    /// <summary>
    /// Descriptor for MicroOp attributes used by FSP (Fine-Grained Slot Pilfering).
    ///
    /// Purpose: Define FSP-relevant properties of micro-operations at registration time.
    /// Benefits:
    /// - Centralized attribute specification (avoid per-instance assignment)
    /// - Clear documentation of instruction characteristics
    /// - Enables scheduler to make informed packing decisions
    /// - Supports latency-aware and memory-aware scheduling
    ///
    /// Usage:
    ///   var desc = new MicroOpDescriptor {
    ///       Latency = 2,
    ///       MemFootprintClass = 2,  // Vector memory access
    ///   };
    ///   InstructionRegistry.RegisterSemanticFactory(opcode, factory);
    ///   InstructionRegistry.RegisterOpAttributes(opcode, desc);
    /// </summary>
    public class MicroOpDescriptor
    {
        /// <summary>
        /// Execution latency in cycles (minimum).
        /// Default: 1 cycle
        /// Examples: ADD=1, MUL=3, DIV=16, SQRT=8
        /// Used by scheduler for latency-hiding policies.
        /// </summary>
        public int Latency { get; set; } = 1;

        /// <summary>
        /// Memory footprint classification.
        /// 0 = No memory access (compute-only)
        /// 1 = Scalar memory (single load/store)
        /// 2 = Vector memory (multiple elements)
        /// 3 = Stream memory (large burst transactions via StreamEngine)
        /// Used by scheduler to avoid memory bank conflicts.
        /// </summary>
        public int MemFootprintClass { get; set; } = 0;

        // Blueprint sections 2 and 4: semantic flags live in descriptors, not in factories.
        // These overrides let registration keep construction and per-op policy separate.

        /// <summary>
        /// Whether this instruction writes to a destination register.
        /// When non-null, overrides the value set by the factory lambda.
        /// </summary>
        public bool? WritesRegister { get; set; } = null;

        /// <summary>
        /// Whether this instruction performs a memory access (load or store).
        /// When non-null, overrides the value set by the factory lambda.
        /// </summary>
        public bool? IsMemoryOp { get; set; } = null;

        /// <summary>
        /// Create descriptor with default values (compute-only, 1-cycle latency)
        /// </summary>
        public MicroOpDescriptor()
        {
        }

        /// <summary>
        /// Create descriptor with specified values (V6 B11: IsControlFlow removed -
        /// use <see cref="Arch.OpcodeRegistry.IsControlFlowOp"/> for architectural classification).
        /// </summary>
        public MicroOpDescriptor(int latency, int memFootprintClass)
        {
            Latency = latency;
            MemFootprintClass = memFootprintClass;
        }
    }
}
