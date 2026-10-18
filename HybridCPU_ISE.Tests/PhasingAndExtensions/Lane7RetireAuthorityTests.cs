using Xunit;
using ProcessorModel = YAKSys_Hybrid_CPU.Processor;

namespace HybridCPU_ISE.Tests
{
    public class Lane7RetireAuthorityTests
    {
        [Fact]
        public void ResolveStableRetireOrder_IncludesLane7AfterLiveLsuLanes()
        {
            ProcessorModel.CPU_Core.WriteBackStage writeBackStage = CreateExplicitWriteBackStage(0, 4, 7);
            Span<byte> retireOrder = stackalloc byte[7];

            int retireLaneCount = ProcessorModel.CPU_Core.ResolveStableRetireOrder(writeBackStage, retireOrder);

            Assert.Equal(new byte[] { 0, 4, 7 }, retireOrder[..retireLaneCount].ToArray());
        }

        [Fact]
        public void ResolveRetireEligibleWriteBackLanes_ExcludesFaultedLane7()
        {
            ProcessorModel.CPU_Core.WriteBackStage writeBackStage = CreateExplicitWriteBackStage(7);
            ProcessorModel.CPU_Core.ScalarWriteBackLaneState lane7 = writeBackStage.GetLane(7);
            lane7.HasFault = true;
            writeBackStage.SetLane(7, lane7);

            byte eligibleMask = ProcessorModel.CPU_Core.ResolveRetireEligibleWriteBackLanes(writeBackStage);

            Assert.Equal((byte)0, eligibleMask);
        }

        [Theory]
        [InlineData((byte)7, true)]
        [InlineData((byte)6, false)]
        public void CanRetireLanePrecisely_MatchesLiveAuthoritativeSubset(byte laneIndex, bool expected)
        {
            ProcessorModel.CPU_Core.WriteBackStage writeBackStage = CreateExplicitWriteBackStage(7);

            bool canRetire = ProcessorModel.CPU_Core.CanRetireLanePrecisely(writeBackStage, laneIndex);

            Assert.Equal(expected, canRetire);
        }

        private static ProcessorModel.CPU_Core.WriteBackStage CreateExplicitWriteBackStage(params byte[] occupiedLanes)
        {
            ProcessorModel.CPU_Core.WriteBackStage writeBackStage = new();
            writeBackStage.Clear();
            writeBackStage.Valid = true;
            writeBackStage.UsesExplicitPacketLanes = true;
            writeBackStage.RetainsReferenceSequentialPath = false;
            writeBackStage.MaterializedPhysicalLaneCount = occupiedLanes.Length;

            for (int i = 0; i < occupiedLanes.Length; i++)
            {
                byte laneIndex = occupiedLanes[i];
                ProcessorModel.CPU_Core.ScalarWriteBackLaneState lane = new();
                lane.Clear(laneIndex);
                lane.IsOccupied = true;
                writeBackStage.SetLane(laneIndex, lane);
            }

            return writeBackStage;
        }
    }
}
