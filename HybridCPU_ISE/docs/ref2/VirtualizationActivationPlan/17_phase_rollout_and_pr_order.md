# Phase 17 - Phase Rollout And PR Order

Status: rollout plan. PR-A through PR-J are committed for the exact probe. PR-J closes development-local exact-profile activation evidence; compiler emission stays closed by default and broad activation remains denied.

Current-state authority: `VirtualizationActivationStatusV1.json` is normative for current stage/gate state. Historical sequencing text below remains evidence and cannot override it.

Historical gate statement retained for guard continuity: PR-H closes exact canonical E6 only.

## 2026-08-10 PR-I Sequencing Result

Committed PR-H `89e193b4f1247baaaf1c4188ad121897360a8c75` preserved the
focused/full/static/diagnostic rollback gates. PR-I implements only the accepted
`DrainOnly` and `HostOwnedNonMigratable` profile: it closes new E2, drains or
cancels authoritative E2/E3/E5/E6 registries, serializes policy identity only,
advances restore generation and proves deterministic no-state traces. The
bounded PR-D through PR-I authorization ends here. The exact-scope PR-J Release
Gate requires a new explicit authorization; Compiler Gate remains closed by
default because runtime correctness does not depend on automatic compiler
VMCALL emission.

## 2026-08-10 PR-H Sequencing Result

Committed PR-G `2dfd76b38c9b9470c3e476c43ec0fc60544e9c22` preserved all exit and rollback
gates. PR-H integrates E6 into the existing canonical WB retire-window/head/order
contour and leaves compatibility fault-only. PR-I may open only after the single
PR-H containing commit preserves focused/full/static/diagnostic gates. PR-I is
limited to E7 drain/restore/determinism; compiler emission and broad release are
not authorized.

## 2026-08-10 PR-G Sequencing Result

PR-F is contained by `7f529cd4f9699701b0b2bfdcc8bd90eaf82af781`.
PR-G makes the neutral completion owner the sole E3 consumer for publication.
Route and fence results remain policy inputs; one owner critical section consumes
E3 and emits exactly one record plus opaque E5. Missing owner restores PR-F
no-publication behavior, and compatibility and VMX retire remain fail-closed.
PR-H opens conditionally only after one PR-G commit preserves all exit,
negative/static/diagnostic and rollback gates.

## 2026-08-09 PR-F Sequencing Result

PR-E is contained by `8a36b89af279ec8f108d458621ad8e37d89a8c6d`.
PR-F introduces neutral `InvokeHypercall` and the sole scheduler/execute
composition from E1 plus immutable operands through E2 to E3. No default binding
exists, disable is fail-closed, and the carrier still retires as the established
VMX fault. PR-G opens conditionally only after one PR-F commit preserves the
full negative/static/diagnostic and rollback matrix.

## 2026-08-09 PR-E Sequencing Result

PR-D is contained by `992c0cc2895b444ebc92c4b48d91175567f48076`.
PR-E adds one isolated service-level `DomainHypercallRuntimeExecutor` for exact
leaf `0x0001`, with a disabled default and a private opaque E3. It does not add
the E4 production composition or `InvokeHypercall`; VMX continues to fault.
PR-F opens conditionally only after the single PR-E commit preserves the full
negative/static/diagnostic matrix and disabling the executor restores the PR-D
fault-only boundary.

## 2026-06-11 Audit Contract

- File name: `17_phase_rollout_and_pr_order.md`.
- Purpose: sequence docs, negative gates, owner RFC/ADR, implementation, and release review without turning planning into activation.
- Status: rollout plan only; no runtime activation.
- Scope: PR order for baseline docs, VMREAD/denial docs, hypercall RFC, boundary docs, tests/static gates, release/backlog.
- No-goals: no combined RFC-draft-plus-backend merge, no positive tests before owner map, no release-gate claims before evidence.
- Code anchors: `HypercallBackendAdmissionPolicy.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs` for the first candidate path.
- Authority owner: process sequencing only; neutral runtime owners remain authority.
- Required RFC/ADR: first production code PR requires accepted owner-specific RFC/ADR with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: negative/static gates precede or accompany any positive implementation; docs-only work is not activation.
- Tests/static scans: Phase 16 scans before every PR touching virtualization/runtime/compiler/migration/SecureCompute/lane code.
- Risks: treating the recommended first RFC as implicit approval or broadening one PR beyond reviewed owner scope.
- Next-gate dependency: Phase 18 release gate.

## Phase Goal

Define the order in which documentation, static gates, owner RFC/ADR, implementation, and release review should land.

## Current Baseline

