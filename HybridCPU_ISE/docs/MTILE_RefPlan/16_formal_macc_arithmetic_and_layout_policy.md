# Phase 16 - Formal MACC Arithmetic And Layout Policy

Status: closed/formal-arithmetic-and-layout-policy

Review date: 2026-06-14

## Objective

Define `MTILE_MACC` as a deterministic function of descriptor, layout policy,
numeric policy, source snapshots, and accumulator snapshot. Separate layout
interpretation from arithmetic behavior.

## Policy Layers

- `MatrixTileDescriptor`: rows, columns, element encoding, layout identifier,
  and packed footprint.
- `MatrixTileLayoutPolicy`: explicit address interpretation. Current
  executable policies are row-major/ascending-K for `MTILE_MACC` and
  row-major coordinate permutation for `MTRANSPOSE`; column-major, blocked,
  interleaved, missing, tampered, or operation-mismatched policies fail closed.
- `MatrixTileNumericPolicy`: arithmetic behavior from Phase 15.
- `MatrixTilePublicationPolicy`: retire-only and all-or-none publication.
- `MatrixTileReplayPolicy`: identity and invalidation rules.

## Formal MACC Order

For every output coordinate `(i, j)`:

```text
acc = AccumulatorSnapshot(dst, i, j)
for k in the policy-defined deterministic K order:
    a = DecodeElement(A, i, k, descriptorA, layoutA)
    b = DecodeElement(B, k, j, descriptorB, layoutB)
    product = ApplyMultiplyRule(a, b, numericPolicy)
    acc = ApplyAddRule(acc, product, numericPolicy)
staged(i, j) = ApplyFinalizeRule(acc, numericPolicy)
```

The complete destination is staged before publication. Retire is the only
authority that may publish the accumulator image.

## Transpose Boundary

`MTRANSPOSE` is primarily a layout, shape, and alias operation. It must not
acquire MACC arithmetic semantics. Its supported source/destination layout
matrix and in-place/out-of-place alias rules must be explicit and fail closed.

## Prohibitions

- no VectorALU or dot-product arithmetic fallback;
- no DSC, Lane7, VMX, assist, or external backend fallback;
- no host matrix library;
- no partial accumulator or tile publication;
- no compiler-selected arithmetic that differs from runtime policy;
- no accidental fused operation or host extended precision.

## Evidence

Tests must cover K-order sensitivity, per-step versus final rounding/overflow,
signedness, widening, layout decoding, alias rejection, unsupported layout,
zero dimensions, boundary dimensions, and byte-exact staged results.

## Exit Criteria

Phase 16 is closed. `MatrixTileMaccArithmeticAbi` is the single production
implementation of decode, multiply, add, and finalize. Integer elements use
explicit little-endian codecs, exact `BigInteger` products and sums, and a
final accumulator-encoding trap. Binary32 and binary64 use software IEEE-754
decode and round-to-nearest-ties-to-even after each multiply and add; the path
does not call CLR `float`, `double`, fused multiply-add, or host matrix APIs.

`MatrixTileLayoutPolicyAbi` separately versions and fingerprints the supported
addressing/order rules. Runtime decode requires explicit
row-major/ascending-K policy for `MTILE_MACC` and explicit row-major coordinate
permutation policy for `MTRANSPOSE`. Column-major, blocked, interleaved,
tampered, missing, and operation-mismatched policies fail before execution.

`Phase16MatrixTileFormalArithmeticAndLayoutTests` covers stable policy
fingerprints, missing/tampered policy rejection, little-endian integer
encoding, final integer overflow, K-order sensitivity, separate rounding,
canonical NaN, infinity, subnormal preservation, binary64 byte-exact output,
and transpose alias/layout negatives. Closure result: closed. Later Phase
17-19 evidence tracks policy-bound identity, corpus coverage, and closed
compiler sideband conformance.
