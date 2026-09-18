# SecureCompute Activation Refactoring Index

Status date: 2026-08-07.

This corpus is the next SecureCompute activation runway. It does not replace the closed readiness corpus in `HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew/` and does not approve production activation by itself.

External architecture audit integration, 2026-06-12:

- source audit: `HybridCPU_ISE/docs/ref2/Prompts/securecompute deep-research-report (9).md`;
- the audit verdict `production SecureCompute activation not supported; readiness/policy/projection only` is confirmed by direct local source and test review;
- the audit's web-review limitations are superseded locally: `global.json` pins .NET SDK `10.0.201`, `Directory.Build.props` enables Debug test-support plumbing, and the active solution is `HybridCPU v2.slnx`;
- audit-derived operational gates are integrated into phases `02`, `03`, `13`, `14`, `15`, `17`, `19`, `20`, `21`, `22` and `23`;
- the restricted positive-path prerequisite chain is stricter than the audit shorthand: applicable migration, VMX zero-authority and compiler gates remain mandatory in addition to phases `13`, `14`, `20`, `21` and `22`.

Prior local revalidation snapshot, 2026-08-06:

- external audit SHA `f794ea378779fe153a206af5fe287571884b1bc4` is not present in the local Git object database and is therefore an input revision, not the current local baseline;
- that snapshot used `HEAD` `8bcdb637dcccd73bf3c700f2a7b09c69889aaf4f`; it is retained as history and is superseded by the 2026-08-07 reconciliation below;
- `global.json` requests SDK `10.0.201`, while the actual SDK used by this review is `10.0.204`;
- the reproducibility facts, revalidated findings, phase table, C0/C1/C2 registry and dependency-ordered small-change plan are in `24_audit_revalidation_and_dependency_order.md`.

External analysis reconciliation, 2026-08-07:

- reviewed `docs/ref2/1/SC/deep-research-report SC.md` and `docs/ref2/1/SC/Исследование-SecureCompute.md` in full;
- current local baseline is `b6d4871e0f06ebde07015e393c0d36af0362f506`; the analysis-reported revision `d3814d1f332f083034d3b245f807a45f97792070` is unavailable locally and is not treated as current evidence;
- confirmed findings and plan-level accepted recommendations are recorded in `25_external_analysis_reconciliation_and_recommended_decisions.md`;
- mandatory corrections are: disabled equivalence moves before semantic authority refactors, a hermetic test-only carrier profile precedes the transport probe without opening product compiler emission, and Phase 22 remains permanently deny-only while a future offline verifier owns release evidence.

## A. Executive Summary

Current SecureCompute status:

- `SecureComputeDomainDescriptor` is the neutral root descriptor.
- `SecureComputeSecurityLevel.None` normalizes to `Disabled`.
- absent, disabled and unmaterialized SecureCompute states are no-effect for ordinary operations.
- `DomainRuntimeContext` can carry optional `SecureCompute`, `DomainTag` and `AddressSpaceTag`.
- `RuntimeBoundaryAdmissionService` implements a generic boundary policy route and unit-testable non-ordinary denial. It is not proven as the CPU pipeline Stage-B SecureCompute enforcement owner: the only production callers found are VMX compatibility projection paths, which use the default `Ordinary` secure operation class.
- secure memory, measurement, evidence, migration, I/O, hypercall, grants, VMX compatibility denial/projection, nested design fence and proof-only backend-owner surfaces exist.
- `PrivilegedExecutionStateDescriptor` and `PrivilegedExecutionStateOwnerPolicy` implement the accepted Phase 09 neutral owner-proof contract for `GuestCr0` / `GuestCr4`.
- `PrivilegedExecutionStateProjectionService` implements the Phase 10 field-specific read-only compatibility projection gate for `GuestCr0` / `GuestCr4`.
- `SecureMemoryDomainDescriptor` and `SecureMemoryAdmissionPolicy` implement Phase 11 policy semantics only; no production memory effect path consumes them.
- `SecureIoDomainDescriptor` and `SecureIoHypercallAdmissionPolicy` implement Phase 12 policy semantics only; caller-supplied owner/materialization facts and grants are not device authority.

Why this is not activation approval:

- the current positive backend owner result is `AllowedProofOnlyNoExecution`;
- the current secure hypercall positive-looking result is `AllowedAdmittedDenied`, with backend execution and publication false;
- descriptor materialization, proof-chain acceptance, release-gate tests, documentation closure and generated projection vocabulary are not runtime execution evidence;
- VMX/VMCS/`VmxCaps` remain compatibility/projection vocabulary only.

