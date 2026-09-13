using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123yAtomicMicroOpRegisterMaskInventoryTests
{
    [Fact]
    public void PaperDefinesThreeRolesRawFallbackAndSeparateAuthorityOwners()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.11 Atomic MicroOp architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("The three raw architectural-register roles are distinct:",
            paper, StringComparison.Ordinal);
        Assert.Contains("`BaseRegID` is a required address source",
            paper, StringComparison.Ordinal);
        Assert.Contains("`SrcRegID` is a data source for every opcode except exact `LR_W` and",
            paper, StringComparison.Ordinal);
        Assert.Contains("`DestRegID` is a possible returned-result destination",
            paper, StringComparison.Ordinal);
        Assert.Contains("existing invalid-to-zero aliases",
            paper, StringComparison.Ordinal);
        Assert.Contains("retained cross-family compatibility seam",
            paper, StringComparison.Ordinal);
        Assert.Contains("every unrepresentable participating\nvalue must use the exact raw helper",
            paper, StringComparison.Ordinal);
        Assert.Contains("does not define a universal `DomainId`",
            paper, StringComparison.Ordinal);
    }


    [Fact]
    public void SourceShapeFreezesPredicatesListsCheckedFallbackFoldsAndPublicationOrder()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.Misc.cs");
        string carrier = ExtractBalanced(source,
            "public sealed class AtomicMicroOp");
        string initialize = ExtractBalanced(carrier,
            "public void InitializeMetadata()");

        Assert.Equal(2, Count(initialize, "if (BaseRegID != noReg)"));
        Assert.Equal(2, Count(initialize,
            "if (UsesSourceRegister && SrcRegID != noReg)"));
        Assert.Equal(2, Count(initialize,
            "WritesRegister && DestRegID != 0 && DestRegID != noReg"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(BaseRegID)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(SrcRegID)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)"));
        Assert.Equal(3, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Equal(2, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.DoesNotContain("ArchRegId.Create(", initialize,
            StringComparison.Ordinal);
        Assert.Contains("OpCode is not (", carrier, StringComparison.Ordinal);
        Assert.Contains("IsaOpcodeValues.LR_W", carrier,
            StringComparison.Ordinal);
        Assert.Contains("IsaOpcodeValues.LR_D", carrier,
            StringComparison.Ordinal);

        AssertOrdered(initialize,
            "const ushort noReg = VLIW_Instruction.NoReg;",
            "var readRegs = new List<int>();",
            "readRegs.Add(BaseRegID);",
            "readRegs.Add(SrcRegID);",
            "ReadRegisters = readRegs;",
            "WriteRegisters = WritesRegister",
            "ReadMemoryRanges = new[]",
            "WriteMemoryRanges = new[]",
            "ResourceMask = ResourceBitset.Zero;",
            "ArchRegId.TryCreate(BaseRegID, out ArchRegId baseRegister)",
            "ForRegisterRead(BaseRegID)",
            "ArchRegId.TryCreate(SrcRegID, out ArchRegId sourceRegister)",
            "ForRegisterRead(SrcRegID)",
            "ArchRegId.TryCreate(DestRegID, out ArchRegId destinationRegister)",
            "ForRegisterWrite(DestRegID)",
            "ResourceMaskBuilder.ForAtomic()",
            "ResourceMaskBuilder.ForMemoryDomain(OwnerThreadId)",
            "PublishExplicitStructuralSafetyMask();",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void EveryUshortRetainsExactPerRoleListAndMaskBehavior()
    {
        const ushort noReg = VLIW_Instruction.NoReg;
        ResourceBitset fixedMask =
            ResourceMaskBuilder.ForAtomic() |
            ResourceMaskBuilder.ForMemoryDomain(0);

        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;

            var baseRole = new AtomicMicroOp
            {
                OpCode = IsaOpcodeValues.LR_W,
                BaseRegID = value,
                SrcRegID = noReg,
                DestRegID = noReg,
                Address = 0x1200,
                Size = 1
            };
            baseRole.InitializeMetadata();
            Assert.Equal(value == noReg ? [] : [raw],
                baseRole.ReadRegisters);
            Assert.Empty(baseRole.WriteRegisters);
            Assert.Equal(fixedMask |
                (value == noReg
                    ? ResourceBitset.Zero
                    : ResourceMaskBuilder.ForRegisterRead(raw)),
                baseRole.ResourceMask);
            Assert.Equal([(0x1200UL, 4UL)], baseRole.ReadMemoryRanges);
            Assert.Equal([(0x1200UL, 4UL)], baseRole.WriteMemoryRanges);

            var sourceRole = new AtomicMicroOp
            {
                OpCode = IsaOpcodeValues.SC_W,
                BaseRegID = noReg,
                SrcRegID = value,
                DestRegID = noReg
            };
            sourceRole.InitializeMetadata();
            Assert.Equal(value == noReg ? [] : [raw],
                sourceRole.ReadRegisters);
            Assert.Equal(fixedMask |
                (value == noReg
                    ? ResourceBitset.Zero
                    : ResourceMaskBuilder.ForRegisterRead(raw)),
                sourceRole.ResourceMask);

            var destinationRole = new AtomicMicroOp
            {
                OpCode = IsaOpcodeValues.AMOADD_W,
                BaseRegID = noReg,
                SrcRegID = noReg,
                DestRegID = value,
                WritesRegister = true
            };
            destinationRole.InitializeMetadata();
            bool destinationParticipates = value is not 0 and not noReg;
            Assert.Equal(destinationParticipates ? [raw] : [],
                destinationRole.WriteRegisters);
            Assert.Equal(fixedMask |
                (destinationParticipates
                    ? ResourceMaskBuilder.ForRegisterWrite(raw)
                    : ResourceBitset.Zero),
                destinationRole.ResourceMask);
        }
    }

    [Fact]
    public void DefaultDuplicatesCapabilityFactoryAndMutationSeamsRemainExplicit()
    {
        var defaultCarrier = new AtomicMicroOp();
        defaultCarrier.InitializeMetadata();
        Assert.Equal([0, 0], defaultCarrier.ReadRegisters);
        Assert.Empty(defaultCarrier.WriteRegisters);
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterRead(0) |
            ResourceMaskBuilder.ForAtomic() |
            ResourceMaskBuilder.ForMemoryDomain(0),
            defaultCarrier.ResourceMask);

        var duplicate = new AtomicMicroOp
        {
            OpCode = IsaOpcodeValues.AMOADD_W,
            BaseRegID = 7,
            SrcRegID = 7,
            DestRegID = 9,
            WritesRegister = true
        };
        duplicate.InitializeMetadata();
        Assert.Equal([7, 7], duplicate.ReadRegisters);
        Assert.Equal([9], duplicate.WriteRegisters);

        List<int> reads = Assert.IsType<List<int>>(duplicate.ReadRegisters);
        ResourceBitset cached =
            duplicate.AdmissionMetadata.RegisterHazardMask;
        reads[0] = 31;
        Assert.Equal(31, duplicate.AdmissionMetadata.ReadRegisters[0]);
        Assert.Equal(cached,
            duplicate.AdmissionMetadata.RegisterHazardMask);

        PropertyInfo baseProperty = typeof(AtomicMicroOp).GetProperty(
            nameof(AtomicMicroOp.BaseRegID)) ??
            throw new MissingMemberException();
        baseProperty.SetValue(duplicate, (ushort)12);
        duplicate.RefreshWriteMetadata();
        Assert.Equal([12, 7], duplicate.ReadRegisters);

        uint opcode = (uint)InstructionsEnum.AMOADD_W;
        AtomicMicroOp rawFactory = Assert.IsType<AtomicMicroOp>(
            InstructionRegistry.CreateMicroOp(opcode, new DecoderContext
            {
                OpCode = opcode,
                Reg1ID = 65534,
                Reg2ID = 65533,
                Reg3ID = 65532,
                OwnerThreadId = 0
            }));
        Assert.True(rawFactory.WritesRegister);
        Assert.Equal([65533, 65532], rawFactory.ReadRegisters);
        Assert.Equal([65534], rawFactory.WriteRegisters);
    }

    [Fact]
    public void OwnerDomainExecutionAndRetireBoundariesRemainSeparate()
    {
        var domainFifteen = new AtomicMicroOp
        {
            OwnerThreadId = 15,
            OpCode = IsaOpcodeValues.LR_W,
            BaseRegID = 1,
            SrcRegID = VLIW_Instruction.NoReg,
            DestRegID = 0
        };
        domainFifteen.InitializeMetadata();
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterRead(1) |
            ResourceMaskBuilder.ForAtomic() |
            ResourceMaskBuilder.ForMemoryDomain(15),
            domainFifteen.ResourceMask);

        foreach (int invalidDomain in new[] { -1, 16 })
        {
            var invalid = new AtomicMicroOp
            {
                OwnerThreadId = invalidDomain
            };
            Assert.Throws<ArgumentOutOfRangeException>(
                invalid.InitializeMetadata);
        }

        string root = FindRepositoryRoot();
        string carrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.Misc.cs");
        string baseCarrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        string atomicMemory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "AtomicMemory", "AtomicMemoryUnit.cs");

        Assert.Contains("int vtId = NormalizeExecutionVtId(OwnerThreadId);",
            carrier, StringComparison.Ordinal);
        Assert.Contains("if ((uint)ownerThreadId >= (uint)Processor.CPU_Core.SmtWays)",
            baseCarrier, StringComparison.Ordinal);
        Assert.Contains("if (rawRegId == VLIW_Instruction.NoReg)\n                return 0;",
            baseCarrier, StringComparison.Ordinal);
        Assert.Contains("TryNormalizeFlatArchRegId(rawRegId",
            baseCarrier, StringComparison.Ordinal);
        Assert.Contains(
            "_resolvedRetireEffect = core.AtomicMemoryUnit.ResolveRetireEffect(",
            carrier, StringComparison.Ordinal);
        Assert.Contains("PrevalidateAtomicEffect(retireEffect.AtomicEffect)",
            retire, StringComparison.Ordinal);
        Assert.Contains("atomicEffect.DestinationRegister >= RenameMap.ArchRegs",
            retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredAtomicEffect(retireEffect.AtomicEffect)",
            retire, StringComparison.Ordinal);
        Assert.Contains("internal AtomicRetireOutcome ApplyResolvedRetireEffect",
            atomicMemory, StringComparison.Ordinal);
    }

    [Fact]
    public void WireFspBankReflectionTestSupportAndSerializationSeamsStayIsolated()
    {
        string root = FindRepositoryRoot();
        string registry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Core.cs");
        string projector = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL",
            "Core", "Decoder", "DecodedBundleTransportProjector.cs");
        string memoryShadow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling",
            "Rf06MemoryShadowOracleDifferential.cs");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string carrier = ExtractBalanced(Read(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.Misc.cs"), "public sealed class AtomicMicroOp");
        string compiler = ReadTree(root, "HybridCPU_Compiler");
        string testAssembler = ReadTree(root, "TestAssemblerConsoleApps");

        Assert.Contains("private static void RegisterAtomicOp(uint opCode)",
            registry, StringComparison.Ordinal);
        Assert.Contains("DestRegID = ctx.Reg1ID", registry,
            StringComparison.Ordinal);
        Assert.Contains("BaseRegID = ctx.Reg2ID", registry,
            StringComparison.Ordinal);
        Assert.Contains("SrcRegID = ctx.Reg3ID", registry,
            StringComparison.Ordinal);
        Assert.Contains("Reg1ID = ToLegacyDecoderField(instruction.Rd)",
            projector, StringComparison.Ordinal);
        Assert.Contains("case AtomicMicroOp atomicMicroOp:",
            projector, StringComparison.Ordinal);
        Assert.Contains("else if (carrier is AtomicMicroOp atomic)",
            memoryShadow,
            StringComparison.Ordinal);
        Assert.Contains("SetClassFlexiblePlacement(SlotClass.LsuClass)",
            carrier, StringComparison.Ordinal);
        Assert.Contains("ExecuteTestAtomicWithStableCoreIdentity",
            testSupport, StringComparison.Ordinal);
        Assert.Contains("GeneratedAtomicEffect = atomicEffect",
            testSupport, StringComparison.Ordinal);
        Assert.DoesNotContain("AtomicMicroOp", compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AtomicMicroOp", testAssembler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AcceleratorTokenHandle", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ChannelId", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("DomainId", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TokenId", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("LaneId", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SlotId", carrier,
            StringComparison.Ordinal);
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
                "Rf123yAtomicMicroOpRegisterMaskInventoryTests.cs" and not
                "Rf123zAtomicMicroOpRegisterMaskValidInputCutoverTests.cs")
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

    private static string ReadTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.Exists(Path.Combine(root, relativeRoot))
            ? Directory.EnumerateFiles(Path.Combine(root, relativeRoot),
                    "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText)
            : []);

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
