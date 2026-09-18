# Phase 02 - Global Forbidden Regressions And Static Gates

Status: gate definition. No runtime activation.

## 2026-06-11 Audit Contract

- File name: `02_global_forbidden_regressions_and_static_gates.md`.
- Purpose: define non-regression gates that keep compatibility vocabulary from becoming authority.
- Status: negative/static gate only; no activation approval.
- Scope: forbidden VMX backend names, VMCS mutable store, active VMCS pointer, VMWRITE, `RuntimeOwnedPublication` misuse, SecureCompute VMX authority, lane/stream leakage, compiler emission, migration authority.
- No-goals: no exception path without accepted owner-specific RFC/ADR and adjacent negative tests.
- Code anchors: `RuntimeBoundaryAdmissionService.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `SecureComputeCompatibilityBoundaryMatrixPolicy.cs`.
- Authority owner: conformance can detect violations; neutral runtime owners remain the only runtime authority.
- Required RFC/ADR: none for negative guards; every exception requires owner-specific RFC/ADR with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: forbidden scans are `NO_MATCH` or allowlisted as denial/projection-only; otherwise the path is `future-gated` and `должно оставаться denied`.
- Tests/static scans: source and doc scans listed below plus `VirtualizationActivationPlanAuditGuardTests`.
- Risks: allowlist drift, doc overclaim, or a "temporary" backend/fence bypass.
- Next-gate dependency: Phase 03 exception process.

## Phase Goal

Define the source, documentation, and conformance gates that must fail if any future work reintroduces VMX backend authority or turns projection/admission into runtime execution.

## Historical Baseline (2026-06-11)

The current corpus removed or fenced legacy VMX backend authority, VMCS mutable state, active VMCS pointer state, completion/retire publication from denied paths, VMX-owned SecureCompute, and migration/checkpoint projection authority.

## Owner Of Authority

The gate owner is conformance/static policy, but runtime authority remains with neutral owners. Tests and golden artifacts can detect violations; they cannot grant runtime authority.

## What Can Be Implemented

- Static scans for forbidden names and source patterns.
- Doc-lint for overclaim wording.
- Tests that require VMX frontend paths to go through `RuntimeBoundaryAdmissionService`.
- Tests that deny `RuntimeOwnedPublication` use by VMX frontend until backend owner proof exists.
- Tests that prove SecureCompute, Lane6/Lane7/Stream, compiler, and migration surfaces do not become VMX authority.
- Tests that keep VRT candidate owner/name/illustrative number out of production code until D2 is attributable and accepted.
- Tests that prevent a D2 manifest, public bool, route descriptor, `VmxRetireEffect` or compatibility completion factory from substituting for E2/E3/E5/E6 live tokens.
- Tests that keep the v1 same-object `AcceptedCommitSha` manifest from satisfying final D2 and require the future `VirtualizationDecisionSpecV2`/`VirtualizationDecisionAcceptanceRecordV2` SHA+digest split.
- Tests that reject `rs1`/`rs2` selector numbers, `VmxExitQualification.Leaf`, compatibility `ushort` truncation or a later register-file re-read as the canonical runtime operand snapshot.

## What Remains Denied/Future-Gated

- Any exception to a forbidden regression remains denied until a prior owner-specific RFC/ADR explicitly narrows the exception and adds negative tests.

## Forbidden Shortcuts

- Reintroducing `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, active VMCS pointer, mutable VMCS field store, legacy VMX backend adapter, VMX runtime manager, or Shadow VMCS runtime owner.
- Using `VmExitReason`, `TrapDecision`, VMCS field names, VMCALL trap projection, tests, docs, telemetry, or `VmxCaps` as runtime authority.
- Adding a "temporary" bypass around runtime admission, evidence policy, migration policy, completion fence, or retire rule.

## Required RFC/ADR