Blocked production paths:

- secure backend execution;
- secure hypercall backend success;
- completion publication from proof-only or admitted-denied paths;
- retire publication from proof-only or admitted-denied paths;
- VMREAD expansion beyond the gated `GuestCr0` / `GuestCr4` read-only projection;
- nested secure execution;
- compiler secure backend emission;
- migration of host evidence, native tokens, backend bindings, raw secrets or active host pointers.

First owner-specific RFC/ADR closure:

- `ADR-SC-PES-GuestCr0Cr4` is accepted and implemented as neutral owner proof.
- The owner contract covers value source, domain/address-space binding, epoch, bit legality, evidence and restore classification.
- Owner acceptance keeps projection, mutation, backend execution, completion and retire publication closed.
- Phase 10 is separately implemented with owner, value-source, visibility, migration and conformance gates.
- The Phase 10 result is projection-only and cannot authorize mutation, backend success, completion or retire publication.
- Phase 11 implements direct policy semantics for private/shared/measured/runtime-mutable memory, but has no production effect-path enforcement or canonical region map.
- Phase 12 implements direct I/O/shared-buffer policy semantics, but has no device/IOMMU/DMA effect path or canonical buffer map.
- Phase 13 confirms proof-only contract and identifier vocabulary; backend execution owner and result path remain open.
- `ADR-SC-HYP-BACKEND-OWNER` now defines typed identifier, owner, request/result, argument, replay, cancellation, migration, compiler and rollback contracts, with a reviewed `SecureHypercallBackendOwnerAbiRegistry` production allocation and proof-only admission policy.
- Phase 13 allocates exact production decoded-leaf `0x5343_4859_5042`, SecureCompute service ID `0x5343_5356_4345` and backend owner ID `0x5343_4F57_4E52`. Opcode `259`, trap reason `18` and fixture `0x10` are not those decisions.
- Phase 14 is implemented only as a fail-closed publication policy model. It is not connected to a production backend-result/completion/retire owner chain.
- Phase 15 is implemented only as migration/output classification. It has no serializer, key owner, anti-replay/atomic restore protocol, and unknown payload classes still fall through to allow.
- Phase 16 is implemented only as a visibility classifier. No exclusive production evidence publisher is proven.
- Phase 17 is confirmed narrowly as a read-only VMX zero-authority compatibility boundary. It is not SecureCompute execution evidence.
- Phase 18 nested child-intent owner remains future/design-fenced. Phase 19 closure did not close nested execution, mutable nested state, Shadow VMCS authority or nested completion/retire publication.
- Phase 19 confirms product compiler no-emission. Clean generated-artifact hash proof remains open; an early hermetic test-only carrier profile is conformance-only, while product controlled emission stays a separate future RFC after limited runtime release.
- Phase 20 is a pre-activation evidence classifier driven by caller-supplied facts. It is not an execution gate and authorizes nothing.
- Phase 21 is closed only as a negative/future-gated conformance matrix. Source-string and document-string assertions are documentation guards, not execution proof.
- Phase 22 is permanently deny-only: even a fully populated request returns `DeniedPhase22ManualApprovalNotImplemented`; a future independent offline verifier, not this classifier, owns release-evidence validation.
- The next work is not backend execution. It begins with audit baseline freeze, end-to-end disabled equivalence, canonical operation taxonomy, descriptor registry/opaque binding, grant ledger, SafetyVerifier-issued certificate, production certificate carrier, hermetic test-only decode carrier and an expected-deny end-to-end slice.

## B. Proposed New File Tree

Target directory:

`HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/`

Files:

- `00_securecompute_activation_refactoring_index.md`
- `01_current_state_and_gap_matrix.md`
- `02_global_forbidden_regressions_and_release_guards.md`
- `03_owner_specific_rfc_adr_process.md`
- `04_no_effect_disabled_baseline_revalidation.md`
- `05_secure_descriptor_materialization_activation_plan.md`
- `06_stage_b_secure_admission_activation_plan.md`
- `07_secure_capability_grant_epoch_activation_plan.md`
- `08_measurement_evidence_visibility_activation_plan.md`
- `09_privileged_execution_state_owner_rfc.md`
- `10_guestcr0_guestcr4_readonly_projection_plan.md`
- `11_secure_memory_private_domain_policy_activation_plan.md`
- `12_secure_io_shared_buffer_policy_plan.md`
- `13_secure_hypercall_backend_owner_rfc.md`
- `14_secure_completion_retire_publication_plan.md`
- `15_secure_migration_checkpoint_restore_plan.md`
- `16_secure_debug_attestation_api_plan.md`
- `17_secure_vmx_boundary_zero_authority_plan.md`
- `18_secure_nested_child_intent_owner_rfc.md`
- `19_compiler_no_emission_to_controlled_emission_gate.md`
- `20_positive_secure_runtime_execution_activation_plan.md`
- `21_conformance_negative_positive_test_matrix.md`
- `22_limited_securecompute_release_gate.md`
- `23_open_decision_backlog.md`
- `24_audit_revalidation_and_dependency_order.md`
- `25_external_analysis_reconciliation_and_recommended_decisions.md`

