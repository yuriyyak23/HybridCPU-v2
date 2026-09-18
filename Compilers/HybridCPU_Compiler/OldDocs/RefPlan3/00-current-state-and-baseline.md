# 00 — Current state, invariants and reproducible baseline

## Goal and motivation

Freeze an attributable current-state model and create a correctness-neutral measurement harness before changing scheduling decisions. This phase converts historical/untracked observations into repeatable evidence tied to an exact source manifest, input corpus and profile fingerprint.

**Status:** `Verified` on clean pinned subject
`f1de3445dd897bc525d57dfa882fd736799f203d` as of `2026-08-19`. This is compiler
qualification evidence, not runtime or release authority.

## Confirmed starting state

### Repository and history

- Base SHA: `d4eeec71fa7d9e3a0efa8042dc667422f46181ac`, branch `refactor/compiler-core-authority-boundaries`.
- The initial dirty-worktree capture is retained under `evidence/` as provenance only; it is not a release subject.
- Local history shows the scheduling/bundling foundation entered at `6503318978b6bc6676d67408354648ce6f439118`, was materially aligned in `05a8143a95a84d2e0a9afd8ff475d4450fe0c6cb`, and had a later opcode-semantics correction in `ce3d25d12d514e44eafad41021fd1e44da356c59`.
- Established production-lowering provider, parity, dispatch and caller-migration contracts are inputs; RefPlan3 does not reopen them.
- The blocker register records `NextOpenPool=None`, default-disabled exact VMX compiler emission, compatibility-window gaps, and dirty-subject evidence gaps. Scheduling-internal compiler work is permissible only when it does not open a semantic/runtime contour.

### Actual pipeline reconciliation

