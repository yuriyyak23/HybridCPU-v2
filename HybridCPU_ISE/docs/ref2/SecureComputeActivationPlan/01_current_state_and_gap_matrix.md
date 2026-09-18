# Current State And Gap Matrix

## Phase Metadata

- File name: `01_current_state_and_gap_matrix.md`
- Phase goal: classify the current SecureCompute baseline before activation work.
- Status: readiness audit; not activation approval.
- Scope: current code, tests, `SecureComputerefactoringNew`, old SecureCompute `Plan/`, `Plan2/`, whitebooks, VMX refactoring corpus and Stream/Lane boundary references.
- No-goals: no production path opens; no wording that existing proof-only or admitted-denied paths are execution.

## Current Baseline

The repository implements a bounded SecureCompute baseline:

- neutral root descriptor DTO and admission-level no-effect disabled state;
- optional runtime context full-descriptor binding, plus a second request full-descriptor carrier;
- generic runtime boundary admission hook, reached in production only from VMX compatibility paths found by the current scan;
- memory/evidence/nested policy classifiers plus partial migration classifiers; unknown checkpoint payload values still default allow;
- descriptor-level grants, bounds, provenance and epochs;
- explicit I/O shared-buffer policy and admitted-denied hypercall recognition;
- proof-only backend owner gate;
- VMX/VMCS/`VmxCaps` deny/projection matrix;
- compiler no-emission boundary;
- release-gate tests and static source guards.

## Authority Owner

No positive SecureCompute runtime authority owner is currently proven. Descriptors and policies are data/decision surfaces, and RFC/ADR outputs are governance only. VMX, VMCS, `VmxCaps`, VMREAD, VMWRITE, caller booleans, evidence, telemetry, tests and documentation do not own SecureCompute authority.

## Closure Classification Matrix

