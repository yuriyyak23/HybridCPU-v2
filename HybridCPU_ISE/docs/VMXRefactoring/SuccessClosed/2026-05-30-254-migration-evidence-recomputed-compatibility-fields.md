# Closure 254: Migration Evidence For Recomputed Compatibility Fields

Date: 2026-05-30

## Selected Slice

Chose the evidence-hardening path:

- do not open another VMREAD value field;
- do not add production migration/checkpoint authority;
- prove completion-owned compatibility fields are recomputed projection values only;
- prove checkpoint/migration images do not serialize VMCS projection data, host-owned evidence, or completion projection values as authority.

## Fields Covered

The covered VMREAD fields are the completion-owned slice:

- `ExitReason`
- `ExitQualification`
- `GuestPhysicalAddress`
- `EptViolationQualification`

They remain generated/read-only projections over neutral `CompletionRecord` and `CompletionProjectionService` after runtime admission.

## Migration/Evidence Contract

The generated schema marks all covered fields as:

- owner: `CompletionRecord`;
- evidence: `CompatibilityAlias`;
- access: `ReadOnly`;
- migration policy: `RecomputedCompletion`;
- write access: denied.

`MigrationPayloadClass` intentionally has no payload class for `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, `EptViolationQualification`, `CompletionRecord`, `VmxCompletionProjection`, `VmExitReason`, or VMCS fields.

## What Can Restore

Neutral guest architectural state can restore only through:

- `DomainCheckpointImage`;
- `MigrationValidationPolicy`;
- `RestoreValidationService`;
- `EvidenceRestorePolicy.PreserveGuestArchitecturalState`.

Compatibility projection metadata and compatibility-projection checkpoint authority are rejected as authoritative restore state.

Host-owned runtime evidence, scheduler evidence, backend binding evidence, and native token evidence remain recompute-only and are rejected from checkpoint restore.

## Why No VMCS Field Store Appeared

No VMCS scalar cache, VMCS field store, active VMCS pointer, VMCS manager, VMX runtime manager, mutable projection object, or backend execution path was added.

The conformance source fence checks that migration/checkpoint/evidence sources do not depend on:

- `CompletionRecord`;
- `CompletionProjectionService`;
- `VmxCompletionProjection`;
- `VmcsReadOnlyValueProjectionService`;
- `AdmitVmReadProjection`;
- `VmcsV2Descriptor`;
- `VmcsField`;
- `VmExitReason`;
- VMCS field-store APIs;
- `VmxExecutionUnit`;
- `VmcsManager`;
- `IVmcsManager`.

It also checks that completion projection sources do not expose migration or checkpoint authority.

## Conformance Added

Added:

- `VmxMigrationEvidenceRecomputedCompatibilityFieldTests`.

The tests prove:

- completion-owned VMREAD fields are `RecomputedCompletion`;
- projected completion values do not become checkpoint payload classes;
- neutral guest architectural state can restore under explicit preserve policy;
- compatibility projection metadata and compatibility checkpoint authority are rejected;
- host-owned evidence is recompute-only and rejected from checkpoint restore;
- migration/checkpoint/evidence code has no completion projection or VMREAD projection dependency;
- completion projection code has no migration/checkpoint authority.

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

- `VmxMigrationEvidenceRecomputedCompatibilityFieldTests`: passed, 6 tests.
- `VmxDescriptorReadinessPolicyAuditTests`: passed, 5 tests.
- `VmxGeneratedReadOnlyVmReadValueProjectionTests` plus `VmxMemoryOwnedVmReadValueProjectionTests`: passed, 15 tests.
- `VmxCompatibilityProjectionInventoryTests`, `VmxProjectionSchemaAndQuarantineTests`, `RuntimeBoundaryAdmissionTests`, and `VmxFirstAdmittedCompatibilityPathTests`: passed, 7 tests.
- `VmxControlLikeVmReadDenialTests` plus `VmxExecutionOwnedVmReadValueProjectionTests`: passed, 12 tests.
- `VmxTrapProjectionPublicationFenceTests` plus `VmxTrapCompletionRouteOwnerTests`: passed, 8 tests.

Broad VMX:

- `FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx`: passed, 264 tests.

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

Future migration work must not add broad descriptor/policy payloads by serializing compatibility projection objects. Any new migratable payload requires a neutral runtime owner, explicit serializable state contract, evidence policy, restore policy, and conformance.

## Next Heavy Step

Continue VMREAD field-by-field only where an explicit neutral owner/value source already exists, or design neutral nested child-intent/materialized descriptor payloads only when a real admitted nested/migration path requires them.
