using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests
{
    public sealed class AtomicOrderingPropagationTests
    {
        [Fact]
        public void VliwDecoderV4_PreservesAtomicAcquireReleaseBits()
        {
            var decoder = new VliwDecoderV4();
            var raw = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.AMOADD_W,
                Word1 = VLIW_Instruction.PackArchRegs(5, 6, 7),
                Acquire = true,
                Release = true,
            };

            InstructionIR ir = decoder.Decode(in raw, slotIndex: 0);

            Assert.True(ir.AcquireOrdering);
            Assert.True(ir.ReleaseOrdering);
        }

        [Fact]
        public void InternalOpBuilder_ProjectsAtomicAcquireReleaseBits()
        {
            var ir = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.AMOADD_W,
                Class = InstructionClass.Atomic,
                SerializationClass = SerializationClass.AtomicSerial,
                Rd = 5,
                Rs1 = 6,
                Rs2 = 7,
                Imm = 0,
                AcquireOrdering = true,
                ReleaseOrdering = true,
            };

            InternalOp op = new InternalOpBuilder().Build(ir);

            Assert.Equal(
                InternalOpFlags.AcquireOrdering | InternalOpFlags.ReleaseOrdering,
                op.Flags);
            Assert.True(op.HasOrdering);
        }
    }
}
