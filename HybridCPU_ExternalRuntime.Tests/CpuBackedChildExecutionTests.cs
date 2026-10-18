using System.Diagnostics;
using HybridCPU.Compiler.NativeAot;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed partial class CpuBackedChildExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateMaterializationRefusalRetainsExactBackingForReleaseWithoutStart(bool duplicateHistory)
    {
        var package = Compile(nameof(LongRun));
        ulong imageBase = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(package.AsSpan(32));
        ulong entry = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(package.AsSpan(40));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(32), 0x4000_0000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(40), 0x4000_0000 + entry - imageBase);
        package.AsSpan(288, 32).Clear();
        System.Security.Cryptography.SHA256.HashData(package).CopyTo(package, 288);
        Assert.Equal(HybridCPU.Compiler.Core.Target.Runtime.HybridCpuStartupStatusV1.Success,
            new HybridCPU.Compiler.Core.Target.Runtime.HybridCpuRestrictedImageBuilderV1().Inspect(package).Status);
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(840))).Receipt!.Lease;
        var child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(), ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory,
            16 * 1024 * 1024, Op(841))).Receipt!.Lease;
        var mapping = runtime.MapChildGuestMemory(child, new(0, 16 * 1024 * 1024, Op(842))).Receipt!.Mapping;
        var request = new ExternalChildArtifactBindRequest(mapping, package, 3, Op(843)) { MaximumPrivateDataBytes = 4096 };
        var refused = runtime.BindChildExecutableArtifact(child, request);
        Assert.Equal(ExternalRuntimeOutcome.Denied, refused.Outcome);
        var proof = runtime.ResolveOriginalArtifactBindDisposition(child, request)!;
        Assert.False(proof.NoEffect); Assert.Same(refused, proof.Raw); Assert.NotNull(proof.CleanupArtifact);
        Assert.Equal(4096UL, proof.CleanupArtifact!.MaximumPrivateDataBytes);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var owner = ((System.Collections.IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", flags)!.GetValue(runtime)!)[child.Handle]!;
        var fact = owner.GetType().GetProperty("ArtifactBind")!.GetValue(owner)!;
        var backing = fact.GetType().GetProperty("Backing")!.GetValue(fact)!;
        Assert.Same(backing, owner.GetType().GetProperty("Execution")!.GetValue(owner));
        var privateData = Assert.IsType<OwnedExecutableDataMemory>(backing.GetType().GetField("memory", flags)!.GetValue(backing));
        Assert.Equal(4096UL, privateData.MaximumDataBytes);
        Assert.NotNull(backing.GetType().GetField("kernel", flags)!.GetValue(backing));
        Assert.Null(runtime.ResolveChildExecutableArtifactBind(child, request));
        Assert.Null(runtime.ResolveOriginalArtifactBindDisposition(child, request with { }));
        Assert.Null(runtime.BindChildExecutableArtifact(child, request).Receipt);
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.StartChildExecution(child,
            new(proof.CleanupArtifact!.ArtifactHandle, proof.CleanupArtifact.ArtifactEpoch, Op(844))).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.UnmapChildGuestMemory(mapping, Op(845)).Outcome);
        if (duplicateHistory)
        {
            var history = (System.Collections.IList)owner.GetType().GetProperty("OriginalArtifactBinds")!.GetValue(owner)!;
            history.Add(fact);
            owner.GetType().GetProperty("ArtifactBindsIssued")!.SetValue(owner, 2UL);
            Assert.Null(runtime.ResolveOriginalArtifactBindDisposition(child, request));
            Assert.Null(runtime.ResolveChildExecutableArtifactBind(child, request));
            Assert.Null(runtime.BindChildExecutableArtifact(child, request).Receipt);
            Assert.Null(runtime.ReleaseChildExecutableArtifact(new(child, proof.CleanupArtifact, Op(846))).Receipt);
            Assert.False(privateData.Retired);
            Assert.Same(privateData, backing.GetType().GetField("memory", flags)!.GetValue(backing));
            Assert.NotNull(backing.GetType().GetField("kernel", flags)!.GetValue(backing));
            Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.UnmapChildGuestMemory(mapping, Op(850)).Outcome);
            // Same original survives; remove only injected ambiguity, not an effect or owner.
            history.RemoveAt(1);
            owner.GetType().GetProperty("ArtifactBindsIssued")!.SetValue(owner, 1UL);
            Assert.Same(refused, runtime.ResolveOriginalArtifactBindDisposition(child, request)!.Raw);
        }
        var release = new ExternalArtifactReleaseRequest(child, proof.CleanupArtifact, Op(846));
        Assert.True(runtime.ReleaseChildExecutableArtifact(release).Receipt!.NoFurtherUse);
        Assert.True(privateData.Retired); Assert.Equal(0UL, privateData.AllocatedDataBytes);
        Assert.Null(backing.GetType().GetField("memory", flags)!.GetValue(backing));
        Assert.Null(backing.GetType().GetField("kernel", flags)!.GetValue(backing));
        Assert.Same(release, runtime.ResolveChildExecutableArtifactRelease(release)!.Request);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.UnmapChildGuestMemory(mapping, Op(847)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseChildDomain(child, Op(848)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(parent, Op(849)).Outcome);
    }
    [Fact]
    public void OriginalParserRefusalSurvivesReplyLossWithoutBackingOrRepeatedBind()
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(820))).Receipt!.Lease;
        var child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(), ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory,
            8192, Op(821))).Receipt!.Lease;
        var mapping = runtime.MapChildGuestMemory(child, new(0, 8192, Op(822))).Receipt!.Mapping;
        var request = new ExternalChildArtifactBindRequest(mapping, [1, 2, 3, 4], 3, Op(823));
        var refused = runtime.BindChildExecutableArtifact(child, request);
        Assert.Equal(ExternalRuntimeOutcome.Denied, refused.Outcome);
        var original = runtime.ResolveOriginalArtifactBindDisposition(child, request)!;
        Assert.True(original.NoEffect); Assert.Same(request, original.Request); Assert.Same(refused, original.Raw);
        Assert.Null(runtime.ResolveChildExecutableArtifactBind(child, request));
        Assert.Null(runtime.ResolveOriginalArtifactBindDisposition(child, request with { }));
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.BindChildExecutableArtifact(child, request).Outcome);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var owner = ((System.Collections.IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", flags)!.GetValue(runtime)!)[child.Handle]!;
        Assert.Null(owner.GetType().GetProperty("Execution")!.GetValue(owner));
        var history = (System.Collections.IList)owner.GetType().GetProperty("OriginalArtifactBinds")!.GetValue(owner)!;
        var fact = history[0]!;
        history[0] = typeof(object).GetMethod("MemberwiseClone", flags)!.Invoke(fact, null);
        Assert.Null(runtime.ResolveOriginalArtifactBindDisposition(child, request));
        Assert.Null(runtime.BindChildExecutableArtifact(child, request with { Operation = Op(824) }).Receipt);
        history.Clear(); Assert.Null(runtime.ResolveOriginalArtifactBindDisposition(child, request));
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.UnmapChildGuestMemory(mapping, Op(828)).Outcome);
        history.Add(fact); Assert.True(runtime.ResolveOriginalArtifactBindDisposition(child, request)!.NoEffect);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.UnmapChildGuestMemory(mapping, Op(825)).Outcome);
        Assert.True(runtime.ResolveOriginalArtifactBindDisposition(child, request)!.NoEffect);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseChildDomain(child, Op(826)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(parent, Op(827)).Outcome);
    }
    [Fact]
    public void ReusedChildCloseOperationCannotStopAdmittedCpuBackingBeforeRefusal()
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(110))).Receipt!.Lease;
        var create = Op(111);
        var child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(), ExternalChildAuthority.Execute, 16 * 1024 * 1024, create)).Receipt!.Lease;
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, runtime.LoadChildExecutableImage(child, new(Compile(nameof(LongRun)), 2_000_000, Op(112))).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.CloseChildDomain(child, create).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Start, Op(113)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseChildDomain(child, Op(114)).Outcome);
    }

    [Theory]
    [InlineData("childRegistry")] [InlineData("guestMappingRegistry")] [InlineData("registry")]
    public void OriginalBindRejectsEqualProducingOwnerCopyBeforeBackingCreation(string registryName)
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(90))).Receipt!.Lease;
        var child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(), ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory,
            16 * 1024 * 1024, Op(91))).Receipt!.Lease;
        var mapping = runtime.MapChildGuestMemory(child, new(0, 16 * 1024 * 1024, Op(92))).Receipt!.Mapping;
        var request = new ExternalChildArtifactBindRequest(mapping, Compile(nameof(LongRun)), 2_000_000, Op(93));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var registry = (System.Collections.IDictionary)typeof(HybridCpuExternalRuntime).GetField(registryName, flags)!.GetValue(runtime)!;
        var key = Assert.Single(registry.Keys.Cast<object>());
        var original = registry[key]!;
        registry[key] = typeof(object).GetMethod("MemberwiseClone", flags)!.Invoke(original, null)!;
        var refused = runtime.BindChildExecutableArtifact(child, request);
        Assert.Null(refused.Receipt);
        Assert.NotEqual(ExternalRuntimeOutcome.Succeeded, refused.Outcome);
        registry[key] = original;
        Assert.Null(runtime.ResolveChildExecutableArtifactBind(child, request));
        var artifact = runtime.BindChildExecutableArtifact(child, request).Receipt!;
        Assert.NotNull(artifact);
        Assert.True(runtime.ReleaseChildExecutableArtifact(new(child, artifact, Op(94))).Receipt!.NoFurtherUse);
    }

    [Fact]
    public void OriginalBindLookupPreservesExactCleanupIdentityWithoutRepeatingAdmission()
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(80))).Receipt!.Lease;
        var child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(), ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory,
            16 * 1024 * 1024, Op(81))).Receipt!.Lease;
        var mapping = runtime.MapChildGuestMemory(child, new(0, 16 * 1024 * 1024, Op(82))).Receipt!.Mapping;
        var request = new ExternalChildArtifactBindRequest(mapping, Compile(nameof(LongRun)), 2_000_000, Op(83));
        Assert.Null(runtime.ResolveChildExecutableArtifactBind(child, request));
        var artifact = runtime.BindChildExecutableArtifact(child, request).Receipt!;
        Assert.Same(artifact, runtime.ResolveChildExecutableArtifactBind(child, request));
        Assert.Null(runtime.ResolveChildExecutableArtifactBind(child, request with { }));
        request.PackageBytes[0] ^= 1;
        Assert.Null(runtime.ResolveChildExecutableArtifactBind(child, request));
        request.PackageBytes[0] ^= 1;
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.BindChildExecutableArtifact(child, request).Outcome);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var owners = (System.Collections.IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", flags)!.GetValue(runtime)!;
        var original = owners[child.Handle]!;
        owners[child.Handle] = typeof(object).GetMethod("MemberwiseClone", flags)!.Invoke(original, null)!;
        Assert.Null(runtime.ResolveChildExecutableArtifactBind(child, request));
        owners[child.Handle] = original;
        Assert.Same(artifact, runtime.ResolveChildExecutableArtifactBind(child, request));
        var mappingOwners = (System.Collections.IDictionary)typeof(HybridCpuExternalRuntime).GetField("guestMappingRegistry", flags)!.GetValue(runtime)!;
        foreach (var sourceOwner in new[] { original, mappingOwners[mapping.Handle]! })
        {
            var admissionProperty = sourceOwner.GetType().GetProperty("OriginalAdmission")!;
            var originalAdmission = admissionProperty.GetValue(sourceOwner)!;
            admissionProperty.SetValue(sourceOwner, typeof(object).GetMethod("MemberwiseClone", flags)!.Invoke(originalAdmission, null));
            Assert.Null(runtime.ResolveChildExecutableArtifactBind(child, request));
            Assert.Null(runtime.ReleaseChildExecutableArtifact(new(child, artifact, Op(800))).Receipt);
            Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.UnmapChildGuestMemory(mapping, Op(801)).Outcome);
            admissionProperty.SetValue(sourceOwner, originalAdmission);
            Assert.Same(artifact, runtime.ResolveChildExecutableArtifactBind(child, request));
        }
        Assert.True(runtime.ReleaseChildExecutableArtifact(new(child, artifact, Op(84))).Receipt!.NoFurtherUse);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.UnmapChildGuestMemory(mapping, Op(85)).Outcome);
        Assert.Same(artifact, runtime.ResolveChildExecutableArtifactBind(child, request));
    }

    [Fact]
    public void ExactArtifactReleaseBeforeStartRejectsCopiedOwnersAndKeepsOriginalOutcomeAfterMappingClosure()
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(60))).Receipt!.Lease;
        var child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(), ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory,
            16 * 1024 * 1024, Op(61))).Receipt!.Lease;
        var mapping = runtime.MapChildGuestMemory(child, new(0, 16 * 1024 * 1024, Op(62))).Receipt!.Mapping;
        var artifact = runtime.BindChildExecutableArtifact(child, new(mapping, Compile(nameof(LongRun)), 2_000_000, Op(63))).Receipt!;
        var request = new ExternalArtifactReleaseRequest(child, artifact, Op(64));
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.UnmapChildGuestMemory(mapping, Op(65)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.ReleaseChildExecutableArtifact(request with { Artifact = artifact with { } }).Outcome);
        Assert.Null(runtime.ResolveChildExecutableArtifactRelease(request));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var owners = (System.Collections.IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", flags)!.GetValue(runtime)!;
        var original = owners[child.Handle]!;
        owners[child.Handle] = typeof(object).GetMethod("MemberwiseClone", flags)!.Invoke(original, null)!;
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.ReleaseChildExecutableArtifact(request).Outcome);
        Assert.Null(runtime.ResolveChildExecutableArtifactRelease(request));
        owners[child.Handle] = original;
        var released = runtime.ReleaseChildExecutableArtifact(request);
        Assert.Equal(ExternalRuntimeOutcome.Closed, released.Outcome);
        Assert.True(released.Receipt!.NoFurtherUse);
        Assert.Null(runtime.ResolveChildExecutableArtifactRelease(request with { }));
        var releaseProperty = original.GetType().GetProperty("ArtifactRelease")!;
        var releaseFact = releaseProperty.GetValue(original)!;
        foreach (var corrupted in new object?[] { typeof(object).GetMethod("MemberwiseClone", flags)!.Invoke(releaseFact, null), null })
        {
            releaseProperty.SetValue(original, corrupted);
            Assert.Null(runtime.ResolveChildExecutableArtifactRelease(request));
            Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.ReleaseChildExecutableArtifact(request with { Operation = Op(700) }).Outcome);
            Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.StartChildExecution(child, new(artifact.ArtifactHandle, artifact.ArtifactEpoch, Op(701))).Outcome);
            Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.UnmapChildGuestMemory(mapping, Op(702)).Outcome);
            Assert.Equal(1UL, original.GetType().GetProperty("ArtifactReleasesIssued")!.GetValue(original));
        }
        releaseProperty.SetValue(original, releaseFact);
        Assert.Same(released.Receipt, runtime.ResolveChildExecutableArtifactRelease(request));
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.ReleaseChildExecutableArtifact(request).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.StartChildExecution(child, new(artifact.ArtifactHandle, artifact.ArtifactEpoch, Op(66))).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.UnmapChildGuestMemory(mapping, Op(67)).Outcome);
        Assert.Same(released.Receipt, runtime.ResolveChildExecutableArtifactRelease(request));
        owners[child.Handle] = typeof(object).GetMethod("MemberwiseClone", flags)!.Invoke(original, null)!;
        Assert.Null(runtime.ResolveChildExecutableArtifactRelease(request));
        owners[child.Handle] = original;
        Assert.Same(released.Receipt, runtime.ResolveChildExecutableArtifactRelease(request));
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseChildDomain(child, Op(68)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(parent, Op(69)).Outcome);
        Assert.Same(released.Receipt, runtime.ResolveChildExecutableArtifactRelease(request));
    }

    [Fact]
    public void V3CorrelatesArtifactMappingExecutionAndRetiredWorkAndClosesBoundedIo()
    {
        byte[] package = Compile(nameof(LongRun));
        var runtime = new HybridCpuExternalRuntime();
        ExternalDomainLease parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(20))).Receipt!.Lease;
        ExternalChildDomainLease child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(),
            ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory | ExternalChildAuthority.VirtualIo,
            16 * 1024 * 1024, Op(21))).Receipt!.Lease;
        ExternalGuestMappingLease mapping = runtime.MapChildGuestMemory(child,
            new(0, 16 * 1024 * 1024, Op(22))).Receipt!.Mapping;

        var artifact = runtime.BindChildExecutableArtifact(child,
            new(mapping, package, 2_000_000, Op(23)));
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, artifact.Outcome);
        Assert.Equal(mapping.Handle, artifact.Receipt!.MappingHandle);
        Assert.Equal(mapping.Epoch, artifact.Receipt.MappingEpoch);

        var wrongMapping = artifact.Receipt with { MappingEpoch = new(mapping.Epoch.Value + 1) };
        Assert.NotEqual(wrongMapping.MappingEpoch, artifact.Receipt.MappingEpoch);
        var execution = runtime.StartChildExecution(child,
            new(artifact.Receipt.ArtifactHandle, artifact.Receipt.ArtifactEpoch, Op(24)));
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, execution.Outcome);
        Assert.Equal(artifact.Receipt.ArtifactHandle, execution.Receipt!.ArtifactHandle);
        Assert.Equal(mapping.Handle, execution.Receipt.MappingHandle);
        Assert.True(execution.Receipt.ExecutionGeneration.Value > 0);
        Assert.True(execution.Receipt.RetiredPipelineCycles > 0);
        Assert.True(execution.Receipt.LastRetireSequence > 0);
        Assert.True(execution.Receipt.IsTerminal);

        var io = runtime.BindChildVirtualIo(child, new(new(Guid.NewGuid()), new(1),
            ExternalChildVirtualIoRights.Read, 4096, Op(25)));
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, io.Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Stale,
            runtime.CloseChildVirtualIo(child, io.Receipt!.IoHandle,
                new(io.Receipt.IoEpoch.Value + 1), Op(26)).Outcome);
        var ioClose = runtime.CloseChildVirtualIo(child, io.Receipt.IoHandle, io.Receipt.IoEpoch, Op(27));
        Assert.Equal(ExternalRuntimeOutcome.Closed, ioClose.Outcome);
        Assert.True(ioClose.Receipt!.IsTerminal);

        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.UnmapChildGuestMemory(mapping, Op(28)).Outcome);
        var releaseRequest = new ExternalArtifactReleaseRequest(child, artifact.Receipt, Op(31));
        var release = runtime.ReleaseChildExecutableArtifact(releaseRequest);
        Assert.Equal(ExternalRuntimeOutcome.Closed, release.Outcome);
        Assert.True(release.Receipt!.NoFurtherUse);
        Assert.Same(release.Receipt, runtime.ResolveChildExecutableArtifactRelease(releaseRequest));
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.StartChildExecution(child,
            new(artifact.Receipt.ArtifactHandle, artifact.Receipt.ArtifactEpoch, Op(32))).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.UnmapChildGuestMemory(mapping, Op(33)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseChildDomain(child, Op(29)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(parent, Op(30)).Outcome);
    }

    [Fact]
    public void CpuBackedChildProducesIndependentStartParkResumeAndTerminalCloseReceipts()
    {
        byte[] package = Compile(nameof(LongRun));
        var runtime = new HybridCpuExternalRuntime();
        ExternalDomainLease parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!.Lease;
        ExternalChildDomainLease child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(),
            ExternalChildAuthority.Execute, 4096, Op(2))).Receipt!.Lease;
        var loaded = runtime.LoadChildExecutableImage(child, new(package, 2_000_000, Op(3)));

        Assert.True(loaded.Outcome == ExternalRuntimeOutcome.Succeeded, loaded.Reason);
        Assert.Matches("^[0-9a-f]{64}$", loaded.Receipt!.PackageSha256);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Start, Op(4)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Park, Op(5)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Resume, Op(6)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Park, Op(7)).Outcome);
        ExternalChildResult<ExternalChildDomainCloseReceipt> close = runtime.CloseChildDomain(child, Op(8));
        Assert.True(close.Outcome == ExternalRuntimeOutcome.Closed, close.Reason);
        Assert.True(close.Receipt!.IsTerminal);
    }

    public static int LongRun()
    {
        int a = 1;
        return a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a;
    }

    private static byte[] Compile(string method, bool expectSuccess = true, bool tamperMetadata = false,
        string? expectedDiagnostic = null)
    {
        string output = Path.Combine(Path.GetTempPath(), $"external-child-{Guid.NewGuid():N}.hce");
        string? nominalDirectory = null;
        try
        {
            var start = new ProcessStartInfo("dotnet") { RedirectStandardError = true, UseShellExecute = false };
            string assemblyPath = typeof(CpuBackedChildExecutionTests).Assembly.Location;
            if (method is nameof(ManagedNominalSlotReturn) or nameof(ManagedNominalOverloadReturn) or nameof(ManagedNominalReferenceReturn) or nameof(ManagedNominalNullReturn) or nameof(ManagedForeignStaticMethodReturn) or nameof(ManagedForeignStaticDirectReturn) or nameof(ManagedFailedStaticReturn) or nameof(ManagedMultiStaticReturn) or nameof(ManagedMultiFailedStaticReturn))
            {
                nominalDirectory = output + ".inputs";
                Directory.CreateDirectory(nominalDirectory);
                string primary = Path.Combine(nominalDirectory, Path.GetFileName(assemblyPath));
                File.Copy(assemblyPath, primary);
                string dependency = typeof(HybridCPU.NonRTL.NominalSlotCorpus.NominalBase).Assembly.Location;
                File.Copy(dependency, Path.Combine(nominalDirectory, Path.GetFileName(dependency)));
                assemblyPath = primary;
                start.Environment["HYBRIDCPU_NOMINAL_SLOT_METHOD"] = method;
                ConfigureNominalBodyPresentation(start, assemblyPath, output + ".body-world.json");
            }
            start.ArgumentList.Add(typeof(NativeAotSeamArtifactV1).Assembly.Location);
            start.ArgumentList.Add("compile-image");
            Add(start, "--assembly", assemblyPath);
            Add(start, "--type", typeof(CpuBackedChildExecutionTests).FullName!);
            Add(start, "--method", method);
            Add(start, "--out", output);
            Add(start, "--source-commit", NativeAotSeamBaselineV1.SourceCommit);
            Add(start, "--patch-digest", NativeAotSeamBaselineV1.PatchDigest);
            if (method is nameof(ManagedNominalSlotReturn) or nameof(ManagedNominalOverloadReturn) or nameof(ManagedNominalReferenceReturn) or nameof(ManagedNominalNullReturn) or nameof(ManagedForeignStaticMethodReturn) or nameof(ManagedForeignStaticDirectReturn) or nameof(ManagedFailedStaticReturn) or nameof(ManagedMultiStaticReturn) or nameof(ManagedMultiFailedStaticReturn))
            {
                string coreLib = typeof(int).Assembly.Location;
                start.Environment["HYBRIDCPU_METADATA_ONLY_INPUTS_V1"] = System.Text.Json.JsonSerializer.Serialize(
                    new[] { coreLib, Path.Combine(Path.GetDirectoryName(coreLib)!, "System.Runtime.dll") }
                    .Select(path => new { Path = path, Sha256 = tamperMetadata ? new string('0', 64) : Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() }));
            }
            if (method is nameof(ManagedArrayReturn) or nameof(ManagedArrayBoundsReturn) or nameof(ManagedCatchFinallyReturn) or nameof(ManagedFinallyReturn) or nameof(ManagedRethrowReturn) or nameof(ManagedCalleeCatchFinallyReturn) or nameof(ManagedCalleeFinallyCatchReturn) or nameof(ManagedAncestorRethrowReturn) or nameof(ManagedNestedCatchRethrowReturn) or nameof(ManagedNestedForeignCatchRethrowReturn) or nameof(ManagedFinallyReplacementReturn) or nameof(ManagedObjectFieldReturn) or nameof(ManagedInstanceCallReturn) or nameof(ManagedVirtualDispatchReturn) or nameof(ManagedInheritedSlotReturn) or nameof(ManagedHiddenSlotReturn) or nameof(ManagedNullVirtualReturn) or nameof(ManagedDeepHiddenSlotReturn) or nameof(ManagedDistinctSlotsReturn) or nameof(UnsupportedMethodImplSlotReturn) or nameof(UnsupportedGenericSlotReturn))
            {
                string coreLib = typeof(int).Assembly.Location;
                string[] metadataPaths = [coreLib, Path.Combine(Path.GetDirectoryName(coreLib)!, "System.Runtime.dll")];
                start.Environment["HYBRIDCPU_METADATA_ONLY_INPUTS_V1"] = System.Text.Json.JsonSerializer.Serialize(
                    metadataPaths.Select(path => new { Path = path,
                        Sha256 = tamperMetadata ? new string('0', 64) : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() }));
            }
            using Process process = Process.Start(start)!;
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (!expectSuccess)
            {
                Assert.NotEqual(0, process.ExitCode);
                Assert.False(File.Exists(output));
                if (expectedDiagnostic is not null) Assert.Contains(expectedDiagnostic, error, StringComparison.Ordinal);
                return [];
            }
            Assert.True(process.ExitCode == 0, error);
            byte[] package = File.ReadAllBytes(output);
            if (nominalDirectory is not null && Environment.GetEnvironmentVariable("HYBRIDCPU_NOMINAL_SLOT_ARTIFACT_DIRECTORY") is { } artifactDirectory)
            {
                File.Copy(output, Path.Combine(artifactDirectory, method + ".hcexe"), overwrite: true);
                File.Copy(output + ".json", Path.Combine(artifactDirectory, method + ".hcexe.json"), overwrite: true);
                File.Copy(output + ".body-world.json", Path.Combine(artifactDirectory, method + ".body-world.json.snapshot"), overwrite: true);
            }
            return package;
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
            if (File.Exists(output + ".json")) File.Delete(output + ".json");
            if (File.Exists(output + ".body-world.json")) File.Delete(output + ".body-world.json");
            if (nominalDirectory is not null) Directory.Delete(nominalDirectory, recursive: true);
        }
    }

    private static void Add(ProcessStartInfo start, string name, string value)
    {
        start.ArgumentList.Add(name);
        start.ArgumentList.Add(value);
    }

    private static ExternalOperationIdentity Op(ulong generation) => new(new(Guid.NewGuid()), new(generation));
}
