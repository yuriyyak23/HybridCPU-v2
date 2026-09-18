# Closure 252: Control-Like VMREAD Fields Kept Denied

Date: 2026-05-29

## Selected Slice

Chose the fail-closed option:

- do not open another VMREAD value field;
- keep remaining control-like aliases denied unless a real neutral owner/value source exists;
- add conformance so this cannot regress silently.

## Fields Covered

The guarded remaining control-like fields are:

- `GuestCr0`
- `GuestCr4`
- `HostCr0`
- `HostCr3`
- `PinBasedControls`
- `ProcBasedControls`
- `ExitControls`
- `EntryControls`
- `SecondaryProcControls`

`GuestCr3` is not part of this denied residual set because it already has a neutral memory-domain source from closure `241`.

## Current Decisions

The explicit VMREAD value-projection decisions are:

- `GuestCr0` -> `PrivilegedExecutionStateProjectionDenied`
- `GuestCr4` -> `PrivilegedExecutionStateProjectionDenied`
- `HostCr0` -> `HostExecutionStateOwnerMissing`
- `HostCr3` -> `HostAddressSpaceOwnerMissing`
- `PinBasedControls` -> `CompatibilityControlValueProjectionDenied`
- `ProcBasedControls` -> `CompatibilityControlValueProjectionDenied`
- `ExitControls` -> `CompatibilityControlValueProjectionDenied`
- `EntryControls` -> `CompatibilityControlValueProjectionDenied`
- `SecondaryProcControls` -> `CompatibilityControlValueProjectionDenied`

## Why No New Value Opened

No neutral owner currently exposes a read-only value source for these fields:

- `GuestCr0` and `GuestCr4` need neutral privileged execution-state semantics.
- `HostCr0` needs a neutral host-execution owner.
- `HostCr3` needs a neutral host-address-space owner.
- Compatibility-control fields need a separate neutral control-bit value contract.

The existing opened sources are not reused:

- `ExecutionDomainReadOnlyStateView` is guest PC/SP/flags only.
- `MemoryDomainReadOnlyTranslationView.AddressSpaceRoot` is guest/domain CR3 only.
- `CompatibilityControlDescriptor` intentionally keeps control values unprojected.

## Conformance Added

Added:

- `VmxControlLikeVmReadDenialTests`.

The tests prove:

- all covered fields return `ReadOnlyProjectionDenied`;
- each field returns the correct explicit denial decision;
- runtime admission is reached before the denial;
- schema owner/evidence metadata remains correct;
- schema access remains read-only and write-denied;
- opened neutral sources are not reused as missing control-like authority;
- there is no VMCS field store, control-bit mapper, VMCS manager, or VMX execution unit fallback.

## Why No VMCS Field Store Appeared

No VMCS scalar store, active VMCS pointer, mutable projection object, VMCS manager, host fake value, control-bit mapper, or fallback read path was added.

The denial is not based on:

- `VmcsV2Descriptor`;
- `VmcsV2Blocks`;
- scalar cache;
- VMCS block;
- test-only behavior;
- legacy control helpers.

## Why VMREAD Is Still Not Feature-Complete Backend Execution

This closure only strengthens fail-closed conformance. VMREAD remains generated/read-only compatibility projection for fields with explicit neutral value sources. Unsupported control-like aliases remain denied.

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
- `dotnet build TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --no-restore`: passed, 0 warnings.

Focused tests:

- `VmxControlLikeVmReadDenialTests`: passed, 4 tests.
- `VmxExecutionOwnedVmReadValueProjectionTests`: passed, 8 tests.
- `VmxGeneratedReadOnlyVmReadValueProjectionTests`: passed, 5 tests.
- `VmxMemoryOwnedVmReadValueProjectionTests`: passed, 10 tests.
- `VmxCompatibilityControlOwnerDesignTests`: passed, 4 tests.
- `VmxFirstAdmittedCompatibilityPathTests`: passed, 1 test.
- `RuntimeBoundaryAdmissionTests`: passed, 4 tests.
- `VmxProjectionSchemaAndQuarantineTests`: passed, 1 test.
- `VmxCompatibilityProjectionInventoryTests`: passed, 1 test.

Broad VMX:

- `FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx`: passed, 253 tests.

Static:

- Full `CloseToHSL/Core/Virtualization` forbidden-authority scan found only pre-existing conformance/static-evidence contract string literals that name forbidden markers as deny-list entries.
- Production scan excluding `Conformance` found no forbidden authority markers.
- `HybridCPU_ISE.csproj` has no `Virtualization/Substrate` or `Virtualization\Substrate` entries.
- `TestAssemblerConsoleApps` has no `core.VmxUnit`, `core.Vmcs`, or `vmxUnit` references.
- No `VmxExecutionUnit.cs`, `VmcsManager.cs`, or `IVmcsManager.cs` files exist outside `CloseToHSL`.
- `Legacy/VMX` has no C# files.
- `rg -il "legacy" CloseToHSL/Core/Virtualization --glob "*.cs"` returned no production legacy markers.

## Residual Risk

Future work must not open these fields by inferring values from existing projection objects. Each field requires an explicit neutral owner/value source and a new conformance slice before it can project a value.

## Next Heavy Step

Continue VMREAD field-by-field only through explicit neutral owners, or audit descriptor readiness policy. Do not open control-like fields until neutral semantics and value sources exist.
