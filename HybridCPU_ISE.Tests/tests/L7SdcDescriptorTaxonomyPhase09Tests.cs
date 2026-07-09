using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcDescriptorTaxonomyPhase09Tests
{
    [Fact]
    public void Lane7DescriptorTaxonomy_PinsCurrentMatMulAndTensorMetadataOnly()
    {
        AcceleratorDescriptorTaxonomyEntry current =
            AcceleratorDescriptorTaxonomyCatalog.CurrentMatMul;
        AcceleratorDescriptorTaxonomyEntry tensor =
            AcceleratorDescriptorTaxonomyCatalog.TensorMetadata;

        Assert.Equal(
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.Matrix,
                AcceleratorDeviceId.ReferenceMatMul,
                AcceleratorOperationKind.MatMul,
                AcceleratorShapeKind.Matrix2D),
            current.Key);
        Assert.True(current.IsCurrentRuntimeContour);
        Assert.False(current.GrantsDescriptorAcceptanceAuthority);
        Assert.False(current.GrantsTokenAuthority);
        Assert.False(current.GrantsExecutionAuthority);
        Assert.False(current.GrantsCommitAuthority);
        Assert.False(current.GrantsCompilerEmissionAuthority);

        Assert.Equal(
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.Tensor,
                AcceleratorDeviceId.TensorMetadata,
                AcceleratorOperationKind.TensorContract,
                AcceleratorShapeKind.TensorND),
            tensor.Key);
        Assert.True(tensor.IsMetadataOnly);
        Assert.False(tensor.GrantsDescriptorAcceptanceAuthority);
        Assert.False(tensor.GrantsCapabilityAuthority);
        Assert.False(tensor.GrantsTokenAuthority);
        Assert.False(tensor.GrantsExecutionAuthority);
        Assert.False(tensor.GrantsCommitAuthority);
        Assert.False(tensor.GrantsCompilerEmissionAuthority);

        Assert.True(AcceleratorDescriptorTaxonomyCatalog.TryGetEntry(
            tensor.Key,
            out AcceleratorDescriptorTaxonomyEntry resolved));
        Assert.Same(tensor, resolved);
    }

    [Fact]
    public void TensorCapabilityMetadata_CanBeQueriedButDoesNotWidenRuntimeAuthority()
    {
        var runtime = new ExternalAcceleratorRuntime(new Processor.MainMemoryArea());
        Assert.True(runtime.Capabilities.TryGetDescriptor(
            MatMulCapabilityProvider.AcceleratorId,
            out AcceleratorCapabilityDescriptor? matMulDescriptor));
        Assert.Equal(AcceleratorDescriptorTaxonomyCatalog.CurrentMatMul.Key, matMulDescriptor!.TaxonomyKey);
        Assert.False(runtime.Capabilities.TryGetDescriptor(
            TensorMetadataCapabilityProvider.AcceleratorId,
            out _));

        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(new TensorMetadataCapabilityProvider());

        AcceleratorCapabilityQueryResult query =
            registry.Query(TensorMetadataCapabilityProvider.AcceleratorId);

        Assert.True(query.IsMetadataAvailable, query.RejectReason);
        Assert.False(query.GrantsDecodeAuthority);
        Assert.False(query.GrantsCommandSubmissionAuthority);
        Assert.False(query.GrantsExecutionAuthority);
        Assert.False(query.GrantsCommitAuthority);

        AcceleratorCapabilityDescriptor descriptor = query.Descriptor!;
        Assert.Equal(AcceleratorDescriptorTaxonomyCatalog.TensorMetadata.Key, descriptor.TaxonomyKey);
        Assert.Equal(AcceleratorDescriptorTaxonomyStatus.MetadataOnly, descriptor.TaxonomyStatus);
        Assert.False(descriptor.GrantsDescriptorAcceptanceAuthority);
        Assert.False(descriptor.GrantsTokenAuthority);
        Assert.False(descriptor.GrantsExecutionAuthority);
        Assert.False(descriptor.GrantsCommitAuthority);
        Assert.False(descriptor.GrantsCompilerEmissionAuthority);
        Assert.True(descriptor.TryGetOperation(
            "tensor-contract",
            out AcceleratorOperationCapability? operation));
        Assert.True(operation!.SupportsShape("tensor-nd", elementCount: 16, rank: 3));

        AcceleratorOwnerBinding owner = L7SdcTestDescriptorFactory.CreateOwnerBinding();
        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDescriptorAcceptance(
                owner,
                L7SdcTestDescriptorFactory.CreateGuardEvidence(owner));
        AcceleratorCapabilityAcceptanceResult accepted =
            registry.AcceptCapability(
                TensorMetadataCapabilityProvider.AcceleratorId,
                owner,
                guardDecision);

        Assert.True(accepted.IsAccepted, accepted.RejectReason);
        Assert.False(accepted.GrantsDecodeAuthority);
        Assert.False(accepted.GrantsCommandSubmissionAuthority);
        Assert.False(accepted.GrantsExecutionAuthority);
        Assert.False(accepted.GrantsCommitAuthority);
    }

    [Fact]
    public void TensorDescriptorBytes_AreHeaderReadableButParserRejectedBeforeTokenPublication()
    {
        byte[] tensorDescriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor(
            acceleratorClass: AcceleratorClassId.Tensor,
            acceleratorId: AcceleratorDeviceId.TensorMetadata,
            operation: AcceleratorOperationKind.TensorContract,
            shape: AcceleratorShapeKind.TensorND,
            shapeRank: 3,
            elementCount: 8);
        AcceleratorDescriptorReference reference =
            L7SdcTestDescriptorFactory.CreateReference(tensorDescriptorBytes);

        Assert.True(AcceleratorDescriptorParser.TryReadHeader(
            tensorDescriptorBytes,
            reference,
            out AcceleratorDescriptorHeader header,
            out AcceleratorDescriptorReference effectiveReference,
            out AcceleratorDescriptorValidationResult? headerFailure), headerFailure?.Message);
        Assert.Equal(reference, effectiveReference);
        Assert.Equal(AcceleratorClassId.Tensor, header.AcceleratorClass);
        Assert.Equal(AcceleratorDeviceId.TensorMetadata, header.AcceleratorId);
        Assert.Equal(AcceleratorOperationKind.TensorContract, header.Operation);
        Assert.Equal(AcceleratorShapeKind.TensorND, header.Shape);

        AcceleratorDescriptorValidationResult parse =
            L7SdcTestDescriptorFactory.ParseWithGuard(
                tensorDescriptorBytes,
                reference: reference);

        Assert.False(parse.IsDescriptorAbiAccepted);
        Assert.Null(parse.Descriptor);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedAcceleratorClass, parse.Fault);
        Assert.Contains("metadata-only", parse.Message);
    }

    [Fact]
    public void Lane6Dsc2Taxonomy_RemainsParserOnlyAndNoCompilerOrTokenAuthority()
    {
        DmaStreamComputeDsc2CapabilitySet dsc2 =
            DmaStreamComputeDsc2CapabilitySet.ParserOnlyFoundation;

        Assert.True(dsc2.Has(
            DmaStreamComputeDsc2CapabilityId.Tile2D,
            DmaStreamComputeDsc2CapabilityStage.ParserKnown |
            DmaStreamComputeDsc2CapabilityStage.Validation |
            DmaStreamComputeDsc2CapabilityStage.FootprintNormalization));
        Assert.False(dsc2.GrantsExecution);
        Assert.False(dsc2.GrantsCompilerLowering);

        var owner = new DmaStreamComputeOwnerBinding
        {
            OwnerVirtualThreadId = 1,
            OwnerContextId = 2,
            OwnerCoreId = 3,
            OwnerPodId = 4,
            OwnerDomainTag = 0xD5C,
            DeviceId = DmaStreamComputeDescriptor.CanonicalLane6DeviceId
        };
        DmaStreamComputeCapabilityQueryResult query =
            DmaStreamComputeCapabilityQueryResult.Capture(owner);

        Assert.True(query.IsAccepted);
        Assert.False(query.CanIssueToken);
        Assert.False(query.CanPublishMemory);
        Assert.False(query.CanProductionLower);
    }
}
