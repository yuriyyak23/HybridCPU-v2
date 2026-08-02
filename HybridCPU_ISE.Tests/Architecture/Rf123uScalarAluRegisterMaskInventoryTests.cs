using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123uScalarAluRegisterMaskInventoryTests
{
    [Fact]
    public void PaperDefinesThreeRolesAbsenceInvalidAliasesAndLaterCutoverOnly()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.9 Scalar-ALU architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("The three public mutable `ushort` roles",
            paper, StringComparison.Ordinal);
        Assert.Contains("`NoReg=65535` alone is metadata\nabsence",
            paper, StringComparison.Ordinal);
        Assert.Contains("existing invalid-to-zero\naliases",
            paper, StringComparison.Ordinal);
        Assert.Contains("without applying the metadata `NoReg` predicate",
            paper, StringComparison.Ordinal);
        Assert.Contains("A later valid-input-only cutover may branch independently at the three",
            paper, StringComparison.Ordinal);
        Assert.Contains("Invalid-input behavior, signature migration",
            paper, StringComparison.Ordinal);
    }


    [Fact]
    public void SourceShapeFreezesPredicatesOrderingRawFoldsAndStatefulWriteList()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Vector",
            "MicroOp.Compute.cs");
        string scalar = ExtractBalanced(source, "public class ScalarALUMicroOp");
        string initialize = ExtractBalanced(
            scalar, "public void InitializeMetadata()");

        Assert.Equal(2, Count(initialize, "Src1RegID != noReg"));
        Assert.Equal(2, Count(initialize,
            "!UsesImmediate && Src2RegID != noReg"));
        Assert.Equal(2, Count(initialize,
            "WritesRegister && DestRegID != noReg"));
        Assert.Contains("var readRegs = new List<int>();", initialize,
            StringComparison.Ordinal);
        Assert.Contains("WriteRegisters = new[] { (int)DestRegID };",
            initialize, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteRegisters = Array.Empty<int>()",
            initialize, StringComparison.Ordinal);
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(Src1RegID)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(Src2RegID)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)"));
        Assert.Equal(3, Count(initialize, "ArchRegId.TryCreate("));
        AssertOrdered(initialize,
            "readRegs.Add(Src1RegID)",
            "readRegs.Add(Src2RegID)",
            "ReadRegisters = readRegs;",
            "WriteRegisters = new[] { (int)DestRegID };",
            "ResourceMask = ResourceBitset.Zero;",
            "ArchRegId.TryCreate(Src1RegID, out ArchRegId source1Register)",
            "ResourceMaskBuilder.ForRegisterRead(Src1RegID)",
            "ArchRegId.TryCreate(Src2RegID, out ArchRegId source2Register)",
            "ResourceMaskBuilder.ForRegisterRead(Src2RegID)",
            "ArchRegId.TryCreate(DestRegID, out ArchRegId destinationRegister)",
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void EveryUshortPreservesIndependentSourceImmediateAndDestinationRoles()
    {
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            bool present = value != VLIW_Instruction.NoReg;

            ScalarALUMicroOp source1 = Create(
                value, VLIW_Instruction.NoReg, VLIW_Instruction.NoReg,
                usesImmediate: false, writesRegister: false);
            Assert.Equal(present ? 1 : 0, source1.ReadRegisters.Count);
            if (present) Assert.Equal(value, source1.ReadRegisters[0]);
            Assert.Equal(Expected(value, VLIW_Instruction.NoReg,
                VLIW_Instruction.NoReg, false, false), source1.ResourceMask);

            ScalarALUMicroOp source2 = Create(
                VLIW_Instruction.NoReg, value, VLIW_Instruction.NoReg,
                usesImmediate: false, writesRegister: false);
            Assert.Equal(present ? 1 : 0, source2.ReadRegisters.Count);
            if (present) Assert.Equal(value, source2.ReadRegisters[0]);

            ScalarALUMicroOp gatedSource2 = Create(
                VLIW_Instruction.NoReg, value, VLIW_Instruction.NoReg,
                usesImmediate: true, writesRegister: false);
            Assert.Empty(gatedSource2.ReadRegisters);
            Assert.Equal(ResourceBitset.Zero, gatedSource2.ResourceMask);

            ScalarALUMicroOp destination = Create(
                VLIW_Instruction.NoReg, VLIW_Instruction.NoReg, value,
                usesImmediate: true, writesRegister: true);
            Assert.Equal(present ? 1 : 0, destination.WriteRegisters.Count);
            if (present) Assert.Equal(value, destination.WriteRegisters[0]);
            Assert.Equal(Expected(VLIW_Instruction.NoReg,
                VLIW_Instruction.NoReg, value, true, true),
                destination.ResourceMask);
        }
    }

    [Fact]
    public void DuplicateReflectionListAndStaleWriteMutationSeamsRemainExplicit()
    {
        ScalarALUMicroOp operation = Create(7, 7, 9, false, true);
        Assert.Equal([7, 7], operation.ReadRegisters);
        Assert.Equal([9], operation.WriteRegisters);

        var reads = Assert.IsType<List<int>>(operation.ReadRegisters);
        reads[0] = 31;
        Assert.Equal(31, operation.AdmissionMetadata.ReadRegisters[0]);
        Assert.Equal(
            MicroOpAdmissionMetadata.BuildRegisterHazardMask([7, 7], [9]),
            operation.AdmissionMetadata.RegisterHazardMask);

        PropertyInfo source = typeof(ScalarALUMicroOp).GetProperty(
            nameof(ScalarALUMicroOp.Src1RegID)) ??
            throw new MissingMemberException();
        source.SetValue(operation, (ushort)65534);
        operation.Src2RegID = VLIW_Instruction.NoReg;
        operation.WritesRegister = false;
        operation.InitializeMetadata();
        Assert.Equal([65534], operation.ReadRegisters);
        Assert.Equal([9], operation.WriteRegisters);

        string helper = Read(FindRepositoryRoot(), "HybridCPU_ISE.Tests",
            "TestHelpers", "MicroOpTestHelper.cs");
        Assert.Contains("public static ScalarALUMicroOp CreateScalarALU(",
            helper, StringComparison.Ordinal);
        Assert.Contains("op.Src2RegID = (ushort)(immediate & 0xFFFF);",
            helper, StringComparison.Ordinal);
    }

    [Fact]
    public void FactoryExecutionRetireReplayAndWireAuthoritiesRemainSeparate()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Core.cs");
        string initialization = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Diagnostics", "InstructionRegistry.Initialize.Scalar.cs");
        string projection = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL", "Core",
            "Decoder", "Rf06ScalarLegacyProjection.cs");
        string dataflow = ReadTree(root, "HybridCPU_ISE", file =>
            file.EndsWith("CPU_Core.PipelineExecution.Dataflow.cs",
                StringComparison.Ordinal));
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "Registers", "Retire", "RetireCoordinator.cs");
        string compiler = ReadTree(root, "HybridCPU_Compiler", _ => true);

        Assert.Equal(13, Count(helpers, "new ScalarALUMicroOp"));
        Assert.Equal(9, Count(initialization, "new ScalarALUMicroOp"));
        Assert.Contains("ScalarALUMicroOp carrier = new()", projection,
            StringComparison.Ordinal);
        Assert.Contains("ArchRegId.FromRawValue", projection,
            StringComparison.Ordinal);
        Assert.Contains("regID == 0 || regID >=", dataflow,
            StringComparison.Ordinal);
        Assert.Contains("return 0;", dataflow, StringComparison.Ordinal);
        Assert.Contains("(uint)record.ArchReg >= (uint)RenameMap.ArchRegs",
            retire, StringComparison.Ordinal);
        Assert.Contains("if (record.ArchReg == 0)", retire,
            StringComparison.Ordinal);
        Assert.DoesNotContain("new ScalarALUMicroOp", compiler,
            StringComparison.Ordinal);

        ScalarALUMicroOp noRegDestination = Create(
            VLIW_Instruction.NoReg, VLIW_Instruction.NoReg,
            VLIW_Instruction.NoReg, true, true);
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = null!;
        Span<RetireRecord> records = stackalloc RetireRecord[1];
        int count = 0;
        noRegDestination.EmitWriteBackRetireRecords(
            ref core, records, ref count);
        Assert.Equal(1, count);
        Assert.Equal(65535, records[0].ArchReg);

        string production = ReadTree(root, "HybridCPU_ISE", _ => true);
        Assert.DoesNotContain("JsonSerializer.Deserialize<ScalarALUMicroOp>",
            production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize(scalarALUMicroOp",
            production, StringComparison.OrdinalIgnoreCase);
    }

    private static ScalarALUMicroOp Create(
        ushort source1, ushort source2, ushort destination,
        bool usesImmediate, bool writesRegister)
    {
        var operation = new ScalarALUMicroOp
        {
            Src1RegID = source1,
            Src2RegID = source2,
            DestRegID = destination,
            UsesImmediate = usesImmediate,
            WritesRegister = writesRegister,
            OwnerThreadId = 0,
        };
        operation.InitializeMetadata();
        return operation;
    }

    private static ResourceBitset Expected(
        ushort source1, ushort source2, ushort destination,
        bool usesImmediate, bool writesRegister)
    {
        ResourceBitset result = ResourceBitset.Zero;
        if (source1 != VLIW_Instruction.NoReg)
            result |= ResourceMaskBuilder.ForRegisterRead(source1);
        if (!usesImmediate && source2 != VLIW_Instruction.NoReg)
            result |= ResourceMaskBuilder.ForRegisterRead(source2);
        if (writesRegister && destination != VLIW_Instruction.NoReg)
            result |= ResourceMaskBuilder.ForRegisterWrite(destination);
        return result;
    }

    private static void AssertMutableUshort(string name)
    {
        PropertyInfo property = typeof(ScalarALUMicroOp).GetProperty(name) ??
            throw new MissingMemberException();
        Assert.Equal(typeof(ushort), property.PropertyType);
        Assert.True(property.GetMethod?.IsPublic);
        Assert.True(property.SetMethod?.IsPublic);
    }

    private static void AssertManifest(
        string root, string relativeRoot, int count, string sha256)
    {
        string[] paths = FindFilesContaining(root, relativeRoot);
        Assert.Equal(count, paths.Length);
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join("\n", paths))))
            .ToLowerInvariant());
    }

    private static string[] FindFilesContaining(
        string root, string relativeRoot)
    {
        string directory = Path.Combine(root, relativeRoot);
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => Path.GetFileName(path) is not
                "Rf123uScalarAluRegisterMaskInventoryTests.cs" and not
                "Rf123vScalarAluRegisterMaskValidInputCutoverTests.cs")
            .Where(path => Regex.IsMatch(File.ReadAllText(path),
                @"\bScalarALUMicroOp\b", RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
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
            int index = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previous,
                $"Marker '{marker}' was missing or out of order.");
            previous = index;
        }
    }

    private static string ReadTree(
        string root, string relativeRoot, Func<string, bool> predicate) =>
        string.Join("\n", Directory.Exists(Path.Combine(root, relativeRoot))
            ? Directory.EnumerateFiles(Path.Combine(root, relativeRoot),
                    "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path) && predicate(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText)
            : []);

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
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
