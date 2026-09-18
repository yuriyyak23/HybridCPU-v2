# Phase 13 - Conformance Golden Artifacts And Static Gates

## Goal

Define the proof surfaces for the refactoring plan: conformance tests, generated artifact parity, static source scans, documentation scans, and golden files. These prove boundaries; they do not create runtime authority.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- VMX projection schema has generated parity and provenance contracts.
- VMX refactoring tests cover projection, denial, quarantine, host evidence non-leak, backend admission, and publication fences.
- Owner-specific `GuestCr0`/`GuestCr4` negative coverage is tracked by `VmxGuestControlRegisterOwnerDecisionTests`.
- VMCS write and compatibility-control hardening is tracked by `VmxVmcsWriteCompatibilityControlPolicyTests`.
- VMCALL backend-owner readiness is tracked by `VmxHypercallBackendOwnerDecisionReadinessTests`.
- Trap completion route and retire-publication hardening is tracked by `VmxTrapCompletionRouteRetirePublicationHardeningTests`.
- Nested child-intent hardening is tracked by `VmxNestedChildIntentHardeningTests`.
- Memory/I/O/IOMMU/Lane6/Lane7/Stream boundary hardening is tracked by `VmxMemoryIoLaneStreamBoundaryHardeningTests`.
- Capability/evidence/SecureCompute authority-separation hardening is tracked by `VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests`.
- Compiler/ISA/runtime no-emission hardening is tracked by `VmxCompilerIsaRuntimeNoEmissionContractTests`.
- Phase 13 consolidation is tracked by `VmxConformanceGoldenArtifactsAndStaticGatesTests`; it is a static/doc/source proof fixture only.
- SecureCompute tests cover no-effect, runtime admission, memory, evidence, migration, I/O/hypercall, VMX boundary, nested design fence, and release gates.
- Stream/L7 tests cover current bounded contours, helper/model boundaries, conflict/cache evidence, and compiler contracts.

## Already Closed / Must Not Reopen

