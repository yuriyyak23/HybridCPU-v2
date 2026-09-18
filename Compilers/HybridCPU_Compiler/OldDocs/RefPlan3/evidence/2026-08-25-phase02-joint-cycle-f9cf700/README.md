# Phase 02 bounded joint cycle composition

- Implementation subject: `f9cf700`.
- Evidence JSON SHA-256:
  `c3d1d0505a87b6058451a27844fd7e979f316f97dfb02779b77717e0f8805598`.
- Disposition: `DirtyAttributed`; the three listed foreign paths were preserved and excluded.

The default-off path compares complete, structurally validated alternatives for blocks of at most
`TopK=12`, reuses `HybridCpuSlotModel.SearchStructuralAssignments`, and has no second slot packer.
Production selection is bounded by `12` candidates, depth `8`, `4096` evaluated states and beam
`64`; elapsed time is diagnostic only. Larger blocks and joint schedules that do not reduce the
latency-inclusive block schedule length return the exact existing path.

Focused tests passed `14/14`, the Phase 00–02 cumulative matrix passed `53/53`, and the Release
compiler build completed with zero errors. The adversarial compatibility corpus improves from
three cycle groups to two. All six representative profiles have exact cycle and bundle parity;
their geomeans are `1.00x`. Therefore correctness/no-regression verification passes, but the
`<=0.99x` representative improvement target does not: default enablement remains fail-closed.

Release p95 time changed `-1.54%` and p95 allocation `0.00%` over six alternating repetitions.
The full compiler suite remains deferred to the end of completed Phase 03 by user direction.

The next Phase 03 gate is shadow-only topology evidence. No Phase 02 witness grants runtime lane,
execution, publication, commit or retire authority.
