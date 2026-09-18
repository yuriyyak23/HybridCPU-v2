# Closed: execution domain descriptor authority

Date: 2026-05-24

## Rule / basis

Execution authority must belong to execution-domain substrate, not to VMX/VMCS state. VMX remains a compatibility frontend that can project into this descriptor boundary.

## Changed

- Added immutable `ExecutionDomainDescriptor` state for domain tag, bundle legality, scheduling budget, execution extension, and compatibility projection flag.
- Added explicit `IsAuthoritativeExecutionStateOwner` classifier.
- Added helper properties for legality, scheduling, and extension presence.
- Added a compatibility projection toggle helper.
- No VMX opcode handling, VMCS ABI, decoder, encoder, or RTL behavior was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
