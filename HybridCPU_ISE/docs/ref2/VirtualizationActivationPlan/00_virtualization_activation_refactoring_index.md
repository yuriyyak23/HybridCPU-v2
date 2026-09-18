# Virtualization Activation Refactoring Index

Status date: 2026-08-11

Status: activation dependency corpus. PR-I is the containing provenance for exact E7. PR-J development-local lifecycle linearization, exact activation/rollback and later non-self-referential evidence are closed at their committed subject and evidence records. Compiler emission remains closed by default. This corpus does not approve broad VMX backend authority.

## Current-State Authority Rule

`VirtualizationActivationStatusV1.json` is the sole machine-readable source for current stage and gate status. Every section named `Current Baseline`, `Active Critical Blockers`, `Remaining Production Blockers`, or `Next Open Pool` must agree with it. Dated journals and historical closure sections remain audit evidence but do not participate in current-state evaluation.

At the current subject, D2, O1 and E2-E7 are closed for exactly `PROBE_NO_STATE_V1`; E1 remains closed fault-only. `CompilerGate` is `ClosedByDefault`, `ReleaseGate` is `ClosedDevelopmentLocalExactProfile`, the exact profile is default-disabled until explicit exact-profile activation, and broad activation is denied. The PR-J subject is `bcd2d7f4654d4dab17c7a6705cb885fdd572510d` / `2ae54ab2ba4f0da1e9d95fc95dfb1ad83b080e33`; later evidence commit `c4c72b593bfcbb2c4517c5d0f38811bee7e7d962` records it without self-reference.

## 2026-08-11 PR-J Development-Local Closure

The PR-J subject adds a per-domain lifecycle epoch/gate whose transition-in-flight count covers new E2 publication and E2->E3, E3->E5 and E5->E6 handoffs. Drain closes new E2 before registry observation; cancellation waits transition quiescence before touching the authoritative E2/E3/E5/E6 registries. A default-disabled exact runtime profile provisions only the accepted domain grant and neutral owners, and its kill switch drains to zero, revokes the exact binding/grant and restores the unbound fault-only fallback. Mandatory race and rollback tests passed on the subject. The later development-local evidence record is immutable evidence, not runtime authority; the 200-iteration exact-profile monitoring baseline is closed and no automatic activation-expansion pool remains.

## 2026-08-10 PR-I E7 Closure Addendum

Committed PR-H `89e193b4f1247baaaf1c4188ad121897360a8c75` opened PR-I.
PR-I adds a domain drain gate backed by the authoritative live E2/E3/E5/E6
registries, a policy-identity-only checkpoint, one-shot restore-generation
advance and exact local D2/O1 reload. No runtime token, host evidence,
compatibility projection or architectural state is serialized. Compiler and
release pools are not automatically authorized.

## 2026-08-10 PR-H Closure Addendum

Committed PR-G `2dfd76b38c9b9470c3e476c43ec0fc60544e9c22` opened PR-H.
The per-CPU neutral retire owner now consumes one live E5 only after stable WB
head/order selection and full batch prevalidation, and the existing WB finalizer
consumes E6 with zero architectural effects. Compatibility remains fault-only.
PR-I E7 is conditional on the PR-H containing commit and green gates; compiler
emission and broad activation remain denied.

## 2026-08-09 PR-E Closure Addendum

PR-D is contained by commit `992c0cc2895b444ebc92c4b48d91175567f48076`.
The bounded sequence therefore opened PR-E, which now adds one production-compiled
but default-off neutral executor for exactly `PROBE_NO_STATE_V1`. It can consume
one live E2 and return one opaque, owner-bound E3 with canonical non-zero
no-effect/no-result digests. It is not composed into decode, VMX compatibility,
completion or retire. Production VMCALL remains fault-only. PR-F is the next
conditional pool only after PR-E receives one containing commit and all exit,
negative/static and rollback gates remain green.

## 2026-08-09 PR-F Closure Addendum

