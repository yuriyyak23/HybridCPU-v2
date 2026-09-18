# 18 — Donor quality and unified FSP profitability planning

## Goal and motivation

Replace scattered Post-FSP scoring/slack heuristics with one explainable donor-quality model before defining certificate ABI or emitting evidence.

**Status:** `Planned`.

## Confirmed starting state

- `HybridCpuStealabilityAnalyzer` already excludes barrier, control-flow, system, exclusive-cycle and may-trap operations and emits advisory verdicts.
- `HybridCpuLocalListScheduler` already has default-off `UsePostFspScoring` and `UseConditionalSlackReservation` profile heuristics.
- Runtime register-group rejects in the benchmark occur during SMT/FSP reclaim attempts; they are not proof of an illegal foreground compiler bundle.
- Reducing raw rejects by suppressing attempts could worsen useful reclaim and total cycles.

## In scope

- Donor eligibility, resource class, preferred hole classes, predicted acceptance, expected reclaim value, priority and risk.
- Unified replacement/adapter for existing Post-FSP score and slack reservation.
- Separate foreground and donor/FSP outcome taxonomies with correct denominators.

## Explicit non-goals

- No runtime accept prediction as legality, no FSP emission/fast path and no second parallel heuristic.
- No sacrificing foreground cycles solely to reduce raw reject count.

## Canonical types/components

Proposed `FspDonorSuitabilityV1`, `DonorResourceClass`, `PreferredHoleClass`, `FspProfitabilityReportV1`; existing `HybridCpuStealabilityAnalyzer`, Post-FSP scoring/slack settings, Phase 03 resources and Phase 17 VT context.

## Target architecture and data flow

```text
static stealability + resource/pressure + VT/domain context
 + profile outcome rates (profitability only)
 -> donor eligibility/risk/value report
 -> bounded joint schedule cost and optional slack decision
 -> foreground schedule unchanged unless cycles/value gate wins
 -> Phase 19 static evidence payload
```

## Invariants and authority boundaries

- Static hard exclusions remain legality; profile cannot make an ineligible donor eligible.
- Predicted acceptance/value is advisory and cannot bypass runtime certificates.
- Foreground structural conflicts and donor/FSP rejects are separate namespaces.
- Every rate records numerator and denominator.

## Dependencies

Phases 03, 05, 07, 08 and 17 verified.

## Implementation backlog

1. Define foreground versus donor/FSP event taxonomy and denominator schema.
2. Lift existing stealability verdict into resource/hole/risk categories.
3. Add expected reclaim value from eligible donor, accepted inject, useful slots and observed cycles saved.
4. Implement one versioned profitability report with static/profile components separated.
5. Adapt/deprecate internal Post-FSP score/slack code to delegate to the new model; no parallel decisions.
6. Add lexicographic cost so foreground cycles/correctness precede expected reclaim.
7. Shadow compare the existing heuristics and the new report.
8. Freeze workloads with attempts suppressed versus useful reclaim to prevent metric gaming.

## Migration and compatibility strategy

Existing Post-FSP flags remain compatibility switches but initially delegate to shadow comparison. No public API removal. Default scheduling remains unchanged until cycles/useful-reclaim gates pass.

## Tests

### Positive/property/determinism

- Eligible/ineligible donor classes, preferred holes, risk ordering and stable report bytes.
- Metric reconciliation: attempts = accepts + rejects + stale/fallback partitions where applicable.
- Same profile/input yields identical decisions.

### Negative

- Profile tries to override barrier/trap/system exclusion, cross-domain/contour donor, missing denominator and stale profile/model.

### Static

- Only one production slack/donor decision owner.
- Predicted acceptance cannot reach runtime admission APIs.

## Benchmarks and KPI

- Foreground structural conflicts/rejects reported separately from donor/FSP certificate rejects.
- Report `FSP reject / reclaim-attempt`, `accepted / eligible-donor`, successful injects, useful reclaimed slots, cycles saved and attempt suppression.
- Qualified packed profiles: total runtime cycles <=0.99x old heuristic, IPC non-regressing, useful reclaimed slots non-regressing, physical lane realization `1.0`, width drops `0`.
- Raw reject reduction alone is never an acceptance criterion.

## Diagnostics and telemetry

Donor ID/VT/domain/contour, static eligibility reason, resource/hole class, predicted probability/value/risk, numerator/denominator counters, actual outcome and cycles saved attribution.

## Bounded-search, timeout and fallback policy

No wall-clock cutoff. Fixed W=8 report work, max 64 donor alternatives/cycle and inherited Phase 02/17 state caps. Limit invokes the current compatibility heuristic or no-slack baseline according to a pinned policy.

## Risks and forbidden shortcuts

- No gaming reject rate by eliminating useful attempts.
- No duplicate Post-FSP heuristic path.
- No compiler claim that a donor certificate will be accepted.

## Rollback / kill switch

Disable unified profitability consumption and restore the pinned existing/default-off behavior; retain denominator telemetry.

## Acceptance and merge gates

- Event accounting identities and foreground/donor separation tests pass.
- Existing/new shadow report and representative cycles/useful-reclaim qualification archived.
- Static authority scan and deterministic fallback pass.

## Status criteria

- `implemented`: unified report/model and compatibility adapters exist.
- `verified`: accounting, authority and performance gates pass.
- `default-enabled`: only after Phase 24 compiler+FSP-evidence profile review.
- `release-authorized`: profitability policy only; no runtime acceptance authority.

## Residual work / next gate

Phase 19 serializes only static claims from this model; Phase 20 emits them without runtime use.