| Stage | Canonical object and owner | Current algorithm | Hard constraints vs heuristics | Consumers | Existing coverage | Debt / do not duplicate |
| --- | --- | --- | --- | --- | --- | --- |
| IR/CFG | `IrProgram`, `IrInstruction`, `ControlFlowGraph`, `IrBasicBlock`; `HybridCpuIrBuilder` | Decode encoded instruction stream, normalize operands/defs/uses/effects, form leaders/blocks/edges | Hard: opcode/ABI validation, exact virtualization ingress denial, source/target validity. Heuristic: none | dependency analyzers, decomposition, scheduler | compiler emission, control-flow, provider and negative-matrix tests | Keep canonical builder and exact closed-world ingress; do not create a parallel IR |
| Dependence graph | `IrProgramDependencyGraph`; `HybridCpuProgramDependencyAnalyzer` | per-block pair analysis plus precise register RAW/WAR/WAW; memory must/may; control/serialization; separate inter-block carry graph | Hard conservative edges; domain/capability pruning only from static facts | DAG builder; schedule result retains graph | `CompilerV4LsuTruthTests`, compiler integration; sparse direct graph-property coverage | Inter-block graph exists but local scheduler does not schedule across it; no distance edges |
| Scheduler DAG | `IrBasicBlockSchedulingDag`, `IrSchedulingNode`; `HybridCpuBasicBlockSchedulingDagBuilder` | one DAG per basic block; reverse critical-path computation | Hard dependency/latency; deterministic instruction index fallback | local and program-order schedulers | indirect scheduler tests | No first-class scheduling region; current `ParallelRegionInfo` is decomposition, not scheduling |
| Candidate readiness | dictionaries of remaining dependency counts/ready cycles plus sorted `IrSchedulingNode` list; local scheduler owns | dependency count zero and `readyCycle <= currentCycle`; includes zero-latency same-cycle successor discovery | Hard readiness; priority is heuristic | `BuildCycleGroup` | flexibility, bank/profile and determinism tests | Ready-window size/cap-hit telemetry absent |
| Cycle-group selection | `IrScheduleCycleGroup`; `HybridCpuLocalListScheduler.BuildCycleGroup` | greedy repeated `SelectBestCycleCandidate`; optional early slack/load stop | Hard append feasibility through `AnalyzeCandidateBundle`; heuristic lexicographic fields and stable instruction index | `IrBasicBlockSchedule`, bundler | selected heuristic tests; no frozen adversarial greedy-gap corpus | Main confirmed gap: no comparison of complete membership alternatives; many profile features default-off |
| Structural legality | `IrCandidateBundleAnalysis`; `HybridCpuInstructionLegalityChecker`, `HybridCpuClassCapacityChecker`, `HybridCpuStructuralResourceModel` | pair hazards, class capacity/alias, structural units, W=8 slot existence | Hard compiler structural evidence only; never runtime legality | scheduler, bundler, agreement | V4/V5, provider parity, negative matrices | Class-capacity comment says advisory while hazards make it blocking; no unified footprint/state API |
| Slot/resource placement | `IrBundlePlacementSearchResult` and related pair/triplet/block/program results; `HybridCpuSlotModel` | exact enumerate legal W<=8 assignments, Pareto frontier, deterministic transition-quality selection; DP-like global state per current bundle placement | Hard structural masks; profile/certificate pressure only tie-break when explicitly enabled | `HybridCpuBundleFormer` | integration and API-surface tests; limited direct optimality/property corpus | Already implemented—do not replace. Early compiler masks can diverge from runtime physical class topology |
| Bundle formation | `IrProgramBundlingResult`, `IrMaterializedBundle`; `HybridCpuBundleFormer` | membership immutable; global <=96 bundles, block lookahead <=64, pair/triplet/local fallback; optional class-first then exhaustive fallback | Hard revalidation and exact assignment; heuristic placement quality only | agreement, lowerer, relocation | compiler integration/parity | Must not become a hidden rescheduler or policy repairer |
| Compiler evidence | `IrAdmissibilityAgreement`, `TypedSlotBundleFacts`, `StealabilityVerdict`; `HybridCpuBundleBuilder` | typed facts validation, safety-mask diagnostic, advisory stealability analysis | Typed facts are structural evidence; stealability mismatch advisory | compiled program/envelope adapter/diagnostics | V5 contract tests, parity harness, authority-negative tests | No structured donor certificate/version/epoch binding; current boolean hint is weak |
| Lowering/emission | `VLIW_Bundle`, `VliwBundleAnnotations`, `HybridCpuCompiledProgram`; lowerer/serializer/canonical compiler | materialized slots -> NOP-filled W=8 bundles -> relocation -> 256-byte serialization; publish image and annotations | Exact carrier/ABI constraints; no runtime permission | main memory/cache/fetch/decode; production-package adapter | emission inventory, golden artifacts, provider tests | Preserve the established six-provider dispatcher and exact no-fallback contours |
| Runtime ingress | image + `VliwBundleAnnotations`; memory/cache/fetch/canonical decoder | main memory stores annotation carrier by address; cache reads it; fetched decode fails closed when required annotation/decode context is absent | Runtime transport validation hard; compiler bridge acceptance is not execution | decoded bundle state and FSP scheduler | fetch/decode, bridge authority-negative, MatrixTile/DSC/L7 tests | `CompilerRuntimeBridge` is evidence ingress, not the live legality owner |
| Runtime legality/FSP | `MicroOp`, `SlotClassCapacityState`, `BundleResourceCertificate4Way`, replay/boundary state; `MicroOpScheduler`, `IRuntimeLegalityService`, `SafetyVerifier` | Stage A capacity -> explicit runtime legality -> dynamic outer caps; Stage B hard pin or deterministic free-lane materialization; FSP donor scan/injection | All live legality, owner/domain/replay/resource checks hard and runtime-owned | execution/backend/completion/retire | extensive Stage A/B, FSP, replay, certificate and SafetyVerifier tests | Compiler may predict and emit evidence only; never own PRF/rename/commit/retire |

### Greedy-gap verdict

The research conclusion is confirmed with one precision correction:

1. `BuildCycleGroup` chooses membership greedily before bundle materialization.
2. Every tentative append calls `AnalyzeCandidateBundle`, whose `AnalyzeStructuralAssignment` performs exact backtracking until it finds one feasible slot assignment. Thus scheduler membership is not blind to slot feasibility.
3. The scheduler does **not** call `SearchStructuralAssignments`, compare Pareto placement candidates, consider transition quality, or backtrack over an earlier membership choice.
4. `HybridCpuBundleFormer` later calls the exact enumeration/search machinery after cycle membership is immutable.

