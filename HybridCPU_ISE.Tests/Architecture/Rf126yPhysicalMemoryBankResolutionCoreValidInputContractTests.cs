using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6y zero-caller discriminated physical bank-resolution contract.
/// </summary>
public sealed class Rf126yPhysicalMemoryBankResolutionCoreValidInputContractTests
{
    private const string ContractRelativePath =
        "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/PhysicalMemoryBankResolution.cs";

    [Fact]
    public void PublicShapeMatchesExactClosedPhysicalResolutionUnion()
    {
        Type type = typeof(PhysicalMemoryBankResolution);
        Assert.Equal("YAKSys_Hybrid_CPU.Memory", type.Namespace);
        Assert.True(type.IsValueType);
        Assert.True(type.IsSealed);
        Assert.Empty(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));

        Assert.Equal(typeof(PhysicalMemoryBankResolutionKind), type
            .GetProperty(nameof(PhysicalMemoryBankResolution.Kind))!
            .PropertyType);
        Assert.Equal(typeof(PhysicalMemoryBankBinding?), type
            .GetProperty(nameof(PhysicalMemoryBankResolution.Binding))!
            .PropertyType);
        Assert.Equal(typeof(PhysicalMemoryBankUnavailableReason?), type
            .GetProperty(nameof(
                PhysicalMemoryBankResolution.UnavailableReason))!
            .PropertyType);
        Assert.All(type.GetProperties(BindingFlags.Public |
                                      BindingFlags.Instance),
            property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void DiscriminantAndReasonDomainsAreExactBytes()
    {
        Assert.Equal(typeof(byte),
            Enum.GetUnderlyingType(typeof(PhysicalMemoryBankResolutionKind)));
        Assert.Equal(
            new[]
            {
                PhysicalMemoryBankResolutionKind.Unavailable,
                PhysicalMemoryBankResolutionKind.Resolved
            },
            Enum.GetValues<PhysicalMemoryBankResolutionKind>());
        Assert.Equal((byte)0,
            (byte)PhysicalMemoryBankResolutionKind.Unavailable);
        Assert.Equal((byte)1,
            (byte)PhysicalMemoryBankResolutionKind.Resolved);

        Assert.Equal(typeof(byte),
            Enum.GetUnderlyingType(
                typeof(PhysicalMemoryBankUnavailableReason)));
        Assert.Equal(
            new[]
            {
                PhysicalMemoryBankUnavailableReason.NoPublishedGeometry,
                PhysicalMemoryBankUnavailableReason.InvalidBankCount,
                PhysicalMemoryBankUnavailableReason.InvalidBankWidth,
                PhysicalMemoryBankUnavailableReason.GenerationUnavailable
            },
            Enum.GetValues<PhysicalMemoryBankUnavailableReason>());
        Assert.Equal(
            new byte[] { 0, 1, 2, 3 },
            Enum.GetValues<PhysicalMemoryBankUnavailableReason>()
                .Select(reason => (byte)reason)
                .ToArray());
    }

    [Fact]
    public void DefaultFailsClosedAsNoPublishedGeometryWithoutBinding()
    {
        PhysicalMemoryBankResolution result = default;

        Assert.Equal(PhysicalMemoryBankResolutionKind.Unavailable,
            result.Kind);
        Assert.False(result.IsResolved);
        Assert.Null(result.Binding);
        Assert.Equal(
            PhysicalMemoryBankUnavailableReason.NoPublishedGeometry,
            result.UnavailableReason);
        Assert.Equal(result,
            PhysicalMemoryBankResolution.NoPublishedGeometry);
        Assert.Equal("Unavailable(NoPublishedGeometry)",
            result.ToString());

        Assert.False(result.TryGetResolved(
            out PhysicalMemoryBankBinding absentBinding));
        Assert.Equal(default, absentBinding);
        Assert.True(result.TryGetUnavailableReason(
            out PhysicalMemoryBankUnavailableReason reason));
        Assert.Equal(
            PhysicalMemoryBankUnavailableReason.NoPublishedGeometry,
            reason);
    }

    [Theory]
    [InlineData(PhysicalMemoryBankUnavailableReason.NoPublishedGeometry)]
    [InlineData(PhysicalMemoryBankUnavailableReason.InvalidBankCount)]
    [InlineData(PhysicalMemoryBankUnavailableReason.InvalidBankWidth)]
    [InlineData(PhysicalMemoryBankUnavailableReason.GenerationUnavailable)]
    public void EveryUnavailableReasonCarriesNeitherIndexNorGeneration(
        PhysicalMemoryBankUnavailableReason reason)
    {
        Assert.True(PhysicalMemoryBankResolution.IsRepresentable(reason));
        PhysicalMemoryBankResolution result =
            PhysicalMemoryBankResolution.Unavailable(reason);

        Assert.Equal(PhysicalMemoryBankResolutionKind.Unavailable,
            result.Kind);
        Assert.False(result.IsResolved);
        Assert.Null(result.Binding);
        Assert.Equal(reason, result.UnavailableReason);
        Assert.Equal($"Unavailable({reason})", result.ToString());
        Assert.False(result.TryGetResolved(out _));
        Assert.True(result.TryGetUnavailableReason(
            out PhysicalMemoryBankUnavailableReason actual));
        Assert.Equal(reason, actual);
    }

    [Theory]
    [InlineData(0, 1UL)]
    [InlineData(1, 2UL)]
    [InlineData(15, 16UL)]
    [InlineData(int.MaxValue, ulong.MaxValue)]
    public void ResolvedCarriesExactlyOneWellFormedBinding(
        int rawIndex,
        ulong rawGeneration)
    {
        PhysicalMemoryBankBinding binding =
            PhysicalMemoryBankBinding.Create(
                PhysicalMemoryBankIndex.Create(rawIndex),
                MemoryBankGeometryGeneration.Create(rawGeneration));
        PhysicalMemoryBankResolution result =
            PhysicalMemoryBankResolution.Resolved(binding);

        Assert.Equal(PhysicalMemoryBankResolutionKind.Resolved, result.Kind);
        Assert.True(result.IsResolved);
        Assert.Equal(binding, result.Binding);
        Assert.Null(result.UnavailableReason);
        Assert.True(result.TryGetResolved(
            out PhysicalMemoryBankBinding actual));
        Assert.Equal(binding, actual);
        Assert.False(result.TryGetUnavailableReason(out _));
        Assert.Equal($"Resolved({binding})", result.ToString());
    }

    [Fact]
    public void ResolvedPhysicalBankZeroDoesNotAliasUnavailable()
    {
        PhysicalMemoryBankBinding binding =
            PhysicalMemoryBankBinding.Create(
                PhysicalMemoryBankIndex.Zero,
                MemoryBankGeometryGeneration.Create(1));
        PhysicalMemoryBankResolution resolved =
            PhysicalMemoryBankResolution.Resolved(binding);

        Assert.True(resolved.IsResolved);
        Assert.Equal(0, resolved.Binding!.Value.BankIndex.Value);
        Assert.NotEqual(default, resolved);
        Assert.NotEqual(
            PhysicalMemoryBankResolution.NoPublishedGeometry,
            resolved);
    }

    [Fact]
    public void DefaultOrMalformedBindingCannotBecomeResolved()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PhysicalMemoryBankResolution.Resolved(default));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalMemoryBankResolution.Resolved(
                new PhysicalMemoryBankBinding(
                    PhysicalMemoryBankIndex.Zero,
                    default)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(127)]
    [InlineData(255)]
    public void UnknownUnavailableReasonRejectsWithoutFallback(byte raw)
    {
        PhysicalMemoryBankUnavailableReason invalid =
            (PhysicalMemoryBankUnavailableReason)raw;

        Assert.False(PhysicalMemoryBankResolution.IsRepresentable(invalid));
        ArgumentOutOfRangeException exception = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            PhysicalMemoryBankResolution.Unavailable(invalid));
        Assert.Equal("reason", exception.ParamName);
    }

    [Fact]
    public void EqualitySeparatesResolvedBindingAndUnavailableReason()
    {
        PhysicalMemoryBankBinding binding1 =
            PhysicalMemoryBankBinding.Create(
                PhysicalMemoryBankIndex.Zero,
                MemoryBankGeometryGeneration.Create(1));
        PhysicalMemoryBankBinding binding2 =
            PhysicalMemoryBankBinding.Create(
                PhysicalMemoryBankIndex.Create(1),
                MemoryBankGeometryGeneration.Create(1));

        Assert.NotEqual(
            PhysicalMemoryBankResolution.Resolved(binding1),
            PhysicalMemoryBankResolution.Resolved(binding2));
        Assert.NotEqual(
            PhysicalMemoryBankResolution.Unavailable(
                PhysicalMemoryBankUnavailableReason.InvalidBankCount),
            PhysicalMemoryBankResolution.Unavailable(
                PhysicalMemoryBankUnavailableReason.InvalidBankWidth));
        Assert.NotEqual(
            PhysicalMemoryBankResolution.Resolved(binding1),
            PhysicalMemoryBankResolution.Unavailable(
                PhysicalMemoryBankUnavailableReason.NoPublishedGeometry));
    }

    [Fact]
    public void ContractHasNoResolverUpdateRequestArithmeticOrWireAuthority()
    {
        Type type = typeof(PhysicalMemoryBankResolution);
        Assert.DoesNotContain(type.GetMethods(
                BindingFlags.Public | BindingFlags.Static),
            method => method.IsSpecialName &&
                      method.Name.StartsWith("op_", StringComparison.Ordinal) &&
                      method.Name is not "op_Equality" and not "op_Inequality");

        string contract = ContractSource();
        Assert.DoesNotMatch(
            @"\b(?:Compute|ResolveAddress|Publish|Install|Replace|Update|" +
            @"Accept|Enqueue|Dequeue|Complete|Cancel|Replay|Allocate)\s*\(",
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
        Assert.DoesNotContain("MemoryBankGeometryUpdateResult", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryRequestId", contract,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeclarationIsUniqueAndProductionExternalCallersAreZero()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(
            root, ContractRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.DoesNotContain("PhysicalMemoryBankResolution",
            JoinSources(Path.Combine(root, "HybridCPU_ISE"), contractPath),
            StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankUnavailableReason",
            JoinSources(Path.Combine(root, "HybridCPU_ISE"), contractPath),
            StringComparison.Ordinal);
        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler", "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps"
                 })
        {
            string external = JoinSources(Path.Combine(root, externalRoot));
            Assert.DoesNotContain("PhysicalMemoryBankResolution", external,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "PhysicalMemoryBankUnavailableReason", external,
                StringComparison.Ordinal);
        }

        string contract = ContractSource();
        Assert.Equal(1, Regex.Matches(contract,
            @"public\s+readonly\s+record\s+struct\s+" +
            @"PhysicalMemoryBankResolution\b").Count);
        Assert.Equal(1, Regex.Matches(contract,
            @"public\s+enum\s+PhysicalMemoryBankUnavailableReason\b").Count);
    }

    [Fact]
    public void RuntimeResolverStorageRequestsQueuesAndWiresRemainRaw()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(
            root, ContractRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string updateResultContractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "MemoryBankGeometryUpdateResult.cs"));
        string production = JoinSources(
            Path.Combine(root, "HybridCPU_ISE"),
            contractPath, updateResultContractPath);
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Helpers.cs");
        string operations = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Operations.cs");

        Assert.DoesNotContain("PhysicalMemoryBankResolution", production,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PhysicalMemoryBankUnavailableReason", production,
            StringComparison.Ordinal);
        Assert.Contains(
            "private PhysicalMemoryBankIndex ComputeBankId(ulong address)",
            helpers, StringComparison.Ordinal);
        Assert.DoesNotContain("ComputeBankId(token.Address)", operations,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GeometryGeneration", operations,
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
    public void PhysicalAndSchedulerVisibleResolutionRemainDistinct()
    {
        string contract = ContractSource();

        Assert.DoesNotContain(
            "YAKSys_Hybrid_CPU.Core.Decoder.MemoryBankResolution",
            contract, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", contract,
            StringComparison.Ordinal);
        Assert.Contains("distinct from scheduler-visible memory-bank resolution",
            contract, StringComparison.Ordinal);
    }


    private static string ContractSource() => Read(
        FindRepositoryRoot(), ContractRelativePath.Split('/'));

    private static string Evidence(string root) => Read(root,
        "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6y-physical-memory-bank-resolution-core-valid-input-contract.md");

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
