# RefPlan5 — dependency-correct HybridCPU compiler platform roadmap

## Purpose

RefPlan5 replaces the planned part of RefPlan4 after architecture/code audit. It preserves the verified scheduling foundation, removes dependency inversions, separates compiler evidence from runtime authority, makes LLVM optional, moves final register allocation after schedule-changing transforms, and decomposes NativeAOT into implementable contracts.

The target is a standalone multi-frontend HybridCPU compiler platform, not an LLVM-owned compiler.

```text
HybridCPU ASM/API -------------------------------+
                                                  |
C/C++/Rust/... -> LLVM IR -> LLVMSharp adapter ---+
                                                  |
C# -> Roslyn/CIL -> CIL/NativeAOT adapter --------+
                                                  v
                                      Canonical HybridCPU IR
                                                  |
                                      HybridCPU.Compiler.Core
                                                  |
                         regions / loops / scheduling / W=8 placement
                                                  |
                             VT / FSP / VDSA planning
                                                  |
                      late RA / frame / lowering / encoding
                                                  |
                         ASM / object / executable / evidence
                                                  |
                                         HybridCPU-v2 runtime
```

`HybridCPU.Compiler.Core` must not reference LLVMSharp, LLVM runtime libraries, Roslyn, NativeAOT or ILCompiler. Native ASM/API compilation must build and run with none of those installed.

## Inherited completed foundation

`00` is the reproducible baseline. `01–03` are the three completed implementation phases. They are preserved as verified foundation and are changed only when an audit correction is required to keep later contracts dependency-correct.

| ID | Phase | Disposition |
|---|---|---|
| 00 | Current state and reproducible baseline | Inherited / Verified |
| 01 | Unified machine resource model | Inherited / Verified |
| 02 | Bounded joint cycle composition | Inherited / Verified |
| 03 | Register groups, PRF ports, banks/channels and certificate resources | Inherited / Verified; retain existing default-off gates |

Do not reimplement existing `HybridCpuLocalListScheduler`, `HybridCpuCycleGroupSearch`, `HybridCpuSlotModel` exact W=8 placement / `SearchStructuralAssignments`, `HybridCpuBundleFormer`, dependency analyzers, lowering/serialization, or current authority/evidence guards.

## Corrected phase graph

Phase 08 is intentionally split into two gates to remove the former Phase 07 ↔ Phase 08 dependency inversion: target register/DataLayout facts must exist before register classes/liveness, while the full ABI/object/platform contract can complete afterwards.

| ID | Phase | Depends on |
|---|---|---|
| 04 | Core modularization and standalone native frontend | 00–03 |
| 05 | Authority, capability, schema and provenance contracts | 03,04 |
| 06 | Frontend-neutral Canonical IR and SchedulingRegion contracts | 04,05 |
| 08A | TargetMachine core: DataLayout/register inventory/primitive ABI | 05,06 |
| 07 | Virtual values, allocation constraints, liveness and pressure | 03,06,08A |
| 08B | Full native ABI, memory model, stack/object/link/platform contract | 05–08A,07 |
| 09 | LLVMSharp toolchain contract and fail-closed importer | 06,08A,08B |
| 10 | LLVM semantic mapping and analysis bridge | 07–09 |
| 11 | LLVM optimization pipeline and semantic firewall | 09,10 |
| 12 | EBB/guarded regions, memory dependence and gated hyperblocks | 03,06,07,08A |
| 13 | Loop canonicalization, distance DAG and HybridCPU MII family | 03,07,08A,08B,12 |
| 14 | Deterministic production modulo/software-pipeline scheduler | 02,03,07,12,13 |
| 15 | Exact scheduling oracle and optimality-gap framework | 03,07,12–14 |
| 16 | Modulo expansion and architecture-driven loop transforms | 07,13–15 |
| 17 | VT-aware and cross-VT scheduling | 03,07,12–16 |
| 18 | FSP donor quality and versioned static evidence | 03,05,07,12,17 |
| 19 | VDSA and donor-prefetch advisory planning | 03,05,12,13,17,18 |
| 20 | Schedule-aware register allocation, spills and frame lowering | 07,08B,12–19 |
| 21 | Restricted C# AOT frontend | 06,08A,08B,20 |
| 22 | HybridCPU managed ABI | 08B,20,21 |
| 23 | ILCompiler/NativeAOT target-enablement seam | 06,08B,20–22 |
| 24 | NativeAOT object/link/runtime integration | 08B,20,22,23 |
| 25 | GC, safepoints, EH/unwind, metadata, helpers and interop | 20,22–24 |
| 26 | RID/runtime-pack/publish qualification | 21–25 |
| 27 | Cross-profile integration, migration and release qualification | selected verified 04–26 |

LLVM phases and architecture-optimization phases may proceed in parallel after their shared contracts are stable. Native mode never waits on LLVM or .NET.

## Non-negotiable invariants

