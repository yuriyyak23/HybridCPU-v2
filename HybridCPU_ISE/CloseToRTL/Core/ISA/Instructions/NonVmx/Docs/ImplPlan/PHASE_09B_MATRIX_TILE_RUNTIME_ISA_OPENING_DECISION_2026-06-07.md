# Phase 09B Matrix/Tile Runtime ISA Opening Decision

Date: 2026-06-07

## Decision

The requested executable runtime/ISA opening for `MTILE_LOAD`, `MTILE_STORE`,
`MTILE_MACC`, and `MTRANSPOSE` is blocked in the current evidence model.

This is an explicit no-opening decision for this iteration, not executable
closure. The rows remain optional-disabled declared matrix rows.

## Requested Gates

The following gates are not closed by the current plan or source evidence:

- tile execution model
- tile descriptor ABI
- memory-shape/fault model
- accumulator tile ABI
- transpose policy ABI
- VLM closure
- decoder/encoder ABI
- IR projection
- materializer
- typed tile MicroOp
- scheduler lane binding
- execute/capture
- retire
- replay/rollback
- golden artifacts

## Evidence Review

The manifest marks all four rows as `success/optional-disabled` with
`open(runtime): decoder/runtime intentionally fail-closed`.

Phase 09B states that existing numeric enum/opcode constants are retained only
as prior declared optional-disabled surface and are not decoder, registry,
materializer, execution, retire, replay, helper, VMX, Lane6, Lane7, or
external-backend authority.

The current decoder intentionally rejects the matrix/tile opcodes before IR and
runtime publication. Leaf instruction files carry `IsExecutable=false`,
`CompilerHelperAllowed=false`, and `Phase09NegativeDecisionGate` markers.

## Blocked Production Path

Do not open any of the following without a new production package that closes
the full evidence chain in one package:

- opcode/contour authority beyond declared optional-disabled enum constants
- decoder/encoder ABI
- `InstructionIR` projection
- registry/materializer rows
- typed tile MicroOps
- scheduler/lane binding
- tile memory execution or tile MACC/transpose execution
- retire-owned staged publication or staged commit
- replay/rollback and conformance tests
- golden artifacts
- VMX-specific projection
- Lane6, Lane7, external backend, descriptor, scalar, vector, or multi-op fallback

## First Gate Pool Review

The first requested gate pool is blocked:

- tile execution model: blocked by absence of an architectural tile register
  file, absence of an explicit memory-backed tile state model, absence of tile
  lifetime/ownership policy, and absence of retire-owned tile publication plus
  replay/rollback state policy.
- tile descriptor ABI: blocked by absence of canonical tile identity, shape,
  element type, lifetime, and descriptor encoding carrier for the `MTILE_*`
  instructions.
- memory/fault model: blocked by absence of matrix/tile memory alignment,
  partial-fault, staged publication/commit, fault replay ordering, and golden
  fault vectors.

Existing Lane6 `DSC2` Tile2D parser evidence and Lane7 MatMul descriptor
evidence are external/descriptor evidence only. They do not authorize `MTILE_*`
decoder opening, tile state, typed MicroOps, execution, retire, replay, or VMX
paths.

## Runtime Pipeline Gate Pool Review

The next requested gate pool is also blocked before executable closure:

- decoder/encoder: canonical decode intentionally fails closed for optional
  matrix and matrix-memory contours; no encoder ABI is opened by enum/opcode
  constants.
- IR projection: there is no matrix/tile `InstructionIR` carrier that owns
  tile identity, shape, element type, accumulator footprint, transpose policy,
  fault shape, or retire publication semantics.
- materializer: there is no registry factory or materializer that can turn the
  declared rows into typed runtime objects.
- typed tile MicroOp and scheduler lane: leaf files require typed tile MicroOps
  and lane binding but explicitly publish none.
- execute/capture: memory EA fallback for retained opcodes is not tile
  execution authority, and no MACC/transpose capture semantics exist.
- retire: there is no retire-owned staged tile publication or staged store
  commit contract.
- replay/rollback and golden artifacts: no tile state rollback, fault replay
  ordering, conformance vectors, or golden artifacts exist for these rows.

