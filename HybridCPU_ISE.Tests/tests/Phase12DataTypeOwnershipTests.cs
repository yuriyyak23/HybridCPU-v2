using System;
using System.IO;
using Xunit;

namespace HybridCPU_ISE.Tests;

public sealed class Phase12DataTypeOwnershipTests
{
    [Fact]
    public void CanonicalDataTypeEnum_LivesUnderArchInsteadOfCompat()
    {
        string repoRoot = FindRepoRoot();
        string canonicalPath = Path.Combine(repoRoot, "HybridCPU_ISE", "Arch", "DataTypeEnum.cs");
        string compatPath = Path.Combine(repoRoot, "HybridCPU_ISE", "Arch", "Compat", "DataTypeEnum.cs");
        string dataTypeUtilsPath = Path.Combine(repoRoot, "HybridCPU_ISE", "Arch", "DataType.cs");

        Assert.True(File.Exists(canonicalPath));
        Assert.False(File.Exists(compatPath));

        string dataTypeUtilsSource = File.ReadAllText(dataTypeUtilsPath);
        Assert.Contains("compat containers project into it but do not own it", dataTypeUtilsSource, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate repository root for DataType ownership assertions.");
    }
}
