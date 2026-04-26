using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09DirectFactoryCallerBoundaryTests
{
    [Fact]
    public void InstructionRegistryCreateMicroOp_ProductionCallersStayInsideProjectorOrRegistryHelpers()
    {
        string repoRoot = FindRepoRoot();
        string runtimeRoot = Path.Combine(repoRoot, "HybridCPU_ISE");
        string compilerRoot = Path.Combine(repoRoot, "HybridCPU_Compiler");
        string diagnosticsRoot = Path.Combine(runtimeRoot, "Core", "Diagnostics");
        string allowedProjectorPath = Path.Combine(
            runtimeRoot,
            "Core",
            "Decoder",
            "DecodedBundleTransportProjector.cs");

        var unexpectedCallSites = new List<string>();
        foreach (string productionRoot in new[] { runtimeRoot, compilerRoot })
        {
            foreach (string filePath in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                bool isAllowedFile =
                    filePath.Equals(allowedProjectorPath, StringComparison.OrdinalIgnoreCase) ||
                    filePath.StartsWith(diagnosticsRoot, StringComparison.OrdinalIgnoreCase);
                if (isAllowedFile)
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(filePath);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (lines[lineIndex].Contains("InstructionRegistry.CreateMicroOp(", StringComparison.Ordinal))
                    {
                        unexpectedCallSites.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{lineIndex + 1}");
                    }
                }
            }
        }

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void PublishedSemanticHelpers_DoNotRoundTripSystemAtomicStreamOrVmxContoursThroughCreateMicroOp()
    {
        string repoRoot = FindRepoRoot();
        string helperPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Diagnostics",
            "InstructionRegistry.Helpers.Core.cs");
        string source = File.ReadAllText(helperPath);

        string[] removedRoundTrips =
        {
            "CreateMicroOp(opCode, context) is not SysEventMicroOp",
            "CreateMicroOp(opCode, context) is not StreamControlMicroOp",
            "CreateMicroOp(opCode, context) is not AtomicMicroOp",
            "CreateMicroOp(opCode, context) is not VmxMicroOp"
        };

        foreach (string removedRoundTrip in removedRoundTrips)
        {
            Assert.DoesNotContain(removedRoundTrip, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PublishedCsrAndSystemReadHelpers_DoNotRoundTripThroughCreateMicroOp()
    {
        string repoRoot = FindRepoRoot();
        string coreHelperPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Diagnostics",
            "InstructionRegistry.Helpers.Core.cs");
        string csrHelperPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Diagnostics",
            "InstructionRegistry.Helpers.Csr.cs");

        string coreSource = File.ReadAllText(coreHelperPath);
        string csrSource = File.ReadAllText(csrHelperPath);

        Assert.DoesNotContain(
            "switch (CreateMicroOp(opCode, context))",
            coreSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "branchMicroOp = CreateMicroOp(opCode, context) as BranchMicroOp;",
            coreSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "csrMicroOp = CreateMicroOp(opCode, context) as CSRMicroOp;",
            csrSource,
            StringComparison.Ordinal);
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
