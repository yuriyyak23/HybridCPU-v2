using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6o zero-caller valid-input representation contract for physical
/// memory-bank queue/array positions only.
/// </summary>
public sealed class Rf126oPhysicalMemoryBankIndexCoreValidInputContractTests
{
    private const string ContractRelativePath =
        "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/PhysicalMemoryBankIndex.cs";

    [Fact]
    public void PublicShapeIsOwnerLocalRepresentationOnly()
    {
        Assert.Equal("YAKSys_Hybrid_CPU.Memory",
            typeof(PhysicalMemoryBankIndex).Namespace);
        Assert.True(typeof(PhysicalMemoryBankIndex).IsValueType);
        Assert.Equal(0, PhysicalMemoryBankIndex.MinValue);
        Assert.Equal(int.MaxValue, PhysicalMemoryBankIndex.MaxValue);
        Assert.Equal(typeof(int), typeof(PhysicalMemoryBankIndex)
            .GetProperty(nameof(PhysicalMemoryBankIndex.Value))!.PropertyType);

        ConstructorInfo constructor = Assert.Single(
            typeof(PhysicalMemoryBankIndex).GetConstructors(
                BindingFlags.Public | BindingFlags.Instance));
        ParameterInfo parameter = Assert.Single(constructor.GetParameters());
        Assert.Equal(typeof(int), parameter.ParameterType);

        string contract = ContractSource();
        Assert.Contains("This is not a geometry-membership or queue-legality",
            contract, StringComparison.Ordinal);
        foreach (string forbidden in new[]
                 {
                     "MemoryBankGeometryGeneration", "MemoryBankId",
                     "MemoryRequestToken", "BankCount", "BankWidthBytes",
                     "Address", "Queue<", "Dictionary<", "ResourceMask",
                     "ReplayToken", "Certificate"
                 })
        {
            Assert.DoesNotContain(forbidden, contract,
                StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(4096)]
    [InlineData(1_000_000)]
    [InlineData(int.MaxValue)]
    public void RepresentativeValidOrdinalsHaveExactParity(int raw)
    {
        Assert.True(PhysicalMemoryBankIndex.IsRepresentable(raw));

        PhysicalMemoryBankIndex fromConstructor = new(raw);
        PhysicalMemoryBankIndex fromCreate =
            PhysicalMemoryBankIndex.Create(raw);
        PhysicalMemoryBankIndex fromRaw =
            PhysicalMemoryBankIndex.FromRawValue(raw);
        PhysicalMemoryBankIndex fromCast = (PhysicalMemoryBankIndex)raw;

        Assert.True(PhysicalMemoryBankIndex.TryCreate(
            raw, out PhysicalMemoryBankIndex fromTry));
        Assert.Equal(fromConstructor, fromCreate);
        Assert.Equal(fromConstructor, fromRaw);
        Assert.Equal(fromConstructor, fromCast);
        Assert.Equal(fromConstructor, fromTry);
        Assert.Equal(raw, fromRaw.Value);
        Assert.Equal(raw, fromRaw.ToRawValue());
        Assert.Equal(raw, (int)fromRaw);
        Assert.Equal($"physical-bank{raw}", fromRaw.ToString());
        Assert.Equal(fromRaw.GetHashCode(), fromCreate.GetHashCode());

        string json = JsonSerializer.Serialize(fromRaw);
        Assert.Equal(fromRaw,
            JsonSerializer.Deserialize<PhysicalMemoryBankIndex>(json));
    }

    [Fact]
    public void DeterministicSamplesRoundTripAcrossWholeNonNegativeDomain()
    {
        var random = new Random(0x1260);
        for (int sample = 0; sample < 2_000; sample++)
        {
            int raw = random.Next(0, int.MaxValue);
            PhysicalMemoryBankIndex index =
                PhysicalMemoryBankIndex.FromRawValue(raw);
            Assert.Equal(raw, index.ToRawValue());
            Assert.True(PhysicalMemoryBankIndex.TryCreate(
                raw, out PhysicalMemoryBankIndex recreated));
            Assert.Equal(index, recreated);
        }
    }

    [Fact]
    public void ZeroAndDefaultArePresentPhysicalBankZero()
    {
        PhysicalMemoryBankIndex zero = new(0);
        Assert.Equal(zero, default);
        Assert.Equal(zero, PhysicalMemoryBankIndex.Zero);
        Assert.Equal(zero, PhysicalMemoryBankIndex.Create(0));
        Assert.Equal(zero, PhysicalMemoryBankIndex.FromRawValue(0));
        Assert.True(PhysicalMemoryBankIndex.TryCreate(
            0, out PhysicalMemoryBankIndex fromTry));
        Assert.Equal(zero, fromTry);
        Assert.Equal(0, zero.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-16)]
    [InlineData(-4096)]
    [InlineData(int.MinValue)]
    public void NegativeOrdinalsAreRejectedWithoutZeroAlias(int raw)
    {
        Assert.False(PhysicalMemoryBankIndex.IsRepresentable(raw));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PhysicalMemoryBankIndex(raw));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PhysicalMemoryBankIndex.Create(raw));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PhysicalMemoryBankIndex.FromRawValue(raw));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => (PhysicalMemoryBankIndex)raw);

        Assert.False(PhysicalMemoryBankIndex.TryCreate(
            raw, out PhysicalMemoryBankIndex failed));
        Assert.Equal(default, failed);
        Assert.Equal(PhysicalMemoryBankIndex.Zero, failed);
    }

    [Fact]
    public void DeclarationIsUniqueAndAllRuntimeExternalCallersAreZero()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(
            root, ContractRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string bindingContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankBinding.cs"));
        string envelopeContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "CanonicalVectorPhysicalBankEnvelope.cs"));
        string production = JoinSources(
            Path.Combine(root, "HybridCPU_ISE"),
            contractPath, bindingContractPath, envelopeContractPath);

        Assert.Equal(12, Regex.Matches(production,
            @"\bPhysicalMemoryBankIndex\b").Count);
        Assert.Contains(
            "public PhysicalMemoryBankIndex BankIndex { get; }",
            File.ReadAllText(bindingContractPath),
            StringComparison.Ordinal);
        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler", "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps"
                 })
        {
            Assert.DoesNotContain("PhysicalMemoryBankIndex",
                JoinSources(Path.Combine(root, externalRoot)),
                StringComparison.Ordinal);
        }

        string contract = File.ReadAllText(contractPath);
        Assert.Equal(1, Regex.Matches(contract,
            @"public\s+readonly\s+record\s+struct\s+PhysicalMemoryBankIndex\b").Count);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", contract,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            @"\b(?:MemoryBankId|SlotId|LaneId|VtId|TokenId|DomainId)\b",
            contract);
    }

    [Fact]
    public void RawResolverTokenAndDiagnosticSurfacesRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Helpers.cs");
        string operations = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "MemorySubsystem.Operations.cs");
        string subsystem = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Subsystem", "MemorySubsystem.cs");

        Assert.Equal(1, Regex.Matches(helpers,
            @"private\s+PhysicalMemoryBankIndex\s+ComputeBankId\(ulong\s+address\)").Count);
        Assert.Equal(5, Regex.Matches(
            helpers + "\n" + operations + "\n" + subsystem,
            @"\bComputeBankId\s*\(").Count - 1);

        Type token = typeof(MemorySubsystem.MemoryRequestToken);
        Assert.Null(token.GetProperty("PhysicalBankIndex",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(token.GetProperty("GeometryGeneration",
            BindingFlags.Public | BindingFlags.Instance));

        PropertyInfo burstBank = typeof(MemorySubsystem.BurstEventArgs)
            .GetProperty(nameof(MemorySubsystem.BurstEventArgs.BankId))!;
        Assert.Equal(typeof(int), burstBank.PropertyType);
        Assert.True(burstBank.CanWrite);
    }


    private static string ContractSource() => Read(
        FindRepositoryRoot(),
        ContractRelativePath.Split('/'));

    private static string JoinSources(
        string sourceRoot,
        params string[] excludedPaths)
    {
        var normalizedExcluded = excludedPaths
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return string.Join("\n",
            Directory.EnumerateFiles(sourceRoot, "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Where(path =>
                    !normalizedExcluded.Contains(Path.GetFullPath(path)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static bool IsBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/",
                   StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName,
                    "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName,
                    "Documentation")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
