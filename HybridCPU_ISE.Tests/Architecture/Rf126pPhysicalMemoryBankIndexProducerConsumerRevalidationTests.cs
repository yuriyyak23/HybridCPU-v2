using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6p decision-only closed-world revalidation of the private physical
/// memory-bank index producer and its six direct consumers.
/// </summary>
public sealed class Rf126pPhysicalMemoryBankIndexProducerConsumerRevalidationTests
{
    [Fact]
    public void PaperAuthorityAndCoreContractAuthorizeOnlyTheSelectedContour()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");
        string contract = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "PhysicalMemoryBankIndex.cs");

        Assert.Contains("#### 3.7.24 Physical memory-bank position and geometry lifetime",
            paper, StringComparison.Ordinal);
        Assert.Contains("topology-local queue/array position", paper,
            StringComparison.Ordinal);
        Assert.Contains("distinct from scheduler-visible `MemoryBankId`", paper,
            StringComparison.Ordinal);
        Assert.Contains("Physical position zero is valid bank zero", paper,
            StringComparison.Ordinal);
        Assert.Contains("public readonly record struct PhysicalMemoryBankIndex",
            contract, StringComparison.Ordinal);
        Assert.Contains("value >= MinValue", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentProducerHasExactlySixDirectConsumers()
    {
        string root = FindRepositoryRoot();
        string helpers = Helpers(root);
        string operations = Operations(root);
        string subsystem = Subsystem(root);
        string contour = helpers + "\n" + operations + "\n" + subsystem;

        Assert.Equal(1, Regex.Matches(helpers,
            @"private\s+PhysicalMemoryBankIndex\s+ComputeBankId\(ulong\s+address\)").Count);
        Assert.Equal(6, Regex.Matches(contour, @"\bComputeBankId\s*\(").Count);
        Assert.Equal(2, Regex.Matches(subsystem,
            @"PhysicalMemoryBankIndex\s+bankIndex\s*=\s*ComputeBankId\(address\);").Count);
        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalMemoryBankIndex\s+bankIndex\s*=\s*ComputeBankId\(").Count);
        Assert.Equal(1, Regex.Matches(helpers,
            @"PhysicalMemoryBankIndex\s+bankIndex\s*=\s*ComputeBankId\(address\);").Count);
    }

    [Theory]
    [InlineData(0UL, 64, 16, 0)]
    [InlineData(64UL, 64, 16, 1)]
    [InlineData(1088UL, 64, 32, 17)]
    [InlineData(4096UL, 0, 16, 1)]
    [InlineData(4096UL, -7, 16, 1)]
    [InlineData(4096UL, 4096, 0, 1)]
    [InlineData(61440UL, 0, 0, 15)]
    [InlineData(ulong.MaxValue, 1, int.MaxValue, 3)]
    public void ExistingResolverOutputsAreTotalForCheckedRepresentation(
        ulong address,
        int bankWidthBytes,
        int numBanks,
        int expected)
    {
        int raw = MemoryBankRouting.ResolveBankId(
            address, bankWidthBytes, numBanks);
        Assert.Equal(expected, raw);
        Assert.True(PhysicalMemoryBankIndex.IsRepresentable(raw));
        Assert.Equal(raw,
            PhysicalMemoryBankIndex.FromRawValue(raw).Value);
    }

    [Fact]
    public void GeometryNormalizationAndResolverFallbackRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string subsystem = Subsystem(root);
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");

        Assert.Contains("int sanitized = Math.Max(1, value);", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("bankWidthBytes > 0", routing, StringComparison.Ordinal);
        Assert.Contains(": DefaultBankWidthBytes;", routing, StringComparison.Ordinal);
        Assert.Contains("numBanks > 0", routing, StringComparison.Ordinal);
        Assert.Contains(": DefaultNumBanks;", routing, StringComparison.Ordinal);
        Assert.Contains(
            "return (int)((address / (ulong)resolvedBankWidthBytes) % (ulong)resolvedNumBanks);",
            routing, StringComparison.Ordinal);
    }

    [Fact]
    public void SixConsumersProjectOnlyIntoExistingRawOwnerSeams()
    {
        string root = FindRepositoryRoot();
        string helpers = Helpers(root);
        string operations = Operations(root);
        string subsystem = Subsystem(root);

        Assert.Contains("bankQueues[physicalBankBinding.BankIndex.Value]", operations,
            StringComparison.Ordinal);
        Assert.Contains("RemoveQueuedBankRequest(",
            operations, StringComparison.Ordinal);
        Assert.Contains("bankOccupied[bankIndex.Value]", subsystem, StringComparison.Ordinal);
        Assert.Contains("bankQueues[bankIndex.Value].Count", subsystem, StringComparison.Ordinal);
        Assert.Contains("OnBurstStarted(address, length, true, bankIndex.Value)", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("bankLastAccessCycle[bankIndex.Value]", helpers, StringComparison.Ordinal);
        Assert.Contains("private bool RemoveQueuedBankRequest(ulong requestID, int bankId)",
            helpers, StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationTopologyAndInvalidBehaviorAreOutsideCutover()
    {
        string root = FindRepositoryRoot();
        string helpers = Helpers(root);
        string operations = Operations(root);
        string subsystem = Subsystem(root);

        Assert.Contains("if (requestID == 0)", operations, StringComparison.Ordinal);
        Assert.Contains("return false;", operations, StringComparison.Ordinal);
        Assert.Contains("if ((uint)bankId >= (uint)NumBanks", helpers,
            StringComparison.Ordinal);
        Assert.Contains("ReconfigureBankTopology();", subsystem,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GeometryGeneration", operations,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalBankIndex", operations,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublicWireReflectionAndExternalCallersRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        Type token = typeof(MemorySubsystem.MemoryRequestToken);
        Assert.Null(token.GetProperty("PhysicalBankIndex",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(token.GetProperty("GeometryGeneration",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(typeof(int), typeof(MemorySubsystem.BurstEventArgs)
            .GetProperty(nameof(MemorySubsystem.BurstEventArgs.BankId))!.PropertyType);

        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler", "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps"
                 })
        {
            Assert.DoesNotContain("PhysicalMemoryBankIndex",
                ReadTree(Path.Combine(root, externalRoot)), StringComparison.Ordinal);
        }
    }


    private static string Helpers(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.Helpers.cs");

    private static string Operations(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.Operations.cs");

    private static string Subsystem(string root) => Read(root, "HybridCPU_ISE",
        "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.cs");

    private static string ReadTree(string path) =>
        string.Join("\n", Directory.EnumerateFiles(path, "*.cs",
                SearchOption.AllDirectories)
            .Where(file => !file.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "HybridCPU repository root was not found.");
    }
}
