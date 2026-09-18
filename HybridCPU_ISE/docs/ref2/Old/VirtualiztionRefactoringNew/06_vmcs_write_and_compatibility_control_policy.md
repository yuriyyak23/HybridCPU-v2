# Phase 06 - VMCS Write And Compatibility Control Policy

## Goal

Keep VMCS write behavior and compatibility-control fields denied unless a separate neutral write/control owner exists. This phase documents why control descriptors are not enough to expose control-bit values.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- `VmcsFieldProjectionSchema.CanWrite(...)` returns `false` for all entries.
- Compatibility-control fields are present in the generated schema as read-only aliases.
- `VmcsReadOnlyValueProjectionService` returns `CompatibilityControlValueProjectionDenied` for `CompatibilityControlDescriptor`-owned fields.
- `CompatibilityControlDescriptor` exposes neutral fail-closed control semantics, not frozen VMX control-bit values.
- `VmcsFieldAliasProjection` returns `WriteDenied` for write access.
- `VmcsV2Descriptor` has no public scalar write authority; `TryWriteScalarField` remains absent.

## Decision Record - VMCS Write And Compatibility Control Denial

Decision id: `ADR-VIRT-VMCS-WRITE-CONTROL-2026-06-04`.

Status: accepted as a denial/readiness hardening decision only. It does not implement VMWRITE, does not open compatibility-control VMREAD values, does not add a mutable VMCS field store, and does not authorize VMX backend execution or SecureCompute authority.

Current write owner: none. All generated VMCS projection entries remain write-denied by `VmcsFieldProjectionSchema.CanWrite(...) == false`, `VmcsFieldAliasDecision.WriteDenied`, and the absence of `VmcsV2Descriptor.TryWriteScalarField`.

Current compatibility-control value owner: none. `CompatibilityControlDescriptor` is a neutral fail-closed policy descriptor. It can say that runtime boundary admission, read-projection-only, write denial, backend denial, authoritative-mutation denial, trap policy, and publication fence requirements exist. It does not own frozen VMX control-bit values and cannot map directly to `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, or `SecondaryProcControls`.

Current VMREAD result for every compatibility-control field: `ReadOnlyProjectionDenied` with `VmcsReadOnlyValueProjectionDecision.CompatibilityControlValueProjectionDenied`.

Current VMWRITE result: no positive production write path exists. VMWRITE is frozen compatibility opcode vocabulary only; decode vocabulary cannot mutate schema entries, compatibility controls, runtime domains, SecureCompute descriptors, completion records, or retire state.

## Compatibility-Control Matrix

| Field | Schema owner | Access policy | Migration policy | Current VMREAD result | Current VMWRITE result | Reason / decision | Test anchor |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `PinBasedControls` | `CompatibilityControlDescriptor` | `ReadOnly` schema vocabulary | `ProjectionOnly` | denied | denied | `CompatibilityControlValueProjectionDenied`; no frozen VMX control-bit value mapper | `VmxVmcsWriteCompatibilityControlPolicyTests` |
| `ProcBasedControls` | `CompatibilityControlDescriptor` | `ReadOnly` schema vocabulary | `ProjectionOnly` | denied | denied | `CompatibilityControlValueProjectionDenied`; no frozen VMX control-bit value mapper | `VmxVmcsWriteCompatibilityControlPolicyTests` |
| `ExitControls` | `CompatibilityControlDescriptor` | `ReadOnly` schema vocabulary | `ProjectionOnly` | denied | denied | `CompatibilityControlValueProjectionDenied`; no frozen VMX control-bit value mapper | `VmxVmcsWriteCompatibilityControlPolicyTests` |
| `EntryControls` | `CompatibilityControlDescriptor` | `ReadOnly` schema vocabulary | `ProjectionOnly` | denied | denied | `CompatibilityControlValueProjectionDenied`; no frozen VMX control-bit value mapper | `VmxVmcsWriteCompatibilityControlPolicyTests` |
| `SecondaryProcControls` | `CompatibilityControlDescriptor` | `ReadOnly` schema vocabulary | `ProjectionOnly` | denied | denied | `CompatibilityControlValueProjectionDenied`; no frozen VMX control-bit value mapper | `VmxVmcsWriteCompatibilityControlPolicyTests` |

Read-only schema vocabulary is not value availability. Projection service denial remains final for the current slice.

## Future Read-Only Control-Value Mapper Preconditions

If frozen control-bit projection is ever approved, it must be a separate read-only mapper with:

- a neutral owner for each field;
- explicit field-by-field rows, not category-wide control opening;
- frozen bit definitions, reserved-bit masks, must-be-zero/must-be-one policy, and stale-state denial;
- runtime boundary admission and evidence visibility approval;
- migration/checkpoint classification as projection-only unless a separate neutral owner class says otherwise;
- tests proving missing owner, partial mapper, reserved-bit violation, schema owner mismatch, stale epoch, host alias, SecureCompute, VMWRITE, and VMCS scalar fallback attempts all deny.

`CompatibilityControlDescriptor` presence alone cannot satisfy these preconditions.

## Future Write Owner Preconditions

Any future write path requires a different ADR/RFC from the read-only mapper. Required preconditions:

- neutral write owner and runtime operation kind;
- capability, root-authority, evidence, domain-boundary, and scheduling gates;
- explicit authoritative-mutation policy and rollback/abort behavior;
- field-by-field reserved-bit and policy validation;
- no generated schema write-through;
- no mutable VMCS cache or active VMCS pointer authority;
- no SecureCompute activation, grant, state mutation, completion publication, or retire publication through VMWRITE;
- negative tests before any positive write path.

Until that owner exists, `CanWrite=false` is the final schema policy for all generated entries.

## Already Closed / Must Not Reopen

- Do not create VMCS writes through generated projection entries.
- Do not create a mutable VMCS control-bit store.
- Do not use `CompatibilityControlDescriptor` as direct VMX control-bit projection.
- Do not infer control values from policy presence.
- Do not make write denial depend on test-only behavior.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/CompatibilityControls/CompatibilityControlDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE/docs/VMXRefactoring/SuccessClosed/2026-05-29-243-compatibility-control-neutral-semantics-denied-projection.md`
- `HybridCPU_ISE/docs/VMXRefactoring/SuccessClosed/2026-05-29-245-control-vmread-values-explicitly-denied.md`

