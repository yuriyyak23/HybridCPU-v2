using System.Collections;
using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed partial class CpuBackedChildExecutionTests
{
    [Fact]
    public void MalformedNoEffectRefusalCannotPreventIndependentExactArtifactRetirement()
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(530))).Receipt!.Lease;
        var child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(), ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory,
            16 * 1024 * 1024, Op(531))).Receipt!.Lease;
        var mapping = runtime.MapChildGuestMemory(child, new(0, 16 * 1024 * 1024, Op(532))).Receipt!.Mapping;
        var artifact = runtime.BindChildExecutableArtifact(child, new(mapping, Compile(nameof(LongRun)), 3, Op(533))).Receipt!;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var owner = ((IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", flags)!.GetValue(runtime)!)[child.Handle]!;
        var execution = owner.GetType().GetProperty("Execution")!.GetValue(owner)!;
        var memory = (OwnedExecutableDataMemory)execution.GetType().GetField("memory", flags)!.GetValue(execution)!;
        typeof(HybridCpuExternalRuntime).GetField("nextExecutionGeneration", flags)!.SetValue(runtime, long.MaxValue);
        var request = new ExternalOriginalChildExecutionStartRequest(child, artifact, new(artifact.ArtifactHandle, artifact.ArtifactEpoch, Op(534)));
        Assert.True(runtime.StartOriginalChildExecution(request)!.NoEffect);
        var rows = (IList)owner.GetType().GetProperty("OriginalStartRefusals")!.GetValue(owner)!;
        rows.Add(rows[0]); owner.GetType().GetProperty("StartRefusalsIssued")!.SetValue(owner, 2UL);
        Assert.Null(runtime.ResolveOriginalChildExecutionStart(request));
        Assert.Null(runtime.StartOriginalChildExecution(request));
        Assert.Equal(0UL, owner.GetType().GetProperty("ExecutionStartsIssued")!.GetValue(owner));
        var release = new ExternalArtifactReleaseRequest(child, artifact, Op(535));
        Assert.True(runtime.ReleaseChildExecutableArtifact(release).Receipt!.NoFurtherUse);
        Assert.True(memory.Retired); Assert.Equal(0UL, memory.AllocatedDataBytes);
        Assert.Null(execution.GetType().GetField("memory", flags)!.GetValue(execution));
        Assert.Null(execution.GetType().GetField("kernel", flags)!.GetValue(execution));
        Assert.Null(runtime.StartOriginalChildExecution(request));
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.UnmapChildGuestMemory(mapping, Op(536)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseChildDomain(child, Op(537)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(parent, Op(538)).Outcome);
    }
    [Fact]
    public void OriginalStartRetainsResultRejectsCopiesAndPinsLostHistoryWithoutReexecution()
    {
        var package = Compile(nameof(LongRun));
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(500))).Receipt!.Lease;
        var child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(), ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory,
            16 * 1024 * 1024, Op(501))).Receipt!.Lease;
        var mapping = runtime.MapChildGuestMemory(child, new(0, 16 * 1024 * 1024, Op(502))).Receipt!.Mapping;
        var artifact = runtime.BindChildExecutableArtifact(child, new(mapping, package, 2_000_000, Op(503))).Receipt!;
        var nativeRecords = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var nativeRecord = nativeRecords[child.Handle]!;
        var bindProperty = nativeRecord.GetType().GetProperty("ArtifactBind")!;
        var bindFact = bindProperty.GetValue(nativeRecord)!;
        bindProperty.SetValue(nativeRecord, typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(bindFact, null));
        var copiedBind = runtime.StartOriginalChildExecution(new(child, artifact,
            new(artifact.ArtifactHandle, artifact.ArtifactEpoch, Op(512))))!;
        Assert.True(copiedBind.NoEffect);
        Assert.Equal(ExternalRuntimeOutcome.Denied, copiedBind.Raw.Outcome);
        Assert.Null(nativeRecord.GetType().GetProperty("OriginalExecutionStart")!.GetValue(nativeRecord));
        bindProperty.SetValue(nativeRecord, bindFact);
        var childAdmissionProperty = nativeRecord.GetType().GetProperty("OriginalAdmission")!;
        var originalAdmission = (ExternalChildDomainCreateReceipt)childAdmissionProperty.GetValue(nativeRecord)!;
        childAdmissionProperty.SetValue(nativeRecord, originalAdmission with { });
        var refusedFactory = runtime.StartOriginalChildExecution(new(child, artifact,
            new(artifact.ArtifactHandle, artifact.ArtifactEpoch, Op(510))))!;
        Assert.True(refusedFactory.NoEffect);
        Assert.Equal(ExternalRuntimeOutcome.Denied, refusedFactory.Raw.Outcome);
        Assert.Null(nativeRecord.GetType().GetProperty("OriginalExecutionStart")!.GetValue(nativeRecord));
        childAdmissionProperty.SetValue(nativeRecord, originalAdmission);
        var nextGeneration = typeof(HybridCpuExternalRuntime).GetField("nextExecutionGeneration", BindingFlags.Instance | BindingFlags.NonPublic)!;
        nextGeneration.SetValue(runtime, long.MaxValue);
        var exhaustedRequest = new ExternalOriginalChildExecutionStartRequest(child, artifact,
            new(artifact.ArtifactHandle, artifact.ArtifactEpoch, Op(511)));
        var exhausted = runtime.StartOriginalChildExecution(exhaustedRequest)!;
        Assert.True(exhausted.NoEffect);
        Assert.Null(nativeRecord.GetType().GetProperty("OriginalExecutionStart")!.GetValue(nativeRecord));
        Assert.Equal(ExternalChildDomainState.Ready, nativeRecord.GetType().GetProperty("State")!.GetValue(nativeRecord));
        nextGeneration.SetValue(runtime, 0L); // Explicit test allocator reset after proven zero effect.
        Assert.Same(exhausted.Raw, runtime.ResolveOriginalChildExecutionStart(exhaustedRequest)!.Raw);
        var repeatedRefusal = runtime.StartOriginalChildExecution(exhaustedRequest)!;
        Assert.True(repeatedRefusal.NoEffect);
        Assert.Same(exhausted.Raw, repeatedRefusal.Raw);
        Assert.Equal(0UL, nativeRecord.GetType().GetProperty("ExecutionStartsIssued")!.GetValue(nativeRecord));
        Assert.Null(runtime.StartOriginalChildExecution(exhaustedRequest with { }));
        Assert.Null(runtime.ResolveOriginalChildExecutionStart(exhaustedRequest with { }));
        var refusalRows = (IList)nativeRecord.GetType().GetProperty("OriginalStartRefusals")!.GetValue(nativeRecord)!;
        var originalRefusal = refusalRows[0]!;
        refusalRows.Add(originalRefusal);
        nativeRecord.GetType().GetProperty("StartRefusalsIssued")!.SetValue(nativeRecord, 2UL);
        Assert.Null(runtime.ResolveOriginalChildExecutionStart(exhaustedRequest));
        Assert.Null(runtime.StartOriginalChildExecution(exhaustedRequest));
        Assert.Equal(0UL, nativeRecord.GetType().GetProperty("ExecutionStartsIssued")!.GetValue(nativeRecord));
        var execution = nativeRecord.GetType().GetProperty("Execution")!.GetValue(nativeRecord)!;
        var privateMemory = (OwnedExecutableDataMemory)execution.GetType().GetField("memory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(execution)!;
        Assert.False(privateMemory.Retired);
        refusalRows.RemoveAt(1);
        nativeRecord.GetType().GetProperty("StartRefusalsIssued")!.SetValue(nativeRecord, 1UL);
        Assert.Same(exhausted.Raw, runtime.ResolveOriginalChildExecutionStart(exhaustedRequest)!.Raw);
        refusalRows[0] = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(originalRefusal, null);
        Assert.Null(runtime.ResolveOriginalChildExecutionStart(exhaustedRequest));
        Assert.Null(runtime.StartOriginalChildExecution(exhaustedRequest));
        Assert.Equal(ExternalRuntimeOutcome.Faulted, runtime.StartChildExecution(child,
            new(artifact.ArtifactHandle, artifact.ArtifactEpoch, Op(513))).Outcome);
        Assert.Equal(0UL, nativeRecord.GetType().GetProperty("ExecutionStartsIssued")!.GetValue(nativeRecord));
        refusalRows.Clear();
        Assert.Null(runtime.ResolveOriginalChildExecutionStart(exhaustedRequest));
        Assert.Null(runtime.StartOriginalChildExecution(exhaustedRequest));
        refusalRows.Add(originalRefusal); // Restore test-corrupted history; source/fact not fabricated.
        Assert.Same(exhausted.Raw, runtime.ResolveOriginalChildExecutionStart(exhaustedRequest)!.Raw);
        var start = new ExternalChildExecutionStartRequest(artifact.ArtifactHandle, artifact.ArtifactEpoch, Op(504));
        var request = new ExternalOriginalChildExecutionStartRequest(child, artifact, start);
        Assert.Null(runtime.ResolveOriginalExecutionTerminal(request));
        var copiedArtifact = runtime.StartOriginalChildExecution(request with { Artifact = artifact with { } });
        Assert.True(copiedArtifact!.NoEffect);
        Assert.Equal(ExternalRuntimeOutcome.Denied, copiedArtifact.Raw.Outcome);
        Assert.Null(runtime.ResolveOriginalChildExecutionStart(request));
        var observed = runtime.StartOriginalChildExecution(request)!;
        Assert.False(observed.NoEffect);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, observed.Raw.Outcome);
        var terminal = Assert.IsType<ExternalOriginalExecutionTerminalObservation>(runtime.ResolveOriginalExecutionTerminal(request));
        Assert.Same(request, terminal.Request);
        Assert.Same(observed.Raw.Receipt, terminal.Receipt);
        Assert.True(Enum.IsDefined(terminal.Kind));
        Assert.Same(terminal, runtime.ResolveOriginalExecutionTerminal(request));
        Assert.True(runtime.ValidateCurrentOriginalExecutionTerminal(request, terminal));
        Assert.False(runtime.ValidateCurrentOriginalExecutionTerminal(request, terminal with { }));
        Assert.False(runtime.ValidateCurrentOriginalExecutionTerminal(request with { }, terminal));
        Assert.Null(runtime.ResolveOriginalExecutionTerminal(request with { }));
        Assert.Same(observed.Raw, runtime.ResolveOriginalChildExecutionStart(request)!.Raw);
        Assert.Null(runtime.ResolveOriginalChildExecutionStart(request with { }));
        var repeated = runtime.StartOriginalChildExecution(request)!;
        Assert.False(repeated.NoEffect);
        Assert.Same(observed.Raw, repeated.Raw);
        var records = (IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        var record = records[child.Handle]!;
        var factProperty = record.GetType().GetProperty("OriginalExecutionStart")!;
        var generationProperty = record.GetType().GetProperty("ExecutionGeneration")!;
        var generation = generationProperty.GetValue(record);
        var originalFact = factProperty.GetValue(record)!;
        var clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(originalFact, null);
        factProperty.SetValue(record, clone);
        Assert.Null(runtime.ResolveOriginalExecutionTerminal(request));
        Assert.Null(runtime.ResolveOriginalChildExecutionStart(request));
        Assert.False(runtime.ValidateCurrentOriginalExecutionTerminal(request, terminal));
        Assert.Null(runtime.StartOriginalChildExecution(request));
        Assert.Equal(generation, generationProperty.GetValue(record));
        factProperty.SetValue(record, null);
        Assert.Null(runtime.ResolveOriginalChildExecutionStart(request));
        Assert.Null(runtime.StartOriginalChildExecution(request));
        Assert.Equal(generation, generationProperty.GetValue(record));
        var release = runtime.ReleaseChildExecutableArtifact(new(child, artifact, Op(505)));
        Assert.Equal(ExternalRuntimeOutcome.Closed, release.Outcome);
        Assert.True(release.Receipt!.NoFurtherUse); // Independent actual joined backing guarantee.
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.UnmapChildGuestMemory(mapping, Op(506)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseChildDomain(child, Op(507)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(parent, Op(508)).Outcome);
    }
}
