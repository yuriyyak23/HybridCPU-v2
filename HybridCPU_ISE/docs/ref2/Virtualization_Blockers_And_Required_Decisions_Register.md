# HybridCPU-v2 Virtualization: blockers and required decisions register

- Status: `MACHINE-CURRENT DECISION REGISTER / NO AUTHORITY GRANT`
- Baseline date: `2026-08-13`
- Baseline HEAD: `87f1f5884d35a66ce24c905eab9fa18198d4017a`
- Baseline tree: `f7e47eaea108886e8058d30d3b334d94d4c6c534`

## Purpose and precedence

This register summarizes the blockers and owner decisions that must be closed
before any further expansion of the virtualization refactoring plan. It is a
governance/readiness document only. It does not authorize runtime behavior,
change `NextOpenPool`, accept an RFC/ADR, enable a profile, or make VMX/VMCS an
authority source.

Current state is determined only by
[`VirtualizationActivationStatusV1.json`](VirtualizationActivationPlan/VirtualizationActivationStatusV1.json).
If this register conflicts with that machine-readable status, an accepted
owner-specific decision, or current production code, those sources take
precedence. Historical phase sections are evidence and do not become current
state merely because they still contain `Blocked...` wording.

Canonical production code is `HybridCPU_ISE/CloseToHSL`. VMX/VMCS remains a
frozen compatibility ABI and projection vocabulary. Runtime authority remains
with the neutral domain, owner, policy, admission, execution, completion and
retire contours.

## Current stop condition

Phase 58 closed production construction/reachability and rollback for the
already-proven exact `PROBE_NO_STATE_V1` contour. Phase 59 then used the
repository owner's ordered authorization to release that one existing profile
against runtime subject `e29d6b2150bf10c136ba3897eb17a9cab03c9967`, tree
`b3cd41275e548dc39fc7c0c7c5d411c339a0f29e`.

The release remains explicit and default-disabled. Phase 60 then selects the
first production-reachable existing exact read-only VMREAD profile after
excluding descriptor-owned profiles whose configuration callers remain
test-only. The released profile is only Phase-55 `ExitReason` plus reason-bound
`ExitQualification`, sourced from the exact committed neutral completion
snapshot. Runtime authority stays in the existing neutral owners; both release
records are evidence only.

Phase 61 then used the repository owner's exact `PinBasedControls` governance
authorization. Its original E0 found neither a production construction/caller
for the candidate policy nor an owner-approved per-bit equivalence map. The
separately authorized Phase 62 correction now closes only the first defect:
canonical CPU-core construction issues an immutable snapshot through
`CompatibilityControlPolicyOwner`, and production interrupt dispatch consumes
the current owner/domain/generation-bound neutral event-routing policy. Phase
63 now freezes the exact HybridCPU VMX8 bit decision: bits 0..2 map to the three
named neutral predicates and bits 3..63 are unsupported legal zero. Phase 64
repeats E0 from that real graph and accepts the immutable read-only D2 mapping.
Phase 65 implements the separately authorized exact production scalar-delivery
composition through the existing single-use receipt and canonical register
writeback/retire contour. It remains explicit and default-disabled; no release
claim was authorized. Machine-current is:

```text
NextOpenPool = None
NextCandidatePool = NoneUntilSeparateOwnerAuthorizationForNextExactFieldOrOperation
```

Therefore the only active hard blocker is **B0** below. None of the later
surface-specific entries is automatically selected or authorized.

## Active blocker

### B0 — no next exact field or operation is authorized

**Blocking fact.** Phase 66 releases the exact Phase 65 `PinBasedControls`
profile through immutable default-disabled construction. It does not select any
later compatibility field or operation.

**Decision required.** A separate repository-owner authorization must identify
one next exact field or operation and its bounded E0/D2/release scope.

**Exit.** A future pool opens only after that new exact owner decision.

**Forbidden workaround.** Do not infer bits from enum ordinals, schema
membership, adjacency, default zero, raw VMCS state, frontend behavior, or test
construction. Do not open `SecondaryProcControls` or another field.

## Independent future decision gates

The following entries are not currently open pools. Each remains denied or
default-disabled until separately selected and accepted.

### B1 — activation and release of existing default-off exact profiles