D2, O1 and E2-E7 are closed for exactly `HybridCPU.VMCALL.Runtime.v1` leaf `0x0001` / `PROBE_NO_STATE_V1`; E1 remains closed fault-only. PR-I is contained by `46917937d58fd22b2c0b9ee9308c5ead6e8af11f` with tree `1150abad86d260b63cc17a076cb23af32f074c1f`. PR-J subject `bcd2d7f4654d4dab17c7a6705cb885fdd572510d` / tree `2ae54ab2ba4f0da1e9d95fc95dfb1ad83b080e33` and its later evidence record close development-local exact-profile activation. Compiler emission is `ClosedByDefault`, not the next dependency.

Separately, Phase 42 closes exact `GuestCr0`/`GuestCr4` read-only VMREAD scalar delivery at subject `253e33435b1500a04ecde9228631fb3fab547d15`. It is default-disabled, `DrainOnly`, sourced only by the existing privileged execution-state owner, and grants no backend, completion, VMX-retire, VMWRITE or adjacent-field authority.

Worktree evaluation: the shared per-domain lifecycle gate counts new-E2 publication and every E2->E3/E3->E5/E5->E6 handoff; all required drain/restore/cancel races pass repeatedly. The exact profile defaults disabled and its kill switch reaches transition plus E2/E3/E5/E6 zero before revoking binding/grant and restoring fault-only fallback. Both VMCALL and Phase 42 VMREAD closures were replayed on their named clean subjects before later evidence was recorded.

## 2026-08-09 PR-A Sequencing Result

PR-A is closed only as D2 v2 governance/negative substrate: immutable schemas,
binary canonical bytes and digests, exact-byte/SHA validation, logical
owner/architecture review roles, CODEOWNERS matching policy, append-only
revocation/supersession and the complete negative matrix now exist. The
repository still has no CODEOWNERS file, completed review evidence, attributable
accepted SpecV2/AcceptanceRecordV2 pair, production owner/capability registry or
generated operation lookup.

Therefore the four states remain distinct:

1. Phase 38 architecture/policy values are decided.
2. PR-A machine structure is validator-tested.
3. An attributable accepted instance is absent.
4. Runtime authority is absent.

PR-A completion does not automatically open PR-B. PR-B may be considered only
after explicit scope authorization and real repository attribution; even then it
may materialize governance/registry inputs only. The later separately authorized
PR-C may materialize immutable O1/operand identity while E2-E7 and backend remain
denied by their later gates.

## 2026-08-09 PR-B Sequencing Result

The repository owner separately authorized PR-B. Commit A fixes the exact
SpecV2 and CODEOWNERS blob; the later AcceptanceRecordV2 binds that SHA/digest,
the same attributable repository principal in both logical review roles, stable
owner/capability allocation metadata and one generated exact policy lookup.
Machine D2 is accepted. PR-C now provides a non-capability O1 and immutable
fault-only operand snapshot. No live owner service, capability grant, E2,
backend, completion or retire authority exists. E2 is not opened
automatically.

## ISE-ROLLOUT-ORDER-GATE-17 - Closure Record

Closure date: 2026-06-18.

State: closed `SEQUENCING-ONLY / READINESS-ONLY / NO-IMPLEMENTATION-PERMISSION`.

This closure records the rollout order only. It does not accept an owner RFC/ADR, does not allocate an exact VMCALL leaf, does not convert closed audit records into implementation permission, does not permit bundling an RFC draft with backend execution, and does not open backend execution, completion publication, retire publication, mutation, migration payload authority, SecureCompute activation, lane/stream passthrough, nested execution, or compiler emission.

| Rollout surface | Current result | Authority owner | Evidence class | Boundary |
| --- | --- | --- | --- | --- |
| docs/process PRs | may land as readiness documentation | none; process sequencing only | documentation/readiness evidence | no runtime authority, no owner acceptance |
| negative/static gate PRs | may land before or with future implementation | conformance detects only | denial/static evidence | green tests and scans are not implementation permission |
| owner RFC/ADR PR | required before production code | neutral runtime owner only | attributable owner decision evidence | draft, silence, handoff, response audit, or closure record is not acceptance |
| backend owner implementation PR | blocked until accepted exact-owner RFC/ADR | neutral runtime owner plus exact operation | implementation evidence after acceptance | cannot include unrelated VMREAD widening, VMWRITE, SecureCompute, nested, lane/stream, compiler, or migration payload authority |
| completion route/fence PR | future-gated after backend execution authority | neutral completion/route owner | route/fence evidence | backend success is not completion publication |
| retire PR | future-gated after completion publication and explicit retire rule | neutral retire owner | retire evidence | completion publication is not retire publication |
| release/backlog PR | release wording/backlog only until implementation evidence exists | release gate owner plus runtime owners | release review evidence | release wording cannot create activation |