PR-E is contained by commit `8a36b89af279ec8f108d458621ad8e37d89a8c6d`.
PR-F adds the exclusive E4 composition: only the canonical lane-7 scheduler seam
after live E1 and immutable operand capture can issue E2 and attach an opaque
carrier-bound dispatch; `VmxMicroOp.Execute` consumes it once into E3. The new
neutral `InvokeHypercall` operation is `RuntimeService`, `NoStateExecution`,
typed-capability-required and non-projection. With no configured binding, or
after disable/revocation, the PR-D fault-only path is unchanged. Completion and
retire remain absent; PR-G is conditional on a clean green PR-F commit.

## 2026-08-10 PR-G Closure Addendum

PR-F is contained by commit `7f529cd4f9699701b0b2bfdcc8bd90eaf82af781`.
PR-G adds only the neutral completion boundary: a live exact E3 is revalidated,
routed through the split completion-only route and pure fence policy, then
consumed once by `DomainHypercallCompletionOwner`, which atomically emits one
neutral `CompletionRecord` and one opaque record-bound E5. The legacy
caller-boolean fence remains scaffolding, compatibility has no caller, and VMX
retire remains the established fault. PR-H is conditional on a clean green PR-G
commit and may open only the canonical E6 retire-eligibility contour.

## 2026-06-11 Audit Contract

