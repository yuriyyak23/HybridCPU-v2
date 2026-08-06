using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6w zero-caller valid-input contract for one immutable physical
/// memory-bank geometry tuple.
/// </summary>
public sealed class Rf126wPhysicalMemoryBankGeometryCoreValidInputContractTests
{
    private const string ContractRelativePath =
        "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/PhysicalMemoryBankGeometry.cs";

    [Fact]
    public void PublicShapeMatchesExactImmutableTupleAuthority()
    {
        Type type = typeof(PhysicalMemoryBankGeometry);
        Assert.Equal("YAKSys_Hybrid_CPU.Memory", type.Namespace);
        Assert.True(type.IsValueType);
        Assert.True(type.IsSealed);
        Assert.Equal(1, PhysicalMemoryBankGeometry.MinBankCount);
        Assert.Equal(int.MaxValue, PhysicalMemoryBankGeometry.MaxBankCount);
        Assert.Equal(1, PhysicalMemoryBankGeometry.MinBankWidthBytes);
        Assert.Equal(int.MaxValue,
            PhysicalMemoryBankGeometry.MaxBankWidthBytes);

        Assert.Equal(typeof(int), type
            .GetProperty(nameof(PhysicalMemoryBankGeometry.BankCount))!
            .PropertyType);
        Assert.Equal(typeof(int), type
            .GetProperty(nameof(PhysicalMemoryBankGeometry.BankWidthBytes))!
            .PropertyType);
        Assert.Equal(typeof(MemoryBankGeometryGeneration), type
            .GetProperty(nameof(PhysicalMemoryBankGeometry.Generation))!
            .PropertyType);

        Assert.All(type.GetProperties(BindingFlags.Public |
                                      BindingFlags.Instance),
            property => Assert.Null(property.SetMethod));

        ConstructorInfo constructor = Assert.Single(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            new[]
            {
                typeof(int),
                typeof(int),
                typeof(MemoryBankGeometryGeneration)
            },
            constructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
    }

    [Theory]
    [InlineData(1, 1, 1UL)]
    [InlineData(8, 64, 2UL)]
    [InlineData(16, 4_096, 16UL)]
    [InlineData(17, 65, 17UL)]
    [InlineData(int.MaxValue, int.MaxValue, ulong.MaxValue)]
    public void RepresentativeValidTuplesHaveExactValueParity(
        int bankCount,
        int bankWidthBytes,
        ulong rawGeneration)
    {
        MemoryBankGeometryGeneration generation =
            MemoryBankGeometryGeneration.Create(rawGeneration);

        Assert.True(PhysicalMemoryBankGeometry.IsBankCountRepresentable(
            bankCount));
        Assert.True(PhysicalMemoryBankGeometry.IsBankWidthRepresentable(
            bankWidthBytes));
        Assert.True(PhysicalMemoryBankGeometry.AreComponentsRepresentable(
            bankCount, bankWidthBytes, generation));

        PhysicalMemoryBankGeometry fromConstructor =
            new(bankCount, bankWidthBytes, generation);
        PhysicalMemoryBankGeometry fromCreate =
            PhysicalMemoryBankGeometry.Create(
                bankCount, bankWidthBytes, generation);
        Assert.True(PhysicalMemoryBankGeometry.TryCreate(
            bankCount,
            bankWidthBytes,
            generation,
            out PhysicalMemoryBankGeometry fromTry));

        Assert.Equal(fromConstructor, fromCreate);
        Assert.Equal(fromConstructor, fromTry);
        Assert.True(fromConstructor.IsWellFormed);
        Assert.Equal(bankCount, fromConstructor.BankCount);
        Assert.Equal(bankWidthBytes, fromConstructor.BankWidthBytes);
        Assert.Equal(generation, fromConstructor.Generation);
        Assert.Equal(
            $"physical-memory-bank-geometry(count={bankCount}, " +
            $"width-bytes={bankWidthBytes}, generation={rawGeneration})",
            fromConstructor.ToString());
    }

    [Fact]
    public void DeterministicPositiveSamplesPreserveAllComponents()
    {
        var random = new Random(0x1262);
        for (int sample = 0; sample < 2_000; sample++)
        {
            int count = random.Next(1, int.MaxValue);
            int width = random.Next(1, int.MaxValue);
            ulong rawGeneration =
                ((ulong)(uint)random.Next() << 32) |
                (uint)random.Next();
            if (rawGeneration == 0)
            {
                rawGeneration = 1;
            }

            MemoryBankGeometryGeneration generation =
                MemoryBankGeometryGeneration.Create(rawGeneration);
            PhysicalMemoryBankGeometry geometry =
                PhysicalMemoryBankGeometry.Create(count, width, generation);

            Assert.Equal(count, geometry.BankCount);
            Assert.Equal(width, geometry.BankWidthBytes);
            Assert.Equal(rawGeneration, geometry.Generation.Value);
            Assert.True(geometry.IsWellFormed);
        }
    }

    [Fact]
    public void DefaultIsOuterAbsenceAndNotAWellFormedTuple()
    {
        PhysicalMemoryBankGeometry absent = default;

        Assert.Equal(0, absent.BankCount);
        Assert.Equal(0, absent.BankWidthBytes);
        Assert.Equal(default, absent.Generation);
        Assert.False(absent.IsWellFormed);
        Assert.Equal("no-physical-memory-bank-geometry", absent.ToString());
        Assert.False(PhysicalMemoryBankGeometry.AreComponentsRepresentable(
            absent.BankCount, absent.BankWidthBytes, absent.Generation));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonPositiveBankCountRejectsWithoutAlias(int bankCount)
    {
        MemoryBankGeometryGeneration generation =
            MemoryBankGeometryGeneration.Create(1);

        ArgumentOutOfRangeException exception = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new PhysicalMemoryBankGeometry(bankCount, 64, generation));
        Assert.Equal("bankCount", exception.ParamName);
        Assert.False(PhysicalMemoryBankGeometry.TryCreate(
            bankCount, 64, generation, out PhysicalMemoryBankGeometry failed));
        Assert.Equal(default, failed);
        Assert.NotEqual(PhysicalMemoryBankGeometry.Create(1, 64, generation),
            failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonPositiveBankWidthRejectsWithoutAlias(int bankWidthBytes)
    {
        MemoryBankGeometryGeneration generation =
            MemoryBankGeometryGeneration.Create(1);

        ArgumentOutOfRangeException exception = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new PhysicalMemoryBankGeometry(8, bankWidthBytes, generation));
        Assert.Equal("bankWidthBytes", exception.ParamName);
        Assert.False(PhysicalMemoryBankGeometry.TryCreate(
            8,
            bankWidthBytes,
            generation,
            out PhysicalMemoryBankGeometry failed));
        Assert.Equal(default, failed);
    }

    [Fact]
    public void UnissuedGenerationRejectsWithoutZeroToOneNormalization()
    {
        MemoryBankGeometryGeneration unissued = default;

        ArgumentOutOfRangeException exception = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new PhysicalMemoryBankGeometry(8, 64, unissued));
        Assert.Equal("generation", exception.ParamName);
        Assert.False(PhysicalMemoryBankGeometry.TryCreate(
            8, 64, unissued, out PhysicalMemoryBankGeometry failed));
        Assert.Equal(default, failed);
        Assert.NotEqual(PhysicalMemoryBankGeometry.Create(
            8, 64, MemoryBankGeometryGeneration.Create(1)), failed);
    }

    [Fact]
    public void InvalidComponentPrecedenceIsCountThenWidthThenGeneration()
    {
        ArgumentOutOfRangeException count = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new PhysicalMemoryBankGeometry(0, 0, default));
        Assert.Equal("bankCount", count.ParamName);

        ArgumentOutOfRangeException width = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new PhysicalMemoryBankGeometry(1, 0, default));
        Assert.Equal("bankWidthBytes", width.ParamName);

