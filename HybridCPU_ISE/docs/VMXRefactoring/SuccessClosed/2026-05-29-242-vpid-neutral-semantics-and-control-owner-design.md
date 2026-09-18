# Closure 242: VPID Neutral Semantics And Control Owner Design

Date: 2026-05-29

## Selected Slice

Closed the next heavy step in two deliberately separate parts:

1. `Vpid` VMREAD is admitted as a generated read-only value projection only when the value comes from neutral memory-domain identity.
2. A neutral compatibility-control owner skeleton is defined, but no control VMREAD field is opened yet.

## VMREAD Dependencies Found

- Decode and admission path: `VmxCompatibilityAdmissionService.AdmitVmReadProjection`.
- Runtime authority gate: `RuntimeBoundaryAdmissionService` with `ReadCompatibilityProjection`.
- Generated owner metadata: `VmcsFieldProjectionSchema`.
- Frozen alias/evidence validation: `VmcsFieldAliasProjection`.
- Memory value source: `MemoryDomainDescriptor.TryCreateReadOnlyTranslationView()` and `MemoryDomainReadOnlyTranslationView`.
- Read-only value mapper: `VmcsReadOnlyValueProjectionService`.

## Fields Projected

- `GuestCr3`: already projected from neutral `AddressSpaceRoot` by closure `241`.
- `EptPointer`: already projected from owned neutral `SecondStageRoot` by closure `241`.
- `Vpid`: now projected from neutral `MemoryDomainReadOnlyTranslationView.AddressSpaceTag`.

VPID projection requires:

- generated schema owner `MemoryDomainDescriptor`;
- successful runtime boundary admission;
- compatibility-alias evidence visibility;
- valid memory-domain translation control;
- `AddressSpaceTaggingEnabled == true`;
- non-zero `AddressSpaceTag`.

If tagging is disabled or the tag is not materialized, `Vpid` remains denied with `MemorySourceDenied`. No zero/fake VPID value is projected.

## Fields Still Denied

- `HostCr3`.
- `Cr3TargetCount`.
- execution-owned fields.
- `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, and `SecondaryProcControls`.
- unknown/ungenerated fields.
- all writes.

## Neutral Control Owner Design

Added `CompatibilityControlDescriptor` under `Core/Runtime/Capabilities/CompatibilityControls`.

The descriptor is neutral runtime vocabulary:

- `CompatibilityControlMaterializationState`;
- `CompatibilityControlReadOnlyView`;
- `TryCreateReadOnlyControlView()`.

The default owner has no materialized read-only view. Control VMREAD fields still return `NeutralOwnerValueSourceMissing`; the VMREAD projection service does not dispatch to compatibility-control values yet.

## Why No VMCS Field Store Appeared

The value path never consults `TryReadScalarField`, `ReadFieldValue`, `WriteFieldValue`, active VMCS pointer state, or a mutable VMCS projection object. Values are produced only after runtime admission and generated schema owner lookup, and only from neutral runtime/domain sources.

## Why This Is Not Feature-Complete VMX Backend Execution

This is still a compatibility projection slice:

```text
VMREAD
  -> decode
  -> frozen alias/evidence validation
  -> RuntimeBoundaryAdmissionService
  -> generated owner lookup
  -> neutral read-only value source
  -> compatibility value projection
```

There is no broad VMREAD backend, no VMWRITE enablement, no VMX execution unit, no launch/resume path, and no successful VMX retire backend.

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

- `VmxMemoryOwnedVmReadValueProjectionTests`;
- `VmxCompatibilityControlOwnerDesignTests`.

Verified:

- VPID projects from neutral `AddressSpaceTag`.
- VPID denies when address-space tagging is not materialized.
- `HostCr3` and `Cr3TargetCount` remain denied.
- compatibility-control fields remain denied even though a neutral owner skeleton exists.
- source fences reject VMCS field-store and manager/backend markers.

Build/test/static results:

- production build: passed (`HybridCPU_ISE.csproj --no-restore`, existing warnings only).
- tests build: passed (`HybridCPU_ISE.Tests.csproj --no-restore`, existing warnings only).
- console compatibility build: passed (`TestAssemblerConsoleApps.csproj --no-restore`, existing obsolete-constructor warnings only).
- `VmxMemoryOwnedVmReadValueProjectionTests`: 7 passed.
- `VmxCompatibilityControlOwnerDesignTests`: 3 passed.
- `VmxGeneratedReadOnlyVmReadValueProjectionTests`: 5 passed.
- `RuntimeBoundaryAdmissionTests`: 4 passed.
- `VmxFirstAdmittedCompatibilityPathTests`: 1 passed.
- `VmxProjectionSchemaAndQuarantineTests`: 1 passed.
- `VmxCompatibilityProjectionInventoryTests`: 1 passed.
- broad VMX excluding NonVmx: 229 passed.
- forbidden marker scan over production virtualization excluding conformance: no matches.
- `Virtualization/Substrate` project include scan: no matches.
- `Legacy/VMX` C# scan: no files.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` scan: no files.
- `TestAssemblerConsoleApps` scan for `core.VmxUnit`, `core.Vmcs`, and `vmxUnit`: no matches.

## Residual Risk

The neutral compatibility-control owner is intentionally not materialized yet. Future work must define real neutral control semantics before opening any generated control field. `HostCr3` and `Cr3TargetCount` still need explicit neutral semantics or must remain denied.

## Next Heavy Step

Materialize the neutral compatibility-control owner only if real runtime control policy exists, then admit a small control-field VMREAD slice with conformance. Otherwise continue with residual memory-owned fields (`HostCr3`, `Cr3TargetCount`) and keep them denied until their neutral value source is explicit.