- File name: `00_virtualization_activation_refactoring_index.md`.
- Purpose: index the activation-plan corpus and record the current readiness-only boundary.
- Status: no activation approval; current runtime state is projection/admission denial plus future-gated RFC candidates.
- Scope: VMREAD projection matrix, VMWRITE denial, VMCALL admitted-denied skeleton, completion/retire gates, nested/SecureCompute/lane/compiler/migration boundaries.
- No-goals: no VMX backend authority, no VMCS state store, no active VMCS pointer, no VMWRITE, no VMCALL backend success, no nested/SecureCompute/lane/compiler/migration activation.
- Code anchors: `VmxCompatibilityAdmissionService.Traps.cs`, `VmcsReadOnlyValueProjectionService.cs`, `VmcsFieldProjectionSchema.cs`, `HypercallBackendAdmissionPolicy.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: neutral runtime owners only; VMX/VMCS/`VmxCaps`, docs, tests, generated schemas, migration images, and telemetry are not authority.
- Required RFC/ADR: every future positive path requires owner-specific RFC/ADR with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: all unproven paths are `не доказано`, `future-gated`, `должно оставаться denied`, or `требует owner-specific RFC/ADR`; no readiness item is renamed to activation.
- Tests/static scans: `VirtualizationActivationPlanAuditGuardTests` plus Phase 16 scans for VMCS store, active pointer, VMWRITE, VMCALL owner, `RuntimeOwnedPublication`, SecureCompute VMX authority, lane/stream leakage, and compiler emission.
- Risks: wording drift around "success", proof-only/admitted-denied results, completion/retire publication, or release-gate claims.
- Next-gate dependency: Phase 03 owner-specific RFC/ADR process before any positive implementation.

Semantic denials for this corpus:

- admission != execution.
- backend success != completion publication.
- completion publication != retire publication.
- proof-only != activation approval.
- admitted-denied != backend success.

## 2026-08-06 Current-HEAD Reconciliation

The active reading order is now:

1. `evidence/2026-08-07-e1-containing-sha-evidence.json` for the clean E1-containing SHA and local Baseline provenance; the 2026-08-06 manifests are retained as historical evidence;
2. `01_current_state_and_gap_matrix.md` for the verified current-state split and audit verdicts;
3. `02_global_forbidden_regressions_and_static_gates.md` for fail-closed guards;
4. `17_phase_rollout_and_pr_order.md` for the only active dependency order;
5. `19_open_decision_backlog.md` for the compact decision/status journal and normative all-phase blocker register;
6. `32_e1_nonforgeable_safetyverifier_admission_contract.md` for the implemented fault-only typed-admission boundary;
7. `33_vrt_external_audit_reconciliation_and_blocker_decisions.md` for the current VRT audit verdicts and adopted blocker rules;
8. `34_d2_schema_attribution_and_e2_negative_substrate.md` for the implemented fail-closed D2/E2 substrate;
9. `35_a594_clean_sha_evidence_and_disabled_review_workflow.md` for the repeated local CI evidence and disabled review workflow;
10. `36_testing_only_research_runtime_probe.md` for the closed non-production P1 experiment and its strict separation from D2/E2/E3;
11. `37_testing_only_canonical_issue_materialization_composition.md` for the closed default-off P2 experiment at the canonical seam and its strict production non-authority;
12. `38_first_production_vmcall_slice_repository_owner_adr.md` for the repository-owner accepted first-slice architecture/ABI decision and the machine-D2/runtime non-authorization boundary;
13. owner-specific phase documents for obligations, never for permission.

The following dated repository facts are retained as historical checkpoints and
must not be conflated with current state. Current state is defined by
`VirtualizationActivationStatusV1.json`, the Phase 38 PR-J closure and the later
Phase 42 subject/evidence chain:

- clean local commit `55807df77978a960382fa913dda4e7ace0093a6b`, tree `3f356900b4c534e965a69640e88e0c5ecf902b7c`, contains E1 and reproduced the local Baseline with `outcome: passed`;
- the external audit reports `f794ea378779fe153a206af5fe287571884b1bc4`, which is absent from the local object database and belongs to an unproven remote lineage relative to this checkout;
- the plan is tracked at current `HEAD`, so the audit's plan-absent finding is historical for its reported SHA and is refuted for current `HEAD`;
- current local `HEAD` is `ddfffa2d7b86fb3ece21f8d21c6447ae47ee3868`; the worktree is dirty and Phase 36/diagnostics changes are not clean-SHA evidence;
- `CloseToHSL` is the canonical tracked source tree with 933 files at current `HEAD`; obsolete `CloseToRTL` has zero tracked files and cannot satisfy an evidence/source guard; untracked TESTING-only P1 files are working-copy evidence, not part of that tracked count;
- E0 clean-subject provenance is closed by the local-only manifest and clean-SHA VMX test run; this closure is evidence only and grants no runtime authority;
- E1 typed SafetyVerifier admission is closed as fault-only canonical transport; it grants no backend, completion or retire authority;
- Phase 34 plus PR-A provide the fail-closed v1/v2 governance substrate; the separately authorized PR-B now adds committed exact SpecV2 bytes, real CODEOWNERS/review receipts, an attributable AcceptanceRecordV2, allocation metadata and the exact generated lookup;
- clean local commit `a594d10abcbe8593d23fed16310af30706893452` reproduced Baseline successfully; Phase 35 adds only malformed-SHA, reviewer-mismatch, duplicate-leaf and incomplete-map denials plus a disabled repository-owner review workflow;
- Phase 36 closes a separate `TESTING`-only state-minimal/no-payload research probe: SafetyVerifier issues prototype admission over live E1 and a neutral runtime owner returns an exact-once opaque receipt; it has no numeric leaf, production caller, completion or retire authority, so D2 and production E2-E7 remain unchanged and compiler/release gates remain unopened;
- Phase 37 closes P2 as default-off `TESTING`-only canonical issue/materialization composition of that same probe; structured identity/generation/replay/slot denials and exact-once diagnostics are model/testing evidence only, no P3 is authorized, and later PR-B/PR-C close D2/O1/operand independently without promoting P2. Production PR-D closes D2-bound E2 admission and PR-E closes only the isolated default-off exact E3 executor/receipt; E4-E7 and compiler/release gates remain unopened;
- `deep-research-report 2.md` was rechecked against local code: P2 is closed, current VMCALL qualification carries selectors rather than runtime operand values, the v1 single-manifest acceptance provenance is insufficient for final D2, and constructible completion/retire shapes are not E3/E5/E6 authority;
- Phase 38 accepts the architecture role `DomainHypercallRuntimeOwner` and the exact first-slice ABI `HybridCPU.VMCALL.Runtime.v1` / 16-bit / `0x0001` / `PROBE_NO_STATE_V1` / `Rs1` value / `Rs2=x0` / `Rd=x0` / no-state/no-payload / `DrainOnly`; this is a repository-owner ADR, not a machine-loaded accepted decision or runtime capability;
- machine D2 is `ATTRIBUTABLY MATERIALIZED / ACCEPTED POLICY ONLY`: Phase 38 fixes the values, PR-A supplies the v2 validator substrate, commit A fixes exact SpecV2/CODEOWNERS bytes, and later PR-B artifacts bind the accepted record, reviews, allocations and exact lookup;
- PR-A through PR-E are committed at their declared boundaries. The explicitly authorized bounded PR-D -> PR-I sequence is active; PR-F now supplies only the exclusive canonical E4 path to E3 while completion and retire remain unreachable. PR-G may open only after the PR-F containing commit preserves all exit and rollback checks;
- no external archive or CI side-load may satisfy a repository guard.

Current closure: the exact default-disabled `PROBE_NO_STATE_V1` VMCALL profile
and the separate exact default-disabled `GuestCr0`/`GuestCr4` VMREAD scalar
delivery profile are closed at their named clean subjects. All other surfaces
remain denied unless a new named owner-specific E0/D2 package is accepted.

Files `20` through `31` are retained only as immutable historical wait/check snapshots. They are not active phases, do not form a dependency chain, and grant no implementation authority. Their compact, fail-closed history is maintained in Phase 19. Historical wording that requires an organizationally external owner is superseded by Phase 33: neutrality means independence from the compatibility authority plane, and repository-local attributable governance is allowed.

The active model has two graphs. Build/readiness requires `SpecV2 + AcceptanceRecordV2 -> D2 accepted -> O1 loaded`, while `E0 + E1 implementation readiness` only permits production composition work to be considered. Per attempt, the runtime chain is `canonical decode -> legality/owner-domain guards -> Stage A -> Stage B -> E1 -> one-time operand snapshot -> D2/O1 resolution -> RuntimeBoundaryAdmission/neutral trap -> E2 -> E3`. E4 proves that this is the only canonical composition to E3; it is not a post-E3 runtime stage. Atomic completion plus E5, E6 retire and E7 drain/restore/determinism follow. Optional compiler emission and exact-scope release are separate governance gates.

No closed readiness check, monitor tick, wait timeout, green test, source scan, proof-only result, projection, admission, or compatibility effect can skip a dependency in this model.

## A. Executive Summary

Current baseline:

- The current `VirtualiztionRefactoringNew` corpus is a readiness and denial-closure corpus, not activation approval.
- VMX is a frozen compatibility frontend and ABI/projection vocabulary.
- VMCS/VMCSv2 is generated/read-only/denied projection vocabulary, not a mutable state store.
- VMREAD is open only field-by-field through decode, projection validation, `RuntimeBoundaryAdmissionService`, generated schema lookup, explicit neutral owner value source, and evidence/access policy.
- Current VMREAD projected fields are `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, `EptViolationQualification`, `GuestCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount`, `GuestPc`, `GuestSp`, `GuestFlags`, and guarded read-only `GuestCr0`/`GuestCr4`.
- `GuestCr0`/`GuestCr4` are available only through the neutral privileged execution-state descriptor plus field-specific projection, visibility, `RevalidatedAfterRestore`, and conformance gates. Their mutation, backend execution, completion publication, and retire publication remain denied.
- Host aliases, compatibility-control fields, unknown fields, and all writes remain denied.
- `AllowedProofOnlyNoExecution` and `AllowedAdmittedDenied` are evidence-only or admission-only semantics; they do not imply backend success, completion publication, or retire publication.
- VMCALL has neutral trap projection, backend admission, route, and publication fences, but current production flow passes `MissingNeutralOwner` and uses `ProjectionOnlyDenied`; backend execution, completion publication, and retire publication remain denied.
- Nested virtualization remains future-gated behind neutral child-intent ownership.
- SecureCompute remains a neutral runtime descriptor/admission discipline. It is not a VMX mode, secure VMCS, `VmxCaps` authority, CHERI ISA, tagged memory, or current positive secure backend execution.
- Lane6/Lane7/Stream surfaces are runtime/helper/model surfaces. Their telemetry, tokens, replay evidence, and helper state are not virtualization authority.

