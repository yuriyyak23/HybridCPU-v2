using System;
using System.IO;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09VectorRegistrationHelperSurfaceTests
{
    [Fact]
    public void InstructionRegistryVectorHelpers_DoNotRetainUnusedNonPublishedRegistrationDuplicates()
    {
        string repoRoot = FindRepoRoot();
        string filePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Diagnostics",
            "InstructionRegistry.Helpers.Vector.cs");
        string source = File.ReadAllText(filePath);

        string[] removedLegacyHelpers =
        {
            "private static void RegisterVectorBinaryOp(",
            "private static void RegisterVectorUnaryOp(",
            "private static void RegisterVectorFmaOp(",
            "private static void RegisterVectorReductionOp(",
            "private static void RegisterVectorComparisonOp(",
            "private static void RegisterVectorMaskOp(",
            "private static void RegisterVectorMaskPopCountOp(",
            "private static void RegisterVectorPermutationOp(",
            "private static void RegisterVectorPredicativeMovementOp(",
            "private static void RegisterVectorSlideOp(",
            "private static void RegisterVectorDotProductOp(",
            "private static void RegisterVectorOp("
        };

        foreach (string helperSignature in removedLegacyHelpers)
        {
            Assert.DoesNotContain(helperSignature, source, StringComparison.Ordinal);
        }
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
