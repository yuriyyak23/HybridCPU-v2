# SecureCompute Refactoring Index

## Purpose

Этот документ является индексом поэтапного плана интеграции SecureCompute в HybridCPU-v2. План фиксирует архитектурную позицию: SecureCompute не является новым VMX-режимом, защищенным VMCS или битом `VmxCaps`; он является opt-in усилением нейтральной доменной runtime-модели.

Фактическая папка плана: `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/`.

Такое размещение предпочтительно для текущего этапа, потому что SecureCompute должен развиваться рядом с нейтральными доменными владельцами, а не внутри VMXRefactoring. Альтернативная публикационная папка `HybridCPU_ISE/docs/SecureComputeRefactoring/` пригодна для сводной документации после первых PR. Вариант `HybridCPU_ISE/docs/VMXRefactoring/SecureCompute/` менее предпочтителен: он может создать ложное впечатление, что SecureCompute является веткой VMX, хотя VMX остается только frozen compatibility frontend.

## Scope

План покрывает два первых слоя:

- Layer 1: HybridCPU Secure Domain. Это neutral runtime-domain extension с явными secure descriptors, Stage B/runtime admission, memory/evidence/migration/I/O/hypercall/debug/projection policy и fail-closed поведением только для secure-domain операций.
- Layer 2: CHERI-like authority discipline without ISA expansion. Это дисциплина полномочий на уровне descriptors, typed grants, monotonic derivation, sealing-like opaque handles, bounded authority, provenance, revocation и epochs.

План также задает future layer: capability-aware ISA/memory model. Этот слой явно не входит в текущие фазы.

## Non-goals

В первые два слоя не входят:

- capability registers;
- tagged memory;
- capability-bearing operands;
- capability-aware `LOAD` / `STORE` / `FETCH`;
- CHERI-like pointer provenance на уровне каждого указателя;
- новые форматы операндов;
- новые кодировки ISA;
- новые режимы адресации;
- изменения decoder/encoder ABI, требующие новых instruction encodings;
- обязательная capability-семантика для существующих non-VMX инструкций.

Также не допускается перенос secure authority в `VMCS`, `VmxCaps`, `VMREAD`, `VMWRITE`, active VMCS pointer, legacy VMX helpers или compatibility projection metadata.

## Architectural invariants

Архитектурная ось HybridCPU остается `Legality A|B`, а не VMX. Виртуализация остается domain-level extension of generic legality/runtime model.

Stage A может получить только классификационные metadata для будущих runtime checks: `mayTouchMemory`, `mayTouchIo`, `mayCreateEvidence`, `mayTrap`, `mayAffectRetirePublication`, `isDebugSensitive`, `isPrivilegedStateSensitive`, `requiresSecureDomainCheck`. Stage A не должна требовать secure policy для существующих инструкций.

Stage B и `RuntimeBoundaryAdmissionService` являются местом реальных secure-решений. Runtime/Domains/Memory/Capabilities/Evidence/Migration/Completion/I/O остаются source of truth.

VMX остается frozen compatibility frontend / ABI vocabulary / generated projection surface. `VMCS` остается read-only compatibility projection, но не state owner. `VmxCaps` остается projection of typed grants, но не capability authority.

Отсутствие `SecureComputeDomainDescriptor`, а также `SecurityLevel = Disabled` или `None`, эквивалентно отсутствию secure-domain enforcement. Чтобы `None` и `Disabled` не разошлись по семантике, materialization должна либо нормализовать `None -> Disabled`, либо оставить только один нейтральный disabled-state в первой реализации.

## Proposed descriptors / policies

План вводит следующие нейтральные сущности как проектные цели, не как уже реализованные типы:

- `SecureComputeDomainDescriptor`;
- `SecureMemoryDomainDescriptor`;
- `DomainMeasurementDescriptor`;
- `SecureEvidencePolicy`;
- `SecureMigrationDescriptor`;
- `SecureIoDomainDescriptor`;
- `SecureHypercallDescriptor`.

Главный дескриптор `SecureComputeDomainDescriptor` агрегирует:

- `DomainTag`;
- `SecurityLevel`;
- `MeasurementRequired`;
- `PrivateMemoryRequired`;
- `HostInspectionPolicy`;
- `EvidenceVisibilityPolicy`;
- `MigrationPolicy`;
- `IoPolicy`;
- `HypercallPolicy`;
- `DebugPolicy`;
- `CompatibilityProjectionPolicy`.

## Integration points

Основные зоны интеграции:

- `CloseToHSL/Core/Runtime/Domains`;
- `CloseToHSL/Core/Runtime/Memory`;
- `CloseToHSL/Core/Runtime/Capabilities`;
- `CloseToHSL/Core/Runtime/Events`;
- `CloseToHSL/Core/Runtime/Completion`;
- `CloseToHSL/Core/Runtime/Migration`;
- `CloseToHSL/Core/Runtime/Nested`;
- `CloseToHSL/Core/Virtualization/Compatibility`;
- `HybridCPU_ISE.Tests/VmxRefactoring`;
- `HybridCPU_ISE/docs/VMXRefactoring`.

Нужно учитывать текущие VMX closures после 240: generated read-only VMREAD value projection, completion-owned projection, memory-owned projection, `VPID` / `CR3 target count` / `HostCr3` denial, compatibility-control fail-closed semantics, execution-owned projection for `GuestPc` / `GuestSp` / `GuestFlags`, `GuestCr0` / `GuestCr4` denial, host execution aliases denial, descriptor readiness fail-closed, migration/evidence proof for recomputed compatibility fields.

## No-regression requirements

SecureCompute не должен:

- менять текущую ISA;
- менять decoder/encoder ABI;
- менять typed `InstructionIR` projection для существующих инструкций;
- менять publication/materialization typed `MicroOp` без secure-domain opt-in;
- менять 2048-bit bundle / 256-byte native VLIW carrier;
- менять EPIC/VLIW typed-slot legality для обычных доменов;
- менять scheduling, bundling, lane binding для non-secure доменов;
- добавлять Lane6/Lane7 secure side effects без runtime descriptors;
- превращать admitted-denied VMCALL/trap path в backend success;
- открывать VMREAD secure-sensitive fields без neutral owner, read-only source, evidence policy, migration classification и conformance tests.

## Tests and conformance

Общий тестовый принцип: inactive secure compute оставляет существующие тесты без изменений, а enabled secure compute fail-closed там, где отсутствует neutral owner или subpolicy.

Плановые conformance families:

- first PR no-effect tests: `absent descriptor -> unchanged`, `disabled descriptor -> unchanged`, `SecurityLevel.None -> unchanged`;
- no-regression tests for absent/disabled descriptor;
- early VMX denial-only guard before positive secure paths: VMX cannot activate SecureCompute, `VmxCaps` cannot grant SecureCompute, VMCS cannot store secure state;
- source-pattern guards against VMCS authority, scalar cache, active VMCS pointer, VMX runtime manager;
- schema owner mismatch denial;
- migration/evidence non-leak guards;
- VMREAD/VMWRITE denied-by-default matrix;
- compiler no-emission boundary tests;
- Stage A metadata-only tests;
- Stage B runtime admission fail-closed tests;
- replay/rollback/epoch tests.

## Closure criteria

План считается закрытым как документационный артефакт, когда все файлы `00`-`13` сохранены в `Plan/`, а open-decision backlog сохранен в `Plan2/14-securecompute-open-decision-backlog.md`, имеют единую структуру, явно разделяют Layer 1, Layer 2 и future capability-aware ISA/memory layer, и не предлагают VMX/VMCS/VmxCaps как владельцев secure state. Наличие документации и shell-типов означает только design baseline + shells; это не feature-complete SecureCompute и не доказанная реализация secure compute без code proof и no-effect tests.

## Forbidden shortcuts

Запрещенные сокращения:

- добавить `VmxCaps.SecureCompute` как authority;
- хранить secure state в VMCS;
- читать secure state через VMREAD backend;
- писать secure state через VMWRITE;
- использовать active VMCS pointer как secure-domain identity;
- восстановить `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager` как runtime owner;
- сериализовать VMCS projection как checkpoint authority;
- сериализовать host-owned evidence, scheduler evidence, backend binding evidence, native token evidence или debug traces как guest state.

## Open questions

- Resolved 2026-05-30: for Phase 0-3, runtime-local `Plan/` remains the authoritative plan location; repo-level publication can be handled as later documentation cleanup.
- Resolved 2026-05-30: `DomainRuntimeContext` carries a direct optional `SecureCompute` descriptor view plus neutral `DomainTag` / `AddressSpaceTag` binding metadata.
- Resolved 2026-05-31: Layer 2 uses combined runtime descriptor/grant/provenance epochs for the current baseline; future epoch-model changes are tracked in `Plan2/14-securecompute-open-decision-backlog.md` only if a new decision is needed.
