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
            request.MaximumPipelineCycles <= 0)
            return Denied<ExternalChildArtifactBindReceipt>("Artifact bytes and finite execution bound are required.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildArtifactBindReceipt>(lookup, reason);
        if (!TryFindMapping(request.GuestMapping, out GuestMappingRecord? mapping, out lookup, out reason))
            return Result<ExternalChildArtifactBindReceipt>(lookup, reason);

        lock (record!.Sync)
        {
            if (record.State != ExternalChildDomainState.Ready || record.Artifact is not null || record.Execution is not null)
                return Denied<ExternalChildArtifactBindReceipt>("Artifact binding is single-use and requires a ready child.");
            if (!record.Authority.HasFlag(ExternalChildAuthority.Execute) ||
                !record.Authority.HasFlag(ExternalChildAuthority.GuestMemory))
                return Denied<ExternalChildArtifactBindReceipt>("Artifact binding requires child execution and guest-memory authority.");
            if (mapping!.Closed || mapping.Lease.Child != child)
                return Denied<ExternalChildArtifactBindReceipt>("Artifact mapping is closed or belongs to another child.");
            if ((ulong)request.PackageBytes.LongLength > mapping.Length)
                return Denied<ExternalChildArtifactBindReceipt>("Artifact exceeds the exact admitted guest mapping.");
            if (!ReserveChildOperation(record, request.Operation, out reason))
                return Denied<ExternalChildArtifactBindReceipt>(reason);
            if (!CpuBackedChildExecution.TryCreate(request.PackageBytes, request.MaximumPipelineCycles,
                    out CpuBackedChildExecution? execution, out reason))
                return Result<ExternalChildArtifactBindReceipt>(ExternalRuntimeOutcome.Denied, reason);

            var receipt = new ExternalChildArtifactBindReceipt(
                new(Guid.NewGuid()), new(NextNonZero(ref nextArtifactEpoch)),
                child.Handle, child.Epoch, mapping.Lease.Handle, mapping.Lease.Epoch, child.Parent,
                execution!.PackageSha256, request.MaximumPipelineCycles, request.Operation,
                manifest.ContractVersion, manifest.Generation);
            record.Execution = execution;
            record.Artifact = receipt;
            return Success(ExternalRuntimeOutcome.Succeeded, receipt);
        }
    }

    public ExternalChildResult<ExternalChildExecutionReceipt> StartChildExecution(
        ExternalChildDomainLease child,
        ExternalChildExecutionStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Valid(request.Operation) || request.ArtifactHandle.Value == Guid.Empty || request.ArtifactEpoch.Value == 0)
            return Denied<ExternalChildExecutionReceipt>("Artifact identity and start operation are required.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildExecutionReceipt>(lookup, reason);

        lock (record!.Sync)
        {
            ExternalChildArtifactBindReceipt? artifact = record.Artifact;
            if (record.State != ExternalChildDomainState.Ready || artifact is null || record.Execution is null)
                return Denied<ExternalChildExecutionReceipt>("The child has no ready executable artifact binding.");
            if (artifact.ArtifactHandle != request.ArtifactHandle)
                return Denied<ExternalChildExecutionReceipt>("Start identifies another artifact.");
            if (artifact.ArtifactEpoch != request.ArtifactEpoch)
                return Result<ExternalChildExecutionReceipt>(ExternalRuntimeOutcome.Stale, "Artifact epoch is stale.");
            if (!record.Mappings.TryGetValue(artifact.MappingHandle, out GuestMappingRecord? mapping) ||
                mapping.Closed || mapping.Lease.Epoch != artifact.MappingEpoch)
                return Result<ExternalChildExecutionReceipt>(ExternalRuntimeOutcome.Stale, "The exact artifact guest mapping is no longer live.");
            if (!ReserveChildOperation(record, request.Operation, out reason))
                return Denied<ExternalChildExecutionReceipt>(reason);
            if (!record.Execution.ExecuteToTerminal(out var terminal, out reason))
            {
                record.State = ExternalChildDomainState.Parked;
                return Result<ExternalChildExecutionReceipt>(ExternalRuntimeOutcome.Faulted, reason);
            }

            record.State = ExternalChildDomainState.Parked;
            record.ExecutionGeneration = new(NextNonZero(ref nextExecutionGeneration));
            var receipt = new ExternalChildExecutionReceipt(
                artifact.ArtifactHandle, artifact.ArtifactEpoch, child.Handle, child.Epoch,
                artifact.MappingHandle, artifact.MappingEpoch, child.Parent, artifact.PackageSha256,
                record.ExecutionGeneration, terminal!.RetiredPipelineCycles, terminal.LastRetireSequence,
                terminal.LastRetiredBundlePc - record.Execution.ImageBase, request.Operation,
                manifest.ContractVersion, manifest.Generation, IsTerminal: true);
            return Success(ExternalRuntimeOutcome.Succeeded, receipt);
        }
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
