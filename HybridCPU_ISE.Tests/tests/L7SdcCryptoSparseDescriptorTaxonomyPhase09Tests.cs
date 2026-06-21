using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcCryptoSparseDescriptorTaxonomyPhase09Tests
{
    [Fact]
    public void CryptoHashTaxonomy_IsMetadataOnlyAndGrantsNoAuthority()
    {
        AssertMetadataOnlyTaxonomy(
            AcceleratorDescriptorTaxonomyCatalog.CryptoHashMetadata,
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.CryptoHash,
                AcceleratorDeviceId.CryptoHashMetadata,
                AcceleratorOperationKind.CryptoHashContract,
                AcceleratorShapeKind.CryptoHashBlock),
            "crypto.hash.metadata.v1");
    }

    [Fact]
    public void SparseGraphTaxonomy_IsMetadataOnlyAndGrantsNoAuthority()
    {
        AssertMetadataOnlyTaxonomy(
            AcceleratorDescriptorTaxonomyCatalog.SparseGraphMetadata,
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.SparseGraph,
                AcceleratorDeviceId.SparseGraphMetadata,
                AcceleratorOperationKind.SparseGraphContract,
                AcceleratorShapeKind.SparseGraphCsr),
            "sparse.graph.metadata.v1");
    }

    [Fact]
    public void CryptoHashCapabilityProvider_IsQueryableButNotRuntimeRegistered()
    {
        AssertProviderQueryableButNotRuntimeRegistered(
            new CryptoHashMetadataCapabilityProvider(),
            CryptoHashMetadataCapabilityProvider.AcceleratorId,
            AcceleratorDescriptorTaxonomyCatalog.CryptoHashMetadata.Key,
            "crypto-hash-contract",
            "crypto-hash-block",
            rank: 1);
    }

    [Fact]
    public void SparseGraphCapabilityProvider_IsQueryableButNotRuntimeRegistered()
    {
        AssertProviderQueryableButNotRuntimeRegistered(
            new SparseGraphMetadataCapabilityProvider(),
            SparseGraphMetadataCapabilityProvider.AcceleratorId,
            AcceleratorDescriptorTaxonomyCatalog.SparseGraphMetadata.Key,
            "sparse-graph-contract",
            "sparse-graph-csr",
            rank: 2);
    }

    [Fact]
    public void CryptoHashDescriptorHeaderReadsButParserRejectsBeforeAcceptance()
    {
        AssertDescriptorHeaderReadableButParserRejected(
            AcceleratorClassId.CryptoHash,
            AcceleratorDeviceId.CryptoHashMetadata,
            AcceleratorOperationKind.CryptoHashContract,
            AcceleratorShapeKind.CryptoHashBlock,
            shapeRank: 1,
            elementCount: 64);
    }

    [Fact]
    public void SparseGraphDescriptorHeaderReadsButParserRejectsBeforeAcceptance()
    {
        AssertDescriptorHeaderReadableButParserRejected(
            AcceleratorClassId.SparseGraph,
            AcceleratorDeviceId.SparseGraphMetadata,
            AcceleratorOperationKind.SparseGraphContract,
            AcceleratorShapeKind.SparseGraphCsr,
            shapeRank: 2,
            elementCount: 256);
    }

    [Fact]
    public void CryptoHashMetadataCannotIssueTokensResultsFaultsOrBackendWork()
    {
        AssertMetadataCannotIssueTokens(
            new CryptoHashMetadataCapabilityProvider(),
            CryptoHashMetadataCapabilityProvider.AcceleratorId,
            AcceleratorClassId.CryptoHash,
            AcceleratorDeviceId.CryptoHashMetadata,
            AcceleratorOperationKind.CryptoHashContract,
            AcceleratorShapeKind.CryptoHashBlock,
            expectedName: "crypto.hash.metadata.v1");
    }

    [Fact]
    public void SparseGraphMetadataCannotIssueTokensResultsFaultsOrBackendWork()
    {
        AssertMetadataCannotIssueTokens(
            new SparseGraphMetadataCapabilityProvider(),
            SparseGraphMetadataCapabilityProvider.AcceleratorId,
            AcceleratorClassId.SparseGraph,
            AcceleratorDeviceId.SparseGraphMetadata,
            AcceleratorOperationKind.SparseGraphContract,
            AcceleratorShapeKind.SparseGraphCsr,
            expectedName: "sparse.graph.metadata.v1");
    }

    [Fact]
    public void AdjacentRicherDescriptorContoursRemainUnselectedAndParserRejected()
    {
        string[] closedClassNames =
        [
            "Dsp",
            "Compression",
            "Media"
        ];

        foreach (string name in closedClassNames)
        {
            Assert.False(Enum.TryParse<AcceleratorClassId>(name, ignoreCase: false, out _));
            Assert.False(Enum.TryParse<AcceleratorDeviceId>($"{name}Metadata", ignoreCase: false, out _));
            Assert.False(Enum.TryParse<AcceleratorOperationKind>($"{name}Contract", ignoreCase: false, out _));
        }

        Assert.False(AcceleratorDescriptorTaxonomyCatalog.TryGetEntry(
            (AcceleratorClassId)7,
            (AcceleratorDeviceId)7,
            (AcceleratorOperationKind)7,
            (AcceleratorShapeKind)7,
            out _));

        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor(
            acceleratorClass: (AcceleratorClassId)7,
            acceleratorId: (AcceleratorDeviceId)7,
            operation: (AcceleratorOperationKind)7,
            datatype: AcceleratorDatatype.Float32,
            shape: (AcceleratorShapeKind)7,
            shapeRank: 1,
            elementCount: 16);

        AcceleratorDescriptorValidationResult parse =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(parse.IsValid);
        Assert.Null(parse.Descriptor);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedAcceleratorClass, parse.Fault);
        Assert.DoesNotContain("metadata-only", parse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompilerSurfaceKeepsCryptoSparseAndAdjacentDescriptorNoEmissionBoundary()
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
        Assert.DoesNotContain(threadMethods, name => name.Contains("Crypto", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Sparse", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Graph", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Dsp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Compression", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Media", StringComparison.OrdinalIgnoreCase));

        string compilerText = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        Assert.DoesNotContain("crypto.hash.metadata.v1", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("sparse.graph.metadata.v1", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_CRYPTO", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_HASH", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_SPARSE", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_GRAPH", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_DSP", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_COMPRESS", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_MEDIA", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CryptoHashMetadataCapabilityProvider), compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(SparseGraphMetadataCapabilityProvider), compilerText, StringComparison.Ordinal);
    }

    private static void AssertMetadataOnlyTaxonomy(
        AcceleratorDescriptorTaxonomyEntry entry,
        AcceleratorDescriptorTaxonomyKey expectedKey,
        string expectedName)
    {
        Assert.Equal(expectedKey, entry.Key);
        Assert.Equal(expectedName, entry.Name);
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

    private static void AssertProviderQueryableButNotRuntimeRegistered(
        IAcceleratorCapabilityProvider provider,
        string acceleratorId,
        AcceleratorDescriptorTaxonomyKey expectedKey,
        string operationName,
        string shapeName,
        byte rank)
    {
        var runtime = new ExternalAcceleratorRuntime(new Processor.MainMemoryArea());
        Assert.True(runtime.Capabilities.TryGetDescriptor(
            MatMulCapabilityProvider.AcceleratorId,
            out _));
        Assert.False(runtime.Capabilities.TryGetDescriptor(acceleratorId, out _));

        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(provider);

        AcceleratorCapabilityQueryResult query = registry.Query(acceleratorId);

        Assert.True(query.IsMetadataAvailable, query.RejectReason);
        Assert.False(query.GrantsDecodeAuthority);
        Assert.False(query.GrantsCommandSubmissionAuthority);
        Assert.False(query.GrantsExecutionAuthority);
        Assert.False(query.GrantsCommitAuthority);

        AcceleratorCapabilityDescriptor descriptor = query.Descriptor!;
        Assert.Equal(expectedKey, descriptor.TaxonomyKey);
        Assert.Equal(AcceleratorDescriptorTaxonomyStatus.MetadataOnly, descriptor.TaxonomyStatus);
        Assert.False(descriptor.GrantsDescriptorAcceptanceAuthority);
        Assert.False(descriptor.GrantsTokenAuthority);
        Assert.False(descriptor.GrantsExecutionAuthority);
        Assert.False(descriptor.GrantsCommitAuthority);
        Assert.False(descriptor.GrantsCompilerEmissionAuthority);
        Assert.Equal(0u, descriptor.ResourceModel.MaxQueueOccupancy);
        Assert.Equal(0u, descriptor.ResourceModel.EstimateQueueOccupancy(128));
        Assert.True(descriptor.TryGetOperation(
            operationName,
            out AcceleratorOperationCapability? operation));
        Assert.True(operation!.SupportsDatatype("metadata"));
        Assert.True(operation.SupportsShape(shapeName, elementCount: 128, rank: rank));

        AcceleratorOwnerBinding owner = L7SdcTestDescriptorFactory.CreateOwnerBinding();
        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDescriptorAcceptance(
                owner,
                L7SdcTestDescriptorFactory.CreateGuardEvidence(owner));
        AcceleratorCapabilityAcceptanceResult accepted =
            registry.AcceptCapability(acceleratorId, owner, guardDecision);

        Assert.True(accepted.IsAccepted, accepted.RejectReason);
        Assert.False(accepted.GrantsDecodeAuthority);
        Assert.False(accepted.GrantsCommandSubmissionAuthority);
        Assert.False(accepted.GrantsExecutionAuthority);
        Assert.False(accepted.GrantsCommitAuthority);
    }

    private static void AssertDescriptorHeaderReadableButParserRejected(
        AcceleratorClassId acceleratorClass,
        AcceleratorDeviceId acceleratorId,
        AcceleratorOperationKind operation,
        AcceleratorShapeKind shape,
        ushort shapeRank,
        ulong elementCount)
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor(
            acceleratorClass: acceleratorClass,
            acceleratorId: acceleratorId,
            operation: operation,
            datatype: AcceleratorDatatype.Float32,
            shape: shape,
            shapeRank: shapeRank,
            elementCount: elementCount);
        AcceleratorDescriptorReference reference =
            L7SdcTestDescriptorFactory.CreateReference(descriptorBytes);

        Assert.True(AcceleratorDescriptorParser.TryReadHeader(
            descriptorBytes,
            reference,
            out AcceleratorDescriptorHeader header,
            out AcceleratorDescriptorReference effectiveReference,
            out AcceleratorDescriptorValidationResult? headerFailure), headerFailure?.Message);
        Assert.Equal(reference, effectiveReference);
        Assert.Equal(acceleratorClass, header.AcceleratorClass);
        Assert.Equal(acceleratorId, header.AcceleratorId);
        Assert.Equal(operation, header.Operation);
        Assert.Equal(shape, header.Shape);

        AcceleratorDescriptorValidationResult parse =
            L7SdcTestDescriptorFactory.ParseWithGuard(
                descriptorBytes,
                reference: reference);

        Assert.False(parse.IsValid);
        Assert.Null(parse.Descriptor);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedAcceleratorClass, parse.Fault);
        Assert.Contains("metadata-only", parse.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertMetadataCannotIssueTokens(
        IAcceleratorCapabilityProvider provider,
        string acceleratorId,
        AcceleratorClassId acceleratorClass,
        AcceleratorDeviceId acceleratorDevice,
        AcceleratorOperationKind operation,
        AcceleratorShapeKind shape,
        string expectedName)
    {
        AcceleratorCommandDescriptor matMulDescriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorCommandDescriptor metadataDescriptor = matMulDescriptor with
        {
            Header = matMulDescriptor.Header with
            {
                AcceleratorClass = acceleratorClass,
                AcceleratorId = acceleratorDevice,
                Operation = operation,
                Shape = shape,
                ShapeRank = 1,
                ElementCount = 128
            },
            AcceleratorClass = acceleratorClass,
            AcceleratorId = acceleratorDevice,
            Operation = operation,
            Shape = shape,
            ShapeRank = 1,
            ElementCount = 128
        };

        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(provider);
        AcceleratorCapabilityAcceptanceResult accepted =
            registry.AcceptCapability(
                acceleratorId,
                metadataDescriptor.OwnerBinding,
                metadataDescriptor.OwnerGuardDecision);
        Assert.True(accepted.IsAccepted, accepted.RejectReason);

        var store = new AcceleratorTokenStore();
        AcceleratorTokenAdmissionResult admission =
            store.Create(
                metadataDescriptor,
                accepted,
                metadataDescriptor.OwnerGuardDecision.Evidence);

        Assert.True(admission.IsNonTrappingReject);
        Assert.Equal(AcceleratorTokenFaultCode.CapabilityNotAccepted, admission.FaultCode);
        Assert.Contains(expectedName, admission.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, store.Count);
        Assert.Equal(AcceleratorTokenHandle.Invalid, admission.Handle);
        Assert.Null(admission.Token);
    }

}
