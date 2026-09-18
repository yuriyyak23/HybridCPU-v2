# Phase 18 - Numeric And Layout Golden Corpus

Status: closed/machine-readable-numeric-layout-golden-corpus

Review date: 2026-06-17

## Objective

Make numeric and layout vectors part of the executable specification rather
than test-local constants.

## Canonical Location

Create a versioned corpus under:

`Documentation/Stream WhiteBook/03_MatrixTile/Golden/`

Minimum artifacts:

- signed 8-bit source with 32-bit accumulator MACC;
- signed 16-bit source with 32-bit accumulator MACC;
- unsigned boundary and overflow cases;
- binary32 MACC using the ratified rounding profile;
- binary64 MACC using the ratified rounding profile;
- basic transpose layout permutation;
- transpose alias rejection;
- unsupported policy and descriptor/policy mismatch cases.

## Artifact Schema

Every vector must carry:

- schema and numeric-policy ABI versions;
- operation and descriptor identities;
- source and destination layout policies;
- input tile images;
- accumulator image before execute;
- explicit numeric policy;
- expected staged result or typed execute fault;
- expected retire result or typed retire rejection;
- expected replay fingerprint and epoch;
- wrong-owner, wrong-dtype, wrong-policy, and wrong-resource outcomes.

## Loader And Determinism

Runtime tests must parse the artifacts through a typed schema, execute the
normal decode/materialize/MicroOp/capture/retire path, and compare byte-exact
results. The corpus must not call private arithmetic helpers as an alternate
execution path.

Regeneration must be deterministic, reviewed, and independent of compiler
output or host matrix libraries.

## Closure Evidence

Corpus:

`Documentation/Stream WhiteBook/03_MatrixTile/Golden/matrix_tile_numeric_layout_golden_v1.json`

The v1 corpus carries schema, numeric-policy ABI, layout-policy ABI, operation,
descriptor-shape inputs, layout/numeric policy profile, tile images,
accumulator-before image, expected staged result or typed fault, retire
publication/fault result, replay epoch, policy identity fingerprint, and
declared wrong-owner/wrong-dtype/wrong-policy/wrong-resource outcomes.

Included vectors:

- signed 8-bit source with 32-bit accumulator MACC;
- signed 16-bit source with 32-bit accumulator MACC;
- signed 32-bit source with 64-bit accumulator MACC;
- unsigned 8-bit boundary MACC;
- unsigned 64-bit overflow typed fault;
- binary32 separately rounded MACC;
- binary64 MACC;
- basic transpose layout permutation;
- transpose non-square in-place alias rejection;
- dtype/numeric-policy mismatch rejection.

Loader:

`HybridCPU_ISE.Tests/tests/Phase18MatrixTileNumericLayoutGoldenCorpusTests.cs`

The loader parses JSON and runs the normal runtime path through
`DecoderContext`, projection/materialization, typed `MatrixTileMicroOp`,
execute capture, retire, rollback, and replay. It does not call private
arithmetic helpers or compiler output as an oracle.

Verification:

```text
dotnet test HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --no-restore --filter "FullyQualifiedName~Phase18MatrixTileNumericLayoutGoldenCorpusTests" -v:minimal
Passed: 5/5
```

Package note: Phase 19 reclosed numeric-sensitive package readiness and
compiler handoff after proving that the compiler-owned MTILE path carries
runtime-owned numeric/layout policy sidebands into source and lowered bundle
annotations.

## Exit Criteria

Phase 18 closes only when all artifacts pass on the production runtime path and
negative vectors fail for the declared reason. Numeric-sensitive package
readiness and `ClosesGoldenArtifacts` are restored only by Phase 19 package
reclosure.

Closure result: closed for corpus and production-path loader evidence. Phase
19 subsequently closed package reclosure and compiler sideband conformance.