Closure invariants:

- PR order is sequencing guidance, not authority.
- Closing audits, baselines, handoffs, response checks, conformance gates, or backlog rows is not implementation permission.
- Negative/static gates may precede implementation, but passing them does not authorize a positive path.
- Any production code PR must point to an accepted owner-specific RFC/ADR with exact operation, owner, value source, capability policy, evidence class, migration class, completion policy, retire policy, denial reasons, and adjacent negative tests.
- Draft RFC text, recommended first path, or owner-map readiness cannot be merged together with backend execution as a shortcut around neutral owner acceptance.
- Split sequencing remains the default: owner first, backend execution second, completion route/fence third, retire last.

## 2026-08-06 Active Dependency Map

The June closure remains sequencing-only and is not implementation permission. The active model separates build/readiness dependencies from the per-attempt runtime chain.

Build/readiness dependency:

```text
VirtualizationDecisionSpecV2 + VirtualizationDecisionAcceptanceRecordV2
  -> D2 accepted
  -> O1 loaded

E0 evidence + E1 implementation readiness
  -> production composition may be implemented
```

Per-attempt runtime chain:

```text
canonical decode
  -> generic legality / owner-domain guards
  -> Stage A -> Stage B -> E1
  -> one-time VirtualizationOperandSnapshot
  -> D2/O1 exact operation resolution
  -> RuntimeBoundaryAdmission / neutral trap / NeutralTrapResult
  -> SafetyVerifier E2
  -> DomainHypercallRuntimeExecutor
  -> E3
```

E4 is proof that this chain is the only canonical production composition to E3; it is not a runtime authority stage after E3. Atomic completion plus E5, E6 retire, and E7 drain/restore/determinism follow E3. The optional Compiler Gate and exact-scope Release Gate are rollout governance, not E-numbered authority stages.

No phase may be opened by closing a readiness check. A phase opens only when every listed dependency is evidenced and its named owner accepts that phase's responsibility. An unassigned owner means `BLOCKED`, not implicit ownership by VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, docs, tests or release review.

### Separate Research Prototype Lane

The prototype lane is deliberately outside the production E0-E7/D2/O1 dependency model and cannot satisfy any of its arrows:

`P1 TESTING-only live-E1 no-state probe (CLOSED) -> P2 default-off TESTING-only canonical composition experiment (CLOSED)`.

P1 is closed in Phase 36. P2 is closed in Phase 37: the same probe is optionally composed at the canonical issue/materialization seam through a removable TESTING-only partial hook, with typed context-generation binding and exact-once receipt. It remains default-off even in TESTING and absent without TESTING. It grants no numeric leaf, production backend, compatibility frontend/dispatcher connection, completion or retire. Production machine D2 is now closed independently by PR-B; E2-E7 remain blocked, compiler and release gates remain unopened, and no P3 research pool is authorized by this map.

### E0 - Reproducible Evidence Baseline

- Owner: repository/evidence maintainer; process owner only, never runtime authority.
- Inputs: current `HEAD`, complete Git status, tracked plan/guard inventory, source/test/doc hashes, toolchain version.
- Allowed changes: documentation, manifest generation/validation, clean-checkout and source-inventory tests.
- Forbidden bypasses: external archive substitution, CI side-load, claiming a dirty snapshot is reproduced by `HEAD`, or treating a manifest as execution proof.
- Required negative checks: wrong SHA/hash, missing tracked file, dirty/untracked source, unexpected origin, and external path must fail closed.
- Required positive checks: a clean checkout reproduces all manifest anchors and runs the plan guard without external files.
- Exit: one clean subject SHA contains the inspected source and prior plan/guard baseline; a repository-local manifest records the subject/tree, hashes, canonical source layout and local commands without requiring an impossible self-referential commit hash. Current state: `CLOSED/EVIDENCE-ONLY` for E1-containing local subject `55807df77978a960382fa913dda4e7ace0093a6b`; its local Baseline passed, `CloseToHSL` is tracked and `CloseToRTL` is obsolete/untracked.
- Rollback: revert docs/tests/manifest only; runtime behavior is unchanged.
- Next pool: closed by the E1 implementation and tests; no backend work was opened.

### E1 - Typed SafetyVerifier Admission

