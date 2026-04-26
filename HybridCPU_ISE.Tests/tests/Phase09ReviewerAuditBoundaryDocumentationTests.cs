using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09ReviewerAuditBoundaryDocumentationTests
    {
        [Fact]
        public void T9_16a_CompilerRuntimeBoundaryArtifact_States_FailClosedHandshake_And_StagedFactSplit()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\compiler-runtime-boundary.md");

            Assert.Contains("DeclareCompilerContractVersion(...)", text, StringComparison.Ordinal);
            Assert.Contains("EnsureCompilerContractHandshake(...)", text, StringComparison.Ordinal);
            Assert.Contains("IrAdmissibilityAgreement", text, StringComparison.Ordinal);
            Assert.Contains("ValidationOnly", text, StringComparison.Ordinal);
            Assert.Contains("CompatibilityValidation", text, StringComparison.Ordinal);
            Assert.Contains("missing facts remain compatible", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("structured handoff", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("quarantine", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("StrictVerification", text, StringComparison.Ordinal);
            Assert.Contains("RequiredForAdmission", text, StringComparison.Ordinal);
            Assert.Contains("runtime legality remains the authority", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_16b_SchedulerContourArtifact_Separates_TypedSlot_Pipelined_And_CompatibilityContours()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\scheduler-contours-and-boundaries.md");

            Assert.Contains("PackBundleIntraCoreSmt(...)", text, StringComparison.Ordinal);
            Assert.Contains("PipelineFspStage1_Nominate(...)", text, StringComparison.Ordinal);
            Assert.Contains("PipelineFspStage2_Intersect(...)", text, StringComparison.Ordinal);
            Assert.Contains("TypedSlotEnabled == false", text, StringComparison.Ordinal);
            Assert.Contains("ResolveNextInjectableSlot(...)", text, StringComparison.Ordinal);
            Assert.Contains("TryClassAdmission(...)", text, StringComparison.Ordinal);
            Assert.Contains("TryMaterializeLane(...)", text, StringComparison.Ordinal);
            Assert.Contains("LateBindingConflict", text, StringComparison.Ordinal);
            Assert.Contains("does not widen legality", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not generic port arbitration", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("bundle-local mutation into the working bundle", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not architectural retirement", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("TryInjectAssistCandidates(...)", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_16c_RuntimeLegalityChainArtifact_States_GuardBeforeReuse_AuthoritySources_And_HazardDomainSplit()
        {
            string text = ReadRepoFile("HybridCPU_ISE\\docs\\runtime-legality-chain.md");

            Assert.Contains("PrepareSmt(...)", text, StringComparison.Ordinal);
            Assert.Contains("EvaluateSmtBoundaryGuard(...)", text, StringComparison.Ordinal);
            Assert.Contains("TryRejectSmtOwnerDomainGuard(...)", text, StringComparison.Ordinal);
            Assert.Contains("LegalityAuthoritySource.GuardPlane", text, StringComparison.Ordinal);
            Assert.Contains("LegalityAuthoritySource.ReplayPhaseCertificate", text, StringComparison.Ordinal);
            Assert.Contains("LegalityAuthoritySource.StructuralCertificate", text, StringComparison.Ordinal);
            Assert.Contains("RefreshSmtAfterMutation(...)", text, StringComparison.Ordinal);
            Assert.Contains("InvalidatePhaseMismatch(...)", text, StringComparison.Ordinal);
            Assert.Contains("SharedMask", text, StringComparison.Ordinal);
            Assert.Contains("RegMaskVT0..VT3", text, StringComparison.Ordinal);
            Assert.Contains("LegalityDecision", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_16d_EvidenceMap_Bounds_SmokeBaseline_And_RuntimeMatrix()
        {
            string mapText = ReadRepoFile("Documentation\\paper-claim-evidence-map.md");
            string baselineText = ReadRepoFile("Documentation\\validation-baseline.md");

            Assert.Contains("live code wins", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Documentation/validation-baseline.md", mapText, StringComparison.Ordinal);
            Assert.Contains("Documentation/AsmAppTestResults.md", mapText, StringComparison.Ordinal);
            Assert.Contains("smoke baseline", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not a repository-wide pass total", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("runtime sanity matrix", mapText, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("Documentation/paper-claim-evidence-map.md", baselineText, StringComparison.Ordinal);
            Assert.Contains("smoke subset", baselineText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_16e_AssistReplayAndOperationalDocs_State_ResidualAssistBoundary_And_FactPolicySplit()
        {
            string assistText = ReadRepoFile("HybridCPU_ISE\\docs\\assist-semantics.md");
            string replayText = ReadRepoFile("HybridCPU_ISE\\docs\\replay-envelope.md");
            string operationalText = ReadRepoFile("Documentation\\operational-semantics.md");

            Assert.Contains("single-cycle intra-core assist path", assistText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("residual legality and residual lane capacity", assistText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("foreground-subordinate", assistText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("pipelined FSP contour", assistText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("TryInjectAssistCandidates(...)", assistText, StringComparison.Ordinal);

            Assert.Contains("SharedMask", replayText, StringComparison.Ordinal);
            Assert.Contains("RegMaskVT0..VT3", replayText, StringComparison.Ordinal);
            Assert.Contains("preserves, rather than erases", replayText, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("ValidationOnly", operationalText, StringComparison.Ordinal);
            Assert.Contains("CompatibilityValidation", operationalText, StringComparison.Ordinal);
            Assert.Contains("DeclareCompilerContractVersion(...)", operationalText, StringComparison.Ordinal);
            Assert.Contains("EnsureCompilerContractHandshake(...)", operationalText, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_16f_ReplayTokenArtifacts_State_ExplicitRollbackBoundaries_And_CorrectAnchors()
        {
            string mapText = ReadRepoFile("Documentation\\paper-claim-evidence-map.md");
            string anchorText = ReadRepoFile("Documentation\\manuscript-implementation-anchor-map.md");
            string rollbackText = ReadRepoFile("HybridCPU_ISE\\docs\\rollback-boundaries.md");

            Assert.Contains("ReplayToken rollback boundaries are explicit and narrow", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("owner-thread restore context", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("exact bound main-memory byte ranges", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rename-map", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("commit-map", mapText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("free-list", mapText, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("HybridCPU_ISE/Core/Pipeline/MicroOps/ReplayToken.cs", anchorText, StringComparison.Ordinal);
            Assert.DoesNotContain("HybridCPU_ISE/Core/Diagnostics/ReplayToken.cs", anchorText, StringComparison.Ordinal);
            Assert.Contains("RetireCoordinator.cs", anchorText, StringComparison.Ordinal);

            Assert.Contains("architectural integer registers", rollbackText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("exact main-memory", rollbackText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("HasSideEffects", rollbackText, StringComparison.Ordinal);
            Assert.Contains("rename-map image", rollbackText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("free-list", rollbackText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("RetireCoordinator", rollbackText, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_16g_ReviewerResponseArtifact_Separates_FullRebuttal_From_Concession()
        {
            string relativePath = "Documentation\\reviewer-blocker-response-summary-2026-04-21.md";
            if (!TryReadRepoFile(relativePath, out string text))
            {
                return;
            }

            Assert.Contains("## Fully Rebutted", text, StringComparison.Ordinal);
            Assert.Contains("## Partially Rebutted Or Explicitly Conceded", text, StringComparison.Ordinal);
            Assert.Contains("backend-global rollback is explicitly conceded", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hardware cost only as a bounded non-claim", text, StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadRepoFile(string relativePath)
        {
            string repoRoot = CompatFreezeScanner.FindRepoRoot();
            string fullPath = Path.Combine(repoRoot, relativePath);
            Assert.True(File.Exists(fullPath), $"Missing repository document: {relativePath}");
            return File.ReadAllText(fullPath);
        }

        private static bool TryReadRepoFile(string relativePath, out string text)
        {
            string repoRoot = CompatFreezeScanner.FindRepoRoot();
            string fullPath = Path.Combine(repoRoot, relativePath);
            if (!File.Exists(fullPath))
            {
                text = string.Empty;
                return false;
            }

            text = File.ReadAllText(fullPath);
            return true;
        }
    }
}
