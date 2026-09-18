# Closure 250: Execution-Owned VMREAD Value Projection

Date: 2026-05-29

## Selected Slice

Closed the execution-owned VMREAD heavy step for the safest architectural-state subset only:

- `GuestPc`
- `GuestSp`
- `GuestFlags`

`GuestCr0` and `GuestCr4` remain denied because neutral privileged execution-state semantics are not materialized yet.

## Dependencies Checked

The current VMREAD/projection chain is still:

```text
VMREAD(field)
  -> VMREAD decode
  -> frozen alias projection validation
  -> RuntimeBoundaryAdmissionService(ReadCompatibilityProjection)
  -> VmcsFieldProjectionSchema owner lookup
  -> neutral owner/value source
  -> evidence policy
  -> generated/read-only compatibility value projection
```

Checked dependencies:

- `VmcsFieldProjectionSchema` marks `GuestPc`, `GuestSp`, `GuestFlags`, `GuestCr0`, and `GuestCr4` as `ExecutionDomainDescriptor`-owned, `GuestArchitecturalState`, read-only, descriptor-owned fields.
- `ExecutionDomainDescriptor` now exposes `TryCreateReadOnlyStateView()`.
- `ExecutionDomainReadOnlyStateView` is neutral runtime state under `Core/Runtime/Domains/Descriptors/ExecutionDomain`.
- `VmcsReadOnlyValueProjectionService` consumes only that neutral view for the opened execution-owned fields.

## Production Change

Added neutral execution read-only state:

- `ExecutionDomainReadOnlyStateView`
- `ExecutionDomainDescriptor.ReadOnlyState`
- `ExecutionDomainDescriptor.TryCreateReadOnlyStateView(...)`
- `ExecutionDomainDescriptor.WithReadOnlyState(...)`

`VmxCompatibilityAdmissionService.AdmitVmReadProjection` now passes the admitted runtime context execution descriptor into `VmcsReadOnlyValueProjectionService`.

`VmcsReadOnlyValueProjectionService` now projects:

- `GuestPc` from `ExecutionDomainReadOnlyStateView.GuestPc`
- `GuestSp` from `ExecutionDomainReadOnlyStateView.GuestSp`
- `GuestFlags` from `ExecutionDomainReadOnlyStateView.GuestFlags`

The projection succeeds only when the corresponding `HasMaterialized*` flag is true.

## What Remains Denied

Still denied/fail-closed:

- default/unmaterialized execution descriptors;
- partially materialized execution fields whose per-field flag is false;
- `GuestCr0`;
- `GuestCr4`;
- `HostCr3`;
- compatibility-control fields;
- unknown fields;
- all writes.

`GuestCr0` and `GuestCr4` return `PrivilegedExecutionStateProjectionDenied` until a neutral privileged execution-state owner and semantics contract exist.

## Why No VMCS Field Store Appeared

No VMCS scalar store, active VMCS pointer, mutable projection object, VMCS manager, or fallback read path was added.

The value source is not:

- `VmcsV2Descriptor`;
- `VmcsV2Blocks`;
- scalar cache;
- VMCS block;
- test-only behavior;
- legacy guest-state capture/materialization helpers.

The only admitted source for these fields is neutral `ExecutionDomainReadOnlyStateView`.

## Why VMREAD Is Still Not Feature-Complete Backend Execution

This is a generated/read-only compatibility value projection after neutral authority, not a VMREAD backend.

VMREAD still cannot execute broadly, write back mutable VMCS state, materialize hidden state, or bypass runtime admission. It can only project admitted fields whose neutral owners expose explicit read-only values.

## Tests

Added:

- `VmxExecutionOwnedVmReadValueProjectionTests`.

Updated:

- `VmxGeneratedReadOnlyVmReadValueProjectionTests`.

The tests prove:

- `GuestPc`, `GuestSp`, and `GuestFlags` are projected only from neutral execution read-only state;
- projection requires runtime admission and generated schema owner metadata;
- `GuestArchitecturalState` evidence is enforced;
- default/unmaterialized execution descriptors remain denied;
- per-field missing materialization remains denied;
- `GuestCr0` and `GuestCr4` remain explicitly denied;
- no VMCS field store or legacy guest-state helper fallback is present.

## Documentation

Updated:

- `audit3.md`;
- `audit4.md`;
- `audit5.md`;
- `2026-05-24-vmx-current-model-completion-audit.md`;
- `ОСНОВЫ и ПРАВИЛА VMX.md`.

## Verification

Builds:

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, 0 warnings.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, 0 warnings.
- `dotnet build TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --no-restore`: passed, 2 existing obsolete-constructor warnings.

Focused tests:

- `VmxExecutionOwnedVmReadValueProjectionTests`: passed, 6 tests.
- `VmxGeneratedReadOnlyVmReadValueProjectionTests`: passed, 5 tests.
- `VmxMemoryOwnedVmReadValueProjectionTests`: passed, 10 tests.
- `VmxFirstAdmittedCompatibilityPathTests`: passed, 1 test.
- `RuntimeBoundaryAdmissionTests`: passed, 4 tests.
- `VmxProjectionSchemaAndQuarantineTests`: passed, 1 test.
- `VmxCompatibilityProjectionInventoryTests`: passed, 1 test.

Broad VMX:

- `FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx`: passed, 247 tests.

Static:

- Full `CloseToHSL/Core/Virtualization` forbidden-authority scan found only pre-existing conformance/static-evidence contract string literals that name forbidden markers as deny-list entries.
- Production scan excluding `Conformance` found no forbidden authority markers.
- `HybridCPU_ISE.csproj` has no `Virtualization/Substrate` or `Virtualization\Substrate` entries.
- `TestAssemblerConsoleApps` has no `core.VmxUnit`, `core.Vmcs`, or `vmxUnit` references.
- No `VmxExecutionUnit.cs`, `VmcsManager.cs`, or `IVmcsManager.cs` files exist outside `CloseToHSL`.
- `Legacy/VMX` has no C# files.
- `rg -il "legacy" CloseToHSL/Core/Virtualization --glob "*.cs"` returned no production legacy markers.
- `git diff --check`: passed; Git reported only LF-to-CRLF normalization warnings.

## Residual Risk

`ExecutionDomainReadOnlyStateView` currently covers only the narrow `GuestPc`/`GuestSp`/`GuestFlags` read-only projection slice. It is not a full guest CPU state model.

Future work must not infer `GuestCr0` or `GuestCr4` from this view. Those fields need separate neutral privileged execution-state semantics and conformance, or they must remain denied.

## Next Heavy Step

Continue VMREAD expansion field-by-field through explicit neutral owners only. The next honest branch is either:

1. define neutral privileged execution-state semantics for `GuestCr0` / `GuestCr4`, then open a tiny read-only slice; or
2. keep `GuestCr0` / `GuestCr4` denied and move to another field only where a neutral value source already exists.