- Owner: existing canonical legality/SafetyVerifier owner; this plan does not appoint a new owner.
- Inputs: closed E0 plus the identity and invalidation model in `32_e1_nonforgeable_safetyverifier_admission_contract.md`.
- Allowed changes: an immutable, non-publicly forgeable certificate and SafetyVerifier issuance/validation path, with all VMX execution still faulting.
- Forbidden bypasses: caller-supplied validation booleans as authority; compiler/test/generated proof token; VMX/FSP/lane/stream as issuer; a public/default constructible certificate.
- Required negative checks: forged/default/stale, cross-VT, cross-domain, source/working-slot mismatch, replay-epoch mismatch, and direct-service bypass denied.
- Required positive checks: SafetyVerifier can issue one correctly bound certificate while dispatcher still preserves legacy fault behavior.
- Exit: typed certificate exists, only the canonical SafetyVerifier-backed internal service can issue a live-valid instance, the canonical issue-packet lane-7 path transports it without identity reconstruction, and no backend is enabled. Current state: `CLOSED/FAULT-ONLY`.
- Rollback: disable issuance/consumption and retain byte/trace-equivalent VMX fault.
- Next pool record: the clean containing-SHA refresh, fail-closed Phase 34 substrate, PR-B machine D2, PR-C identity and PR-D E2 admission are now closed in sequence; E1 itself remains fault-only.

### D2 - Accepted Neutral Owner Decision And Exact Numeric Leaf

- Owner: architecture role `DomainHypercallRuntimeOwner` and stable allocation `HCOWNR`/policy 1/epoch 1 are accepted by the repository-owner ADR in Phase 38 and are independent from the compatibility authority plane. Production registry materialization and attributable owner/architecture review evidence are absent; VMX frontend self-approval is forbidden.
- Inputs: E0 and E1; `VirtualizationDecisionSpecV2`, later `VirtualizationDecisionAcceptanceRecordV2`, canonical encoders/digests and validator.
- Accepted decision: namespace `HybridCPU.VMCALL.Runtime.v1`, 16-bit width, `0x0000` invalid, exact leaf `0x0001`, operation `PROBE_NO_STATE_V1`, `Rs1` full value, `Rs2=x0`, `Rd=x0`, no-state/no-payload and `DrainOnly`.
- Accepted secure boundary: the first operation is denied in SecureCompute domains; ordinary VMCALL capability/evidence never transitively opens the separate SecureCompute authority plane.
- Accepted exact policy: typed DomainGranted capability bit 41 with NonDelegable/RuntimeRevocable/DomainLocal/HostOnly/NeverProject and generation binding; evidence None; execution-domain-only/non-zero tag/no memory-I/O-address-space; SecureCompute denied; DenyBeforeExecution/DenyAttemptReplay; DrainOnly; HostOwnedRuntimeEvidence/HostOwnedNonMigratable/NeverProject completion; atomic E3-to-record+E5; precise E5-bound no-state retire; deny all adjacent leaves.
- Allowed changes: machine governance substrate and later owner-authored accepted artifacts. The spec contains owner/operation identity, exact numeric leaf, argument/result ABI including width/high-bit policy, value source, state/effect class, capability/evidence policy, denial taxonomy, cancellation, migration, completion and retire contracts. The acceptance record references the spec SHA+digest and attributable required-reviewer/CODEOWNERS evidence; it does not contain its own future commit SHA.
- Forbidden bypasses: treating this ADR, opcode `259`, compatibility `ushort`, VMFUNC leaf 1, `VmxExitQualification`, fixture values, silence, handoff, monitor closure or backlog priority as a machine-validated accepted decision or live runtime capability.
- Required negative checks: malformed/missing/self-referential spec SHA, digest mismatch, missing/zero/wrong OwnerId, missing attribution, reviewer-role mismatch, compatibility self-approval, draft/revoked/superseded state, invalid lineage, absent/duplicate/cross-namespace numeric leaf, incomplete owner map, unknown policy enum, noncanonical bytes, high bits and adjacent leaves all remain denied. Phase 35 closes only the older v1 local negative checks.
- Required positive checks: machine-validated acceptance record over byte-identical immutable spec content, attributable owner and architecture review and exactly one namespace-scoped numeric leaf. This remains governance evidence only.
- Exit: accepted spec+record, repository owner/capability allocation registries, matched review mapping and exact generated lookup exist. Current state: `ATTRIBUTABLY MATERIALIZED / ACCEPTED POLICY ONLY`; PR-B closes machine D2 without runtime authority.
- Rollback: rejection, withdrawal or supersession leaves all VMX execution faulting and invalidates dependent work.
- Next pool record: PR-B and PR-C closed, and the repository owner later explicitly authorized bounded PR-D -> PR-I. PR-D E2 is admission-only and does not imply a backend.