**Current fact.** Exact VMCALL `PROBE_NO_STATE_V1` and the exact Phase-55
completion reason/qualification VMREAD profile each have one bounded production
release claim and remain explicitly default-disabled. Other VMREAD scalar
delivery contours have no release claim. Broad activation is denied.

**Decisions required.** A release/operations owner must decide separately for
each exact profile:

- whether production activation is desired;
- the immutable configuration and construction/bootstrap caller;
- deployment scope, rollback/kill switch, drain and quiescence rules;
- release-candidate SHA and evidence retention;
- whether compiler emission remains disabled.

**Required proof.** Real non-test configuration reachability, disabled-path
equivalence, activation/rollback races, cross-domain isolation, clean-source
VMX and SecureCompute matrices, hook-free Release and SHA-bound evidence.

**Stays denied.** Broad VMX activation, automatic enabling of all closed
contours, or the claims `Virtualization complete`, `VMX fully activated`, and
`Broad VMREAD activation`.

### B2 — any new VMCALL operation or leaf

**Current fact.** Only namespace `HybridCPU.VMCALL.Runtime.v1`, leaf `0x0001`,
operation `PROBE_NO_STATE_V1` has a closed exact D2/O1/E1–E7 chain. Closing it
does not allocate another leaf or authorize another backend.

**Decision required.** An owner-specific RFC/ADR must define the exact leaf,
operand/result ABI, neutral backend owner, capability policy, evidence,
cancellation/replay, completion, retire and migration semantics.

**Required proof.** Fresh E0; immutable SpecV2 and later attributable
AcceptanceRecordV2; exact operand capture; live E2; owner execution receipt;
exclusive E4 composition; separate E5/E6; E7; adjacent-leaf negatives;
activation and rollback proof.

**Stays denied.** Generic VMCALL backend, compatibility-exit authority,
caller-created completion, reuse of the existing leaf’s receipts, or compiler
metadata as runtime authority.

### B3 — additional VMREAD fields, host aliases or broad field admission

**Current fact.** Only previously accepted exact field groups are implemented.
Host aliases, `HostCr3`, compatibility controls, unknown fields and any field
without a canonical neutral source remain denied. Historical four-field
completion blockage was resolved by the separate Phase 55 and Phase 57 exact
contours; it is not authority for a generic completion field class.

**Decision required.** For every new exact field or inseparable semantic group,
an owner-approved field map must specify:

- neutral owner and exact production value source;
- presence/zero semantics and semantic compatibility;
- admission, generation, replay/restore and receipt binding;
- migration class;
- frozen compatibility mapping and mismatch denial;
- separate production-composition authorization after D2.

**Required proof.** Production source/caller reachability; absent, zero,
foreign/stale/duplicate/cross-owner/domain/context/VT/attempt/event negatives;
PRF/writeback/retire delivery; no fallthrough; clean-source gates.

**Stays denied.** Inference, zero fallback, VMCS backing storage, compatibility
factories as authority, broad completion-class admission, and automatic
adjacent-field activation.

### B4 — VMWRITE and all compatibility-driven mutation

**Current fact.** VMWRITE is deny-by-default. Opcode and VMCS field vocabulary
do not identify the neutral owner permitted to mutate architectural state.

**Decision required.** A mutation-owner RFC/ADR must define the exact writable
field set, canonical neutral mutation owner, source register ABI, admission and
capability, atomicity/ordering, validation, rollback, completion/retire effects
and migration semantics.

**Required proof.** Exact production caller, owner-issued non-forgeable token,
stale/cross-scope/duplicate/replay/squash negatives, atomic visibility at the
canonical commit point, restore/rebind invalidation and disabled-path
equivalence.

**Stays denied.** Mutable VMCS backing store, active-VMCS pointer authority,
generic field writes, SecureCompute-sensitive writes, and write authorization
derived from a successful VMREAD.

### B5 — nested virtualization and child-domain execution

**Current fact.** Child intent and Shadow VMCS objects are compatibility/read
vocabulary only. No authority-issued parent/child identity edge, independent
grants, address-space relationship or migration contract is accepted.

**Decision required.** A neutral child-intent owner RFC/ADR must define parent
and child identities/epochs, delegation limits, independent admission grants,
stage ownership, failure reflection, completion target, retire policy and
migration/restore behavior.

**Required proof.** Real production construction and execution callers,
cross-parent/child/domain/address-space negatives, revocation and nested
restore races, deterministic fault attribution and absence of Shadow
VMCS/VMCS12/VMCS02 authority.

