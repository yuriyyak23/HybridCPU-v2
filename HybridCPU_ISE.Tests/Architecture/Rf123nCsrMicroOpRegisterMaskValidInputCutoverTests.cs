using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123nCsrMicroOpRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void InitializeMetadataUsesTwoIndependentCheckedPathsWithExactRawFallbacks()
    {
        string root = FindRepositoryRoot();
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        string csr = Slice(
            control,
            "public abstract class CSRMicroOp : MicroOp",
            "/// <summary>\n    /// NOP (No Operation) micro-operation");
        string initialize = ExtractMethod(csr, "public void InitializeMetadata()");

        Assert.Equal(2, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(sourceRegisterId)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(SrcRegID)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite(destinationRegisterId)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)"));
        Assert.DoesNotContain("ArchRegId.Create(", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", initialize, StringComparison.Ordinal);
        Assert.DoesNotContain("%", initialize, StringComparison.Ordinal);

        int sourceFilter = initialize.IndexOf(
            "WritesFromSourceRegister && HasArchitecturalSourceRegister",
            StringComparison.Ordinal);
        int sourceChoice = initialize.IndexOf(
            "ArchRegId.TryCreate(\n                        SrcRegID",
            StringComparison.Ordinal);
        int destinationFilter = initialize.IndexOf(
            "HasArchitecturalDestinationRegister",
            StringComparison.Ordinal);
        int destinationChoice = initialize.IndexOf(
            "ArchRegId.TryCreate(\n                        DestRegID",
            StringComparison.Ordinal);
        Assert.True(sourceFilter >= 0 && sourceChoice > sourceFilter);
        Assert.True(destinationFilter >= 0 && destinationChoice > destinationFilter);

        Assert.Equal(1, Count(initialize, "ResourceMaskBuilder.ForAtomic()"));
        Assert.Contains("PublishExplicitStructuralSafetyMask();", initialize,
            StringComparison.Ordinal);
        Assert.Contains("RefreshAdmissionMetadata(this);", initialize,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EveryRawUshortRetainsFrozenSourceAndConfiguredDestinationBehavior()
    {
        ResourceBitset atomic = ResourceMaskBuilder.ForAtomic();

        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            bool participates =
                value != 0 &&
                value != VLIW_Instruction.NoArchReg &&
                value != VLIW_Instruction.NoReg;

            var source = new CsrReadWriteMicroOp
            {
                SrcRegID = value,
                DestRegID = VLIW_Instruction.NoReg,
            };
            source.InitializeMetadata();
            Assert.Equal(
                participates ? new[] { (int)value } : Array.Empty<int>(),
                source.ReadRegisters);
            Assert.Empty(source.WriteRegisters);
            Assert.Equal(
                participates ? atomic | ExpectedRead(value) : atomic,
                source.ResourceMask);
            AssertMetadataSnapshots(source);

            var destination = new CsrReadCounterMicroOp
            {
                DestRegID = value,
                WritesRegister = true,
            };
            destination.InitializeMetadata();
            Assert.Empty(destination.ReadRegisters);
            Assert.Equal(
                participates ? new[] { (int)value } : Array.Empty<int>(),
                destination.WriteRegisters);
            Assert.Equal(participates, destination.WritesRegister);
            Assert.Equal(
                participates ? atomic | ExpectedWrite(value) : atomic,
                destination.ResourceMask);
            AssertMetadataSnapshots(destination);

            bool representable = ArchRegId.TryCreate(value, out ArchRegId checkedId);
            Assert.Equal(value <= ArchRegId.MaxValue, representable);
            if (participates && representable)
            {
                Assert.Equal(
                    ResourceMaskBuilder.ForRegisterRead(value),
                    ResourceMaskBuilder.ForArchitecturalRegisterRead(checkedId));
                Assert.Equal(
                    ResourceMaskBuilder.ForRegisterWrite(value),
                    ResourceMaskBuilder.ForArchitecturalRegisterWrite(checkedId));
            }
        }
    }

    [Fact]
    public void ConcreteRolesAndStatefulWritebackCapabilityRemainUnchanged()
    {
        foreach (Type type in ConcreteTypes)
        {
            var operation = Assert.IsAssignableFrom<CSRMicroOp>(
                Activator.CreateInstance(type));
            operation.SrcRegID = 7;
            operation.DestRegID = 9;
            operation.WritesRegister = true;
            operation.InitializeMetadata();

            bool readsSource =
                operation is CsrReadWriteMicroOp or
                CsrReadSetMicroOp or
                CsrReadClearMicroOp;
            bool writesDestination = operation is not CsrClearMicroOp;
            Assert.Equal(
                readsSource ? new[] { 7 } : Array.Empty<int>(),
                operation.ReadRegisters);
            Assert.Equal(
                writesDestination ? new[] { 9 } : Array.Empty<int>(),
                operation.WriteRegisters);
            Assert.Equal(writesDestination, operation.WritesRegister);
        }

        var stateful = new CsrReadCounterMicroOp { DestRegID = 9 };
        stateful.InitializeMetadata();
        Assert.False(stateful.WritesRegister);

        stateful.WritesRegister = true;
        stateful.InitializeMetadata();
        Assert.True(stateful.WritesRegister);
        Assert.Equal(new[] { 9 }, stateful.WriteRegisters);

        stateful.DestRegID = VLIW_Instruction.NoArchReg;
        stateful.InitializeMetadata();
        Assert.False(stateful.WritesRegister);
        Assert.Empty(stateful.WriteRegisters);

        stateful.DestRegID = 9;
        stateful.InitializeMetadata();
        Assert.True(stateful.WritesRegister);
        Assert.Equal(new[] { 9 }, stateful.WriteRegisters);
    }

    [Fact]
    public void CutoverAddsNoSignatureWireInvalidOrTestSupportSurface()
    {
        PropertyInfo source = typeof(CSRMicroOp).GetProperty(nameof(CSRMicroOp.SrcRegID))
            ?? throw new MissingMemberException(nameof(CSRMicroOp),
                nameof(CSRMicroOp.SrcRegID));
        PropertyInfo destination = typeof(CSRMicroOp).GetProperty(
            nameof(CSRMicroOp.DestRegID))
            ?? throw new MissingMemberException(nameof(CSRMicroOp),
                nameof(CSRMicroOp.DestRegID));
        AssertMutableUshortProperty(source);
        AssertMutableUshortProperty(destination);

        MethodInfo initialize = Assert.Single(typeof(CSRMicroOp)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.DeclaredOnly)
            .Where(method => method.Name == nameof(CSRMicroOp.InitializeMetadata)));
        Assert.Empty(initialize.GetParameters());
        Assert.Equal(typeof(void), initialize.ReturnType);

        var valid = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.CSRRW,
            Immediate = CsrAddresses.VexcpMask,
            HasImmediate = true,
            Reg1ID = 9,
            Reg2ID = 7,
        };
        CSRMicroOp operation = Assert.IsType<CsrReadWriteMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.CSRRW, valid));
        Assert.Equal(new[] { 7 }, operation.ReadRegisters);
        Assert.Equal(new[] { 9 }, operation.WriteRegisters);

        DecoderContext invalidSource = valid;
        invalidSource.Reg2ID = 32;
        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.CSRRW,
                invalidSource));

        DecoderContext invalidDestination = valid;
        invalidDestination.Reg1ID = 32;
        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.CSRRW,
                invalidDestination));

        string root = FindRepositoryRoot();
        Assert.DoesNotContain("CSRMicroOp",
            ReadTree(root, "HybridCPU_Compiler"), StringComparison.Ordinal);
        Assert.DoesNotContain("CSRMicroOp",
            ReadTree(root, "TestAssemblerConsoleApps"), StringComparison.Ordinal);
        Assert.DoesNotContain("CSRMicroOp",
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
                "Core", "CPU_Core.TestSupport.cs"), StringComparison.Ordinal);
    }

    private static void AssertMetadataSnapshots(CSRMicroOp operation)
    {
        Assert.Equal(operation.ReadRegisters,
            operation.AdmissionMetadata.ReadRegisters);
        Assert.Equal(operation.WriteRegisters,
            operation.AdmissionMetadata.WriteRegisters);
        Assert.Equal(operation.ResourceMask.Low, operation.SafetyMask.Low);
        Assert.Equal(1UL << 63, operation.SafetyMask.High);
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

    private static void AssertMutableUshortProperty(PropertyInfo property)
    {
        Assert.Equal(typeof(ushort), property.PropertyType);
        Assert.True(property.GetMethod?.IsPublic);
        Assert.True(property.SetMethod?.IsPublic);
    }

    private static string ReadTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static bool IsBuildOutput(string path) =>
        path.Contains(
            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker '{startMarker}' was not found.");
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"End marker '{endMarker}' was not found.");
        return source[start..end];
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

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

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

    private static readonly Type[] ConcreteTypes =
    [
        typeof(CsrClearMicroOp),
        typeof(CsrReadClearImmediateMicroOp),
        typeof(CsrReadClearMicroOp),
        typeof(CsrReadCounterMicroOp),
        typeof(CsrReadSetImmediateMicroOp),
        typeof(CsrReadSetMicroOp),
        typeof(CsrReadWriteImmediateMicroOp),
        typeof(CsrReadWriteMicroOp),
    ];
}
