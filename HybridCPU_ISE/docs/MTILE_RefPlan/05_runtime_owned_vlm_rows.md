# Phase 05 - Runtime-Owned VLM Rows

## Status: closed/runtime-owned-descriptor-only-vlm-rows

Decision: `ClosedRuntimeOwnedMatrixTileVlmRows`.

Descriptor-backed VLM legality is runtime-owned and descriptor-only. It checks
the four MTILE mnemonics against canonical descriptor, datatype, shape, alias,
and semantic policy.

Non-descriptor contours fail closed. Classifier and optional-disabled metadata
remain non-authority. VLM rows do not own decoder admission.

Phase 06 decoder/encoder ABI now owns canonical decoder acceptance. Later
phases own IR, materialization, execution, retire, replay, and placement.
