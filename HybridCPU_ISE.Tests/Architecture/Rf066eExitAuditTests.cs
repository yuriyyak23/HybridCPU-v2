using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf066eExitAuditTests
{
    [Fact]
    public void RemainingProductionTransportProjectorCallers_AreExactlyTheDeclaredCompatibilityLedger()
    {
        string root = CompatFreezeScanner.FindRepoRoot();
        string[] productionRoots =
        {
            Path.Combine(root, "HybridCPU_ISE"),
            Path.Combine(root, "HybridCPU_Compiler"),
            Path.Combine(root, "CpuInterfaceBridge"),
            Path.Combine(root, "TestAssemblerConsoleApps")
        };
        string projectorPath = Path.Combine(
            "HybridCPU_ISE", "Legacy", "CloseToHSL", "Core", "Decoder",
            "DecodedBundleTransportProjector.cs");
        var actual = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string sourceRoot in productionRoots)
        {
            foreach (string path in Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !IsGeneratedOutput(path)))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.Equals(projectorPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int count = CountOccurrences(
                    File.ReadAllText(path),
                    "DecodedBundleTransportProjector.");
                if (count > 0)
                {
                    actual[relative] = count;
                }
            }
        }

        var expected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["HybridCPU_ISE/CloseToHSL/Core/Frontend/Decode/BundleParser/CPU_Core.Decoder.cs"] = 2,
            ["TestAssemblerConsoleApps/StreamVectorSpecSuite.cs"] = 1,
            ["TestAssemblerConsoleApps/MatrixTileSpecSuite.cs"] = 1
        };
        Assert.Equal(expected.OrderBy(pair => pair.Key), actual.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void ReconstructionAuthorities_AreRuntimeOwnedOrDeclaredCompatibilityOnly()
    {
        string root = CompatFreezeScanner.FindRepoRoot();
        string compilerRoot = Path.Combine(root, "HybridCPU_Compiler");
        Assert.DoesNotContain(
            Directory.GetFiles(compilerRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsGeneratedOutput(path)),
            path => File.ReadAllText(path).Contains(
                "InternalOpBuilder.MapToKind(",
                StringComparison.Ordinal));

        string projector = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "Legacy",
            "CloseToHSL",
            "Core",
            "Decoder",
            "DecodedBundleTransportProjector.cs"));
        Assert.Equal(1, CountOccurrences(projector, "new DecoderContext"));
        Assert.Equal(1, CountOccurrences(projector, "InstructionRegistry.CreateMicroOp("));
        Assert.Equal(2, CountOccurrences(projector, "ProjectCanonicalMaterializationInstruction("));

        string matrixHarness = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MatrixTileFullPipelineHarness.cs"));
        Assert.DoesNotContain("DecodedBundleTransportProjector", matrixHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionRegistry.CreateMicroOp(", matrixHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("new DecoderContext", matrixHarness, StringComparison.Ordinal);
    }

    [Fact]
    public void InstructionIrRemovalCondition_IsNotMetAndAdapterRemainsPublic()
    {
        string root = CompatFreezeScanner.FindRepoRoot();
        string[] externalConsumers =
        {
            Path.Combine(root, "HybridCPU_Compiler"),
            Path.Combine(root, "CpuInterfaceBridge"),
            Path.Combine(root, "TestAssemblerConsoleApps")
        };
        int consumerFiles = externalConsumers.Sum(directory =>
            Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Count(path => !IsGeneratedOutput(path) &&
                               File.ReadAllText(path).Contains("InstructionIR", StringComparison.Ordinal)));

        Assert.True(consumerFiles > 0, "InstructionIR still has external compatibility consumers.");
        string adapter = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MicroOps",
            "IR",
            "InstructionIR.cs"));
        Assert.Contains("public sealed", adapter, StringComparison.Ordinal);
    }

    private static bool IsGeneratedOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        for (int offset = 0; (offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0; offset += value.Length)
        {
            count++;
        }

        return count;
    }
}
