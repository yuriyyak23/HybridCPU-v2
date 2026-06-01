# Non-VMX Iteration 04B Vector, Lane6, and Lane7 Deferred Templates Closure

Date: 2026-05-26

Scope: CloseToRTL template/no-emission boundary only. This snapshot does not claim executable instruction, descriptor, queue, maintenance, or control-plane support.

## Closed as template boundaries

- Vector predicate/select: `VMERGE`, `VSELECT`, `VFIRST`, `VANY`, `VALL`, `VMSIF`, `VMSOF`.
- Vector widen/narrow/convert: `VWADD`, `VWADDU`, `VWSUB`, `VWSUBU`, `VWMUL`, `VWMULU`, `VWMACC`, `VNSRL`, `VNSRA`, `VSEXT`, `VCVT.I`, `VCVT.U`, `VCVT.F`.
- Vector segment/structure/memory contours: `VLDSEG2/4/8`, `VSTSEG2/4/8`, 2D `VLOAD`/`VSTORE`, indexed+2D `VGATHER`/`VSCATTER`, `VZIP`, `VUNZIP`, `VINTERLEAVE`, `VDEINTERLEAVE`.
- Vector fixed-point/saturating and scan: `VSUB.SAT`, `VMUL.SAT`, `VSLL.SAT`, `VSRL.SAT`, `VSRA.SAT`, `VAVG`, `VAVG.R`, `VCLIP`, `VSCAN.MIN`, `VSCAN.MAX`.
- Dot/matrix deferral: `VDOT.BLOCKSCALE`, `VDOT.ACCUM`, `VDOT.WIDE.I16`, `VDOT.WIDE.I32`, `MTILE_LOAD`, `MTILE_STORE`, `MTILE_MACC`, `MTRANSPOSE`.
- Lane6 descriptor/shape/queue/query/carrier contours, including descriptor-owned `DmaStreamCompute.*`, shape expansion, queue lifecycle, query, and `DSC2`.
- Lane7 counters, hints, translation/cache/IOMMU maintenance, and accelerator topology/lifecycle/queue commands.

## Evidence state

- Opcode/decoder/encoder ABI: not allocated by this iteration.
- `VectorLegalityMatrix`: unchanged; vector contours remain fail-closed unless already closed by prior runtime evidence.
- Lane6 descriptor op-types: not promoted to scalar opcodes.
- Lane7 control-plane rows: no execution authority, no host-evidence publication.
- CloseToRTL object: aggregate partial metadata only; no `Opcode`, no `Execute`, no side-effect or writeback authority.
- Compiler boundary: helper surface remains no-emission.

## Deferred work

Execution closure for these contours still requires catalog/status evidence, opcode or descriptor op-type allocation where appropriate, decoder/encoder ABI, IR/projection, materializer, typed MicroOp, execute/capture semantics, retire-owned publication, replay/rollback/conformance tests, and golden artifacts.