### O1 - Immutable Operation Owner Policy Snapshot

- Owner: `DomainHypercallRuntimeOwner` defines the accepted policy; runtime decision loading materializes an immutable snapshot. O1 is policy, not permission.
- Inputs: one machine-validated `AcceptedVirtualizationDecision` from D2.
- Allowed changes after D2: bind DecisionId/spec digest, OwnerId/policy version, namespace/operation, leaf width/exact leaf, operand/result ABI, operation/effect class and capability/evidence/domain/cancellation/replay/migration policies.
- Forbidden bypasses: constructing O1 from docs, compatibility payload, VMX/VMCS/`VmxCaps`, compiler metadata, test fixtures or an unvalidated spec; treating O1 as E2.
- Required negative checks: wrong decision/spec digest, zero/wrong owner, stale policy, wrong namespace/leaf/ABI, revoked/superseded decision and mutation after load deny.
- Required positive checks: one immutable deterministic O1 materializes only from one valid accepted decision and contains no runtime execution bit/token.
- Exit: O1 exists as a non-capability policy snapshot. Current state: `CLOSED/IMMUTABLE POLICY ONLY`; it loads only from the exact accepted D2 and has no execution bit or live grant.
- Rollback: unload/revoke the policy snapshot and preserve E1 fault-only behavior.
- Next pool: canonical operand snapshot.

### Canonical Virtualization Operand Snapshot

- Owner: canonical operand materialization/register-read owner; VMX payload, backend and compatibility frontend are consumers or projections, never the value owner.
- Inputs: machine D2, O1 and one live E1-bound canonical attempt.
- Allowed changes: capture `Rs1` selector and full architectural value exactly once after E1, verify `Rs2=x0`, `Rd=x0` and upper leaf bits zero, and bind attempt, VT/domain/context, source/working slot, bundle/replay epoch and restore generation in an immutable object with deterministic identity/digest.
- Forbidden bypasses: treating `VmxExitQualification.Leaf` as a runtime value, silent truncation to `ushort`, re-reading registers in E2/E3/retire, reconstructing operands from current lane/VT, or allowing FSP/donor state to replace the admitted carrier.
- Required negative checks: nonzero Rs2/Rd, zero/high-bit/wrong/adjacent leaf, changed register value, selector/value mismatch, stale replay/restore generation, wrong VT/domain/context/slot, FSP donor substitution, mutation after snapshot and digest mismatch deny.
- Required positive checks: one snapshot preserves exact selector/full-value pairs for the admitted `0x0001` attempt without opening backend, completion or retire.
- Exit: immutable operand identity exists for later E2 validation. Current state: `CLOSED/FAULT-ONLY E1-BOUND IDENTITY`; the canonical seam captures the full `Rs1` value once, binds O1/attempt/carrier/restore generation, and still executes VMCALL as a fault.
- Rollback: remove/disable snapshot materialization and preserve E1 fault-only behavior.
- Next pool: E2 operation-specific SafetyVerifier admission.

### E2 - Operation-Specific SafetyVerifier Admission

- Owner: existing canonical legality/SafetyVerifier owner consumes D2; the D2 decision owner cannot mint a live certificate through the compatibility frontend.
- Inputs: E0-E1 plus accepted D2 spec/acceptance record/registry, live O1 owner-policy snapshot, live immutable operand snapshot and live VT/domain/capability/attempt/restore generations. Evidence and address-space identities are absent by contract for `PROBE_NO_STATE_V1`.
- Allowed changes after D2: replace the Phase 34 unissued opaque E2 reservation with SafetyVerifier-exclusive issuance that internally performs common runtime admission for exactly the accepted leaf and live attempt; bind capability generation and keep backend/completion/retire false.
- Forbidden bypasses: manifest/acceptance-record-as-token, caller booleans, register selector as leaf value, compatibility-width truncation, register-file re-read, public/default construction, VMX/FSP/lane/compiler/test issuance, or reconstructing identity after scheduling.
- Required negative checks: absent/draft/withdrawn D2, wrong/adjacent/high-bit leaf, stale registry/O1, operand mutation, cross-VT/domain/attempt, unexpected address-space binding, revoked capability, changed evidence/restore epoch and duplicate consumption denied.
- Required positive checks: canonical SafetyVerifier captures and validates the accepted runtime leaf value for one fault-only attempt while execution still faults.
- Exit: one live production E2 certificate is non-forgeable, D2-bound and operation-specific; no backend is reachable. Current state: `CLOSED/ADMISSION-ONLY/FAULT-ONLY`. SafetyVerifier exclusively issues and validates E2 from the exact accepted D2/O1, live E1 and canonical operand, generation-bearing typed grant, runtime-root epoch and live restore-generation owner, and internally runs common execution-only runtime admission. The Phase-34 boolean request remains denied and the Phase-36 TESTING prototype cannot satisfy this contract.
- Rollback: disable E2 issuance and preserve the E1 deterministic fault path.
- Next pool: closed PR-E E3 backend for that exact leaf only. It is default-off, isolated from production composition and cannot publish completion or retire.

