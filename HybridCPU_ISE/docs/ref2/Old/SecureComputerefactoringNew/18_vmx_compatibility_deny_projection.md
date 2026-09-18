# Phase 18 - VMX Compatibility Deny Projection

## Goal

Preserve VMX as a frozen compatibility frontend. VMX, VMCS, VMREAD, VMWRITE and VmxCaps cannot activate, grant, materialize, checkpoint, migrate, publish or own SecureCompute authority. Optional future projection can only be read-only and backed by neutral owner, visibility, migration and conformance proof.

## Current Code Baseline

`SecureComputeCompatibilityBoundary` denies VMX activation, VmxCaps authority, VMCS state store and write mutation. `SecureComputeCompatibilityBoundaryMatrixPolicy` requires neutral owner, read-only source, secure visibility, migration classification and conformance proof for secure-sensitive VMREAD projection. `SecureComputeVmWriteDenyPolicy` keeps writes denied. `SecureComputeVmxCapsProjectionFence` denies authority grant, activation bit and write mutation.

VMREAD has a specific semantic trap: an entry in `VmcsFieldProjectionSchema` is not a current readable value. Schema owner, access metadata and generated alias presence are not sufficient. Actual value projection requires `VmcsReadOnlyValueProjectionService`, neutral value owner, read-only source, evidence/access policy, migration classification and tests.

VMREAD projection state matrix:

| State / artifact | Meaning | Not sufficient for |
|---|---|---|
| `VmcsFieldProjectionSchema` entry present | generated compatibility alias metadata exists | current readable value, VMREAD authority or SecureCompute authority |
| Schema `ReadOnly` / `CanRead` | alias may be considered by the projection evaluator | value projection, runtime admission or write authority |
| `VmcsReadOnlyValueProjectionService` present | fail-closed value-source evaluator exists | broad VMREAD opening or authority for every schema entry |
| Runtime admission allowed | projection-only runtime boundary allowed this compatibility read attempt | field value publication without neutral owner/value source |
| Read-only value projected | only admitted for a field with neutral owner, value source, evidence policy, migration classification and conformance proof | backend success, migration authority, completion/retire publication or production activation |

`GuestCr0` and `GuestCr4` remain the sharp edge of this boundary. They may appear as generated read-only schema entries, but current value projection remains denied until a neutral privileged execution-state owner RFC is approved and implemented with semantics, value source, visibility policy, migration classification and tests. The owner RFC by itself is not a VMREAD opening.

## Already Closed / Must Not Reopen

- VMX cannot activate SecureCompute.
- VmxCaps cannot grant SecureCompute.
- VMCS cannot store SecureCompute state.
- VMWRITE cannot mutate SecureCompute state.
- VMREAD of secure-sensitive fields remains denied unless the full neutral read-only proof chain exists.
- Compatibility projection cannot become backend success.
- `GuestCr0`, `GuestCr4`, host execution aliases, `HostCr3`, compatibility-control fields, unknown fields and all writes remain denied unless separate neutral owner/RFC requirements are met.
- Compatibility-control schema entries marked read-only do not mean current readable frozen VMX control-bit values.
- `VmcsReadOnlyValueProjectionService` existence does not project `GuestCr0`, `GuestCr4` or compatibility-control values without their separate neutral owner/value contracts.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Frontend/SecureComputeCompatibilityBoundary.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeCompatibilityBoundaryMatrixPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmReadVisibilityPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmWriteDenyPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmxCapsProjectionFence.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/CompatibilityControls/CompatibilityControlDescriptor.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxDenialGuardTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase8BoundaryMatrixTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase10ReleaseGateTests.cs`

## Work Items

- Keep the deny/projection matrix as a mandatory activation prerequisite.
- Require every future read-only projection to prove neutral owner, read-only source, secure visibility, migration class and conformance.
- Keep all write/mutation paths denied.
- Keep compatibility advertisement policy in Plan2 until separate RFC/ADR.
- Maintain a field-by-field VMREAD readiness matrix:
  - projected only with neutral owner/value source: completion-owned fields, memory-owned fields and the currently admitted execution-owned `GuestPc`, `GuestSp`, `GuestFlags`;
  - denied until neutral privileged execution-state owner RFC: `GuestCr0`, `GuestCr4`;
  - denied until neutral host owner: `HostPc`, `HostSp`, `HostFlags`, `HostCr0`, `HostCr3`;
  - denied until frozen control-bit value contract: compatibility-control fields;
  - denied by default: unknown fields and all writes.

## Explicit Non-Goals

- No VMX-owned SecureCompute authority.
- No VMCS-backed SecureCompute state.
- No VmxCaps grant or activation bit.
- No VMREAD backend mutation.
- No VMWRITE secure-state mutation.
- No broad VMREAD opening from schema entry alone.
- No `GuestCr0`/`GuestCr4` opening without neutral privileged execution-state owner semantics, visibility policy, migration classification and tests.
- No compatibility-control value projection from `CompatibilityControlDescriptor` fail-closed semantics alone.
- No `VmcsReadOnlyValueProjectionService` presence as authority to project every generated read-only schema entry.

## Done Criteria

- Boundary matrix tests remain green.
- Documentation states VMX compatibility is projection-only or denied.
- Any future read-only projection is explicitly non-authoritative.
- Schema `ReadOnly` and current value projection are separately documented.
- Phase 21 blocks activation if VMX/VMCS/VmxCaps bypass exists.

## Required Tests / Static Checks

- `SecureComputeVmxDenialGuardTests`
- `SecureComputeVmxPhase8BoundaryMatrixTests`
- `SecureComputeVmxPhase10ReleaseGateTests`
- `VmxCapsProjectionBoundaryTests`
- VMREAD field tests for `GuestCr0`, `GuestCr4`, compatibility-control fields and all VMWRITE denial.
- Source scans for forbidden authority phrases in VMX and SecureCompute directories.
- `SecureComputePhase10ReleaseGateTests` VMREAD/schema wording guard proving schema entries, read-only metadata, `VmcsReadOnlyValueProjectionService` and `CompatibilityControlDescriptor` are not current readable values by themselves.

## Residual Risk

Generated compatibility schemas can look authoritative because they carry VMX names. Keep schema owner and neutral owner separate in all future projections.

The audit adds that `ReadOnly` may be misread as "readable now." Keep generated schema presence, read-only access metadata and actual admitted value projection as three different states.

## Next Phase Dependency

Phase 19 applies the same deny/projection discipline to nested secure design fences.