| Area | Classification | Evidence anchor | Activation implication |
| --- | --- | --- | --- |
| SecureCompute root descriptor | partial DTO; lifecycle owner open | `SecureComputeDomainDescriptor.cs` | public construction and `IsActive` are not materialization/revocation authority |
| disabled/no-effect baseline | partial | `SecureComputeDomainDescriptorNoEffectTests.cs`, `SecureRuntimeBoundaryAdmissionHookTests.cs` | admission-level ordinary no-effect only; pipeline/SMT/FSP/memory/I/O/exception/retire equivalence unproven |
| DomainRuntimeContext binding | overestimated | `DomainRuntimeContext.cs` | carries replaceable full descriptor; no opaque registry binding/generation |
| RuntimeBoundaryAdmissionService secure checks | generic policy route only; CPU Stage-B open | `RuntimeBoundaryAdmissionService.cs`, `VmxCompatibilityAdmissionService*.cs` | direct policy tests deny missing/disabled/unmaterialized non-ordinary requests; canonical decode/SafetyVerifier/issue reachability is absent |
| secure memory/private/shared/measured policy | partial policy class | `SecureMemoryDomainDescriptor.cs`, `SecureMemoryAdmissionPolicy.cs` | first-match regions are not canonical and production translation/cache/backing/DMA paths do not call the policy |
| measurement/evidence visibility | implemented policy, visibility-only | `DomainMeasurementDescriptor.cs`, `SecureEvidencePolicy.cs` | evidence does not become authority |
| debug/attestation visibility | partial classifier | `SecureDebugAttestationVisibilityPolicy.cs`, focused tests | no exclusive production publisher/API path; visibility results are not authority |
| secure migration/checkpoint/restore | partial classifier | `SecureMigrationAdmissionPolicy.cs`, `SecureCheckpointPayloadPolicy.cs`, `SecureOutputManifestClassificationPolicy.cs` | unknown payloads default allow; no serializer/sealer/key owner/anti-replay/atomic restore protocol |
| secure I/O/shared buffer | partial policy class | `SecureIoDomainDescriptor.cs`, `SecureIoHypercallAdmissionPolicy.cs` | first-match buffer lookup, no device/PASID/IOMMU owner and no DMA effect-path caller |
| secure hypercall/trap | admitted-denied | `SecureHypercallDescriptor.cs`, `SecureIoHypercallAdmissionPolicy.cs` | no backend success or publication |
| Phase 13 owner/service contract | proof-only contract/identifier vocabulary | `SecureHypercallBackendContract.cs`, `SecureHypercallBackendOwnerAbiRegistry.cs`, `SecureHypercallBackendContractAdmissionPolicy.cs`, `SecureHypercallBackendOwnerPhase13Tests.cs` | direct classifier tests only; execution/result/publication owner path remains absent |
| completion/retire publication | partial policy model; production owner chain open | `SecureCompletionRetirePublicationAuthorityPolicy.cs` | no production caller from a SecureCompute backend result to completion and retire |
| typed grants/monotonicity | partial validator | `SecureGrantAuthorityPolicy.cs`, `SecureAuthorityBounds.cs` | caller-supplied handles/booleans are validated; no mint/revoke/reissue ledger |
| SecureBackendOwnerAdmissionPolicy | proof-only | `SecureBackendOwnerAdmissionPolicy.cs` | `AllowedProofOnlyNoExecution` stays non-executing |
| privileged execution-state owner | implemented owner proof | `PrivilegedExecutionStateDescriptor.cs`, `PrivilegedExecutionStateOwnerPolicy.cs` | owner acceptance validates tags, epoch, legality, evidence and restore class but authorizes no projection or side effects by itself |
| `GuestCr0`/`GuestCr4` read-only projection | implemented narrow projection-only path | `PrivilegedExecutionStateProjectionService.cs`, `VmcsReadOnlyValueProjectionService.cs`, `GuestCr0Cr4ReadOnlyProjectionTests.cs` | values project only after owner/value/visibility/migration/conformance gates; no mutation, backend success or publication |
| SecureIoHypercallAdmissionPolicy | admitted-denied | `SecureIoHypercallAdmissionPolicy.cs` | `AllowedAdmittedDenied` stays non-executing |
| VMX compatibility deny/projection | implemented Phase 17 named-path zero-authority gate | `SecureComputeCompatibilityBoundaryMatrixPolicy.cs`, `SecureComputeNamedPathVmxZeroAuthorityPolicy.cs`, `SecureComputeVmxPhase17NamedPositivePathZeroAuthorityTests.cs` | projection only after neutral owner/result; VMX/VMCS/`VmxCaps` cannot own SecureCompute authority |
| VMREAD/VMWRITE secure-state denial | implemented denial | `SecureComputeVmReadVisibilityPolicy.cs`, `SecureComputeVmWriteDenyPolicy.cs` | VMREAD/VMWRITE cannot own secure state |
| SecureCompute VmxCaps denial | implemented denial | `SecureComputeVmxCapsProjectionFence.cs` | `VmxCaps` cannot grant or activate |
| nested secure domain | design-fence | `SecureNestedDomainAdmissionPolicy.cs` | no nested execution |
| Lane6/Lane7/Stream boundary | model/helper-only, denied as SecureCompute authority | Stream whitebooks, VMX tests | not VMX or SecureCompute authority |
| compiler/no-emission boundary | implemented Phase 19 product fail-closed compiler decision gate | `SecureComputeNoEmissionContract.cs`, `SecureComputeControlledEmissionGatePolicy.cs`, compiler negative tests | product no-compiler-change is the only allowed current decision; a hermetic test-only carrier profile is recommended but unimplemented and owns no runtime authority |
| positive runtime execution activation | Phase 20 pre-activation evidence classifier | `SecurePositiveRuntimeExecutionActivationPolicy.cs`, focused negative tests | caller booleans classify missing evidence; no executable path and every authority bit remains false |
| conformance evidence matrix | Phase 21 negative/future-gated matrix | executable policy negatives plus source/doc-string guards | documentation checks are excluded from execution-proof numerator |
| limited release gate | Phase 22 permanently deny-only | `SecureComputePhase22LimitedReleaseGatePolicy.cs` | fully populated request still denies; future release evidence belongs to a separate offline verifier |
| CHERI/capability-aware ISA boundary | forbidden/current future only | old SecureCompute docs and no-emission tests | no tagged memory or capability-aware LOAD/STORE/FETCH |
| release gate and product-claim hygiene | implemented for readiness, future for activation | `SecureComputePhase10ReleaseGateTests.cs` | activation claim still blocked |

