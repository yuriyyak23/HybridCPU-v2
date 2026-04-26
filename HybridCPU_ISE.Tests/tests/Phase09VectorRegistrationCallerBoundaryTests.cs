using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09VectorRegistrationCallerBoundaryTests
{
    [Fact]
    public void VectorRegistrationHelpers_ProductionCallersStayInsideInstructionRegistryInitializeVector()
    {
        string repoRoot = FindRepoRoot();
        string productionRoot = Path.Combine(repoRoot, "HybridCPU_ISE");
        string allowedCallerPath = Path.Combine(
            productionRoot,
            "Core",
            "Diagnostics",
            "InstructionRegistry.Initialize.Vector.cs");
        string helperDefinitionPath = Path.Combine(
            productionRoot,
            "Core",
            "Diagnostics",
            "InstructionRegistry.Helpers.Vector.cs");

        string[] helperCalls =
        {
            "RegisterPublishedVectorBinaryOp(",
            "RegisterVectorTransferOp(",
            "RegisterPublishedVectorUnaryOp(",
            "RegisterPublishedVectorFmaOp(",
            "RegisterPublishedVectorReductionOp(",
            "RegisterPublishedVectorComparisonOp(",
            "RegisterPublishedVectorMaskOp(",
            "RegisterPublishedVectorMaskPopCountOp(",
            "RegisterPublishedVectorPermutationOp(",
            "RegisterPublishedVectorPredicativeMovementOp(",
            "RegisterPublishedVectorSlideOp(",
            "RegisterPublishedVectorDotProductOp(",
            "RegisterVectorConfigOp("
        };

        var unexpectedCallSites = new List<string>();
        foreach (string filePath in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            if (filePath.Equals(allowedCallerPath, StringComparison.OrdinalIgnoreCase) ||
                filePath.Equals(helperDefinitionPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(filePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                foreach (string helperCall in helperCalls)
                {
                    if (lines[lineIndex].Contains(helperCall, StringComparison.Ordinal))
                    {
                        unexpectedCallSites.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{lineIndex + 1}:{helperCall}");
                    }
                }
            }
        }

        Assert.Empty(unexpectedCallSites);
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
