# Phase 16 - Conformance Negative Positive Test Matrix

Status: conformance plan. PR-A through PR-J are committed. PR-J lifecycle and exact activation/rollback tests passed on the committed subject and are bound by a later development-local evidence record; compiler stays closed by default.

## 2026-08-11 PR-J Development-Local Concurrency And Rollback Matrix

`VmxPrJLifecycleConcurrencyTests` executes the required real races: drain vs E2->E3, E3->E5 and E5->E6; restore vs new E2; cancel vs E3 publication and E5->E6; and two independent domains draining concurrently. Test-only barriers pause inside the real owner handoffs, while the Release build contains no barrier hook behavior. Every transition removes its source authority and publishes its target under one per-domain transition lease; the transition count is part of E7 quiescence.

`VmxPrJExactActivationRollbackTests` proves default-disabled state, rejection of a non-exact leaf, exact domain/capability provisioning, and kill-switch order `close new E2 -> transition zero -> E2/E3/E5/E6 zero -> revoke exact binding/grant -> deterministic fault-only fallback`. `prj-exact-release-activation-rollback` emits structured counters and trace from those real contracts and remains diagnostic evidence rather than runtime authority.

## 2026-08-10 PR-I E7 Addendum

Focused tests cover pre-operation checkpoint, live E2/E3/E5/E6 checkpoint
denial, cancel-to-zero, stale authority after restore, wrong SpecDigest,
duplicate restore, policy-only serialization and repeatable no-state traces.
FSP/SMT scheduling variants compare architectural results rather than cycles or
physical placement. Diagnostics execute the real lifecycle contracts and retain
zero register, memory and VM-state writes.

## 2026-08-10 PR-H E6 Addendum

Focused coverage now proves canonical head success, real WB retire-window
integration, opaque E6, E5/E6 consume-once separation, and denials for non-head,
wrong slot/identity, squash, duplicate/foreign owner and stale restore generation.
The positive test has zero architectural writes and leaves compatibility retire
faulted. Missing completion or retire-owner binding preserves the PR-F/PR-G
rollback contours.

## 2026-08-10 PR-G E5 Addendum

Focused coverage now requires atomic record+E5 success from one live E3 and
denials for missing completion owner, duplicate E3 consumption, forged/direct
token construction, restore invalidation, caller-boolean authority and any
compatibility or retire consumer. Positive E5 remains host-only test/diagnostic
evidence and cannot satisfy E6.

## 2026-08-09 PR-F E4 Closure Addendum

`VmxPrFCanonicalHypercallCompositionTests` covers the real scheduler and execute
seams: default/no-binding rollback, exact E1/operand/E2 preparation, execute-stage
E3, zero/adjacent/high-bit denial, replay invalidation, disable-before-execute
revocation and private carrier-bound dispatch. The distinct `InvokeHypercall`
operation is runtime-service/no-state/non-projection and carries no mutation bit.

`prf-canonical-hypercall-composition` executes those real contracts and reports
canonical E4/E3 counters alongside zero compatibility-direct, completion and
retire counters. Static guards require exactly one neutral production call site
to the exact executor and none below the compatibility frontend. VMX retire
continues to return `SecurityPolicyViolation` until later E5/E6 pools close.

## 2026-08-09 PR-E E3 Closure Addendum

`VmxPrEExactProbeExecutorTests` executes the real isolated executor contract.
The default mode denies without consuming E2. The explicitly enabled test mode
accepts only the exact Phase-38 binding, atomically consumes one live E2 and
returns one private-constructor E3 bound to E2 digest, attempt, D2/owner policy,
operation/leaf, execution sequence and restore generation. Canonical non-zero
no-effect/no-result digests bind the no-state/no-payload result.

