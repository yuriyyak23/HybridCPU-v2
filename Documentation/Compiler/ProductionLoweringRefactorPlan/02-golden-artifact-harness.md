# 02 — Positive golden artifact harness foundation

## Goal

Create a positive golden artifact harness before introducing production providers. The harness proves that a compiler-produced package is byte/word-stable, contour-scoped, and authority-bounded.

This phase should add test infrastructure and initial baselines only. It should not migrate callers.

## Artifact classes to snapshot

Production-lowering tests must snapshot the package as separated envelopes, not as a monolithic execution-ready object:

- carrier words/bytes;
- sideband envelope;
- descriptor envelope;
- typed-slot facts envelope;
- runtime bridge envelope, if present;
- evidence envelope;
- telemetry snapshot;
- no-fallback proof;
- required runtime authority map.

## Golden manifest fields

Each golden artifact entry should include:

```text
artifact_id
source_test
contour
intent_kind
opcode_or_opcode_family
producer_surface
production_gate_id
carrier_words_or_bytes_hash
descriptor_hash
sideband_hash
typed_slot_fact_hash
evidence_hash
no_fallback_proof_id
runtime_authority_dependency
explicit_non_claims
ise_decode_parity_status
```

The `explicit_non_claims` field should state at minimum:

```text
not execution
not publication
not commit
not retire
not final runtime legality
```

## First positive baselines

Start with golden artifacts for surfaces that already have stable compatibility/helper emission, but do not yet label them production:

1. Scalar VLIW candidate carrier, if a minimal scalar builder exists.
2. VLOAD/VSTORE direct vector-transfer helper carrier.
3. MatrixTile helper carrier and optional policy sideband, marked helper-only.
4. DSC lane6 descriptor-backed carrier package, marked compatibility/descriptor-backed.
5. L7 ACCEL_SUBMIT carrier plus descriptor sideband, marked compatibility/descriptor-backed.

## Negative baselines

Golden tests must include fail-closed cases:

- unknown contour;
- cross-contour provider analysis;
- descriptorless L7 submit;
- DSC descriptor with missing owner/domain guard;
- VectorTransfer zero length or zero stride;
- VectorTransfer indexed/2D addressing through direct helper;
- MatrixTile missing runtime-owned numeric/layout policy where required;
- VMX emission attempt;
- SecureCompute emission attempt.

## File areas likely touched

- `HybridCPU_ISE.Tests/CompilerTests/*Golden*Tests.cs`
- `HybridCPU_ISE.Tests/TestData/CompilerGoldenArtifacts/`
- optional golden manifest helper under `HybridCPU_ISE.Tests/TestHelpers/`

## Merge gate

- Baselines are explicit and reviewed.
- No production provider is introduced yet.
- Every positive artifact still records runtime authority pending.
- Every negative case remains fail-closed.

## Rollback

Remove the golden artifact test data and harness. No runtime ABI or compiler source behavior changes are involved in this phase.
