using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCPU.Compiler.Core.API.Migration;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerPhase14CompatibilitySurfaceMigrationTests
{
    [Fact]
    public void EveryCompilerObsoleteSourceFileIsOwnedByExactlyOneCatalogRow()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string compilerRoot = Path.Combine(repoRoot, "HybridCPU_Compiler");
        string[] actual = Directory.EnumerateFiles(compilerRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path =>
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("[Obsolete", StringComparison.Ordinal))
            .Select(path => Normalize(Path.GetRelativePath(repoRoot, path)))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        string[] catalog = CompilerCompatibilitySurfaceCatalog.Rows
            .SelectMany(static row => row.SourceFiles)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(catalog.Distinct(StringComparer.Ordinal).Count(), catalog.Length);
        Assert.Equal(actual, catalog);
    }

    [Fact]
    public void CatalogRowsAreFailClosedRemovalEvidenceOnly()
    {
        Assert.Equal(
            "CompilerCompatibilitySurfaceCatalog/v1",
            CompilerCompatibilitySurfaceCatalog.CatalogVersion);
        Assert.NotEmpty(CompilerCompatibilitySurfaceCatalog.Rows);

        foreach (CompilerCompatibilitySurfaceRow row in CompilerCompatibilitySurfaceCatalog.Rows)
        {
            Assert.False(row.CreatesRuntimeAuthority);
            Assert.NotEmpty(row.SourceFiles);
            Assert.False(string.IsNullOrWhiteSpace(row.ReplacementSurface));
            Assert.False(string.IsNullOrWhiteSpace(row.LocalCallerEvidence));
            Assert.False(string.IsNullOrWhiteSpace(row.RemovalGate));
        }

        Assert.Equal(
            CompilerCompatibilityDisposition.ZeroLocalCallersCompatibilityWindowRequired,
            CompilerCompatibilitySurfaceCatalog.GetRequired("asm-facade-hierarchy").Disposition);
        Assert.Equal(
            CompilerCompatibilityDisposition.ZeroLocalCallersCompatibilityWindowRequired,
            CompilerCompatibilitySurfaceCatalog.GetRequired("raw-matrix-vector-thread-helpers").Disposition);
    }

    [Fact]
    public void PositiveEmissionRowsNameOnlyDecisionBearingTypedHelpers()
    {
        Assert.All(
            CompilerMatrixTilePositiveEmissionAbiContract.Rows,
            static row => Assert.EndsWith("WithDecision", row.HelperName, StringComparison.Ordinal));
        Assert.All(
            CompilerVectorTransferPositiveEmissionAbiContract.Rows,
            static row => Assert.EndsWith("WithDecision", row.HelperName, StringComparison.Ordinal));

        Assert.Empty(
            CompilerMatrixTilePositiveEmissionAbiContract.TypedPublicHelperNames.Intersect(
                CompilerMatrixTilePositiveEmissionAbiContract.CompatibilityHelperNames,
                StringComparer.Ordinal));
        Assert.Empty(
            CompilerVectorTransferPositiveEmissionAbiContract.TypedPublicHelperNames.Intersect(
                CompilerVectorTransferPositiveEmissionAbiContract.CompatibilityHelperNames,
                StringComparer.Ordinal));
    }

    [Fact]
    public void MigratedLocalClientsDoNotConsumeRawHelperOrBackendBoolSurfaces()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] clientRoots =
        [
            "CpuInterfaceBridge",
            "HybridCPU_EnvGUI",
            "MinimalAsmApp",
            "TestAssemblerConsoleApps",
            "forms"
        ];
        var sources = new List<(string Path, string Text)>();
        foreach (string clientRoot in clientRoots)
        {
            string absoluteRoot = Path.Combine(repoRoot, clientRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            sources.AddRange(
                Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories)
                    .Where(static path =>
                        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                    .Select(path => (Normalize(Path.GetRelativePath(repoRoot, path)), File.ReadAllText(path))));
        }

        string[] forbidden =
        [
            "AppAsmFacade",
            "PlatformAsmFacade",
            "IAppAsmFacade",
            "IPlatformAsmFacade",
            "ExpertBackendFacade",
            "IExpertBackendFacade",
            ".CompileMtileLoad(",
            ".CompileMtileStore(",
            ".CompileMtileMacc(",
            ".CompileMtranspose(",
            ".CompileVload(",
            ".CompileVstore(",
            ".TryRecoverFromInstruction(",
            "CompilerBackendLoweringDecision.IsAllowed",
            "CanSelectForProductionLowering("
        ];
        foreach ((string path, string text) in sources)
        {
            foreach (string token in forbidden)
            {
                Assert.False(
                    text.Contains(token, StringComparison.Ordinal),
                    $"Legacy compiler token '{token}' remains in local client {path}.");
            }
        }
    }

    [Fact]
    public void MatrixTileExamplesUseTypedDecisionHelpersWithoutFacadeSuppression()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string exampleRoot = Path.Combine(repoRoot, "MinimalAsmApp", "Examples", "Matrix");
        string combined = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(exampleRoot, "*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("AppAsmFacade", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("#pragma warning disable CS0618", combined, StringComparison.Ordinal);
        Assert.Contains("CompileMtileLoadWithDecision", combined, StringComparison.Ordinal);
        Assert.Contains("CompileMtileStoreWithDecision", combined, StringComparison.Ordinal);
        Assert.Contains("CompileMtileMaccWithDecision", combined, StringComparison.Ordinal);
        Assert.Contains("CompileMtransposeWithDecision", combined, StringComparison.Ordinal);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
