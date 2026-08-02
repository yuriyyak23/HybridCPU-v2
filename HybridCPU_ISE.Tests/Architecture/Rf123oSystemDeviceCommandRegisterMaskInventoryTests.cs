using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123oSystemDeviceCommandRegisterMaskInventoryTests
{
    private const string ThisFileName =
        "Rf123oSystemDeviceCommandRegisterMaskInventoryTests.cs";

    [Fact]
    public void PaperDefinesDistinctRolesAliasesPrivateBypassAndOnlyLaterValidCutover()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.6 System-device command architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("The two optional architectural-register roles are distinct",
            paper, StringComparison.Ordinal);
        Assert.Contains("`NoArchReg=255` and the retained runtime `NoReg=65535` alias",
            paper, StringComparison.Ordinal);
        Assert.Contains("fixed accelerator resource zero and the low",
            paper, StringComparison.Ordinal);
        Assert.Contains("exactly one production caller", paper,
            StringComparison.Ordinal);
        Assert.Contains("Reflection can nevertheless invoke it with arbitrary",
            paper, StringComparison.Ordinal);
        Assert.Contains("does not create a universal `DomainId`", paper,
            StringComparison.Ordinal);
        Assert.Contains("later valid-input-only cutover", paper,
            StringComparison.Ordinal);
        Assert.Contains("Changing sentinel",
            paper, StringComparison.Ordinal);
    }


    [Fact]
    public void SourceShapeFreezesListsBaseResourcesFoldOrderSafetyAndSingleCaller()
    {
        string root = FindRepositoryRoot();
        string source = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Lane7Accelerator",
            "SystemDeviceCommandMicroOp.cs");
        string initialize = ExtractMethod(source, "public void InitializeMetadata()");
        string aggregate = ExtractMethod(
            source,
            "private static ResourceBitset BuildResourceMask(");
        string normalizer = ExtractMethod(
            source,
            "private static ushort NormalizeOptionalArchRegister(");

        Assert.Contains("UsesTokenRegister(CommandKind) && TokenRegister != 0",
            initialize, StringComparison.Ordinal);
        Assert.Contains("WriteRegisters = WritesRegister", initialize,
            StringComparison.Ordinal);
        Assert.Contains("Placement.DomainTag,\n                ReadRegisters,\n                WriteRegisters",
            initialize, StringComparison.Ordinal);
        Assert.Contains("PublishExplicitStructuralSafetyMask();", initialize,
            StringComparison.Ordinal);
        Assert.Contains("RefreshAdmissionMetadata(this);", initialize,
            StringComparison.Ordinal);

        Assert.Contains("ownerDomainTag & 0xFUL", aggregate,
            StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForAccelerator(0)", aggregate,
            StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForMemoryDomain(resourceDomainBucket)",
            aggregate, StringComparison.Ordinal);
        Assert.Equal(2, Count(aggregate, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(aggregate,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(aggregate,
            "ResourceMaskBuilder.ForRegisterRead(registerId)"));
        Assert.Equal(1, Count(aggregate,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.Equal(1, Count(aggregate,
            "ResourceMaskBuilder.ForRegisterWrite(registerId)"));
        Assert.True(aggregate.IndexOf("readRegisters.Count", StringComparison.Ordinal) <
                    aggregate.IndexOf("writeRegisters.Count", StringComparison.Ordinal));
        Assert.DoesNotContain("ArchRegId.Create(", aggregate,
            StringComparison.Ordinal);

        Assert.Contains("rawRegister == VLIW_Instruction.NoReg", normalizer,
            StringComparison.Ordinal);
        Assert.Contains("rawRegister == VLIW_Instruction.NoArchReg", normalizer,
            StringComparison.Ordinal);
        Assert.Contains("return 0;", normalizer, StringComparison.Ordinal);
        Assert.Contains("TryNormalizeFlatArchRegId", normalizer,
            StringComparison.Ordinal);
        Assert.Contains("throw new DecodeProjectionFaultException", normalizer,
            StringComparison.Ordinal);

        Assert.Equal(2, Count(source, "BuildResourceMask(\n"));
        Assert.Equal(1, Count(source, "ResourceMask = BuildResourceMask("));
    }

    [Fact]
    public void EveryUshortPreservesDestinationAndTokenConstructionBehavior()
    {
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            bool sentinel =
                value == VLIW_Instruction.NoArchReg ||
                value == VLIW_Instruction.NoReg;
            bool accepted = value <= ArchRegId.MaxValue || sentinel;

            if (!accepted)
            {
                Assert.Throws<DecodeProjectionFaultException>(
                    () => new AcceleratorQueryCapsMicroOp(value));
                Assert.Throws<DecodeProjectionFaultException>(
                    () => new AcceleratorPollMicroOp(0, value));
                continue;
            }

            ushort normalized = sentinel ? (ushort)0 : value;
            var destination = new AcceleratorQueryCapsMicroOp(value);
            Assert.Equal(normalized, destination.DestinationRegister);
            Assert.Empty(destination.ReadRegisters);
            Assert.Equal(
                normalized == 0 ? Array.Empty<int>() : [(int)normalized],
                destination.WriteRegisters);
            Assert.Equal(
                BaseMask(0) |
                (normalized == 0
                    ? ResourceBitset.Zero
                    : ResourceMaskBuilder.ForRegisterWrite(normalized)),
                destination.ResourceMask);
            AssertMetadataSnapshots(destination);

            var token = new AcceleratorPollMicroOp(0, value);
            Assert.Equal(normalized, token.TokenRegister);
            Assert.Equal(
                normalized == 0 ? Array.Empty<int>() : [(int)normalized],
                token.ReadRegisters);
            Assert.Empty(token.WriteRegisters);
            Assert.Equal(
                BaseMask(0) |
                (normalized == 0
                    ? ResourceBitset.Zero
                    : ResourceMaskBuilder.ForRegisterRead(normalized)),
                token.ResourceMask);
            AssertMetadataSnapshots(token);
        }
    }

    [Fact]
    public void SevenKindsRolesDomainBucketAndInheritedMutationSeamsRemainExplicit()
    {
        SystemDeviceCommandMicroOp[] operations =
        [
            new AcceleratorQueryCapsMicroOp(9),
            new AcceleratorSubmitMicroOp(9),
            new AcceleratorPollMicroOp(9, 7),
            new AcceleratorStatusMicroOp(9, 7),
            new AcceleratorWaitMicroOp(9, 7),
            new AcceleratorCancelMicroOp(9, 7),
            new AcceleratorFenceMicroOp(9, 7),
        ];

        foreach (SystemDeviceCommandMicroOp operation in operations)
        {
            bool readsToken = operation.CommandKind is
                SystemDeviceCommandKind.Poll or
                SystemDeviceCommandKind.Status or
                SystemDeviceCommandKind.Wait or
                SystemDeviceCommandKind.Cancel or
                SystemDeviceCommandKind.Fence;
            Assert.Equal(readsToken ? [7] : Array.Empty<int>(),
                operation.ReadRegisters);
            Assert.Equal([9], operation.WriteRegisters);
            Assert.Equal(
                BaseMask(0) |
                (readsToken
                    ? ResourceMaskBuilder.ForRegisterRead(7)
                    : ResourceBitset.Zero) |
                ResourceMaskBuilder.ForRegisterWrite(9),
                operation.ResourceMask);
            Assert.Equal(SlotClass.SystemSingleton,
                operation.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned,
                operation.Placement.PinningKind);
            Assert.Equal((byte)7, operation.Placement.PinnedLaneId);
            Assert.False(operation.IsStealable);
            AssertMetadataSnapshots(operation);
        }

        var domain = new AcceleratorPollMicroOp(9, 7);
        domain.Placement = domain.Placement with { DomainTag = 0xABCDEUL };
        domain.InitializeMetadata();
        Assert.Equal(
            BaseMask(0xE) |
            ResourceMaskBuilder.ForRegisterRead(7) |
            ResourceMaskBuilder.ForRegisterWrite(9),
            domain.ResourceMask);

        var writeMutation = new AcceleratorQueryCapsMicroOp(9);
        writeMutation.WritesRegister = false;
        writeMutation.DestRegID = 31;
        writeMutation.InitializeMetadata();
        Assert.Empty(writeMutation.WriteRegisters);
        Assert.Equal((ushort)9, writeMutation.DestinationRegister);
        Assert.Equal((ushort)31, writeMutation.DestRegID);

        var zeroWriteMutation = new AcceleratorQueryCapsMicroOp();
        zeroWriteMutation.WritesRegister = true;
        zeroWriteMutation.InitializeMetadata();
        Assert.Equal([0], zeroWriteMutation.WriteRegisters);
        Assert.Equal(BaseMask(0) | ResourceMaskBuilder.ForRegisterWrite(0),
            zeroWriteMutation.ResourceMask);

        var exposedArray = new AcceleratorPollMicroOp(0, 7);
        int[] readArray = Assert.IsType<int[]>(exposedArray.ReadRegisters);
        readArray[0] = 31;
        Assert.Equal(31, exposedArray.ReadRegisters[0]);
        Assert.Equal(31, exposedArray.AdmissionMetadata.ReadRegisters[0]);
        Assert.Equal(
            MicroOpAdmissionMetadata.BuildRegisterHazardMask([7], []),
            exposedArray.AdmissionMetadata.RegisterHazardMask);
        exposedArray.InitializeMetadata();
        Assert.Equal([7], exposedArray.ReadRegisters);
    }

    [Fact]
    public void PrivateReflectionRawListsRetainRawArithmeticClampAndNullFailure()
    {
        MethodInfo aggregate = typeof(SystemDeviceCommandMicroOp).GetMethod(
            "BuildResourceMask",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                nameof(SystemDeviceCommandMicroOp), "BuildResourceMask");
        Assert.True(aggregate.IsPrivate);
        Assert.Equal(typeof(ResourceBitset), aggregate.ReturnType);
        Assert.Equal(
            [typeof(ulong), typeof(IReadOnlyList<int>), typeof(IReadOnlyList<int>)],
            aggregate.GetParameters().Select(parameter => parameter.ParameterType));

        int[] reads = [-4, -1, 64, int.MaxValue];
        int[] writes = [-4, -1, 64, int.MaxValue];
        var reflected = Assert.IsType<ResourceBitset>(aggregate.Invoke(
            null, [0x21UL, reads, writes]));
        ResourceBitset expected = BaseMask(1);
        foreach (int value in reads)
            expected |= ResourceMaskBuilder.ForRegisterRead(value);
        foreach (int value in writes)
            expected |= ResourceMaskBuilder.ForRegisterWrite(value);
        Assert.Equal(expected, reflected);

        TargetInvocationException nullRead = Assert.Throws<TargetInvocationException>(
            () => aggregate.Invoke(
                null,
                [0UL, null, Array.Empty<int>()]));
        Assert.IsType<NullReferenceException>(nullRead.InnerException);

        TargetInvocationException nullWrite = Assert.Throws<TargetInvocationException>(
            () => aggregate.Invoke(
                null,
                [0UL, Array.Empty<int>(), null]));
        Assert.IsType<NullReferenceException>(nullWrite.InnerException);
    }

    [Fact]
    public void WireCompilerReplayTelemetryReflectionAndTestSupportSeamsRemainExplicit()
    {
        var valid = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.ACCEL_STATUS,
            Reg1ID = 9,
            Reg2ID = 7,
            Reg3ID = 0,
        };
        var operation = Assert.IsType<AcceleratorStatusMicroOp>(
            InstructionRegistry.CreateMicroOp(valid.OpCode, valid));
        Assert.Equal([7], operation.ReadRegisters);
        Assert.Equal([9], operation.WriteRegisters);

        DecoderContext invalidRegister = valid;
        invalidRegister.Reg2ID = 32;
        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                invalidRegister.OpCode, invalidRegister));

        DecoderContext invalidUnusedField = valid;
        invalidUnusedField.Reg3ID = 1;
        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                invalidUnusedField.OpCode, invalidUnusedField));

        DecoderContext absent = valid;
        absent.Reg1ID = VLIW_Instruction.NoArchReg;
        absent.Reg2ID = VLIW_Instruction.NoReg;
        var suppressed = Assert.IsType<AcceleratorStatusMicroOp>(
            InstructionRegistry.CreateMicroOp(absent.OpCode, absent));
        Assert.Equal((ushort)0, suppressed.DestinationRegister);
        Assert.Equal((ushort)0, suppressed.TokenRegister);
        Assert.Empty(suppressed.ReadRegisters);
        Assert.Empty(suppressed.WriteRegisters);

        string root = FindRepositoryRoot();
        string production = ReadTree(root, "HybridCPU_ISE");
        string tests = ReadTree(
            root, "HybridCPU_ISE.Tests", excludedFileNames: [ThisFileName]);
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string compiler = ReadTree(root, "HybridCPU_Compiler");
        string assembler = ReadTree(root, "TestAssemblerConsoleApps");
        string replay = ReadTree(
            root,
            "HybridCPU_ISE",
            requiredPathFragment:
                $"{Path.DirectorySeparatorChar}Replay{Path.DirectorySeparatorChar}");

        Assert.DoesNotContain("GetMethod(\"BuildResourceMask\"", tests,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SystemDeviceCommandMicroOp).GetProperty", tests,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SystemDeviceCommandMicroOp).GetField", tests,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SystemDeviceCommandMicroOp", testSupport,
            StringComparison.Ordinal);
        Assert.Contains("op.Placement = op.Placement with { DomainTag = domainTag }",
            assembler, StringComparison.Ordinal);
        Assert.Contains("TokenDestinationRegister > ArchRegId.MaxValue",
            compiler, StringComparison.Ordinal);
        Assert.Contains("byte TokenDestinationRegister", compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SystemDeviceCommandMicroOp", replay,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonSerializer.Deserialize<SystemDeviceCommandMicroOp>",
            production, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonSerializer.Serialize(systemDeviceCommand",
            production, StringComparison.Ordinal);
    }

    private static PropertyInfo RequiredProperty(string name) =>
        typeof(SystemDeviceCommandMicroOp).GetProperty(name)
        ?? throw new MissingMemberException(nameof(SystemDeviceCommandMicroOp), name);

    private static void AssertMetadataSnapshots(
        SystemDeviceCommandMicroOp operation)
    {
        Assert.Equal(operation.ReadRegisters,
            operation.AdmissionMetadata.ReadRegisters);
        Assert.Equal(operation.WriteRegisters,
            operation.AdmissionMetadata.WriteRegisters);
        Assert.Equal(operation.ResourceMask.Low, operation.SafetyMask.Low);
        Assert.True(operation.SafetyMask.High != 0);
        Assert.Equal(operation.Placement,
            operation.AdmissionMetadata.Placement);
    }

    private static ResourceBitset BaseMask(ulong ownerDomainTag) =>
        ResourceMaskBuilder.ForAccelerator(0) |
        ResourceMaskBuilder.ForMemoryDomain((int)(ownerDomainTag & 0xFUL));

    private static string[] FindFilesContainingAny(
        string root,
        string relativeRoot,
        IReadOnlyList<string> values,
        params string[] excludedFileNames) =>
        Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !excludedFileNames.Contains(
                Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .Where(path =>
            {
                string text = File.ReadAllText(path);
                return values.Any(value => text.Contains(value, StringComparison.Ordinal));
            })
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static string ReadTree(
        string root,
        string relativeRoot,
        IReadOnlyCollection<string>? excludedFileNames = null,
        string? requiredPathFragment = null) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => excludedFileNames is null ||
                           !excludedFileNames.Contains(
                               Path.GetFileName(path),
                               StringComparer.OrdinalIgnoreCase))
            .Where(path => requiredPathFragment is null ||
                           path.Contains(
                               requiredPathFragment,
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

        throw new InvalidOperationException(
            $"Method '{signature}' has no closing brace.");
    }

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

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

    private static readonly Type[] ExpectedConcreteTypes =
    [
        typeof(AcceleratorCancelMicroOp),
        typeof(AcceleratorFenceMicroOp),
        typeof(AcceleratorPollMicroOp),
        typeof(AcceleratorQueryCapsMicroOp),
        typeof(AcceleratorStatusMicroOp),
        typeof(AcceleratorSubmitMicroOp),
        typeof(AcceleratorWaitMicroOp),
    ];

    private static readonly Type[] ExpectedTokenTypes =
    [
        typeof(AcceleratorCancelMicroOp),
        typeof(AcceleratorFenceMicroOp),
        typeof(AcceleratorPollMicroOp),
        typeof(AcceleratorStatusMicroOp),
        typeof(AcceleratorWaitMicroOp),
    ];

    private static readonly string[] CarrierSymbols =
    [
        "SystemDeviceCommandMicroOp",
        "AcceleratorCancelMicroOp",
        "AcceleratorFenceMicroOp",
        "AcceleratorPollMicroOp",
        "AcceleratorQueryCapsMicroOp",
        "AcceleratorStatusMicroOp",
        "AcceleratorSubmitMicroOp",
        "AcceleratorWaitMicroOp",
    ];

    private static readonly string[] ExpectedProductionFiles =
    [
        "HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06SpecializedCapabilityProjection.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Initialize.Base.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorRegisterAbi.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs",
        "HybridCPU_ISE/Legacy/CloseToHSL/Core/Decoder/DecodedBundleTransportProjector.cs",
    ];

    private static readonly string[] ExpectedTestFiles =
    [
        "HybridCPU_ISE.Tests/Architecture/Rf06SpecializedCapabilityProjectionTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084aaAcceleratorRegisterAbiSourceBlockerAuditTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084aqAcceleratorCommitApprovedResidualExclusionTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084atAcceleratorRegisterAbiApprovedResidualExclusionTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084bbConsolidatedExitEvidenceTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084sAcceleratorCommitProtocolBlockerAuditTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084tExitReadinessClosedWorldReconciliationTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf123pSystemDeviceCommandRegisterMaskValidInputCutoverTests.cs",
        "HybridCPU_ISE.Tests/CompilerTests/CompilerBackendLoweringPhase11Tests.cs",
        "HybridCPU_ISE.Tests/CompilerTests/L7SdcCompilerPhase12Tests.cs",
        "HybridCPU_ISE.Tests/VmxRefactoring/VmxMemoryIoLaneStreamBoundaryHardeningTests.cs",
        "HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs",
        "HybridCPU_ISE.Tests/VmxRefactoring/VmxVmcsShadowManagerEvidenceContracts.cs",
        "HybridCPU_ISE.Tests/tests/AddressingBackendResolverPhase06Tests.cs",
        "HybridCPU_ISE.Tests/tests/CachePrefetchNonCoherentPhase09Tests.cs",
        "HybridCPU_ISE.Tests/tests/DmaStreamComputeAllOrNonePhase08Tests.cs",
        "HybridCPU_ISE.Tests/tests/DmaStreamComputeDsc2Phase07Tests.cs",
        "HybridCPU_ISE.Tests/tests/Ex1Phase12ConformanceMigrationTests.cs",
        "HybridCPU_ISE.Tests/tests/GlobalMemoryConflictServicePhase05Tests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcBackendTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcHardPinnedPlacementTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcInstructionTransportSidebandTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcLane7PressureTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcNativeCarrierValidationTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcNoBranchControlAuthorityTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcOpcodeSurfaceTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcOwnerDomainGuardTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcPhase08AStatusTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcPhase08ExecutableTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcPhase10GateTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcPollWaitCancelFenceTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcRegisterAbiTests.cs",
        "HybridCPU_ISE.Tests/tests/L7SdcTokenHandleIsNotAuthorityTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase00InstructionInventoryTests.cs",
    ];
}
