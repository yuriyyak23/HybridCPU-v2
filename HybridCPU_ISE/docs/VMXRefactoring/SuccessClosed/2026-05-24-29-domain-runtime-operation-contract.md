# Closed: domain runtime operation contract

Date: 2026-05-24

## Rule / basis

VMX execution must become a compatibility frontend that translates VMX instructions into generic domain/runtime operations. VMX opcodes remain frozen ABI names, but the semantic operation vocabulary must be domain/runtime-oriented.

## Changed

- Added `DomainRuntimeOperationKind` for activate/deactivate, enter/resume, projection read/write, invalidation, capability invocation, save, and restore operations.
- Added `DomainRuntimeOperationSource` to distinguish compatibility frontend, runtime service, and migration replay origins.
- Added `DomainRuntimeOperation` properties and helper construction from compatibility frontend.
- Added a fail-safe `CanMutateAuthoritativeState` classifier for projection-only and migration replay paths.
- No VMX handler, VMCS ABI, decoder, encoder, or RTL behavior was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
