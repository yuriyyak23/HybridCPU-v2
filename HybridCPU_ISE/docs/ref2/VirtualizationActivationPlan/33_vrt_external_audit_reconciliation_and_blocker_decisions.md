# Phase 33 - VRT External Audit Reconciliation And Blocker Decisions

Status: `AUDIT RECONCILED / RECOMMENDATIONS TRIAGED / NO ACTIVATION PERMISSION`.

Current-state precedence: this audit reconciliation is historical. Its absence/blocker findings do not participate in current-state evaluation; `VirtualizationActivationStatusV1.json` is authoritative for current D2/O1/E1-E7 and release gates.

## 2026-08-09 Current-State Addendum

This phase records the pre-PR-A audit checkpoint and retains its findings as
historical evidence. Phase 38 later decides the exact profile, PR-A closes v2
validation, PR-B closes attributable machine D2, and PR-C closes non-capability
O1/operand identity. Those later closures supersede candidate/absence findings
in this file but preserve its authority separation: P1/P2 remain TESTING-only
and cannot authorize the production path. Later PR-D through PR-I closed exact
E2-E7 independently; later PR-J closes the development-local exact-profile release evidence while keeping activation default-disabled and broad activation denied.

## 2026-06-11 Audit Contract

- File name: `33_vrt_external_audit_reconciliation_and_blocker_decisions.md`.
- Purpose: reconcile the VRT research reports, including `deep-research-report 2.md`, with the current local code, tests, WhiteBook, internal architecture guidance and active activation plan.
- Status: evidence and planning update only; no owner is appointed, no RFC/ADR is accepted and no production path is opened.
- Scope: current execution/projection/scaffolding classification, audit verdicts, governance meaning of neutral ownership, D2/E2/E3 separation, first-slice recommendations, phase dependencies and static denials.
- No-goals: no exact VMCALL leaf allocation or reservation, no backend executor, no allowed backend admission, no production canonical composition, no completion record publication, no retire publication and no broad virtualization claim. Closed TESTING-only P2 is model evidence only.
- Code anchors: `SafetyVerifier.VirtualizationAdmission.cs`, `MicroOp.IO.cs`, `MicroOpScheduler.SMT.cs`, `MicroOpScheduler.ResearchVirtualizationCanonicalComposition.cs`, `ExecutionDispatcherV4.VmxCompatibility.cs`, `CPU_Core.PipelineExecution.VmxRetire.cs`, `VmxInstructionPayload.cs`, `VmxExitQualification.cs`, `VirtualizationOperationDecisionManifest.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `HypercallBackendAdmissionPolicy.cs`, `TrapCompletionRoutePolicy.cs`, `CompletionRecordCompatibilityProjection.cs`.
- Authority owner: existing neutral runtime owners only; VMX/VMCS, compatibility DTOs, generated artifacts, compiler, migration, lanes, streams, tests, documents and this reconciliation are not authority.
- Required RFC/ADR: every positive path still requires an attributable owner-approved architecture decision and full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: every material report claim has a current-worktree verdict and every adopted recommendation preserves fail-closed behavior and the strict dependency order.
- Tests/static scans: plan corpus guards, canonical-caller scans, exact-leaf absence, missing-owner denial, completion-constructor isolation, positive-route absence and E1 anti-forgery guards.
- Risks: treating a plausible recommendation, candidate owner name, candidate semantic leaf, illustrative numeric value, direct compatibility service result, positive-shaped DTO or green readiness gate as production authority.
- Next-gate dependency: production work requires a D2 owner-approved decision artifact; before D2 only fail-closed substrate/governance work and the separately bounded TESTING-only research lane from Phase 36 are permitted.

## Evidence Scope And Provenance Verdict

The reports were rechecked again locally on 2026-08-08. The inspected local repository has `HEAD` `ddfffa2d7b86fb3ece21f8d21c6447ae47ee3868`; the active source tree is `HybridCPU_ISE/CloseToHSL`. The working tree is dirty and contains the Phase 36/37 TESTING-only research lane, diagnostics-console work and user-owned documentation/archive changes. Therefore current-worktree observations are not clean-SHA evidence.

Clean evidence remains scoped to its recorded subjects: E1 is contained by `55807df77978a960382fa913dda4e7ace0093a6b`, and the Phase 34 fail-closed D2/E2 substrate is contained by `a594d10abcbe8593d23fed16310af30706893452`. Neither clean subject contains the current Phase 36/37 worktree. This provenance gap blocks a release/evidence claim for P1/P2 but does not convert either prototype into production authority. No remote Git operation is used or required by this local reconciliation.

Both VRT reports reason from remote SHA `d3814d1f332f083034d3b245f807a45f97792070`. That object is not the local subject used by the active plan. Claims about the absence of `VirtualizationActivationPlan` or exact remote file contents are provenance-specific and cannot override current local files. No external archive, remote page or copied plan file was used to satisfy a local guard.

The current CI workflow is present at `.github/workflows/ci.yml`, and VMX generated-projection drift is checked by `tools/VMXProjectionLineage/VerifyProjectionLineage.ps1`. This partially refutes a broad claim that no CI/generation evidence exists. It does not prove a clean SHA containing Phase 36 or any future production D2/E2-E7 implementation, compiler gate or release gate.

## Verified Runtime Classification

| Class | Verified current surface | What it proves | What it does not prove |
| --- | --- | --- | --- |
| pipeline-executed | frozen decode -> `VmxMicroOp` -> hard-pinned lane 7 -> E1 certificate transport -> `VmxRetireEffect.Fault` -> canonical fail-closed retire | VMX carriers are recognized, attempt-bound at E1 and deterministically fault | successful VMREAD, VMCALL backend, completion or retire success |
| direct projection API | `AdmitVmReadProjection` and `AdmitVmCallTrapProjection`, used directly by tests and not called by the production dispatcher | field-local read-only projection and admitted-denied trap projection contracts | architectural instruction reachability or writeback |
| model/scaffolding | generated VMCS schema, positive route descriptors, fence DTOs, compatibility completion factories, nested/migration/compiler policies | vocabulary, policy shape and negative/isolated behavior | a live owner-bound positive chain |
| unreachable/isolated | successful-looking `VmxRetireEffect` factories and `CompletionRecord.FromCompatibilityExit` from production execution/pipeline/runtime composition | testable compatibility shape only | execution, publication or architectural success |

## Verdicts On Material Audit Findings

| Audit finding | Verdict | Current local evidence |
| --- | --- | --- |
| VMX is a frozen compatibility ABI/projection vocabulary, not the authority plane | **подтверждено** | WhiteBook principles/authority model and compatibility namespace layout; neutral domain, trap, completion and retire services own facts |
| Canonical VMX instruction flow remains fault-only | **подтверждено** | `VmxMicroOp.Execute` produces `SecurityPolicyViolation`; dispatcher returns `VmxFault`; retire calls `ApplyRemovedFrontendFailClosedEffect` |
| E1 is typed, attempt-bound and non-forgeable but cannot authorize a backend | **подтверждено** for current worktree | SafetyVerifier alone issues/validates a live opaque certificate; accepted leaf and later identities are typed absent; backend/completion/retire flags are false |
| VMREAD read-only value projection exists | **подтверждено частично** | direct service projects admitted completion/memory/execution-owned fields and guarded `GuestCr0`/`GuestCr4`; no canonical dispatcher caller or retire writeback exists, so this is not architectural VMREAD |
| `GuestCr0`/`GuestCr4` have guarded read-only projection | **подтверждено** | neutral privileged-state descriptor plus domain/address-space/epoch/visibility/migration/conformance gates; result explicitly denies backend, mutation, completion and retire |
| VMWRITE has no admitted positive path | **подтверждено** | current projection/access policy and conformance keep writes denied; no neutral write owner or canonical execution path exists |
| VMCALL has neutral trap projection but no production backend | **подтверждено** | handler passes `MissingNeutralOwner`, builds `ProjectionOnlyDenied`; backend decision enum has no allowed decision or executor |
| VMCALL request currently carries register selectors rather than the runtime leaf value | **подтверждено** | `HypercallLeafRegister` becomes decode `Rs1`; `VmxMicroOp`/payload bind register indices, not an operand value captured from canonical register state |
| An accepted owner-specific ADR and exact numeric VMCALL leaf exist | **опровергнуто** | only draft/denied owner scaffolding exists; no accepted manifest, exact-leaf registry, allowed admission or executor exists |
| Backend result, completion publication and retire publication are separate authorities | **подтверждено частично** | route/fence/result shapes separate the states, but current positive-shaped inputs are public booleans/descriptors rather than live owner-bound E3/E5/E6 tokens |
| `CompletionRecord` creation is already owner-only and non-forgeable | **опровергнуто** | compatibility factories accept a positive-looking fence DTO; caller search finds tests only, so they are isolated scaffolding but not a sufficient future production contract |
| `VmxRetireEffect` proves successful VMX execution | **опровергнуто** | canonical retire faults every valid VMX effect; the object is compatibility vocabulary, not an execution receipt or retire grant |
| Compiler-controlled VMX emission is production-proven | **не доказано** | no-emission/lowering policy shapes exist, but no production exact-leaf emission plus runtime revalidation chain is present |
| Nested virtualization is an active recursive runtime | **не доказано** | neutral intent/projection/conformance models exist and Shadow VMCS is fenced; no canonical admitted nested execution and no recursive owner graph are proven |
| Memory/IOMMU/lane/stream evidence can authorize virtualization | **опровергнуто** | those subsystems have neutral owners or evidence roles; no such artifact is accepted as VMX/backend/completion/retire authority |
| A new monolithic `VirtualizationRuntimeContext` is required | **подтверждено частично как design concern, не принято как solution** | the need for stable VT/domain/epoch owner bindings is valid, but `DomainRuntimeContext` and separate execution/memory/I/O/trap/capability owners already exist; a duplicate aggregate owner could violate ownership separation |
| The plan was absent and had to be substituted from an external archive | **опровергнуто для текущего HEAD; не доказано для reported remote SHA** | the plan is local and tracked at the clean subject; the reports used a different remote SHA and cannot establish the current working-copy inventory |
| Reproducible CI/generator evidence is wholly absent | **опровергнуто частично** | repository CI and generated projection drift verification exist; a clean containing SHA for the current E1 worktree and future activation slices remains required |

### `deep-research-report 2.md` Critical-Finding Addendum

| Audit finding | Verified verdict | Current local evidence and disposition |
| --- | --- | --- |
| Phase 37 P2 is already composed at the canonical issue/materialization seam | **confirmed, TESTING-only** | the partial hook is declared in `MicroOpScheduler.SMT.cs`, implemented only in a file beginning with `#if TESTING`, is default-off, and has no production dispatcher/completion/retire caller; the stale Phase 19 P2-open row is corrected |
| Current VMCALL qualification contains a runtime leaf value | **refuted** | `VmxInstructionPayload.FromDecodedRegisters` constructs `VmxExitQualification(rs1, ..., rs2)` from encoded selectors. No canonical register value is captured, so future E2 requires a separate immutable operand snapshot |
| The v1 D2 manifest is a sufficient final acceptance-provenance contract | **refuted** | `AcceptedCommitSha` is embedded in the same manifest that declares `Accepted`; it can validate shape but cannot non-circularly attest its own containing commit. No production consumer exists, so this is a governance blocker rather than an active runtime flaw |
| Existing `ushort Leaf` proves that production VMCALL ABI must be 16-bit | **not proven** | `VmxExitQualification` and `VmxRetireEffect.VmCall` are frozen compatibility vocabulary. Sixteen bits is a plausible D2 candidate, but deriving runtime authority from compatibility types would violate the WhiteBook owner rule; D2 must decide width and high-bit policy explicitly |
| Positive route/fence/completion DTOs are adequate future publication authority | **refuted** | public booleans and positive descriptors can construct completion records in direct calls; current callers are tests only. E5 must instead consume one live E3 receipt into a separate opaque consume-once token |
| A positive `VmxRetireEffect` can serve as execution or retire proof | **refuted** | positive factories are constructible, but production execution creates a fault and canonical retire maps every valid compatibility effect to a fault. Future E6 must be a separate canonical consume-once grant |
| The next research step should be P3 | **refuted** | P1/P2 already establish the bounded model shape. Without D2, another positive layer would form a shadow runtime; only governance/negative hardening is permitted |
| Public/remote SHA work is required in this local task | **not adopted for this scope** | clean containing-SHA provenance remains a release requirement, but the repository-owner instruction is local-only. No fetch/pull/push or remote substitution is permitted |

