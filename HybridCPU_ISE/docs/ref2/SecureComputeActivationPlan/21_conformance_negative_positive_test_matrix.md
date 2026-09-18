# Conformance Negative Positive Test Matrix

## Phase Metadata

- File name: `21_conformance_negative_positive_test_matrix.md`
- Phase goal: define mandatory tests and static/source scans before any limited activation claim.
- ADR ID: `ADR-SC-CONFORMANCE-NEGATIVE-POSITIVE-MATRIX`
- Status: negative/future-gated conformance matrix only; no positive execution or release proof.
- Scope: negative tests, future positive tests and static/source scans.
- No-goals: no positive backend execution by test existence.

## Current Baseline

The repository already has `SecureComputeRefactoring` and `VmxRefactoring` tests for no-effect, fail-closed policy, Phase 09 privileged execution-state owner proof, Phase 10 field-specific read-only projection, Phase 11 secure memory/private-domain policy admission, Phase 12 secure I/O/shared-buffer policy admission, proof-only backend owner, admitted-denied hypercall, VMX denial, nested design fence, Phase 15 migration/output-manifest classification, migration/evidence and no-emission.

Phase 21 is closed only for the current negative/future-gated conformance matrix. `SecureComputePhase21ConformanceEvidencePolicy` packages current conformance evidence for release-gate consumption and keeps runtime execution, backend-result acceptance, completion publication, retire publication, VMX authority, compiler emission, nested execution and production release approval false.

## Authority Owner

Conformance gates verify authority; they do not own authority.

## Proof-Type Separation

| Proof type | Counts toward execution/release proof? | Current examples |
| --- | --- | --- |
| executable production-path behavior | yes, when it reaches the named production owner/path | none for positive SecureCompute execution |
| executable negative policy behavior | yes, for the bounded denial branch only | Phase 13/20/22 deny results, VMX zero-authority decisions, compiler no-emission decision |
| scoped structural/source guard | supporting evidence only | forbidden caller/dependency scans |
| documentation/source-string assertion | no | tests reading ActivationPlan/WhiteBook or checking source text tokens |
| caller-supplied `...Validated`/`...Proven` boolean | no | classifier request fields in Phases 20-22 |

Documentation checks must be reported separately and excluded from the conformance numerator. A green doc-string test cannot close an owner, reachability or effect-path row.

## What Can Be Implemented

- negative tests for every current denial and fail-closed branch;
- static/source scans over this activation corpus;
- product-claim scans;
- a typed conformance evidence classifier that release tests can consume without owning authority;
- future positive test skeletons that remain skipped or unmerged until owner-specific RFC/ADR approval.

## What Remains Denied/Future-Gated

- all positive backend execution tests;
- all completion/retire publication positive tests;
- all production activation claims from conformance evidence alone;
- VMREAD expansion beyond the two Phase 10 fields;
- hypercall backend success;
- nested execution;
- controlled compiler emission.

## Forbidden Shortcuts

- treating tests as runtime authority;
- treating golden artifacts as activation evidence;
- adding positive tests without production owner code;
- allowing forbidden terms outside denied/future contexts;
- weakening existing negative conformance to make a positive path pass.

## Required RFC/ADR

Future positive tests require the owner-specific RFC/ADR named by the path. Negative tests require no RFC/ADR.

## Required Tests

The mandatory test set is the union of `Required Negative Tests` and `Future Positive Tests After Owner-Specific RFC/ADR` below.

Every release-evidence run must record:

- repository revision and dirty-worktree disclosure;
- pinned SDK from `global.json` and actual `dotnet --info`/SDK used;
- configuration and `DefineTestSupport` state;
- exact test filters, counts and results;
- exact source-scan commands and scoped hit classification.

## Required Negative Tests