Therefore the gap is “greedy membership with exact existence checks before cost-aware exact placement,” not “no exact placement.” Phase 02 must reuse the existing search.

### Implemented mechanisms that RefPlan3 must not re-plan

- W=8 slot mask and exact assignment machinery.
- Pareto placement selection and deterministic tie-breaks.
- Adjacent pair/triplet, basic-block and program placement lookahead.
- `HybridCpuBundleFormer` materialization contract.
- Basic-block RAW/WAR/WAW/memory/control/serialization dependency foundation and inter-block carry graph.
- Class-capacity, SystemSingleton/branch alias and exclusive-cycle checks.
- Compiler typed-slot evidence, safety-mask diagnostics and advisory `StealabilityHint`/`HybridCpuStealabilityAnalyzer`.
- Six exact normal production providers, package/golden/parity gates, canonical dispatch and local caller migration.
- Runtime W=8 Stage A/Stage B, `BundleResourceCertificate4Way`, FSP/replay, `SafetyVerifier`, PRF/rename/backend/retire ownership.

### Research claims corrected or still unproven

| Claim | Reconciled disposition |
| --- | --- |
| exact slot placement already exists | Confirmed for compiler structural masks; it is not runtime lane authority and its early mask topology is intentionally different in places |
| cycle membership is selected before exact placement | Confirmed, with exact feasibility already checked incrementally |
| rich profile-guided scheduler is current behavior | Mechanisms exist, but flags default false; canonical `CompileProgram` supplies no profile and uses defaults |
| register-group rejects and memory stalls are present | Supported by untracked historical snapshot; must be reproduced on exact subject |
| register-group/PRF/bank topology is compiler modeled | False beyond coarse structural resources and profile scores; symbol scan found no compiler `RegisterHazardMask`, PRF port or bank/channel footprint model |
| regions, oracle, modulo scheduling, MII, renaming, donor certificates exist | False outside research prose; exact symbol scan found no implementations |

## In scope

- Exact source/worktree manifest and evidence attribution.
- Frozen synthetic and representative benchmark corpus.
- Versioned compiler scheduling metrics, deterministic fingerprints and comparison tooling.
- Baselines for correctness, schedule, placement, runtime and compile resources.
- Static guards for global authority/non-goal invariants.

## Explicit non-goals

- No scheduler/resource/region behavior change.
- No new provider, opcode, VMX/SecureCompute/device/runtime contour.
- No runtime metadata adoption or fast path.
- No compatibility API removal.
- No claim that historical results belong to current HEAD/worktree.

## Canonical components affected by the future implementation

- `HybridCpuCanonicalCompiler`, `HybridCpuLocalListScheduler`, `HybridCpuBundleFormer` as observation points only.
- New `CompilerScheduleMetricsV1`, `CompilerScheduleFingerprintV1`, `CompilerBenchmarkManifestV1` under compiler telemetry/evidence namespaces.
- Existing `IrProgramSchedule`, placement search summaries, `IrProgramBundlingResult`, `HybridCpuCompiledProgram` read-only projections.
- `CompilerEvidenceSnapshotSerializer` only if a separate compiler-evidence envelope is needed; do not mutate production authority semantics.

## Target data flow

```text
exact source manifest + input bytes + sideband + profile hash + options
  -> unchanged compiler pipeline
  -> read-only schedule/resource/bundle observations
  -> canonical ordered CompilerScheduleMetricsV1
  -> deterministic JSON + artifact/schedule fingerprints
  -> baseline comparator
```

Telemetry collection must not enter readiness, legality, priority, placement or lowering decisions.

## Invariants and authority boundaries

- Baseline instrumentation is observational: it cannot change readiness, cycle membership, placement, bundle bytes, annotations or runtime behavior.
- Identical source/input/profile/model/options must produce byte-identical schedules, bundles, evidence and ordered metrics.
- W=8 typed bundles, SystemSingleton, serialization, hard-pinned lanes and no-cross-contour rules remain fixed baseline constraints.
- Profile facts are recorded for profitability attribution only and cannot participate in legality.
- Runtime Stage A, SafetyVerifier, Stage B, replay, execution, publication, commit and retire remain the final authorities; compiler evidence is never an admission claim.

