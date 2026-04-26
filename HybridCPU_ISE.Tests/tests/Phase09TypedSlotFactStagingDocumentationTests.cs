using System;
using System.IO;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09TypedSlotFactStagingDocumentationTests
    {
        [Fact]
        public void T9_11a_TypedSlotFactStagingArtifact_States_CurrentMode_And_FutureStages()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\22. typed-slot-contract-staging.md");

            Assert.Contains("TypedSlotFactMode", text, StringComparison.Ordinal);
            Assert.Contains("ValidationOnly", text, StringComparison.Ordinal);
            Assert.Contains("WarnOnMissing", text, StringComparison.Ordinal);
            Assert.Contains("RequiredForAdmission", text, StringComparison.Ordinal);
            Assert.Contains("current mainline status is", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not yet the sole correctness carrier", text, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("Documentation\\WhiteBook\\0. chapter-index.md")]
        [InlineData("Documentation\\WhiteBook\\7. compiler-runtime-contract.md")]
        [InlineData("Documentation\\WhiteBook\\10. safety-isolation-and-legality.md")]
        [InlineData("Documentation\\WhiteBook\\20. legality-predicate.md")]
        [InlineData("Documentation\\WhiteBook\\19. references-and-reading-order.md")]
        public void T9_11b_WhiteBookEntryPoints_Expose_TypedSlotFactStagingArtifact(string relativePath)
        {
            string text = ReadRepoFile(relativePath);

            Assert.Contains("22. typed-slot-contract-staging.md", text, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_11c_CodeSurface_Freezes_CurrentValidationOnlyMode_And_OptionalMissingFacts()
        {
            Assert.Equal(TypedSlotFactMode.ValidationOnly, TypedSlotFactStaging.CurrentMode);
            Assert.True(TypedSlotFactStaging.AllowsCanonicalExecutionWithoutFacts);
            Assert.Equal(CompilerTypedSlotPolicyMode.CompatibilityValidation, CompilerContract.CurrentTypedSlotPolicy.Mode);
            Assert.True(CompilerContract.CurrentTypedSlotPolicy.AllowsMissingFacts);
            Assert.True(CompilerContract.CurrentTypedSlotPolicy.QuarantineLogsAgreementFailures);
            Assert.False(CompilerContract.CurrentTypedSlotPolicy.RejectsStructuralAgreementFailures);

            MicroOp?[] emptyBundle = new MicroOp?[8];
            Assert.True(SafetyVerifier.ValidateTypedSlotFacts(default, emptyBundle));
            Assert.True(HybridCpuTypedSlotFactsEmitter.ValidateEmittedFacts(default));

            string typedSlotText = ReadRepoFile("HybridCPU_ISE\\Core\\Pipeline\\Safety\\SafetyVerifier.TypedSlot.cs");
            Assert.Contains("ValidationOnly", typedSlotText, StringComparison.Ordinal);

            string compilerText = ReadRepoFile("HybridCPU_Compiler\\Core\\IR\\Admission\\HybridCpuBundleBuilder.cs");
            Assert.Contains("ValidationOnly", compilerText, StringComparison.Ordinal);

            string admissionText = ReadRepoFile("HybridCPU_Compiler\\Core\\IR\\Model\\IrBundleAdmissionResult.cs");
            Assert.Contains("ValidationOnly", admissionText, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_11d_BridgePolicySurface_States_Current_Strict_And_Future_Required_Semantics()
        {
            string contractText = ReadRepoFile("HybridCPU_ISE\\Core\\Contracts\\CompilerContract.cs");
            Assert.Contains("CompilerTypedSlotPolicy", contractText, StringComparison.Ordinal);
            Assert.Contains("CompatibilityValidation", contractText, StringComparison.Ordinal);
            Assert.Contains("StrictVerification", contractText, StringComparison.Ordinal);
            Assert.Contains("RequiredForAdmission", contractText, StringComparison.Ordinal);

            string stagingText = ReadRepoFile("Documentation\\WhiteBook\\22. typed-slot-contract-staging.md");
            Assert.Contains("CompatibilityValidation", stagingText, StringComparison.Ordinal);
            Assert.Contains("quarantine", stagingText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("strict verification", stagingText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("future seam", stagingText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void T9_11e_ValidationSummary_Cites_StagingArtifact_And_AgreementProofSuites()
        {
            string text = ReadRepoFile("Documentation\\WhiteBook\\13. validation-status-and-test-evidence.md");

            Assert.Contains("Phase09TypedSlotFactStagingDocumentationTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("Stage7AgreementTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("CompilerV5ContractAlignmentTests.cs", text, StringComparison.Ordinal);
            Assert.Contains("validation-baseline.md", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dotnet test", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("dotnet vstest", text, StringComparison.OrdinalIgnoreCase);
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
