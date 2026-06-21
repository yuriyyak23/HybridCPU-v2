# Historical ISA Model Status Snapshot

## Status

This file is a historical status snapshot for an older ISA-model subfamily.
It is retained for repository archaeology only and is not authoritative for the current validation
baseline, current pass counts, or current repository-wide completion state.

Use these entry points for current authority instead:

- `Documentation/validation-baseline.md`
- `Documentation/evidence-matrix.md`
- `Documentation/WhiteBook/13. validation-status-and-test-evidence.md`
- `HybridCPU_ISE.Tests/EVALUATION_TESTS_README.md`

## What This Historical Note Still Tells You

The older ISA-model effort was centered on two proof themes that still map onto live architectural
language:

- register-group/structural conflict screening
- intra-core SMT densification

Legacy names inside retained test families may still use `FSP` or `SafetyMask` vocabulary.
Those names must be read as historical aliases rather than current repository-facing architecture
terminology.

## Historical Anchor Points

The retained historical note is still useful for locating older family names such as:

- `ISAModelHazardDetectionTests`
- `ISAModelFSPInvariantTests`

Those names should be interpreted as localized historical families, not as the current validation
baseline and not as a complete index of live proof coverage.

## What This File No Longer Claims

- No current pass totals.
- No repository-wide completion percentage.
- No current estimate of total remaining ISA-model test categories.
- No authoritative roadmap for the live repository.
- No author/date stamp that should be treated as current validation truth.

## Current Reading Rule

If this file disagrees with current code, current named proof suites, or validation artifacts, treat
this file as historical and prefer the live evidence surfaces listed above.
