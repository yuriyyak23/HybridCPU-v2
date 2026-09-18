# Phasing And PR Breakdown

## Purpose

Документ разбивает SecureCompute refactoring на PR/closure phases. Каждая фаза имеет цель, touched areas, likely files, tests, gates, closure criteria, forbidden shortcuts and rollback risk.

## Scope

План покрывает Phase 0 through Phase 10:

- Phase 0 - Documentation and baseline lock;
- Phase 1 - `SecureComputeDomainDescriptor` skeleton, disabled/no-effect;
- Phase 1.5 - VMX denial-only guard;
- Phase 2 - Secure admission policy fail-closed hooks;
- Phase 3 - `SecureMemoryDomainDescriptor` private/shared/measured policy;
- Phase 4 - Measurement/evidence model;
- Phase 5 - Secure migration policy;
- Phase 6 - Secure I/O and hypercall design fences;
- Phase 7 - CHERI-like descriptor/grant monotonicity discipline;
- Phase 8 - VMX compatibility boundary deny/projection rules;
- Phase 9 - Nested secure domain design fence;
- Phase 10 - conformance hardening and stale-doc cleanup.

Files 11-13 are meta/future documents, not implementation phases 11-13. Plan 11 is the master conformance matrix, Plan 12 is the phasing ledger, and Plan 13 is a future capability-aware ISA/memory quarantine document.

## Non-goals

Фазы не включают capability-aware ISA/memory model, new instruction encodings, VMX secure mode, secure VMCS or production-ready secure compute claim.

## Architectural invariants

Каждая фаза должна сохранять:

- absent/disabled secure descriptor -> unchanged behavior;
- Stage A metadata-only posture;
- Stage B/runtime admission ownership;
- VMX as compatibility frontend only;
- VMCS/VmxCaps not authority;
- evidence/migration/completion ownership by neutral runtime services.

## Implementation status

- [x] Phase 0 - documentation/baseline lock: plan and VMX non-regression baseline established.
- [x] Phase 1 - `SecureComputeDomainDescriptor` skeleton: disabled/no-effect semantics implemented; `SecurityLevel.None` normalizes to `Disabled`; no VMX exposure.
- [x] Phase 1.5 - VMX denial-only guard: VMX/VmxCaps/VMCS/VMREAD/VMWRITE secure-authority denial tests added.
- [x] Phase 2 - secure admission policy fail-closed hooks: `RuntimeBoundaryAdmissionService` has an opt-in secure-domain admission hook that defaults to ordinary/no-effect, checks neutral domain binding for enabled secure descriptors, and fails closed only for secure-domain operation classes.
- [x] Phase 3 - `SecureMemoryDomainDescriptor` private/shared/measured policy: Stage B secure memory policy baseline implemented and tightened with domain/address-space binding, explicit shared DMA buffer direction/owner/lifetime/evidence, runtime-mutable dirty/migration classification, host-evidence non-leak conformance, ordinary `LOAD`/`STORE`/`FETCH` no-effect and VMCS-backed memory authority guards.
- [x] Phase 4 - Measurement/evidence model: baseline closed with measurement materialization, policy/memory digest binding, evidence visibility/fence checks, checkpoint denial and VMX no-authority guards.
- [x] Phase 4.5 / Plan 05 - Secure evidence/publication fences: closed with compatibility alias evidence, completion-vs-retire separation, admitted-denied VMCALL publication, sideband visibility and stale replay/rollback guards.
- [x] Phase 5 - Secure migration policy: closed with restore revalidation/reattest admission, migration payload class denial, VMCS/compat metadata rejection, stale epoch/grant guards and private-memory sealed/encrypted payload contract.
- [x] Phase 6 - Secure I/O and hypercall design fences: closed with secure I/O owner, typed shared-buffer grants, raw private pointer denial, neutral hypercall backend owner, admitted-denied semantics and completion/retire fences.
- [x] Phase 7 - CHERI-like descriptor/grant monotonicity discipline baseline closed on 2026-05-31.
- [x] Phase 8 - VMX compatibility boundary deny/projection rules closed on 2026-05-31.
- [x] Phase 9 - Nested secure domain design fence closed on 2026-05-31.
- [x] Phase 10 - conformance hardening and stale-doc cleanup release gate closed on 2026-05-31.
- [x] Post-Phase10 secure backend owner/RFC proof gate closed on 2026-05-31: neutral owner proof-chain admission exists with negative conformance, but backend execution remains closed.

