# ADR: Phase 3 Lane6 DMA Execution Model

Status date: 2026-04-29.

Status: historical baseline; superseded for the current DSC1 Phase 06 contour.

Decision: Option A kept the carrier fail-closed in the 2026-04-29 baseline.
Current code supersedes that decision for DSC1 Phase 06 only.

Ex1 status: the current CloseToRTL/NonRTL code now implements the scoped DSC1
Phase 06 materialized lane6 contour. Ex1 Phase12 remains the migration gate for
new claims beyond that contour, and Ex1 Phase13 records dependency order only as
a planning/documentation gate.

## Context

Phase 3 decides whether lane6 `DmaStreamComputeMicroOp` stays a descriptor and
decode evidence carrier or becomes executable DMA/stream-compute ISA behavior.
This decision is based on the current code baseline:

- Historical ADR baseline: `DmaStreamComputeMicroOp.Execute(ref Processor.CPU_Core core)`
  was fail-closed and `DmaStreamComputeDescriptorParser.ExecutionEnabled` was `false`.
- Current code supersedes that baseline for the DSC1 Phase 06 contour:
  `Execute(...)` enters `ExecuteMaterializedMicroOpToCommitPending(...)` after
  materialization and `ExecutionEnabled` is `true`.
- `DmaStreamComputeRuntime` is the explicit runtime path. The canonical
  micro-op entry is `ExecuteMaterializedMicroOpToCommitPending(...)`; the
  direct `ExecuteToCommitPending(...)` helper remains helper/orchestration code.
- `DmaStreamComputeToken.Commit(...)` is an explicit model commit API.
- `DMAController` channel controls are separate memory-transfer controls, not
  DSC token lifecycle or lane6 ISA controls.
- Runtime/helper memory uses physical main memory through
  `TryReadPhysicalRange(...)` and `TryWritePhysicalRange(...)`.

## Current Decision State

Lane6 DSC1 is no longer descriptor/decode evidence only. It has a scoped
materialized Phase 06 execution path.

The current contract keeps these boundaries:

- `DmaStreamComputeMicroOp.Execute(...)` remains gated to the current DSC1
  Phase 06 contour and fails closed outside it.
- `DmaStreamComputeRuntime` remains the explicit runtime path; direct
  `ExecuteToCommitPending(...)` is helper/orchestration code, while canonical
  micro-op execution uses `ExecuteMaterializedMicroOpToCommitPending(...)`.
- Issue/admission owns current lane6 token allocation for the materialized
  micro-op path.
- Normal issue/execute/retire invokes the materialized DSC runtime entry only
  for the current DSC1 contour.
- Broad production compiler/backend lowering may not treat arbitrary DSC forms
  as executable memory compute ISA.
- Documentation and conformance tests continue to protect the adjacent
  fail-closed boundaries.

## Rejected Alternative

The broad version of Option B, "Make Lane6 Executable", is not approved for
arbitrary DSC descriptors or production lowering.

Expansion beyond current DSC1 Phase 06 remains an architecture change. Before
new implementation, a separate plan must specify pipeline stage, token
allocation, runtime invocation, commit/retire boundary, fault priority, precise
exception path, replay/squash/trap/context-switch behavior, CPU
load/store/atomic/fence ordering, physical versus virtual/IOMMU addressing,
synchronous versus async completion, compiler/backend lowering, and conformance
tests.

## Consequences

The current implemented contract is scoped and non-breaking for DSC1 Phase 06.

Future executable expansion stays in Future Design until separately approved.
WhiteBook "Current Implemented Contract" sections must not describe DSC2,
queue/async DMA overlap, global CPU load/store conflict hooks, executable DSC
fences, virtual/IOMMU/cache-coherent DSC memory, or production DSC
compiler/backend lowering as implemented behavior.

The Ex1 dependency graph also blocks executable DSC2, successful partial
completion, IOMMU-backed DSC execution, coherent DMA/cache, and compiler/backend
production lowering until the relevant ADR, code, tests, compiler conformance,
and Phase12 documentation claim-safety are complete. Helper/runtime tokens,
parser-only DSC2 surfaces, backend infrastructure, conflict/cache observers,
and compiler sideband transport are not upstream execution evidence.
