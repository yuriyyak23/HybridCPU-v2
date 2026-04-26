using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Phase04
{
    public sealed class Phase04_ScalarMicroOpArchStateTests
    {
        [Fact]
        public void ScalarAlu_Execute_DoesNotNormalizeOutOfRangeFlatIdsThroughRemovedLegacyRegisterMap()
        {
            var core = new Processor.CPU_Core(0);

            core.WriteCommittedArch(1, 16, 100UL);
            core.WriteCommittedArch(1, 17, 5UL);

            var op = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                OwnerThreadId = 1,
                VirtualThreadId = 1,
                DestRegID = 18,
                Src1RegID = 48,
                Src2RegID = 17,
                WritesRegister = true,
            };
            op.InitializeMetadata();

            Assert.True(op.Execute(ref core));

            // Retire through the WB typed retire packet path - the production
            // authority since Commit() no longer calls RetireCoordinator directly.
            Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
            int retireRecordCount = 0;
            op.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
            core.RetireCoordinator.Retire(retireRecords[..retireRecordCount]);

            Assert.Equal(5UL, core.ReadArch(1, 18));
        }
    }
}
