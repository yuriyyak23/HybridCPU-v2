# 112. Generated projection descriptor-owned diagnostics

Date: 2026-05-24

## Rule / basis

- Generated/read-only VMCS projection may keep VMCS ABI naming, but diagnostics should not imply that VMCS owns event queues or vector-stream policy.
- Event routing and vector-stream legality are descriptor/runtime-owned substrate concerns.
- Compatibility projection text should preserve behavior while pointing audit evidence at descriptor authority.

## What changed

- Reworded lane completion event routing diagnostics in `VmcsV2Blocks.cs` from VMCS event injection queue to descriptor-owned event injection queue.
- Reworded vector/stream save mask denial from VMCSv2 vector/stream policy to descriptor-owned vector/stream policy.

## How verified

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Main project build: succeeded, 0 errors, 54 warnings.
