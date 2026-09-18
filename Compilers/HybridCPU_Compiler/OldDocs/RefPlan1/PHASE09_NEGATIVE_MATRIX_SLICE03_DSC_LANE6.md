# Phase 09 Negative Matrix Slice 03 - DSC lane6

Status: implemented as focused compiler-core negative gates.

## Scope

This slice freezes the DSC/lane6 authority boundary before caller migration:

- rejected DSC lowering remains lane6-only and records no L7/Stream/scalar fallback;
- lane6 descriptor sideband on non-DSC opcode fails at compiler IR build;
- lane6 DSC and lane7 L7-SDC descriptor collision fails at compiler IR build and decoder ingress;
- descriptor ABI validity remains descriptor evidence only and does not grant memory/register publication, execution, commit, retire, or runtime legality.

## Tests

Added `CompilerPhase09DscLane6NegativeMatrixTests`:

- `DscLane6ContourProviderRejectsLoweringWithoutL7StreamOrScalarFallback`
- `DscLane6DescriptorOnNonDscOpcodeRejectsAtCompilerIrBuild`
- `DscLane6AndL7Lane7DescriptorCollisionRejectsAtCompilerIrBuild`
- `DscLane6AndL7Lane7DescriptorCollisionRejectsAtDecoder`
- `DscDescriptorAbiValidityDoesNotPublishMemoryOrRegisterAuthority`

## Authority Notes

- `DmaStreamComputeLane6` provider rejection is `NoEmission` with forbidden cross-contour fallback.
- Descriptor ABI validation reports `DescriptorAbiConstruction` and `NoExecutionClaim`.
- Runtime bridge descriptor ingress acceptance still requires runtime Legality A/B, commit, retire and publication.
- Lane6 and lane7 descriptor sidebands are mutually exclusive; there is no hidden DSC-to-L7 or L7-to-DSC fallback.

## Historical Next Task

The L7-SDC/lane7 negative matrix listed here has since been implemented. For
the current open compiler work, use:

- `PHASE09_REMAINING_COMPILER_WORK.md`
- `CONTINUATION_PROMPT_PHASE09.md`