### E3 - Neutral Backend

- Owner: the accepted neutral runtime owner from D2; VMX frontend cannot own or execute it.
- Inputs: E0-E2 complete.
- Allowed changes: one backend executor for the exact leaf consuming E2 exactly once and returning an opaque owner-bound E3 with canonical non-zero no-effect/no-result digests; no completion or retire publication.
- Forbidden bypasses: `BackendExecutionAuthorized: true`, public bool authorization, service locator/callback from compatibility code, broad leaf ranges, host handles in results.
- Required negative checks: unknown/adjacent leaf, wrong capability/evidence/domain, forged/stale certificate and duplicate execution denied.
- Required positive checks: exact service-level leaf result is deterministic and contains no completion/retire permission.
- Exit: exact backend semantics and execution receipt pass owner-specific tests behind a default-off kill switch. Current state: `CLOSED/ISOLATED E3/NO PUBLICATION`; one live E2 can be consumed once only by the exact executor, and E3 grants neither completion nor retire.
- Rollback: disable executor and return the pre-existing deterministic fault before effects.
- Next pool: PR-F E4 canonical composition, conditional on a clean PR-E containing commit and unchanged green rollback gates.

### E4 - Canonical Pipeline Composition

- Owner: canonical ISA/decode, SafetyVerifier and runtime composition owners jointly; compatibility frontend remains non-authoritative.
- Inputs: E0-E3 complete.
- Allowed changes: compose the verified attempt-bound chain through canonical decode/schedule/execute so that E3 is reachable only through it; introduce a new neutral `InvokeHypercall`-equivalent operation with `Source=RuntimeService`, `AuthorityClass=NoStateExecution`, `IsProjectionOnly=false` and exact typed capability. Propagate `ExecutionOnly` through every common-legality consumer without granting mutation privilege.
- Forbidden bypasses: dispatcher calling `AdmitVmCallTrapProjection`; direct service as an ISA path; reconstructing identity from current lane/VT; writing result/completion/retire here.
- Required negative checks: direct service cannot enter production composition; disabled flag and all other VMX opcodes remain byte/trace-equivalent faults; FSP donor/source identities cannot alias.
- Required positive checks: canonical decode -> legality/owner-domain guards -> Stage A -> Stage B -> E1 -> one-time operand snapshot -> D2/O1 resolution -> runtime admission/neutral trap -> E2 -> E3 reaches the backend exactly once for the accepted leaf, without architectural publication.
- Exit: E4 evidence proves that this is the only canonical production composition to E3, with completion and retire still false. Current state: `CLOSED/EXCLUSIVE CANONICAL COMPOSITION/FAULT-ONLY RETIRE`; E4 is a composition property, not a post-E3 runtime token.
- Rollback: kill switch returns to deterministic fault before publication.
- Next pool: PR-G route/publication policy and atomic completion-owner work, conditional on a clean PR-F containing commit and unchanged green rollback gates.

### E5 - Completion Publication

- Owner: neutral completion-publication owner; backend owner result is input, not publication authority.
- Inputs: E0-E4 complete and a live owner-bound E3 backend execution receipt.
- Allowed changes: route E3 through `TrapCompletionRouteService`, obtain a `TrapCompletionPublicationFence` policy decision, then let the neutral completion owner atomically publish exactly one `CompletionRecord` plus one opaque attempt-bound E5 `CompletionPublicationToken`. Completion-only route remains retire-denied.
- Forbidden bypasses: frontend or admission handler creates `CompletionRecord`; `CompletionPublicationAllowed`, route descriptor/boolean/evidence enum or a constructible compatibility factory stands in for the E5 token; coupled positive descriptor used from VMX code.
- Required negative checks: boolean-only authorization, direct positive fence construction, E5-before-record, record-without-E5, non-atomic or duplicate publication, forged/stale/cross-VT/domain/attempt E5 and any non-retire consumer are denied.
- Required positive checks: the neutral owner atomically publishes one completion and its matching E5 while retire remains false.
- Exit: completion publication is exact-once and independently evidenced; no architectural state change. Current state: `CLOSED/ATOMIC NEUTRAL COMPLETION+E5/RETIRE-DENIED`.
- Rollback: stop new operations, discard unpublished completions, drain/fault according to the accepted contract.
- Next pool: E6 retire.