**Stays denied.** Nested execution, nested result reuse as CPU authority,
VMCS-owned child state, or promotion of existing nested/IOMMU helpers.

### B6 — SecureCompute visibility or execution through virtualization

**Current fact.** SecureCompute remains a separate owner domain. VMX, VMCS,
`VmxCaps`, virtualization admission and completion receipts cannot transitively
grant secure visibility or execution. The previously observed SecureCompute
plan/guard drift is not a current matrix failure, but future semantic changes
still belong to the SecureCompute owner.

**Decision required.** A SecureCompute-owner RFC/ADR is required for any
positive secure projection or execution path. It must define secure identity,
grant provenance, non-transitivity, host-evidence isolation, completion,
retire, migration and denial semantics. Any compiler emission requires an
additional compiler decision.

**Required proof.** Full SecureCompute matrix, virtualization cross-matrix,
secure/non-secure cross-owner negatives, restore/revocation races and proof
that disabled virtualization does not change SecureCompute semantics.

**Stays denied.** Fixing SecureCompute behavior merely to satisfy a
virtualization guard, VMCS storage of secure state, or secure activation from
compatibility metadata.

### B7 — memory, I/O, IOMMU, DMA, device, Lane6/Lane7 and stream expansion

**Current fact.** These subsystems have separate existing owners. Scheduler
tokens, telemetry, helper success, backend bindings and debug traces are not
portable virtualization authority.

**Decision required.** The relevant subsystem owners must accept a bounded
transaction contract containing exact MemoryDomain, I/O/IOMMU, device and
lane-local authorizations, attempt identity, cancellation/replay, completion,
retire and migration disposition.

**Required proof.** Each token is owner-issued, live, scope-bound and consumed
once; missing/stale/cross-attempt/cross-owner combinations deny; no monolithic
VMX envelope bypasses an owner; races cover cancellation, restore, drain and
concurrent domains.

**Stays denied.** IOMMU/DMA/device results as CPU authority, lane/stream
passthrough from VMX, and migration of native tokens or telemetry.

### B8 — compiler emission beyond the exact default-off probe profile

**Current fact.** Exact probe emission is separately controlled and
default-disabled. All adjacent virtualization and SecureCompute emission is
denied. Compiler output cannot grant runtime correctness or authority.

**Decision required.** After an exact runtime operation is accepted and
implemented, the compiler owner must accept a separate emission decision tied
to that operation’s immutable D2 identity and ABI.

**Required proof.** Exact lowering and golden carrier, compiler-off
equivalence, no authority-bearing sideband, full-width operand preservation,
runtime revalidation, adjacent-opcode/leaf negatives and independent rollback.

**Stays denied.** Compiler-created E1/E2/capability/completion/retire evidence,
compiler-generated SecureCompute authority, or using untracked compiler files
as clean-source dependencies.

### B9 — migration/checkpoint serialization beyond current classifications

**Current fact.** Exact VMCALL uses `DrainOnly`; neutral completion observation
uses `RecomputedCompletion`. Completion snapshots, generations, receipts and
issuer seals are intentionally not serialized. VMCS projection metadata is not
migration authority.

**Decision required.** Any move from drain/recompute to serialized in-flight
state requires a neutral migration-owner decision defining payload owner,
version, validation, restore identity, replay disposition, revocation,
pending-effect ordering and deterministic resume/fault behavior.

**Required proof.** Restore into same and different owner/domain/address-space
epochs, stale/forged/duplicate payload negatives, in-flight boundary races,
quiescence and no resurrection of consumed receipts or authority tokens.

**Stays denied.** Serialization of live grants/seals/tokens by default,
checkpoint-as-authority, or reconstructing a valid receipt from compatibility
fields.

### B10 — new completion producers, completion classes or arbitration rules

**Current fact.** Existing CPU translation producers are exact registrations
under one `ArchitecturalCompletionCommitOwner`; stage-aware precise-fault
arbitration and lifecycle rules are closed only for their proven scope.

**Decision required.** Every additional producer requires a bounded neutral
producer authorization naming exact class, facts, provenance, owner identity
and epoch, admission policy, same-window/older-younger arbitration,
commit-denial response and lifecycle invalidation.

