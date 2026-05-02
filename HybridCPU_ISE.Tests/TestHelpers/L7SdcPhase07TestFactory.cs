using System;
using System.Collections.Generic;
using System.IO;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal sealed record L7SdcPhase07Fixture(
    AcceleratorTokenStore Store,
    AcceleratorToken Token,
    AcceleratorTokenAdmissionResult Admission,
    AcceleratorCommandDescriptor Descriptor,
    AcceleratorCapabilityAcceptanceResult CapabilityAcceptance,
    AcceleratorGuardEvidence Evidence)
{
    public AcceleratorQueueAdmissionRequest CreateQueueAdmissionRequest(
        bool conflictAccepted = true,
        string conflictEvidenceMessage =
            "Phase 07 test placeholder conflict acceptance evidence.")
    {
        return new AcceleratorQueueAdmissionRequest
        {
            Descriptor = Descriptor,
            CapabilityAcceptance = CapabilityAcceptance,
            TokenAdmission = Admission,
            ConflictAccepted = conflictAccepted,
            ConflictEvidenceMessage = conflictEvidenceMessage
        };
    }
}

internal static class L7SdcPhase07TestFactory
{
    internal static L7SdcPhase07Fixture CreateAcceptedToken(
        ulong mappingEpoch = 0,
        ulong iommuDomainEpoch = 0,
        AcceleratorTelemetry? telemetry = null) =>
        CreateAcceptedTokenForDescriptor(
            mappingEpoch: mappingEpoch,
            iommuDomainEpoch: iommuDomainEpoch,
            telemetry: telemetry);

    internal static L7SdcPhase07Fixture CreateAcceptedTokenForDescriptor(
        IReadOnlyList<AcceleratorMemoryRange>? sourceRanges = null,
        IReadOnlyList<AcceleratorMemoryRange>? destinationRanges = null,
        IReadOnlyList<AcceleratorMemoryRange>? scratchRanges = null,
        ulong mappingEpoch = 0,
        ulong iommuDomainEpoch = 0,
        AcceleratorTelemetry? telemetry = null)
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor(
            sourceRanges: sourceRanges,
            destinationRanges: destinationRanges,
            scratchRanges: scratchRanges);
        AcceleratorOwnerBinding ownerBinding =
            L7SdcTestDescriptorFactory.ReadOwnerBinding(descriptorBytes);
        AcceleratorGuardEvidence evidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                ownerBinding,
                activeDomainCertificate: ownerBinding.DomainTag,
                mappingEpoch: new AcceleratorMappingEpoch(mappingEpoch),
                iommuDomainEpoch: new AcceleratorIommuDomainEpoch(iommuDomainEpoch));
        AcceleratorDescriptorValidationResult descriptorResult =
            L7SdcTestDescriptorFactory.ParseWithGuard(
                descriptorBytes,
                evidence,
                telemetry: telemetry);
        if (!descriptorResult.IsValid)
        {
            throw new InvalidOperationException(descriptorResult.Message);
        }

        AcceleratorCommandDescriptor descriptor = descriptorResult.RequireDescriptor();
        var registry = new AcceleratorCapabilityRegistry(telemetry);
        registry.RegisterProvider(new MatMulCapabilityProvider());
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance =
            registry.AcceptCapability(
                "matmul.fixture.v1",
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision);
        var store = new AcceleratorTokenStore(telemetry);
        AcceleratorTokenAdmissionResult admission =
            store.Create(descriptor, capabilityAcceptance, evidence);
        if (!admission.IsAccepted)
        {
            throw new InvalidOperationException(admission.Message);
        }

        return new L7SdcPhase07Fixture(
            store,
            admission.Token!,
            admission,
            descriptor,
            capabilityAcceptance,
            evidence);
    }

    internal static AcceleratorCommandQueue CreateQueue(
        int capacity = 2,
        bool deviceAvailable = true,
        AcceleratorTelemetry? telemetry = null) =>
        new(
            capacity,
            new AcceleratorDevice(
                "matmul.fixture.v1",
                deviceAvailable),
            telemetry);

    internal static AcceleratorGuardEvidence CreateOwnerDriftEvidence(
        AcceleratorCommandDescriptor descriptor)
    {
        AcceleratorOwnerBinding driftedOwner =
            L7SdcTestDescriptorFactory.CreateOwnerBinding(ownerContextId: 0xBAD);
        return L7SdcTestDescriptorFactory.CreateGuardEvidence(
            driftedOwner,
            activeDomainCertificate: descriptor.OwnerBinding.DomainTag);
    }

    internal static AcceleratorGuardEvidence CreateMappingDriftEvidence(
        AcceleratorCommandDescriptor descriptor,
        ulong mappingEpoch,
        ulong iommuDomainEpoch) =>
        L7SdcTestDescriptorFactory.CreateGuardEvidence(
            descriptor.OwnerBinding,
            activeDomainCertificate: descriptor.OwnerBinding.DomainTag,
            mappingEpoch: new AcceleratorMappingEpoch(mappingEpoch),
            iommuDomainEpoch: new AcceleratorIommuDomainEpoch(iommuDomainEpoch));

    internal static byte[] Fill(byte value, int length)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }

    internal static void InitializeMainMemory(ulong bytes)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, bytes);
        Processor.Memory = null;
    }

    internal static void WriteMainMemory(ulong address, ReadOnlySpan<byte> data)
    {
        if (!Processor.MainMemory.TryWritePhysicalRange(address, data))
        {
            throw new InvalidOperationException(
                $"Could not initialize test memory at 0x{address:X}.");
        }
    }

    internal static byte[] ReadMainMemory(ulong address, int length)
    {
        byte[] bytes = new byte[length];
        if (!Processor.MainMemory.TryReadPhysicalRange(address, bytes))
        {
            throw new InvalidOperationException(
                $"Could not read test memory at 0x{address:X}.");
        }

        return bytes;
    }

    internal static string ResolveRepoRoot()
    {
        string current = Directory.GetCurrentDirectory();
        DirectoryInfo? directory = new(current);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not resolve repository root from '{current}'.");
    }
}