## Dependencies

None. A clean/reviewable compiler-only subject is an execution precondition, not a dependency that this plan is allowed to create by reset/commit.

## Implementation backlog

1. Define a manifest schema with base SHA, dirty-state policy, exact file hashes, build configuration, runtime contract version, input/profile/options hashes and tool version.
2. Freeze synthetic DAG cases: independent wide set, RAW chain, zero-latency chain, greedy counterexample, lane6/lane7 choke, SystemSingleton, serialization, memory must/may, register WAR/WAW, and no-placement cases.
3. Freeze representative program inputs for `alu`, `novt`, `vt`, `max`, `lk`, `bnmcz`, replay, safety, stream-vector and matrix-tile; distinguish compiler scheduling inputs from runtime repetitions.
4. Add read-only metrics for per-block/region ready-window distribution, cycle groups, bundle count, widths, scheduler fallback, placement evaluated/Pareto/transition counts, compile elapsed time and peak memory.
5. Add stable instruction/schedule/bundle fingerprints ordered by block, cycle, order, instruction index and physical slot.
6. Add comparator output with absolute/relative deltas and first divergence.
7. Capture `Documentation/AsmAppTestResults3.md` only as `HistoricalUnverified` until reproduced.
8. Record environment limitations and unavailable counters explicitly; never encode unavailable as zero.
9. Add static source guards for forbidden authority names/paths and profile-to-legality calls.
10. Run focused and full qualification only on the exact subject and archive command, exit code and artifacts.

## Migration and compatibility

- Metrics are optional, side-effect-free and versioned; default compiler return types remain unchanged in the first slice.
- Prefer observer/sink injection or a separate result projection over adding required public API fields.
- Unknown metric fields are ignored by readers; schema version mismatch is explicit.
- Existing progress observer strings are not repurposed as a machine-readable ABI.

## Tests

### Positive and property tests

- Same input/profile/options yields identical schedule, bundle, image, annotations and metrics JSON across 100 repetitions.
- Metrics counts reconcile with `IrProgramSchedule`, `IrProgramBundlingResult` and serialized image length.
- Property-generated W<=8 cases preserve placement/member fingerprints with metrics on/off.

### Negative tests

- Missing/corrupt profile is recorded as absent/ignored and produces baseline schedule.
- Unknown metric version, missing manifest hash and unavailable counter fail comparison, not compilation.
- Dirty/unattributed subject cannot be labelled release baseline.

### Static tests

- Metrics types expose no `IsAllowed`, `ExecutionReady`, `Commit`, `Retire`, authority or capability claim.
- No telemetry read occurs from legality/resource reservation methods.
- `BundleFormer` membership hash before/after materialization is equal.

## Benchmarks and KPI gates

- Baseline all KPIs listed in the README, including `schedule_cycles`, bundles, both widths, ready window, placement search counts, compile time/memory and runtime cycles/IPC/stalls/rejects.
- Phase acceptance target is not a speedup: it is exact reproducibility and complete attribution.
- Required: 0 semantic/image/schedule delta with metrics enabled; 100% metric reconciliation; coefficient of variation for deterministic compiler counts = 0; runtime timing values report median and dispersion.
- Historical snapshot delta is reported, never silently accepted.
- Historical `Compiler schedule cycle groups` are non-empty groups/bundles, not makespan
  `schedule_cycles`. The reproduced current makespans are tracked separately so latency holes are
  not collapsed into bundle counts.

## Diagnostics and telemetry

Every record includes schema, run ID, source manifest hash, input hash, profile hash/absence, options hash, block/region ID, fallback/cap reason and evidence quality (`Measured`, `HistoricalUnverified`, `Unavailable`).

## Bounded-search, timeout and fallback policy

No new search is introduced. Measurement buffers are bounded by instruction/block counts and may aggregate distributions. If a sink fails, compilation either continues with an explicit telemetry-failure diagnostic or fails only in strict benchmark mode; emitted output remains identical.

## Risks and forbidden shortcuts

