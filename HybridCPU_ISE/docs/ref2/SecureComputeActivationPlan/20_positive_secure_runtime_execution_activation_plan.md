# Pre-Activation Runtime Evidence Classifier And Future Execution Plan

## Phase Metadata

- File name: `20_positive_secure_runtime_execution_activation_plan.md`
- ADR ID: `ADR-SC-POSITIVE-RUNTIME-EXECUTION-ACTIVATION`
- Phase goal: classify whether prerequisites for a future minimal named SecureCompute path exist without treating caller assertions as runtime proof.
- Status: pre-activation evidence classifier only; no named positive runtime owner/path/reachability chain is locally proven.
- Maximum current statement: Phase 20 remains future-gated; prior gates through Phase 17 and Phase 19 remain closed, Phase 18 remains future/design-fenced, and no production SecureCompute activation is approved.
- Scope: named path owner, runtime authority owner, backend-result owner boundary, request/result vocabulary, execution preconditions, publication preconditions, migration/output manifest, restore rules, debug/attestation visibility, VMX projection, compiler no-emission, nested exclusion, source scans, release evidence and rollback.
- No-goals: no backend executor, no success stub, no production release, no compiler secure emission, no VMX-owned authority and no Phase 18 nested execution.

## Current Baseline

Current backend owner admission remains `AllowedProofOnlyNoExecution` or denial. `SecureIoHypercallAdmissionPolicy` can still recognize `AllowedAdmittedDenied`, but `BackendExecutionAuthorized`, `CompletionPublicationAuthorized` and `RetirePublicationAuthorized` remain false. `SecureCompletionRetirePublicationAuthorityPolicy` has a typed publication ladder, but no SecureCompute producer proves `SecurePublicationBackendResultState.InternalNeutralResult` through a named runtime owner/path/reachability chain.

Local source review for Phase 20 finds no production SecureCompute `BackendExecutionAuthorized: true` path and no production SecureCompute runtime request/result owner that can produce a neutral backend result. Manifest, debug/attestation, VMX projection and compiler no-emission gates are required evidence or boundaries, not execution proof.

## Named Positive Path Owner

None is currently proven.

The first future path must name exactly one positive path owner. The owner must be a neutral runtime-owned descriptor, not a hypercall admission result, not a VMX compatibility projection, not a debug/attestation visibility result, not an output manifest and not compiler sideband metadata.

The recommended future candidate remains a restricted neutral runtime owner with no publication semantics by default. The current Phase 13 hypercall backend owner registry is not that owner because it stops at proof-only admission.

## Exact Runtime Authority Owner

No current runtime authority owner exists for Phase 20.

Future acceptance requires all of:

- materialized neutral owner identity;
- current owner epoch;
- typed grant proof;
- evidence proof;
- backend-result owner boundary proof;
- owner/path/reachability proof from the owner to the runtime path;
- negative conformance proof for all denied shortcuts.

Admission is not execution authority. Owner acceptance is not backend execution authority.

## Backend-Result Owner Boundary

The backend-result owner boundary remains open. A future positive path must prove where a neutral backend result is produced and who owns it.

Current code intentionally denies these substitutions:

- proof-only owner admission as backend result;
- admitted-denied hypercall recognition as backend result;
- registry-backed Phase 13 admission as backend result;
- Phase 14 publication vocabulary as backend result;
- Phase 15 output manifest entry as backend result;
- Phase 16 visibility as backend result;
- Phase 17 compatibility projection as backend result;
- Phase 19 no-compiler-change decision as backend result.

`SecurePositiveRuntimeExecutionActivationPolicy` records this fail-closed boundary for Phase 20.

## Request And Result Vocabulary

Phase 20 adds typed, neutral, non-executing evidence-classification vocabulary:

- `SecurePositiveRuntimePathCandidate`;
- `SecurePositiveRuntimeExecutionActivationRequest`;
- `SecurePositiveRuntimeExecutionActivationDecision`;
- `SecurePositiveRuntimeExecutionActivationResult`;
- `SecurePositiveRuntimeExecutionActivationPolicy`.

