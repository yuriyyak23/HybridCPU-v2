using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6u zero-caller valid-input representation contract for the
/// timed-memory owner's physical geometry generation only.
/// </summary>
public sealed class Rf126uMemoryBankGeometryGenerationCoreValidInputContractTests
{
    private const string ContractRelativePath =
        "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/MemoryBankGeometryGeneration.cs";
    private const string GeometryContractRelativePath =
        "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/PhysicalMemoryBankGeometry.cs";
    private const string BindingContractRelativePath =
        "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/PhysicalMemoryBankBinding.cs";

    [Fact]
    public void PublicShapeMatchesUInt64Authority()
    {
        Type type = typeof(MemoryBankGeometryGeneration);
        Assert.Equal("YAKSys_Hybrid_CPU.Memory", type.Namespace);
        Assert.True(type.IsValueType);
        Assert.Equal(1UL, MemoryBankGeometryGeneration.MinValue);
        Assert.Equal(ulong.MaxValue, MemoryBankGeometryGeneration.MaxValue);
        Assert.Equal(typeof(ulong), type
            .GetProperty(nameof(MemoryBankGeometryGeneration.Value))!
            .PropertyType);

        ConstructorInfo constructor = Assert.Single(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(typeof(ulong),
            Assert.Single(constructor.GetParameters()).ParameterType);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(15UL)]
    [InlineData(16UL)]
    [InlineData(4_096UL)]
    [InlineData(ulong.MaxValue - 1UL)]
    [InlineData(ulong.MaxValue)]
    public void RepresentativeIssuedValuesHaveExactParity(ulong raw)
    {
        Assert.True(MemoryBankGeometryGeneration.IsRepresentable(raw));

        MemoryBankGeometryGeneration fromConstructor = new(raw);
        MemoryBankGeometryGeneration fromCreate =
            MemoryBankGeometryGeneration.Create(raw);
        MemoryBankGeometryGeneration fromRaw =
            MemoryBankGeometryGeneration.FromRawValue(raw);

        Assert.True(MemoryBankGeometryGeneration.TryCreate(
            raw, out MemoryBankGeometryGeneration fromTry));
        Assert.Equal(fromConstructor, fromCreate);
        Assert.Equal(fromConstructor, fromRaw);
        Assert.Equal(fromConstructor, fromTry);
        Assert.True(fromRaw.IsIssued);
        Assert.Equal(raw, fromRaw.Value);
        Assert.Equal(raw, fromRaw.ToRawValue());
        Assert.Equal($"memory-bank-geometry-generation{raw}",
            fromRaw.ToString());
        Assert.Equal(fromRaw.GetHashCode(), fromCreate.GetHashCode());
    }

    [Fact]
    public void DeterministicPositiveSamplesRoundTripAcrossUInt64Domain()
    {
        var random = new Random(0x1261);
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        for (int sample = 0; sample < 2_000; sample++)
        {
            random.NextBytes(bytes);
            ulong raw = BitConverter.ToUInt64(bytes);
            if (raw == 0)
            {
                raw = 1;
            }

            MemoryBankGeometryGeneration generation =
                MemoryBankGeometryGeneration.FromRawValue(raw);
            Assert.Equal(raw, generation.ToRawValue());
            Assert.True(MemoryBankGeometryGeneration.TryCreate(
                raw, out MemoryBankGeometryGeneration recreated));
            Assert.Equal(generation, recreated);
        }
    }

    [Fact]
    public void DefaultIsUnissuedAbsentRawZero()
    {
        MemoryBankGeometryGeneration unissued = default;

        Assert.False(unissued.IsIssued);
        Assert.Equal(0UL, unissued.Value);
        Assert.Equal(0UL, unissued.ToRawValue());
        Assert.Equal("unissued", unissued.ToString());
        Assert.False(MemoryBankGeometryGeneration.IsRepresentable(0));
    }

    [Fact]
    public void ZeroIsRejectedWithoutNormalizationOrIssuedAlias()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MemoryBankGeometryGeneration(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MemoryBankGeometryGeneration.Create(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MemoryBankGeometryGeneration.FromRawValue(0));

        Assert.False(MemoryBankGeometryGeneration.TryCreate(
            0, out MemoryBankGeometryGeneration failed));
        Assert.Equal(default, failed);
        Assert.False(failed.IsIssued);
        Assert.NotEqual(MemoryBankGeometryGeneration.Create(1), failed);
    }

    [Fact]
    public void ContractHasNoNumericOperatorsArithmeticOrAllocator()
    {
        Type type = typeof(MemoryBankGeometryGeneration);
        Assert.DoesNotContain(type.GetMethods(
                BindingFlags.Public | BindingFlags.Static),
            method => method.IsSpecialName &&
                      method.Name.StartsWith("op_", StringComparison.Ordinal) &&
                      method.Name is not "op_Equality" and not "op_Inequality");

        string contract = ContractSource();
        Assert.DoesNotMatch(
            @"\b(?:Next|Advance|Increment|Decrement|Allocate|Issue|Successor)\s*\(",
            contract);
        Assert.DoesNotMatch(
            @"(?:\+\+|--|%\s*|Math\.(?:Clamp|Max|Min)\s*\()",
            contract);
        Assert.DoesNotContain("JsonConstructor", contract,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeclarationIsUniqueWithOneOwnerAndZeroExternalCallers()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(
            root, ContractRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string geometryContractPath = Path.GetFullPath(Path.Combine(
            root,
            GeometryContractRelativePath.Replace(
                '/', Path.DirectorySeparatorChar)));
        string bindingContractPath = Path.GetFullPath(Path.Combine(
            root,
            BindingContractRelativePath.Replace(
                '/', Path.DirectorySeparatorChar)));

        string production = JoinSources(Path.Combine(root, "HybridCPU_ISE"),
            contractPath, geometryContractPath, bindingContractPath);
        Assert.Contains(
            "private ulong _lastIssuedPhysicalBankGeometryGeneration;",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankGeometryGeneration.Create(nextGenerationRaw)",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "public MemoryBankGeometryGeneration Generation { get; }",
            File.ReadAllText(geometryContractPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "public MemoryBankGeometryGeneration Generation { get; }",
            File.ReadAllText(bindingContractPath),
            StringComparison.Ordinal);
        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler", "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps"
                 })
        {
            Assert.DoesNotContain("MemoryBankGeometryGeneration",
                JoinSources(Path.Combine(root, externalRoot)),
                StringComparison.Ordinal);
        }

        Assert.Equal(1, Regex.Matches(ContractSource(),
            @"public\s+readonly\s+record\s+struct\s+MemoryBankGeometryGeneration\b")
            .Count);
    }

    [Fact]
    public void ContractDoesNotConflateOtherIdentifierFamilies()
    {
        string contract = ContractSource();
        foreach (string forbidden in new[]
                 {
                     "PhysicalMemoryBankIndex", "MemoryBankId", "VtId",
                     "SlotId", "LaneId", "ReplayToken", "EpochId",
                     "TokenId", "DomainId", "Certificate", "MemoryRequestToken",
                     "BankCount", "BankWidthBytes", "Queue<", "Dictionary<"
                 })
        {
            Assert.DoesNotContain(forbidden, contract,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RuntimeGeometryRequestAndWireSurfacesRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string memorySources = JoinSources(Path.Combine(root, "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem"),
            Path.Combine(root,
                ContractRelativePath.Replace('/', Path.DirectorySeparatorChar)),
            Path.Combine(root,
                GeometryContractRelativePath.Replace(
                    '/', Path.DirectorySeparatorChar)),
            Path.Combine(root,
                BindingContractRelativePath.Replace(
                    '/', Path.DirectorySeparatorChar)));
        string external = string.Join("\n", new[]
        {
            JoinSources(Path.Combine(root, "HybridCPU_ISE", "Machine")),
            JoinSources(Path.Combine(root, "TestAssemblerConsoleApps"))
        });

        Assert.Contains(
            "private ulong _lastIssuedPhysicalBankGeometryGeneration;",
            memorySources, StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankGeometryGeneration.Create(nextGenerationRaw)",
            memorySources, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", external,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GeometryGeneration", external,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperAuthorityAndEvidenceMatchDormantContract()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");
        string evidence = Evidence(root);

        Assert.Contains(
            "retained raw form of `MemoryBankGeometryGeneration` is `UInt64`",
            paper, StringComparison.Ordinal);
        Assert.Contains(
            "Issued values are exactly `1..UInt64.MaxValue`",
            evidence, StringComparison.Ordinal);
        Assert.Contains("Production caller migration: none", evidence,
            StringComparison.Ordinal);
        Assert.Contains("Existing invalid-input behavior change: none",
            evidence, StringComparison.Ordinal);
    }


    private static string ContractSource() => Read(
        FindRepositoryRoot(), ContractRelativePath.Split('/'));

    private static string Evidence(string root) => Read(root,
        "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6u-memory-bank-geometry-generation-core-valid-input-contract.md");

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
