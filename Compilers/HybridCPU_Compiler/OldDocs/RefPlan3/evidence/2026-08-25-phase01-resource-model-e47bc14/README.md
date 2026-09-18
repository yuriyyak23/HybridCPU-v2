# Phase 01 unified machine resource model

- Implementation subject: `e47bc14c908d51ba039ab285ab97e59a9f7590a6`.
- Branch: `refactor/compiler-core-authority-boundaries`.
- Disposition: `DirtyAttributed`; the three foreign worktree paths listed in the JSON were
  preserved and excluded from both Phase 01 commits.
- Evidence JSON SHA-256:
  `8f3e75a5a039441b63a29ceb2f5ac6acd23cbc2b93309c44009e58c2ca9e74d2`.

## Gate result

The compiler-owned V1 description, immutable footprints, checked cycle state and default-off
shadow adapter preserve the existing exact legality/placement path as the only scheduling
decision source. Unsupported facts, including the matrix-tile topology gap, are `Unknown` and
fall back to that exact path. No compiler result grants runtime legality, issue, publication,
commit or retire authority.

Focused Phase 01 tests passed `14/14`; the Phase 00+01 cumulative matrix passed `39/39`; and the
Release compiler build completed with `0` errors and `49` existing warnings. Per user direction,
the long full compiler suite is counted only at the end of completed Phase 03 and is not claimed
for this subject.

The completed six-profile Release diagnostic used two warmups and twelve alternating baseline /
shadow repetitions. Shadow p95 time changed `-0.45%`, p95 allocated bytes changed `+0.21%`, and
disagreements, fallbacks and artifact parity failures were all zero. Wall-clock observations were
telemetry only. Production output selection is bounded solely by the deterministic maximum of
`64` shadow evaluations per compilation.

## Next gate

Phase 02 may add only a default-off, counter-bounded comparison of complete ready subsets. It must
reuse the existing exact placement search and return the exact existing greedy result on disable,
unsupported input or deterministic budget fallback. Bundle materialization may validate a witness
but cannot repair membership or scheduling policy.
