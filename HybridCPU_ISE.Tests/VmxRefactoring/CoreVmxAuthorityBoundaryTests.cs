using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests;

public sealed class CoreVmxAuthorityBoundaryTests
{
    [Fact]
    public void CoreVmx_DoesNotUseDirectLegacyAuthorityHooks()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string coreVmxRoot = Path.Combine(projectRoot, "Core", "VMX");
        var contract = new CoreVmxAuthorityBoundaryContract();

        foreach (string file in Directory.EnumerateFiles(coreVmxRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');

            if (relativePath.StartsWith("Core/VMX/Conformance/", StringComparison.Ordinal))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                CoreVmxAuthorityBoundaryViolation violation =
                    contract.EvaluateLine(relativePath, lines[index]);

                Assert.True(
                    violation == CoreVmxAuthorityBoundaryViolation.None,
                    $"{relativePath}:{index + 1} violates {violation}: {lines[index].Trim()}");
            }
        }
    }

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
