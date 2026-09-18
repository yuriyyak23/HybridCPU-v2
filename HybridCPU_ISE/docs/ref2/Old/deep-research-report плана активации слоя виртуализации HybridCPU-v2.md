# Архитектурный аудит роли и плана активации слоя виртуализации HybridCPU-v2


## Роль и задача слоя виртуализации

Роль слоя виртуализации в HybridCPU-v2 — не стать новой VMX-owned виртуализацией, а создать контролируемую границу совместимости, в которой VMX-словарь можно декодировать, проверять на допустимость, сопоставлять с нейтральными доменами и, если нейтральный владелец это разрешил, проецировать в VMX-совместимую форму без передачи полномочия самому VMX. Это определение почти буквально следует из `Virtualization WhiteBook`: VMX — frozen compatibility frontend; vocabulary may describe ABI/projection/completion vocabulary, but must not own execution domains, trap policy, completion publication, memory authority, capability grants, migration payloads or secure-compute authority. citeturn18view0turn19view1turn19view2turn19view3turn25view1turn25view3

Практическая задача этого слоя состоит из четырёх подзадач. Во-первых, сохранить стабильный compatibility ABI: opcode aliases, operation kinds, VMCS field aliases, `VmExitReason`, `VmxExitQualification`, VMX-facing retire vocabulary. Во-вторых, обеспечить строго ограниченную проекцию read-only значений через `VMREAD` — но только там, где есть явный neutral owner и explicit value source. В-третьих, обеспечить admitted-denied path для `VMCALL`, доказывающий, что frontend правильно проходит через decode, projection validation, runtime admission, neutral trap policy и publication fences, не превращаясь при этом в backend execution. В-четвёртых, сохранить deny-by-default для `VMWRITE`, host aliases, control fields, SecureCompute-through-VMX, nested authority, lane/stream leakage и compiler emission. citeturn19view1turn19view2turn19view3turn21view0turn21view1turn25view1turn25view2turn25view3turn27view0turn28view0turn28view1

Этот замысел хорошо виден и в коде. `RuntimeBoundaryAdmissionService` прямо запрещает compatibility frontend напрямую мутировать авторитетное runtime-state и разрешает проход только через neutral admission gates. `VmxCompatibilityAdmissionService` оформляет `VMREAD` как `ReadCompatibilityProjection`, то есть как projection-only operation, а не execution backend. `VmxCompatibilityAdmissionService.Traps` строит `VMCALL` как trap projection, затем deliberately вызывает `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`, после чего route и publication fence отрабатывают fail-closed. `TrapCompletionRoutePolicy` различает `ProjectionOnlyDenied` и `RuntimeOwnedPublication`, а `TrapCompletionPublicationFence` отдельно запрещает publication при отсутствии completion authorization и отдельно запрещает retire publication. citeturn26view0turn26view1turn27view0turn28view0turn28view1

Ниже — три коротких показательных фрагмента из репозитория.

```csharp
DomainRuntimeOperation.FromCompatibilityFrontend(
    DomainRuntimeOperationKind.ReadCompatibilityProjection,
    requiresCapabilityGrant: false,
    isProjectionOnly: true)
```

Источник: `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs`. Смысл: `VMREAD` оформлен как projection-only path, а не как backend execution. citeturn26view1

```csharp
HypercallBackendAdmissionRequest.MissingNeutralOwner(...)
TrapCompletionRouteRequest.ProjectionOnlyDenied(...)
```

Источник: `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`. Смысл: текущий `VMCALL` не имеет materialized neutral backend owner и потому по конструкции остаётся admitted-denied. citeturn27view0turn27view2

```csharp
public static TrapCompletionRouteDescriptor ProjectionOnlyDenied { get; }
public static TrapCompletionRouteDescriptor RuntimeOwnedPublication { get; }
```

Источник: `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`. Смысл: route authorization и publication — это отдельный нейтральный слой; сама его доступность не даёт фронтенду права объявить успех. citeturn28view0

## Матрица полномочий и текущее состояние

Ключевой инвариант, заданный в приложенном тексте, сводится к отрицанию опасных тождеств: carrier не равен execution, schema не равна availability, projection не равна authority, admission не равен backend execution, route authorization не равен publication, completion не равен retire, evidence не равно migration authority, compiler metadata не равно emission, VMX не равно neutral owner. Именно по этим инвариантам и следует читать текущий слой. fileciteturn0file0 citeturn2view0turn25view1turn25view3

