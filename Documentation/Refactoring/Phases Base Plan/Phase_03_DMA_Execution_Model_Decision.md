# Phase 3 - DMA Execution Model Decision

Status date: 2026-04-29.

Status: closed architecture decision.

Decision date: 2026-04-29.

Decision: Option A - Keep Fail-Closed Carrier.

Goal: decide whether lane6 `DmaStreamComputeMicroOp` remains a descriptor
evidence carrier or becomes an executable DMA/stream-compute micro-op.

This phase is not permission to implement executable DMA. It records that
permission was not granted for the current refactoring scope.

## Current Baseline

- `DmaStreamComputeMicroOp.Execute(ref Processor.CPU_Core core)` throws
  fail-closed.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` is `false`.
- `DmaStreamComputeRuntime` is an explicit runtime/model helper.
- `DmaStreamComputeToken.Commit(...)` is an explicit commit API.
- No lane6 ISA start/stop/pause/resume/reset/fence commands exist.
- `DMAController` pause/resume/cancel APIs are separate DMA channel controls
  and are not DSC token controls.
- `Processor.MainMemoryArea.TryReadPhysicalRange(...)` and
  `TryWritePhysicalRange(...)` are the runtime/helper memory portal; there is
  no current virtual/IOMMU/cache-coherent DSC memory model.

## Option A - Keep Fail-Closed Carrier

Decision meaning:

- lane6 DSC remains descriptor/decode evidence only;
- no pipeline execution path is added;
- no token is allocated by normal pipeline issue/execute/retire;
- runtime helper remains explicit and test/orchestration-only;
- compiler/backend must not lower production memory compute to executable DSC.

Completed follow-through:

- record the decision in WhiteBook/phase docs;
- keep negative `DmaStreamComputeMicroOp.Execute` tests authoritative;
- keep compiler/backend conformance tests fail-closed for executable lowering;
- maintain future executable semantics only under Future Design.

Compatibility: non-breaking.

## Option B - Make Lane6 Executable

Decision meaning:

- `DmaStreamComputeMicroOp.Execute` becomes a real executable micro-op;
- pipeline, memory, commit, retire, replay, fault, and ordering semantics must
  be specified before implementation.

Required architecture decisions before coding:

- token allocation point: decode, issue, execute, memory stage, or retire;
- runtime invocation point: `Execute`, scheduler, memory stage, or DMA engine;
- commit point: execute, memory, writeback, retire, or external completion;
- exception path and priority versus other VLIW slot exceptions;
- replay/squash/trap/context-switch behavior for active tokens;
- ordering against CPU loads/stores, atomics, fences, and store buffers;
- physical versus virtual/IOMMU addressing;
- synchronous versus async completion and polling/interrupt/fence semantics;
- compiler lowering contract and whether lane6 ever writes `rd`.

Minimum implementation plan if approved:

- positive copy/add/mul/fma/reduce execution tests;
- retire exception publication tests;
- replay/squash cancellation tests;
- all-or-none commit and rollback tests;
- CPU load/store ordering litmus tests;
- trap priority tests in multi-slot bundles;
- compiler/backend conformance tests;
- WhiteBook migration from Future Design to Current Implemented Contract only
  after code lands.

Compatibility: breaking relative to the current fail-closed baseline.

Phase 3 result: not approved. Option B remains future architecture only.

## Decision Record

Option A is selected for the stabilization branch.

Reason:

- current code is internally consistent as fail-closed carrier plus explicit
  runtime helper;
- Phase 0-2 tests now protect that boundary;
- Option B touches pipeline, memory, retire, replay, exceptions, compiler, and
  documentation, so it cannot be treated as cleanup.
- no separate architecture approval for executable lane6 DMA was provided.

Consequences:

- `DmaStreamComputeMicroOp.Execute(...)` remains fail-closed.
- `DmaStreamComputeRuntime` remains explicit runtime/model helper code.
- No pipeline token allocation point is added.
- No production compiler/backend executable DSC lowering is approved.
- Documentation and tests continue to protect the fail-closed boundary.
- Future executable semantics stay in Future Design / Phase 6 until separately
  approved.

## Exit Criteria

- Architecture decision record explicitly selects Option A.
- No code implementation is needed for this phase.
- Option B requires a separate implementation plan before touching code.