The negative matrix denies forged, stale, revoked and adjacent-leaf E2,
duplicate and concurrent execution, foreign receipt issuers and restore-stale
receipts. `pre-exact-probe-executor-no-publication` exercises the same real
contracts and reports zero composition, completion and retire publications.
There is no `InvokeHypercall`, compatibility callback, production dispatcher
composition, `CompletionRecord`, E5 or E6 in PR-E. The PR-D fault-only rollback
remains available by leaving the executor disabled.

## 2026-08-09 PR-D E2 Closure Addendum

`VmxPrDProductionE2AdmissionTests` executes the actual v2 E2 issuance and
validation contour. It binds the exact accepted D2/O1 instance, E1 attempt and
issuer generation, canonical immutable operand digest, VT/context/domain,
bundle/replay, exact leaf, HCOWNR policy, a neutral generation-bearing typed
grant lease, non-mutating runtime-root epoch and the live restore-generation
owner. SafetyVerifier itself invokes `RuntimeBoundaryAdmissionService` with
`ExecutionOnly`, `NoStateExecution`, `DomainGranted` bit 41 and no evidence or
address-space identity.

The negative matrix denies missing inputs, duplicate issuance, foreign domain,
non-zero address-space identity, forged/revoked/stale capability leases, wrong
grant policies, frontend or mutation-capable roots, SecureCompute, restore
generation changes and explicit E2 revocation. The historical Phase-34 boolean
request remains always denied. E2 has a private constructor and an issuer-owned
live registry. There is no consume/executor API in PR-D, and VMCALL still emits
the existing `SecurityPolicyViolation` fault.

## 2026-08-09 PR-A D2 V2 Closure

`VmxD2V2GovernanceNegativeSubstrateTests` executes the real immutable
`VirtualizationDecisionSpecV2`, `VirtualizationDecisionAcceptanceRecordV2`,
append-only revocation/supersession records, versioned binary canonical encoder
and fail-closed validator. It covers wrong/malformed digest and SHA, a
self-referential spec SHA, wrong DecisionId, noncanonical/exact-commit bytes,
zero/wrong owner, zero/duplicate/adjacent/cross-namespace leaves, ABI mismatch,
missing/unknown/wrong policy, incomplete owner map, missing CODEOWNERS,
review-role mismatch, compatibility-only review, inactive acceptance states and
invalid/effective lineage.

The only positive fixture is test-local structural validation. Its result is an
immutable `AcceptedVirtualizationDecision` policy object with capability,
backend, completion and retire authority all false. No populated production
acceptance record, accepted-operation registry or completed review evidence was
added. `pra-d2-governance-negative` exercises only real negative contracts and
reports zero accepted/runtime-authority objects. This closes PR-A conformance;
it did not by itself prove an attributable accepted instance or open PR-B. The
later explicitly authorized PR-B uses committed bytes and real repository
attribution; that positive result remains policy-only evidence.

## 2026-06-11 Audit Contract

- File name: `16_conformance_negative_positive_test_matrix.md`.
- Purpose: define negative guards required now and future positive tests allowed only after owner-specific RFC/ADR.
- Status: conformance plan; positive tests are `future-gated`.
- Scope: VMCS/store/pointer absence, VmxCaps non-authority, VMREAD/VMWRITE denials, VMCALL missing owner, publication/retire separation, nested/SecureCompute/lane/compiler/migration guards.
- No-goals: no runtime authority from tests, goldens, snapshots, or green suites; no positive tests without RFC/ADR owner map.
- Code anchors: `HybridCPU_ISE.Tests/VmxRefactoring/**`, `HybridCPU_ISE.Tests/SecureComputeRefactoring/**`, `HybridCPU_ISE.Tests/CompilerTests/**`, `CompilerSourceScanner.cs`, `VirtualizationActivationPlanAuditGuardTests.cs`, runtime anchors from Phases 01-15.
- Authority owner: conformance detects and blocks; neutral runtime owners grant authority only after implementation.
- Required RFC/ADR: every positive test must cite accepted RFC/ADR with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: negative matrix is runnable now; positive matrix remains disabled/not merged until owner implementation exists; proof-only/admitted-denied are not backend success.
- Tests/static scans: required test groups and scans listed below, plus plan-doc audit metadata guards.
- Risks: broad green test suite being cited as activation proof, or positive tests landing without adjacent denials.
- Next-gate dependency: Phase 17 rollout sequencing and Phase 18 release gate.