| Область | Текущий статус | Нейтральный владелец | Роль VMX | Что разрешено сейчас | Что должно оставаться denied / future-gated |
|---|---|---|---|---|---|
| VMX frontend | implemented compatibility-only | нет самостоятельного owner; только ingress/projection | frozen decode / alias / ABI surface | decode и классификация совместимых операций | backend state transition, domain ownership |
| VMCS fields | projection-only | runtime descriptors / completion | schema vocabulary | generated read-only/denied schema | mutable store, active pointer, migration authority |
| VMREAD completion-owned fields | current implemented behavior | `CompletionRecord` | compatibility value projection | `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, `EptViolationQualification` | превращение completion в mutable VMCS store |
| VMREAD memory-owned slice | current implemented behavior | `MemoryDomainDescriptor` / read-only translation view | compatibility projection | `GuestCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount` | reuse для host root, authority leak |
| VMREAD execution-owned slice | current implemented behavior | `ExecutionDomainDescriptor` / read-only state view | compatibility projection | `GuestPc`, `GuestSp`, `GuestFlags` | обобщение на privileged/control/host fields |
| `GuestCr0` / `GuestCr4` | future-gated, denied | будущий privileged execution-state owner | none beyond alias/schema | ничего положительного | until explicit owner/value/evidence/migration/tests |
| `VMWRITE` | forbidden / denied | owner absent | denied alias | ничего | все write paths |
| `VMCALL` | admitted-denied | neutral trap policy exists; neutral backend owner absent | VMX exit projection | decode → projection → runtime admission → neutral trap → denied backend | backend success, completion publication, retire publication |
| completion route | designed/fenced | `TrapCompletionRouteService` | downstream projection only | `ProjectionOnlyDenied` path | using `RuntimeOwnedPublication` without backend owner |
| completion publication | fenced | `TrapCompletionPublicationFence` | no authority | deny cleanly on missing backend / route | publication from admitted-denied |
| retire publication | future-gated | separate neutral retire rule | compatibility retire vocabulary only | fail-closed modelling | retire from completion alone |
| capabilities | grant-first neutral model | `CapabilityDescriptorSet` and runtime policies | `VmxCaps` projection only | compatibility visibility | grants from `VmxCaps` |
| evidence | neutral policy | evidence policies / host-evidence boundary | guest-facing projection only | selected guest-visible projections | host evidence leakage |
| memory / I/O / lanes / stream | boundary-gated | their own neutral owners | no authority | field-local read-only projection only | lane passthrough, stream authority, DMA/IOMMU via VMX |
| SecureCompute | zero-authority VMX boundary | SecureCompute descriptors and policies | deny/projection boundary only | selected guarded projection surfaces | activation, grant, VMCS secure state, secure execution via VMX |
| nested virtualization | future-gated neutral model | future child-intent owner | compatibility bridge vocabulary only | none production | Shadow VMCS / VMCS12 / VMCS02 authority |
| compiler emission | no-emission preserved | runtime owner only at execution time | frozen opcode vocabulary | metadata / examples / intent | compiler-generated backend |
| migration / checkpoint | gate only | neutral migration/checkpoint descriptors | no authority | explicit classification work | VMREAD, VMCS projection, completion values, lane tokens, host evidence as payload authority |

Содержательно эта матрица выводится из `Current State And Closure Matrix`, `Roadmap And Residual Risk`, `Admission Boundaries`, `VMCS Projection And Field Access`, `Trap/Completion/Retire`, `Security Invariants` и соответствующих code anchors. citeturn21view0turn21view1turn25view1turn19view2turn19view3turn25view3turn26view0turn26view1turn27view0turn28view0turn28view1

Особо важно развести четыре близких, но не тождественных вещи. `VMREAD`-успех по одному полю не открывает соседние поля; completion projection не открывает migration authority; admission legitimate request не открывает execution; `TrapDecision` или `VmExitReason` не являются runtime policy. Эти запреты сформулированы не как словесная предосторожность, а как архитектурные security invariants и residual-risk red flags. citeturn19view2turn19view3turn21view1turn25view3

Сопоставление `Virtualization plan` и `SecureCompute Phase 09/10` требует особой осторожности. В SecureCompute corpus нейтральный owner-proof и read-only projection для чувствительных полей развиваются внутри SecureCompute descriptor/admission модели; это не означает, что virtualization-side `GuestCr0` / `GuestCr4` уже открыты вообще. Наоборот, virtualization-side Phase 05 оставляет их denied до появления отдельного privileged execution-state owner и полного owner/value/evidence/migration/test package. Репозиторий в доступной форме также подчёркивает, что SecureCompute не является VMX mode и что его activation is opt-in только через `SecureComputeDomainDescriptor`, а не через VMX. citeturn23view0turn25view3 Источник в архиве: `VirtualizationActivationPlan/05_privileged_execution_state_owner_rfc.md`, строки 1–16 и 82–95; `SecureComputeActivationPlan/09_privileged_execution_state_owner_rfc.md`, `10_guestcr0_guestcr4_readonly_projection_plan.md`, `17_secure_vmx_boundary_zero_authority_plan.md`.

## План активации слоя виртуализации через ZIP-корпус

Первый и самый важный практический вывод: в архиве **нет** найденных исполняемых скриптов или явной конфигурации включения. Архив состоит из Markdown-плана. Поэтому буквальная «активация слоя виртуализации в ZIP» в смысле запуска готового скрипта **не указана / не найдена**. Реалистичный, честный план должен быть двухступенчатым: сначала распаковка и верификация планового корпуса, затем — отдельная инженерная реализация ровно одного owner-specific пути в исходном дереве репозитория.

### Таблица найденных файлов и их ролей

| Имя | Путь | Роль | Команды / параметры | Зависимости |
|---|---|---|---|---|
| `00_virtualization_activation_refactoring_index.md` | `VirtualizationActivationPlan/...` | индекс плана; фиксирует readiness-only boundary | нет исполняемых команд | опирается на code anchors `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs` |
| `04_vmread_projection_completion_and_denial_matrix.md` | `VirtualizationActivationPlan/...` | матрица VMREAD: что projected, что denied | предложены `rg`-сканы по `GuestCr0/GuestCr4`, `VmcsField` и fallback paths | зависит от `VmcsFieldProjectionSchema`, value projection services |
| `05_privileged_execution_state_owner_rfc.md` | `VirtualizationActivationPlan/...` | будущий owner для `GuestCr0/GuestCr4` | `rg -n "GuestCr0|GuestCr4|PrivilegedExecution|CR0|CR4" ...` | требуется новый neutral owner; сейчас denied |
| `06_neutral_hypercall_backend_owner_rfc.md` | `VirtualizationActivationPlan/...` | первый рекомендуемый RFC для одного no-state local `VMCALL` leaf | PR split `06A/06B/07A`; exact leaf not selected | нужен `HypercallBackendAdmissionDecision` с allowed backend execution decision; сейчас отсутствует |
| `07_vmcall_success_path_activation_plan.md` | `VirtualizationActivationPlan/...` | будущий success path для `VMCALL` | статический scan по retire/publication markers | зависит от Phase 06, 08, 09 |
| `08_trap_completion_route_publication_plan.md` | `VirtualizationActivationPlan/...` | отдельный gate completion publication | scan по `RuntimeOwnedCompletionPublication|RuntimeOwnedPublication` | зависит от backend owner; не даёт retire |
| `09_retire_publication_activation_plan.md` | `VirtualizationActivationPlan/...` | отдельный gate retire publication | scan по `RetirePublicationAllowed|...|CompatibilityExit` | completion ≠ retire; нужен explicit retire rule |
| `10_vmwrite_neutral_owner_policy.md` | `VirtualizationActivationPlan/...` | policy полного deny для `VMWRITE` | no `WriteFieldValue`, no `VmWrite`, no `VmcsFieldStore` | neutral write owner отсутствует |
| `13_securecompute_virtualization_boundary_plan.md` | `VirtualizationActivationPlan/...` | нулевая власть VMX над SecureCompute | boundary-only | зависит от SecureCompute descriptors/policies, не от VMX |
| `16_conformance_negative_positive_test_matrix.md` | `VirtualizationActivationPlan/...` | обязательная матрица тестов и static scans | содержит конкретные `rg` команды | нужна до и после любого положительного пути |
| `18_release_gate_for_limited_runtime_virtualization.md` | `VirtualizationActivationPlan/...` | конечный release gate | перечисляет цепочку допуска | без него нельзя claim-ить activation |
| `RuntimeBoundaryAdmissionService.cs` | `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/...` | нейтральный admission boundary | runtime validates domain/capability/evidence and denies frontend authoritative mutation | нужен context/root authority/evidence policy |
| `VmxCompatibilityAdmissionService.cs` | `.../Compatibility/Frontend/Handlers/...` | `VMREAD` projection path | `ReadCompatibilityProjection` | зависит от decode, projection validator, runtime admission, read-only value projection |
| `VmxCompatibilityAdmissionService.Traps.cs` | `.../Compatibility/Frontend/Handlers/...` | `VMCALL` admitted-denied chain | uses `MissingNeutralOwner`, `ProjectionOnlyDenied` | зависит от trap policy, backend admission, route, fence |
| `TrapCompletionRoutePolicy.cs` | `.../Runtime/Completion/Routing/...` | route authorization | `ProjectionOnlyDenied`, `RuntimeOwnedPublication` | без backend execution route denies publication |
| `TrapCompletionPublicationFence.cs` | `.../Runtime/Completion/Records/...` | fence completion / retire | differentiates `DeniedBackendExecution`, `DeniedRetirePublication`, `Allowed` | neutral trap + route + retire permission required |

Архив в явном виде предлагает и обязательные диагностические команды. Наиболее важные из них таковы:

```powershell
rg -n "VmcsManager|IVmcsManager|VmxExecutionUnit|ActiveVmcs|VmcsFieldStore" HybridCPU_ISE --glob "*.cs"
rg -n "RuntimeOwnedPublication" HybridCPU_ISE/CloseToHSL/Core/Virtualization HybridCPU_ISE/CloseToHSL/Core/Runtime
rg -n "VmxCaps.*grant|VMCS.*authority|SecureCompute.*VMX|Lane6|Lane7|Stream.*authority" HybridCPU_ISE Documentation
rg -n "<release-policy-overclaim-denylist>" HybridCPU_ISE/docs Documentation
```

Источник: `VirtualizationActivationPlan/16_conformance_negative_positive_test_matrix.md`, строки 105–112.

### Пошаговый реалистичный план

Ниже дан честный план, разделённый на **то, что можно сделать сейчас**, и **то, что потребуется реализовать, если целью действительно является limited activation**.

| Шаг | Команда / действие | Ожидаемый результат | Проверка |
|---|---|---|---|
| Распаковка ZIP | `Expand-Archive -LiteralPath ".\SecureComputeActivationPlan(1).zip" -DestinationPath ".\plan" -Force` | два каталога: `SecureComputeActivationPlan/`, `VirtualizationActivationPlan/` | Microsoft указывает, что `Expand-Archive` извлекает именно ZIP-архивы, `-DestinationPath` создаёт целевой каталог, `-Force` разрешает перезапись. citeturn33view0turn33view1turn33view2 |
| Инвентаризация плана | проверить наличие 20 файлов в `VirtualizationActivationPlan/` и 24 файлов в `SecureComputeActivationPlan/` | подтверждение, что это плановый корпус, а не runtime package | локальная проверка содержимого архива |
| Сборка репозитория | `dotnet build "HybridCPU v2.slnx" -c Release` | репозиторий компилируется целиком | `dotnet build` строит solution/project и их зависимости; solution format `.slnx` поддерживается CLI. citeturn32view1turn1view0 |
| Базовые тесты | `dotnet test "HybridCPU v2.slnx" -c Release --no-build` | текущий baseline подтверждён тестами | `dotnet test` строит/запускает тесты; применимо к .NET 6 SDK и позднее. citeturn32view0turn32view1 |
| Static scans на запрещённые регрессии | выполнить `rg`-команды из Phase 16 | нет reintroduced `VmcsManager`, `VmxExecutionUnit`, active VMCS, VMCS store, premature `RuntimeOwnedPublication` | результаты match должны быть либо пустыми, либо объяснёнными deny/guard context |
| Подтверждение текущего статуса | проверить, что `VMWRITE` denied, `VMCALL` missing owner denied, SecureCompute through VMX denied | текущий статус остаётся readiness/projection only | архив: `16_conformance_negative_positive_test_matrix.md`, строки 36–48 |
| Выбор первого положительного пути | принять owner-specific RFC/ADR для **одного** no-state domain-local `VMCALL` leaf | scope пути строго ограничен | архив: `06_neutral_hypercall_backend_owner_rfc.md`, строки 7–15, 90–107 |
| Реализация neutral backend owner | materialize neutral owner, backend executor, allowed backend admission decision, but still without publication shortcut | `MissingNeutralOwner` исчезает только для exact leaf | пока это не сделано, успеха нет. Источник: `06_neutral_hypercall_backend_owner_rfc.md`, строки 92–120 |
| Реализация completion gate | подключить Phase 08 так, чтобы completion разрешался только после backend authorization; route-only не считался publication | появился разрешённый completion-only path | архив: `08_trap_completion_route_publication_plan.md`, строки 79–90 |
| Реализация retire gate | отдельно ввести explicit retire rule, rollback/evidence/migration checks | completion publication не равен retire publication | архив: `09_retire_publication_activation_plan.md`, строки 7–16 |
| Повторные positive и adjacent negative tests | гонятся exact-leaf positive tests и все adjacent denials | только approved leaf positive; всё соседнее остаётся denied | recovery, rollback, host-evidence, migration class, nested, SecureCompute, lane/stream — всё проверяется отдельно |
| Release claim boundary | обновить документацию и пройти Phase 18 gate | можно claim-ить только **limited runtime virtualization activation for exactly one path** | архив: `18_release_gate_for_limited_runtime_virtualization.md`, строки 36–49 |

Следующий поток отражает не текущую действительность системы «как есть», а правильный инженерный маршрут, который выводится из ZIP-плана и репозиторных WhiteBook.

```mermaid
flowchart LR
    A[Распаковка ZIP-корпуса] --> B[Проверка структуры плана]
    B --> C[Сборка и baseline-тесты репозитория]
    C --> D[Static scans на запреты]
    D --> E[Принятие owner-specific RFC/ADR]
    E --> F[Включение neutral backend owner]
    F --> G[Гейт completion publication]
    G --> H[Гейт retire publication]
    H --> I[Валидация exact-leaf positive path]
    I --> J[Проверка соседних denial paths]
    J --> K[Обновление claim boundary]
