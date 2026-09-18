# 00 - Current state and authority-boundary assessment

## Status

Complete. This document was the documentation baseline; it is reconciled here
with the implemented phases 01-11. No runtime authority is implied by any
completion recorded below.

## Current compiler shape

1. **Intent and contour selection**
   - `CompilerSemanticIntent` and `CompilerDefaultExecutionContourSelector`
     classify operations without granting authority.
   - Unknown contours resolve to rejected analyzer/provider paths and cannot
     scalar-fallback.

2. **Authority and package taxonomy**
   - `CompilerAuthorityClass`, `CompilerPublicationClass`,
     `CompilerExecutionClaim`, and `CompilerRuntimeAuthorityDependency` keep
     carrier, evidence, bridge, and runtime lifecycle responsibilities
     separate.
   - `CompilerProductionLoweringStatus.ProductionCarrierPackageRuntimeAuthorityPending`
     means a compiler package was constructed; it never means runtime execution
     completed.

3. **Shell/provider split**
   - `IContourLoweringProvider` remains the compatibility/fail-closed shell
     path.
   - `IContourProductionLoweringProvider` is the explicit package path.
   - `DefaultContourLoweringProviderRegistry` resolves a production provider
     only for an exact, explicitly enabled contour and complete profile gate.

## Current entrypoint classification

| Surface | Product | Authority status | Current role |
| --- | --- | --- | --- |
| `IContourAnalyzer.Analyze` | `ContourAnalysisReport` | evidence-only | classifier and missing-requirement report. |
| `IContourLoweringProvider.Lower` | shell decision | fail-closed | compatibility shell; never package construction. |
| `IContourProductionLoweringProvider.TryProduce` | separated package | runtime authority pending | explicit bounded provider path only. |
| vector/matrix helper lowerers and recovery APIs | helper/parser artifacts | non-production | candidate source or helper-only evidence; never authority. |
| `CompileDmaStreamCompute*` | lane6 carrier plus descriptor metadata | compatibility transport | shadow-candidate source for the DSC provider. |
| `CompileAcceleratorSubmit` | lane7 carrier plus descriptor metadata | typed compatibility transport | shadow-candidate source for the L7 provider. |
| raw `CompileInstruction` and observation APIs | carrier/sideband observation | non-authoritative | compatibility or inspection only. |

## Explicit production-package inventory

The following contours have bounded providers: native scalar, native
load/store, native branch/control, direct vector transfer, DSC lane6, and
L7-SDC lane7. Each provider:

- requires profile, exact contour, intent, artifact, runtime-dependency,
  no-fallback, parity, and telemetry/evidence gates;
- preserves Legality A/B, execution, publication, commit, and retire as
  runtime dependencies;
- returns `RuntimeAuthorityPending`, not execution/publication/commit/retire;
- preserves the compatibility shell and does not migrate public callers.

## Deliberate non-production contours

- MatrixTile remains helper ABI only.
- VMX remains projection/no-emission.
- SecureCompute remains policy/admission/evidence-only.
- ParserOnly, NoEmission, FutureGated, and UnknownRejected remain fail-closed.

## Reconciled gap summary

The normal provider/package track is implemented. The remaining work is not a
missing compiler gate: it is separate authority design work. In particular,
caller migration, legacy API removal, VMX runtime backend authority, and
SecureCompute runtime backend authority are outside this completed track.

## Verification

Phase 01 source scanning, Phase 02 golden artifacts, Phase 05 parity, provider
matrices through Phase 11, and the full `CompilerTests` suite are the evidence
for this assessment. See phase 12 for exact results and the remaining RFC-only
exit conditions.