## Adopted Blocker Decisions

The following recommendations are accepted as plan rules because they match HybridCPU-v2 authority separation and current code:

1. **Neutral means external to the compatibility authority plane.** A neutral owner may live in this repository, but it must be owned/reviewed as a runtime domain service and must not be VMX/VMCS/`VmxCaps`-derived or self-approved by the compatibility frontend.
2. **Governance and runtime authority are distinct.** D2 is the attributable owner-approved architecture/ABI decision. E2 is a live operation-specific SafetyVerifier admission certificate created only after D2 inputs exist. E3 is an opaque backend execution receipt. None substitutes for another.
3. **The accepted D2 artifact allocates the leaf.** A versioned machine-readable decision manifest may be generated/validated as governance evidence; it is not runtime authority. The exact-leaf runtime registry is generated only after acceptance and remains an input to SafetyVerifier/runtime validation, not an authorization source.
4. **Positive authority uses opaque attempt-bound tokens.** E3 backend receipt, E5 completion-publication token and E6 retire grant bind owner, operation/leaf, VT/domain/address space, attempt/epoch, capability revocation and evidence/restore generations as applicable. Public bools and enums remain policy/scaffolding only.
5. **First successful slice should be state-minimal.** A no-state, no-payload, domain-local probe is the preferred semantic candidate because it avoids VMCS mutation, guest register result and migration payload. This is a recommendation to the future decision owner, not an accepted ABI.
6. **VMREAD opens separately and field-by-field.** The existing direct read-only projection remains projection-only. Any architectural VMREAD requires canonical operand capture, immutable owner read effect, precise destination writeback and retire proof.
7. **VMWRITE stays denied for the first limited release.** Future writes, if any, are owner-specific commands over neutral state, never a mutable VMCS store.
8. **Completion is exact-once and retire remains canonical.** The compatibility frontend/admission handler never creates `CompletionRecord`; only a neutral completion owner consumes a live E3 receipt. Only canonical retire may consume an E6 grant and publish architectural register/PC effects.
9. **Migration serializes owner state, not projections/tokens.** The preferred first slice is drain-only/`NoPayload`; restore invalidates outstanding E1-E6 identities.
10. **Compiler emission is optional and last.** A compiler may emit intent only after a SHA-bound accepted manifest and released runtime path exist; runtime revalidates every authority input.
11. **D2 acceptance uses two provenance artifacts.** The future immutable Decision Spec owns operation/ABI/policy content and its digest; a later Acceptance Record references that spec SHA+digest plus attributable review evidence and withdrawal/replacement lineage. A self-referential `accepted_commit_sha` is not accepted as final provenance.
12. **Runtime operands are captured once.** After canonical E1 materialization, one immutable snapshot binds operand register identities and actual values to attempt, VT/domain/context, bundle/replay and restore generation. E2 consumes the snapshot; E3/E5/E6 cannot re-read or reconstruct it from compatibility qualification.
13. **Compatibility success shapes never become authority.** `CompletionPublicationAllowed`, positive route descriptors, `CompletionRecord` factories and `VmxRetireEffect` remain data/scaffolding. Production completion and retire require separate opaque consume-once E5 and E6 artifacts derived in order from a live E3 receipt.