Status-class audit:

- Phase 0: docs-only + negative conformance baseline.
- Phase 1: shell-only + no-effect policy admission.
- Phase 1.5: negative conformance.
- Phase 2: fail-closed policy admission.
- Phase 3: fail-closed policy admission + negative conformance.
- Phase 4: positive policy admission for measured-enter/evidence prerequisites + negative conformance; no production attestation.
- Phase 4.5: positive policy admission for fenced publication + negative conformance; no production evidence transport.
- Phase 5: fail-closed migration policy + negative conformance; no live migration or production sealing.
- Phase 6: positive policy admission for explicit shared-buffer I/O and admitted-denied hypercall + negative conformance; no positive secure backend execution.
- Phase 7: runtime descriptor/grant discipline + negative conformance; not CHERI ISA.
- Phase 8: VMX deny/projection matrix + negative conformance; not secure VMCS.
- Phase 9: design fence + negative conformance; not nested secure execution.
- Post-Phase10 owner/RFC gate: positive policy-admission evidence + negative conformance; no positive backend execution.

## Proposed descriptors / policies

Phase 0 - Documentation and baseline lock:

- Цель: зафиксировать текущие owners, VMREAD deny matrix, no-regression baseline.
- Touched areas: docs and tests only.
- Files likely to be added: SecureCompute plan docs, baseline tests if missing.
- Files likely to be modified: VMXRefactoring docs cross-links, if desired.
- Tests to add: source-pattern baseline guards, inactive secure compute placeholder tests.
- Non-regression gates: existing VMXRefactoring tests pass.
- Closure criteria: no code behavior changes, forbidden-authority list enforced.
- Forbidden shortcuts: no descriptor code in Phase 0 unless split into Phase 1.
- Rollback risk: low; documentation/test-only.

Phase 1 - `SecureComputeDomainDescriptor` skeleton, disabled/no-effect:

- Цель: add neutral descriptor skeleton and disabled/default semantics without VMX exposure.
- Touched areas: `Core/Runtime/Domains/SecureCompute`, `DomainRuntimeContext` optional attachment if needed.
- Files likely to be added: descriptor enum/value objects; tests for absent/disabled/no-effect.
- Files likely to be modified: neutral domain context only if optional field is required.
- Tests to add: `absent descriptor -> unchanged`, `disabled descriptor -> unchanged`, `SecurityLevel.None -> unchanged`, VMX cannot activate, VmxCaps cannot grant.
- Non-regression gates: no Stage A behavior change, no non-VMX instruction failures.
- Closure criteria: descriptor can exist inactive and causes no new denial; if only shell types are present, closure says design baseline + shells only, not feature-complete SecureCompute.
- Forbidden shortcuts: no VMCS field, no `VmxCaps` bit, no VMREAD exposure.
- Rollback risk: low.

Phase 1.5 - VMX denial-only guard:

- Цель: add early compatibility denial guards before any positive secure behavior.
- Touched areas: VMX/SecureCompute boundary conformance tests, `VmxCaps` write/no-effect tests, VMCS projection authority guards.
- Files likely to be added: VMX-boundary tests under `HybridCPU_ISE.Tests/VmxRefactoring`; optional conformance shell tests under `CloseToHSL/Core/Virtualization/SecureCompute/Conformance`.
- Files likely to be modified: tests only unless explicit denied-decision names are missing.
- Tests to add: VMX cannot activate SecureCompute, `VmxCaps` cannot grant SecureCompute, VMCS cannot store secure state, VMREAD/VMWRITE secure-state backend denied.
- Non-regression gates: closures 240-255 remain unchanged; no VMX projection value is opened.
- Closure criteria: denial-only VMX guard exists before secure admission, memory, evidence, migration, I/O or hypercall positive paths.
- Forbidden shortcuts: no positive VMX exposure, no `VmxCaps.SecureCompute`, no secure VMCS field.
- Rollback risk: low-medium; denial tests may require naming existing fail-closed decisions.

