using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.Addressing;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Execution;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class AddressingBackendResolverPhase06Tests
{
    private const AddressingBackendCapabilities BaseCapabilities =
        AddressingBackendCapabilities.ExplicitAddressSpaceContract |
        AddressingBackendCapabilities.OwnerDomainDeviceBinding |
        AddressingBackendCapabilities.TypedFaultClassification;

    [Fact]
    public void Phase06_PhysicalAddressSpaceSelectsPhysicalWrapperOnlyAndHelperRemainsPhysical()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
            DmaStreamComputeTelemetryTests.WriteMemory(
                0x1000,
                new byte[] { 0x11, 0x22, 0x33, 0x44 });
            DmaStreamComputeDescriptor descriptor =
                DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
            var physicalBackend = new PhysicalMainMemoryBurstBackend(Processor.MainMemory);
            var iommuBackend = new IOMMUBurstBackend();
            var resolver = new AddressingBackendResolver(
                physicalBackend,
                iommuBackend);

            AddressingBackendResolution resolution = resolver.Resolve(
                AddressingBackendResolutionRequest.ForDmaStreamCompute(
                    descriptor,
                    MemoryAddressSpaceKind.Physical,
                    BaseCapabilities | AddressingBackendCapabilities.PhysicalMainMemoryBackend,
                    tokenId: 0x606));

            Assert.True(resolution.IsSelected, resolution.Message);
            Assert.Equal(AddressingBackendKind.PhysicalMainMemory, resolution.BackendKind);
            Assert.Same(physicalBackend, resolution.Backend);
            Assert.NotSame(iommuBackend, resolution.Backend);
            Assert.False(resolution.IsCurrentArchitecturalExecution);
            Assert.False(resolution.ChangesCurrentDscHelperPath);
            Assert.False(resolution.ChangesCurrentL7CarrierExecution);

            byte[] buffer = new byte[4];
            Assert.True(resolution.Backend!.Read(descriptor.OwnerBinding.DeviceId, 0x1000, buffer));
            Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, buffer);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        Assert.Empty(ScanCurrentExecutableSurfacesForPhase06Hooks(repoRoot));
    }

    [Fact]
    public void Phase06_IommuTranslatedSelectionUsesExplicitIommuBackendWithApprovedDeviceId()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            DmaStreamComputeTelemetryTests.InitializeMainMemory(0x20000);
            YAKSys_Hybrid_CPU.Memory.IOMMU.Initialize();
            DmaStreamComputeDescriptor descriptor =
                DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
            uint deviceId = descriptor.OwnerBinding.DeviceId;
            ulong ioVirtualAddress = 0xA000;
            ulong physicalAddress = 0x3000;
            DmaStreamComputeTelemetryTests.WriteMemory(
                physicalAddress,
                new byte[] { 0x5A, 0x6B, 0x7C, 0x8D, 0, 0, 0, 0 });
            Assert.True(YAKSys_Hybrid_CPU.Memory.IOMMU.Map(
                deviceId,
                ioVirtualAddress,
                physicalAddress,
                0x1000,
                IOMMUAccessPermissions.ReadWrite));

            var physicalBackend = new PhysicalMainMemoryBurstBackend(Processor.MainMemory);
            var iommuBackend = new IOMMUBurstBackend();
            var resolver = new AddressingBackendResolver(
                physicalBackend,
                iommuBackend);
            AddressingBackendParticipantIdentity identity =
                AddressingBackendParticipantIdentity.FromDmaStreamCompute(
                    descriptor,
                    tokenId: 0x607,
                    mappingEpoch: 0x123,
                    mappingEpochKnown: true);
            var request = new AddressingBackendResolutionRequest(
                MemoryAddressSpaceKind.IommuTranslated,
                AddressingBackendAccessKind.ReadWrite,
                new[] { new AddressingBackendRange(ioVirtualAddress, 8) },
                identity,
                BaseCapabilities |
                AddressingBackendCapabilities.IommuBurstBackend |
                AddressingBackendCapabilities.MappingEpoch);

            AddressingBackendResolution resolution = resolver.Resolve(request);

            Assert.True(resolution.IsSelected, resolution.Message);
            Assert.Equal(AddressingBackendKind.IommuBurst, resolution.BackendKind);
            Assert.Same(iommuBackend, resolution.Backend);
            Assert.False(resolution.AllowsSilentFallbackToPhysical);

            byte[] read = new byte[4];
            Assert.True(resolution.Backend!.Read(deviceId, ioVirtualAddress, read));
            Assert.Equal(new byte[] { 0x5A, 0x6B, 0x7C, 0x8D }, read);

            byte[] write = { 0x99, 0x88, 0x77, 0x66 };
            Assert.True(resolution.Backend.Write(deviceId, ioVirtualAddress + 4, write));
            byte[] physical = DmaStreamComputeTelemetryTests.ReadMemory(physicalAddress + 4, 4);
            Assert.Equal(write, physical);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
            YAKSys_Hybrid_CPU.Memory.IOMMU.Initialize();
        }
    }

    [Fact]
    public void Phase06_UnsupportedOrMissingIommuBackendRejectsWithoutPhysicalFallback()
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var physicalBackend = new PhysicalMainMemoryBurstBackend(Processor.MainMemory);
        var resolverWithoutIommu = new AddressingBackendResolver(
            physicalBackend,
            iommuBackend: null);
        AddressingBackendParticipantIdentity identity =
            AddressingBackendParticipantIdentity.FromDmaStreamCompute(
                descriptor,
                tokenId: 0x608,
                mappingEpoch: 1,
                mappingEpochKnown: true);

        AddressingBackendResolution missingIommu = resolverWithoutIommu.Resolve(
            new AddressingBackendResolutionRequest(
                MemoryAddressSpaceKind.IommuTranslated,
                AddressingBackendAccessKind.ReadWrite,
                new[] { new AddressingBackendRange(0x1000, 16) },
                identity,
                BaseCapabilities |
                AddressingBackendCapabilities.PhysicalMainMemoryBackend |
                AddressingBackendCapabilities.MappingEpoch));

        Assert.True(missingIommu.IsRejected);
        Assert.Equal(AddressingBackendFaultKind.CapabilityMismatch, missingIommu.FaultKind);
        Assert.Equal(AddressingBackendKind.None, missingIommu.BackendKind);
        Assert.Null(missingIommu.Backend);
        Assert.False(missingIommu.AllowsSilentFallbackToPhysical);

        AddressingBackendResolution unsupported = new AddressingBackendResolver(
            physicalBackend,
            new IOMMUBurstBackend()).Resolve(
            new AddressingBackendResolutionRequest(
                MemoryAddressSpaceKind.ReservedFuture,
                AddressingBackendAccessKind.ReadWrite,
                new[] { new AddressingBackendRange(0x1000, 16) },
                identity,
                BaseCapabilities |
                AddressingBackendCapabilities.PhysicalMainMemoryBackend |
                AddressingBackendCapabilities.IommuBurstBackend |
                AddressingBackendCapabilities.MappingEpoch));

        Assert.True(unsupported.IsRejected);
        Assert.Equal(AddressingBackendFaultKind.UnsupportedAddressSpace, unsupported.FaultKind);
        Assert.Null(unsupported.Backend);
        Assert.False(unsupported.AllowsSilentFallbackToPhysical);
    }

    [Fact]
    public void Phase06_DeviceOwnerDomainAndMappingEpochMismatchRejectBeforeBackendSelection()
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var resolver = new AddressingBackendResolver(
            new PhysicalMainMemoryBurstBackend(Processor.MainMemory),
            new IOMMUBurstBackend());
        AddressingBackendParticipantIdentity identity =
            AddressingBackendParticipantIdentity.FromDmaStreamCompute(
                descriptor,
                tokenId: 0x609,
                mappingEpoch: 10,
                mappingEpochKnown: true);

        AddressingBackendResolution ownerMismatch = resolver.Resolve(
            new AddressingBackendResolutionRequest(
                MemoryAddressSpaceKind.Physical,
                AddressingBackendAccessKind.ReadWrite,
                new[] { new AddressingBackendRange(0x1000, 16) },
                identity,
                BaseCapabilities | AddressingBackendCapabilities.PhysicalMainMemoryBackend,
                currentAuthority: identity with { OwnerContextId = identity.OwnerContextId + 1 }));
        Assert.True(ownerMismatch.IsRejected);
        Assert.Equal(AddressingBackendFaultKind.OwnerDomainMismatch, ownerMismatch.FaultKind);
        Assert.Null(ownerMismatch.Backend);

        AddressingBackendResolution deviceMismatch = resolver.Resolve(
            new AddressingBackendResolutionRequest(
                MemoryAddressSpaceKind.Physical,
                AddressingBackendAccessKind.ReadWrite,
                new[] { new AddressingBackendRange(0x1000, 16) },
                identity,
                BaseCapabilities | AddressingBackendCapabilities.PhysicalMainMemoryBackend,
                currentAuthority: identity with { DeviceId = identity.DeviceId + 1 }));
        Assert.True(deviceMismatch.IsRejected);
        Assert.Equal(AddressingBackendFaultKind.DeviceMismatch, deviceMismatch.FaultKind);
        Assert.Null(deviceMismatch.Backend);

        AddressingBackendResolution epochDrift = resolver.Resolve(
            new AddressingBackendResolutionRequest(
                MemoryAddressSpaceKind.IommuTranslated,
                AddressingBackendAccessKind.ReadWrite,
                new[] { new AddressingBackendRange(0xA000, 16) },
                identity,
                BaseCapabilities |
                AddressingBackendCapabilities.IommuBurstBackend |
                AddressingBackendCapabilities.MappingEpoch,
                currentAuthority: identity with { MappingEpoch = 11, MappingEpochKnown = true }));
        Assert.True(epochDrift.IsRejected);
        Assert.Equal(AddressingBackendFaultKind.MappingEpochRevoked, epochDrift.FaultKind);
        Assert.Null(epochDrift.Backend);

        AddressingBackendResolution missingCurrentEpoch = resolver.Resolve(
            new AddressingBackendResolutionRequest(
                MemoryAddressSpaceKind.IommuTranslated,
                AddressingBackendAccessKind.ReadWrite,
                new[] { new AddressingBackendRange(0xA000, 16) },
                identity,
                BaseCapabilities |
                AddressingBackendCapabilities.IommuBurstBackend |
                AddressingBackendCapabilities.MappingEpoch,
                currentAuthority: identity with { MappingEpochKnown = false }));
        Assert.True(missingCurrentEpoch.IsRejected);
        Assert.Equal(AddressingBackendFaultKind.MissingMappingEpoch, missingCurrentEpoch.FaultKind);
        Assert.Null(missingCurrentEpoch.Backend);
    }

    [Fact]
    public void Phase06_L7IommuRequestCarriesGuardBackedDeviceAndMappingEpoch()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            DmaStreamComputeTelemetryTests.InitializeMainMemory(0x20000);
            YAKSys_Hybrid_CPU.Memory.IOMMU.Initialize();
            AcceleratorCommandDescriptor descriptor = CreateL7Descriptor(mappingEpoch: 0x456);
            uint deviceId = (uint)descriptor.AcceleratorId;
            ulong ioVirtualAddress = 0xB000;
            ulong physicalAddress = 0x4000;
            byte[] expected = { 0x10, 0x20, 0x30, 0x40 };
            DmaStreamComputeTelemetryTests.WriteMemory(physicalAddress, expected);
            Assert.True(YAKSys_Hybrid_CPU.Memory.IOMMU.Map(
                deviceId,
                ioVirtualAddress,
                physicalAddress,
                0x1000,
                IOMMUAccessPermissions.ReadWrite));

            var iommuBackend = new IOMMUBurstBackend();
            var resolver = new AddressingBackendResolver(
                new PhysicalMainMemoryBurstBackend(Processor.MainMemory),
                iommuBackend);
            AddressingBackendResolution resolution = resolver.Resolve(
                AddressingBackendResolutionRequest.ForL7Accelerator(
                    descriptor,
                    MemoryAddressSpaceKind.IommuTranslated,
                    BaseCapabilities |
                    AddressingBackendCapabilities.IommuBurstBackend |
                    AddressingBackendCapabilities.MappingEpoch,
                    tokenId: 0x700,
                    commandId: 0x701,
                    issueAge: 0x33,
                    replayGeneration: 0x44));

            Assert.True(resolution.IsSelected, resolution.Message);
            Assert.Equal(AddressingBackendKind.IommuBurst, resolution.BackendKind);
            Assert.Same(iommuBackend, resolution.Backend);

            AddressingBackendResolutionRequest request =
                AddressingBackendResolutionRequest.ForL7Accelerator(
                    descriptor,
                    MemoryAddressSpaceKind.IommuTranslated,
                    BaseCapabilities |
                    AddressingBackendCapabilities.IommuBurstBackend |
                    AddressingBackendCapabilities.MappingEpoch,
                    tokenId: 0x700,
                    commandId: 0x701,
                    issueAge: 0x33,
                    replayGeneration: 0x44);
            Assert.Equal(deviceId, request.AcceptedIdentity.DeviceId);
            Assert.Equal(descriptor.OwnerBinding.DomainTag, request.AcceptedIdentity.MemoryDomainTag);
            Assert.Equal(0x456UL, request.AcceptedIdentity.MappingEpoch);
            Assert.True(request.AcceptedIdentity.MappingEpochKnown);
            Assert.Equal(0x700UL, request.AcceptedIdentity.TokenId);
            Assert.Equal(0x701UL, request.AcceptedIdentity.CommandId);
            Assert.Equal(0x33UL, request.AcceptedIdentity.IssueAge);
            Assert.Equal(0x44UL, request.AcceptedIdentity.ReplayGeneration);

            byte[] actual = new byte[expected.Length];
            Assert.True(resolution.Backend!.Read(deviceId, ioVirtualAddress, actual));
            Assert.Equal(expected, actual);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
            YAKSys_Hybrid_CPU.Memory.IOMMU.Initialize();
        }
    }

    [Fact]
    public void Phase06_AddressingFaultsMapToDscFaultRecordsWithoutMemoryPublication()
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        byte[] original = DmaStreamComputeTelemetryTests.Fill(0x44, 16);
        DmaStreamComputeTelemetryTests.WriteMemory(0x9000, original);
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var resolver = new AddressingBackendResolver();

        AssertFaultMapping(
            resolver,
            descriptor,
            AddressingBackendFaultKind.TranslationFault,
            DmaStreamComputeTokenFaultKind.TranslationFault,
            DmaStreamComputeFaultSourcePhase.Iommu);
        AssertFaultMapping(
            resolver,
            descriptor,
            AddressingBackendFaultKind.PermissionFault,
            DmaStreamComputeTokenFaultKind.PermissionFault,
            DmaStreamComputeFaultSourcePhase.Iommu);
        AssertFaultMapping(
            resolver,
            descriptor,
            AddressingBackendFaultKind.OwnerDomainMismatch,
            DmaStreamComputeTokenFaultKind.DomainViolation,
            DmaStreamComputeFaultSourcePhase.Admission);
        AssertFaultMapping(
            resolver,
            descriptor,
            AddressingBackendFaultKind.DeviceMismatch,
            DmaStreamComputeTokenFaultKind.DmaDeviceFault,
            DmaStreamComputeFaultSourcePhase.Admission);
        AssertFaultMapping(
            resolver,
            descriptor,
            AddressingBackendFaultKind.AlignmentFault,
            DmaStreamComputeTokenFaultKind.AlignmentFault,
            DmaStreamComputeFaultSourcePhase.Backend);
        AssertFaultMapping(
            resolver,
            descriptor,
            AddressingBackendFaultKind.BoundsFault,
            DmaStreamComputeTokenFaultKind.MemoryFault,
            DmaStreamComputeFaultSourcePhase.Backend);

        var token = new DmaStreamComputeToken(descriptor, tokenId: 0x60A);
        DmaStreamComputeFaultRecord record =
            resolver.CreateDmaStreamComputeFaultRecord(
                descriptor,
                AddressingBackendFaultKind.TranslationFault,
                faultAddress: 0xA000,
                isWrite: false);
        DmaStreamComputeCommitResult result = token.PublishFault(record);

        Assert.True(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.Equal(original, DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));
    }

    [Fact]
    public void Phase06_CurrentExecutableBoundariesRemainClosedAndCompilerLoweringForbidden()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var core = new Processor.CPU_Core(0);

        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);
        Assert.Throws<InvalidOperationException>(
            () => new DmaStreamComputeMicroOp(descriptor).Execute(ref core));

        foreach (SystemDeviceCommandMicroOp carrier in CreateL7Carriers())
        {
            Assert.False(carrier.WritesRegister);
            Assert.Throws<InvalidOperationException>(() => carrier.Execute(ref core));
        }

        byte[] descriptorBytes = DmaStreamComputeTestDescriptorFactory.BuildDescriptor();
        BinaryPrimitives.WriteUInt32LittleEndian(descriptorBytes.AsSpan(12), 1);
        DmaStreamComputeValidationResult rejected =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                DmaStreamComputeTestDescriptorFactory.CreateGuardDecision(descriptorBytes));
        Assert.False(rejected.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.ReservedFieldFault, rejected.Fault);

        var iommuBackend = new IOMMUBurstBackend();
        Assert.Throws<NotSupportedException>(
            () => iommuBackend.RegisterAcceleratorDevice(
                1,
                new AcceleratorDMACapabilities()));
        Assert.Throws<NotSupportedException>(
            () => iommuBackend.InitiateAcceleratorDMA(1, 0x1000, 0x2000, 16));

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        Assert.Empty(ScanCurrentExecutableSurfacesForPhase06Hooks(repoRoot));

        string compilerText = ReadAllSourceText(Path.Combine(repoRoot, "HybridCPU_Compiler"));
        Assert.DoesNotContain("AddressingBackendResolver", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryAddressSpaceKind", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("IommuTranslated", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMainMemoryBurstBackend", compilerText, StringComparison.Ordinal);
    }

    private static void AssertFaultMapping(
        AddressingBackendResolver resolver,
        DmaStreamComputeDescriptor descriptor,
        AddressingBackendFaultKind addressingFault,
        DmaStreamComputeTokenFaultKind tokenFault,
        DmaStreamComputeFaultSourcePhase sourcePhase)
    {
        DmaStreamComputeFaultRecord record =
            resolver.CreateDmaStreamComputeFaultRecord(
                descriptor,
                addressingFault,
                faultAddress: 0xA000,
                isWrite: true);

        Assert.Equal(tokenFault, record.FaultKind);
        Assert.Equal(sourcePhase, record.SourcePhase);
        Assert.True(record.RequiresRetireExceptionPublication);
        Assert.True(record.RequiresFuturePrecisePublicationMetadata);
        Assert.False(record.IsFullPipelinePreciseArchitecturalException);
    }

    private static string[] ScanCurrentExecutableSurfacesForPhase06Hooks(
        string repoRoot) =>
        CompatFreezeScanner.ScanProductionFilesForPatterns(
            repoRoot,
            new[]
            {
                "AddressingBackendResolver",
                "AddressingBackendResolutionRequest",
                "MemoryAddressSpaceKind",
                "PhysicalMainMemoryBurstBackend"
            },
            new[]
            {
                Path.Combine(
                    "HybridCPU_ISE",
                    "Core",
                    "Execution",
                    "Addressing",
                    "AddressingBackendResolver.cs"),
                Path.Combine(
                    "HybridCPU_ISE",
                    "Core",
                    "Execution",
                    "BurstIO",
                    "PhysicalMainMemoryBurstBackend.cs")
            },
            new[]
            {
                Path.Combine("HybridCPU_ISE", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeRuntime.cs"),
                Path.Combine("HybridCPU_ISE", "Core", "Execution", "DmaStreamCompute", "DmaStreamAcceleratorBackend.cs"),
                Path.Combine("HybridCPU_ISE", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeDescriptor.cs"),
                Path.Combine("HybridCPU_ISE", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeDescriptorParser.cs"),
                Path.Combine("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "DmaStreamComputeMicroOp.cs"),
                Path.Combine("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "SystemDeviceCommandMicroOp.cs"),
                Path.Combine("HybridCPU_ISE", "Core", "Memory", "MemoryUnit.cs"),
                Path.Combine("HybridCPU_ISE", "Core", "Memory", "AtomicMemoryUnit.cs"),
                Path.Combine("HybridCPU_ISE", "Memory", "DMA", "DMAController.cs"),
                Path.Combine("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamEngine.BurstIO.cs")
            });

    private static AcceleratorCommandDescriptor CreateL7Descriptor(
        ulong mappingEpoch = 0)
    {
        byte[] bytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        AcceleratorOwnerBinding owner = L7SdcTestDescriptorFactory.ReadOwnerBinding(bytes);
        AcceleratorGuardEvidence evidence = L7SdcTestDescriptorFactory.CreateGuardEvidence(
            owner,
            mappingEpoch: new AcceleratorMappingEpoch(mappingEpoch));
        AcceleratorDescriptorValidationResult validation =
            L7SdcTestDescriptorFactory.ParseWithGuard(bytes, evidence);
        Assert.True(validation.IsValid, validation.Message);
        return validation.RequireDescriptor();
    }

    private static SystemDeviceCommandMicroOp[] CreateL7Carriers() =>
        new SystemDeviceCommandMicroOp[]
        {
            new AcceleratorQueryCapsMicroOp(),
            new AcceleratorSubmitMicroOp(CreateL7Descriptor()),
            new AcceleratorPollMicroOp(),
            new AcceleratorWaitMicroOp(),
            new AcceleratorCancelMicroOp(),
            new AcceleratorFenceMicroOp()
        };

    private static string ReadAllSourceText(string root)
    {
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file => !CompatFreezeScanner.IsGeneratedPath(file))
                .Select(File.ReadAllText));
    }
}
