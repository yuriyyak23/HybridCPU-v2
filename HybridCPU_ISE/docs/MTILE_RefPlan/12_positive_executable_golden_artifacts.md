# Phase 12 - Positive Executable Golden Artifacts

Status: closed/runtime-isa

The positive executable golden artifacts cover:

- legal decode/encode round-trip;
- IR/materializer;
- execute/retire;
- memory fault;
- descriptor fault;
- replay/rollback;
- operation-specific resource class and placement;
- typed MatrixTile StreamEngine/SRF transport.

The runtime no-fallback/no-hidden-lowering audit permits typed MTILE MicroOps,
dedicated MTILE lane6 transport, bounded typed StreamEngine/SRF adapters, and
the existing retire/replay ABIs.

It rejects ordinary LSU MicroOps, common ALU placement for all MTILE,
`DmaStreamCompute`, generic StreamEngine execution authority, scalar/vector/dot
fallback, Lane7, VMX, external backend substitution, aliases, and hidden
lowering.

`ClosesGoldenArtifacts = true` is revalidated after Phase 14.
Compiler scope remains closed.

## Numeric Golden Reopening

The Phase 12 artifacts remain valid for decode, resource placement, typed
transport, retire-only visibility, fault, and rollback behavior. They are not
a normative numeric corpus for all MACC profiles.

Phase 18 must add machine-readable vectors that bind descriptor, layout,
numeric policy, inputs, accumulator snapshot, staged result, retire result,
replay identity, and expected fail-closed cases. Numeric-sensitive
`ClosesGoldenArtifacts` must be treated as false until that corpus and its
runtime loader tests close.
