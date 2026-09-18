

Соберу итоговый отчёт как audit summary, не как новый план: сначала отделю confirmed blockers от residual risks, затем дам список ошибок/дыр по фазам и финальный go/no-go вывод.

Главная картина уже видна: план архитектурно в целом правильный, но не готов к активации. Основные блокеры — неполные матрицы, недостаточно автоматизированные guard rails и опасность принять projection/admission за execution/publication.

# Общий суммарный отчёт аудита

Аудит проверял актуализованный фазовый план в `HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew/` против исходных архитектурных ограничений: VMX как frozen compatibility frontend, нейтральные runtime owners как единственный источник authority, Stage A / Stage B, запрет mutable VMCS и запрет VMX-owned authority. Эти ограничения прямо заданы во вводных к задаче. 

## Итоговый verdict

**План в целом архитектурно направлен правильно, но не является activation-ready.**

Он корректно фиксирует базовые инварианты: VMX не является authority, нейтральные runtime owners владеют доменами, capabilities, evidence, memory/I/O/lanes, trap policy, completion routing, retire publication и SecureCompute policy; VMCS/VMCSv2 остаётся generated/read-only/denied projection, а VMREAD открывается только field-by-field через admission, schema lookup, neutral owner и evidence/access policy. 

Главный вывод: **это хороший readiness/refactoring plan corpus, но не разрешение на включение runtime-реализации виртуализации.** Финальная фаза сама признаёт, что текущая baseline — не VMX backend activation baseline; VMREAD частично реализован только как read-only projection, VMCALL остаётся trap projection с backend denial, а VMCS writes, privileged/control/host fields, nested execution, SecureCompute backend execution и broad stream/L7 virtualization остаются denied или future-gated. 

---

# Критические блокеры

## 1. Нет activation-ready owner map для всех будущих активируемых путей

План правильно утверждает, что authority принадлежит neutral runtime owners, но для ряда будущих путей он ещё **не доведён до complete owner contract**. Особенно это касается:

- `GuestCr0` / `GuestCr4`;
- VMCALL successful backend execution;
- nested virtualization child intent;
- memory/I/O/IOMMU virtualization;
- lane6/lane7 virtualization projection boundaries;
- SecureCompute read-only compatibility projection.

Для VMREAD план требует матрицу “field → schema owner → evidence class → migration policy → current result → denial reason → test anchor”, но это пока work item, а не доказанное завершённое состояние. 

**Блокер:** нельзя переходить к активации, пока каждая будущая positive path не имеет нейтрального владельца, admission contract, evidence policy, migration classification, completion/retire rules и negative tests.

## 2. Риск перепутать admission с execution/publication

План явно фиксирует, что runtime admission не означает backend execution и не означает publication.  Но это же остаётся главным residual risk: будущий shortcut может трактовать успешный admission как достаточное основание для completion publication или retire effects. 

**Блокер:** перед активацией нужен enforced proof, что ни один VMX-compatible path не может пройти:

`admission → completion record → retire`

без отдельного backend owner, route authorization, `TrapCompletionPublicationFence` и explicit retire permission.

## 3. VMCALL остаётся admitted-denied, а не backend success

Текущий VMCALL path имеет форму decode/projection validation/runtime admission/trap/backend admission/route/fence, но `HypercallBackendAdmissionService` отказывает из-за отсутствия neutral backend descriptor / owner. План прямо говорит: current result может быть только admitted-denied trap projection, а не backend completion. 

Также явно запрещено использовать `VmExitReason.VmCall` как backend authorization, `TrapDecision` как neutral runtime policy, compatibility projection как backend owner, и запрещено публиковать completion/retire effects из admitted-denied VMCALL. 

**Блокер:** VMCALL нельзя считать активируемым, пока нет отдельного neutral hypercall backend owner RFC/ADR с typed capability, evidence policy, domain validation, argument model, completion fence и retire rule.

## 4. Completion/retire fences существуют, но это не permission

План корректно разделяет route authorization, completion publication и retire publication. `TrapCompletionRouteService.Authorize(...)` требует runtime admission, neutral trap result, runtime-owned route descriptor, domain validation, backend execution authorization, completion publication permission и retire publication permission. `ProjectionOnlyDenied` отказывает в completion и retire publication. 

Однако residual risk сформулирован очень точно: классы route/fence уже существуют в production code, поэтому разработчик может ошибочно принять существование класса за разрешение на публикацию. 

