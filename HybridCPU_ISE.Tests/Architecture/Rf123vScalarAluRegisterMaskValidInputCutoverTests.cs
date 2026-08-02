using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123vScalarAluRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void ScalarAluUsesThreeIndependentCheckedPathsWithExactRawFallbacks()
    {
        string initialize = ReadInitializeMetadata();

        Assert.Equal(3, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(initialize,
            "ArchRegId.TryCreate(Src1RegID, out ArchRegId source1Register)"));
        Assert.Equal(1, Count(initialize,
            "ForArchitecturalRegisterRead(source1Register)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(Src1RegID)"));
        Assert.Equal(1, Count(initialize,
            "ArchRegId.TryCreate(Src2RegID, out ArchRegId source2Register)"));
        Assert.Equal(1, Count(initialize,
            "ForArchitecturalRegisterRead(source2Register)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(Src2RegID)"));
        Assert.Equal(1, Count(initialize,
            "ArchRegId.TryCreate(DestRegID, out ArchRegId destinationRegister)"));
        Assert.Equal(1, Count(initialize,
            "ForArchitecturalRegisterWrite(destinationRegister)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)"));
        Assert.DoesNotContain("ArchRegId.Create(", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("%", initialize, StringComparison.Ordinal);

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
    public void EveryRepresentableRegisterPreservesCheckedHelperAndRoleParity()
    {
        for (int raw = ArchRegId.MinValue; raw <= ArchRegId.MaxValue; raw++)
        {
            Assert.True(ArchRegId.TryCreate(raw, out ArchRegId checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterRead(checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterWrite(checkedId));

            ushort value = (ushort)raw;
            Assert.Equal(Expected(value, VLIW_Instruction.NoReg,
                    VLIW_Instruction.NoReg, false, false),
                Create(value, VLIW_Instruction.NoReg,
                    VLIW_Instruction.NoReg, false, false).ResourceMask);
            Assert.Equal(Expected(VLIW_Instruction.NoReg, value,
                    VLIW_Instruction.NoReg, false, false),
                Create(VLIW_Instruction.NoReg, value,
                    VLIW_Instruction.NoReg, false, false).ResourceMask);
            Assert.Equal(Expected(VLIW_Instruction.NoReg,
                    VLIW_Instruction.NoReg, value, true, true),
                Create(VLIW_Instruction.NoReg, VLIW_Instruction.NoReg,
                    value, true, true).ResourceMask);
        }
    }

    [Fact]
    public void EveryUshortPreservesAllThreeIndependentRoleResults()
    {
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            bool present = value != VLIW_Instruction.NoReg;

            ScalarALUMicroOp source1 = Create(
                value, VLIW_Instruction.NoReg, VLIW_Instruction.NoReg,
                false, false);
            Assert.Equal(present ? 1 : 0, source1.ReadRegisters.Count);
            if (present) Assert.Equal(value, source1.ReadRegisters[0]);
            Assert.Equal(Expected(value, VLIW_Instruction.NoReg,
                VLIW_Instruction.NoReg, false, false), source1.ResourceMask);

            ScalarALUMicroOp source2 = Create(
                VLIW_Instruction.NoReg, value, VLIW_Instruction.NoReg,
                false, false);
            Assert.Equal(present ? 1 : 0, source2.ReadRegisters.Count);
            if (present) Assert.Equal(value, source2.ReadRegisters[0]);
            Assert.Equal(Expected(VLIW_Instruction.NoReg, value,
                VLIW_Instruction.NoReg, false, false), source2.ResourceMask);

            ScalarALUMicroOp destination = Create(
                VLIW_Instruction.NoReg, VLIW_Instruction.NoReg, value,
                true, true);
            Assert.Equal(present ? 1 : 0, destination.WriteRegisters.Count);
            if (present) Assert.Equal(value, destination.WriteRegisters[0]);
            Assert.Equal(Expected(VLIW_Instruction.NoReg,
                VLIW_Instruction.NoReg, value, true, true),
                destination.ResourceMask);
        }
    }

    [Fact]
    public void ImmediateSentinelDuplicateAndStaleWriteSeamsRemainFrozen()
    {
        foreach (ushort value in new ushort[] { 0, 31, 32, 255, 65534, 65535 })
        {
            ScalarALUMicroOp registerForm = Create(
                value, value, value, false, true);
            bool present = value != VLIW_Instruction.NoReg;
            Assert.Equal(present ? [(int)value, (int)value] : [],
                registerForm.ReadRegisters);
            Assert.Equal(present ? [(int)value] : [],
                registerForm.WriteRegisters);
            Assert.Equal(Expected(value, value, value, false, true),
                registerForm.ResourceMask);

            ScalarALUMicroOp immediateForm = Create(
                value, value, value, true, true);
            Assert.Equal(present ? [(int)value] : [],
                immediateForm.ReadRegisters);
            Assert.Equal(Expected(value, value, value, true, true),
                immediateForm.ResourceMask);
        }

        ScalarALUMicroOp stateful = Create(7, 7, 9, false, true);
        stateful.Src1RegID = VLIW_Instruction.NoReg;
        stateful.Src2RegID = VLIW_Instruction.NoReg;
        stateful.DestRegID = VLIW_Instruction.NoReg;
        stateful.WritesRegister = false;
        stateful.InitializeMetadata();
        Assert.Empty(stateful.ReadRegisters);
        Assert.Equal([9], stateful.WriteRegisters);
        Assert.Equal(ResourceBitset.Zero, stateful.ResourceMask);
    }

    [Fact]
    public void PublicFactoriesExecutionAndRetireBoundariesRemainUnchanged()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(ScalarALUMicroOp).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.DeclaredOnly));
        Assert.Empty(constructor.GetParameters());
        Assert.Equal(typeof(ushort),
            typeof(ScalarALUMicroOp).GetProperty(
                nameof(ScalarALUMicroOp.Src1RegID))!.PropertyType);
        Assert.Equal(typeof(ushort),
            typeof(ScalarALUMicroOp).GetProperty(
                nameof(ScalarALUMicroOp.Src2RegID))!.PropertyType);

        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Vector",
            "MicroOp.Compute.cs"));
        Assert.Contains(
            "ReadUnifiedScalarSourceOperand(ref core, vtId, Src1RegID)",
            source, StringComparison.Ordinal);
        Assert.Contains("? Immediate", source, StringComparison.Ordinal);
        Assert.Contains(
            ": ReadUnifiedScalarSourceOperand(ref core, vtId, Src2RegID)",
            source, StringComparison.Ordinal);
        Assert.Contains(
            "RetireRecord.RegisterWrite(vtId, DestRegID, _result)",
            source, StringComparison.Ordinal);

        ScalarALUMicroOp noRegDestination = Create(
            VLIW_Instruction.NoReg, VLIW_Instruction.NoReg,
            VLIW_Instruction.NoReg, true, true);
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = null!;
        Span<RetireRecord> records = stackalloc RetireRecord[1];
        int recordCount = 0;
        noRegDestination.EmitWriteBackRetireRecords(
            ref core, records, ref recordCount);
        Assert.Equal(1, recordCount);
        Assert.Equal(65535, records[0].ArchReg);

        foreach ((InstructionsEnum opcode, bool immediate) in new[]
                 {
                     (InstructionsEnum.ADD, false),
                     (InstructionsEnum.ADDI, true),
                 })
        {
            var context = new DecoderContext
            {
                OpCode = (uint)opcode,
                Reg1ID = 9,
                Reg2ID = 7,
                Reg3ID = 5,
                Immediate = 3,
                HasImmediate = immediate,
            };
            ScalarALUMicroOp operation = Assert.IsType<ScalarALUMicroOp>(
                InstructionRegistry.CreateMicroOp(context.OpCode, context));
            Assert.Equal((ushort)9, operation.DestRegID);
            Assert.Equal((ushort)7, operation.Src1RegID);
            Assert.Equal(immediate, operation.UsesImmediate);
        }
    }

    [Fact]
    public void Rf06FspCompilerReflectionAndTestSupportContoursStayIsolated()
    {
        string root = FindRepositoryRoot();
        string projection = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL", "Core",
            "Decoder", "Rf06ScalarLegacyProjection.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Fsp",
            "CPU_Core.PipelineExecution.Fsp.cs");
        string helper = Read(root, "HybridCPU_ISE.Tests", "TestHelpers",
            "MicroOpTestHelper.cs");
        string compiler = ReadTree(root, "HybridCPU_Compiler");

        Assert.Contains("ArchRegId.FromRawValue", projection,
            StringComparison.Ordinal);
        Assert.Contains("ScalarALUMicroOp carrier = new()", projection,
            StringComparison.Ordinal);
        Assert.Contains("candidate is not Core.ScalarALUMicroOp", fsp,
            StringComparison.Ordinal);
        Assert.Contains(
            "op.Src2RegID = (ushort)(immediate & 0xFFFF);", helper,
            StringComparison.Ordinal);
        Assert.DoesNotContain("new ScalarALUMicroOp", compiler,
            StringComparison.Ordinal);

        ScalarALUMicroOp operation = Create(7, 8, 9, false, true);
        PropertyInfo property = typeof(ScalarALUMicroOp).GetProperty(
            nameof(ScalarALUMicroOp.Src1RegID)) ??
            throw new MissingMemberException();
        property.SetValue(operation, (ushort)65534);
        operation.InitializeMetadata();
        Assert.Equal([65534, 8], operation.ReadRegisters);
        Assert.Equal(Expected(65534, 8, 9, false, true),
            operation.ResourceMask);
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

    private static string ReadInitializeMetadata()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Vector",
            "MicroOp.Compute.cs");
        string scalar = ExtractBalanced(source, "public class ScalarALUMicroOp");
        return ExtractBalanced(scalar, "public void InitializeMetadata()");
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

    private static void AssertOrdered(string text, params string[] values)
    {
        int previous = -1;
        foreach (string value in values)
        {
            int current = text.IndexOf(
                value, previous + 1, StringComparison.Ordinal);
            Assert.True(current > previous,
                $"'{value}' must occur after offset {previous}.");
            previous = current;
        }
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(
            value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static string ReadTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

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
