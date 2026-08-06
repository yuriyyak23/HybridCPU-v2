using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6z zero-caller discriminated geometry-update result contract.
/// </summary>
public sealed class Rf126zMemoryBankGeometryUpdateResultCoreValidInputContractTests
{
    private const string ContractRelativePath =
        "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/" +
        "MemoryBankGeometryUpdateResult.cs";

    [Fact]
    public void PublicShapeMatchesExactClosedGeometryUpdateUnion()
    {
        Type type = typeof(MemoryBankGeometryUpdateResult);
        Assert.Equal("YAKSys_Hybrid_CPU.Memory", type.Namespace);
        Assert.True(type.IsValueType);
        Assert.True(type.IsSealed);
        Assert.Empty(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));

        Assert.Equal(typeof(MemoryBankGeometryUpdateResultKind), type
            .GetProperty(nameof(MemoryBankGeometryUpdateResult.Kind))!
            .PropertyType);
        Assert.Equal(typeof(MemoryBankGeometryUpdateRejectReason?), type
            .GetProperty(nameof(MemoryBankGeometryUpdateResult.RejectReason))!
            .PropertyType);
        Assert.All(type.GetProperties(BindingFlags.Public |
                                      BindingFlags.Instance),
            property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void DiscriminantAndReasonDomainsAreExactBytesInAuthorityOrder()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(
            typeof(MemoryBankGeometryUpdateResultKind)));
        Assert.Equal(
            new[]
            {
                MemoryBankGeometryUpdateResultKind.Rejected,
                MemoryBankGeometryUpdateResultKind.Applied
            },
            Enum.GetValues<MemoryBankGeometryUpdateResultKind>());
        Assert.Equal(new byte[] { 0, 1 },
            Enum.GetValues<MemoryBankGeometryUpdateResultKind>()
                .Select(kind => (byte)kind));

        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(
            typeof(MemoryBankGeometryUpdateRejectReason)));
        Assert.Equal(
            new[]
            {
                MemoryBankGeometryUpdateRejectReason.InvalidBankCount,
                MemoryBankGeometryUpdateRejectReason.InvalidBankWidth,
                MemoryBankGeometryUpdateRejectReason.Busy,
                MemoryBankGeometryUpdateRejectReason.GenerationExhausted,
                MemoryBankGeometryUpdateRejectReason.PlatformRejected
            },
            Enum.GetValues<MemoryBankGeometryUpdateRejectReason>());
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4 },
            Enum.GetValues<MemoryBankGeometryUpdateRejectReason>()
                .Select(reason => (byte)reason));
    }

    [Fact]
    public void DefaultFailsClosedAsInvalidBankCount()
    {
        MemoryBankGeometryUpdateResult result = default;

        Assert.Equal(MemoryBankGeometryUpdateResultKind.Rejected, result.Kind);
        Assert.False(result.IsApplied);
        Assert.Equal(
            MemoryBankGeometryUpdateRejectReason.InvalidBankCount,
            result.RejectReason);
        Assert.Equal(result,
            MemoryBankGeometryUpdateResult.InvalidBankCount);
        Assert.True(result.TryGetRejectReason(
            out MemoryBankGeometryUpdateRejectReason reason));
        Assert.Equal(
            MemoryBankGeometryUpdateRejectReason.InvalidBankCount,
            reason);
        Assert.Equal("Rejected(InvalidBankCount)", result.ToString());
    }

    [Theory]
    [InlineData(MemoryBankGeometryUpdateRejectReason.InvalidBankCount)]
    [InlineData(MemoryBankGeometryUpdateRejectReason.InvalidBankWidth)]
    [InlineData(MemoryBankGeometryUpdateRejectReason.Busy)]
    [InlineData(MemoryBankGeometryUpdateRejectReason.GenerationExhausted)]
    [InlineData(MemoryBankGeometryUpdateRejectReason.PlatformRejected)]
    public void EveryExactRejectionCarriesOnlyItsReason(
        MemoryBankGeometryUpdateRejectReason reason)
    {
        Assert.True(MemoryBankGeometryUpdateResult.IsRepresentable(reason));
        MemoryBankGeometryUpdateResult result =
            MemoryBankGeometryUpdateResult.Rejected(reason);

        Assert.Equal(MemoryBankGeometryUpdateResultKind.Rejected, result.Kind);
        Assert.False(result.IsApplied);
        Assert.Equal(reason, result.RejectReason);
        Assert.True(result.TryGetRejectReason(
            out MemoryBankGeometryUpdateRejectReason actual));
        Assert.Equal(reason, actual);
        Assert.Equal($"Rejected({reason})", result.ToString());
    }

    [Fact]
    public void AppliedCarriesNoRejectReason()
    {
        MemoryBankGeometryUpdateResult result =
            MemoryBankGeometryUpdateResult.Applied;

        Assert.Equal(MemoryBankGeometryUpdateResultKind.Applied, result.Kind);
        Assert.True(result.IsApplied);
        Assert.Null(result.RejectReason);
        Assert.False(result.TryGetRejectReason(out _));
        Assert.NotEqual(default, result);
        Assert.Equal("Applied", result.ToString());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(127)]
    [InlineData(255)]
    public void UnknownRejectReasonCannotAliasInvalidBankCount(byte raw)
    {
        MemoryBankGeometryUpdateRejectReason reason =
            (MemoryBankGeometryUpdateRejectReason)raw;

        Assert.False(MemoryBankGeometryUpdateResult.IsRepresentable(reason));
        ArgumentOutOfRangeException exception = Assert.Throws<
            ArgumentOutOfRangeException>(() =>
                MemoryBankGeometryUpdateResult.Rejected(reason));
        Assert.Equal("reason", exception.ParamName);
    }

    [Fact]
    public void EqualitySeparatesAppliedAndEveryRejection()
    {
        MemoryBankGeometryUpdateResult applied =
            MemoryBankGeometryUpdateResult.Applied;
        MemoryBankGeometryUpdateResult invalidCount =
            MemoryBankGeometryUpdateResult.Rejected(
                MemoryBankGeometryUpdateRejectReason.InvalidBankCount);
        MemoryBankGeometryUpdateResult invalidWidth =
            MemoryBankGeometryUpdateResult.Rejected(
                MemoryBankGeometryUpdateRejectReason.InvalidBankWidth);

        Assert.NotEqual(applied, invalidCount);
        Assert.NotEqual(invalidCount, invalidWidth);
        Assert.Equal(default, invalidCount);
    }

    [Fact]
    public void ContractHasNoMutationAllocationQuiescenceOrWireAuthority()
    {
        Type type = typeof(MemoryBankGeometryUpdateResult);
        Assert.DoesNotContain(type.GetMethods(
                BindingFlags.Public | BindingFlags.Static),
            method => method.IsSpecialName &&
                      method.Name.StartsWith("op_", StringComparison.Ordinal) &&
                      method.Name is not "op_Equality" and not "op_Inequality");

        string contract = ContractSource();
        Assert.DoesNotMatch(
            @"\b(?:Publish|Install|Replace|Resolve|Accept|Enqueue|Dequeue|" +
            @"Complete|Cancel|Replay|Allocate|Advance|Prepare|Commit)\s*\(",
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
        Assert.DoesNotContain("PhysicalMemoryBankGeometry", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryRequestId", contract,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeclarationIsUniqueWithOneOwnerAndZeroExternalCallers()
    {
        string root = FindRepositoryRoot();
        string contractPath = ContractPath(root);
        string production = JoinSources(
            Path.Combine(root, "HybridCPU_ISE"), contractPath);

        Assert.Contains(
            "public MemoryBankGeometryUpdateResult TryReplacePhysicalMemoryBankGeometry(",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "MemoryBankGeometryUpdateRejectReason.GenerationExhausted",
            production, StringComparison.Ordinal);
        foreach (string externalRoot in new[]
                 {
                     "HybridCPU_Compiler", "CpuInterfaceBridge",
                     "HybridCPU_RoslynBridge", "TestAssemblerConsoleApps"
                 })
        {
            string external = JoinSources(Path.Combine(root, externalRoot));
            Assert.DoesNotContain("MemoryBankGeometryUpdateResult", external,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "MemoryBankGeometryUpdateRejectReason", external,
                StringComparison.Ordinal);
        }

        string contract = ContractSource();
        Assert.Equal(1, Regex.Matches(contract,
            @"public\s+readonly\s+record\s+struct\s+" +
            @"MemoryBankGeometryUpdateResult\b").Count);
        Assert.Equal(1, Regex.Matches(contract,
            @"public\s+enum\s+MemoryBankGeometryUpdateRejectReason\b").Count);
    }

    [Fact]
    public void RuntimeLifecycleIsAuthoritativeAndCompatibilitySettersRemainRaw()
    {
        string root = FindRepositoryRoot();
        string production = JoinSources(
            Path.Combine(root, "HybridCPU_ISE"), ContractPath(root));
        string subsystem = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.cs");

        Assert.Contains(
            "public MemoryBankGeometryUpdateResult TryReplacePhysicalMemoryBankGeometry(",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "private ulong _lastIssuedPhysicalBankGeometryGeneration;",
            production, StringComparison.Ordinal);
        Assert.Contains(
            "private PhysicalMemoryBankGeometry _publishedPhysicalBankGeometry;",
            production, StringComparison.Ordinal);
        Assert.Contains("private int _numBanks = 8;", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("int sanitized = Math.Max(1, value);", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("_numBanks = sanitized;", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("ReconfigureBankTopology();", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("private int _bankWidthBytes = 64;",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("_bankWidthBytes = value;", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("MemoryBankGeometryGeneration.Create(",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("PhysicalMemoryBankGeometry.Create(",
            subsystem, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateResultDoesNotConflateOtherIdentifierFamilies()
    {
        string contract = ContractSource();
        foreach (string forbidden in new[]
                 {
                     "MemoryBankId", "PhysicalMemoryBankIndex",
                     "MemoryBankGeometryGeneration",
                     "PhysicalMemoryBankBinding",
                     "PhysicalMemoryBankResolution", "VtId", "SlotId",
                     "LaneId", "Pinning", "ChannelId", "DomainId", "TokenId",
                     "Replay", "Certificate", "Telemetry"
                 })
        {
            Assert.DoesNotContain(forbidden, contract,
                StringComparison.Ordinal);
        }
    }


    private static string ContractSource() => Read(
        FindRepositoryRoot(), ContractRelativePath.Split('/'));

    private static string ContractPath(string root) => Path.GetFullPath(
        Path.Combine(root, ContractRelativePath.Replace(
            '/', Path.DirectorySeparatorChar)));

    private static string Evidence(string root) => Read(root,
        "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6z-memory-bank-geometry-update-result-core-valid-input-contract.md");

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