This vocabulary is a guard and evidence classifier. Its boolean inputs such as `OwnerPathReachabilityProven` and `NeutralBackendResultProduced` are caller assertions and can never be passed to execution as authority. It is not a backend executor, execution certificate, success stub, compiler ABI or release gate.

All current Phase 20 results keep:

- `RuntimeExecutionAuthorized == false`;
- `BackendResultAccepted == false`;
- `CompletionPublicationAuthorized == false`;
- `RetirePublicationAuthorized == false`;
- `VmxAuthorityAuthorized == false`;
- `CompilerEmissionAuthorized == false`;
- `NestedExecutionAuthorized == false`;
- `ProductionReleaseApproved == false`.

## Execution Preconditions

A future named path must prove all of the following before runtime execution can be considered:

1. exact named positive path owner;
2. exact runtime authority owner;
3. backend-result owner boundary;
4. owner/path/reachability from the owner to the path;
5. neutral backend result produced by that path;
6. complete migration/output-manifest coverage;
7. restore-time revalidation or recomputation rules;
8. Phase 18 nested exclusion;
9. Phase 19 no-compiler-change decision unless a later controlled-emission RFC is separately approved;
10. independent offline release-verifier evidence if a limited/production build profile is claimed; Phase 22 itself remains deny-only.

The current repository fails before item 1 because canonical taxonomy, descriptor registry, SafetyVerifier certificate, grant ledger, disabled equivalence and production admission carrier are also absent. Phase 20 therefore remains pre-activation only.

## Completion Publication Preconditions

Completion publication remains Phase 14-owned. A future Phase 20 path can request completion publication only after all execution preconditions plus:

- `SecurePublicationPathKind.NeutralRuntimeBackendResult`;
- `SecurePublicationBackendResultState.InternalNeutralResult`;
- materialized backend-result owner;
- materialized internal completion record;
- current neutral completion owner;
- completion owner proof;
- completion fence;
- owner/path/reachability from backend result to completion owner.

Current proof-only, admitted-denied, registry-backed, manifest-only, visibility-only and projection-only paths do not meet these preconditions.

## Retire Publication Preconditions

Retire publication remains separate from completion. It additionally requires:

- prior secure completion publication;
- current neutral retire owner;
- retire owner proof;
- explicit retire fence;
- safe retire evidence class;
- safe retire migration class;
- owner/path/reachability from completion to retire owner.

Retire cannot be derived from admission, completion fence presence, route flags, manifest coverage, debug visibility, VMX projection or compiler no-emission.

## Migration And Output Manifest Evidence Package

Phase 15 manifest coverage is required before any future positive path evidence can be considered. It is still classification evidence only.

The required future package must classify:

- request state;
- internal backend result;
- internal completion record;
- guest-visible output;
- retire-visible state;
- recomputed-after-restore state.

Denied as authority or payload:

- host-owned evidence;
- scheduler evidence;
- backend binding evidence;
- native tokens;
- raw measurement secrets;
- raw sealing keys;
- active host pointers;
- VMCS projection metadata;
- compatibility projection metadata.

Manifest-only records are not execution proof.

## Restore And Recomputed-State Rules

Restore must revalidate or recompute every allowed future output. Recomputed-after-restore state requires restore validation proof and cannot import host-owned evidence, native tokens, active pointers, raw secrets, VMCS metadata or compatibility projection metadata as authority.

Any future internal result restored after migration must be recomputed by the neutral owner or revalidated by a separately approved restore rule. Restored metadata cannot stand in for owner/path/reachability.

## Debug And Attestation Visibility Limits

Phase 16 visibility remains visibility only.

Debug trace, attestation report, telemetry snapshot, host-inspection metadata and compatibility-alias evidence cannot become:

- runtime authority;
- compatibility-read value authority;
- migration authority;
- activation evidence;
- backend owner proof;
- completion publication;
- retire publication.

## VMX Projection After Neutral Result Only

Phase 17 remains closed as named-path VMX zero-authority. VMX readiness/projection is not SecureCompute authority.