## Work Items

- Document all compatibility-control schema entries and their current denial reason. Done in this phase.
- Define the future requirement for a frozen read-only control-value mapper, if one is ever approved. Done as preconditions above.
- Define the separate requirement for any neutral write owner, policy, capability, evidence, and tests. Done as preconditions above.
- Add explicit docs that generated schema `ReadOnly` does not imply current projected value. Done in this phase.
- Preserve all write paths as denied until a later owner-specific plan exists.

## Explicit Non-Goals

- Do not open VMWRITE.
- Do not add one-off control-bit projections.
- Do not create a compatibility-control mapper from inferred bits.
- Do not allow VMX control writes to mutate runtime domains.

## Done Criteria

- All control fields are listed as denied.
- Write policy is documented as all denied in the current corpus.
- Any future read-only control mapper has named preconditions.
- Any future write path is separated from read-only projection and requires its own owner decision.
- Focused tests prove `CanWrite=false`, compatibility-control value denial, write-alias denial, no scalar write authority, and no mutable VMCS manager/store return.

## Required Tests / Static Checks

- `FullyQualifiedName~VmxControlLikeVmReadDenialTests`
- `FullyQualifiedName~VmxCompatibilityControlOwnerDesignTests`
- `FullyQualifiedName~VmxVmcsWriteCompatibilityControlPolicyTests`
- Static scan for `CanWrite` behavior.
- Static scan for VMCS write/store vocabulary in product code.
- Documentation scan ensuring control fields are not described as currently projected values.

Owner-specific static gates:

- `rg -n "CanWrite\\(VmcsFieldProjectionSchemaEntry entry\\) => false|CompatibilityControlValueProjectionDenied|VmcsFieldAliasDecision.WriteDenied" HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility --glob "*.cs"`
- `rg -n "TryWriteScalarField|WriteKnownScalar|_scalarValues|_scalarWritten|VmcsManager|VmxExecutionUnit|VmxRuntimeManager" HybridCPU_ISE/CloseToHSL HybridCPU_ISE/NonRTL --glob "*.cs" --glob "!**/Conformance/**" --glob "!**/Plan/**" --glob "!**/Docs/**" --glob "!**/bin/**" --glob "!**/obj/**"`
- Documentation overclaim scan for compatibility-control field names paired with successful value-projection language, positive VMWRITE language, or positive `CanWrite` assignment; result must be `NO MATCH` outside static-gate definitions.

## Residual Risk

The phrase "read-only" in schema entries can be misread as "currently readable value." The projection service remains the deciding layer.

## External Audit Risk Update

`CanWrite=false` must be protected by static scans and negative tests. VMWRITE must not be implemented as a normal write to generated VMCS schema, compatibility controls, or a mutable cache. Any future write path requires a separate neutral write owner and policy.

## Next Phase Dependency

Phase 07 depends on the same owner-first discipline for VMCALL backend decisions.