**Required proof.** Real production caller to canonical retire; deterministic
winner; forged/stale/duplicate/cross-scope negatives; squash/replay;
restore/rebind/owner-replacement races; exact-once commit and observation.

**Stays denied.** Parallel completion owner, broad completion-class registry,
VMREAD-specific producer registry, generic exception promotion, VMCALL/trap
fence surrogate, or compatibility reason as producer authority.

### B11 — broad virtualization completion claim

**Current fact.** The plan contains multiple independently closed exact
contours and multiple intentionally denied surfaces. Their union is not a
single broad activation proof.

**Decision required.** Before any system-wide completion or release claim, an
architecture/release review must enumerate every included and excluded
surface, prove construction reachability and defaults, reconcile migration and
rollback, and bind the claim to one clean release-candidate SHA.

**Required proof.** Requirement-by-requirement completion audit across VMREAD,
VMCALL, VMWRITE, nested, SecureCompute, memory/I/O/lane/stream, compiler,
migration and release gates.

**Stays denied.** `Virtualization complete`, `VMX fully activated`, broad
VMREAD, broad VMX backend, or adjacent subsystem activation claims.

## Operational cross-gates that can block any selected pool

These gates do not choose or authorize a pool, but any red result prevents a
subject/evidence commit and prevents opening the next boundary:

1. focused positive and negative tests for the selected scope;
2. repeated replay/restore/rebind/owner-replacement races as applicable;
3. forbidden authority and adjacent-surface scans;
4. canonical full VMX matrix;
5. full SecureCompute cross-matrix without changing its semantics;
6. hook-free Release build;
7. `git diff --check`;
8. clean-source verification from tracked canonical source only;
9. zero ignored/untracked production C# dependencies;
10. separate subject and later non-self-referential evidence commits.

A dirty user worktree is not itself architectural authority or a reason to
rewrite user files. A pre-existing red cross-gate must be resolved by its owner
or explicitly accepted as a bounded correction before virtualization commits
can proceed.

## Historical blockers that are no longer current

The following must not be used to reopen work automatically:

| Historical finding | Current disposition |
|---|---|
| Missing completion restore/rebind callers and undefined commit denial | Closed by Phase 52. |
| No canonical CPU instruction translation source/producer | Closed by Phase 54. |
| Missing exact reason/qualification compatibility mapping | Closed for the accepted tuple by Phase 55. |
| No canonical neutral CPU second-stage producer or exact GPA | Closed by Phase 56. |
| GPA/EPT qualification VMREAD coverage absent | Closed for the exact committed second-stage producer by Phase 57. |
| Original all-or-nothing four-field completion VMREAD candidate | Superseded by separate Phase 55 and Phase 57 exact decisions; no generic group authority exists. |
| Phase 51 SecureCompute cross-matrix failure caused by plan/guard drift | Later mandatory matrices are green; future SecureCompute semantic work remains separately owned. |
| Old VMCALL missing-owner/wait-series records | Historical readiness evidence superseded for the exact probe by accepted Phase 38 and PR-B–PR-J closures; no authority for new leaves. |

The machine status explicitly sets
`HistoricalSectionsParticipateInCurrentState=false`. Retained historical
`Blocked...` fields are audit evidence, not an active candidate selector.

## Minimum decision package for the next selected pool

Any post-Phase-57 authorization should contain all of the following before
implementation begins:

1. **Decision identity:** stable decision ID, exact operation/field set and
   authority plane.
2. **Owner map:** operation/field, neutral owner, value source, capability
   policy, evidence class, migration class and denial reason.
3. **Production E0:** real callers, constructors/configuration path, activation
   default and canonical linearization point.
4. **Identity model:** owner epoch, domain/context/VT, attempt/event and any
   operation/address-space identity required for uniqueness.
5. **Ordering model:** older/younger and same-window arbitration, squash,
   replay, restore and commit-denial behavior.
6. **Compatibility boundary:** exact mapping only; no VMX/VMCS authority,
   inference or zero fallback.
7. **Migration and rollback:** drain/recompute/serialize choice, restore
   validation, kill switch and deterministic rollback.
8. **Verification contract:** focused positives/negatives, races, forbidden
   scans, VMX and SecureCompute matrices, hook-free Release, clean source and
   evidence chain.
