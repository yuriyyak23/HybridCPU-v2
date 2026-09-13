using System;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.ArchitectureAndExecution
{
    public sealed class PipelineHiddenWriteHazardTests
    {
        [Fact]
        public void MoveMicroOp_Dt4MetadataRefresh_FailsClosedBeforeHiddenWriteStateCanExist()
        {
            var move = CreateRetainedMove(dataType: 4, reg1Id: 6, reg2Id: 7);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => move.RefreshWriteMetadata());

            Assert.Contains("Move DT=4", exception.Message, StringComparison.Ordinal);
            Assert.Contains("unsupported", exception.Message, StringComparison.Ordinal);
            Assert.Empty(move.WriteRegisters);
            Assert.False(move.WritesRegister);
        }

        [Fact]
        public void MoveMicroOp_Dt5MetadataRefresh_FailsClosedBeforeHiddenWriteStateCanExist()
        {
            var move = CreateRetainedMove(dataType: 5, reg1Id: 6, reg2Id: 7);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => move.RefreshWriteMetadata());

            Assert.Contains("Move DT=5", exception.Message, StringComparison.Ordinal);
            Assert.Contains("unsupported", exception.Message, StringComparison.Ordinal);
            Assert.Empty(move.WriteRegisters);
            Assert.False(move.WritesRegister);
        }

        [Fact]
        public void MoveMicroOp_Dt4Execute_FailsClosedWithoutPublishingSecondaryWrite()
        {
            var core = new Processor.CPU_Core(0);
            var move = CreateRetainedMove(dataType: 4, reg1Id: 6, reg2Id: 7);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => move.Execute(ref core));

            Assert.Contains("Move DT=4", exception.Message, StringComparison.Ordinal);
            Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
            Assert.Equal((ushort)0, move.DestRegID);
            Assert.False(move.WritesRegister);
        }

        private static MoveMicroOp CreateRetainedMove(byte dataType, byte reg1Id, byte reg2Id)
        {
            return new MoveMicroOp
            {
                Instruction = new VLIW_Instruction
                {
                    DataType = dataType,
                    Word1 = VLIW_Instruction.PackArchRegs(
                        reg1Id,
                        reg2Id,
                        VLIW_Instruction.NoArchReg),
                    Src2Pointer = 0x1111,
                    Word3 = 0x2222
                }
            };
        }
    }
}
