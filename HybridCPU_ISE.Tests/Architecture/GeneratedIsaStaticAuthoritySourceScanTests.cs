namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// C#-first ISA authority tripwires: handwritten typed rows are compiled and
/// generated artifacts are projections, never a feed-back source for the registry.
/// </summary>
public sealed class GeneratedIsaStaticAuthoritySourceScanTests
{
    private static readonly string[] LegacyRowFiles =
    [
        "NonRTL/Arch/OpcodeInfo.Registry.Data.MemoryControl.cs",
        "NonRTL/Arch/OpcodeInfo.Registry.Data.Scalar.cs",
        "NonRTL/Arch/OpcodeInfo.Registry.Data.System.cs",
        "NonRTL/Arch/OpcodeInfo.Registry.Data.Vector.cs",
    ];

    [Fact]
    public void TypedOpcodeRows_AreCompiledAndAreTheOnlyHandwrittenRowSource()
    {
        string root = FindRepositoryRoot();
        string projectText = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "HybridCPU_ISE.csproj"));

        foreach (string legacyRow in LegacyRowFiles)
        {
            Assert.DoesNotContain($"<Compile Remove=\"{legacyRow.Replace('/', '\\')}\"", projectText, StringComparison.Ordinal);
        }

        string sourceRoot = Path.Combine(root, "HybridCPU_ISE");
        string generatedPath = Path.Combine(sourceRoot, "NonRTL", "Arch", "Generated", "GeneratedIsaShadowCatalog.g.cs");
        string[] handwrittenConstructors = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase) &&
                           File.ReadAllText(path).Contains("new OpcodeInfo(", StringComparison.Ordinal))
            .Where(path => !string.Equals(path, generatedPath, StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(sourceRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(LegacyRowFiles.OrderBy(path => path, StringComparer.Ordinal), handwrittenConstructors);
    }

    [Fact]
    public void IsaV4Surface_RemainsTheCSharpStaticPolicySurface()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "NonRTL", "Arch", "IsaV4Surface.cs"));

        Assert.Contains("GeneratedIsaCatalog.GetStaticPolicy", source, StringComparison.Ordinal);
        Assert.Contains("GeneratedIsaCatalog.PipelineClassMap", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new HashSet", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Dictionary", source, StringComparison.Ordinal);
    }

    [Fact]
    public void InstructionRegistry_StaticBindingSelectionReadsTheGeneratedRegistryFacade()
    {
        string root = FindRepositoryRoot();
        string diagnosticsRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Diagnostics");
        string source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(diagnosticsRoot, "InstructionRegistry*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        Assert.Contains("OpcodeRegistry.GetInfo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpcodeInfo(", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the HybridCPU repository root.");
    }
}
