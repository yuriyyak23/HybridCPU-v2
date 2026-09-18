# Phase 15 - Numeric Policy ABI And Supported Profiles

Status: closed/runtime-owned-numeric-policy-abi

Review date: 2026-06-13

## Objective

Replace implicit MACC arithmetic defaults with one runtime-owned,
versioned, fail-closed `MatrixTileNumericPolicy` identity.

## Required ABI

The policy must explicitly bind:

- source element type;
- accumulator type and publish format;
- signedness;
- widening rule;
- multiply rule;
- add rule;
- rounding mode;
- saturation mode;
- overflow mode;
- NaN policy;
- infinity policy;
- denormal policy;
- reproducibility mode;
- exception policy;
- ABI version and stable fingerprint.

No field may be inferred from compiler metadata, host behavior, CLR defaults,
descriptor byte width alone, or an existing accumulator buffer. Missing,
unknown, reserved, contradictory, or unsupported combinations fail closed
before arithmetic starts.

## Closed Decision

`MatrixTileNumericPolicyAbi` owns ABI version 1 and the stable policy identity.
The policy binds every required field and computes an FNV-1a fingerprint over
the versioned field order. Validation recomputes the fingerprint and rejects
zero, stale, contradictory, reserved, or unsupported identities.

The production runtime carrier is:

```text
InstructionSlotMetadata.MatrixTileNumericPolicy
  -> VectorInstructionPayload.MatrixTileNumericPolicy
  -> MatrixTileInstructionIrProjection
  -> MatrixTileMaccSemanticContract
  -> typed MatrixTile MicroOp
```

Canonical VLIW decode and the registry materializer require the sideband for
`MTILE_MACC`. Raw dtype/width-only carriers fail with `NumericPolicyFault`
before a MicroOp or arithmetic side effect exists.

The three-argument projector retained for older compiler source is a legacy
Phase 14 audit bridge only. Production decode and materialization call the
strict overload. Phase 19 later supplied the compiler-owned sideband bridge, so
numeric-positive handoff is now allowed only when compiler source and lowered
annotations carry the runtime-owned numeric/layout policies and runtime strict
projection accepts them.

## Supported Profile Matrix

The runtime-owned table is:

- signed and unsigned 8-, 16-, 32-, and 64-bit integer sources;
- 32- and 64-bit integer accumulator profiles;
- binary32 and binary64 source/accumulator profiles;
- exact accumulator byte width and retire publication encoding.

The table states which combinations execute and which remain outside the
runtime ABI. A policy vocabulary entry still does not imply execution support.
Custom and low-bit quantized formats remain outside this plan.

| Profile | Source | Accumulator/publish | Runtime execution | Decision |
| --- | --- | --- | --- | --- |
| `SignedInt8ToInt32` | signed 8 | signed 32 | supported | exact integer |
| `UnsignedInt8ToUInt32` | unsigned 8 | unsigned 32 | supported | exact integer |
| `SignedInt16ToInt32` | signed 16 | signed 32 | supported | exact integer |
| `UnsignedInt16ToUInt32` | unsigned 16 | unsigned 32 | supported | exact integer |
| `SignedInt32ToInt64` | signed 32 | signed 64 | supported | exact integer |
| `UnsignedInt32ToUInt64` | unsigned 32 | unsigned 64 | supported | exact integer |
| `SignedInt64ToInt64` | signed 64 | signed 64 | supported | final encoding trap |
| `UnsignedInt64ToUInt64` | unsigned 64 | unsigned 64 | supported | final encoding trap |
| `Binary32ToBinary32` | binary32 | binary32 | supported | Phase 16 software IEEE-754 |
| `Binary64ToBinary64` | binary64 | binary64 | supported | Phase 16 software IEEE-754 |

`FLOAT16`, `BFLOAT16`, float8, custom, mixed-sign, mixed-width, and quantized
profiles are not entries in the supported matrix and fail closed.

## Historical Phase 16 Handoff

Phase 15 closed the numeric-policy ABI and integer profile decisions. Floating
multiply/add semantics were intentionally left to Phase 16 and are no longer an
open item in the current plan: Phase 16 ratified binary32 and binary64 as
software IEEE-754, round-to-nearest-ties-to-even, separately rounded profiles.

## Ratified Phase 15 Integer Decisions

- multiply uses an exact mathematical integer product;
- add uses an exact unbounded intermediate in deterministic ascending K order;
- accumulator overflow is tested at final accumulator encoding;
- saturation is disabled; no saturating profile is supported;
- integer rounding is explicitly `NotApplicableExactInteger`;
- NaN, infinity, and denormal policy are explicitly `NotApplicable`;
- overflow becomes typed arithmetic-fault capture;
- packed source, accumulator, and publish encoding is canonical little-endian;
- integer reproducibility is independent of CLR checked context and host FPU
  state.

Binary32 and binary64 are executable profiles after Phase 16. Float16,
bfloat16, float8, custom, mixed-sign, mixed-width, and quantized profiles
remain unsupported and fail closed.

## Integration

Update descriptor/sideband, decoder validation, IR projection, materializer,
typed MicroOp metadata, capture records, retire validation, replay identity,
runtime package contract, status ledger, and handoff package only where the
explicit policy must be carried or checked.

Do not change tile-stream placement, MatrixTile architectural ownership, or
retire-only publication.

## Evidence

- full policy round-trip and stable fingerprint tests;
- supported-profile positive matrix;
- missing/reserved/contradictory policy negative matrix;
- descriptor/policy dtype and width mismatch rejection;
- no implicit default tests;
- no host floating-mode dependency;
- no compiler, VectorALU, DSC, Lane7, VMX, assist, or backend authority.

Production evidence:

- `Phase15MatrixTileNumericPolicyAbiTests` covers the exact profile table,
  policy round-trip, stable fingerprint, missing/reserved/unsupported/tampered
  identities, dtype mismatch, strict runtime materialization, and fail-closed
  package/handoff readiness;
- Phase 09-14 MatrixTile regressions pass with explicit policy annotations for
  MACC fixtures;
- `git diff --name-only -- HybridCPU_Compiler` was empty for this Phase 15
  runtime-only closure; Phase 19 later intentionally changed compiler transport
  code to carry runtime-owned sidebands.

## Exit Criteria

Phase 15 is closed for policy ABI ownership. Every executable MACC profile has
one explicit runtime policy and every other combination deterministically fails
closed. Later phases promoted binary32/binary64 arithmetic and carried policy
identity through capture, retire, replay, corpus, compiler sideband transport,
and package audit.
