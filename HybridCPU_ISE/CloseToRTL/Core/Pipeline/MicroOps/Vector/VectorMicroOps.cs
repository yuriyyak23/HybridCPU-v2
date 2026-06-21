using HybridCPU_ISE.Arch;

using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Execution;
using AddressGen = YAKSys_Hybrid_CPU.Execution.AddressGen;
using PortType = YAKSys_Hybrid_CPU.Execution.PortType;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Abstract base class for vector micro-operations.
    /// Provides common functionality for strip-mining, burst I/O, and async memory tracking.
    ///
    /// Design goals:
    /// - Decouple vector operation logic from monolithic StreamEngine
    /// - Encapsulate strip-mining FSM (chunk processing up to VLMAX)
    /// - Integrate async memory subsystem via MemoryRequestToken
    /// - Maintain same semantics as original StreamEngine
    /// </summary>
    public abstract class VectorMicroOp : MicroOp
    {
        /// <summary>
        /// Original VLIW instruction containing all addressing/type info
        /// </summary>
        public VLIW_Instruction Instruction { get; set; }

        /// <summary>
        /// Execution state for multi-cycle strip-mining
        /// </summary>
        protected enum ExecutionState
        {
            NotStarted,      // Initial state
            LoadingOperands, // Reading operands from memory
            Computing,       // Executing operation in VectorALU
            StoringResults,  // Writing results to memory
            Complete         // Operation finished
        }

        protected ExecutionState _state = ExecutionState.NotStarted;
        protected ulong _elementsProcessed = 0;
        protected ulong _totalElements;

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public VectorMicroOp()
        {
            // FSP Metadata: Vector operations can be stolen
            IsMemoryOp = true;

            // ISA v4 Phase 02: vector ops are ScalarAlu class (extension slot, not mandatory core)
            InstructionClass = Arch.InstructionClass.ScalarAlu;
            SerializationClass = Arch.SerializationClass.Free;
            // Phase 01: Typed-slot taxonomy - vector ops routed through ALU lanes
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        /// <summary>
        /// Initialize FSP metadata for vector operations.
        /// Memory ranges are computed based on addressing mode and stream length.
        /// Phase: Safety Tags & Certificates - Mandatory safety mask computation.
        /// </summary>
        public virtual void InitializeMetadata()
        {
            // Vector operations don't use scalar registers directly
            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();

            // Memory ranges will be determined during execution based on:
            // - Addressing mode (1D, 2D, indexed)
            // - Stream length
            // - Element size
            // - Stride
            // For now, leave empty - will be populated by derived classes or during Execute
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();
            ResourceMask = ResourceBitset.Zero;
            SafetyMask = SafetyMask128.Zero;
        }

        /// <summary>
        /// Re-synchronises the producer-side admission snapshot after a derived vector
        /// micro-op refines memory-range facts beyond the base vector defaults.
        /// </summary>
        protected void RefreshVectorAdmissionMetadata(bool readsMemory, bool writesMemory)
        {
            ResourceMask = ResourceBitset.Zero;

            IReadOnlyList<int> readRegisters = ReadRegisters ?? Array.Empty<int>();
            for (int i = 0; i < readRegisters.Count; i++)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(readRegisters[i]);
            }

            IReadOnlyList<int> writeRegisters = WriteRegisters ?? Array.Empty<int>();
            for (int i = 0; i < writeRegisters.Count; i++)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterWrite(writeRegisters[i]);
            }

            if (readsMemory || writesMemory)
            {
                ResourceMask |= ResourceMaskBuilder.ForStreamEngine(0);
            }

            if (readsMemory)
            {
                ResourceMask |= ResourceMaskBuilder.ForLoad();
            }

            if (writesMemory)
            {
                ResourceMask |= ResourceMaskBuilder.ForStore();
            }

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        /// <summary>
        /// Get element size from data type
        /// </summary>
        protected int GetElementSize()
        {
            if (!DataTypeUtils.IsValid(Instruction.DataTypeValue))
                return 0;
            return DataTypeUtils.SizeOf(Instruction.DataTypeValue);
        }

        /// <summary>
        /// Enforces fail-closed behavior for element-sized mainline vector contours.
        /// Dedicated vector carriers must not silently complete when decode materialized
        /// an unsupported element type onto an otherwise authoritative execution path.
        /// </summary>
        protected void ThrowIfUnsupportedElementDataType(string executionSurface)
        {
            if (GetElementSize() != 0)
                return;

            throw ExecutionFaultContract.CreateUnsupportedVectorElementTypeException(
                $"{executionSurface} rejected unsupported element DataType 0x{Instruction.DataType:X2} on the authoritative mainline vector compute contour instead of hidden success/no-op.");
        }

        /// <summary>
        /// Enforces fail-closed behavior during authoritative vector carrier publication.
        /// Materialization must not publish zero-footprint or empty memory facts for an
        /// unsupported element DataType and defer the failure until a later execute stage.
        /// </summary>
        protected void ThrowIfUnsupportedElementDataTypeForMetadata(string metadataSurface)
        {
            if (Instruction.StreamLength == 0 || GetElementSize() != 0)
                return;

            DecodeProjectionFaultException exception = new(
                ExecutionFaultContract.FormatMessage(
                    ExecutionFaultCategory.UnsupportedVectorElementType,
                    $"{metadataSurface} rejected unsupported element DataType 0x{Instruction.DataType:X2} on the authoritative mainline vector publication contour. " +
                    "Materialized vector carriers must fail closed during metadata publication instead of emitting zero-footprint or empty follow-through facts for an invalid element type."));
            ExecutionFaultContract.Stamp(exception, ExecutionFaultCategory.UnsupportedVectorElementType);
            throw exception;
        }

        /// <summary>
        /// Enforces fail-closed behavior for zero-length mainline vector compute contours.
        /// Dedicated vector carriers must not silently complete when decode materialized
        /// an authoritative vector compute path with StreamLength == 0.
        /// </summary>
        protected void ThrowIfZeroLengthVectorComputeContour(string executionSurface)
        {
            if (Instruction.StreamLength != 0)
                return;

            throw new InvalidOperationException(
                $"{executionSurface} rejected StreamLength == 0 on the authoritative mainline vector compute contour instead of hidden success/no-op.");
        }

        /// <summary>
        /// Get current VL from RVV config (clamped to VLMAX)
        /// </summary>
        protected ulong GetVL(ref Processor.CPU_Core core)
        {
            return Math.Min(core.VectorConfig.VL, Processor.CPU_Core.RVV_Config.VLMAX);
        }

        /// <summary>
        /// Calculate chunk size for current strip-mine iteration
        /// </summary>
        protected ulong GetChunkSize(ulong remaining)
        {
            ulong vlmax = Processor.CPU_Core.RVV_Config.VLMAX;
            return Math.Min(remaining, vlmax);
        }

        public override string GetDescription()
        {
            return $"{GetType().Name}: OpCode={OpCode}, Elements={_totalElements}, Progress={_elementsProcessed}";
        }
    }

    /// <summary>
    /// Vector binary operation micro-operation (two operands > one result)
    /// Examples: VADD, VSUB, VMUL, VDIV, VAND, VOR, VXOR, VMIN, VMAX, shifts
    ///
    /// Execution pipeline:
    /// 1. Load operand A from memory
    /// 2. Load operand B from memory
    /// 3. Compute result via VectorALU
    /// 4. Store result to memory
    /// </summary>
}
