using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        private static ushort NormalizeCanonicalOrSuppressedCsrDestinationRegister(
            uint opCode,
            ushort rawRegId)
        {
            if (!TryDecodeCanonicalOrUnpackedNoArchRegister(rawRegId, out byte registerId))
            {
                throw new DecodeProjectionFaultException(
                    $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opCode)} reached InstructionRegistry.CreateMicroOp(...) with a non-canonical destination register encoding. " +
                    "CSR manual publication requires a flat architectural rd or an explicit NoReg/NoArchReg sentinel so optional writeback stays synchronized with the authoritative lane-7 carrier.");
            }

            return registerId == ArchRegisterTripletEncoding.NoArchReg
                ? ArchRegisterTripletEncoding.NoReg
                : registerId;
        }

        private static ushort NormalizeCanonicalOrSuppressedCsrSourceRegister(
            uint opCode,
            ushort rawRegId,
            string operandName)
        {
            if (!TryDecodeCanonicalArchRegister(rawRegId, out byte registerId))
            {
                throw new DecodeProjectionFaultException(
                    $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opCode)} reached InstructionRegistry.CreateMicroOp(...) with a non-canonical {operandName} register encoding. " +
                    "CSR manual publication requires a flat architectural source register or an explicit NoReg/x0 suppression sentinel so runtime follow-through does not invent hidden register truth.");
            }

            return registerId == ArchRegisterTripletEncoding.NoArchReg
                ? ArchRegisterTripletEncoding.NoReg
                : registerId;
        }

        private static ulong NormalizeCanonicalCsrImmediateField(
            uint opCode,
            ushort rawImmediateField,
            string operandName)
        {
            if (rawImmediateField > 0x1F)
            {
                throw new DecodeProjectionFaultException(
                    $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opCode)} reached InstructionRegistry.CreateMicroOp(...) with a non-canonical {operandName} immediate encoding. " +
                    "CSR immediate manual publication must provide the canonical 5-bit zimm payload instead of relying on compatibility masking.");
            }

            return rawImmediateField;
        }

        internal static bool TryCreatePublishedCsrMicroOp(
            in InstructionIR instruction,
            out CSRMicroOp? csrMicroOp)
        {
            csrMicroOp = null;

            ushort opcode = instruction.CanonicalOpcode.Value;
            if (InstructionClassifier.GetClass(opcode) != InstructionClass.Csr)
            {
                return false;
            }

            switch (opcode)
            {
                case Processor.CPU_Core.IsaOpcodeValues.CSRRW:
                    csrMicroOp = CreatePublishedRegisterSourceCsrMicroOp<CsrReadWriteMicroOp>(opcode, instruction);
                    return true;
                case Processor.CPU_Core.IsaOpcodeValues.CSRRS:
                    csrMicroOp = CreatePublishedRegisterSourceCsrMicroOp<CsrReadSetMicroOp>(opcode, instruction);
                    return true;
                case Processor.CPU_Core.IsaOpcodeValues.CSRRC:
                    csrMicroOp = CreatePublishedRegisterSourceCsrMicroOp<CsrReadClearMicroOp>(opcode, instruction);
                    return true;
                case Processor.CPU_Core.IsaOpcodeValues.CSRRWI:
                    csrMicroOp = CreatePublishedImmediateSourceCsrMicroOp<CsrReadWriteImmediateMicroOp>(opcode, instruction);
                    return csrMicroOp is not null;
                case Processor.CPU_Core.IsaOpcodeValues.CSRRSI:
                    csrMicroOp = CreatePublishedImmediateSourceCsrMicroOp<CsrReadSetImmediateMicroOp>(opcode, instruction);
                    return csrMicroOp is not null;
                case Processor.CPU_Core.IsaOpcodeValues.CSRRCI:
                    csrMicroOp = CreatePublishedImmediateSourceCsrMicroOp<CsrReadClearImmediateMicroOp>(opcode, instruction);
                    return csrMicroOp is not null;
                case Processor.CPU_Core.IsaOpcodeValues.CSR_CLEAR:
                    csrMicroOp = new CsrClearMicroOp
                    {
                        OpCode = opcode,
                    };
                    csrMicroOp.InitializeMetadata();
                    return true;
                case Processor.CPU_Core.IsaOpcodeValues.VSETVEXCPMASK:
                    csrMicroOp = CreatePublishedVectorExceptionControlCsrMicroOp(
                        opcode,
                        instruction,
                        CsrAddresses.VexcpMask);
                    return true;
                case Processor.CPU_Core.IsaOpcodeValues.VSETVEXCPPRI:
                    csrMicroOp = CreatePublishedVectorExceptionControlCsrMicroOp(
                        opcode,
                        instruction,
                        CsrAddresses.VexcpPri);
                    return true;
                default:
                    return false;
            }
        }

        private static T CreatePublishedRegisterSourceCsrMicroOp<T>(
            ushort opcode,
            in InstructionIR instruction)
            where T : CSRMicroOp, new()
        {
            var csrMicroOp = new T
            {
                OpCode = opcode,
                CSRAddress = unchecked((ushort)instruction.Imm),
                DestRegID = NormalizeCanonicalOrSuppressedCsrDestinationRegister(
                    opcode,
                    ToLegacyDecoderField(instruction.Rd)),
                SrcRegID = NormalizeCanonicalOrSuppressedCsrSourceRegister(
                    opcode,
                    ToLegacyDecoderField(instruction.Rs1),
                    "rs1"),
            };

            csrMicroOp.InitializeMetadata();
            return csrMicroOp;
        }

        private static T? CreatePublishedImmediateSourceCsrMicroOp<T>(
            ushort opcode,
            in InstructionIR instruction)
            where T : CSRMicroOp, new()
        {
            if (instruction.Rs1 > 0x1F)
            {
                return null;
            }

            var csrMicroOp = new T
            {
                OpCode = opcode,
                CSRAddress = unchecked((ushort)instruction.Imm),
                DestRegID = NormalizeCanonicalOrSuppressedCsrDestinationRegister(
                    opcode,
                    ToLegacyDecoderField(instruction.Rd)),
                WriteValue = NormalizeCanonicalCsrImmediateField(
                    opcode,
                    instruction.Rs1,
                    "zimm"),
            };

            csrMicroOp.InitializeMetadata();
            return csrMicroOp;
        }

        private static CsrReadWriteMicroOp CreatePublishedVectorExceptionControlCsrMicroOp(
            ushort opcode,
            in InstructionIR instruction,
            ushort csrAddress)
        {
            var csrMicroOp = new CsrReadWriteMicroOp
            {
                OpCode = opcode,
                CSRAddress = csrAddress,
                DestRegID = ArchRegisterTripletEncoding.NoReg,
                SrcRegID = NormalizeCanonicalOrSuppressedCsrSourceRegister(
                    opcode,
                    ToLegacyDecoderField(instruction.Rs1),
                    "rs1"),
            };

            csrMicroOp.InitializeMetadata();
            return csrMicroOp;
        }

        private static void RegisterCsrClearOp(uint opCode)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                var op = new CsrClearMicroOp
                {
                    OpCode = ctx.OpCode,
                };
                op.InitializeMetadata();
                return op;
            });

            RegisterOpAttributes(
                opCode,
                CreatePublishedSystemLikeDescriptor(
                    opCode,
                    writesRegister: false,
                    isMemoryOp: false));
        }

        private static void RegisterVectorExceptionControlCsrOp(uint opCode, ushort csrAddress)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                if (!TryDecodeCanonicalArchRegister(ctx.Reg2ID, out byte sourceRegister))
                {
                    throw new DecodeProjectionFaultException(
                        $"Opcode {OpcodeRegistry.GetMnemonicOrHex(ctx.OpCode)} reached InstructionRegistry.CreateMicroOp(...) with a non-canonical source register encoding. " +
                        "Vector exception control publication requires a flat architectural rs1 so the mainline CSR carrier preserves authoritative metadata and retire follow-through.");
                }

                ushort materializedSourceRegister = sourceRegister == ArchRegisterTripletEncoding.NoArchReg
                    ? ArchRegisterTripletEncoding.NoReg
                    : sourceRegister;

                var op = new CsrReadWriteMicroOp
                {
                    OpCode = ctx.OpCode,
                    CSRAddress = csrAddress,
                    DestRegID = ArchRegisterTripletEncoding.NoReg,
                    SrcRegID = materializedSourceRegister
                };

                op.InitializeMetadata();

                ushort opcode = unchecked((ushort)ctx.OpCode);
                InstructionClass instructionClass = InstructionClassifier.GetClass(opcode);
                op.ApplyCanonicalDecodeProjection(
                    instructionClass,
                    InstructionClassifier.GetSerializationClass(opcode),
                    BundleLegalityAnalyzer.BuildCanonicalPlacement(instructionClass, op.Placement.DomainTag),
                    isMemoryOp: false,
                    isControlFlow: false,
                    writesRegister: false,
                    readRegisters: sourceRegister != 0 && sourceRegister != ArchRegisterTripletEncoding.NoArchReg
                        ? new[] { (int)sourceRegister }
                        : Array.Empty<int>(),
                    writeRegisters: Array.Empty<int>());
                op.RefreshAdmissionMetadata();
                return op;
            });

            RegisterOpAttributes(opCode, new MicroOpDescriptor(latency: 1, memFootprintClass: 0)
            {
                WritesRegister = false,
                IsMemoryOp = false,
            });
        }
    }
}
