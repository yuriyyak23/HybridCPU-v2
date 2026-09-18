# 26 — RID, runtime pack and `dotnet publish` qualification

## Goal

Turn the completed compiler/runtime contracts into an SDK-consumable NativeAOT target.

## Required work

- Define `hybridcpu` RID and RID graph policy, including supported host RIDs and whether the target is OS-specific or initially a controlled bare-metal/simulator/runtime environment.
- Build/version target/runtime packs, native libraries, the pinned Phase 23 ILCompiler target-enablement patch/adapter bits and HybridCPU toolchain discovery.
- Add SDK/MSBuild props/targets that select the correct compiler, object writer/linker and runtime pack for `PublishAot=true`.
- Support cross-compilation hosts explicitly; host-target assumptions are versioned and target binaries/libraries cannot be replaced by host equivalents.
- Produce deterministic publish provenance containing SDK/runtime/compiler/ABI/evidence versions, exact `dotnet/runtime` baseline/patch-set digest, runtime-pack digests, linker identity/options and HybridCPU-v2 target contract digest.
- Ensure unsupported managed capabilities fail during build with actionable diagnostics rather than at runtime.
- Define package ownership and update policy for compiler pack, runtime pack, targeting/SDK integration pack and optional simulator/tooling components; version skew must be detected before codegen/link.
- Make the runtime-pack feature descriptor machine-readable so Phase 22/25 capabilities (GC, EH, interop, TLS, helpers, managed FSP/VDSA safety level) are matched against application requirements.
- Define reproducible/offline installation and tool discovery without requiring LLVM unless the user separately selects the LLVM frontend profile.

## Staged productization

Do not block early bring-up on an upstream/public RID. The implementation may begin as an explicitly versioned custom SDK/runtime pack distributed with the HybridCPU toolchain. Upstream RID/SDK integration, if desired, is a later ecosystem decision and not a compiler-correctness prerequisite.

Recommended gates:

```text
26A local custom target pack + explicit toolchain path
26B installable versioned compiler/runtime packs
26C SDK/MSBuild integration for PublishAot
26D clean-machine/cross-host qualification
26E release-quality `dotnet publish -r hybridcpu -p:PublishAot=true`
```

Each gate records the exact supported managed capability set. A pack that supports only restricted/no-GC programs must advertise exactly that rather than presenting itself as full NativeAOT.

## Qualification command

```text
dotnet publish -r hybridcpu -p:PublishAot=true
```

This command is the phase exit criterion, not the starting integration strategy.

## Determinism / fallback

SDK target selection must be deterministic from project properties, RID, installed compatible packs and explicit toolchain configuration. It must not silently fall back to JIT, host RID, another NativeAOT architecture or LLVM codegen when HybridCPU support is missing.

Missing/wrong compiler/runtime packs, target ABI mismatch, Phase 23 baseline mismatch or unsupported managed feature produce stable build errors before final image emission.

## Tests / acceptance

Clean-machine pack install, offline restore where intended, minimal/restricted/full-capability sample matrices, deterministic two-build artifact comparison, missing/wrong pack/toolchain and RID mismatch negative tests.

Add cross-host matrices, stale/mixed pack versions, host-object contamination, missing linker/runtime helper, unsupported GC/EH/interop feature and intentionally absent LLVM installation. Verify build provenance can reconstruct exactly which compiler/runtime/linker/SDK artifacts produced the image.

**Acceptance:** pinned SDK/runtime packs publish only the capability-qualified application set reproducibly; `dotnet publish -r hybridcpu -p:PublishAot=true` works on supported hosts with explicit HybridCPU target-enablement and no LLVM dependency unless an LLVM frontend is independently selected.
