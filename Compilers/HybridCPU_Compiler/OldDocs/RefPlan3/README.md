# RefPlan3 — evidence-driven HybridCPU compiler modernization

## Evidence subject

- Plan/status schema: `RefPlan3StatusV1`; verified Phase 00 subject and current phase states are recorded in that file.
- Evidence policy: current local sources/tests/generators/scripts/diagnostics/benchmarks and local Git history only; no remote Git or assumed `master` state.
- Audit inputs and earlier dirty-worktree captures are provenance only; they do not supersede clean, exact-subject evidence.
- Phase 00 is verified on `f1de3445dd897bc525d57dfa882fd736799f203d`; later phases require their own pinned subject and gates.

`Fact` means directly supported by the audited tree/history. `Hypothesis` or `target` requires the named phase evidence.

## Current-state verdict

The compiler already has IR/CFG, intra/inter-block dependency analysis, critical-path BB list scheduling, W=8 structural feasibility, exact slot-assignment enumeration, deterministic Pareto placement/lookahead, bundle formation/lowering/serialization and advisory stealability evidence. Runtime owns Stage A admission, `SafetyVerifier`, Stage B materialization, FSP/replay, execution, publication, commit and retire. RefPlan3 must not build another exact packer or duplicate established provider/dispatch/parity contracts.

The confirmed scheduling gap remains `HybridCpuLocalListScheduler.BuildCycleGroup`: it greedily commits cycle membership. `CanAddToCycle` calls structural analysis and proves at least one exact placement exists for the growing set, but does not compare alternative complete CycleGroups. `HybridCpuBundleFormer` materializes fixed membership and must not repair policy. Phase 02 therefore reuses `HybridCpuSlotModel.SearchStructuralAssignments` inside bounded joint composition.

External-audit reconciliation produced these accepted corrections:

- Production code selection may use deterministic states/cuts/candidates/nodes/iterations only. Wall-clock is limited to oracle/CI/watchdog behavior that cannot choose emitted code.
- Region abstraction precedes liveness/pressure; operand renaming is split behind a real virtual-value/interference/carrier-allocation/encoding layer.
- `PredicateMask` exists and is preserved, but it is vector-mask evidence—not a proven general control-predication contract. Phase 09 audits capability before guarded/hyperblock work.
- Loop/MII and modulo production have no dependency on the Phase 12 oracle backend.
- Coarse `IrMemoryRegion` overlap is insufficient; Phase 08 adds proof-labelled alias/affine distance analysis.
- VT-aware policy, donor quality and VDSA/donor-prefetch need first-class compiler phases.
- Register-group counts from the supplied logs are donor/FSP reclaim-path rejects, not automatically foreground compiler illegality; denominator-correct metrics replace the raw “rejects -20%” gate.
- Compiler static evidence and runtime epoch/generation/replay context are physically separate.
- Phase 13 computes a finite, witnessed `MaxStageCount(II)` by deterministic APSP over the
  versioned difference-constraint graph; MII and stage bound are distinct facts.
- Phase 14 accepts only a joint temporal+exact-placement result. `IrModuloSchedule` owns the
  prescribed W=8 witness and `BundleFormer` may only verify/materialize it.
- Modulo value versioning is explicit and fail-closed: the first production subset accepts only
  `NoneRequired`; runtime rename is never a compiler proof of rotating-register/MVE correctness.
- Constraint solving and CFG transformation have separate owners:
  `HybridCpuConstraintModuloScheduler` and `HybridCpuModuloLoopExpander`.
- `SMT` is reserved for simultaneous multithreading in HybridCPU terminology. Constraint services
  use solver-neutral problems/backends and stable constraint IDs.

## Pipeline before RefPlan3

```text
VLIW instructions + sideband
 -> HybridCpuIrBuilder -> IrProgram / CFG / IrBasicBlock
 -> HybridCpuProgramDependencyAnalyzer
 -> per-BB scheduling DAG
 -> HybridCpuLocalListScheduler
      ready set -> greedy BuildCycleGroup
      each append proves structural/exact-placement existence
 -> HybridCpuBundleFormer
      exact placement/Pareto/lookahead; membership fixed
 -> HybridCpuBundleBuilder -> typed facts + advisory StealabilityHint
 -> lower/serialize/annotations
 -> runtime ingress/decode
 -> Stage A + SafetyVerifier -> Stage B -> execution/commit/retire
```