## External Audit Addendum, 2026-06-11

Verified post-fix code facts:

- `RuntimeBoundaryAdmissionService` selects `context.SecureCompute ?? request.SecureDescriptor` and calls `_secureAdmission.Admit(...)` for every `request.SecureOperationClass != Ordinary`.
- The old `secureDescriptor is { IsEnabled: true }` Stage B guard has been removed from the runtime path and is retained only as a release-regression scan pattern.
- Direct calls to `RuntimeBoundaryAdmissionService` propagate missing, disabled and unmaterialized descriptor denials for caller-selected non-ordinary operations; CPU Stage-B reachability remains unproven.
- `SecureRuntimeBoundaryAdmissionHookTests` assert ordinary no-effect for absent/disabled/unmaterialized descriptors and fail-closed denial for non-ordinary missing/disabled/unmaterialized descriptors.

Activation implication: the direct generic-boundary behavior is valid for ordinary/no-effect and non-ordinary fail-closed unit cases. The CPU Stage-B P0 blocker remains open because decode/SafetyVerifier/issue production reachability and an immutable admission carrier are absent.

## External Architecture Audit Revalidation, 2026-06-12

Direct local verification confirms:

- toolchain pin: .NET SDK `10.0.201` in `global.json`;
- solution entrypoint: `HybridCPU v2.slnx`;
- Debug test-support behavior: `DefineTestSupport=true` by default through `Directory.Build.props`;
- no production SecureCompute `BackendExecutionAuthorized: true` path;
- `AllowedProofOnlyNoExecution` remains the backend-owner ceiling;
- `AllowedAdmittedDenied` and `DeniedBackendSuccessClosed` remain the secure-hypercall execution boundary;
- generic completion/retire-authorized results exist in neutral trap-routing infrastructure, so SecureCompute source scans must be owner/path scoped rather than treating every repository-wide true flag as a violation;
- the VMX ABI proves `VMCALL` opcode `259` and register form, while `SecureHypercallBackendOwnerAbiRegistry` separately owns the exact Phase 13 decoded leaf/service/owner IDs;
- `SecureHypercallDescriptor.AllowedHypercallIds` is policy data; historical test fixture `0x10` is not hard-coded in the production descriptor and is not the Phase 13 ABI allocation.

Current P0 production-activation blockers:

- no canonical operation taxonomy derived from decode identity;
- no SafetyVerifier-issued `SecureAdmissionCertificate`;
- no single descriptor registry/lifecycle owner or opaque runtime binding;
- two full-descriptor carrier sources remain (`context.SecureCompute ?? request.SecureDescriptor`);
- no grant mint/revoke/reissue ledger;
- no exhaustive operation-specific admission; generic positive/default-allow branches remain;
- no CPU production composition root from admission through backend result, completion and retire;
- no approved and implemented owner-specific positive backend execution path;
- Phase 14 supplies a negative policy classifier only; no production caller or future positive completion/retire output ownership exists;
- typed backend request/result proof vocabulary exists but has no execution authority;
- no SecureCompute completion/retire owner chain after a future real backend result;
- no Phase 20 positive execution path consuming the Phase 15 output/migration manifest classification or Phase 16 visibility classification for a named path;
- no independent offline release evidence package/verifier for one named restricted path; Phase 22 remains deny-only.
- Phase 17 VMX zero-authority is closed for named positive-looking paths, but no Phase 20 positive execution path exists.
- Phase 20 records missing evidence but is not the next implementation step; the dependency chain starts with audit reproducibility, taxonomy, descriptor registry, certificate and grant ledger.
- Phase 21 can package current negative/future-gated conformance evidence, but that package is not runtime execution or release proof.
- Phase 22 has a fail-closed release classifier, but no production release approval exists without a named positive runtime path.

## 2026-08-07 Revalidated Closure Summary

