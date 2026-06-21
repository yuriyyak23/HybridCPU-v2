using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcTopologyQueueTaxonomyPhase09Tests
{
    [Fact]
    public void TopologyQueueTaxonomy_IsMetadataOnlyAndGrantsNoAuthority()
    {
        AcceleratorDescriptorTaxonomyEntry entry =
            AcceleratorDescriptorTaxonomyCatalog.TopologyQueueMetadata;

        Assert.Equal(
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.TopologyQueue,
                AcceleratorDeviceId.TopologyQueueMetadata,
                AcceleratorOperationKind.TopologyQueueContract,
                AcceleratorShapeKind.TopologyQueueMap),
            entry.Key);
        Assert.Equal("topology.queue.metadata.v1", entry.Name);
        Assert.Equal(AcceleratorDescriptorTaxonomyStatus.MetadataOnly, entry.Status);
        Assert.True(entry.IsMetadataOnly);
        Assert.False(entry.GrantsDescriptorAcceptanceAuthority);
        Assert.False(entry.GrantsCapabilityAuthority);
        Assert.False(entry.GrantsTopologyQueryAuthority);
        Assert.False(entry.GrantsQueueOpenAuthority);
        Assert.False(entry.GrantsQueueBindAuthority);
        Assert.False(entry.GrantsQueueLifecycleAuthority);
        Assert.False(entry.GrantsTokenAuthority);
        Assert.False(entry.GrantsExecutionAuthority);
        Assert.False(entry.GrantsCommitAuthority);
        Assert.False(entry.GrantsCompilerEmissionAuthority);

        Assert.True(AcceleratorDescriptorTaxonomyCatalog.TryGetEntry(
            entry.Key,
            out AcceleratorDescriptorTaxonomyEntry resolved));
        Assert.Same(entry, resolved);
    }

    [Fact]
    public void TopologyQueueCapabilityProvider_IsQueryableButNotRuntimeRegistered()
    {
        var runtime = new ExternalAcceleratorRuntime(new Processor.MainMemoryArea());
        Assert.True(runtime.Capabilities.TryGetDescriptor(
            MatMulCapabilityProvider.AcceleratorId,
            out _));
        Assert.False(runtime.Capabilities.TryGetDescriptor(
            TopologyQueueMetadataCapabilityProvider.AcceleratorId,
            out _));

        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(new TopologyQueueMetadataCapabilityProvider());

        AcceleratorCapabilityQueryResult query =
            registry.Query(TopologyQueueMetadataCapabilityProvider.AcceleratorId);

        Assert.True(query.IsMetadataAvailable, query.RejectReason);
        Assert.False(query.GrantsDecodeAuthority);
        Assert.False(query.GrantsCommandSubmissionAuthority);
        Assert.False(query.GrantsTopologyQueryAuthority);
        Assert.False(query.GrantsQueueOpenAuthority);
        Assert.False(query.GrantsQueueBindAuthority);
        Assert.False(query.GrantsQueueLifecycleAuthority);
        Assert.False(query.GrantsExecutionAuthority);
        Assert.False(query.GrantsCommitAuthority);

        AcceleratorCapabilityDescriptor descriptor = query.Descriptor!;
        Assert.Equal(
            AcceleratorDescriptorTaxonomyCatalog.TopologyQueueMetadata.Key,
            descriptor.TaxonomyKey);
        Assert.Equal(AcceleratorDescriptorTaxonomyStatus.MetadataOnly, descriptor.TaxonomyStatus);
        Assert.False(descriptor.GrantsDescriptorAcceptanceAuthority);
        Assert.False(descriptor.GrantsTopologyQueryAuthority);
        Assert.False(descriptor.GrantsQueueOpenAuthority);
        Assert.False(descriptor.GrantsQueueBindAuthority);
        Assert.False(descriptor.GrantsQueueLifecycleAuthority);
        Assert.False(descriptor.GrantsTokenAuthority);
        Assert.False(descriptor.GrantsExecutionAuthority);
        Assert.False(descriptor.GrantsCommitAuthority);
        Assert.False(descriptor.GrantsCompilerEmissionAuthority);
        Assert.Equal(0u, descriptor.ResourceModel.MaxQueueOccupancy);
        Assert.Equal(0u, descriptor.ResourceModel.EstimateQueueOccupancy(8));
        Assert.True(descriptor.TryGetOperation(
            "topology-queue-contract",
            out AcceleratorOperationCapability? operation));
        Assert.True(operation!.SupportsDatatype("metadata"));
        Assert.True(operation.SupportsShape("topology-queue-map", elementCount: 4, rank: 1));

        AcceleratorOwnerBinding owner = L7SdcTestDescriptorFactory.CreateOwnerBinding();
        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDescriptorAcceptance(
                owner,
                L7SdcTestDescriptorFactory.CreateGuardEvidence(owner));
        AcceleratorCapabilityAcceptanceResult accepted =
            registry.AcceptCapability(
                TopologyQueueMetadataCapabilityProvider.AcceleratorId,
                owner,
                guardDecision);

        Assert.True(accepted.IsAccepted, accepted.RejectReason);
        Assert.False(accepted.GrantsDecodeAuthority);
        Assert.False(accepted.GrantsCommandSubmissionAuthority);
        Assert.False(accepted.GrantsTopologyQueryAuthority);
        Assert.False(accepted.GrantsQueueOpenAuthority);
        Assert.False(accepted.GrantsQueueBindAuthority);
        Assert.False(accepted.GrantsQueueLifecycleAuthority);
        Assert.False(accepted.GrantsExecutionAuthority);
        Assert.False(accepted.GrantsCommitAuthority);
    }

    [Fact]
    public void TopologyQueueDescriptorHeaderReadsButParserRejectsBeforeAcceptance()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor(
            acceleratorClass: AcceleratorClassId.TopologyQueue,
            acceleratorId: AcceleratorDeviceId.TopologyQueueMetadata,
            operation: AcceleratorOperationKind.TopologyQueueContract,
            datatype: AcceleratorDatatype.Int32,
            shape: AcceleratorShapeKind.TopologyQueueMap,
            shapeRank: 1,
            elementCount: 4);
        AcceleratorDescriptorReference reference =
            L7SdcTestDescriptorFactory.CreateReference(descriptorBytes);

        Assert.True(AcceleratorDescriptorParser.TryReadHeader(
            descriptorBytes,
            reference,
            out AcceleratorDescriptorHeader header,
            out AcceleratorDescriptorReference effectiveReference,
            out AcceleratorDescriptorValidationResult? headerFailure), headerFailure?.Message);
        Assert.Equal(reference, effectiveReference);
        Assert.Equal(AcceleratorClassId.TopologyQueue, header.AcceleratorClass);
        Assert.Equal(AcceleratorDeviceId.TopologyQueueMetadata, header.AcceleratorId);
        Assert.Equal(AcceleratorOperationKind.TopologyQueueContract, header.Operation);
        Assert.Equal(AcceleratorShapeKind.TopologyQueueMap, header.Shape);

        AcceleratorDescriptorValidationResult parse =
            L7SdcTestDescriptorFactory.ParseWithGuard(
                descriptorBytes,
                reference: reference);

        Assert.False(parse.IsValid);
        Assert.Null(parse.Descriptor);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedAcceleratorClass, parse.Fault);
        Assert.Contains("metadata-only", parse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TopologyQueueMetadataCannotIssueTokensResultsFaultsOrQueueWork()
    {
        AcceleratorCommandDescriptor matMulDescriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorCommandDescriptor topologyDescriptor = matMulDescriptor with
        {
            Header = matMulDescriptor.Header with
            {
                AcceleratorClass = AcceleratorClassId.TopologyQueue,
                AcceleratorId = AcceleratorDeviceId.TopologyQueueMetadata,
                Operation = AcceleratorOperationKind.TopologyQueueContract,
                Shape = AcceleratorShapeKind.TopologyQueueMap,
                ShapeRank = 1,
                ElementCount = 4
            },
            AcceleratorClass = AcceleratorClassId.TopologyQueue,
            AcceleratorId = AcceleratorDeviceId.TopologyQueueMetadata,
            Operation = AcceleratorOperationKind.TopologyQueueContract,
            Shape = AcceleratorShapeKind.TopologyQueueMap,
            ShapeRank = 1,
            ElementCount = 4
        };

        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(new TopologyQueueMetadataCapabilityProvider());
        AcceleratorCapabilityAcceptanceResult accepted =
            registry.AcceptCapability(
                TopologyQueueMetadataCapabilityProvider.AcceleratorId,
                topologyDescriptor.OwnerBinding,
                topologyDescriptor.OwnerGuardDecision);
        Assert.True(accepted.IsAccepted, accepted.RejectReason);

        var store = new AcceleratorTokenStore();
        AcceleratorTokenAdmissionResult admission =
            store.Create(
                topologyDescriptor,
                accepted,
                topologyDescriptor.OwnerGuardDecision.Evidence);

        Assert.True(admission.IsNonTrappingReject);
        Assert.Equal(AcceleratorTokenFaultCode.CapabilityNotAccepted, admission.FaultCode);
        Assert.Contains("metadata", admission.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, store.Count);
        Assert.Equal(AcceleratorTokenHandle.Invalid, admission.Handle);
        Assert.Null(admission.Token);
    }

    [Fact]
    public void UnselectedTopologyAndQueueCommandsRemainReservedAndParserRejected()
    {
        string[] closedCommands =
        [
            "ACCEL_QUERY_ABI",
            "ACCEL_QUERY_TOPOLOGY",
            "ACCEL_OPEN",
            "ACCEL_CLOSE",
            "ACCEL_BIND_QUEUE",
            "ACCEL_UNBIND_QUEUE"
        ];

        foreach (string mnemonic in closedCommands)
        {
            InstructionSupportStatus status =
                InstructionSupportStatusCatalog.GetStatus(mnemonic);

            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.Equal("Lane7TopologyQueue", status.ExtensionName);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(Enum.TryParse<InstructionsEnum>(mnemonic, ignoreCase: false, out _));
            Assert.DoesNotContain(mnemonic, IsaV4Surface.SystemDeviceCommandOpcodes);
            Assert.DoesNotContain(
                OpcodeRegistry.Opcodes,
                info => string.Equals(info.Mnemonic, mnemonic, StringComparison.Ordinal));
        }

        var raw = new VLIW_Instruction
        {
            OpCode = 267,
            DataTypeValue = DataTypeEnum.UINT64,
            PredicateMask = 0,
            Word1 = VLIW_Instruction.PackArchRegs(5, 0, 0),
            Src2Pointer = 0,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
        AcceleratorCarrierValidationResult carrier =
            AcceleratorDescriptorParser.ValidateNativeCarrier(
                in raw,
                opcode: 267,
                slotIndex: 7,
                hasDescriptorSideband: false);

        Assert.False(carrier.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.DescriptorCarrierDecodeFault, carrier.Fault);
        Assert.Contains("not a canonical", carrier.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompilerSurfaceKeepsTopologyQueueNoEmissionBoundary()
    {
        string[] threadMethods = typeof(HybridCpuThreadCompilerContext)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .Select(static method => method.Name)
            .ToArray();

        Assert.Equal(
            [nameof(HybridCpuThreadCompilerContext.CompileAcceleratorSubmit)],
            threadMethods
                .Where(name => name.Contains("Accelerator", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.DoesNotContain(threadMethods, name => name.Contains("Topology", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("QueryAbi", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("BindQueue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Open", StringComparison.OrdinalIgnoreCase));

        string compilerText = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        Assert.DoesNotContain("ACCEL_QUERY_TOPOLOGY", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_OPEN", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_BIND_QUEUE", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(TopologyQueueMetadataCapabilityProvider), compilerText, StringComparison.Ordinal);
    }

}
