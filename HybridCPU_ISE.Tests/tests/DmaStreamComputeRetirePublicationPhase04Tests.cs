using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeRetirePublicationPhase04Tests
{
    [Fact]
    public void Phase04_HelperFaultsRemainModelRetireStyleNotPipelinePrecise()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var token = new DmaStreamComputeToken(descriptor, tokenId: 0x40);
        token.MarkIssued();

        DmaStreamComputeCommitResult result = token.PublishFault(
            DmaStreamComputeTokenFaultKind.MemoryFault,
            "helper source read fault",
            faultAddress: 0x1000,
            isWrite: false);

        Assert.True(result.RequiresRetireExceptionPublication);
        Assert.NotNull(result.Fault);
        Assert.False(result.Fault!.IsFullPipelinePreciseArchitecturalException);
        Assert.False(result.Fault.RequiresFuturePrecisePublicationMetadata);
        Assert.Equal(
            DmaStreamComputeFaultPublicationContract.ModelRetireStyleOnly,
            result.Fault.PublicationContract);
        Assert.IsType<PageFaultException>(result.CreateRetireException());
    }

    [Fact]
    public void Phase04_RetirePublicationMetadataCarriesFuturePreciseIdentity()
    {
        DmaStreamComputeActiveTokenEntry entry = AllocateEntry(
            issuingPc: 0x4000,
            bundleId: 0x55,
            issueCycle: 33,
            replayEpoch: 7);

        DmaStreamComputeRetireObservation observation =
            DmaStreamComputeRetirePublication.ObserveFutureRetire(
                entry,
                architecturalInstructionAge: 0xABC,
                ownerThreadId: 9,
                completionCycle: 44);

        DmaStreamComputeRetirePublicationMetadata metadata = observation.Metadata;
        Assert.True(metadata.HasRequiredFuturePreciseIdentity);
        Assert.Equal(entry.Handle.TokenId, metadata.TokenId);
        Assert.Equal(0x4000UL, metadata.IssuingPc);
        Assert.Equal(0x55UL, metadata.BundleId);
        Assert.Equal(0xABCUL, metadata.ArchitecturalInstructionAge);
        Assert.Equal(6, metadata.SlotIndex);
        Assert.Equal(6, metadata.LaneIndex);
        Assert.Equal(SlotClass.DmaStreamClass, metadata.SlotClass);
        Assert.Equal(SlotClass.DmaStreamClass, metadata.LaneClass);
        Assert.Equal(9, metadata.OwnerThreadId);
        Assert.Equal(entry.Token.Descriptor.OwnerBinding.OwnerVirtualThreadId, metadata.OwnerVirtualThreadId);
        Assert.Equal(entry.Token.Descriptor.OwnerBinding.OwnerContextId, metadata.OwnerContextId);
        Assert.Equal(entry.Token.Descriptor.OwnerBinding.OwnerCoreId, metadata.OwnerCoreId);
        Assert.Equal(entry.Token.Descriptor.OwnerBinding.OwnerPodId, metadata.OwnerPodId);
        Assert.Equal(entry.Token.Descriptor.OwnerBinding.OwnerDomainTag, metadata.OwnerDomainTag);
        Assert.Equal(entry.Token.Descriptor.OwnerBinding.DeviceId, metadata.DeviceId);
        Assert.Equal(entry.Token.Descriptor.DescriptorReference.DescriptorAddress, metadata.DescriptorAddress);
        Assert.Equal(entry.Token.Descriptor.DescriptorIdentityHash, metadata.DescriptorIdentityHash);
        Assert.Equal(entry.Token.Descriptor.NormalizedFootprintHash, metadata.NormalizedFootprintHash);
        Assert.Equal(33UL, metadata.IssueCycle);
        Assert.Equal(44UL, metadata.CompletionCycle);
        Assert.Equal(7UL, metadata.ReplayEpoch);
    }

    [Fact]
    public void Phase04_CommitPendingDoesNotMeanMemoryVisible()
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        byte[] original = DmaStreamComputeTelemetryTests.Fill(0x11, 16);
        byte[] staged = DmaStreamComputeTelemetryTests.Fill(0xA5, 16);
        DmaStreamComputeTelemetryTests.WriteMemory(0x9000, original);
        DmaStreamComputeActiveTokenEntry entry = AllocateEntry();

        MoveToCommitPending(entry.Token, staged);

        DmaStreamComputeRetireObservation observation =
            DmaStreamComputeRetirePublication.ObserveFutureRetire(
                entry,
                architecturalInstructionAge: 1);

        Assert.Equal(
            DmaStreamComputeRetireObservationKind.CommitReadyMemoryNotVisible,
            observation.Kind);
        Assert.True(observation.RequiresCommitAttempt);
        Assert.False(observation.MemoryMayBeVisible);
        Assert.Equal(original, DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));
    }

    [Fact]
    public void Phase04_FaultedAndCanceledTokensNeverPublishMemory()
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        byte[] original = DmaStreamComputeTelemetryTests.Fill(0x22, 16);
        byte[] staged = DmaStreamComputeTelemetryTests.Fill(0x5A, 16);
        DmaStreamComputeTelemetryTests.WriteMemory(0x9000, original);

        DmaStreamComputeActiveTokenEntry faultedEntry = AllocateEntry(bundleId: 1);
        MoveToCommitPending(faultedEntry.Token, staged);
        DmaStreamComputeCommitResult faultedResult = faultedEntry.Token.PublishFault(
            DmaStreamComputeTokenFaultKind.PartialCompletionFault,
            "coverage mismatch after compute",
            faultAddress: 0x9000,
            isWrite: true);

        DmaStreamComputeRetireObservation faultedObservation =
            DmaStreamComputeRetirePublication.ObserveFutureRetire(
                faultedEntry,
                architecturalInstructionAge: 2);

        Assert.False(faultedResult.Succeeded);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, faultedEntry.Token.State);
        Assert.Equal(DmaStreamComputeRetireObservationKind.FaultPublicationCandidate, faultedObservation.Kind);
        Assert.True(faultedObservation.RequiresExceptionPublication);
        Assert.False(faultedObservation.MemoryMayBeVisible);
        Assert.Equal(original, DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));

        DmaStreamComputeActiveTokenEntry canceledEntry = AllocateEntry(bundleId: 2);
        MoveToCommitPending(canceledEntry.Token, staged);
        canceledEntry.Token.Cancel(DmaStreamComputeTokenCancelReason.ReplayDiscard);

        DmaStreamComputeRetireObservation canceledObservation =
            DmaStreamComputeRetirePublication.ObserveFutureRetire(
                canceledEntry,
                architecturalInstructionAge: 3);

        Assert.Equal(DmaStreamComputeTokenState.Canceled, canceledEntry.Token.State);
        Assert.Equal(
            DmaStreamComputeRetireObservationKind.CancellationSuppressesPublication,
            canceledObservation.Kind);
        Assert.False(canceledObservation.RequiresExceptionPublication);
        Assert.False(canceledObservation.MemoryMayBeVisible);
        Assert.Equal(original, DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));
    }

    [Fact]
    public void Phase04_CommitFailureIsFaultNotSuccessfulPartialCompletion()
    {
        DmaStreamComputeDescriptor descriptor = CreateTwoDestinationDescriptor();
        var token = new DmaStreamComputeToken(descriptor, tokenId: 0x41);
        var memory = new FailingSecondWriteMemory(0x1000, failWriteAttempt: 2);
        byte[] firstOriginal = DmaStreamComputeTelemetryTests.Fill(0x10, 4);
        byte[] secondOriginal = DmaStreamComputeTelemetryTests.Fill(0x20, 4);
        memory.Seed(0x100, firstOriginal);
        memory.Seed(0x200, secondOriginal);

        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(0x100, DmaStreamComputeTelemetryTests.Fill(0xA1, 4));
        token.StageDestinationWrite(0x200, DmaStreamComputeTelemetryTests.Fill(0xB2, 4));
        Assert.False(token.MarkComputeComplete().RequiresRetireExceptionPublication);

        DmaStreamComputeCommitResult result =
            token.Commit(memory, descriptor.OwnerGuardDecision);

        Assert.False(result.Succeeded);
        Assert.False(result.IsCanceled);
        Assert.True(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.Equal(DmaStreamComputeTokenFaultKind.MemoryFault, result.Fault!.FaultKind);
        Assert.Equal(firstOriginal, memory.ReadBytes(0x100, 4));
        Assert.Equal(secondOriginal, memory.ReadBytes(0x200, 4));
    }

    [Fact]
    public void Phase04_BackendExceptionsNormalizeIntoTokenFaultRecords()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var token = new DmaStreamComputeToken(descriptor, tokenId: 0x42);
        token.MarkIssued();
        IOException backendException = new("device timeout");

        DmaStreamComputeCommitResult result =
            DmaStreamComputeRetirePublication.NormalizeBackendExceptionToTokenFault(
                token,
                backendException,
                faultAddress: 0x9000,
                isWrite: true);

        Assert.False(result.Succeeded);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.Same(token.LastFault, result.Fault);
        Assert.True(result.Fault!.BackendExceptionNormalized);
        Assert.Equal(typeof(IOException).FullName, result.Fault.NormalizedHostExceptionType);
        Assert.Equal(DmaStreamComputeFaultSourcePhase.Backend, result.Fault.SourcePhase);
        Assert.True(result.Fault.RequiresFuturePrecisePublicationMetadata);
        Assert.Equal(
            DmaStreamComputeFaultPriorityClass.RuntimeReadBackendIommu,
            DmaStreamComputeRetirePublication.ResolveSameTokenFaultPriority(
                result.Fault.FaultKind,
                result.Fault.SourcePhase));
        Assert.Equal(
            DmaStreamComputeFaultPriorityClass.QuotaBackpressureTokenCap,
            DmaStreamComputeRetirePublication.ResolveValidationFaultPriority(
                DmaStreamComputeValidationFault.TokenCapAdmissionReject));
    }

    [Fact]
    public void Phase04_PublicationSurfaceRemainsExplicitFutureSeamNotNormalLane6Path()
    {
        DmaStreamComputeActiveTokenEntry entry = AllocateEntry();
        DmaStreamComputeRetireObservation observation =
            DmaStreamComputeRetirePublication.ObserveFutureRetire(
                entry,
                architecturalInstructionAge: 1);

        Assert.True(observation.IsExplicitFuturePublicationSurface);
        Assert.False(observation.IsNormalPipelineExecutableLane6Path);
        Assert.Equal(
            DmaStreamComputeRetirePublicationSurfaceKind.ExplicitTestFutureSeam,
            observation.SurfaceKind);

        entry.Token.MarkIssued();
        entry.Token.MarkReadsComplete();
        entry.Token.StageDestinationWrite(0x9000, DmaStreamComputeTelemetryTests.Fill(0x77, 16));
        Assert.False(entry.Token.MarkComputeComplete().RequiresRetireExceptionPublication);
        Assert.True(entry.Token.Commit(Processor.MainMemory, entry.Token.Descriptor.OwnerGuardDecision).Succeeded);
        DmaStreamComputeRetireObservation illegalCommittedObservation =
            DmaStreamComputeRetirePublication.ObserveFutureRetire(
                entry,
                architecturalInstructionAge: 1);
        Assert.Equal(
            DmaStreamComputeRetireObservationKind.IllegalCommittedBeforeRetireObservation,
            illegalCommittedObservation.Kind);
        Assert.True(illegalCommittedObservation.MemoryMayBeVisible);

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string microOpText = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "MicroOps",
            "DmaStreamComputeMicroOp.cs"));
        string compilerText = ReadAllSourceText(Path.Combine(repoRoot, "HybridCPU_Compiler"));

        Assert.DoesNotContain("DmaStreamComputeRuntime.ExecuteToCommitPending", microOpText, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeRetirePublication.", microOpText, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeRuntime.ExecuteToCommitPending", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeTokenStore", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeRetirePublication.", compilerText, StringComparison.Ordinal);
        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);

        var core = new Processor.CPU_Core(0);
        var microOp = new DmaStreamComputeMicroOp(entry.Token.Descriptor);
        Assert.Throws<InvalidOperationException>(() => microOp.Execute(ref core));

        string[] unexpectedPublicationCallSites = CompatFreezeScanner.ScanProductionFilesForPatterns(
            repoRoot,
            new[] { "DmaStreamComputeRetirePublication" },
            new[]
            {
                Path.Combine(
                    "HybridCPU_ISE",
                    "Core",
                    "Execution",
                    "DmaStreamCompute",
                    "DmaStreamComputeRetirePublication.cs")
            },
            new[] { "HybridCPU_ISE" });
        Assert.Empty(unexpectedPublicationCallSites);
    }

    private static DmaStreamComputeActiveTokenEntry AllocateEntry(
        ulong issuingPc = 0x400,
        ulong bundleId = 0x44,
        ulong issueCycle = 12,
        ulong replayEpoch = 3)
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var store = new DmaStreamComputeTokenStore();

        DmaStreamComputeIssueAdmissionResult admission =
            store.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(
                        issuingPc,
                        bundleId,
                        issueCycle,
                        replayEpoch)));

        Assert.True(admission.IsAccepted, admission.Message);
        Assert.NotNull(admission.Entry);
        return admission.Entry!;
    }

    private static void MoveToCommitPending(DmaStreamComputeToken token, byte[] staged)
    {
        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(0x9000, staged);
        DmaStreamComputeCommitResult completion = token.MarkComputeComplete();

        Assert.False(completion.Succeeded);
        Assert.False(completion.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, token.State);
    }

    private static DmaStreamComputeDescriptor CreateTwoDestinationDescriptor()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        DmaStreamComputeMemoryRange[] ranges =
        [
            new DmaStreamComputeMemoryRange(0x100, 4),
            new DmaStreamComputeMemoryRange(0x200, 4)
        ];

        return descriptor with
        {
            WriteMemoryRanges = ranges,
            NormalizedWriteMemoryRanges = ranges,
            NormalizedFootprintHash = descriptor.NormalizedFootprintHash ^ 0x204UL
        };
    }

    private static string ReadAllSourceText(string root)
    {
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file => !CompatFreezeScanner.IsGeneratedPath(file))
                .Select(File.ReadAllText));
    }

    private sealed class FailingSecondWriteMemory : Processor.MainMemoryArea
    {
        private readonly byte[] _bytes;
        private readonly int _failWriteAttempt;
        private int _writeAttempts;

        public FailingSecondWriteMemory(int length, int failWriteAttempt)
        {
            _bytes = new byte[length];
            _failWriteAttempt = failWriteAttempt;
        }

        public override long Length => _bytes.Length;

        public void Seed(ulong address, byte[] data)
        {
            data.CopyTo(_bytes.AsSpan(checked((int)address), data.Length));
        }

        public byte[] ReadBytes(ulong address, int length)
        {
            byte[] result = new byte[length];
            _bytes.AsSpan(checked((int)address), length).CopyTo(result);
            return result;
        }

        public override bool TryReadPhysicalRange(ulong physicalAddress, Span<byte> buffer)
        {
            if (!HasRange(physicalAddress, buffer.Length))
            {
                return false;
            }

            _bytes.AsSpan(checked((int)physicalAddress), buffer.Length).CopyTo(buffer);
            return true;
        }

        public override bool TryWritePhysicalRange(ulong physicalAddress, ReadOnlySpan<byte> buffer)
        {
            if (!HasRange(physicalAddress, buffer.Length))
            {
                return false;
            }

            _writeAttempts++;
            if (_writeAttempts == _failWriteAttempt)
            {
                return false;
            }

            buffer.CopyTo(_bytes.AsSpan(checked((int)physicalAddress), buffer.Length));
            return true;
        }

        private bool HasRange(ulong address, int length)
        {
            if (length < 0 || address > int.MaxValue)
            {
                return false;
            }

            ulong requestLength = (ulong)length;
            return requestLength <= (ulong)_bytes.Length &&
                   address <= (ulong)_bytes.Length - requestLength;
        }
    }
}
