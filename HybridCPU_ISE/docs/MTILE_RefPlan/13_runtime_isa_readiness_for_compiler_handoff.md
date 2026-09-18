# Phase 13 - Runtime/ISA Package And Compiler Handoff

Status: closed/phase14-revalidated

The positive status/catalog promotion is closed:

- all four mnemonics are `OptionalEnabled`;
- runtime evidence is `ConformanceTested`;
- executable runtime authority is present;
- runtime-owned legality remains final ISA authority.

The positive compiler emission handoff package publishes opcode, runtime status,
publication kind, `MatrixTileMemory`/`MatrixTileCompute`, required slot class,
and physical lane mask.

The current compiler implementation already exists in this checkout.
The older compiler-visible no-emission boundary is superseded.
No compiler code is edited by Phase 13 or Phase 14.

Compiler helpers and emission remain compiler-owned downstream consumers. They
cannot override runtime legality, reinterpret MTILE memory as ordinary LSU, or
lower MTILE through DSC, VMX, scalar/vector/dot, Lane7, or external backend
fallback.

Phase 14 revalidated resource placement, golden evidence, package readiness,
and compiler handoff metadata for the pre-numeric-policy package.

## Post-Phase14 Numeric Reopening And Phase19 Reclosure

The last sentence above is retained as the Phase 14 closure record and is
superseded for numeric-sensitive readiness by Phases 15-19.

Phase 19 reclosed the numeric-sensitive compiler handoff:

- explicit numeric-policy package readiness is true;
- numeric-sensitive golden closure is true;
- compiler emission remains downstream evidence, not runtime numeric support
  authority;
- the compiler path carries runtime-owned numeric/layout policy sidebands into
  source and lowered `InstructionSlotMetadata`;
- unsupported, absent, tampered, or operation-mismatched policy still fails
  closed in runtime projection/materialization;
- runtime rejection cannot trigger vector, DSC, Lane7, VMX, assist, or backend
  fallback.

Load/store resource placement and retire authority remain closed. Transpose
state/publication semantics remain closed, subject to the Phase 16 layout
policy separation, Phase 18 layout golden revalidation, and Phase 19 compiler
sideband conformance.
