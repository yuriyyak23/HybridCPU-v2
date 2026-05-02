using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeDsc2Phase07Tests
{
    [Fact]
    public void Phase07_Dsc1CompatibilityRemainsStrictAndCurrentOnly()
    {
        byte[] descriptorBytes = DmaStreamComputeTestDescriptorFactory.BuildDescriptor();
        DmaStreamComputeDescriptorReference reference =
            DmaStreamComputeTestDescriptorFactory.CreateReference(descriptorBytes);
        DmaStreamComputeValidationResult valid =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                DmaStreamComputeTestDescriptorFactory.CreateGuardDecision(descriptorBytes, reference),
                reference);

        Assert.True(valid.IsValid, valid.Message);
        Assert.Equal(DmaStreamComputeDescriptorParser.CurrentAbiVersion, valid.Descriptor!.AbiVersion);
        Assert.Equal(DmaStreamComputeRangeEncoding.InlineContiguous, valid.Descriptor.RangeEncoding);
        Assert.Equal(DmaStreamComputePartialCompletionPolicy.AllOrNone, valid.Descriptor.PartialCompletionPolicy);
        Assert.Equal(DmaStreamComputeShapeKind.Contiguous1D, valid.Descriptor.Shape);
        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);

        byte[] reserved = DmaStreamComputeTestDescriptorFactory.BuildDescriptor();
        WriteUInt32(reserved, HeaderFlagsOffset, 1);
        DmaStreamComputeValidationResult reservedResult =
            DmaStreamComputeDescriptorParser.Parse(
                reserved,
                DmaStreamComputeTestDescriptorFactory.CreateGuardDecision(reserved));
        Assert.False(reservedResult.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.ReservedFieldFault, reservedResult.Fault);

        byte[] unknownAbi = DmaStreamComputeTestDescriptorFactory.BuildDescriptor();
        WriteUInt16(unknownAbi, HeaderAbiVersionOffset, 2);
        DmaStreamComputeValidationResult unknownAbiResult =
            DmaStreamComputeDescriptorParser.Parse(unknownAbi);
        Assert.False(unknownAbiResult.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.UnsupportedAbiVersion, unknownAbiResult.Fault);

        AssertDsc1RejectsEncoding(
            HeaderRangeEncodingOffset,
            2,
            DmaStreamComputeValidationFault.UnsupportedShape);
        AssertDsc1RejectsEncoding(
            HeaderPartialCompletionPolicyOffset,
            2,
            DmaStreamComputeValidationFault.ReservedFieldFault);
        AssertDsc1RejectsEncoding(
            HeaderMaskRangeCountOffset,
            1,
            DmaStreamComputeValidationFault.ReservedFieldFault);
        AssertDsc1RejectsEncoding(
            HeaderAccumulatorRangeCountOffset,
            1,
            DmaStreamComputeValidationFault.ReservedFieldFault);
    }

    [Fact]
    public void Phase07_Dsc2KnownParserOnlyStridedDescriptorNormalizesExactWithoutExecutionAuthority()
    {
        byte[] descriptorBytes = BuildDsc2(
            PhysicalAddressSpaceExtension(),
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0x1000, 4),
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Write, 4, 4, 0x9000, 4));

        DmaStreamComputeDsc2ValidationResult result = ParseDsc2(descriptorBytes);

        Assert.True(result.IsParserAccepted, result.Message);
        Assert.False(result.ExecutionEnabled);
        Assert.False(result.CanIssueToken);
        Assert.False(result.CanPublishMemory);
        Assert.False(result.CanProductionLower);

        DmaStreamComputeDsc2Descriptor descriptor = result.RequireParserOnlyDescriptor();
        Assert.True(descriptor.IsParserOnly);
        Assert.False(descriptor.ExecutionEnabled);
        Assert.False(descriptor.CanIssueToken);
        Assert.False(descriptor.CanPublishMemory);
        Assert.False(descriptor.CanProductionLower);
        Assert.Equal(DmaStreamComputeDsc2ExecutionState.ParserOnlyExecutionDisabled, descriptor.ExecutionState);
        Assert.Equal(DmaStreamComputeDsc2AddressSpaceKind.Physical, descriptor.AddressSpace);
        Assert.True(descriptor.NormalizedFootprint.IsExact);
        Assert.Single(descriptor.NormalizedFootprint.ReadRanges);
        Assert.Single(descriptor.NormalizedFootprint.WriteRanges);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x1000, 16), descriptor.NormalizedFootprint.ReadRanges[0]);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x9000, 16), descriptor.NormalizedFootprint.WriteRanges[0]);
        Assert.NotEqual(0UL, descriptor.NormalizedFootprint.NormalizedFootprintHash);
    }

    [Fact]
    public void Phase07_Dsc2MalformedHeadersAndExtensionTablesReject()
    {
        (string Name, Action<byte[]> Mutate, DmaStreamComputeValidationFault Fault)[] cases =
        {
            (
                "bad header size",
                bytes => WriteUInt16(bytes, HeaderSizeOffset, 64),
                DmaStreamComputeValidationFault.DescriptorDecodeFault),
            (
                "unaligned extension table",
                bytes => WriteUInt32(bytes, ExtensionTableOffsetOffset, DmaStreamComputeDescriptorParser.Dsc2HeaderSize + 4),
                DmaStreamComputeValidationFault.MalformedExtension),
            (
                "extension count exceeds table bytes",
                bytes => WriteUInt16(bytes, ExtensionCountOffset, 4),
                DmaStreamComputeValidationFault.MalformedExtension),
            (
                "extension block too short",
                bytes => WriteUInt32(bytes, DmaStreamComputeDescriptorParser.Dsc2HeaderSize + ExtensionLengthFieldOffset, 16),
                DmaStreamComputeValidationFault.MalformedExtension),
            (
                "bad block alignment",
                bytes => WriteUInt16(bytes, DmaStreamComputeDescriptorParser.Dsc2HeaderSize + ExtensionAlignmentFieldOffset, 3),
                DmaStreamComputeValidationFault.MalformedExtension)
        };

        foreach ((string name, Action<byte[]> mutate, DmaStreamComputeValidationFault fault) in cases)
        {
            byte[] descriptorBytes = BuildDsc2(
                PhysicalAddressSpaceExtension(),
                StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0x1000, 4));
            mutate(descriptorBytes);

            DmaStreamComputeDsc2ValidationResult result = ParseDsc2(descriptorBytes);

            Assert.False(result.IsParserAccepted);
            Assert.Equal(fault, result.Fault);
            Assert.Contains("DSC2", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Phase07_Dsc2UnknownExtensionCompatibilityRulesAreExplicit()
    {
        byte[] unknownRequired = BuildDsc2(
            UnknownExtension(0x9000, DmaStreamComputeDsc2ExtensionFlags.Required | DmaStreamComputeDsc2ExtensionFlags.Semantic),
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0x1000, 4));
        DmaStreamComputeDsc2ValidationResult requiredResult = ParseDsc2(unknownRequired);
        Assert.False(requiredResult.IsParserAccepted);
        Assert.Equal(DmaStreamComputeValidationFault.UnsupportedCapability, requiredResult.Fault);

        byte[] unknownOptionalSemantic = BuildDsc2(
            UnknownExtension(0x9001, DmaStreamComputeDsc2ExtensionFlags.Semantic),
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0x1000, 4));
        DmaStreamComputeDsc2ValidationResult semanticResult = ParseDsc2(unknownOptionalSemantic);
        Assert.False(semanticResult.IsParserAccepted);
        Assert.Equal(DmaStreamComputeValidationFault.UnsupportedCapability, semanticResult.Fault);

        byte[] unknownOptionalNonSemantic = BuildDsc2(
            UnknownExtension(0x9002, DmaStreamComputeDsc2ExtensionFlags.NonSemantic),
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0x1000, 4));
        DmaStreamComputeDsc2ValidationResult nonSemanticResult = ParseDsc2(unknownOptionalNonSemantic);
        Assert.True(nonSemanticResult.IsParserAccepted, nonSemanticResult.Message);
    }

    [Fact]
    public void Phase07_Dsc2AbsentCapabilityRejectsDependentExtension()
    {
        byte[] descriptorBytes = BuildDsc2(
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0x1000, 4));
        var missingStridedCapability = DmaStreamComputeDsc2CapabilitySet.Create(
            new DmaStreamComputeDsc2CapabilityGrant(
                DmaStreamComputeDsc2CapabilityId.AddressSpace,
                DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                DmaStreamComputeDsc2CapabilityStage.Validation |
                DmaStreamComputeDsc2CapabilityStage.BackendAddressSpace));

        DmaStreamComputeDsc2ValidationResult result =
            ParseDsc2(descriptorBytes, missingStridedCapability);

        Assert.False(result.IsParserAccepted);
        Assert.Equal(DmaStreamComputeValidationFault.UnsupportedCapability, result.Fault);
        Assert.False(result.ExecutionEnabled);
        Assert.False(result.CanIssueToken);
    }

    [Fact]
    public void Phase07_Dsc2NormalizedFootprintsAreDeterministicExactConservativeAndRejectUnsafeCases()
    {
        byte[] exactBytes = BuildDsc2(
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0x1000, 4));

        DmaStreamComputeDsc2Descriptor exactA = ParseDsc2(exactBytes).RequireParserOnlyDescriptor();
        DmaStreamComputeDsc2Descriptor exactB = ParseDsc2(exactBytes).RequireParserOnlyDescriptor();
        Assert.True(exactA.NormalizedFootprint.IsExact);
        Assert.Equal(exactA.NormalizedFootprint.NormalizedFootprintHash, exactB.NormalizedFootprint.NormalizedFootprintHash);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x1000, 16), exactA.NormalizedFootprint.ReadRanges[0]);

        byte[] conservativeBytes = BuildDsc2(
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 80, 0x2000, 8));
        DmaStreamComputeDsc2Descriptor conservative =
            ParseDsc2(conservativeBytes).RequireParserOnlyDescriptor();
        Assert.True(conservative.NormalizedFootprint.IsConservative);
        Assert.Single(conservative.NormalizedFootprint.ReadRanges);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x2000, 636), conservative.NormalizedFootprint.ReadRanges[0]);

        byte[] overflowBytes = BuildDsc2(
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 3, ulong.MaxValue - 3, 4));
        DmaStreamComputeDsc2ValidationResult overflow = ParseDsc2(overflowBytes);
        Assert.False(overflow.IsParserAccepted);
        Assert.Equal(DmaStreamComputeValidationFault.RangeOverflow, overflow.Fault);

        byte[] underApproxBytes = BuildDsc2(
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0x3000, 4),
            FootprintSummaryExtension(
                DmaStreamComputeDsc2FootprintOutcomeKind.Conservative,
                new DmaStreamComputeMemoryRange(0x3000, 8),
                default));
        DmaStreamComputeDsc2ValidationResult underApprox = ParseDsc2(underApproxBytes);
        Assert.False(underApprox.IsParserAccepted);
        Assert.Equal(DmaStreamComputeValidationFault.UnderApproximatedFootprintFault, underApprox.Fault);
    }

    [Fact]
    public void Phase07_Dsc2TileAndScatterGatherFootprintsNormalizeWithoutExecutionAuthority()
    {
        byte[] descriptorBytes = BuildDsc2(
            ScatterGatherExtension(
                DmaStreamComputeDsc2AccessKind.Read,
                4,
                new DmaStreamComputeMemoryRange(0x4000, 8),
                new DmaStreamComputeMemoryRange(0x5000, 8)),
            Tile2DExtension(
                DmaStreamComputeDsc2AccessKind.Write,
                4,
                rows: 2,
                columns: 2,
                baseAddress: 0x9000,
                rowStride: 8,
                columnStride: 4));

        DmaStreamComputeDsc2Descriptor descriptor =
            ParseDsc2(descriptorBytes).RequireParserOnlyDescriptor();

        Assert.True(descriptor.NormalizedFootprint.IsExact);
        Assert.Equal(2, descriptor.NormalizedFootprint.ReadRanges.Count);
        Assert.Single(descriptor.NormalizedFootprint.WriteRanges);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x4000, 8), descriptor.NormalizedFootprint.ReadRanges[0]);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x5000, 8), descriptor.NormalizedFootprint.ReadRanges[1]);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x9000, 16), descriptor.NormalizedFootprint.WriteRanges[0]);
        Assert.False(descriptor.CanIssueToken);
        Assert.False(descriptor.CanPublishMemory);
    }

    [Fact]
    public void Phase07_Dsc2AddressSpaceDeviceDomainAndMappingEpochMismatchesReject()
    {
        byte[] deviceMismatch = BuildDsc2(
            AddressSpaceExtension(
                DmaStreamComputeDsc2AddressSpaceKind.Physical,
                DeviceId + 1,
                OwnerDomainTag,
                0),
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0x1000, 4));
        AssertAddressSpaceFault(deviceMismatch);

        byte[] domainMismatch = BuildDsc2(
            AddressSpaceExtension(
                DmaStreamComputeDsc2AddressSpaceKind.Physical,
                DeviceId,
                OwnerDomainTag + 1,
                0),
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0x1000, 4));
        AssertAddressSpaceFault(domainMismatch);

        byte[] missingEpoch = BuildDsc2(
            addressSpace: DmaStreamComputeDsc2AddressSpaceKind.IommuTranslated,
            extensions: new[]
            {
                AddressSpaceExtension(
                    DmaStreamComputeDsc2AddressSpaceKind.IommuTranslated,
                    DeviceId,
                    OwnerDomainTag,
                    0),
                StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0xA000, 4)
            });
        AssertAddressSpaceFault(missingEpoch);

        byte[] summaryMismatch = BuildDsc2(
            addressSpace: DmaStreamComputeDsc2AddressSpaceKind.Physical,
            extensions: new[]
            {
                AddressSpaceExtension(
                    DmaStreamComputeDsc2AddressSpaceKind.IommuTranslated,
                    DeviceId,
                    OwnerDomainTag,
                    0x123),
                StridedRangeExtension(DmaStreamComputeDsc2AccessKind.Read, 4, 4, 0xA000, 4)
            });
        AssertAddressSpaceFault(summaryMismatch);
    }

    [Fact]
    public void Phase07_Dsc2ParserKnownCapabilitiesDoNotEnableTokenIssueMemoryEffectsOrCompilerLowering()
    {
        byte[] descriptorBytes = BuildDsc2(
            PhysicalAddressSpaceExtension(),
            StridedRangeExtension(DmaStreamComputeDsc2AccessKind.ReadWrite, 4, 4, 0x1000, 4));
        var metadataPlusFutureExecutionWords = DmaStreamComputeDsc2CapabilitySet.Create(
            DmaStreamComputeDsc2CapabilitySet.ParserOnlyFoundation.Grants
                .Concat(new[]
                {
                    new DmaStreamComputeDsc2CapabilityGrant(
                        DmaStreamComputeDsc2CapabilityId.StridedRange,
                        DmaStreamComputeDsc2CapabilityStage.Execution |
                        DmaStreamComputeDsc2CapabilityStage.CompilerLowering)
                })
                .ToArray());

        DmaStreamComputeDsc2Descriptor descriptor =
            ParseDsc2(descriptorBytes, metadataPlusFutureExecutionWords).RequireParserOnlyDescriptor();

        Assert.True(descriptor.CapabilitySet.GrantsExecution);
        Assert.True(descriptor.CapabilitySet.GrantsCompilerLowering);
        Assert.False(descriptor.ExecutionEnabled);
        Assert.False(descriptor.CanIssueToken);
        Assert.False(descriptor.CanPublishMemory);
        Assert.False(descriptor.CanProductionLower);

        MethodInfo[] compilerMethods = typeof(HybridCpuThreadCompilerContext).GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.DoesNotContain(
            compilerMethods,
            static method => method.Name.Contains("Dsc2", StringComparison.OrdinalIgnoreCase));

        string compilerText = ReadAllSourceText(Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_Compiler"));
        Assert.DoesNotContain("DmaStreamComputeDsc2", compilerText, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase07_CurrentExecutableBoundariesRemainFailClosedAndPhase06ResolverIsNotWiredIntoRuntime()
    {
        DmaStreamComputeDescriptor dsc1Descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var core = new Processor.CPU_Core(0);

        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);
        Assert.Throws<InvalidOperationException>(
            () => new DmaStreamComputeMicroOp(dsc1Descriptor).Execute(ref core));

        SystemDeviceCommandMicroOp[] l7Carriers =
        {
            new AcceleratorQueryCapsMicroOp(),
            new AcceleratorSubmitMicroOp(),
            new AcceleratorPollMicroOp(),
            new AcceleratorWaitMicroOp(),
            new AcceleratorCancelMicroOp(),
            new AcceleratorFenceMicroOp()
        };

        foreach (SystemDeviceCommandMicroOp carrier in l7Carriers)
        {
            Assert.False(carrier.WritesRegister);
            Assert.Throws<InvalidOperationException>(() => carrier.Execute(ref core));
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string runtimeText = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Execution",
            "DmaStreamCompute",
            "DmaStreamComputeRuntime.cs"));
        string microOpText = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "MicroOps",
            "DmaStreamComputeMicroOp.cs"));

        Assert.DoesNotContain("AddressingBackendResolver", runtimeText, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryAddressSpaceKind", runtimeText, StringComparison.Ordinal);
        Assert.DoesNotContain("ParseDsc2", runtimeText, StringComparison.Ordinal);
        Assert.DoesNotContain("ParseDsc2", microOpText, StringComparison.Ordinal);
    }

    private const ulong IdentityHash = 0xD5C2000000000007UL;
    private const ulong CapabilityHash = 0xCA9AB111UL;
    private const ulong OwnerDomainTag = 0xD0A11UL;
    private const uint DeviceId = DmaStreamComputeDescriptor.CanonicalLane6DeviceId;

    private const int HeaderAbiVersionOffset = 4;
    private const int HeaderFlagsOffset = 12;
    private const int HeaderRangeEncodingOffset = 46;
    private const int HeaderMaskRangeCountOffset = 52;
    private const int HeaderAccumulatorRangeCountOffset = 54;
    private const int HeaderPartialCompletionPolicyOffset = 56;

    private const int HeaderSizeOffset = 8;
    private const int ExtensionTableOffsetOffset = 16;
    private const int ExtensionCountOffset = 20;
    private const int ExtensionLengthFieldOffset = 8;
    private const int ExtensionAlignmentFieldOffset = 6;

    private static void AssertDsc1RejectsEncoding(
        int offset,
        ushort value,
        DmaStreamComputeValidationFault expectedFault)
    {
        byte[] descriptorBytes = DmaStreamComputeTestDescriptorFactory.BuildDescriptor();
        WriteUInt16(descriptorBytes, offset, value);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                DmaStreamComputeTestDescriptorFactory.CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Equal(expectedFault, result.Fault);
    }

    private static void AssertAddressSpaceFault(byte[] descriptorBytes)
    {
        DmaStreamComputeDsc2ValidationResult result = ParseDsc2(descriptorBytes);
        Assert.False(result.IsParserAccepted);
        Assert.Equal(DmaStreamComputeValidationFault.AddressSpaceFault, result.Fault);
        Assert.False(result.ExecutionEnabled);
    }

    private static DmaStreamComputeDsc2ValidationResult ParseDsc2(
        byte[] descriptorBytes,
        DmaStreamComputeDsc2CapabilitySet? capabilitySet = null)
    {
        DmaStreamComputeDescriptorReference reference = CreateDsc2Reference(descriptorBytes);
        DmaStreamComputeStructuralReadResult structuralRead =
            DmaStreamComputeDescriptorParser.ReadDsc2StructuralOwnerBinding(
                descriptorBytes,
                reference);
        DmaStreamComputeOwnerGuardDecision guardDecision = structuralRead.IsValid
            ? CreateDsc2GuardDecision(descriptorBytes, reference)
            : default;
        return DmaStreamComputeDescriptorParser.ParseDsc2ParserOnly(
            descriptorBytes,
            capabilitySet ?? DmaStreamComputeDsc2CapabilitySet.ParserOnlyFoundation,
            guardDecision,
            reference);
    }

    private static DmaStreamComputeDescriptorReference CreateDsc2Reference(
        byte[] descriptorBytes) =>
        new(
            descriptorAddress: 0xD52000,
            descriptorSize: (uint)descriptorBytes.Length,
            descriptorIdentityHash: IdentityHash);

    private static DmaStreamComputeOwnerGuardDecision CreateDsc2GuardDecision(
        byte[] descriptorBytes,
        DmaStreamComputeDescriptorReference? descriptorReference = null)
    {
        DmaStreamComputeStructuralReadResult structuralRead =
            DmaStreamComputeDescriptorParser.ReadDsc2StructuralOwnerBinding(
                descriptorBytes,
                descriptorReference);
        Assert.True(structuralRead.IsValid, structuralRead.Message);

        DmaStreamComputeOwnerBinding ownerBinding =
            structuralRead.RequireOwnerBindingForGuard();
        var context = new DmaStreamComputeOwnerGuardContext(
            ownerBinding.OwnerVirtualThreadId,
            ownerBinding.OwnerContextId,
            ownerBinding.OwnerCoreId,
            ownerBinding.OwnerPodId,
            ownerBinding.OwnerDomainTag,
            activeDomainCertificate: ownerBinding.OwnerDomainTag,
            deviceId: ownerBinding.DeviceId);
        return new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(
            ownerBinding,
            context);
    }

    private static byte[] BuildDsc2(
        params Dsc2ExtensionSpec[] extensions) =>
        BuildDsc2(DmaStreamComputeDsc2AddressSpaceKind.Physical, extensions);

    private static byte[] BuildDsc2(
        DmaStreamComputeDsc2AddressSpaceKind addressSpace,
        params Dsc2ExtensionSpec[] extensions)
    {
        byte[][] extensionBlocks = extensions.Select(BuildExtensionBlock).ToArray();
        int extensionTableBytes = extensionBlocks.Sum(static block => block.Length);
        int totalSize = DmaStreamComputeDescriptorParser.Dsc2HeaderSize + extensionTableBytes;
        byte[] bytes = new byte[totalSize];

        WriteUInt32(bytes, 0, DmaStreamComputeDescriptorParser.Dsc2Magic);
        WriteUInt16(bytes, 4, DmaStreamComputeDescriptorParser.Dsc2MajorVersion);
        WriteUInt16(bytes, 6, DmaStreamComputeDescriptorParser.Dsc2MinorVersion);
        WriteUInt16(bytes, HeaderSizeOffset, DmaStreamComputeDescriptorParser.Dsc2HeaderSize);
        WriteUInt32(bytes, 12, (uint)totalSize);
        WriteUInt32(bytes, ExtensionTableOffsetOffset, (uint)DmaStreamComputeDescriptorParser.Dsc2HeaderSize);
        WriteUInt16(bytes, ExtensionCountOffset, (ushort)extensionBlocks.Length);
        WriteUInt32(bytes, 24, (uint)extensionTableBytes);
        WriteUInt64(bytes, 32, IdentityHash);
        WriteUInt64(bytes, 40, CapabilityHash);
        WriteUInt16(bytes, 56, 1);
        WriteUInt16(bytes, 58, (ushort)DmaStreamComputeDsc2ParserStatus.ParserOnly);
        WriteUInt32(bytes, 60, 77);
        WriteUInt32(bytes, 64, 1);
        WriteUInt32(bytes, 68, 2);
        WriteUInt32(bytes, 72, DeviceId);
        WriteUInt16(bytes, 76, (ushort)addressSpace);
        WriteUInt64(bytes, 80, OwnerDomainTag);

        int cursor = DmaStreamComputeDescriptorParser.Dsc2HeaderSize;
        foreach (byte[] extensionBlock in extensionBlocks)
        {
            extensionBlock.CopyTo(bytes.AsSpan(cursor));
            cursor += extensionBlock.Length;
        }

        return bytes;
    }

    private static byte[] BuildExtensionBlock(Dsc2ExtensionSpec extension)
    {
        int length = DmaStreamComputeDescriptorParser.Dsc2ExtensionBlockHeaderSize + extension.Payload.Length;
        byte[] bytes = new byte[length];
        WriteUInt16(bytes, 0, extension.RawType);
        WriteUInt16(bytes, 2, extension.Version);
        WriteUInt16(bytes, 4, (ushort)extension.Flags);
        WriteUInt16(bytes, 6, extension.Alignment);
        WriteUInt32(bytes, 8, (uint)length);
        WriteUInt16(bytes, 12, (ushort)extension.CapabilityId);
        WriteUInt16(bytes, 14, DmaStreamComputeDescriptorParser.Dsc2MajorVersion);
        extension.Payload.CopyTo(bytes.AsSpan(DmaStreamComputeDescriptorParser.Dsc2ExtensionBlockHeaderSize));
        return bytes;
    }

    private static Dsc2ExtensionSpec PhysicalAddressSpaceExtension() =>
        AddressSpaceExtension(
            DmaStreamComputeDsc2AddressSpaceKind.Physical,
            DeviceId,
            OwnerDomainTag,
            0);

    private static Dsc2ExtensionSpec AddressSpaceExtension(
        DmaStreamComputeDsc2AddressSpaceKind addressSpace,
        uint deviceId,
        ulong domainTag,
        ulong mappingEpoch)
    {
        byte[] payload = new byte[32];
        WriteUInt16(payload, 0, (ushort)addressSpace);
        WriteUInt32(payload, 4, deviceId);
        WriteUInt64(payload, 8, domainTag);
        WriteUInt64(payload, 16, mappingEpoch);
        return KnownExtension(
            DmaStreamComputeDsc2ExtensionType.AddressSpace,
            DmaStreamComputeDsc2CapabilityId.AddressSpace,
            payload);
    }

    private static Dsc2ExtensionSpec StridedRangeExtension(
        DmaStreamComputeDsc2AccessKind accessKind,
        ushort elementSize,
        uint elementCount,
        ulong baseAddress,
        ulong strideBytes)
    {
        byte[] payload = new byte[32];
        WriteUInt16(payload, 0, (ushort)accessKind);
        WriteUInt16(payload, 2, elementSize);
        WriteUInt32(payload, 4, elementCount);
        WriteUInt64(payload, 8, baseAddress);
        WriteUInt64(payload, 16, strideBytes);
        return KnownExtension(
            DmaStreamComputeDsc2ExtensionType.StridedRange,
            DmaStreamComputeDsc2CapabilityId.StridedRange,
            payload);
    }

    private static Dsc2ExtensionSpec Tile2DExtension(
        DmaStreamComputeDsc2AccessKind accessKind,
        ushort elementSize,
        uint rows,
        uint columns,
        ulong baseAddress,
        ulong rowStride,
        ulong columnStride)
    {
        byte[] payload = new byte[40];
        WriteUInt16(payload, 0, (ushort)accessKind);
        WriteUInt16(payload, 2, elementSize);
        WriteUInt32(payload, 4, rows);
        WriteUInt32(payload, 8, columns);
        WriteUInt64(payload, 16, baseAddress);
        WriteUInt64(payload, 24, rowStride);
        WriteUInt64(payload, 32, columnStride);
        return KnownExtension(
            DmaStreamComputeDsc2ExtensionType.Tile2D,
            DmaStreamComputeDsc2CapabilityId.Tile2D,
            payload);
    }

    private static Dsc2ExtensionSpec ScatterGatherExtension(
        DmaStreamComputeDsc2AccessKind accessKind,
        ushort elementSize,
        params DmaStreamComputeMemoryRange[] segments)
    {
        byte[] payload = new byte[16 + (segments.Length * 16)];
        WriteUInt16(payload, 0, (ushort)accessKind);
        WriteUInt16(payload, 2, elementSize);
        WriteUInt32(payload, 4, (uint)segments.Length);
        WriteUInt32(payload, 8, (uint)(segments.Length * 16));

        int cursor = 16;
        foreach (DmaStreamComputeMemoryRange segment in segments)
        {
            WriteUInt64(payload, cursor, segment.Address);
            WriteUInt64(payload, cursor + 8, segment.Length);
            cursor += 16;
        }

        return KnownExtension(
            DmaStreamComputeDsc2ExtensionType.ScatterGather,
            DmaStreamComputeDsc2CapabilityId.ScatterGather,
            payload);
    }

    private static Dsc2ExtensionSpec FootprintSummaryExtension(
        DmaStreamComputeDsc2FootprintOutcomeKind outcomeKind,
        DmaStreamComputeMemoryRange readRange,
        DmaStreamComputeMemoryRange writeRange)
    {
        byte[] payload = new byte[48];
        WriteUInt16(payload, 0, (ushort)outcomeKind);
        WriteUInt64(payload, 8, readRange.Address);
        WriteUInt64(payload, 16, readRange.Length);
        WriteUInt64(payload, 24, writeRange.Address);
        WriteUInt64(payload, 32, writeRange.Length);
        return KnownExtension(
            DmaStreamComputeDsc2ExtensionType.FootprintSummary,
            DmaStreamComputeDsc2CapabilityId.FootprintSummary,
            payload);
    }

    private static Dsc2ExtensionSpec UnknownExtension(
        ushort rawType,
        DmaStreamComputeDsc2ExtensionFlags flags) =>
        new(rawType, flags, DmaStreamComputeDsc2CapabilityId.None, Array.Empty<byte>());

    private static Dsc2ExtensionSpec KnownExtension(
        DmaStreamComputeDsc2ExtensionType extensionType,
        DmaStreamComputeDsc2CapabilityId capabilityId,
        byte[] payload) =>
        new((ushort)extensionType, DmaStreamComputeDsc2ExtensionFlags.Required | DmaStreamComputeDsc2ExtensionFlags.Semantic, capabilityId, payload);

    private static string ReadAllSourceText(string root)
    {
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file => !CompatFreezeScanner.IsGeneratedPath(file))
                .Select(File.ReadAllText));
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);

    private sealed record Dsc2ExtensionSpec(
        ushort RawType,
        DmaStreamComputeDsc2ExtensionFlags Flags,
        DmaStreamComputeDsc2CapabilityId CapabilityId,
        byte[] Payload,
        ushort Version = 1,
        ushort Alignment = 8);
}
