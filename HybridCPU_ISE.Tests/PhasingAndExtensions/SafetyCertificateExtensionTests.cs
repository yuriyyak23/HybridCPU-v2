using Xunit;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 05 – Safety Certificate Class-Level Semantics Extension tests.
    /// Validates CertificateRejectDetail, CanInjectWithReason, ClassOccupancy tracking,
    /// TypedSlotRejectClassification, and SafetyVerifier.ClassifyReject.
    /// </summary>
    public class SafetyCertificateExtensionTests
    {
        #region Helpers

        /// <summary>
        /// Create a MicroOp with explicit SafetyMask, VirtualThreadId, and SlotClass.
        /// </summary>
        private static NopMicroOp CreateOpWithMask(
            int vt, ulong low, ulong high = 0,
            SlotClass slotClass = SlotClass.Unclassified,
            SlotPinningKind pinning = SlotPinningKind.ClassFlexible)
        {
            return new NopMicroOp
            {
                OpCode = 0,
                OwnerThreadId = 0,
                VirtualThreadId = vt,
                SafetyMask = new SafetyMask128(low, high),
                Placement = SlotPlacementMetadata.Default with
                {
                    RequiredSlotClass = slotClass,
                    PinningKind = pinning
                }
            };
        }

        /// <summary>
        /// Create a ScalarALU op (RequiredSlotClass = AluClass) with explicit registers.
        /// </summary>
        private static ScalarALUMicroOp CreateAluOp(
            int vt,
            ushort dest,
            ushort src1,
            ushort src2,
            bool writesRegister = true,
            bool usesImmediate = false,
            ulong structuralLowMask = 0)
        {
            var op = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                OwnerThreadId = 0,
                VirtualThreadId = vt,
                DestRegID = dest,
                Src1RegID = src1,
                Src2RegID = src2,
                UsesImmediate = usesImmediate,
                WritesRegister = writesRegister,
                SafetyMask = new SafetyMask128(structuralLowMask, 0)
            };
            op.InitializeMetadata();
            return op;
        }

        private static ScalarALUMicroOp CreateReadOnlyAluOp(int vt, ushort src)
        {
            return CreateAluOp(
                vt,
                VLIW_Instruction.NoReg,
                src,
                VLIW_Instruction.NoReg,
                writesRegister: false,
                usesImmediate: true);
        }

        private static ScalarALUMicroOp CreateWriteOnlyAluOp(int vt, ushort dest)
        {
            return CreateAluOp(
                vt,
                dest,
                VLIW_Instruction.NoReg,
                VLIW_Instruction.NoReg,
                writesRegister: true,
                usesImmediate: true);
        }

        #endregion

        #region CanInjectWithReason — SharedResourceConflict

        [Fact]
        public void CanInjectWithReason_WhenSharedResourceConflict_ThenReturnsSharedResourceConflict()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateOpWithMask(vt: 0, low: 1UL << 32); // memory domain bit 32
            var op2 = CreateOpWithMask(vt: 1, low: 1UL << 32); // same domain

            cert.AddOperation(op1);

            bool result = cert.CanInjectWithReason(op2, out var detail);

            Assert.False(result);
            Assert.Equal(CertificateRejectDetail.SharedResourceConflict, detail);
        }

        [Fact]
        public void CanInjectWithReason_WhenHighBitSharedConflict_ThenReturnsSharedResourceConflict()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateOpWithMask(vt: 0, low: 0, high: 1UL);
            var op2 = CreateOpWithMask(vt: 2, low: 0, high: 1UL);

            cert.AddOperation(op1);

            bool result = cert.CanInjectWithReason(op2, out var detail);

            Assert.False(result);
            Assert.Equal(CertificateRejectDetail.SharedResourceConflict, detail);
        }

        #endregion

        #region CanInjectWithReason — RegisterGroupConflict

        [Fact]
        public void CanInjectWithReason_WhenRegisterGroupConflictWAW_ThenReturnsRegisterGroupConflict()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 2);
            var op2 = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 3);

            cert.AddOperation(op1);

            bool result = cert.CanInjectWithReason(op2, out var detail);

            Assert.False(result);
            Assert.Equal(CertificateRejectDetail.RegisterGroupConflict, detail);
        }

        [Fact]
        public void CanInjectWithReason_WhenWARHazard_ThenReturnsRegisterGroupConflict()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            // op1 reads group 0; op2 writes group 0 through explicit admission facts.
            var readOp = CreateReadOnlyAluOp(vt: 0, src: 0);
            var writeOp = CreateWriteOnlyAluOp(vt: 0, dest: 1);

            cert.AddOperation(readOp);

            bool result = cert.CanInjectWithReason(writeOp, out var detail);

            Assert.False(result);
            Assert.Equal(CertificateRejectDetail.RegisterGroupConflict, detail);
        }

        #endregion

        #region CanInjectWithReason — None (success)

        [Fact]
        public void CanInjectWithReason_WhenEmptyCert_ThenReturnsNone()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op = CreateAluOp(vt: 0, dest: 4, src1: 0, src2: 1);

            bool result = cert.CanInjectWithReason(op, out var detail);

            Assert.True(result);
            Assert.Equal(CertificateRejectDetail.None, detail);
        }

        [Fact]
        public void CanInjectWithReason_WhenDifferentVtSameRegGroup_ThenReturnsNone()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 2);
            var op2 = CreateAluOp(vt: 1, dest: 0, src1: 1, src2: 3);

            cert.AddOperation(op1);

            bool result = cert.CanInjectWithReason(op2, out var detail);

            Assert.True(result);
            Assert.Equal(CertificateRejectDetail.None, detail);
        }

        [Fact]
        public void CanInjectWithReason_WhenDifferentSharedResources_ThenReturnsNone()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateOpWithMask(vt: 0, low: 1UL << 32);
            var op2 = CreateOpWithMask(vt: 1, low: 1UL << 33);

            cert.AddOperation(op1);

            bool result = cert.CanInjectWithReason(op2, out var detail);

            Assert.True(result);
            Assert.Equal(CertificateRejectDetail.None, detail);
        }

        [Fact]
        public void CanInjectWithReason_WhenRARSameVt_ThenReturnsNone()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            // Both ops only read group 0 through explicit admission facts.
            var op1 = CreateReadOnlyAluOp(vt: 0, src: 0);
            var op2 = CreateReadOnlyAluOp(vt: 0, src: 1);

            cert.AddOperation(op1);

            bool result = cert.CanInjectWithReason(op2, out var detail);

            Assert.True(result);
            Assert.Equal(CertificateRejectDetail.None, detail);
        }

        #endregion

        #region CanInjectWithReason — Consistency with CanInject

        [Fact]
        public void CanInjectWithReason_AlwaysAgreesWithCanInject_OnAccept()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 2);
            var op2 = CreateAluOp(vt: 1, dest: 4, src1: 5, src2: 6);

            cert.AddOperation(op1);

            bool canInject = cert.CanInject(op2);
            bool withReason = cert.CanInjectWithReason(op2, out var detail);

            Assert.Equal(canInject, withReason);
            Assert.Equal(CertificateRejectDetail.None, detail);
        }

        [Fact]
        public void CanInjectWithReason_AlwaysAgreesWithCanInject_OnReject()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op1 = CreateOpWithMask(vt: 0, low: 1UL << 48); // LSU load channel
            var op2 = CreateOpWithMask(vt: 3, low: 1UL << 48); // same channel

            cert.AddOperation(op1);

            bool canInject = cert.CanInject(op2);
            bool withReason = cert.CanInjectWithReason(op2, out _);

            Assert.Equal(canInject, withReason);
            Assert.False(canInject);
        }

        #endregion

        #region AddOperation — ClassOccupancy tracking

        [Fact]
        public void AddOperation_IncrementsClassOccupancy_ForAluClass()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op = CreateOpWithMask(vt: 0, low: 0, slotClass: SlotClass.AluClass);

            cert.AddOperation(op);

            Assert.Equal(1, cert.ClassOccupancy.AluOccupied);
        }

        [Fact]
        public void AddOperation_IncrementsClassOccupancy_ForLsuClass()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op = CreateOpWithMask(vt: 0, low: 0, slotClass: SlotClass.LsuClass);

            cert.AddOperation(op);

            Assert.Equal(1, cert.ClassOccupancy.LsuOccupied);
        }

        [Fact]
        public void AddOperation_IncrementsClassOccupancy_ForDmaStreamClass()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op = CreateOpWithMask(vt: 0, low: 0, slotClass: SlotClass.DmaStreamClass);

            cert.AddOperation(op);

            Assert.Equal(1, cert.ClassOccupancy.DmaStreamOccupied);
        }

        [Fact]
        public void AddOperation_IncrementsClassOccupancy_ForBranchControl()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op = CreateOpWithMask(vt: 0, low: 0, slotClass: SlotClass.BranchControl);

            cert.AddOperation(op);

            Assert.Equal(1, cert.ClassOccupancy.BranchControlOccupied);
        }

        [Fact]
        public void AddOperation_IncrementsClassOccupancy_ForSystemSingleton()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            var op = CreateOpWithMask(vt: 0, low: 0, slotClass: SlotClass.SystemSingleton);

            cert.AddOperation(op);

            Assert.Equal(1, cert.ClassOccupancy.SystemSingletonOccupied);
        }

        [Fact]
        public void AddOperation_After4AluOps_ClassOccupancyHasNoFreeAluCapacity()
        {
            var cert = BundleResourceCertificate4Way.Empty;
            // Initialize totals so HasFreeCapacity works
            cert.ClassOccupancy.InitializeFromLaneMap();

            for (int i = 0; i < 4; i++)
            {
                var op = CreateOpWithMask(
                    vt: i, low: 0,
                    slotClass: SlotClass.AluClass);
                cert.AddOperation(op);
            }

            Assert.Equal(4, cert.ClassOccupancy.AluOccupied);
            Assert.False(cert.ClassOccupancy.HasFreeCapacity(SlotClass.AluClass));
        }

        [Fact]
        public void AddOperation_MixedClasses_TracksEachClassSeparately()
        {
            var cert = BundleResourceCertificate4Way.Empty;

            cert.AddOperation(CreateOpWithMask(vt: 0, low: 0, slotClass: SlotClass.AluClass));
            cert.AddOperation(CreateOpWithMask(vt: 0, low: 0, slotClass: SlotClass.AluClass));
            cert.AddOperation(CreateOpWithMask(vt: 1, low: 0, slotClass: SlotClass.LsuClass));
            cert.AddOperation(CreateOpWithMask(vt: 2, low: 0, slotClass: SlotClass.DmaStreamClass));

            Assert.Equal(2, cert.ClassOccupancy.AluOccupied);
            Assert.Equal(1, cert.ClassOccupancy.LsuOccupied);
            Assert.Equal(1, cert.ClassOccupancy.DmaStreamOccupied);
            Assert.Equal(0, cert.ClassOccupancy.BranchControlOccupied);
        }

        [Fact]
        public void AddOperation_EmptyCert_ClassOccupancyStartsAtZero()
        {
            var cert = BundleResourceCertificate4Way.Empty;

            Assert.Equal(0, cert.ClassOccupancy.AluOccupied);
            Assert.Equal(0, cert.ClassOccupancy.LsuOccupied);
            Assert.Equal(0, cert.ClassOccupancy.DmaStreamOccupied);
            Assert.Equal(0, cert.ClassOccupancy.BranchControlOccupied);
            Assert.Equal(0, cert.ClassOccupancy.SystemSingletonOccupied);
        }

        #endregion

        #region ClassifyReject — PinnedLaneConflict

        [Fact]
        public void ClassifyReject_WhenPinnedLaneConflict_ThenIsPinnedConflictTrue()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.PinnedLaneConflict,
                CertificateRejectDetail.None,
                SlotClass.AluClass,
                SlotPinningKind.HardPinned);

            Assert.True(classification.IsPinnedConflict);
            Assert.False(classification.IsClassCapacityIssue);
            Assert.False(classification.IsDynamicStateIssue);
        }

        #endregion

        #region ClassifyReject — ClassCapacity rejects

        [Fact]
        public void ClassifyReject_WhenStaticClassOvercommit_ThenIsClassCapacityAndStaticOvercommit()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.StaticClassOvercommit,
                CertificateRejectDetail.None,
                SlotClass.AluClass,
                SlotPinningKind.ClassFlexible);

            Assert.True(classification.IsClassCapacityIssue);
            Assert.True(classification.IsStaticOvercommit);
            Assert.False(classification.IsDynamicExhaustion);
            Assert.False(classification.IsDynamicStateIssue);
        }

        [Fact]
        public void ClassifyReject_WhenDynamicClassExhaustion_ThenIsClassCapacityAndDynamic()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.DynamicClassExhaustion,
                CertificateRejectDetail.None,
                SlotClass.LsuClass,
                SlotPinningKind.ClassFlexible);

            Assert.True(classification.IsClassCapacityIssue);
            Assert.True(classification.IsDynamicExhaustion);
            Assert.True(classification.IsDynamicStateIssue);
            Assert.False(classification.IsStaticOvercommit);
        }

        #endregion

        #region ClassifyReject — Dynamic state rejects

        [Fact]
        public void ClassifyReject_WhenScoreboardReject_ThenIsDynamicStateIssue()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.ScoreboardReject,
                CertificateRejectDetail.None,
                SlotClass.LsuClass,
                SlotPinningKind.ClassFlexible);

            Assert.True(classification.IsDynamicStateIssue);
            Assert.False(classification.IsClassCapacityIssue);
            Assert.False(classification.IsPinnedConflict);
        }

        [Fact]
        public void ClassifyReject_WhenBankPendingReject_ThenIsDynamicStateIssue()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.BankPendingReject,
                CertificateRejectDetail.None,
                SlotClass.LsuClass,
                SlotPinningKind.ClassFlexible);

            Assert.True(classification.IsDynamicStateIssue);
            Assert.False(classification.IsClassCapacityIssue);
        }

        [Fact]
        public void ClassifyReject_WhenSpeculationBudgetReject_ThenIsDynamicStateIssue()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.SpeculationBudgetReject,
                CertificateRejectDetail.None,
                SlotClass.AluClass,
                SlotPinningKind.ClassFlexible);

            Assert.True(classification.IsDynamicStateIssue);
            Assert.False(classification.IsClassCapacityIssue);
        }

        [Fact]
        public void ClassifyReject_WhenAssistQuotaReject_ThenIsDynamicStateIssue()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.AssistQuotaReject,
                CertificateRejectDetail.None,
                SlotClass.LsuClass,
                SlotPinningKind.ClassFlexible);

            Assert.True(classification.IsDynamicStateIssue);
            Assert.False(classification.IsClassCapacityIssue);
        }

        [Fact]
        public void ClassifyReject_WhenAssistBackpressureReject_ThenIsDynamicStateIssue()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.AssistBackpressureReject,
                CertificateRejectDetail.None,
                SlotClass.DmaStreamClass,
                SlotPinningKind.ClassFlexible);

            Assert.True(classification.IsDynamicStateIssue);
            Assert.False(classification.IsClassCapacityIssue);
        }

        #endregion

        #region ClassifyReject — Passthrough fields

        [Fact]
        public void ClassifyReject_PreservesInputFields()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.ResourceConflict,
                CertificateRejectDetail.SharedResourceConflict,
                SlotClass.DmaStreamClass,
                SlotPinningKind.HardPinned);

            Assert.Equal(TypedSlotRejectReason.ResourceConflict, classification.AdmissionReject);
            Assert.Equal(CertificateRejectDetail.SharedResourceConflict, classification.CertificateDetail);
            Assert.Equal(SlotClass.DmaStreamClass, classification.CandidateClass);
            Assert.Equal(SlotPinningKind.HardPinned, classification.PinningKind);
        }

        [Fact]
        public void ClassifyReject_WhenResourceConflict_ThenNoBooleanFlagsSet()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.ResourceConflict,
                CertificateRejectDetail.RegisterGroupConflict,
                SlotClass.AluClass,
                SlotPinningKind.ClassFlexible);

            Assert.False(classification.IsPinnedConflict);
            Assert.False(classification.IsClassCapacityIssue);
            Assert.False(classification.IsDynamicStateIssue);
            Assert.False(classification.IsStaticOvercommit);
            Assert.False(classification.IsDynamicExhaustion);
        }

        [Fact]
        public void ClassifyReject_WhenNone_ThenAllFlagsFalse()
        {
            var classification = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.None,
                CertificateRejectDetail.None,
                SlotClass.AluClass,
                SlotPinningKind.ClassFlexible);

            Assert.False(classification.IsPinnedConflict);
            Assert.False(classification.IsClassCapacityIssue);
            Assert.False(classification.IsDynamicStateIssue);
            Assert.False(classification.IsStaticOvercommit);
            Assert.False(classification.IsDynamicExhaustion);
        }

        #endregion

        #region CertificateRejectDetail — Enum values

        [Fact]
        public void CertificateRejectDetail_HasExpectedValues()
        {
            Assert.Equal(0, (byte)CertificateRejectDetail.None);
            Assert.Equal(1, (byte)CertificateRejectDetail.SharedResourceConflict);
            Assert.Equal(2, (byte)CertificateRejectDetail.RegisterGroupConflict);
        }

        #endregion
    }
}