Compatibility projection for a future Phase 20 path is allowed only after a neutral runtime result exists, and even then it remains projection-only. VMX, VMCS, `VmxCaps`, active VMCS pointers, trap reasons, `VMCALL` transport opcode and compatibility completion cannot source service identity, owner identity, grants, migration authority, backend success, completion publication or retire publication.

Transport opcode, trap reason, decoded leaf, service ID and backend owner ID remain separate typed concepts. `0x10` remains a fixture, not production ABI.

## Compiler No-Emission Decision

Phase 19 is closed as an explicit product no-compiler-change decision. Product compiler secure emission remains closed.

The first future restricted runtime path must use existing runtime transport without product compiler secure emission unless a later controlled-emission RFC/ADR is approved after named limited runtime release. Before the neutral transport probe, a hermetic test-only carrier profile may be implemented solely to drive the public canonical decoder to expected denial; it is not product emission, executable authority or release evidence. Compiler sideband metadata cannot become executable authority or runtime evidence.

## Phase 18 Nested Exclusion

Phase 18 remains future/design-fenced. Phase 20 excludes:

- nested execution;
- mutable nested secure state;
- Shadow VMCS authority;
- VMCS12/VMCS02 authority;
- nested completion publication;
- nested retire publication;
- nested migration authority from checkpoint facts.

Nested child intent and parent-child monotonicity cannot satisfy Phase 20 runtime execution.

## Failure Taxonomy

Phase 20 fail-closed decisions:

- `FutureGatedNoNamedPositivePath`;
- `DeniedProofOnlyOwnerAdmission`;
- `DeniedAdmittedDeniedHypercallAdmission`;
- `DeniedPhase13ProofOnlyAdmission`;
- `DeniedPhase14PublicationVocabularyOnly`;
- `DeniedPhase15ManifestOnlyEvidence`;
- `DeniedPhase16VisibilityOnlyEvidence`;
- `DeniedPhase17VmxZeroAuthorityOnly`;
- `DeniedPhase18NestedDesignFence`;
- `DeniedPhase19NoCompilerChangeOnly`;
- `DeniedMissingRuntimeAuthorityOwner`;
- `DeniedMissingBackendResultOwnerBoundary`;
- `DeniedMissingOwnerPathReachabilityProof`;
- `DeniedMissingNeutralBackendResult`;
- `DeniedMissingMigrationOutputManifestEvidence`;
- `DeniedMissingRestoreRules`;
- `DeniedMissingCompilerNoEmissionDecision`;
- `DeniedProductionReleaseGate`.

## Owner/Path/Reachability Scoped Source-Scan Policy

Phase 20 source scans are owner/path/reachability scoped. Blanket scans over generic trap-route flags are not valid SecureCompute execution evidence.

The owner/path/reachability scoped source-scan policy is part of the Phase 20 evidence boundary, not a production activation shortcut.

Required scoped scans:

- `SecurePositiveRuntimeExecutionActivationPolicy.cs` keeps all authority/release bits false.
- `SecureHypercallBackendOwnerAbiRegistry.cs` remains identifier allocation only.
- `SecureHypercallBackendContractAdmissionPolicy.cs` remains proof-only for valid registry-backed admission.
- `SecureCompletionRetirePublicationAuthorityPolicy.cs` cannot be used as runtime owner/path proof.
- `SecureOutputManifestClassificationPolicy.cs` remains manifest/classification evidence only.
- `SecureDebugAttestationVisibilityPolicy.cs` remains visibility only.
- `SecureComputeNamedPathVmxZeroAuthorityPolicy.cs` remains zero-authority.
- `SecureComputeControlledEmissionGatePolicy.cs` keeps no-compiler-change as the only current compiler decision.
- completion/retire record and VMX trap/projection paths are scanned only for reachability from a named SecureCompute owner, not for generic infrastructure flags.

Forbidden current source evidence:

- production SecureCompute `BackendExecutionAuthorized: true`;
- production SecureCompute `CompletionPublicationAuthorized: true`;
- production SecureCompute `RetirePublicationAuthorized: true`;
- `SecureBackendExecutionRequest` or `SecureBackendExecutionDecision` as current production runtime vocabulary;
- VMX/VMCS/`VmxCaps` owner identity;
- compiler secure emission shortcut;
- nested execution shortcut.

