using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-13.0 closed-world cleanup inventory. These guards record deletion
/// eligibility facts; they do not make any retained compatibility contour an
/// architectural authority.
/// </summary>
public sealed class Rf130CleanupEntryInventoryTests
{
    private static readonly string[] ProductionRoots =
    [
        "HybridCPU_ISE",
        "HybridCPU_Compiler",
        "CpuInterfaceBridge",
        "TestAssemblerConsoleApps",
        "tools",
    ];

    [Fact]
    public void StaticIsaCandidates_RecordGeneratedStorageAfterRawRowRemoval()
    {
        string facade = Read("HybridCPU_ISE", "NonRTL", "Arch", "IsaV4Surface.cs");
        Assert.Contains("GeneratedIsaCatalog.GetStaticPolicy", facade, StringComparison.Ordinal);
        Assert.Contains("GeneratedIsaCatalog.PipelineClassMap", facade, StringComparison.Ordinal);
        Assert.DoesNotContain("new HashSet", facade, StringComparison.Ordinal);
        Assert.DoesNotContain("new Dictionary", facade, StringComparison.Ordinal);

        string generator = Read("tools", "HybridCPU.IsaGen", "Program.cs");
        Assert.Contains("ReadStrictManifest(manifestPath)", generator, StringComparison.Ordinal);
        Assert.Contains("VerifyGeneratedCatalogParity(catalog)", generator, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCSharpCatalog", generator, StringComparison.Ordinal);

        string[] handwrittenRowFiles = SourceFiles("HybridCPU_ISE")
            .Where(path => !IsGenerated(path))
            .Where(path => File.ReadAllText(path).Contains("new OpcodeInfo(", StringComparison.Ordinal))
            .Select(Relative)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(handwrittenRowFiles);

        string classifier = Read("HybridCPU_ISE", "NonRTL", "Arch", "InstructionClassifier.cs");
        Assert.Contains("GeneratedIsaCatalog.TryGetDescriptor", classifier, StringComparison.Ordinal);
        Assert.Contains("_ => InstructionClass.ScalarAlu", classifier, StringComparison.Ordinal);
        Assert.Contains("_ => SerializationClass.Free", classifier, StringComparison.Ordinal);

        string support = Read("HybridCPU_ISE", "NonRTL", "Arch", "InstructionSupportStatus.cs");
        Assert.Contains("BuildExplicitStatuses", support, StringComparison.Ordinal);
        Assert.Contains("new InstructionSupportStatus(", support, StringComparison.Ordinal);
    }

    [Fact]
    public void CompatibilityCandidates_HaveClosedWorldRuntimeTestSupportAndReflectionDisposition()
    {
        Assert.Equal(
            new[]
            {
                "HybridCPU_ISE/CloseToHSL/Core/Frontend/Decode/BundleParser/CPU_Core.Decoder.cs",
                "TestAssemblerConsoleApps/MatrixTileSpecSuite.cs",
                "TestAssemblerConsoleApps/StreamVectorSpecSuite.cs",
            },
            ProductionCallers("DecodedBundleTransportProjector."));

        Assert.Equal(
            new[] { "TestAssemblerConsoleApps/SimpleAsmApp.Showcase.cs" },
            ExternalCallers("InstructionIR"));

        Assert.Empty(ProductionCallers("InternalOpBuilder.MapToKind("));
        Assert.Empty(ProductionCallers("new InternalOpBuilder("));
        Assert.NotEmpty(TestCallers("InternalOpBuilder.MapToKind("));
        Assert.Contains(
            "HybridCPU_ISE.Tests/tests/Phase2DecoderDisentanglingTests.cs",
            TestCallers("typeof(InternalOpBuilder)"));

        Assert.Equal(
            new[]
            {
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Fsp/CPU_Core.PipelineExecution.Fsp.cs",
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Rf06ScalarSchedulerRouting.cs",
            },
            ProductionCallers("Rf06ScalarLegacyProjection."));

        Assert.Equal(
            new[] { "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Core/CPU_Core.TestSupport.cs" },
            ProductionCallers("_loopBuffer.TryReplay("));
        Assert.Contains(
            "_loopBuffer.TryGetReplayEntry(",
            Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs"),
            StringComparison.Ordinal);

        string reflectionInventory = Read("HybridCPU_ISE.Tests", "tests", "Phase03PublicFacadeOpcodeSurfaceTests.cs");
        Assert.Contains("typeof(Processor.CPU_Core.InstructionsEnum)", reflectionInventory, StringComparison.Ordinal);
        Assert.Contains("Enum.GetNames", string.Join('\n', SourceFiles("HybridCPU_ISE.Tests").Select(File.ReadAllText)), StringComparison.Ordinal);
    }

    [Fact]
    public void ProtectedFallbacksAndExcludedProofTests_RemainVisibleInsteadOfBeingCalledLegacyFree()
    {
        string scheduler = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Smt", "MicroOpScheduler.SMT.cs");
        Assert.Contains("if (TypedSlotEnabled)", scheduler, StringComparison.Ordinal);
        Assert.Contains("public bool TypedSlotEnabled", scheduler, StringComparison.Ordinal);

        string project = Read("HybridCPU_ISE.Tests", "HybridCPU_ISE.Tests.csproj");
        MatchCollection compileRemovals = Regex.Matches(project, "<Compile Remove=\"([^\"]+)\"");
        string[] excludedFixtures = compileRemovals
            .Select(match => match.Groups[1].Value)
            .Where(path => !path.StartsWith("obj_r6fresh", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(37, excludedFixtures.Length);
        Assert.All(excludedFixtures, path => Assert.True(File.Exists(Path.Combine(Root, "HybridCPU_ISE.Tests", path)), path));

        string quarantineManifest = Read("eng", "test-quarantine.json");
        Assert.Contains("\"schemaVersion\": 2", quarantineManifest, StringComparison.Ordinal);
        Assert.Contains("\"entryStatus\": \"quarantined\"", quarantineManifest, StringComparison.Ordinal);
        Assert.Contains("\"reviewedPhase\": \"RF-13.24\"", quarantineManifest, StringComparison.Ordinal);

        Assert.Empty(SourceFiles("HybridCPU_ISE.Tests")
            .SelectMany(path => File.ReadLines(path))
            .Where(line => Regex.IsMatch(line, @"\[(Fact|Theory)\([^\]]*Skip\s*=")));
    }

    [Fact]
    public void LedgerAndEvidence_NameRollbackAndTheSingleNextSlice()
    {
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "12_RF13", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF13", "rf13.0-cleanup-entry-inventory-freeze.md");

        Assert.Contains("RF-13.0", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-13.1", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly one next task", ledger, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("8bcdb637dcccd73bf3c700f2a7b09c69889aaf4f", evidence, StringComparison.Ordinal);
        Assert.Contains("rollback", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("valid-input parity", evidence, StringComparison.Ordinal);
        Assert.Contains("invalid behavior", evidence, StringComparison.Ordinal);
        Assert.Contains("deletion proof", evidence, StringComparison.Ordinal);
    }

    private static string[] ProductionCallers(string token) => ProductionRoots
        .SelectMany(SourceFiles)
        .Where(path => !IsCandidateDeclaration(path, token))
        .Where(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
        .Select(Relative)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

    private static string[] ExternalCallers(string token) =>
        new[] { "HybridCPU_Compiler", "CpuInterfaceBridge", "TestAssemblerConsoleApps" }
            .SelectMany(SourceFiles)
            .Where(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
            .Select(Relative)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static string[] TestCallers(string token) => SourceFiles("HybridCPU_ISE.Tests")
        .Where(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
        .Select(Relative)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

    private static bool IsCandidateDeclaration(string path, string token) =>
        token == "DecodedBundleTransportProjector." && path.EndsWith("DecodedBundleTransportProjector.cs", StringComparison.OrdinalIgnoreCase) ||
        token.StartsWith("InternalOpBuilder", StringComparison.Ordinal) && path.EndsWith("InternalOpBuilder.cs", StringComparison.OrdinalIgnoreCase) ||
        token == "new InternalOpBuilder(" && path.EndsWith("InternalOpBuilder.cs", StringComparison.OrdinalIgnoreCase) ||
        token == "Rf06ScalarLegacyProjection." && path.EndsWith("Rf06ScalarLegacyProjection.cs", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SourceFiles(string relativeRoot)
    {
        string root = Path.Combine(Root, relativeRoot);
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGenerated(string path) =>
        path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}Generated{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root, .. parts]));

    private static string Relative(string path) =>
        Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string Root { get; } = FindRepositoryRoot();

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
