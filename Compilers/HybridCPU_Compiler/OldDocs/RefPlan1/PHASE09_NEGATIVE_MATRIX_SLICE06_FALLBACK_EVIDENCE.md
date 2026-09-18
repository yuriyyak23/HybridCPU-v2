# Phase 09 Negative Matrix Slice 06 - Fallback and Evidence

Status: implemented as focused compiler-core negative gates.

## Scope

This slice freezes fallback and evidence cleanup boundaries before caller migration:

- hidden cross-contour fallback remains forbidden by default;
- same-contour structural retry is recorded separately from lowering fallback;
- host-owned evidence cannot enter guest-visible or domain architectural state;
- evidence-only records cannot claim authority;
- negative-decision telemetry includes required structured fields.

## Tests

Added `CompilerPhase09FallbackEvidenceNegativeMatrixTests`:

- `CrossContourProviderMismatchRejectsWithoutHiddenFallback`
- `SameContourStructuralRetryPolicyIsRecordedSeparatelyFromLoweringFallback`
- `HostOwnedEvidenceCannotEnterGuestOrDomainArchitecturalState`
- `EvidenceRecordCannotClaimAuthorityWhenSemanticsAreEvidenceOnly`
- `NegativeDecisionTelemetryContainsRequiredAuthorityEvidenceEmissionAndFallbackFields`

## Authority Notes

- Cross-contour provider mismatch returns `CrossContourFallbackForbidden`, `NoEmission`, no produced artifacts, and forbidden fallback proof.
- Same-contour structural retry is policy metadata only and is not production lowering fallback.
- Evidence isolation validator rejects guest/domain architectural destinations for host-owned evidence.
- Negative telemetry carries decision, contour, authority, evidence, emission, production lowering and fallback proof fields.

## Historical Next Task

The cleanup/migration readiness task listed here has since started and is
tracked in `PHASE09_CLEANUP_MIGRATION_READINESS.md`. For the current open
compiler work, use:

- `PHASE09_REMAINING_COMPILER_WORK.md`
- `CONTINUATION_PROMPT_PHASE09.md`