## Phase Goal

Define the negative tests that must exist before any activation work and the positive tests that may be enabled only after a complete owner-specific RFC/ADR.

## Historical Baseline (2026-06-11)

Existing VMX refactoring tests cover many denial and projection boundaries, including VMREAD slices, VMCALL admitted-denied path, hypercall backend admission denial, trap route/fence denial, SecureCompute VMX boundary, compiler no-emission, migration/evidence, and closure documentation.

The current positive VMREAD surface includes only field-specific guarded `GuestCr0`/`GuestCr4` read-only projection. Its tests must prove owner/source/visibility/migration/conformance gates and simultaneously prove no mutation, backend success, completion publication, or retire publication.

The phrase "positive VMREAD surface" means a positive direct projection fixture only. It does not mean an architectural VMREAD instruction, dispatcher success, destination-register writeback, completion publication, or retire publication.

Current tests must also validate the evidence manifest and enforce the Phase 17 E0-E7/D2/O1 dependency model plus separate compiler/release gates. A guard that merely finds expected prose is claim hygiene, not runtime conformance.

## 2026-08-06 Verification Snapshot

- Focused plan/projection/backend-denial/route-retire set: `82/82` passed on `net10.0` with .NET SDK `10.0.204`.
- Entire `HybridCPU_ISE.Tests.VmxRefactoring` namespace: `137/137` passed.
- Broad `FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx` diagnostic run: `474` passed and `1` failed. The failure is `SecureComputePhase22LimitedReleaseGateTests.Docs_RecordFailClosedReleaseGateAndPhase18CompilerVmxBoundaries`, caused by a pre-existing missing phrase in the separately modified SecureCompute activation document; it is outside the files changed by this virtualization-plan audit and is not hidden as a green result.
- Evidence-manifest anchor hashes: verified.
- `git diff --check` for changed plan/WhiteBook/internal-doc/guard files: passed; only existing line-ending conversion warnings were reported.

These results prove only the tested denial/projection/document contracts in the dirty worktree. They are not clean-checkout CI, positive canonical execution, activation, completion or retire evidence.

## 2026-08-07 VRT Reconciliation Verification

- `VirtualizationActivationPlanAuditGuardTests`: `43/43` passed.
- Entire `HybridCPU_ISE.Tests.VmxRefactoring` namespace: `151/151` passed after reconciling archived-plan readers with the user-owned `docs/ref2/Old/VirtualiztionRefactoringNew` location.
- Production scans returned zero matches for the VRT candidate owner/probe/illustrative number, `BackendExecutionAuthorized: true`, `HypercallBackendAdmissionDecision.Allowed`, positive VMX frontend route/completion construction, direct compatibility service calls from execution/pipeline, and compatibility completion factories from execution/pipeline/runtime.
- Generated VMX projection lineage verification passed during build; evidence JSON parses successfully; `git diff --check` reported no whitespace errors (line-ending warnings only).

The PR-C verification was later contained by commit `cf465ca8000b5ccd4b7d1ea1f0c259357dcdd979`. PR-D now issues an operation-specific E2 admission certificate in production-compiled code, but still does not execute a backend, publish completion/retire or approve release. Machine D2 acceptance remains attributable to the committed PR-B artifacts, not to runtime tests.

## ISE-CONFORMANCE-GATES-16 - Closure Record

Closure date: 2026-06-18.

State: closed `READINESS-ONLY / NEGATIVE-MATRIX-CLOSED / POSITIVE-FIXTURES-FUTURE-GATED`.

