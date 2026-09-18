# 110. Nested exit child-domain intent alias

Date: 2026-05-24

## Rule / basis

- Nested model should move toward child-domain intent and domain composition terminology.
- VMCS12 naming may remain as compatibility vocabulary, but substrate-adjacent APIs should expose generic aliases.
- Unsupported nested operation diagnostics should not imply that VMX is the architectural authority.

## What changed

- Added `NestedExitMapper.InvalidChildDomainIntent(...)`.
- Kept `NestedExitMapper.InvalidVmcs12(...)` as a compatibility alias.
- Reworded unsupported nested operation text from VMX operation to nested compatibility operation.

## How verified

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Main project build: succeeded, 0 errors, 54 warnings.
