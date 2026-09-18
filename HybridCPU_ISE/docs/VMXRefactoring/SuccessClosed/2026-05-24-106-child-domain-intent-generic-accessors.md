# 106. Child-domain intent generic accessors

Date: 2026-05-24

## Rule / basis

- Nested model should move toward domain composition and child-domain intent descriptors, not public VMCS12/VMCS02 substrate ownership.
- VMCS12 vocabulary may remain as compatibility projection naming, but substrate-adjacent APIs should offer generic descriptor terminology.
- Existing frozen/compatibility callers must keep working while generic boundaries are introduced.

## What changed

- Added `ChildIntentPointer` to `ChildDomainIntentDescriptor`.
- Kept `Vmcs12Pointer` as a compatibility alias over `ChildIntentPointer`.
- Added generic `TryReadIntentField(...)` and `TryWriteIntentField(...)` APIs.
- Kept `TryVmRead(...)` and `TryVmWrite(...)` as compatibility aliases.
- Reworded nested substrate messages from VMCS12-owned wording toward child-domain intent projection wording.
- Reworded one composed-domain unsupported-bits message to use child-domain intent terminology.

## How verified

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Main project build: succeeded, 0 errors, 54 warnings.
