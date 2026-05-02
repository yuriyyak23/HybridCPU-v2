# ADR 08: AllOrNone Progress And Partial Completion Gate

## Status

Current contract plus future-gated design rule.

This ADR is documentation/design only. It does not approve CPU/ISE code changes and does not approve successful partial completion.

## Context

Phase 08 covers TASK-007: preserve `AllOrNone`, define safe progress diagnostics, and prevent progress evidence from becoming architectural memory visibility.

The current DSC helper/token model stages destination bytes and commits them all-or-none. That behavior is the only successful completion contract currently available.

## Current Contract

- Lane6 `DmaStreamComputeMicroOp` remains fail-closed and is not executable DSC ISA.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled == false`.
- DSC1 accepts only `DmaStreamComputePartialCompletionPolicy.AllOrNone`.
- DSC1 does not define successful partial completion.
- `DmaStreamComputeToken.StageDestinationWrite(...)` requires staged writes to stay inside the normalized write footprint.
- `DmaStreamComputeToken.MarkComputeComplete()` requires exact staged coverage before `CommitPending`.
- `DmaStreamComputeToken.Commit(...)` publishes memory only through the explicit commit API.
- `DmaStreamComputeToken.TryCommitAllOrNone(...)` snapshots destination bytes and rolls back on write failure.
- Staged bytes are not architecturally visible before commit.
- Current helper/model path does not expose successful partial memory visibility.
- Progress counters or telemetry, where present, are non-authoritative diagnostics.
- Compiler/backend production lowering must not depend on partial progress or successful partial completion.

## Decision

Keep `AllOrNone` as the only successful completion policy. Treat progress as diagnostic evidence only.

Successful partial completion remains rejected/forbidden until a separate future ADR defines:

- descriptor ABI support;
- token state semantics;
- visibility and memory publication rules;
- retry, rollback, replay, and cancellation rules;
- precise fault priority;
- ordering, wait, poll, and fence behavior;
- cache/non-coherent visibility obligations;
- compiler/backend lowering contract;
- conformance tests.

If partial success is ever approved, it must be encoded only in DSC2 or a capability-gated extension. DSC1 remains immutable.

## Accepted Direction

### All-Or-None Success

A DSC operation succeeds only when all accepted destination bytes are staged, validated, and committed.

The operation must fault or cancel when:

- any staged write is outside the normalized write footprint;
- staged writes do not exactly cover the required destination footprint;
- staged writes overlap illegally;
- snapshot/backup of destination bytes fails;
- any physical write fails during commit;
- rollback after a partial write cannot be completed.

### Progress Diagnostics

Progress may be recorded only as diagnostics:

- bytes read;
- bytes staged;
- element operations;
- modeled latency;
- backend or helper step counters;
- non-authoritative polling evidence.

Progress diagnostics must not:

- set `Succeeded`;
- set `Committed`;
- publish memory;
- suppress a fault;
- imply precise retire publication;
- allow compiler scheduling to observe partially committed effects.

### Polling Relationship

Future polling may observe diagnostic progress only if the poll contract states that progress is not committed memory visibility.

Polling must not make staged writes visible and must not convert a partial state into success.

### Future Partial-Success Gate

A future partial-success ADR must choose one explicit model:

- partial visibility with precise ranges and commit records;
- retry/resume with idempotent staged writes;
- segmented all-or-none groups;
- checkpoint/rollback with architectural progress records.

Until that ADR exists, any non-`AllOrNone` descriptor must reject.

## Rejected Alternatives

### Alternative 1: Treat Progress As Success

Rejected. Progress evidence is not a memory visibility boundary and does not satisfy precise exception, replay, rollback, ordering, or compiler assumptions.

### Alternative 2: Permit Helper Partial Writes As Success

Rejected. A helper/runtime partial write would bypass architectural commit semantics and break rollback guarantees.

### Alternative 3: Add Partial Success To DSC1

Rejected. DSC1 is immutable and accepts only `AllOrNone`.

### Alternative 4: Let Compiler Schedule Around Partial Visibility

Rejected. Compiler/backend cannot reason about partial memory effects until the ISA, ordering, cache, and fault contracts are explicit.

## Exact Non-Goals

- Do not implement CPU/ISE code in this ADR.
- Do not make lane6 DSC executable.
- Do not define successful partial completion.
- Do not redefine DSC1.
- Do not allow progress diagnostics to imply committed memory.
- Do not authorize compiler/backend production lowering to partial-success DSC.
- Do not claim cache coherence or async overlap from progress counters.

## Required Prerequisites Before Partial-Success Code

- Phase 02 executable lane6 DSC approval.
- Phase 03 token lifecycle and issue/admission allocation.
- Phase 04 precise fault publication and priority.
- Phase 05 ordering/conflict service.
- Phase 07 DSC2/capability ABI for partial-success encoding.
- Phase 09 cache/prefetch/non-coherent protocol.
- Phase 11 compiler/backend lowering contract.
- Phase 12 conformance and documentation migration tests.
- Separate partial-success ADR.

## Required Tests Before Any Partial-Success Claim

- Non-`AllOrNone` DSC1 policy rejects.
- Missing staged write coverage faults.
- Staged write outside normalized footprint faults.
- Overlapping staged writes fault unless explicitly legal.
- Commit write failure rolls back all prior writes.
- Cancel before commit leaves memory unchanged.
- Fault before commit leaves memory unchanged.
- Progress counters can advance without changing token success.
- Polling progress does not publish memory.
- DSC2 partial-success descriptors reject until the future feature is approved.
- Compiler/backend tests reject partial-success lowering while the gate is closed.

## Documentation Migration Rule

Documentation may describe `AllOrNone` as current behavior.

Documentation may describe progress only as diagnostic/model evidence unless and until a later ADR and implementation define architectural progress state.

Documentation must not state that successful partial completion exists until code, tests, compiler contract, and migration review are complete.

## Code Evidence

- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeDescriptor.cs`
  - `DmaStreamComputePartialCompletionPolicy` currently contains only `AllOrNone`.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeDescriptorParser.cs`
  - Rejects any v1 partial completion policy other than `AllOrNone`.
  - `ExecutionEnabled => false`.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeToken.cs`
  - `StageDestinationWrite(...)` validates staged writes against normalized write footprints.
  - `MarkComputeComplete()` requires exact staged write coverage.
  - `Commit(...)` calls `TryCommitAllOrNone(...)`.
  - `TryCommitAllOrNone(...)` snapshots destination bytes and rolls back on write failure.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeRuntime.cs`
  - Runtime/helper publishes `CommitPending`, not automatic architectural memory visibility.

## Strict Prohibitions

This ADR must not be used to claim:

- lane6 DSC is executable;
- partial completion is a successful architectural mode;
- progress counters publish memory;
- polling commits staged writes;
- helper/runtime partial writes are architectural success;
- compiler/backend may rely on partial memory visibility.
