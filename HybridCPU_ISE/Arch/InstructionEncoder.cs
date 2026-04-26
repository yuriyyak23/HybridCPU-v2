using HybridCPU_ISE.Arch;

using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Centralized instruction encoder for VLIW instruction generation.
    ///
    /// Design goals:
    /// - Single source of truth for instruction encoding
    /// - Eliminates duplication in compiler and emulator code
    /// - Provides type-safe instruction building
    /// - Supports extended addressing modes (Indexed, 2D) and new instruction flags
    /// - HLS-friendly: no dynamic allocation, clear data flow
    ///
    /// Architecture contract:
    /// - VLIW instruction is 256 bits (32 bytes, 4×64-bit words)
    /// - Word 0: OpCode, DataType, PredicateMask, Flags, Immediate
    /// - Word 1: Destination/Source1 pointer or register IDs
    /// - Word 2: Source2 pointer or descriptor address
    /// - Word 3: StreamLength, Stride, RowStride
    /// </summary>
    public static class InstructionEncoder
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte RequireArchReg(ushort regId, string paramName)
        {
            if (regId > ArchRegId.MaxValue)
            {
                throw new ArgumentOutOfRangeException(paramName, regId,
                    $"Architectural register id must be in [0, {ArchRegId.MaxValue}].");
            }

            return (byte)regId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte RequireOptionalArchReg(ushort regId, string paramName)
        {
            if (regId == VLIW_Instruction.NoReg ||
                regId == VLIW_Instruction.NoArchReg)
            {
                return VLIW_Instruction.NoArchReg;
            }

            return RequireArchReg(regId, paramName);
        }

        /// <summary>
        /// Encode a scalar register operation using flat architectural register ids.
        /// StreamLength = 1, operands packed as register IDs in Word1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeScalar(
            uint opCode,
            DataTypeEnum dataType,
            ushort reg1,
            ushort reg2,
            ushort reg3,
            byte predicateMask = 0,
            ushort immediate = 0,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask;
            inst.Immediate = immediate;
            inst.StreamLength = 1; // Scalar = single element
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            inst.Word1 = VLIW_Instruction.PackArchRegs(
                RequireArchReg(reg1, nameof(reg1)),
                RequireArchReg(reg2, nameof(reg2)),
                RequireArchReg(reg3, nameof(reg3)));

            return inst;
        }

        /// <summary>
        /// Encode a vector memory-to-memory operation with 1D strided addressing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeVector1D(
            uint opCode,
            DataTypeEnum dataType,
            ulong destSrc1Ptr,
            ulong src2Ptr,
            ulong streamLength,
            ushort stride = 0,
            byte predicateMask = 0,
            ushort immediate = 0,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask;
            inst.Immediate = immediate;
            inst.DestSrc1Pointer = destSrc1Ptr;
            inst.Src2Pointer = src2Ptr;
            inst.StreamLength = (uint)streamLength;
            inst.Stride = stride;
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            // Clear special addressing flags
            inst.Indexed = false;
            inst.Is2D = false;

            return inst;
        }

        /// <summary>
        /// Encode a vector operation with 2D addressing pattern.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeVector2D(
            uint opCode,
            DataTypeEnum dataType,
            ulong destSrc1Ptr,
            ulong src2Ptr,
            ulong streamLength,
            ushort colStride,
            ushort rowStride,
            uint rowLength,
            byte predicateMask = 0,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask;
            inst.DestSrc1Pointer = destSrc1Ptr;
            inst.Src2Pointer = src2Ptr;
            inst.StreamLength = (uint)streamLength;
            inst.Stride = colStride;
            inst.RowStride = rowStride;
            inst.Immediate = (ushort)rowLength; // Store row length in immediate field
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            // Set 2D addressing flag
            inst.Is2D = true;
            inst.Indexed = false;

            return inst;
        }

        /// <summary>
        /// Encode a vector operation with indexed (gather/scatter) addressing.
        /// Word2 contains descriptor address pointing to Indexed2SrcDesc structure.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeVectorIndexed(
            uint opCode,
            DataTypeEnum dataType,
            ulong destSrc1Ptr,
            ulong descriptorAddr,
            ulong streamLength,
            byte predicateMask = 0,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask;
            inst.DestSrc1Pointer = destSrc1Ptr;
            inst.Src2Pointer = descriptorAddr; // Points to descriptor in memory
            inst.StreamLength = (uint)streamLength;
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            // Set indexed addressing flag
            inst.Indexed = true;
            inst.Is2D = false;

            return inst;
        }

        /// <summary>
        /// Encode a comparison instruction that generates a predicate mask.
        /// Result is written to predicate register specified in destination field.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeVectorComparison(
            uint opCode,
            DataTypeEnum dataType,
            ulong src1Ptr,
            ulong src2Ptr,
            ulong streamLength,
            byte destPredicateReg,
            ushort stride = 0,
            byte predicateMask = 0,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask; // Input mask (for masked comparisons)
            inst.DestSrc1Pointer = src1Ptr;
            inst.Src2Pointer = src2Ptr;
            inst.StreamLength = (uint)streamLength;
            inst.Stride = stride;
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            // Store destination predicate register in immediate field
            inst.Immediate = destPredicateReg;

            inst.Indexed = false;
            inst.Is2D = false;

            return inst;
        }

        /// <summary>
        /// Encode a reduction operation (sum, min, max, etc.)
        /// Result is a scalar value written to specified register.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeVectorReduction(
            uint opCode,
            DataTypeEnum dataType,
            ulong srcPtr,
            ushort destReg,
            ulong streamLength,
            ushort stride = 0,
            byte predicateMask = 0,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask;
            inst.DestSrc1Pointer = srcPtr;
            inst.StreamLength = (uint)streamLength;
            inst.Stride = stride;
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            // Set reduction flag
            inst.Reduction = true;

            // Store destination register in immediate field
            inst.Immediate = destReg;

            inst.Indexed = false;
            inst.Is2D = false;

            return inst;
        }

        /// <summary>
        /// Encode a control flow instruction (jump, call, return).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeControlFlow(
            uint opCode,
            ushort reg1,
            ushort reg2,
            ushort reg3,
            ulong targetAddress)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.Word1 = VLIW_Instruction.PackArchRegs(
                RequireOptionalArchReg(reg1, nameof(reg1)),
                RequireOptionalArchReg(reg2, nameof(reg2)),
                RequireOptionalArchReg(reg3, nameof(reg3)));
            inst.Src2Pointer = targetAddress; // Jump target

            return inst;
        }

        /// <summary>
        /// Encode a system instruction (CPU control, VMX, etc.)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeSystem(
            uint opCode,
            ushort reg1,
            ulong param1,
            ulong param2)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.Word1 = VLIW_Instruction.PackArchRegs(
                RequireArchReg(reg1, nameof(reg1)),
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg);
            inst.Src2Pointer = param1;
            inst.StreamLength = (uint)param2;

            return inst;
        }

        /// <summary>
        /// Encode a dot-product instruction (VDOT_VV, VDOTU_VV, VDOTF_VV).
        /// Result is a scalar written to element 0 of destination vector.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeDotProduct(
            uint opCode,
            DataTypeEnum dataType,
            ulong destPtr,
            ulong src1Ptr,
            ulong src2Ptr,
            ulong streamLength,
            ushort stride = 0,
            byte predicateMask = 0,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask;
            inst.DestSrc1Pointer = src1Ptr;  // First source
            inst.Src2Pointer = src2Ptr;       // Second source
            inst.StreamLength = (uint)streamLength;
            inst.Stride = stride;
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            // Set reduction flag (dot product reduces to scalar)
            inst.Reduction = true;

            // Store destination pointer in immediate field (architectural decision for ternary ops)
            // Note: May need adjustment based on actual instruction encoding

            inst.Indexed = false;
            inst.Is2D = false;

            return inst;
        }

        /// <summary>
        /// Encode a predicative movement instruction (VCOMPRESS_VM, VEXPAND_VM).
        /// These operations pack/unpack elements based on predicate mask.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodePredicativeMovement(
            uint opCode,
            DataTypeEnum dataType,
            ulong srcPtr,
            ulong destPtr,
            ulong streamLength,
            byte predicateMask,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask;  // Mask determines which elements to pack/unpack
            inst.DestSrc1Pointer = srcPtr;
            inst.Src2Pointer = destPtr;
            inst.StreamLength = (uint)streamLength;
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            inst.Indexed = false;
            inst.Is2D = false;

            return inst;
        }

        /// <summary>
        /// Encode a permutation/gather instruction (VPERMUTE_VV, VRGATHER_VV).
        /// Uses indexed addressing where index vector determines element ordering.
        /// Word2 contains descriptor address pointing to index vector.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodePermutation(
            uint opCode,
            DataTypeEnum dataType,
            ulong srcPtr,
            ulong indexDescriptorAddr,
            ulong destPtr,
            ulong streamLength,
            byte predicateMask = 0,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask;
            inst.DestSrc1Pointer = srcPtr;        // Source data vector
            inst.Src2Pointer = indexDescriptorAddr; // Descriptor containing index vector
            inst.StreamLength = (uint)streamLength;
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            // Set indexed addressing flag (gather/permute use index vector)
            inst.Indexed = true;
            inst.Is2D = false;

            return inst;
        }

        /// <summary>
        /// Encode a slide instruction (VSLIDEUP_VI, VSLIDEDOWN_VI).
        /// Slides vector elements up or down by immediate offset.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeSlide(
            uint opCode,
            DataTypeEnum dataType,
            ulong srcPtr,
            ulong destPtr,
            ulong streamLength,
            ushort slideOffset,
            byte predicateMask = 0,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask;
            inst.DestSrc1Pointer = srcPtr;
            inst.Src2Pointer = destPtr;
            inst.StreamLength = (uint)streamLength;
            inst.Immediate = slideOffset;  // Slide offset in immediate field
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            inst.Indexed = false;
            inst.Is2D = false;

            return inst;
        }

        /// <summary>
        /// Encode a FMA (Fused Multiply-Add) instruction using TriOpDesc descriptor.
        /// Word1 = accumulator/destination base address.
        /// Word2 = TriOpDesc descriptor address in memory (holds SrcA, SrcB, StrideA, StrideB).
        /// </summary>
        /// <param name="opCode">FMA opcode (VFMADD_VV, VFMSUB_VV, VFNMADD_VV, VFNMSUB_VV)</param>
        /// <param name="dataType">Element data type</param>
        /// <param name="accDestPtr">Accumulator/destination base address</param>
        /// <param name="triOpDescAddr">Address of TriOpDesc descriptor in memory</param>
        /// <param name="streamLength">Number of elements to process</param>
        /// <param name="destStride">Byte stride for accumulator/destination (0 = packed)</param>
        /// <param name="predicateMask">Predicate register index (0 = all active)</param>
        /// <param name="tailAgnostic">Per-instruction tail-agnostic policy</param>
        /// <param name="maskAgnostic">Per-instruction mask-agnostic policy</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeFMA(
            uint opCode,
            DataTypeEnum dataType,
            ulong accDestPtr,
            ulong triOpDescAddr,
            ulong streamLength,
            ushort destStride = 0,
            byte predicateMask = 0,
            bool tailAgnostic = false,
            bool maskAgnostic = false)
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = opCode;
            inst.DataType = (byte)dataType;
            inst.PredicateMask = predicateMask;
            inst.DestSrc1Pointer = accDestPtr;
            inst.Src2Pointer = triOpDescAddr;
            inst.StreamLength = (uint)streamLength;
            inst.Stride = destStride;
            inst.TailAgnostic = tailAgnostic;
            inst.MaskAgnostic = maskAgnostic;

            inst.Indexed = false;
            inst.Is2D = false;

            return inst;
        }

        /// <summary>
        /// Helper: Validate instruction encoding before execution.
        /// Returns true if instruction is well-formed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateInstruction(in VLIW_Instruction inst)
        {
            // Check opcode is not zero (Nope is allowed)
            if (inst.OpCode == 0 && inst.OpCode != (uint)Processor.CPU_Core.InstructionsEnum.Nope)
                return false;

            // Check data type is valid
            if (!Arch.DataTypeUtils.IsValid(inst.DataTypeValue))
                return false;

            // Check predicate register index is in range (0-15)
            if (inst.PredicateMask > 15)
                return false;

            // Check flags are consistent
            if (inst.Indexed && inst.Is2D)
                return false; // Cannot be both indexed and 2D

            return true;
        }

        /// <summary>
        /// Encode a CSR (Control and Status Register) read operation.
        /// Reads a CSR value into a scalar register.
        /// CSR address space: see Appendix A.2 (0x000=VL, 0x010=RoundingMode, 0x020=OverflowCount, etc.)
        /// </summary>
        /// <param name="csrAddr">CSR address (encoded in Immediate field)</param>
        /// <param name="destReg">Destination register for read value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeCSRRead(
            ushort csrAddr,
            ushort destReg)
        {
            var inst = new VLIW_Instruction();
            // Lower the assembler-style CSRR helper to canonical CSRRS rd, csr, x0.
            inst.OpCode = (uint)Processor.CPU_Core.InstructionsEnum.CSRRS;
            inst.DataType = (byte)DataTypeEnum.UINT32;
            inst.StreamLength = 1;
            inst.Immediate = csrAddr;
            inst.Word1 = VLIW_Instruction.PackArchRegs(
                RequireArchReg(destReg, nameof(destReg)),
                0,
                VLIW_Instruction.NoArchReg);
            return inst;
        }

        /// <summary>
        /// Encode a CSR (Control and Status Register) write operation.
        /// Writes a value from the source register (packed in Word1) to the addressed CSR.
        /// CSR address space: see Appendix A.2.
        /// </summary>
        /// <param name="csrAddr">CSR address (encoded in Immediate field)</param>
        /// <param name="srcReg">Source register whose value will be written to the CSR</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeCSRWrite(
            ushort csrAddr,
            ushort srcReg)
        {
            var inst = new VLIW_Instruction();
            // Lower the assembler-style CSRW helper to canonical CSRRW x0, csr, rs1.
            inst.OpCode = (uint)Processor.CPU_Core.InstructionsEnum.CSRRW;
            inst.DataType = (byte)DataTypeEnum.UINT32;
            inst.StreamLength = 1;
            inst.Immediate = csrAddr;
            inst.Word1 = VLIW_Instruction.PackArchRegs(
                VLIW_Instruction.NoArchReg,
                RequireArchReg(srcReg, nameof(srcReg)),
                VLIW_Instruction.NoArchReg);
            return inst;
        }

        /// <summary>
        /// Encode a vector exception status clear operation.
        /// Clears all exception counters without affecting modes or flags.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VLIW_Instruction EncodeClearExceptionCounters()
        {
            var inst = new VLIW_Instruction();
            inst.OpCode = (uint)Processor.CPU_Core.InstructionsEnum.CSR_CLEAR;
            inst.DataType = (byte)DataTypeEnum.UINT32;
            inst.StreamLength = 1;
            return inst;
        }
    }
}

