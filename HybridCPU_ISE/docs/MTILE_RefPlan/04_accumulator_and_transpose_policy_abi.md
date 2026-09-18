# Phase 04 - Accumulator And Transpose Policy ABI

## Status: closed/runtime-owned-accumulator-and-transpose-semantic-abi

Decision: `ClosedAccumulatorAndTransposeSemanticAbi`.

`MTILE_MACC`:

- validates `MaccRowsKColumnsShapeCompatibilityValidated`;
- uses `WideningIntegerAccumulatorDtypePolicySelected`;
- captures source and accumulator snapshots at execute;
- publishes the accumulator result only at retire.

`MTRANSPOSE`:

- uses `OutOfPlaceOrSquareInPlaceAliasPolicySelected`;
- applies `RowMajorTransposeShapePermutationSelected`;
- captures the source tile at execute;
- publishes the destination tile only at retire.

Phase 05 runtime-owned VLM rows are closed.
Vector transpose and external backend evidence remain non-authority.

## Post-Closure Numeric Audit

This phase closed the original widening-integer behavior and transpose
publication boundary. It did not define a complete `MatrixTileNumericPolicy`.
The live implementation currently encodes signed/unsigned integer element
families, widening accumulator selection, fixed loop order, and overflow-trap
behavior through separate enums and execution code.

That implementation is a legacy supported baseline, not proof of a complete
numeric specification. In particular, implicit defaults for multiply, add,
rounding, saturation, exceptional values, reproducibility, and exception
handling must not be inferred from this closure record.

Phase 15 supersedes the broad numeric-closure interpretation while preserving
the Phase 04 state, alias, transpose, and retire-only publication decisions.
