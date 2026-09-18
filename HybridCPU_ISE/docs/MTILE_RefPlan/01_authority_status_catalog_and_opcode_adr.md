# Phase 01 - Authority, Status, And Opcode ADR

## Status: closed/opcode-authority-only

The production package opcode identity is the runtime-owned numeric opcode and
canonical runtime metadata for `MTILE_LOAD`, `MTILE_STORE`, `MTILE_MACC`, and
`MTRANSPOSE`.

No descriptor op-type authority is opened by this phase. Parser acceptance,
compiler metadata, aliases, host evidence, and external backends are not ISA
authority.

Positive status/catalog promotion is delayed until Phase 13 and requires the
complete runtime evidence chain. That promotion is now closed and revalidated
after Phase 14.

Closure dependencies:

- Phase 02 architectural tile state and descriptor ABI;
- Phase 03 memory shape and fault ABI;
- Phase 04 accumulator/transpose semantic ABI;
- Phase 05 runtime-owned VLM rows are closed;
- Phases 06-12 decode through executable evidence;
- Phase 14 corrected placement-sensitive resource ownership.

Final status is `OptionalEnabled` with `ConformanceTested` runtime evidence.
Runtime-owned legality remains final authority.