## Recommendations Not Accepted As Decisions At The 2026-08-08 Audit2 Checkpoint

This section is historical. Phase 38 supersedes the candidate-only disposition for the owner role, namespace, width, exact leaf and operation after a new external proposal was verified and the repository owner explicitly accepted it on 2026-08-09. The authority-separation and no-runtime-permission findings below remain in force.

- `DomainHypercallRuntimeOwner` was recorded here as a candidate; Phase 38 now accepts it as the neutral architecture role, while runtime OwnerId/reviewer attribution/service materialization remain absent.
- `HCPU_HV_PROBE_V1` is recorded as a preferred semantic candidate, not as an accepted operation name.
- The report's 16-bit leaf-width recommendation was a candidate at this checkpoint. Phase 38 accepts 16-bit width with mandatory full-register high-bit-zero validation and no silent truncation.
- The report's illustrative numeric value `0x48594350_00000001` is neither selected, allocated, reserved nor recommended by this plan. It must not appear as a production constant, enum member, allowlist entry or positive fixture before D2.
- A new `VirtualizationRuntimeContext` is not required by this plan. A later ADR may introduce a non-owning composition view only if it proves that it cannot duplicate `DomainRuntimeContext` or execution/memory/I/O/trap/capability ownership.
- Files 20-31 remain immutable historical snapshots for path-stable evidence. Their semantic state is compacted into Phase 19; relocating them is optional repository hygiene and grants no authority.

