# 109. Lane7 domain ownership wording

Date: 2026-05-24

## Rule / basis

- Lane6/Lane7/vector-stream ownership must be outside VMX/VMCS authority.
- VMFUNC and VMCS terms may remain at frozen compatibility edges, but substrate diagnostics should describe descriptor/domain ownership.
- Textual diagnostics are part of conformance/audit evidence and should not imply VMCS-owned Lane7 authority.

## What changed

- Updated Lane7 diagnostics in `Lane7StateBlock.cs`:
  - VMCS ownership wording became Lane7 domain ownership.
  - VMCS Lane7 virtualization ownership became descriptor-owned Lane7 virtualization ownership.
  - VMCS fast-path leaf map became descriptor-owned fast-path leaf map.

## How verified

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Main project build: succeeded, 0 errors, 54 warnings.