        ArgumentOutOfRangeException generation = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new PhysicalMemoryBankGeometry(1, 1, default));
        Assert.Equal("generation", generation.ParamName);
    }

    [Fact]
    public void EqualityIncludesCountWidthAndGeneration()
    {
        MemoryBankGeometryGeneration generation1 =
            MemoryBankGeometryGeneration.Create(1);
        MemoryBankGeometryGeneration generation2 =
            MemoryBankGeometryGeneration.Create(2);
        PhysicalMemoryBankGeometry baseline =
            new(8, 64, generation1);

        Assert.NotEqual(baseline,
            new PhysicalMemoryBankGeometry(9, 64, generation1));
        Assert.NotEqual(baseline,
            new PhysicalMemoryBankGeometry(8, 65, generation1));
        Assert.NotEqual(baseline,
            new PhysicalMemoryBankGeometry(8, 64, generation2));
        Assert.Equal(baseline.GetHashCode(),
            PhysicalMemoryBankGeometry.Create(8, 64, generation1)
                .GetHashCode());
    }

    [Fact]
    public void ContractHasNoAllocatorResolverArithmeticOrWireAuthority()
    {
        Type type = typeof(PhysicalMemoryBankGeometry);
        Assert.DoesNotContain(type.GetMethods(
                BindingFlags.Public | BindingFlags.Static),
            method => method.IsSpecialName &&
                      method.Name.StartsWith("op_", StringComparison.Ordinal) &&
                      method.Name is not "op_Equality" and not "op_Inequality");

        string contract = ContractSource();
        Assert.DoesNotMatch(
            @"\b(?:Publish|Install|Replace|Update|Resolve|Bind|Accept|Queue|" +
            @"Allocate|Issue|Next|Advance|Increment|Decrement)\s*\(",
            contract);
        Assert.DoesNotMatch(
            @"(?:\+\+|--|%\s*|/\s*BankWidthBytes|" +
            @"Math\.(?:Clamp|Max|Min)\s*\()",
            contract);
        Assert.DoesNotContain("JsonConstructor", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("implicit operator", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("explicit operator", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankIndex", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", contract,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeclarationIsUniqueWithOneOwnerAndZeroExternalCallers()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(
            root, ContractRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        string production = JoinSources(
            Path.Combine(root, "HybridCPU_ISE"), contractPath);
        Assert.Contains(
            "private PhysicalMemoryBankGeometry _publishedPhysicalBankGeometry;",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "public PhysicalMemoryBankGeometry PublishedPhysicalBankGeometry",
            production, StringComparison.Ordinal);
        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler", "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps"
                 })
        {
            Assert.DoesNotContain("PhysicalMemoryBankGeometry",
                JoinSources(Path.Combine(root, externalRoot)),
                StringComparison.Ordinal);
        }

        Assert.Equal(1, Regex.Matches(ContractSource(),
            @"public\s+readonly\s+record\s+struct\s+" +
            @"PhysicalMemoryBankGeometry\b").Count);
    }

    [Fact]
    public void RuntimeStorageResolverRequestsSettersArraysAndWiresRemainRaw()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(
            root, ContractRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string production = JoinSources(
            Path.Combine(root, "HybridCPU_ISE"), contractPath);

        Assert.Contains(
            "private PhysicalMemoryBankGeometry _publishedPhysicalBankGeometry;",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "public PhysicalMemoryBankGeometry PublishedPhysicalBankGeometry",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "TryReplacePhysicalMemoryBankGeometry(",
            production, StringComparison.Ordinal);
        Assert.Contains("public int NumBanks", production,
            StringComparison.Ordinal);
        Assert.Contains("public int BankWidthBytes", production,
            StringComparison.Ordinal);
        Assert.Contains("PhysicalMemoryBankIndex.FromRawValue(",
            production, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "MemoryBankGeometryGeneration Generation { get; set;",
            production, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractDoesNotConflateSchedulerOrOtherIdentifierFamilies()
    {
        string contract = ContractSource();
        foreach (string forbidden in new[]
                 {
                     "MemoryBankId", "PhysicalMemoryBankIndex", "VtId",
                     "SlotId", "LaneId", "ReplayToken", "TokenId",
                     "DomainId", "Certificate", "MemoryRequestId",
                     "Queue<", "Dictionary<", "BitArray"
                 })
        {
            Assert.DoesNotContain(forbidden, contract,
                StringComparison.Ordinal);
        }
    }


    private static string ContractSource() => Read(
        FindRepositoryRoot(), ContractRelativePath.Split('/'));

    private static string Evidence(string root) => Read(root,
        "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6w-physical-memory-bank-geometry-core-valid-input-contract.md");

    private static string JoinSources(
        string sourceRoot,
        string? excludedPath = null)
    {
        string? normalizedExcluded = excludedPath is null
            ? null
            : Path.GetFullPath(excludedPath);
        return string.Join("\n",
            Directory.EnumerateFiles(sourceRoot, "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Where(path => normalizedExcluded is null ||
                               !string.Equals(Path.GetFullPath(path),
                                   normalizedExcluded,
                                   StringComparison.OrdinalIgnoreCase))
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
            if (Directory.Exists(Path.Combine(
                    current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(
                    current.FullName, "Documentation")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
