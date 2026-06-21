using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;
using HybridCPU_ISE.Core;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 09 – Stage 7 Agreement Extension tests.
    /// Validates TypedSlotBundleFacts, AgreementViolationReport,
    /// ValidateTypedSlotFacts, VerifyTypedSlotAgreement,
    /// ProcessorCompilerBridge extension, ReplayDeterminismReport extension,
    /// and backward compatibility with pre-Phase 8 bundles.
    /// </summary>
    public class Stage7AgreementTests
    {
        #region Helpers

        private static MicroOp CreateAluOp(int vtId = 1, SlotPinningKind pinning = SlotPinningKind.ClassFlexible, byte pinnedLane = 0)
        {
            var op = MicroOpTestHelper.CreateScalarALU(vtId, destReg: 20, src1Reg: 21, src2Reg: 22);
            op.Placement = op.Placement with
            {
                RequiredSlotClass = SlotClass.AluClass,
                PinningKind = pinning,
                PinnedLaneId = pinning == SlotPinningKind.HardPinned ? pinnedLane : (byte)0
            };
            return op;
        }

        private static MicroOp CreateLsuOp(int vtId = 1)
        {
            var op = MicroOpTestHelper.CreateLoad(vtId, destReg: 30, address: 0x1000);
            op.Placement = op.Placement with
            {
                RequiredSlotClass = SlotClass.LsuClass,
                PinningKind = SlotPinningKind.ClassFlexible
            };
            return op;
        }

        private static MicroOp CreateDmaOp(int vtId = 1)
        {
            var op = MicroOpTestHelper.CreateScalarALU(vtId, destReg: 40, src1Reg: 41, src2Reg: 42);
            op.Placement = op.Placement with
            {
                RequiredSlotClass = SlotClass.DmaStreamClass,
                PinningKind = SlotPinningKind.ClassFlexible
            };
            return op;
        }

        private static MicroOp CreateBranchOp(int vtId = 1)
        {
            var op = MicroOpTestHelper.CreateScalarALU(vtId, destReg: 50, src1Reg: 51, src2Reg: 52);
            op.Placement = op.Placement with
            {
                RequiredSlotClass = SlotClass.BranchControl,
                PinningKind = SlotPinningKind.ClassFlexible
            };
            return op;
        }

        private static ProcessorCompilerBridge CreateHandshakenBridge(string producerSurface = "Stage7AgreementTests")
        {
            var bridge = new ProcessorCompilerBridge();
            bridge.DeclareCompilerContractVersion(CompilerContract.Version, producerSurface);
            return bridge;
        }

        private static SlotClassCapacityState CreateInitializedCapacity()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            return state;
        }

        #endregion

        #region TypedSlotBundleFacts — IsEmpty / default

        [Fact]
        public void DefaultFacts_IsEmpty_ReturnsTrue()
        {
            var facts = default(TypedSlotBundleFacts);
            Assert.True(facts.IsEmpty);
        }

        [Fact]
        public void FactsWithAluCount_IsEmpty_ReturnsFalse()
        {
            var facts = new TypedSlotBundleFacts { AluCount = 1, FlexibleOpCount = 1 };
            Assert.False(facts.IsEmpty);
        }

        #endregion

        #region TypedSlotBundleFacts — FromBundle

        [Fact]
        public void FromBundle_EmptyBundle_ReturnsEmptyFacts()
        {
            var bundle = new MicroOp?[8];
            var facts = TypedSlotBundleFacts.FromBundle(bundle);
            Assert.True(facts.IsEmpty);
        }

        [Fact]
        public void FromBundle_SingleAluOp_CountsCorrectly()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();

            var facts = TypedSlotBundleFacts.FromBundle(bundle);

            Assert.Equal(1, facts.AluCount);
            Assert.Equal(0, facts.LsuCount);
            Assert.Equal(1, facts.FlexibleOpCount);
            Assert.Equal(0, facts.PinnedOpCount);
            Assert.Equal(SlotClass.AluClass, facts.Slot0Class);
            Assert.False(facts.IsEmpty);
        }

        [Fact]
        public void FromBundle_MixedOps_CountsAllClasses()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();
            bundle[1] = CreateAluOp();
            bundle[4] = CreateLsuOp();
            bundle[6] = CreateDmaOp();
            bundle[7] = CreateBranchOp();

            var facts = TypedSlotBundleFacts.FromBundle(bundle);

            Assert.Equal(2, facts.AluCount);
            Assert.Equal(1, facts.LsuCount);
            Assert.Equal(1, facts.DmaStreamCount);
            Assert.Equal(1, facts.BranchControlCount);
            Assert.Equal(5, facts.FlexibleOpCount);
            Assert.Equal(0, facts.PinnedOpCount);
        }

        [Fact]
        public void FromBundle_HardPinnedOp_SetsPinningMaskBit()
        {
            var bundle = new MicroOp?[8];
            bundle[2] = CreateAluOp(pinning: SlotPinningKind.HardPinned, pinnedLane: 2);

            var facts = TypedSlotBundleFacts.FromBundle(bundle);

            Assert.Equal(1, facts.PinnedOpCount);
            Assert.Equal(0, facts.FlexibleOpCount);
            Assert.True(facts.IsSlotPinned(2));
            Assert.False(facts.IsSlotPinned(0));
        }

        [Fact]
        public void FromBundle_GetSlotClass_ReturnsCorrectPerSlot()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();
            bundle[4] = CreateLsuOp();

            var facts = TypedSlotBundleFacts.FromBundle(bundle);

            Assert.Equal(SlotClass.AluClass, facts.GetSlotClass(0));
            Assert.Equal(SlotClass.Unclassified, facts.GetSlotClass(1));
            Assert.Equal(SlotClass.LsuClass, facts.GetSlotClass(4));
        }

        #endregion

        #region ValidateTypedSlotFacts — correct facts

        [Fact]
        public void ValidateTypedSlotFacts_CorrectFacts_ReturnsTrue()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();
            bundle[4] = CreateLsuOp();

            var facts = TypedSlotBundleFacts.FromBundle(bundle);

            Assert.True(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
        }

        [Fact]
        public void ValidateTypedSlotFacts_EmptyFacts_ReturnsTrue()
        {
            var bundle = new MicroOp?[8];
            var facts = default(TypedSlotBundleFacts);

            Assert.True(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
        }

        [Fact]
        public void ValidateTypedSlotFacts_EmptyBundle_WithEmptyFacts_ReturnsTrue()
        {
            var bundle = new MicroOp?[8];
            var facts = TypedSlotBundleFacts.FromBundle(bundle);

            Assert.True(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
        }

        #endregion

        #region ValidateTypedSlotFacts — capacity exceeded

        [Fact]
        public void ValidateTypedSlotFacts_AluCountExceedsCapacity_ReturnsFalse()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();

            // Construct facts with inflated AluCount exceeding capacity (4)
            var facts = new TypedSlotBundleFacts
            {
                Slot0Class = SlotClass.AluClass,
                Slot1Class = SlotClass.Unclassified,
                Slot2Class = SlotClass.Unclassified,
                Slot3Class = SlotClass.Unclassified,
                Slot4Class = SlotClass.Unclassified,
                Slot5Class = SlotClass.Unclassified,
                Slot6Class = SlotClass.Unclassified,
                Slot7Class = SlotClass.Unclassified,
                AluCount = 5, // exceeds capacity of 4
                FlexibleOpCount = 1,
                PinnedOpCount = 0
            };

            Assert.False(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
        }

        #endregion

        #region ValidateTypedSlotFacts — duplicate pinned lanes

        [Fact]
        public void ValidateTypedSlotFacts_DuplicatePinnedLanes_ReturnsFalse()
        {
            var op1 = CreateAluOp(pinning: SlotPinningKind.HardPinned, pinnedLane: 0);
            var op2 = MicroOpTestHelper.CreateScalarALU(1, destReg: 25, src1Reg: 26, src2Reg: 27);
            op2.Placement = op2.Placement with
            {
                RequiredSlotClass = SlotClass.AluClass,
                PinningKind = SlotPinningKind.HardPinned,
                PinnedLaneId = 0
            }; // duplicate lane

            var bundle = new MicroOp?[8];
            bundle[0] = op1;
            bundle[1] = op2;

            // Build facts from bundle — FromBundle will produce correct facts,
            // but the bundle itself has duplicate pinned lanes (both PinnedLaneId=0)
            var facts = TypedSlotBundleFacts.FromBundle(bundle);

            Assert.False(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
        }

        #endregion

        #region ValidateTypedSlotFacts — class count mismatch

        [Fact]
        public void ValidateTypedSlotFacts_ClassCountMismatch_ReturnsFalse()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();

            // Facts claiming 2 ALU ops but bundle has only 1
            var facts = new TypedSlotBundleFacts
            {
                Slot0Class = SlotClass.AluClass,
                Slot1Class = SlotClass.Unclassified,
                Slot2Class = SlotClass.Unclassified,
                Slot3Class = SlotClass.Unclassified,
                Slot4Class = SlotClass.Unclassified,
                Slot5Class = SlotClass.Unclassified,
                Slot6Class = SlotClass.Unclassified,
                Slot7Class = SlotClass.Unclassified,
                AluCount = 2, // mismatch: actual is 1
                FlexibleOpCount = 1,
                PinnedOpCount = 0
            };

            Assert.False(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
        }

        #endregion

        #region ValidateTypedSlotFacts — op count mismatch

        [Fact]
        public void ValidateTypedSlotFacts_PinnedFlexibleCountMismatch_ReturnsFalse()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();

            // Facts with wrong FlexibleOpCount
            var facts = new TypedSlotBundleFacts
            {
                Slot0Class = SlotClass.AluClass,
                Slot1Class = SlotClass.Unclassified,
                Slot2Class = SlotClass.Unclassified,
                Slot3Class = SlotClass.Unclassified,
                Slot4Class = SlotClass.Unclassified,
                Slot5Class = SlotClass.Unclassified,
                Slot6Class = SlotClass.Unclassified,
                Slot7Class = SlotClass.Unclassified,
                AluCount = 1,
                FlexibleOpCount = 2, // mismatch: actual flexible is 1
                PinnedOpCount = 0
            };

            Assert.False(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
        }

        #endregion

        #region ValidateTypedSlotFacts — slot class mismatch

        [Fact]
        public void ValidateTypedSlotFacts_SlotClassMismatch_ReturnsFalse()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();

            // Facts declaring slot 0 as LsuClass but actual op is AluClass
            var facts = new TypedSlotBundleFacts
            {
                Slot0Class = SlotClass.LsuClass, // mismatch: actual is AluClass
                Slot1Class = SlotClass.Unclassified,
                Slot2Class = SlotClass.Unclassified,
                Slot3Class = SlotClass.Unclassified,
                Slot4Class = SlotClass.Unclassified,
                Slot5Class = SlotClass.Unclassified,
                Slot6Class = SlotClass.Unclassified,
                Slot7Class = SlotClass.Unclassified,
                AluCount = 1,
                FlexibleOpCount = 1,
                PinnedOpCount = 0
            };

            Assert.False(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
        }

        #endregion

        #region AgreementViolationReport — structural vs dynamic

        [Theory]
        [InlineData(TypedSlotRejectReason.StaticClassOvercommit, true)]
        [InlineData(TypedSlotRejectReason.PinnedLaneConflict, true)]
        public void IsStructuralReject_StructuralReasons_ReturnsTrue(TypedSlotRejectReason reason, bool expected)
        {
            Assert.Equal(expected, AgreementViolationReport.IsStructuralReject(reason));
        }

        [Theory]
        [InlineData(TypedSlotRejectReason.ScoreboardReject)]
        [InlineData(TypedSlotRejectReason.BankPendingReject)]
        [InlineData(TypedSlotRejectReason.AssistQuotaReject)]
        [InlineData(TypedSlotRejectReason.AssistBackpressureReject)]
        [InlineData(TypedSlotRejectReason.SpeculationBudgetReject)]
        [InlineData(TypedSlotRejectReason.DomainReject)]
        [InlineData(TypedSlotRejectReason.DynamicClassExhaustion)]
        public void IsDynamicReject_DynamicReasons_ReturnsTrue(TypedSlotRejectReason reason)
        {
            Assert.True(AgreementViolationReport.IsDynamicReject(reason));
        }

        [Theory]
        [InlineData(TypedSlotRejectReason.StaticClassOvercommit)]
        [InlineData(TypedSlotRejectReason.PinnedLaneConflict)]
        public void IsDynamicReject_StructuralReasons_ReturnsFalse(TypedSlotRejectReason reason)
        {
            Assert.False(AgreementViolationReport.IsDynamicReject(reason));
        }

        [Fact]
        public void AgreementViolationReport_StructuralMismatch_CorrectlyClassified()
        {
            var report = new AgreementViolationReport
            {
                CompilerFacts = new TypedSlotBundleFacts { AluCount = 5 },
                RuntimeReject = TypedSlotRejectReason.StaticClassOvercommit,
                IsStructuralMismatch = AgreementViolationReport.IsStructuralReject(TypedSlotRejectReason.StaticClassOvercommit)
            };

            Assert.True(report.IsStructuralMismatch);
        }

        [Fact]
        public void AgreementViolationReport_DynamicReject_NotStructuralMismatch()
        {
            var report = new AgreementViolationReport
            {
                CompilerFacts = new TypedSlotBundleFacts { AluCount = 1, FlexibleOpCount = 1 },
                RuntimeReject = TypedSlotRejectReason.ScoreboardReject,
                IsStructuralMismatch = AgreementViolationReport.IsStructuralReject(TypedSlotRejectReason.ScoreboardReject)
            };

            Assert.False(report.IsStructuralMismatch);
        }

        #endregion

        #region VerifyTypedSlotAgreement — capacity checks

        [Fact]
        public void VerifyTypedSlotAgreement_FitsCapacity_ReturnsTrue()
        {
            var facts = new TypedSlotBundleFacts
            {
                AluCount = 4,
                LsuCount = 2,
                DmaStreamCount = 1,
                BranchControlCount = 1,
                SystemSingletonCount = 0,
                FlexibleOpCount = 8,
                PinnedOpCount = 0
            };
            var capacity = CreateInitializedCapacity();

            Assert.True(SafetyVerifier.VerifyTypedSlotAgreement(facts, capacity));
        }

        [Fact]
        public void VerifyTypedSlotAgreement_AluExceedsCapacity_ReturnsFalse()
        {
            var facts = new TypedSlotBundleFacts
            {
                AluCount = 5, // exceeds 4
                FlexibleOpCount = 5,
                PinnedOpCount = 0
            };
            var capacity = CreateInitializedCapacity();

            Assert.False(SafetyVerifier.VerifyTypedSlotAgreement(facts, capacity));
        }

        [Fact]
        public void VerifyTypedSlotAgreement_LsuExceedsCapacity_ReturnsFalse()
        {
            var facts = new TypedSlotBundleFacts
            {
                LsuCount = 3, // exceeds 2
                FlexibleOpCount = 3,
                PinnedOpCount = 0
            };
            var capacity = CreateInitializedCapacity();

            Assert.False(SafetyVerifier.VerifyTypedSlotAgreement(facts, capacity));
        }

        [Fact]
        public void VerifyTypedSlotAgreement_DmaStreamExceedsCapacity_ReturnsFalse()
        {
            var facts = new TypedSlotBundleFacts
            {
                DmaStreamCount = 2, // exceeds 1
                FlexibleOpCount = 2,
                PinnedOpCount = 0
            };
            var capacity = CreateInitializedCapacity();

            Assert.False(SafetyVerifier.VerifyTypedSlotAgreement(facts, capacity));
        }

        [Fact]
        public void VerifyTypedSlotAgreement_BranchControlExceedsCapacity_ReturnsFalse()
        {
            var facts = new TypedSlotBundleFacts
            {
                BranchControlCount = 2, // exceeds 1
                FlexibleOpCount = 2,
                PinnedOpCount = 0
            };
            var capacity = CreateInitializedCapacity();

            Assert.False(SafetyVerifier.VerifyTypedSlotAgreement(facts, capacity));
        }

        [Fact]
        public void VerifyTypedSlotAgreement_SystemSingletonExceedsCapacity_ReturnsFalse()
        {
            var facts = new TypedSlotBundleFacts
            {
                SystemSingletonCount = 2, // exceeds 1
                FlexibleOpCount = 2,
                PinnedOpCount = 0
            };
            var capacity = CreateInitializedCapacity();

            Assert.False(SafetyVerifier.VerifyTypedSlotAgreement(facts, capacity));
        }

        [Fact]
        public void VerifyTypedSlotAgreement_EmptyFacts_ReturnsTrue()
        {
            var facts = default(TypedSlotBundleFacts);
            var capacity = CreateInitializedCapacity();

            Assert.True(SafetyVerifier.VerifyTypedSlotAgreement(facts, capacity));
        }

        #endregion

        #region ProcessorCompilerBridge — typed-slot facts acceptance

        [Fact]
        public void CompilerBridge_AcceptTypedSlotFacts_StoresFacts()
        {
            var bridge = CreateHandshakenBridge();
            var facts = new TypedSlotBundleFacts { AluCount = 2, FlexibleOpCount = 2 };

            bridge.AcceptTypedSlotFacts(facts);

            Assert.Equal(2, bridge.LastAcceptedFacts.AluCount);
            Assert.Null(bridge.LastViolation);
            Assert.Equal(CompilerTypedSlotIngressAction.RecordedWithoutValidation, bridge.LastTypedSlotIngressAction);
        }

        [Fact]
        public void CompilerBridge_ValidateCompilerFacts_DefaultsTrue()
        {
            var bridge = new ProcessorCompilerBridge();

            Assert.True(bridge.ValidateCompilerFacts);
        }

        [Fact]
        public void CompilerBridge_AcceptTypedSlotFacts_ValidationDisabled_NoViolation()
        {
            var bridge = CreateHandshakenBridge();
            bridge.ValidateCompilerFacts = false;

            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();

            // Facts with wrong count — but validation is off
            var facts = new TypedSlotBundleFacts
            {
                Slot0Class = SlotClass.AluClass,
                AluCount = 3,
                FlexibleOpCount = 1
            };

            bridge.AcceptTypedSlotFacts(facts, bundle);

            Assert.Null(bridge.LastViolation);
            Assert.Equal(CompilerTypedSlotIngressAction.RecordedWithoutValidation, bridge.LastTypedSlotIngressAction);
        }

        [Fact]
        public void CompilerBridge_AcceptTypedSlotFacts_CompatibilityValidation_InvalidFacts_QuarantineLogsViolation()
        {
            var bridge = CreateHandshakenBridge();
            bridge.ValidateCompilerFacts = true;

            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();

            // Facts with mismatched class count
            var facts = new TypedSlotBundleFacts
            {
                Slot0Class = SlotClass.AluClass,
                Slot1Class = SlotClass.Unclassified,
                Slot2Class = SlotClass.Unclassified,
                Slot3Class = SlotClass.Unclassified,
                Slot4Class = SlotClass.Unclassified,
                Slot5Class = SlotClass.Unclassified,
                Slot6Class = SlotClass.Unclassified,
                Slot7Class = SlotClass.Unclassified,
                AluCount = 3, // mismatch
                FlexibleOpCount = 1,
                PinnedOpCount = 0
            };

            bridge.AcceptTypedSlotFacts(facts, bundle);

            Assert.NotNull(bridge.LastViolation);
            Assert.True(bridge.LastViolation.Value.IsStructuralMismatch);
            Assert.Equal(CompilerTypedSlotIngressAction.QuarantinedAgreementFailure, bridge.LastTypedSlotIngressAction);
            Assert.Equal(CompilerTypedSlotPolicyMode.CompatibilityValidation, bridge.TypedSlotPolicy.Mode);
        }

        [Fact]
        public void CompilerBridge_AcceptTypedSlotFacts_StrictVerification_InvalidFacts_RejectsAfterRecordingViolation()
        {
            var bridge = CreateHandshakenBridge();
            bridge.TypedSlotPolicy = CompilerTypedSlotPolicy.StrictVerification;
            bridge.ValidateCompilerFacts = false;

            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();

            var facts = new TypedSlotBundleFacts
            {
                Slot0Class = SlotClass.AluClass,
                Slot1Class = SlotClass.Unclassified,
                Slot2Class = SlotClass.Unclassified,
                Slot3Class = SlotClass.Unclassified,
                Slot4Class = SlotClass.Unclassified,
                Slot5Class = SlotClass.Unclassified,
                Slot6Class = SlotClass.Unclassified,
                Slot7Class = SlotClass.Unclassified,
                AluCount = 3,
                FlexibleOpCount = 1,
                PinnedOpCount = 0
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => bridge.AcceptTypedSlotFacts(facts, bundle));

            Assert.Contains("strict typed-slot verification", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(bridge.LastViolation);
            Assert.True(bridge.LastViolation.Value.IsStructuralMismatch);
            Assert.Equal(CompilerTypedSlotIngressAction.RejectedAgreementFailure, bridge.LastTypedSlotIngressAction);
        }

        [Fact]
        public void CompilerBridge_AcceptTypedSlotFacts_StrictVerification_MissingFacts_RemainsCompatible()
        {
            var bridge = CreateHandshakenBridge();
            bridge.TypedSlotPolicy = CompilerTypedSlotPolicy.StrictVerification;

            bridge.AcceptTypedSlotFacts(default, new MicroOp?[8]);

            Assert.True(bridge.LastAcceptedFacts.IsEmpty);
            Assert.Null(bridge.LastViolation);
            Assert.Equal(CompilerTypedSlotIngressAction.AcceptedMissingFacts, bridge.LastTypedSlotIngressAction);
        }

        [Fact]
        public void CompilerBridge_AcceptTypedSlotFacts_StrictVerification_WithoutBundle_RejectsUnverifiableFacts()
        {
            var bridge = CreateHandshakenBridge();
            bridge.TypedSlotPolicy = CompilerTypedSlotPolicy.StrictVerification;
            var facts = new TypedSlotBundleFacts { AluCount = 1, FlexibleOpCount = 1 };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => bridge.AcceptTypedSlotFacts(facts));

            Assert.Contains("requires a runtime bundle", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(CompilerTypedSlotIngressAction.RejectedAgreementFailure, bridge.LastTypedSlotIngressAction);
        }

        [Fact]
        public void CompilerBridge_TypedSlotPolicy_RequiredForAdmissionFuture_IsReservedAndCannotBeActivated()
        {
            var bridge = CreateHandshakenBridge();

            NotSupportedException ex = Assert.Throws<NotSupportedException>(
                () => bridge.TypedSlotPolicy = CompilerTypedSlotPolicy.RequiredForAdmissionFuture);

            Assert.Contains("future", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(CompilerTypedSlotPolicyMode.CompatibilityValidation, bridge.TypedSlotPolicy.Mode);
        }

        [Fact]
        public void CompilerBridge_RecordAgreementViolation_StructuralReject()
        {
            var bridge = CreateHandshakenBridge();
            var facts = new TypedSlotBundleFacts { AluCount = 5, FlexibleOpCount = 5 };

            bridge.RecordAgreementViolation(facts, TypedSlotRejectReason.StaticClassOvercommit);

            Assert.NotNull(bridge.LastViolation);
            Assert.True(bridge.LastViolation.Value.IsStructuralMismatch);
            Assert.Equal(TypedSlotRejectReason.StaticClassOvercommit, bridge.LastViolation.Value.RuntimeReject);
            Assert.Equal(CompilerTypedSlotIngressAction.QuarantinedAgreementFailure, bridge.LastTypedSlotIngressAction);
        }

        [Fact]
        public void CompilerBridge_RecordAgreementViolation_StrictVerification_StructuralReject_Throws()
        {
            var bridge = CreateHandshakenBridge();
            bridge.TypedSlotPolicy = CompilerTypedSlotPolicy.StrictVerification;
            var facts = new TypedSlotBundleFacts { AluCount = 5, FlexibleOpCount = 5 };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => bridge.RecordAgreementViolation(facts, TypedSlotRejectReason.StaticClassOvercommit));

            Assert.Contains("strict typed-slot verification", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(bridge.LastViolation);
            Assert.True(bridge.LastViolation.Value.IsStructuralMismatch);
            Assert.Equal(CompilerTypedSlotIngressAction.RejectedAgreementFailure, bridge.LastTypedSlotIngressAction);
        }

        [Fact]
        public void CompilerBridge_RecordAgreementViolation_DynamicReject()
        {
            var bridge = CreateHandshakenBridge();
            var facts = new TypedSlotBundleFacts { AluCount = 1, FlexibleOpCount = 1 };

            bridge.RecordAgreementViolation(facts, TypedSlotRejectReason.ScoreboardReject);

            Assert.NotNull(bridge.LastViolation);
            Assert.False(bridge.LastViolation.Value.IsStructuralMismatch);
            Assert.Equal(CompilerTypedSlotIngressAction.RecordedDynamicRuntimeReject, bridge.LastTypedSlotIngressAction);
        }

        [Fact]
        public void CompilerBridge_DefaultFacts_IsEmpty()
        {
            var bridge = new ProcessorCompilerBridge();
            Assert.True(bridge.LastAcceptedFacts.IsEmpty);
        }

        [Fact]
        public void CompilerBridge_DeclareCompilerContractVersion_CurrentVersion_BindsHandshakeState()
        {
            var bridge = new ProcessorCompilerBridge();

            bridge.DeclareCompilerContractVersion(CompilerContract.Version, "Stage7AgreementTests.Handshake");

            Assert.True(bridge.HasContractHandshake);
            Assert.Equal(CompilerContract.Version, bridge.DeclaredCompilerContractVersion);
            Assert.Equal("Stage7AgreementTests.Handshake", bridge.ContractHandshakeProducerSurface);
        }

        [Fact]
        public void CompilerBridge_DeclareCompilerContractVersion_StaleVersion_Throws()
        {
            var bridge = new ProcessorCompilerBridge();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => bridge.DeclareCompilerContractVersion(CompilerContract.Version - 1, "Stage7AgreementTests.Stale"));

            Assert.Contains("Compiler contract mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CompilerBridge_AcceptTypedSlotFacts_WithoutHandshake_Throws()
        {
            var bridge = new ProcessorCompilerBridge();
            var facts = new TypedSlotBundleFacts { AluCount = 1, FlexibleOpCount = 1 };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => bridge.AcceptTypedSlotFacts(facts));

            Assert.Contains("requires an explicit compiler contract handshake", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region ReplayDeterminismReport — TypedSlotAgreementValid

        [Fact]
        public void ReplayDeterminismReport_OldConstructor_TypedSlotAgreementValid_DefaultsTrue()
        {
            var report = new ReplayDeterminismReport(
                isDeterministic: true,
                comparedEvents: 100,
                comparedReplayEvents: 50,
                comparedTimelineSamples: 200,
                comparedInvalidationEvents: 10,
                comparedEpochs: 5,
                mismatchThreadId: -1,
                mismatchCycle: -1,
                mismatchField: "",
                expectedValue: "",
                actualValue: "");

            Assert.True(report.TypedSlotAgreementValid);
            Assert.True(report.IsDeterministic);
        }

        [Fact]
        public void ReplayDeterminismReport_NewConstructor_TypedSlotAgreementValid_PreservesValue()
        {
            var report = new ReplayDeterminismReport(
                isDeterministic: true,
                comparedEvents: 100,
                comparedReplayEvents: 50,
                comparedTimelineSamples: 200,
                comparedInvalidationEvents: 10,
                comparedEpochs: 5,
                mismatchThreadId: -1,
                mismatchCycle: -1,
                mismatchField: "",
                expectedValue: "",
                actualValue: "",
                typedSlotAgreementValid: false);

            Assert.False(report.TypedSlotAgreementValid);
            Assert.True(report.IsDeterministic);
        }

        [Fact]
        public void ReplayDeterminismReport_NewConstructor_AllFieldsPreserved()
        {
            var report = new ReplayDeterminismReport(
                isDeterministic: false,
                comparedEvents: 42,
                comparedReplayEvents: 21,
                comparedTimelineSamples: 84,
                comparedInvalidationEvents: 7,
                comparedEpochs: 3,
                mismatchThreadId: 2,
                mismatchCycle: 1000L,
                mismatchField: "LaneId",
                expectedValue: "3",
                actualValue: "5",
                typedSlotAgreementValid: true);

            Assert.False(report.IsDeterministic);
            Assert.Equal(42, report.ComparedEvents);
            Assert.Equal(21, report.ComparedReplayEvents);
            Assert.Equal(84, report.ComparedTimelineSamples);
            Assert.Equal(7, report.ComparedInvalidationEvents);
            Assert.Equal(3, report.ComparedEpochs);
            Assert.Equal(2, report.MismatchThreadId);
            Assert.Equal(1000L, report.MismatchCycle);
            Assert.Equal("LaneId", report.MismatchField);
            Assert.Equal("3", report.ExpectedValue);
            Assert.Equal("5", report.ActualValue);
            Assert.True(report.TypedSlotAgreementValid);
        }

        #endregion

        #region Cross-validation: FromBundle → ValidateTypedSlotFacts roundtrip

        [Fact]
        public void CrossValidation_FromBundle_ThenValidate_Passes()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();
            bundle[1] = CreateAluOp(pinning: SlotPinningKind.HardPinned, pinnedLane: 1);
            bundle[4] = CreateLsuOp();
            bundle[6] = CreateDmaOp();
            bundle[7] = CreateBranchOp();

            var facts = TypedSlotBundleFacts.FromBundle(bundle);

            Assert.True(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
            Assert.Equal(2, facts.AluCount);
            Assert.Equal(1, facts.LsuCount);
            Assert.Equal(1, facts.DmaStreamCount);
            Assert.Equal(1, facts.BranchControlCount);
            Assert.Equal(4, facts.FlexibleOpCount);
            Assert.Equal(1, facts.PinnedOpCount);
        }

        [Fact]
        public void CrossValidation_FromBundle_FullBundle_Passes()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();
            bundle[1] = CreateAluOp();
            bundle[2] = CreateAluOp();
            bundle[3] = CreateAluOp();
            bundle[4] = CreateLsuOp();
            bundle[5] = CreateLsuOp();
            bundle[6] = CreateDmaOp();
            bundle[7] = CreateBranchOp();

            var facts = TypedSlotBundleFacts.FromBundle(bundle);

            Assert.True(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
            Assert.Equal(4, facts.AluCount);
            Assert.Equal(2, facts.LsuCount);
            Assert.Equal(1, facts.DmaStreamCount);
            Assert.Equal(1, facts.BranchControlCount);
            Assert.Equal(8, facts.FlexibleOpCount);
        }

        #endregion

        #region Backward compatibility

        [Fact]
        public void BackwardCompat_DefaultFacts_ValidateReturnsTrue()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = CreateAluOp();
            var facts = default(TypedSlotBundleFacts);

            // Default facts are empty → validation skips, returns true
            Assert.True(SafetyVerifier.ValidateTypedSlotFacts(facts, bundle));
        }

        [Fact]
        public void BackwardCompat_DefaultFacts_VerifyAgreementReturnsTrue()
        {
            var facts = default(TypedSlotBundleFacts);
            var capacity = CreateInitializedCapacity();

            Assert.True(SafetyVerifier.VerifyTypedSlotAgreement(facts, capacity));
        }

        [Fact]
        public void BackwardCompat_CompilerBridge_NoFacts_LastAcceptedIsEmpty()
        {
            var bridge = new ProcessorCompilerBridge();
            Assert.True(bridge.LastAcceptedFacts.IsEmpty);
            Assert.Null(bridge.LastViolation);
        }

        #endregion

        #region GetSlotClass boundary

        [Fact]
        public void GetSlotClass_InvalidIndex_ThrowsArgumentOutOfRange()
        {
            var facts = default(TypedSlotBundleFacts);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => facts.GetSlotClass(8));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => facts.GetSlotClass(-1));
        }

        #endregion
    }
}