Phase 2 - Secure admission policy fail-closed hooks:

- Status: closed as Phase 2 baseline on 2026-05-30; tightened on 2026-05-30 with neutral `DomainRuntimeContext.DomainTag` / `AddressSpaceTag` binding checks. Closure is limited to neutral runtime-boundary hook + tests; it is not feature-complete SecureCompute and does not open secure evidence/migration/I/O/hypercall positive paths.
- Цель: add secure-aware checks to `RuntimeBoundaryAdmissionService` for enabled descriptors.
- Touched areas: runtime boundary admission, domain operation classification.
- Files likely to be added: secure admission decision enums and policies.
- Files likely to be modified: admission service, tests.
- Tests to add: enabled descriptor missing required subpolicy denied only for secure operations; ordinary operations unchanged; secure checks not evaluated when descriptor is absent/disabled.
- Non-regression gates: Stage A remains metadata-only; secure checks run only for materialized enabled descriptor and secure-domain operation classes.
- Closure criteria: missing owner/subpolicy fails closed without broad regression.
- Forbidden shortcuts: no direct VMX hook, no legacy helper authority.
- Rollback risk: medium; admission changes can over-deny if scoped poorly.

Phase 3 - `SecureMemoryDomainDescriptor` private/shared/measured policy:

- Status: closed as Phase 3 baseline on 2026-05-30; tightened on 2026-05-30 for shared-DMA binding, runtime-mutable dirty/migration classification, host-evidence non-leak guard and secure memory domain/address-space binding. Closure is limited to descriptor classification, Stage B secure memory admission policy and conformance/source guards; it is not feature-complete secure memory and does not open positive hypercall, migration or VMX memory authority paths.
- Цель: define secure memory classes and Stage B memory-policy route.
- Touched areas: runtime memory descriptors, memory translation policy, IOMMU boundaries.
- Files likely to be added: secure memory descriptor/policy classes.
- Files likely to be modified: memory admission adapters, tests.
- Tests to add: private host read denied, shared explicit only, measured required, DMA private denied.
- Non-regression gates: ordinary memory unchanged, ordinary `LOAD`/`STORE`/`FETCH` unchanged, VMREAD memory-owned fields unchanged.
- Closure criteria: secure memory denied without policy only when secure policy requires it.
- Forbidden shortcuts: no tagged memory, no capability-aware pointers, no operand-format changes, no VMCS-backed EPT/VPID authority.
- Rollback risk: medium.

Phase 4 - Measurement/evidence model:

- Status: closed as Phase 4 baseline on 2026-05-30. Closure is limited to neutral measurement/evidence descriptors, Stage B measured-enter admission, attestation publication guard, checkpoint raw-secret denial and VMX no-authority tests; it is not a production cryptographic attestation protocol.
- Цель: add measurement descriptor and secure evidence visibility categories.
- Touched areas: evidence policy, domain measurement, completion fence design.
- Files likely to be added: `DomainMeasurementDescriptor`, `SecureEvidencePolicy`.
- Files likely to be modified: admission tests and evidence conformance.
- Tests to add: measurement missing denied, host evidence denied, compatibility alias denied, fence required, attestation not VMREAD/VMCS/`VmxCaps`.
- Non-regression gates: ordinary domains unaffected.
- Closure criteria: measurement is evidence-bound, not VMX field; `SecureEvidencePolicy` is narrowing layer over `EvidencePolicyDescriptor`.
- Forbidden shortcuts: no raw secrets, no host evidence guest state, no independent secure evidence authority.
- Rollback risk: medium.

Phase 4.5 / Plan 05 - Secure evidence/publication fences:

- Status: closed as Phase 4.5 baseline on 2026-05-30. Closure is limited to evidence publication decision fences, compatibility alias policy checks, sideband visibility classification, stale replay/rollback denial and VMCALL admitted-denied publication proof; it is not production secure evidence transport.
- Goal: separate evidence visibility, completion publication and retire side-effect publication.
- Touched areas: secure evidence publication policy, secure completion fence tests, VMX admitted-denied trap conformance.
- Files added: `SecureEvidencePublicationPolicy`, `SecureEvidencePublicationPolicyTests`.
- Files modified: VMX denial guard tests and SecureCompute plan status files.
- Tests added: compatibility alias denied unless secure + neutral + explicit projection policy all allow it; completion denied before fence; completion fence does not imply retire; retire requires explicit retire fence; admitted-denied VMCALL cannot publish completion/retire; Lane6/Lane7 sideband visibility respects evidence class; stale evidence epoch is denied on replay/restore.
- Non-regression gates: no VMCS/VmxCaps/VMREAD/VMWRITE authority in secure evidence publication sources.
- Closure criteria: `SecureEvidencePolicy` remains a narrowing layer over neutral `EvidencePolicyDescriptor`, and publication fences are separate from VMX compatibility projection.
- Forbidden shortcuts: no compatibility alias by default, no host-owned guest state, no completion-as-retire shortcut, no VMREAD evidence backend.
- Rollback risk: medium.

Phase 5 - Secure migration policy:

- Status: closed as Phase 5 baseline on 2026-05-30. Closure is limited to migration descriptor policy classes, checkpoint payload denial, restore admission guards and sealed/encrypted private-memory payload contract proof; it is not binary migration format, live migration, key management or production sealing.
- Цель: add secure checkpoint payload classification and restore validation.
- Touched areas: migration validation, checkpoint image classification, restore service.
- Files likely to be added: `SecureMigrationDescriptor`, payload class enums.
- Files likely to be modified: migration conformance tests.
- Tests to add: host evidence rejected, VMCS projection rejected, epoch rollback rejected, re-attestation required, private memory migration denied without sealed/encrypted payload contract.
- Non-regression gates: existing migration/evidence proof for recomputed compatibility fields remains.
- Closure criteria: restore rebuilds host evidence and rejects compatibility authority; private secure memory migration remains denied until sealed-payload semantics are separately specified.
- Forbidden shortcuts: no VMCS image authority, no raw sealing keys, no plain private memory migration.
- Rollback risk: medium-high.

Phase 6 - Secure I/O and hypercall design fences:

- Status: closed as Phase 6 baseline on 2026-05-30. Closure is limited to secure I/O and hypercall policy fences, argument/shared-buffer classification and admitted-denied proof; it does not add a concrete device model, hypercall ABI or positive secure backend execution.
- Цель: add descriptors and denial-only/fail-closed admission for secure I/O/hypercall paths.
- Touched areas: I/O domain descriptors, IOMMU, hypercall backend admission, completion routing.
- Files likely to be added: `SecureIoDomainDescriptor`, `SecureHypercallDescriptor`.
- Files likely to be modified: hypercall admission tests.
- Tests to add: missing I/O owner denied, DMA default-denied except explicit shared buffers, raw private pointer denied, missing backend owner denied, admitted-denied != success.
- Non-regression gates: existing VMCALL admitted-denied path remains.
- Closure criteria: first secure hypercall stage has no positive backend success; later success requires neutral owner, typed grant, policy, argument/shared-buffer classification, evidence, completion fence and retire rule.
- Forbidden shortcuts: no VMX backend success, no DMA bypass, no raw private guest pointer as ordinary address.
- Rollback risk: medium.

Phase 7 - CHERI-like descriptor/grant monotonicity discipline:

- Status: closed as Phase 7 first safe pool on 2026-05-31. Closure is limited to runtime descriptor/grant discipline: provenance validation, authority bounds validation, combined epoch validation, monotonic child derivation, migration restore rederive/revalidate baseline and negative conformance. It is not CHERI ISA, not tagged memory, not capability registers and not feature-complete SecureCompute.
- Цель: add Layer 2 runtime authority discipline without ISA expansion.
- Touched areas: capabilities, grants, secure descriptors, nested policy.
- Files likely to be added: secure grant handles, derivation records, epoch/revocation policy.
- Files likely to be modified: `CapabilityDescriptorSet` integration tests, admission services.
- Tests to add: child cannot exceed parent, revoked grant denied, forged scalar handle rejected, handle with missing provenance or stale epoch rejected.
- Non-regression gates: no capability registers, no tagged memory, no operand changes.
- Closure criteria: monotonic bounded authority proven at descriptor/grant level; `SecureGrantHandle` is not forgeable from guest-visible scalar values and requires provenance + epoch validation.
- Forbidden shortcuts: no scalar VMX capability authority.
- Rollback risk: medium-high.

