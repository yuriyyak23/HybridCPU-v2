using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123lBranchMicroOpRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void InitializeMetadataUsesFourIndependentCheckedPathsWithExactRawFallbacks()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MicroOps",
            "Control",
            "MicroOp.Control.cs"));
        string method = ExtractMethod(source, "public void InitializeMetadata()");

        Assert.Equal(4, Count(method, "ArchRegId.TryCreate("));
        Assert.Equal(3, Count(method,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(method,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.Equal(2, Count(method,
            "ResourceMaskBuilder.ForRegisterRead(Reg1ID)"));
        Assert.Equal(1, Count(method,
            "ResourceMaskBuilder.ForRegisterRead(Reg2ID)"));
        Assert.Equal(1, Count(method,
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)"));
        Assert.DoesNotContain("ArchRegId.Create(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", method, StringComparison.Ordinal);
        Assert.DoesNotContain("%", method, StringComparison.Ordinal);

        Assert.Equal(1, Count(method,
            "ReadRegisters = new[] { (int)Reg1ID, (int)Reg2ID };"));
        Assert.Equal(1, Count(method,
            "ReadRegisters = new[] { (int)Reg1ID };"));
        Assert.Equal(1, Count(method, "? new[] { (int)DestRegID }"));
        Assert.Equal(1, Count(method, "DestRegID != 0"));
        Assert.Equal(2, Count(method,
            "Reg1ID != VLIW_Instruction.NoReg"));
        Assert.Equal(1, Count(method,
            "DestRegID != VLIW_Instruction.NoReg"));
        Assert.Contains("PublishExplicitStructuralSafetyMask();", method,
            StringComparison.Ordinal);
        Assert.Contains("RefreshAdmissionMetadata(this);", method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EveryRawUshortRoleRetainsItsPreCutoverListsFiltersAndResourceMask()
    {
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;

            BranchMicroOp conditionalFirst = Create(
                InstructionsEnum.BEQ,
                isConditional: true,
                destination: VLIW_Instruction.NoReg,
                source1: value,
                source2: 0);
            Assert.Equal(new[] { (int)value, 0 }, conditionalFirst.ReadRegisters);
            Assert.Empty(conditionalFirst.WriteRegisters);
            Assert.Equal(ExpectedRead(value) | ExpectedRead(0),
                conditionalFirst.ResourceMask);

            BranchMicroOp conditionalSecond = Create(
                InstructionsEnum.BEQ,
                isConditional: true,
                destination: VLIW_Instruction.NoReg,
                source1: 0,
                source2: value);
            Assert.Equal(new[] { 0, (int)value }, conditionalSecond.ReadRegisters);
            Assert.Empty(conditionalSecond.WriteRegisters);
            Assert.Equal(ExpectedRead(0) | ExpectedRead(value),
                conditionalSecond.ResourceMask);

            BranchMicroOp jalrBase = Create(
                InstructionsEnum.JALR,
                isConditional: false,
                destination: 0,
                source1: value,
                source2: 123);
            Assert.Empty(jalrBase.WriteRegisters);
            if (value == VLIW_Instruction.NoReg)
            {
                Assert.Empty(jalrBase.ReadRegisters);
                Assert.Equal(ResourceBitset.Zero, jalrBase.ResourceMask);
            }
            else
            {
                Assert.Equal(new[] { (int)value }, jalrBase.ReadRegisters);
                Assert.Equal(ExpectedRead(value), jalrBase.ResourceMask);
            }

            BranchMicroOp link = Create(
                InstructionsEnum.JAL,
                isConditional: false,
                destination: value,
                source1: VLIW_Instruction.NoReg,
                source2: VLIW_Instruction.NoReg);
            bool publishesLink =
                value != 0 && value != VLIW_Instruction.NoReg;
            Assert.Equal(publishesLink, link.WritesRegister);
            Assert.Empty(link.ReadRegisters);
            if (publishesLink)
            {
                Assert.Equal(new[] { (int)value }, link.WriteRegisters);
                Assert.Equal(ExpectedWrite(value), link.ResourceMask);
            }
            else
            {
                Assert.Empty(link.WriteRegisters);
                Assert.Equal(ResourceBitset.Zero, link.ResourceMask);
            }
        }
    }

    [Fact]
    public void CutoverAddsNoSignatureStorageCompilerWireOrTestSupportSurface()
    {
        ConstructorInfo constructor = Assert.Single(typeof(BranchMicroOp)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.Empty(constructor.GetParameters());
        AssertMutableUshortProperty(nameof(BranchMicroOp.Reg1ID));
        AssertMutableUshortProperty(nameof(BranchMicroOp.Reg2ID));
        AssertMutableUshortProperty(nameof(BranchMicroOp.DestRegID));

        MethodInfo initialize = Assert.Single(typeof(BranchMicroOp)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.DeclaredOnly)
            .Where(method => method.Name == nameof(BranchMicroOp.InitializeMetadata)));
        Assert.Empty(initialize.GetParameters());
        Assert.Equal(typeof(void), initialize.ReturnType);

        string root = FindRepositoryRoot();
        string compiler = ReadTree(root, "HybridCPU_Compiler");
        string assembler = ReadTree(root, "TestAssemblerConsoleApps");
        string testSupport = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.TestSupport.cs"));
        Assert.DoesNotContain("BranchMicroOp", compiler, StringComparison.Ordinal);
        Assert.DoesNotContain("BranchMicroOp", assembler, StringComparison.Ordinal);
        Assert.DoesNotContain("BranchMicroOp", testSupport, StringComparison.Ordinal);
    }

    private static BranchMicroOp Create(
        InstructionsEnum opcode,
        bool isConditional,
        ushort destination,
        ushort source1,
        ushort source2)
    {
        var operation = new BranchMicroOp
        {
            OpCode = (uint)opcode,
            IsConditional = isConditional,
            DestRegID = destination,
            Reg1ID = source1,
            Reg2ID = source2,
        };
        operation.InitializeMetadata();
        return operation;
    }

    private static ResourceBitset ExpectedRead(ushort value)
    {
        int group = Math.Min(value / 4, 15);
        return new ResourceBitset(1UL << group, 0);
    }

    private static ResourceBitset ExpectedWrite(ushort value)
    {
        int group = Math.Min(value / 4, 15);
        return new ResourceBitset(1UL << (16 + group), 0);
    }

    private static void AssertMutableUshortProperty(string propertyName)
    {
        PropertyInfo property = typeof(BranchMicroOp).GetProperty(propertyName)
            ?? throw new MissingMemberException(nameof(BranchMicroOp), propertyName);
        Assert.Equal(typeof(ushort), property.PropertyType);
        Assert.True(property.GetMethod?.IsPublic);
        Assert.True(property.SetMethod?.IsPublic);
    }

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method signature '{signature}' was not found.");
        int brace = source.IndexOf('{', start);
        Assert.True(brace >= 0);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }

        throw new InvalidOperationException($"Method '{signature}' has no closing brace.");
    }

    private static string ReadTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
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

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