**Блокер:** нужны автоматические проверки, что `RuntimeOwnedPublication` не используется в VMX frontend paths до появления реального backend owner и publication policy. План это требует, но это должно быть не только текстом, а CI/static gate. 

---

# Высокие риски

## 5. VMREAD expansion может случайно стать пакетным открытием VMCS

План правильно фиксирует: current projected fields ограничены completion-owned, memory-owned и execution-owned projection fields; `GuestCr0`, `GuestCr4`, host aliases, compatibility-control fields, unknown fields и writes остаются denied. 

Но риск остаётся в том, что generated schema может быть воспринята как “список доступных VMCS fields”, а не как compatibility vocabulary. План требует, чтобы matrix покрывала every generated schema entry, чтобы каждое projected field имело named neutral value source, а каждое denied field — named denial reason. 

**Ошибка, которую нельзя допустить:** открыть VMREAD category-wide, например “все execution fields” или “все memory fields”, вместо field-by-field.

## 6. `GuestCr0` / `GuestCr4` — главный следующий технический риск

План правильно оставляет `GuestCr0` и `GuestCr4` denied. Они помечены как execution-domain-owned read-only aliases в schema, но `VmcsReadOnlyValueProjectionService` возвращает `PrivilegedExecutionStateProjectionDenied`, а `ExecutionDomainReadOnlyStateView` материализует только `GuestPc`, `GuestSp`, `GuestFlags`. 

**Риск:** кто-то может попытаться открыть CR0/CR4 через существующий guest/domain execution view. План это запрещает, но нужно усилить тестами: нельзя reuse текущего guest read-only view как privileged control state owner.

**Блокирующее условие для будущего открытия:** нужен отдельный neutral privileged execution state owner с bit legality, reserved-bit rules, paging/protection interaction, state epoch, evidence visibility и migration/checkpoint class.

## 7. SecureCompute boundary корректно закрыт, но опасен claim drift

SecureCompute в плане описан правильно: это neutral secure-domain descriptor, VMX/VMCS/`VmxCaps` не могут активировать, grant’ить или хранить SecureCompute authority; positive secure backend runtime execution остаётся future-gated. 

Также явно закрыто: SecureCompute не VMX mode, не secure VMCS, `VmxCaps` не authority, VMX не может activate/grant/materialize/checkpoint/migrate/own SecureCompute. 

**Риск:** документация или capability matrix может начать писать “SecureCompute supported via VMX projection” без строгого qualifier: projection/denial only unless secure runtime owner + visibility/evidence/migration policy explicitly allow read-only projection.

## 8. Tests/golden artifacts могут быть ошибочно приняты за runtime authority

План корректно говорит, что tests are proof surfaces, not runtime owners; golden artifacts are conformance evidence, not production state; telemetry/diagnostics cannot satisfy capability/evidence authority. 

**Риск:** passing broad test filter может быть использован как аргумент “путь безопасен”, хотя owner-specific denial rules ещё не закрыты.

**Блокер:** нужны targeted conformance gates, а не только broad `dotnet test`.

---

# Ошибки и недоработки плана

## 1. План местами выглядит как “готовый граф работ”, но часть задач всё ещё discovery

Есть несколько фаз, где work items формулируются как “produce matrix”, “define checklist”, “document decisions”, “define scans”. Это нормально для docs/refactoring corpus, но **не для activation readiness**.

Особенно незавершённые зоны:

- Phase 04: VMREAD matrix ещё должна быть произведена.
- Phase 07: neutral hypercall backend owner только специфицируется, не существует.
- Phase 08: route/publication checklist ещё должен быть формализован.
- Phase 11: SecureCompute boundary matrix ещё должна быть построена.
- Phase 13: static scans и conformance gates ещё должны быть определены/автоматизированы.

## 2. Недостаточно доказано, что все локальные пути заменены на пути репозитория

В аудите выявлено, что многие code anchors в плане указываются как:

`CloseToHSL/...`

А репозиторный путь должен быть:

`HybridCPU_ISE/CloseToHSL/...`

Это не архитектурная ошибка, но это **claim hygiene / reproducibility blocker**: reviewer не должен вручную угадывать корень. Финальный корпус должен использовать только repo-root paths, например:

`HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`

а не:

`CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`

