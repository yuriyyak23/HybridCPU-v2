# HybridCPU ISE Memory Accelerators

This directory is the compact current-contract context for descriptor-backed lane6
`DmaStreamCompute`. It separates implemented carrier behavior, explicit
runtime-side helper APIs, token commit semantics, fail-closed surfaces, and future
architecture.

## Files

- `01_Current_Contract.md` - live semantic contract, hard constraints, and relevant code surfaces.
- `02_Phase_Evidence_Ledger.md` - closed phase ledger and latest validation evidence.
- `03_Validation_And_Rollback.md` - validation commands, delta classification, and rollback rules.
- `04_Continuation_Prompt_Phase12.md` - archived prompt used for Phase 12 closure.
- `05_Phase_03_DMA_Execution_Model_ADR.md` - accepted Phase 3 decision for the
  lane6 execution model.

## Current Status

As of 2026-04-30, after Ex1 Phase00-Phase13 closure:

- The lane6 carrier is implemented as descriptor/decode evidence only.
- Phase 3 and Ex1 Phase02 keep Option A as the current contract: the lane6
  carrier stays fail-closed unless a later ADR, implementation, tests, and
  Phase12 documentation migration close the executable gate.
- `DmaStreamComputeMicroOp.Execute(...)` is disabled and must fail closed.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` is `false`.
- `DmaStreamComputeRuntime.ExecuteToCommitPending(...)` is an explicit
  runtime helper, not canonical micro-op execution.
- Runtime/helper memory is exact physical main memory via
  `TryReadPhysicalRange` and `TryWritePhysicalRange`.
- DSC2 is a parser-only/model-only descriptor foundation. Parser acceptance,
  capability grants, and normalized footprints do not authorize token issue,
  memory publication, or compiler/backend production lowering.
- No executable DMA queue, async overlap, global fence, cache coherency, or
  pause/resume/cancel/reset DSC ISA surface is current behavior.
- Phase12 is the conformance/documentation migration gate. Phase13 is a
  planning/dependency gate only and does not approve implementation.

## Source Of Truth

- Live code is authoritative for implementation behavior.
- `Documentation/AsmAppTestResults.md` is the runtime harness comparison baseline.
- Telemetry, replay evidence, certificates, and tokens are evidence surfaces only, not authority.
- Ex1 traceability is under `Documentation/Refactoring/Phases Ex1/`, with
  Phase12 guarding documentation migration and Phase13 guarding dependency
  order/non-inversion.
- This directory intentionally excludes old external-analysis drafts, broken prompts, and stale
  "next task" wording.

## Non-Negotiable Boundaries

- `W=8`: lanes 0-3 ALU, 4-5 LSU, lane6 DMA/stream, lane7 branch/system.
- Active frontend is native VLIW only.
- `DmaStreamCompute` is the lane6 descriptor carrier plus explicit helper/token
  model, not a custom accelerator and not executable lane6 DMA.
- No pipeline token allocation or production executable DSC lowering is approved
  for the current contract.
- Helper/runtime tokens, retire-style observations, progress diagnostics,
  backend infrastructure, cache/conflict observers, and compiler sideband
  transport are downstream evidence only. They must not satisfy upstream
  executable DSC, DSC2 execution, async overlap, IOMMU-backed execution,
  coherent DMA/cache, or production lowering gates.
- Descriptor payload is typed sideband metadata; raw scalar fields and reserved bits are not ABI.
- `VLIW_Instruction` remains four 64-bit words; bundles remain eight 32-byte slots.
- Owner/domain guards precede descriptor acceptance, replay/certificate reuse,
  helper admission, commit, and exception publication.
- Raw `VirtualThreadId` hints are not owner/domain authority.
- `CustomAcceleratorMicroOp`, registry accelerator contours, MatMul fixture, and accelerator DMA
  seams remain fail-closed until a full carrier contract exists.
- No scalar, ALU, vector, `GenericMicroOp`, telemetry, replay-token, or silent no-op fallback may
  make `DmaStreamCompute` succeed.
- Public enum values and reject reasons are append-only.

## Audit Status

| Audit ID | Status |
| --- | --- |
| AUDIT-002 | Still valid: lane6 direct execution is fail-closed. |
| AUDIT-005 | Still valid: runtime helper is separate from StreamEngine and DMAController. |
| AUDIT-007 | Still valid: runtime/helper uses physical main memory. |
| AUDIT-009 | Future design decision: no DSC pause/resume/cancel/reset/fence ISA surface. |
| AUDIT-011 | Still valid: StreamEngine, DMAController, DSC token lifecycle, and L7 models remain separate. |
| AUDIT-012 | Resolved in current WhiteBook paths; stale roots may appear only as `Stale only` examples. |
