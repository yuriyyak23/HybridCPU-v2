# Phase 17 - Numeric Capture, Retire, Replay, And Rollback Identity

Status: closed/policy-bound-replay-identity

Review date: 2026-06-17

## Objective

Carry numeric and layout identity through the existing Phase 09-11
capture/retire/replay architecture without creating a second publication or
rollback mechanism.

## Capture Requirements

Execute capture must bind:

- owner and memory domain;
- opcode and operation kind;
- descriptor and layout-policy identities;
- numeric-policy version and fingerprint;
- resource class, slot class, lane, and resource contour;
- source and accumulator snapshot identities;
- footprint, state epoch, dependency identity, and publication surface;
- staged result or typed fault.

Capture remains architecturally invisible.

## Retire Requirements

Retire must revalidate all bound identities before publishing. Faulted,
cancelled, stale, duplicate, wrong-owner, wrong-domain, wrong-descriptor,
wrong-policy, wrong-resource, wrong-epoch, wrong-dependency, or wrong-surface
records publish nothing.

## Replay Validity

Replay is valid only when owner, domain, descriptor, layout policy, numeric
policy, resource class, footprint, epoch, dependency identity, publication
surface, and invalidation state all match.

Required negative rules:

```text
fingerprint matches and descriptor changed -> replay invalid
token is live and epoch changed -> replay invalid
capture exists and retire rejects -> no publication
numeric policy changed -> replay invalid
```

Replay and rollback use the existing core-owned checkpoints. SRF state,
StreamEngine completion, telemetry, compiler metadata, and host evidence are
not replay authority.

## Evidence

- `MatrixTilePolicyBoundIdentityAbi` binds owner/domain, opcode/operation,
  numeric/layout ABI versions and fingerprints, descriptor/resource/snapshot
  identity, footprint, core replay epoch, dependency identity, and publication
  surface into every execute capture.
- `MatrixTileExecutionCaptureRecord`, `MatrixTileCaptureIdentity`, and
  `MatrixTileReplayIdentity` now carry policy-bound identity. Retire and replay
  revalidate it against current core-owned epoch and materialized instruction
  policy before publication.
- `MatrixTileReplayRollbackAbi` includes numeric/layout policy fingerprints,
  the policy envelope fingerprint, replay epoch, dependency fingerprint, and
  publication surface in replay validity.
- Phase 17 tests cover execute binding, retire rejection for tampered numeric
  and layout policies, stale epoch rejection with no publication, materialized
  numeric-policy replay mismatch, and binary32 MACC rollback/replay.
- Existing Phase 10/11 fault-capture tests were updated to bind manual fault
  captures before identity creation, preserving deterministic fault retirement
  and replay through the existing architecture.

Verification:

```text
dotnet test HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --no-restore --filter "FullyQualifiedName~Phase09MatrixTileRuntimeIsaPackageContractTests|FullyQualifiedName~Phase10MatrixTileRetirePublicationTests|FullyQualifiedName~Phase11MatrixTileReplayRollbackConformanceTests|FullyQualifiedName~Phase12MatrixTilePositiveGoldenArtifactTests|FullyQualifiedName~Phase14MatrixTileResourceContourCorrectionTests|FullyQualifiedName~Phase15MatrixTileNumericPolicyAbiTests|FullyQualifiedName~Phase16MatrixTileFormalArithmeticAndLayoutTests|FullyQualifiedName~Phase17MatrixTilePolicyBoundIdentityTests" -v:minimal
Passed: 115/115
```

Compiler boundary:

```text
git diff --name-only -- HybridCPU_Compiler
<empty for Phase 17 runtime-only closure>
```

Phase 19 later intentionally changed the compiler transport path to carry
runtime-owned numeric/layout sidebands into source and lowered annotations.

## Exit Criteria

Phase 17 closes when numeric policy is inseparable from replay identity and all
rejections preserve the original retire-only publication boundary.

Closure result: closed. Phase 18 later closed the machine-readable numeric and
layout golden corpus, and Phase 19 later closed compiler sideband conformance
and package reclosure.
