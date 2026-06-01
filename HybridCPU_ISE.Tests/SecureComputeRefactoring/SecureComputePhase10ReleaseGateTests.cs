using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureComputePhase10ReleaseGateTests
{
    private static readonly Regex[] ForbiddenAffirmativeDocClaims =
    {
        new(@"\bVmxCaps\s+grants\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMCS\s+owns\s+secure\s+state\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMX\s+activates\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVmxCaps\.SecureCompute\s*=\s*true\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMCS\.SecureState\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMCS\s+stores\s+secure\s+state\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bPhase\s+[789]\b.*\bproduction-ready\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bPhase\s+[789]\b.*\bfeature-complete\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bPhase\s+[789]\b.*\bpositive\s+secure\s+backend\s+execution\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly string[] ForbiddenLayerOneTwoSourceTokens =
    {
        "VmxCaps.Secure",
        "VmcsManager",
        "IVmcsManager",
        "VmxExecutionUnit",
        "ReadFieldValue(",
        "WriteFieldValue(",
        "BackendExecutionAuthorized: true",
        "MutableNestedStateAuthorized: true",
        "InstructionEncoding",
        "Decoder",
        "Encoder",
        "OperandForm",
        "AddressingMode",
        "CapabilityAware",
        "CapabilityRegister",
        "CapabilityRegisters",
        "CapabilityOperand",
        "CapabilityMetadata",
        "TaggedMemory",
        "TagProvenance",
        "ProvisionalTag",
        "TagCheckpoint",
        "ProvenanceCheckpoint",
    };

    [Fact]
    public void Phase10Plans_RecordReleaseGateStatusAndHardeningInvariants()
    {
        string plan10 = ReadPlan("10-nested-secure-domain-plan.md");
        string plan11 = ReadPlan("11-test-and-conformance-master-plan.md");
        string plan12 = ReadPlan("12-phasing-and-pr-breakdown.md");
        string plan13 = ReadPlan("13-future-capability-aware-isa-memory-layer.md");

        Assert.Contains("Design fence only: this plan does not implement nested secure backend entry", plan10);
        Assert.Contains("does not authorize backend success", plan10);
        Assert.Contains("Phase 10 release gate closed on 2026-05-31", plan10);

        Assert.Contains("Positive policy-admission tests:", plan11);
        Assert.Contains("Positive runtime-execution tests:", plan11);
        Assert.Contains("not opened for secure backend execution", plan11);
        Assert.Contains("Phase 10 conformance hardening and stale-doc cleanup closed on 2026-05-31", plan11);

        Assert.Contains("Files 11-13 are meta/future documents, not implementation phases 11-13", plan12);
        Assert.Contains("Phase 10 - conformance hardening and stale-doc cleanup release gate closed on 2026-05-31", plan12);
        Assert.Contains("Mandatory gates:", plan12);
        Assert.Contains("status-label audit", plan12);
        Assert.Contains("production-claim audit", plan12);
        Assert.Contains("Phase 7: runtime descriptor/grant discipline + negative conformance; not CHERI ISA.", plan12);
        Assert.Contains("Phase 8: VMX deny/projection matrix + negative conformance; not secure VMCS.", plan12);
        Assert.Contains("Phase 9: design fence + negative conformance; not nested secure execution.", plan12);

        Assert.Contains("Import ban: current Layer 1/Layer 2 product code must not reference future capability-aware types", plan13);
        Assert.Contains("Lane6/Lane7 paths may carry only neutral descriptor/evidence envelopes", plan13);
        Assert.Contains("Any capability-aware ISA or memory change requires a new RFC / architecture decision record", plan13);
        Assert.Contains("current SecureCompute migration descriptors must not add provisional tag/provenance checkpoint fields", plan13);
        Assert.Contains("Phase 10 release gate closed on 2026-05-31", plan13);
    }

    [Fact]
    public void SecureComputeDocs_DoNotMakeAffirmativeVmxOrProductionClaims()
    {
        List<string> failures = new();
        foreach (string path in EnumerateSecureComputeMarkdownSources())
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Regex pattern in ForbiddenAffirmativeDocClaims)
                {
                    if (!pattern.IsMatch(lines[index]) ||
                        IsForbiddenOrNegativeContext(lines, index))
                    {
                        continue;
                    }

                    failures.Add($"{Relative(path)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Forbidden affirmative SecureCompute documentation claims:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void LayerOneTwoSecureComputeSources_DoNotImportFutureIsaOrVmxAuthority()
    {
        List<string> failures = new();
        foreach (string path in EnumerateLayerOneTwoProductionSources())
        {
            string source = File.ReadAllText(path);
            foreach (string token in ForbiddenLayerOneTwoSourceTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    failures.Add($"{Relative(path)} contains forbidden token `{token}`.");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Layer 1/2 SecureCompute product sources must remain descriptor/grant-only:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void PhaseSevenEightNineEvidenceTestsRemainPresent()
    {
        string secureTests = ReadProjectSource(
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureAuthorityDisciplineTests.cs",
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureIoHypercallPolicyTests.cs",
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureNestedDomainDesignFenceTests.cs");
        string vmxTests = ReadProjectSource(
            "HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase8BoundaryMatrixTests.cs",
            "HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase9NestedFenceTests.cs");

        Assert.Contains("SecureGrantAuthority_ValidScalarShapeWithoutProvenanceIsDenied", secureTests);
        Assert.Contains("SecureGrantAuthority_CompatibilityProjectionCannotSatisfySecureAuthority", secureTests);
        Assert.Contains("SecureHypercall_AdmittedDeniedDoesNotAuthorizeBackendSuccess", secureTests);
        Assert.Contains("SecureNestedChildIntent_ValidSubsetIsDesignFenceOnly", secureTests);

        Assert.Contains("SecureComputeVmxReadMatrix_DeniesSecureSensitiveCompatibilityFields", vmxTests);
        Assert.Contains("SecureComputeVmxWriteVmxCapsCheckpointAndBackendProjectionAreDenied", vmxTests);
        Assert.Contains("SecureComputeVmxNestedSources_KeepShadowVmcsAsCompatibilityBridgeOnly", vmxTests);
    }

    private static bool IsForbiddenOrNegativeContext(string[] lines, int index)
    {
        int first = Math.Max(0, index - 5);
        int count = Math.Min(lines.Length - first, 11);
        string window = string.Join(" ", lines.Skip(first).Take(count)).ToLowerInvariant();

        return window.Contains("forbidden", StringComparison.Ordinal) ||
            window.Contains("banned", StringComparison.Ordinal) ||
            window.Contains("banning", StringComparison.Ordinal) ||
            window.Contains("ban ", StringComparison.Ordinal) ||
            window.Contains("should be banned", StringComparison.Ordinal) ||
            window.Contains("must be banned", StringComparison.Ordinal) ||
            window.Contains("баниться", StringComparison.Ordinal) ||
            window.Contains("запрет", StringComparison.Ordinal) ||
            window.Contains("запрещ", StringComparison.Ordinal) ||
            window.Contains("cannot", StringComparison.Ordinal) ||
            window.Contains("can not", StringComparison.Ordinal) ||
            window.Contains("must not", StringComparison.Ordinal) ||
            window.Contains("do not", StringComparison.Ordinal) ||
            window.Contains("does not", StringComparison.Ordinal) ||
            window.Contains("not ", StringComparison.Ordinal) ||
            window.Contains("no ", StringComparison.Ordinal) ||
            window.Contains("non-goals", StringComparison.Ordinal) ||
            window.Contains("deny", StringComparison.Ordinal) ||
            window.Contains("denied", StringComparison.Ordinal) ||
            window.Contains("reject", StringComparison.Ordinal) ||
            window.Contains("wrong form", StringComparison.Ordinal) ||
            window.Contains("bad example", StringComparison.Ordinal) ||
            window.Contains("not implementation guidance", StringComparison.Ordinal) ||
            window.Contains("regression", StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateSecureComputeMarkdownSources()
    {
        string root = SecureComputeRoot();
        return Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .Where(path => path.Contains(Path.DirectorySeparatorChar + "Plan" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                path.Contains(Path.DirectorySeparatorChar + "Plan2" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                path.Contains(Path.DirectorySeparatorChar + "Docs" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateLayerOneTwoProductionSources()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] roots =
        {
            Path.Combine(repositoryRoot, "HybridCPU_ISE", "CloseToRTL", "Core", "Runtime", "Domains", "SecureCompute"),
            Path.Combine(repositoryRoot, "HybridCPU_ISE", "CloseToRTL", "Core", "Virtualization", "SecureCompute"),
        };

        return roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Conformance" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string ReadPlan(string fileName) =>
        File.ReadAllText(Path.Combine(SecureComputeRoot(), "Plan", fileName));

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string root = FindRepositoryRoot();
        return string.Concat(relativePaths.Select(path => File.ReadAllText(Path.Combine(
            root,
            path.Replace('/', Path.DirectorySeparatorChar)))));
    }

    private static string SecureComputeRoot() =>
        Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "CloseToRTL",
            "Core",
            "Runtime",
            "Domains",
            "SecureCompute");

    private static string Relative(string path) =>
        Path.GetRelativePath(FindRepositoryRoot(), path).Replace('\\', '/');

    private static string FindRepositoryRoot()
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
