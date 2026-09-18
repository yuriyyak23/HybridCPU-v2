# 18 — FSP donor quality and versioned static evidence

## Goal

Improve donor usefulness and emit structured compiler evidence for FSP while preserving dynamic FSP as runtime-owned behavior.

## Current-code reality and reuse

Reuse `HybridCpuStealabilityAnalyzer`, typed-slot facts, existing scheduler post-FSP scoring and Phase 03 resource footprints. Replace weak boolean-only hints only where a versioned evidence record adds measurable value; do not create a competing runtime certificate.

## FSP ownership model

Compiler responsibility is limited to **static donor quality / eligibility evidence and profitability planning**. Runtime remains sole authority for dynamic slot reclamation, donor selection at execution time, freshness/replay validation, Stage A/B interaction, injection, execution, publication, commit and retire.

A compile-time “donor” is therefore a candidate classification, not a future runtime donor reservation. Compiler output cannot reserve a reclaimed slot or promise that donor/receiver state will still be compatible.

## Evidence fields

Stable bundle/instruction identity, eligible static slot/classes, dependency horizon, predicted resource slack/pressure, VT relation, lane6/lane7 exclusions, memory/serialization exclusions, topology/model digest and profitability score inputs.

Add explicit precision/provenance for every evidence field (`ExactStatic`, `ConservativeSet`, `Unknown`, `ProfileOnly`) and a schema-level `Authority=CompilerEvidenceOnly` marker. Unknown dynamic state remains unknown; it is not predicted into a permission bit.

The compiler does **not** emit trusted freshness, epoch/generation, owner validity, replay status, runtime donor identity, dynamic slot ownership, `LegalityDecision` or execution permission. Trusted runtime attaches/revalidates all dynamic context.

## Donor-quality analysis

Profitability should model the value of a reclaimable static hole, receiver demand, likely resource compatibility and schedule impact rather than maximizing the number of marked donors. Important outputs include:

- `eligible_static_donor_candidates`;
- `predicted_useful_reclaim_opportunities`;
- pressure/conflict reasons by register group, bank/channel, lane6/lane7 and certificate class;
- expected cycles/critical-path value if runtime can reclaim;
- evidence precision and profile dependence.

Profile data may change ranking/thresholds only. It cannot change static dependency safety, special-contour exclusions or resource facts from `Unknown` to `Exact`.

## Rollout

1. donor-quality scoring default-off;
2. emit-only evidence;
3. runtime shadow comparison;
4. any runtime fast-path/adoption is a separately authorized runtime change with its own SafetyVerifier/LegalityDecision tests, not compiler authority granted by this phase.

On schema/digest mismatch, runtime ignores/rejects the advisory evidence and executes the existing dynamic FSP path. Compiler-side feature disable returns to the verified prior scheduling behavior.

## Tests / acceptance

Negative controls for stale evidence, topology mismatch, replay, register-group conflicts and special contours. Measure accepted/eligible donors, successful injects, useful reclaimed slots and cycles saved—not reject count alone.

Add static tests that forbid authority-bearing fields (`epoch`, `fresh`, `owner`, `admitted`, `execute`, `commit`, `retire`) in compiler FSP evidence except explicitly namespaced diagnostic observations that cannot be consumed as permission. Mutate runtime freshness/replay state after compilation and verify compiler evidence never bypasses revalidation.

**Acceptance:** static evidence is deterministic/versioned, has zero authority-bearing fields, carries precision/provenance, and demonstrates predictive/profitability value before any runtime consumer optimization; dynamic FSP correctness remains unchanged when all compiler evidence is removed.
