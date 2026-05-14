
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        private static void RegisterScalarArithmeticInstructions()
        {
            // ========== Scalar Arithmetic ==========
            // Example: Register with explicit MicroOpDescriptor for better FSP awareness
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.Addition, ctx =>
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
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.Addition, CreatePublishedScalarDescriptor((uint)Processor.CPU_Core.InstructionsEnum.Addition));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.Subtraction, ctx =>
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
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.Subtraction, CreatePublishedScalarDescriptor((uint)Processor.CPU_Core.InstructionsEnum.Subtraction));

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

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.Multiplication, ctx =>
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
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.Multiplication, CreatePublishedScalarDescriptor((uint)Processor.CPU_Core.InstructionsEnum.Multiplication));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.Division, ctx =>
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
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.Division, CreatePublishedScalarDescriptor((uint)Processor.CPU_Core.InstructionsEnum.Division));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.Modulus, ctx =>
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
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.Modulus, new MicroOpDescriptor
            {
                Latency = 4,
                MemFootprintClass = 0,
                WritesRegister = true,
                IsMemoryOp = false,
            });

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.ShiftLeft, ctx =>
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
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.ShiftLeft, new MicroOpDescriptor
            {
                Latency = 1,
                MemFootprintClass = 0,
                WritesRegister = true,
                IsMemoryOp = false,
            });

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.ShiftRight, ctx =>
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
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.ShiftRight, new MicroOpDescriptor
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

        }
    }
}