This closes the current runtime/ISA package iteration as a negative package
gate: the pipeline contour remains reserved/optional-disabled until a future
ADR or production package supplies the full chain from DEC through GLD.

## Semantic ABI And VLM Gate Pool Review

The accumulator, transpose, and VLM gate pool remains blocked:

- accumulator tile ABI: `MTILE_MACC` has no architectural accumulator tile
  owner, accumulator footprint ABI, dtype promotion policy, shape compatibility
  policy, exception/saturation policy, retire publication policy, or
  replay/rollback policy.
- transpose policy ABI: `MTRANSPOSE` has no policy carrier for source and
  destination tile identity, aliasing, in-place transpose, layout/permutation,
  staged publication, fault replay, or golden vectors.
- VLM closure: the four matrix/tile rows remain outside the runtime
  `VectorLegalityMatrix`. Existing optional-disabled status and
  `InstructionClassifier` memory/scalar classes are metadata only and do not
  authorize execution, materialization, or compiler emission.

Compiler update readiness is not reached. The runtime/ISA package still lacks
closed ABI authority for tile state, descriptor shape, accumulator semantics,
transpose policy, VLM rows, decoder/encoder, IR, materializer, typed tile
MicroOps, scheduler lane binding, execute/capture, retire, replay/rollback, and
golden artifacts.

## Closure Ledger

The complete requested runtime/ISA closure ledger is recorded as blocked:

- `CAT`: status/catalog promotion remains optional-disabled and declared-only.
- `OP`: retained numeric opcodes are not executable opcode or descriptor
  authority.
- `ABI`: tile state/descriptor ABI and accumulator/transpose ABI are absent.
- `VLM`: matrix/tile rows are not present in runtime-owned VLM rows.
- `DEC`: canonical decoder remains fail-closed and no encoder ABI exists.
- `IR`: no tile-aware `InstructionIR` projection exists.
- `MAT`: no registry/materializer authority exists.
- `UOP`: no typed tile MicroOp exists.
- `SCH`: no scheduler lane binding exists.
- `EXE`: no tile execute/capture semantics exist.
- `RET`: no retire-owned tile publication or store commit exists.
- `RPL`: no replay/rollback conformance exists.
- `GLD`: no executable matrix/tile golden artifacts exist.

This ledger intentionally blocks compiler scope. Compiler updates for these
rows must wait until the runtime/ISA package has positive executable closure
for the full ledger above.

## Status Catalog And Opcode Authority Review

The status/catalog and opcode authority pool is closed as a no-opening audit:

- `MTILE_LOAD`, `MTILE_STORE`, `MTILE_MACC`, and `MTRANSPOSE` remain
  optional-disabled declared rows.
- retained numeric opcode values are metadata only and are not executable opcode
  authority.
- no descriptor authority is opened for the four rows.
- future promotion requires an ADR or production runtime/ISA package that
  decides whether the retained numeric opcode values are executable ISA opcodes
  or whether a separate descriptor carrier owns the contour.

This review does not remove the future production task. Positive
status/catalog promotion and opcode-or-descriptor authority remain open until
the full executable evidence chain closes.

## Remaining Runtime ISA Open Pool

The following runtime/ISA tasks remain open as future production work only.
They are not compiler tasks and they do not authorize compiler updates:

- positive status/catalog promotion
- opcode or descriptor authority
- architectural tile state owner
- canonical tile descriptor ABI
- tile memory shape and fault ABI
- accumulator tile ABI
- transpose policy ABI
- runtime-owned VLM rows
- decoder/encoder ABI
- IR projection and materializer
- typed tile MicroOp and scheduler lane
- execute/capture semantics
- retire publication and commit
- replay/rollback conformance
- positive executable golden artifacts

No remaining task in this pool is closable without a future ADR or production
runtime/ISA package that supplies positive authority for the full evidence
chain. Until then, the rows remain optional-disabled and fail-closed.

## VMX Boundary

No VMX path is opened. If future matrix/tile execution exposes tile state,
host-owned evidence, migration/checkpoint state, DMA/backend authority, or
guest-visible privileged effects, that future package must add the appropriate
generic runtime and virtualization-boundary policy before execution.
