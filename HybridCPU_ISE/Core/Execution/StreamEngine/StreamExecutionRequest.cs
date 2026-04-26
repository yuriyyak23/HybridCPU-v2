using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// Validated ingress projection for live stream execution.
    /// Carries only runtime-relevant facts and intentionally discards compat-only transport hints.
    /// </summary>
    internal readonly struct StreamExecutionRequest
    {
        internal const byte NoArchReg = VLIW_Instruction.NoArchReg;

        public uint OpCode { get; }

        public DataTypeEnum DataTypeValue { get; }

        public byte PredicateMask { get; }

        public ulong DestSrc1Pointer { get; }

        public ulong Src2Pointer { get; }

        public ushort Immediate { get; }

        public uint StreamLength { get; }

        public ushort Stride { get; }

        public ushort RowStride { get; }

        public bool Indexed { get; }

        public bool Is2D { get; }

        public bool TailAgnostic { get; }

        public bool MaskAgnostic { get; }

        public bool IsScalar => StreamLength <= 1;

        public bool RequiresRetireVisibleScalarCarrier =>
            (StreamLength == 1 && IsScalar) ||
            OpCode == (uint)Processor.CPU_Core.InstructionsEnum.VPOPC;

        public bool RequiresPredicateStateCarrier
        {
            get
            {
                if (StreamLength == 0 || IsScalar)
                {
                    return false;
                }

                return OpcodeRegistry.IsComparisonOp(OpCode) ||
                       (OpcodeRegistry.IsMaskManipOp(OpCode) &&
                        OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VPOPC);
            }
        }

        public bool RequiresMemoryVisibleCarrier
        {
            get
            {
                if (StreamLength == 0 || IsScalar)
                {
                    return false;
                }

                return OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VPOPC &&
                       !RequiresPredicateStateCarrier;
            }
        }

        public bool IsUnsupportedScalarizedVectorHelperSurface =>
            StreamLength == 1 &&
            IsScalar &&
            OpCode != (uint)Processor.CPU_Core.InstructionsEnum.VPOPC &&
            OpcodeRegistry.IsVectorOp(OpCode);

        public bool IsUnsupportedZeroLengthHelperSurface =>
            StreamLength == 0 &&
            !RequiresRetireVisibleScalarCarrier;

        public bool IsUnsupportedControlHelperSurface =>
            OpCode is
                (uint)Processor.CPU_Core.InstructionsEnum.STREAM_SETUP or
                (uint)Processor.CPU_Core.InstructionsEnum.STREAM_START or
                (uint)Processor.CPU_Core.InstructionsEnum.STREAM_WAIT;

        private StreamExecutionRequest(
            uint opCode,
            DataTypeEnum dataTypeValue,
            byte predicateMask,
            ulong destSrc1Pointer,
            ulong src2Pointer,
            ushort immediate,
            uint streamLength,
            ushort stride,
            ushort rowStride,
            bool indexed,
            bool is2D,
            bool tailAgnostic,
            bool maskAgnostic)
        {
            OpCode = opCode;
            DataTypeValue = dataTypeValue;
            PredicateMask = predicateMask;
            DestSrc1Pointer = destSrc1Pointer;
            Src2Pointer = src2Pointer;
            Immediate = immediate;
            StreamLength = streamLength;
            Stride = stride;
            RowStride = rowStride;
            Indexed = indexed;
            Is2D = is2D;
            TailAgnostic = tailAgnostic;
            MaskAgnostic = maskAgnostic;
        }

        internal static StreamExecutionRequest CreateValidatedCompatIngress(
            in VLIW_Instruction instruction)
        {
            // word3[50] and word3[49:48] are compat transport fields only.
            // Live execution projects the architecturally relevant payload once and then
            // ignores the VirtualThreadId hint completely. Retired policy-gap must already
            // be zero at production ingress and therefore fails closed here.
            ulong validatedWord3 = VLIW_Instruction.ValidateWord3ForProductionIngress(instruction.Word3);

            return new StreamExecutionRequest(
                instruction.OpCode,
                instruction.DataTypeValue,
                instruction.PredicateMask,
                instruction.DestSrc1Pointer,
                instruction.Src2Pointer,
                instruction.Immediate,
                (uint)((validatedWord3 >> 16) & 0xFFFF_FFFFUL),
                (ushort)(validatedWord3 & 0xFFFFUL),
                (ushort)((validatedWord3 >> 51) & 0x1FFFUL),
                instruction.Indexed,
                instruction.Is2D,
                instruction.TailAgnostic,
                instruction.MaskAgnostic);
        }

        internal bool TryUnpackArchRegs(out byte rd, out byte rs1, out byte rs2)
        {
            return VLIW_Instruction.TryUnpackArchRegs(
                DestSrc1Pointer,
                out rd,
                out rs1,
                out rs2);
        }
    }
}
