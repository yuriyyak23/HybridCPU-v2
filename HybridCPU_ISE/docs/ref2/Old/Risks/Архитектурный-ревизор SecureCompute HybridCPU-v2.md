
Соберу итоговый отчёт как ревизию плана против текущего репозитория: сначала проверю сам каталог плана и связанные файлы/поиск по SecureCompute, затем сведу блокеры, ошибки путей, пробелы и приоритеты исправления.

Нашёл актуальный набор документов плана в репозитории: это 16 файлов `00…15` в `HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew`. Далее сверяю их с кодовой базой и соседними audit/refactor-документами.

Ключевой вывод уже виден: план в целом не пытается «включить Secure Compute», а переводит старый activation-план в readiness/future-gated модель. Самые опасные зоны — VMCALL backend, CR0/CR4, VMCS writes, SecureCompute backend и stream/L7 overclaim.

Ниже — общий суммарный отчёт аудита по актуализованному плану `SecureCompute / Virtualization RefactoringNew`, с акцентом на риски, блокеры, ошибки и места, где план можно неверно прочитать как разрешение на реализацию.

## 1. Итоговый вердикт

План в целом **корректно переориентирован** с опасной “activation” логики на безопасную модель **readiness / denied / projection-only / future-gated**. Он не заявляет, что SecureCompute уже активирован, и не открывает VMX/VMCS как authority. Это соответствует текущему состоянию SecureCompute: release gate прямо говорит, что активация может быть заявлена только если каждый пункт подтверждён кодом, тестами, документацией и negative conformance; если что-то остаётся docs-only, fail-closed, proof-only, admitted-denied или future — activation заблокирован. 

Главный вывод: **архитектурно план годный как документационный readiness/audit corpus, но не годный как work order на кодовую реализацию без дополнительных RFC/ADR**. Сам план Phase 15 это тоже фиксирует: он не авторизует implementation сам по себе, а следующий work order допустим только после owner/admission/evidence/route/publication/conformance/doc preconditions. 

---

## 2. Проверенный corpus и фактическое расположение

Актуальный corpus состоит из 16 файлов `00…15` в:

```text
HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew/
```

Именно с ошибкой в имени каталога: `VirtualiztionRefactoringNew`, а не `VirtualizationRefactoringNew`. Это важно: в путях нужно использовать фактическое имя директории из репозитория, иначе ссылки и команды не сработают. Список всех 16 файлов подтверждён поиском по репозиторию.   

План заявляет, что заменяет старый activation-oriented research plan на readiness/refactoring plan, основанный на текущем коде, VMX-аудитах и Virtualization/SecureCompute/Stream whitebooks. 

---

## 3. Критический блокер №1: не путать документационный план с разрешением на код

Самый опасный риск — прочитать этот corpus как “можно начинать включать SecureCompute / VMX backend / VMCALL backend”. Это неверно.

Phase 15 прямо запрещает объявлять архитектуру production-ready, смешивать unrelated worktree changes и превращать future-gated items в backlog-free tasks. 

Phase 13 также фиксирует, что tests/golden artifacts — это proof surfaces, а не runtime owners; telemetry/diagnostics не могут заменить authority; passing broad test filter не переопределяет owner-specific denial rules. 

**Практический вывод:** следующий шаг должен быть не “рефакторим всё”, а один узкий RFC/ADR. Сам план предлагает safest candidate: owner decision для `GuestCr0` / `GuestCr4`; hypercall backend RFC крупнее и должен ждать конкретного runtime contract по route/completion/retire/capability/evidence. 

---

## 4. Критический блокер №2: SecureCompute не активирован и не production-ready

Текущий SecureCompute baseline сильный, но **не activation-complete**. В release gate указано: нет positive secure backend runtime execution. 

Код подтверждает эту модель: `SecureComputeDomainDescriptor` по умолчанию создаётся как disabled/no-effect с `SecureComputeSecurityLevel.Disabled`, deny/fail-closed политиками и `CompatibilityProjectionPolicy.DenyAll`.  Также descriptor считается active только если он enabled и materialized; иначе `IsNoEffect`. 

Plan2 отдельно фиксирует open decision backlog: positive secure backend runtime execution owner/RFC **unopened**, требует отдельной фазы, approved RFC/ADR, neutral runtime owner и доказательства, что VMX/VMCS/`VmxCaps` не могут авторизовать execution. 

**Блокер:** любые формулировки вида “SecureCompute activated”, “production-ready SecureCompute”, “feature-complete SecureCompute”, “VMX activates SecureCompute” должны считаться ошибкой.

---

## 5. Критический блокер №3: VMX/VMCS не являются authority

