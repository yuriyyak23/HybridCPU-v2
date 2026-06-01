# Phase 14 - Lane7 Accelerator Control

## Goal

Promote Lane7 accelerator topology, lifecycle, and queue-binding rows only with
capability, authority, execution, commit, replay, and virtualization policies
closed.

## Phase 14 Closure

Phase 14 is closed only as a negative production decision gate. The package
strengthens capability, authority, lifecycle, replay, migration/checkpoint, and
virtualization-boundary marker partials for the listed rows, but it does not open executable closure.

No numeric opcode allocation, decoder/encoder ABI, `InstructionIR` projection,
registry/materializer row, typed Lane7 accelerator-control MicroOp, scheduler
binding, execution/capture path, retire side-effect publication,
replay/rollback engine, golden artifact, compiler helper, generic system-op
fallback, Lane6 fallback, Lane7 submit fallback, external backend fallback, or
VMX-specific path is published by this phase.

## Production Path Overlay

Use the Lane7 full production path in `README.md`. Accelerator rows require
bounded capability/topology results, token/queue authority, owner/domain guard,
control-plane MicroOps, retire/side-effect publication, replay/migration,
golden artifacts, and no host-evidence leaks. VMX-compatible projection is
required for backend authority, host-owned evidence, migration/checkpoint, new
capabilities visible to a guest, or nested virtualization policy.

## Instructions / Contours

- Topology/capability: `ACCEL_QUERY_ABI`, `ACCEL_QUERY_TOPOLOGY`.
- Lifecycle: `ACCEL_OPEN`, `ACCEL_CLOSE`.
- Queue binding: `ACCEL_BIND_QUEUE`, `ACCEL_UNBIND_QUEUE`.

## Existing Partial Files

- `Lane07SystemControl\AcceleratorControl\Topology\*.cs`
- `Lane07SystemControl\AcceleratorControl\Lifecycle\*.cs`
- `Lane07SystemControl\AcceleratorControl\QueueBinding\*.cs`
- Aggregate metadata compatibility: `Lane07SystemControl\NonVmxLane07DeferredTemplates.cs`
- Phase 14 marker partials:
  - `*.CapabilityContract.cs`
  - `*.AuthorityContract.cs`
  - `*.LifecycleContract.cs`

## New Partial Files Allowed

- `*.CapabilityContract.cs` for bounded capability/topology results.
- `*.AuthorityContract.cs` for device/queue/token authority.
- `*.LifecycleContract.cs` for open/close and bind/unbind retire semantics.

## Local CloseToRTL Logic

Production/local partials may record no-host-evidence-leak requirements, queue authority placeholders, token authority, bounded result footprints, and fail-closed control-plane metadata. Accelerator runtime integration opens only through the Lane7 production path.

## Production Evidence Gates

Capability authority, topology ABI, accelerator runtime, queue authority, token authority, command queue semantics, decoder/encoder ABI, `InstructionIR`, materializer, typed Lane7 MicroOps, retire side effects, rollback/replay, virtualization policy, migration/checkpoint policy, conformance, and golden/no-emission tests.

## Metadata Constants

Preserve `Lane7AcceleratorControlDeferred`, `NoHostEvidenceLeak`, `RequiresRetireOwnedPublication`, `IsExecutable=false`, `CompilerHelperAllowed=false`, and any future virtualization-boundary markers. Anchor-only leaf files are not execution evidence.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate; topology/lifecycle/queue payloads must be explicit.
- InstructionIR/projection: production gate; control-plane sidebands must be typed.
- Typed MicroOp/materializer: future Lane7 control MicroOps.
- Execute/capture semantics: absent; external accelerator runtime not integrated.
- Retire/writeback/side effects: bounded query publication or retire-owned lifecycle side effects only after authority gates.
- Replay/rollback/conformance: capability stability, no host leak, open/close rollback, queue bind/unbind ordering, migration/checkpoint consistency.

## Boundaries

- Vector VLM: not applicable.
- Lane6: queue binding may interact with Lane6 tokens only through future authority gates.
- Lane7/VMX: accelerator and host-owned evidence require virtualization-boundary policy before guest visibility.
- No-emission: mandatory.

## Risks

- Capability query leaking host topology.
- Queue binding without token authority.
- Treating accelerator control as ordinary system opcode without control-plane model.

## Closure Criteria

- Phase 14 is closed when marker partials and focused tests prove the rows
  remain fail-closed and non-executable. This is not production execution
  closure.
- A production package may promote each row only after capability, token/queue authority, runtime/backend, retire, replay, golden, migration, and virtualization-boundary evidence close.
- Local partials without that package remain metadata/control contracts only.

## Prohibited Actions

- Integrate external accelerators, publish host evidence, add VMX-compatible projection, or add compiler helpers only through the Lane7 production path with explicit backend/helper/virtualization authority.
