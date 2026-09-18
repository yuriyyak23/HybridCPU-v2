# 27 — Cross-profile integration, migration, benchmarking and release qualification

## Goal

Qualify the compiler platform as a versioned product across native, LLVM and .NET profiles, including migration and measurable optimization quality.

## Qualification matrix

Run independently for `HybridCPU.Native`, `HybridCPU.LLVM`, `HybridCPU.DotNetAot.Restricted`, `HybridCPU.DotNetAot.Managed` and selected `HybridCPU.Full` capability sets. Success in one profile grants no authority to another.

Each release row is identified by:

```text
(profile,
 compiler commit,
 HybridCPU-v2 target/runtime revision,
 machine/topology digest,
 target/managed ABI versions,
 evidence/capability schema versions,
 frontend/toolchain versions,
 deterministic optimization options,
 profile-data hash/absence,
 runtime-pack/linker identity)
```

## Required gates

- deterministic build/codegen/evidence provenance;
- ABI, object, evidence and capability schema compatibility/migration tests;
- compiler/runtime architecture-version mismatch tests;
- native no-LLVM/no-.NET dependency/loadability test;
- LLVM version/API-surface compatibility and semantic-firewall tests;
- NativeAOT pinned-baseline/patch-set compatibility test;
- authority-negative suite proving compiler evidence cannot become runtime legality/freshness/execution/publication/commit/retire permission;
- Phase 00 corpus plus region/loop/VT/FSP/VDSA and managed benchmark suites;
- compile-time/peak-memory budgets and runtime cycles/IPC/stall/resource metrics;
- exact-oracle optimality gap for bounded scheduling/modulo corpus;
- profile-guided profitability A/B tests with legality held invariant;
- rollback/kill-switch exercise for every default-enabled optimization;
- feature/schema downgrade/upgrade/migration tests and explicit rejection outside the compatibility window;
- corrupted/stale evidence and runtime-pack mismatch negative controls;
- clean fallback verification after each optimization/frontend is disabled;
- **cross-document roadmap-contract consistency check** proving README summaries do not weaken the normative contract of their owning phase;
- **Phase 13 MII summary parity check** proving README exposes the complete `Proven | NotApplicable | Unknown | Unsupported` status lattice and never treats `Unknown/Unsupported` as zero;
- **Phase 04 native-profile artifact parity check** proving README keeps the full standalone artifact path `Canonical IR -> schedule -> bundles -> ASM -> object/binary where supported -> evidence -> scheduling-report -> provenance` and preserves no-LLVM/.NET loadability.

## Roadmap contract consistency gate

The top-level `RefPlan5/README.md` is a navigation/summary document, not a weaker alternate specification. Before release qualification, mechanically or review-procedurally compare every cross-cutting README summary against the owning phase documents.

At minimum the gate must verify:

1. **MII contract parity with Phase 13**
   - the status lattice is exactly `Proven | NotApplicable | Unknown | Unsupported` unless Phase 13 itself is versioned to a new lattice;
   - `Unknown` and `Unsupported` are never summarized as zero, free capacity or implicit `NotApplicable`;
   - `ProvenLowerBoundII` is usable only when every required component is proven/not-applicable or a separately proven conservative bound explicitly covers the unknown fact;
   - `ChosenII >= max(all required proven MII components)` remains necessary but not sufficient, and exact modulo placement/resource feasibility remains mandatory.

2. **Standalone `HybridCPU.Native` parity with Phase 04**
   - the public profile exposes the same core artifact chain: Canonical IR, schedule, bundles, ASM, object/binary where supported, evidence, scheduling report and provenance;
   - none of those artifacts may require LLVMSharp, LLVM native libraries, Roslyn, ILCompiler or NativeAOT implementation assemblies to be installed or loaded;
   - optional frontends may add ingress paths, but cannot make richer compiler artifacts exclusive to LLVM/.NET profiles.

3. **Authority/fallback parity**
   - README must not weaken runtime ownership of `SafetyVerifier`, `LegalityDecision`, Stage A admission, Stage B materialization, replay/freshness, dynamic FSP, execution, publication, commit or retire;
   - README must not omit deterministic bounds or introduce wall-clock-driven production decisions where the owning phase forbids them;
   - README must not describe compiler evidence/profile facts as legality authority.

