# 04 — Core modularization and standalone native frontend

## Goal

Make the architectural boundary physically enforceable before adding LLVM or .NET integrations.

## Current-code reality and reuse

The repository currently builds the compiler from a single `HybridCPU_Compiler.csproj`; `Core/IR`, API/facade code and production providers coexist. Reuse the existing Canonical IR, `HybridCpuCanonicalCompiler`, dependency/resource models, `HybridCpuLocalListScheduler`, `HybridCpuCycleGroupSearch`, exact `HybridCpuSlotModel` placement, `HybridCpuBundleFormer`, lowering and serializer. Do not create a second compiler pipeline.

## Inputs / outputs

Input: verified Phase 00–03 core and native ASM/API ingress. Output: an enforceable `HybridCPU.Compiler.Core` dependency boundary plus a native frontend/CLI/API that reaches the same canonical pipeline without LLVM/.NET assemblies or native libraries.

The standalone native profile must expose, from the same core pipeline, deterministic access to:

`Canonical IR -> schedule -> bundles -> ASM -> object/binary where supported -> evidence -> scheduling report -> provenance`.

## Required work

1. Split project boundaries or equivalent build targets so Core can be built and tested independently.
2. Move only dependency-neutral contracts into Core; adapters/facades depend inward.
3. Preserve direct ASM/API -> Canonical IR path and all current emit modes.
4. Add architecture tests forbidding Core references to LLVMSharp, LLVM, Roslyn, `ILCompiler` and NativeAOT implementation assemblies.
5. Keep existing runtime bridge/evidence vocabulary at an outward boundary; Core must not gain runtime execution authority.
6. Establish deterministic diagnostic/result contracts for `Unsupported`, `InvalidInput`, `BudgetExhausted` and conservative fallback.
7. Add a frontend registration abstraction owned outside Core. Core consumes only versioned Canonical IR/target/capability contracts; it never probes which frontend produced them.
8. Remove static initializers, service locators or transitive package references that can cause LLVM/Roslyn/NativeAOT assemblies or native libraries to load in `HybridCPU.Native`.
9. Define an explicit standalone artifact matrix so native mode is not accidentally reduced to “ASM only” while LLVM/.NET paths gain richer evidence/object outputs.

## Dependency and authority rules

- `HybridCPU.Compiler.Core` is the only owner of target-independent HybridCPU scheduling/placement policy.
- Frontends may canonicalize source semantics but cannot call `BundleFormer`, final encoder or runtime legality APIs around the core pipeline.
- Core may emit evidence; it cannot create runtime freshness/epoch/replay/publication/commit/retire authority.
- Runtime-facing adapters may depend on Core contracts; Core must not depend on a runtime implementation package.

## Safety / fallback

This phase changes packaging, not scheduling policy. Native parity is the rollback baseline. No new frontend is allowed to bypass Canonical IR validation.

## Tests and acceptance

- Native corpus is byte-identical for schedule/bundles/image/evidence before vs after modularization.
- Core builds/tests with LLVM/.NET integration packages absent and with LLVM native libraries absent.
- Add a **loadability negative test**: run the native CLI/API in an environment where LLVMSharp/Roslyn/ILCompiler assemblies and LLVM native libraries are physically unavailable; all standalone outputs above must still work.
- Dependency graph tests fail on forbidden direct **and transitive** project/package references.
- Reflection/static-constructor scan proves no optional frontend is loaded by Core/native startup.
- Existing Phase 00–03 determinism and authority-negative suites remain green.

**Acceptance:** zero semantic/codegen delta in native profile; standalone Core boundary is mechanically enforced; every native artifact promised by the public profile is obtainable without LLVM or .NET compiler integration installed.
