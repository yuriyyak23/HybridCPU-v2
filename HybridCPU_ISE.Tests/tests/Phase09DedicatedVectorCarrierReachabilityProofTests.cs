using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09DedicatedVectorCarrierReachabilityProofTests
{
    [Fact]
    public void LiveProductionVectorCarrierInstantiationSet_StaysPinnedToPublishedFamilies()
    {
        string repoRoot = FindRepoRoot();
        string productionRoot = Path.Combine(repoRoot, "HybridCPU_ISE");
        string helperPath = Path.Combine(
            productionRoot,
            "Core",
            "Diagnostics",
            "InstructionRegistry.Helpers.Vector.cs");

        var expectedTypes = new SortedSet<string>(StringComparer.Ordinal)
        {
            "VectorBinaryOpMicroOp",
            "VectorComparisonMicroOp",
            "VectorDotProductMicroOp",
            "VectorFmaMicroOp",
            "VectorMaskOpMicroOp",
            "VectorMaskPopCountMicroOp",
            "VectorPermutationMicroOp",
            "VectorPredicativeMovementMicroOp",
            "VectorReductionMicroOp",
            "VectorSlideMicroOp",
            "VectorTransferMicroOp",
            "VectorUnaryOpMicroOp"
        };

        var discoveredTypes = new SortedSet<string>(StringComparer.Ordinal);
        var unexpectedCallSites = new List<string>();
        var pattern = new Regex(@"new\s+(Vector[A-Za-z0-9_]+MicroOp)\b", RegexOptions.Compiled);

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
                MatchCollection matches = pattern.Matches(lines[lineIndex]);
                foreach (Match match in matches)
                {
                    string typeName = match.Groups[1].Value;
                    discoveredTypes.Add(typeName);

                    if (!filePath.Equals(helperPath, StringComparison.OrdinalIgnoreCase))
                    {
                        unexpectedCallSites.Add(
                            $"{Path.GetRelativePath(repoRoot, filePath)}:{lineIndex + 1}:{typeName}");
                    }
                }
            }
        }

        Assert.Empty(unexpectedCallSites);
        Assert.Equal(expectedTypes, discoveredTypes);
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