Phase 8 - VMX compatibility boundary deny/projection rules:

- Status: closed as Phase 8 boundary matrix on 2026-05-31. Closure is limited to deny-by-default VMX compatibility projection rules, schema owner mismatch denial, VMWRITE no-effect, `VmxCaps` no descriptor materialization, VMCS checkpoint authority denial and projection-not-backend-success proof. It is not secure VMCS and not VMX-owned SecureCompute.
- Цель: expand the early Phase 1.5 denial guard into the full VMX compatibility deny/projection matrix.
- Touched areas: VMX compatibility conformance, projection policy.
- Files likely to be added: VMX secure boundary conformance tests.
- Files likely to be modified: `VmcsReadOnlyValueProjectionService` only if denied decisions need explicit names.
- Tests to add: VMREAD secure field denied unless neutral owner + read-only source + secure visibility + migration class + conformance tests exist, VMWRITE denied, VmxCaps secure bit no effect, VMCS checkpoint rejected, `GuestCr0`/`GuestCr4`/`HostCr3`/host execution aliases remain denied.
- Non-regression gates: closures 240-255 behavior unchanged.
- Closure criteria: all secure-sensitive compatibility paths denied unless neutral owner exists.
- Forbidden shortcuts: no secure VMCS, no VMREAD backend.
- Rollback risk: medium.

Phase 9 - Nested secure domain design fence:

- Status: closed as Phase 9 design fence on 2026-05-31. Closure is limited to neutral child-intent admission, parent/child monotonic authority checks, host-evidence leakage denial, nested projection/migration bounds and Shadow VMCS/VMCS12/VMCS02 authority rejection. It is not implemented nested SecureCompute, not mutable nested VMX state and not positive secure backend execution.
- Цель: add future-safe neutral child-intent model and fail-closed nested tests.
- Touched areas: nested descriptors, nested memory composition, nested projection conformance.
- Files likely to be added: child-intent descriptor design types or tests-only placeholders.
- Files likely to be modified: nested conformance tests.
- Tests added: missing child-intent owner denied, missing parent descriptor denied, child > parent denied, child compatibility projection/migration payload > parent denied, host evidence leakage denied, nested projection expansion denied, stale parent/child epoch denied, Shadow VMCS bridge-only guard and VMCS12/VMCS02 authority rejection.
- Non-regression gates: existing nested readiness fail-closed; no VMX/VmxCaps/VMREAD/VMWRITE backend authority.
- Closure criteria: nested secure direction cannot use mutable Shadow VMCS, VMCS12 or VMCS02 as runtime authority and remains design-fence-only.
- Forbidden shortcuts: no Shadow VMCS state owner.
- Rollback risk: medium.

Phase 10 - conformance hardening and stale-doc cleanup:

Status: closed release-gate phase on 2026-05-31. This was not cosmetic cleanup; it closed source-pattern, schema, migration/evidence, documentation drift, status-label and production-claim gates before any production SecureCompute claim.

- Цель: close source-pattern, schema, migration/evidence and documentation drift.
- Touched areas: conformance tests, docs, golden artifacts.
- Files likely to be added: doc-lint or source guard tests.
- Files likely to be modified: stale VMXRefactoring/SecureCompute docs references.
- Tests to add: stale docs do not claim VMX owns SecureCompute; generated schema owner mismatch denied; status labels match actual implementation class.
- Non-regression gates: full test suite, projection lineage checks, source-pattern guards, doc-lint, golden/schema parity, full no-regression suite, status-label audit and production-claim audit.
- Mandatory gates: VMX no-authority, VMCS no-state-owner, VmxCaps no-grant, no new ISA encoding, ordinary `LOAD`/`STORE`/`FETCH` unchanged, migration host-evidence denied, secure backend execution not claimed unless separately implemented and tested.
- Closure criteria: docs and tests agree that SecureCompute is neutral domain opt-in and every closed phase is labeled as docs-only, shell-only, fail-closed policy, negative conformance, positive policy admission or positive backend execution.
- Forbidden shortcuts: no code behavior hidden in cleanup PR.
- Rollback risk: low-medium.

