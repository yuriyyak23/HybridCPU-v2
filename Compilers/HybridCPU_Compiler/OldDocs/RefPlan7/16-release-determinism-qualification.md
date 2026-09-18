# Phase 16 — Final release, determinism and authority qualification

## Goal

Turn the implemented managed platform into a versioned, reproducible, evidence-backed release profile.

## Prerequisites

Every enabled feature phase is closed with checked-in implementation and evidence manifests.

## Release manifest

Bind a release to exact:

```text
CompilerSHA
ManagedRuntimeSHA
RuntimeKernelSHA
ISESHA
NativeAbiDigest
ManagedAbiDigest
ImageAbiDigest
KernelAbiDigest
TrapAbiDigest
EnabledFeatureBits[]
EvidenceArtifactDigests[]
```

Do not report a feature as supported if its evidence manifest is incomplete.

## Deterministic compilation/image

For identical source/toolchain/options/contracts stabilize method/type/symbol IDs, generic instantiation order, metadata tables, relocations, link input order, section layout/padding and timestamp/build-ID policy.

Required qualification:

```text
clean build A
clean build B
SHA256(A.hcexe) == SHA256(B.hcexe)
```

## Runtime determinism categories

Keep separate claims for:

- deterministic compilation;
- deterministic image bytes;
- deterministic single-context runtime;
- deterministic cooperative scheduler mode;
- replay of external inputs/scheduling/events.

Wall clock, host filesystem/network/randomness and host scheduling are nondeterministic inputs unless virtualized/logged.

## Authority-boundary qualification

Automated project/dependency tests reject:

- Compiler -> ISE execution/scheduler implementation dependency in production managed path;
- ISE -> ManagedRuntime/compiler metadata dependency;
- RuntimeKernel -> compiler IR/type-system implementation;
- ManagedRuntime -> ISE pipeline/retire/MMU implementation internals.

Only neutral versioned contracts may cross boundaries.

## Required final matrix

Compiler/CIL/CFG/SSA/phi; ABI/frame/stack-walk; object/type/static-init; stack maps/GC; dispatch/delegates/generics; EH; trap/MMU; threads/TLS; memory-order/synchronization; interop; async/reflection; ISE execution; positive/negative/property/fuzz; cross-layer end to end.

Every stack-map/EH/unwind/relocation proof is checked against final lowered code, not earlier IR.

## Failure domains

Keep managed exception/OOM, runtime fail-fast, kernel/service errors and architectural illegal/privilege/protection/page/alignment traps distinct. Integrity failures are never silently converted to arbitrary managed exceptions.

## Release disposition

Only evidence-closed capabilities become default-enabled. Experimental/platform-dependent capabilities remain behind explicit feature bits.