Путь папки `VirtualiztionRefactoringNew` содержит опечатку в слове `Virtualiztion`. Судя по репозиторию, папка с таким именем существует, поэтому путь надо сохранять как есть, но в index желательно явно указать: **folder name intentionally matches repository path**.

## 3. Phase numbering расходится с исходной рекомендованной схемой

В пользовательском ТЗ исходно предлагался `00_activation_plan_index.md` и Phase 00–15. В текущем корпусе используется:

`00_refactoring_plan_index.md`, затем `01_current_state...` до `15_final...`.

Это не критично, но есть риск несогласованности с внешними reviewer expectations. Если план будет принят как финальный activation plan, лучше либо:

- переименовать index в `00_activation_plan_index.md`, либо
- в текущем index явно зафиксировать, что это updated path/name и что Phase numbering intentionally shifted.

## 4. План недостаточно жёстко отделяет “docs-only phase” от будущих code work orders

Финальная фаза правильно запрещает реализовывать код, объявлять production-ready, смешивать unrelated compiler/tests/examples/docs changes и превращать future-gated items в backlog-free tasks. 

Но в общем тексте плана есть риск, что “next work order” будет прочитан как implicit permission. Финальная фаза сама предупреждает: наиболее вероятная будущая ошибка — принять documented path shape за approval to implement it. 

**Нужна правка:** в каждом phase file добавить явную фразу: “This phase creates review material only; it does not authorize production behavior.”

---

# Пофазовые риски

## Phase 01 — Current State Inventory

Риск: inventory может быть неполным, если не покрывает every generated VMCS field и every VMX-compatible ingress path. Базовые anchors есть: `VmxCompatibilityAdmissionService`, `RuntimeBoundaryAdmissionService`, `VmcsReadOnlyValueProjectionService`, `VmcsFieldProjectionSchema`, execution/memory views, hypercall backend admission, route/fence. Но это должно быть превращено в проверяемую таблицу, а не только перечисление.  

**Блокер:** нет complete current-state matrix → нельзя доказать отсутствие hidden authority path.

## Phase 02 — Guard Rails

Риск: guard rails пока описаны как требуемые scans, но не доказано, что они запускаются в CI и fail build.  

**Блокер:** forbidden-name scans, overclaim scans, anchor-presence scans и markdown skeleton checks должны быть executable artifacts, иначе это только документационное пожелание.

## Phase 03 — Runtime Admission

Риск: текущие VMREAD/VMCALL paths проходят `RuntimeBoundaryAdmissionService`, но future paths могут появиться сбоку. План требует enum current VMX-compatible ingress paths и operation kind per path.   

**Блокер:** каждый новый VMX-compatible path должен иметь admission checklist до code review.

## Phase 04 — VMREAD

Риск: VMREAD выглядит “почти готовым”, но фактически открыт только ограниченный read-only projection subset.  

**Блокер:** complete field matrix по every schema entry. Без неё нельзя расширять VMREAD.

## Phase 05 — Privileged Execution State

Риск: CR0/CR4 могут быть открыты через неправильный owner.  

**Блокер:** отдельный privileged execution state owner; до этого `GuestCr0`/`GuestCr4` остаются denied.

## Phase 06 — VMCS Write / Compatibility Controls

Риск: кто-то может реализовать VMWRITE как “обычную запись в VMCS”.  

**Блокер:** `CanWrite=false` должен быть защищён static scan + negative tests; любые future writes требуют separate neutral write owner.

## Phase 07 — VMCALL / Hypercall

Риск: admitted-denied VMCALL будет принят за working hypercall backend.  

**Блокер:** no neutral backend owner → no backend execution, no completion, no retire.

## Phase 08 — Trap Completion / Retire

Риск: existence of `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` будет воспринято как разрешение.  

**Блокер:** route authorization, completion publication и retire publication должны оставаться отдельными gates.

## Phase 09 — Nested Virtualization

Риск: Shadow VMCS / VMCS12 / VMCS02 вернутся как state store.  

**Блокер:** nested только через neutral child intent descriptors, capability filter, nested memory composition owner и evidence policy.

## Phase 10 — Memory/I/O/IOMMU/Lanes

Риск: lane6/lane7 или stream evidence будут использованы как virtualization authority.  

**Блокер:** stream/lane/L7 остаются runtime/helper/model surfaces, не VMX authority, не VMCS state, не SecureCompute authority. Это прямо запрещено в global forbidden regressions. 

## Phase 11 — SecureCompute

