namespace YAKSys_Hybrid_CPU.Arch
{
    public static partial class OpcodeRegistry
    {
        private static OpcodeInfo[] CreateVectorOpcodes() =>
        [
            // ========== Vector Arithmetic Operations ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VADD, "VADD", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSUB, "VSUB", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMUL, "VMUL", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 3, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VDIV, "VDIV", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 16, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSQRT, "VSQRT", OpcodeCategory.Vector, 1, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite | InstructionFlags.FloatingPoint, 8, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMOD, "VMOD", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 16, 2),

            // ========== Vector Logical Operations ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VXOR, "VXOR", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VOR, "VOR", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VAND, "VAND", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VNOT, "VNOT", OpcodeCategory.Vector, 1, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),

            // ========== Vector Comparison Instructions (Generate Masks) ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VCMPEQ, "VCMPEQ", OpcodeCategory.Comparison, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VCMPNE, "VCMPNE", OpcodeCategory.Comparison, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VCMPLT, "VCMPLT", OpcodeCategory.Comparison, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VCMPLE, "VCMPLE", OpcodeCategory.Comparison, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VCMPGT, "VCMPGT", OpcodeCategory.Comparison, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VCMPGE, "VCMPGE", OpcodeCategory.Comparison, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),

            // ========== Predicate Mask Manipulation Instructions ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMAND, "VMAND", OpcodeCategory.Comparison, 2, InstructionFlags.Vector | InstructionFlags.TwoOperand | InstructionFlags.MaskManipulation, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMOR, "VMOR", OpcodeCategory.Comparison, 2, InstructionFlags.Vector | InstructionFlags.TwoOperand | InstructionFlags.MaskManipulation, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMXOR, "VMXOR", OpcodeCategory.Comparison, 2, InstructionFlags.Vector | InstructionFlags.TwoOperand | InstructionFlags.MaskManipulation, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMNOT, "VMNOT", OpcodeCategory.Comparison, 1, InstructionFlags.Vector | InstructionFlags.TwoOperand | InstructionFlags.MaskManipulation, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VPOPC, "VPOPC", OpcodeCategory.Comparison, 1, InstructionFlags.Vector | InstructionFlags.Reduction | InstructionFlags.MaskManipulation, 2, 0),

            // ========== Vector Shift Instructions ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSLL, "VSLL", OpcodeCategory.BitManip, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSRL, "VSRL", OpcodeCategory.BitManip, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSRA, "VSRA", OpcodeCategory.BitManip, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),

            // ========== Vector FMA Instructions ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VFMADD, "VFMADD", OpcodeCategory.Vector, 3, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.ThreeOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite | InstructionFlags.FloatingPoint, 4, 3),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VFMSUB, "VFMSUB", OpcodeCategory.Vector, 3, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.ThreeOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite | InstructionFlags.FloatingPoint, 4, 3),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VFNMADD, "VFNMADD", OpcodeCategory.Vector, 3, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.ThreeOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite | InstructionFlags.FloatingPoint, 4, 3),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VFNMSUB, "VFNMSUB", OpcodeCategory.Vector, 3, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.ThreeOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite | InstructionFlags.FloatingPoint, 4, 3),

            // ========== Vector Min/Max Instructions ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMIN, "VMIN", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMAX, "VMAX", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMINU, "VMINU", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMAXU, "VMAXU", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.TwoOperand | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 1, 2),

            // ========== Vector Reduction Instructions ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VREDSUM, "VREDSUM", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 8, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VREDMAX, "VREDMAX", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 8, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VREDMIN, "VREDMIN", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 8, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VREDMAXU, "VREDMAXU", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 8, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VREDMINU, "VREDMINU", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 8, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VREDAND, "VREDAND", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 8, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VREDOR, "VREDOR", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 8, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VREDXOR, "VREDXOR", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 8, 2),

            // ========== Vector Configuration Instructions ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSETVL, "VSETVL", OpcodeCategory.System, 2, InstructionFlags.Vector | InstructionFlags.ModifiesFlags, 1, 0, InstructionClass.System, SerializationClass.FullSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSETVLI, "VSETVLI", OpcodeCategory.System, 2, InstructionFlags.Vector | InstructionFlags.ModifiesFlags | InstructionFlags.UsesImmediate, 1, 0, InstructionClass.System, SerializationClass.FullSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSETIVLI, "VSETIVLI", OpcodeCategory.System, 2, InstructionFlags.Vector | InstructionFlags.ModifiesFlags | InstructionFlags.UsesImmediate, 1, 0, InstructionClass.System, SerializationClass.FullSerial),

            // ========== Vector Transfer Instructions ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VLOAD, "VLOAD", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 2, InstructionClass.Memory, SerializationClass.Free),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSTORE, "VSTORE", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 2, InstructionClass.Memory, SerializationClass.MemoryOrdered),

            // ========== Dot Product Instructions (ML/DSP) ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VDOT, "VDOT", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 12, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VDOTU, "VDOTU", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 12, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VDOTF, "VDOTF", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.FloatingPoint | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 12, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VDOT_FP8, "VDOT_FP8", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Reduction | InstructionFlags.FloatingPoint | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 12, 2),

            // ========== Predicative Movement Instructions (ARM SVE style) ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VCOMPRESS, "VCOMPRESS", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VEXPAND, "VEXPAND", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 2),

            // ========== Bit Manipulation Instructions ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VREVERSE, "VREVERSE", OpcodeCategory.BitManip, 1, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 2, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VPOPCNT, "VPOPCNT", OpcodeCategory.BitManip, 1, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 2, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VCLZ, "VCLZ", OpcodeCategory.BitManip, 1, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 2, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VCTZ, "VCTZ", OpcodeCategory.BitManip, 1, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 2, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VBREV8, "VBREV8", OpcodeCategory.BitManip, 1, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 2, 2),

            // ========== Permutation and Gather/Scatter Instructions ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VPERMUTE, "VPERMUTE", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Indexed | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSLIDEUP, "VSLIDEUP", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.UsesImmediate | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 3, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSLIDEDOWN, "VSLIDEDOWN", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.UsesImmediate | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 3, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VRGATHER, "VRGATHER", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Indexed | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 2),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VGATHER, "VGATHER", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Indexed | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 2, InstructionClass.Memory, SerializationClass.Free),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSCATTER, "VSCATTER", OpcodeCategory.Vector, 2, InstructionFlags.Vector | InstructionFlags.Maskable | InstructionFlags.Indexed | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 2, InstructionClass.Memory, SerializationClass.MemoryOrdered),
        ];
    }
}