```

### Почему это именно план, а не готовая инструкция включения

ZIP-корпус с высокой последовательностью повторяет одну и ту же идею: сначала documentation/process/static gates, затем field-by-field owner work, затем no-state hypercall owner RFC, затем separate completion gate, затем separate retire gate, затем migration/evidence classification, вплоть до конечного release gate. В нём нет признаков package-level `enable.sh`, `enable.ps1`, конфигурационного флага наподобие `VirtualizationEnabled=true` или production deployment manifest. Более того, сами фазы формулируются так, чтобы не позволить подмену: `admitted-denied is not backend success`, `backend success != completion publication`, `completion publication != retire publication`, `schema membership is not field availability`, `all writes remain denied`. Источник в архиве: `00_virtualization_activation_refactoring_index.md`, `04_vmread_projection_completion_and_denial_matrix.md`, `06_neutral_hypercall_backend_owner_rfc.md`, `07_vmcall_success_path_activation_plan.md`, `08_trap_completion_route_publication_plan.md`, `09_retire_publication_activation_plan.md`, `10_vmwrite_neutral_owner_policy.md`, `18_release_gate_for_limited_runtime_virtualization.md`.

## Риски, тесты и итоговый вердикт

Главный риск здесь не технический, а методологический: неверно классифицировать уже существующую projection/admission инфраструктуру как activation. Это может произойти четырьмя типичными способами. Первый — принять `VMREAD` success по одному полю за общее открытие VMCS/VMX state. Второй — принять neutral trap result или `VmExitReason.VmCall` за backend success. Третий — принять route authorization за publication. Четвёртый — принять completion publication за retire publication. WhiteBook и code anchors именно против этих shortcut-ошибок и построены. citeturn19view2turn19view3turn21view1turn25view3turn27view0turn28view0turn28view1

Второй класс рисков связан с регрессией authority model. Признаками такой регрессии будут возвращение `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, mutable VMCS store, active VMCS pointer, use of `VmExitReason` inside neutral runtime policy, use of `VmxCaps` as capability source, SecureCompute-through-VMX, compiler active VMX backend emission, lane passthrough и превращение host evidence или lane tokens в migration payload. Именно эти red flags перечислены в WhiteBook residual risk и в plan-static scans. citeturn21view1turn25view3 Источник в архиве: `16_conformance_negative_positive_test_matrix.md`, строки 36–48 и 105–112.

