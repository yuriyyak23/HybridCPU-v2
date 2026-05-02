# ADR: Phase 3 Lane6 DMA Execution Model

Status date: 2026-04-29.

Status: accepted.

Decision: Option A - Keep Fail-Closed Carrier.

Ex1 status: confirmed by Ex1 Phase02 and ADR_02 as the current contract. Ex1
Phase12 remains the migration gate for any future executable claim, and Ex1
Phase13 records dependency order only as a planning/documentation gate.

## Context

Phase 3 decides whether lane6 `DmaStreamComputeMicroOp` stays a descriptor and
decode evidence carrier or becomes executable DMA/stream-compute ISA behavior.
This decision is based on the current code baseline:

- `DmaStreamComputeMicroOp.Execute(ref Processor.CPU_Core core)` throws
  fail-closed.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` is `false`.
- `DmaStreamComputeRuntime` is an explicit runtime/model helper and is not
  called by `MicroOp.Execute`.
- `DmaStreamComputeToken.Commit(...)` is an explicit model commit API.
- `DMAController` channel controls are separate memory-transfer controls, not
  DSC token lifecycle or lane6 ISA controls.
- Runtime/helper memory uses physical main memory through
  `TryReadPhysicalRange(...)` and `TryWritePhysicalRange(...)`.

## Decision

Lane6 DSC remains descriptor/decode evidence only.

The current contract keeps these boundaries:

- `DmaStreamComputeMicroOp.Execute(...)` remains fail-closed.
- `DmaStreamComputeRuntime` remains explicit runtime/model helper code.
- No pipeline token allocation point is added.
- No normal issue/execute/retire path invokes DSC runtime helper execution.
- No production compiler/backend lowering may treat DSC as executable memory
  compute ISA.
- Documentation and conformance tests continue to protect the fail-closed
  boundary.

## Rejected Alternative

Option B, "Make Lane6 Executable", is not approved for the current refactoring
scope.

Executable lane6 DMA would be a breaking architecture change. Before any code
implementation, a separate plan must specify pipeline stage, token allocation,
runtime invocation, commit/retire boundary, fault priority, precise exception
path, replay/squash/trap/context-switch behavior, CPU load/store/atomic/fence
ordering, physical versus virtual/IOMMU addressing, synchronous versus async
completion, compiler/backend lowering, and conformance tests.

## Consequences

The current implemented contract is unchanged and non-breaking.

Future executable behavior stays in Future Design / Phase 6 as `FEATURE-000`
until separately approved. WhiteBook "Current Implemented Contract" sections
must not describe pipeline-executable DSC, async DMA overlap, global CPU
load/store conflict hooks, executable DSC fences, virtual/IOMMU/cache-coherent
DSC memory, or production executable DSC compiler/backend lowering as
implemented behavior.

The Ex1 dependency graph also blocks executable DSC2, successful partial
completion, IOMMU-backed DSC execution, coherent DMA/cache, and compiler/backend
production lowering until the relevant ADR, code, tests, compiler conformance,
and Phase12 documentation claim-safety are complete. Helper/runtime tokens,
parser-only DSC2 surfaces, backend infrastructure, conflict/cache observers,
and compiler sideband transport are not upstream execution evidence.
