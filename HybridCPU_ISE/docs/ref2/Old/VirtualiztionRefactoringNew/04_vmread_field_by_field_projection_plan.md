# Phase 04 - VMREAD Field By Field Projection Plan

## Goal

Replace any broad VMREAD activation idea with a field-by-field projection plan. Each field must name a generated schema entry, neutral owner, value source, evidence class, migration class, denial reason, and tests.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- Current admitted VMREAD path is `VmxCompatibilityAdmissionService.AdmitVmReadProjection(...)`.
- `TryReadScalarField` is not the current admitted value path.
- `VmcsFieldProjectionSchema` contains generated read-only entries and `CanWrite(...)` is always false.
- `VmcsReadOnlyValueProjectionService` projects only from admitted neutral sources.
- Completion-owned projected fields are `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification`.
- Memory-owned projected fields are `GuestCr3`, `EptPointer`, `Vpid`, and `Cr3TargetCount`.
- Execution-owned projected fields are `GuestPc`, `GuestSp`, and `GuestFlags`.
- `GuestCr0`, `GuestCr4`, host aliases, compatibility-control fields, unknown fields, and writes remain denied.

## Already Closed / Must Not Reopen

- VMCS scalar fallback is forbidden.
- Mutable VMCS state is forbidden.
- Host aliases cannot reuse guest-owned views.
- Control fields cannot infer frozen VMX control-bit values from `CompatibilityControlDescriptor`.
- `HostCr3` cannot reuse `MemoryDomainReadOnlyTranslationView.AddressSpaceRoot`.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Descriptors/ExecutionDomain/ExecutionDomainReadOnlyStateView.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Memory/Translation/MemoryDomainReadOnlyTranslationView.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/CompatibilityControls/CompatibilityControlDescriptor.cs`
- `Documentation/Virtualization WhiteBook/10_VMCS_Projection_And_Field_Access.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`

## Work Items

- Produce a VMREAD matrix with columns: field, schema owner, evidence class, migration policy, current result, reason, test anchor.
- Mark current projection fields as implemented projection-only, not state ownership.
- Mark denied fields with exact denial decision names from `VmcsReadOnlyValueProjectionDecision`.
- Require any future field to add a neutral owner value source before schema exposure can become projected value.
- Keep all writes denied unless a later phase creates an explicit neutral write owner and separate policy.

## Current VMREAD Readiness Matrix

Generated schema entries are compatibility vocabulary. A row marked projected means read-only projection only, not runtime ownership, migration authority, checkpoint authority, backend execution, completion publication, or retire publication.

| Field | Schema owner | Evidence class | Migration policy | Current result | Reason / decision | Test anchor |
| --- | --- | --- | --- | --- | --- | --- |
| `GuestPc` | `ExecutionDomainDescriptor` | `GuestArchitecturalState` | `DescriptorOwned` | projected when state view is materialized | `ReadOnlyValueProjected`; value comes from `ExecutionDomainReadOnlyStateView` PC slice | `VmxExecutionOwnedVmReadValueProjectionTests` |
| `GuestSp` | `ExecutionDomainDescriptor` | `GuestArchitecturalState` | `DescriptorOwned` | projected when state view is materialized | `ReadOnlyValueProjected`; value comes from `ExecutionDomainReadOnlyStateView` SP slice | `VmxExecutionOwnedVmReadValueProjectionTests` |
| `GuestFlags` | `ExecutionDomainDescriptor` | `GuestArchitecturalState` | `DescriptorOwned` | projected when state view is materialized | `ReadOnlyValueProjected`; value comes from `ExecutionDomainReadOnlyStateView` flags slice | `VmxExecutionOwnedVmReadValueProjectionTests` |
| `GuestCr0` | `ExecutionDomainDescriptor` schema alias only | `GuestArchitecturalState` | `DescriptorOwned` in schema, future owner required | future-gated; currently denied | `PrivilegedExecutionStateProjectionDenied`; no `PrivilegedExecutionStateDescriptor`, bit legality, memory-mode coupling, evidence, migration, or stale-state chain | `VmxGuestControlRegisterOwnerDecisionTests` |
| `GuestCr3` | `MemoryDomainDescriptor` | `GuestArchitecturalState` | `DescriptorOwned` | projected when translation view is materialized | `ReadOnlyValueProjected`; value comes from `MemoryDomainReadOnlyTranslationView.AddressSpaceRoot` | `VmxMemoryOwnedVmReadValueProjectionTests` |
| `GuestCr4` | `ExecutionDomainDescriptor` schema alias only | `GuestArchitecturalState` | `DescriptorOwned` in schema, future owner required | future-gated; currently denied | `PrivilegedExecutionStateProjectionDenied`; no `PrivilegedExecutionStateDescriptor`, bit legality, memory-mode coupling, evidence, migration, or stale-state chain | `VmxGuestControlRegisterOwnerDecisionTests` |
| `HostPc` | `ExecutionDomainDescriptor` schema alias only | `CompatibilityAlias` | `ProjectionOnly` | denied | `HostExecutionStateOwnerMissing`; guest read-only state cannot stand in for host execution state | `VmxExecutionOwnedVmReadValueProjectionTests` |
| `HostSp` | `ExecutionDomainDescriptor` schema alias only | `CompatibilityAlias` | `ProjectionOnly` | denied | `HostExecutionStateOwnerMissing`; guest read-only state cannot stand in for host execution state | `VmxExecutionOwnedVmReadValueProjectionTests` |
| `HostFlags` | `ExecutionDomainDescriptor` schema alias only | `CompatibilityAlias` | `ProjectionOnly` | denied | `HostExecutionStateOwnerMissing`; guest read-only state cannot stand in for host execution state | `VmxExecutionOwnedVmReadValueProjectionTests` |
| `HostCr0` | `ExecutionDomainDescriptor` schema alias only | `CompatibilityAlias` | `ProjectionOnly` | denied | `HostExecutionStateOwnerMissing`; no neutral host-execution control-register owner | `VmxControlLikeVmReadDenialTests` |
| `HostCr3` | `MemoryDomainDescriptor` schema alias only | `CompatibilityAlias` | `ProjectionOnly` | denied | `HostAddressSpaceOwnerMissing`; guest memory translation root cannot stand in for host address-space state | `VmxControlLikeVmReadDenialTests` |
| `PinBasedControls` | `CompatibilityControlDescriptor` schema alias only | `CompatibilityAlias` | `ProjectionOnly` | denied | `CompatibilityControlValueProjectionDenied`; fail-closed controls do not define frozen VMX control-bit value projection | `VmxControlLikeVmReadDenialTests` |
| `ProcBasedControls` | `CompatibilityControlDescriptor` schema alias only | `CompatibilityAlias` | `ProjectionOnly` | denied | `CompatibilityControlValueProjectionDenied`; fail-closed controls do not define frozen VMX control-bit value projection | `VmxControlLikeVmReadDenialTests` |
| `ExitControls` | `CompatibilityControlDescriptor` schema alias only | `CompatibilityAlias` | `ProjectionOnly` | denied | `CompatibilityControlValueProjectionDenied`; fail-closed controls do not define frozen VMX control-bit value projection | `VmxControlLikeVmReadDenialTests` |
| `EntryControls` | `CompatibilityControlDescriptor` schema alias only | `CompatibilityAlias` | `ProjectionOnly` | denied | `CompatibilityControlValueProjectionDenied`; fail-closed controls do not define frozen VMX control-bit value projection | `VmxControlLikeVmReadDenialTests` |
| `EptPointer` | `MemoryDomainDescriptor` | `CompatibilityAlias` | `DescriptorOwned` | projected when second-stage translation is owned | `ReadOnlyValueProjected`; value comes from `MemoryDomainReadOnlyTranslationView.SecondStageRoot` | `VmxMemoryOwnedVmReadValueProjectionTests` |
| `Vpid` | `MemoryDomainDescriptor` | `CompatibilityAlias` | `DescriptorOwned` | projected when address-space tagging is enabled and non-zero | `ReadOnlyValueProjected`; value comes from `MemoryDomainReadOnlyTranslationView.AddressSpaceTag` | `VmxMemoryOwnedVmReadValueProjectionTests` |
| `SecondaryProcControls` | `CompatibilityControlDescriptor` schema alias only | `CompatibilityAlias` | `ProjectionOnly` | denied | `CompatibilityControlValueProjectionDenied`; fail-closed controls do not define frozen VMX control-bit value projection | `VmxControlLikeVmReadDenialTests` |
| `Cr3TargetCount` | `MemoryDomainDescriptor` | `CompatibilityAlias` | `DescriptorOwned` | projected when translation view is materialized | `ReadOnlyValueProjected`; value comes from `MemoryDomainReadOnlyTranslationView.AddressSpaceTargetCount` | `VmxMemoryOwnedVmReadValueProjectionTests` |
| `ExitReason` | `CompletionRecord` | `CompatibilityAlias` | `RecomputedCompletion` | projected only from compatibility completion source | `ReadOnlyValueProjected`; recomputed from neutral `CompletionRecord`, not backend success | `VmxGeneratedReadOnlyVmReadValueProjectionTests` |
| `ExitQualification` | `CompletionRecord` | `CompatibilityAlias` | `RecomputedCompletion` | projected only from compatibility completion source | `ReadOnlyValueProjected`; recomputed from neutral `CompletionRecord`, not backend success | `VmxGeneratedReadOnlyVmReadValueProjectionTests` |
| `GuestPhysicalAddress` | `CompletionRecord` | `CompatibilityAlias` | `RecomputedCompletion` | projected only from compatibility completion source | `ReadOnlyValueProjected`; recomputed from neutral `CompletionRecord`, not memory owner mutation | `VmxGeneratedReadOnlyVmReadValueProjectionTests` |
| `EptViolationQualification` | `CompletionRecord` | `CompatibilityAlias` | `RecomputedCompletion` | projected only from compatibility completion source | `ReadOnlyValueProjected`; recomputed from neutral `CompletionRecord`, not backend success | `VmxGeneratedReadOnlyVmReadValueProjectionTests` |

`GuestCr0` and `GuestCr4` are not absent. They are explicit future-gated rows with current denial decision `PrivilegedExecutionStateProjectionDenied`. Future opening requires the owner/evidence/test chain in `05_privileged_execution_state_owner_decision.md`; it must not open a category-wide execution-field projection.

## Explicit Non-Goals

- Do not open `GuestCr0` or `GuestCr4` in this phase.
- Do not open host aliases in this phase.
- Do not open compatibility-control values in this phase.
- Do not create VMCS scalar storage.
- Do not describe VMREAD as a complete backend.

## Done Criteria

- The matrix covers every generated schema entry.
- Every current projected field has a named neutral value source.
- Every denied field has a named denial reason.
- The plan states that opening one field never opens a category.
- The plan states that projection values are not migration/checkpoint authority unless the neutral owner and migration policy say so.

## Required Tests / Static Checks

- `FullyQualifiedName~VmxGeneratedReadOnlyVmReadValueProjectionTests`
- `FullyQualifiedName~VmxControlLikeVmReadDenialTests`
- Generated schema parity tests.
- Static scan for scalar VMCS fallback.

## Residual Risk

The generated schema includes read-only entries that are still denied by value projection policy. Reviewers must not treat schema presence as projected value availability.

## External Audit Risk Update

Generated VMCS schema is compatibility vocabulary, not field availability. VMREAD must not open category-wide groups such as all execution fields or all memory fields. Every schema entry needs a matrix row with owner, evidence class, migration policy, current result, denial reason, and test anchor before any projection claim can be made.

## Next Phase Dependency

Phase 05 decides the privileged execution-state owner requirement for `GuestCr0` and `GuestCr4`.
