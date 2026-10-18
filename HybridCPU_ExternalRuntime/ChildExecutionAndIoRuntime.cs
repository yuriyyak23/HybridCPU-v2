using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime
{
    public ExternalChildResult<ExternalChildArtifactBindReceipt> BindChildExecutableArtifact(
        ExternalChildDomainLease child,
        ExternalChildArtifactBindRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Valid(request.Operation) || request.PackageBytes is null || request.PackageBytes.Length == 0 ||
            request.MaximumPipelineCycles <= 0 || request.MaximumPrivateDataBytes > 0x3000_0000 || request.MaximumPrivateDataBytes % 4096 != 0)
            return Denied<ExternalChildArtifactBindReceipt>("Artifact bytes and finite execution bound are required.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildArtifactBindReceipt>(lookup, reason);
        if (!TryFindMapping(request.GuestMapping, out GuestMappingRecord? mapping, out lookup, out reason))
            return Result<ExternalChildArtifactBindReceipt>(lookup, reason);

        var package = request.PackageBytes.ToArray(); // Caller memory outside owner locks.
        if (!TryGuestMapLineage(record!, out var lineage))
            return Denied<ExternalChildArtifactBindReceipt>("Original artifact lineage is unavailable.");
        lock (record!.ParentSource.Sync)
        return WithNestedLineage<ExternalChildResult<ExternalChildArtifactBindReceipt>>(lineage, 0, () =>
        {
            if (!HasOriginalNestedGuestSources(record) || record.ImmediateParent is not null && lineage.Any(source =>
                source.State is not (ExternalChildDomainState.Ready or ExternalChildDomainState.Running or ExternalChildDomainState.Parked) ||
                source.OriginalClose is { Observation: not { NoEffect: true } }) ||
                !OriginalBindHistoryIntact(record) || record.ArtifactBindsIssued == ulong.MaxValue ||
                record.OriginalArtifactBinds.Any(fact => ReferenceEquals(fact.Request, request) || fact.Request.Operation == request.Operation))
                return Denied<ExternalChildArtifactBindReceipt>("Original Bind history/correlation unavailable; invocation cannot repeat.");
            if (!TryFindChild(child, out var currentChild, out _, out _) || !ReferenceEquals(currentChild, record) ||
                !TryFind(child.Parent, out var parent, out _, out _) || !ReferenceEquals(parent, record.ParentSource) ||
                parent!.Lifecycle is Lifecycle.Closed or Lifecycle.Draining or Lifecycle.Revoked or Lifecycle.Faulted or Lifecycle.Quarantined or Lifecycle.Opening ||
                !TryFindMapping(request.GuestMapping, out var currentMapping, out _, out _) || !ReferenceEquals(currentMapping, mapping) ||
                !record.Mappings.TryGetValue(mapping!.Lease.Handle, out var ownedMapping) || !ReferenceEquals(ownedMapping, mapping))
                return Denied<ExternalChildArtifactBindReceipt>("Original artifact admission parent/child/mapping source is unavailable.");
            if (record.State != ExternalChildDomainState.Ready || record.Artifact is not null || record.Execution is not null || record.ArtifactBind is not null)
                return Denied<ExternalChildArtifactBindReceipt>("Artifact binding is single-use and requires a ready child.");
            if (!record.Authority.HasFlag(ExternalChildAuthority.Execute) ||
                !record.Authority.HasFlag(ExternalChildAuthority.GuestMemory))
                return Denied<ExternalChildArtifactBindReceipt>("Artifact binding requires child execution and guest-memory authority.");
            if (mapping!.Closed || mapping.Lease.Child != child)
                return Denied<ExternalChildArtifactBindReceipt>("Artifact mapping is closed or belongs to another child.");
            if ((ulong)package.LongLength > mapping.Length)
                return Denied<ExternalChildArtifactBindReceipt>("Artifact exceeds the exact admitted guest mapping.");
            record.OriginalArtifactBinds.EnsureCapacity(record.OriginalArtifactBinds.Count + 1);
            if (!ReserveChildOperation(record, request.Operation, out reason))
                return Denied<ExternalChildArtifactBindReceipt>(reason);
            var fact = new ArtifactBindFact(request, record, mapping, package);
            fact.EstablishSelf();
            record.OriginalArtifactBinds.Add(fact);
            record.ArtifactBindsIssued++;
            record.ArtifactBind = fact; // Original intent before possible backing creation.
            CpuBackedChildExecution? execution = null;
            bool noEffect = false;
            bool created;
            try
            {
                created = CpuBackedChildExecution.TryCreate(package, request.MaximumPipelineCycles,
                    out execution, out reason, out noEffect, backing => fact.Backing = backing,
                    request.MaximumPrivateDataBytes == 0 ? 0x3000_0000 : request.MaximumPrivateDataBytes);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                created = false;
                reason = $"Native materialization failed: {exception.GetType().Name}.";
            }
            if (!created)
            {
                fact.NoEffect = noEffect;
                fact.Raw = Result<ExternalChildArtifactBindReceipt>(ExternalRuntimeOutcome.Denied, reason);
                if (noEffect) record.ArtifactBind = null;
                else if (fact.Backing is { } backing)
                {
                    var cleanup = new ExternalChildArtifactBindReceipt(
                        new(Guid.NewGuid()), new(NextNonZero(ref nextArtifactEpoch)),
                        child.Handle, child.Epoch, mapping.Lease.Handle, mapping.Lease.Epoch, child.Parent,
                        fact.Digest, request.MaximumPipelineCycles, request.Operation,
                        manifest.ContractVersion, manifest.Generation) { MaximumPrivateDataBytes = request.MaximumPrivateDataBytes };
                    fact.CleanupArtifact = cleanup;
                    record.Execution = backing;
                    record.Artifact = cleanup;
                    record.ArtifactAdmissionOwner = record;
                    record.ArtifactAdmissionMapping = mapping;
                }
                return fact.Raw;
            }

            var receipt = new ExternalChildArtifactBindReceipt(
                new(Guid.NewGuid()), new(NextNonZero(ref nextArtifactEpoch)),
                child.Handle, child.Epoch, mapping.Lease.Handle, mapping.Lease.Epoch, child.Parent,
                execution!.PackageSha256, request.MaximumPipelineCycles, request.Operation,
                manifest.ContractVersion, manifest.Generation) { MaximumPrivateDataBytes = request.MaximumPrivateDataBytes };
            record.Execution = execution;
            record.Artifact = receipt;
            record.ArtifactAdmissionOwner = record;
            record.ArtifactAdmissionMapping = mapping;
            fact.Observation = receipt; // Original fact before caller callbacks/transport loss.
            fact.Raw = Success(ExternalRuntimeOutcome.Succeeded, receipt);
            return fact.Raw;
        });
    }

    public ExternalChildResult<ExternalChildExecutionReceipt> StartChildExecution(
        ExternalChildDomainLease child,
        ExternalChildExecutionStartRequest request)
        => StartChildExecutionCore(child, request, original: null, out _);

    private ExternalChildResult<ExternalChildExecutionReceipt> StartChildExecutionCore(
        ExternalChildDomainLease child, ExternalChildExecutionStartRequest request,
        ExternalOriginalChildExecutionStartRequest? original, out bool noEffect)
    {
        noEffect = true;
        ArgumentNullException.ThrowIfNull(request);
        if (!Valid(request.Operation) || request.ArtifactHandle.Value == Guid.Empty || request.ArtifactEpoch.Value == 0)
            return Denied<ExternalChildExecutionReceipt>("Artifact identity and start operation are required.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildExecutionReceipt>(lookup, reason);

        if (!TryGuestMapLineage(record!, out var lineage))
            return Denied<ExternalChildExecutionReceipt>("Original Start ancestry unavailable before effect.");
        var invocationNoEffect = true;
        try
        {
        lock (record!.ParentSource.Sync)
        return WithNestedLineage<ExternalChildResult<ExternalChildExecutionReceipt>>(lineage, 0, () =>
        {
            if (!HasOriginalNestedGuestSources(record) || record.ImmediateParent is not null && lineage.Any(source =>
                source.State is not (ExternalChildDomainState.Ready or ExternalChildDomainState.Running or ExternalChildDomainState.Parked) ||
                source.OriginalClose is { Observation: not { NoEffect: true } }))
                return Denied<ExternalChildExecutionReceipt>("Original Start ancestry/readiness unavailable before effect.");
            ExternalChildArtifactBindReceipt? artifact = record.Artifact;
            if (!OriginalBindHistoryIntact(record) || !OriginalStartRefusalsIntact(record))
            {
                invocationNoEffect = false;
                return Result<ExternalChildExecutionReceipt>(ExternalRuntimeOutcome.Faulted, "Original refusal history is unknown; Start denied.");
            }
            if (original is not null && record.ExecutionStartsIssued != 0)
            {
                invocationNoEffect = false; // Original lifetime already crossed the possible-effect boundary.
                return record.OriginalExecutionStart is { } previous && ReferenceEquals(previous.OwnerSelf, previous) && ReferenceEquals(previous.Original, original)
                    && previous.Observation is { } known ? known
                    : Result<ExternalChildExecutionReceipt>(ExternalRuntimeOutcome.Faulted, "Original Start history is unknown; no invocation retry.");
            }
            if (!TryFindChild(child, out var currentChild, out _, out _) || !ReferenceEquals(currentChild, record) ||
                !ReferenceEquals(record.OriginalOwner, record) ||
                !TryFind(child.Parent, out var parent, out _, out _) || !ReferenceEquals(parent, record.ParentSource) ||
                parent!.Lifecycle is Lifecycle.Closed or Lifecycle.Draining or Lifecycle.Revoked or Lifecycle.Faulted or Lifecycle.Quarantined or Lifecycle.Opening ||
                !ReferenceEquals(record.ArtifactAdmissionOwner, record) ||
                artifact is null || record.ArtifactBind is not { } bind || !ReferenceEquals(bind.OwnerSelf, bind) || !ReferenceEquals(bind.Child, record) ||
                !ReferenceEquals(bind.Observation, artifact) || bind.ChildAdmission is null ||
                !ReferenceEquals(bind.ChildAdmission, record.OriginalAdmission) || record.OriginalExecutionStart is not null || record.ExecutionStartsIssued != 0 ||
                (original is not null && (!ReferenceEquals(original.Artifact, artifact) || !ReferenceEquals(original.Start, request) || original.Child != child)))
                return Denied<ExternalChildExecutionReceipt>("Original Start factory/parent/attempt is unavailable.");
            if (record.ArtifactRelease is not null || record.ArtifactReleasesIssued != 0)
                return Denied<ExternalChildExecutionReceipt>("Original artifact release intent permanently refuses further Start.");
            if (record.State != ExternalChildDomainState.Ready || artifact is null || record.Execution is null)
                return Denied<ExternalChildExecutionReceipt>("The child has no ready executable artifact binding.");
            if (artifact.ArtifactHandle != request.ArtifactHandle)
                return Denied<ExternalChildExecutionReceipt>("Start identifies another artifact.");
            if (artifact.ArtifactEpoch != request.ArtifactEpoch)
                return Result<ExternalChildExecutionReceipt>(ExternalRuntimeOutcome.Stale, "Artifact epoch is stale.");
            if (!record.Mappings.TryGetValue(artifact.MappingHandle, out GuestMappingRecord? mapping) ||
                mapping.Closed || mapping.Lease.Epoch != artifact.MappingEpoch ||
                !ReferenceEquals(mapping.OriginalOwner, mapping) || !ReferenceEquals(record.ArtifactAdmissionMapping, mapping) ||
                !ReferenceEquals(bind.Mapping, mapping) || bind.MappingAdmission is null ||
                !ReferenceEquals(bind.MappingAdmission, mapping.OriginalAdmission) ||
                !TryFindMapping(mapping.Lease, out var currentMapping, out _, out _) || !ReferenceEquals(currentMapping, mapping))
                return Result<ExternalChildExecutionReceipt>(ExternalRuntimeOutcome.Stale, "The exact artifact guest mapping is no longer live.");
            if (!TryReserveExecutionGeneration(out var reservedGeneration))
                return Denied<ExternalChildExecutionReceipt>("Execution generation capacity is exhausted before effect.");
            if (!ReserveChildOperation(record, request.Operation, out reason))
                return Denied<ExternalChildExecutionReceipt>(reason);
            var fact = new OriginalExecutionStartFact(original, request, record, mapping, artifact, record.Execution);
            fact.EstablishSelf();
            record.OriginalExecutionStart = fact; // Original identity before possible execution.
            record.ExecutionStartsIssued = 1;
            invocationNoEffect = false;
            if (!record.Execution.ExecuteToTerminal(out var terminal, out reason))
            {
                record.State = ExternalChildDomainState.Parked;
                fact.Observation = Result<ExternalChildExecutionReceipt>(ExternalRuntimeOutcome.Faulted, reason);
                return fact.Observation;
            }

            record.State = ExternalChildDomainState.Parked;
            record.ExecutionGeneration = new(reservedGeneration);
            var receipt = new ExternalChildExecutionReceipt(
                artifact.ArtifactHandle, artifact.ArtifactEpoch, child.Handle, child.Epoch,
                artifact.MappingHandle, artifact.MappingEpoch, child.Parent, artifact.PackageSha256,
                record.ExecutionGeneration, terminal!.RetiredPipelineCycles, terminal.LastRetireSequence,
                terminal.LastRetiredBundlePc - record.Execution.ImageBase, request.Operation,
                manifest.ContractVersion, manifest.Generation, IsTerminal: true);
            if (original is not null)
            {
                ExternalExecutionTerminalKind? kind = terminal.Status switch
                {
                    HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed.HybridCpuIseManagedGuestExecutionStatusV1.Completed => ExternalExecutionTerminalKind.Completed,
                    HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed.HybridCpuIseManagedGuestExecutionStatusV1.CycleBudgetExceeded => ExternalExecutionTerminalKind.BudgetExhausted,
                    HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed.HybridCpuIseManagedGuestExecutionStatusV1.KernelRejected => ExternalExecutionTerminalKind.KernelRejected,
                    HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed.HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault => ExternalExecutionTerminalKind.ExecutionFailure,
                    _ => null,
                };
                if (kind is { } known)
                    fact.TerminalObservation = new(original, receipt, known)
                    { ProcessExitCode = known == ExternalExecutionTerminalKind.Completed ? terminal.ProcessExitCode : null }; // Raw before return/transport.
            }
            fact.Observation = Success(ExternalRuntimeOutcome.Succeeded, receipt);
            return fact.Observation;
        });
        }
        finally { noEffect = invocationNoEffect; }
    }

    public ExternalChildResult<ExternalChildVirtualIoBindReceipt> BindChildVirtualIo(
        ExternalChildDomainLease child,
        ExternalChildVirtualIoBindRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        const ExternalChildVirtualIoRights known = ExternalChildVirtualIoRights.Read |
            ExternalChildVirtualIoRights.Write | ExternalChildVirtualIoRights.Configure;
        if (!Valid(request.Operation) || request.ParentDeviceHandle.Value == Guid.Empty ||
            request.ParentDeviceEpoch.Value == 0 || request.Rights == ExternalChildVirtualIoRights.None ||
            (request.Rights & ~known) != 0 || request.MaximumTransferBytes == 0)
            return Denied<ExternalChildVirtualIoBindReceipt>("Bounded parent device identity, rights, and transfer limit are required.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildVirtualIoBindReceipt>(lookup, reason);
        lock (record!.Sync)
        {
            if (record.State is ExternalChildDomainState.Closed ||
                !record.Authority.HasFlag(ExternalChildAuthority.VirtualIo))
                return Denied<ExternalChildVirtualIoBindReceipt>("Child lifecycle or authority does not admit virtual I/O.");
            if (!ReserveChildOperation(record, request.Operation, out reason))
                return Denied<ExternalChildVirtualIoBindReceipt>(reason);
            var receipt = new ExternalChildVirtualIoBindReceipt(
                new(Guid.NewGuid()), new(NextNonZero(ref nextVirtualIoEpoch)), child.Handle, child.Epoch,
                child.Parent, request.ParentDeviceHandle, request.ParentDeviceEpoch, request.Rights,
                request.MaximumTransferBytes, request.Operation, manifest.ContractVersion, manifest.Generation);
            record.VirtualIo.Add(receipt.IoHandle, new() { Receipt = receipt });
            return Success(ExternalRuntimeOutcome.Succeeded, receipt);
        }
    }

    public ExternalChildResult<ExternalChildVirtualIoCloseReceipt> CloseChildVirtualIo(
        ExternalChildDomainLease child,
        ExternalChildVirtualIoHandle ioHandle,
        ExternalChildVirtualIoEpoch ioEpoch,
        ExternalOperationIdentity operation)
    {
        if (!Valid(operation) || ioHandle.Value == Guid.Empty || ioEpoch.Value == 0)
            return Denied<ExternalChildVirtualIoCloseReceipt>("Exact I/O identity and close operation are required.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildVirtualIoCloseReceipt>(lookup, reason);
        lock (record!.Sync)
        {
            if (!record.VirtualIo.TryGetValue(ioHandle, out VirtualIoRecord? io))
                return Result<ExternalChildVirtualIoCloseReceipt>(ExternalRuntimeOutcome.NotFound, "I/O binding was not found.");
            if (io.Receipt.IoEpoch != ioEpoch)
                return Result<ExternalChildVirtualIoCloseReceipt>(ExternalRuntimeOutcome.Stale, "I/O binding epoch is stale.");
            if (io.Closed)
                return Result<ExternalChildVirtualIoCloseReceipt>(ExternalRuntimeOutcome.Stale, "I/O binding is already closed.");
            if (!ReserveChildOperation(record, operation, out reason))
                return Denied<ExternalChildVirtualIoCloseReceipt>(reason);
            io.Closed = true;
            return Success(ExternalRuntimeOutcome.Closed, new ExternalChildVirtualIoCloseReceipt(
                ioHandle, ioEpoch, child.Handle, child.Epoch, child.Parent, operation,
                manifest.ContractVersion, manifest.Generation, IsTerminal: true));
        }
    }
}