9. **Explicit exclusions:** VMWRITE, adjacent fields/leaves, nested,
   SecureCompute, compiler, memory/I/O/device/lane/stream and broad activation
   unless the selected decision is specifically for one of them.
10. **Machine-current transition:** exactly one `NextOpenPool`; successful
    closure must return to `None` unless another pool is separately authorized.

## Recommended owner decision order

No technical preference below is authorization. The owner should choose one
bounded objective at a time:

1. decide whether any existing default-off exact profile should be activated
   or released;
2. otherwise select one exact new VMREAD field group or one exact new VMCALL
   operation and restart at E0/D2;
3. treat VMWRITE, nested, SecureCompute and memory/I/O/lane/stream as separate
   owner programs, never as adjacent cleanup;
4. open compiler emission only after its exact runtime contour is accepted;
5. open serialized migration only after the runtime contour and its lifecycle
   are closed;
6. consider a broad completion claim only after every included surface has its
   own clean, attributable closure.

Until such a decision is accepted, the correct verdict is:

```text
BLOCKED: no authorized post-Phase-57 candidate pool.
All future-gated surfaces remain denied or default-disabled.
```

## Primary references

- [`VirtualizationActivationStatusV1.json`](VirtualizationActivationPlan/VirtualizationActivationStatusV1.json)
- [`00_virtualization_activation_refactoring_index.md`](VirtualizationActivationPlan/00_virtualization_activation_refactoring_index.md)
- [`02_global_forbidden_regressions_and_static_gates.md`](VirtualizationActivationPlan/02_global_forbidden_regressions_and_static_gates.md)
- [`03_owner_specific_rfc_adr_process.md`](VirtualizationActivationPlan/03_owner_specific_rfc_adr_process.md)
- [`04_vmread_projection_completion_and_denial_matrix.md`](VirtualizationActivationPlan/04_vmread_projection_completion_and_denial_matrix.md)
- [`10_vmwrite_neutral_owner_policy.md`](VirtualizationActivationPlan/10_vmwrite_neutral_owner_policy.md)
- [`11_nested_child_intent_owner_rfc.md`](VirtualizationActivationPlan/11_nested_child_intent_owner_rfc.md)
- [`12_memory_io_iommu_lane_stream_boundary_activation_plan.md`](VirtualizationActivationPlan/12_memory_io_iommu_lane_stream_boundary_activation_plan.md)
- [`13_securecompute_virtualization_boundary_plan.md`](VirtualizationActivationPlan/13_securecompute_virtualization_boundary_plan.md)
- [`14_compiler_no_emission_to_controlled_emission_gate.md`](VirtualizationActivationPlan/14_compiler_no_emission_to_controlled_emission_gate.md)
- [`15_migration_checkpoint_restore_authority_plan.md`](VirtualizationActivationPlan/15_migration_checkpoint_restore_authority_plan.md)
- [`17_phase_rollout_and_pr_order.md`](VirtualizationActivationPlan/17_phase_rollout_and_pr_order.md)
- [`18_release_gate_for_limited_runtime_virtualization.md`](VirtualizationActivationPlan/18_release_gate_for_limited_runtime_virtualization.md)
- [`19_open_decision_backlog.md`](VirtualizationActivationPlan/19_open_decision_backlog.md)
- [`51_external_virtualization_audit_revalidation_and_plan_corrections.md`](VirtualizationActivationPlan/51_external_virtualization_audit_revalidation_and_plan_corrections.md)
- [`52_neutral_completion_lifecycle_correction.md`](VirtualizationActivationPlan/52_neutral_completion_lifecycle_correction.md)
- [`54_canonical_neutral_cpu_instruction_translation_contour_and_producer_reaudit.md`](VirtualizationActivationPlan/54_canonical_neutral_cpu_instruction_translation_contour_and_producer_reaudit.md)
- [`55_completion_reason_qualification_vmread_projection_e0_d2.md`](VirtualizationActivationPlan/55_completion_reason_qualification_vmread_projection_e0_d2.md)
- [`56_canonical_neutral_cpu_second_stage_translation_contour_and_producer.md`](VirtualizationActivationPlan/56_canonical_neutral_cpu_second_stage_translation_contour_and_producer.md)
- [`57_exact_completion_backed_gpa_ept_vmread_projection.md`](VirtualizationActivationPlan/57_exact_completion_backed_gpa_ept_vmread_projection.md)
