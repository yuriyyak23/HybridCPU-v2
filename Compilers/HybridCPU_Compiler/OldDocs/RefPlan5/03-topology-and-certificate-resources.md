# 03 — Register groups, PRF ports, banks/channels and certificate-aware resources

## Goal and motivation

Make known heterogeneous contention explicit state instead of score-only telemetry, while preserving the rule that compiler predictions are structural evidence and runtime owns live legality.

**Status:** `Verified` in default-off compiler shadow mode on implementation subject
`0b5f7d2e444dbc7b2658c200bf3bcdaffa874e18`. Default enablement is not authorized.

Evidence: `evidence/2026-08-25-phase03-topology-0b5f7d2/compiler_phase03_topology_v1.json`.

## Confirmed starting state

- Runtime `BundleResourceCertificate4Way` checks shared masks and per-VT register read/write group masks; Stage A also owns scoreboard, bank-pending, hardware and speculation budgets.
- Compiler has no `RegisterHazardMask`, PRF-port or bank/channel footprint. It only has coarse structural ports plus optional profile penalties/tie-breaks.
- Historical candidate data reports physical realization `1.0`, width drops `0`, register-group rejects `2000–3000`, and dominant memory stalls on selected profiles; this is not yet current release evidence.
- `HybridCpuDependencyAnalyzer` classifies memory as must/may overlap but not exact/possible/unknown bank/channel demand.

## In scope

- Static compiler register-group read/write masks and PRF port demand.
- Exact/possible/unknown bank and channel sets from address evidence.
- Compiler structural-certificate footprint classes aligned by versioned mapping.
- Hard constraints for known architectural conflicts; expected-cost terms for possible/unknown facts.
- Prediction/runtime differential telemetry.

## Explicit non-goals

- No compiler ownership of physical register allocation, live PRF, rename, scoreboard, MSHR, bank-pending tokens or dynamic capacity.
- No lane/certificate authority and no SafetyVerifier bypass.
- No profile-derived `NoAlias`, exact bank or legality.
- No new runtime capability or contour.

## Canonical types/components

- Extend `IrResourceFootprint` with `RegisterReadGroupMask`, `RegisterWriteGroupMask`, required PRF read/write ports, bank/channel knowledge, shared certificate class and evidence precision.
- Extend `HybridCpuCycleResourceState` with per-VT group masks, port counters, known bank/channel occupancy and certificate-class usage.
- New immutable `HybridCpuMachineTopologyV1` with provenance and digest.
- Static mapping adapters from canonical IR operands/opcode info; runtime parity adapters are test-only.

## Target architecture and data flow

```text
static operands/address expression/opcode
 -> exact/conservative footprint + provenance
 -> known conflict: hard CanReserve rejection
 -> possible overlap: feasible + lexicographic pressure cost
 -> unknown: conservative dependency legality + profitability cost only
 -> schedule
 -> runtime executes normal Stage A/B and reports actual reject category
 -> offline prediction confusion matrix
```

## Invariants and authority boundaries

- `Known architectural constraint -> compiler structural feasibility`; `unknown runtime behavior -> cost/telemetry`.
- Runtime register-group mask layout, port capacity and bank hash must be bound to a version/digest; unknown version fails closed to conservative compiler behavior.
- Per-VT register groups must not be merged into one mask; RAR remains allowed only where canonical semantics say it is non-destructive.
- Certificate footprint cannot mean execution eligibility.
- Runtime dynamic resources always revalidate.
- Stage A class admission, Stage B lane materialization, replay/freshness and backend PRF/rename availability are never inferred from compiler resource state.

## Dependencies

Phases 01 and 02 verified.

## Implementation backlog

1. Audit the canonical runtime mask/group/port/bank/channel producers and document which facts are stable architectural contracts versus implementation details.
2. Define topology schema and exact version/digest compatibility policy; do not copy mutable runtime values.
3. Derive register-group read/write masks from typed register operands; label unsupported/special/VT-shared state explicitly.
4. Derive PRF port demand by operand class and instruction semantics; support multi-cycle reservations only when architecturally specified.
5. Add address-evidence lattice: `Exact`, `FiniteSet`, `All`, `Unknown`; compute bank/channel sets only from static expressions and canonical mapping.
6. Map structural certificate classes without serializing runtime certificates.
7. Extend cycle feasibility and cost with structured reasons.
8. Add shadow prediction beside current compiler decisions; collect confusion matrix against runtime reject telemetry by resource.
9. Enable known register/port/bank constraints separately behind feature flags; possible/unknown costs separately gated.
10. Add target-topology digest to schedule evidence and invalidate cached decisions on mismatch.