## Revised Dependency Model

```text
D2 immutable Decision Spec + later attributable Acceptance Record
  -> D2 accepted
  -> O1 immutable owner-policy snapshot

E0 reproducible clean-SHA evidence
  + E1 generic fault-only SafetyVerifier readiness
  -> production composition may be implemented

per attempt:
canonical decode / legality / Stage A / Stage B / E1
  -> immutable canonical runtime-operand snapshot for the live E1 attempt
  -> D2/O1 exact resolution
  -> runtime boundary admission / neutral trap result
  -> E2 live operation-specific SafetyVerifier certificate
  -> E3 exact-leaf backend + opaque execution receipt
  -> E4 proof that this is the only canonical production composition to E3
  -> atomic exact-once neutral completion publication + E5
  -> E6 canonical precise retire
  -> E7 migration, rollback and determinism

Compiler Gate = optional
Release Gate = exact-scope
```

Closing a readiness check never opens the next implementation phase. Each arrow requires its own owner, evidence, negative checks and rollback boundary.

## Permitted Pool Before D2

- update the owner-decision manifest schema and validation tool without inserting an owner, leaf or accepted state;
- split the future D2 schema into immutable Decision Spec and later Acceptance Record, with digest/reference/reviewer/withdrawal negative validation and no populated accepted instance;
- define an opaque E2 type whose constructor/issuer is inaccessible and whose D2-dependent identities are typed absent;
- define a neutral owner interface plus disabled/null implementation that can only deny;
- add negative conformance for missing/draft/withdrawn attribution, absent/duplicate leaf, adjacent leaves and forbidden owner planes;
- add scans preventing exact candidate numbers/names from entering production source;
- add state-machine vocabulary for `Absent`, `Draft`, `Accepted`, `Withdrawn` while ensuring only a verified accepted manifest can reach later issuance;
- refresh clean-SHA manifests and CI artifacts after repository-owner commits.
- build state-minimal/no-payload experiments only under `TESTING`, with SafetyVerifier-only admission, no numeric leaf, no production dispatcher/completion/retire connection and explicit diagnostics classification.

