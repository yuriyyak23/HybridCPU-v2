# Closure 253: Descriptor Readiness Policy Audit

Date: 2026-05-30

## Selected Slice

Chose the fail-closed audit path:

- no new VMREAD value field opened;
- no production readiness authority added;
- migration/nested/restore readiness stays separate from VMREAD projection values;
- conformance now proves readiness does not fall back to VMCS scalar state or compatibility projection metadata.

## Dependencies Audited

The audited readiness chain is:

```text
VmcsV2Descriptor.ValidateMigrationReadiness
VmcsV2Descriptor.ValidateNestedEnablementReadiness
MigrationValidationPolicy
RestoreValidationService
DomainCheckpointImage
NestedProjectionService
NestedDomainProjectionCheckpointService
```

The existing admitted VMREAD path is intentionally outside this authority chain.

## Readiness Decisions

Current fail-closed decisions remain:

- VMCSv2 migration readiness requires materialized guest GPR persistence and still returns `GuestGprPersistenceIncomplete` by default.
- VMCSv2 nested enablement readiness requires materialized guest GPR persistence first and otherwise remains blocked; after that, legacy Shadow VMCS remains removed and nested enablement must use neutral nested-domain projection.
- Restore rejects compatibility-projection checkpoints and compatibility projection metadata as authoritative state.
- Migration import requires explicit `PreserveGuestArchitecturalState` for guest architectural state.
- Host-owned evidence is rejected from checkpoint restore.
- Nested checkpoint readiness requires neutral projection admission, checkpoint image, migration policy, and restore/evidence policy.

## Why No VMREAD Value Opened

This closure does not add an owner/value source for any residual VMREAD field.

It only proves that opened neutral VMREAD values, such as `GuestPc` from `ExecutionDomainReadOnlyStateView`, cannot make descriptor migration/nested readiness successful. Readiness still requires materialized neutral state and migration/evidence policy, not compatibility projection values.

## Why No VMCS Field Store Appeared

No VMCS scalar cache, active VMCS pointer, VMCS manager, VMX runtime manager, field-store API, or mutable projection object was added.

The conformance source fence checks that readiness does not call:

- `AdmitVmReadProjection`;
- `VmcsReadOnlyValueProjectionService`;
- `TryReadScalarField`;
- `ReadFieldValue`;
- `WriteFieldValue`;
- `HardwareWrite`;
- `DirectWrite`;
- `VmxExecutionUnit`;
- `VmcsManager`;
- `IVmcsManager`.

## Conformance Added

Added:

- `VmxDescriptorReadinessPolicyAuditTests`.

The tests prove:

- admitted `GuestPc` VMREAD projection does not affect VMCSv2 readiness;
- default migration/nested readiness remains `GuestGprPersistenceIncomplete`;
- compatibility projection metadata cannot restore authoritative state;
- guest state import requires preserve restore policy;
- host-owned evidence is rejected;
- nested checkpoint readiness requires neutral projection/checkpoint/policy gates;
- readiness sources do not depend on VMREAD projection services or VMCS field stores.

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

- `VmxDescriptorReadinessPolicyAuditTests`: passed, 5 tests.
- `VmxFirstAdmittedCompatibilityPathTests`: passed, 1 test.
- `RuntimeBoundaryAdmissionTests`: passed, 4 tests.
- `VmxProjectionSchemaAndQuarantineTests`: passed, 1 test.
- `VmxCompatibilityProjectionInventoryTests`: passed, 1 test.
- `VmxControlLikeVmReadDenialTests`: passed, 4 tests.
- `VmxExecutionOwnedVmReadValueProjectionTests`: passed, 8 tests.
- `VmxGeneratedReadOnlyVmReadValueProjectionTests` plus `VmxMemoryOwnedVmReadValueProjectionTests`: passed, 15 tests.

Broad VMX:

- `FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx`: passed, 258 tests.

Static:

- Full `CloseToHSL/Core/Virtualization` forbidden-authority scan found only pre-existing conformance/static-evidence deny-list string literals.
- Production scan excluding `Conformance` found no forbidden authority markers.
- `HybridCPU_ISE.csproj` has no `Virtualization/Substrate` or `Virtualization\Substrate` entries.
- `TestAssemblerConsoleApps` has no `core.VmxUnit`, `core.Vmcs`, or `vmxUnit` references.
- No `VmxExecutionUnit.cs`, `VmcsManager.cs`, or `IVmcsManager.cs` files exist outside `CloseToHSL`.
- `Legacy/VMX` has no C# files.
- `rg -il "legacy" CloseToHSL/Core/Virtualization --glob "*.cs"` returned no production legacy markers.
- `git diff --check` passed with only existing LF-to-CRLF normalization warnings.

## Residual Risk

Future work must not turn migration/nested readiness into a convenience consumer of compatibility projection values. Any successful readiness path needs explicit neutral materialized state, migration policy, evidence policy, and conformance before it can open.

## Next Heavy Step

Continue VMREAD field-by-field only where an explicit neutral owner/value source already exists, or prove migration/evidence handling for recomputed compatibility fields without serializing VMCS projection data or host-owned evidence as authority.