Post-Phase10 - secure backend owner/RFC proof gate:

- Status: closed on 2026-05-31 as policy-admission evidence only; it is not positive secure backend runtime execution.
- Goal: require a neutral materialized backend owner, approved RFC/ADR, grant/evidence/completion/retire proof chain, current epoch and matching negative tests before any future runtime-execution claim.
- Touched areas: SecureCompute backend owner descriptor/policy and conformance tests.
- Files added: `SecureBackendOwnerDescriptor`, `SecureBackendOwnerAdmissionPolicy`, `SecureBackendOwnerRfcGateTests`.
- Tests added: missing owner denied, compatibility/VMX/VMCS/VmxCaps sources denied, missing RFC/ADR denied, incomplete proof chain denied, stale epoch denied, missing negative tests denied, backend execution request denied even with complete proof.
- Non-regression gates: no VMX authority, no VMCS state owner, no `VmxCaps` grant, no `BackendExecutionAuthorized: true`, no secure hypercall backend success.
- Closure criteria: a complete proof chain may be accepted only as policy evidence (`AllowedProofOnlyNoExecution`); runtime execution stays closed.
- Forbidden shortcuts: no hidden positive backend execution path, no compatibility projection owner, no production SecureCompute claim.
- Rollback risk: low-medium.

## Integration points

PRs should land in order unless a phase is split into docs-only and code/test sub-closures. Phase 1.5 VMX boundary hardening should land before positive secure-domain paths and can run in parallel with neutral descriptor work only if it remains denial-only and does not require secure descriptor implementation.

## No-regression requirements

Every PR must report:

- descriptor absent behavior;
- descriptor disabled behavior;
- `SecurityLevel.None` behavior or explicit absence from the model;
- Stage A impact;
- Stage B admission impact;
- VMX/VmxCaps/VMCS exposure;
- migration/evidence impact;
- completion/retire publication impact;
- forbidden patterns scanned.

## Tests and conformance

Minimum per-phase test rule:

- one absent/disabled no-effect test;
- one `SecurityLevel.None -> unchanged` or one proof that no `None` state exists;
- one missing-owner denied test;
- one forbidden-authority guard;
- one migration/evidence visibility guard when touching evidence/migration;
- one VMX boundary guard when touching compatibility surfaces.

## Closure criteria

The refactoring plan is complete when Phase 0-10, Phase 1.5 and the Post-Phase10 owner/RFC proof gate can be converted into closure tasks with explicit owners, tests and rollback risks, and future capability-aware ISA remains outside Layer 1/2. Shell-only milestones close only as design baseline + shells, not feature-complete SecureCompute. Remaining unresolved decisions are tracked in `Plan2/14-securecompute-open-decision-backlog.md`.

## Forbidden shortcuts

- Do not combine Phase 1 descriptor skeleton with VMX exposure.
- Do not delay basic VMX denial-only guards until the full Phase 8 projection matrix.
- Do not open positive I/O/hypercall/migration paths before fail-closed tests.
- Do not mix Layer 2 descriptor discipline with capability-aware ISA work.
- Do not claim production secure compute from documentation-only phases.

## Open questions

- Resolved 2026-05-30: Phase 1/2 are split in implementation history. Phase 1 owns descriptor disabled/no-effect shell; Phase 2 owns optional `DomainRuntimeContext` integration and neutral binding checks.
- Resolved 2026-05-30: Phase 1.5 keeps early denial-only VMX guards. Phase 8 remains responsible for the full compatibility projection matrix, positive read-only projection conformance and migration-classified visibility rules.
- Resolved 2026-05-31: Phase 10 and Post-Phase10 owner/RFC proof gate are closed as release-gate/policy-evidence work only; production runtime execution remains in `Plan2/14-securecompute-open-decision-backlog.md`.