Canonical 4-way multithreaded compilation currently produces separate per-VT programs and a compatibility interleaver. Mixed-VT program inputs also exist and the historical packed profiles report 32 cross-VT groups, but no first-class fairness/complementarity policy exists.

## Pipeline after RefPlan3

```text
IR/CFG + optional profitability profile
 -> MachineResourceModel
 -> BB-only SchedulingRegion
 -> liveness/pressure
 -> optional bounded VirtualValue -> carrier allocation -> encoding rewrite
 -> EBB formation
 -> proof-labelled memory dependence / affine distance
 -> straight-line bounded joint CycleGroup + existing exact placement
 -> branches:
      predicate audit -> control-sensitive/guarded -> capability-gated hyperblock
      loop DAG/MII/stage bound -> bounded constraint modulo -> unroll/fusion/U&J
      VT context -> cross-VT complementarity/fairness
      donor-quality -> static FSP envelope -> emit-only
      memory+loop+VT -> static VDSA/donor-prefetch intent
 -> unchanged lowering/materialization contract
 -> runtime-owned validation/execution; optional shadow/reuse only by separate authorization
```

Phase 12 exact oracle runs beside this pipeline as CI/research qualification. Production compilation neither loads nor requires its solver backend.

## Phase graph and current plan disposition

Phase 00 is verified on its pinned subject; unimplemented phases remain `Planned`. “Gated” is a
precondition, not authorization.

| ID | Phase | Depends on | Initial/default disposition |
| --- | --- | --- | --- |
| 00 | [Current state and reproducible baseline](00-current-state-and-baseline.md) | — | Verified; no behavior change |
| 01 | [Unified machine resource model](01-resource-footprint-and-machine-model.md) | 00 | Verified; shadow remains default-off |
| 02 | [Bounded joint cycle composition](02-bounded-joint-cycle-composition.md) | 01 | Verified default-off; default-enable KPI unmet |
| 03 | [Topology/register/port/bank/certificate resources](03-topology-and-certificate-resources.md) | 01 | Verified default-off; live topology remains runtime-owned |
| 04 | [BB-only SchedulingRegion abstraction](04-bb-only-scheduling-region-abstraction.md) | 02, 03 | Planned; byte-parity mode |
| 05 | [Liveness and pressure analysis](05-liveness-and-pressure-analysis.md) | 04 | Planned; analysis first |
| 06 | [Bounded local carrier allocation/rename](06-bounded-local-carrier-allocation-and-renaming.md) | 05 | Planned; optional/default-off |
| 07 | [EBB region formation](07-ebb-region-formation.md) | 05, 06 | Planned; BB fallback |
| 08 | [Memory-dependence refinement](08-memory-dependence-refinement.md) | 03, 04 | Planned; conservative/shadow |
| 09 | [Predicate capability audit](09-predicate-capability-audit.md) | 07, 08 | Planned; audit-only first |
| 10 | [Control-sensitive and guarded trees](10-control-sensitive-and-guarded-tree-scheduling.md) | 07, 08, 09 | Planned; guarded subset gated |
| 11 | [Capability-gated hyperblocks](11-gated-hyperblocks.md) | 05, 08, 09, 10 | Planned; may become ISA-extension defer |
| 12 | [Exact scheduler oracle](12-exact-scheduler-oracle.md) | 03, 04 | Planned; optional CI/research only |
| 13 | [Loop canonicalization, distance DAG, MII and stage bound](13-loop-canonicalization-distance-dag-and-mii.md) | 03, 04, 05, 08 | Planned; no oracle dependency |
| 14 | [Bounded constraint modulo scheduling](14-bounded-constraint-modulo-scheduling.md) | 03, 08, 13 | Planned; joint witness, counter-bounded/default-off |
| 15 | [Architecture-driven unrolling](15-architecture-driven-unrolling.md) | 13 | Planned; modulo optional |
| 16 | [Fusion and unroll-and-jam](16-fusion-and-unroll-and-jam.md) | 08, 13, 15 | Planned; separate transform gates |
| 17 | [VT-aware/cross-VT scheduling](17-vt-aware-cross-vt-scheduling.md) | 02, 03, 04, 05, 08 | Planned; VT-local fallback |
| 18 | [Donor quality/FSP profitability](18-donor-quality-and-fsp-profitability.md) | 03, 05, 07, 08, 17 | Planned; replaces/generalizes old heuristics |
| 19 | [Static FSP donor evidence ABI](19-structured-fsp-donor-certificate-abi.md) | 03, 07, 18 | Planned; schema only |
| 20 | [FSP static envelope emit-only](20-fsp-certificate-emit-only.md) | 19 | Planned; runtime ignores |
| 21 | [Runtime validation/measurement](21-runtime-validation-and-measurement.md) | 00, 20 + external authorization | Planned; no decision influence |
| 22 | [Separately authorized FSP reuse](22-separately-authorized-fsp-fast-path.md) | 21 + new authorization | Planned; intentionally unauthorized |
| 23 | [VDSA/donor-prefetch advisory planning](23-vdsa-and-donor-prefetch-planning.md) | 03, 08, 13, 17 | Planned; static emit-only |
| 24 | [Integration and release profiles](24-integration-qualification-and-release-gates.md) | 00 + selected verified slice | Planned; per-profile decisions |

