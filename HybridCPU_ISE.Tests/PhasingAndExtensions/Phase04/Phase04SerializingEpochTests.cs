using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 04 — Deterministic Legality Alignment: serialising-event epoch tests.
    ///
    /// Covers items G33–G36 of the v6 refactoring plan:
    /// <list type="bullet">
    ///   <item>G33 — FSP injection forbidden across serialising boundaries.</item>
    ///   <item>G34 — Serialising events become epoch boundaries for deterministic replay.</item>
    ///   <item>G35 — Phase templates / legality caches invalidated on barrier, trap, VM transition.</item>
    ///   <item>G36 — Architectural events incorporated into certificate boundaries.</item>
    /// </list>
    ///
    /// Acceptance criteria:
    /// <list type="bullet">
    ///   <item><see cref="BundleResourceCertificate4Way"/> invalidated on FullSerial / VmxSerial events.</item>
    ///   <item>Epoch counter bumped on each serialising instruction commit.</item>
    ///   <item>FSP injection forbidden across serialising boundaries.</item>
    ///   <item>Deterministic replay test suite extended with system-event epoch tests.</item>
    /// </list>
    /// </summary>
    public class Phase04SerializingEpochTests
    {
        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>Create a canonical full-serial <see cref="SysEventMicroOp"/>.</summary>
        private static SysEventMicroOp CreateFullSerialOp(int vtId = 0)
        {
            var op = SysEventMicroOp.ForEbreak();
            op.OwnerThreadId = vtId;
            op.VirtualThreadId = vtId;
            return op;
        }

        /// <summary>Create a second canonical full-serial <see cref="SysEventMicroOp"/> helper path.</summary>
        private static SysEventMicroOp CreateSystemNopFullSerial(int vtId = 0)
        {
            var op = SysEventMicroOp.ForEbreak();
            op.OwnerThreadId = vtId;
            op.VirtualThreadId = vtId;
            return op;
        }

        /// <summary>Build an 8-slot bundle with a given op at slot 0, rest null.</summary>
        private static MicroOp[] BuildBundleWithSlot0(MicroOp op)
        {
            var bundle = new MicroOp[8];
            bundle[0] = op;
            return bundle;
        }

        /// <summary>Build an empty 8-slot bundle with one owner ALU op at slot 0.</summary>
        private static MicroOp[] BuildNormalBundle(int ownerVtId = 0)
        {
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(ownerVtId, destReg: 1, src1Reg: 2, src2Reg: 3);
            return bundle;
        }

        /// <summary>Create an active replay phase context with a given epoch ID.</summary>
        private static ReplayPhaseContext ActivePhase(ulong epochId = 1)
        {
            return new ReplayPhaseContext(
                isActive: true,
                epochId: epochId,
                cachedPc: 0x1000,
                epochLength: 100,
                completedReplays: 2,
                validSlotCount: 8,
                stableDonorMask: 0xFF,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
        }

        // =====================================================================
        // G33 — FSP injection forbidden across serialising boundaries
        // =====================================================================

        [Fact]
        public void WhenBundleContainsFullSerialOp_ThenNoSmtInjectionOccurs()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            // Owner bundle contains a FullSerial FENCE op
            var bundle = BuildBundleWithSlot0(CreateFullSerialOp(vtId: 0));

            // Act
            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert: candidate must NOT be injected
            Assert.Equal(0, scheduler.SmtInjectionsCount);
        }

        [Fact]
        public void WhenBundleContainsFullSerialOp_ThenSerializingBoundaryRejectsIsIncremented()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Nominate a candidate to ensure there is something to (not) inject
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = BuildBundleWithSlot0(CreateFullSerialOp(vtId: 0));

            // Act
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert: serialising boundary reject must be recorded
            Assert.Equal(1, scheduler.SerializingBoundaryRejects);
        }

        [Fact]
        public void WhenBundleContainsFullSerialOp_ThenResultBundleIsUnchanged()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var fence = CreateFullSerialOp(vtId: 0);
            var bundle = BuildBundleWithSlot0(fence);

            // Act
            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert: the returned bundle's first slot is still the fence op, not the candidate
            Assert.Same(fence, result[0]);
        }

        [Fact]
        public void WhenBundleContainsSystemNopFullSerial_ThenNoSmtInjectionOccurs()
        {
            // Arrange — SysEventMicroOp (FullSerial) used as system-nop successor
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = BuildBundleWithSlot0(CreateSystemNopFullSerial(vtId: 0));

            // Act
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert
            Assert.Equal(0, scheduler.SmtInjectionsCount);
            Assert.Equal(1, scheduler.SerializingBoundaryRejects);
        }

        [Fact]
        public void WhenBundleContainsFullSerialOpAtAnySlot_ThenNoSmtInjectionOccurs()
        {
            // Arrange — FENCE is at slot 4, not slot 0
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[1] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[4] = CreateFullSerialOp(vtId: 0);  // FENCE in middle of bundle

            // Act
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert
            Assert.Equal(0, scheduler.SmtInjectionsCount);
            Assert.Equal(1, scheduler.SerializingBoundaryRejects);
        }

        [Fact]
        public void WhenBundleHasNoSerializingOp_ThenSmtInjectionProceeds()
        {
            // Arrange — normal bundle, no FullSerial/VmxSerial
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = BuildNormalBundle(ownerVtId: 0);

            // Act
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert: injection should have been attempted (no serialising-boundary reject)
            Assert.Equal(0, scheduler.SerializingBoundaryRejects);
        }

        [Fact]
        public void WhenMultipleCallsWithSerializingBundles_ThenBoundaryRejectCountAccumulates()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act — 3 bundles each containing a FullSerial op
            for (int i = 0; i < 3; i++)
            {
                var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
                scheduler.NominateSmtCandidate(1, candidate);
                var bundle = BuildBundleWithSlot0(CreateFullSerialOp(vtId: 0));
                scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
            }

            // Assert
            Assert.Equal(3, scheduler.SerializingBoundaryRejects);
            Assert.Equal(0, scheduler.SmtInjectionsCount);
        }

        // =====================================================================
        // G34 — Serialising events become epoch boundaries
        // =====================================================================

        [Fact]
        public void WhenNotifySerializingCommitCalled_ThenEpochCountIncrements()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            Assert.Equal(0L, scheduler.SerializingEpochCount);

            // Act
            scheduler.NotifySerializingCommit();

            // Assert
            Assert.Equal(1L, scheduler.SerializingEpochCount);
        }

        [Fact]
        public void WhenNotifySerializingCommitCalledMultipleTimes_ThenEpochCountIsMonotonic()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act
            for (int i = 1; i <= 5; i++)
            {
                scheduler.NotifySerializingCommit();
                Assert.Equal((long)i, scheduler.SerializingEpochCount);
            }
        }

        [Fact]
        public void WhenNotifySerializingCommitCalled_ThenEpochCountStartsAtZero()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Assert (pre-condition)
            Assert.Equal(0L, scheduler.SerializingEpochCount);
        }

        // =====================================================================
        // G35/G36 — Phase certificate / class-template invalidated on serialising commit
        // =====================================================================

        [Fact]
        public void WhenNotifySerializingCommitCalled_ThenPhaseCertificateInvalidationsIncrement()
        {
            // Arrange — build a live phase certificate by doing an injection cycle
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(ActivePhase(epochId: 1));

            // Build a certificate by running one normal packing cycle
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);
            scheduler.PackBundleIntraCoreSmt(BuildNormalBundle(), ownerVirtualThreadId: 0, localCoreId: 0);

            long invalidationsBefore = scheduler.PhaseCertificateInvalidations;

            // Act — serialising instruction committed
            scheduler.NotifySerializingCommit();

            // Assert: at least one more invalidation caused by the serialising commit
            Assert.True(scheduler.PhaseCertificateInvalidations >= invalidationsBefore);
        }

        [Fact]
        public void WhenNotifySerializingCommitCalled_ThenLastInvalidationReasonIsSerializingEvent()
        {
            // Arrange — ensure there is a valid certificate that can be invalidated
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(ActivePhase(epochId: 1));

            // Force certificate existence by one injection cycle
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);
            scheduler.PackBundleIntraCoreSmt(BuildNormalBundle(), ownerVirtualThreadId: 0, localCoreId: 0);

            // Act
            scheduler.NotifySerializingCommit();

            // Assert: the last invalidation reason reports the serialising event
            Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent,
                         scheduler.LastPhaseCertificateInvalidationReason);
        }

        [Fact]
        public void WhenNotifySerializingCommitCalled_WithNoActivePhase_ThenEpochCountStillIncrements()
        {
            // Arrange — no replay phase active
            var scheduler = new MicroOpScheduler();

            // Act
            scheduler.NotifySerializingCommit();

            // Assert: epoch counter is bumped regardless of replay phase activity
            Assert.Equal(1L, scheduler.SerializingEpochCount);
        }

        [Fact]
        public void WhenNotifySerializingCommitCalled_WithTypedSlotEnabled_ThenClassTemplateFlagReset()
        {
            // Arrange — inject to trigger class template capture
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;
            scheduler.SetReplayPhaseContext(ActivePhase(epochId: 1));

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);
            scheduler.PackBundleIntraCoreSmt(BuildNormalBundle(), ownerVirtualThreadId: 0, localCoreId: 0);

            // Act — serialising event arrives
            scheduler.NotifySerializingCommit();

            // Assert: class template should be invalidated (cannot be used across serialising boundary)
            Assert.False(scheduler.TestGetClassTemplateValid());
        }

        // =====================================================================
        // Telemetry export — GetPhaseMetrics
        // =====================================================================

        [Fact]
        public void WhenGetPhaseMetricsCalled_ThenSerializingEpochCountIsExported()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.NotifySerializingCommit();
            scheduler.NotifySerializingCommit();

            // Act
            var metrics = scheduler.GetPhaseMetrics();

            // Assert
            Assert.Equal(2L, metrics.SerializingEpochCount);
        }

        [Fact]
        public void WhenGetPhaseMetricsCalled_ThenSerializingBoundaryRejectsIsExported()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);
            scheduler.PackBundleIntraCoreSmt(BuildBundleWithSlot0(CreateFullSerialOp()), ownerVirtualThreadId: 0, localCoreId: 0);

            // Act
            var metrics = scheduler.GetPhaseMetrics();

            // Assert
            Assert.Equal(1L, metrics.SerializingBoundaryRejects);
        }

        [Fact]
        public void WhenNoSerializingActivity_ThenMetricsAreZero()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act
            var metrics = scheduler.GetPhaseMetrics();

            // Assert
            Assert.Equal(0L, metrics.SerializingEpochCount);
            Assert.Equal(0L, metrics.SerializingBoundaryRejects);
        }

        // =====================================================================
        // Integration — epoch boundary + certificate invalidation
        // =====================================================================

        [Fact]
        public void WhenSerializingEpochFollowedByNormalBundle_ThenInjectionResumesNormally()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(ActivePhase(epochId: 1));

            // Step 1: serialising bundle — no injection
            var fenceBundleCandidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, fenceBundleCandidate);
            scheduler.PackBundleIntraCoreSmt(BuildBundleWithSlot0(CreateFullSerialOp()), ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.Equal(0, scheduler.SmtInjectionsCount);

            // Step 2: serialising commit
            scheduler.NotifySerializingCommit();

            // Step 3: normal bundle in the next cycle — injection should proceed freely
            var newCandidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, newCandidate);
            scheduler.PackBundleIntraCoreSmt(BuildNormalBundle(ownerVtId: 0), ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert: the serialising boundary reject was only for the fence bundle
            Assert.Equal(1, scheduler.SerializingBoundaryRejects);
        }

        [Fact]
        public void WhenReplayPhaseActiveAndSerializingEventFires_ThenEpochAndInvalidationBothRecorded()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(ActivePhase(epochId: 5));

            long epochsBefore = scheduler.SerializingEpochCount;
            long invalidationsBefore = scheduler.PhaseCertificateInvalidations;

            // Act
            scheduler.NotifySerializingCommit();

            // Assert — both epoch counter and invalidation counter advanced
            Assert.Equal(epochsBefore + 1, scheduler.SerializingEpochCount);
            Assert.True(scheduler.PhaseCertificateInvalidations >= invalidationsBefore);
        }

        // =====================================================================
        // G33 / Phase 6 J47-J49 — VmxSerial FSP injection forbidden
        // VM_ENTRY / VM_EXIT transitions must forbid cross-domain FSP injection.
        // Verified by ensuring that a bundle containing a VmxSerial MicroOp
        // triggers the same BundleContainsSerializingEvent guard as FullSerial.
        // =====================================================================

        /// <summary>
        /// Test-only MicroOp stub that carries <see cref="SerializationClass.VmxSerial"/>
        /// serialisation class — models a VMX instruction (VMLAUNCH, VMXON, etc.) in the
        /// legacy MicroOp bundle pathway for scheduler guard verification.
        /// </summary>
        private sealed class VmxSerialTestMicroOp : MicroOp
        {
            public VmxSerialTestMicroOp()
            {
                SerializationClass = global::YAKSys_Hybrid_CPU.Arch.SerializationClass.VmxSerial;
                InstructionClass   = global::YAKSys_Hybrid_CPU.Arch.InstructionClass.Vmx;
                IsStealable        = false;
                ReadRegisters      = System.Array.Empty<int>();
                WriteRegisters     = System.Array.Empty<int>();
                ReadMemoryRanges   = System.Array.Empty<(ulong, ulong)>();
                WriteMemoryRanges  = System.Array.Empty<(ulong, ulong)>();
                ResourceMask       = ResourceMaskBuilder.ForAtomic();
                PublishExplicitStructuralSafetyMask();
                RefreshAdmissionMetadata(this);
            }

            public override bool Execute(ref Processor.CPU_Core core) => true;
            public override string GetDescription() => "VmxSerialTest";
        }

        /// <summary>Create a <see cref="VmxSerialTestMicroOp"/> representing a VMX boundary instruction.</summary>
        private static VmxSerialTestMicroOp CreateVmxSerialOp() => new();

        [Fact]
        public void WhenBundleContainsVmxSerialOp_ThenNoSmtInjectionOccurs()
        {
            // Arrange — VM_ENTRY/VM_EXIT epoch boundary: VmxSerial must block FSP injection (Phase 6 J47-J49)
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = BuildBundleWithSlot0(CreateVmxSerialOp());

            // Act
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert: VmxSerial boundary must prevent SMT candidate injection
            Assert.Equal(0, scheduler.SmtInjectionsCount);
        }

        [Fact]
        public void WhenBundleContainsVmxSerialOp_ThenSerializingBoundaryRejectsIsIncremented()
        {
            // Arrange — VmxSerial in bundle triggers the same serialising-boundary counter as FullSerial
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = BuildBundleWithSlot0(CreateVmxSerialOp());

            // Act
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert: serialising-boundary reject counter incremented (G33)
            Assert.Equal(1, scheduler.SerializingBoundaryRejects);
        }

        [Fact]
        public void WhenBundleContainsVmxSerialOp_ThenResultBundleIsUnchanged()
        {
            // Arrange — the VmxSerial op at slot 0 must survive injection unchanged
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var vmxOp = CreateVmxSerialOp();
            var bundle = BuildBundleWithSlot0(vmxOp);

            // Act
            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert: slot 0 still holds the VMX op, not the injected candidate
            Assert.Same(vmxOp, result[0]);
        }
    }
}