- ordinary no-effect: absent descriptor -> allowed;
- ordinary no-effect: disabled descriptor -> allowed;
- ordinary no-effect: unmaterialized descriptor -> allowed;
- `None` normalizes to `Disabled` or is proven equivalent;
- ordinary operations unchanged;
- non-ordinary fail-closed: absent descriptor -> denied by direct generic admission;
- non-ordinary fail-closed: disabled descriptor -> denied by direct generic admission;
- non-ordinary fail-closed: unmaterialized descriptor -> denied by direct generic admission;
- direct generic-service denial coverage is enum-wide across `EnterSecureDomain`, `TouchSecureMemory`, `CreateEvidence`, `PublishCompletion`, `PublishRetireSideEffect`, `SecureIo`, `SecureHypercall`, `SecureMigration`, `NestedSecureDomain` and `CompatibilityProjection`;
- policy deny reasons for missing/disabled/unmaterialized descriptors reach the generic result as `SecureDomainBoundaryDenied`; CPU Stage-B reachability remains an open C0 test requirement;
- incomplete secure descriptor -> denied/fail-closed;
- missing subdescriptor -> denied/fail-closed;
- stale epoch -> denied;
- missing grant -> denied;
- non-monotonic grant derivation -> denied;
- host-owned evidence publication -> denied;
- SecureCompute cannot be activated by VMX;
- `VmxCaps` cannot grant SecureCompute;
- VMCS cannot store secure state;
- VMREAD cannot read secure state without explicit neutral owner;
- VMWRITE cannot write secure state;
- `AllowedProofOnlyNoExecution` cannot execute backend;
- `AllowedAdmittedDenied` cannot publish completion/retire;
- proof-only backend owner admission cannot publish completion;
- proof-only backend owner admission cannot publish retire;
- registry-backed Phase 13 owner/service admission cannot publish completion or retire;
- generic trap-route publication flags cannot become SecureCompute publication authority without owner/path reachability;
- VMX projection-only paths cannot become SecureCompute completion/retire publication authority;
- named positive-looking SecureCompute paths cannot become VMX activation, `VmxCaps` grant, VMCS state store, active pointer identity, compatibility read/write authority, VMCS checkpoint authority, completion publication or retire publication;
- compatibility projection for a named SecureCompute path requires a neutral runtime result and remains projection-only;
- `SecureIoHypercallAdmissionPolicy` cannot authorize backend success in current baseline;
- `TrapDecision`/`VmExitReason` cannot be runtime authority;
- private memory descriptor cannot become hardware tag;
- measured descriptor cannot become activation evidence;
- secure-memory descriptor missing/unmaterialized/stale epoch -> denied;
- private host read -> denied;
- private DMA through I/O or hypercall argument path -> denied;
- shared DMA without explicit memory/I/O/shared-buffer policy, owner/lifetime/evidence binding, current shared-buffer grant epoch or typed grant -> denied;
- secure I/O or shared-buffer hypercall without a neutral I/O owner -> denied;
- shared-buffer hypercall with missing validated owner, wrong owner domain, stale lifetime, denied evidence or stale buffer-grant epoch -> denied;
- shared-buffer ID without current descriptor-owned binding cannot authorize admission;
- raw private pointer or forged opaque handle in a secure hypercall -> denied;
- publication-fence presence does not authorize backend execution, completion publication or retire publication;
- current secure hypercall backend-success request -> `DeniedBackendSuccessClosed`;
- measured memory admission does not satisfy private-domain activation;
- runtime-mutable memory without dirty and migration classification -> denied;
- sealed payload contract cannot become CHERI sealing/sealed capability;
- migration rejects host evidence, raw secrets, raw sealing keys, active pointers;
- restore requires validation/rebuild;
- output manifest rejects host evidence, scheduler evidence, backend binding evidence, native tokens, raw secrets, raw sealing keys, active pointers, VMCS metadata and compatibility metadata;
- internal backend result and internal completion record are manifest-only and create no runtime, completion publication or retire publication authority;
- output manifest requires request state, internal backend result, internal completion record, guest-visible output, retire-visible state and recomputed-after-restore state;
- recomputed-after-restore state requires restore validation proof;
- owner/path/reachability classification is required before output-manifest entry acceptance;
- debug/attestation cannot expose host-only evidence;
- debug trace cannot become migration payload, guest state, completion publication or retire publication;
- attestation output cannot become compatibility-read authority, activation evidence, completion publication or retire publication;
- telemetry cannot satisfy backend-owner proof;
- host-inspection metadata cannot bypass private-memory policy;
- compatibility-alias evidence requires projection policy and creates no read authority;
- Lane6/Lane7 native tokens cannot migrate as guest state;
- compiler no-emission prevents secure backend emission;
- no-compiler-change is the only allowed current SecureCompute compiler decision;
- secure backend helper, secure hypercall helper, sideband metadata and future controlled-emission requests are denied;
- controlled emission remains denied even with hypothetical prerequisite flags in Phase 19;
- CHERI/capability-aware LOAD/STORE/FETCH terms appear only in forbidden/future docs, not production implementation;
- nested child intent cannot execute backend or become mutable state.
- legacy tests that allow `EnterSecureDomain` with missing/disabled descriptors are removed or inverted before activation.
- privileged execution-state owner missing/unmaterialized descriptor -> denied;
- privileged execution-state owner domain/address-space mismatch -> denied;
- privileged execution-state owner stale epoch -> denied;
- privileged execution-state owner reserved or missing required bits -> denied;
- privileged execution-state owner host-owned/alias evidence -> denied;
- privileged execution-state owner without restore revalidation class -> denied;
- accepted privileged execution-state owner keeps projection, mutation, backend, completion and retire authority false;
- accepted privileged execution-state owner without Phase 10 projection inputs does not project `GuestCr0`/`GuestCr4`;
- missing Phase 10 value source, visibility, migration or conformance proof -> denied;
- any guest privileged/control field outside `GuestCr0`/`GuestCr4` -> denied;
- `GuestCr0`/`GuestCr4` VMWRITE remains denied.