- Do not rebuild or run qualification against an unattributed mixed worktree and call it release evidence.
- Do not use width/occupancy as the only primary metric.
- Do not infer bank conflicts from missing runtime counters.
- Do not alter scheduler ordering while “adding telemetry.”
- Do not remove or rename compatibility APIs.

## Rollback and kill switch

Disable/remove the optional metrics sink. The no-sink path must be byte-for-byte the original pipeline. A runtime kill switch is neither required nor allowed because this phase emits no runtime artifact.

## Acceptance and merge gates

- Exact compiler-only file manifest and reviewed dirty-state disposition.
- All positive/negative/property/determinism/static tests pass.
- Metrics on/off output equivalence is exact.
- Six representative profiles and required runtime negative controls are captured with commands and hashes.
- Markdown/JSON validation and `git diff --check` pass.
- No file outside the approved implementation slice changes.

## Status criteria

- `implemented`: versioned metrics/corpus/comparator exist without behavior change.
- `verified`: exact equivalence, determinism, reconciliation and representative baseline evidence pass on one attributable subject.
- `default-enabled`: metrics may be enabled by default only if compile-time/memory overhead stays <=1% and output remains exact; otherwise remain opt-in.
- `release-authorized`: a separate release record names SHA, manifests and results; implementation or tests alone do not authorize release.

## Residual work and next gate

Phase 00 evidence includes versioned deterministic schedule metrics,
separate compile-resource telemetry, exact source-manifest construction, a fail-closed comparator,
the frozen synthetic/profile inventory, 100-run determinism, byte-equivalence and static authority
isolation tests. The clean-subject capture and compiler-focused suite satisfy `verified`, but not
`default-enabled` or `release-authorized`.

The six compiler profiles are now reproduced in
[`evidence/2026-08-19-phase00-current-dirty-attributed`](evidence/2026-08-19-phase00-current-dirty-attributed/README.md):
the deterministic report hashes 1,292 exact source files and has SHA-256
`64fb8cf54d74935088cd50ff0b0431b4bd3968a620d276e763c56f79d4330360` across repeated captures.
Measured makespans are `75/75/48/49/48/48`; historical cycle-group/bundle/width values reproduce
with zero delta. The subject remains `DirtyAttributed`, not release evidence.

The required runtime controls are archived in
[`evidence/2026-08-19-phase00-runtime-controls-dirty-attributed`](evidence/2026-08-19-phase00-runtime-controls-dirty-attributed/README.md).
Replay, safety, stream-vector and matrix-tile each have three raw repetitions, separate physical
timing distributions and a common normalized outcome fingerprint across repetitions. The
normalization removes only timestamps, elapsed values and rates derived directly from elapsed
time; correctness, checksums and architectural counters remain fingerprinted.
The deterministic control report has SHA-256
`bf07ee0e8a2d4f257980cc23b387fe48d3494885577579b4ea8268ac2bb13424` and binds 1,397 exact
source files through manifest fingerprint
`c8050281fb26c9241399348c9a0535205a002415ef04ff120f2ab61a0d8b8bb0`.

The clean compiler capture is archived in
[`evidence/2026-08-19-phase00-clean-f1de344`](evidence/2026-08-19-phase00-clean-f1de344/README.md).
It binds 1,284 exact source files with manifest fingerprint
`d99abb7554314b4784aa5e0d09e485f63ec24355bfdaf2d97610ebfc51a42599`; two captures produced
byte-identical deterministic artifact SHA-256
`cad4465ed108269aef19ab0091db6bd413437e2ebabb049f2cceb0c4dbe6ea63`.

The clean runtime controls are archived in
[`evidence/2026-08-19-phase00-runtime-controls-clean-f1de344`](evidence/2026-08-19-phase00-runtime-controls-clean-f1de344/README.md).
All four profiles passed three repetitions with one normalized fingerprint per profile. The
deterministic report SHA-256 is
`2456aeca559483568e01745979a1ebded2ee9c2eb40d8e7b23a3d2fcd332b2cd`.
Phase 00 tests passed `25/25`; the exact-subject compiler-focused suite passed `720/720`.
Historical snapshot values and unavailable counters remain explicitly non-zero-substitutable
evidence. The exact next admissible work is Phase 01 contracts/extraction/default-off shadow mode.
