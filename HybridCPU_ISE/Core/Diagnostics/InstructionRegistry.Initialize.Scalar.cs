
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
