using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests
{
    public class Phase09ReplayFlushInvalidationReasonTests
    {
        private static Processor.CPU_Core CreateCoreWithPrimedReplayPhase(ulong totalIterations = 8)
        {
            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x1000);
            core.TestInitializeFSPScheduler();
            core.TestPrimeReplayPhase(
                pc: 0x1000,
                totalIterations: totalIterations,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));
            return core;
        }

        private static Processor.CPU_Core CreatePodBackedCoreWithPrimedReplayPhase(
            out PodController pod,
            ulong totalIterations = 8)
        {
            pod = new PodController(0, 0, new MicroOpScheduler());
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x1000);
            core.TestPrimeReplayPhase(
                pc: 0x1000,
                totalIterations: totalIterations,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));
            return core;
        }

        private static void ConfigureTwoRunnableForegroundVirtualThreads(Processor.CPU_Core core)
        {
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);
            core.WriteVirtualThreadPipelineState(1, PipelineState.Task);
        }

        private static MicroOp?[] CreateIntraCoreSmtClassCapacityMismatchBundle()
        {
            return
            [
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 16, src2Reg: 17),
                MicroOpTestHelper.CreateScalarALU(0, destReg: 2, src1Reg: 18, src2Reg: 19),
                MicroOpTestHelper.CreateScalarALU(0, destReg: 3, src1Reg: 20, src2Reg: 21),
                MicroOpTestHelper.CreateScalarALU(0, destReg: 4, src1Reg: 22, src2Reg: 23),
                MicroOpTestHelper.CreateScalarALU(1, destReg: 5, src1Reg: 24, src2Reg: 25),
                null,
                null,
                null
            ];
        }

        private static ScalarALUMicroOp CreateScopedScalarAlu(
            int virtualThreadId,
            ushort destReg,
            ushort src1Reg,
            ushort src2Reg,
            int ownerContextId,
            byte ownerDomainTag)
        {
            ScalarALUMicroOp microOp =
                MicroOpTestHelper.CreateScalarALU(
                    virtualThreadId,
                    destReg,
                    src1Reg,
                    src2Reg);
            microOp.OwnerContextId = ownerContextId;
            microOp.Placement = microOp.Placement with { DomainTag = ownerDomainTag };
            microOp.InitializeMetadata();
            return microOp;
        }

        private static MicroOp?[] CreateIntraCoreSmtDomainBoundaryBundle(
            int ownerContextId,
            byte ownerDomainTag)
        {
            return
            [
                CreateScopedScalarAlu(0, destReg: 1, src1Reg: 16, src2Reg: 17, ownerContextId, ownerDomainTag),
                CreateScopedScalarAlu(0, destReg: 2, src1Reg: 18, src2Reg: 19, ownerContextId, ownerDomainTag),
                CreateScopedScalarAlu(0, destReg: 3, src1Reg: 20, src2Reg: 21, ownerContextId, ownerDomainTag),
                CreateScopedScalarAlu(0, destReg: 4, src1Reg: 22, src2Reg: 23, ownerContextId, ownerDomainTag),
                CreateScopedScalarAlu(1, destReg: 5, src1Reg: 24, src2Reg: 25, ownerContextId, ownerDomainTag),
                null,
                null,
                null
            ];
        }

        private static void SeedSchedulerClassTemplate(MicroOpScheduler scheduler)
        {
            var capacityState = new SlotClassCapacityState();
            capacityState.InitializeFromLaneMap();
            scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(capacityState));
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(0);
        }

        private static int ComputeClassTemplateDomainScopeId(
            int ownerVirtualThreadId,
            int ownerContextId,
            byte ownerDomainTag)
        {
            if (ownerContextId == ownerVirtualThreadId && ownerDomainTag == 0)
            {
                return ownerContextId;
            }

            var domainScopeHasher = new HardwareHash();
            domainScopeHasher.Initialize();
            domainScopeHasher.Compress((ulong)(uint)ownerVirtualThreadId);
            domainScopeHasher.Compress((ulong)(uint)ownerContextId);
            domainScopeHasher.Compress(ownerDomainTag);
            return unchecked((int)domainScopeHasher.Finalize());
        }

        private static void SeedSchedulerClassTemplateForOwnerScope(
            MicroOpScheduler scheduler,
            MicroOp?[] bundle,
            int ownerVirtualThreadId,
            int ownerContextId,
            byte ownerDomainTag)
        {
            scheduler.TestSetClassCapacityTemplate(
                new ClassCapacityTemplate(
                    SlotClassCapacity.ComputeFromBundle(bundle)));
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(
                ComputeClassTemplateDomainScopeId(
                    ownerVirtualThreadId,
                    ownerContextId,
                    ownerDomainTag));
        }

        private static void PrimeSchedulerReplayCertificate(
            MicroOpScheduler scheduler,
            ReplayPhaseContext phase)
        {
            scheduler.SetReplayPhaseContext(phase);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, destReg: 8, src1Reg: 9, src2Reg: 10));
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
        }

        [Theory]
        [InlineData(AssistInvalidationReason.Trap, ReplayPhaseInvalidationReason.SerializingEvent)]
        [InlineData(AssistInvalidationReason.Fence, ReplayPhaseInvalidationReason.SerializingEvent)]
        [InlineData(AssistInvalidationReason.VmTransition, ReplayPhaseInvalidationReason.SerializingEvent)]
        [InlineData(AssistInvalidationReason.SerializingBoundary, ReplayPhaseInvalidationReason.SerializingEvent)]
        [InlineData(AssistInvalidationReason.PipelineFlush, ReplayPhaseInvalidationReason.Manual)]
        public void FlushPipeline_WhenReplayPhaseIsActive_ThenReplayAndSchedulerPublishMappedInvalidationReason(
            AssistInvalidationReason assistInvalidationReason,
            ReplayPhaseInvalidationReason expectedReplayInvalidationReason)
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();

            core.FlushPipeline(assistInvalidationReason);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = core.TestGetFSPScheduler()!.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(expectedReplayInvalidationReason, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(expectedReplayInvalidationReason, schedulerPhase.LastInvalidationReason);
        }

        [Fact]
        public void InvalidateVliwFetchState_WhenReplayPhaseIsActive_ThenReplayAndSchedulerPublishCertificateMutation()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();

            core.InvalidateVliwFetchState(0x1000);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = core.TestGetFSPScheduler()!.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.CertificateMutation, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.CertificateMutation, schedulerPhase.LastInvalidationReason);
        }

        [Fact]
        public void InvalidateVliwFetchState_WhenReplayPhaseAlreadyInactiveWithSerializingBoundary_ThenSchedulerDoesNotReplayInvalidateAgain()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);

            core.TestHandleRetiredSerializingBoundary();

            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();

            core.InvalidateVliwFetchState(0x1000);

            ReplayPhaseContext replayPhaseAfter = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhaseAfter.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhaseAfter.LastInvalidationReason);
            Assert.Equal(assistInvalidationsBefore, scheduler.AssistInvalidations);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
        }

        [Fact]
        public void TryReplay_WhenRequestedPcMismatchesCachedReplayPhase_ThenReplayAndSchedulerPublishPcMismatch()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);

            Assert.False(core.TestTryReplayFetchAtPc(0x1200));

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.PcMismatch, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.PcMismatch, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.PcMismatch, scheduler.LastPhaseCertificateInvalidationReason);
        }

        [Fact]
        public void EndCycle_WhenReplayPhaseCompletes_ThenReplayAndSchedulerPublishCompleted()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase(totalIterations: 2);
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);

            Assert.True(core.TestTryReplayFetchAtPc(0x1000));
            Assert.True(core.TestTryReplayFetchAtPc(0x1000));

            ReplayPhaseContext drainingPhase = core.GetReplayPhaseContext();
            Assert.True(drainingPhase.IsActive);

            core.TestAdvanceReplayEndCycle();

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.Completed, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.Completed, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.Completed, scheduler.LastPhaseCertificateInvalidationReason);
        }

        [Fact]
        public void PublishCurrentReplayPhase_WhenReplayEpochReloadedBeforeSchedulerRepublish_ThenSchedulerPublishesPhaseMismatchAndExpiresClassTemplate()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            PrimeSchedulerReplayCertificate(scheduler, core.GetReplayPhaseContext());
            Assert.Equal(0, scheduler.PhaseCertificatePhaseMismatchInvalidations);

            SeedSchedulerClassTemplate(scheduler);
            Assert.True(scheduler.TestGetClassTemplateValid());

            long phaseMismatchInvalidationsBefore = scheduler.PhaseCertificatePhaseMismatchInvalidations;
            long classTemplateInvalidationsBefore = scheduler.ClassTemplateInvalidations;
            ReplayPhaseContext schedulerPhaseBeforeReload = scheduler.TestGetReplayPhaseContext();

            core.TestReloadReplayPhaseWithoutSchedulerPublication(
                pc: 0x1400,
                totalIterations: 6,
                MicroOpTestHelper.CreateScalarALU(0, destReg: 7, src1Reg: 8, src2Reg: 9));

            ReplayPhaseContext replayPhaseAfterReload = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhaseBeforeRepublish = scheduler.TestGetReplayPhaseContext();

            Assert.True(replayPhaseAfterReload.IsActive);
            Assert.NotEqual(schedulerPhaseBeforeReload.EpochId, replayPhaseAfterReload.EpochId);
            Assert.Equal(schedulerPhaseBeforeReload.EpochId, schedulerPhaseBeforeRepublish.EpochId);

            core.TestPublishCurrentReplayPhaseToSchedulerIfNeeded();

            ReplayPhaseContext schedulerPhaseAfterRepublish = scheduler.TestGetReplayPhaseContext();

            Assert.True(schedulerPhaseAfterRepublish.IsActive);
            Assert.Equal(replayPhaseAfterReload.EpochId, schedulerPhaseAfterRepublish.EpochId);
            Assert.Equal(replayPhaseAfterReload.CachedPc, schedulerPhaseAfterRepublish.CachedPc);
            Assert.True(scheduler.PhaseCertificatePhaseMismatchInvalidations > phaseMismatchInvalidationsBefore);
            Assert.Equal(ReplayPhaseInvalidationReason.PhaseMismatch, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.False(scheduler.TestGetClassTemplateValid());
            Assert.True(scheduler.ClassTemplateInvalidations > classTemplateInvalidationsBefore);
            Assert.Equal(AssistInvalidationReason.Replay, scheduler.LastAssistInvalidationReason);
        }

        [Fact]
        public void ApplyFspPacking_WhenReplayPhaseAlreadyMatchesScheduler_ThenRepublishDoesNotChurnPhaseMismatchOrExpireClassTemplate()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            core.VectorConfig.FSP_Enabled = 1;

            PrimeSchedulerReplayCertificate(scheduler, core.GetReplayPhaseContext());
            SeedSchedulerClassTemplate(scheduler);

            long phaseMismatchInvalidationsBefore = scheduler.PhaseCertificatePhaseMismatchInvalidations;
            long classTemplateInvalidationsBefore = scheduler.ClassTemplateInvalidations;
            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            ReplayPhaseContext schedulerPhaseBeforeRepublish = scheduler.TestGetReplayPhaseContext();

            core.TestSetDecodedBundle(MicroOpTestHelper.CreateScalarALU(0, destReg: 16, src1Reg: 17, src2Reg: 18));
            core.TestRefreshCurrentFspDerivedIssuePlan();

            ReplayPhaseContext schedulerPhaseAfterRepublish = scheduler.TestGetReplayPhaseContext();

            Assert.True(schedulerPhaseAfterRepublish.IsActive);
            Assert.Equal(schedulerPhaseBeforeRepublish.EpochId, schedulerPhaseAfterRepublish.EpochId);
            Assert.Equal(schedulerPhaseBeforeRepublish.CachedPc, schedulerPhaseAfterRepublish.CachedPc);
            Assert.Equal(phaseMismatchInvalidationsBefore, scheduler.PhaseCertificatePhaseMismatchInvalidations);
            Assert.True(scheduler.TestGetClassTemplateValid());
            Assert.Equal(classTemplateInvalidationsBefore, scheduler.ClassTemplateInvalidations);
            Assert.Equal(assistInvalidationsBefore, scheduler.AssistInvalidations);
        }

        [Fact]
        public void ApplyFspPacking_WhenPodLocalSchedulerMatchesCoreScheduler_ThenReplayPhasePublishesOnlyOnce()
        {
            PodController? originalPod = Processor.Pods[0];
            try
            {
                Processor.CPU_Core core = CreatePodBackedCoreWithPrimedReplayPhase(out PodController pod);
                MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
                core.VectorConfig.FSP_Enabled = 1;

                Assert.Same(pod.Scheduler, scheduler);
                scheduler.TestSetPhaseMetrics(new SchedulerPhaseMetrics());

                core.TestSetDecodedBundle(MicroOpTestHelper.CreateScalarALU(0, destReg: 12, src1Reg: 13, src2Reg: 14));
                core.TestRefreshCurrentFspDerivedIssuePlan();

                ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

                Assert.True(schedulerPhaseAfter.IsActive);
                Assert.Equal(1, scheduler.ReplayAwareCycles);
                Assert.Equal(0, scheduler.ClassTemplateInvalidations);
                Assert.Equal(0, scheduler.AssistInvalidations);
            }
            finally
            {
                Processor.Pods[0] = originalPod!;
            }
        }

        [Fact]
        public void ApplyFspPacking_WhenIntraCoreTypedSlotTemplateSeesLiveCapacityMismatch_ThenInvalidateAsClassCapacityMismatch()
        {
            PodController? originalPod = Processor.Pods[0];
            try
            {
                Processor.CPU_Core core = CreatePodBackedCoreWithPrimedReplayPhase(out PodController pod);
                MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
                ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();

                Assert.Same(pod.Scheduler, scheduler);
                Assert.True(replayPhase.IsActive);

                core.VectorConfig.FSP_Enabled = 1;
                scheduler.TypedSlotEnabled = true;
                ConfigureTwoRunnableForegroundVirtualThreads(core);
                scheduler.TestSetPhaseMetrics(new SchedulerPhaseMetrics());

                SeedSchedulerClassTemplate(scheduler);
                Assert.True(scheduler.TestGetClassTemplateValid());
                Assert.Equal(0, scheduler.TestGetClassTemplateDomainId());

                core.TestSetDecodedBundle(CreateIntraCoreSmtClassCapacityMismatchBundle());
                core.TestRefreshCurrentFspDerivedIssuePlan();

                ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

                Assert.True(schedulerPhaseAfter.IsActive);
                Assert.Equal(replayPhase.EpochId, schedulerPhaseAfter.EpochId);
                Assert.Equal(replayPhase.CachedPc, schedulerPhaseAfter.CachedPc);
                Assert.False(scheduler.TestGetClassTemplateValid());
                Assert.Equal(1, scheduler.ClassTemplateInvalidations);
                Assert.Equal(1, scheduler.ClassTemplateCapacityMismatchInvalidations);
                Assert.Equal(0, scheduler.ClassTemplateDomainInvalidations);
                Assert.Equal(ReplayPhaseInvalidationReason.ClassCapacityMismatch, scheduler.LastPhaseCertificateInvalidationReason);
                Assert.Equal(0, scheduler.PhaseCertificateInvalidations);
                Assert.Equal(0, scheduler.AssistInvalidations);
            }
            finally
            {
                Processor.Pods[0] = originalPod!;
            }
        }

        [Fact]
        public void ApplyFspPacking_WhenClassCapacityMismatchAlreadyInvalidatedTemplate_ThenSecondPassDoesNotChurnCounters()
        {
            PodController? originalPod = Processor.Pods[0];
            try
            {
                Processor.CPU_Core core = CreatePodBackedCoreWithPrimedReplayPhase(out PodController pod);
                MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;

                Assert.Same(pod.Scheduler, scheduler);

                core.VectorConfig.FSP_Enabled = 1;
                scheduler.TypedSlotEnabled = true;
                ConfigureTwoRunnableForegroundVirtualThreads(core);
                scheduler.TestSetPhaseMetrics(new SchedulerPhaseMetrics());

                SeedSchedulerClassTemplate(scheduler);
                core.TestSetDecodedBundle(CreateIntraCoreSmtClassCapacityMismatchBundle());
                core.TestRefreshCurrentFspDerivedIssuePlan();

                long classTemplateInvalidationsAfterFirst = scheduler.ClassTemplateInvalidations;
                long classTemplateCapacityMismatchInvalidationsAfterFirst = scheduler.ClassTemplateCapacityMismatchInvalidations;
                long phaseCertificateInvalidationsAfterFirst = scheduler.PhaseCertificateInvalidations;
                long assistInvalidationsAfterFirst = scheduler.AssistInvalidations;
                ReplayPhaseContext schedulerPhaseAfterFirst = scheduler.TestGetReplayPhaseContext();

                Assert.False(scheduler.TestGetClassTemplateValid());
                Assert.Equal(ReplayPhaseInvalidationReason.ClassCapacityMismatch, scheduler.LastPhaseCertificateInvalidationReason);

                core.TestSetDecodedBundle(CreateIntraCoreSmtClassCapacityMismatchBundle());
                core.TestRefreshCurrentFspDerivedIssuePlan();

                ReplayPhaseContext schedulerPhaseAfterSecond = scheduler.TestGetReplayPhaseContext();

                Assert.False(scheduler.TestGetClassTemplateValid());
                Assert.Equal(classTemplateInvalidationsAfterFirst, scheduler.ClassTemplateInvalidations);
                Assert.Equal(classTemplateCapacityMismatchInvalidationsAfterFirst, scheduler.ClassTemplateCapacityMismatchInvalidations);
                Assert.Equal(phaseCertificateInvalidationsAfterFirst, scheduler.PhaseCertificateInvalidations);
                Assert.Equal(assistInvalidationsAfterFirst, scheduler.AssistInvalidations);
                Assert.Equal(schedulerPhaseAfterFirst.EpochId, schedulerPhaseAfterSecond.EpochId);
                Assert.Equal(schedulerPhaseAfterFirst.CachedPc, schedulerPhaseAfterSecond.CachedPc);
                Assert.True(schedulerPhaseAfterSecond.IsActive);
                Assert.Equal(ReplayPhaseInvalidationReason.ClassCapacityMismatch, scheduler.LastPhaseCertificateInvalidationReason);
            }
            finally
            {
                Processor.Pods[0] = originalPod!;
            }
        }

        [Fact]
        public void ApplyFspPacking_WhenIntraCoreTypedSlotOwnerScopeChangesWithinSameVt_ThenInvalidateAsDomainBoundary()
        {
            PodController? originalPod = Processor.Pods[0];
            try
            {
                Processor.CPU_Core core = CreatePodBackedCoreWithPrimedReplayPhase(out PodController pod);
                MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
                ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();

                Assert.Same(pod.Scheduler, scheduler);
                Assert.True(replayPhase.IsActive);

                core.VectorConfig.FSP_Enabled = 1;
                scheduler.TypedSlotEnabled = true;
                ConfigureTwoRunnableForegroundVirtualThreads(core);

                SeedSchedulerClassTemplateForOwnerScope(
                    scheduler,
                    CreateIntraCoreSmtDomainBoundaryBundle(ownerContextId: 7, ownerDomainTag: 3),
                    ownerVirtualThreadId: 0,
                    ownerContextId: 7,
                    ownerDomainTag: 3);

                scheduler.TestSetPhaseMetrics(new SchedulerPhaseMetrics());

                core.TestSetDecodedBundle(
                    CreateIntraCoreSmtDomainBoundaryBundle(ownerContextId: 11, ownerDomainTag: 5));
                core.TestRefreshCurrentFspDerivedIssuePlan();

                ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

                Assert.True(schedulerPhaseAfter.IsActive);
                Assert.Equal(replayPhase.EpochId, schedulerPhaseAfter.EpochId);
                Assert.Equal(replayPhase.CachedPc, schedulerPhaseAfter.CachedPc);
                Assert.False(scheduler.TestGetClassTemplateValid());
                Assert.Equal(1, scheduler.ClassTemplateInvalidations);
                Assert.Equal(1, scheduler.ClassTemplateDomainInvalidations);
                Assert.Equal(0, scheduler.ClassTemplateCapacityMismatchInvalidations);
                Assert.Equal(ReplayPhaseInvalidationReason.DomainBoundary, scheduler.LastPhaseCertificateInvalidationReason);
                Assert.Equal(0, scheduler.PhaseCertificateInvalidations);
                Assert.Equal(0, scheduler.AssistInvalidations);
            }
            finally
            {
                Processor.Pods[0] = originalPod!;
            }
        }

        [Fact]
        public void ApplyFspPacking_WhenDomainBoundaryAlreadyInvalidatedTemplate_ThenSecondPassDoesNotChurnCounters()
        {
            PodController? originalPod = Processor.Pods[0];
            try
            {
                Processor.CPU_Core core = CreatePodBackedCoreWithPrimedReplayPhase(out PodController pod);
                MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;

                Assert.Same(pod.Scheduler, scheduler);

                core.VectorConfig.FSP_Enabled = 1;
                scheduler.TypedSlotEnabled = true;
                ConfigureTwoRunnableForegroundVirtualThreads(core);

                SeedSchedulerClassTemplateForOwnerScope(
                    scheduler,
                    CreateIntraCoreSmtDomainBoundaryBundle(ownerContextId: 7, ownerDomainTag: 3),
                    ownerVirtualThreadId: 0,
                    ownerContextId: 7,
                    ownerDomainTag: 3);

                scheduler.TestSetPhaseMetrics(new SchedulerPhaseMetrics());

                core.TestSetDecodedBundle(
                    CreateIntraCoreSmtDomainBoundaryBundle(ownerContextId: 11, ownerDomainTag: 5));
                core.TestRefreshCurrentFspDerivedIssuePlan();

                long classTemplateInvalidationsAfterFirst = scheduler.ClassTemplateInvalidations;
                long classTemplateDomainInvalidationsAfterFirst = scheduler.ClassTemplateDomainInvalidations;
                long phaseCertificateInvalidationsAfterFirst = scheduler.PhaseCertificateInvalidations;
                long assistInvalidationsAfterFirst = scheduler.AssistInvalidations;
                ReplayPhaseContext schedulerPhaseAfterFirst = scheduler.TestGetReplayPhaseContext();

                Assert.False(scheduler.TestGetClassTemplateValid());
                Assert.Equal(ReplayPhaseInvalidationReason.DomainBoundary, scheduler.LastPhaseCertificateInvalidationReason);

                core.TestSetDecodedBundle(
                    CreateIntraCoreSmtDomainBoundaryBundle(ownerContextId: 11, ownerDomainTag: 5));
                core.TestRefreshCurrentFspDerivedIssuePlan();

                ReplayPhaseContext schedulerPhaseAfterSecond = scheduler.TestGetReplayPhaseContext();

                Assert.False(scheduler.TestGetClassTemplateValid());
                Assert.Equal(classTemplateInvalidationsAfterFirst, scheduler.ClassTemplateInvalidations);
                Assert.Equal(classTemplateDomainInvalidationsAfterFirst, scheduler.ClassTemplateDomainInvalidations);
                Assert.Equal(0, scheduler.ClassTemplateCapacityMismatchInvalidations);
                Assert.Equal(phaseCertificateInvalidationsAfterFirst, scheduler.PhaseCertificateInvalidations);
                Assert.Equal(assistInvalidationsAfterFirst, scheduler.AssistInvalidations);
                Assert.Equal(schedulerPhaseAfterFirst.EpochId, schedulerPhaseAfterSecond.EpochId);
                Assert.Equal(schedulerPhaseAfterFirst.CachedPc, schedulerPhaseAfterSecond.CachedPc);
                Assert.True(schedulerPhaseAfterSecond.IsActive);
                Assert.Equal(ReplayPhaseInvalidationReason.DomainBoundary, scheduler.LastPhaseCertificateInvalidationReason);
            }
            finally
            {
                Processor.Pods[0] = originalPod!;
            }
        }

        [Fact]
        public void ApplyFspPacking_WhenReplayPhaseAlreadyInactiveWithCompletedReason_ThenSchedulerDoesNotReplayInvalidateAgain()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase(totalIterations: 2);
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            core.VectorConfig.FSP_Enabled = 1;
            SeedSchedulerClassTemplate(scheduler);

            Assert.True(core.TestTryReplayFetchAtPc(0x1000));
            Assert.True(core.TestTryReplayFetchAtPc(0x1000));
            core.TestAdvanceReplayEndCycle();

            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();

            core.TestSetDecodedBundle(MicroOpTestHelper.CreateScalarALU(0, destReg: 20, src1Reg: 21, src2Reg: 22));
            core.TestRefreshCurrentFspDerivedIssuePlan();

            ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

            Assert.False(schedulerPhaseAfter.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.Completed, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(assistInvalidationsBefore, scheduler.AssistInvalidations);
            Assert.Equal(ReplayPhaseInvalidationReason.Completed, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.Replay, scheduler.LastAssistInvalidationReason);
        }

        [Fact]
        public void ApplyFspPacking_WhenReplayPhaseAlreadyInactiveWithCertificateMutation_ThenSchedulerDoesNotReplayInvalidateAgain()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            core.VectorConfig.FSP_Enabled = 1;
            SeedSchedulerClassTemplate(scheduler);

            core.InvalidateVliwFetchState(0x1000);

            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();

            core.TestSetDecodedBundle(MicroOpTestHelper.CreateScalarALU(0, destReg: 24, src1Reg: 25, src2Reg: 26));
            core.TestRefreshCurrentFspDerivedIssuePlan();

            ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

            Assert.False(schedulerPhaseAfter.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.CertificateMutation, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(assistInvalidationsBefore, scheduler.AssistInvalidations);
            Assert.Equal(ReplayPhaseInvalidationReason.CertificateMutation, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.Replay, scheduler.LastAssistInvalidationReason);
        }

        [Fact]
        public void ApplyFspPacking_WhenReplayPhaseAlreadyInactiveWithPcMismatch_ThenSchedulerDoesNotReplayInvalidateAgain()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            core.VectorConfig.FSP_Enabled = 1;
            SeedSchedulerClassTemplate(scheduler);

            Assert.False(core.TestTryReplayFetchAtPc(0x1200));

            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();

            core.TestSetDecodedBundle(MicroOpTestHelper.CreateScalarALU(0, destReg: 28, src1Reg: 29, src2Reg: 30));
            core.TestRefreshCurrentFspDerivedIssuePlan();

            ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

            Assert.False(schedulerPhaseAfter.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.PcMismatch, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(assistInvalidationsBefore, scheduler.AssistInvalidations);
            Assert.Equal(ReplayPhaseInvalidationReason.PcMismatch, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.Replay, scheduler.LastAssistInvalidationReason);
        }

        [Fact]
        public void ApplyFspPacking_WhenReplayPhaseAlreadyInactiveWithManualFlushReason_ThenSchedulerDoesNotReplayInvalidateAgain()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            core.VectorConfig.FSP_Enabled = 1;
            SeedSchedulerClassTemplate(scheduler);

            core.FlushPipeline(AssistInvalidationReason.PipelineFlush);

            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();

            core.TestSetDecodedBundle(MicroOpTestHelper.CreateScalarALU(0, destReg: 12, src1Reg: 13, src2Reg: 14));
            core.TestRefreshCurrentFspDerivedIssuePlan();

            ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

            Assert.False(schedulerPhaseAfter.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(assistInvalidationsBefore, scheduler.AssistInvalidations);
            Assert.Equal(ReplayPhaseInvalidationReason.Manual, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(AssistInvalidationReason.PipelineFlush, scheduler.LastAssistInvalidationReason);
        }

        [Theory]
        [InlineData(AssistInvalidationReason.Trap)]
        [InlineData(AssistInvalidationReason.Fence)]
        [InlineData(AssistInvalidationReason.VmTransition)]
        public void ApplyFspPacking_WhenReplayPhaseAlreadyInactiveWithFlushSerializingReason_ThenSchedulerDoesNotReplayInvalidateAgain(
            AssistInvalidationReason assistInvalidationReason)
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            core.VectorConfig.FSP_Enabled = 1;
            SeedSchedulerClassTemplate(scheduler);

            core.FlushPipeline(assistInvalidationReason);

            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();

            core.TestSetDecodedBundle(MicroOpTestHelper.CreateScalarALU(0, destReg: 12, src1Reg: 13, src2Reg: 14));
            core.TestRefreshCurrentFspDerivedIssuePlan();

            ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

            Assert.False(schedulerPhaseAfter.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(assistInvalidationsBefore, scheduler.AssistInvalidations);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(assistInvalidationReason, scheduler.LastAssistInvalidationReason);
        }

        [Fact]
        public void RetiredSerializingBoundary_WhenReplayPhaseIsActive_ThenReplayAndSchedulerPublishSerializingEvent()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;

            core.TestHandleRetiredSerializingBoundary();

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(assistInvalidationsBefore + 1, scheduler.AssistInvalidations);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
        }

        [Fact]
        public void RetiredSerializingBoundary_WhenAssistBoundaryWasAlreadyHandled_ThenSchedulerDoesNotDoubleInvalidateAssist()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            long serializingEpochCountBefore = scheduler.SerializingEpochCount;

            core.TestHandleRetiredSerializingBoundary(assistBoundaryKilledThisRetireWindow: true);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
            Assert.Equal(assistInvalidationsBefore, scheduler.AssistInvalidations);
        }

        [Fact]
        public void ApplyRetiredVmxEffect_OnInactiveGuestVt_ThenReplayAndSchedulerPublishSerializingEventWithoutRedirectingActiveFrontend()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            ulong activeLivePcBefore = core.ReadActiveLivePc();

            core.WriteCommittedPc(2, 0x2200);
            core.WriteCommittedArch(2, 2, 0x3300);
            core.WriteVirtualThreadPipelineState(2, PipelineState.GuestExecution);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x1002);
            core.Vmcs.WriteFieldValue(VmcsField.HostPc, unchecked((long)0x6600UL));
            core.Vmcs.WriteFieldValue(VmcsField.HostSp, unchecked((long)0x7700UL));

            VmxRetireOutcome outcome = core.ApplyRetiredVmxEffectForTesting(
                VmxRetireEffect.Control(
                    VmxOperationKind.VmxOff,
                    exitGuestContextOnRetire: true),
                virtualThreadId: 2);

            ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
            ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

            Assert.True(outcome.FlushesPipeline);
            Assert.False(replayPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
            Assert.False(schedulerPhase.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(activeLivePcBefore, core.ReadActiveLivePc());
            Assert.Equal(AssistInvalidationReason.VmTransition, scheduler.LastAssistInvalidationReason);
        }

        [Fact]
        public void ApplyFspPacking_WhenReplayPhaseAlreadyInactiveWithVmxTransitionReason_ThenSchedulerDoesNotReplayInvalidateAgain()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            core.VectorConfig.FSP_Enabled = 1;
            ulong activeLivePcBefore = core.ReadActiveLivePc();

            core.WriteCommittedPc(2, 0x2200);
            core.WriteCommittedArch(2, 2, 0x3300);
            core.WriteVirtualThreadPipelineState(2, PipelineState.GuestExecution);
            core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
            core.Vmcs.LoadPointer(0x1002);
            core.Vmcs.WriteFieldValue(VmcsField.HostPc, unchecked((long)0x6600UL));
            core.Vmcs.WriteFieldValue(VmcsField.HostSp, unchecked((long)0x7700UL));

            VmxRetireOutcome outcome = core.ApplyRetiredVmxEffectForTesting(
                VmxRetireEffect.Control(
                    VmxOperationKind.VmxOff,
                    exitGuestContextOnRetire: true),
                virtualThreadId: 2);

            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();

            core.TestSetDecodedBundle(MicroOpTestHelper.CreateScalarALU(0, destReg: 12, src1Reg: 13, src2Reg: 14));
            core.TestRefreshCurrentFspDerivedIssuePlan();

            ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

            Assert.True(outcome.FlushesPipeline);
            Assert.False(schedulerPhaseAfter.IsActive);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(assistInvalidationsBefore, scheduler.AssistInvalidations);
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
            Assert.Equal(activeLivePcBefore, core.ReadActiveLivePc());
            Assert.Equal(AssistInvalidationReason.VmTransition, scheduler.LastAssistInvalidationReason);
        }

        [Fact]
        public void ApplyFspPacking_WhenReplayPhaseAlreadyInactiveWithSerializingBoundary_ThenSchedulerDoesNotReplayInvalidateAgain()
        {
            Processor.CPU_Core core = CreateCoreWithPrimedReplayPhase();
            MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
            SeedSchedulerClassTemplate(scheduler);
            core.VectorConfig.FSP_Enabled = 1;

            core.TestHandleRetiredSerializingBoundary();

            long assistInvalidationsBefore = scheduler.AssistInvalidations;
            ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();

            core.TestSetDecodedBundle(MicroOpTestHelper.CreateScalarALU(0, destReg: 4, src1Reg: 5, src2Reg: 6));
            core.TestRefreshCurrentFspDerivedIssuePlan();

            ReplayPhaseContext schedulerPhaseAfter = scheduler.TestGetReplayPhaseContext();

            Assert.Equal(assistInvalidationsBefore, scheduler.AssistInvalidations);
            Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhaseAfter.LastInvalidationReason);
            Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
        }
    }
}