Why the prior corpus is readiness only:

- Phase 16 explicitly records GO for documentation/readiness/test/static-gate maintenance and NO-GO for active runtime virtualization.
- It closes no production owner map for a positive path.
- It requires a new owner-specific RFC/ADR before any production path can open.
- It forbids treating VMREAD admission, VMCALL trap projection, completion routing, tests, docs, telemetry, or generated schemas as backend authority.

Blocked production paths:

- VMX backend execution and legacy VMX runtime manager.
- Mutable VMCS field store and active VMCS pointer.
- VMWRITE without a neutral write owner.
- VMCALL backend success without a neutral hypercall backend owner.
- Treating proof-only or admitted-denied results as backend success.
- Completion/retire publication without backend execution authorization, route authorization, publication fence, and explicit retire permission.
- Nested execution without neutral child-intent owner.
- SecureCompute activation through VMX/VMCS/`VmxCaps`/VMREAD/VMWRITE.
- Lane6/Lane7/Stream evidence becoming virtualization authority.
- Compiler backend emission before controlled emission RFC and gates.
- Migration/checkpoint images containing host-owned evidence, backend handles, native tokens, scheduler evidence, debug traces, VMCS projection authority, or compatibility projection metadata as authority.

Recommended first owner-specific RFC/ADR:

Open `06_neutral_hypercall_backend_owner_rfc.md` first for a very narrow, no-state, domain-local neutral hypercall backend owner. This is the first production-oriented path because the current code already has the full denied skeleton around VMCALL: trap projection, backend admission, route policy, publication fence, and retire denial. A minimal neutral owner can exercise the complete admission/execution/completion/retire/evidence/migration chain without opening VMCS state, VMWRITE, nested, SecureCompute, lanes, or compiler emission. It is riskier than a read-only VMREAD extension, but it is the path that actually moves the system from projection-only/admitted-denied toward limited active runtime virtualization.

## B. Proposed New File Tree

`HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/`

| File | Purpose |
| --- | --- |
| `00_virtualization_activation_refactoring_index.md` | Index, executive summary, priority, blockers, closure matrix, release gate, first PR order. |
| `01_current_state_and_gap_matrix.md` | Baseline inventory from readiness corpus to activation gaps. |
| `02_global_forbidden_regressions_and_static_gates.md` | Non-regression rules and source/static scans. |
| `03_owner_specific_rfc_adr_process.md` | Required process before any positive path opens. |
| `04_vmread_projection_completion_and_denial_matrix.md` | VMREAD projected/denied matrix and expansion discipline. |
| `05_privileged_execution_state_owner_rfc.md` | Implemented guarded neutral owner and read-only `GuestCr0`/`GuestCr4` projection closure. |
| `06_neutral_hypercall_backend_owner_rfc.md` | Recommended first owner-specific RFC. |
| `07_vmcall_success_path_activation_plan.md` | Future VMCALL success path after neutral backend owner approval. |
| `08_trap_completion_route_publication_plan.md` | Completion route and publication activation gate. |
| `09_retire_publication_activation_plan.md` | Explicit retire publication gate. |
| `10_vmwrite_neutral_owner_policy.md` | VMWRITE remains denied until neutral write owner exists. |
| `11_nested_child_intent_owner_rfc.md` | Future nested child-intent owner RFC. |
| `12_memory_io_iommu_lane_stream_boundary_activation_plan.md` | Memory/I/O/IOMMU/Lane6/Lane7/Stream boundary gates. |
| `13_securecompute_virtualization_boundary_plan.md` | SecureCompute remains neutral and not VMX-owned. |
| `14_compiler_no_emission_to_controlled_emission_gate.md` | Compiler/no-emission to controlled emission gate. |
| `15_migration_checkpoint_restore_authority_plan.md` | Migration/checkpoint/restore authority and evidence non-leak plan. |
| `16_conformance_negative_positive_test_matrix.md` | Negative tests, future positive tests, and static scans. |
| `17_phase_rollout_and_pr_order.md` | Recommended rollout and PR sequencing. |
| `18_release_gate_for_limited_runtime_virtualization.md` | Definition of limited runtime virtualization activation. |
| `19_open_decision_backlog.md` | Backlog of deferred owner decisions. |
| `20_hv_rfc_intake_packet.md` | Historical snapshot. Owner-facing RFC/ADR intake packet for the selected minimal VMCALL backend owner; draft-only, no acceptance or production behavior change. |
| `21_hv_owner_handoff_packet.md` | Historical snapshot. Handoff and response-audit record for the Phase 20 packet; no attributable owner response found, all runtime behavior remains blocked/future-gated. |
| `22_hv_no_response_fallback.md` | Historical snapshot. No-response fallback: wait for attributable owner response or later choose a replacement intake; VMCALL remains denied/future-gated. |
| `23_hv_replacement_intake_decision.md` | Historical snapshot. Process-only decision to keep waiting for the VMCALL owner response; no replacement intake selected and no production behavior change. |
| `24_hv_late_owner_response_audit.md` | Historical snapshot. Periodic late owner-response audit; no attributable VMCALL owner response found, wait/denied/future-gated state preserved. |
| `25_hv_wait_state_monitor.md` | Historical snapshot. Monitor-only cadence for attributable VMCALL owner response; no external owner artifact found, wait/denied/future-gated state preserved. |
| `26_hv_owner_artifact_recheck.md` | Historical snapshot. Artifact-triggered owner response recheck; no attributable external owner artifact found, gate not fired, wait/denied/future-gated state preserved. |
| `27_hv_wait_or_reselect_checkpoint.md` | Historical snapshot. Process-only wait-or-reselect checkpoint; wait continues, no replacement intake selected, VMCALL remains denied/future-gated. |
| `28_hv_denied_chain_refresh.md` | Historical snapshot. Readiness-only refresh of the VMCALL denied production chain: MissingNeutralOwner, ProjectionOnlyDenied, completion denied, retire denied. |
| `29_hv_owner_response_watchdog.md` | Historical snapshot. Watchdog-only repeat owner-response check; no attributable artifact found and the denied VMCALL chain remains unchanged. |
| `30_hv_final_wait_baseline.md` | Historical snapshot. Final wait-baseline snapshot for the current VMCALL owner-response series; no artifact, no replacement, denied chain preserved. |
| `31_hv_post_baseline_artifact_sentry.md` | Historical snapshot. Post-baseline sentry-only check for attributable owner artifact; no artifact and no replacement selection, wait/denied/future-gated preserved. |
| `32_e1_nonforgeable_safetyverifier_admission_contract.md` | Implemented E1 contract: exclusive SafetyVerifier live issuance/validation, attempt-bound identities, canonical issue-packet lane-7 transport, invalidation and static anti-forgery gates; backend/completion/retire remain false. |
| `33_vrt_external_audit_reconciliation_and_blocker_decisions.md` | Historical VRT reconciliation with a current-state addendum; later Phase 38/PR-A/PR-B/PR-C supersede candidate-only D2 findings without promoting P1/P2 or opening E2. |
| `34_d2_schema_attribution_and_e2_negative_substrate.md` | Fail-closed D2 schema/validator, repository attribution gates, disabled owner interface and opaque unissued E2 substrate. |
| `35_a594_clean_sha_evidence_and_disabled_review_workflow.md` | Local clean-SHA CI evidence for Phase 34 and a disabled v1 review workflow; later PR-B acceptance is independent and does not promote that workflow. |
| `36_testing_only_research_runtime_probe.md` | Closed TESTING-only P1 research probe with live E1 revalidation, typed runtime identities and exact-once no-state receipt; no production gate is closed. |
| `37_testing_only_canonical_issue_materialization_composition.md` | Closed default-off TESTING-only P2 composition at canonical issue/materialization; no leaf, production backend, completion or retire authority. |
| `38_first_production_vmcall_slice_repository_owner_adr.md` | Exact accepted D2 profile and PR-A through PR-I closure register; PR-J is the exact-scope release gate. |
| `VirtualizationActivationStatusV1.json` | Sole machine-readable current D2/O1/E1-E7, compiler/release/broad-activation and next-pool status source. |
| `evidence/2026-08-06-current-worktree-evidence.json` | Historical dirty-worktree evidence snapshot. |
| `evidence/2026-08-06-clean-head-evidence.json` | Local-only clean-subject provenance, canonical `CloseToHSL` layout, source hashes and verification results. |
| `evidence/2026-08-07-e1-containing-sha-evidence.json` | Clean E1-containing SHA, tree and repeated local Baseline provenance; evidence only. |
| `evidence/2026-08-08-a594-clean-sha-evidence.json` | Clean Phase 34 SHA, tree and local Baseline provenance; evidence only. |
| `evidence/virtualization-operation-decision-manifest.schema.json` | D2 decision schema only; contains no owner appointment, operation selection or numeric leaf. |