Риск: `VmxCaps` или VMCS projection начнут выглядеть как secure capability grant.  

**Блокер:** SecureCompute boundary matrix для VMREAD, VMWRITE, `VmxCaps`, VMCS checkpoint, migration, evidence, hypercall paths. 

## Phase 12 — Compiler/ISA

Риск: compiler/no-emission tests или examples превратятся в production backend emission.  

**Блокер:** compiler не должен bypass runtime admission и не должен добавлять capability-aware SecureCompute ISA через side effects.

## Phase 13 — Conformance

Риск: tests останутся списком, а не gate.  

**Блокер:** forbidden-name, overclaim, anchor-presence scans и `git diff --check` должны быть обязательной частью CI/review. 

## Phase 14 — Claim Hygiene

Риск: stale docs будут говорить “activate VMX” или “backend ready”, хотя план только readiness.  

**Блокер:** все claims должны иметь статус: implemented, projection-only, denied, model/helper-only, future-gated, forbidden.

## Phase 15 — Final Readiness

Риск: финальный review выдаст broad activation вместо narrow work order.  

**Блокер:** следующий work order должен быть bounded to one owner decision, not broad activation. 

---

# Главные архитектурные red flags

1. Появление `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, `VmcsManagerAdapter`, `VmxRuntimeManager`, `VmcsProjectionRuntimeManager`, `VmcsV2RuntimeManager`.
2. Любая active VMCS pointer модель.
3. Любой mutable VMCS field store/cache.
4. Любой VMX-owned runtime manager.
5. `VmxCaps` как grant source.
6. `VmExitReason.VmCall` как authorization.
7. `TrapDecision` как neutral runtime policy.
8. Completion record без `TrapCompletionPublicationFence`.
9. Retire success без explicit retire publication permission.
10. VMREAD projection как migration/checkpoint authority.
11. Shadow VMCS / VMCS12 / VMCS02 как nested runtime state.
12. SecureCompute activation через VMX/VMCS/VMWRITE/VmxCaps.
13. Host evidence leak в guest-visible VMCS projection.
14. Stream/Lane6/Lane7 telemetry/evidence как virtualization authority.
15. No-emission tests как путь к production backend emission.

---

# Что нужно исправить перед переходом к activation readiness

1. **Нормализовать все пути к repo-root paths.**  
   Все `CloseToHSL/...` заменить на `HybridCPU_ISE/CloseToHSL/...`, все `NonRTL/...` на `HybridCPU_ISE/NonRTL/...`, тесты — на `HybridCPU_ISE.Tests/...`.

2. **Собрать complete VMREAD matrix.**  
   Every generated schema entry должен иметь owner/denial/evidence/migration/test anchor.

3. **Сделать guard rails исполняемыми.**  
   `rg` scans и markdown checks должны быть command-ready и желательно CI-bound.

4. **Добавить explicit negative tests для всех denied states.**  
   Особенно: CR0/CR4, HostCr3, host aliases, compatibility controls, all writes, VMCALL missing owner, RuntimeOwnedPublication misuse, SecureCompute VMX authority attempts.

5. **Разделить readiness docs и future implementation work orders.**  
   В каждом файле явно указать: “does not authorize runtime activation”.

6. **Сделать final readiness matrix.**  
   Обязательные статусы: implemented, projection-only, denied, model/helper-only, future-gated, forbidden.

7. **Первым следующим work order делать не VMCALL, а GuestCr0/GuestCr4 owner RFC/ADR.**  
   Финальная фаза тоже указывает, что safest candidate — privileged execution-state owner decision for `GuestCr0`/`GuestCr4`; hypercall backend owner больше и должен ждать route/completion/retire/capability/evidence contract. 

---

# Финальный go/no-go

**GO для продолжения документационного refactoring/readiness corpus.**

**NO-GO для включения активной runtime-реализации виртуализации.**

Причина: план правильно защищает архитектурную линию, но пока не закрывает все activation gates. Самые сильные блокеры — отсутствие complete owner maps/matrices, отсутствие доказанных executable static gates, future-gated VMCALL backend owner, denied privileged state fields, и риск перепутать projection/admission с backend execution/publication.

Минимальный следующий безопасный шаг: **owner-specific RFC/ADR для neutral privileged execution state owner (`GuestCr0`/`GuestCr4`) + negative tests + VMREAD matrix update**, без изменения VMX frontend authority и без открытия VMWRITE/VMCALL backend.