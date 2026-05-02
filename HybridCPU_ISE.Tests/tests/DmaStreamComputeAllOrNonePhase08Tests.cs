using System;
using System.Buffers.Binary;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeAllOrNonePhase08Tests
{
    [Fact]
    public void Phase08_MissingStagedWriteCoverage_FaultsBeforeCommitPending()
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x31, 16);
        WriteMemory(0x9000, original);
        DmaStreamComputeDescriptor descriptor =
            CreateDescriptor(new DmaStreamComputeMemoryRange(0x9000, 16));
        DmaStreamComputeToken token = CreateReadsCompleteToken(descriptor);

        token.StageDestinationWrite(0x9000, Fill(0xA1, 8));
        DmaStreamComputeCommitResult result = token.MarkComputeComplete();

        Assert.False(result.Succeeded);
        Assert.True(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.Equal(DmaStreamComputeTokenFaultKind.PartialCompletionFault, result.Fault!.FaultKind);
        Assert.Equal(0, token.StagedWriteCount);
        Assert.False(token.HasCommitted);
        Assert.Equal(original, ReadMemory(0x9000, 16));
    }

    [Fact]
    public void Phase08_StagedWriteOutsideNormalizedFootprint_FaultsWithoutMemoryEffects()
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x32, 16);
        WriteMemory(0x9000, original);
        DmaStreamComputeDescriptor descriptor =
            CreateDescriptor(new DmaStreamComputeMemoryRange(0x9000, 16));
        DmaStreamComputeToken token = CreateReadsCompleteToken(descriptor);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => token.StageDestinationWrite(0x9010, Fill(0xB2, 4)));

        Assert.Contains("outside", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.Equal(DmaStreamComputeTokenFaultKind.PartialCompletionFault, token.LastFault!.FaultKind);
        Assert.Equal(0, token.StagedWriteCount);
        Assert.Equal(original, ReadMemory(0x9000, 16));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Phase08_OverlappingOrDuplicateStagedWrites_DoNotCreatePartialSuccess(
        bool duplicateWrite)
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x33, 16);
        WriteMemory(0x9000, original);
        DmaStreamComputeDescriptor descriptor =
            CreateDescriptor(new DmaStreamComputeMemoryRange(0x9000, 16));
        DmaStreamComputeToken token = CreateReadsCompleteToken(descriptor);

        token.StageDestinationWrite(0x9000, Fill(0xC3, 16));
        if (duplicateWrite)
        {
            token.StageDestinationWrite(0x9000, Fill(0xD4, 16));
        }
        else
        {
            token.StageDestinationWrite(0x9008, Fill(0xD4, 4));
        }

        DmaStreamComputeCommitResult result = token.MarkComputeComplete();

        Assert.False(result.Succeeded);
        Assert.True(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenFaultKind.PartialCompletionFault, result.Fault!.FaultKind);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.Equal(0, token.StagedWriteCount);
        Assert.False(token.HasCommitted);
        Assert.Equal(original, ReadMemory(0x9000, 16));
    }

    [Fact]
    public void Phase08_CommitWriteFailure_RollsBackAllPriorAndFailedRangeBytes()
    {
        var memory = new FailingWriteMemoryArea(0x10000, failAddress: 0x9010);
        Processor.MainMemory = memory;
        Processor.Memory = null;
        byte[] firstOriginal = Fill(0x10, 8);
        byte[] secondOriginal = Fill(0x20, 8);
        WriteMemory(0x9000, firstOriginal);
        WriteMemory(0x9010, secondOriginal);
        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            new DmaStreamComputeMemoryRange(0x9000, 8),
            new DmaStreamComputeMemoryRange(0x9010, 8));
        DmaStreamComputeToken token = CreateReadsCompleteToken(descriptor);
        token.StageDestinationWrite(0x9000, Fill(0xA8, 8));
        token.StageDestinationWrite(0x9010, Fill(0xB8, 8));
        Assert.False(token.MarkComputeComplete().RequiresRetireExceptionPublication);

        memory.FailureEnabled = true;
        DmaStreamComputeCommitResult result =
            token.Commit(memory, descriptor.OwnerGuardDecision);

        Assert.False(result.Succeeded);
        Assert.True(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.Equal(DmaStreamComputeTokenFaultKind.MemoryFault, result.Fault!.FaultKind);
        Assert.Equal(0, token.StagedWriteCount);
        Assert.Equal(firstOriginal, ReadMemory(0x9000, 8));
        Assert.Equal(secondOriginal, ReadMemory(0x9010, 8));
    }

    [Fact]
    public void Phase08_CancelOrFaultBeforeCommit_LeavesMemoryUnchanged()
    {
        InitializeMainMemory(0x10000);
        byte[] cancelOriginal = Fill(0x44, 16);
        byte[] faultOriginal = Fill(0x55, 16);
        WriteMemory(0x9000, cancelOriginal);
        WriteMemory(0x9020, faultOriginal);
        DmaStreamComputeDescriptor cancelDescriptor =
            CreateDescriptor(new DmaStreamComputeMemoryRange(0x9000, 16));
        DmaStreamComputeToken cancelToken = CreateCommitPendingToken(
            cancelDescriptor,
            0x9000,
            Fill(0xE1, 16));

        cancelToken.Cancel(DmaStreamComputeTokenCancelReason.Flush);
        DmaStreamComputeCommitResult cancelResult =
            cancelToken.Commit(Processor.MainMemory, cancelDescriptor.OwnerGuardDecision);

        Assert.False(cancelResult.Succeeded);
        Assert.True(cancelResult.IsCanceled);
        Assert.Equal(DmaStreamComputeTokenState.Canceled, cancelToken.State);
        Assert.Equal(cancelOriginal, ReadMemory(0x9000, 16));

        DmaStreamComputeDescriptor faultDescriptor =
            CreateDescriptor(new DmaStreamComputeMemoryRange(0x9020, 16));
        DmaStreamComputeToken faultToken = CreateReadsCompleteToken(faultDescriptor);
        faultToken.StageDestinationWrite(0x9020, Fill(0xF2, 16));
        DmaStreamComputeCommitResult faultBeforeCommit = faultToken.PublishFault(
            DmaStreamComputeTokenFaultKind.DmaDeviceFault,
            "backend reported failure before commit",
            faultAddress: 0x9020,
            isWrite: true);
        DmaStreamComputeCommitResult commitAfterFault =
            faultToken.Commit(Processor.MainMemory, faultDescriptor.OwnerGuardDecision);

        Assert.False(faultBeforeCommit.Succeeded);
        Assert.False(commitAfterFault.Succeeded);
        Assert.True(commitAfterFault.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, faultToken.State);
        Assert.Equal(0, faultToken.StagedWriteCount);
        Assert.Equal(faultOriginal, ReadMemory(0x9020, 16));
    }

    [Fact]
    public void Phase08_ProgressDiagnosticsRemainMetadataOnly()
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x61, 16);
        WriteMemory(0x9000, original);
        DmaStreamComputeDescriptor descriptor =
            CreateDescriptor(new DmaStreamComputeMemoryRange(0x9000, 16));
        var token = new DmaStreamComputeToken(descriptor, tokenId: 8);

        DmaStreamComputeProgressDiagnostics progress =
            token.RecordProgressDiagnostics(
                bytesRead: 16,
                bytesStaged: 8,
                elementOperations: 4,
                modeledLatencyCycles: 3,
                backendStepCount: 1);
        token.RecordProgressDiagnostics(bytesStaged: 8, backendStepCount: 1);

        Assert.Equal(DmaStreamComputeTokenState.Admitted, token.State);
        Assert.False(token.HasCommitted);
        Assert.Equal(0, token.StagedWriteCount);
        Assert.Equal(16UL, progress.BytesRead);
        Assert.Equal(16UL, token.ProgressDiagnostics.BytesStaged);
        Assert.Equal(2UL, token.ProgressDiagnostics.BackendStepCount);
        Assert.False(token.ProgressDiagnostics.IsAuthoritative);
        Assert.False(token.ProgressDiagnostics.CanIssueToken);
        Assert.False(token.ProgressDiagnostics.CanSetSucceeded);
        Assert.False(token.ProgressDiagnostics.CanSetCommitted);
        Assert.False(token.ProgressDiagnostics.CanPublishMemory);
        Assert.False(token.ProgressDiagnostics.IsRetirePublication);
        Assert.Equal(original, ReadMemory(0x9000, 16));
    }

    [Fact]
    public void Phase08_PollingOrProgressObservation_DoesNotPublishStagedMemory()
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x71, 16);
        byte[] staged = Fill(0x72, 16);
        WriteMemory(0x9000, original);
        DmaStreamComputeDescriptor descriptor =
            CreateDescriptor(new DmaStreamComputeMemoryRange(0x9000, 16));
        DmaStreamComputeToken token = CreateCommitPendingToken(descriptor, 0x9000, staged);

        token.RecordProgressDiagnostics(bytesStaged: 16, backendStepCount: 1);
        DmaStreamComputeStagedWrite snapshot = Assert.Single(token.GetStagedWriteSnapshot());

        Assert.Equal(0x9000UL, snapshot.Address);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, token.State);
        Assert.False(token.HasCommitted);
        Assert.False(token.ProgressDiagnostics.CanPublishMemory);
        Assert.Equal(original, ReadMemory(0x9000, 16));
    }

    [Fact]
    public void Phase08_Dsc1NonAllOrNonePolicy_RejectsAsReserved()
    {
        byte[] descriptorBytes = DmaStreamComputeTestDescriptorFactory.BuildDescriptor();
        WriteUInt16(descriptorBytes, Dsc1PartialCompletionPolicyOffset, 2);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                DmaStreamComputeTestDescriptorFactory.CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.ReservedFieldFault, result.Fault);
        Assert.Contains("all-or-none", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase08_Dsc2PartialSuccessExtension_RejectsWhileGateClosed()
    {
        byte[] descriptorBytes = BuildDsc2(PartialCompletionPolicyExtension(rawPolicy: 2));
        var capabilitiesIncludingFutureWord =
            DmaStreamComputeDsc2CapabilitySet.Create(
                DmaStreamComputeDsc2CapabilitySet.ParserOnlyFoundation.Grants
                    .Concat(new[]
                    {
                        new DmaStreamComputeDsc2CapabilityGrant(
                            DmaStreamComputeDsc2CapabilityId.PartialCompletionPolicy,
                            DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                            DmaStreamComputeDsc2CapabilityStage.Validation)
                    })
                    .ToArray());

        DmaStreamComputeDsc2ValidationResult result =
            ParseDsc2(descriptorBytes, capabilitiesIncludingFutureWord);

        Assert.False(result.IsParserAccepted);
        Assert.Equal(DmaStreamComputeValidationFault.UnsupportedCapability, result.Fault);
        Assert.Contains("partial", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.ExecutionEnabled);
        Assert.False(result.CanIssueToken);
        Assert.False(result.CanPublishMemory);
        Assert.False(result.CanProductionLower);
    }

    [Fact]
    public void Phase08_ExecutableBoundariesRemainFailClosed()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var core = new Processor.CPU_Core(0);

        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);
        InvalidOperationException dmaEx = Assert.Throws<InvalidOperationException>(
            () => new DmaStreamComputeMicroOp(descriptor).Execute(ref core));
        Assert.Contains("fail closed", dmaEx.Message, StringComparison.OrdinalIgnoreCase);

        SystemDeviceCommandMicroOp[] carriers =
        {
            new AcceleratorQueryCapsMicroOp(),
            new AcceleratorSubmitMicroOp(),
            new AcceleratorPollMicroOp(),
            new AcceleratorWaitMicroOp(),
            new AcceleratorCancelMicroOp(),
            new AcceleratorFenceMicroOp()
        };

        foreach (SystemDeviceCommandMicroOp carrier in carriers)
        {
            Assert.False(carrier.WritesRegister);
            Assert.Empty(carrier.WriteRegisters);
            Assert.Throws<InvalidOperationException>(() => carrier.Execute(ref core));
        }
    }

    [Fact]
    public void Phase08_CompilerProductionLoweringToPartialSuccessDscDsc2OrL7_RemainsForbidden()
    {
        var dscContext = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        DmaStreamComputeDescriptor dscDescriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor() with
            {
                PartialCompletionPolicy = (DmaStreamComputePartialCompletionPolicy)2
            };

        Assert.Throws<InvalidOperationException>(
            () => dscContext.CompileDmaStreamCompute(dscDescriptor));
        Assert.Equal(0, dscContext.InstructionCount);

        MethodInfo[] compilerMethods = typeof(HybridCpuThreadCompilerContext).GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.DoesNotContain(
            compilerMethods,
            static method => method.Name.Contains("Dsc2", StringComparison.OrdinalIgnoreCase));

        var l7Context = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        AcceleratorCommandDescriptor l7Descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor() with
            {
                PartialCompletionPolicy = (AcceleratorPartialCompletionPolicy)2
            };

        Assert.Throws<InvalidOperationException>(
            () => l7Context.CompileAcceleratorSubmit(
                IrAcceleratorIntent.ForMatMul(l7Descriptor),
                CompilerAcceleratorCapabilityModel.ReferenceMatMul));
        Assert.Equal(0, l7Context.InstructionCount);
    }

    private const int Dsc1PartialCompletionPolicyOffset = 56;
    private const ulong Dsc2IdentityHash = 0xD5C2000000000008UL;
    private const ulong Dsc2CapabilityHash = 0xCA9AB118UL;
    private const ulong Dsc2OwnerDomainTag = 0xD0A11UL;
    private const uint Dsc2DeviceId = DmaStreamComputeDescriptor.CanonicalLane6DeviceId;

    private static DmaStreamComputeDescriptor CreateDescriptor(
        params DmaStreamComputeMemoryRange[] writeRanges)
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        DmaStreamComputeMemoryRange[] normalizedWrites = writeRanges.ToArray();
        return descriptor with
        {
            WriteMemoryRanges = normalizedWrites,
            NormalizedWriteMemoryRanges = normalizedWrites,
            NormalizedFootprintHash = 0x8808UL
        };
    }

    private static DmaStreamComputeToken CreateReadsCompleteToken(
        DmaStreamComputeDescriptor descriptor)
    {
        var token = new DmaStreamComputeToken(descriptor, descriptor.DescriptorIdentityHash);
        token.MarkIssued();
        token.MarkReadsComplete();
        return token;
    }

    private static DmaStreamComputeToken CreateCommitPendingToken(
        DmaStreamComputeDescriptor descriptor,
        ulong address,
        byte[] staged)
    {
        DmaStreamComputeToken token = CreateReadsCompleteToken(descriptor);
        token.StageDestinationWrite(address, staged);
        DmaStreamComputeCommitResult result = token.MarkComputeComplete();
        Assert.False(result.Succeeded);
        Assert.False(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, token.State);
        return token;
    }

    private static void InitializeMainMemory(ulong bytes)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, bytes);
        Processor.Memory = null;
    }

    private static byte[] Fill(byte value, int count)
    {
        byte[] bytes = new byte[count];
        Array.Fill(bytes, value);
        return bytes;
    }

    private static void WriteMemory(ulong address, byte[] bytes) =>
        Assert.True(Processor.MainMemory.TryWritePhysicalRange(address, bytes));

    private static byte[] ReadMemory(ulong address, int length)
    {
        byte[] bytes = new byte[length];
        Assert.True(Processor.MainMemory.TryReadPhysicalRange(address, bytes));
        return bytes;
    }

    private static DmaStreamComputeDsc2ValidationResult ParseDsc2(
        byte[] descriptorBytes,
        DmaStreamComputeDsc2CapabilitySet capabilitySet)
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
            capabilitySet,
            guardDecision,
            reference);
    }

    private static DmaStreamComputeDescriptorReference CreateDsc2Reference(
        byte[] descriptorBytes) =>
        new(
            descriptorAddress: 0xD82000,
            descriptorSize: (uint)descriptorBytes.Length,
            descriptorIdentityHash: Dsc2IdentityHash);

    private static DmaStreamComputeOwnerGuardDecision CreateDsc2GuardDecision(
        byte[] descriptorBytes,
        DmaStreamComputeDescriptorReference descriptorReference)
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

    private static byte[] BuildDsc2(params Dsc2ExtensionSpec[] extensions)
    {
        byte[][] extensionBlocks = extensions.Select(BuildExtensionBlock).ToArray();
        int extensionTableBytes = extensionBlocks.Sum(static block => block.Length);
        int totalSize = DmaStreamComputeDescriptorParser.Dsc2HeaderSize + extensionTableBytes;
        byte[] bytes = new byte[totalSize];

        WriteUInt32(bytes, 0, DmaStreamComputeDescriptorParser.Dsc2Magic);
        WriteUInt16(bytes, 4, DmaStreamComputeDescriptorParser.Dsc2MajorVersion);
        WriteUInt16(bytes, 6, DmaStreamComputeDescriptorParser.Dsc2MinorVersion);
        WriteUInt16(bytes, 8, DmaStreamComputeDescriptorParser.Dsc2HeaderSize);
        WriteUInt32(bytes, 12, (uint)totalSize);
        WriteUInt32(bytes, 16, (uint)DmaStreamComputeDescriptorParser.Dsc2HeaderSize);
        WriteUInt16(bytes, 20, (ushort)extensionBlocks.Length);
        WriteUInt32(bytes, 24, (uint)extensionTableBytes);
        WriteUInt64(bytes, 32, Dsc2IdentityHash);
        WriteUInt64(bytes, 40, Dsc2CapabilityHash);
        WriteUInt16(bytes, 56, 1);
        WriteUInt16(bytes, 58, (ushort)DmaStreamComputeDsc2ParserStatus.ParserOnly);
        WriteUInt32(bytes, 60, 77);
        WriteUInt32(bytes, 64, 1);
        WriteUInt32(bytes, 68, 2);
        WriteUInt32(bytes, 72, Dsc2DeviceId);
        WriteUInt16(bytes, 76, (ushort)DmaStreamComputeDsc2AddressSpaceKind.Physical);
        WriteUInt64(bytes, 80, Dsc2OwnerDomainTag);

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
        int length = DmaStreamComputeDescriptorParser.Dsc2ExtensionBlockHeaderSize +
            extension.Payload.Length;
        byte[] bytes = new byte[length];
        WriteUInt16(bytes, 0, (ushort)extension.ExtensionType);
        WriteUInt16(bytes, 2, 1);
        WriteUInt16(bytes, 4, (ushort)(
            DmaStreamComputeDsc2ExtensionFlags.Required |
            DmaStreamComputeDsc2ExtensionFlags.Semantic));
        WriteUInt16(bytes, 6, 8);
        WriteUInt32(bytes, 8, (uint)length);
        WriteUInt16(bytes, 12, (ushort)extension.CapabilityId);
        WriteUInt16(bytes, 14, DmaStreamComputeDescriptorParser.Dsc2MajorVersion);
        extension.Payload.CopyTo(bytes.AsSpan(
            DmaStreamComputeDescriptorParser.Dsc2ExtensionBlockHeaderSize));
        return bytes;
    }

    private static Dsc2ExtensionSpec PartialCompletionPolicyExtension(ushort rawPolicy)
    {
        byte[] payload = new byte[8];
        WriteUInt16(payload, 0, rawPolicy);
        return new Dsc2ExtensionSpec(
            DmaStreamComputeDsc2ExtensionType.PartialCompletionPolicy,
            DmaStreamComputeDsc2CapabilityId.PartialCompletionPolicy,
            payload);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);

    private sealed class FailingWriteMemoryArea : Processor.MainMemoryArea
    {
        private readonly ulong _failAddress;
        private bool _failureConsumed;

        public FailingWriteMemoryArea(ulong length, ulong failAddress)
        {
            _failAddress = failAddress;
            SetLength((long)length);
        }

        public bool FailureEnabled { get; set; }

        public override bool TryWritePhysicalRange(
            ulong physicalAddress,
            ReadOnlySpan<byte> buffer)
        {
            if (FailureEnabled &&
                !_failureConsumed &&
                physicalAddress == _failAddress)
            {
                base.TryWritePhysicalRange(physicalAddress, buffer);
                _failureConsumed = true;
                return false;
            }

            return base.TryWritePhysicalRange(physicalAddress, buffer);
        }
    }

    private sealed record Dsc2ExtensionSpec(
        DmaStreamComputeDsc2ExtensionType ExtensionType,
        DmaStreamComputeDsc2CapabilityId CapabilityId,
        byte[] Payload);
}
