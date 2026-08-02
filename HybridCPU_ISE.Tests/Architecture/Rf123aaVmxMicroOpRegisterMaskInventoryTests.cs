using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123aaVmxMicroOpRegisterMaskInventoryTests
{
    [Fact]
    public void PaperDefinesExactRolesAbsenceRawFallbackAndSeparateOwners()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.12 VMX MicroOp architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("exact `VMREAD` reads `Rs1`", paper,
            StringComparison.Ordinal);
        Assert.Contains("exact `VMWRITE` reads `Rs1` and then `Rs2`", paper,
            StringComparison.Ordinal);
        Assert.Contains("exact `VMREAD`, `VMPTRST` and `VMFUNC` may write `Rd`",
            paper, StringComparison.Ordinal);
        Assert.Contains("x0 and 255 are two retained absence",
            paper, StringComparison.Ordinal);
        Assert.Contains("raw bytes 32..254 also participate", paper,
            StringComparison.Ordinal);
        Assert.Contains("six existing\nsource folds and the one destination fold",
            paper, StringComparison.Ordinal);
        Assert.Contains("derived VMX `RegisterWrite` and `PcWrite` records",
            paper, StringComparison.Ordinal);
        Assert.Contains("creates no universal `ChannelId`, `DomainId` or\n`TokenId`",
            paper, StringComparison.Ordinal);
    }


    [Fact]
    public void SourceShapeFreezesPredicatesRawFoldsAndPublicationOrder()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.IO.cs");
        string carrier = ExtractBalanced(source,
            "public sealed class VmxMicroOp");
        string initialize = ExtractBalanced(carrier,
            "private void InitializeMetadata()");

        Assert.Equal(7, Count(initialize, "HasArchitecturalRegister("));
        Assert.Equal(6, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead("));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterWrite("));
        Assert.Equal(7, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Equal(6, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.Contains("registerId != 0 &&", carrier,
            StringComparison.Ordinal);
        Assert.Contains("registerId != VLIW_Instruction.NoArchReg",
            carrier, StringComparison.Ordinal);
        Assert.Contains("registerId == VLIW_Instruction.NoArchReg",
            carrier, StringComparison.Ordinal);
        Assert.Contains("? (byte)0", carrier, StringComparison.Ordinal);

        AssertOrdered(initialize,
            "var readRegs = new List<int>();",
            "ResourceMask = ResourceBitset.Zero;",
            "ushort opcode = unchecked((ushort)OpCode);",
            "WritesRegister =",
            "switch (opcode)",
            "ReadRegisters = readRegs;",
            "WriteRegisters = WritesRegister",
            "if (WritesRegister)",
            "ResourceMaskBuilder.ForRegisterWrite(Rd)",
            "PublishExplicitStructuralSafetyMask();",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void EveryByteRetainsExactPerOpcodeListMaskAndZeroSemantics()
    {
        foreach ((ushort opcode, int readCount, bool writes) in RoleCases())
        {
            for (int raw = byte.MinValue; raw <= byte.MaxValue; raw++)
            {
                byte value = (byte)raw;
                bool participates = value is not 0 and not
                    VLIW_Instruction.NoArchReg;
                var carrier = new VmxMicroOp
                {
                    OpCode = opcode,
                    Rd = value,
                    Rs1 = value,
                    Rs2 = value
                };

                carrier.RefreshWriteMetadata();

                Assert.Equal(participates
                    ? Enumerable.Repeat(raw, readCount).ToArray()
                    : [], carrier.ReadRegisters);
                Assert.Equal(writes && participates ? [raw] : [],
                    carrier.WriteRegisters);
                Assert.Equal(writes && participates, carrier.WritesRegister);

                ResourceBitset expected = ResourceBitset.Zero;
                if (participates && readCount != 0)
                    expected |= ResourceMaskBuilder.ForRegisterRead(raw);
                if (participates && writes)
                    expected |= ResourceMaskBuilder.ForRegisterWrite(raw);
                Assert.Equal(expected, carrier.ResourceMask);
            }
        }
    }

    [Fact]
    public void DefaultDuplicatesFactoryAndReflectionSeamsRemainExplicit()
    {
        var defaultCarrier = new VmxMicroOp();
        defaultCarrier.RefreshWriteMetadata();
        Assert.Empty(defaultCarrier.ReadRegisters);
        Assert.Empty(defaultCarrier.WriteRegisters);
        Assert.False(defaultCarrier.WritesRegister);
        Assert.Equal(ResourceBitset.Zero, defaultCarrier.ResourceMask);

        var duplicate = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMWRITE,
            Rs1 = 9,
            Rs2 = 9
        };
        duplicate.RefreshWriteMetadata();
        Assert.Equal([9, 9], duplicate.ReadRegisters);

        List<int> reads = Assert.IsType<List<int>>(duplicate.ReadRegisters);
        ResourceBitset cached =
            duplicate.AdmissionMetadata.RegisterHazardMask;
        reads[0] = 31;
        Assert.Equal(31, duplicate.AdmissionMetadata.ReadRegisters[0]);
        Assert.Equal(cached,
            duplicate.AdmissionMetadata.RegisterHazardMask);

        PropertyInfo rs1Property = typeof(VmxMicroOp).GetProperty(
            nameof(VmxMicroOp.Rs1)) ??
            throw new MissingMemberException();
        rs1Property.SetValue(duplicate, (byte)254);
        duplicate.RefreshWriteMetadata();
        Assert.Equal([254, 9], duplicate.ReadRegisters);
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterRead(254) |
            ResourceMaskBuilder.ForRegisterRead(9),
            duplicate.ResourceMask);

        VmxMicroOp sentinelFactory = Assert.IsType<VmxMicroOp>(
            InstructionRegistry.CreateMicroOp(IsaOpcodeValues.VMREAD,
                new DecoderContext
                {
                    OpCode = IsaOpcodeValues.VMREAD,
                    Reg1ID = VLIW_Instruction.NoReg,
                    Reg2ID = VLIW_Instruction.NoArchReg,
                    Reg3ID = 0
                }));
        Assert.Equal(VLIW_Instruction.NoArchReg, sentinelFactory.Rd);
        Assert.Equal(VLIW_Instruction.NoReg, sentinelFactory.DestRegID);
        Assert.Empty(sentinelFactory.ReadRegisters);
        Assert.Empty(sentinelFactory.WriteRegisters);

        Assert.ThrowsAny<InvalidOperationException>(() =>
            InstructionRegistry.CreateMicroOp(IsaOpcodeValues.VMREAD,
                new DecoderContext
                {
                    OpCode = IsaOpcodeValues.VMREAD,
                    Reg1ID = 32,
                    Reg2ID = 1,
                    Reg3ID = 0
                }));
    }

    [Fact]
    public void WireExecutionRetirePlacementAndUnrelatedFamiliesStaySeparate()
    {
        string root = FindRepositoryRoot();
        string carrierSource = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "MicroOps", "Types", "MicroOp.IO.cs");
        string carrier = ExtractBalanced(carrierSource,
            "public sealed class VmxMicroOp");
        string registry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Core.cs");
        string compiler = Read(root, "HybridCPU_Compiler", "Legacy", "VMX-2",
            "Core", "IR", "Model", "VmxCompilerAuthority.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Virtualization", "Compatibility", "Frontend", "Retire",
            "VmxRetireModel.cs");

        Assert.Contains("TryDecodeCanonicalOrUnpackedNoArchRegister",
            registry, StringComparison.Ordinal);
        Assert.Contains("rd == ArchRegisterTripletEncoding.NoArchReg",
            registry, StringComparison.Ordinal);
        Assert.Contains("throw new DecodeProjectionFaultException",
            registry, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.VMPTRST", compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("VmxMicroOp", compiler,
            StringComparison.Ordinal);
        Assert.Contains("public bool HasRegisterDestination", retire,
            StringComparison.Ordinal);
        Assert.Contains("public ushort RegisterDestination", retire,
            StringComparison.Ordinal);
        Assert.Contains("VmxRetireEffect.Fault(", carrier,
            StringComparison.Ordinal);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", carrier,
            StringComparison.Ordinal);
        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)",
            carrier, StringComparison.Ordinal);

        foreach (string unrelated in new[]
                 {
                     "MemoryBankId", "AcceleratorTokenHandle", "ChannelId",
                     "DomainId", "TokenId", "SlotId"
                 })
        {
            Assert.DoesNotContain(unrelated, carrier,
                StringComparison.Ordinal);
        }
    }

    private static IEnumerable<(ushort Opcode, int ReadCount, bool Writes)>
        RoleCases()
    {
        yield return (IsaOpcodeValues.VMREAD, 1, true);
        yield return (IsaOpcodeValues.VMWRITE, 2, false);
        yield return (IsaOpcodeValues.VMCLEAR, 1, false);
        yield return (IsaOpcodeValues.VMPTRLD, 1, false);
        yield return (IsaOpcodeValues.VMCALL, 2, false);
        yield return (IsaOpcodeValues.INVEPT, 2, false);
        yield return (IsaOpcodeValues.INVVPID, 2, false);
        yield return (IsaOpcodeValues.VMFUNC, 2, true);
        yield return (IsaOpcodeValues.VMSAVEX, 2, false);
        yield return (IsaOpcodeValues.VMRESTX, 2, false);
        yield return (IsaOpcodeValues.VMPTRST, 0, true);
        yield return (IsaOpcodeValues.VMXON, 0, false);
        yield return (0, 0, false);
        yield return (ushort.MaxValue, 0, false);
    }

    private const string EmptySha256 =
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private static void AssertPublicMutableProperty(
        Type declaredType,
        string name,
        Type propertyType)
    {
        PropertyInfo property = declaredType.GetProperty(name) ??
            throw new MissingMemberException(declaredType.FullName, name);
        Assert.Equal(propertyType, property.PropertyType);
        Assert.True(property.GetMethod?.IsPublic);
        Assert.True(property.SetMethod?.IsPublic);
    }

    private static void AssertManifest(
        string root,
        string relativeRoot,
        string token,
        int count,
        string sha256)
    {
        string[] paths = FindFilesContaining(root, relativeRoot, token);
        Assert.Equal(count, paths.Length);
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join("\n", paths))))
            .ToLowerInvariant());
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
            .Where(path => Path.GetFileName(path) is not
                "Rf123aaVmxMicroOpRegisterMaskInventoryTests.cs" and not
                "Rf123abVmxMicroOpRegisterMaskValidInputCutoverTests.cs")
            .Where(path => Regex.IsMatch(File.ReadAllText(path),
                $@"\b{Regex.Escape(token)}\b",
                RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(root, path)
                .Replace('\\', '/'))
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