## Implemented Phase 10 Positive Tests

- `GuestCr0` projects read-only after all owner/value/visibility/migration/conformance gates;
- `GuestCr4` projects read-only after all owner/value/visibility/migration/conformance gates;
- projection value comes from `PrivilegedExecutionStateDescriptor`, not a generic execution snapshot;
- projection authorizes no backend success, mutation, completion publication or retire publication;
- compatibility matrix allows only the two named guest privileged-control fields.

## Implemented Phase 11 Positive Policy-Admission Tests

- shared memory host inspection is allowed only for explicit shared regions;
- measurement access is allowed only for measured regions;
- runtime-mutable touch is allowed only with dirty and migration classification;
- shared DMA is allowed only after explicit memory policy, I/O shared-buffer binding, current grant epoch and typed capability grant;
- secure-memory admission is routed through Stage B only for non-ordinary secure operations and remains no-effect for ordinary operations.

## Implemented Phase 12 Positive Policy-Admission Tests

- secure shared DMA is admitted only through explicit shared-buffer policy plus neutral owner, memory binding, direction/range, current lifetime/evidence/buffer grant and typed capability grant;
- secure hypercall shared-buffer arguments are admitted only through the same current descriptor-owned binding plus a current typed argument grant;
- an allowed secure I/O policy result is explicitly `IsPolicyAdmissionOnly`;
- allowed secure I/O policy admission keeps backend execution, completion publication and retire publication false even when required fences are present;
- ID-only shared-buffer lookup is absent from the production authority path.

## Implemented Phase 14 Negative Publication Tests

- `SecureCompletionRetirePublicationAuthorityPolicy` denies completion and retire publication from `SecureBackendOwnerAdmissionResult.AllowedProofOnlyNoExecution`;
- `SecureCompletionRetirePublicationAuthorityPolicy` denies completion and retire publication from `SecureIoHypercallAdmissionResult.AllowedAdmittedDenied`;
- `SecureCompletionRetirePublicationAuthorityPolicy` denies completion and retire publication from registry-backed `SecureHypercallBackendContractAdmissionResult.AllowedProofOnlyNoExecution`;
- completion fence presence cannot cross the backend-result owner boundary;
- retire publication requires separate retire owner and explicit retire fence after completion publication;
- unsafe host-owned evidence and unclassified migration class block retire publication;
- generic `TrapCompletionRoutePolicy` publication flags are not SecureCompute authority without owner/path reachability;
- VMX compatibility projection remains zero-authority;
- migration payload classification excludes host-owned evidence, scheduler evidence, backend binding evidence, native tokens, raw secrets, active host pointers, VMCS metadata and compatibility metadata;
- compiler no-emission remains closed for new instruction encoding, capability-aware load/store/fetch and VMX secure-mode emission.