## Critical paths

Highest-ROI first compiler release:

```text
00 -> 01 -> 02 -> 24 CompilerOnly
```

Region/loop production path, with the oracle outside it:

```text
01 -> (02 || 03) -> 04 -> 05 -> 06(optional implementation, may be disabled) -> 07
03 + 04 -> 08
03 + 04 + 05 + 08 -> 13 MII + stage bound -> 14 joint constraint schedule + loop expansion
13 -> 15 -> 16

03 + 04 -> 12 oracle -> qualification reports only
```

Control branch:

```text
07 + 08 -> 09 predicate audit -> 10 control-sensitive/guarded
                                      -> 11 only if existing ISA capability passes
```

VT/FSP and memory-assist branches:

```text
02 + 03 + 04 + 05 + 08 -> 17 VT-aware
03 + 05 + 07 + 08 + 17 -> 18 donor quality -> 19 static ABI -> 20 emit-only
20 -> 21 external shadow authorization -> 22 separate reuse authorization

03 + 08 + 13 + 17 -> 23 VDSA/donor-prefetch static intent
```

## Global invariants and authority boundaries

1. `compiler scheduling / bundle / metadata / certificate != runtime legality / execution / publication / commit / retire authority`.
2. W=8 typed topology is fixed. Stage A class admission and Stage B materialization stay separate.
3. `BundleFormer` materializes immutable membership and prescribed placement when supplied; it
   never searches for a different modulo placement or splits/reorders/moves cycles to repair policy.
4. SystemSingleton, serialization, exclusive-cycle and hard-pinned lane constraints are hard; no cross-contour fallback.
5. Profile data affects profitability only; `MayAlias` cannot become `NoAlias` from telemetry.
6. Virtual SMT, FSP/replay, live PRF/rename, backend, completion, publication, commit and retire remain runtime-owned.
7. Production output is bounded only by deterministic states/cuts/candidates/nodes/iterations. Elapsed time is diagnostic or a CI/oracle/watchdog abort and cannot choose code.
8. Schedule cost is lexicographic: correctness/authority, makespan/II, stalls/critical progress, pressure/conflicts, bundles/productive width, expected donor value, stable tie-break.
9. Oracle/solver is optional CI/research infrastructure, never a required production backend.
10. Compiler FSP envelope contains static identity/claims only. Trusted runtime attaches epoch/generation/replay/owner context; stale/mismatch fails closed.
11. Compiler assist intent is advisory and cannot construct/authorize VDSA/DonorPrefetch runtime micro-ops.
12. Compatibility APIs remain until a separately accepted compatibility-window decision.

## KPI matrix

Snapshot-only values below remain `HistoricalUnverified`; values explicitly reconciled by Phase 00
are labelled as reproduced and still qualify only that pinned subject.

