using HybridCPU_ISE.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    public class ReplayAwareFspDeterminismTests
    {
        [Fact]
        public void WhenReplayAwareFspScenarioRepeatedThenReplayEvidenceRemainsDeterministic()
        {
            TraceSink baseline = RunScenario();
            TraceSink candidate = RunScenario();

            ReplayDeterminismReport report = ReplayEngine.CompareRepeatedRuns(baseline, candidate);

            Assert.True(report.IsDeterministic, report.Describe());
            Assert.True(report.ComparedReplayEvents > 0);
        }

        private static TraceSink RunScenario()
        {
            var trace = new TraceSink(TraceFormat.JSON, "phase3-replay-aware-determinism.json");
            trace.SetEnabled(true);
            trace.SetLevel(TraceLevel.Full);

            var scheduler = new MicroOpScheduler();
            var phase = new ReplayPhaseContext(
                isActive: true,
                epochId: 31,
                cachedPc: 0x5000,
                epochLength: 10,
                completedReplays: 3,
                validSlotCount: 5,
                stableDonorMask: 0xE0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);

            scheduler.SetReplayPhaseContext(phase);

            for (int cycle = 0; cycle < 2; cycle++)
            {
                var bundle = new MicroOp[8];
                for (int i = 0; i < 5; i++)
                {
                    bundle[i] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(i * 3 + 1), (ushort)(32 + i), (ushort)(48 + i));
                }

                scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11));
                scheduler.NominateSmtCandidate(2, MicroOpTestHelper.CreateScalarALU(2, 17, 18, 19));
                scheduler.NominateSmtCandidate(3, MicroOpTestHelper.CreateScalarALU(3, 25, 26, 27));
                scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

                SchedulerPhaseMetrics metrics = scheduler.GetPhaseMetrics();
                trace.RecordPhaseAwareState(
                    new FullStateTraceEvent
                    {
                        ThreadId = 0,
                        CycleNumber = cycle,
                        BundleId = cycle,
                        OpIndex = 0,
                        Opcode = 0,
                        PipelineStage = "CYCLE",
                        CurrentFSPPolicy = "ReplayAwarePhase1.DenseTimeline"
                    },
                    phase,
                    metrics,
                    phaseCertificateTemplateReusable: metrics.PhaseCertificateReadyHits > 0);
            }

            return trace;
        }
    }
}