## Implemented Phase 15 Negative Classification Tests

- `SecureOutputManifestClassificationPolicy` rejects host-owned evidence, scheduler evidence, backend binding evidence, native token evidence, raw measurement secrets, raw sealing keys, active host pointers, VMCS projection metadata and compatibility projection metadata;
- internal backend result is manifest coverage only and is not backend success evidence;
- internal completion record is manifest coverage only and is not checkpoint or restore authority;
- missing one required manifest entry blocks future positive-path evidence;
- a complete manifest classification creates no runtime, completion publication or retire publication authority;
- recomputed-after-restore state requires restore validation proof;
- owner/path/reachability classification is required before a manifest entry is accepted;
- scoped source guards prove Phase 15 sources do not depend on VMX/VMCS/`VmxCaps`, VMREAD/VMWRITE, generic trap-route services, backend execution request/result types, publication true flags or compiler controlled-emission shortcuts.

## Implemented Phase 16 Negative Visibility Tests

- `SecureDebugAttestationVisibilityPolicy` classifies debug trace as debug-only visibility and creates no authority;
- debug trace cannot request migration payload, completion publication or retire publication;
- attestation report can become guest-visible only under evidence policy and never creates compatibility-read authority, activation evidence, completion publication or retire publication;
- host-owned, recomputed, missing and stale attestation evidence is denied;
- telemetry snapshot is host-only quarantined evidence and cannot satisfy backend-owner proof;
- host-inspection metadata cannot request private-memory inspection;
- compatibility-alias evidence requires explicit projection policy and creates no read authority;
- scoped source guards prove Phase 16 sources do not depend on VMX/VMCS/`VmxCaps`, compatibility read/write instructions, backend execution request/result types, publication true flags or compiler controlled-emission shortcuts.

## Implemented Phase 17 Negative VMX Zero-Authority Tests

- `SecureComputeNamedPathVmxZeroAuthorityPolicy` classifies Phase 10, Phase 13, Phase 14, Phase 15, Phase 16 and future Phase 20 positive-looking paths as VMX zero-authority;
- compatibility projection for a named path is denied without a neutral runtime result;
- compatibility projection after a neutral result remains projection-only and creates no VMX, completion or retire authority;
- VMX activation, `VmxCaps` grant, VMCS state store, active pointer identity, compatibility read/write authority and VMCS checkpoint authority shortcuts are denied;
- completion and retire publication shortcuts from compatibility projection are denied;
- existing VMX boundary matrix remains negative for backend success and mutation;
- scoped source guards prove Phase 17 sources do not depend on VMX runtime managers, field read/write helpers, backend execution request/result types, publication true flags or compiler controlled-emission shortcuts.

## Implemented Phase 19 Negative Compiler No-Emission Tests

- `SecureComputeControlledEmissionGatePolicy` allows only `NoCompilerChange` with no compiler emission requested;
- secure backend helper, secure hypercall helper, secure sideband metadata and future controlled-emission requests are denied before any positive runtime owner;
- controlled-emission request remains denied even with positive runtime owner, compiler RFC, release approval and backend execution flags;
- new instruction encoding, new operand format, capability-aware memory instruction and VMX secure-mode emission attempts are denied through `SecureComputeNoEmissionContract`;
- selected compiler API/IR source scans prove no SecureCompute emit/helper shortcut, backend execution flag, completion publication flag, retire publication flag, capability-aware instruction or tagged-memory shortcut.

Future hermetic test-only carrier tests form a separate conformance slice: they must prove production artifact exclusion and public canonical decode reachability to an expected denial. They do not enter the positive execution or release-proof numerator and do not change the current product no-emission decision.

## Implemented Phase 20 Pre-Activation Classifier Tests

