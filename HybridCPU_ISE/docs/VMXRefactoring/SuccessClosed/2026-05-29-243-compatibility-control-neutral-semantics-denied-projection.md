# Closure 243: Compatibility Control Neutral Semantics With Denied Projection

Date: 2026-05-29

## Selected Slice

Materialized `CompatibilityControlDescriptor` with explicit neutral fail-closed control semantics, but did not open any generated control VMREAD field.

This closes the owner-design step before any control-field compatibility projection.

## Neutral Semantics Materialized

`CompatibilityControlReadOnlyView.FailClosedProjectionOnly` now carries a semantically complete read-only policy view:

- runtime trap policy, neutral trap result, and publication fence requirements;
- runtime boundary admission requirement;
- read-projection-only execution;
- write denial;
- backend execution denial;
- authoritative mutation denial;
- neutral completion source and publication-fence requirements;
- retire publication requires a neutral permit;
- publication denied until a neutral route is materialized;
- root authority, descriptor, capability, scheduling, no-emission, and projection-evidence admission requirements;
- nested intent requires a neutral owner;
- second-stage translation and address-space tags require the memory owner;
- control-value projection remains denied.

`CompatibilityControlDescriptor.FromNeutralSemantics()` reports `ReadOnlyProjectionAvailable` only when that view is semantically complete.

## Fields Still Denied

Control VMREAD fields still have no admitted value mapper:

- `PinBasedControls`;
- `ProcBasedControls`;
- `ExitControls`;
- `EntryControls`;
- `SecondaryProcControls`.

They still stop at `NeutralOwnerValueSourceMissing` in `VmcsReadOnlyValueProjectionService`.

## Why No Control VMREAD Field Opened

This task materializes a neutral owner, not a VMCS control-bit projection. `VmcsReadOnlyValueProjectionService` still admits only completion-owned and memory-owned value sources. It does not dispatch `VmcsFieldProjectionOwner.CompatibilityControlDescriptor`.

Opening any control field still requires a separate field-by-field mapper from neutral policy semantics to frozen compatibility bits, plus conformance proving runtime admission and no VMCS field store fallback.

## Why No VMCS Field Store Appeared

No mutable VMCS projection object, active VMCS pointer, scalar control cache, `TryReadScalarField`, `ReadFieldValue`, or `WriteFieldValue` path was introduced. The owner is a read-only neutral runtime policy descriptor under `Core/Runtime/Capabilities/CompatibilityControls`.

## Why This Is Not Feature-Complete VMX Backend Execution

The change does not add VMX backend execution, launch/resume, VMWRITE, broad VMREAD, or successful retire behavior. It only gives future compatibility projection work a neutral control owner to consult after runtime admission.

## Absence Confirmation

Kept absent:

- `VmxExecutionUnit`;
- `VmcsManager`;
- `IVmcsManager`;
- active VMCS pointer state;
- VMCS field store;
- `VmcsManagerAdapter`;
- `VmxRuntimeManager`;
- `VmcsProjectionRuntimeManager`;
- `VmcsV2RuntimeManager`.

## Tests And Static Evidence

Added/updated:

- `VmxCompatibilityControlOwnerDesignTests`.

Verified:

- the default descriptor remains not materialized;
- `FailClosedProjectionOnly` materializes a complete neutral read-only policy view;
- materialized control ownership still keeps generated control VMREAD fields denied;
- runtime owner source has no `VmcsField` or concrete control-field aliases;
- VMREAD projection source still has no compatibility-control dispatch.

Build/test/static results:

- production build: passed (`HybridCPU_ISE.csproj --no-restore`, existing warnings only).
- tests build: passed (`HybridCPU_ISE.Tests.csproj --no-restore`, existing warnings only).
- console compatibility build: passed (`TestAssemblerConsoleApps.csproj --no-restore`, existing obsolete-constructor warnings only).
- `VmxCompatibilityControlOwnerDesignTests`: 4 passed.
- `VmxMemoryOwnedVmReadValueProjectionTests` plus `VmxGeneratedReadOnlyVmReadValueProjectionTests`: 12 passed.
- `RuntimeBoundaryAdmissionTests`: 4 passed.
- `VmxFirstAdmittedCompatibilityPathTests`: 1 passed.
- `VmxProjectionSchemaAndQuarantineTests`: 1 passed.
- `VmxCompatibilityProjectionInventoryTests`: 1 passed.
- broad VMX excluding NonVmx: 230 passed.
- forbidden marker scan over production virtualization excluding conformance: no matches.
- full virtualization marker scan matched conformance string-evidence only.
- `Virtualization/Substrate` project include scan: no matches.
- `Legacy/VMX` C# scan: no files.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` scan: no files.
- `TestAssemblerConsoleApps` scan for `core.VmxUnit`, `core.Vmcs`, and `vmxUnit`: no matches.

## Residual Risk

The field-by-field compatibility-control mapper remains intentionally absent. Future work must decide whether frozen control-bit projection is needed at all; if it is, the mapper must be generated/read-only, after runtime admission, and backed only by this neutral policy view.

## Next Heavy Step

Either keep control fields denied, or add a small generated/read-only compatibility-control projection mapper for one control field with conformance proving it uses `CompatibilityControlDescriptor` semantics and not a VMCS field store.
