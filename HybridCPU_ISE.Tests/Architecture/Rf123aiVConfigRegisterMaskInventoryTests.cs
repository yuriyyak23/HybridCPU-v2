using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123aiVConfigRegisterMaskInventoryTests
{
    [Fact]
    public void PaperDefinesDistinctRolesAbsenceRawSeamsAndLaterCutover()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.13a Vector-config register-role and metadata-fold boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("exactly `VSETVL`, `VSETVLI` and `VSETIVLI`", paper,
            StringComparison.Ordinal);
        Assert.Contains("x0 and `NoReg` are\nrole-specific source absence forms",
            paper, StringComparison.Ordinal);
        Assert.Contains("returns false only for x0 and\n`NoReg`; every other `ushort`",
            paper, StringComparison.Ordinal);
        Assert.Contains("reflection can expose the full `ushort` source domain",
            paper, StringComparison.Ordinal);
        Assert.Contains("every nonrepresentable source\ntherefore return zero",
            paper, StringComparison.Ordinal);
        Assert.Contains("destination at or above 32 before any\npublication",
            paper, StringComparison.Ordinal);
        Assert.Contains("source in 1..31 and `ForArchitecturalRegisterWrite(ArchRegId)`",
            paper, StringComparison.Ordinal);
        Assert.Contains("There is no bank identifier, bank resolver, unresolved-bank fallback",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DeclarationStorageConfigurationAndFoldShapeRemainOrderedWithRawFallbacks()
    {
        string source = ReadVectorData();
        string carrier = ExtractBalanced(source, "public class VConfigMicroOp");
        string initialize = ExtractBalanced(carrier, "public void InitializeMetadata()");

        Assert.Contains("public class VConfigMicroOp : MicroOp", carrier,
            StringComparison.Ordinal);
        Assert.Contains("public VectorConfigOperationKind OperationKind { get; private set; }",
            carrier, StringComparison.Ordinal);
        Assert.Contains("public ushort SrcReg1ID { get; private set; } = VLIW_Instruction.NoReg",
            carrier, StringComparison.Ordinal);
        Assert.Contains("public ushort SrcReg2ID { get; private set; } = VLIW_Instruction.NoReg",
            carrier, StringComparison.Ordinal);
        Assert.Contains("public void ConfigureForRegisterVType(", carrier,
            StringComparison.Ordinal);
        Assert.Contains("public void ConfigureForImmediateVType(", carrier,
            StringComparison.Ordinal);
        Assert.Contains("public void ConfigureForImmediateAvlAndVType(", carrier,
            StringComparison.Ordinal);
        Assert.Contains("VectorConfigOperationKind.Vsetvl => BuildReadRegisterList(SrcReg1ID, SrcReg2ID)",
            initialize, StringComparison.Ordinal);
        Assert.Contains("VectorConfigOperationKind.Vsetvli => BuildReadRegisterList(SrcReg1ID)",
            initialize, StringComparison.Ordinal);
        Assert.Contains("_ => Array.Empty<int>()", initialize,
            StringComparison.Ordinal);
        Assert.Contains("? new[] { (int)DestRegID }", initialize,
            StringComparison.Ordinal);
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(registerId)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)"));
        Assert.Equal(2, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(initialize,
            "ForArchitecturalRegisterRead(register)"));
        Assert.Equal(1, Count(initialize,
            "ForArchitecturalRegisterWrite("));

        AssertOrdered(initialize,
            "bool writesRegister = HasArchitecturalDestinationRegister()",
            "ReadRegisters = OperationKind switch",
            "WriteRegisters = writesRegister",
            "ReadMemoryRanges = Array.Empty",
            "WriteMemoryRanges = Array.Empty",
            "ResourceMask = ResourceBitset.Zero",
            "foreach (int registerId in ReadRegisters)",
            "ArchRegId.TryCreate(registerId, out ArchRegId register)",
            "ForArchitecturalRegisterRead(register)",
            "ForRegisterRead(registerId)",
            "if (writesRegister)",
            "ArchRegId.TryCreate(",
            "ForArchitecturalRegisterWrite(",
            "ForRegisterWrite(DestRegID)",
            "PublishExplicitStructuralSafetyMask()",
            "RefreshAdmissionMetadata(this)");

        string buildReads = ExtractBalanced(carrier,
            "private static int[] BuildReadRegisterList(");
        Assert.Contains("rawRegister == 0", buildReads, StringComparison.Ordinal);
        Assert.Contains("rawRegister == VLIW_Instruction.NoReg", buildReads,
            StringComparison.Ordinal);
        Assert.Contains("registers.Add(rawRegister)", buildReads,
            StringComparison.Ordinal);
        string destination = ExtractBalanced(carrier,
            "private bool HasArchitecturalDestinationRegister()");
        Assert.Contains("DestRegID != 0", destination, StringComparison.Ordinal);
        Assert.Contains("DestRegID != VLIW_Instruction.NoReg", destination,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedWorldProductionCompilerAndTestCallerManifestRemainsExact()
    {
        string root = FindRepositoryRoot();

        Assert.Equal(
        [
            "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Helpers.Vector.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.StageFlow.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Data.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs"
        ], FindFilesContaining(root, "HybridCPU_ISE", "VConfigMicroOp"));
        Assert.Empty(FindFilesContaining(root, "HybridCPU_Compiler", "VConfigMicroOp"));
        Assert.Empty(FindFilesContaining(root, "TestAssemblerConsoleApps", "VConfigMicroOp"));

        Assert.Equal(
        [
            "HybridCPU_ISE.Tests/Architecture/Rf084gVectorConfigWriteIdentitySourceBlockerAuditTests.cs",
            "HybridCPU_ISE.Tests/Architecture/Rf084wVectorConfigOptionalRdRegisterWriteBlockerAuditTests.cs",
            "HybridCPU_ISE.Tests/Architecture/Rf107VectorSegmentStorePublicationInventoryTests.cs",
            "HybridCPU_ISE.Tests/CompilerTests/CompilerEmissionInventoryTests.cs",
            "HybridCPU_ISE.Tests/tests/CanonicalStructuralSafetyMaskTests.cs",
            "HybridCPU_ISE.Tests/tests/DecoderContextImmediateAbiTests.cs",
            "HybridCPU_ISE.Tests/tests/Phase00InstructionInventoryTests.cs",
            "HybridCPU_ISE.Tests/tests/Phase09CanonicalDecodePublicationContractTests.cs",
            "HybridCPU_ISE.Tests/tests/RetireContractClosureTests.cs"
        ], FindFilesContaining(root, "HybridCPU_ISE.Tests", "VConfigMicroOp",
            "Rf123agVectorAdmissionRegisterMaskInventoryTests.cs",
            "Rf123ahVectorAdmissionRegisterMaskValidInputCutoverTests.cs",
            "Rf123aiVConfigRegisterMaskInventoryTests.cs",
            "Rf123ajVConfigRegisterMaskValidInputCutoverTests.cs",
            "Rf123akArchitecturalRegisterResourceMaskCallerClosureAuditTests.cs"));

        string registry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Vector.cs");
        string factory = ExtractBalanced(registry,
            "private static void RegisterVectorConfigOp(");
        Assert.Equal(1, Count(factory, "new VConfigMicroOp"));
        Assert.Equal(1, Count(factory, "ConfigureForRegisterVType("));
        Assert.Equal(1, Count(factory, "ConfigureForImmediateVType("));
        Assert.Equal(1, Count(factory, "ConfigureForImmediateAvlAndVType("));
        Assert.Contains("registerId == byte.MaxValue) ? ushort.MaxValue : registerId",
            factory, StringComparison.Ordinal);
        Assert.Contains("VSETVL manual publication requires canonical rs1/rs2",
            factory, StringComparison.Ordinal);
        Assert.Contains("VSETVLI manual publication requires a canonical rs1",
            factory, StringComparison.Ordinal);

        string compiler = Read(root, "HybridCPU_Compiler", "API", "Facade",
            "PlatformAsmFacade.cs");
        Assert.Equal(1, Count(compiler, "public void VSetVli("));
        Assert.Equal(1, Count(compiler, "InstructionsEnum.VSETVLI"));
        Assert.DoesNotContain("InstructionsEnum.VSETVL,", compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.VSETIVLI", compiler,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublicByteSourceProfilesKeepX0AbsenceOrderDuplicatesAndRawMasks()
    {
        for (int raw = byte.MinValue; raw <= byte.MaxValue; raw++)
        {
            byte register = (byte)raw;

            var vsetvl = new VConfigMicroOp
            {
                DestRegID = VLIW_Instruction.NoReg
            };
            vsetvl.ConfigureForRegisterVType(register, register);
            vsetvl.InitializeMetadata();
            int[] expectedPair = register == 0
                ? []
                : [register, register];
            Assert.Equal(expectedPair, vsetvl.ReadRegisters);
            Assert.Empty(vsetvl.WriteRegisters);
            Assert.Equal(register == 0
                    ? ResourceBitset.Zero
                    : ResourceMaskBuilder.ForRegisterRead(register),
                vsetvl.ResourceMask);

            var vsetvli = new VConfigMicroOp
            {
                DestRegID = VLIW_Instruction.NoReg
            };
            vsetvli.ConfigureForImmediateVType(
                VectorConfigOperationKind.Vsetvli,
                register,
                encodedVTypeImmediate: 0);
            vsetvli.InitializeMetadata();
            Assert.Equal(register == 0 ? [] : [raw], vsetvli.ReadRegisters);

            var vsetivli = new VConfigMicroOp
            {
                DestRegID = VLIW_Instruction.NoReg
            };
            vsetivli.ConfigureForImmediateAvlAndVType(0, 0);
            vsetivli.InitializeMetadata();
            Assert.Empty(vsetivli.ReadRegisters);
            Assert.Empty(vsetivli.WriteRegisters);
        }
    }

    [Fact]
    public void ReflectionSourceAndPublicDestinationExposeExactUshortCompatibilityDomain()
    {
        PropertyInfo operation = RequiredProperty(nameof(VConfigMicroOp.OperationKind));
        PropertyInfo source1 = RequiredProperty(nameof(VConfigMicroOp.SrcReg1ID));

        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort register = (ushort)raw;
            var source = new VConfigMicroOp
            {
                DestRegID = VLIW_Instruction.NoReg
            };
            operation.SetValue(source, VectorConfigOperationKind.Vsetvli);
            source1.SetValue(source, register);
            source.InitializeMetadata();

            bool sourceAbsent = register is 0 or VLIW_Instruction.NoReg;
            Assert.Equal(sourceAbsent ? [] : [raw], source.ReadRegisters);
            Assert.Equal(sourceAbsent
                    ? ResourceBitset.Zero
                    : ResourceMaskBuilder.ForRegisterRead(raw),
                source.ResourceMask);

            var destination = new VConfigMicroOp
            {
                DestRegID = register
            };
            destination.ConfigureForImmediateAvlAndVType(0, 0);
            destination.InitializeMetadata();

            bool destinationAbsent = register is 0 or VLIW_Instruction.NoReg;
            Assert.Equal(!destinationAbsent, destination.WritesRegister);
            Assert.Equal(destinationAbsent ? [] : [raw],
                destination.WriteRegisters);
            Assert.Equal(destinationAbsent
                    ? ResourceBitset.Zero
                    : ResourceMaskBuilder.ForRegisterWrite(raw),
                destination.ResourceMask);
        }
    }

    [Fact]
    public void ExecutionRetireReflectionAndUnrelatedFamilySeamsRemainExplicit()
    {
        Type type = typeof(VConfigMicroOp);
        ConstructorInfo constructor = Assert.Single(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(constructor.GetParameters());
        Assert.True(RequiredProperty(nameof(VConfigMicroOp.OperationKind))
            .GetSetMethod(nonPublic: true)!.IsPrivate);
        Assert.True(RequiredProperty(nameof(VConfigMicroOp.SrcReg1ID))
            .GetSetMethod(nonPublic: true)!.IsPrivate);
        Assert.True(RequiredProperty(nameof(VConfigMicroOp.SrcReg2ID))
            .GetSetMethod(nonPublic: true)!.IsPrivate);
        Assert.True(typeof(MicroOp).GetProperty(nameof(MicroOp.ReadRegisters))!
            .GetSetMethod(nonPublic: true)!.IsFamily);
        Assert.True(typeof(MicroOp).GetProperty(nameof(MicroOp.WriteRegisters))!
            .GetSetMethod(nonPublic: true)!.IsFamily);

        var carrier = new VConfigMicroOp();
        Assert.Equal(SlotClass.SystemSingleton,
            carrier.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, carrier.Placement.PinningKind);
        Assert.Equal(7, carrier.Placement.PinnedLaneId);

        string root = FindRepositoryRoot();
        string microOpBase = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        Assert.Contains("if (rawRegId == VLIW_Instruction.NoReg)",
            microOpBase, StringComparison.Ordinal);
        Assert.Contains("TryNormalizeFlatArchRegId(rawRegId",
            microOpBase, StringComparison.Ordinal);
        Assert.Contains("? value\n                : 0;", microOpBase,
            StringComparison.Ordinal);

        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        Assert.Contains("vectorConfigEffect.DestinationRegister >= RenameMap.ArchRegs",
            retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredVectorConfigEffect", retire,
            StringComparison.Ordinal);
        Assert.Contains("EmitGeneratedVectorConfigRetireRecords", retire,
            StringComparison.Ordinal);

        string initialize = ExtractBalanced(ReadVectorData(),
            "public class VConfigMicroOp");
        foreach (string unrelated in new[]
                 {
                     "MemoryBankId", "ForMemoryBank", "ForDMAChannel",
                     "DomainId", "DomainTag", "Token", "Generation",
                     "SlotId", "ForSlot"
                 })
        {
            Assert.DoesNotContain(unrelated,
                ExtractBalanced(initialize, "public void InitializeMetadata()"),
                StringComparison.Ordinal);
        }
    }

    private static PropertyInfo RequiredProperty(string name) =>
        typeof(VConfigMicroOp).GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingMemberException(typeof(VConfigMicroOp).FullName, name);

    private static string ReadVectorData() =>
        Read(FindRepositoryRoot(), "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Data.cs");

    private static string[] FindFilesContaining(
        string root,
        string relativeRoot,
        string token,
        params string[] excludedFileNames)
    {
        string absoluteRoot = Path.Combine(root,
            relativeRoot.Replace('/', Path.DirectorySeparatorChar));
        var excluded = new HashSet<string>(excludedFileNames,
            StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(absoluteRoot, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !excluded.Contains(Path.GetFileName(path)))
            .Where(path => File.ReadAllText(path).Contains(token,
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path)
                .Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertOrdered(string source, params string[] tokens)
    {
        int previous = -1;
        foreach (string token in tokens)
        {
            int current = source.IndexOf(token, previous + 1,
                StringComparison.Ordinal);
            Assert.True(current > previous,
                $"Expected token after offset {previous}: {token}");
            previous = current;
        }
    }

    private static string ExtractBalanced(string source, string marker)
    {
        int markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Missing marker: {marker}");
        int openBrace = source.IndexOf('{', markerIndex);
        Assert.True(openBrace >= 0, $"Missing opening brace after: {marker}");
        int depth = 0;
        for (int i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}' && --depth == 0)
                return source[markerIndex..(i + 1)];
        }

        throw new InvalidOperationException($"Unbalanced source after: {marker}");
    }

    private static int Count(string source, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(token, index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(Path.Combine([root, .. components]));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