- `SecurePositiveRuntimeExecutionActivationPolicy` classifies current evidence-only candidates as future-gated and creates no runtime, backend-result, completion, retire, VMX, compiler, nested or release authority;
- Phase 13 registry-backed admission cannot become runtime execution, completion publication or retire publication;
- proof-only backend-owner admission and admitted-denied secure hypercall recognition cannot execute, publish completion or retire;
- Phase 14 publication vocabulary, including a hypothetical completion-publication result, is not Phase 20 owner/path/reachability proof;
- Phase 15 manifest coverage is required, but complete manifest classification remains manifest-only and is not execution proof;
- Phase 16 debug/attestation visibility cannot become runtime evidence;
- Phase 17 compatibility projection remains zero-authority and cannot become runtime execution;
- Phase 18 nested child intent remains future/design-fenced and cannot become runtime execution;
- Phase 19 no-compiler-change is required but is not runtime execution proof;
- future named-path preconditions fail closed until exact runtime authority owner, backend-result owner boundary, owner/path/reachability proof, neutral backend result, migration/output-manifest coverage, restore rules, nested exclusion, product compiler no-emission decision and separately verified offline release evidence exist;
- scoped source guards prove Phase 20 sources do not create backend execution, completion publication, retire publication, VMX, compiler emission, nested execution or production release authority.

## Implemented Phase 21 Conformance Evidence Gate Tests

- `SecureComputePhase21ConformanceEvidencePolicy` accepts the current matrix only as a future-gated conformance evidence package;
- Phase 13 registry-backed admission, Phase 14 completion/retire fail-closed checks, Phase 15 manifest coverage, Phase 16 visibility denial, Phase 17 VMX zero-authority, Phase 18 nested design fence, Phase 19 no-compiler-change and Phase 20 future-gated runtime evidence are all required before the matrix can be consumed;
- production activation, compiler secure emission, VMX-owned SecureCompute authority, Phase 18 nested execution, manifest-only execution proof and visibility-only runtime evidence overclaims are denied;
- future positive-path evidence can be packaged for a separate offline release verifier only and still creates no runtime, publication, VMX, compiler, nested or release authority;
- scoped source guards prove Phase 21 sources do not create backend execution, completion publication, retire publication, VMX authority, compiler emission, nested execution or production release authority.

## Implemented Phase 22 Fail-Closed Limited Release Gate Tests

- `SecureComputePhase22LimitedReleaseGatePolicy` rejects the current Phase 21 matrix as release evidence because no named positive runtime owner/path/reachability chain is locally proven;
- Phase 22 requires Phase 21 matrix consumption, owner-specific RFC/ADR, production owner code, owner/path/reachability proof, typed request/result model, backend-result owner boundary, completion/retire policy, migration/output-manifest and restore evidence, debug/attestation limits, VMX zero-authority, Phase 19 no-compiler-change, Phase 18 nested exclusion, scoped product wording and bounded rollback;
- a fully populated hypothetical future evidence package still creates no release authority in the current code and returns `DeniedPhase22ManualApprovalNotImplemented`;
- scoped source guards prove Phase 22 sources do not create backend execution, completion publication, retire publication, VMX authority, compiler emission, nested execution or production release authority.
- Phase 22 remains permanently deny-only; future offline release-verifier tests are a separate evidence class and cannot create an allow branch or runtime input here.

## Future Positive Tests After Owner-Specific RFC/ADR

- secure descriptor materializes;
- secure policy admission succeeds for explicitly allowed no-side-effect operation;
- typed secure grant is recognized by runtime admission;
- evidence policy exposes only allowed guest-visible evidence;
- typed backend execution request/result works only after Phase 20 implementation;
- completion fence allows publication only after backend success;
- retire publication allowed only after explicit retire rule;
- migration class is explicit and excludes host evidence;
- VMX/VMCS/`VmxCaps` remain denied even when secure path is positive.

## Required Static/Source Scans

