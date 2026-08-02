using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123acCustomAcceleratorRegisterMaskInventoryTests
{
    [Fact]
    public void PaperDefinesRegisterRolesRawAliasesAndSeparateAcceleratorFamily()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.13 Retained custom-accelerator architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("zero selects\n   accelerator resource zero and is not absence",
            paper, StringComparison.Ordinal);
        Assert.Contains("every element including x0 participates",
            paper, StringComparison.Ordinal);
        Assert.Contains("negative inputs can alias low resource groups",
            paper, StringComparison.Ordinal);
        Assert.Contains("null input array is\nassigned to `ReadRegisters`",
            paper, StringComparison.Ordinal);
        Assert.Contains("`outputRegId` is\nnot copied to `DestRegID`",
            paper, StringComparison.Ordinal);
        Assert.Contains("`acceleratorId` and `ForAccelerator` must remain raw and\nunchanged",
            paper, StringComparison.Ordinal);
        Assert.Contains("introduces no universal `ChannelId`,\n`DomainId` or `TokenId`",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedWorldCarrierAndCallerManifestsRemainFrozen()
    {
        string root = FindRepositoryRoot();

        Assert.Equal(
            [
                "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.Misc.cs"
            ],
            FindFilesContaining(root, "HybridCPU_ISE",
                nameof(CustomAcceleratorMicroOp)));
        Assert.Empty(FindFilesContaining(root, "HybridCPU_Compiler",
            nameof(CustomAcceleratorMicroOp)));
        Assert.Empty(FindFilesContaining(root, "TestAssemblerConsoleApps",
            nameof(CustomAcceleratorMicroOp)));
        Assert.Equal(
            [
                "HybridCPU_ISE.Tests/Architecture/Rf084agMiscRegisterWriteReachabilityAuditTests.cs",
                "HybridCPU_ISE.Tests/Architecture/Rf084bbConsolidatedExitEvidenceTests.cs",
                "HybridCPU_ISE.Tests/PhasingAndExtensions/SlotClassTaxonomyTests.cs",
                "HybridCPU_ISE.Tests/tests/Phase09CanonicalDecodePublicationContractTests.cs"
            ],
            FindFilesContaining(root, "HybridCPU_ISE.Tests",
                nameof(CustomAcceleratorMicroOp)));

        string production = ReadSourceTreesExcept(
            root,
            [
                Path.Combine("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                    "MicroOps", "Types", "MicroOp.Misc.cs")
            ],
            "HybridCPU_ISE");
        Assert.DoesNotContain("new CustomAcceleratorMicroOp", production,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CustomAcceleratorMicroOp.InitializeMetadata", production,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublicShapeRawFoldsAndMutationOrderRemainFrozen()
    {
        Type type = typeof(CustomAcceleratorMicroOp);
        Assert.False(type.IsSealed);
        ConstructorInfo constructor = Assert.Single(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance |
            BindingFlags.DeclaredOnly));
        Assert.Empty(constructor.GetParameters());

        MethodInfo initialize = type.GetMethod(
            nameof(CustomAcceleratorMicroOp.InitializeMetadata)) ??
            throw new MissingMethodException();
        Assert.True(initialize.IsPublic);
        Assert.Equal(typeof(void), initialize.ReturnType);
        Assert.Equal(
            [typeof(int), typeof(int[]), typeof(int)],
            initialize.GetParameters().Select(parameter =>
                parameter.ParameterType).ToArray());

        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.Misc.cs");
        string carrier = ExtractBalanced(source,
            "public class CustomAcceleratorMicroOp");
        string body = ExtractBalanced(carrier,
            "public void InitializeMetadata(int acceleratorId, int[] inputRegIds, int outputRegId)");

        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForAccelerator("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForRegisterRead("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForRegisterWrite("));
        Assert.Equal(2, Count(body, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(body,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(body,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.DoesNotContain("RefreshWriteMetadata", carrier,
            StringComparison.Ordinal);

        AssertOrdered(body,
            "ReadRegisters = inputRegIds;",
            "if (WritesRegister)",
            "WriteRegisters = new[] { outputRegId };",
            "ResourceMask = ResourceBitset.Zero;",
            "ResourceMaskBuilder.ForAccelerator(acceleratorId)",
            "foreach (int regId in inputRegIds)",
            "ResourceMaskBuilder.ForRegisterRead(regId)",
            "if (WritesRegister)",
            "ResourceMaskBuilder.ForRegisterWrite(outputRegId)",
            "PublishExplicitStructuralSafetyMask();",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void ValidAndRawRegistersPreserveListsMasksAliasesAndStatefulWriteSeam()
    {
        int[] inputs = [0, 1, 1, 31, 32, 255, -1, int.MinValue, int.MaxValue];
        var carrier = new CustomAcceleratorMicroOp
        {
            WritesRegister = true,
            DestRegID = 29
        };

        carrier.InitializeMetadata(0, inputs, 0);

        Assert.Same(inputs, carrier.ReadRegisters);
        Assert.Equal([0], carrier.WriteRegisters);
        ResourceBitset expected = ResourceMaskBuilder.ForAccelerator(0);
        foreach (int input in inputs)
            expected |= ResourceMaskBuilder.ForRegisterRead(input);
        expected |= ResourceMaskBuilder.ForRegisterWrite(0);
        Assert.Equal(expected, carrier.ResourceMask);
        Assert.Equal(29, carrier.DestRegID);

        MicroOpAdmissionMetadata cached = carrier.AdmissionMetadata;
        uint cachedHazardMask = cached.RegisterHazardMask;
        inputs[0] = 12;
        Assert.Equal(12, cached.ReadRegisters[0]);
        Assert.Equal(cachedHazardMask, carrier.AdmissionMetadata.RegisterHazardMask);
        Assert.Equal(expected, carrier.ResourceMask);

        carrier.WritesRegister = false;
        carrier.InitializeMetadata(3, [2], 99);
        Assert.Equal([2], carrier.ReadRegisters);
        Assert.Equal([0], carrier.WriteRegisters);
        Assert.False(carrier.AdmissionMetadata.WritesRegister);
        Assert.Equal(
            ResourceMaskBuilder.ForAccelerator(3) |
            ResourceMaskBuilder.ForRegisterRead(2),
            carrier.ResourceMask);
    }

    [Fact]
    public void InvalidAcceleratorAndNullArrayPreserveWinnerAndPartialMutation()
    {
        var invalidAccelerator = new CustomAcceleratorMicroOp
        {
            WritesRegister = true
        };
        int[] invalidReads = [7];
        ArgumentOutOfRangeException low = Assert.Throws<ArgumentOutOfRangeException>(
            () => invalidAccelerator.InitializeMetadata(-1, invalidReads, 8));
        Assert.Equal("accelId", low.ParamName);
        Assert.Same(invalidReads, invalidAccelerator.ReadRegisters);
        Assert.Equal([8], invalidAccelerator.WriteRegisters);
        Assert.Equal(ResourceBitset.Zero, invalidAccelerator.ResourceMask);

        ArgumentOutOfRangeException high = Assert.Throws<ArgumentOutOfRangeException>(
            () => invalidAccelerator.InitializeMetadata(4, [9], 10));
        Assert.Equal("accelId", high.ParamName);
        Assert.Equal([9], invalidAccelerator.ReadRegisters);
        Assert.Equal([10], invalidAccelerator.WriteRegisters);
        Assert.Equal(ResourceBitset.Zero, invalidAccelerator.ResourceMask);

        var nullReads = new CustomAcceleratorMicroOp
        {
            WritesRegister = true
        };
        Assert.Throws<NullReferenceException>(() =>
            nullReads.InitializeMetadata(1, null!, 11));
        Assert.Null(nullReads.ReadRegisters);
        Assert.Equal([11], nullReads.WriteRegisters);
        Assert.Equal(ResourceMaskBuilder.ForAccelerator(1),
            nullReads.ResourceMask);
    }

    [Fact]
    public void FailClosedPlacementReflectionAndGenericWriteBackSeamsStaySeparate()
    {
        var carrier = new CustomAcceleratorMicroOp
        {
            OpCode = 0xFFFF,
            WritesRegister = true,
            DestRegID = 7,
            OwnerThreadId = 0
        };
        Assert.Equal(SlotClass.Unclassified,
            carrier.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned,
            carrier.Placement.PinningKind);
        Assert.Equal(0, carrier.Placement.PinnedLaneId);

        YAKSys_Hybrid_CPU.Processor.CPU_Core core = new(0);
        Assert.Throws<InvalidOpcodeException>(() => carrier.Execute(ref core));

        carrier.CapturePrimaryWriteBackResult(0xA5UL);
        FieldInfo results = typeof(CustomAcceleratorMicroOp).GetField(
            "_results", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingFieldException();
        Assert.Equal([0xA5UL], Assert.IsType<ulong[]>(results.GetValue(carrier)));

        Span<RetireRecord> records = stackalloc RetireRecord[1];
        int count = 0;
        carrier.EmitWriteBackRetireRecords(ref core, records, ref count);
        Assert.Equal(1, count);
        Assert.Equal(RetireRecordKind.RegisterWrite, records[0].Kind);
        Assert.Equal(7, records[0].ArchReg);
        Assert.Equal(0xA5UL, records[0].Value);

        string root = FindRepositoryRoot();
        string runtime = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Runtime.cs");
        string accelerators = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Diagnostics", "InstructionRegistry.Accelerators.cs");
        Assert.Contains("if (IsCustomAcceleratorOpcode(opCode))", runtime,
            StringComparison.Ordinal);
        Assert.Contains("throw CreateUnsupportedCustomAcceleratorException(opCode)",
            runtime, StringComparison.Ordinal);
        Assert.Contains("Direct/manual publication must fail closed",
            accelerators, StringComparison.Ordinal);

        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        Assert.DoesNotContain(nameof(CustomAcceleratorMicroOp), testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId",
            ExtractBalanced(Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
                "Pipeline", "MicroOps", "Types", "MicroOp.Misc.cs"),
                "public class CustomAcceleratorMicroOp"),
            StringComparison.Ordinal);
    }

    private static string[] FindFilesContaining(
        string root,
        string relativeRoot,
        string token)
    {
        string directory = Path.Combine(root, relativeRoot);
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path =>
                Path.GetFileName(path) !=
                    "Rf123acCustomAcceleratorRegisterMaskInventoryTests.cs" &&
                Path.GetFileName(path) !=
                    "Rf123adCustomAcceleratorRegisterMaskValidInputCutoverTests.cs")
            .Where(path => Regex.IsMatch(File.ReadAllText(path),
                $@"\b{Regex.Escape(token)}\b",
                RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(root, path)
                .Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ReadSourceTreesExcept(
        string root,
        IReadOnlyList<string> excludedRelativePaths,
        params string[] sourceTrees)
    {
        var excluded = excludedRelativePaths
            .Select(path => Path.GetFullPath(Path.Combine(root, path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return string.Join("\n", sourceTrees.SelectMany(tree =>
            Directory.EnumerateFiles(Path.Combine(root, tree), "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Where(path => !excluded.Contains(Path.GetFullPath(path)))
                .Select(File.ReadAllText)));
    }

    private static string ExtractBalanced(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{signature}' was not found.");
        int brace = source.IndexOf('{', start);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }
        throw new InvalidOperationException($"'{signature}' was not closed.");
    }

    private static void AssertOrdered(string source, params string[] markers)
    {
        int previous = -1;
        foreach (string marker in markers)
        {
            int index = source.IndexOf(marker, previous + 1,
                StringComparison.Ordinal);
            Assert.True(index > previous,
                $"Marker '{marker}' was missing or out of order.");
            previous = index;
        }
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains(
            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset,
            StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException(
            "HybridCPU repository root was not found.");
    }
}
