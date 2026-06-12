# Phase 09B Matrix/Tile Negative Golden Manifest

Date: 2026-06-07

## Scope

This manifest covers `MTILE_LOAD`, `MTILE_STORE`, `MTILE_MACC`, and
`MTRANSPOSE`.

## Decision

No executable matrix/tile golden artifacts are published for these rows in the
current runtime/ISA model.

The artifact for this iteration is a negative golden manifest only. It records
that positive execution vectors are blocked until the runtime/ISA package has
closed decoder/encoder, IR projection, materializer, typed tile MicroOp,
scheduler lane, execute/capture, retire, replay/rollback, and golden vector
authority. tile state/descriptor ABI, accumulator/transpose ABI, and
descriptor-only VLM rows are already closed runtime/ISA prerequisites.

## Required Before Positive Golden Publication

- architectural tile state owner
- canonical tile descriptor ABI
- memory shape and partial-fault model
- accumulator tile ABI for `MTILE_MACC`
- transpose policy ABI for `MTRANSPOSE`
- runtime-owned VLM rows, closed as descriptor-only legality rows
- decoder/encoder ABI
- tile-aware `InstructionIR` projection
- registry/materializer authority
- typed tile MicroOp
- scheduler lane binding
- execute/capture semantics
- retire-owned staged publication or commit
- replay/rollback conformance
- executable golden vectors

## Non-Authority Sources

The following are not positive golden authority for `MTILE_*` or `MTRANSPOSE`:

- enum or numeric opcode presence
- optional-disabled status/catalog rows
- `InstructionClassifier` memory/scalar class metadata
- canonical decoder rejection messages
- Lane6 `DSC2` tiled shape descriptors
- Lane7 MatMul/accelerator descriptors
- vector transpose execution evidence
- memory effective-address fallback behavior
- compiler no-emission or helper contracts