1. `compiler evidence != runtime legality != execution authority != publication != commit != retire`.
2. Runtime remains final authority for `SafetyVerifier`, `LegalityDecision`, dynamic FSP, replay/freshness, execution, publication, commit and retire.
3. Compiler certificates, LLVM facts, profile facts, typed-slot metadata and FSP/VDSA evidence are evidence/hints only.
4. Compiler code cannot mint runtime freshness, epoch, generation, owner, replay validity or execution permission.
5. Profile data changes profitability only; it cannot turn `MayAlias` into `NoAlias`.
6. LLVM optimizers and LLVM MachineScheduler never own HybridCPU scheduling/placement legality.
7. `BundleFormer` materializes a policy-correct schedule; it is not a repair scheduler.
8. Exact W=8 structural placement is one shared primitive reused by local, region, modulo and oracle paths.
9. Final physical/group register allocation occurs after region/loop/modulo transforms and VT/FSP/VDSA planning.
10. Production search decisions use deterministic bounds only: states, candidates, cuts, nodes, iterations, stages and II attempts. Wall-clock limits may guard CI/oracle processes only.
11. Unknown opcode semantics, capability, ABI/schema version, relocation, register class or managed runtime requirement fails closed or falls back conservatively before emission.
12. Stage A class admission and Stage B lane materialization are runtime/backend mechanisms; the compiler may provide class/lane compatibility evidence but cannot claim dynamic admission or materialization authority.
13. Replay/freshness/publication/commit/retire state is never serialized as compiler-created authority.

## Modulo-scheduling contract

```text
ProvenLowerBoundII = max(
  RecMII,
  SlotMII,
  PRFReadPortMII,
  PRFWritePortMII,
  RegisterGroupMII,
  MemoryBankMII,
  MemoryChannelMII,
  Lane6MII,
  Lane7MII,
  CertificateMII)

ChosenII >= ProvenLowerBoundII
AND FeasibleModuloPlacement(ChosenII)
```

Each MII component uses the same typed status lattice as Phase 13:

```text
MiiComponentResult =
    Proven(value, proofInputs, targetDigest)
  | NotApplicable(reason)
  | Unknown(reason)
  | Unsupported(reason)
```

`ProvenLowerBoundII` is usable only when every required component is `Proven` or `NotApplicable`, or when a separately proven conservative bound explicitly covers an otherwise unknown fact. `Unknown`/`Unsupported` are never silently treated as zero. `max(MII)` is a proven lower bound, not a feasibility proof. Production uses deterministic SDC/difference constraints plus lazy discrete resource/placement refinement and shared exact W=8 placement. An SMT-solver backend is optional; an exact solver is CI/research oracle and measures production optimality gap.

## Shared phase gate

Every new phase must specify versioned inputs/outputs, mechanisms to reuse, authority boundary, deterministic work bounds, fail-closed/default-off fallback, positive/property/negative-control tests, telemetry that cannot feed legality, KPI acceptance, rollback, and provenance tied to compiler commit, HybridCPU-v2 contract revision, machine-model digest, ABI/schema versions and options/profile hashes.

Every schedule-changing transform must invalidate/recompute all affected dependency, MII, liveness, pressure, resource and placement facts. No stale proof/evidence may survive a mutation unless its schema explicitly proves preservation.

Cross-document consistency is itself a gate: README summaries may not collapse, weaken or reinterpret a stricter status lattice, authority boundary, fallback rule or acceptance condition defined by the owning phase. The owning phase document is normative when it is more specific; any summary mismatch must be corrected before release qualification.

## Cross-cutting release gates

- **Determinism:** identical input/toolchain/capability/profile hashes produce byte-identical scheduling decisions, evidence and object metadata where timestamps are excluded by contract.
- **Fail-closed:** unknown legality-relevant facts reject or select a conservative verified fallback before emission.
- **Native independence:** `HybridCPU.Native` build/test must pass with LLVM/.NET frontend assemblies and native LLVM libraries absent.
- **Authority:** runtime SafetyVerifier/LegalityDecision remains authoritative even for fully certified compiler output.
- **Compatibility:** ABI/evidence/capability schemas have explicit version negotiation and migration/rejection policy.
- **Optimization quality:** production scheduler quality is measured against deterministic baselines and, on bounded CI/research kernels, the exact oracle; SafetyVerifier reject-rate is classified diagnostically, never optimized by weakening checks.

## Product profiles

- **HybridCPU.Native** — ASM/API -> Canonical IR -> schedule/bundles -> ASM -> object/binary where supported -> evidence -> scheduling-report -> provenance; no LLVM/.NET dependency.
- **HybridCPU.LLVM** — `.ll`/`.bc` -> LLVMSharp adapter -> same core.
- **HybridCPU.DotNetAot.Restricted** — restricted CIL subset with deterministic rejection of unsupported managed semantics.
- **HybridCPU.DotNetAot.Managed** — managed ABI + NativeAOT runtime/GC/EH/interop after separate qualification.
- **HybridCPU.Full** — all verified frontends plus explicitly enabled VT/FSP/VDSA capabilities.

Common outputs should include `ir`, `schedule`, `bundles`, `asm`, `object`, `binary`, `evidence`, `scheduling-report` and `provenance`.
