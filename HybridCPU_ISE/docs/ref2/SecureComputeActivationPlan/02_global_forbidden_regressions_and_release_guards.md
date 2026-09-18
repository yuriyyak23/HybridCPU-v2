# Global Forbidden Regressions And Release Guards

## Phase Metadata

- File name: `02_global_forbidden_regressions_and_release_guards.md`
- Phase goal: define regressions that must fail release before any activation work can merge.
- Status: release guard; not activation approval.
- Scope: source scans, doc scans, product-claim scans and owner-map completeness guards.
- No-goals: no production implementation and no exception path for compatibility surfaces.

## Current Baseline

The current corpus already denies VMX authority, secure VMCS, `VmxCaps` authority, CHERI ISA, tagged memory, proof-only execution, admitted-denied backend success, migration of host evidence and nested execution.

## Authority Owner

Release authority belongs to conformance gates. Runtime authority remains neutral runtime-owned and owner-specific.

## What Can Be Implemented

- static source scans over `SecureComputeActivationPlan`;
- tests mirroring `SecureComputePhase10ReleaseGateTests`;
- source guards for backend execution and publication booleans;
- product-claim lint.

## What Remains Denied/Future-Gated

- all VMX/VMCS/`VmxCaps` authority;
- all CHERI/tagged-memory/capability-register semantics;
- all backend execution without Phase 20 implementation;
- all completion/retire publication without backend success and explicit fences.

## Forbidden Shortcuts

- `VMX activates SecureCompute`;
- secure VMCS or VMCS secure state store;
- `VmxCaps` SecureCompute grant;
- VMREAD secure-state authority;
- VMWRITE secure-state mutation;
- active VMCS pointer as domain identity;
- Stage B bypass for non-ordinary secure operations remains a forbidden regression when the secure descriptor is absent, disabled or unmaterialized;
- proof-only backend owner as execution;
- admitted-denied hypercall as success;
- private memory descriptor as hardware tag;
- sealed checkpoint payload as CHERI sealing;
- debug or telemetry as runtime authority.
- caller-supplied full descriptor as runtime identity or authority;
- `...Validated`, `...Authorized` or `...Proven` boolean as a runtime certificate;
- admission certificate as an execution certificate;
- generic positive/default-allow handling for an unknown operation or migration payload class;
- documentation/source-string success as executable conformance or release evidence.

## Required RFC/ADR

None for guards. Any proposed exception must be rejected unless a prior owner-specific RFC/ADR exists and this file is updated with new negative tests.

## Code Anchors

- `SecureBackendOwnerAdmissionPolicy.cs`
- `SecureIoHypercallAdmissionPolicy.cs`
- `SecureCompletionPublicationFence.cs`
- `SecureComputeCompatibilityBoundaryMatrixPolicy.cs`
- `SecureComputeVmReadVisibilityPolicy.cs`
- `SecureComputeVmWriteDenyPolicy.cs`
- `SecureComputeVmxCapsProjectionFence.cs`
- `SecureComputeNoEmissionContract.cs`

## Documentation Anchors

- `SecureComputerefactoringNew/02_architecture_invariants_and_closure_taxonomy.md`
- `SecureComputerefactoringNew/21_release_gate_and_activation_checklist.md`
- `VirtualiztionRefactoringNew/16_external_audit_activation_readiness_addendum.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`

## Required Tests

- doc-lint tests for forbidden authority claims;
- source tests that `BackendExecutionAuthorized: true` is absent in current secure backend/hypercall code;
- source tests that `CompletionPublicationAuthorized: true` and `RetirePublicationAuthorized: true` are absent from admitted-denied constructors;
- VMX denial tests for activate, grant, store, read, write, checkpoint and caps.
- Stage B negative tests proving non-ordinary secure operations with absent, disabled or unmaterialized descriptors are denied.

## Required Static/Source Scans

Run scans for:

- `VMX activates SecureCompute`
- `VmxCaps grants SecureCompute`
- `secure VMCS`
- `BackendExecutionAuthorized: true`
- `CompletionPublicationAuthorized: true`
- `RetirePublicationAuthorized: true`
- `AllowedProofOnlyNoExecution.*backend`
- `AllowedAdmittedDenied.*success`
- `CHERI ISA`
- `tagged memory`
- `capability-aware LOAD`
- `capability-aware STORE`
- `capability-aware FETCH`
- `RuntimeOwnedPublication.*authorizes`
- `secureDescriptor is { IsEnabled: true }`
- `context.SecureCompute ?? request.SecureDescriptor`
- `_ => Allowed`

Matches are allowed only in denied, non-goal, blocker or future-gated contexts.

The descriptor-carrier and default-allow patterns are findings, not blanket lexical bans: review their owner, input provenance and reachable consumers before classifying a match.

Scans must be owner/path scoped. Neutral runtime infrastructure may legitimately contain publication-authorized result shapes; a violation occurs when a SecureCompute proof-only, admitted-denied, policy-only or projection-only path can reach those results without the required backend, completion and retire owners.

## Audit-Derived Rollback Gate

Any regression in a mandatory deny-path blocks merge and release. Rollback must cover code and claims:

- remove or disable the newly positive owner path;
- restore compiler no-emission when controlled emission is not independently approved;
- close compatibility advertisement or projection rows added by the failed path;
- remove `activated`, production-ready or feature-complete wording;
- restore green negative tests and source scans before work continues.

Do not use destructive repository-wide rollback commands in a dirty worktree. Revert only the scoped positive-path changes or apply a bounded corrective patch.

## Migration/Evidence Classification

Release must fail if host-owned evidence, scheduler evidence, backend binding evidence, native token evidence, debug traces, VMCS metadata, raw measurement secrets, raw sealing keys or active host pointers become checkpoint authority.

## Completion/Retire Implications

Release must fail if completion route or fence class existence is described as publication permission.

## SecureCompute Activation Implications

These guards are prerequisites. Passing them is not activation.

## Exit Criteria

- all guard patterns have tests or static commands;
- no forbidden shortcut appears outside denied/future contexts;
- every future exception points to an owner-specific RFC/ADR.
- rollback trigger and bounded rollback procedure are recorded for every future positive path.

## Dependency

Previous: `01_current_state_and_gap_matrix.md`. Next: `03_owner_specific_rfc_adr_process.md`.
