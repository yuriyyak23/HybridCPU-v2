# DmaStreamCompute Current Contract

## Implemented Contour

`DmaStreamComputeMicroOp` is a typed lane6 carrier using
`SlotClass.DmaStreamClass`.

`DmaStreamComputeMicroOp.Execute(ref Processor.CPU_Core core)` enters
`DmaStreamComputeRuntime.ExecuteMaterializedMicroOpToCommitPending(...)` for the
guard-accepted DSC1 contour.
`DmaStreamComputeRuntime` is an explicit runtime helper; the direct
orchestration/test seam remains `ExecuteToCommitPending(...)`.

Compiler helpers emit the canonical carrier and typed descriptor sideband.
Runtime descriptor validation, owner/domain guard, placement, token allocation,
and commit remain final authority.

## DSC1

- magic `DSC1`, ABI version 1;
- operations Copy, Add, Mul, Fma, Reduce;
- integer 8-64 and float 32/64 element types;
- contiguous or fixed-reduce shape;
- exact inline ranges;
- `AllOrNone` completion;
- explicit owner VT/context/core/pod/device/domain;
- non-zero, aligned, non-overflowing ranges.

Unsupported or dirty fields fail closed.

## Execution And Commit

The runtime:

1. validates descriptor, guard, owner/domain, placement, and pressure;
2. allocates an issue-owned token;
3. reads exact physical ranges through the DSC backend;
4. computes into token-owned staged buffers;
5. enters `CommitPending`;
6. commits only after fresh guard and exact coverage validation.

Partial physical write failure restores destination checkpoints. Token,
certificate, replay evidence, or telemetry cannot substitute for guard
authority.

## Replay

Replay binds descriptor, carrier, certificate input, footprint, owner/domain,
token lifecycle, lane placement, and envelope hash. Missing payload,
wrong-owner, wrong-lane, stale token, or incomplete evidence rejects reuse.

## Explicit Non-Features

- DSC2 is parser/capability/footprint foundation only;
- no queue or architectural async overlap;
- no pause/resume/reset/fence DSC ISA lifecycle;
- no coherent DMA/cache claim;
- no successful partial completion;
- no StreamEngine, VectorALU, DMAController, MatrixTile, assist, or external
  accelerator fallback.

The helper drives current work synchronously: no current async DMA overlap.

## Authority Separation

`DmaStreamCompute` is not a custom accelerator and is not MTILE. Lane6 sharing
is a physical conflict fact only.

Current anchor:
`Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`.
