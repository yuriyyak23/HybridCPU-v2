# Conformance, Golden Artifacts, And No-Emission

Conformance is an architectural boundary, not just a test suite. It prevents legacy VMX authority from returning through refactors, generated artifacts, compiler emission, or compatibility adapters.

## Conformance Families

Current virtualization conformance families include:

- ABI freeze contracts;
- authority boundary contracts;
- capability authority contracts;
- generated parity contracts;
- host-evidence non-leak contracts;
- memory translation authority contracts;
- migration replay contracts;
- nested composition contracts;
- no-emission contracts;
- golden artifact manifests.

Relevant path:

- `CloseToRTL/Core/Virtualization/Conformance/**`

## VMX Refactoring Tests

Relevant tests include:

- `VmxProjectionSchemaAndQuarantineTests`
- `VmxCompatibilityProjectionInventoryTests`
- `RuntimeBoundaryAdmissionTests`
- `VmxFirstAdmittedCompatibilityPathTests`
- `VmxGeneratedReadOnlyVmReadValueProjectionTests`
- `VmxMemoryOwnedVmReadValueProjectionTests`
- `VmxExecutionOwnedVmReadValueProjectionTests`
- `VmxControlLikeVmReadDenialTests`
- `VmxNeutralTrapResultSplitTests`
- `VmxAdmittedDeniedVmCallTrapPathTests`
- `VmxTrapProjectionPublicationFenceTests`
- `VmxTrapCompletionRouteOwnerTests`
- `VmxHypercallBackendAdmissionPolicyTests`
- `VmxDescriptorReadinessPolicyAuditTests`
- `VmxMigrationEvidenceRecomputedCompatibilityFieldTests`
- `SecureComputeVmxPhase8BoundaryMatrixTests`
- `SecureComputeVmxPhase9NestedFenceTests`
- `SecureComputeVmxPhase10ReleaseGateTests`
- `VmcsV2MutableHelperAuthorityTests`
- `CoreVmxAuthorityBoundaryTests`
- `VmxCapsProjectionBoundaryTests`

These tests prove that retained VMX vocabulary remains compatibility-only, that VMREAD opens only field-by-field through neutral value sources, and that admitted paths remain denied where backend authority has not landed.

## Generated Artifacts

Generated artifacts include:

- compatibility alias schema;
- VMCS field projection schema;
- capability descriptor projection schema;
- compatibility spec artifact inventory.

Generated-lineage evidence matters because a hand-edited compatibility projection can easily become a silent authority leak. Generated artifacts must carry canonical hashes, entry counts, and conformance parity tests.

## No-Emission Contracts

No-emission contracts ensure the compiler/backend cannot emit execution paths for virtualization features that are still denied, projection-only, or model-only.

No-emission applies to:

- VMX backend execution not admitted through runtime authority;
- VMCS writes without neutral owner;
- VMX trap/intercept success without completion and retire publication;
- VMCALL backend success without a neutral hypercall backend owner;
- SecureCompute activation/grant/checkpoint authority through VMX compatibility;
- compatibility aliases that are intentionally denied;
- legacy manager/field-store restoration.

## Static Quarantine

Static quarantine checks should continue to prove:

- no `VmxExecutionUnit` return;
- no `VmcsManager` or `IVmcsManager` return;
- no active VMCS pointer authority;
- no VMCS field-store methods;
- no direct hardware write helpers;
- no runtime branching on `VmExitReason` as authority;
- no VMX vocabulary in neutral runtime trap result types.

Some conformance files intentionally contain forbidden names as strings. Those are tripwires, not implementation.

## Broad Filter Caveat

Raw test filters such as `FullyQualifiedName~Vmx` can match `NonVmx` because the substring `Vmx` appears inside `NonVmx`. Failures isolated to known NonVmx instruction inventory counters should be classified as unrelated debt unless the change touched that area.

## Evidence Rule

A compatibility behavior is "closed" only when there is:

- code owner evidence;
- conformance evidence;
- no-emission or static quarantine evidence where relevant;
- documentation evidence that classifies current behavior versus denied/future behavior.
