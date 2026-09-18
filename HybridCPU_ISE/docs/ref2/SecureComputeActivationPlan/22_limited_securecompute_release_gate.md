# Limited SecureCompute Release Gate

## Phase Metadata

- File name: `22_limited_securecompute_release_gate.md`
- Phase goal: define the only acceptable meaning of "limited SecureCompute activated".
- ADR ID: `ADR-SC-LIMITED-SECURECOMPUTE-RELEASE-GATE`
- Status: permanently deny-only runtime classifier. No current or future input may turn this policy into release approval; independently proven named-path evidence belongs to a separate offline verifier and review process.
- Scope: release checklist, product-claim hygiene, denied shortcut checks and activation wording.
- No-goals: no feature-complete claim and no broad workload support claim.

## What Limited SecureCompute Activated Does Not Mean

- feature-complete SecureCompute;
- SecureCompute VMX mode;
- secure VMCS;
- `VmxCaps` SecureCompute grant;
- CHERI ISA;
- tagged memory;
- capability registers;
- capability-aware LOAD/STORE/FETCH;
- nested SecureCompute execution;
- VMWRITE support;
- compiler general secure emission;
- Lane6/Lane7 passthrough;
- migration of host evidence;
- production-ready claim for all secure workloads.

## What Limited SecureCompute Activated May Mean

Only a future independent offline release-evidence verifier and review process may establish the following for one named build profile; the Phase 22 runtime classifier itself remains deny-only:

- one restricted owner-specific secure path is activated;
- neutral owner exists in production code;
- typed request/result model exists;
- a SafetyVerifier-issued certificate and production issue carrier enforce the named operation; the generic `RuntimeBoundaryAdmissionService` alone is insufficient;
- Stage B denies non-ordinary secure operations when descriptors are absent, disabled or unmaterialized;
- capability/evidence/migration classes are explicit;
- completion fence and retire rule exist where publication is claimed;
- compatibility projection happens only after neutral success;
- all denied shortcuts remain denied;
- release-gate tests block overclaims.

## Current Baseline

Current baseline does not satisfy production SecureCompute activation. Phases 09/10 prove only a narrow read-only projection and Phase 17 proves VMX zero authority. Phases 11-16 and 20-22 are policy/classification surfaces without a named production effect chain. Phase 15 also contains unknown-payload default-allow behavior. Phase 19 enforces current no-emission but lacks clean generated-artifact reproducibility evidence. None authorizes hardware tags, CHERI semantics, VMX/VMCALL authority, backend execution, completion publication, retire publication or product activation.

`SecureComputePhase22LimitedReleaseGatePolicy` implements the permanent hard deny. It rejects release over Phase 21 future-gated evidence and, even when every hypothetical prerequisite boolean is true, returns `DeniedPhase22ManualApprovalNotImplemented` with all authority bits false. It must not be converted into approval by flipping the final branch or trusting the request booleans. Phase 18 nested execution remains excluded and product compiler secure emission remains closed.

External audit update, revalidated 2026-08-06: direct generic-service tests cover missing/disabled/unmaterialized non-ordinary inputs. No CPU decode/SafetyVerifier/certificate/issue call graph is proven, so CPU Stage-B enforcement remains open.

## Authority Owner

Phase 22 owns only negative classification. A future independent `SecureComputeReleaseEvidenceVerifier` owns offline evidence validation and emits, at most, a SHA-bound build-profile artifact. Runtime authority remains with the owner-specific neutral path and SafetyVerifier.

## What Can Be Implemented

- release checklist tests;
- product-claim scan;
- full negative conformance suite;
- a permanently fail-closed limited-release evidence classifier;
- future backend activation evidence schema for consumption by the separate offline verifier, never an allow branch here.

## What Remains Denied/Future-Gated

Everything not explicitly named in the one limited owner path remains denied or future-gated.

## Forbidden Shortcuts

- product claim from documentation closure;
- product claim from proof-only owner;
- product claim from admitted-denied hypercall;
- product claim from VMX projection;
- product claim from evidence/telemetry/tests;
- product claim with any missing code/test/doc/negative-conformance item.
- product claim based on caller-supplied proof booleans, source strings, documentation strings or the Phase 22 classifier itself.
- changing `DeniedPhase22ManualApprovalNotImplemented` into an allowed decision;
- feeding an offline release artifact into SafetyVerifier as certificate, descriptor, grant or runtime boolean.

