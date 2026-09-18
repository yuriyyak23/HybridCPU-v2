# Success Closure 241: Memory-Owned VMREAD Value Projection Slice

Date: 2026-05-29

## Selected Slice

Closed the next VMREAD neutral value-source expansion for two memory-owned generated fields only:

- `GuestCr3`
- `EptPointer`

This is an incremental extension of closure `240`, not broad VMREAD execution.

## VMREAD / Projection Dependencies Found

The active path is:

```text
VMREAD(field)
  -> VMREAD decode
  -> frozen alias projection validation
  -> RuntimeBoundaryAdmissionService(ReadCompatibilityProjection)
  -> VmcsFieldProjectionSchema owner lookup
  -> MemoryDomainDescriptor owner
  -> field-specific evidence policy
  -> generated/read-only compatibility value projection
```

Relevant implementation points:

- `VmxCompatibilityAdmissionService.AdmitVmReadProjection`
- `VmxCompatibilityVmReadAdmissionRequest`
- `VmcsReadOnlyValueProjectionService`
- `VmcsReadOnlyValueProjectionRequest`
- `VmcsFieldProjectionSchema`
- `VmcsFieldAliasProjection`
- `VmcsFieldProjectionOwner.MemoryDomainDescriptor`
- `EvidenceVisibilityClass.GuestArchitecturalState`
- `EvidenceVisibilityClass.CompatibilityAlias`
- `RuntimeBoundaryAdmissionService`
- `ReadCompatibilityProjection`

## Projected Fields

`GuestCr3` is projected from:

```text
MemoryDomainDescriptor
  -> TryCreateReadOnlyTranslationView()
  -> MemoryDomainTranslationControl.AddressSpaceRoot
```

`EptPointer` is projected from:

```text
MemoryDomainDescriptor
  -> TryCreateReadOnlyTranslationView()
  -> OwnsSecondStageTranslation == true
  -> MemoryDomainTranslationControl.SecondStageRoot
```

The neutral value carrier is `MemoryDomainReadOnlyTranslationView` under `Core/Runtime/Memory/Translation`.

## Still Denied / Fail-Closed

The following generated fields remain denied because this closure does not admit an explicit neutral value source for them:

- `HostCr3`
- `Vpid`
- `Cr3TargetCount`
- execution-owned fields such as `GuestPc`, `GuestSp`, `GuestFlags`, `GuestCr0`, `GuestCr4`
- compatibility-control fields such as `PinBasedControls`, `ProcBasedControls`, `SecondaryProcControls`, `ExitControls`, `EntryControls`
- unknown fields
- all writes

Invalid memory translation control is denied before value projection.

## Why No VMCS Field Store Appeared

No value is read from `VmcsV2Descriptor`, VMCS projection blocks, active VMCS pointer state, scalar caches, or mutable VMCS objects.

`VmxCompatibilityAdmissionService` passes only the admitted runtime context memory descriptor to `VmcsReadOnlyValueProjectionService`. The service then dispatches by generated schema owner metadata and uses the neutral descriptor's explicit read-only translation view.

`TryReadScalarField` remains a denied compatibility ABI and is not used by the admitted value path.

## Why This Is Not Feature-Complete VMX Backend Execution

The admitted path still only projects read-only compatibility values after neutral runtime admission and evidence checks.

It does not:

- execute VMX backend semantics;
- create successful VMREAD retire/writeback behavior beyond the compatibility admission result;
- enable VMWRITE;
- publish completion or retire effects;
- admit VMCALL backend execution;
- introduce nested VMX execution.

## Absence Checks

No `VmxExecutionUnit.cs`, `VmcsManager.cs`, or `IVmcsManager.cs` was restored.

No `VmcsManagerAdapter`, `VmxRuntimeManager`, `VmcsProjectionRuntimeManager`, or `VmcsV2RuntimeManager` was introduced.

No active VMCS pointer state or VMCS field store was introduced.

## Tests / Static Results

Builds:

```text
dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore
  Passed

dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore
  Passed

dotnet build TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --no-restore
  Passed
```

Targeted tests:

```text
VmxMemoryOwnedVmReadValueProjectionTests
  Passed: 6

VmxGeneratedReadOnlyVmReadValueProjectionTests
  Passed: 5

RuntimeBoundaryAdmissionTests
  Passed: 4

VmxFirstAdmittedCompatibilityPathTests
  Passed: 1

VmxProjectionSchemaAndQuarantineTests
  Passed: 1

VmxCompatibilityProjectionInventoryTests
  Passed: 1
```

Broad VMX:

```text
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx"
  Passed: 225
```

Static:

```text
rg forbidden-manager/backend markers under CloseToHSL/Core/Virtualization
  Only historical conformance-string evidence matched.

rg forbidden-manager/backend markers under CloseToHSL/Core/Virtualization excluding **/Conformance/**
  No matches.

rg Virtualization\Substrate project markers
  No matches.

rg VmxExecutionUnit.cs / VmcsManager.cs / IVmcsManager.cs
  No matches.

rg Legacy/VMX *.cs
  No matches.
```

Note: an initial parallel test build hit a transient `VBCSCompiler` file lock on `obj/testsupport`; the sequential rerun passed.

## Residual Risk

`Vpid` has a plausible neutral source in `MemoryDomainTranslationControl.AddressSpaceTag`, but its compatibility value semantics need a separate explicit rule around disabled tagging and zero/nonzero projection before it can be honestly admitted.

`HostCr3` still has no neutral host-side read-only value source in this VMREAD projection path.

`Cr3TargetCount` still has no neutral CR3 target-list/count owner.

Execution-owned and compatibility-control fields still need explicit neutral descriptor/control owners before projection.

## Next Heavy Step

Continue VMREAD expansion only field-by-field:

1. Define explicit neutral value semantics for `Vpid` or keep it denied.
2. Introduce a neutral compatibility-control owner before projecting control fields.
3. Add execution-owned descriptor values only if `ExecutionDomainDescriptor` exposes read-only materialized state.
4. Keep runtime-owned trap completion route design separate from VMREAD value projection.