Обязательный набор тестов я бы сформулировал так. Положительные тесты допустимы только для того, что уже доказано: partial read-only `VMREAD` field slices, admitted-denied `VMCALL`, deny-by-default `VMWRITE`, zero-authority SecureCompute boundary, no-emission compiler boundary. Все соседние состояния должны иметь негативные тесты: `GuestCr0` / `GuestCr4`, host aliases, control fields, unknown fields, nested state, SecureCompute through VMX, use of `RuntimeOwnedPublication` before backend owner, lane6/lane7 token leakage, migration of completion or VMCS projection data, checkpoint authority from compatibility metadata. Это согласуется и с `Phase 16`, и с repository security invariants. citeturn21view0turn21view1turn25view3 Источник в архиве: `16_conformance_negative_positive_test_matrix.md`, `15_migration_checkpoint_restore_authority_plan.md`, `13_securecompute_virtualization_boundary_plan.md`.

С точки зрения исправлений или усилений до Phase 18 я бы обозначил пять первоочередных пунктов. Во-первых, зафиксировать exact first leaf для Phase 06 и не оставлять его в неопределённости. Во-вторых, материализовать explicit allowed backend execution decision, которого сейчас нет. В-третьих, не смешивать completion-only и retire-capable route semantics; текущий код уже помогает это делать, но plan должен сохранять разделение и в naming. В-четвёртых, явно развести virtualization-side `GuestCr0` / `GuestCr4` и SecureCompute-side owner proof, чтобы никто не обобщил второй на первый. В-пятых, сохранить жёсткий doc-claim discipline: любые фразы про activation допустимы только после update claim boundary по Phase 18. Источник в архиве: `06_neutral_hypercall_backend_owner_rfc.md`, строки 90–120; `08_trap_completion_route_publication_plan.md`, строки 79–90; `18_release_gate_for_limited_runtime_virtualization.md`, строки 36–49. citeturn21view1turn25view3turn27view0turn28view0turn28view1

