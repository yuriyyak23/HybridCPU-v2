using HybridCPU_ISE.Arch;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase56CanonicalCpuSecondStageTranslationContourTests
{
    private const ulong Domain = 7;
    private const ulong RuntimeAddressSpace = 9;
    private static readonly CpuInstructionTranslationScope Scope = new(Domain, 3, 0);

    [Fact]
    public void GovernanceContract_KeepsCompatibilityClosedUntilSeparateD2()
    {
        Assert.Equal("ExistingCpuInstructionTranslationOwner",
            Phase56CanonicalCpuSecondStageTranslationE0Contract.TranslationOwner);
        Assert.Equal("MemoryDomainRuntimeAndMemoryDomainDescriptorOnly",
            Phase56CanonicalCpuSecondStageTranslationE0Contract.ConfigurationOwner);
        Assert.Equal("CanonicalCpuSecondStageTranslationFaultProducer",
            Phase56CanonicalCpuSecondStageTranslationE0Contract.Producer);
        Assert.False(Phase56CanonicalCpuSecondStageTranslationE0Contract.VmReadOpened);
        Assert.False(Phase56CanonicalCpuSecondStageTranslationE0Contract.VmWriteOpened);
        Assert.False(Phase56CanonicalCpuSecondStageTranslationE0Contract.VmxAuthorityGranted);
        Assert.False(Phase56CanonicalCpuSecondStageTranslationE0Contract.NestedOrIommuAuthorityUsed);

        string currentVmRead = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Events/VmRead/" +
            "CompletionReasonQualificationVmReadScalarDeliveryCanonicalComposition.cs");
        Assert.Contains("field is VmcsField.GuestPhysicalAddress or VmcsField.EptViolationQualification",
            currentVmRead);
        Assert.Contains("GPA and EPT violation qualification lack neutral semantic coverage",
            currentVmRead);
    }

    [Fact]
    public void MachineStatus_ClosesProducerBoundaryAndOpensOnlyExactGpaEptE0D2()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        using JsonDocument status = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "VirtualizationActivationStatusV1.json")));
        JsonElement machine = status.RootElement;
        JsonElement phase56 = machine.GetProperty(
            "Phase56CanonicalNeutralCpuSecondStageTranslationContourAndProducer");

        Assert.Equal("ClosedGreenSubjectAndLaterNonSelfReferentialEvidence",
            phase56.GetProperty("State").GetString());
        Assert.Equal("MemoryDomainRuntimeAndMemoryDomainDescriptorOnly",
            phase56.GetProperty("ConfigurationOwner").GetString());
        Assert.Equal("ExactCanonicalCpuSecondStageTranslationFaultProducer",
            phase56.GetProperty("ProducerRegistration").GetString());
        Assert.Equal("Denied", phase56.GetProperty("VmxVmcsEptAuthority").GetString());
        Assert.Equal("NotOpenedByPhase56", phase56.GetProperty("VmRead").GetString());
        Assert.Equal("None",
            machine.GetProperty("NextOpenPool").GetString());
    }

    [Fact]
    public void LaterEvidence_IsNonSelfReferentialAndHashesExactSubjectBytes()
    {
        const string subject = "eedc5379bf498ed109559faac8d221677d143904";
        const string tree = "e0caf650b2c13dfeddd0de6af534250a75c4e0ab";
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string evidencePath = Path.Combine(
            root,
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan", "evidence",
            "2026-08-13-phase56-canonical-neutral-cpu-second-stage-translation-clean-evidence.json");
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(evidencePath));
        JsonElement record = evidence.RootElement;

        Assert.True(record.GetProperty("non_self_referential").GetBoolean());
        Assert.Equal(subject,
            record.GetProperty("implementation_subject").GetProperty("commit_sha").GetString());
        Assert.Equal(tree, GitText(root, "rev-parse", $"{subject}^{{tree}}"));
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

    [Fact]
    public void DefaultConstruction_RemainsIdentityAndDoesNotCreateMemoryDomainAuthority()
    {
        Processor.MainMemoryArea memory = CreateMemory();
        CpuCorePlatformContext context =
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation);
        var core = new Processor.CPU_Core(0, context);

        Assert.Same(CpuInstructionTranslationPolicy.Identity,
            context.CpuInstructionTranslationPolicy);
        Assert.Equal(CpuInstructionTranslationMode.Identity,
            context.CpuInstructionTranslationPolicy.Mode);
        Assert.Empty(context.CpuInstructionTranslationPolicy.StageOneRegions);
        Assert.Null(context.CpuInstructionTranslationPolicy.SecondStageBinding);
        Assert.NotNull(core);
    }

    [Fact]
    public void TwoStagePolicy_RequiresCanonicalRuntimeOwnedMemoryDomainSource()
    {
        var runtime = new MemoryDomainRuntime();
        var nonAuthoritative = new MemoryDomainDescriptor(
            AddressSpace(),
            new MemoryTranslationPolicy(
                MemoryTranslationAuthority.CompatibilityProjection,
                TranslationInvalidationPermission.None,
                requireFenceBeforeInvalidation: true,
                allowCompatibilityProjection: true),
            TranslationControl(),
            dirtyTracking: null,
            ownsSecondStageTranslation: true);

        InvalidOperationException denied = Assert.Throws<InvalidOperationException>(() =>
            CpuInstructionTranslationPolicy.CreateTwoStageBoundedRegions(
                Scope,
                new[] { StageOneRegion() },
                runtime,
                nonAuthoritative,
                RuntimeAddressSpace));

        Assert.Contains("runtime-owned", denied.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(runtime.CurrentTranslationSource);
    }

    [Fact]
    public void ProductionFetch_UsesExplicitVaToGpaThenNeutralSecondStageToCpuPhysical()
    {
        Processor.MainMemoryArea memory = CreateMemory();
        InstallSecondStageMapping(memory, guestPhysicalPage: 0x3000, cpuPhysicalPage: 0x5000,
            leafPermissions: 0xFUL);
        byte[] bundle = new byte[256];
        bundle[0] = 0x5A;
        Assert.True(memory.TryWritePhysicalRange(0x5000, bundle));
        (Processor.CPU_Core core, _, _) = CreateTwoStageCore(memory);
        core.PrepareExecutionStart(0x1000, activeVtId: 0);

        core.ExecutePipelineCycle();

        Processor.CPU_Core.FetchStage fetch = core.GetFetchStage();
        Assert.True(fetch.Valid);
        Assert.Equal(0x1000UL, fetch.PC);
        Assert.Equal(0x5A, fetch.VLIWBundle![0]);
    }

    [Fact]
    public void ProductionScalarLoad_UsesSameCanonicalTwoStageOwner()
    {
        Processor.MainMemoryArea memory = CreateMemory();
        InstallSecondStageMapping(memory, 0x3000, 0x5000, leafPermissions: 0xFUL);
        const ulong expected = 0x1122_3344_5566_7788UL;
        Assert.True(memory.TryWritePhysicalRange(0x5080, BitConverter.GetBytes(expected)));
        (Processor.CPU_Core core, _, _) = CreateTwoStageCore(memory);
        LoadMicroOp load = CreateLoad(0x1080);

        Assert.True(load.Execute(ref core));
        Assert.True(load.TryGetPrimaryWriteBackResult(out ulong value));
        Assert.Equal(expected, value);
        Assert.True(load.TryGetCpuTranslatedAddress(out ulong physical, out ulong ownerEpoch));
        Assert.Equal(0x5080UL, physical);
        Assert.NotEqual(0UL, ownerEpoch);
    }

    [Fact]
    public void ProductionScalarStore_UsesSameCanonicalTwoStageOwner()
    {
        Processor.MainMemoryArea memory = CreateMemory();
        InstallSecondStageMapping(memory, 0x3000, 0x5000, leafPermissions: 0xFUL);
        (Processor.CPU_Core core, _, _) = CreateTwoStageCore(memory);
        StoreMicroOp store = CreateStore(0x1090, 0xA1B2_C3D4_E5F6_0718UL);

        Assert.True(store.Execute(ref core));

        Span<byte> bytes = stackalloc byte[8];
        Assert.True(memory.TryReadPhysicalRange(0x5090, bytes));
        Assert.Equal(0xA1B2_C3D4_E5F6_0718UL, BitConverter.ToUInt64(bytes));
    }

    [Fact]
    public void SecondStageViolation_CommitsExactGpaNeutralFactsAndFullProvenance()
    {
        Processor.MainMemoryArea memory = CreateMemory();
        InstallSecondStageDirectoryOnly(memory);
        (Processor.CPU_Core core, _, _) = CreateTwoStageCore(memory);
        core.PrepareExecutionStart(0x1000, activeVtId: 0);

        CpuInstructionTranslationFaultException exception =
            Assert.Throws<CpuInstructionTranslationFaultException>(core.ExecutePipelineCycle);

        CpuInstructionTranslationFault fault = exception.Fault;
        Assert.Equal(CpuInstructionTranslationFaultStage.SecondStage, fault.FaultStage);
        Assert.Equal(CpuInstructionTranslationFaultReason.SecondStageViolation, fault.Reason);
        Assert.True(fault.HasGuestPhysicalAddress);
        Assert.Equal(0x3000UL, fault.GuestPhysicalAddress);
        Assert.NotEqual(0UL, fault.MemoryDomainOwnerEpoch);
        Assert.NotEqual(0UL, fault.AddressSpaceGeneration);
        Assert.Equal(RuntimeAddressSpace, fault.AddressSpaceIdentity);

        CompletionObservationResult observed = core.DomainCompletionObservationOwner.Observe(
            new CompletionObservationScope(Domain, Scope.ContextId, Scope.VirtualThreadId));
        Assert.True(observed.IsObserved);
        NeutralCompletionObservationSnapshot snapshot = observed.Snapshot!.Value;
        Assert.Equal(
            core.CanonicalCpuSecondStageTranslationFaultCompletionProducer.OwnerIdentity,
            snapshot.ProducerOwnerIdentity);
        Assert.True(snapshot.Facts.FaultAddress.IsPresent);
        Assert.Equal(0x3000UL, snapshot.Facts.FaultAddress.Value);
        Assert.Equal(NeutralFaultAddressSemantic.GuestPhysicalAddress,
            snapshot.Facts.FaultAddress.Semantic);
        Assert.Equal(NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation,
            snapshot.Facts.FaultAuxiliary.Semantic);
        Assert.True(snapshot.TranslationProvenance.HasSecondStageIdentity);
        Assert.Equal(fault.Request.OperationIdentity.MemoryOperationId,
            snapshot.TranslationProvenance.MemoryOperationId);
        Assert.Equal(fault.OwnerEpoch,
            snapshot.TranslationProvenance.TranslationOwnerEpoch);
        Assert.Equal(fault.AddressSpaceGeneration,
            snapshot.TranslationProvenance.AddressSpaceGeneration);
    }

    [Fact]
    public void PresentZeroGpa_RemainsDistinctFromAbsentAddressFact()
    {
        Processor.MainMemoryArea memory = CreateMemory();
        InstallSecondStageDirectoryOnly(memory);
        var runtime = new MemoryDomainRuntime();
        MemoryDomainDescriptor descriptor = Descriptor(guestPhysicalBase: 0);
        CpuInstructionTranslationPolicy policy =
            CpuInstructionTranslationPolicy.CreateTwoStageBoundedRegions(
                Scope,
                new[] { new CpuInstructionStageOneRegion(0x1000, 0, 0x1000, true, true, true) },
                runtime,
                descriptor,
                RuntimeAddressSpace);
        var core = new Processor.CPU_Core(
            0,
            CpuCorePlatformContext.CreateFixed(
                memory,
                ProcessorMode.Emulation,
                cpuInstructionTranslationPolicy: policy));
        core.PrepareExecutionStart(0x1000, activeVtId: 0);

        _ = Assert.Throws<CpuInstructionTranslationFaultException>(core.ExecutePipelineCycle);

        NeutralArchitecturalCompletionFacts facts = core.DomainCompletionObservationOwner.Observe(
            new CompletionObservationScope(Domain, Scope.ContextId, Scope.VirtualThreadId))
            .Snapshot!.Value.Facts;
        Assert.True(facts.FaultAddress.IsPresent);
        Assert.Equal(0UL, facts.FaultAddress.Value);
        Assert.Equal(NeutralFaultAddressSemantic.GuestPhysicalAddress,
            facts.FaultAddress.Semantic);
    }

    [Fact]
    public void SecondStageFacts_AreDeniedByStageOneProducerRegistration()
    {
        Processor.MainMemoryArea memory = CreateMemory();
        InstallSecondStageDirectoryOnly(memory);
        (Processor.CPU_Core core, _, _) = CreateTwoStageCore(memory);
        var candidate = new ArchitecturalCompletionCandidate(
            Domain,
            Scope.ContextId,
            Scope.VirtualThreadId,
            AttemptId: 91,
            EventId: 92,
            Facts: new NeutralArchitecturalCompletionFacts(
                NeutralArchitecturalCompletionClass.TranslationFault,
                NeutralScalarFact.Present((ulong)CpuInstructionTranslationFaultReason.SecondStageViolation),
                NeutralScalarFact.Present(1),
                NeutralAddressFact.Present(0x3000, NeutralFaultAddressSemantic.GuestPhysicalAddress),
                NeutralAuxiliaryFact.Present(
                    CpuSecondStageNeutralAuxiliary.Encode(
                        CpuInstructionTranslationAccessKind.ScalarLoad,
                        8,
                        CpuSecondStageFaultKind.NotPresent,
                        1),
                    NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation)),
            TranslationProvenance:
                NeutralTranslationProvenance.Present(93, 1, 1, 1, RuntimeAddressSpace));

        ArchitecturalCompletionCommitResult denied =
            core.ArchitecturalCompletionCommitOwner.CommitAtCanonicalPreciseFaultBoundary(
                core.CanonicalCpuTranslationFaultCompletionProducer,
                candidate);

        Assert.Equal(ArchitecturalCompletionCommitDecision.DeniedProducerPolicy,
            denied.Decision);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            core.DomainCompletionObservationOwner.Observe(
                new CompletionObservationScope(Domain, Scope.ContextId, Scope.VirtualThreadId)).Decision);
    }

    [Fact]
    public void SpeculativeSecondStageFault_IsSquashedWithoutObservation()
    {
        Processor.MainMemoryArea memory = CreateMemory();
        InstallSecondStageDirectoryOnly(memory);
        (Processor.CPU_Core core, _, _) = CreateTwoStageCore(memory);
        LoadMicroOp load = CreateLoad(0x1080);
        load.MarkSpeculative();

        Assert.False(load.Execute(ref core));
        Assert.True(load.IsSpeculativeFaultSuppressed);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            core.DomainCompletionObservationOwner.Observe(
                new CompletionObservationScope(Domain, Scope.ContextId, Scope.VirtualThreadId)).Decision);
    }

    [Fact]
    public void MisconfiguredLeaf_IsDistinctNeutralFaultAndNotVmcsShapedAuthority()
    {
        Processor.MainMemoryArea memory = CreateMemory();
        InstallSecondStageMapping(memory, 0x3000, 0x5000,
            leafPermissions: 1UL << 63);
        (Processor.CPU_Core core, _, _) = CreateTwoStageCore(memory);
        core.PrepareExecutionStart(0x1000, activeVtId: 0);

        CpuInstructionTranslationFaultException exception =
            Assert.Throws<CpuInstructionTranslationFaultException>(core.ExecutePipelineCycle);

        Assert.Equal(CpuInstructionTranslationFaultReason.SecondStageMisconfiguration,
            exception.Fault.Reason);
        Assert.True(CpuSecondStageNeutralAuxiliary.TryDecode(
            exception.Fault.Auxiliary,
            out CpuSecondStageFaultKind kind,
            out CpuInstructionTranslationAccessKind access,
            out ushort size,
            out byte level));
        Assert.Equal(CpuSecondStageFaultKind.Misconfiguration, kind);
        Assert.Equal(CpuInstructionTranslationAccessKind.InstructionFetch, access);
        Assert.Equal((ushort)256, size);
        Assert.Equal((byte)1, level);
    }

    [Fact]
    public void MemoryDomainReplacement_InvalidatesObservationAndOldPolicyCannotRepublish()
    {
        Processor.MainMemoryArea memory = CreateMemory();
        InstallSecondStageDirectoryOnly(memory);
        (Processor.CPU_Core core, MemoryDomainRuntime runtime, _) = CreateTwoStageCore(memory);
        core.PrepareExecutionStart(0x1000, activeVtId: 0);
        _ = Assert.Throws<CpuInstructionTranslationFaultException>(core.ExecutePipelineCycle);
        var scope = new CompletionObservationScope(Domain, Scope.ContextId, Scope.VirtualThreadId);
        Assert.True(core.DomainCompletionObservationOwner.Observe(scope).IsObserved);

        MemoryDomainSourceBindResult replacement =
            runtime.ReplaceAuthoritativeTranslationView(Descriptor(), RuntimeAddressSpace);

        Assert.True(replacement.IsBound);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            core.DomainCompletionObservationOwner.Observe(scope).Decision);
        core.PrepareExecutionStart(0x1000, activeVtId: 0);
        Assert.Throws<CpuInstructionTranslationSourceStaleException>(core.ExecutePipelineCycle);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            core.DomainCompletionObservationOwner.Observe(scope).Decision);
    }

    [Fact]
    public async Task RebindRacingPageWalk_SquashesStaleResultWithoutCompletionPublication()
    {
        var memory = new BlockingPageTableMemory();
        InstallSecondStageMapping(memory, 0x3000, 0x5000, leafPermissions: 0xFUL);
        (Processor.CPU_Core core, MemoryDomainRuntime runtime, _) = CreateTwoStageCore(memory);
        core.PrepareExecutionStart(0x1000, activeVtId: 0);
        memory.Arm();

        Task<Exception?> execution = Task.Run(() => Record.Exception(core.ExecutePipelineCycle));
        Assert.True(memory.WalkEntered.Wait(TimeSpan.FromSeconds(5)));
        MemoryDomainSourceBindResult replacement =
            runtime.RebindAuthoritativeTranslationViewAfterRestore(
                Descriptor(), RuntimeAddressSpace);
        memory.ReleaseWalk.Set();
        Exception? exception = await execution;

        Assert.True(replacement.IsBound);
        Assert.IsType<CpuInstructionTranslationSourceStaleException>(exception);
        Assert.Equal(CompletionObservationDecision.DeniedAbsent,
            core.DomainCompletionObservationOwner.Observe(
                new CompletionObservationScope(Domain, Scope.ContextId, Scope.VirtualThreadId)).Decision);
    }

    private static (Processor.CPU_Core Core, MemoryDomainRuntime Runtime, MemoryDomainDescriptor Descriptor)
        CreateTwoStageCore(Processor.MainMemoryArea memory)
    {
        var runtime = new MemoryDomainRuntime();
        MemoryDomainDescriptor descriptor = Descriptor();
        CpuInstructionTranslationPolicy policy =
            CpuInstructionTranslationPolicy.CreateTwoStageBoundedRegions(
                Scope,
                new[] { StageOneRegion() },
                runtime,
                descriptor,
                RuntimeAddressSpace);
        return (
            new Processor.CPU_Core(
                0,
                CpuCorePlatformContext.CreateFixed(
                    memory,
                    ProcessorMode.Emulation,
                    cpuInstructionTranslationPolicy: policy)),
            runtime,
            descriptor);
    }

    private static MemoryDomainDescriptor Descriptor(ulong guestPhysicalBase = 0x3000) => new(
        AddressSpace(guestPhysicalBase),
        new MemoryTranslationPolicy(),
        TranslationControl(),
        dirtyTracking: null,
        ownsSecondStageTranslation: true);

    private static AddressSpaceDescriptor AddressSpace(ulong guestPhysicalBase = 0x3000) => new(
        new AddressSpaceId((ushort)Domain, (ushort)RuntimeAddressSpace, 0x1000, 1, 1, 1),
        AddressSpaceAuthority.RuntimeOwned,
        guestPhysicalBase: guestPhysicalBase,
        sizeBytes: 0x1000,
        generation: 1,
        compatibilityProjectionEnabled: false);

    private static MemoryDomainTranslationControl TranslationControl() => new(
        TranslationEnabled: true,
        AddressSpaceTaggingEnabled: true,
        AddressSpaceRoot: 0,
        SecondStageRoot: 0x1000,
        DomainTag: (ushort)Domain,
        AddressSpaceTag: (ushort)RuntimeAddressSpace,
        AddressSpaceGeneration: 1,
        DefaultMemoryType: MemoryDomainTranslationControl.WriteBackMemoryType);

    private static CpuInstructionStageOneRegion StageOneRegion() =>
        new(0x1000, 0x3000, 0x1000, Readable: true, Writable: true, Executable: true);

    private static void InstallSecondStageDirectoryOnly(Processor.MainMemoryArea memory)
    {
        Assert.True(memory.TryWritePhysicalRange(0x1000, BitConverter.GetBytes(0x2001UL)));
    }

    private static void InstallSecondStageMapping(
        Processor.MainMemoryArea memory,
        ulong guestPhysicalPage,
        ulong cpuPhysicalPage,
        ulong leafPermissions)
    {
        InstallSecondStageDirectoryOnly(memory);
        ulong leafAddress = 0x2000 + ((guestPhysicalPage >> 12) & 0x3FFUL) * 8UL;
        Assert.True(memory.TryWritePhysicalRange(
            leafAddress,
            BitConverter.GetBytes(cpuPhysicalPage | leafPermissions)));
    }

    private static Processor.MainMemoryArea CreateMemory()
    {
        var memory = new Processor.MultiBankMemoryArea(1, 0x8000);
        memory.SetLength(0x8000);
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
                DomainTag = Domain,
            },
        };
        operation.InitializeMetadata();
        return operation;
    }

    private static StoreMicroOp CreateStore(ulong address, ulong value)
    {
        var operation = new StoreMicroOp
        {
            Address = address,
            Size = 8,
            Value = value,
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SD,
            BaseRegID = 1,
            OwnerContextId = Scope.ContextId,
            OwnerThreadId = Scope.VirtualThreadId,
            VirtualThreadId = Scope.VirtualThreadId,
            Placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = SlotClass.LsuClass,
                PinningKind = SlotPinningKind.ClassFlexible,
                DomainTag = Domain,
            },
        };
        operation.InitializeMetadata();
        return operation;
    }

    private sealed class BlockingPageTableMemory : Processor.MultiBankMemoryArea
    {
        private int _armed;

        internal BlockingPageTableMemory() : base(1, 0x8000)
        {
            SetLength(0x8000);
        }

        internal ManualResetEventSlim WalkEntered { get; } = new(false);
        internal ManualResetEventSlim ReleaseWalk { get; } = new(false);

        internal void Arm() => Interlocked.Exchange(ref _armed, 1);

        public override bool TryReadPhysicalRange(ulong physicalAddress, Span<byte> buffer)
        {
            if (physicalAddress == 0x1000 &&
                Interlocked.Exchange(ref _armed, 0) == 1)
            {
                WalkEntered.Set();
                if (!ReleaseWalk.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Timed out waiting for MemoryDomain rebind race.");
            }
            return base.TryReadPhysicalRange(physicalAddress, buffer);
        }
    }

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
