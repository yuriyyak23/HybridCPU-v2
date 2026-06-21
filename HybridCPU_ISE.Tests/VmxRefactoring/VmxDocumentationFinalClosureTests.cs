namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxDocumentationMigrationClaimHygieneTests
{
    [Fact]
    public void DocumentationMigration_IsClosedAsClaimHygieneOnly()
    {
        string phase14 = ReadPlan("14_documentation_migration_and_claim_hygiene.md");
        string phase15 = ReadPlan("15_final_readiness_review_and_next_work_order.md");

        Assert.Contains("ADR-VIRT-DOC-CLAIM-HYGIENE-2026-06-05", phase14);
        Assert.Contains("VmxDocumentationMigrationClaimHygieneTests", phase14);
        Assert.Contains("documentation migration and claim hygiene only", phase14);
        Assert.Contains("does not modify production runtime code", phase14);
        Assert.Contains("No remaining Phase 14 work requires ISE CPU production implementation", phase14);
        Assert.Contains("ADR-VIRT-DOC-CLAIM-HYGIENE-2026-06-05", phase15);

        foreach (string status in new[]
                 {
                     "implemented",
                     "projection-only",
                     "denied",
                     "model/helper-only",
                     "future-gated",
                     "forbidden",
                 })
        {
            Assert.Contains(status, phase14);
        }

        string claimText = RemoveStaticGateCommandLines(phase14);
        Assert.DoesNotContain("...`", claimText, StringComparison.Ordinal);
    }

    [Fact]
    public void PhaseDocumentation_UsesRepoRootAnchorsAndAvoidsForbiddenActivationClaims()
    {
        string docs = RemoveStaticGateCommandLines(ReadAllPlanDocs());

        foreach (string requiredAnchor in new[]
                 {
                     "HybridCPU_ISE/CloseToRTL/",
                     "HybridCPU_ISE.Tests",
                     "VirtualiztionRefactoringNew",
                 })
        {
            Assert.Contains(requiredAnchor, docs);
        }

        foreach (string forbidden in ForbiddenOverclaimPhrases())
        {
            Assert.DoesNotContain(forbidden, docs, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static string[] ForbiddenOverclaimPhrases() =>
    [
        "runtime activation approved",
        "activation approved",
        "production ready",
        "SecureCompute supported via VMX",
        "VMWRITE allowed",
        "VMCALL backend success allowed",
        "completion publication allowed",
        "retire publication allowed",
        "examples production authority",
    ];

    internal static string ReadPlan(string fileName) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "VirtualiztionRefactoringNew",
            fileName));

    internal static string ReadAllPlanDocs()
    {
        string planRoot = Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "VirtualiztionRefactoringNew");

        return string.Join(
            Environment.NewLine,
            Directory
                .GetFiles(planRoot, "*.md", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    internal static string RemoveStaticGateCommandLines(string text)
    {
        string[] ignoredMarkers =
        [
            "rg -n",
            "scan",
            "expected `NO_MATCH`",
            "Documentation overclaim",
        ];

        return string.Join(
            Environment.NewLine,
            text
                .Split([Environment.NewLine], StringSplitOptions.None)
                .Where(line => !ignoredMarkers.Any(marker =>
                    line.Contains(marker, StringComparison.OrdinalIgnoreCase))));
    }

    internal static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}

public sealed class VmxFinalReadinessReviewClosureTests
{
    [Fact]
    public void FinalReadiness_ClosesWithoutProductionCodeWorkOrder()
    {
        string phase15 = VmxDocumentationMigrationClaimHygieneTests.ReadPlan(
            "15_final_readiness_review_and_next_work_order.md");
        string index = VmxDocumentationMigrationClaimHygieneTests.ReadPlan(
            "00_refactoring_plan_index.md");

        Assert.Contains("ADR-VIRT-FINAL-READINESS-2026-06-05", phase15);
        Assert.Contains("GO for documentation/readiness/test/static-gate maintenance", phase15);
        Assert.Contains("NO-GO for active runtime virtualization", phase15);
        Assert.Contains("NO immediate ISE CPU production-code task remains", phase15);
        Assert.Contains("Future production work requires a fresh owner-specific RFC/ADR", phase15);
        Assert.Contains("Final readiness matrix", phase15);
        Assert.Contains("No immediate ISE CPU production-code task remains", index);

        foreach (string deniedSurface in new[]
                 {
                     "VMX backend execution",
                     "VMWRITE",
                     "mutable VMCS state",
                     "VMCALL backend success",
                     "SecureCompute through VMX/VMCS/`VmxCaps`",
                     "Nested execution",
                     "Completion publication",
                     "Retire publication",
                 })
        {
            Assert.Contains(deniedSurface, phase15);
        }
    }
}

public sealed class VmxExternalAuditActivationReadinessAddendumTests
{
    [Fact]
    public void ExternalAuditAddendum_ClosesAuditWithoutActivationApproval()
    {
        string phase16 = VmxDocumentationMigrationClaimHygieneTests.ReadPlan(
            "16_external_audit_activation_readiness_addendum.md");
        string phase15 = VmxDocumentationMigrationClaimHygieneTests.ReadPlan(
            "15_final_readiness_review_and_next_work_order.md");

        Assert.StartsWith("# Phase 16 - External Audit Activation Readiness Addendum", phase16);
        Assert.Contains("ADR-VIRT-EXTERNAL-AUDIT-READINESS-2026-06-05", phase16);
        Assert.Contains("NO-GO for active runtime virtualization", phase16);
        Assert.Contains("NO immediate ISE CPU production-code task remains", phase16);
        Assert.Contains("new owner-specific RFC/ADR", phase16);
        Assert.Contains("No next phase is opened by this corpus", phase16);
        Assert.Contains("Phase 16 is the external audit activation-readiness addendum", phase15);

        string claimText = VmxDocumentationMigrationClaimHygieneTests.RemoveStaticGateCommandLines(phase16);
        foreach (string forbidden in VmxDocumentationMigrationClaimHygieneTests.ForbiddenOverclaimPhrases())
        {
            Assert.DoesNotContain(forbidden, claimText, StringComparison.OrdinalIgnoreCase);
        }
    }
}
