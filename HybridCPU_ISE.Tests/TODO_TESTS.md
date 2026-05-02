# Historical Test Ideas

## Status

This file is historical and non-authoritative.
It is retained as a quarantined scratchpad of old validation ideas, not as the current validation
plan.

Authoritative current evidence lives in:

- `Documentation/validation-baseline.md`
- `Documentation/evidence-matrix.md`
- `Documentation/WhiteBook/13. validation-status-and-test-evidence.md`
- `HybridCPU_ISE.Tests/EVALUATION_TESTS_README.md`
- `build/recount-validation-evidence.ps1`

## Current Interpretation

Older notes in this area used legacy FSP-first and mask-first language.
The current architecture-facing vocabulary is:

- typed-slot legality and reject taxonomy;
- register-group and structural conflict screening;
- replay certificate coordination and invalidation;
- assist quota and backpressure legality;
- precise fault and write-back ordering;
- memory binding seams and observable backend limitations;
- bounded replay-stable lane reuse rather than universal deterministic execution.

## How To Promote An Idea From This Scratchpad

An old idea becomes current work only when it is rewritten into:

- a named proof suite under `HybridCPU_ISE.Tests/`;
- a current code surface with no stale terminology;
- a documented evidence mapping in `Documentation/evidence-matrix.md`;
- a reproducible command under the declared `VSTest` runner policy.

Until then, this file must not be cited as current validation coverage or as a repository roadmap.