## C. Phase Map

| File | Phase class | Activation role |
| --- | --- | --- |
| `01_current_state_and_gap_matrix.md` | readiness audit | proves the current baseline remains bounded and non-activating |
| `02_global_forbidden_regressions_and_release_guards.md` | release guard | makes forbidden shortcuts explicit before any owner opens |
| `03_owner_specific_rfc_adr_process.md` | process gate | defines the only route to future positive paths |
| `04_no_effect_disabled_baseline_revalidation.md` | partial non-regression gate | admission-level no-effect only; full disabled equivalence is open |
| `05_secure_descriptor_materialization_activation_plan.md` | open lifecycle-owner gate | requires one registry owner and opaque binding; DTO completeness is insufficient |
| `06_stage_b_secure_admission_activation_plan.md` | open CPU admission gate | generic policy route exists, but SafetyVerifier/CPU Stage-B reachability is unproven |
| `07_secure_capability_grant_epoch_activation_plan.md` | partial validator gate | validation exists; mint/revoke/reissue ledger is open |
| `08_measurement_evidence_visibility_activation_plan.md` | partial classifier gate | classification exists; one production evidence publisher is open |
| `09_privileged_execution_state_owner_rfc.md` | accepted owner RFC/ADR | implements neutral `GuestCr0` / `GuestCr4` owner proof |
| `10_guestcr0_guestcr4_readonly_projection_plan.md` | implemented projection-only gate | opens only gated read-only `GuestCr0` / `GuestCr4` values without authority or side effects |
| `11_secure_memory_private_domain_policy_activation_plan.md` | partial policy class | not connected to canonical load/store/cache/prefetch effects; region map is not canonical |
| `12_secure_io_shared_buffer_policy_plan.md` | partial policy class | not connected to device/IOMMU/DMA effects; shared-buffer map is not canonical |
| `13_secure_hypercall_backend_owner_rfc.md` | proof-only contract classifier | identifiers exist; executable owner/result path is open |
| `14_secure_completion_retire_publication_plan.md` | partial policy model | no production backend-result/completion/retire owner chain |
| `15_secure_migration_checkpoint_restore_plan.md` | partial classifier | no checkpoint/restore protocol and default-allow payload handling remains |
| `16_secure_debug_attestation_api_plan.md` | partial visibility classifier | no exclusive production publisher |
| `17_secure_vmx_boundary_zero_authority_plan.md` | closed fail-closed compatibility guard | preserves VMX/VMCS/`VmxCaps` zero authority for named positive-looking paths |
| `18_secure_nested_child_intent_owner_rfc.md` | future RFC/design fence | keeps nested execution, mutable nested state, Shadow VMCS authority and nested publication denied |
| `19_compiler_no_emission_to_controlled_emission_gate.md` | confirmed product no-emission; C2 artifact proof open | permits only a future isolated test carrier before probe; product emission remains a post-release RFC |
| `20_positive_secure_runtime_execution_activation_plan.md` | pre-activation evidence classifier | records missing facts; it is not an executable path |
| `21_conformance_negative_positive_test_matrix.md` | negative/future-gated conformance matrix | executable negative tests are separated from source/doc guards |
| `22_limited_securecompute_release_gate.md` | permanent deny-only classifier | no request can approve release; future approval evidence is offline and separately owned |
| `23_open_decision_backlog.md` | backlog | quarantines unresolved work |
| `24_audit_revalidation_and_dependency_order.md` | current audit/dependency ledger | records baseline, statuses, blockers and exact small-change order |
| `25_external_analysis_reconciliation_and_recommended_decisions.md` | external-analysis reconciliation | records source-checked recommended decisions and their compatibility limits |

## D. Activation Priority Recommendation

