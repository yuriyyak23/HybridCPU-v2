# Phase 13 - SecureCompute Virtualization Boundary Plan

Status: boundary and denial plan. SecureCompute is not activated through virtualization.

## 2026-06-11 Audit Contract

- File name: `13_securecompute_virtualization_boundary_plan.md`.
- Purpose: keep SecureCompute under neutral runtime descriptors and outside VMX/VMCS/`VmxCaps` authority.
- Status: boundary/denial plan; SecureCompute is not activated through virtualization.
- Scope: secure descriptors, runtime admission hook, compatibility projection denial, secure VMREAD prerequisites, VMWRITE denial, secure migration/evidence exclusions.
- No-goals: no VMX mode, no secure VMCS, no `VmxCaps` grant, no CHERI/tagged-memory semantics, no secure backend execution through VMX.
- Code anchors: `SecureComputeDomainDescriptor.cs`, `SecureComputeCompatibilityBoundaryMatrixPolicy.cs`, `RuntimeBoundaryAdmissionService.cs`.
- Authority owner: SecureCompute runtime descriptors/policies only; VMX owns none.
- Required RFC/ADR: mandatory for any SecureCompute positive path with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: VMX/VMCS/`VmxCaps` SecureCompute activation is `не доказано` and `должно оставаться denied`; first virtualization VMCALL path must be non-secure or secure-no-effect.
- Tests/static scans: VMX cannot activate SecureCompute, VMCS cannot store secure state, `VmxCaps` cannot grant authority, VMWRITE no secure effect, secure-sensitive VMREAD denied without full proof chain.
- Risks: compatibility projection or descriptor materialization being misread as secure backend success.
- Next-gate dependency: separate SecureCompute owner RFC/ADR; Phase 18 must re-check this boundary.

## Phase Goal

Preserve the SecureCompute boundary while virtualization activation proceeds through neutral owners only. SecureCompute must remain a neutral runtime descriptor/admission discipline, not a VMX mode or VMCS state.

## Historical Baseline (2026-06-11)

`SecureComputeDomainDescriptor` implements disabled/no-effect and active descriptor semantics. `RuntimeBoundaryAdmissionService` has an optional secure-domain hook. `SecureComputeCompatibilityBoundaryMatrixPolicy` denies VMX/VMCS/`VmxCaps` authority and allows only tightly constrained read-only projection when neutral owner, read-only source, secure visibility, migration class, and conformance proof exist.

`SecureDomainAdmissionDecision.AllowedSecureOperation` is secure-domain admission only; it is not secure backend execution. `SecureBackendRfcAdrState.Approved` can produce at most `AllowedProofOnlyNoExecution`, whose `BackendExecutionAuthorized` remains false, and any request for backend execution is denied as `DeniedBackendExecutionClosed`. The presence of `SecureCompletionPublicationFence` and its completion/retire predicates is policy structure, not proof that a production publication path executed.

The privileged execution-state owner and `GuestCr0`/`GuestCr4` projection are a field-specific read-only compatibility contract. They do not generalize into SecureCompute activation, broad VMCS availability, mutation, backend execution, completion publication, or retire publication.

## ISE-SECCOMP-VMX-BOUNDARY-13 - Closure Record

Closure date: 2026-06-18.

State: closed `DENIED/PROOF-ONLY BASELINE / NO-SECCOMP-VMX-ACTIVATION`.

This closure records the current SecureCompute virtualization boundary only. It does not accept a SecureCompute owner RFC/ADR, does not implement a secure backend executor, does not add secure completion or retire publication, and does not make VMX, VMCS, Shadow VMCS, or `VmxCaps` an authority source.

| Surface | Current result | Owner/value source | Evidence class | Migration class | Denial or boundary reason |
| --- | --- | --- | --- | --- | --- |
| `AllowedSecureOperation` | admission-only secure-domain result | SecureCompute runtime admission policy | policy admission evidence | not checkpoint payload authority | admission is not backend execution, completion publication, or retire publication |
| `AllowedProofOnlyNoExecution` | proof-only/no-execution result | secure backend owner RFC gate | proof-only owner evidence | not migration authority | `BackendExecutionAuthorized == false`; `DeniedBackendExecutionClosed` remains the execution boundary |
| `VmcsProjection`, `VmxCapsProjection`, `ShadowVmcsProjection` | denied as backend owner sources | none from virtualization frontend | denial evidence | not checkpoint payload authority | `DeniedNonNeutralAuthoritySource`; compatibility projection is not secure runtime ownership |
| secure-sensitive VMREAD projection | guarded read-only projection only when all source/owner/visibility/migration/conformance gates pass | neutral field owner plus secure visibility policy | compatibility projection evidence | `RevalidatedAfterRestore` when explicitly classified | VMREAD output is not SecureCompute activation and is not serialized secure authority |
| secure-sensitive VMWRITE | denied/no secure effect | no neutral write owner | write-denial evidence | not migration authority | VMCS schema, operand decode, and compatibility fields create no secure mutation authority |
| secure completion/publication fence predicates | policy-shape evidence only | secure runtime publication policy | fence evaluation evidence | not retire authority | a true isolated predicate is not proof that production completion or retire publication occurred |
| secure migration/evidence payloads | governed by secure migration policy | secure runtime migration owner | migration classification evidence | secure policy only | VMCS projection metadata, `VmxCaps`, lane/stream telemetry, or frontend state cannot become secure checkpoint authority |