No RFC/ADR is needed for negative guards. Any allowed exception requires an already approved owner-specific RFC/ADR and must update this file.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeCompatibilityBoundaryMatrixPolicy.cs`
- Lane6/Lane7/Stream anchors named in Phase 12.

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/02_non_regression_baseline_and_guard_rails.md`
- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/13_conformance_golden_artifacts_and_static_gates.md`
- `Documentation/Virtualization WhiteBook/02_Principles_And_Non_Goals.md`
- `Documentation/Virtualization WhiteBook/15_Security_Invariants.md`
- `Documentation/Virtualization WhiteBook/19_Source_References_And_Check_Commands.md`

## Required Tests

- Keep existing closure tests green.
- Add activation-plan static tests for:
  - forbidden legacy VMX authority names outside tests/docs quarantine;
  - no `RuntimeOwnedPublication` use inside VMX frontend until Phase 06/07/08 gates are satisfied;
  - no SecureCompute activation path through VMX/VMCS/`VmxCaps`;
  - no Lane6/Lane7/Stream token or telemetry authority import into virtualization activation paths;
  - no compiler backend emission path for virtualization operations.

## Required Static/Source Scans

Suggested scans:

```powershell
rg -n "VmcsManager|IVmcsManager|VmxExecutionUnit|ActiveVmcs|active VMCS|VmcsFieldStore|VMCS field store" HybridCPU_ISE --glob "*.cs" --glob "!*.Tests/*"
rg -n "RuntimeOwnedPublication" HybridCPU_ISE/CloseToHSL/Core/Virtualization HybridCPU_ISE/CloseToHSL/Core/Runtime
rg -n "VmxCaps.*grant|SecureCompute.*VMX|VMX.*SecureCompute|VMCS.*SecureCompute" HybridCPU_ISE Documentation
rg -n "DmaStreamCompute|Lane6|Lane7|Stream" HybridCPU_ISE/CloseToHSL/Core/Virtualization HybridCPU_ISE/CloseToHSL/Core/Runtime/Events
rg -n "HCPU_HV_PROBE_V1|0x48594350_00000001|DomainHypercallRuntimeOwner|HybridCPU\.VMCALL\.Runtime\.v1|PROBE_NO_STATE_V1|DomainHypercallRuntimeExecutor" HybridCPU_ISE/CloseToHSL --glob "*.cs"
rg -n -i "VMCALL.*0x0001|hypercall.*0x0001|0x0001.*(VMCALL|hypercall)" HybridCPU_ISE/CloseToHSL --glob "*.cs"
rg -n "FromCompatibilityExit\(|TryFromCompatibilityExit\(" HybridCPU_ISE/CloseToHSL/Core/Execution HybridCPU_ISE/CloseToHSL/Core/Pipeline HybridCPU_ISE/CloseToHSL/Core/Runtime
```

Expected result is either `NO_MATCH` or a reviewed allowlist entry with explicit denial context.

### Current-HEAD Mandatory Guard Set

The following checks are mandatory before and after every plan update. They are readiness guards only:

- provenance: the evidence manifest must name current `HEAD`, declare dirty/clean state, and reject external side-loads;
- source layout: only tracked `CloseToHSL` may satisfy active source guards; `CloseToRTL` is obsolete and must have no tracked active-source files;
- pipeline: `ExecuteVmx` must remain `VmxFault` and the current retire owner must retain `ApplyRemovedFrontendFailClosedEffect` until a later authorized canonical-composition phase;
- direct-service isolation: production dispatcher/retire files must not reference `AdmitVmReadProjection` or `AdmitVmCallTrapProjection`;
- typed admission boundary: compatibility validation booleans remain projection/decode inputs and must never be described or consumed as SafetyVerifier authorization;
- E1 non-forgeability: the implemented `VirtualizationAdmissionCertificate` must remain internal, issuer-live and SafetyVerifier-exclusive, with no public/default constructor, compatibility/frontend issuer, compiler/test/generated issuer, deserialize-to-valid path or identity reconstruction from booleans;
- E1/E2 identity closure: E1 keeps D2-dependent identities explicitly absent and fault-only; only after machine D2/O1 may E2 bind the exact runtime leaf value, VT/domain, source/working slot, bundle/attempt/replay/restore generations, capability and evidence epochs. Address-space/descriptor identity is operation-specific and absent for `PROBE_NO_STATE_V1`;
- D2 provenance: v1 structural validity is not acceptance; machine D2 requires an immutable `VirtualizationDecisionSpecV2` plus a later `VirtualizationDecisionAcceptanceRecordV2` over the spec SHA+digest and matched reviewer evidence, with no self-referential containing-commit claim;
- PR-B/PR-C closure: the committed Phase 38 pair and exact lookup satisfy machine D2, while PR-C materializes only immutable O1 and E1-bound operand identity. Static gates must reject conversion of D2/O1/snapshot or allocation metadata into a live grant, E2, allowed backend, completion or retire authority;
- operand capture: production E2 must consume actual values from one immutable E1-bound canonical operand snapshot; a selector, compatibility qualification, width truncation, FSP donor value or register-file re-read is a failing match;
- backend: `HypercallBackendAdmissionDecision` must contain no allowed value, and production must contain no `BackendExecutionAuthorized: true` or backend executor;
- accepted-ADR isolation: exact Phase 38 vocabulary may match only the PR-A neutral governance contracts/validator and test/diagnostic/doc fixtures. It must have no production registry, lookup, grant, operand, executor or compatibility-frontend match. `DomainHypercallRuntimeExecutor` remains absent; contextual scans deny runtime VMCALL/hypercall allocation of `0x0001` while allowing the distinct frozen `VmxFunctionLeaf.CapabilityQuery = 1` and non-consumable governance policy constants;
- completion: VMX frontend must not reference `RuntimeOwnedCompletionPublication` or `RuntimeOwnedPublication` and must not construct `CompletionRecord`;
- retire: no compatibility/frontend path may issue a retire grant or write architectural registers/PC from projection DTOs;
- compiler/migration/lane/SecureCompute: no such surface may mint or restore runtime authority for virtualization.

Positive-looking direct unit fixtures are allowlisted only inside tests. They must not be counted as production matches or activation evidence.

Current narrow allowlist: `Compatibility/Frontend/Projection/Completion/CompletionRecordCompatibilityProjection.cs` contains an isolated compatibility factory, and its only callers are tests. The allowlist does not permit handler/dispatcher/pipeline/retire use. Any new production caller, any additional frontend constructor, or any attempt to treat the factory as owner-bound completion publication must fail the gate.

## Migration/Evidence Classification

Static gates must classify host-owned evidence, scheduler evidence, backend handles, native tokens, telemetry, debug traces, VMCS projection metadata, and golden artifacts as non-authoritative. Any migration-visible item must have a neutral migration class.

## Completion/Retire Implications

Any future production path constructing completion or retire publication must prove an ordered opaque chain:

- one live E2 authorized the exact attempt;
- one E3 receipt proves actual owner execution and is consumed once;
- one separate E5 token is issued and consumed once by the neutral completion owner to create one record;
- one separate E6 grant is issued and consumed once by canonical retire at the correct ROB/retire slot;
- all route/fence booleans, positive DTOs, compatibility completion factories and `VmxRetireEffect` remain data only and cannot replace any link.

## Exit Criteria

- Guard list is complete for current known forbidden regressions.
- Positive exceptions require owner-specific RFC/ADR.
- Source and doc scans can be wired into CI or run manually with clear expected outcomes.

## Dependency On Previous/Next Phase

Depends on Phase 01. Phase 03 defines the process for exceptions and future positive paths.