| Candidate | Benefit | Risk | Required owner map | Expected impact | Security risk | Conformance burden | Choose or defer |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Neutral privileged execution-state owner and projection for `GuestCr0` / `GuestCr4` | Narrow read-only compatibility path without backend side effects | May be misread as VMX authority or backend activation | execution-state descriptor, bit legality, value source, evidence, migration, field-specific projection | medium | medium | high but bounded | Phases 09 and 10 closed; keep all side effects denied |
| Secure backend owner typed request/result model | Direct bridge to real execution | Opens the central authority path | backend owner, typed request/result, grants, evidence, completion, retire, migration | high | high | very high | future-gated until Phase 20 and Phase 22 prerequisites are proven |
| Secure hypercall backend owner | Important user-facing path | Highest ambiguity with VMCALL/trap/projection/publication | hypercall owner, typed args, shared buffers, grants, evidence, backend, completion, retire | high | high | very high | Phase 13 supplies proof-only vocabulary; executable owner/result and publication paths remain open |
| Secure memory/private-domain policy activation | Foundational for confidentiality | Can drift into hardware tags or CHERI semantics | memory owner, private/shared/measured policy, grants, migration, evidence | medium-high | high | high | Phase 11 has policy classes only; production enforcement and canonical maps are open |

Recommendation: follow the dependency order in Phase 24. Phase 20 remains a pre-activation classifier and is not the next implementation step. Do not infer backend execution from any policy/classifier, proof contract, projection, manifest, visibility result, fence or no-emission decision.

## E. Critical P0 Blockers

- no canonical decode-owned SecureCompute operation taxonomy;
- no SafetyVerifier-issued immutable admission certificate;
- no production composition root from decode through issue/execute/result/completion/retire;
- no single descriptor materialization/revocation registry or opaque runtime binding;
- the generic request and runtime context can both carry a full descriptor;
- no grant mint/revoke/reissue ledger;
- operation-specific admission is not exhaustive and generic positive/default-allow branches remain;
- no complete owner map for any new positive path;
- no neutral backend execution owner;
- `AllowedProofOnlyNoExecution` may be misread as execution;
- `AllowedAdmittedDenied` may be misread as backend success;
- completion and retire publication separation is proven fail-closed for current paths, but no future positive path has an output owner chain;
- no Phase 20 positive execution path consumes the Phase 15 output/migration manifest classification for a named path;
- SecureCompute VMX/VMCS/`VmxCaps` denial is not proven for any future positive path;
- any privileged/control-field projection beyond `GuestCr0`/`GuestCr4` lacks an owner-specific contract and remains denied;
- memory/private descriptors must not become hardware tags or CHERI semantics;
- product compiler no-emission boundary must remain closed until a post-limited-release RFC; a hermetic test-only carrier profile remains isolated conformance tooling;
- nested secure domains have no child-intent execution owner;
- no product claim without code, tests, docs and negative conformance.

The ranked C0/C1/C2 registry and the required change sequence are normative in `24_audit_revalidation_and_dependency_order.md`.

Bounded policy-routing result, not a closed CPU Stage-B item:

- direct calls to the generic boundary service route non-ordinary missing/disabled/unmaterialized descriptors to denial. No production CPU decode/SafetyVerifier/issue caller supplies the taxonomy, so canonical Stage-B enforcement remains C0-open.

Closed narrow owner/projection item:

- Phase 09 neutral owner proof and Phase 10 field-specific `GuestCr0`/`GuestCr4` read-only projection are implemented with negative tests and source guards. VMX authority, VMWRITE, backend execution and publication remain denied.

Partial memory/private-domain policy item:

- Phase 11 contains direct policy checks, but no production memory/DMA caller and no canonical non-overlapping region map. It is not memory enforcement.

Partial secure I/O/shared-buffer policy item:

- Phase 12 contains direct policy checks, but no device/IOMMU/DMA effect owner and no canonical non-overlapping shared-buffer map. It is not I/O enforcement.

Negative completion/retire policy item:

- Phase 14 models and tests negative publication decisions. No production caller or backend-result/completion/retire owner chain is present.

Partial migration/output-manifest classification item:

- Phase 15 contains classifiers, but unknown migration payload classes can currently default to allowed and there is no checkpoint/restore protocol owner.
- Phase 16 contains visibility classifiers, but no exclusive production evidence publisher.
- Phase 17 VMX boundary zero-authority gate is implemented as fail-closed named-path vocabulary for Phase 10/13/14/15/16 and future Phase 20 positive-looking paths. VMX activation, `VmxCaps`, VMCS store, active pointer identity, compatibility read/write, VMCS checkpoint metadata and compatibility projection cannot create SecureCompute authority.
- Phase 19 compiler no-emission to controlled-emission gate is implemented as fail-closed compiler decision vocabulary. No-compiler-change is the only allowed current result; secure backend helper, secure hypercall helper, sideband metadata and future controlled-emission requests remain denied.
- Phase 20 is a pre-activation evidence classifier whose boolean inputs are not runtime proof.
- Phase 21 is a negative/future-gated matrix; documentation/source-string checks are excluded from execution proof.
- Phase 22 is permanently deny-only. Even a request with all booleans true returns manual-approval-not-implemented with all authority false; future release evidence is validated by a separate offline owner.

## F. Closure Matrix Location

The required closure classification matrix is in `01_current_state_and_gap_matrix.md`.

## G. Test Plan Location

The required negative, future-positive and static/source scan plan is in `21_conformance_negative_positive_test_matrix.md`.

## H. Release Gate Location

The definition of "limited SecureCompute activated" is in `22_limited_securecompute_release_gate.md`.

## Phase Metadata

External audit addendum, revalidated 2026-08-06: direct generic-service routing was corrected for caller-selected non-ordinary operations. No CPU decode/SafetyVerifier/issue caller is proven, so this does not close the Stage-B C0 blocker or approve production activation.

- File name: `00_securecompute_activation_refactoring_index.md`
- Phase goal: index the new activation corpus and state that it is not activation approval.
- Current baseline: bounded SecureCompute readiness corpus plus code/test guards.
- Owner of authority: neutral runtime owners only; this file owns no runtime authority.
- Scope: documentation index and activation ordering.
- No-goals: no code activation, no VMX path, no CHERI/tagged-memory path.
- Required RFC/ADR: none for the index.
- Acceptance criteria: every phase file is listed and the first RFC recommendation is explicit.
- Tests/static gates: release-gate doc scan must include this directory before any activation claim.
- Risks: readers may treat the plan as permission; every file repeats non-activation status.
- Next-gate dependency: `01_current_state_and_gap_matrix.md`.

## Current Baseline

The baseline is bounded readiness: descriptor and policy DTOs, admission-level ordinary no-effect, a generic boundary policy route, proof-only/admitted-denied classifiers, VMX read-only zero-authority projection, nested design fence, compiler no-emission and deny-only release classifiers. CPU Stage-B SecureCompute enforcement and all positive effect paths remain unproven.

## Authority Owner

This index owns no runtime authority. All future authority must be neutral runtime-owned and owner-specific.

## What Can Be Implemented

The directory and its release-gate/static-scan integration can be implemented immediately.

## What Remains Denied/Future-Gated

All positive execution, completion publication, retire publication, VMREAD expansion beyond the two Phase 10 fields, migration of host-owned evidence, nested execution and compiler secure emission remain denied or future-gated.

## Forbidden Shortcuts

No VMX/VMCS/`VmxCaps` authority, no secure VMCS, no CHERI/tagged-memory path, no proof-only execution, no admitted-denied backend success and no docs-only activation claim.

## Required RFC/ADR

None for the index. `ADR-SC-PES-GuestCr0Cr4` and its Phase 10 projection are implemented narrowly. Phases 11 and 12 expose policy classes only; production enforcement remains open. Every positive path still requires its own RFC/ADR and release proof.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/**`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/**`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/**`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureCompute*.cs`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/1/SC/deep-research-report SC.md`
- `HybridCPU_ISE/docs/ref2/1/SC/Исследование-SecureCompute.md`
- `HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew/`
- `HybridCPU_ISE/docs/ref2/deep-research-report (7).md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `Documentation/Virtualization WhiteBook/`
- `Documentation/Stream WhiteBook/`

## Required Tests

Release-gate tests must eventually scan this directory for forbidden authority shortcuts and product-claim overreach.

## Required Static/Source Scans

Use the scan set defined in `21_conformance_negative_positive_test_matrix.md`.

## Migration/Evidence Classification

This index classifies no payload. It requires future phases to classify every evidence and migration output explicitly.

## Completion/Retire Implications

This index publishes no completion or retire effects and cannot be cited as publication permission.

## SecureCompute Activation Implications

The index is an activation plan, not activation approval.

## Exit Criteria

The file tree, first-RFC recommendation, blockers, test-plan pointer and release-gate pointer are present.