## D. Activation Priority Recommendation

| Candidate | Benefit | Risk | Required owner map | Expected code impact | Security risk | Conformance burden | Decision |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Neutral hypercall backend owner / VMCALL success path | Exercises the complete active chain: admission, backend execution, completion, retire, evidence, migration, projection. Uses existing denied skeleton. | High if route/publication/retire separation is weakened. | Hypercall owner, leaf set, argument policy, capability grant, evidence class, migration class, completion route, retire rule, rollback rule. | Moderate: `HypercallBackendDescriptor`, admission request construction, backend executor, route request, tests; no VMCS state. | Medium-high; host evidence and backend handles must not leak. | High; needs negative and positive owner-specific tests. | Choose first, but only as a no-state minimal leaf and only after RFC/ADR. |
| Privileged execution-state owner for `GuestCr0`/`GuestCr4` | Guarded read-only VMREAD extension; no backend execution or publication. | Residual risk is accidental widening from field-local projection to general VMCS/SecureCompute authority. | Implemented neutral descriptor, legality policy, read-only projection gates, evidence and `RevalidatedAfterRestore` class. | Implemented in neutral owner plus separate projection service. | Contained by owner/source/visibility/migration/conformance gates and adjacent denials. | Covered by owner and projection tests. | Closed as projection-only readiness; not an activation candidate. |
| Nested child-intent owner | Enables future nested composition through neutral intent instead of Shadow VMCS. | Very high; parent/child authority, migration, evidence, completion mapping, memory composition. | Parent owner, child intent, capability filter, evidence policy, nested memory composition, migration, completion mapping, rollback. | High and cross-cutting. | High. | Very high. | Defer. It is not the first activation path. |

