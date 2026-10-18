using System.Collections.Concurrent;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxMemoryDomainAddressSpaceGenerationGateTests
{
    private const ulong Domain = 7;
    private const ulong RuntimeAddressSpace = 9;

    [Fact]
    public void Bind_ReplacesCallerGenerationWithRuntimeOwnedNonZeroGeneration()
    {
        var runtime = new MemoryDomainRuntime();
        MemoryDomainDescriptor supplied = Descriptor(generation: 0xdead);
        MemoryDomainSourceBindResult bound =
            runtime.BindAuthoritativeTranslationView(supplied, RuntimeAddressSpace);
        Assert.True(bound.IsBound);
        Assert.NotSame(supplied, bound.Descriptor);
        Assert.NotEqual(0UL, bound.AddressSpaceGeneration);
        Assert.NotEqual(0xdeadUL, bound.AddressSpaceGeneration);
        Assert.Equal(bound.AddressSpaceGeneration,
            bound.Descriptor!.TranslationControl.AddressSpaceGeneration);
        Assert.Equal(0xdeadUL, supplied.TranslationControl.AddressSpaceGeneration);
    }

    [Theory]
    [InlineData(VmcsField.GuestCr3, 0x12345000UL)]
    [InlineData(VmcsField.EptPointer, 0xabcdf000UL)]
    [InlineData(VmcsField.Vpid, 9UL)]
    [InlineData(VmcsField.Cr3TargetCount, 2UL)]
    public void Capture_ReturnsExactAtomicMemoryOwnedField(VmcsField field, ulong expected)
    {
        var runtime = new MemoryDomainRuntime();
        MemoryDomainSourceBindResult bound =
            runtime.BindAuthoritativeTranslationView(Descriptor(), RuntimeAddressSpace);
        MemoryDomainSourceCaptureResult result = runtime.CaptureVmReadScalarSource(
            bound.Descriptor, Domain, RuntimeAddressSpace, field);
        Assert.True(result.IsCaptured, result.Reason);
        MemoryDomainRuntime.SourceCapture capture = result.Capture!;
        Assert.Same(bound.Descriptor, capture.SourceOwner);
        Assert.Equal(bound.AddressSpaceGeneration, capture.AddressSpaceGeneration);
        Assert.Equal(expected, capture.Value);
        Assert.True(runtime.IsAuthenticCapture(capture));
        Assert.False(capture.RuntimeAuthorityGranted);
        Assert.False(capture.IsReceipt);
    }

    [Fact]
    public void FieldSpecificGatesAndAdjacentFieldsFailIndependently()
    {
        var noSecondStage = new MemoryDomainRuntime();
        MemoryDomainSourceBindResult second = noSecondStage.BindAuthoritativeTranslationView(
            Descriptor(ownsSecondStage: false), RuntimeAddressSpace);
        Assert.Equal(MemoryDomainSourceCaptureDecision.SecondStageDenied,
            noSecondStage.CaptureVmReadScalarSource(
                second.Descriptor, Domain, RuntimeAddressSpace, VmcsField.EptPointer).Decision);
        Assert.True(noSecondStage.CaptureVmReadScalarSource(
            second.Descriptor, Domain, RuntimeAddressSpace, VmcsField.GuestCr3).IsCaptured);

        var noTagging = new MemoryDomainRuntime();
        MemoryDomainSourceBindResult tag = noTagging.BindAuthoritativeTranslationView(
            Descriptor(tagging: false, addressSpaceTag: 0), RuntimeAddressSpace);
        Assert.Equal(MemoryDomainSourceCaptureDecision.TaggingDenied,
            noTagging.CaptureVmReadScalarSource(
                tag.Descriptor, Domain, RuntimeAddressSpace, VmcsField.Vpid).Decision);
        Assert.True(noTagging.CaptureVmReadScalarSource(
            tag.Descriptor, Domain, RuntimeAddressSpace, VmcsField.Cr3TargetCount).IsCaptured);

        foreach (VmcsField denied in new[]
                 {
                     VmcsField.GuestCr0, VmcsField.GuestCr4, VmcsField.GuestPc,
                     VmcsField.HostCr3, VmcsField.PinBasedControls,
                 })
            Assert.Equal(MemoryDomainSourceCaptureDecision.FieldDenied,
                noTagging.CaptureVmReadScalarSource(
                    tag.Descriptor, Domain, RuntimeAddressSpace, denied).Decision);
    }

    [Fact]
    public void ReplacementRestoreAndRebind_AdvanceGenerationAndDenyStaleSource()
    {
        var runtime = new MemoryDomainRuntime();
        MemoryDomainSourceBindResult first =
            runtime.BindAuthoritativeTranslationView(Descriptor(), RuntimeAddressSpace);
        MemoryDomainRuntime.SourceCapture capture = runtime.CaptureVmReadScalarSource(
            first.Descriptor, Domain, RuntimeAddressSpace, VmcsField.GuestCr3).Capture!;
        MemoryDomainSourceBindResult second = runtime.ReplaceAuthoritativeTranslationView(
            Descriptor(root: 0x22222000, targetCount: 3), RuntimeAddressSpace);
        Assert.NotEqual(first.AddressSpaceGeneration, second.AddressSpaceGeneration);
        Assert.Equal(MemoryDomainSourceCaptureDecision.StaleOrForeignDescriptor,
            runtime.CaptureVmReadScalarSource(
                first.Descriptor, Domain, RuntimeAddressSpace, VmcsField.GuestCr3).Decision);
        Assert.True(runtime.IsAuthenticCapture(capture));
        Assert.Equal(0x12345000UL, capture.Value);
        MemoryDomainSourceBindResult restored = runtime.RebindAuthoritativeTranslationViewAfterRestore(
            Descriptor(root: 0x33333000), RuntimeAddressSpace);
        Assert.True(restored.AddressSpaceGeneration > second.AddressSpaceGeneration);
        runtime.UnbindAuthoritativeTranslationView();
        Assert.Equal(0UL, runtime.CurrentAddressSpaceGeneration);
        MemoryDomainSourceBindResult rebound = runtime.BindAuthoritativeTranslationView(
            Descriptor(), RuntimeAddressSpace);
        Assert.True(rebound.AddressSpaceGeneration > restored.AddressSpaceGeneration);
    }

    [Fact]
    public async Task CaptureVersusReplacement_NeverProducesMixedOwnerGenerationOrValue()
    {
        var runtime = new MemoryDomainRuntime();
        runtime.BindAuthoritativeTranslationView(Descriptor(), RuntimeAddressSpace);
        var failures = new ConcurrentQueue<string>();
        Task replace = Task.Run(() =>
        {
            for (int index = 1; index <= 500; index++)
                runtime.ReplaceAuthoritativeTranslationView(
                    Descriptor(root: (ulong)index * 0x1000), RuntimeAddressSpace);
        });
        Task capture = Task.Run(() =>
        {
            for (int index = 0; index < 2000; index++)
            {
                MemoryDomainDescriptor? owner = runtime.CurrentTranslationSource;
                MemoryDomainSourceCaptureResult result = runtime.CaptureVmReadScalarSource(
                    owner, Domain, RuntimeAddressSpace, VmcsField.GuestCr3);
                if (!result.IsCaptured) continue;
                MemoryDomainRuntime.SourceCapture snapshot = result.Capture!;
                if (!ReferenceEquals(owner, snapshot.SourceOwner) ||
                    snapshot.SourceOwner.TranslationControl.AddressSpaceGeneration !=
                        snapshot.AddressSpaceGeneration ||
                    snapshot.SourceOwner.TranslationControl.AddressSpaceRoot != snapshot.Value)
                    failures.Enqueue("mixed memory source snapshot");
            }
        });
        await Task.WhenAll(replace, capture);
        Assert.Empty(failures);
    }

    private static MemoryDomainDescriptor Descriptor(
        ulong root = 0x12345000,
        ulong secondStage = 0xabcdf000,
        bool tagging = true,
        ushort addressSpaceTag = 9,
        byte targetCount = 2,
        ulong generation = 3,
        bool ownsSecondStage = true) => new(
            null, null,
            new MemoryDomainTranslationControl(
                true, tagging, root, secondStage, (ushort)Domain,
                addressSpaceTag, generation,
                MemoryDomainTranslationControl.WriteBackMemoryType, targetCount),
            null, ownsSecondStage);
}
