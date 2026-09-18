# 111. Composed-domain projection wording

Date: 2026-05-24

## Rule / basis

- Nested model should describe domain composition as child-domain intent plus composed-domain projection.
- VMCS02 terminology should stay in compatibility glossary/projection surfaces, not in substrate-facing composition diagnostics.
- Local API names should avoid implying VMCS12 authority when the object is already a `ChildDomainIntentDescriptor`.

## What changed

- Renamed local `vmcs12` parameters in `ComposedDomainProjectionComposer` to `childIntent`.
- Reworded the fail-closed mandatory control diagnostic from VMCS02 composition to composed-domain projection.
- Reworded NPT-specific diagnostic text to generic mandatory translation control.

## How verified

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Main project build: succeeded, 0 errors, 54 warnings.