| Metric | Candidate baseline / measurement | Target and regression gate |
| --- | --- | --- |
| `schedule_cycles` | historical snapshot unavailable; reproduced current makespan: alu/novt 75, packed 48–49 | Phase 02 representative geomean `<=0.99x`; no workload `>1.01x` |
| `bundle_count` / non-empty cycle groups | historical snapshot and reproduced corpus: alu 60, novt 61, packed 36–37 | `<=1.00x` unless a measured cycle win is reviewed |
| single-VT / packed-VT width | `3.0833/3.0492`; `4.5556/4.4595` | secondary only; cannot pass without cycles/correctness |
| physical lane realization / width drops | `1.0` / `0` | preserve `1.0` and `0` on qualified packed profiles |
| `ready_window_size` | unavailable | avg/p50/p95/max per region; absence blocks search qualification |
| `cycle_search_states` / pruned alternatives | unavailable | state/prune/cap/fallback counts; production has no time cutoff; cap-hit <=1% Phase 02 |
| `optimality_gap_cycles / bundles` | unavailable | optional oracle median cycles gap <=1, p90 relative <=10% |
| `first_divergence_cycle` | unavailable | required for every production/oracle mismatch |
| peak PRF read/write ports | unavailable | predicted/known/unknown separated; no known overcommit |
| foreground register-group conflicts | unavailable | separately attributable compiler foreground metric; non-increasing |
| FSP register-group reject / reclaim-attempt | vt/max `3000/5625`, lk `2000/8625`, bnmcz `2125/8500` | rate decreases only with accepted/eligible, useful slots and cycles non-regressing |
| accepted / eligible donor; successful injects | unavailable | mandatory numerator/denominator; increase is supporting evidence |
| useful reclaimed slots / cycles saved | only aggregate reclaim ratios available | primary FSP profitability evidence; total cycles must decrease/non-regress |
| runtime certificate rejects | existing donor path counts above | foreground and donor/FSP namespaces never mixed |
| bank/channel conflicts | bank counter unavailable | exact/possible/unknown classes; unavailable is not zero |
| lane6/lane7 pressure | unavailable | avg/p95/max/choke counts; no hard-pin overcommit |
| `RecMII / SlotMII / PortMII / RegGroupMII / MemMII / ChosenII / MaxStageCount(II)` | unavailable | every eligible loop emits all; `ChosenII >= max(components)` and finite stage bound has a witness |
| code growth | unavailable | default module <=5%; phase-specific per-loop/function hard caps |
| compile time and memory | no compiler-only baseline | default time p95 <=1.10x, memory p95 <=1.15x; observed time never selects code |
| FSP donor candidates / accept / fallback / stale / reject | aggregate only | every event accounted with eligibility/attempt denominators |
| VT fairness/complementarity | four VTs, 32 cross-VT groups in packed snapshot | no starvation-bound violation when legal candidate exists; packed cycles <=0.98x |
| VDSA/donor-prefetch candidates | unavailable | emit-only has zero runtime delta; future runtime benefit must be cycles/memory-stall based |
| runtime cycles / IPC / memory stalls | alu 3001/1.7398/1028; packed 10625–11000/1.91–1.98/~4000–4125 | release geomean cycles <=0.98x, no workload >1.01x, IPC non-regressing |

## Test and benchmark matrix

| Layer | Positive/property/determinism | Negative/static | Representative evidence |
| --- | --- | --- | --- |
| Baseline/resource/joint search | DAG motifs, W<=8 exhaustive placement, byte parity | caps/fallback, singleton/hard pins, no duplicate packer | compiler corpus + synthetic greedy cases |
| Region/liveness/allocation | BB parity, intervals, interference/allocation/encoding | incomplete closure, special/VT-visible carrier, no runtime PRF owner | generated CFG/value graphs |
| EBB/memory | side-exit/effect equivalence, alias lattice/affine distances | MayAlias, overflow, volatile/atomic, contour/domain | EBB and memory-access corpus |
| Predicate/guards/hyperblock | opcode capability goldens, path traces | unsupported guard, store/trap/system, identity rebuild | decoder/execution predicate corpus |
| Oracle/loops/modulo/transforms | exhaustive tiny schedules/horizons, MII/stage witnesses, prescribed placement and output equivalence | unknown distance/alias/horizon, unproved value versioning, counter exhaustion, code/pressure cap | small oracle corpus + hot loops |
| VT/FSP | owner/fairness, accounting identities, static ABI bytes | cross-domain/contour, stale runtime context, authority scans | single/packed VT + FSP runtime logs |
| VDSA intent | criticality/reuse/static intent determinism | MayAlias, lane6/bandwidth, runtime-owner dependency scan | memory-stall profiles; emit on/off equality |
| Integration | per-release-profile matrices and parallel-build determinism | dirty subject, watchdog invalidation, rollback/canary | clean exact SHA, raw distributions archived |

