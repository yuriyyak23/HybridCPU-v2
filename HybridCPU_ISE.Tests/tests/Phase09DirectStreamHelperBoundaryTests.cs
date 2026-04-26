using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09DirectStreamHelperBoundaryTests
{
    [Fact]
    public void ExecuteStreamInstruction_RuntimeHelperIsRemovedFromCpuCoreSurface()
    {
        Assert.Null(typeof(Processor.CPU_Core).GetMethod(
            "ExecuteStreamInstruction",
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic));

        string repoRoot = FindRepoRoot();
        string helperPath = Path.Combine(repoRoot, "HybridCPU_ISE", "Core", "System", "CPU_Core.System.cs");
        string source = File.ReadAllText(helperPath);
        Assert.DoesNotContain("ExecuteStreamInstruction(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectCompatRetireCarrierSurface_IsAbsentFromRuntimeAndTestSupport()
    {
        string repoRoot = FindRepoRoot();
        string productionRoot = Path.Combine(repoRoot, "HybridCPU_ISE");

        string[] forbiddenPatterns =
        {
            "DirectCompatRetireTransaction",
            "DirectCompatRetireTypedEffectKind",
            "ResolveDirectCompatRetireTransaction(",
            "ApplyDirectCompatRetireTransaction(",
            "ProjectDirectCompatRetireTransactionFromRetireBatch(",
            "TestApplyDirectCompatRetireTransaction(",
            "DirectCompatRetireApplyCount"
        };

        var unexpectedCallSites = new List<string>();
        foreach (string filePath in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(filePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                foreach (string forbiddenPattern in forbiddenPatterns)
                {
                    if (lines[lineIndex].Contains(forbiddenPattern, StringComparison.Ordinal))
                    {
                        unexpectedCallSites.Add(
                            $"{Path.GetRelativePath(repoRoot, filePath)}:{lineIndex + 1}:{forbiddenPattern}");
                    }
                }
            }
        }

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void TestExecuteDirectStreamCompat_HasNoProductionCallSites()
    {
        string repoRoot = FindRepoRoot();
        string productionRoot = Path.Combine(repoRoot, "HybridCPU_ISE");

        var unexpectedCallSites = new List<string>();
        foreach (string filePath in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(filePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (lines[lineIndex].Contains(".TestExecuteDirectStreamCompat(", StringComparison.Ordinal))
                {
                    unexpectedCallSites.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{lineIndex + 1}");
                }
            }
        }

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void StreamEngineCaptureRetireWindowPublications_ProductionCallersStayInsideCpuCoreTestSupport()
    {
        string repoRoot = FindRepoRoot();
        string productionRoot = Path.Combine(repoRoot, "HybridCPU_ISE");
        string allowedTestSeamPath = Path.Combine(
            productionRoot,
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.TestSupport.cs");

        var unexpectedCallSites = new List<string>();
        foreach (string filePath in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            if (filePath.Equals(allowedTestSeamPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(filePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (lines[lineIndex].Contains("StreamEngine.CaptureRetireWindowPublications(", StringComparison.Ordinal))
                {
                    unexpectedCallSites.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{lineIndex + 1}");
                }
            }
        }

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void Phase09RetireContractClosureTests_UsesRetireWindowSnapshotContourInsteadOfLegacyCompatCarrier()
    {
        string repoRoot = FindRepoRoot();
        string retireContractClosurePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE.Tests",
            "tests",
            "Phase09RetireContractClosureTests.cs");

        string[] forbiddenPatterns =
        {
            ".ResolveDirectCompatRetireTransaction(",
            "TestApplyDirectCompatRetireTransaction(",
            "DirectCompatRetireTransaction transaction =",
            "DirectCompatRetireTypedEffectKind.",
            "DirectCompatRetireApplyCount"
        };

        var unexpectedCarrierUsages = new List<string>();
        string[] lines = File.ReadAllLines(retireContractClosurePath);
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            foreach (string forbiddenPattern in forbiddenPatterns)
            {
                if (lines[lineIndex].Contains(forbiddenPattern, StringComparison.Ordinal))
                {
                    unexpectedCarrierUsages.Add(
                        $"{Path.GetRelativePath(repoRoot, retireContractClosurePath)}:{lineIndex + 1}:{forbiddenPattern}");
                }
            }
        }

        Assert.Empty(unexpectedCarrierUsages);
    }

    [Fact]
    public void RemainingRetainedProjectionSuites_DoNotCallDirectCompatResolveOrCarrierContours()
    {
        string repoRoot = FindRepoRoot();
        string testsRoot = Path.Combine(repoRoot, "HybridCPU_ISE.Tests");

        string[] targetRelativePaths =
        {
            Path.Combine("tests", "Phase04DirectCompatRetireTransactionTests.cs"),
            Path.Combine("tests", "Phase03CanonicalOpcodeIdentityTests.cs"),
            Path.Combine("tests", "Phase09StreamEngineDeferredParityTests.cs"),
            Path.Combine("tests", "Phase09VmxSubsystemTests.cs"),
            Path.Combine("tests", "Phase1SemanticCoreTests.cs")
        };
        string[] forbiddenPatterns =
        {
            ".ResolveDirectCompatRetireTransaction(",
            "TestApplyDirectCompatRetireTransaction(",
            "DirectCompatRetireTransaction transaction =",
            "private static DirectCompatRetireTransaction Resolve(",
            "DirectCompatRetireTypedEffectKind.",
            "DirectCompatRetireApplyCount"
        };

        var unexpectedCarrierUsages = new List<string>();
        foreach (string relativePath in targetRelativePaths)
        {
            string targetPath = Path.Combine(testsRoot, relativePath);
            string[] lines = File.ReadAllLines(targetPath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                foreach (string forbiddenPattern in forbiddenPatterns)
                {
                    if (lines[lineIndex].Contains(forbiddenPattern, StringComparison.Ordinal))
                    {
                        unexpectedCarrierUsages.Add(
                            $"{Path.GetRelativePath(repoRoot, targetPath)}:{lineIndex + 1}:{forbiddenPattern}");
                    }
                }
            }
        }

        Assert.Empty(unexpectedCarrierUsages);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            bool hasRepoLayout =
                Directory.Exists(Path.Combine(current.FullName, "Documentation")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests"));
            if (hasRepoLayout)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate HybridCPU ISE repository root from test output directory.");
    }
}
