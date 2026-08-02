using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123aeMoveMicroOpRegisterMaskInventoryTests
{
    [Fact]
    public void PaperDefinesMoveRolesSentinelsFailureWinnersAndOnlyLaterCutover()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("#### 3.7.14 Retained MoveMicroOp architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("DT=0 appends `reg1Id` as the sole read role and `reg2Id` as the sole write",
            paper, StringComparison.Ordinal);
        Assert.Contains("`NoReg=65535` alone is metadata absence", paper,
            StringComparison.Ordinal);
        Assert.Contains("including the\nunpacked value 255", paper,
            StringComparison.Ordinal);
        Assert.Contains("prior successful metadata remains intact", paper,
            StringComparison.Ordinal);
        Assert.Contains("existing invalid-to-zero source\naliases", paper,
            StringComparison.Ordinal);
        Assert.Contains("one read-loop\nfold and the one write-loop fold", paper,
            StringComparison.Ordinal);
        Assert.Contains("every other participating `ushort` must retain the exact raw helper",
            paper, StringComparison.Ordinal);
        Assert.Contains("No bank resolver or unresolved-bank\nfallback", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedWorldCarrierAndConstructorManifestRemainsExactAndDormant()
    {
        string root = FindRepositoryRoot();

        Assert.Equal(
        [
            "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Helpers.Vector.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.Misc.cs"
        ], FindFilesContaining(root, "HybridCPU_ISE", "MoveMicroOp"));
        Assert.Empty(FindFilesContaining(root, "HybridCPU_Compiler", "MoveMicroOp"));
        Assert.Empty(FindFilesContaining(root, "TestAssemblerConsoleApps", "MoveMicroOp"));
        Assert.Equal(
        [
            "HybridCPU_ISE.Tests/Architecture/Rf084agMiscRegisterWriteReachabilityAuditTests.cs",
            "HybridCPU_ISE.Tests/Architecture/Rf084bbConsolidatedExitEvidenceTests.cs",
            "HybridCPU_ISE.Tests/ArchitectureAndExecution/PipelineHiddenWriteHazardTests.cs",
            "HybridCPU_ISE.Tests/PhasingAndExtensions/SlotClassTaxonomyTests.cs",
            "HybridCPU_ISE.Tests/tests/Phase02InstructionIrTests.cs",
            "HybridCPU_ISE.Tests/tests/Phase09CanonicalDecodePublicationContractTests.cs"
        ], FindFilesContaining(root, "HybridCPU_ISE.Tests", "MoveMicroOp"));

        string registry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Vector.cs");
        Assert.Equal(2, Count(registry, "new MoveMicroOp"));
        Assert.Equal(1, Count(registry, "RegisterRetainedMoveOp("));
        Assert.Equal(1, Count(registry, "RegisterRetainedMoveNumOp("));
        Assert.Contains("private static void RegisterRetainedMoveOp", registry,
            StringComparison.Ordinal);
        Assert.Contains("private static void RegisterRetainedMoveNumOp", registry,
            StringComparison.Ordinal);

        string otherProduction = ReadSourceTreesExcept(root,
        [
            "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Helpers.Vector.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.Misc.cs"
        ], "HybridCPU_ISE");
        Assert.DoesNotContain("new MoveMicroOp", otherProduction,
            StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterRetainedMoveOp(", otherProduction,
            StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterRetainedMoveNumOp(", otherProduction,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeclarationStorageAndTwoFoldBoundaryRemainFrozen()
    {
        Type type = typeof(MoveMicroOp);
        ConstructorInfo constructor = Assert.Single(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(constructor.GetParameters());

        PropertyInfo instruction = type.GetProperty(nameof(MoveMicroOp.Instruction))!;
        Assert.Equal(typeof(VLIW_Instruction), instruction.PropertyType);
        Assert.True(instruction.CanRead);
        Assert.True(instruction.CanWrite);

        MethodInfo initialize = type.GetMethod("InitializeMetadata",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.True(initialize.IsPrivate);
        Assert.Empty(initialize.GetParameters());
        Assert.Equal(typeof(void), initialize.ReturnType);

        MethodInfo projection = type.GetMethod(
            "ApplyCanonicalRuntimeMoveShapeProjection",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.True(projection.IsAssembly);
        Assert.Equal(
            [typeof(byte), typeof(ushort), typeof(ushort), typeof(ulong)],
            projection.GetParameters().Select(parameter =>
                parameter.ParameterType).ToArray());

        Assert.Equal(typeof(byte), PrivateField(type, "_projectedDataType").FieldType);
        Assert.Equal(typeof(ushort), PrivateField(type, "_projectedReg1Id").FieldType);
        Assert.Equal(typeof(ushort), PrivateField(type, "_projectedReg2Id").FieldType);
        Assert.Equal(typeof(bool), PrivateField(type, "_hasProjectedMoveShape").FieldType);

        string carrier = ExtractBalanced(Read(FindRepositoryRoot(), "HybridCPU_ISE",
                "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
                "MicroOp.Misc.cs"),
            "public class MoveMicroOp");
        string body = ExtractBalanced(carrier, "private void InitializeMetadata()");

        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForRegisterRead("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForRegisterWrite("));
        Assert.Equal(2, Count(body, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(body,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(body,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForStore("));
        Assert.Equal(1, Count(body, "ResourceMaskBuilder.ForLoad("));

        AssertOrdered(body,
            "byte dataType = ResolveMoveDataType();",
            "ushort reg1Id = ResolveMoveReg1Id();",
            "ushort reg2Id = ResolveMoveReg2Id();",
            "switch (dataType)",
            "case 0:",
            "readRegs.Add(reg1Id)",
            "writeRegs.Add(reg2Id)",
            "case 1:",
            "case 3:",
            "writeRegs.Add(reg1Id)",
            "case 2:",
            "readRegs.Add(reg1Id)",
            "case 4:",
            "throw CreateUnsupportedRetainedRegisterContourException(dataType)",
            "ReadRegisters = readRegs;",
            "WriteRegisters =",
            "ResourceMask = ResourceBitset.Zero;",
            "ArchRegId.TryCreate(readRegs[i]",
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(readRegister)",
            "ResourceMaskBuilder.ForRegisterRead(readRegs[i])",
            "ArchRegId.TryCreate(writeRegs[i]",
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite(writeRegister)",
            "ResourceMaskBuilder.ForRegisterWrite(writeRegs[i])",
            "ResourceMaskBuilder.ForStore()",
            "ResourceMaskBuilder.ForLoad()");
    }

    [Fact]
    public void DtZeroThroughThreePublishExactListsMasksAndClassFlexiblePlacement()
    {
        AssertMetadata(
            Create(dataType: 0, reg1Id: 0, reg2Id: 31),
            [0], [31],
            ResourceMaskBuilder.ForRegisterRead(0) |
            ResourceMaskBuilder.ForRegisterWrite(31),
            MicroOpClass.Alu, SlotClass.AluClass,
            writesRegister: true, hasSideEffects: false);

        AssertMetadata(
            Create(dataType: 1, reg1Id: 32, reg2Id: 7),
            [], [32],
            ResourceMaskBuilder.ForRegisterWrite(32),
            MicroOpClass.Alu, SlotClass.AluClass,
            writesRegister: true, hasSideEffects: false);

        AssertMetadata(
            Create(dataType: 2, reg1Id: 65534, reg2Id: 9),
            [65534], [],
            ResourceMaskBuilder.ForRegisterRead(65534) |
            ResourceMaskBuilder.ForStore(),
            MicroOpClass.Lsu, SlotClass.LsuClass,
            writesRegister: false, hasSideEffects: true);

        AssertMetadata(
            Create(dataType: 3, reg1Id: 31, reg2Id: 8),
            [], [31],
            ResourceMaskBuilder.ForRegisterWrite(31) |
            ResourceMaskBuilder.ForLoad(),
            MicroOpClass.Lsu, SlotClass.LsuClass,
            writesRegister: true, hasSideEffects: false);
    }

    [Fact]
    public void NoRegPackedNoArchRegAndRaw255KeepDistinctAbsenceAndWireForms()
    {
        MoveMicroOp absent = Create(0, VLIW_Instruction.NoReg,
            VLIW_Instruction.NoReg);
        absent.RefreshWriteMetadata();
        Assert.Empty(absent.ReadRegisters);
        Assert.Empty(absent.WriteRegisters);
        Assert.Equal(ResourceBitset.Zero, absent.ResourceMask);

        MoveMicroOp storeWithoutRegister = Create(2, VLIW_Instruction.NoReg, 4);
        storeWithoutRegister.RefreshWriteMetadata();
        Assert.Empty(storeWithoutRegister.ReadRegisters);
        Assert.Equal(ResourceMaskBuilder.ForStore(),
            storeWithoutRegister.ResourceMask);

        ulong packed = VLIW_Instruction.PackArchRegs(
            VLIW_Instruction.NoArchReg,
            VLIW_Instruction.NoArchReg,
            VLIW_Instruction.NoArchReg);
        var packedInstruction = new VLIW_Instruction { Word1 = packed };
        Assert.Equal(VLIW_Instruction.NoReg, packedInstruction.Reg1ID);
        Assert.Equal(VLIW_Instruction.NoReg, packedInstruction.Reg2ID);

        MoveMicroOp raw255 = Create(0, 255, 255);
        raw255.RefreshWriteMetadata();
        Assert.Equal([255], raw255.ReadRegisters);
        Assert.Equal([255], raw255.WriteRegisters);
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterRead(255) |
            ResourceMaskBuilder.ForRegisterWrite(255),
            raw255.ResourceMask);
    }

    [Fact]
    public void NullInstructionUnsupportedTypesAndRetiredMemoryExecutionKeepWinners()
    {
        var fresh = new MoveMicroOp();
        fresh.RefreshWriteMetadata();
        Assert.Equal([0], fresh.ReadRegisters);
        Assert.Equal([0], fresh.WriteRegisters);
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterRead(0) |
            ResourceMaskBuilder.ForRegisterWrite(0),
            fresh.ResourceMask);
        Assert.True(fresh.WritesRegister);

        MoveMicroOp carrier = Create(0, 1, 2);
        carrier.RefreshWriteMetadata();
        IReadOnlyList<int> reads = carrier.ReadRegisters;
        IReadOnlyList<int> writes = carrier.WriteRegisters;
        ResourceBitset mask = carrier.ResourceMask;
        SlotPlacementMetadata placement = carrier.Placement;

        VLIW_Instruction instruction = carrier.Instruction;
        instruction.DataType = 4;
        carrier.Instruction = instruction;
        Assert.Throws<InvalidOperationException>(() => carrier.RefreshWriteMetadata());
        Assert.Same(reads, carrier.ReadRegisters);
        Assert.Same(writes, carrier.WriteRegisters);
        Assert.Equal(mask, carrier.ResourceMask);
        Assert.Equal(placement, carrier.Placement);

        instruction = carrier.Instruction;
        instruction.DataType = 6;
        carrier.Instruction = instruction;
        Assert.Throws<InvalidOperationException>(() => carrier.RefreshWriteMetadata());
        Assert.Same(reads, carrier.ReadRegisters);
        Assert.Same(writes, carrier.WriteRegisters);
        Assert.Equal(mask, carrier.ResourceMask);

        var core = new Processor.CPU_Core(0);
        MoveMicroOp store = Create(2, 1, 2);
        store.RefreshWriteMetadata();
        Assert.Throws<InvalidOperationException>(() => store.Execute(ref core));
        MoveMicroOp load = Create(3, 1, 2);
        load.RefreshWriteMetadata();
        Assert.Throws<InvalidOperationException>(() => load.Execute(ref core));
    }

    [Fact]
    public void ReflectionMutationListAliasingAndAuthorityConsumersRemainExplicit()
    {
        var reflected = new MoveMicroOp();
        PrivateField(typeof(MoveMicroOp), "_projectedDataType")
            .SetValue(reflected, (byte)0);
        PrivateField(typeof(MoveMicroOp), "_projectedReg1Id")
            .SetValue(reflected, (ushort)32);
        PrivateField(typeof(MoveMicroOp), "_projectedReg2Id")
            .SetValue(reflected, (ushort)31);
        PrivateField(typeof(MoveMicroOp), "_hasProjectedMoveShape")
            .SetValue(reflected, true);
        reflected.RefreshWriteMetadata();
        Assert.Equal([32], reflected.ReadRegisters);
        Assert.Equal([31], reflected.WriteRegisters);

        ResourceBitset frozenMask = reflected.ResourceMask;
        reflected.RefreshAdmissionMetadata();
        uint frozenHazardMask = reflected.AdmissionMetadata.RegisterHazardMask;
        List<int> mutableReads = Assert.IsType<List<int>>(reflected.ReadRegisters);
        int[] mutableWrites = Assert.IsType<int[]>(reflected.WriteRegisters);
        mutableReads[0] = -1;
        mutableWrites[0] = 65534;
        Assert.Equal(-1, reflected.ReadRegisters[0]);
        Assert.Equal(65534, reflected.WriteRegisters[0]);
        Assert.Equal(frozenMask, reflected.ResourceMask);
        Assert.Equal(frozenHazardMask,
            reflected.AdmissionMetadata.RegisterHazardMask);

        MoveMicroOp invalidSource = Create(0, 32, 1);
        invalidSource.RefreshWriteMetadata();
        var core = new Processor.CPU_Core(0);
        Assert.True(invalidSource.Execute(ref core));
        Assert.True(invalidSource.TryGetPrimaryWriteBackResult(out ulong value));
        Assert.Equal(0UL, value);

        string root = FindRepositoryRoot();
        string state = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "State", "Architectural",
            "CPU_Core.StateData.cs");
        Assert.Contains("if ((uint)archReg >= (uint)RenameMap.ArchRegs) return 0",
            state, StringComparison.Ordinal);
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "Registers", "Retire", "RetireCoordinator.cs");
        Assert.Contains("(uint)record.ArchReg >= (uint)RenameMap.ArchRegs",
            retire, StringComparison.Ordinal);

        string carrier = ExtractBalanced(Read(root, "HybridCPU_ISE",
                "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
                "MicroOp.Misc.cs"),
            "public class MoveMicroOp");
        Assert.Contains("Math.Clamp(OwnerThreadId, 0, Processor.CPU_Core.SmtWays - 1)",
            carrier, StringComparison.Ordinal);
        Assert.Contains("GetDescription()", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceleratorTokenHandle", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Dictionary<", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("%", ExtractBalanced(carrier,
            "private void InitializeMetadata()"), StringComparison.Ordinal);

        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        Assert.DoesNotContain("MoveMicroOp", testSupport,
            StringComparison.Ordinal);
    }

    private static MoveMicroOp Create(byte dataType, ushort reg1Id, ushort reg2Id)
    {
        return new MoveMicroOp
        {
            Instruction = new VLIW_Instruction
            {
                DataType = dataType,
                Word1 = (ulong)reg1Id | ((ulong)reg2Id << 16),
                Src2Pointer = 0x1234
            }
        };
    }

    private static void AssertMetadata(
        MoveMicroOp carrier,
        int[] expectedReads,
        int[] expectedWrites,
        ResourceBitset expectedMask,
        MicroOpClass expectedClass,
        SlotClass expectedSlotClass,
        bool writesRegister,
        bool hasSideEffects)
    {
        carrier.RefreshWriteMetadata();
        Assert.Equal(expectedReads, carrier.ReadRegisters);
        Assert.Equal(expectedWrites, carrier.WriteRegisters);
        Assert.Equal(expectedMask, carrier.ResourceMask);
        Assert.Equal(expectedClass, carrier.Class);
        Assert.Equal(expectedSlotClass, carrier.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible,
            carrier.Placement.PinningKind);
        Assert.Equal(0, carrier.Placement.PinnedLaneId);
        Assert.Equal(writesRegister, carrier.WritesRegister);
        Assert.Equal(hasSideEffects, carrier.HasSideEffects);
    }

    private static FieldInfo PrivateField(Type type, string name) =>
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingFieldException(type.FullName, name);

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
                    "Rf123aeMoveMicroOpRegisterMaskInventoryTests.cs" &&
                Path.GetFileName(path) !=
                    "Rf123afMoveMicroOpRegisterMaskValidInputCutoverTests.cs")
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
