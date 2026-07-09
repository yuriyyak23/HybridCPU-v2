# 09 — StreamEngineVector scoped production candidate

## Goal

Introduce a tightly scoped production candidate for `ExecutionContourKind.StreamEngineVector`, limited to direct VLOAD/VSTORE vector-transfer carrier packages, and only after scalar/load-store/branch production foundations are proven.

This phase must not convert the whole vector/stream surface into production lowering. The existing vector-transfer lowerer currently emits helper/transport ABI carrier candidates; that remains the starting point.

## Scope allowed

Allowed candidate subset:

- `VLOAD` direct 1D transfer;
- `VSTORE` direct 1D transfer;
- explicit non-empty element count;
- explicit non-zero stride;
- no indexed addressing;
- no 2D addressing;
- no scalar/vector dot fallback;
- no base memory/vector fallback;
- no widening/FMA fallback;
- no transpose/segment fallback.

## What is produced

A scoped stream/vector production provider may produce:

- VLOAD/VSTORE carrier words/bytes;
- transfer shape facts;
- source/destination address structural facts;
- no-fallback proof;
- evidence and telemetry;
- runtime-authority-pending header.

It must not produce:

- memory publication;
- completed vector transfer execution;
- commit or retire;
- scalar fallback;
- stream fallback from MatrixTile;
- general vector production lowering.

## Required gates

- Explicit `StreamEngineVector.DirectTransferProduction` gate.
- Intent classifier distinguishes direct transfer from general vector stream.
- Existing helper ABI golden artifacts exist.
- Production package parity matches helper carrier for the allowed subset.
- ISE decode/encode parity exists.
- All unsupported shapes fail closed.
- Runtime Legality A/B, execution, publication, commit, and retire remain pending.

## Tests

### Positive

- VLOAD contiguous transfer package golden snapshot;
- VSTORE contiguous transfer package golden snapshot;
- helper carrier vs production package carrier shadow compare;
- no-fallback proof records direct-transfer-only scope;
- telemetry records unsupported alternatives as excluded.

### Negative

- zero `StreamLength` rejects;
- zero `Stride` rejects;
- indexed addressing rejects;
- 2D addressing rejects;
- vector ALU opcodes reject;
- MatrixTile helper opcodes reject;
- DSC/L7 descriptors reject;
- missing gate rejects.

## Migration risk

The largest risk is accidentally treating `CompilerVectorTransferEmissionLowerer.LowerWithDecision` as production lowering. It is currently helper/transport ABI. The production candidate must wrap and shadow-compare it, not reclassify it silently.

## Rollback

Disable the direct-transfer production gate. Existing helper API remains helper-only.

## Merge gate

- Only direct VLOAD/VSTORE transfer subset enabled.
- No general vector production claim.
- Helper success remains separate from production gate success.
