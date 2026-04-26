
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        private static void RegisterVectorInstructions()
        {
            // ========== Vector Operations ==========
            RegisterVectorConfigOp((uint)Processor.CPU_Core.InstructionsEnum.VSETVL);
            RegisterVectorConfigOp((uint)Processor.CPU_Core.InstructionsEnum.VSETVLI);
            RegisterVectorConfigOp((uint)Processor.CPU_Core.InstructionsEnum.VSETIVLI);

            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VADD);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VSUB);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VMUL);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VDIV);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VMOD);

            // Legacy vector transfer opcodes still execute through StreamEngine but must now
            // materialize a concrete MicroOp so the live WB retire path keeps explicit authority.
            RegisterVectorTransferOp((uint)Processor.CPU_Core.InstructionsEnum.VLOAD, 4);
            RegisterVectorTransferOp((uint)Processor.CPU_Core.InstructionsEnum.VSTORE, 4);

            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VXOR);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VOR);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VAND);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VSLL);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VSRL);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VSRA);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VMIN);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VMAX);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VMINU);
            RegisterPublishedVectorBinaryOp((uint)Processor.CPU_Core.InstructionsEnum.VMAXU);

            RegisterPublishedVectorUnaryOp((uint)Processor.CPU_Core.InstructionsEnum.VSQRT);
            RegisterPublishedVectorUnaryOp((uint)Processor.CPU_Core.InstructionsEnum.VNOT);
            RegisterPublishedVectorUnaryOp((uint)Processor.CPU_Core.InstructionsEnum.VPOPCNT);
            RegisterPublishedVectorUnaryOp((uint)Processor.CPU_Core.InstructionsEnum.VCLZ);
            RegisterPublishedVectorUnaryOp((uint)Processor.CPU_Core.InstructionsEnum.VCTZ);
            RegisterPublishedVectorUnaryOp((uint)Processor.CPU_Core.InstructionsEnum.VBREV8);
            RegisterPublishedVectorUnaryOp((uint)Processor.CPU_Core.InstructionsEnum.VREVERSE);

            RegisterPublishedVectorComparisonOp((uint)Processor.CPU_Core.InstructionsEnum.VCMPEQ);
            RegisterPublishedVectorComparisonOp((uint)Processor.CPU_Core.InstructionsEnum.VCMPNE);
            RegisterPublishedVectorComparisonOp((uint)Processor.CPU_Core.InstructionsEnum.VCMPLT);
            RegisterPublishedVectorComparisonOp((uint)Processor.CPU_Core.InstructionsEnum.VCMPLE);
            RegisterPublishedVectorComparisonOp((uint)Processor.CPU_Core.InstructionsEnum.VCMPGT);
            RegisterPublishedVectorComparisonOp((uint)Processor.CPU_Core.InstructionsEnum.VCMPGE);

            RegisterPublishedVectorFmaOp((uint)Processor.CPU_Core.InstructionsEnum.VFMADD);
            RegisterPublishedVectorFmaOp((uint)Processor.CPU_Core.InstructionsEnum.VFMSUB);
            RegisterPublishedVectorFmaOp((uint)Processor.CPU_Core.InstructionsEnum.VFNMADD);
            RegisterPublishedVectorFmaOp((uint)Processor.CPU_Core.InstructionsEnum.VFNMSUB);

            RegisterPublishedVectorReductionOp((uint)Processor.CPU_Core.InstructionsEnum.VREDSUM);
            RegisterPublishedVectorReductionOp((uint)Processor.CPU_Core.InstructionsEnum.VREDMAX);
            RegisterPublishedVectorReductionOp((uint)Processor.CPU_Core.InstructionsEnum.VREDMIN);
            RegisterPublishedVectorReductionOp((uint)Processor.CPU_Core.InstructionsEnum.VREDMAXU);
            RegisterPublishedVectorReductionOp((uint)Processor.CPU_Core.InstructionsEnum.VREDMINU);
            RegisterPublishedVectorReductionOp((uint)Processor.CPU_Core.InstructionsEnum.VREDAND);
            RegisterPublishedVectorReductionOp((uint)Processor.CPU_Core.InstructionsEnum.VREDOR);
            RegisterPublishedVectorReductionOp((uint)Processor.CPU_Core.InstructionsEnum.VREDXOR);

            RegisterPublishedVectorDotProductOp((uint)Processor.CPU_Core.InstructionsEnum.VDOT);
            RegisterPublishedVectorDotProductOp((uint)Processor.CPU_Core.InstructionsEnum.VDOTU);
            RegisterPublishedVectorDotProductOp((uint)Processor.CPU_Core.InstructionsEnum.VDOTF);
            RegisterPublishedVectorDotProductOp((uint)Processor.CPU_Core.InstructionsEnum.VDOT_FP8);

            RegisterPublishedVectorPermutationOp((uint)Processor.CPU_Core.InstructionsEnum.VPERMUTE);
            RegisterPublishedVectorPermutationOp((uint)Processor.CPU_Core.InstructionsEnum.VRGATHER);
            RegisterPublishedDescriptorOnly((uint)Processor.CPU_Core.InstructionsEnum.VGATHER, memFootprintClass: 3);
            RegisterPublishedDescriptorOnly((uint)Processor.CPU_Core.InstructionsEnum.VSCATTER, memFootprintClass: 3);
            RegisterPublishedVectorSlideOp((uint)Processor.CPU_Core.InstructionsEnum.VSLIDEUP);
            RegisterPublishedVectorSlideOp((uint)Processor.CPU_Core.InstructionsEnum.VSLIDEDOWN);
            RegisterPublishedVectorMaskOp((uint)Processor.CPU_Core.InstructionsEnum.VMAND);
            RegisterPublishedVectorMaskOp((uint)Processor.CPU_Core.InstructionsEnum.VMOR);
            RegisterPublishedVectorMaskOp((uint)Processor.CPU_Core.InstructionsEnum.VMXOR);
            RegisterPublishedVectorMaskOp((uint)Processor.CPU_Core.InstructionsEnum.VMNOT);
            RegisterPublishedVectorMaskPopCountOp((uint)Processor.CPU_Core.InstructionsEnum.VPOPC);

            RegisterPublishedVectorPredicativeMovementOp((uint)Processor.CPU_Core.InstructionsEnum.VCOMPRESS);
            RegisterPublishedVectorPredicativeMovementOp((uint)Processor.CPU_Core.InstructionsEnum.VEXPAND);
        }
    }
}
