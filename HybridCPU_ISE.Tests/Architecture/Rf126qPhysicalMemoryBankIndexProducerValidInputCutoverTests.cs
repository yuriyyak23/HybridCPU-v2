using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6q valid-input signature-parity cutover of the private physical
/// memory-bank producer and its six direct consumers.
/// </summary>
public sealed class Rf126qPhysicalMemoryBankIndexProducerValidInputCutoverTests
{
    [Fact]
    public void PrivateProducerReturnsCheckedPhysicalIndex()
    {
        MethodInfo method = typeof(MemorySubsystem).GetMethod(
            "ComputeBankId",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        Assert.NotNull(method);
        Assert.Equal(typeof(PhysicalMemoryBankIndex), method.ReturnType);
        ParameterInfo parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(ulong), parameter.ParameterType);

        string helpers = Helpers(FindRepositoryRoot());
        Assert.Equal(1, Regex.Matches(helpers,
            @"private\s+PhysicalMemoryBankIndex\s+ComputeBankId\(ulong\s+address\)").Count);
        Assert.Contains("PhysicalMemoryBankIndex.FromRawValue(",
            helpers, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            @"private\s+int\s+ComputeBankId\(ulong\s+address\)",
            helpers);
    }

    [Theory]
    [InlineData(0UL, 64, 16, 0)]
    [InlineData(64UL, 64, 16, 1)]
    [InlineData(1088UL, 64, 32, 17)]
    [InlineData(4096UL, 0, 16, 1)]
    [InlineData(4096UL, -7, 16, 1)]
    [InlineData(ulong.MaxValue, 1, 32, 31)]
    public void PrivateProducerPreservesResolverValuesExactly(
        ulong address,
        int bankWidthBytes,
        int numBanks,
        int expected)
    {
        Processor processor = default;
        var memory = new MemorySubsystem(ref processor)
        {
            BankWidthBytes = bankWidthBytes,
            NumBanks = numBanks
        };
        MethodInfo method = typeof(MemorySubsystem).GetMethod(
            "ComputeBankId",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        var actual = Assert.IsType<PhysicalMemoryBankIndex>(
            method.Invoke(memory, new object[] { address }));

        Assert.Equal(expected, actual.Value);
    }

    [Fact]
    public void FiveRawResolverConsumersHoldCheckedLocalsAndCancellationUsesBinding()
    {
        string root = FindRepositoryRoot();
        string contour = Helpers(root) + "\n" + Operations(root) + "\n" +
                         Subsystem(root);

        Assert.Equal(6, Regex.Matches(contour, @"\bComputeBankId\s*\(").Count);
        Assert.Equal(5, Regex.Matches(contour,
            @"PhysicalMemoryBankIndex\s+bankIndex\s*=\s*ComputeBankId\(").Count);
        Assert.DoesNotMatch(
            @"int\s+bankId\s*=\s*ComputeBankId\(",
            contour);
    }

    [Fact]
    public void ProjectionsOccurOnlyAtRetainedRawDownstreamSeams()
    {
        string root = FindRepositoryRoot();
        string helpers = Helpers(root);
        string operations = Operations(root);
        string subsystem = Subsystem(root);
        string contour = helpers + "\n" + operations + "\n" + subsystem;

        Assert.Contains("bankQueues[physicalBankBinding.BankIndex.Value]",
            operations, StringComparison.Ordinal);
        Assert.Contains("RemoveQueuedBankRequest(",
            operations, StringComparison.Ordinal);
        Assert.Contains("bankOccupied[bankIndex.Value]", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("bankQueues[bankIndex.Value].Count", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("OnBurstStarted(address, length, true, bankIndex.Value)",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("bankLastAccessCycle[bankIndex.Value]", helpers,
            StringComparison.Ordinal);
        Assert.Equal(16, Regex.Matches(contour, @"\bbankIndex\.Value\b").Count);
    }

    [Fact]
    public void RawStorageWireAndOwnerSignaturesRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string helpers = Helpers(root);
        string subsystem = Subsystem(root);

        Assert.Contains("private bool RemoveQueuedBankRequest(ulong requestID, int bankId)",
            helpers, StringComparison.Ordinal);
        Assert.Contains("protected virtual void OnBurstStarted(ulong address, int length, bool isRead, int bankId)",
            helpers, StringComparison.Ordinal);
        Assert.Contains("public int BankId { get; set; }", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("Queue<BankRequest>[] bankQueues", subsystem,
            StringComparison.Ordinal);
        Assert.Null(typeof(MemorySubsystem.MemoryRequestToken).GetProperty(
            "PhysicalBankIndex",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(MemorySubsystem.MemoryRequestToken).GetProperty(
            "GeometryGeneration",
            BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void ResolverFallbackAndInvalidBehaviorRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "LoadStore", "MemoryBankRouting.cs");
        string subsystem = Subsystem(root);
        string operations = Operations(root);

        Assert.Contains(": DefaultBankWidthBytes;", routing,
            StringComparison.Ordinal);
        Assert.Contains(": DefaultNumBanks;", routing,
            StringComparison.Ordinal);
        Assert.Contains("int sanitized = Math.Max(1, value);", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("if (requestID == 0)", operations,
            StringComparison.Ordinal);
        Assert.Contains("pendingRequests.Remove(requestID);", operations,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalWireAndTestSupportCallersRemainZero()
    {
        string root = FindRepositoryRoot();
        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler", "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps"
                 })
        {
            Assert.DoesNotContain("PhysicalMemoryBankIndex",
                ReadTree(Path.Combine(root, externalRoot)),
                StringComparison.Ordinal);
        }

        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        Assert.DoesNotContain("PhysicalMemoryBankIndex", testSupport,
            StringComparison.Ordinal);
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
