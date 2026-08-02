using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6x zero-caller valid-input contract for a topology-local physical
/// bank index bound to one issued geometry generation.
/// </summary>
public sealed class Rf126xPhysicalMemoryBankBindingCoreValidInputContractTests
{
    private const string ContractRelativePath =
        "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/PhysicalMemoryBankBinding.cs";

    [Fact]
    public void PublicShapeMatchesExactImmutableBindingAuthority()
    {
        Type type = typeof(PhysicalMemoryBankBinding);
        Assert.Equal("YAKSys_Hybrid_CPU.Memory", type.Namespace);
        Assert.True(type.IsValueType);
        Assert.True(type.IsSealed);

        Assert.Equal(typeof(PhysicalMemoryBankIndex), type
            .GetProperty(nameof(PhysicalMemoryBankBinding.BankIndex))!
            .PropertyType);
        Assert.Equal(typeof(MemoryBankGeometryGeneration), type
            .GetProperty(nameof(PhysicalMemoryBankBinding.Generation))!
            .PropertyType);
        Assert.All(type.GetProperties(BindingFlags.Public |
                                      BindingFlags.Instance),
            property => Assert.Null(property.SetMethod));

        ConstructorInfo constructor = Assert.Single(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            new[]
            {
                typeof(PhysicalMemoryBankIndex),
                typeof(MemoryBankGeometryGeneration)
            },
            constructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
    }

    [Theory]
    [InlineData(0, 1UL)]
    [InlineData(1, 2UL)]
    [InlineData(15, 16UL)]
    [InlineData(16, 17UL)]
    [InlineData(4_096, 65_535UL)]
    [InlineData(int.MaxValue, ulong.MaxValue)]
    public void RepresentativeBindingsHaveExactValueParity(
        int rawIndex,
        ulong rawGeneration)
    {
        PhysicalMemoryBankIndex index =
            PhysicalMemoryBankIndex.Create(rawIndex);
        MemoryBankGeometryGeneration generation =
            MemoryBankGeometryGeneration.Create(rawGeneration);

        Assert.True(PhysicalMemoryBankBinding.AreComponentsRepresentable(
            index, generation));

        PhysicalMemoryBankBinding fromConstructor =
            new(index, generation);
        PhysicalMemoryBankBinding fromCreate =
            PhysicalMemoryBankBinding.Create(index, generation);
        Assert.True(PhysicalMemoryBankBinding.TryCreate(
            index, generation, out PhysicalMemoryBankBinding fromTry));

        Assert.Equal(fromConstructor, fromCreate);
        Assert.Equal(fromConstructor, fromTry);
        Assert.True(fromConstructor.IsWellFormed);
        Assert.Equal(index, fromConstructor.BankIndex);
        Assert.Equal(generation, fromConstructor.Generation);
        Assert.Equal(
            $"physical-memory-bank-binding(index={rawIndex}, " +
            $"generation={rawGeneration})",
            fromConstructor.ToString());
    }

    [Fact]
    public void PhysicalBankZeroIsAValidPresentBinding()
    {
        PhysicalMemoryBankBinding binding = new(
            PhysicalMemoryBankIndex.Zero,
            MemoryBankGeometryGeneration.Create(1));

        Assert.True(binding.IsWellFormed);
        Assert.Equal(0, binding.BankIndex.Value);
        Assert.NotEqual(default, binding);
        Assert.NotEqual("no-physical-memory-bank-binding",
            binding.ToString());
    }

    [Fact]
    public void DeterministicSamplesPreserveIndexAndGeneration()
    {
        var random = new Random(0x1263);
        for (int sample = 0; sample < 2_000; sample++)
        {
            int rawIndex = random.Next(0, int.MaxValue);
            ulong rawGeneration =
                ((ulong)(uint)random.Next() << 32) |
                (uint)random.Next();
            if (rawGeneration == 0)
            {
                rawGeneration = 1;
            }

            PhysicalMemoryBankBinding binding =
                PhysicalMemoryBankBinding.Create(
                    PhysicalMemoryBankIndex.Create(rawIndex),
                    MemoryBankGeometryGeneration.Create(rawGeneration));

            Assert.Equal(rawIndex, binding.BankIndex.Value);
            Assert.Equal(rawGeneration, binding.Generation.Value);
            Assert.True(binding.IsWellFormed);
        }
    }

    [Fact]
    public void DefaultIsAbsenceAndDoesNotAliasBankZeroGenerationOne()
    {
        PhysicalMemoryBankBinding absent = default;
        PhysicalMemoryBankBinding first = PhysicalMemoryBankBinding.Create(
            PhysicalMemoryBankIndex.Zero,
            MemoryBankGeometryGeneration.Create(1));

        Assert.Equal(PhysicalMemoryBankIndex.Zero, absent.BankIndex);
        Assert.Equal(default, absent.Generation);
        Assert.False(absent.IsWellFormed);
        Assert.Equal("no-physical-memory-bank-binding", absent.ToString());
        Assert.NotEqual(first, absent);
    }

    [Fact]
    public void UnissuedGenerationRejectsWithoutZeroToOneNormalization()
    {
        PhysicalMemoryBankIndex index = PhysicalMemoryBankIndex.Zero;
        MemoryBankGeometryGeneration unissued = default;

        ArgumentOutOfRangeException exception = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            new PhysicalMemoryBankBinding(index, unissued));
        Assert.Equal("generation", exception.ParamName);
        Assert.False(PhysicalMemoryBankBinding.AreComponentsRepresentable(
            index, unissued));
        Assert.False(PhysicalMemoryBankBinding.TryCreate(
            index, unissued, out PhysicalMemoryBankBinding failed));
        Assert.Equal(default, failed);
        Assert.NotEqual(PhysicalMemoryBankBinding.Create(
            index, MemoryBankGeometryGeneration.Create(1)), failed);
    }