## Release Evidence Boundaries

Phase 20 is not Phase 22. Passing Phase 20 future-gated tests does not approve production activation.

Future release evidence must include:

- repository revision and dirty-worktree disclosure;
- SDK from `global.json` and actual SDK used;
- test configuration and `DefineTestSupport` state;
- exact focused and regression test filters;
- exact scoped source-scan commands and classified hits;
- one named owner/path/reachability chain;
- migration/output-manifest package;
- restore/recomputed-state rules;
- debug/attestation visibility limits;
- VMX projection-after-neutral-result proof;
- compiler no-emission proof;
- Phase 18 nested exclusion proof;
- bounded rollback procedure.

## Bounded Rollback Procedure

If Phase 20 wording or policy regresses:

1. Set `SecurePositiveRuntimeExecutionActivationPolicy` to return `FutureGatedNoNamedPositivePath` for every candidate.
2. Remove any caller that treats admission, manifest, visibility, projection or compiler no-emission as execution proof.
3. Keep Phase 13 registry identifiers intact unless the regression is identifier-specific.
4. Preserve Phase 14/15/16 negative classifier boundaries, Phase 17 VMX zero-authority and Phase 19 compiler no-emission.
5. Keep Phase 18 nested design fence.
6. Re-run Phase 20 focused tests plus Phase 14/15/16/17/19 regression slices, release/conformance tests, VMX boundary tests, compiler no-emission tests and scoped scans.
7. Correct release wording to state that Phase 20 remains future-gated.

Rollback must not use repository-wide destructive reset and must not disturb unrelated runtime owners.

## Code And Test Anchors

- `SecurePositiveRuntimeExecutionActivationPolicy.cs`
- `SecurePositiveRuntimeExecutionActivationPhase20Tests.cs`
- `SecureHypercallBackendOwnerAbiRegistry.cs`
- `SecureHypercallBackendContractAdmissionPolicy.cs`
- `SecureCompletionRetirePublicationAuthorityPolicy.cs`
- `SecureOutputManifestClassificationPolicy.cs`
- `SecureDebugAttestationVisibilityPolicy.cs`
- `SecureComputeNamedPathVmxZeroAuthorityPolicy.cs`
- `SecureComputeControlledEmissionGatePolicy.cs`

## Applicable prerequisite chain

- reproducible audit baseline;
- canonical decode-owned operation taxonomy;
- single descriptor registry with opaque binding;
- SafetyVerifier-issued immutable admission certificate;
- end-to-end disabled equivalence;
- grant ledger;
- production certificate carrier through issue;
- hermetic test-only carrier profile and public decode-to-expected-deny slice;
- one side-effect-free named neutral backend probe before any memory/I/O/VMX/nested/compiler emission path;
- canonical region/shared-buffer maps before their respective effect domains;
- Phase 13 owner/service RFC only if a later secure hypercall path is selected;
- Phase 14 backend/completion/retire separation;
- closed Phase 15 output-manifest and restore classification;
- closed Phase 16 debug/attestation visibility classification;
- closed Phase 17 VMX zero-authority regression proof;
- closed Phase 19 explicit no-compiler-change decision, or a future separately approved controlled-emission RFC;
- Phase 18 nested exclusion remains in force;
- Phase 21 conformance matrix;
- separate offline named-path release-evidence verification; Phase 22 remains deny-only.

## Exit Criteria

Not satisfied for a positive path.

Satisfied only for current Phase 20 pre-activation classifier:

- current evidence classified;
- missing named owner/path/reachability gap explicit;
- prerequisite policy/negative gates retain their bounded classifications;
- Phase 18 remains future/design-fenced;
- product compiler secure emission remains closed;
- release remains independently evidence-gated while Phase 22 stays permanently deny-only;
- no production activation claim.

## Dependency

Previous: `19_compiler_no_emission_to_controlled_emission_gate.md`. Next: `21_conformance_negative_positive_test_matrix.md`.