- forbidden VMX/VMCS/`VmxCaps` authority markers;
- Stage B admission bypass on non-ordinary secure operation classes;
- `secureDescriptor is { IsEnabled: true }` guards that skip fail-closed secure admission for non-ordinary operations;
- runtime tests and source-scan proof that old allowed-by-absence/disabled expectations have been inverted or removed before activation across the non-ordinary taxonomy;
- secure VMCS / VMREAD secure-state authority;
- `AllowedProofOnlyNoExecution` overclaim;
- `AllowedAdmittedDenied` overclaim;
- production-ready / feature-complete overclaim;
- CHERI ISA / tagged memory / capability-aware LOAD/STORE/FETCH accidental implementation;
- hardware tag terminology outside docs/future/deny contexts;
- `RuntimeOwnedPublication` misuse;
- completion/retire publication without fence;
- nested execution wording;
- Stream/Lane authority leakage;
- host evidence in migration/checkpoint;
- compiler no-emission bypass.
- privileged execution-state owner source dependency on compatibility state stores, virtualization execution units or scalar field fallback;
- category-wide privileged/control-field projection;
- generic execution-state epoch/value fallback for `GuestCr0`/`GuestCr4`;
- Phase 10 projection with any backend-success, mutation, completion or retire true flag.
- Phase 11 secure-memory policy source dependency on VMCS, VMREAD/VMWRITE, `VmxCaps`, hardware tags, CHERI or capability-aware LOAD/STORE/FETCH.
- Phase 11 measured-memory wording as production activation evidence.
- Phase 12 ID-only shared-buffer authority such as `AllowsSharedBuffer(bufferId)`.
- Phase 12 shared-buffer admission without current owner, lifetime, evidence and buffer-grant epoch validation.
- Phase 12 propagation of publication-fence flags into allowed I/O completion or retire authority.
- Phase 12 secure I/O or hypercall dependency on VMX, VMCS, `VmxCaps`, VMCALL authority, raw host/device pointers or Lane6/Lane7 native-token authority.
- Phase 13 confusion between VMCALL opcode, decoded leaf, SecureCompute service ID and backend owner ID; tests must consume `SecureHypercallBackendOwnerAbiRegistry` symbols, not numeric fixtures.
- Phase 13 unresolved contract, transport-opcode mismatch, unknown service, decoded-leaf mismatch, unsupported version, missing/wrong/stale owner, missing/stale grant or evidence, invalid shared buffer, raw pointer, invalid opaque handle, replay violation and pre-execution cancellation.
- Phase 13 proof-only acceptance with backend execution, completion publication and retire publication all false.
- Phase 14 `SecureCompletionRetirePublicationAuthorityPolicy` proof-only, admitted-denied, registry-backed Phase 13, generic route flag and VMX projection-only denial branches.
- Phase 14 source scan must prove `SecureCompletionRetirePublicationAuthorityPolicy` does not call `TrapCompletionRouteService`, `TrapCompletionRouteDescriptor` or `TrapCompletionPublicationFence`.
- repository-wide publication-flag scans that produce false authority conclusions without owner/path reachability analysis.
- Phase 15 `SecureOutputManifestClassificationPolicy` forbidden-payload denial branches and complete-manifest classification branch.
- Phase 15 source scan must prove `SecureOutputManifestClassificationPolicy` does not call VMX/VMCS/`VmxCaps`, VMREAD/VMWRITE, `TrapCompletionRouteService`, `TrapCompletionRouteDescriptor`, backend execution request/result types or compiler controlled-emission shortcuts.
- Phase 16 `SecureDebugAttestationVisibilityPolicy` denied shortcut branches for migration payload, compatibility-read value source, activation evidence, backend-owner proof, private-memory inspection, completion publication and retire publication.
- Phase 16 source scan must prove `SecureDebugAttestationVisibilityPolicy` does not call VMX/VMCS/`VmxCaps`, compatibility read/write instruction paths, backend execution request/result types, publication true flags or compiler controlled-emission shortcuts.
- Phase 17 `SecureComputeNamedPathVmxZeroAuthorityPolicy` denied shortcut branches for VMX activation, `VmxCaps` grant, VMCS state store, active pointer identity, compatibility read/write authority, VMCS checkpoint authority, completion publication and retire publication.
- Phase 17 source scan must prove `SecureComputeNamedPathVmxZeroAuthorityPolicy` does not call VMX runtime managers, field read/write helpers, backend execution request/result types, publication true flags or compiler controlled-emission shortcuts.
- Phase 19 `SecureComputeControlledEmissionGatePolicy` denied branches for secure backend helper, secure hypercall helper, sideband metadata, future controlled emission, new instruction encoding, new operand format, capability-aware memory instructions and VMX secure-mode emission.
- Phase 19 source scan must prove selected compiler API/IR surfaces do not expose SecureCompute emit/helper shortcuts, backend execution flags, publication flags, capability-aware instruction shortcuts or tagged-memory shortcuts.
- Phase 20 `SecurePositiveRuntimeExecutionActivationPolicy` denied branches for proof-only owner admission, admitted-denied secure hypercall admission, registry-backed Phase 13 proof-only admission, Phase 14 publication vocabulary, Phase 15 manifest-only evidence, Phase 16 visibility-only evidence, Phase 17 zero-authority projection, Phase 18 nested design fence, Phase 19 no-compiler-change-only evidence and missing future named-path owner/path/reachability proof.
- Phase 20 source scan must prove all current runtime execution, backend-result, completion publication, retire publication, VMX authority, compiler emission, nested execution and production release authority bits remain false.
- Phase 21 `SecureComputePhase21ConformanceEvidencePolicy` source scan must prove current matrix evidence, future positive path evidence and release-input wording do not create runtime execution, backend-result, completion publication, retire publication, VMX authority, compiler emission, nested execution or production release authority.
- Phase 22 `SecureComputePhase22LimitedReleaseGatePolicy` source scan must prove release evidence classification does not create runtime execution, backend-result, completion publication, retire publication, VMX authority, compiler emission, nested execution or production release authority while no named positive runtime path is locally proven.

