using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123qLoadMicroOpRegisterMaskInventoryTests
{
    private const string ThisFileName =
        "Rf123qLoadMicroOpRegisterMaskInventoryTests.cs";

    [Fact]
    public void PaperDefinesRolesRawBypassOwnersAndOnlyLaterValidCutover()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.7 Scalar-load architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("The two architectural-register roles are distinct",
            paper, StringComparison.Ordinal);
        Assert.Contains("none aliases to absence or zero", paper,
            StringComparison.Ordinal);
        Assert.Contains("does not clear a previously published write list", paper,
            StringComparison.Ordinal);
        Assert.Contains("raw compatibility bypass", paper,
            StringComparison.Ordinal);
        Assert.Contains("does not create a universal `DomainId`", paper,
            StringComparison.Ordinal);
        Assert.Contains("destination outside\n`0..31` is rejected by retire prevalidation",
            paper, StringComparison.Ordinal);
        Assert.Contains("later valid-input-only cutover", paper,
            StringComparison.Ordinal);
        Assert.Contains("`StoreMicroOp` and other memory/vector/system", paper,
            StringComparison.Ordinal);
    }


    [Fact]
    public void SourceShapeFreezesSentinelListsRawFoldsAndPublicationOrder()
    {
        string root = FindRepositoryRoot();
        string source = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");
        string initialize = ExtractMethod(
            source, "public void InitializeMetadata()");

        Assert.Contains("const ushort noReg = VLIW_Instruction.NoReg;",
            initialize, StringComparison.Ordinal);
        Assert.Equal(2, Count(initialize, "BaseRegID != noReg"));
        Assert.Equal(2, Count(initialize,
            "WritesRegister && DestRegID != noReg"));
        Assert.Contains("ReadRegisters = new[] { (int)BaseRegID };",
            initialize, StringComparison.Ordinal);
        Assert.Contains("ReadRegisters = Array.Empty<int>();", initialize,
            StringComparison.Ordinal);
        Assert.Contains("WriteRegisters = new[] { (int)DestRegID };",
            initialize, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteRegisters = Array.Empty<int>();",
            initialize, StringComparison.Ordinal);
        Assert.Contains("ReadMemoryRanges = new[] { (Address, (ulong)Size) };",
            initialize, StringComparison.Ordinal);
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(BaseRegID)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)"));
        Assert.Equal(2, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(baseRegister)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite(destinationRegister)"));
        Assert.DoesNotContain("ArchRegId.Create(", initialize,
            StringComparison.Ordinal);

        AssertOrdered(initialize,
            "ResourceMask = ResourceBitset.Zero;",
            "ArchRegId.TryCreate(BaseRegID, out ArchRegId baseRegister)",
            "ResourceMaskBuilder.ForRegisterRead(BaseRegID)",
            "ArchRegId.TryCreate(DestRegID, out ArchRegId destinationRegister)",
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)",
            "ResourceMaskBuilder.ForLoad()",
            "ResourceMaskBuilder.ForMemoryDomain(OwnerThreadId)",
            "PublishExplicitStructuralSafetyMask();",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void EveryUshortPreservesRawBaseAndDestinationBehavior()
    {
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            bool present = value != VLIW_Instruction.NoReg;
            var operation = new LoadMicroOp
            {
                BaseRegID = value,
                DestRegID = value,
                WritesRegister = true,
                Address = 0x1000,
                Size = 8,
                OwnerThreadId = 0,
            };

            operation.InitializeMetadata();

            Assert.Equal(present ? [(int)value] : Array.Empty<int>(),
                operation.ReadRegisters);
            Assert.Equal(present ? [(int)value] : Array.Empty<int>(),
                operation.WriteRegisters);
            Assert.Equal([(0x1000UL, 8UL)], operation.ReadMemoryRanges);

            ResourceBitset expected =
                ResourceMaskBuilder.ForLoad() |
                ResourceMaskBuilder.ForMemoryDomain(0);
            if (present)
            {
                expected |= ResourceMaskBuilder.ForRegisterRead(value);
                expected |= ResourceMaskBuilder.ForRegisterWrite(value);
            }

            Assert.Equal(expected, operation.ResourceMask);
            Assert.Equal(expected.Low, operation.SafetyMask.Low);
            Assert.Equal(operation.ReadRegisters,
                operation.AdmissionMetadata.ReadRegisters);
            Assert.Equal(operation.WriteRegisters,
                operation.AdmissionMetadata.WriteRegisters);
        }
    }

    [Fact]
    public void RefreshMutationReflectionAndTestSupportBypassesRemainExplicit()
    {
        var operation = new LoadMicroOp
        {
            BaseRegID = 7,
            DestRegID = 9,
            WritesRegister = true,
            Address = 0xFFFF000000000000UL,
            Size = 8,
        };
        operation.InitializeMetadata();
        Assert.False(operation.IsStealable);
        Assert.Equal([9], operation.WriteRegisters);

        operation.WritesRegister = false;
        operation.BaseRegID = VLIW_Instruction.NoReg;
        operation.Address = 0x1000;
        operation.InitializeMetadata();
        Assert.Empty(operation.ReadRegisters);
        Assert.Equal([9], operation.WriteRegisters);
        Assert.False(operation.IsStealable);
        Assert.Equal(
            ResourceMaskBuilder.ForLoad() |
            ResourceMaskBuilder.ForMemoryDomain(0),
            operation.ResourceMask);

        var exposed = new LoadMicroOp
        {
            BaseRegID = 7,
            DestRegID = 9,
            WritesRegister = true,
        };
        exposed.InitializeMetadata();
        int[] reads = Assert.IsType<int[]>(exposed.ReadRegisters);
        reads[0] = 31;
        Assert.Equal(31, exposed.AdmissionMetadata.ReadRegisters[0]);
        Assert.Equal(
            MicroOpAdmissionMetadata.BuildRegisterHazardMask([7], [9]),
            exposed.AdmissionMetadata.RegisterHazardMask);

        PropertyInfo baseProperty =
            typeof(LoadMicroOp).GetProperty(nameof(LoadMicroOp.BaseRegID))
            ?? throw new MissingMemberException();
        baseProperty.SetValue(exposed, (ushort)(ushort.MaxValue - 1));
        exposed.InitializeMetadata();
        Assert.Equal([ushort.MaxValue - 1], exposed.ReadRegisters);

        string root = FindRepositoryRoot();
        string coreTestSupport = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string helper = Read(root, "HybridCPU_ISE.Tests", "TestHelpers",
            "MicroOpTestHelper.cs");
        Assert.Contains("DestRegID = destRegId", coreTestSupport,
            StringComparison.Ordinal);
        Assert.Contains("BaseRegID = 1", coreTestSupport,
            StringComparison.Ordinal);
        Assert.Contains("DestRegID = destReg", helper,
            StringComparison.Ordinal);
        Assert.DoesNotContain("BaseRegID =", ExtractMethod(
            helper, "public static LoadMicroOp CreateLoad("),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DecoderCompilerRetireReplayAndBankSeamsRemainSeparated()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.LD,
            Reg1ID = 9,
            Reg2ID = 7,
            MemoryAddress = 0x1000,
            HasMemoryAddress = true,
        };
        var canonical = Assert.IsType<LoadMicroOp>(
            InstructionRegistry.CreateMicroOp(context.OpCode, context));
        Assert.True(canonical.WritesRegister);
        Assert.Equal([7], canonical.ReadRegisters);
        Assert.Equal([9], canonical.WriteRegisters);

        DecoderContext rawBase = context;
        rawBase.Reg2ID = 32;
        var bypassedBase = Assert.IsType<LoadMicroOp>(
            InstructionRegistry.CreateMicroOp(rawBase.OpCode, rawBase));
        Assert.Equal([32], bypassedBase.ReadRegisters);

        DecoderContext rawDestination = context;
        rawDestination.Reg1ID = ArchRegisterTripletEncoding.NoArchReg;
        var bypassedDestination = Assert.IsType<LoadMicroOp>(
            InstructionRegistry.CreateMicroOp(
                rawDestination.OpCode, rawDestination));
        Assert.Equal([ArchRegisterTripletEncoding.NoArchReg],
            bypassedDestination.WriteRegisters);

        DecoderContext absentDestination = context;
        absentDestination.Reg1ID = VLIW_Instruction.NoReg;
        Assert.Throws<InvalidOperationException>(() =>
            InstructionRegistry.CreateMicroOp(
                absentDestination.OpCode, absentDestination));

        string root = FindRepositoryRoot();
        string coreFactory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Core.cs");
        string vectorFactory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Vector.cs");
        string runtime = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Runtime.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "Registers", "Retire", "RetireCoordinator.cs");
        string compiler = ReadTree(root, "HybridCPU_Compiler");
        string production = ReadTree(root, "HybridCPU_ISE");

        Assert.Equal(1, Count(coreFactory, "new LoadMicroOp"));
        Assert.Contains("DestRegID = ctx.Reg1ID", coreFactory,
            StringComparison.Ordinal);
        Assert.Contains("BaseRegID = ctx.Reg2ID", coreFactory,
            StringComparison.Ordinal);
        Assert.Contains("WritesRegister = true", coreFactory,
            StringComparison.Ordinal);
        Assert.Equal(1, Count(vectorFactory,
            "private static void RegisterRetainedAbsoluteLoadOp("));
        Assert.Equal(1, Count(production,
            "RegisterRetainedAbsoluteLoadOp("));
        Assert.Contains("NormalizeRequiredLegacyMemoryRegister", vectorFactory,
            StringComparison.Ordinal);
        Assert.Contains("microOp.RefreshWriteMetadata();", runtime,
            StringComparison.Ordinal);
        Assert.Contains("(uint)record.ArchReg >= (uint)RenameMap.ArchRegs",
            retire, StringComparison.Ordinal);
        Assert.Contains("if (record.ArchReg == 0)", retire,
            StringComparison.Ordinal);
        Assert.Contains("NativeVliwLoadStoreProductionProvider", compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("new LoadMicroOp", compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonSerializer.Serialize(loadMicroOp", production,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonSerializer.Deserialize<LoadMicroOp>", production,
            StringComparison.Ordinal);
        Assert.Contains("MemoryBankId =>", Read(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Memory",
            "MicroOp.LoadStore.cs"), StringComparison.Ordinal);
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
                    @"\bLoadMicroOp\b",
                    RegexOptions.CultureInvariant))
                .Select(path => Path.GetRelativePath(root, path)
                    .Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray()
            : [];

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

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method '{signature}' was not found.");
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

        throw new InvalidOperationException(
            $"Method '{signature}' has no closing brace.");
    }

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
        "HybridCPU_ISE.Tests/Architecture/Rf072gScalarLoadRetryContourTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf072hScalarLoadFallbackDenialTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf072oScalarLoadSpeculativeSuppressionTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf07ExitClosedWorldContourAuditTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf07ExitExecuteFalseOwnerInventoryTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf083eScalarMemoryTransportBlockerAuditTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf083gAuthorizedScalarLoadExactHandoffTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf083nScalarLoadTypedStageBTopologyBlockerTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084bbConsolidatedExitEvidenceTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf08Rf09DocumentationCurrentStateTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf101MemoryCycleAuthorityDecisionTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf103SingleLaneScalarLoadMemoryCycleTests.cs",
        "HybridCPU_ISE.Tests/ArchitectureAndExecution/MicroOpTestHelperTests.cs",
        "HybridCPU_ISE.Tests/ISAModel/ISAModelInOrderEquivalenceTests.cs",
        "HybridCPU_ISE.Tests/MemoryAndRouting/GRLBTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part2.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part3.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part4.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part5.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part6.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/Phase2RefactoringTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/Phase9NominationRefactoringTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/SlotClassCapacityTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/SlotClassTaxonomyTests.cs",
        "HybridCPU_ISE.Tests/SafetyAndVerification/DynamicGRLBMemoryIntegrationTests.cs",
        "HybridCPU_ISE.Tests/SafetyAndVerification/SafetyMaskTests.cs",
        "HybridCPU_ISE.Tests/SafetyAndVerification/SafetyVerifierTests.cs",
        "HybridCPU_ISE.Tests/SafetyAndVerification/UniversalScoreboardTests.cs",
        "HybridCPU_ISE.Tests/TestHelpers/MicroOpTestHelper.cs",
        "HybridCPU_ISE.Tests/tests/CanonicalStructuralSafetyMaskTests.cs",
        "HybridCPU_ISE.Tests/tests/DecodeIssueCarrierRuntimeFactsTests.cs",
        "HybridCPU_ISE.Tests/tests/DecoderContextImmediateAbiTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase02InstructionIrTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase03CarrierProjectionOwnerResourceTailTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase03DecodeBankPendingStallTailTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase03SlotMetaPolicyTailTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase08LoadStoreRetireSemanticsTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09CanonicalDecodePublicationContractTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09DeferredMemoryBoundaryProofTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09LoadStoreMainMemoryBindingSeamTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09LoadStoreMicroOpMemorySubsystemBindingSeamTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09StateAccessorSnapshotTruthTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase5LegacyRemovalTests.cs",
        "HybridCPU_ISE.Tests/tests/RetireContractClosureTests.cs",
        "HybridCPU_ISE.Tests/tests/SingleLaneDispatchMshrReservationTests.cs",
        "HybridCPU_ISE.Tests/tests/SingleLaneMemoryTransferLatchTests.cs",
        "HybridCPU_ISE.Tests/tests/SingleLaneWriteBackTransferLatchTests.cs",
    ];
}