This closure records the current conformance matrix only. It does not accept an owner RFC/ADR, does not enable positive backend execution tests, does not convert a green suite, generated parity fixture, golden artifact, static scan, or positive-looking fixture into activation proof, and does not open backend execution, completion publication, retire publication, mutation, migration payload authority, SecureCompute activation, lane/stream passthrough, nested execution, or compiler emission.

| Conformance surface | Current result | Authority owner | Evidence class | Migration/evidence requirement | Boundary |
| --- | --- | --- | --- | --- | --- |
| negative VMX/frontend scans | runnable now | none; conformance detects only | static denial evidence | not migration authority | must keep `MissingNeutralOwner`, `ProjectionOnlyDenied`, no `Allowed`, no `BackendExecutionAuthorized: true`, no frontend runtime publication |
| guarded `GuestCr0`/`GuestCr4` positive projection tests | current positive surface only | privileged execution-state owner | read-only projection evidence | `RevalidatedAfterRestore` plus conformance proof | field-local projection only; no mutation, backend, completion, retire, or widening |
| hypercall positive-looking fixtures | future-gated | neutral hypercall backend owner after machine-accepted D2 | readiness or proof-only evidence | `DrainOnly` operation and `HostOwnedNonMigratable` completion | architecture leaf/policies and immutable O1/operand identity are fixed, but no VMCALL backend success test may merge without the live E2-E7 chain |
| SecureCompute proof/conformance fixtures | denial/proof-only | SecureCompute runtime owners | proof-only or denial evidence | secure migration/evidence policy required | `AllowedSecureOperation` and `AllowedProofOnlyNoExecution` are not backend execution or publication |
| generated parity and golden artifacts | conformance evidence only | generated-schema/runtime owners when separately accepted | parity/golden evidence | not payload authority | generated or golden evidence is not runtime authority and cannot substitute owner acceptance |
| compiler no-emission tests | denied/future-gated | compiler RFC plus accepted runtime owner | no-emission evidence | not migration authority | no VMX/SecureCompute/lane emission authority |
| migration/evidence tests | denied/future-gated | neutral migration/checkpoint owner | payload-denial evidence | explicit class required | VMCS projection metadata, compiler artifacts, proof-only, lane/stream evidence, and VMREAD output remain non-authority |
| future positive tests | not merged/not enabled by this phase | owner-specific RFC/ADR only | implementation evidence after owner acceptance | exact migration/evidence class per path | must separately assert backend execution, completion route, completion fence, retire rule, and adjacent denials |

Closure invariants:

- Conformance tests, fixtures, generated parity, static scans, and goldens are evidence about boundaries; they are not authority.
- A broad green suite is not activation approval and cannot satisfy owner acceptance.
- Positive-looking tests or fixture names are not exact leaf IDs, owner maps, backend execution authorization, completion publication, or retire publication.
- The only current positive projection remains guarded `GuestCr0`/`GuestCr4` read-only projection, with no widening.
- Every future positive test must cite an accepted owner-specific RFC/ADR with exact operation, owner, value source, capability policy, evidence class, migration class, completion policy, retire policy, denial reasons, and adjacent negative tests.
- Proof-only, admitted-denied, manifest-only, visibility-only, no-emission, and readiness-only results must remain non-execution semantics in tests and documentation.

## Owner Of Authority

Conformance proves boundaries. It does not grant authority. Runtime authority remains with neutral owners.

## What Can Be Implemented

Negative tests immediately:

- VMCS store absent.
- Active VMCS pointer absent.
- `VmxCaps` cannot grant authority.
- VMREAD denied fields remain denied.
- `GuestCr0`/`GuestCr4` deny on missing owner/source/visibility/migration/conformance facts and allow only field-specific read-only projection after all gates.
- VMWRITE denied for all fields.
- VMCALL missing neutral owner remains denied.
- `RuntimeOwnedPublication` cannot be used by VMX frontend before backend owner.
- Completion cannot publish without fence.
- Retire cannot publish without explicit retire permission.
- Shadow VMCS cannot own nested state.
- SecureCompute cannot be activated through VMX/VMCS/`VmxCaps`.
- `AllowedSecureOperation` remains admission-only and `AllowedProofOnlyNoExecution` remains proof-only with backend execution false.
- exact VMCALL leaf is architecture-decided but not present in a machine-validated registry; documentation or a candidate class cannot enable a positive backend test.
- VRT candidate owner/name/illustrative numeric leaf cannot appear in production code, generated runtime registry or positive fixture before D2 acceptance.
- E1 certificate remains generic fault-only; absent/draft/withdrawn D2, register selector substituted for runtime leaf value, adjacent leaf, operand mutation, revoked capability and stale restore generation must deny E2 issuance/validation.
- PR-C conformance must prove exact accepted-D2-only O1 loading, execution-only common legality without mutation privilege, one-time full-value operand capture at the live E1 seam, adjacent/high-bit denial, stale restore/carrier denial and unchanged fault-only VMX execution. Its positive fixture is structural identity evidence, never E2 or backend authority.
- a D2 manifest, accepted-looking enum or public boolean cannot be consumed as a runtime authorization token.
- E3 backend receipt, E5 completion token and E6 retire grant must be opaque, owner/attempt-bound and mutually non-substitutable before any future positive test can merge.
- Lane6/Lane7 tokens cannot migrate as guest state.
- Tests/golden artifacts cannot be runtime authority.

Future positive tests:

- neutral owner admits exact operation;
- capability and evidence policy required;
- backend execution authorized only by owner;
- completion route authorized only after backend success;
- fence creates completion only when route permits;
- retire policy permits only exact approved path;
- migration class explicit;
- adjacent denied states remain denied;
- rollback and host-evidence non-leak pass.

The exact first-slice matrix must also deny wrong/noncanonical D2 digests and SHA, self-reference, wrong DecisionId, zero/duplicate/adjacent/cross-namespace leaf, zero/wrong owner, missing policy/CODEOWNERS, reviewer-role mismatch, compatibility-only review, revoked/superseded lineage and capability collisions. Runtime negatives cover fabricated `Allowed`, missing/stale grant generation, full-domain or mutation-privilege overreach, duplicate E2 consume/E3 execution, direct executor access, record/E5 non-atomicity, duplicate E5/E6, squash at each lifecycle boundary, stale restore generation and every compatibility/compiler/VMCS/lane shortcut.

The determinism matrix covers FSP off/on, SMT 1-way/4-way and legal schedule variation, replay off/on/invalidation, checkpoint before/after retirement, cancel before E3 and squash between E3-E5/E5-E6. Architectural traces compare retired/fault sequences, writes, PC/domain transitions and completion/retire multiplicity, never cycles or physical scheduling details.

## What Remains Denied/Future-Gated

All positive tests remain disabled/not merged unless their owner-specific RFC/ADR is accepted and the implementation exists.

## Forbidden Shortcuts

- Skipping negative tests because the positive path is narrow.
- Treating broad green test suite as activation proof.
- Using snapshots/goldens as owner evidence.
- Enabling positive tests before owner map is complete.

## Required RFC/ADR

Negative tests require no RFC/ADR. Every positive test must cite a specific RFC/ADR ID and owner map.

## Code Anchors