План корректно удерживает VMX как compatibility frontend vocabulary, а VMCS/VMCSv2 — как generated/read-only/denied projection vocabulary, не mutable state store. Это зафиксировано в summary index. 

Кодовая база это подтверждает. `RuntimeBoundaryAdmissionService` валидирует domain boundary, capability boundary, evidence boundary, frontend mutation denial, optional SecureCompute admission и root authority.  Compatibility frontend authoritative mutation отклоняется, кроме explicit activation/deactivation operation kinds. 

**Риск:** если кто-то вернёт `VmcsManager`, active VMCS pointer, VMCS field store, mutable VMCS cache или VMX-owned runtime manager, это не просто “legacy cleanup issue”, а архитектурная регрессия. Phase 01 и Phase 02 прямо запрещают такие return points.  

---

## 6. VMREAD: в целом корректно, но есть важный semantic trap

Текущий admitted VMREAD path корректно описан как:

```text
decode
→ projection validation
→ RuntimeBoundaryAdmissionService
→ generated schema lookup
→ neutral owner value source
→ evidence/access policy
→ field-by-field projection
```

В коде `AdmitVmReadProjection` сначала делает decode, затем compatibility projection validation, затем вызывает `RuntimeBoundaryAdmissionService.Validate(...)` с `DomainRuntimeOperationKind.ReadCompatibilityProjection`, и только после этого вызывает `VmcsReadOnlyValueProjectionService.Project(...)`.  

Схема действительно generated/read-only: `VmcsFieldProjectionSchema` содержит entries для execution, memory, completion и compatibility-control owners. 

Но план правильно подчёркивает semantic trap: **наличие поля в generated schema не означает, что поле реально читается как value projection**. Phase 04 требует матрицу field-by-field и запрещает broad VMREAD activation. 

### Projected сейчас

План корректно перечисляет projected поля:

```text
Completion-owned:
ExitReason
ExitQualification
GuestPhysicalAddress
EptViolationQualification

Memory-owned:
GuestCr3
EptPointer
Vpid
Cr3TargetCount

Execution-owned:
GuestPc
GuestSp
GuestFlags
```

Это совпадает с Phase 04 current baseline. 

### Denied сейчас

Также корректно denied:

```text
GuestCr0
GuestCr4
HostPc
HostSp
HostFlags
HostCr0
HostCr3
compatibility-control fields
unknown fields
all writes
```

План Phase 05 правильно держит `GuestCr0`/`GuestCr4` denied до появления neutral privileged execution-state owner с реальными semantics, visibility policy, migration classification и tests. 

**Риск/ошибка для будущей реализации:** нельзя открыть `GuestCr0`/`GuestCr4` просто потому, что они есть в schema как `ExecutionDomainDescriptor`. Schema owner не равен value source.

---

## 7. VMCS writes и compatibility-control fields: должны оставаться закрыты

Phase 06 корректно фиксирует, что `VmcsFieldProjectionSchema.CanWrite(...)` возвращает `false`, compatibility-control fields есть в schema как read-only aliases, но `CompatibilityControlDescriptor` не является frozen VMX control-bit value source. 

**Риск:** слово `ReadOnly` может быть неправильно прочитано как “readable now”. Phase 06 прямо указывает, что schema `ReadOnly` не означает current projected value. 

**Блокер для любого VMWRITE:** нужен отдельный neutral write owner, policy, capability, evidence и tests. В текущем corpus всё write-поведение должно оставаться denied. 

---

## 8. VMCALL / hypercall backend: самый высокий execution-риск

VMCALL сейчас не является backend execution. Он является neutral trap projection с backend admission denial.

Код подтверждает: `AdmitVmCallTrapProjection` выполняет decode, projection validation, runtime admission, neutral trap policy evaluation и затем создаёт backend admission через `CreateHypercallBackendAdmission(...)`.  В результате `IsAdmittedDeniedTrapProjection` требует, чтобы backend execution был denied, completion route denied, publication fence denied, completion publication false и retire publication false. 

`HypercallBackendAdmissionService` deny-ит backend execution, если нет neutral runtime backend descriptor, если authority не runtime, если нет validated domain, capability/evidence или если neutral backend owner не materialized. 

**Главный блокер:** `RuntimeOwnedPublication` нельзя использовать раньше реального neutral backend owner. Phase 07 прямо запрещает использовать `VmExitReason.VmCall`, `TrapDecision` или compatibility projection как backend authorization. 

---

## 9. Completion route / retire publication: классы есть, permission нет

Наличие `TrapCompletionRouteService` и `TrapCompletionPublicationFence` не означает, что публикация разрешена. Phase 08 чётко разделяет route authorization, completion publication и retire publication. 

