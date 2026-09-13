# DmaStreamCompute Evidence Ledger

Status: closed for the bounded DSC1 contour

Closed evidence:

- typed descriptor sideband and parser validation;
- lane6 `DmaStreamClass` placement;
- materialized MicroOp execution;
- owner/domain guard;
- normalized read/write footprint;
- issue-owned token lifecycle;
- staged all-or-none commit and rollback;
- precise retire fault publication;
- replay identity and invalidation;
- pressure, quota, and telemetry;
- compiler carrier emission without fallback.

Open architecture, not current behavior:

- executable DSC2;
- queue and asynchronous overlap;
- coherent DMA/cache;
- IOMMU-backed DSC execution;
- successful partial completion;
- new lifecycle ISA controls.

Evidence is scoped. Parser, helper, backend, token, telemetry, or compiler
surfaces cannot independently promote an open architecture item.
