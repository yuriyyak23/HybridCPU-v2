# Closure 255: Execution Read-Only State View Hardening

Date: 2026-05-30

## Selected Slice

The external audit's safe execution snapshot step was already implemented by closure `250`.

This closure therefore hardens the existing neutral source instead of reopening the same VMREAD fields:

- keep `GuestPc`, `GuestSp`, and `GuestFlags` as the only execution-owned VMREAD values;
- add explicit materialization metadata to the neutral execution snapshot;
- prove snapshot metadata does not become a VMREAD value;
- keep `GuestCr0` and `GuestCr4` denied.

## Code Changes

Updated:

- `ExecutionDomainReadOnlyStateView`.

Added neutral metadata:

- `StateEpoch`;
- `IsMaterialized`;
- `HasCompleteGuestPcSpFlags`.

The static factory `FromGuestPcSpFlags` can carry a neutral `stateEpoch`, and `Unmaterialized` remains epoch `0` with no materialized guest state.

## Conformance Changes

Updated:

- `VmxExecutionOwnedVmReadValueProjectionTests`.

The tests now prove:

- unmaterialized state has no materialized snapshot and epoch `0`;
- a materialized safe snapshot can carry a neutral `StateEpoch`;
- the view contains only guest PC/SP/flags and no guest control-register or host execution fields;
- `GuestPc`, `GuestSp`, and `GuestFlags` still project from the neutral view;
- `GuestCr0` and `GuestCr4` remain `PrivilegedExecutionStateProjectionDenied`;
- `VmcsReadOnlyValueProjectionService` does not read or project `StateEpoch`.

## Why No VMCS Field Store Appeared

No VMCS scalar cache, VMCS field store, active VMCS pointer, VMCS manager, VMX runtime manager, legacy helper, mutable projection store, or backend VMREAD path was added.

`StateEpoch` is neutral descriptor metadata only. It is not mapped to a VMCS field and is not accepted as a fallback value source.

## Why Privileged Execution Fields Stay Denied

`GuestCr0` and `GuestCr4` need a separate neutral privileged execution-state owner/value contract. The safe PC/SP/flags snapshot is not allowed to stand in for privileged control-register semantics.

## Documentation

Updated:

- `audit3.md`;
- `audit4.md`;
- `audit5.md`;
- `2026-05-24-vmx-current-model-completion-audit.md`;
- `ОСНОВЫ и ПРАВИЛА VMX.md`.

## Verification

Builds:

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed with 54 pre-existing broader project warnings.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, 0 warnings.
- `dotnet build TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --no-restore`: passed with 2 pre-existing obsolete-constructor warnings.

Focused tests:

- `VmxExecutionOwnedVmReadValueProjectionTests`: passed, 9 tests.
- `VmxControlLikeVmReadDenialTests`: passed, 4 tests.
- `VmxGeneratedReadOnlyVmReadValueProjectionTests` plus `VmxMemoryOwnedVmReadValueProjectionTests`: passed, 15 tests.
- `VmxDescriptorReadinessPolicyAuditTests` plus `VmxMigrationEvidenceRecomputedCompatibilityFieldTests`: passed, 11 tests.
- `VmxFirstAdmittedCompatibilityPathTests`, `RuntimeBoundaryAdmissionTests`, `VmxProjectionSchemaAndQuarantineTests`, and `VmxCompatibilityProjectionInventoryTests`: passed, 7 tests.

Broad VMX:

- `FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx`: passed, 265 tests.

Static:

- Full `CloseToHSL/Core/Virtualization` forbidden-authority scan found only pre-existing conformance/static-evidence deny-list string literals.
- Production scan excluding `Conformance` found no forbidden authority markers.
- `HybridCPU_ISE.csproj` has no `Virtualization/Substrate` or `Virtualization\Substrate` entries.
- `TestAssemblerConsoleApps` has no `core.VmxUnit`, `core.Vmcs`, or `vmxUnit` references.
- No `VmxExecutionUnit.cs`, `VmcsManager.cs`, or `IVmcsManager.cs` files exist outside `CloseToHSL`.
- `Legacy/VMX` has no C# files.
- `rg -il "legacy" CloseToHSL/Core/Virtualization --glob "*.cs"` returned no production legacy markers.
- `git diff --check` passed with only LF-to-CRLF normalization warnings.

## Residual Risk

Future work must not treat execution snapshot metadata as VMREAD field data. `StateEpoch` is useful for neutral lifecycle tracking, but it is not compatibility ABI state.

## Next Heavy Step

The best next heavy step is a privileged execution-state owner design only if real neutral CR0/CR4 semantics are ready. Otherwise keep `GuestCr0` and `GuestCr4` denied and continue VMREAD field-by-field only where an explicit neutral owner/value source already exists.