## E. Critical Blockers Before Any Activation

- P0: no complete owner map for the chosen path.
- P0: admission, backend execution, completion publication, and retire publication separation not proven.
- P0: VMCALL still lacks a materialized neutral backend owner.
- P0: `RuntimeOwnedPublication` misuse is not statically blocked from VMX frontend paths.
- P0 closure: `GuestCr0`/`GuestCr4` now have a neutral privileged execution-state owner and guarded read-only projection; this is field-local projection readiness, not general VMCS, SecureCompute, backend, completion, or retire activation.
- P0: nested virtualization has no neutral child-intent owner for active nested execution.
- P0: SecureCompute cannot be activated by VMX, VMCS, `VmxCaps`, VMREAD, or VMWRITE.
- P0: migration/evidence non-leak is not proven for the chosen positive path.
- P0: compiler no-emission boundary remains closed until controlled emission RFC.
- P0: no positive path may merge without negative tests for adjacent denied states.

## F. Closure Classification Matrix

| Area | Classification | Activation boundary |
| --- | --- | --- |
| VMX frontend | implemented, frozen compatibility vocabulary | May decode/project; cannot own runtime authority. |
| VMCS/VMCSv2 | projection-only; state-store role forbidden | Generated schema is vocabulary, not availability or state. |
| VMREAD | exact `GuestCr0`/`GuestCr4` projection plus default-disabled scalar delivery; other fields remain projection-only or denied | Field-by-field only through neutral owners; Phase 42 does not create VMCS value authority or broad activation. |
| VMWRITE | denied; future-gated | No write until neutral write owner and policy exist. |
| VMCALL | exact `PROBE_NO_STATE_V1` path closed under a default-disabled neutral profile; compatibility fallback remains admitted-denied | No adjacent leaf or broad activation inherits the exact profile. |
| Hypercall backend | exact neutral no-state backend only | Compatibility `MissingNeutralOwner` remains the unbound fallback; no other operation is admitted. |
| Trap completion route | exact owner-bound E5 only | Compatibility factories and frontend paths remain non-authoritative. |
| Retire publication | exact E6 no-state retire only | Completion is not retire; no VMREAD scalar result uses E6 or a VMX retire effect. |
| `GuestCr0`/`GuestCr4` | implemented guarded read-only projection and exact canonical scalar delivery | Neutral privileged execution-state descriptor is the sole value source; D2/receipt do not authorize mutation, backend execution, trap completion or VMX retire effects. |
| Host aliases | denied | Need separate neutral host owners; guest views cannot be reused. |
| Compatibility controls | denied value projection; model/helper-only fail-closed owner | Need explicit neutral control-bit value contract. |
| Nested virtualization | future-gated/denied | Child intent owner required; Shadow VMCS/VMCS12/VMCS02 not authority. |
| Memory/I/O/IOMMU | neutral owners implemented in parts; activation path-specific | VMX aliases cannot own memory/I/O/IOMMU. |
| Lane6/Lane7/Stream | model/helper-only or scoped native contours; not virtualization authority | Tokens, telemetry, replay evidence, helpers cannot grant VMX authority. |
| SecureCompute | neutral policy baseline/proof-only; VMX authority forbidden | No VMX/VMCS/`VmxCaps` activation. |
| Compiler/no-emission | no-emission closed; controlled emission future-gated | Compiler metadata cannot execute backend paths. |
| Migration/checkpoint | neutral policies implemented in parts; VMCS authority denied | Host-owned evidence and projection metadata excluded. |
| Conformance/golden artifacts | implemented negative/readiness gates; future positive gates needed | Tests prove or block; tests are not runtime authority. |

