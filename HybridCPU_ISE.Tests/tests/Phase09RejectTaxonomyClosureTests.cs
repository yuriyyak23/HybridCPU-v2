using System;
using System.IO;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09RejectTaxonomyClosureTests
    {
        [Fact]
        public void T9_10a_RejectTaxonomyMatrix_States_Current_NonOneToOne_Closure()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\21. reject-taxonomy-matrix.md");

            Assert.Contains("AdmissibilityClassification", text, StringComparison.Ordinal);
            Assert.Contains("AdmissibilityRuntimeVocabulary", text, StringComparison.Ordinal);
            Assert.Contains("TypedSlotRejectReason", text, StringComparison.Ordinal);
            Assert.Contains("TypedSlotRejectClassification", text, StringComparison.Ordinal);
            Assert.Contains("TypedSlotAliasedLaneConflict", text, StringComparison.Ordinal);
            Assert.Contains("TypedSlotFactsInvalid", text, StringComparison.Ordinal);
            Assert.Contains("ResourceConflict", text, StringComparison.Ordinal);
            Assert.Contains("DomainReject", text, StringComparison.Ordinal);
            Assert.Contains("not a claim that every layer uses one shared enum", text, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("Documentation\\WhiteBook\\0. chapter-index.md")]
        [InlineData("Documentation\\WhiteBook\\7. compiler-runtime-contract.md")]
        [InlineData("Documentation\\WhiteBook\\19. references-and-reading-order.md")]
        public void T9_10b_EntryPoints_Expose_RejectTaxonomyMatrix(string relativePath)
        {
            string text = ReadRepoFile(relativePath);

            Assert.Contains("21. reject-taxonomy-matrix.md", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_10c_ClassifyReject_Keeps_Static_And_Dynamic_ClassCapacity_Families_Distinct()
        {
            TypedSlotRejectClassification staticReject = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.StaticClassOvercommit,
                CertificateRejectDetail.None,
                SlotClass.AluClass,
                SlotPinningKind.ClassFlexible);
            TypedSlotRejectClassification dynamicReject = SafetyVerifier.ClassifyReject(
                TypedSlotRejectReason.DynamicClassExhaustion,
                CertificateRejectDetail.None,
                SlotClass.AluClass,
                SlotPinningKind.ClassFlexible);

            Assert.True(staticReject.IsClassCapacityIssue);
            Assert.True(staticReject.IsStaticOvercommit);
            Assert.False(staticReject.IsDynamicExhaustion);
            Assert.False(staticReject.IsDynamicStateIssue);

            Assert.True(dynamicReject.IsClassCapacityIssue);
            Assert.False(dynamicReject.IsStaticOvercommit);
            Assert.True(dynamicReject.IsDynamicExhaustion);
            Assert.True(dynamicReject.IsDynamicStateIssue);
        }

        [Theory]
        [InlineData(TypedSlotRejectReason.ScoreboardReject)]
        [InlineData(TypedSlotRejectReason.BankPendingReject)]
        [InlineData(TypedSlotRejectReason.HardwareBudgetReject)]
        [InlineData(TypedSlotRejectReason.SpeculationBudgetReject)]
        [InlineData(TypedSlotRejectReason.AssistQuotaReject)]
        [InlineData(TypedSlotRejectReason.AssistBackpressureReject)]
        public void T9_10d_DynamicStateRejects_Set_DynamicStateIssue(TypedSlotRejectReason reason)
        {
            TypedSlotRejectClassification classification = SafetyVerifier.ClassifyReject(
                reason,
                CertificateRejectDetail.None,
                SlotClass.LsuClass,
                SlotPinningKind.ClassFlexible);

            Assert.True(classification.IsDynamicStateIssue);
            Assert.False(classification.IsStaticOvercommit);
            Assert.False(classification.IsPinnedConflict);
        }

        [Fact]
        public void T9_10e_CompilerOnlyStructuralFailures_HaveNo_RuntimeRejectTwins_And_MainlineLegalityDenials_CollapseToResourceConflict()
        {
            Assert.True(Enum.IsDefined(typeof(AdmissibilityClassification), AdmissibilityClassification.TypedSlotAliasedLaneConflict));
            Assert.True(Enum.IsDefined(typeof(AdmissibilityClassification), AdmissibilityClassification.TypedSlotFactsInvalid));
            Assert.False(Enum.TryParse<TypedSlotRejectReason>(nameof(AdmissibilityClassification.TypedSlotAliasedLaneConflict), out _));
            Assert.False(Enum.TryParse<TypedSlotRejectReason>(nameof(AdmissibilityClassification.TypedSlotFactsInvalid), out _));

            string schedulerText = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Scheduling\\MicroOpScheduler.Admission.cs");
            Assert.Contains("rejectReason = TypedSlotRejectReason.ResourceConflict;", schedulerText, StringComparison.Ordinal);
            Assert.DoesNotContain("rejectReason = TypedSlotRejectReason.DomainReject;", schedulerText, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_10f_AdmissibilityRuntimeVocabulary_Publishes_Current_Relationships_Without_False_OneToOne_Claims()
        {
            AdmissibilityRuntimeVocabularyRelation admissible = AdmissibilityRuntimeVocabulary.Describe(
                AdmissibilityClassification.StructurallyAdmissible);
            AdmissibilityRuntimeVocabularyRelation safetyMask = AdmissibilityRuntimeVocabulary.Describe(
                AdmissibilityClassification.SafetyMaskConflict);
            AdmissibilityRuntimeVocabularyRelation classCapacity = AdmissibilityRuntimeVocabulary.Describe(
                AdmissibilityClassification.TypedSlotClassCapacityExceeded);
            AdmissibilityRuntimeVocabularyRelation aliasedLane = AdmissibilityRuntimeVocabulary.Describe(
                AdmissibilityClassification.TypedSlotAliasedLaneConflict);
            AdmissibilityRuntimeVocabularyRelation invalidFacts = AdmissibilityRuntimeVocabulary.Describe(
                AdmissibilityClassification.TypedSlotFactsInvalid);

            Assert.Equal(AdmissibilityRuntimeVocabularyRelationKind.RuntimeContinuation, admissible.RelationKind);
            Assert.False(admissible.HasDirectSchedulerTwin);
            Assert.Null(admissible.SchedulerRejectReason);

            Assert.Equal(AdmissibilityRuntimeVocabularyRelationKind.CompatibilityAdjacentSchedulerFamily, safetyMask.RelationKind);
            Assert.False(safetyMask.HasDirectSchedulerTwin);
            Assert.Equal((TypedSlotRejectReason?)TypedSlotRejectReason.ResourceConflict, safetyMask.SchedulerRejectReason);
            Assert.Null(safetyMask.RuntimeDiagnosticRejectKind);

            Assert.Equal(AdmissibilityRuntimeVocabularyRelationKind.DirectSchedulerStructuralTwin, classCapacity.RelationKind);
            Assert.True(classCapacity.HasDirectSchedulerTwin);
            Assert.Equal((TypedSlotRejectReason?)TypedSlotRejectReason.StaticClassOvercommit, classCapacity.SchedulerRejectReason);
            Assert.Null(classCapacity.RuntimeDiagnosticRejectKind);

            Assert.Equal(AdmissibilityRuntimeVocabularyRelationKind.CompilerOnlyStructuralInvalidity, aliasedLane.RelationKind);
            Assert.False(aliasedLane.HasDirectSchedulerTwin);
            Assert.Null(aliasedLane.SchedulerRejectReason);
            Assert.Equal((RejectKind?)RejectKind.CrossLaneConflict, aliasedLane.RuntimeDiagnosticRejectKind);

            Assert.Equal(AdmissibilityRuntimeVocabularyRelationKind.CompilerOnlyStructuralInvalidity, invalidFacts.RelationKind);
            Assert.False(invalidFacts.HasDirectSchedulerTwin);
            Assert.Null(invalidFacts.SchedulerRejectReason);
            Assert.Null(invalidFacts.RuntimeDiagnosticRejectKind);
        }

        [Fact]
        public void T9_10g_Only_Proven_Structural_Twins_Are_Exposed_As_Direct_Mappings()
        {
            Assert.True(
                AdmissibilityRuntimeVocabulary.TryGetDirectSchedulerRejectTwin(
                    AdmissibilityClassification.TypedSlotClassCapacityExceeded,
                    out TypedSlotRejectReason compilerToRuntime));
            Assert.Equal(TypedSlotRejectReason.StaticClassOvercommit, compilerToRuntime);

            Assert.False(
                AdmissibilityRuntimeVocabulary.TryGetDirectSchedulerRejectTwin(
                    AdmissibilityClassification.SafetyMaskConflict,
                    out TypedSlotRejectReason safetyMaskTwin));
            Assert.Equal(TypedSlotRejectReason.None, safetyMaskTwin);

            Assert.True(
                AdmissibilityRuntimeVocabulary.TryGetDirectCompilerStructuralTwin(
                    TypedSlotRejectReason.StaticClassOvercommit,
                    out AdmissibilityClassification runtimeToCompiler));
            Assert.Equal(AdmissibilityClassification.TypedSlotClassCapacityExceeded, runtimeToCompiler);

            Assert.False(
                AdmissibilityRuntimeVocabulary.TryGetDirectCompilerStructuralTwin(
                    TypedSlotRejectReason.ResourceConflict,
                    out AdmissibilityClassification resourceConflictTwin));
            Assert.Equal(AdmissibilityClassification.StructurallyAdmissible, resourceConflictTwin);
        }

        private static string ReadRepoFile(string relativePath)
        {
            string repoRoot = CompatFreezeScanner.FindRepoRoot();
            string fullPath = Path.Combine(repoRoot, relativePath);
            Assert.True(File.Exists(fullPath), $"Missing repository document: {relativePath}");
            return File.ReadAllText(fullPath);
        }
    }
}
