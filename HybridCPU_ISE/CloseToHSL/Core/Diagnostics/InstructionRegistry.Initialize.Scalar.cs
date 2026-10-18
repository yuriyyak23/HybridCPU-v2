
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        private static void RegisterScalarArithmeticInstructions()
        {
            // ========== Scalar Arithmetic ==========
            // Example: Register with explicit MicroOpDescriptor for better FSP awareness
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.ADD, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    Src2RegID = ctx.Reg3ID,
                    UsesImmediate = false,
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.ADD, CreatePublishedScalarDescriptor((uint)Processor.CPU_Core.InstructionsEnum.ADD));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.SUB, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    Src2RegID = ctx.Reg3ID,
                    UsesImmediate = false,
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.SUB, CreatePublishedScalarDescriptor((uint)Processor.CPU_Core.InstructionsEnum.SUB));

            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.ADDW);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SUBW);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SLLW);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SRLW);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SRAW);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.MULW);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.DIVW);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.DIVUW);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.REMW);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.REMUW);
            RegisterScalarUnaryRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SEXT_W);
            RegisterScalarUnaryRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.ZEXT_W);

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.MUL, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    Src2RegID = ctx.Reg3ID,
                    UsesImmediate = false,
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.MUL, CreatePublishedScalarDescriptor((uint)Processor.CPU_Core.InstructionsEnum.MUL));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.DIV, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    Src2RegID = ctx.Reg3ID,
                    UsesImmediate = false,
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.DIV, CreatePublishedScalarDescriptor((uint)Processor.CPU_Core.InstructionsEnum.DIV));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.SLL, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    Src2RegID = ctx.Reg3ID,
                    UsesImmediate = false,
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.SLL, new MicroOpDescriptor
            {
                Latency = 1,
                MemFootprintClass = 0,
                WritesRegister = true,
                IsMemoryOp = false,
            });

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.SRL, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    Src2RegID = ctx.Reg3ID,
                    UsesImmediate = false,
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.SRL, new MicroOpDescriptor
            {
                Latency = 1,
                MemFootprintClass = 0,
                WritesRegister = true,
                IsMemoryOp = false,
            });

            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SRA);

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.XOR, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    Src2RegID = ctx.Reg3ID,
                    UsesImmediate = false,
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.XOR, new MicroOpDescriptor
            {
                Latency = 1,
                MemFootprintClass = 0,
                WritesRegister = true,
                IsMemoryOp = false,
            });

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.OR, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    Src2RegID = ctx.Reg3ID,
                    UsesImmediate = false,
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.OR, new MicroOpDescriptor
            {
                Latency = 1,
                MemFootprintClass = 0,
                WritesRegister = true,
                IsMemoryOp = false,
            });

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.AND, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    Src2RegID = ctx.Reg3ID,
                    UsesImmediate = false,
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.AND, new MicroOpDescriptor
            {
                Latency = 1,
                MemFootprintClass = 0,
                WritesRegister = true,
                IsMemoryOp = false,
            });

            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.CZERO_EQZ);
            RegisterScalarUnaryRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.CLZ);
            RegisterScalarUnaryRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.CTZ);
            RegisterScalarUnaryRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.CPOP);
            RegisterScalarUnaryRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SEXT_B);
            RegisterScalarUnaryRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SEXT_H);
            RegisterScalarUnaryRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.ZEXT_H);
            RegisterScalarRotateRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.ROL);
            RegisterScalarRotateRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.ROR);
            RegisterScalarRotateImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.ROLI);
            RegisterScalarRotateImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.RORI);
            RegisterScalarBitfieldRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.BSET);
            RegisterScalarBitfieldRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.BCLR);
            RegisterScalarBitfieldRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.BINV);
            RegisterScalarBitfieldRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.BEXT);
            RegisterScalarBitfieldImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.BSETI);
            RegisterScalarBitfieldImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.BCLRI);
            RegisterScalarBitfieldImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.BINVI);
            RegisterScalarBitfieldImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.BEXTI);
            RegisterScalarBooleanInvertRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.ANDN);
            RegisterScalarBooleanInvertRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.ORN);
            RegisterScalarBooleanInvertRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.XNOR);
            RegisterScalarMinMaxRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.MIN);
            RegisterScalarMinMaxRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.MAX);
            RegisterScalarMinMaxRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.MINU);
            RegisterScalarMinMaxRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.MAXU);
            RegisterScalarUnaryRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.REV8);
            RegisterScalarUnaryRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.BREV8);
            RegisterScalarZeroingSelectRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.CZERO_NEZ);
            RegisterScalarAddressGenerationRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SH1ADD);
            RegisterScalarAddressGenerationRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SH2ADD);
            RegisterScalarAddressGenerationRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SH3ADD);
            RegisterScalarAddressGenerationRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.ADD_UW);
            RegisterScalarAddressGenerationRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SH1ADD_UW);
            RegisterScalarAddressGenerationRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SH2ADD_UW);
            RegisterScalarAddressGenerationRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SH3ADD_UW);
            RegisterScalarAddressGenerationImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.SLLI_UW);
            RegisterScalarCarryLessRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.CLMUL);
            RegisterScalarCarryLessRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.CLMULH);
            RegisterScalarCarryLessRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.CLMULR);

        }
    }
}