Closure invariants:

- VMX, VMCS, Shadow VMCS, and `VmxCaps` cannot grant SecureCompute authority.
- `SecureDomainAdmissionDecision.AllowedSecureOperation` is not backend success.
- `SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution` is not execution.
- A materialized descriptor, proof-only RFC state, or secure completion fence type is not completion publication.
- Completion publication, if ever added by a secure runtime owner, is still not retire publication.
- Guarded `GuestCr0`/`GuestCr4` projection remains field-local read-only compatibility projection and cannot be generalized into SecureCompute activation.
- Any future positive SecureCompute path requires a separate owner-specific SecureCompute RFC/ADR with owner, value source, capability policy, evidence class, migration class, completion policy, retire policy, denial reasons, and negative tests.

## Owner Of Authority

SecureCompute authority belongs to secure runtime descriptors and policies: domain, memory, measurement, evidence, migration, I/O, hypercall, debug, compatibility projection policy, and secure nested intent. VMX owns none of these.

## What Can Be Implemented

- Negative tests that virtualization activation does not expose SecureCompute authority.
- Static scans for VMX/VMCS/`VmxCaps` SecureCompute activation attempts.
- For the recommended first VMCALL path, explicit secure-domain denial/no-effect behavior.
- Documentation that SecureCompute compatibility remains projection/denial only.

## What Remains Denied/Future-Gated

- SecureCompute backend execution through VMX.
- Secure VMCS.
- Secure descriptor materialization through `VmxCaps`.
- Secure state storage in VMCS or checkpoint projection metadata.
- VMWRITE to SecureCompute state.
- Secure nested execution through Shadow VMCS/VMCS12/VMCS02.

## Forbidden Shortcuts

- Calling SecureCompute a VMX mode.
- Treating `VmxCaps` as SecureCompute grant authority.
- Adding CHERI/tagged-memory/capability-aware ISA semantics to this virtualization path.
- Treating compatibility projection as secure backend success.
- Treating `AllowedSecureOperation` as backend execution.
- Treating `AllowedProofOnlyNoExecution`, an approved proof RFC state, or a materialized secure descriptor as backend success or publication.
- Treating a secure completion fence type or a true predicate in isolated policy input as evidence that production completion/retire publication occurred.
- Generalizing guarded `GuestCr0`/`GuestCr4` projection into SecureCompute or VMX authority.

## Required RFC/ADR

Required for any SecureCompute positive path. The recommended first virtualization VMCALL path should explicitly be non-secure or secure-no-effect unless a separate SecureCompute owner RFC/ADR exists.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Domain/SecureComputeDomainDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeCompatibilityBoundaryMatrixPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/11_capability_evidence_and_securecompute_boundary.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `Documentation/Virtualization WhiteBook/15_Security_Invariants.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`

## Required Tests

- VMX cannot activate SecureCompute.
- VMCS cannot store SecureCompute state.
- `VmxCaps` cannot grant SecureCompute authority.
- VMWRITE has no secure effect.
- Secure-sensitive VMREAD remains denied without all secure projection prerequisites.
- Guarded `GuestCr0`/`GuestCr4` projection remains read-only and cannot authorize backend success, mutation, completion, or retire.
- `AllowedSecureOperation` and `AllowedProofOnlyNoExecution` never satisfy virtualization backend execution.
- VMCALL owner path denies or no-effects secure-domain-sensitive classes unless secure owner RFC exists.

## Required Static/Source Scans

```powershell
rg -n "SecureCompute.*VMX|VMX.*SecureCompute|VmxCaps.*Secure|Secure.*Vmcs|Vmcs.*Secure|CHERI|tagged memory|capability-aware" HybridCPU_ISE Documentation
```

Matches must be denial, non-goal, proof-only, or neutral runtime policy context.

Additional execution-authority scan:

```powershell
rg -n "AllowedSecureOperation|AllowedProofOnlyNoExecution|BackendExecutionAuthorized:\s*true|CanPublishCompletion|CanPublishRetire" HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute HybridCPU_ISE/CloseToHSL/Core/Virtualization
```

Every match must preserve the distinction between admission/proof/policy shape and executed backend/publication authority.

## Migration/Evidence Classification

Secure host-owned evidence, measurement secrets, raw sealing keys, active host pointers, debug traces, backend handles, and compatibility projection metadata must not become guest checkpoint state. Secure migration remains governed by secure migration policy, not VMCS projection.

## Completion/Retire Implications

No secure completion or retire publication is allowed through the VMX frontend. Any future secure backend path requires its own completion/retire fence and must still not use VMX as authority.

## Exit Criteria

- SecureCompute remains outside VMX authority.
- Activation path either denies secure cases or explicitly proves no secure side effect.
- Static scans and negative tests cover VMX/VMCS/`VmxCaps` attempts.

## Dependency On Previous/Next Phase

Depends on Phases 02 and 03. It must be checked before Phase 18 release gate.
