# Closed: compiler no-emission regression gate

Date: 2026-05-24

## Rule / basis

VMX is a compatibility frontend, not an architectural substrate. VMX-aware compiler boundaries must not emit host-owned evidence, native Lane6/Lane7 tokens, or direct substrate bypasses as guest-visible architectural code. Descriptor validation must precede compatibility projection emission.

## Changed

- Replaced the empty `VirtualizationNoEmissionRegressionGate` placeholder with a minimal fail-closed compiler-boundary gate.
- Added `VirtualizationEmissionRequest` to carry compatibility frontend, generated projection, descriptor validation, host evidence, and native lane-token facts.
- Added `VirtualizationEmissionDecision` so no-emission regressions fail closed with explicit reasons.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
