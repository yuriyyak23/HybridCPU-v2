# RefPlan6 Phase 00 factual dependency graph

Baseline: `ccfa88c0857f681074a480f65e50559395c590eb` / tree
`94f06044497dac7f5966e3e067f645aacc7bc651`.

## Build graph

```text
Core (no ProjectReference, no PackageReference)
├─ Native -> Core
├─ CIL -> Core
│  └─ NativeAOT adapter -> CIL + Core
├─ LLVM -> Core + LLVMSharp + LLVMSharp.Interop
└─ Oracle -> Core

ReleaseQualification -> Core + Native + CIL + NativeAOT + LLVM + Oracle
Legacy HybridCPU_Compiler -> Core + HybridCPU_ISE
HybridCPU_ISE.Tests -> compiler components + HybridCPU_ISE (test/evidence only)
```

The ScalarControlFlowV2 production path is `CIL -> Core` and, at the integration
boundary, `NativeAOT adapter -> CIL + Core`. None of those four project files references
LLVM, LLVMSharp or HybridCPU_ISE. LLVM remains a sibling optional ingress. The legacy
umbrella compiler and the test project are outward integration/evidence consumers and
are not dependencies of Core, CIL, Native or NativeAOT.

## Semantic/data authority graph

```text
PE/CIL body
  -> RestrictedCilImporter (admission only)
  -> Canonical IR / CFG / value flow (Core)
  -> dependency/liveness/resource/scheduling/allocation (Core)
  -> native ABI + final frame (Core target)
  -> HCO writer -> static linker -> restricted image builder (Core target)
  -> loader/ISE consumption and execution (runtime authority)

NativeAOT/ILCompiler -> roots and available bodies only -> same CIL/Core path
LLVM IR/bitcode -> optional LLVM importer -> same Core path (not reachable from CIL path)
```

Compiler-owned metadata is structural evidence. Runtime legality, freshness, replay,
execution, faults, publication, commit and retire remain ISE/runtime-owned.

## Contract owners and Phase 00 re-diff

| Contract | Owner/source | Baseline result |
|---|---|---|
| target/data layout | `HybridCpuTargetMachineContractV1` | unchanged since RefPlan5 Phase 27 |
| platform/object facts | `HybridCpuTargetPlatformContractV1` | unchanged |
| native ABI/frame | `HybridCpuNativeAbiContractV2` | unchanged; nested-call execution still Phase 3 blocker |
| restricted CIL | `RestrictedCilContractsV1` + importer | unchanged; single-method/backward-branch limits confirmed |
| HCO | `HybridCpuObjectFormatContractV1` + writer | unchanged |
| static link | `HybridCpuStaticLinkerV1` | unchanged |
| image/startup | `HybridCpuRestrictedImageBuilderV1` | unchanged |
| NativeAOT seam | adapter contracts and pinned patch set | unchanged; single-method assumption confirmed |
| LLVM | separate LLVM project | optional sibling only |
| ISE-facing ISA | generated ISA catalog + encoder + runtime tests | unchanged; Phase 3/6 execution parity remains required |

No Phase 00 evidence authorizes an ISE/runtime/loader/ISA modification.