## Risk register

| Risk | Detection | Mitigation / rollback |
| --- | --- | --- |
| Nondeterministic wall-clock fallback | static scan plus slow/fast machine schedule fingerprints | counter-only production budgets; watchdog fails run, never selects code |
| Region/rename dependency inversion | dependency/status JSON and API build gates | BB-only region -> pressure -> allocator -> EBB |
| Fake rename without carrier allocator | def-use/encoding/interference tests | keep Phase 05 analysis-only; Phase 06 default off |
| Arbitrary or unsound modulo horizon | APSP/exhaustive tiny-loop cross-check and bound witnesses | finite Phase 13 `MaxStageCount(II)` or fail closed |
| Temporal modulo result lacks exact W=8 placement | witness parity and BundleFormer no-repair tests | joint `IrModuloSchedule` membership + prescribed placement |
| Runtime rename assumed to solve overlapping iterations | modulo lifetime/versioning corpus | initial Phase 14 accepts `NoneRequired` only |
| Constraint solver mutates compiler CFG | dependency/API ownership scans | separate scheduler and `HybridCpuModuloLoopExpander` |
| Predication assumed from `PredicateMask` | capability matrix/effect goldens | speculation-free Phase 10; defer Phase 11 to ISA extension |
| MayAlias blocks all advanced transforms | coverage telemetry | improve static address/affine proofs; never use profile legality |
| Raw FSP rejects gamed by suppressing attempts | denominator/accounting identities and cycles/useful slots | unified Phase 18 metrics/cost |
| Compiler claims runtime freshness | schema/static scans | no epoch/generation/replay fields in static envelope |
| VT shaping crosses authority/domain | owner/fairness/domain properties | VT-local deterministic fallback |
| Assist intent becomes runtime bypass | dependency/static authority scan | emit-only Phase 23; separate future runtime plan |
| Dirty worktree invalidates claims | manifest status/hash check | no release claim until clean exact subject |

## Intentionally deferred or externally blocked

- Phase 11 implementation if Phase 09 finds no sufficient existing general predication; create a separate ISA-extension project.
- Phase 21 runtime shadow consumption until explicit runtime/SafetyVerifier owner authorization.
- Phase 22 reuse/fast path until a new, separately scoped authorization after Phase 21 evidence.
- Runtime consumption/injection of Phase 23 VDSA/donor-prefetch intent; requires a separate assist-owner plan.
- Broad VMX, SecureCompute, device/lane/runtime activation.
- Compatibility API deletion without an accepted window/external-caller decision.
- Monolithic whole-function constraint solving, unbounded search, a new exact packer and compiler-
  owned PRF/replay/commit/retire state.

## Verified baseline and next phase

Phase 00 is `Verified` on clean subject
`f1de3445dd897bc525d57dfa882fd736799f203d`. The six representative compiler profiles and the
replay, safety, stream-vector and matrix-tile controls have clean exact-subject manifests; repeated
deterministic compiler artifacts are byte-identical and every runtime control has one normalized
outcome fingerprint across three repetitions. Phase 00 tests passed `25/25` and the compiler-focused
suite passed `720/720`. This verification opened Phase 01 only. Phase 01 is now separately
`Verified` on implementation subject `e47bc14c908d51ba039ab285ab97e59a9f7590a6` with evidence in
`evidence/2026-08-25-phase01-resource-model-e47bc14/`; its unified model remains default-off and
the existing exact path remains the decision source. Phase 02 is separately `Verified` default-off
on `f9cf700`; its representative cycle/bundle geomeans are exact `1.00x`, so default enablement is
not authorized. Phase 03 is separately `Verified` in default-off shadow mode on
`0b5f7d2e444dbc7b2658c200bf3bcdaffa874e18`; its immutable topology records known register and
certificate layout while PRF capacities and live bank/channel geometry remain `Unknown`.
The next dependency gate is Phase 04, which remains `Planned` and was not opened under the current
scope. None of these
verifications is default enablement, release, runtime-legality,
execution, publication, commit or retire authority. Missing counters remain `Unavailable`;
historical values remain `HistoricalUnverified` except for explicitly reconciled
group/bundle/width fields.