## Historical Restrictions Before D2 And Current Carry-Forward

- an allowed backend admission decision, `BackendExecutionAuthorized: true` or any backend executor;
- production composition from VMX compatibility handlers or direct projection services;
- a production exact-leaf registry containing an unaccepted value;
- handler/frontend `CompletionRecord` creation or a positive completion route;
- consumption of `VmxRetireEffect` as success evidence or any successful retire;
- VMREAD architectural writeback, VMWRITE, nested execution, SecureCompute authority, lane/stream authority, compiler emission or broad release wording.

## Historical Next Open Pool And Current Disposition

The repository owner committed E1 at `55807df77978a960382fa913dda4e7ace0093a6b`; the repeated local Baseline on that clean SHA passed. Phase 34 closed the governance/fail-closed substrate pool, and Phase 35 reproduced it at `a594d10abcbe8593d23fed16310af30706893452`. Phase 36 then closed prototype P1 only: a TESTING-only, no-state/no-payload probe with live E1 revalidation, SafetyVerifier-issued prototype admission and an opaque exact-once research receipt. That receipt is not production E2 or E3 and has no leaf, completion or retire authority.

P2 is closed in Phase 37 as default-off TESTING-only composition at the canonical issue/materialization boundary. The audit supports the Phase 37 `no P3` stop: another positive prototype layer would risk becoming a shadow runtime without D2 provenance.

Phase 38 supersedes this checkpoint's candidate-only decisions. Its historical next pool was completed by PR-A and separately authorized PR-B/PR-C. The current positive production gate is a separately authorized E2 review; no live capability/root/restore contour or E2 issuer exists, and E3, production execution connection, completion and retire remain unauthorized.