## Code Anchors

- `HybridCPU_ISE.Tests/SecureComputeRefactoring/**`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureCompute*.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureCompletionRetirePublicationAuthorityPhase14Tests.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureMigrationCheckpointRestoreOutputManifestPhase15Tests.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureDebugAttestationVisibilityPhase16Tests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase17NamedPositivePathZeroAuthorityTests.cs`
- `HybridCPU_ISE.Tests/CompilerTests/SecureComputeCompilerPhase19ControlledEmissionGateTests.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecurePositiveRuntimeExecutionActivationPhase20Tests.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureComputePhase21ConformanceMatrixTests.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureComputePhase22LimitedReleaseGateTests.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Conformance/ReleaseGate/SecureComputePhase21ConformanceEvidencePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Conformance/ReleaseGate/SecureComputePhase22LimitedReleaseGatePolicy.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxGuestControlRegisterOwnerDecisionTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxTrapCompletionRouteRetirePublicationHardeningTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxCompilerIsaRuntimeNoEmissionContractTests.cs`

## Documentation Anchors

- all files in this directory;
- `SecureComputerefactoringNew/21_release_gate_and_activation_checklist.md`;
- `VirtualiztionRefactoringNew/13_conformance_golden_artifacts_and_static_gates.md`.

## Migration/Evidence Classification

Tests must assert classification, not merely object presence. Host-owned and recomputed evidence remain non-authoritative. Phase 15 manifest tests classify future request/result/completion/guest-output/retire/recomputed entries without creating runtime or publication authority.

## Completion/Retire Implications

Tests must prove every current path is below completion publication and retire publication unless a future RFC implements otherwise.

## SecureCompute Activation Implications

Passing existing negative tests alone is not activation. Limited activation needs one future named production path, executable positive and negative tests, independent release evidence, and all source/doc guards reported separately.

## Exit Criteria

- current negative policy/classifier matrix maintained, with executable tests reported separately from source/document guards;
- positive matrix ready but gated behind owner-specific RFC/ADR and Phase 22 release review;
- static/source scans documented and enforceable;
- release gate may consume the matrix only as negative input, never as execution or release proof.

## Dependency

Previous: `20_positive_secure_runtime_execution_activation_plan.md`. Next: `22_limited_securecompute_release_gate.md`.