| Class | Phases/areas |
| --- | --- |
| confirmed within a narrow negative or projection scope | 02-03 governance, 09-10 read-only projection, 13 proof-only ABI vocabulary, 17 VMX zero-authority, 18 design fence, compiler no-emission decision in 19 |
| partial | 04, 07-08, 11-12, 15-16, 19 generated-artifact proof |
| overestimated by earlier wording | 05 descriptor ownership, 06 CPU Stage-B enforcement, 14 production publication authority |
| open/future-gated | canonical taxonomy, registry, ledger, certificate, production carrier, hermetic test profile, decode-to-deny path, transport probe, backend receipt/completion/retire chain, memory/IOMMU/hypercall effects, checkpoint protocol, evidence publisher, disabled equivalence and independent offline release verifier |

The ranked C0/C1/C2 blocker registry and exact dependency order are in `24_audit_revalidation_and_dependency_order.md`. The source-checked recommended decisions and corrected ordering are in `25_external_analysis_reconciliation_and_recommended_decisions.md`.

## What Can Be Implemented

- Activation corpus and release-gate docs.
- Static gates over this new directory.
- Gap-specific negative tests that preserve current denial behavior.
- Owner map templates.
- Phase 20 pre-activation evidence classification maintenance only; executable work follows Phase 24 ordering.

## What Remains Denied/Future-Gated

- secure backend execution;
- hypercall backend success;
- publication from proof-only or admitted-denied paths;
- VMCS secure state store;
- VMREAD expansion beyond the gated `GuestCr0`/`GuestCr4` fields;
- nested execution;
- compiler secure emission;
- CHERI ISA or tagged-memory semantics.

## Forbidden Shortcuts

- VMX/VMCS/`VmxCaps` authority.
- SecureCompute VMX mode.
- VMREAD/VMWRITE as secure-state authority.
- Capability registers, capability operands or capability-aware LOAD/STORE/FETCH.
- Treating tests, docs, telemetry or evidence as activation evidence.

## Required RFC/ADR

No RFC/ADR is required to classify the baseline. `ADR-SC-PES-GuestCr0Cr4` and Phase 10 projection are implemented narrowly. Phase 11/12 policy semantics do not establish memory or I/O enforcement; any positive path requires its named implementation and release gates.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/**`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Services/DomainRuntimeContext.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Publication/SecureCompletionRetirePublicationAuthorityPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Checkpoint/SecureOutputManifestClassificationPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/**`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Conformance/NoEmission/SecureComputeNoEmissionContract.cs`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/deep-research-report (7).md`
- `HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew/`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan2/`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `Documentation/Virtualization WhiteBook/`
- `Documentation/Stream WhiteBook/`

## Required Tests

- existing SecureComputeRefactoring tests must stay green;
- existing VMX boundary tests must stay green;
- add release-gate coverage for this activation directory.

## Required Static/Source Scans

- scan for VMX/VMCS/`VmxCaps` authority wording;
- scan for proof-only/admitted-denied overclaim;
- scan for CHERI/tagged-memory/capability-aware ISA implementation wording outside denied/future contexts;
- scan for product activation claims.

## Migration/Evidence Classification

All current host-owned evidence, scheduler evidence, backend binding evidence, native tokens, raw secrets, active host pointers and VMCS/compat metadata remain denied as checkpoint/migration/output-manifest authority. Phase 15 classifies request state, internal backend result, internal completion record, guest-visible output, retire-visible state and recomputed-after-restore state as manifest coverage only.
Phase 16 classifies debug trace, attestation report, telemetry snapshot, host-inspection metadata and compatibility-alias evidence as visibility only; those entries do not become migration payload, checkpoint authority, backend owner proof, activation evidence, completion publication or retire publication.

## Completion/Retire Implications

No current area publishes completion or retire effects for SecureCompute execution. Phase 14 proves proof-only, admitted-denied and registry-backed Phase 13 paths remain below publication; fences and route classes remain infrastructure unless a future owner/path-reachable backend result satisfies the Phase 14 policy. Phase 15 output-manifest classification does not publish completion or retire effects.

## SecureCompute Activation Implications

Current state is readiness/proof-only/release-gate, not activated production SecureCompute.

## Exit Criteria

- every mandatory area has a classification row;
- every future path has a denial reason or RFC dependency;
- no activation claim is made.

## Dependency

Previous: `00_securecompute_activation_refactoring_index.md`. Next: `02_global_forbidden_regressions_and_release_guards.md`.