## Migration and compatibility

- Coarse current structural model remains fallback.
- Each resource family has an independent kill switch.
- Unknown topology/version preserves prior schedule and runtime validation.
- No change to public compatibility APIs or current emission ABI in this phase.

## Tests

### Positive/property/determinism

- Exact read/write group aggregation including per-VT separation and RAR allowance.
- Port count conservation; known distinct banks/channels co-schedule when other constraints allow.
- Footprint precision monotonically refines cost/feasibility without profile input.
- Stable results across repeated and collection-order-perturbed runs.

### Negative

- RAW/WAR/WAW group conflict, over-port, same exact bank/channel conflict, unknown mapping, stale topology digest, special/control/VT-shared register, lane6/lane7 certificate collision.
- A low profile conflict rate cannot turn a known conflict feasible.

### Static

- No compiler reference to runtime mutable PRF/rename/commit/retire state.
- No `SafetyVerifier` bypass or compiler-produced runtime `LegalityDecision`.
- Profile reader absent from footprint/feasibility code.

## Benchmarks and KPI

- Footprint coverage 100% for default-enabled ops; exact/finite/unknown rates reported.
- Known compiler-predicted conflict false-negative rate `0` on generated parity cases.
- Foreground compiler structural/group conflicts are reported separately and must not increase unexpectedly; known compiler-predicted conflict false negatives remain zero.
- Donor/FSP register-group rejects are reported only as `reject / reclaim-attempt` together with `accepted / eligible-donor`, successful injects, useful reclaimed slots and cycles saved. No raw reject-reduction target applies in this phase.
- Total runtime cycles/IPC remain primary; physical lane realization stays `1.0000` and width drops stay `0` on qualified packed profiles.
- Peak PRF port, predicted group conflict, bank/channel conflict, lane6/lane7 pressure emitted for every run.
- Compile time/memory incremental p95 <=5%/8%; total plan thresholds remain <=10%/15%.

## Diagnostics and telemetry

Per-resource requested/used/capacity, precision, topology digest, predicted decision, actual runtime reject classification, false-positive/negative counters, exact vs possible bank/channel conflict and first divergence cycle.

Telemetry is diagnostic/profitability input only after explicit schema review; it cannot be fed back into legality or promote `Unknown/Possible` to `Exact`.

## Bounded-search, timeout and fallback

Resource checks are fixed-size bitset/counter operations. Address-set derivation caps finite sets at 8; larger sets become `All/Unknown`. Unsupported topology falls back to Phase 01 coarse constraints and records why.

## Risks and forbidden shortcuts

- Do not infer exact bank/channel from samples.
- Do not overfit current diagnostic counts into a hard ISA capacity.
- Do not serialize compiler cycle state as a live runtime certificate.
- Do not sacrifice cycles merely to reduce runtime reject counters; reject telemetry is classified evidence, not a legality/performance objective by itself.

## Rollback / kill switch

Per-family flags: register groups, PRF ports, bank/channel, certificate cost. Disable returns to Phase 02 behavior; topology digest mismatch automatically takes conservative fallback.

## Acceptance and merge gates

- Owner-reviewed stable topology contract or explicit `Unknown` disposition for every field.
- Generated/static/runtime differential tests pass.
- Zero known-fact false negatives; performance and correctness gates use cycles/IPC, schedule feasibility and classified resource diagnostics rather than a raw reject-reduction target.
- Authority/static guards and deterministic outputs pass.

## Audit correction: downstream consumers

- Phase 07 consumes stable register-group/PRF topology for liveness/pressure after Phase 08A fixes the target register namespace.
- Phase 13 consumes only proven static resource capacities when deriving MII components; an unknown capacity cannot silently become an optimistic zero-cost resource.
- Phase 20 consumes the same register-group/PRF contract for physical allocation and spill repair; no second allocator-specific topology model is allowed.
- Phases 18–19 consume these facts only for FSP/VDSA profitability/evidence and never as runtime admission authority.

## Status criteria

- `implemented`: footprint/state support all resource families in shadow mode.
- `verified`: topology parity, differential accuracy and KPI gates pass.
- `default-enabled`: known-fact constraints may enable per family; probabilistic costs require separate profitability evidence.
- `release-authorized`: compiler release only; runtime remains unchanged unless separately authorized.

## Residual work / next gate

Phase 04 is the next dependency gate. Known register-group constraints, PRF ports, banks/channels and certificate costs remain default-off. PRF capacities and default bank/channel mappings remain explicitly `Unknown` until an immutable owner-reviewed architectural contract exists.