- `HybridCPU_ISE.Tests/VmxRefactoring/**`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/**`
- `HybridCPU_ISE.Tests/CompilerTests/**`
- `HybridCPU_ISE.Tests/TestHelpers/CompilerSourceScanner.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VirtualizationActivationPlanAuditGuardTests.cs`
- Runtime and virtualization anchors from Phases 01-15.

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/13_conformance_golden_artifacts_and_static_gates.md`
- `Documentation/Virtualization WhiteBook/14_Conformance_Golden_Artifacts.md`
- `Documentation/Virtualization WhiteBook/19_Source_References_And_Check_Commands.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`

## Required Tests

Suggested new test groups:

- `VirtualizationActivationPlanBaselineTests`
- `VirtualizationActivationPlanAuditGuardTests`
- `VmxRuntimeOwnedPublicationMisuseStaticTests`
- `VmxNoVmcsAuthorityRegressionTests`
- `VmxSecureComputeAuthorityAbsenceTests`
- `VmxLaneStreamEvidenceNonAuthorityTests`
- `VmxHypercallOwnerPositivePathTests` only after Phase 06/07 implementation.
- `GuestCr0Cr4ReadOnlyProjectionTests`
- `PrivilegedExecutionStateOwnerPolicyTests`
- `SecureBackendOwnerRfcGateTests`
- `SecureComputeDomainDescriptorNoEffectTests`

## Required Static/Source Scans

```powershell
rg -n "VmcsManager|IVmcsManager|VmxExecutionUnit|ActiveVmcs|VmcsFieldStore" HybridCPU_ISE --glob "*.cs"
rg -n "RuntimeOwnedPublication" HybridCPU_ISE/CloseToHSL/Core/Virtualization HybridCPU_ISE/CloseToHSL/Core/Runtime
rg -n "VmxCaps.*grant|VMCS.*authority|SecureCompute.*VMX|Lane6|Lane7|Stream.*authority" HybridCPU_ISE Documentation
rg -n "AllowedSecureOperation|AllowedProofOnlyNoExecution|BackendExecutionAuthorized:\s*true" HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute HybridCPU_ISE/CloseToHSL/Core/Virtualization
rg -n "HCPU_HV_PROBE_V1|0x48594350_00000001|DomainHypercallRuntimeOwner" HybridCPU_ISE/CloseToHSL --glob "*.cs"
rg -n "FromCompatibilityExit\(|TryFromCompatibilityExit\(" HybridCPU_ISE/CloseToHSL/Core/Execution HybridCPU_ISE/CloseToHSL/Core/Pipeline HybridCPU_ISE/CloseToHSL/Core/Runtime
rg -n "<release-policy-overclaim-denylist>" HybridCPU_ISE/docs Documentation
```

## Migration/Evidence Classification

Every positive test must assert migration/evidence class. Every negative test must assert that host-owned evidence, debug traces, scheduler evidence, native tokens, backend handles, VMCS projection metadata, and tests/goldens are not runtime authority. Every plan-doc guard test in this directory must assert that proof-only and admitted-denied semantics do not become backend success wording.

## Completion/Retire Implications

Positive tests must separately assert backend execution, completion route, completion fence, and retire rule. A single success assertion is insufficient.

## Exit Criteria

- Negative matrix is implementable immediately.
- Positive matrix is clearly gated by RFC/ADR.
- Static scans have expected outcomes and allowlist discipline.

## Dependency On Previous/Next Phase

Depends on all previous phases. Phase 17 orders the PRs; Phase 18 defines release gate.

## 2026-08-07 D2/E2 Negative Gate Addition

`VmxD2DecisionAndE2NegativeSubstrateTests` now denies non-accepted decisions, missing/mismatched attribution, absent CODEOWNERS proof, compatibility self-approval and absent exact-leaf cardinality. It also proves that the disabled owner interface has no execution method, the E2 certificate has no public constructor or issuer, schema data contains no appointed owner/operation/leaf, the VMX frontend still supplies `MissingNeutralOwner` and `ProjectionOnlyDenied`, and no backend/completion/retire shortcut was introduced. These are negative readiness checks only and do not satisfy D2.

Phase 35 additionally denies malformed accepted-commit SHA, required-reviewer mismatch, duplicate leaves and incomplete owner map. It proves the repository-owner review workflow has no approve, accept, appoint or execution API and reports no appointment, decision-acceptance or backend authority. No test fixture supplies a selected or reserved leaf.