    [Fact]
    public void EqualityIncludesBothIndexAndGeneration()
    {
        PhysicalMemoryBankBinding baseline =
            PhysicalMemoryBankBinding.Create(
                PhysicalMemoryBankIndex.Create(7),
                MemoryBankGeometryGeneration.Create(11));

        Assert.NotEqual(baseline,
            PhysicalMemoryBankBinding.Create(
                PhysicalMemoryBankIndex.Create(8),
                MemoryBankGeometryGeneration.Create(11)));
        Assert.NotEqual(baseline,
            PhysicalMemoryBankBinding.Create(
                PhysicalMemoryBankIndex.Create(7),
                MemoryBankGeometryGeneration.Create(12)));
        Assert.Equal(baseline.GetHashCode(),
            PhysicalMemoryBankBinding.Create(
                PhysicalMemoryBankIndex.Create(7),
                MemoryBankGeometryGeneration.Create(11)).GetHashCode());
    }

    [Fact]
    public void BindingCannotClaimMembershipOrSameSnapshotProvenance()
    {
        string contract = ContractSource();

        foreach (string forbidden in new[]
                 {
                     "PhysicalMemoryBankGeometry", "BankCount",
                     "BankWidthBytes", "index <",
                     "SameSnapshot", "PublishedGeometry", "OwnerId",
                     "SubsystemId"
                 })
        {
            Assert.DoesNotContain(forbidden, contract,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ContractHasNoResolverRequestQueueArithmeticOrWireAuthority()
    {
        Type type = typeof(PhysicalMemoryBankBinding);
        Assert.DoesNotContain(type.GetMethods(
                BindingFlags.Public | BindingFlags.Static),
            method => method.IsSpecialName &&
                      method.Name.StartsWith("op_", StringComparison.Ordinal) &&
                      method.Name is not "op_Equality" and not "op_Inequality");

        string contract = ContractSource();
        Assert.DoesNotMatch(
            @"\b(?:Resolve|Publish|Install|Replace|Update|Accept|Enqueue|" +
            @"Dequeue|Complete|Cancel|Replay|Allocate|Issue|Next|Advance)\s*\(",
            contract);
        Assert.DoesNotMatch(
            @"(?:\+\+|--|%\s*|Math\.(?:Clamp|Max|Min)\s*\()",
            contract);
        Assert.DoesNotContain("JsonConstructor", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("implicit operator", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("explicit operator", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankResolution", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryUpdateResult", contract,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeclarationIsUniqueAndExternalCallersRemainZero()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(
            root, ContractRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string resolutionContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankResolution.cs"));

        string production = JoinSources(Path.Combine(root, "HybridCPU_ISE"),
            contractPath, resolutionContractPath);
        Assert.Contains(
            "PhysicalMemoryBankBinding PhysicalBankBinding",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "CapturePublishedPhysicalMemoryBankBindingUnderControllerGate(",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "Resolved(\n        PhysicalMemoryBankBinding binding)",
            File.ReadAllText(resolutionContractPath),
            StringComparison.Ordinal);
        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler", "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps"
                 })
        {
            Assert.DoesNotContain("PhysicalMemoryBankBinding",
                JoinSources(Path.Combine(root, externalRoot)),
                StringComparison.Ordinal);
        }

        Assert.Equal(1, Regex.Matches(ContractSource(),
            @"public\s+readonly\s+record\s+struct\s+" +
            @"PhysicalMemoryBankBinding\b").Count);
    }

    [Fact]
    public void ControllerStorageIsTypedWhileLegacyCancellationAndWiresRemainRaw()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(
            root, ContractRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string resolutionContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "PhysicalMemoryBankResolution.cs"));
        string updateResultContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemoryBankGeometryUpdateResult.cs"));
        string production = JoinSources(
            Path.Combine(root, "HybridCPU_ISE"),
            contractPath, resolutionContractPath, updateResultContractPath);
        string operations = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Operations.cs");

        Assert.Contains(
            "PhysicalMemoryBankBinding PhysicalBankBinding",
            production, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(
            production,
            @"request\.PhysicalBankBinding").Count);
        Assert.DoesNotContain("GeometryGeneration", operations,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ComputeBankId(token.Address)",
            operations, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankResolution", production,
            StringComparison.Ordinal);
        Assert.Contains(
            "private readonly PhysicalMemoryBankBinding? _binding;",
            File.ReadAllText(resolutionContractPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "public MemoryBankGeometryUpdateResult TryReplacePhysicalMemoryBankGeometry(",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "public readonly record struct MemoryBankGeometryUpdateResult",
            File.ReadAllText(updateResultContractPath),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ContractDoesNotConflateSchedulerOrOtherIdentifierFamilies()
    {
        string contract = ContractSource();
        foreach (string forbidden in new[]
                 {
                     "MemoryBankId", "VtId", "SlotId", "LaneId",
                     "ReplayToken", "TokenId", "DomainId", "Certificate",
                     "MemoryRequestId", "Queue<", "Dictionary<", "BitArray"
                 })
        {
            Assert.DoesNotContain(forbidden, contract,
                StringComparison.Ordinal);
        }
    }


    private static string ContractSource() => Read(
        FindRepositoryRoot(), ContractRelativePath.Split('/'));

    private static string Evidence(string root) => Read(root,
        "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6x-physical-memory-bank-binding-core-valid-input-contract.md");

    private static string JoinSources(
        string sourceRoot,
        params string[] excludedPaths)
    {
        var excluded = excludedPaths
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return string.Join("\n",
            Directory.EnumerateFiles(sourceRoot, "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Where(path => !excluded.Contains(Path.GetFullPath(path)))
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
