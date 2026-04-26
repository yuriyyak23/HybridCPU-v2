// Phase 06: Scheduler / FSP — Metadata-Driven Scheduling
// Covers:
//   - SlotMetadata: construction, defaults, all policy fields
//   - BundleMetadata: construction, FspBoundary, GetSlotMetadata, factory helpers
//   - FspProcessor.TryPilfer: stealable/non-stealable slots, FspBoundary, donor selection, determinism
//   - MicroOp.AdmissionMetadata.IsStealable: producer admission truth for runtime/FSP
//   - MicroOpScheduler.Nominate(): admission-gated FSP admission
//   - SafetyVerifier stealability: admission-driven rule 1

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase06
{
    // ─────────────────────────────────────────────────────────────────────────
    // Test helper: minimal concrete MicroOp for scheduler/FSP tests
    // ─────────────────────────────────────────────────────────────────────────

    internal sealed class Fsp06TestMicroOp : MicroOp
    {
        public Fsp06TestMicroOp(
            int virtualThreadId = 0,
            bool isControlFlow = false,
            bool canBeStolen = true)
        {
            VirtualThreadId = virtualThreadId;
            OwnerThreadId = virtualThreadId;
            IsControlFlow = isControlFlow;
            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();
            Placement = Placement with
            {
                RequiredSlotClass = SlotClass.AluClass,
                PinningKind = SlotPinningKind.ClassFlexible
            };
            IsStealable = canBeStolen;
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;
        public override string GetDescription() => nameof(Fsp06TestMicroOp);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // StealabilityPolicy enum tests
    // ─────────────────────────────────────────────────────────────────────────

    public class StealabilityPolicyEnumTests
    {
        [Fact]
        public void StealabilityPolicy_ExactlyTwoMembers()
            => Assert.Equal(2, Enum.GetValues<StealabilityPolicy>().Length);

        [Fact]
        public void StealabilityPolicy_Stealable_IsZero()
            => Assert.Equal(0, (int)StealabilityPolicy.Stealable);

        [Fact]
        public void StealabilityPolicy_NotStealable_IsOne()
            => Assert.Equal(1, (int)StealabilityPolicy.NotStealable);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BranchHint enum tests
    // ─────────────────────────────────────────────────────────────────────────

    public class BranchHintEnumTests
    {
        [Fact]
        public void BranchHint_ExactlyThreeMembers()
            => Assert.Equal(3, Enum.GetValues<BranchHint>().Length);

        [Fact]
        public void BranchHint_None_IsZero()
            => Assert.Equal(0, (int)BranchHint.None);

        [Theory]
        [InlineData(BranchHint.Likely)]
        [InlineData(BranchHint.Unlikely)]
        public void BranchHint_NonDefaultValues_AreNonZero(BranchHint hint)
            => Assert.NotEqual(0, (int)hint);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LocalityHint enum tests
    // ─────────────────────────────────────────────────────────────────────────

    public class LocalityHintEnumTests
    {
        [Fact]
        public void LocalityHint_ExactlyThreeMembers()
            => Assert.Equal(3, Enum.GetValues<LocalityHint>().Length);

        [Fact]
        public void LocalityHint_None_IsZero()
            => Assert.Equal(0, (int)LocalityHint.None);

        [Theory]
        [InlineData(LocalityHint.Hot)]
        [InlineData(LocalityHint.Cold)]
        public void LocalityHint_NonDefaultValues_AreNonZero(LocalityHint hint)
            => Assert.NotEqual(0, (int)hint);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ThermalHint enum tests
    // ─────────────────────────────────────────────────────────────────────────

    public class ThermalHintEnumTests
    {
        [Fact]
        public void ThermalHint_ExactlyThreeMembers()
            => Assert.Equal(3, Enum.GetValues<ThermalHint>().Length);

        [Fact]
        public void ThermalHint_None_IsZero()
            => Assert.Equal(0, (int)ThermalHint.None);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SlotMetadata record tests
    // ─────────────────────────────────────────────────────────────────────────

    public class SlotMetadataTests
    {
        [Fact]
        public void SlotMetadata_Default_HasStealablePolicy()
            => Assert.Equal(StealabilityPolicy.Stealable, new SlotMetadata().StealabilityPolicy);

        [Fact]
        public void SlotMetadata_Default_HasNoBranchHint()
            => Assert.Equal(BranchHint.None, new SlotMetadata().BranchHint);

        [Fact]
        public void SlotMetadata_Default_HasNoLocalityHint()
            => Assert.Equal(LocalityHint.None, new SlotMetadata().LocalityHint);

        [Fact]
        public void SlotMetadata_Default_HasNoPreferredVt()
            => Assert.Equal(0xFF, new SlotMetadata().PreferredVt);

        [Fact]
        public void SlotMetadata_Default_HasNoDonorVtHint()
            => Assert.Equal(0xFF, new SlotMetadata().DonorVtHint);

        [Fact]
        public void SlotMetadata_DefaultSingleton_HasStealablePolicy()
            => Assert.Equal(StealabilityPolicy.Stealable, SlotMetadata.Default.StealabilityPolicy);

        [Fact]
        public void SlotMetadata_WithNotStealable_CanBeConstructed()
        {
            var meta = new SlotMetadata { StealabilityPolicy = StealabilityPolicy.NotStealable };
            Assert.Equal(StealabilityPolicy.NotStealable, meta.StealabilityPolicy);
        }

        [Fact]
        public void SlotMetadata_AllFields_CanBeSetViaInit()
        {
            var meta = new SlotMetadata
            {
                BranchHint = BranchHint.Likely,
                StealabilityPolicy = StealabilityPolicy.NotStealable,
                LocalityHint = LocalityHint.Hot,
                PreferredVt = 2,
                DonorVtHint = 3,
            };
            Assert.Equal(BranchHint.Likely, meta.BranchHint);
            Assert.Equal(StealabilityPolicy.NotStealable, meta.StealabilityPolicy);
            Assert.Equal(LocalityHint.Hot, meta.LocalityHint);
            Assert.Equal(2, meta.PreferredVt);
            Assert.Equal(3, meta.DonorVtHint);
        }

        [Fact]
        public void SlotMetadata_IsRecord_SupportValueEquality()
        {
            var a = new SlotMetadata { BranchHint = BranchHint.Likely };
            var b = new SlotMetadata { BranchHint = BranchHint.Likely };
            Assert.Equal(a, b);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BundleMetadata record tests
    // ─────────────────────────────────────────────────────────────────────────

    public class BundleMetadataTests
    {
        private static BundleMetadata MakeBundle(bool fspBoundary = false)
        {
            var slots = new SlotMetadata[BundleMetadata.BundleSlotCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = SlotMetadata.Default;
            return new BundleMetadata { FspBoundary = fspBoundary, SlotMetadata = slots };
        }

        [Fact]
        public void BundleMetadata_BundleSlotCount_IsEight()
            => Assert.Equal(8, BundleMetadata.BundleSlotCount);

        [Fact]
        public void BundleMetadata_Default_FspBoundaryIsFalse()
            => Assert.False(MakeBundle().FspBoundary);

        [Fact]
        public void BundleMetadata_Default_BundleThermalHintIsNone()
            => Assert.Equal(ThermalHint.None, MakeBundle().BundleThermalHint);

        [Fact]
        public void BundleMetadata_GetSlotMetadata_ReturnsCorrectSlot()
        {
            var custom = new SlotMetadata { BranchHint = BranchHint.Unlikely };
            var slots = new SlotMetadata[BundleMetadata.BundleSlotCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = SlotMetadata.Default;
            slots[3] = custom;
            var bm = new BundleMetadata { SlotMetadata = slots };
            Assert.Equal(BranchHint.Unlikely, bm.GetSlotMetadata(3).BranchHint);
        }

        [Fact]
        public void BundleMetadata_GetSlotMetadata_OutOfRange_ReturnsDefault()
        {
            var bm = MakeBundle();
            Assert.Equal(SlotMetadata.Default, bm.GetSlotMetadata(99));
        }

        [Fact]
        public void BundleMetadata_GetSlotMetadata_NegativeIndex_ReturnsDefault()
        {
            var bm = MakeBundle();
            Assert.Equal(SlotMetadata.Default, bm.GetSlotMetadata(-1));
        }

        [Fact]
        public void BundleMetadata_CreateAllStealable_AllSlotsAreStealable()
        {
            var bm = BundleMetadata.CreateAllStealable();
            for (int i = 0; i < BundleMetadata.BundleSlotCount; i++)
                Assert.Equal(StealabilityPolicy.Stealable, bm.GetSlotMetadata(i).StealabilityPolicy);
        }

        [Fact]
        public void BundleMetadata_CreateAllStealable_FspBoundaryIsFalse()
            => Assert.False(BundleMetadata.CreateAllStealable().FspBoundary);

        [Fact]
        public void BundleMetadata_CreateFspFence_FspBoundaryIsTrue()
            => Assert.True(BundleMetadata.CreateFspFence().FspBoundary);

        [Fact]
        public void BundleMetadata_CreateFspFence_AllSlotsNotStealable()
        {
            var bm = BundleMetadata.CreateFspFence();
            for (int i = 0; i < BundleMetadata.BundleSlotCount; i++)
                Assert.Equal(StealabilityPolicy.NotStealable, bm.GetSlotMetadata(i).StealabilityPolicy);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MicroOp admission metadata — stealability authority
    // ─────────────────────────────────────────────────────────────────────────

    public class MicroOpAdmissionMetadataStealabilityTests
    {
        [Fact]
        public void WhenExplicitStealabilityIsTrue_ThenAdmissionMetadataIsStealable()
        {
            var op = new Fsp06TestMicroOp(canBeStolen: true);
            Assert.True(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void WhenExplicitStealabilityIsFalse_ThenAdmissionMetadataIsNotStealable()
        {
            var op = new Fsp06TestMicroOp(canBeStolen: false);
            Assert.False(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void WhenStealabilityChanges_ThenAdmissionMetadataRefreshes()
        {
            var op = new Fsp06TestMicroOp(canBeStolen: true);
            Assert.True(op.AdmissionMetadata.IsStealable);

            op.IsStealable = false;

            Assert.False(op.AdmissionMetadata.IsStealable);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FspProcessor.TryPilfer() tests
    // ─────────────────────────────────────────────────────────────────────────

    public class FspProcessorTests
    {
        private readonly FspProcessor _fsp = new();

        private static MicroOp?[] MakeBundle(params MicroOp?[] ops)
        {
            var bundle = new MicroOp?[8];
            for (int i = 0; i < ops.Length && i < 8; i++)
                bundle[i] = ops[i];
            return bundle;
        }

        private static BundleMetadata AllStealable()
            => BundleMetadata.CreateAllStealable();

        private static BundleMetadata AllNotStealable()
            => BundleMetadata.CreateFspFence();

        [Fact]
        public void TryPilfer_NullBundle_ReturnsEmpty()
        {
            var result = _fsp.TryPilfer(null!, AllStealable());
            Assert.False(result.HasPilfers);
        }

        [Fact]
        public void TryPilfer_NullBundleMeta_ReturnsEmpty()
        {
            var bundle = MakeBundle();
            var result = _fsp.TryPilfer(bundle, null!);
            Assert.False(result.HasPilfers);
        }

        [Fact]
        public void TryPilfer_FspBoundarySet_ReturnsEmpty()
        {
            // Even if there are free slots, FspBoundary blocks all pilfering
            var bundle = MakeBundle(); // all null = all free
            var result = _fsp.TryPilfer(bundle, AllNotStealable());
            Assert.False(result.HasPilfers);
        }

        [Fact]
        public void TryPilfer_AllSlotsOccupied_ReturnsEmpty()
        {
            var op = new Fsp06TestMicroOp(virtualThreadId: 0);
            var bundle = MakeBundle(op, op, op, op, op, op, op, op);
            var result = _fsp.TryPilfer(bundle, AllStealable());
            Assert.False(result.HasPilfers);
        }

        [Fact]
        public void TryPilfer_NopSlotIsStealable_FoundInResult()
        {
            var donor = new Fsp06TestMicroOp(virtualThreadId: 1);
            var bundle = MakeBundle(new NopMicroOp(), donor, null, null, null, null, null, null);
            var result = _fsp.TryPilfer(bundle, AllStealable());
            Assert.True(result.HasPilfers);
            // Slot 0 (NOP) is free; slot 2-7 are null-free. Donor VT = 1 (from slot 1)
            Assert.Contains(result.PilferDecisions, d => d.SlotIndex == 0);
        }

        [Fact]
        public void TryPilfer_NullSlotIsStealable_FoundInResult()
        {
            var donor = new Fsp06TestMicroOp(virtualThreadId: 2);
            // Slot 0 null, slot 1 has donor
            var bundle = MakeBundle(null, donor, null, null, null, null, null, null);
            var result = _fsp.TryPilfer(bundle, AllStealable());
            Assert.True(result.HasPilfers);
            // All free slots (0, 2–7) should each pick VT=2 from slot 1
            foreach (var d in result.PilferDecisions)
                Assert.Equal(2, d.DonorVtId);
        }

        [Fact]
        public void TryPilfer_SlotWithNotStealableMetadata_Skipped()
        {
            // Slot 0 is free but marked NotStealable; slot 1 is free and Stealable
            var slots = new SlotMetadata[BundleMetadata.BundleSlotCount];
            slots[0] = new SlotMetadata { StealabilityPolicy = StealabilityPolicy.NotStealable };
            for (int i = 1; i < BundleMetadata.BundleSlotCount; i++)
                slots[i] = SlotMetadata.Default;
            var bm = new BundleMetadata { SlotMetadata = slots };

            var donor = new Fsp06TestMicroOp(virtualThreadId: 3);
            var bundle = MakeBundle(null, null, donor, null, null, null, null, null);

            var result = _fsp.TryPilfer(bundle, bm);
            // Slot 0 (NotStealable) must not appear in decisions
            Assert.DoesNotContain(result.PilferDecisions, d => d.SlotIndex == 0);
            // Slot 1 (Stealable) should appear
            Assert.Contains(result.PilferDecisions, d => d.SlotIndex == 1);
        }

        [Fact]
        public void TryPilfer_DonorVtHintUsed_WhenPresent()
        {
            // Slot 0 is free, with DonorVtHint = 3; donor selection should use hint 3
            var slots = new SlotMetadata[BundleMetadata.BundleSlotCount];
            slots[0] = new SlotMetadata { StealabilityPolicy = StealabilityPolicy.Stealable, DonorVtHint = 3 };
            for (int i = 1; i < BundleMetadata.BundleSlotCount; i++)
                slots[i] = SlotMetadata.Default;
            var bm = new BundleMetadata { SlotMetadata = slots };

            var op = new Fsp06TestMicroOp(virtualThreadId: 1);
            var bundle = MakeBundle(null, op, op, op, op, op, op, op);

            var result = _fsp.TryPilfer(bundle, bm);
            var d0 = Assert.Single(result.PilferDecisions, d => d.SlotIndex == 0);
            Assert.Equal(3, d0.DonorVtId); // explicit hint wins
        }

        [Fact]
        public void TryPilfer_NoDonorAvailable_SlotSkipped()
        {
            // All slots free, no occupied slot to donate — FspProcessor must skip
            var bundle = MakeBundle(); // all null
            var result = _fsp.TryPilfer(bundle, AllStealable());
            Assert.False(result.HasPilfers); // no donor in any slot
        }

        [Fact]
        public void TryPilfer_LowestVtSelected_Deterministically()
        {
            // VT 2 and VT 0 both occupy slots; lowest = 0
            var op0 = new Fsp06TestMicroOp(virtualThreadId: 0);
            var op2 = new Fsp06TestMicroOp(virtualThreadId: 2);
            var bundle = MakeBundle(null, op2, op0, null, null, null, null, null);

            var result = _fsp.TryPilfer(bundle, AllStealable());
            Assert.True(result.HasPilfers);
            foreach (var d in result.PilferDecisions)
                Assert.Equal(0, d.DonorVtId); // VT 0 wins
        }

        [Fact]
        public void TryPilfer_Deterministic_SameInputSameOutput()
        {
            var donor = new Fsp06TestMicroOp(virtualThreadId: 1);
            var bundle = MakeBundle(null, donor, null, null, null, null, null, null);

            var r1 = _fsp.TryPilfer(bundle, AllStealable());
            var r2 = _fsp.TryPilfer(bundle, AllStealable());

            Assert.Equal(r1.PilferCount, r2.PilferCount);
            for (int i = 0; i < r1.PilferCount; i++)
            {
                Assert.Equal(r1.PilferDecisions[i].SlotIndex, r2.PilferDecisions[i].SlotIndex);
                Assert.Equal(r1.PilferDecisions[i].DonorVtId, r2.PilferDecisions[i].DonorVtId);
            }
        }

        [Fact]
        public void TryPilfer_PilferCountMatchesEligibleSlots()
        {
            // 3 free stealable slots, 1 occupied donor
            var donor = new Fsp06TestMicroOp(virtualThreadId: 0);
            var bundle = MakeBundle(null, null, null, donor, null, null, null, null);

            var result = _fsp.TryPilfer(bundle, AllStealable());
            // 7 free slots (0,1,2 + 4,5,6,7) all should be found
            Assert.Equal(7, result.PilferCount);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FspResult struct tests
    // ─────────────────────────────────────────────────────────────────────────

    public class FspResultTests
    {
        [Fact]
        public void FspResult_New_HasNoPilfers()
        {
            var r = new FspResult();
            Assert.False(r.HasPilfers);
            Assert.Equal(0, r.PilferCount);
        }

        [Fact]
        public void FspPilferDecision_Properties_AreStored()
        {
            var d = new FspPilferDecision(3, 2);
            Assert.Equal(3, d.SlotIndex);
            Assert.Equal(2, d.DonorVtId);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MicroOpScheduler.Nominate() — metadata-gated admission
    // ─────────────────────────────────────────────────────────────────────────

    public class SchedulerNominateMetadataTests
    {
        [Fact]
        public void Nominate_WithSlotMetaStealable_AcceptedToPort()
        {
            var scheduler = new MicroOpScheduler();
            var op = new Fsp06TestMicroOp(virtualThreadId: 1, canBeStolen: false);
            op.IsStealable = true;

            scheduler.Nominate(0, op);

            // Verify port was accepted — read test-support accessor
            bool hasCandidate = scheduler.HasNominatedCandidate(0);
            Assert.True(hasCandidate);
        }

        [Fact]
        public void Nominate_WithExplicitStealabilityFalse_RejectedFromPort()
        {
            var scheduler = new MicroOpScheduler();
            var op = new Fsp06TestMicroOp(virtualThreadId: 1, canBeStolen: true);
            op.IsStealable = false;

            scheduler.Nominate(0, op);

            bool hasCandidate = scheduler.HasNominatedCandidate(0);
            Assert.False(hasCandidate);
        }

        [Fact]
        public void Nominate_WithExplicitStealable_DefaultAdmissionTruth_Accepted()
        {
            var scheduler = new MicroOpScheduler();
            var op = new Fsp06TestMicroOp(virtualThreadId: 0, canBeStolen: true);

            scheduler.Nominate(0, op);

            Assert.True(scheduler.HasNominatedCandidate(0));
        }

        [Fact]
        public void Nominate_WithExplicitNotStealable_Rejected()
        {
            var scheduler = new MicroOpScheduler();
            var op = new Fsp06TestMicroOp(virtualThreadId: 0, canBeStolen: false);

            scheduler.Nominate(0, op);

            Assert.False(scheduler.HasNominatedCandidate(0));
        }

        [Fact]
        public void Nominate_ControlFlow_AlwaysRejected_EvenIfExplicitlyStealable()
        {
            var scheduler = new MicroOpScheduler();
            var op = new Fsp06TestMicroOp(virtualThreadId: 0, isControlFlow: true, canBeStolen: true);
            op.IsStealable = true;

            scheduler.Nominate(0, op);

            Assert.False(scheduler.HasNominatedCandidate(0));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SafetyVerifier — metadata-driven stealability rule
    // ─────────────────────────────────────────────────────────────────────────

    public class SafetyVerifierMetadataStealabilityTests
    {
        [Fact]
        public void VerifyInjection_WhenExplicitStealabilityIsFalse_ReturnsFalse()
        {
            var verifier = new SafetyVerifier();
            var candidate = new Fsp06TestMicroOp(virtualThreadId: 1, canBeStolen: true);
            candidate.IsStealable = false;

            var bundle = new MicroOp?[8];
            bundle[0] = new Fsp06TestMicroOp(virtualThreadId: 0);

            // Different thread so cross-thread rule applies
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle!, 1, candidate, 0, 1, default);
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjectionWithProof_WhenExplicitStealabilityIsFalse_ReturnsInvalidCondition()
        {
            var verifier = new SafetyVerifier();
            var candidate = new Fsp06TestMicroOp(virtualThreadId: 1, canBeStolen: true);
            candidate.IsStealable = false;

            var bundle = new MicroOp?[8];
            bundle[0] = new Fsp06TestMicroOp(virtualThreadId: 0);

            verifier.VerifyInjectionWithProof((MicroOp[])bundle, 1, candidate, 0, 1, out var conditions);
            // The FSP non-interference condition should be invalid
            bool fspInvalid = Array.Exists(conditions, c =>
                c.Type == VerificationConditionType.FSPNonInterference && !c.IsValid);
            Assert.True(fspInvalid);
        }

        [Fact]
        public void VerifyInjection_SameThread_BypassesStealabilityCheck()
        {
            // Same thread = always safe, stealability not checked
            var verifier = new SafetyVerifier();
            var candidate = new Fsp06TestMicroOp(virtualThreadId: 0, canBeStolen: false);
            candidate.IsStealable = false;

            var bundle = new MicroOp?[8];
            bundle[0] = new Fsp06TestMicroOp(virtualThreadId: 0);

            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle!, 1, candidate, 0, 0, default);
            Assert.True(result); // same thread bypasses Rule 1
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Policy non-leakage: SlotMetadata / BundleMetadata have no policy terms
    // ─────────────────────────────────────────────────────────────────────────

    public class Phase06MetadataPolicyLeakageTests
    {
        private static readonly string[] PolicyTerms =
        [
            "Priority", "Rank", "Score", "Weight",
            "Fairness", "Quota", "Budget", "Credit", "Starvation",
            "Age", "Reorder", "Dispatch", "Retire",
            "Execute", "Pipeline", "Latency",
            "FinalAdmission", "FinalIssue", "FinalSchedule",
            "Committed", "Binding"
        ];

        [Fact]
        public void SlotMetadata_HasNoPolicyTermsInPropertyNames()
            => AssertNoPolicyTerms(typeof(SlotMetadata));

        [Fact]
        public void BundleMetadata_HasNoPolicyTermsInPropertyNames()
            => AssertNoPolicyTerms(typeof(BundleMetadata));

        [Fact]
        public void FspProcessor_HasNoPolicyTermsInPublicApiPropertyNames()
            => AssertNoPolicyTerms(typeof(FspProcessor));

        private static void AssertNoPolicyTerms(Type t)
        {
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (var term in PolicyTerms)
                {
                    Assert.False(
                        prop.Name.Contains(term, StringComparison.OrdinalIgnoreCase),
                        $"{t.Name}.{prop.Name} contains forbidden policy term '{term}'");
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FSP_FENCE opcode confirmed absent from ISA
    // ─────────────────────────────────────────────────────────────────────────

    public class FspFenceOpcodeAbsenceTests
    {
        [Fact]
        public void FspFence_IsInProhibitedOpcodes_NotInInstructionEnum()
        {
            // FSP_FENCE must not appear as a hardware opcode in InstructionsEnum
            var enumNames = Enum.GetNames(typeof(Processor.CPU_Core.InstructionsEnum));
            Assert.DoesNotContain("Fsp_Fence", enumNames, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("FSP_FENCE", enumNames, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void FspBoundary_IsInBundleMetadata_AsNonArchitecturalField()
        {
            // BundleMetadata.FspBoundary carries FSP fence semantics, not the ISA
            var prop = typeof(BundleMetadata).GetProperty(nameof(BundleMetadata.FspBoundary));
            Assert.NotNull(prop);
            Assert.Equal(typeof(bool), prop!.PropertyType);
        }
    }
}
