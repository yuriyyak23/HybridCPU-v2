# Validation Baseline

## Purpose

This document defines the current Phase 06 smoke baseline for the live repository.
It is the authoritative runnable proof subset for validation claims that must remain reproducible while the broader evidence layer is still being refreshed.
For the wider claim-to-code-to-test crosswalk, see `Documentation/paper-claim-evidence-map.md`; this file fixes only the reproducible smoke subset and its runtime-harness cross-check.

## Runner policy

- `global.json` currently declares the repository test runner as `VSTest`.
- `HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj` currently uses the matching `Microsoft.NET.Test.Sdk` plus `xunit.runner.visualstudio` stack.
- The baseline command therefore uses `dotnet test`, not the older `dotnet build` + `dotnet vstest` workaround.

## Authoritative command

```powershell
powershell -ExecutionPolicy Bypass -File .\build\run-validation-baseline.ps1 -NoRestore
```

Equivalent direct command:

```powershell
dotnet test .\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter "FullyQualifiedName~Phase12CompilerContractHandshakeTests|FullyQualifiedName~Phase09RuntimeLegalityServiceReachabilityProofTests|FullyQualifiedName~Phase09ReplayCertificateCoordinatorProofTests|FullyQualifiedName~Phase09ExplicitPacketAssistLaneTests|FullyQualifiedName~Phase09WriteBackFaultOrderingProofTests|FullyQualifiedName~DeterminismEnvelopeTests|FullyQualifiedName~DomainIsolationStressTests|FullyQualifiedName~Phase09CanonicalDecodePublicationContractTests|FullyQualifiedName~Phase11NativeVliwDiagnosticDecodeTests|FullyQualifiedName~Phase12VliwCompatFreezeTests" --no-restore -v minimal
```

## Baseline suite inventory

- Compiler handshake: `Phase12CompilerContractHandshakeTests`
- Runtime legality reachability: `Phase09RuntimeLegalityServiceReachabilityProofTests`
- Replay certificate coordination: `Phase09ReplayCertificateCoordinatorProofTests`
- Assist lane semantics: `Phase09ExplicitPacketAssistLaneTests`
- Fault ordering: `Phase09WriteBackFaultOrderingProofTests`
- Determinism envelope: `DeterminismEnvelopeTests`
- Domain isolation: `DomainIsolationStressTests`
- Canonical decode publication contract: `Phase09CanonicalDecodePublicationContractTests`
- Native-VLIW diagnostic boundary: `Phase11NativeVliwDiagnosticDecodeTests`
- Compat freeze boundary: `Phase12VliwCompatFreezeTests`

## Observed result

Observed on 2026-04-18 from the live workspace:

- Result: passed
- Failed: `0`
- Passed: `51`
- Skipped: `0`
- Latest observed duration: `24 s`

Duration is environment-dependent and varies with build state.
Treat pass/fail, suite inventory, and runner policy as the authoritative baseline surface; treat runtime as informative only.

This baseline is intentionally a smoke subset, not a repository-wide pass total.
Its purpose is to keep the main architecture-proof seams reproducible while the broader evidence recount and document refresh continue.

## Runtime baseline cross-check

Phase 06 also requires the runtime harness to stay aligned with `Documentation/AsmAppTestResults.md`.
On 2026-04-18, `TestAssemblerConsoleApps` matched the recorded key values for the primary matrix:

| Mode | IPC | Retired | Cycles | Last reject kind | Authority | Slack reclaim ratio |
|---|---:|---:|---:|---|---|---:|
| `showcase` | `4.0238` | `169` | `42` | `CrossLaneConflict` | `StructuralCertificate` | `0.4130` |
| `vt` | `4.2000` | `168` | `40` | `CrossLaneConflict` | `StructuralCertificate` | `0.4255` |
| `novt` | `3.3571` | `188` | `56` | `None` | `StructuralCertificate` | `0.0000` |
| `alu` | `3.3571` | `188` | `56` | `None` | `StructuralCertificate` | `0.0000` |
| `max` | `4.2000` | `168` | `40` | `CrossLaneConflict` | `StructuralCertificate` | `0.4255` |
| `lk` | `4.2000` | `168` | `40` | `None` | `StructuralCertificate` | `0.8158` |
| `bnmcz` | `4.2000` | `168` | `40` | `None` | `StructuralCertificate` | `0.7867` |
| `replay` | `Succeeded` | - | - | - | - | - |

No baseline drift was observed in the key values checked against `Documentation/AsmAppTestResults.md`.

## What this baseline does not claim

- It does not assert a fresh full-suite pass count.
- It does not replace the live count surface in `Documentation/evidence-matrix.md` or `build/recount-validation-evidence.ps1`.
- It does not freeze every telemetry export field; it freezes only the current smoke proof surface and the runtime harness sanity check.