Окончательный вердикт по вашей шкале — **A. activation not supported; readiness/projection only.** Обоснование строгое. Репозиторий подтверждает partial read-only `VMREAD`, admitted-denied `VMCALL`, neutral route/fence machinery, deny-by-default `VMWRITE`, SecureCompute zero-authority boundary и compiler no-emission, но одновременно прямо отрицает successful VMX backend execution, mutable VMCS backing store, active VMCS pointer, successful VMCALL backend hypercall path, feature-complete VMREAD backend execution и любые positive paths без neutral owner/publication/retire chain. ZIP-корпус с этим не конфликтует: он не открывает activation, а лишь планирует её на будущее и снабжает её строгими gate-условиями. Следовательно, архитектура собственного authority model не нарушает, но до actual activation ещё не дошла. citeturn18view0turn19view1turn19view2turn19view3turn21view0turn21view1turn25view1turn25view2turn25view3turn27view0turn28view0turn28view1

Коротко об ограничениях. Полный repo-wide поиск по всем файлам через GitHub connector в этой сессии выполнить не удалось из-за недоступности самого callable GitHub resource через `api_tool`; поэтому формула «все фрагменты из репозитория» в строгом машинном смысле ограничена проверенными архитектурными документами и прямыми code anchors, а не абсолютным перебором каждого файла. Кроме того, ZIP-корпус оказался документационным, а не исполняемым: явной процедуры runtime enable внутри архива нет. Но на уровне высоконагруженных выводов это ограничение результата не меняет: **сегодня есть корректно ограждённый compatibility/projection layer; готовой limited runtime activation ещё нет**.