Current VMX VMCALL flow использует projection-only denied route construction, а publication fence denies publication без runtime admission, neutral trap, backend authorization и retire publication permission. 

**Риск:** route/fence production classes выглядят как готовая инфраструктура и могут быть ошибочно использованы как permission. План правильно отмечает это как residual risk. 

---

## 10. Nested virtualization: корректно future-gated

Phase 09 корректно держит nested virtualization за neutral child intent и composition owners. Shadow VMCS, VMCS12, VMCS02 не должны становиться authority payloads. 

**Блокер:** nested execution не должен открываться из VMX vocabulary или SecureCompute design fence. SecureCompute nested design fence не авторизует nested secure backend success. 

---

## 11. Stream / Lane6 / L7: корректно, но зона повышенного overclaim-risk

План правильно исправляет старую “all fail-closed” формулировку: текущий Stream baseline уже имеет bounded DSC1 lane6 contour, где `DmaStreamComputeDescriptorParser.ExecutionEnabled` равен `true`, но unsupported shapes и later descriptor classes остаются fail-closed.  Код parser это подтверждает: `ExecutionEnabled => true`, но сообщение ограничивает execution Phase 06 DSC1 contour и подчёркивает fail-closed для unsupported shapes без StreamEngine/DMAController fallback. 

**Риск двусторонний:**

1. оставить старое “всё fail-closed” — будет неактуально;
2. расширить bounded DSC1/L7 contour до virtualization authority — будет unsafe.

Phase 10 это прямо называет residual risk. 

---

## 12. SecureCompute VMX boundary: корректно deny/projection only

Phase 11 формулирует правильный boundary: SecureCompute authority остаётся в neutral runtime descriptors/policies; VMX, VMCS, `VmxCaps`, VMREAD, VMWRITE, stream helpers, L7 commands, telemetry и tests не являются authoritative. 

Код `SecureComputeCompatibilityBoundaryMatrixPolicy` подтверждает denial matrix: VMREAD требует neutral owner, read-only source, secure visibility, migration classification и conformance; VMWRITE denied; `VmxCaps` не может materialize SecureCompute descriptor; VMCS projection metadata не может быть checkpoint authority; compatibility projection не может стать backend success. 

**Блокер:** любые будущие SecureCompute compatibility advertisements по VMX должны оставаться read-only projection neutral publication/evidence, не grant/activation/ownership. Это же зафиксировано в Plan2 open decisions. 

---

## 13. Compiler / ISA: план корректно запрещает emission creep

Phase 12 правильно фиксирует no-emission contract: compiler/backend emission не должен подразумевать VMX backend, SecureCompute backend, stream/L7 expansion или VMCS authority. 

**Риск:** текущий repo имеет активные compiler refactoring changes outside this plan; phase must not touch them. 

**Блокер:** нельзя добавлять capability-aware ISA, tags, grant registers, operands, ABI или bundle metadata как “подготовку” к SecureCompute. Эти решения находятся в Plan2 backlog и требуют отдельного repository-level architecture proposal. 

---

## 14. Основная ошибка в путях: локальные пути надо заменить на repo-relative

В плане много anchors указаны как локальные:

```text
CloseToHSL/...
NonRTL/...
docs/ref2/...
```

Для репозитория их нужно нормализовать.

### Общее правило замены

```text
CloseToHSL/...      -> HybridCPU_ISE/CloseToHSL/...
NonRTL/...          -> HybridCPU_ISE/NonRTL/...
docs/ref2/...       -> HybridCPU_ISE/docs/ref2/...
HybridCPU_ISE.Tests -> HybridCPU_ISE.Tests
Documentation/...   -> Documentation/...
```

`Documentation/...` уже является repo-relative root path, его не нужно дополнять `HybridCPU_ISE/`.

### Примеры обязательных замен

```text
CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs
->
HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs
```

Файл реально существует по repo path. 

```text
CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs
->
HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs
```

Файл реально открыт из этой зоны. 

```text
CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs
->
HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs
```

Файл реально открыт из этой зоны. 

```text
NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs
->
HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs
```

Файл реально открыт из этой зоны. 

```text
docs/ref2/VirtualiztionRefactoringNew
->
HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew
```

Phase 14 пока использует короткую локальную форму `docs/ref2/VirtualiztionRefactoringNew`; для repo-relative ссылок это нужно заменить. 

---

## 15. Приоритетный список рисков / блокеров / ошибок

### P0 — блокеры, нельзя двигаться дальше без фиксации

1. **Не авторизовывать SecureCompute activation.** Positive secure backend runtime execution отсутствует; Plan2 держит это как unopened open decision.  

2. **Не открывать VMCALL backend execution.** Current VMCALL — admitted-denied projection; backend owner missing. 

