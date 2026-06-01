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

public sealed class L7SdcFftDescriptorTaxonomyPhase09Tests
{
    [Fact]
    public void Phase09_FftTaxonomy_IsMetadataOnlyAndGrantsNoAuthority()
    {
        AcceleratorDescriptorTaxonomyEntry entry =
            AcceleratorDescriptorTaxonomyCatalog.FftMetadata;

        Assert.Equal(
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.Fft,
                AcceleratorDeviceId.FftMetadata,
                AcceleratorOperationKind.FftContract,
                AcceleratorShapeKind.Fft1D),
            entry.Key);
        Assert.Equal("fft.metadata.v1", entry.Name);
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
    public void Phase09_FftCapabilityProvider_IsQueryableButNotRuntimeRegistered()
    {
        var runtime = new ExternalAcceleratorRuntime(new Processor.MainMemoryArea());
        Assert.True(runtime.Capabilities.TryGetDescriptor(
            MatMulCapabilityProvider.AcceleratorId,
            out _));
        Assert.False(runtime.Capabilities.TryGetDescriptor(
            FftMetadataCapabilityProvider.AcceleratorId,
            out _));

        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(new FftMetadataCapabilityProvider());

        AcceleratorCapabilityQueryResult query =
            registry.Query(FftMetadataCapabilityProvider.AcceleratorId);

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
        Assert.Equal(AcceleratorDescriptorTaxonomyCatalog.FftMetadata.Key, descriptor.TaxonomyKey);
        Assert.Equal(AcceleratorDescriptorTaxonomyStatus.MetadataOnly, descriptor.TaxonomyStatus);
        Assert.False(descriptor.GrantsDescriptorAcceptanceAuthority);
        Assert.False(descriptor.GrantsTokenAuthority);
        Assert.False(descriptor.GrantsExecutionAuthority);
        Assert.False(descriptor.GrantsCommitAuthority);
        Assert.False(descriptor.GrantsCompilerEmissionAuthority);
        Assert.Equal(0u, descriptor.ResourceModel.MaxQueueOccupancy);
        Assert.Equal(0u, descriptor.ResourceModel.EstimateQueueOccupancy(1024));
        Assert.True(descriptor.TryGetOperation(
            "fft-contract",
            out AcceleratorOperationCapability? operation));
        Assert.True(operation!.SupportsDatatype("metadata"));
        Assert.True(operation.SupportsShape("fft-1d", elementCount: 1024, rank: 1));

        AcceleratorOwnerBinding owner = L7SdcTestDescriptorFactory.CreateOwnerBinding();
        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDescriptorAcceptance(
                owner,
                L7SdcTestDescriptorFactory.CreateGuardEvidence(owner));
        AcceleratorCapabilityAcceptanceResult accepted =
            registry.AcceptCapability(
                FftMetadataCapabilityProvider.AcceleratorId,
                owner,
                guardDecision);

        Assert.True(accepted.IsAccepted, accepted.RejectReason);
        Assert.False(accepted.GrantsDecodeAuthority);
        Assert.False(accepted.GrantsCommandSubmissionAuthority);
        Assert.False(accepted.GrantsExecutionAuthority);
        Assert.False(accepted.GrantsCommitAuthority);
    }

    [Fact]
    public void Phase09_FftDescriptorHeaderReadsButParserRejectsBeforeAcceptance()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor(
            acceleratorClass: AcceleratorClassId.Fft,
            acceleratorId: AcceleratorDeviceId.FftMetadata,
            operation: AcceleratorOperationKind.FftContract,
            datatype: AcceleratorDatatype.Float32,
            shape: AcceleratorShapeKind.Fft1D,
            shapeRank: 1,
            elementCount: 1024);
        AcceleratorDescriptorReference reference =
            L7SdcTestDescriptorFactory.CreateReference(descriptorBytes);

        Assert.True(AcceleratorDescriptorParser.TryReadHeader(
            descriptorBytes,
            reference,
            out AcceleratorDescriptorHeader header,
            out AcceleratorDescriptorReference effectiveReference,
            out AcceleratorDescriptorValidationResult? headerFailure), headerFailure?.Message);
        Assert.Equal(reference, effectiveReference);
        Assert.Equal(AcceleratorClassId.Fft, header.AcceleratorClass);
        Assert.Equal(AcceleratorDeviceId.FftMetadata, header.AcceleratorId);
        Assert.Equal(AcceleratorOperationKind.FftContract, header.Operation);
        Assert.Equal(AcceleratorShapeKind.Fft1D, header.Shape);

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
    public void Phase09_FftMetadataCannotIssueTokensResultsFaultsOrBackendWork()
    {
        AcceleratorCommandDescriptor matMulDescriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorCommandDescriptor fftDescriptor = matMulDescriptor with
        {
            Header = matMulDescriptor.Header with
            {
                AcceleratorClass = AcceleratorClassId.Fft,
                AcceleratorId = AcceleratorDeviceId.FftMetadata,
                Operation = AcceleratorOperationKind.FftContract,
                Shape = AcceleratorShapeKind.Fft1D,
                ShapeRank = 1,
                ElementCount = 1024
            },
            AcceleratorClass = AcceleratorClassId.Fft,
            AcceleratorId = AcceleratorDeviceId.FftMetadata,
            Operation = AcceleratorOperationKind.FftContract,
            Shape = AcceleratorShapeKind.Fft1D,
            ShapeRank = 1,
            ElementCount = 1024
        };

        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(new FftMetadataCapabilityProvider());
        AcceleratorCapabilityAcceptanceResult accepted =
            registry.AcceptCapability(
                FftMetadataCapabilityProvider.AcceleratorId,
                fftDescriptor.OwnerBinding,
                fftDescriptor.OwnerGuardDecision);
        Assert.True(accepted.IsAccepted, accepted.RejectReason);

        var store = new AcceleratorTokenStore();
        AcceleratorTokenAdmissionResult admission =
            store.Create(
                fftDescriptor,
                accepted,
                fftDescriptor.OwnerGuardDecision.Evidence);

        Assert.True(admission.IsNonTrappingReject);
        Assert.Equal(AcceleratorTokenFaultCode.CapabilityNotAccepted, admission.FaultCode);
        Assert.Contains("fft.metadata.v1", admission.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, store.Count);
        Assert.Equal(AcceleratorTokenHandle.Invalid, admission.Handle);
        Assert.Null(admission.Token);
    }

    [Fact]
    public void Phase09_AdjacentRicherDescriptorContoursRemainUnselectedAndParserRejected()
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
    public void Phase09_CompilerSurfaceKeepsFftAndAdjacentDescriptorNoEmissionBoundary()
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
        Assert.DoesNotContain(threadMethods, name => name.Contains("Fft", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Dsp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Crypto", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Compression", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Media", StringComparison.OrdinalIgnoreCase));

        string compilerText = ReadAllSourceText(Path.Combine(
            CompatFreezeScanner.FindRepoRoot(),
            "HybridCPU_Compiler"));
        Assert.DoesNotContain("fft.metadata.v1", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_FFT", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_DSP", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_CRYPTO", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_COMPRESS", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEL_MEDIA", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FftMetadataCapabilityProvider), compilerText, StringComparison.Ordinal);
    }

    private static string ReadAllSourceText(string root)
    {
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file => !CompatFreezeScanner.IsGeneratedPath(file))
                .Select(File.ReadAllText));
    }
}
