# Phase 09 Negative Matrix Slice 04 - L7-SDC lane7

Status: implemented as focused compiler-core negative gates.

## Scope

This slice freezes the L7-SDC/lane7 authority boundary before caller migration:

- descriptorless `ACCEL_SUBMIT` fails closed at decoder ingress;
- direct descriptorless compiler emission is rejected before carrier emission;
- rejected L7 submit intent cannot carry a runtime fallback promise;
- L7 contour provider rejection records no DSC, Stream or scalar fallback;
- capability metadata, telemetry, token and compiler evidence remain evidence-only;
- valid descriptor ABI status does not grant execution, publication, commit, retire or runtime legality.

## Tests

Added `CompilerPhase09L7SdcLane7NegativeMatrixTests`:

- `L7SdcDescriptorlessAccelSubmitFailsClosedAtDecoderIngress`
- `L7SdcDirectDescriptorlessCompilerEmissionRejectsBeforeCarrierOrFallback`
- `L7SdcRejectedSubmitIntentHasNoDscStreamOrScalarFallbackAfterSubmit`
- `L7SdcLane7ContourProviderRejectsLoweringWithoutDscStreamOrScalarFallback`
- `L7SdcCapabilityTelemetryAndCompilerEvidenceRemainEvidenceOnly`
- `L7SdcDescriptorAbiValidityDoesNotGrantExecutionPublicationCommitRetireOrRuntimeLegality`

## Authority Notes

- L7-SDC remains `L7SdcLane7`, pinned to descriptor sideband and lane7 submit ingress.
- `ACCEL_SUBMIT` without descriptor sideband is fail-closed, not a descriptor parser shortcut.
- Runtime fallback after submit is rejected before emission; rejected submit does not route to DSC, Stream or scalar lowering.
- Capability observations and telemetry are metadata/evidence only; they do not grant decode, command submission, execution or commit authority.
- Descriptor ABI validity remains `DescriptorAbiConstruction` and `DescriptorOnly`; bridge acceptance still requires runtime Legality A/B, execution, commit, retire and publication.

## Historical Next Task

The VMX and SecureCompute negative matrix listed here has since been
implemented. For the current open compiler work, use:

- `PHASE09_REMAINING_COMPILER_WORK.md`
- `CONTINUATION_PROMPT_PHASE09.md`