- Tests are proof surfaces, not runtime owners.
- Golden artifacts are conformance evidence, not production state.
- Telemetry and diagnostics cannot satisfy capability/evidence authority.
- A passing broad test filter cannot override owner-specific denial rules.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Conformance/GeneratedParity/*`
- `HybridCPU_ISE.Tests/VmxRefactoring/*`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/*`
- `HybridCPU_ISE.Tests/tests/DmaStreamCompute*.cs`
- `HybridCPU_ISE.Tests/tests/L7Sdc*.cs`
- `Documentation/Virtualization WhiteBook/19_Source_References_And_Check_Commands.md`

## Work Items

- Define required test groups by phase and authority boundary.
- Define static scans for forbidden legacy names, overclaim language, and required code anchors.
- Define generated artifact parity checks for VMCS projection schema.
- Define doc-lint checks for SecureCompute and stream/L7 claim hygiene.
- Define optional focused no-build test command for VMX/SecureCompute uncertainty.

## Explicit Non-Goals

- Do not create runtime activation tests, backend-success tests, VMWRITE tests, publication tests, or production-path tests in this phase.
- A Phase 13 fixture may only enforce static/doc/source conformance and must not call a new runtime activation path.
- Do not make tests substitute for owner implementation.
- Do not use conformance fixtures as production runtime paths.
- Do not broaden filters that accidentally include unrelated `NonVmx` cases when only VMX proof is needed.

## Done Criteria

- Each phase has named required checks.
- Static scans can be run over the new plan folder.
- Anchor-presence scan proves the docs cite current code anchors.
- Positive shortcut scans return `NO_MATCH`; fail-closed vocabulary is classified by context rather than counted as failure.
- Phase 15 records Phase 13 as conformance/static-gate consolidation only.
- `git diff --check` passes for the new markdown files.

## Required Tests / Static Checks

- Forbidden-name `rg` scan over `VirtualiztionRefactoringNew`.
- Overclaim `rg` scan over `VirtualiztionRefactoringNew`.
- Anchor-presence `rg` scan over `VirtualiztionRefactoringNew`.
- `FullyQualifiedName~VmxGuestControlRegisterOwnerDecisionTests`
- `FullyQualifiedName~VmxVmcsWriteCompatibilityControlPolicyTests`
- `FullyQualifiedName~VmxHypercallBackendOwnerDecisionReadinessTests`
- `FullyQualifiedName~VmxTrapCompletionRouteRetirePublicationHardeningTests`
- `FullyQualifiedName~VmxNestedChildIntentHardeningTests`
- `FullyQualifiedName~VmxMemoryIoLaneStreamBoundaryHardeningTests`
- `FullyQualifiedName~VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests`
- `FullyQualifiedName~VmxCompilerIsaRuntimeNoEmissionContractTests`
- `FullyQualifiedName~VmxConformanceGoldenArtifactsAndStaticGatesTests`
- `git diff --check -- "HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew"`
- Optional focused `dotnet test --no-build` filter for VMX and SecureCompute tests if claims are uncertain.

## Closure Decision - ADR-VIRT-CONFORMANCE-GATES-2026-06-05

Phase 13 is closed as conformance/golden-artifact/static-gate consolidation only. The closure adds an executable proof fixture and documentation gates; it does not modify production runtime code, compiler emission code, VMCS mutation code, SecureCompute backend code, completion publication code, or retire publication code.

The closure preserves the authority split:

- compatibility frontend vocabulary is not runtime authority;
- VMCS/VMREAD projection is not runtime authority;
- runtime admission is not backend execution authority;
- backend execution authority is not completion publication;
- completion publication is not retire publication;
- SecureCompute authority is not issued through VMX, VMCS, or `VmxCaps`;
- Stream/Lane6/Lane7 helper/model/evidence surfaces are not backend execution;
- compiler/ISA no-emission tests and examples are not production emission authority;
- generated schemas and golden artifacts are conformance evidence, not runtime state or authority.

## Closed Pool Traceability Matrix

Every closed pool must have a current-status/ADR anchor, a named focused test, a source-anchor scan, a forbidden production shortcut scan, a documentation-overclaim scan, and a final-readiness entry in Phase 15.

| Pool | Current-status / ADR anchor | Focused guard | Source anchor scan | Forbidden production shortcut scan | Doc overclaim scan | Phase 15 final readiness entry |
| --- | --- | --- | --- | --- | --- | --- |
| `GuestCr0` / `GuestCr4` | `04_vmread_field_by_field_projection_plan.md`; `05_privileged_execution_state_owner_decision.md`; `ADR-VIRT-PES-2026-06-04` | `VmxGuestControlRegisterOwnerDecisionTests` | `GuestCr0`, `GuestCr4`, `PrivilegedExecutionStateProjectionDenied`, `PrivilegedExecutionStateDescriptor` | `VmcsManager`, `VmxExecutionUnit`, `TryReadScalarField`, `TryWriteScalarField`, `RuntimeOwnedPublication`, SecureCompute grant shortcuts | current activation, backend success, SecureCompute authority, and publication claims must be `NO_MATCH` outside denied/future contexts | `Owner-Specific Pool Status - 2026-06-04` |
| Phase 06 VMCS write / compatibility controls | `06_vmcs_write_and_compatibility_control_policy.md`; `ADR-VIRT-VMCS-WRITE-CONTROL-2026-06-04` | `VmxVmcsWriteCompatibilityControlPolicyTests` | `CanWrite(VmcsFieldProjectionSchemaEntry entry) => false`, `CompatibilityControlValueProjectionDenied`, `VmcsFieldAliasDecision.WriteDenied` | mutable VMCS manager/store, scalar write path, VMWRITE positive path, `CanWrite=true` | control-value projection, positive VMWRITE permission, and runtime activation approval claims must be `NO_MATCH` outside denied/future contexts | `Owner-Specific Pool Status - 2026-06-04` |
| Phase 07 VMCALL backend owner readiness | `07_hypercall_backend_owner_and_vmcall_decision.md`; `ADR-VIRT-HYPERCALL-BACKEND-2026-06-04` | `VmxHypercallBackendOwnerDecisionReadinessTests` | `HypercallBackendAdmissionRequest.MissingNeutralOwner`, `DeniedNeutralBackendOwnerMissing`, `TrapCompletionRouteRequest.ProjectionOnlyDenied`, `DeniedBackendExecution` | backend success true, `RuntimeOwnedPublication`, compatibility completion record publication, VMX retire effect publication | VMCALL backend success and frontend-vocabulary-as-authority claims must be `NO_MATCH` outside denied/future contexts | `Owner-Specific Pool Status - 2026-06-04` |
| Phase 08 route / completion / retire publication | `08_trap_completion_route_and_retire_publication.md`; `ADR-VIRT-TRAP-PUBLICATION-2026-06-04` | `VmxTrapCompletionRouteRetirePublicationHardeningTests` | `ProjectionOnlyDenied`, `DeniedCompletionPublication`, `DeniedRetirePublication`, `CompletionPublicationAllowed`, `RetirePublicationAllowed` | `RuntimeOwnedPublication` in VMX frontend paths, compatibility completion record from denied paths, `VmxRetireEffect` publication from denied paths | current completion publication and current retire publication claims must be `NO_MATCH` outside denied/future contexts | `Owner-Specific Pool Status - 2026-06-04` |
| Phase 09 nested child intent | `09_nested_virtualization_child_intent_plan.md`; `ADR-VIRT-NESTED-CHILD-INTENT-2026-06-04` | `VmxNestedChildIntentHardeningTests` | `SecureChildDomainIntentDescriptor`, `DeniedNestedVmcsAuthority`, `DeniedMutableShadowVmcsAuthority`, `CompatibilityProjectionFailed` | nested execution, VMCS12/VMCS02 authority, mutable shadow VMCS state, nested completion/retire publication | nested backend success, nested execution, mutable shadow VMCS, and VMCS12/VMCS02 authority claims must be `NO_MATCH` outside denied/future contexts | `Owner-Specific Pool Status - 2026-06-04` |
| Phase 10 memory/I/O/IOMMU/Lane6/Lane7/Stream | `10_memory_io_iommu_lanes_and_stream_boundary.md`; `ADR-VIRT-MEM-IO-LANE-STREAM-2026-06-05` | `VmxMemoryIoLaneStreamBoundaryHardeningTests` | `VmxCompatibilityIoAliasesAreReadOnlyDenied`, `Guest Lane6 compatibility execution is fail-closed`, `Guest Lane7 compatibility execution is fail-closed`, `VmxDmaDescriptorValidator` | Lane6/Lane7/Stream backend authority shortcut, IOMMU mutation shortcut, mutable VMCS state, compiler VMX opcode emission | Lane6/Lane7 passthrough, stream backend authority, IOMMU VMX backend authority, and DMA VMX backend authority claims must be `NO_MATCH` outside denied/future contexts | `Owner-Specific Pool Status - 2026-06-04` |
| Phase 11 capability/evidence/SecureCompute | `11_capability_evidence_and_securecompute_boundary.md`; `ADR-VIRT-CAP-EVIDENCE-SECCOMP-2026-06-05` | `VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests` | `SecureBackendOwnerAdmissionPolicy`, `AllowedProofOnlyNoExecution`, `DeniedBackendExecutionClosed`, `DeniedVmxCapsAuthority`, `DeniedVmcsCheckpointAuthority` | SecureCompute VMX/VMCS/`VmxCaps` authority, backend success, completion publication, retire publication, compiler VMX emission | SecureCompute production readiness, SecureCompute backend execution, and VMX/Vmcs/VmxCaps activation claims must be `NO_MATCH` outside denied/future contexts | `Owner-Specific Pool Status - 2026-06-04` |
| Phase 12 compiler/ISA/runtime no-emission | `12_compiler_isa_runtime_no_emission_contract.md`; `ADR-VIRT-COMPILER-NOEMISSION-2026-06-05` | `VmxCompilerIsaRuntimeNoEmissionContractTests` | `CompilerVmxAuthority`, `CompilerVmcsV2DescriptorSideband`, `CompilerBackendLoweringContract`, `HybridCpuThreadCompilerContext`, `HybridCpuIrBuilder` | compiler VMX/SecureCompute helper emission, VMX activation/mutation opcode emission, SecureCompute authority imports, runtime publication shortcuts | compiler VMX backend, SecureCompute compiler ISA enablement, examples-as-authority, and sideband-runtime-authority claims must be `NO_MATCH` outside denied/future contexts | `Owner-Specific Pool Status - 2026-06-04` |
| Phase 13 conformance/static-gate consolidation | `13_conformance_golden_artifacts_and_static_gates.md`; `ADR-VIRT-CONFORMANCE-GATES-2026-06-05` | `VmxConformanceGoldenArtifactsAndStaticGatesTests` | `GeneratedProjectionParityContract`, `GeneratedVmcsProjectionProvenanceContract`, `VmcsFieldProjectionSchemaConformanceContract`, `VmxSpecConformanceTests` | any new activation, backend execution, VMWRITE, publication, SecureCompute activation, nested execution, Lane passthrough, stream authority, or compiler emission shortcut | Phase 13 must not describe conformance/golden/static evidence as activation approval | `Phase 13 Conformance Pool Status - 2026-06-05` |

## Static Gate Execution Protocol

- `MATCH_REQUIRED`: source and document anchor scans must find the named ADRs, focused fixtures, generated parity contracts, and denied/future-gated authority vocabulary.
- `NO_MATCH_REQUIRED`: positive production shortcut scans must return `NO_MATCH`. Treat `rg` exit code `1` as success only for these scans.
- Fail-closed vocabulary such as `DeniedBackendExecution`, `ProjectionOnlyDenied`, `CanWrite=false`, `AllowedProofOnlyNoExecution`, and `RuntimeOwnedPublication` in future-gated route policy definitions is not a failure by itself.
- Documented future-gated names are allowed only in denied, future, prerequisite, or must-not-open contexts.
- Current activation/backend/publication claims are forbidden unless the same row or paragraph states denial, future-gating, proof-only status, or no execution/publication authority.

Consolidated Phase 13 static gates:

- ADR/fixture anchor scan:
  - `rg -n "ADR-VIRT-PES-2026-06-04|ADR-VIRT-VMCS-WRITE-CONTROL-2026-06-04|ADR-VIRT-HYPERCALL-BACKEND-2026-06-04|ADR-VIRT-TRAP-PUBLICATION-2026-06-04|ADR-VIRT-NESTED-CHILD-INTENT-2026-06-04|ADR-VIRT-MEM-IO-LANE-STREAM-2026-06-05|ADR-VIRT-CAP-EVIDENCE-SECCOMP-2026-06-05|ADR-VIRT-COMPILER-NOEMISSION-2026-06-05|ADR-VIRT-CONFORMANCE-GATES-2026-06-05|VmxGuestControlRegisterOwnerDecisionTests|VmxVmcsWriteCompatibilityControlPolicyTests|VmxHypercallBackendOwnerDecisionReadinessTests|VmxTrapCompletionRouteRetirePublicationHardeningTests|VmxNestedChildIntentHardeningTests|VmxMemoryIoLaneStreamBoundaryHardeningTests|VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests|VmxCompilerIsaRuntimeNoEmissionContractTests|VmxConformanceGoldenArtifactsAndStaticGatesTests" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew HybridCPU_ISE.Tests/VmxRefactoring --glob "*.md" --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- Generated parity / golden artifact anchor scan:
  - `rg -n "GeneratedProjectionParityContract|GeneratedVmcsProjectionProvenanceContract|VmcsFieldProjectionSchemaConformanceContract|CompatAliasSchemaConformanceContract|VmxCapsBitSchemaConformanceContract|VmxSpecConformanceTests|VmxProjectionSchemaAndQuarantineTests|VmxGeneratedReadOnlyVmReadValueProjectionTests" HybridCPU_ISE/CloseToHSL/Core/Virtualization/Conformance/GeneratedParity HybridCPU_ISE.Tests --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- Positive shortcut production scan, expected `NO_MATCH`:
  - `rg -n "BackendExecutionAuthorized:\\s*true|BackendSuccessAuthorized:\\s*true|AllowBackendExecution\\s*=\\s*true|CompletionPublicationAllowed:\\s*true|RetirePublicationAllowed:\\s*true|MutableNestedStateAuthorized:\\s*true|CanWrite\\s*=\\s*true|CanWrite\\s*=>\\s*true" HybridCPU_ISE/CloseToHSL HybridCPU_ISE/NonRTL HybridCPU_Compiler --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- VMX frontend publication shortcut scan, expected `NO_MATCH`:
  - `rg -n "TrapCompletionRouteDescriptor\\.RuntimeOwnedPublication|CompletionRecord\\.(FromCompatibilityExit|TryFromCompatibilityExit)|VmxRetireEffect\\.(InterceptExit|VmCall|VmFunc)" HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers HybridCPU_ISE/CloseToHSL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- Compiler VMX/SecureCompute helper emission scan, expected `NO_MATCH`:
  - `rg -n "InstructionsEnum\\.(VMXON|VMXOFF|VMLAUNCH|VMRESUME|VMPTRLD|VMPTRST|VMCLEAR|VMWRITE|VMCALL|VMFUNC)|VMXON|VMLAUNCH|VMRESUME|VMWRITE|VMCALL|SecureComputeDomainDescriptor|SecureBackendOwnerAdmissionPolicy|VmxCaps\\.Secure|VmcsManager|IVmcsManager|VmxExecutionUnit" HybridCPU_Compiler/API HybridCPU_Compiler/Core HybridCPU_ISE/CloseToHSL/Core/ISA/Instructions/NonVmx --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- Documentation overclaim scan, expected `NO_MATCH` outside Phase 13 command text:
  - `rg -n "current.*(authorizes|allows|enables|activates).*(VMX backend|backend execution|VMWRITE|VMCALL backend success|completion publication|retire publication|SecureCompute backend|SecureCompute activation|nested execution|Lane6 passthrough|Lane7 passthrough|stream backend|compiler VMX)|production ready|activation approved|examples.*production authority" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md" --glob "!13_conformance_golden_artifacts_and_static_gates.md"`

Owner-specific static gates for `GuestCr0`/`GuestCr4`:

- `rg -n "GuestCr0|GuestCr4|PrivilegedExecutionStateProjectionDenied|PrivilegedExecutionStateDescriptor|VmxGuestControlRegisterOwnerDecisionTests" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md"`
- `rg -n "case VmcsField.GuestCr0|case VmcsField.GuestCr4|TryReadScalarField|TryWriteScalarField|ReadFieldValue\\(|WriteFieldValue\\(|VmcsManager|VmxExecutionUnit" HybridCPU_ISE/CloseToHSL HybridCPU_ISE/NonRTL`
- `rg -n "VmxCaps grants SecureCompute|VMCS owns secure state|VMX activates SecureCompute|BackendExecutionAuthorized: true|TrapCompletionRouteDescriptor.RuntimeOwnedPublication" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md"`

Owner-specific static gates for VMCS writes and compatibility controls:

- `rg -n "CanWrite\\(VmcsFieldProjectionSchemaEntry entry\\) => false|CompatibilityControlValueProjectionDenied|VmcsFieldAliasDecision.WriteDenied" HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility --glob "*.cs"`
- `rg -n "TryWriteScalarField|WriteKnownScalar|_scalarValues|_scalarWritten|VmcsManager|IVmcsManager|VmxExecutionUnit|VmxRuntimeManager|VmcsProjectionRuntimeManager|VmcsV2RuntimeManager|ReadFieldValue\\(|WriteFieldValue\\(" HybridCPU_ISE/CloseToHSL HybridCPU_ISE/NonRTL --glob "*.cs" --glob "!**/Conformance/**" --glob "!**/Plan/**" --glob "!**/Docs/**" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "PinBasedControls.*ReadOnlyValueProjected|ProcBasedControls.*ReadOnlyValueProjected|ExitControls.*ReadOnlyValueProjected|EntryControls.*ReadOnlyValueProjected|SecondaryProcControls.*ReadOnlyValueProjected|VMWRITE.*allowed|CanWrite=true|runtime activation approved|VMCALL backend success.*allowed" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md" --glob "!13_conformance_golden_artifacts_and_static_gates.md"`

Owner-specific static gates for VMCALL hypercall backend owner readiness:

- `rg -n "HypercallBackendAdmissionRequest.MissingNeutralOwner|DeniedNeutralBackendOwnerMissing|TrapCompletionRouteRequest.ProjectionOnlyDenied|DeniedBackendExecution|VmxHypercallBackendOwnerDecisionReadinessTests" HybridCPU_ISE/CloseToHSL HybridCPU_ISE.Tests/VmxRefactoring HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.cs" --glob "*.md" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "TrapCompletionRouteDescriptor.RuntimeOwnedPublication|BackendExecutionAuthorized: true|new HypercallBackendDescriptor|CompletionRecord.TryFromCompatibilityExit|CompletionRecord.FromCompatibilityExit|VmxRetireEffect.InterceptExit|VmxRetireEffect.VmCall|VmxRetireEffect.VmFunc" HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers HybridCPU_ISE/CloseToHSL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs --glob "*.cs"`
- `rg -n "VmExitReason\\.VmCall is backend authorization|TrapDecision is neutral runtime policy|Compatibility projection is backend owner|RuntimeOwnedPublication.*current.*allowed|BackendExecutionAuthorized: true|VMCALL backend success.*allowed" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md" --glob "!13_conformance_golden_artifacts_and_static_gates.md"`

Owner-specific static gates for trap completion route and retire publication:

- `rg -n "ProjectionOnlyDenied|RuntimeOwnedPublication|DeniedBackendExecution|DeniedCompletionPublication|DeniedRetirePublication|CompletionPublicationAllowed|RetirePublicationAllowed|VmxTrapCompletionRouteRetirePublicationHardeningTests" HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility HybridCPU_ISE.Tests/VmxRefactoring HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.cs" --glob "*.md" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "TrapCompletionRouteDescriptor.RuntimeOwnedPublication|CompletionRecord.TryFromCompatibilityExit|CompletionRecord.FromCompatibilityExit|VmxRetireEffect.InterceptExit|VmxRetireEffect.VmCall|VmxRetireEffect.VmFunc" HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers HybridCPU_ISE/CloseToHSL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs --glob "*.cs"`
- `rg -n "CompletionRecord\\.(TryFromCompatibilityExit|FromCompatibilityExit)" HybridCPU_ISE/CloseToHSL --glob "*.cs" --glob "!**/Frontend/Projection/Completion/CompletionRecordCompatibilityProjection.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "new CompletionRecord\\(" HybridCPU_ISE/CloseToHSL --glob "*.cs" --glob "!**/Runtime/Completion/Records/**" --glob "!**/Frontend/Projection/Completion/CompletionRecordCompatibilityProjection.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "RuntimeOwnedPublication.*current.*allowed|CompletionPublicationAllowed.*current.*true|RetirePublicationAllowed.*current.*true|admitted-denied VMCALL.*CompletionRecord\\.FromCompatibilityExit|admitted-denied VMCALL.*VmxRetireEffect\\.InterceptExit|VMCALL backend success.*allowed" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md" --glob "!13_conformance_golden_artifacts_and_static_gates.md"`

Owner-specific static gates for nested child intent hardening:

- `rg -n "SecureChildDomainIntentDescriptor|SecureNestedDomainAdmissionPolicy|DeniedNestedVmcsAuthority|DeniedMutableShadowVmcsAuthority|BackendSuccessAuthorized: false|MutableNestedStateAuthorized: false|ShadowVmcsNestedProjectionService|CompatibilityProjectionFailed|Child-domain intent field read requires neutral runtime-owned nested intent state|VmxNestedChildIntentHardeningTests" HybridCPU_ISE/CloseToHSL HybridCPU_ISE.Tests/VmxRefactoring HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.cs" --glob "*.md" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "BackendSuccessAuthorized: true|MutableNestedStateAuthorized: true|NestedExecutionUnit|NestedRuntimeManager|Vmcs12Manager|Vmcs02Manager|new ShadowVmcsBlock|class ShadowVmcsBlock|IShadowVmcs|TryWriteIntentField|TryVmRead|TryVmWrite|RuntimeOwnedPublication|CompletionRecord\\.FromCompatibilityExit|CompletionRecord\\.TryFromCompatibilityExit|VmxRetireEffect\\.(InterceptExit|VmCall|VmFunc)" HybridCPU_ISE/CloseToHSL/Core/Runtime/Nested HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/Nested HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Nested HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Nested HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Nested HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/NestedDomainControllerCompatibilityProjection.cs HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers HybridCPU_ISE/CloseToHSL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "nested backend success.*(allowed|authorized|active|enabled)|nested execution.*(allowed|authorized|active|enabled)|VMCS12.*(owns|grants|authorizes).*runtime|VMCS02.*(owns|grants|authorizes).*runtime|shadow VMCS.*mutable runtime state|child intent.*publishes completion|child intent.*publishes retire|BackendSuccessAuthorized: true|MutableNestedStateAuthorized: true" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md" --glob "!13_conformance_golden_artifacts_and_static_gates.md"`

Owner-specific static gates for Phase 10 memory/I/O/IOMMU/Lane6/Lane7/Stream boundary hardening:

- `rg -n "VmxMemoryIoLaneStreamBoundaryHardeningTests|VmxCompatibilityIoAliasesAreReadOnlyDenied|Guest Lane6 compatibility execution is fail-closed|Guest Lane7 compatibility execution is fail-closed|VmxDmaDescriptorValidator|Lane6DomainRuntime|Lane7DomainRuntime|MemoryDomainRuntime|IoDomainRuntime|DmaAuthorityService|IotlbInvalidationService" HybridCPU_ISE/CloseToHSL HybridCPU_ISE/NonRTL HybridCPU_ISE.Tests/VmxRefactoring HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.cs" --glob "*.md" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "DmaStreamComputeRuntime|VmxDmaDescriptorValidator|ExternalAcceleratorRuntime|Lane6DomainRuntime|Lane7DomainRuntime|Lane7CompletionPolicy|DmaStreamComputeRetirePublication|SystemDeviceCommandMicroOp" HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers HybridCPU_ISE/CloseToHSL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "TryWriteScalarField|WriteFieldValue\\(|ReadFieldValue\\(|VmcsManager|IVmcsManager|VmxExecutionUnit|TrapCompletionRouteDescriptor.RuntimeOwnedPublication|CompletionRecord\\.(FromCompatibilityExit|TryFromCompatibilityExit)|VmxRetireEffect\\.(InterceptExit|VmCall|VmFunc)" HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Adapters/IO HybridCPU_ISE/CloseToHSL/Core/Runtime/IO HybridCPU_ISE/CloseToHSL/Core/Runtime/Nested/MemoryComposition HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/Memory HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/IO HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/Lane6 HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/Lane7 HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Lane6DmaStream HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Lane7Accelerator HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "InstructionsEnum\\.(VMXON|VMXOFF|VMLAUNCH|VMRESUME|VMPTRLD|VMPTRST|VMCLEAR|VMWRITE|VMCALL)|VMXON|VMWRITE|VMCALL" HybridCPU_Compiler/Core HybridCPU_ISE/CloseToHSL/Core/ISA/Instructions/NonVmx --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "Lane6.*VMX.*backend.*(allowed|authorized|active|enabled)|Lane7.*VMX.*backend.*(allowed|authorized|active|enabled)|stream.*VMX.*backend.*(allowed|authorized|active|enabled)|IOMMU.*VMX.*backend.*(allowed|authorized|active|enabled)|DMA.*VMX.*backend.*(allowed|authorized|active|enabled)|VmxCaps grants SecureCompute|VMCS owns secure state|VMX activates SecureCompute|RuntimeOwnedPublication.*current.*allowed|CompletionPublicationAllowed.*current.*true|RetirePublicationAllowed.*current.*true" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md" --glob "!13_conformance_golden_artifacts_and_static_gates.md"`

Owner-specific static gates for Phase 11 capability/evidence/SecureCompute boundary hardening:

- `rg -n "VmxCapabilityEvidenceSecureComputeBoundaryHardeningTests|SecureBackendOwnerAdmissionPolicy|AllowedProofOnlyNoExecution|DeniedBackendExecutionClosed|SecureComputeCompatibilityBoundaryMatrixPolicy|DeniedVmxCapsAuthority|DeniedVmcsCheckpointAuthority|DeniedBackendSuccess|SecureComputeVmxCapsProjectionFence|SecureComputeVmcsProjectionFence|VmxCapsProjection|CapabilityDescriptorSetSchema|RuntimeBoundaryAdmissionService" HybridCPU_ISE.Tests/VmxRefactoring HybridCPU_ISE/CloseToHSL/Core HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.cs" --glob "*.md" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "BackendExecutionAuthorized: true|AllowBackendExecution = true|VmxCaps\\.Secure|VmcsManager|IVmcsManager|VmxExecutionUnit|ReadFieldValue\\(|WriteFieldValue\\(|CompletionRecord\\.(FromCompatibilityExit|TryFromCompatibilityExit)|TrapCompletionRouteDescriptor\\.RuntimeOwnedPublication|VmxRetireEffect\\.(InterceptExit|VmCall|VmFunc)" HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Backend HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Backend HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Evidence HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Migration HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Io HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Publication HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/CsrProjection/VmxCapsProjection.cs HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "SecureCompute backend execution is allowed|SecureCompute is production ready|SecureCompute supported via VMX|VmxCaps grants SecureCompute|VMCS owns secure state|VMX activates SecureCompute|evidence authorizes backend execution|telemetry authorizes backend execution|migration authorizes SecureCompute activation|runtime admission authorizes backend execution" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md" --glob "!13_conformance_golden_artifacts_and_static_gates.md"`

Owner-specific static gates for Phase 12 compiler/ISA/runtime no-emission hardening:

- `rg -n "VmxCompilerIsaRuntimeNoEmissionContractTests|CompilerVmxAuthority|CompilerVmcsV2DescriptorSideband|CompilerBackendLoweringContract|HybridCpuThreadCompilerContext|HybridCpuIrBuilder|HybridCpuBundleLowerer|DmaStreamComputeCompilerContractTests|CompilerNoEmissionBoundaryTests|L7SdcCompilerEmissionTests" HybridCPU_ISE.Tests HybridCPU_Compiler HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.cs" --glob "*.md" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "InstructionsEnum\\.(VMXON|VMXOFF|VMLAUNCH|VMRESUME|VMPTRLD|VMPTRST|VMCLEAR|VMWRITE|VMCALL|VMFUNC)|VMXON|VMLAUNCH|VMRESUME|VMWRITE|VMCALL|SecureComputeDomainDescriptor|SecureBackendOwnerAdmissionPolicy|VmxCaps\\.Secure|VmcsManager|IVmcsManager|VmxExecutionUnit|BackendExecutionAuthorized: true|TrapCompletionRouteDescriptor\\.RuntimeOwnedPublication|CompletionRecord\\.(FromCompatibilityExit|TryFromCompatibilityExit)|VmxRetireEffect\\.(InterceptExit|VmCall|VmFunc)" HybridCPU_Compiler/API HybridCPU_Compiler/Core/IR/Construction HybridCPU_Compiler/Core/IR/Bundling HybridCPU_ISE/CloseToHSL/Core/ISA/Instructions/NonVmx --glob "*.cs" --glob "!**/bin/**" --glob "!**/obj/**"`
- `rg -n "compiler.*(authorizes|enables|opens).*VMX backend|compiler.*(authorizes|enables|opens).*SecureCompute|no-emission.*authorizes backend|facade.*VMX backend.*allowed|sideband.*runtime authority|examples.*production authority|VMWRITE.*compiler emitted|VMCALL.*compiler backend success|SecureCompute.*compiler ISA.*enabled" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md" --glob "!13_conformance_golden_artifacts_and_static_gates.md"`

## Residual Risk

Static scans can produce intentional hits where forbidden names are listed as absent/must-not-return. The review protocol must classify context, not just count hits.

## External Audit Risk Update

Broad passing tests are not activation proof. Golden artifacts and telemetry prove conformance surfaces only. Activation readiness requires targeted owner-specific gates, negative tests for denied states, executable static scans, and `git diff --check` as a review requirement.

## Next Phase Dependency

Phase 14 depends on these gates to migrate documentation and remove stale claims safely.