3. **Не использовать VMX/VMCS/`VmxCaps` как SecureCompute authority.** VMX не может activate/grant/materialize/checkpoint/migrate/own SecureCompute. 

4. **Не возвращать mutable VMCS state и legacy VMX authority.** `VmcsManager`, active VMCS pointer, VMCS field store — forbidden regressions. 

5. **Не считать schema/read-only alias текущей readable value.** Projection service, owner source и policy решают доступ, не schema entry. 

### P1 — высокие риски корректности

1. **CR0/CR4.** Нельзя открыть без neutral privileged execution-state owner и semantics. 

2. **Compatibility-control fields.** Нельзя выводить frozen VMX control-bit values из `CompatibilityControlDescriptor`. 

3. **Trap completion / retire publication.** Нельзя использовать route/fence классы как permission; они currently deny projection-only paths. 

4. **Stream/L7 overclaim.** DSC1/L7 bounded contours существуют, но не являются virtualization/SecureCompute authority. 

5. **Compiler emission creep.** Нельзя добавлять ISA/compiler lowering как “подготовку” к SecureCompute backend. 

### P2 — документационные ошибки и hygiene

1. **Пути.** Локальные `CloseToHSL/...`, `NonRTL/...`, `docs/ref2/...` заменить на repo-relative.

2. **Опечатка директории.** Использовать фактическое `VirtualiztionRefactoringNew`, не исправлять молча на `VirtualizationRefactoringNew`.

3. **Смешение старого activation text с новым corpus.** Phase 14 правильно требует source-of-truth precedence: current whitebooks/code сильнее старого `deep-research-report (6).md`. 

4. **Forbidden-name scan false positives.** Forbidden names могут встречаться только как absent/forbidden/static-check vocabulary; автоматический scan надо читать с контекстом. 

---

## 16. Оценка полноты

Покрытие хорошее. План закрывает основные зоны:

| Зона | Оценка |
|---|---|
| VMX authority boundary | Полно и корректно |
| Runtime admission | Полно, соответствует коду |
| VMREAD field-by-field | Полно, но нужна финальная матрица всех schema entries |
| CR0/CR4 | Корректно future-gated |
| VMCS writes | Корректно denied |
| Compatibility controls | Корректно denied |
| VMCALL backend | Корректно blocked |
| Completion/retire publication | Корректно separated/blocked |
| Nested virtualization | Корректно future-gated |
| SecureCompute authority | Корректно neutral runtime only |
| Stream/Lane6/L7 | Корректно, но overclaim-sensitive |
| Compiler/ISA | Корректно no-emission |
| Conformance/static gates | Достаточно, но команды надо реально прогнать локально |
| Path hygiene | Требует исправления repo-relative путей |

Главный недостающий артефакт — **единая readiness matrix**, которую Phase 15 требует, но сама по себе ещё не предоставляет как итоговую таблицу по всем surfaces. 

---

## 17. Рекомендуемый следующий work order

Не начинать с hypercall backend. Не начинать с SecureCompute activation. Не начинать с VMCS writes.

Самый безопасный следующий шаг:

```text
RFC/ADR: Neutral privileged execution-state owner for GuestCr0 / GuestCr4
```

Почему: текущий denied reason узкий, тестируемый и локализованный. Это совпадает с рекомендацией Phase 15. 

Минимальный состав такого RFC/ADR:

```text
1. Owner placement:
   ExecutionDomainDescriptor vs new neutral privileged sub-descriptor.

2. Semantics:
   CR0/CR4 bit legality, reserved bits, paging/protection interactions.

3. Visibility:
   guest-visible snapshot semantics and evidence class.

4. Migration:
   descriptor-owned vs recomputed vs projection-only classification.

5. Denial cases:
   missing owner, partial state, stale state, invalid policy, no migration class.

6. Negative conformance:
   no VMCS scalar fallback,
   no host alias leakage,
   no compatibility-control inference,
   no VMX authority.
```

---

## 18. Финальное резюме

План можно принять как **корректный документационный readiness/refactoring corpus**, но с обязательными правками путей и с жёсткой пометкой:

```text
NOT AN ACTIVATION PLAN.
NOT A SECURECOMPUTE PRODUCTION-READY CLAIM.
NOT A VMX BACKEND IMPLEMENTATION WORK ORDER.
```

Главные блокеры остаются:

```text
1. no positive SecureCompute backend runtime execution;
2. no VMCALL backend owner;
3. no completion/retire publication permission;
4. no CR0/CR4 privileged execution-state owner;
5. no VMCS writes;
6. no VMX/VMCS/VmxCaps SecureCompute authority;
7. no broad Stream/L7 virtualization authority;
8. repo-relative path normalization still required.
```