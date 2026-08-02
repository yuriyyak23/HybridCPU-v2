using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123sStoreMicroOpRegisterMaskInventoryTests
{
    private const string ThisFileName =
        "Rf123sStoreMicroOpRegisterMaskInventoryTests.cs";

    [Fact]
    public void PaperDefinesOrderedRolesInvalidToZeroSeamAndLaterCutoverOnly()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.8 Scalar-store architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("The two public mutable `ushort` register roles are distinct and ordered",
            paper, StringComparison.Ordinal);
        Assert.Contains("`NoReg=65535` alone",
            paper, StringComparison.Ordinal);
        Assert.Contains("means metadata absence",
            paper, StringComparison.Ordinal);
        Assert.Contains("invalid-to-zero aliases",
            paper, StringComparison.Ordinal);
        Assert.Contains("The direct `StoreMicroOp.Execute` contour instead consumes the `Value`",
            paper, StringComparison.Ordinal);
        Assert.Contains("Neither register ID is serialized into\nthat retire effect",
            paper, StringComparison.Ordinal);
        Assert.Contains("A later valid-input-only cutover",
            paper, StringComparison.Ordinal);
        Assert.Contains("Invalid-input\nbehavior, signature migration",
            paper, StringComparison.Ordinal);
    }


    [Fact]
    public void SourceShapeFreezesListsRawFoldsAndPublicationOrder()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Memory",
            "MicroOp.LoadStore.cs");
        string store = ExtractBalanced(source, "public class StoreMicroOp");
        string initialize = ExtractBalanced(
            store, "public void InitializeMetadata()");

        Assert.Contains("const ushort noReg = VLIW_Instruction.NoReg;",
            initialize, StringComparison.Ordinal);
        Assert.Equal(2, Count(initialize, "SrcRegID != noReg"));
        Assert.Equal(2, Count(initialize, "BaseRegID != noReg"));
        Assert.Contains("var readRegs = new List<int>();", initialize,
            StringComparison.Ordinal);
        Assert.Contains("if (SrcRegID != noReg) readRegs.Add(SrcRegID);",
            initialize, StringComparison.Ordinal);
        Assert.Contains("if (BaseRegID != noReg) readRegs.Add(BaseRegID);",
            initialize, StringComparison.Ordinal);
        Assert.Contains("ReadRegisters = readRegs;", initialize,
            StringComparison.Ordinal);
        Assert.Contains("WriteRegisters = Array.Empty<int>();", initialize,
            StringComparison.Ordinal);
        Assert.Contains("WriteMemoryRanges = new[] { (Address, (ulong)Size) };",
            initialize, StringComparison.Ordinal);
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(SrcRegID)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(BaseRegID)"));
        Assert.Equal(2, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Contains(
            "ArchRegId.TryCreate(SrcRegID, out ArchRegId sourceRegister)",
            initialize, StringComparison.Ordinal);
        Assert.Contains(
            "ArchRegId.TryCreate(BaseRegID, out ArchRegId baseRegister)",
            initialize, StringComparison.Ordinal);
        Assert.Contains(
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(sourceRegister)",
            initialize, StringComparison.Ordinal);
        Assert.Contains(
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(baseRegister)",
            initialize, StringComparison.Ordinal);
        Assert.DoesNotContain("ArchRegId.Create(", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("%", initialize, StringComparison.Ordinal);

        AssertOrdered(initialize,
            "readRegs.Add(SrcRegID)",
            "readRegs.Add(BaseRegID)",
            "ReadRegisters = readRegs;",
            "WriteRegisters = Array.Empty<int>();",
            "WriteMemoryRanges = new[] { (Address, (ulong)Size) };",
            "ResourceMask = ResourceBitset.Zero;",
            "ArchRegId.TryCreate(SrcRegID, out ArchRegId sourceRegister)",
            "ResourceMaskBuilder.ForRegisterRead(SrcRegID)",
            "ArchRegId.TryCreate(BaseRegID, out ArchRegId baseRegister)",
            "ResourceMaskBuilder.ForRegisterRead(BaseRegID)",
            "ResourceMaskBuilder.ForStore()",
            "ResourceMaskBuilder.ForMemoryDomain(OwnerThreadId)",
            "PublishExplicitStructuralSafetyMask();",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void EveryUshortPreservesIndependentRawSourceAndBaseRoles()
    {
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            bool present = value != VLIW_Instruction.NoReg;

            StoreMicroOp sourceOnly = Create(
                value, VLIW_Instruction.NoReg, address: 0x1000);
            Assert.Equal(present ? [(int)value] : Array.Empty<int>(),
                sourceOnly.ReadRegisters);
            Assert.Equal(Expected(value, VLIW_Instruction.NoReg),
                sourceOnly.ResourceMask);

            StoreMicroOp baseOnly = Create(
                VLIW_Instruction.NoReg, value, address: 0x1000);
            Assert.Equal(present ? [(int)value] : Array.Empty<int>(),
                baseOnly.ReadRegisters);
            Assert.Equal(Expected(VLIW_Instruction.NoReg, value),
                baseOnly.ResourceMask);
        }

        StoreMicroOp duplicate = Create(7, 7, address: 0x1000);
        Assert.Equal([7, 7], duplicate.ReadRegisters);
        Assert.Equal(Expected(7, 7), duplicate.ResourceMask);
    }

    [Fact]
    public void RefreshReflectionAndTestSupportMutationSeamsRemainExplicit()
    {
        StoreMicroOp operation = Create(7, 9, 0xFFFF000000000000UL);
        Assert.False(operation.IsStealable);
        IReadOnlyList<int> firstReads = operation.ReadRegisters;

        operation.SrcRegID = VLIW_Instruction.NoReg;
        operation.BaseRegID = VLIW_Instruction.NoReg;
        operation.Address = 0x1000;
        operation.InitializeMetadata();
        Assert.Empty(operation.ReadRegisters);
        Assert.NotSame(firstReads, operation.ReadRegisters);
        Assert.False(operation.IsStealable);
        Assert.Equal(Expected(
            VLIW_Instruction.NoReg, VLIW_Instruction.NoReg),
            operation.ResourceMask);

        StoreMicroOp exposed = Create(7, 9, 0x1000);
        var reads = Assert.IsType<List<int>>(exposed.ReadRegisters);
        reads[0] = 31;
        Assert.Equal(31, exposed.AdmissionMetadata.ReadRegisters[0]);
        Assert.Equal(
            MicroOpAdmissionMetadata.BuildRegisterHazardMask([7, 9], []),
            exposed.AdmissionMetadata.RegisterHazardMask);

        PropertyInfo sourceProperty =
            typeof(StoreMicroOp).GetProperty(nameof(StoreMicroOp.SrcRegID))
            ?? throw new MissingMemberException();
        sourceProperty.SetValue(exposed, (ushort)65534);
        exposed.InitializeMetadata();
        Assert.Equal([65534, 9], exposed.ReadRegisters);

        string root = FindRepositoryRoot();
        string coreTestSupport = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string helper = Read(root, "HybridCPU_ISE.Tests", "TestHelpers",
            "MicroOpTestHelper.cs");
        Assert.True(Count(coreTestSupport, "SrcRegID = 1") >= 2);
        string createStore = ExtractBalanced(
            helper, "public static StoreMicroOp CreateStore(");
        Assert.Contains("SrcRegID = srcReg", createStore,
            StringComparison.Ordinal);
        Assert.DoesNotContain("BaseRegID =", createStore,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TypedFactoryWireExecutionAndRetireSeamsRemainSeparated()
    {
        (InstructionsEnum Opcode, byte Size)[] typedStores =
        [
            (InstructionsEnum.SB, 1),
            (InstructionsEnum.SH, 2),
            (InstructionsEnum.SW, 4),
            (InstructionsEnum.SD, 8),
        ];
        foreach ((InstructionsEnum opcode, byte size) in typedStores)
        {
            var context = new DecoderContext
            {
                OpCode = (uint)opcode,
                Reg2ID = 7,
                Reg3ID = 9,
                MemoryAddress = 0x1000,
                HasMemoryAddress = true,
            };
            StoreMicroOp operation = Assert.IsType<StoreMicroOp>(
                InstructionRegistry.CreateMicroOp(context.OpCode, context));
            Assert.Equal(size, operation.Size);
            Assert.Equal((ushort)9, operation.SrcRegID);
            Assert.Equal((ushort)7, operation.BaseRegID);
            Assert.Equal([9, 7], operation.ReadRegisters);
            Assert.Empty(operation.WriteRegisters);
        }

        foreach (ushort raw in new ushort[] { 0, 31, 32, 255, 65534, 65535 })
        {
            var context = new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.SD,
                Reg2ID = raw,
                Reg3ID = raw,
                MemoryAddress = 0x1000,
                HasMemoryAddress = true,
            };
            StoreMicroOp operation = Assert.IsType<StoreMicroOp>(
                InstructionRegistry.CreateMicroOp(context.OpCode, context));
            Assert.Equal(Expected(raw, raw), operation.ResourceMask);
        }

        string root = FindRepositoryRoot();
        string coreFactory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Core.cs");
        string vectorFactory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Vector.cs");
        string initialization = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Diagnostics", "InstructionRegistry.Initialize.Base.cs");
        string dataflow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "Dataflow",
            "CPU_Core.PipelineExecution.Dataflow.cs");
        string memoryStage = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Stages", "Memory",
            "CPU_Core.PipelineExecution.Memory.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        string production = ReadTree(root, "HybridCPU_ISE");
        string compiler = ReadTree(root, "HybridCPU_Compiler");

        Assert.Equal(1, Count(coreFactory, "new StoreMicroOp"));
        Assert.Contains("SrcRegID = ctx.Reg3ID", coreFactory,
            StringComparison.Ordinal);
        Assert.Contains("BaseRegID = ctx.Reg2ID", coreFactory,
            StringComparison.Ordinal);
        Assert.Equal(4, Count(initialization, "RegisterTypedStoreOp("));
        Assert.Equal(1, Count(vectorFactory,
            "private static void RegisterRetainedAbsoluteStoreOp("));
        Assert.Equal(1, Count(production, "RegisterRetainedAbsoluteStoreOp("));
        Assert.Contains("NormalizeRequiredLegacyMemoryRegister", vectorFactory,
            StringComparison.Ordinal);

        Assert.Contains(
            "if (regID == 0 || regID >= YAKSys_Hybrid_CPU.Core.Registers.RenameMap.ArchRegs)",
            dataflow, StringComparison.Ordinal);
        Assert.Contains("return 0;", dataflow, StringComparison.Ordinal);
        Assert.Contains(
            "GetRegisterValueWithForwarding(consumerThreadId, storeOp.SrcRegID)",
            memoryStage, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetRegisterValueWithForwarding(consumerThreadId, storeOp.BaseRegID)",
            memoryStage, StringComparison.Ordinal);
        Assert.Contains("PrevalidateDeferredStoreEffect", retire,
            StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredScalarStoreCommit", retire,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SrcRegID", retire, StringComparison.Ordinal);
        Assert.DoesNotContain("BaseRegID", retire, StringComparison.Ordinal);
        Assert.Contains("NativeVliwLoadStoreProductionProvider", compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("new StoreMicroOp", compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize(storeMicroOp",
            production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<StoreMicroOp>",
            production, StringComparison.Ordinal);
    }

    private static StoreMicroOp Create(
        ushort sourceRegister,
        ushort baseRegister,
        ulong address)
    {
        var operation = new StoreMicroOp
        {
            SrcRegID = sourceRegister,
            BaseRegID = baseRegister,
            Address = address,
            Size = 8,
            OwnerThreadId = 0,
        };
        operation.InitializeMetadata();
        return operation;
    }

    private static ResourceBitset Expected(
        ushort sourceRegister,
        ushort baseRegister)
    {
        ResourceBitset result =
            ResourceMaskBuilder.ForStore() |
            ResourceMaskBuilder.ForMemoryDomain(0);
        if (sourceRegister != VLIW_Instruction.NoReg)
            result |= ResourceMaskBuilder.ForRegisterRead(sourceRegister);
        if (baseRegister != VLIW_Instruction.NoReg)
            result |= ResourceMaskBuilder.ForRegisterRead(baseRegister);
        return result;
    }

    private static void AssertMutableUshortProperty(PropertyInfo property)
    {
        Assert.Equal(typeof(ushort), property.PropertyType);
        Assert.True(property.GetMethod?.IsPublic);
        Assert.True(property.SetMethod?.IsPublic);
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

    private static string[] FindFilesContaining(
        string root,
        string relativeRoot,
        params string[] excludedFileNames) =>
        Directory.Exists(Path.Combine(root, relativeRoot))
            ? Directory.EnumerateFiles(
                    Path.Combine(root, relativeRoot), "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Where(path => !excludedFileNames.Contains(
                    Path.GetFileName(path),
                    StringComparer.OrdinalIgnoreCase))
                .Where(path => Regex.IsMatch(
                    File.ReadAllText(path),
                    @"\bStoreMicroOp\b",
                    RegexOptions.CultureInvariant))
                .Select(path => Path.GetRelativePath(root, path)
                    .Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray()
            : [];

    private static string ExtractBalanced(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{signature}' was not found.");
        int brace = source.IndexOf('{', start);
        Assert.True(brace > start);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }

        throw new InvalidOperationException($"'{signature}' was not closed.");
    }

    private static string ReadTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs",
                SearchOption.AllDirectories)
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

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset =
            text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
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

    private static readonly string[] ExpectedProductionFiles =
    [
        "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Helpers.Core.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Helpers.Vector.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Safety.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Core/CPU_Core.TestSupport.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Assist/AssistMicroOp.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.Misc.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Admission/MicroOpScheduler.Admission.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/MicroOpScheduler.Infrastructure.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/MicroOpScheduler.ShadowOracle.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Rf06MemoryShadowOracleDifferential.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Stages/Memory/CPU_Core.PipelineExecution.Memory.cs",
        "HybridCPU_ISE/Legacy/CloseToHSL/Core/Decoder/DecodedBundleTransportProjector.cs",
    ];

    private static readonly string[] ExpectedTestFiles =
    [
        "HybridCPU_ISE.Tests/Architecture/Rf06MemoryShadowOracleDifferentialTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf072afExplicitPacketIndexedVectorEligibilityInventoryTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf072iScalarStoreRetryContourTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf072jScalarStoreFallbackDenialTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf072kScalarStoreInvalidSizeTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf072lScalarStoreSpeculativeSuppressionTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf07ExitClosedWorldContourAuditTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf07ExitExecuteFalseOwnerInventoryTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf083eScalarMemoryTransportBlockerAuditTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf083gAuthorizedScalarLoadExactHandoffTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf103SingleLaneScalarLoadMemoryCycleTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf104ExplicitPacketScalarStoreMemoryCycleTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf105SingleLaneScalarStoreMemoryCycleTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf123qLoadMicroOpRegisterMaskInventoryTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf123rLoadMicroOpRegisterMaskValidInputCutoverTests.cs",
        "HybridCPU_ISE.Tests/ArchitectureAndExecution/MicroOpTestHelperTests.cs",
        "HybridCPU_ISE.Tests/ISAModel/ISAModelInOrderEquivalenceTests.cs",
        "HybridCPU_ISE.Tests/MemoryAndRouting/GRLBTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part2.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part4.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part5.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/SlotClassCapacityTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/SlotClassTaxonomyTests.cs",
        "HybridCPU_ISE.Tests/SafetyAndVerification/SafetyMaskTests.cs",
        "HybridCPU_ISE.Tests/SafetyAndVerification/SafetyVerifierTests.cs",
        "HybridCPU_ISE.Tests/SafetyAndVerification/UniversalScoreboardTests.cs",
        "HybridCPU_ISE.Tests/TestHelpers/MicroOpTestHelper.cs",
        "HybridCPU_ISE.Tests/tests/DecoderContextImmediateAbiTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase02InstructionIrTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase03CarrierProjectionOwnerResourceTailTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase08LoadStoreRetireSemanticsTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09DeferredMemoryBoundaryProofTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09LoadStoreMainMemoryBindingSeamTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09LoadStoreMicroOpMemorySubsystemBindingSeamTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09ReplayTokenMainMemoryBindingSeamTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase5LegacyRemovalTests.cs",
        "HybridCPU_ISE.Tests/tests/RetireContractClosureTests.cs",
        "HybridCPU_ISE.Tests/tests/WriteBackFaultOrderingTests.cs",
    ];
}