### E6 - Retire Publication

- Owner: canonical CPU retire owner.
- Inputs: E0-E5 complete; live ROB/post-Stage-B attempt identity, one published `CompletionRecord`, and its one unconsumed owner-bound E5 token.
- Allowed changes: consume E5 exactly once into an opaque `VirtualizationRetireGrant`-equivalent issued by the canonical retire owner and apply the exact ABI effect only at precise retire.
- Forbidden bypasses: `VmxRetireEffect`, completion presence, route bool, evidence or migration enum as sufficient success proof; frontend/direct service register or PC writes.
- Required negative checks: stale/wrong VT/domain/source/working slot/attempt, x0, duplicate, squash and exception ordering denied.
- Required positive checks: one exact result retires once; completion-only test leaves architectural state unchanged; FSP on/off and SMT interleavings preserve result/order.
- Exit: precise, exactly-once no-state retirement is proven for the exact leaf only. Current state: `CLOSED/EXACT E6/NO-STATE/COMPATIBILITY-FAULT-ONLY`.
- Rollback: disable leaf; drain or fault outstanding attempts before any partial retire.
- Next pool: E7 migration/determinism.

### E7 - Migration And Determinism

- Owner: neutral migration and evidence owners.
- Inputs: E0-E6 complete.
- Allowed changes: first-slice `OperationMigrationPolicy=DrainOnly`, `CompletionMigrationClass=HostOwnedNonMigratable`, domain drain gate, restore generation and replay/evidence schemas without host handles. Checkpoint occurs only after drain, so live E5 does not migrate.
- Forbidden bypasses: serialize compatibility DTOs, route/retire grants, compiler facts, scheduler/FSP evidence, lane tokens, debug traces or host callbacks as authority.
- Required negative checks: checkpoint at disallowed in-flight boundary, stale certificate after restore, host evidence leak and duplicate replay denied.
- Required positive checks: pre-operation/post-retire checkpoint equivalence, no live-E5 serialization, exact completion-class enforcement, two-run trace equality, FSP on/off and SMT deterministic architectural result.
  - Exit: migration class and rollback drill are SHA-bound and repeatable. Current state: `CLOSED/EXACT E7/DRAIN-ONLY/POLICY-IDENTITY-ONLY`.
- Rollback: disable migration for active domains and require drain.
- Next pool: PR-J exact-scope Release Evidence / Release Gate. Compiler support is excluded and stays closed by default.

### Compiler Gate - Optional Emission

- Owner: compiler owner for emission only; runtime re-verification remains authoritative.
- Inputs: E0-E7 complete and explicit decision that compiler emission is in scope.
- Allowed changes: emit the exact instruction intent and non-authoritative sideband only.
- Forbidden bypasses: compiler-created certificate, validation bit, backend/completion/retire token, owner grant or migration authority.
- Required negative checks: forged/stale sideband ignored or denied; restored/replayed carrier reverified; compiler-off path unchanged.
- Required positive checks: emitted exact leaf reaches the same canonical path and golden carrier contains no authority bits.
- Exit: optional emission is separately gated and cannot change runtime legality. Current state: `CLOSED-BY-DEFAULT / NOT A PR-J DEPENDENCY`.
- Rollback: compiler flag off; runtime exact-leaf support, if separately released, remains independently testable.
- Next pool: exact-scope Release Gate.

### Release Gate - Exact-Scope Limited Release

- Owner: release review records evidence; it is not a runtime owner. Runtime authority remains with all accepted owners above.
- Inputs: E0-E7 complete; containing-SHA provenance; code-level cross-registry quiescence proof; an exact activation, kill-switch and rollback contract; SHA-bound verification; and an immutable exact-scope release record. Compiler Gate is excluded.
- Allowed changes: machine-readable claim naming the exact leaf/mode and explicit exclusions.
- Forbidden bypasses: broad VMX/virtualization activation claim; VMREAD ISA, VMWRITE, nested, SecureCompute, lane/stream, active in-flight migration or compiler claims unless separately completed.
- Required negative checks: all adjacent VMX surfaces remain denied; race tests cover drain vs E2->E3, E3->E5 and E5->E6, restore vs new E2, cancel vs E3 publication, cancel vs E5->E6, and two domains draining concurrently; kill switch restores deterministic fault.
- Required positive checks: a per-domain lifecycle epoch/gate or transition-in-flight counter is included in the quiescence predicate; the exact end-to-end path, completion/retire separation, migration/determinism and rollback pass on the designated release-candidate SHA.
- Exit: an immutable release record claims only `limited scoped activation of PROBE_NO_STATE_V1 only`. Current state: `CLOSED / DEVELOPMENT-LOCAL EXACT PROFILE / DEFAULT-DISABLED UNTIL EXPLICIT PROFILE ACTIVATION`.
- Rollback: close new E2, wait for zero transition-in-flight plus zero E2/E3/E5/E6, revoke the exact binding/grant, and preserve deterministic fault-only compatibility behavior.
- The post-release monitoring baseline for that exact slice is closed: the 200-iteration structured-counter/trace run of `prj-exact-release-activation-rollback`, `vmcall-denied`, and `e1-fault-transport` passed. It is diagnostics/evidence only and leaves activation default-disabled. No automatic activation-expansion pool exists; every expansion restarts at E0/D2 as applicable.