## G. Test Plan Summary

Negative tests:

- VMCS store absent.
- Active VMCS pointer absent.
- `VmxCaps` cannot grant authority.
- VMREAD cannot read denied fields.
- VMWRITE always denied unless neutral write owner exists.
- VMCALL missing owner remains denied.
- `RuntimeOwnedPublication` cannot be used by VMX frontend before backend owner.
- Completion cannot publish without fence.
- Retire cannot publish without explicit retire permission.
- Shadow VMCS cannot own nested state.
- SecureCompute cannot be activated through VMX/VMCS/`VmxCaps`.
- Lane6/Lane7 tokens cannot migrate as guest state.
- Tests/golden artifacts cannot be runtime authority.

Positive tests, only after owner-specific RFC/ADR:

- Neutral owner admits exactly the chosen operation.
- Capability/evidence policy is required.
- Backend execution produces neutral result only.
- Completion route is authorized only after backend success.
- Publication fence allows completion only under route permission.
- Retire policy allows publication only under explicit retire permission.
- Migration class is explicit.
- Adjacent denied states remain denied.
- Rollback and no-host-evidence checks pass.

Static/source scans:

- Forbidden manager names and legacy VMX backend markers.
- VMCS field store and active pointer markers.
- Overclaim wording.
- `RuntimeOwnedPublication` usage.
- SecureCompute VMX exposure.
- Stream/Lane authority leakage.
- No-emission boundary bypass.

## H. Release Gate

Limited runtime virtualization activated means:

- exactly one owner-specific path is active;
- a neutral owner exists and is the authority;
- admission, backend execution, completion, retire, evidence, migration, rollback, and negative tests are complete;
- compatibility projection happens only after neutral success;
- all adjacent denied states remain denied;
- no forbidden regression appears in code, docs, tests, migration images, compiler paths, or generated artifacts.

It does not mean:

- all VMX is supported;
- feature-complete claims;
- nested virtualization support;
- SecureCompute active through VMX;
- VMWRITE support;
- compiler virtualization emission open;
- Lane6/Lane7/Stream passthrough support.

## J. First Three PRs

1. `docs/ref2/VirtualizationActivationPlan/00-03` - baseline, forbidden regressions, and owner-specific RFC/ADR process.
2. `docs/ref2/VirtualizationActivationPlan/06-09` - neutral hypercall backend owner RFC, VMCALL success path, completion route, and retire gates.
3. `tests/static` - negative guards for `RuntimeOwnedPublication` misuse, VMCALL missing owner, VMCS store absence, active VMCS pointer absence, SecureCompute VMX authority absence, and Lane6/Lane7 evidence leakage.

## K. Phase 42 Current-State Addendum

Exact `GuestCr0`/`GuestCr4` scalar delivery is closed by implementation subject
`253e33435b1500a04ecde9228631fb3fab547d15` and its later non-self-referential
evidence record. The path uses the existing privileged execution-state owner,
an opaque attempt-bound receipt, canonical scalar writeback, and canonical
`RetireCoordinator` commit. Activation defaults disabled and migration is
`DrainOnly`. This does not open broad VMREAD, VMWRITE, VMCS value authority,
backend/trap completion, SecureCompute, nested, memory/IOMMU/I/O/device,
lane/stream, or compiler emission.
