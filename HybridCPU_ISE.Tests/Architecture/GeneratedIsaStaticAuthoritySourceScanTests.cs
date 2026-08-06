namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// Manifest-input ISA authority tripwires: no handwritten opcode rows remain,
/// and generated artifacts never become runtime JSON input.
/// </summary>
public sealed class GeneratedIsaStaticAuthoritySourceScanTests
{
    [Fact]
    public void GeneratedCatalog_IsTheOnlyOpcodeInfoRowConstructorSource()
    {
        string root = FindRepositoryRoot();
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

        Assert.Empty(handwrittenConstructors);
    }

    [Fact]
    public void OpcodeRegistry_RuntimeServingArrayIsAnIndependentGeneratedCatalogCopy()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "NonRTL", "Arch", "OpcodeInfo.Registry.Data.cs"));

        Assert.Contains("using YAKSys_Hybrid_CPU.Arch.Generated;", source, StringComparison.Ordinal);
        Assert.Contains("public static readonly OpcodeInfo[] Opcodes = [.. GeneratedIsaCatalog.Opcodes];", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public static readonly OpcodeInfo[] Opcodes = BuildOpcodes();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildOpcodes", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_UsesStrictManifestInputAndVerifiesCommittedGeneratedCatalog()
    {
        string root = FindRepositoryRoot();
        string generator = File.ReadAllText(Path.Combine(root, "tools", "HybridCPU.IsaGen", "Program.cs"));

        Assert.Contains("var catalog = ReadStrictManifest(manifestPath);", generator, StringComparison.Ordinal);
        Assert.Contains("VerifyGeneratedCatalogParity(catalog);", generator, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteDeterministic(manifestPath", generator, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCSharpCatalog", generator, StringComparison.Ordinal);
        Assert.DoesNotContain("VerifyCSharpMirrorParity", generator, StringComparison.Ordinal);
    }

    [Fact]
    public void IsaV4Surface_RemainsTheGeneratedRuntimeStaticPolicyFacade()
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
