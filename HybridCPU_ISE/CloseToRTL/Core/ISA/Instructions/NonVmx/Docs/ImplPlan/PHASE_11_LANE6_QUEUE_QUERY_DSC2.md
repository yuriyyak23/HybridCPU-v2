# Phase 11 - Lane6 Queue, Query, And DSC2

## Goal

Evaluate Lane6 queue lifecycle commands, read-only capability queries, and the
DSC2 carrier. Phase 11 closes the current pool as a negative decision gate,
not as executable closure, because token/queue authority, bounded result ABI,
descriptor-v2 execution policy, retire/replay, and golden evidence are not
complete.

## Current Phase 11 Decision

`DSC_POLL`, `DSC_WAIT`, `DSC_CANCEL`, `DSC_FENCE`, and `DSC_COMMIT` remain
`Reserved`/no-allocation fail-closed queue-control rows. `DSC_QUERY_BACKEND`
and `DSC_QUERY_SHAPE` remain `Reserved`/no-allocation fail-closed capability
query rows. `DSC2` remains `ParserOnly`/declared-only and is not runtime
execution evidence.

Existing executable `DSC_STATUS` evidence is scoped only to token status
publication and does not authorize queue poll/wait/cancel/fence/commit.
Existing executable `DSC_QUERY_CAPS` evidence is scoped only to the current
bounded caps word and does not authorize backend or shape query rows. Current
`DmaStreamCompute` execution and Phase 10 descriptor-only declarations do not
authorize queue/query/DSC2 execution. DSC2 parser acceptance and deterministic
footprint validation are parser-only evidence; they do not open runtime
admission, typed MicroOps, retire publication, replay/rollback, compiler
emission, or hidden multi-op lowering.

This phase opens no numeric opcode allocation, decoder/encoder ABI,
`InstructionIR` projection, registry/materializer rows, typed queue/query/DSC2
MicroOps, scheduler/lane binding, queue runtime execution, retire/writeback or
side-effect publication, replay/rollback path, compiler helper, VMX-specific
path, Lane7 fallback, or external backend fallback. Host-owned evidence remains
unpublished; any future guest-visible DMA/backend authority requires a separate
generic runtime model and, where applicable, a virtualization-boundary policy.

## Production Path Overlay

Use the Lane6 full production path in `README.md`. Queue/query rows need
command encoding, token scope, bounded capability results, owner/domain guard,
typed MicroOp/publication, retire/replay, and no host-evidence leak tests.
`DSC2` remains parser-only until a descriptor-v2 ADR opens execution. VMX is
not an integration point for these Non-VMX rows; virtualization-boundary policy
is only a future gate for DMA/backend authority, host evidence, migration, or
nested virtualization policy.

## Instructions / Contours

- Queue lifecycle: `DSC_POLL`, `DSC_WAIT`, `DSC_CANCEL`, `DSC_FENCE`, `DSC_COMMIT`.
- Queries: `DSC_QUERY_BACKEND`, `DSC_QUERY_SHAPE`.
- Carrier: `DSC2`.

## Existing Partial Files

- `Lane06DmaStream\QueueLifecycle\DscPollInstruction.cs`
- `Lane06DmaStream\QueueLifecycle\DscWaitInstruction.cs`
- `Lane06DmaStream\QueueLifecycle\DscCancelInstruction.cs`
- `Lane06DmaStream\QueueLifecycle\DscFenceInstruction.cs`
- `Lane06DmaStream\QueueLifecycle\DscCommitInstruction.cs`
- `Lane06DmaStream\Queries\DscQueryBackendInstruction.cs`
- `Lane06DmaStream\Queries\DscQueryShapeInstruction.cs`
- `Lane06DmaStream\CarrierV2\Dsc2DescriptorCarrier.cs`

## New Partial Files Allowed

- `*.QueueContract.cs` for token namespace, queue handle, command scope, and rollback notes.
- `*.QueryContract.cs` for bounded read-only result footprints.
- `Dsc2DescriptorCarrier.ParserContract.cs` for parser-only/ADR notes.

## Local CloseToRTL Logic

Production/local partials may document queue authority, token scope, bounded
query results, DSC2 parser-only status, no host-evidence leak, and future
virtualization policy requirements. Phase 11 leaf constants are marker ABI only;
queue/query/DSC2 execution opens only through the generic Lane6 production path.

## Production Evidence Gates

Queue/token ABI, decoder/encoder ABI if commands become instructions, `InstructionIR` projection, registry/materializer, queue runtime, descriptor-v2 ADR, descriptor-v2 parser manifest, backward-compatible decoder, retire-owned side effects, rollback/replay, bounded capability result ABI, virtualization policy, conformance, and golden/no-emission tests.

## Metadata Constants

Preserve `Lane6QueueControlNoExecution`, `Lane6CapabilityQueryNoExecution`, `ParserOnlyCarrierNoExecution`, `IsQueueControlOwned`, `IsCapabilityQuery`, `IsReadOnlyQuery`, `IsDescriptorOwned`, `IsCarrierOnly`, `IsParserOnly`, `HasScalarOpcodeAllocation=false`, `NoDsc2ExecutionBeforeAdr`, `NoHostEvidenceLeak`, `NoGuestVisibleHostEvidence`, `RequiresFutureVirtualizationBoundaryPolicy`, `IsExecutable=false`, and `CompilerHelperAllowed=false`.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate; DSC2 remains parser-only until ADR.
- InstructionIR/projection: production gate for queue/query commands.
- Typed MicroOp/materializer: production queue/query materializers only.
- Execute/capture semantics: no execution; queue runtime external.
- Retire/writeback/side effects: owned side effects or bounded query publication only after retire policy.
- Replay/rollback/conformance: token lifecycle, cancel/fence/commit ordering, query stability, no host leak, DSC2 backward compatibility.

## Boundaries

- Vector VLM: not primary.
- Lane6 sideband: mandatory; queue/query/carrier authority is not scalar ALU authority.
- Lane7/VMX: no Lane7, external backend, or VMX-specific fallback; external backend and host evidence require future virtualization policy.
- No-emission: no compiler helpers.

## Risks

- Read-only queries leaking host-owned capability evidence.
- Queue commands executing without rollback policy.
- DSC2 parser acceptance being mistaken for execution.

## Closure Criteria

- A production package may promote queue/query rows only after token authority, bounded result ABI, retire, replay, golden, and virtualization-boundary policy close where needed.
- DSC2 remains parser-only/no-execution before ADR.

## Prohibited Actions

- Do not add queue runtime execution, descriptor-v2 execution, host-evidence publication, scalar opcode allocation, compiler helper emission, hidden scalar/vector lowering, multi-op emission, Lane7 fallback, external backend fallback, or VMX-specific paths.
