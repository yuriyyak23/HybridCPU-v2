# Evidence Matrix

## Goal of this artifact

This matrix records the current smoke proof surface and the named tests that keep architecture-facing validation claims honest. It is intentionally a compact evidence index, not a full-suite pass claim.

## Current smoke baseline

- Runner policy: `VSTest`
- Command: `powershell -ExecutionPolicy Bypass -File .\build\run-validation-baseline.ps1 -NoRestore`
- Result: passed
- Failed: `0`
- Passed: `51`
- Skipped: `0`

## Recount automation

The live source and test declaration counts are checked by:

- `build/recount-validation-evidence.ps1`
- `HybridCPU_ISE.Tests/tests/Phase09ValidationEvidenceCountDocumentationTests.cs`

`Phase09ValidationEvidenceCountDocumentationTests` verifies that the WhiteBook validation chapter, this matrix, and the recount script stay aligned.

## Live evidence counts

| Surface | Count |
|---|---:|
| `HybridCPU_ISE/` `.cs` source files | `365` |
| `HybridCPU_Compiler/` `.cs` source files | `145` |
| `HybridCPU_ISE.Tests/tests/` `.cs` files | `260` |
| `HybridCPU_ISE.Tests/tests/` test declarations | `2160` |
| Full `HybridCPU_ISE.Tests/` `.cs` files | `368` |
| Full `HybridCPU_ISE.Tests/` test declarations | `3678` |

The test-declaration count is a live-text count of attribute declarations matching `^\s*\[(Fact|Theory)\b` in non-generated `.cs` files.

## Phase 02 evidence seams

- `Phase09LegalityPredicateDocumentationTests.cs`
- `Phase09CertificateSemanticsDocumentationTests.cs`
- `Phase09OperationalSemanticsDocumentationTests.cs`
- `Phase09RejectTaxonomyClosureTests.cs`
- `Phase09TypedSlotFactStagingDocumentationTests.cs`
- `Phase09RuntimeLegalityServiceReachabilityProofTests.cs`
- `Phase09SafetyVerifierGuardMatrixProofTests.cs`
- `Phase09ReplayCertificateCoordinatorProofTests.cs`

## Boundary evidence seams

- `Phase12CompilerContractHandshakeTests.cs`
- `Phase11NativeVliwDiagnosticDecodeTests.cs`
- `Phase12VliwCompatFreezeTests.cs`
- `Phase12RetiredCompatPolicyBitBoundaryTests.cs`

## Telemetry and replay evidence seams

- `TelemetryProfileReaderPhase06EligibilityTests.cs`
- `CertificatePressureTelemetryExportTests.cs`
- `TelemetryRejectTaxonomyTests.cs`
- `Phase09CompilerTelemetryTruthTests.cs`
- `LoopPhaseTelemetryTests.cs`
- `PhaseCertificateReuseTelemetryTests.cs`

## Documentation truthfulness gates

- `Phase09ClaimSafetyDocumentationTests.cs`
- `Phase09LegacyTerminologyQuarantineDocumentationTests.cs`
- `Phase09ValidationEvidenceCountDocumentationTests.cs`

## Current limitations

- This matrix records the smoke baseline and named proof surfaces.
- It does not claim a fresh repository-wide full-suite pass.
- Source-file and declaration counts are live-tree evidence, not coverage percentages.