## Independent Offline Release Evidence Verifier

The recommended future release owner is a separate build/offline tool, `SecureComputeReleaseEvidenceVerifier`. It consumes an immutable evidence bundle containing:

- exact clean Git SHA and tree state;
- SDK, target/profile and generator versions;
- generator inputs and generated-output hashes;
- exact test filters, discovered/executed counts and result artifacts;
- named owner/path/reachability proof from decode through retire;
- negative bypass inventory and source scans;
- migration/checkpoint decision for the named contour;
- bounded rollback drill evidence;
- independent reviewer signatures.

Its output is a build-profile artifact cryptographically bound to the reviewed SHA and profile. The artifact is release-process evidence only. It cannot authorize runtime execution, backend acceptance, completion, retire, VMX, compiler emission or nested execution, and it is never an input to the Phase 22 classifier.

## Required RFC/ADR

Required: the owner-specific RFC/ADR for the single limited path, plus implementation evidence and release checklist approval.

## Code Anchors

- the implemented owner path;
- `RuntimeBoundaryAdmissionService.cs`;
- grants/evidence/migration/publication policies;
- VMX deny/projection fences;
- test suites from `21`.

## Documentation Anchors

- this entire activation corpus;
- `SecureComputerefactoringNew/21_release_gate_and_activation_checklist.md`;
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`.

## Required Tests

- all negative tests in `21`;
- all owner-specific positive tests for the one path;
- release-gate doc/product scans;
- compatibility denial scans;
- migration/evidence non-leak tests;
- compiler no-emission tests unless controlled emission RFC is in scope.
- Stage B source/test scan proving old allowed-by-absence/disabled expectations are inverted or removed across the full non-ordinary taxonomy.
- runtime tests, source scans and conformance docs proving ordinary no-effect is separate from non-ordinary fail-closed routing across the full non-ordinary taxonomy.
- Phase 09 owner tests proving descriptor presence, domain/address-space binding, current epoch, bit legality, evidence and restore classification.
- source scans proving Phase 09 owner code has no compatibility/virtualization authority dependency and does not open the current projection service.
- Phase 10 tests proving missing owner/value/visibility/migration/conformance gates deny projection.
- Phase 10 positive tests proving only `GuestCr0`/`GuestCr4` project and all side-effect flags remain false.
- Phase 11 tests proving missing/unmaterialized/stale secure-memory descriptors deny admission.
- Phase 11 tests proving private host reads and private DMA deny, including hypercall argument paths.
- Phase 11 tests proving shared DMA requires explicit memory/I/O policy, shared-buffer binding, current shared-buffer grant epoch and typed capability grant.
- Phase 11 tests proving measured admission does not satisfy private-domain activation.
- Phase 11 tests proving runtime-mutable memory requires dirty and migration classification and private-memory migration requires a sealed/encrypted payload contract.
- Phase 12 tests proving missing neutral I/O owner, missing explicit shared-buffer policy, wrong direction, raw private pointers and missing typed grants deny admission.
- Phase 12 tests proving shared-buffer owner, lifetime, evidence and buffer-grant epoch binding.
- Phase 12 tests proving successful I/O policy admission remains non-executing and cannot publish completion or retire even when fences are present.
- Phase 12 migration tests proving backend bindings and native tokens remain denied as checkpoint/restore authority.
- Phase 14 tests proving proof-only backend owner admission, admitted-denied secure hypercall recognition and registry-backed Phase 13 owner/service admission cannot publish completion or retire.
- Phase 14 tests proving completion fence alone cannot cross the backend-result boundary, retire requires explicit retire owner/fence, generic trap-route flags are not SecureCompute authority, VMX projection remains zero-authority, migration excludes forbidden payloads and compiler no-emission remains closed.
- Phase 15 tests proving output manifests reject host-owned evidence, scheduler evidence, backend binding evidence, native tokens, raw secrets, raw sealing keys, active pointers, VMCS metadata and compatibility metadata.
- Phase 15 tests proving internal backend result and internal completion record are manifest-only and create no runtime, completion publication or retire publication authority.
- Phase 15 tests proving complete manifest coverage includes request state, internal backend result, internal completion record, guest-visible output, retire-visible state and recomputed-after-restore state, while missing coverage blocks future positive-path evidence.
- Phase 15 tests proving recomputed-after-restore state requires restore validation proof and owner/path/reachability classification is required for manifest entry acceptance.
- Phase 16 tests proving debug trace is debug-only visibility, attestation report is guest-visible only under evidence policy, telemetry is host-only, host inspection cannot bypass private-memory policy and compatibility-alias evidence requires projection policy.
- Phase 16 tests proving debug/attestation visibility cannot become migration payload, activation evidence, backend-owner proof, completion publication or retire publication.
- Phase 17 tests proving named positive-looking paths remain VMX zero-authority and compatibility projection requires a neutral runtime result.
- Phase 17 tests proving VMX activation, `VmxCaps` grant, VMCS state store, active pointer identity, compatibility read/write authority, VMCS checkpoint authority, completion publication and retire publication shortcuts are denied.
- Phase 19 tests proving no-compiler-change is the only allowed current decision and secure backend helper, secure hypercall helper, sideband metadata and future controlled-emission requests are denied.
- Phase 19 tests proving no new instruction encoding, operand format, capability-aware memory instruction or VMX secure-mode emission is authorized.
- Phase 20 tests proving registry-backed admission, proof-only owner admission, admitted-denied hypercall recognition, publication vocabulary, manifest-only evidence, debug/attestation visibility, VMX projection, nested child intent and compiler no-emission cannot become runtime execution evidence.
- Phase 20 tests proving manifest coverage is required but manifest-only records are not execution proof.
- Phase 20 tests proving future named-path preconditions fail closed until exact owner/path/reachability, neutral backend result, output-manifest evidence, restore rules, nested exclusion, product compiler no-emission decision and separately verified offline release evidence exist.
- Phase 21 tests proving current conformance evidence is release-input evidence only and cannot become runtime execution, publication, compiler, VMX, nested or production-release authority.
- Phase 22 tests proving every request remains denied, including fully populated booleans, and that hypothetical future release evidence creates no runtime authority.
- future offline-verifier tests proving SHA/profile/hash/signature mismatch, dirty tree, missing path proof, bypass inventory drift and rollback-drill omission deny artifact issuance.
- reproducibility manifest recording repository revision, dirty-worktree disclosure, SDK `10.0.201` compatibility, build configuration, `DefineTestSupport` state, exact filters/counts/results and source-scan commands.
- bounded rollback drill or reviewed rollback procedure covering code, compiler boundary, compatibility advertisement and release wording.

## Required Static/Source Scans

- product-ready overclaim;
- feature-complete overclaim;
- VMX/VMCS/`VmxCaps` authority;
- CHERI/tagged-memory/ISA shortcuts;
- publication without backend owner/fence/retire rule;
- migration of host evidence;
- nested execution wording.
- `secureDescriptor is { IsEnabled: true }` Stage B bypass for non-ordinary secure operations.
- `RuntimeBoundaryAdmission_NoEnabledDescriptorGuardBypassContractTests`.
- `PrivilegedExecutionStateOwner_NotVmcsBacked_SourceGuard`.
- `GuestCr0Cr4Projection_SourceGuardIsFieldSpecificAndProjectionOnly`.
- no scalar fallback, mutable VMCS store, virtualization execution unit or `VmxCaps` authority in the Phase 10 source path.
- `SecureMemorySources_DoNotCreateVmcsBackedTranslationAuthority`.
- no hardware tags, tagged-memory, CHERI, VMX EPT/NPT/VPID, VMREAD/VMWRITE memory authority or capability-aware LOAD/STORE/FETCH in the Phase 11 source path.
- `SecureIoHypercallSources_DoNotCreateVmxVmcsOrVmxCapsAuthority`.
- no ID-only `AllowsSharedBuffer(bufferId)` authority helper in the Phase 12 source path.
- no propagation of publication-fence completion/retire flags into an allowed Phase 12 I/O result.
- Phase 14 scoped source scan over `SecureCompletionRetirePublicationAuthorityPolicy.cs` proving no dependency on `TrapCompletionRouteService`, `TrapCompletionRouteDescriptor` or `TrapCompletionPublicationFence`, and proving proof-only/admitted-denied/registry-backed/VMX projection branches fail closed.
- Phase 15 scoped source scan over `SecureOutputManifestClassificationPolicy.cs` proving forbidden-payload denial branches, complete-manifest classification, no runtime/publication authority true flags and no dependency on VMX/VMCS/`VmxCaps`, VMREAD/VMWRITE, trap-route services, backend execution request/result types or compiler controlled-emission shortcuts.
- Phase 16 scoped source scan over `SecureDebugAttestationVisibilityPolicy.cs` proving denied shortcut branches, no runtime/publication authority true flags and no dependency on VMX/VMCS/`VmxCaps`, compatibility read/write instruction paths, backend execution request/result types or compiler controlled-emission shortcuts.
- Phase 17 scoped source scan over `SecureComputeNamedPathVmxZeroAuthorityPolicy.cs` proving denied shortcut branches, no VMX/publication authority true flags and no dependency on VMX runtime managers, field read/write helpers, backend execution request/result types or compiler controlled-emission shortcuts.
- Phase 19 scoped source scan over `SecureComputeControlledEmissionGatePolicy.cs` and selected compiler API/IR surfaces proving no SecureCompute emit/helper shortcut, backend execution flag, publication flag, capability-aware instruction shortcut or tagged-memory shortcut.
- Phase 20 scoped source scan over `SecurePositiveRuntimeExecutionActivationPolicy.cs` proving all current runtime execution, backend-result, completion publication, retire publication, VMX authority, compiler emission, nested execution and production release authority bits remain false.
- Phase 21 scoped source scan over `SecureComputePhase21ConformanceEvidencePolicy.cs` proving conformance matrix evidence does not create runtime execution, backend-result, completion publication, retire publication, VMX authority, compiler emission, nested execution or production release authority.
- Phase 22 scoped source scan over `SecureComputePhase22LimitedReleaseGatePolicy.cs` proving release evidence classification does not create runtime execution, backend-result, completion publication, retire publication, VMX authority, compiler emission, nested execution or production release authority in the current no-positive-path baseline.
- no raw host/device pointer, VMX/VMCS/`VmxCaps`, VMCALL or Lane6/Lane7 native-token authority in the Phase 12 source or conformance path.

## Migration/Evidence Classification

The release gate must list every state emitted by the activated path and classify it. Unclassified state blocks release.

## Completion/Retire Implications

The release gate must state the highest publication ladder step reached by the path. A no-publication path may still be a limited internal activation if all other gates pass.

## SecureCompute Activation Implications

Activation can be claimed only for the named limited path. All other SecureCompute surfaces remain readiness/fail-closed/proof-only/design-fenced/future.

## Exit Criteria

- one path has production code;
- independent evidence binds the exact decode/SafetyVerifier/certificate/issue/backend/result/completion/retire call graph at an immutable clean revision;
- CPU Stage-B route is proven from canonical decode identity through SafetyVerifier certificate and issue carrier; direct generic-service tests alone are insufficient;
- owner-specific RFC accepted;
- Phase 09 owner acceptance remains projection-closed unless Phase 10 separately passes;
- Phase 10 projection is restricted to `GuestCr0`/`GuestCr4` and remains side-effect-free;
- Phase 11 memory/private-domain policy admission is restricted to descriptor-owned policy checks and remains non-executing;
- Phase 12 secure I/O/shared-buffer policy admission is restricted to current descriptor-owned bindings and remains non-executing and non-publishing;
- tests and static scans pass;
- migration/evidence classes complete;
- VMX zero-authority preserved;
- release notes use limited wording only.
- release evidence names one exact transport/service/owner contract and does not infer service identity from `VMCALL`, a trap reason or a test fixture ID.
- Phase 13 owner-contract/identifier allocation is closed by `SecureHypercallBackendOwnerAbiRegistry`.
- Phase 14 production backend-result/completion/retire owner chain is proven.
- Phase 15 exhaustive checkpoint/restore protocol with deny-by-default payload taxonomy is proven.
- Phase 16 single production evidence publisher is proven.
- Phase 17 VMX boundary zero-authority gate is closed.
- Phase 19 compiler no-emission to controlled-emission gate is closed as an explicit no-compiler-change decision.
- Phase 20 positive runtime execution activation remains future-gated because no named runtime owner/path/reachability chain is locally proven.
- Phase 21 conformance matrix is closed only for current negative/future-gated evidence and is not activation approval.
- Phase 22 classifier remains permanently hard-denied and cannot itself approve release, even after an offline verifier exists.
- limited release remains blocked because backend execution remains closed and Phase 20/22 evidence is not complete.
- applicable Phase 17 and Phase 19 decisions are complete for the named path.
- rollback procedure restores all negative gates without relying on destructive repository-wide reset.

## Dependency

Previous: `21_conformance_negative_positive_test_matrix.md`. Next: `23_open_decision_backlog.md`.
