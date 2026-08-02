using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6aj zero-caller valid-input guards for the immutable canonical
/// vector physical-bank envelope. No production caller or invalid-input
/// contour is migrated by this contract slice.
/// </summary>
public sealed class
    Rf126ajCanonicalVectorPhysicalBankEnvelopeCoreValidInputContractTests
{
    [Fact]
    public void MinGenerationAndPhysicalBankZeroAreValid()
    {
        CanonicalVectorPhysicalBankEnvelope envelope = new(
            MemoryBankGeometryGeneration.Create(
                MemoryBankGeometryGeneration.MinValue),
            new[] { PhysicalMemoryBankIndex.Zero });

        Assert.True(envelope.IsWellFormed);
        Assert.Equal(1, envelope.Count);
        Assert.Equal(1UL, envelope.ElementCount);
        Assert.Equal(MemoryBankGeometryGeneration.MinValue,
            envelope.Generation.Value);
        Assert.Equal(PhysicalMemoryBankIndex.MinValue,
            envelope.GetSourceBankIndex(0).Value);
        Assert.Equal(PhysicalMemoryBankIndex.MinValue,
            envelope.SourceBankIndexes[0].Value);
    }

    [Fact]
    public void MaxComponentsDuplicatesAndOrderRoundTripExactly()
    {
        PhysicalMemoryBankIndex[] indexes =
        {
            PhysicalMemoryBankIndex.Create(PhysicalMemoryBankIndex.MaxValue),
            PhysicalMemoryBankIndex.Zero,
            PhysicalMemoryBankIndex.Create(17),
            PhysicalMemoryBankIndex.Zero
        };

        CanonicalVectorPhysicalBankEnvelope envelope =
            CanonicalVectorPhysicalBankEnvelope.Create(
                MemoryBankGeometryGeneration.Create(
                    MemoryBankGeometryGeneration.MaxValue),
                indexes);

        Assert.True(envelope.IsWellFormed);
        Assert.Equal(MemoryBankGeometryGeneration.MaxValue,
            envelope.Generation.Value);
        Assert.Equal(
            new[]
            {
                PhysicalMemoryBankIndex.MaxValue, 0, 17, 0
            },
            envelope.CopySourceBankIndexes()
                .Select(index => index.ToRawValue()));
    }

    [Fact]
    public void ConstructorAndRawProjectionUseDefensiveCopies()
    {
        PhysicalMemoryBankIndex[] input =
        {
            PhysicalMemoryBankIndex.Zero,
            PhysicalMemoryBankIndex.Create(3)
        };
        CanonicalVectorPhysicalBankEnvelope envelope = new(
            MemoryBankGeometryGeneration.Create(7),
            input);

        input[0] = PhysicalMemoryBankIndex.Create(99);
        PhysicalMemoryBankIndex[] projected =
            envelope.CopySourceBankIndexes();
        projected[1] = PhysicalMemoryBankIndex.Create(88);

        Assert.Equal(0, envelope.GetSourceBankIndex(0).Value);
        Assert.Equal(3, envelope.GetSourceBankIndex(1).Value);
        Assert.Equal(new[] { 0, 3 },
            envelope.SourceBankIndexes.ToArray()
                .Select(index => index.Value));
    }

    [Fact]
    public void EmptyAndUnissuedComponentsFailWithoutZeroAlias()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CanonicalVectorPhysicalBankEnvelope(
                default,
                new[] { PhysicalMemoryBankIndex.Zero }));
        Assert.Throws<ArgumentException>(() =>
            new CanonicalVectorPhysicalBankEnvelope(
                MemoryBankGeometryGeneration.Create(1),
                ReadOnlySpan<PhysicalMemoryBankIndex>.Empty));

        Assert.False(CanonicalVectorPhysicalBankEnvelope.TryCreate(
            default,
            new[] { PhysicalMemoryBankIndex.Zero },
            out CanonicalVectorPhysicalBankEnvelope unissued));
        Assert.False(unissued.IsWellFormed);
        Assert.Equal(0, unissued.Count);
        Assert.Empty(unissued.SourceBankIndexes.ToArray());

        Assert.False(CanonicalVectorPhysicalBankEnvelope.TryCreate(
            MemoryBankGeometryGeneration.Create(1),
            ReadOnlySpan<PhysicalMemoryBankIndex>.Empty,
            out CanonicalVectorPhysicalBankEnvelope empty));
        Assert.False(empty.IsWellFormed);
        Assert.Throws<InvalidOperationException>(
            () => empty.GetSourceBankIndex(0));
        Assert.NotEqual(
            CanonicalVectorPhysicalBankEnvelope.Create(
                MemoryBankGeometryGeneration.Create(1),
                new[] { PhysicalMemoryBankIndex.Zero }).ToString(),
            empty.ToString());
    }

    [Fact]
    public void PublicShapeIsReadonlyCheckedAndArrayIsPrivate()
    {
        Type type = typeof(CanonicalVectorPhysicalBankEnvelope);
        Assert.True(type.IsValueType);
        Assert.True(type.IsDefined(
            typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute),
            inherit: false));

        PropertyInfo generation = type.GetProperty(
            nameof(CanonicalVectorPhysicalBankEnvelope.Generation))!;
        PropertyInfo count = type.GetProperty(
            nameof(CanonicalVectorPhysicalBankEnvelope.Count))!;
        PropertyInfo elementCount = type.GetProperty(
            nameof(CanonicalVectorPhysicalBankEnvelope.ElementCount))!;
        PropertyInfo indexes = type.GetProperty(
            nameof(CanonicalVectorPhysicalBankEnvelope.SourceBankIndexes))!;
        Assert.False(generation.CanWrite);
        Assert.False(count.CanWrite);
        Assert.False(elementCount.CanWrite);
        Assert.False(indexes.CanWrite);
        Assert.Equal(typeof(MemoryBankGeometryGeneration),
            generation.PropertyType);
        Assert.Equal(typeof(int), count.PropertyType);
        Assert.Equal(typeof(ulong), elementCount.PropertyType);
        Assert.Equal(typeof(ReadOnlySpan<PhysicalMemoryBankIndex>),
            indexes.PropertyType);

        FieldInfo storage = type.GetField(
            "_sourceBankIndexes",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.True(storage.IsPrivate);
        Assert.True(storage.IsInitOnly);
        Assert.Equal(typeof(PhysicalMemoryBankIndex[]), storage.FieldType);

        ConstructorInfo constructor = Assert.Single(
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            new[]
            {
                typeof(MemoryBankGeometryGeneration),
                typeof(ReadOnlySpan<PhysicalMemoryBankIndex>)
            },
            constructor.GetParameters()
                .Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void CheckedContractDoesNotClaimOwnerOrExecutionAuthority()
    {
        string root = FindRepositoryRoot();
        string contract = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "CanonicalVectorPhysicalBankEnvelope.cs");

        Assert.Contains("does not prove geometry membership",
            contract, StringComparison.Ordinal);
        Assert.Contains("same-snapshot", contract,
            StringComparison.Ordinal);
        Assert.Contains("request ownership, execution",
            contract, StringComparison.Ordinal);
        Assert.Contains("store visibility or",
            contract, StringComparison.Ordinal);
        Assert.Contains("publication authority",
            contract, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("LaneId", contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TokenId", contract,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LaterSelectedOwnerStorageHasNoExternalOrFunctionalCallers()
    {
        string root = FindRepositoryRoot();
        string contractPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
            "CanonicalVectorPhysicalBankEnvelope.cs"));
        string thisPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126ajCanonicalVectorPhysicalBankEnvelopeCoreValidInputContractTests.cs"));
        string decisionGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126aiCanonicalVectorPhysicalBankEnvelopeArchitectureDecisionTests.cs"));
        string inventoryGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126aMemoryBankGeometryResolutionInventoryDecisionTests.cs"));
        string lifetimeGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126nPhysicalMemoryBankGeometryLifetimeArchitectureDecisionTests.cs"));
        string indexGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126oPhysicalMemoryBankIndexCoreValidInputContractTests.cs"));
        string snapshotGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126vPhysicalMemoryBankGeometrySnapshotRevalidationTests.cs"));
        string ingressGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf120ResourceIdIngressGuardTests.cs"));
        string contourGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126akCanonicalEnvelopeAdmissionStorageServiceRevalidationTests.cs"));
        string captureGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126alCanonicalEnvelopeCaptureAndPrivateStorageValidInputCutoverTests.cs"));
        string serviceGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126amCanonicalStoredEnvelopeServiceConsumptionValidInputCutoverTests.cs"));
        string mismatchDecisionGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126anCanonicalEnvelopeMismatchInvalidBehaviorArchitectureDecisionTests.cs"));
        string removalDecisionGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126aoCanonicalSourceBaseBindingRemovalEligibilityDecisionTests.cs"));
        string compatibilityRemovalGuardPath = Path.GetFullPath(Path.Combine(root,
            "HybridCPU_ISE.Tests", "Architecture",
            "Rf126apCanonicalSourceBaseBindingCompatibilityRemovalTests.cs"));

        string production = ReadTree(
            Path.Combine(root, "HybridCPU_ISE"), contractPath);
        Assert.Equal(6, Regex.Matches(production,
            @"\bCanonicalVectorPhysicalBankEnvelope\b").Count);
        AssertNoEnvelopeReference(ReadTree(
            Path.Combine(root, "HybridCPU_Compiler")));
        AssertNoEnvelopeReference(ReadTree(
            Path.Combine(root, "HybridCPU_RoslynBridge")));
        AssertNoEnvelopeReference(ReadTree(
            Path.Combine(root, "CpuInterfaceBridge")));
        AssertNoEnvelopeReference(ReadTree(
            Path.Combine(root, "TestAssemblerConsoleApps")));
        AssertNoEnvelopeReference(ReadTree(
            Path.Combine(root, "HybridCPU_ISE.Tests"),
            thisPath, decisionGuardPath, inventoryGuardPath,
            lifetimeGuardPath, indexGuardPath, snapshotGuardPath,
            ingressGuardPath, contourGuardPath, captureGuardPath,
            serviceGuardPath, mismatchDecisionGuardPath,
            removalDecisionGuardPath, compatibilityRemovalGuardPath));

        string controller = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Memory", "Timing", "MemoryCycleController.cs");
        Assert.Contains("CanonicalVectorPhysicalBankEnvelope",
            controller, StringComparison.Ordinal);
        Assert.Contains("PhysicalMemoryBankBinding physicalBankBinding",
            controller, StringComparison.Ordinal);
    }


    private static void AssertNoEnvelopeReference(string source) =>
        Assert.DoesNotMatch(
            new Regex(@"\bCanonicalVectorPhysicalBankEnvelope\b",
                RegexOptions.CultureInvariant),
            source);

    private static string ReadTree(
        string root,
        params string[] excludedPaths) =>
        string.Join("\n", Directory.EnumerateFiles(root, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !excludedPaths.Contains(
                Path.GetFullPath(path),
                StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

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
