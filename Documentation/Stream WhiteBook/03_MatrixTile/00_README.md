# MatrixTile Execution Plane

Status: `closed/phase19-compiler-sideband-conformance`

MatrixTile is a closed runtime/ISA execution plane with four canonical
operations:

| Operation | Semantic and runtime contour | Architectural result |
| --- | --- | --- |
| `MTILE_LOAD` | `MatrixTileMemory`; `MatrixTileStreamClass`, lane6 | tile image at retire |
| `MTILE_STORE` | `MatrixTileMemory`; `MatrixTileStreamClass`, lane6 | all-or-none memory commit at retire |
| `MTILE_MACC` | `MatrixTileCompute` | accumulator image at retire |
| `MTRANSPOSE` | `MatrixTileCompute` | destination tile image at retire |

`MatrixTileArchitecturalTileRegisterFile` owns guest architectural tile state.
It is distinct from StreamEngine and SRF: those are typed transport, window,
and staging substrate, never architectural tile authority. The tile register
file is keyed by owner thread and tile identifier and accepts only canonical
tile descriptors and packed images.

## Execution model

All four operations materialize typed MatrixTile runtime objects. Execute
captures validated inputs, staged results, or a typed fault; the capture is
architecturally invisible. Retire is the sole authority for tile publication,
accumulator publication, transpose publication, and store commit. A cancelled,
faulted, stale, duplicate, or mismatched capture publishes nothing.

Replay and rollback use core-owned checkpoints. Their identity binds the core,
owner, opcode and operation, canonical descriptors, resource contour, typed
transfer where applicable, numeric/layout policy, replay epoch, dependency
fingerprint, and publication surface. They cannot use SRF state, host evidence,
or compiler metadata as architectural authority.

## Numeric and layout contract

`MTILE_MACC` uses versioned runtime-owned `MatrixTileNumericPolicy` and
`MatrixTileLayoutPolicy`; `MTRANSPOSE` uses its versioned runtime-owned layout
policy. The policies are fingerprinted and validated by runtime projection,
materialization, execute capture, retire, replay, and rollback. Missing,
tampered, unsupported, or operation-mismatched policy sidebands fail closed
before arithmetic side effects.

The machine-readable corpus is
[`Golden/matrix_tile_numeric_layout_golden_v1.json`](Golden/matrix_tile_numeric_layout_golden_v1.json).
It is ABI-version-bound and is exercised through the production runtime path;
it contains positive results and negative policy, identity, and fault cases.

## Compiler boundary

Positive compiler emission is a downstream transport consumer of the runtime
contract. `MTILE_MACC` carries explicit runtime-owned numeric and layout
sidebands; `MTRANSPOSE` carries a layout sideband and no MACC numeric sideband;
`MTILE_LOAD` and `MTILE_STORE` create no compute numeric/layout authority.
The sidebands survive source and lowered `InstructionSlotMetadata`, then are
revalidated by runtime projection/materialization. Compiler metadata neither
defines arithmetic or layout legality nor opens, closes, or overrides a runtime
gate.

Compiler-produced lowered bundle annotations are a control-plane ingress, not
a MatrixTile data-plane. Their transport path is:

```text
LoweredBundleAnnotations
-> EmitProgram / EmitVliwBundleImage
-> MainMemory VLIW annotation store
-> L2 VLIW cache annotation carrier
-> L1 VLIW cache annotation carrier
-> pipeIF.BundleAnnotations
-> production decode
-> MatrixTile projection/materialization
-> typed MatrixTileMicroOp
```

For `MTILE_LOAD` and `MTILE_STORE`, tile bytes still move through the typed
MatrixTile lane6/SRF data-plane described in
[`02_Tile_Stream_Transport.md`](02_Tile_Stream_Transport.md). Lane6 is a
shared physical carrier, not proof that MatrixTile memory is
`DmaStreamCompute` authority. The architectural tile register file is updated
only by retire publication.

MatrixTile is not ordinary LSU execution, a generic ALU/vector operation,
`DmaStreamCompute`, DSC, Lane7, VMX, assist, generic StreamEngine authority, or
an external-backend fallback. No separate MatrixTile Phase 20 is open.
