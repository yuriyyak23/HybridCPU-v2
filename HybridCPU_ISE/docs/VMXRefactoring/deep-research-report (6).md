# VMX-v1/v2 To Generic Runtime Substrate: Current Research Note

Дата очистки: 2026-05-25  
Статус: актуализировано после VMX compatibility frontend freeze и closure `222`.

## Короткий вывод

Изначальный deep research report правильно сформулировал главный архитектурный поворот:

```text
VMX/VMCS/VMCSv2 не должны быть архитектурной осью.
VMX должен быть compatibility frontend поверх generic runtime/domain substrate.
```

Текущий код уже в основном прошел этот поворот:

- VMX compatibility frontend freeze объявлен.
- Production больше не зависит от physical `Legacy/VMX`.
- `Core/VMX/Substrate` не содержит C# sources.
- `Legacy/VMX/Compatibility` не содержит C# sources.
- `Legacy/VMX/Conformance` содержит только compiled evidence/test contracts.
- `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager` удалены без replacement.

Этот документ больше не является миграционным планом "с нуля". Он является компактной исследовательской заметкой о том, какие идеи старого research report остаются полезными после текущего refactor state.

## Текущая архитектурная модель

VMX is not the virtualization architecture.  
VMX is a frozen compatibility frontend.

Source of truth:

- execution domains: `Core/Runtime/Domains/*`;
- memory/I/O identity, translation, invalidation: `Core/Runtime/Memory/*`, `Core/Runtime/IO/*`, `Memory/MMU/*`;
- typed capabilities: `Core/Runtime/Capabilities/*`;
- Lane6/Lane7/vector-stream state and host-owned evidence: `Core/Runtime/Lanes/*`;
- nested domain model: `Core/Runtime/Nested/*`;
- completion and retire publication: neutral runtime/pipeline services;
- VMX compatibility vocabulary: `Core/VMX/Compatibility/*`;
- historical/conformance evidence: `Legacy/VMX/Conformance/*`.

VMX/VMCS vocabulary is allowed only as:

- frozen ABI;
- compatibility frontend vocabulary;
- generated/read-only projection;
- denied/no-effect alias;
- fail-closed opcode/retire vocabulary;
- conformance evidence;
- explicit physical quarantine.

## Current Inventory

Current checked facts:

- `Core/VMX` legacy-marked C# sources: `0`.
- `Core/VMX/Substrate` C# sources: `0`.
- `Legacy/VMX/Compatibility` C# sources: `0`.
- `Legacy/VMX/Conformance` C# sources: `37`.
- total `Legacy/VMX` C# sources: `37`.
- `VmxExecutionUnit.cs`: absent.
- `VmcsManager.cs`: absent.
- `IVmcsManager.cs`: absent.
- `LegacyVmxFreezeReadinessCertificationContract.CanDeclareFreeze == true`.
- `LegacyVmxFreezeReadinessCertificationContract.ConformanceFolderDeletionSafeWithoutTestEvidenceDecoupling == false`.

## What The Old Research Got Right

The old report is still useful for these principles:

1. VMX must remain a compatibility protocol, not the owner of domain state.
2. VMCS/VMCSv2 should be projection vocabulary over neutral descriptors, not a mutable runtime store.
3. Capabilities must be typed grants, not bitmap authority.
4. Host-owned evidence must never become guest ABI, migration payload, or VMREAD-visible state.
5. Migration/checkpoint must serialize guest-visible domain model, not host caches or backend bindings.
6. Lane6/Lane7/vector-stream runtime and compiler boundaries must remain neutral first, then projected if needed.
7. Generated projection artifacts must either have build-time lineage or be explicitly marked contract-only.
8. VMX opcode/decode/retire vocabulary may be frozen while runtime behavior remains denied/fail-closed.

## What Is Now Closed

### Removed Without Replacement

- `VmxExecutionUnit`.
- `VmcsManager`.
- `IVmcsManager`.
- Dead compatibility backends/adapters:
  - `LegacyVmxIoVirtualizationBackend`;
  - `LegacyVmxTranslationInvalidationBackend`;
  - `LegacyCsrBackedVmxCapabilityDescriptorSource`;
  - `LegacyVmxV1AdapterBoundary`;
  - `LegacyVmxV2AdapterBoundary`.
- VMCS scalar write/cache authority.
- VMCS-shaped checkpoint/restore authority.
- VMX-shaped Lane6/Lane7/vector-stream host-evidence carriers.
- Generic domain/capability/memory/I/O/lane/nested substrate under `Core/VMX/Substrate`.

### Rehomed Or Retained As Compatibility Vocabulary

- `VmxInstructionPayload`.
- `VmxRetireEffect`.
- `VmxRetireOutcome`.
- `VmxOperationKind`.
- `ShadowVmcsNestedProjectionService`.
- VMCS field projection schema and aliases.
- VmxCaps generated capability projection over typed grants.
- VMX IOTLB/invalidation spellings as denied/no-effect aliases.

These are not runtime owners. They are compatibility ABI/projection vocabulary.

### Generated Lineage

Build-time projection lineage verifies:

- VMCS field projection schema/source;
- compat alias map;
- VmxCaps capability-bit schema/source;
- compat spec artifact manifest.

Future projection artifacts must either join the verifier or be explicitly `ProjectionContractOnly`.

## Updated Artifact Mapping

The old report contained large artifact tables based on pre-refactor names. The useful current mapping is smaller:

| Old VMX-shaped concern | Current owner | VMX role |
| --- | --- | --- |
| VMX opcodes | decoder/micro-op/retire compatibility path | frozen, typed fail-closed unless admitted later |
| VMREAD/VMWRITE field ids | generated VMCS field projection schema | read-only/denied projection vocabulary |
| VmxCaps | typed capability grants + generated projection | CSR/ABI projection only |
| VMX invalidation names | neutral memory/I/O invalidation owners | denied/no-effect compatibility spelling |
| VMCS guest/host state | neutral execution/checkpoint descriptors | projected compatibility vocabulary only |
| Lane6 queues/tokens/fences | neutral Lane6 runtime/evidence store | no host evidence leakage |
| Lane7 handles/tokens/backend bindings | neutral Lane7 runtime/evidence store | no checkpointed host evidence |
| Nested VMCS12/VMCS02 | neutral nested descriptors/projection/checkpoint | Shadow VMCS compatibility bridge fail-closed |
| Completion/exit publication | neutral completion/retire routing | VMX-facing result projection only |
| Debug/observability | neutral evidence/observability policy | aggregate/projection only, no authority |

## Current Open Risks

### 1. VMCSv2 Mutable Helper Surface

`VmcsV2Descriptor` and `VmcsV2Blocks` still expose methods that look owner-like and need method-level classification:

- guest-state capture/materialization;
- vector/stream exit recording;
- host-evidence recording/discard;
- root descriptor binding and epoch advance;
- NPT control binding;
- event injection queue/remap/delivery/restore;
- debug trace counter recording/reset;
- bundle binding.

Each method should be classified as:

- generated/read-only projection;
- denied/fail-closed ABI stub;
- retire-publication-only helper;
- runtime-owner-to-extract;
- dead shell to delete.

### 2. Conformance Evidence Is Still Compiled

`Legacy/VMX/Conformance` is production-independent, but tests still compile directly against its contract types and `LegacyVmxQuarantineManifest`.

Do not delete it blindly. Previous move-away probe proved:

- production build passes without it;
- tests build fails without it with `190` compile errors.

Next cleanup needs test-evidence decoupling first.

### 3. VMX Execution Is Frozen And Safe, Not Feature-Complete

Current VMX opcode behavior is typed fail-closed. That is correct for freeze safety.

Future admitted behavior must go through:

```text
decode VMX opcode
-> compatibility projection/access policy
-> RuntimeBoundaryAdmissionService
-> neutral runtime/domain operation
-> neutral completion/retire publication
-> VMX-compatible projected result
```

No admitted path may restore `VmxExecutionUnit`, `VmcsManager`, active VMCS pointer state, VMCS field stores, or renamed VMX runtime managers.

### 4. Nested Compatibility Runtime Wiring

Neutral nested projection/checkpoint services exist, but compatibility nested execution remains fail-closed.

Future enablement must use neutral nested descriptors and runtime admission. Shadow VMCS must remain projection/compatibility vocabulary, not authority.

## Recommended Next Work

Preferred sequence:

1. **Conformance evidence decoupling**  
   Replace direct compiled test dependencies on `Legacy/VMX/Conformance` contracts with static/file evidence or retire obsolete proof contracts. Rerun production and tests with the folder moved away.

2. **VMCSv2 method-level authority audit**  
   Classify every owner-looking method in `VmcsV2Descriptor` and `VmcsV2Blocks`. Delete or extract the smallest honest runtime-owner residue.

3. **EventInjectionBlock cleanup**  
   Move real queue/remap/delivery state to neutral runtime events or prove the VMCSv2 surface is projection-only/fail-closed.

4. **DebugTraceBlock cleanup**  
   Move counter/observability mutation to neutral evidence/observability runtime or prove VMCSv2 debug surface is only compatibility snapshot/projection.

5. **First admitted VMX path design**  
   Pick a low-risk operation such as denied/read-only VMREAD projection or VMCALL trap projection and route it through `RuntimeBoundaryAdmissionService`.

6. **Projection artifact inventory**  
   Classify every `Core/VMX/Compatibility/Generated/*` and `Core/VMX/Compatibility/Frontend/Projection/*` artifact as generated-lineage, contract-only, denied-only, or forbidden-authority.

## Baseline Checks For Future Work

```powershell
rg -il "legacy" "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\VMX" --glob "*.cs" --glob "!bin/**" --glob "!obj/**"

rg --files "\HybridCPU ISE\HybridCPU_ISE\Legacy\VMX" --glob "*.cs"

dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore

dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~Vmx"
```

Expected:

- Core/VMX legacy-marked sources: `0`.
- `Legacy/VMX` C# count: `37`.
- `Legacy/VMX/Compatibility` C# count: `0`.
- production build passes.
- broad VMX filter passes.

## Final Status

```text
Security posture: strong.
VMX compatibility frontend freeze: declared.
Production dependency on Legacy/VMX: absent.
Legacy production cleanup: exhausted.
Conformance evidence cleanup: open.
VMCSv2 mutable helper cleanup: open.
Feature-complete admitted VMX execution: open.
Nested compatibility execution: safe/fail-closed, not feature-complete.
```

The useful residue of the old research is the architectural rule: VMX remains an ABI/projection frontend over a generic descriptor/capability/runtime substrate. The obsolete residue was the old inventory, old migration plan, unresolved citation markup, removed carriers, and pre-freeze assumptions; those have been removed from this note.
