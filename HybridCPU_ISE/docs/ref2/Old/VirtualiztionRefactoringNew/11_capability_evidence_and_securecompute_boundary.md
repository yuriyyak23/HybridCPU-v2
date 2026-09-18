# Phase 11 - Capability Evidence And SecureCompute Boundary

## Goal

Preserve capability, evidence, and SecureCompute authority in neutral runtime descriptors and policies. VMX, VMCS, `VmxCaps`, VMREAD, VMWRITE, stream helpers, L7 commands, telemetry, and tests must remain non-authoritative for SecureCompute.

## Activation Authority

This phase creates review and negative-conformance material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- `SecureComputeDomainDescriptor` is the neutral secure-domain descriptor with disabled/no-effect semantics.
- `DomainRuntimeContext` can carry optional SecureCompute descriptor plus neutral domain and address-space tags.
- `RuntimeBoundaryAdmissionService` invokes SecureCompute admission only for secure operation classes with an enabled descriptor.
- `VmxCapsProjection` publishes only known VMX compatibility capability bits through `CapabilityDescriptorSetSchema.VmxCompatibility`.
- SecureCompute VMX compatibility boundary code denies activation/grant/storage authority through VMX/VMCS/`VmxCaps`.
- `SecureBackendOwnerAdmissionPolicy` can accept a complete neutral proof chain only as `AllowedProofOnlyNoExecution` and denies `RequestsBackendExecution`.
- Positive secure backend runtime execution remains open/future-gated in the SecureCompute whitebook.

## Already Closed / Must Not Reopen

- SecureCompute is not VMX mode.
- SecureCompute is not secure VMCS.
- `VmxCaps` is not SecureCompute authority.
- SecureCompute is not CHERI ISA, tagged memory, capability registers, or capability-aware LOAD/STORE/FETCH.
- VMX cannot activate, grant, materialize, checkpoint, migrate, or own SecureCompute.
- Evidence and telemetry are proof surfaces, not authority.

## Required Code/Doc Anchors

- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Domain/SecureComputeDomainDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Backend/SecureBackendOwnerDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Backend/SecureBackendOwnerAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Services/DomainRuntimeContext.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/CsrProjection/VmxCapsProjection.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeCompatibilityBoundaryMatrixPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmcsProjectionFence.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmxCapsProjectionFence.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests.cs`

## Work Items

- Build a SecureCompute boundary matrix for VMREAD, VMWRITE, `VmxCaps`, VMCS checkpoint, migration, evidence, and hypercall paths.
- Classify current SecureCompute phases as baseline closures, not new virtualization work.
- List future secure backend owner preconditions from the SecureCompute whitebook.
- Require visibility, evidence, migration, completion, and retire classifications for any future read-only secure projection.
- Define negative tests for VMX/VMCS/`VmxCaps` authority attempts.

## Closure Decision - ADR-VIRT-CAP-EVIDENCE-SECCOMP-2026-06-05

Phase 11 is closed as authority-separation hardening only. The closure added focused negative/conformance coverage and documentation gates; it did not add production runtime code, SecureCompute backend execution, VMCS mutation, completion publication, retire publication, or compiler/backend emission.

| Surface | Current authority | Allowed now | Denied / forbidden |
| --- | --- | --- | --- |
| `VmxCaps` capability projection | `CapabilityDescriptorSetSchema.VmxCompatibility` and typed grants | Known read-only VMX compatibility bits only | SecureCompute, measurement, attestation, evidence, grant, activation, or write-mutation bits |
| Secure backend owner proof | Neutral runtime/device/migration owner descriptor plus approved RFC/ADR proof chain | `AllowedProofOnlyNoExecution` only | Compatibility, VMX frontend, VMCS, `VmxCaps`, shadow-VMCS sources and any backend execution request |
| Secure VMREAD projection matrix | Neutral owner + read-only source + visibility + migration + conformance proof | Read-only projection only for explicitly classified ordinary fields | secure evidence/debug/migration fields, schema owner mismatch, missing proof, backend success |
| VMWRITE / VMCS checkpoint / `VmxCaps` materialization | Runtime-owned future policy only | none in current Phase 11 baseline | SecureCompute write mutation, VMCS checkpoint authority, `VmxCaps` descriptor materialization |
| Runtime boundary admission | `RuntimeBoundaryAdmissionService` plus neutral descriptors/tags | projection/admission checks; absent/disabled SecureCompute stays no-effect | treating runtime admission as backend execution authority |
| Evidence, telemetry, replay, migration, Lane6/Lane7/Stream helper surfaces | host/runtime-owned proof surfaces | evidence for review, diagnostics, recomputation, or native runtime domains | SecureCompute activation, backend execution, completion publication, retire publication, VMX authority |

`VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests` is the focused closure fixture for this phase. It ties together capability projection, SecureCompute compatibility matrix, backend-owner proof-only admission, runtime-admission separation, and source-level shortcut absence.

## Explicit Non-Goals

- Do not reimplement SecureCompute phases.
- Do not claim SecureCompute production readiness.
- Do not open secure backend runtime execution.
- Do not add capability-aware ISA or memory semantics.
- Do not make compatibility projection a grant source.

## Done Criteria

- SecureCompute is documented as neutral runtime descriptor/admission discipline.
- VMX compatibility is projection/denial only unless a secure runtime owner and policy explicitly allow read-only projection.
- Positive secure backend runtime execution remains future-gated.
- All VMX/VMCS/`VmxCaps` authority shortcuts are forbidden.
- `VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests` passes and proves Phase 11 did not open execution/publication authority.

## Required Tests / Static Checks

- `FullyQualifiedName~SecureComputeVmxPhase8BoundaryMatrixTests`
- `FullyQualifiedName~VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests`
- SecureCompute runtime admission hook tests.
- SecureCompute evidence/migration/I/O/hypercall policy tests.
- Static doc/source scans for VMX/VMCS/`VmxCaps` SecureCompute authority claims.

## Residual Risk

SecureCompute contains many closed policy and proof surfaces. Readers can misread them as runtime backend execution unless every plan keeps "policy admission" separate from "backend success."

## External Audit Risk Update

SecureCompute claim drift is a high risk. Documentation may state projection/denial only, but must not claim VMX-provided SecureCompute support unless a secure runtime owner, visibility policy, evidence policy, and migration policy explicitly allow read-only projection. VMX, VMCS, VMWRITE, VMREAD, `VmxCaps`, tests, telemetry, and compatibility projections cannot grant SecureCompute authority.

## Next Phase Dependency

Phase 12 depends on these authority boundaries to keep compiler and ISA surfaces from emitting or promising backend behavior prematurely.
