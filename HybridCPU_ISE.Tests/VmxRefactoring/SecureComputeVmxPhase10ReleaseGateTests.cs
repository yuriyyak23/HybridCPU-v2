using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class SecureComputeVmxPhase10ReleaseGateTests
{
    private static readonly Regex[] ForbiddenVmxDocClaims =
    {
        new(@"\bVmxCaps\s+grants\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVmxCaps\s+enables\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMCS\s+owns\s+secure\s+state\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMCS\s+stores\s+secure\s+state\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMX\s+activates\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMX\s+owns\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly string[] ForbiddenCompatibilitySourceTokens =
    {
        "VmxCaps.Secure",
        "VmcsManager",
        "IVmcsManager",
        "VmxExecutionUnit",
        "ReadFieldValue(",
        "WriteFieldValue(",
        "new SecureComputeDomainDescriptor",
        "SecureComputeSecurityLevel.",
        "BackendExecutionAuthorized: true",
        "MutableNestedStateAuthorized: true",
    };

    [Fact]
    public void VmxBoundaryPlans_RecordProjectionOnlyReleaseGate()
    {
        string plan09 = ReadPlan("09-vmx-compatibility-boundary-plan.md");
        string plan12 = ReadPlan("12-phasing-and-pr-breakdown.md");

        Assert.Contains("VMX remains frozen compatibility frontend", plan09);
        Assert.Contains("VMCS remains read-only compatibility projection, not state owner", plan09);
        Assert.Contains("projection of typed grants, not capability authority", plan09);
        Assert.Contains("VMX cannot activate SecureCompute", plan09);
        Assert.Contains("cannot grant SecureCompute", plan09);
        Assert.Contains("VMCS cannot store secure state", plan09);
        Assert.Contains("Next dependent work closed: Phase 10 release gate closed on 2026-05-31", plan09);

        Assert.Contains("Phase 8: VMX deny/projection matrix + negative conformance; not secure VMCS.", plan12);
        Assert.Contains("VMX no-authority, VMCS no-state-owner, VmxCaps no-grant", plan12);
        Assert.Contains("Phase 10 - conformance hardening and stale-doc cleanup release gate closed on 2026-05-31", plan12);
    }

    [Fact]
    public void VmxBoundaryDocs_DoNotMakeAffirmativeVmxOwnershipClaims()
    {
        List<string> failures = new();
        foreach (string path in EnumerateSecureComputeMarkdownSources())
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Regex pattern in ForbiddenVmxDocClaims)
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
            "Forbidden affirmative VMX/SecureCompute documentation claims:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void VmxCompatibilitySources_DoNotBecomeSecureComputeAuthorityPaths()
    {
        List<string> failures = new();
        foreach (string path in EnumerateVmxCompatibilityBoundarySources())
        {
            string source = File.ReadAllText(path);
            foreach (string token in ForbiddenCompatibilitySourceTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    failures.Add($"{Relative(path)} contains forbidden token `{token}`.");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "VMX compatibility sources must stay projection-only for SecureCompute:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
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

    private static IEnumerable<string> EnumerateVmxCompatibilityBoundarySources()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] roots =
        {
            Path.Combine(repositoryRoot, "HybridCPU_ISE", "CloseToRTL", "Core", "Virtualization", "SecureCompute", "Compatibility"),
            Path.Combine(repositoryRoot, "HybridCPU_ISE", "CloseToRTL", "Core", "Virtualization", "SecureCompute", "Generated"),
            Path.Combine(repositoryRoot, "HybridCPU_ISE", "CloseToRTL", "Core", "Virtualization", "Compatibility", "Generated", "VmcsProjection"),
        };

        return roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string ReadPlan(string fileName) =>
        File.ReadAllText(Path.Combine(SecureComputeRoot(), "Plan", fileName));

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
