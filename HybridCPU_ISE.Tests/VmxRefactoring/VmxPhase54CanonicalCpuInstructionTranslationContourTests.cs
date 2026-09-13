using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase54CanonicalCpuInstructionTranslationContourTests
{
    private static readonly CpuInstructionTranslationScope Scope = new(7, 3, 0);

    [Fact]
    public void PlatformContext_DefaultsToIdentityWithoutEnablingTranslationMode()
    {
        var memory = CreateMemory();
        CpuCorePlatformContext context =
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation);
        var core = new Processor.CPU_Core(0, context);
        core.PrepareExecutionStart(0, activeVtId: 0);

        Assert.Same(CpuInstructionTranslationPolicy.Identity, context.CpuInstructionTranslationPolicy);
        core.ExecutePipelineCycle();

        Processor.CPU_Core.FetchStage fetch = core.GetFetchStage();
        Assert.True(fetch.Valid);
        Assert.Equal(0UL, fetch.PC);
    }

    [Fact]
    public void ProductionConstructionPath_IsExplicitContextThenCoreLifecycleReplacement()
    {
        string context = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/State/CpuCorePlatformContext.cs");
        string constructor = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Architecture/State/Architectural/CPU_Core.StateData.cs");
        string lifecycle = ActiveVmxConformanceHelpers.ReadProjectSource(
            "NonRTL/Processor/Core/Processor.CoreIdentity.cs");

        Assert.Contains("cpuInstructionTranslationPolicy ?? CpuInstructionTranslationPolicy.Identity", context);
        Assert.Contains("public CPU_Core(ushort CoreID, CpuCorePlatformContext platformContext)", constructor);
        Assert.Contains("new Core.Memory.CpuInstructionTranslationOwner(", constructor);
        Assert.Contains("platformContext.CpuInstructionTranslationPolicy", constructor);
        Assert.Contains("public static void ReplaceCore(int coreId, CPU_Core replacement)", lifecycle);
    }

    [Fact]
    public void ProductionFetch_UsesMappedPhysicalAddressWhileKeepingVirtualPc()
    {
        var memory = CreateMemory();
        byte[] physicalBundle = new byte[256];
        physicalBundle[0] = 0x5A;
        Assert.True(memory.TryWritePhysicalRange(0x200, physicalBundle));
        Processor.CPU_Core core = CreateMappedCore(
            memory,
            new CpuInstructionTranslationRegion(
                0x1000, 0x200, 0x400, Readable: true, Writable: true, Executable: true));
        core.PrepareExecutionStart(0x1000, activeVtId: 0);

        core.ExecutePipelineCycle();

        Processor.CPU_Core.FetchStage fetch = core.GetFetchStage();
        Assert.True(fetch.Valid);
        Assert.Equal(0x1000UL, fetch.PC);
        Assert.NotNull(fetch.VLIWBundle);
        Assert.Equal(0x5A, fetch.VLIWBundle![0]);
    }

    [Fact]
    public void ProductionFetchFault_CommitsTypedNeutralFactsAndPreservesPresentZero()
    {
        var memory = CreateMemory();
        Processor.CPU_Core core = CreateMappedCore(
            memory,
            new CpuInstructionTranslationRegion(
                0x1000, 0, 0x400, Readable: true, Writable: true, Executable: true));
        core.PrepareExecutionStart(0, activeVtId: 0);

        CpuInstructionTranslationFaultException exception =
            Assert.Throws<CpuInstructionTranslationFaultException>(core.ExecutePipelineCycle);

        Assert.Equal(CpuInstructionTranslationFaultReason.UnmappedAddress, exception.Fault.Reason);
        Assert.Equal(CpuInstructionTranslationAccessKind.InstructionFetch,
            exception.Fault.Request.AccessKind);
        Assert.Equal(0UL, exception.Fault.Request.VirtualAddress);
        Assert.True(exception.Fault.Request.OperationIdentity.IsValid);

        CompletionObservationResult observed = core.DomainCompletionObservationOwner.Observe(
            new CompletionObservationScope(Scope.DomainId, Scope.ContextId, Scope.VirtualThreadId));
        Assert.True(observed.IsObserved);
        NeutralArchitecturalCompletionFacts facts = observed.Snapshot!.Value.Facts;
        Assert.Equal(NeutralArchitecturalCompletionClass.TranslationFault, facts.CompletionClass);
        Assert.True(facts.FaultAddress.IsPresent);
        Assert.Equal(0UL, facts.FaultAddress.Value);
        Assert.Equal(NeutralFaultAddressSemantic.VirtualAddress, facts.FaultAddress.Semantic);
        Assert.True(facts.Qualification.IsPresent);
        Assert.True(facts.FaultAuxiliary.IsPresent);
        Assert.Equal(NeutralFaultAuxiliarySemantic.TranslationFault,
            facts.FaultAuxiliary.Semantic);
    }

    [Fact]
    public void ScalarLoad_UsesProductionMicroOpTranslationCaller()
    {
        var memory = CreateMemory();
        const ulong expected = 0x1122_3344_5566_7788UL;
        Assert.True(memory.TryWritePhysicalRange(0x280, BitConverter.GetBytes(expected)));
        YAKSys_Hybrid_CPU.Memory.MemorySubsystem? previous = Processor.Memory;
        try
        {
            Processor.Memory = null;
            Processor.CPU_Core core = CreateMappedCore(
                memory,
                new CpuInstructionTranslationRegion(
                    0x2000, 0x200, 0x1000, Readable: true, Writable: true, Executable: false));
            LoadMicroOp load = CreateLoad(0x2080);
            Assert.Null(core.GetBoundMemorySubsystem());
            Assert.Equal(0x4000UL, core.GetBoundMainMemoryLength());
            Assert.Equal((byte)8, load.Size);
            Assert.True(load.Execute(ref core));
            Assert.True(load.TryGetPrimaryWriteBackResult(out ulong value));
            Assert.Equal(expected, value);
            Assert.True(load.TryGetCpuTranslatedAddress(out ulong physical, out ulong epoch));
            Assert.Equal(0x280UL, physical);
            Assert.NotEqual(0UL, epoch);
        }
        finally
        {
            Processor.Memory = previous;
        }
    }

    [Fact]
    public void ScalarStorePermissionFault_IsTypedAndNeverBecomesPageFault()
    {
        var memory = CreateMemory();
        Processor.CPU_Core core = CreateMappedCore(
            memory,
            new CpuInstructionTranslationRegion(
                0x2000, 0x200, 0x1000, Readable: true, Writable: false, Executable: false));
        StoreMicroOp store = CreateStore(0x2080);

        Exception exception = Record.Exception(() => store.Execute(ref core))!;

        CpuInstructionTranslationFaultException typed =
            Assert.IsType<CpuInstructionTranslationFaultException>(exception);
        Assert.Equal(CpuInstructionTranslationFaultReason.AccessDenied, typed.Fault.Reason);
        Assert.Equal(CpuInstructionTranslationAccessKind.ScalarStore,
            typed.Fault.Request.AccessKind);
        byte[] bytes = new byte[8];
        Assert.True(memory.TryReadPhysicalRange(0x280, bytes));
        Assert.Equal(new byte[8], bytes);
    }

    [Fact]
    public void SpeculativeScalarTranslationFault_IsSilentlySquashedWithoutCompletion()
    {
        var memory = CreateMemory();
        Processor.CPU_Core core = CreateMappedCore(
            memory,
            new CpuInstructionTranslationRegion(
                0x2000, 0x200, 0x1000, Readable: true, Writable: true, Executable: false));
        LoadMicroOp load = CreateLoad(0x4000);
        load.MarkSpeculative();

        Assert.False(load.Execute(ref core));
        Assert.True(load.IsSpeculativeFaultSuppressed);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            core.DomainCompletionObservationOwner.Observe(
                new CompletionObservationScope(Scope.DomainId, Scope.ContextId, Scope.VirtualThreadId)).Decision);
    }

    [Fact]
    public void ProductionScalarExecuteFault_UsesStageAwareWinnerAndCommitsNeutralCompletion()
    {
        var memory = CreateMemory();
        Processor.CPU_Core core = CreateMappedCore(
            memory,
            new CpuInstructionTranslationRegion(
                0x2000, 0x200, 0x1000, Readable: true, Writable: true, Executable: false));
        LoadMicroOp load = CreateLoad(0x4000);
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.LD,
            DataTypeValue = DataTypeEnum.UINT64,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 0),
        };

        CpuInstructionTranslationFaultException exception =
            Assert.Throws<CpuInstructionTranslationFaultException>((Action)(() =>
                core.TestRunExecuteStageWithDecodedInstruction(
                    instruction,
                    load,
                    isMemoryOp: true,
                    writesRegister: true,
                    reg1Id: instruction.Reg1ID,
                    reg2Id: instruction.Reg2ID,
                    reg3Id: instruction.Reg3ID,
                    pc: 0x9000)));

        Assert.Equal(CpuInstructionTranslationAccessKind.ScalarLoad,
            exception.Fault.Request.AccessKind);
        Assert.True(exception.Fault.Request.OperationIdentity.IsValid);
        CompletionObservationResult observed = core.DomainCompletionObservationOwner.Observe(
            new CompletionObservationScope(Scope.DomainId, Scope.ContextId, Scope.VirtualThreadId));
        Assert.True(observed.IsObserved);
        Assert.Equal(NeutralArchitecturalCompletionClass.TranslationFault,
            observed.Snapshot!.Value.Facts.CompletionClass);
    }

    [Fact]
    public void StageAwareArbitration_PrefersOlderStageThenLowestLaneAndCarriesTypedFault()
    {
        CpuInstructionTranslationFault younger = CreateFault(
            CpuInstructionTranslationAccessKind.ScalarLoad,
            virtualAddress: 0x4000,
            memoryOperationId: 31);
        CpuInstructionTranslationFault older = CreateFault(
            CpuInstructionTranslationAccessKind.ScalarStore,
            virtualAddress: 0x5000,
            memoryOperationId: 29);
        var execute = new Processor.CPU_Core.ExecuteStage
        {
            Valid = true,
            Lane0 = CreateExecuteFaultLane(0, younger),
        };
        var memory = new Processor.CPU_Core.MemoryStage
        {
            Valid = true,
            Lane4 = CreateMemoryFaultLane(4, younger),
            Lane2 = CreateMemoryFaultLane(2, older),
        };
        var writeBack = new Processor.CPU_Core.WriteBackStage();

        Assert.True(Processor.CPU_Core.TryResolveStageAwareExceptionWinnerMetadata(
            writeBack, memory, execute, out var winner));
        Assert.Equal(Processor.CPU_Core.PipelineStage.Memory, winner.WinnerStage);
        Assert.Equal((byte)2, winner.WinnerLaneIndex);
        Assert.Equal(older, winner.CpuTranslationFault);
        Assert.Equal(0x5000UL, winner.FaultAddress);
    }

    [Fact]
    public void ProductionRestore_RemovesCommittedTranslationObservation()
    {
        var memory = CreateMemory();
        Processor.CPU_Core core = CreateMappedCore(
            memory,
            new CpuInstructionTranslationRegion(
                0x1000, 0, 0x400, Readable: true, Writable: true, Executable: true));
        core.PrepareExecutionStart(0, activeVtId: 0);
        _ = Assert.Throws<CpuInstructionTranslationFaultException>(core.ExecutePipelineCycle);
        var scope = new CompletionObservationScope(Scope.DomainId, Scope.ContextId, Scope.VirtualThreadId);
        Assert.True(core.DomainCompletionObservationOwner.Observe(scope).IsObserved);

        Processor.CPU_Core.VectorContext context = core.SaveVectorContext();
        core.RestoreVectorContext(context);

        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            core.DomainCompletionObservationOwner.Observe(scope).Decision);
    }

    [Fact]
    public void PolicyRejectsOverlapsAndAddressOverflow()
    {
        Assert.Throws<ArgumentException>(() =>
            CpuInstructionTranslationPolicy.CreateBoundedRegions(
                Scope,
                new[]
                {
                    new CpuInstructionTranslationRegion(0x1000, 0, 0x100, true, true, true),
                    new CpuInstructionTranslationRegion(0x1080, 0x200, 0x100, true, true, true),
                }));
        Assert.Throws<ArgumentException>(() =>
            CpuInstructionTranslationPolicy.CreateBoundedRegions(
                Scope,
                new[]
                {
                    new CpuInstructionTranslationRegion(ulong.MaxValue, 0, 2, true, false, false),
                }));
    }

    [Theory]
    [InlineData(8, 0, 0x2080UL, CpuInstructionTranslationFaultReason.OwnerScopeMismatch)]
    [InlineData(3, 0, ulong.MaxValue - 3, CpuInstructionTranslationFaultReason.AddressOverflow)]
    public void ScalarRequest_RejectsCrossOwnerScopeAndRangeOverflow(
        int contextId,
        int virtualThreadId,
        ulong address,
        CpuInstructionTranslationFaultReason expectedReason)
    {
        Processor.CPU_Core core = CreateMappedCore(
            CreateMemory(),
            new CpuInstructionTranslationRegion(
                0x2000, 0x200, 0x1000, Readable: true, Writable: true, Executable: false));
        LoadMicroOp load = CreateLoad(address);
        load.OwnerContextId = contextId;
        load.OwnerThreadId = virtualThreadId;
        load.VirtualThreadId = virtualThreadId;

        CpuInstructionTranslationFaultException exception =
            Assert.Throws<CpuInstructionTranslationFaultException>((Action)(() => load.Execute(ref core)));

        Assert.Equal(expectedReason, exception.Fault.Reason);
        Assert.True(exception.Fault.Request.OperationIdentity.IsValid);
    }

    [Fact]
    public void LaterEvidence_IsNonSelfReferentialAndHashesExactSubjectBytes()
    {
        const string subject = "284ed3e6454bdbc8eabb93e97db97122da13a9da";
        const string tree = "2b9d6c94d19bb41e23337461f8bfab10745be236";
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string evidencePath = Path.Combine(
            root,
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan", "evidence",
            "2026-08-13-phase54-canonical-neutral-cpu-instruction-translation-clean-evidence.json");
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(evidencePath));
        JsonElement record = evidence.RootElement;

        Assert.True(record.GetProperty("non_self_referential").GetBoolean());
        Assert.Equal(subject,
            record.GetProperty("implementation_subject").GetProperty("commit_sha").GetString());
        Assert.Equal(tree, GitText(root, "rev-parse", $"{subject}^{{tree}}"));
        Assert.False(record.GetProperty("completion_backed_vmread_opened").GetBoolean());

        foreach (JsonProperty source in record
            .GetProperty("source_hashes_sha256_clean_subject_bytes")
            .EnumerateObject())
        {
            byte[] bytes = GitBytes(root, "cat-file", "blob", $"{subject}:{source.Name}");
            Assert.Equal(
                source.Value.GetString(),
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }

    private static Processor.CPU_Core CreateMappedCore(
        Processor.MainMemoryArea memory,
        params CpuInstructionTranslationRegion[] regions)
    {
        CpuInstructionTranslationPolicy policy =
            CpuInstructionTranslationPolicy.CreateBoundedRegions(Scope, regions);
        return new Processor.CPU_Core(
            0,
            CpuCorePlatformContext.CreateFixed(
                memory,
                ProcessorMode.Emulation,
                cpuInstructionTranslationPolicy: policy));
    }

    private static Processor.MainMemoryArea CreateMemory()
    {
        var memory = new Processor.MultiBankMemoryArea(1, 0x4000);
        memory.SetLength(0x4000);
        return memory;
    }

    private static LoadMicroOp CreateLoad(ulong address)
    {
        var operation = new LoadMicroOp
        {
            Address = address,
            Size = 8,
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.LD,
            DestRegID = 9,
            BaseRegID = 1,
            WritesRegister = true,
            OwnerContextId = Scope.ContextId,
            OwnerThreadId = Scope.VirtualThreadId,
            VirtualThreadId = Scope.VirtualThreadId,
            Placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = SlotClass.LsuClass,
                PinningKind = SlotPinningKind.ClassFlexible,
                DomainTag = Scope.DomainId,
            },
        };
        operation.InitializeMetadata();
        return operation;
    }

    private static StoreMicroOp CreateStore(ulong address)
    {
        var operation = new StoreMicroOp
        {
            Address = address,
            Size = 8,
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SD,
            OwnerContextId = Scope.ContextId,
            OwnerThreadId = Scope.VirtualThreadId,
            VirtualThreadId = Scope.VirtualThreadId,
            Placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = SlotClass.LsuClass,
                PinningKind = SlotPinningKind.ClassFlexible,
                DomainTag = Scope.DomainId,
            },
        };
        operation.InitializeMetadata();
        return operation;
    }

    private static CpuInstructionTranslationFault CreateFault(
        CpuInstructionTranslationAccessKind accessKind,
        ulong virtualAddress,
        ulong memoryOperationId)
    {
        var request = new CpuInstructionTranslationRequest(
            Scope,
            accessKind,
            virtualAddress,
            8,
            new CpuInstructionTranslationOperationIdentity(
                memoryOperationId,
                memoryOperationId,
                memoryOperationId));
        return new CpuInstructionTranslationFault(
            CpuInstructionTranslationFaultReason.UnmappedAddress,
            request,
            OwnerEpoch: 1,
            Qualification: 1,
            Auxiliary: memoryOperationId);
    }

    private static Processor.CPU_Core.ScalarExecuteLaneState CreateExecuteFaultLane(
        byte laneIndex,
        CpuInstructionTranslationFault fault) =>
        new()
        {
            IsOccupied = true,
            LaneIndex = laneIndex,
            HasFault = true,
            FaultAddress = fault.Request.VirtualAddress,
            FaultIsWrite = fault.Request.AccessKind == CpuInstructionTranslationAccessKind.ScalarStore,
            CpuTranslationFault = fault,
        };

    private static Processor.CPU_Core.ScalarMemoryLaneState CreateMemoryFaultLane(
        byte laneIndex,
        CpuInstructionTranslationFault fault) =>
        new()
        {
            IsOccupied = true,
            LaneIndex = laneIndex,
            HasFault = true,
            FaultAddress = fault.Request.VirtualAddress,
            FaultIsWrite = fault.Request.AccessKind == CpuInstructionTranslationAccessKind.ScalarStore,
            CpuTranslationFault = fault,
        };

    private static string GitText(string workingDirectory, params string[] arguments) =>
        System.Text.Encoding.UTF8.GetString(GitBytes(workingDirectory, arguments)).TrimEnd();

    private static byte[] GitBytes(string workingDirectory, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (string argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output.ToArray();
    }
}