## Owner Of Authority

PR sequencing is process authority only. Runtime authority remains with neutral owners.

## What Can Be Implemented

Historical June PR grouping below is retained for traceability only. Where it conflicts with the current E0-E7/D2/O1 model and separate compiler/release gates, the current model is authoritative. Recommended PR order at the time was:

1. Docs baseline and process:
   - `00_virtualization_activation_refactoring_index.md`
   - `01_current_state_and_gap_matrix.md`
   - `02_global_forbidden_regressions_and_static_gates.md`
   - `03_owner_specific_rfc_adr_process.md`
2. VMREAD and denied-owner documentation:
   - `04_vmread_projection_completion_and_denial_matrix.md`
   - `05_privileged_execution_state_owner_rfc.md`
   - `10_vmwrite_neutral_owner_policy.md`
3. Recommended first active-path RFC:
   - `06_neutral_hypercall_backend_owner_rfc.md`
   - `07_vmcall_success_path_activation_plan.md`
   - `08_trap_completion_route_publication_plan.md`
   - `09_retire_publication_activation_plan.md`
4. Boundary docs:
   - `11_nested_child_intent_owner_rfc.md`
   - `12_memory_io_iommu_lane_stream_boundary_activation_plan.md`
   - `13_securecompute_virtualization_boundary_plan.md`
   - `14_compiler_no_emission_to_controlled_emission_gate.md`
   - `15_migration_checkpoint_restore_authority_plan.md`
5. Tests/static gates:
   - `16_conformance_negative_positive_test_matrix.md`
6. Plan-doc guards:
   - `HybridCPU_ISE.Tests/VmxRefactoring/VirtualizationActivationPlanAuditGuardTests.cs`
7. Release/backlog:
   - `18_release_gate_for_limited_runtime_virtualization.md`
   - `19_open_decision_backlog.md`

## What Remains Denied/Future-Gated

Activation-carrying runtime code changes remain out of scope until the owner-specific RFC/ADR PR is accepted and negative tests land. Denied or future-gated scaffolding may land only when it cannot authorize backend execution, completion publication, or retire publication and is covered by adjacent denial tests.

## Forbidden Shortcuts

- Combining first backend implementation with RFC draft in a way that makes review optional.
- Landing positive tests without negative adjacent-state tests.
- Landing release-gate wording before implementation evidence exists.
- Treating docs-only PR as activation.
- Treating plan documentation guards as runtime implementation.

## Required RFC/ADR

The first owner-specific RFC/ADR should be the neutral hypercall backend owner. It should land after baseline/static gate PR and before implementation.

## Code Anchors

Same as Phases 06-09 for the first active-path implementation:

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`

## Documentation Anchors

- All files in this directory.
- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/16_external_audit_activation_readiness_addendum.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`

## Required Tests

First test PR should include negative/static tests only. Positive tests are added or enabled only in the implementation PR after RFC acceptance.

## Required Static/Source Scans

Run scans from Phase 16 before every PR that changes virtualization, runtime, compiler, migration, SecureCompute, or lane/stream code.

## Migration/Evidence Classification

PRs that touch runtime state must update migration/evidence classification in the same PR. Docs-only PRs may define classification requirements without adding payloads.

## Completion/Retire Implications

Completion/retire implementation must be split from backend owner implementation unless the RFC/ADR and test plan prove the whole chain in one reviewed PR. Default recommendation is split: owner first, route/fence next, retire last.

## Exit Criteria

- PR sequence is explicit.
- First production code PR cannot bypass RFC/ADR.
- Tests/static gates precede or accompany positive implementation.

## Dependency On Previous/Next Phase

Depends on Phases 01-16. Phase 18 defines the final release gate.
