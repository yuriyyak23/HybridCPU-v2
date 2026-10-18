using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123kBranchMicroOpRegisterMaskInventoryTests
{
    private const string ThisFileName =
        "Rf123kBranchMicroOpRegisterMaskInventoryTests.cs";

    [Fact]
    public void PaperSeparatesTheThreeRolesAndAuthorizesOnlyValidInputMaskCutover()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.4 Control-flow MicroOp architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("The three register roles remain distinct", paper,
            StringComparison.Ordinal);
        Assert.Contains("without filtering x0, `NoReg`", paper,
            StringComparison.Ordinal);
        Assert.Contains("Architectural x0 is a",
            paper, StringComparison.Ordinal);
        Assert.Contains("present and valid base-register identity",
            paper, StringComparison.Ordinal);
        Assert.Contains("Destination x0 is a present architectural identity",
            paper, StringComparison.Ordinal);
        Assert.Contains("invalid-to-x0 and cross-role compatibility aliases",
            paper, StringComparison.Ordinal);
        Assert.Contains("conditional or JALR source can alias the value zero",
            paper, StringComparison.Ordinal);
        Assert.Contains("`RetireCoordinator` rejects", paper,
            StringComparison.Ordinal);
        Assert.Contains("every unrepresentable value uses the exact raw call",
            paper, StringComparison.Ordinal);
        Assert.Contains("later invalid-input slice", paper, StringComparison.Ordinal);
        Assert.Contains("zero-caller", paper, StringComparison.Ordinal);
    }


    [Fact]
    public void EveryRawUshortPreservesConditionalJalrAndLinkMetadataBehavior()
    {
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            AssertConditionalRole(value, firstRole: true);
            AssertConditionalRole(value, firstRole: false);
            AssertJalrBaseRole(value);
            AssertLinkRole(value);
        }
    }

    [Fact]
    public void ZeroSentinelsCanonicalConversionAndAdmissionSnapshotsRemainDistinct()
    {
        BranchMicroOp conditional = Create(
            InstructionsEnum.BEQ,
            isConditional: true,
            destination: VLIW_Instruction.NoReg,
            source1: 0,
            source2: VLIW_Instruction.NoReg);
        Assert.Equal(new[] { 0, (int)VLIW_Instruction.NoReg },
            conditional.ReadRegisters);
        Assert.Empty(conditional.WriteRegisters);
        Assert.Equal(ExpectedRead(0) | ExpectedRead(VLIW_Instruction.NoReg),
            conditional.ResourceMask);
        Assert.Equal(conditional.ReadRegisters, conditional.AdmissionMetadata.ReadRegisters);
        Assert.Equal(conditional.WriteRegisters,
            conditional.AdmissionMetadata.WriteRegisters);
        Assert.Equal(conditional.ResourceMask.Low, conditional.SafetyMask.Low);
        Assert.Equal(1UL << 63, conditional.SafetyMask.High);

        BranchMicroOp jalrX0 = Create(
            InstructionsEnum.JALR,
            isConditional: false,
            destination: 0,
            source1: 0,
            source2: 123);
        Assert.Equal(new[] { 0 }, jalrX0.ReadRegisters);
        Assert.Empty(jalrX0.WriteRegisters);
        Assert.False(jalrX0.WritesRegister);

        BranchMicroOp jalrAbsent = Create(
            InstructionsEnum.JALR,
            isConditional: false,
            destination: VLIW_Instruction.NoReg,
            source1: VLIW_Instruction.NoReg,
            source2: 123);
        Assert.Empty(jalrAbsent.ReadRegisters);
        Assert.Empty(jalrAbsent.WriteRegisters);
        Assert.Equal(ResourceBitset.Zero, jalrAbsent.ResourceMask);

        BranchMicroOp rawNoArchReg = Create(
            InstructionsEnum.JALR,
            isConditional: false,
            destination: ArchRegisterTripletEncoding.NoArchReg,
            source1: ArchRegisterTripletEncoding.NoArchReg,
            source2: 0);
        Assert.Equal(new[] { (int)ArchRegisterTripletEncoding.NoArchReg },
            rawNoArchReg.ReadRegisters);
        Assert.Equal(new[] { (int)ArchRegisterTripletEncoding.NoArchReg },
            rawNoArchReg.WriteRegisters);
        Assert.True(rawNoArchReg.WritesRegister);

        BranchMicroOp canonicalNoArchReg = CreatePublished(
            InstructionsEnum.BEQ,
            rd: ArchRegisterTripletEncoding.NoArchReg,
            rs1: ArchRegisterTripletEncoding.NoArchReg,
            rs2: 3);
        Assert.Equal(VLIW_Instruction.NoReg, canonicalNoArchReg.DestRegID);
        Assert.Equal(VLIW_Instruction.NoReg, canonicalNoArchReg.Reg1ID);
        Assert.Equal(new[] { (int)VLIW_Instruction.NoReg, 3 },
            canonicalNoArchReg.ReadRegisters);
    }

    [Fact]
    public void FailedDecoderProjectionAliasesAndUncheckedOpcodeNarrowingRemainExplicit()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.JALR,
            Immediate = 4,
            HasImmediate = true,
            Reg1ID = 32,
            Reg2ID = 5,
            Reg3ID = 0,
        };

        BranchMicroOp rawFallback = Assert.IsType<BranchMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.JALR, context));
        Assert.Equal((ushort)0, rawFallback.DestRegID);
        Assert.Equal((ushort)32, rawFallback.Reg1ID);
        Assert.Equal((ushort)5, rawFallback.Reg2ID);
        Assert.False(rawFallback.WritesRegister);
        Assert.Equal(new[] { 32 }, rawFallback.ReadRegisters);
        Assert.Empty(rawFallback.WriteRegisters);
        Assert.Equal(ExpectedRead(32), rawFallback.ResourceMask);

        BranchMicroOp highOpcodeAlias = Create(
            unchecked((uint)InstructionsEnum.JAL + 0x1_0000u),
            isConditional: false,
            destination: 7,
            source1: VLIW_Instruction.NoReg,
            source2: VLIW_Instruction.NoReg);
        Assert.True(highOpcodeAlias.WritesRegister);
        Assert.Equal(new[] { 7 }, highOpcodeAlias.WriteRegisters);
        Assert.Equal(ExpectedWrite(7), highOpcodeAlias.ResourceMask);

        string root = FindRepositoryRoot();
        string dataflow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "Dataflow", "CPU_Core.PipelineExecution.Dataflow.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "Registers", "Retire", "RetireCoordinator.cs");
        Assert.Contains(
            "if (regID == 0 || regID >= YAKSys_Hybrid_CPU.Core.Registers.RenameMap.ArchRegs)",
            dataflow, StringComparison.Ordinal);
        Assert.Contains("return 0;", dataflow, StringComparison.Ordinal);
        Assert.Contains(
            "record.Kind == RetireRecordKind.RegisterWrite",
            retire, StringComparison.Ordinal);
        Assert.Contains(
            "(uint)record.ArchReg >= (uint)RenameMap.ArchRegs",
            retire, StringComparison.Ordinal);
        Assert.Contains("throw new ArgumentOutOfRangeException(", retire,
            StringComparison.Ordinal);
    }


    private static void AssertConditionalRole(ushort value, bool firstRole)
    {
        ushort source1 = firstRole ? value : (ushort)0;
        ushort source2 = firstRole ? (ushort)0 : value;
        BranchMicroOp operation = Create(
            InstructionsEnum.BEQ,
            isConditional: true,
            destination: VLIW_Instruction.NoReg,
            source1,
            source2);

        Assert.Equal(new[] { (int)source1, (int)source2 }, operation.ReadRegisters);
        Assert.Empty(operation.WriteRegisters);
        Assert.False(operation.WritesRegister);
        Assert.Equal(ExpectedRead(source1) | ExpectedRead(source2),
            operation.ResourceMask);
    }

    private static void AssertJalrBaseRole(ushort value)
    {
        BranchMicroOp operation = Create(
            InstructionsEnum.JALR,
            isConditional: false,
            destination: 0,
            source1: value,
            source2: 123);

        if (value == VLIW_Instruction.NoReg)
        {
            Assert.Empty(operation.ReadRegisters);
            Assert.Equal(ResourceBitset.Zero, operation.ResourceMask);
        }
        else
        {
            Assert.Equal(new[] { (int)value }, operation.ReadRegisters);
            Assert.Equal(ExpectedRead(value), operation.ResourceMask);
        }

        Assert.Empty(operation.WriteRegisters);
        Assert.False(operation.WritesRegister);
    }

    private static void AssertLinkRole(ushort value)
    {
        BranchMicroOp operation = Create(
            InstructionsEnum.JAL,
            isConditional: false,
            destination: value,
            source1: VLIW_Instruction.NoReg,
            source2: VLIW_Instruction.NoReg);
        bool publishes = value != 0 && value != VLIW_Instruction.NoReg;

        Assert.Empty(operation.ReadRegisters);
        Assert.Equal(publishes, operation.WritesRegister);
        if (publishes)
        {
            Assert.Equal(new[] { (int)value }, operation.WriteRegisters);
            Assert.Equal(ExpectedWrite(value), operation.ResourceMask);
        }
        else
        {
            Assert.Empty(operation.WriteRegisters);
            Assert.Equal(ResourceBitset.Zero, operation.ResourceMask);
        }
    }

    private static BranchMicroOp Create(
        InstructionsEnum opcode,
        bool isConditional,
        ushort destination,
        ushort source1,
        ushort source2) =>
        Create((uint)opcode, isConditional, destination, source1, source2);

    private static BranchMicroOp Create(
        uint opcode,
        bool isConditional,
        ushort destination,
        ushort source1,
        ushort source2)
    {
        var operation = new BranchMicroOp
        {
            OpCode = opcode,
            IsConditional = isConditional,
            DestRegID = destination,
            Reg1ID = source1,
            Reg2ID = source2,
        };
        operation.InitializeMetadata();
        return operation;
    }

    private static BranchMicroOp CreatePublished(
        InstructionsEnum opcode,
        byte rd,
        byte rs1,
        byte rs2)
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.ControlFlow,
            SerializationClass = SerializationClass.Free,
            Rd = rd,
            Rs1 = rs1,
            Rs2 = rs2,
            Imm = 4,
        };
        MethodInfo method = typeof(InstructionRegistry).GetMethod(
            "TryCreatePublishedControlFlowMicroOp",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                nameof(InstructionRegistry),
                "TryCreatePublishedControlFlowMicroOp");
        object?[] arguments = [instruction, null];
        Assert.True(Assert.IsType<bool>(method.Invoke(null, arguments)));
        return Assert.IsType<BranchMicroOp>(arguments[1]);
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

    private static string[] FindFilesContaining(
        string root,
        string relativeRoot,
        string value,
        params string[] excludedFileNames) =>
        Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !excludedFileNames.Contains(
                Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains(value, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static string ReadTree(
        string root,
        string relativeRoot,
        string? excludedFileName = null,
        string? requiredPathFragment = null,
        string? secondExcludedFileName = null) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => excludedFileName is null ||
                           !path.EndsWith(excludedFileName,
                               StringComparison.OrdinalIgnoreCase))
            .Where(path => secondExcludedFileName is null ||
                           !path.EndsWith(secondExcludedFileName,
                               StringComparison.OrdinalIgnoreCase))
            .Where(path => requiredPathFragment is null ||
                           path.Contains(requiredPathFragment,
                               StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

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

    private static readonly string[] ExpectedProductionFiles =
    [
        "HybridCPU_ISE/CloseToHSL/Core/Contracts/CompilerContract.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06SpecializedCapabilityProjection.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Helpers.Core.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Legality/BundleLegalityAnalyzer.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/ControlFlow/CPU_Core.PipelineExecution.ControlFlow.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.StageFlow.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Control/MicroOp.Control.cs",
    ];

    private static readonly string[] ExpectedTestFiles =
    [
        "HybridCPU_ISE.Tests/Architecture/Rf06SpecializedCapabilityProjectionTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf083fControlPcWriteTransportBlockerAuditTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084cPcWriteCompatibilitySourceCertificationTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084uControlLinkRegisterWriteSourceBlockerAuditTests.cs",
        "HybridCPU_ISE.Tests/CompilerTests/CompilerFacadeControlFlowEmissionTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/Phase4ExtensibilityTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/Phase9NominationRefactoringTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/SlotClassCapacityTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/SlotClassTaxonomyTests.cs",
        "HybridCPU_ISE.Tests/tests/CanonicalStructuralSafetyMaskTests.cs",
        "HybridCPU_ISE.Tests/tests/DecoderContextImmediateAbiTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase00InstructionInventoryTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase02InstructionIrTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase03DirectFactoryConditionalBranchPublicationTailTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase03DirectFactoryUnconditionalBranchPublicationTailTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase03FallbackControlFlowCarrierTailTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase03PublishedControlFlowOperandTailTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09CanonicalDecodePublicationContractTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09DirectFactoryCallerBoundaryTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09DirectFactoryConditionalBranchFollowThroughTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09DirectFactoryUnconditionalBranchFollowThroughTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09LegacyBudgetFreezeTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase1V6CorrectnessCore.cs",
        "HybridCPU_ISE.Tests/tests/Phase5LegacyRemovalTests.cs",
        "HybridCPU_ISE.Tests/tests/WriteBackFaultOrderingTests.cs",
    ];
}