4. **Dependency graph parity**
   - the published phase graph must retain `05/06 -> 08A -> 07 -> 08B` and the later phase dependencies unless the owning phase files are changed in the same reviewed change;
   - no summary may reintroduce the former Phase 07 ↔ Phase 08 cycle or move final RA before schedule-changing transforms.

Any mismatch is a release-blocking documentation-contract defect. The owning phase document is normative when it is more specific; fix the summary or update the phase contract in the same reviewed change. Do not waive a mismatch merely because implementation tests pass.

## KPI framework

### Correctness / authority — hard gates

- zero known semantic miscompiles on qualified corpus;
- zero accepted invalid exact W=8 placements;
- zero runtime SafetyVerifier/LegalityDecision bypasses;
- zero profile-driven legality changes;
- zero compiler-created freshness/epoch/replay/execution authority;
- zero stale analysis/proof reuse detected after code mutation.

### Scheduling quality

Report separately by BB/region/loop/VT mode:

- schedule cycles and emitted bundles;
- productive width and exact-placement failure reasons;
- binding resource/MII components;
- PRF/group/bank/channel/lane6/lane7/certificate pressure;
- spill/reload/rematerialization counts and RA repair stages;
- modulo `ChosenII`, proven lower bound and infeasible-II reason distribution;
- production-vs-oracle `II_gap`, relative gap and comparable objective gap on bounded kernels.

An oracle `Unknown` case is excluded from optimality claims rather than counted as optimal.

### Runtime quality

- total cycles, IPC and relevant stall categories;
- Stage A/Stage B/replay/FSP diagnostics classified by cause;
- FSP: `accepted/eligible donors`, successful injects, useful reclaimed slots and cycles saved;
- VDSA: useful latency hidden, rejected/suppressed hints and interference/resource cost;
- managed profiles: GC pause/work metrics, safepoint/stack-walk correctness, EH/interop transition cost where enabled.

Raw SafetyVerifier/reject-count reduction is **not** a target by itself. A change that lowers rejects by weakening evidence/checks or reducing useful work fails regardless of the counter.

### Compiler cost / determinism

- compile-time and peak memory;
- deterministic production work units: states/candidates/cuts/nodes/iterations/stages/II attempts;
- code size/object size;
- repeat-build schedule/evidence/object/image fingerprints;
- cache hit/miss keyed by all semantics-affecting contract/provenance digests.

Wall-clock measurements are performance telemetry only. They never decide production search output.

## Baselines and acceptance methodology

Every optimization has:

1. a bit-identical or semantically equivalent verified fallback;
2. a frozen adversarial correctness corpus;
3. a representative performance corpus;
4. declared per-case and aggregate regression thresholds;
5. at least two attributable evidence runs before default enablement;
6. an explicit kill switch exercised in CI/release qualification.

Benchmark selection and metric aggregation are versioned. Changing the corpus/weights requires a new qualification record; historical regressions cannot disappear through benchmark-set drift.

## Migration / compatibility

- Support windows are explicit per ABI/evidence/capability/runtime-pack schema.
- Unknown legality-relevant major versions fail closed.
- Migration emits new provenance and cannot elevate compiler evidence to runtime authority.
- Cached schedules/objects/evidence from incompatible target/model/ABI/toolchain versions are rejected, not “best-effort” reused.
- `HybridCPU.Native` compatibility is tested independently from LLVM/.NET package availability.

## Release artifacts

Emit a release manifest with source/toolchain versions, target architecture revision, machine/topology digest, DataLayout/ABI/object/evidence schema versions, enabled capabilities, benchmark summary, oracle-gap summary, deterministic-work budget summary, runtime-pack/ILCompiler baseline, migration window and known unsupported features.

Also emit a machine-readable feature matrix so users/tooling can determine whether a binary requires region scheduling, modulo scheduling, VT/FSP/VDSA, GC, EH, TLS, P/Invoke or other managed capabilities without interpreting prose release notes.

## Acceptance

No P0/P1 correctness/authority regressions; deterministic counts/fingerprints pass; declared runtime/compiler/code-size/optimality gates pass; compatibility window is explicit; every enabled feature has a safe deterministic fallback; all required negative controls pass; and the roadmap-contract consistency gate passes with no README↔phase semantic mismatch. Wall-clock watchdog outcomes never alter production code selection.

**Release principle:** feature verification is evidence for that feature/profile only; runtime remains final authority for legality, replay/freshness, execution, publication, commit and retire.
