# Closure 240: Generated Read-Only VMREAD Value Projection

Date: 2026-05-28

## Slice Chosen

Closed the first honest VMREAD value slice for completion-owned fields only.

The admitted path is now:

```text
VMREAD(field)
  -> VMREAD decode
  -> frozen opcode alias projection validation
  -> RuntimeBoundaryAdmissionService(ReadCompatibilityProjection)
  -> VmcsFieldProjectionSchema owner lookup
  -> VmcsReadOnlyValueProjectionService
  -> CompletionRecord / CompletionProjectionService
  -> generated/read-only compatibility value projection
```

No VMCS field store, active VMCS pointer, VMCS manager, or backend VMREAD execution was introduced.

## Dependencies Found

- `VmxCompatibilityAdmissionService.AdmitVmReadProjection`
- `VmxCompatibilityVmReadAdmissionRequest` / `VmxCompatibilityVmReadAdmissionResult`
- `RuntimeBoundaryAdmissionService`
- `DomainRuntimeOperationKind.ReadCompatibilityProjection`
- `VmcsFieldProjectionSchema`
- `VmcsFieldAliasProjection`
- `VmcsFieldProjectionOwner`
- `CompletionRecord`
- `CompletionProjectionService`
- `EvidenceVisibilityClass`
- existing denied `VmcsV2Descriptor.TryReadScalarField`
- active conformance: `VmxFirstAdmittedCompatibilityPathTests`, `VmxCompatibilityProjectionInventoryTests`, `RuntimeBoundaryAdmissionTests`

## Field Classification

Projected in this closure:

- `ExitReason`: owner `CompletionRecord`, value from `CompletionProjectionService.ProjectToVmx(record).ExitReason`.
- `ExitQualification`: owner `CompletionRecord`, value from `CompletionProjectionService.ProjectToVmx(record).ExitQualification`.
- `GuestPhysicalAddress`: owner `CompletionRecord`, value from `CompletionProjectionService.ProjectToVmx(record).GuestPhysicalAddress`.
- `EptViolationQualification`: owner `CompletionRecord`, value from `CompletionProjectionService.ProjectToVmx(record).EptViolationQualification`.

Still denied/fail-closed:

- `GuestPc`, `GuestSp`, `GuestFlags`, `GuestCr0`, `GuestCr4`, host execution aliases: owner metadata exists, but `ExecutionDomainDescriptor` does not currently expose a concrete VMREAD value source.
- `GuestCr3`, `HostCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount`: owner metadata exists, but this closure did not admit a memory-owned value source.
- `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, `SecondaryProcControls`: compatibility-control owner metadata exists, but no neutral value source is admitted.
- unknown or ungenerated fields: denied by generated schema lookup.
- completion-owned fields without a neutral `CompletionRecord` compatibility projection source: denied.
- all writes: still denied by alias/access policy.

## Why This Is Not A VMCS Store

`VmxCompatibilityAdmissionService` now routes admitted VMREAD value projection to `VmcsReadOnlyValueProjectionService`; it does not fall back to `VmcsV2Descriptor.TryReadScalarField` for value materialization. `TryReadScalarField` remains a denied compatibility ABI on the descriptor and no mutable scalar cache, field dictionary, active pointer, or manager was added.

## Why VMREAD Is Still Not Backend Execution

The only successful values are read-only projections of an already-neutral `CompletionRecord` compatibility projection source after runtime admission. This does not execute VMREAD in a backend, does not publish retire effects, does not make VMX the virtualization architecture, and does not admit descriptor-owned or memory-owned scalar state.

## Files Changed

- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/ActiveVmxCompatibilityConformanceTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxGeneratedReadOnlyVmReadValueProjectionTests.cs`
- `docs/VMXRefactoring/audit3.md`
- `docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`
- `docs/VMXRefactoring/ОСНОВЫ и ПРАВИЛА VMX.md`
- this closure file

## Build, Test, Static Results

Baseline before changes:

- no legacy VMX production authority found by the requested baseline scans;
- no `VmxExecutionUnit.cs`, `VmcsManager.cs`, or `IVmcsManager.cs`;
- no `Virtualization\Substrate` project placeholders;
- production build passed;
- tests build passed;
- `TestAssemblerConsoleApps` build passed.

Post-change:

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed.
- `dotnet build TestAssemblerConsoleApps.csproj --no-restore`: passed with the pre-existing obsolete-constructor warnings in `WhiteBookContractDiagnostics.cs`.
- `VmxFirstAdmittedCompatibilityPathTests`: passed, 1/1.
- `RuntimeBoundaryAdmissionTests`: passed, 4/4.
- `VmxProjectionSchemaAndQuarantineTests`: passed, 1/1 active conformance test.
- `VmxCompatibilityProjectionInventoryTests`: passed, 1/1 active conformance test.
- `VmxGeneratedReadOnlyVmReadValueProjectionTests`: passed, 5/5.
- broad `FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx`: passed, 219/219.

Static:

- exact requested forbidden-marker scan over `CloseToHSL/Core/Virtualization` reports only pre-existing conformance contract string evidence under `CloseToHSL/Core/Virtualization/Conformance`.
- production scan excluding `**/Conformance/**`: clean.
- `Virtualization\Substrate` project scan: clean.

## Residual Risk

- Execution-owned fields need real guest/host architectural value sources under `ExecutionDomainDescriptor` before any VMREAD projection can expose them.
- Memory-owned fields need explicit `MemoryDomainDescriptor` projection policy before exposing CR3/EPT/VPID-style compatibility aliases.
- Compatibility-control fields need a neutral control descriptor before projection.
- Existing conformance sources still carry forbidden marker strings as static evidence; production compatibility/projection sources remain clean.

## Next Heavy Step

Choose the next field-family only after a neutral owner exposes a value source. The strongest candidate is a small `MemoryDomainDescriptor` projection slice for fields that can be proven to map directly to `MemoryDomainTranslationControl`, or a neutral compatibility-control descriptor if control-field values become runtime-owned first.
