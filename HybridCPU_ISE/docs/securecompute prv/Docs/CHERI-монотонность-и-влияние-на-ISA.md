

## 1. CHERI-подобная монотонность политик — не ломает ISA

Правило:

```text
дочерний secure domain не может получить больше прав, чем родитель;
производная secure policy не может расширить host inspection;
производная migration policy не может разрешить больше payload/evidence;
производная compatibility projection policy не может открыть больше полей.
```

— **не ломает текущую ISA**, если реализовано как проверка на уровне доменных дескрипторов, вложенности, миграции и runtime-допуска.

Это не требует:

новых кодов команд;

новых регистров общего назначения;

нового формата операндов;

нового декодирования `LOAD/STORE/BRANCH`;

изменения обычных non-VMX инструкций;

изменения Stage A для существующих инструкций.

Это добавляет инвариант уровня **Stage B / runtime policy**:

```text
если secure domain активен
  → производная политика не может быть шире исходной

если secure domain не активен
  → поведение обычного домена прежнее
```

То есть это **прозрачное усиление доменной модели**.

В терминах HybridCPU это похоже не на изменение ISA, а на усиление `RuntimeBoundaryAdmissionService`, `SecureComputeDomainDescriptor`, `SecureMigrationDescriptor`, `SecureEvidencePolicy`, `SecureIoDomainDescriptor`, `SecureHypercallDescriptor`.

Такой вариант безопасен для текущей архитектуры.

## 2. CHERI-подобная capability-aware memory model — уже не полностью прозрачна

А вот это другое:

```text
новая ISA-подмодель;
новый режим операндов/адресации;
VLIW/EPIC metadata с capability-bearing operands;
capability-aware LOAD/STORE/FETCH;
tagged memory;
capability registers;
bounds/permissions/provenance на каждом указателе.
```

Вот это **уже затрагивает ISA**.

CHERI как архитектура именно так и работает: она добавляет аппаратно поддерживаемые capabilities, которые несут адрес, границы, права и защищаются тегами; обращения к памяти используют эти capability-значения как основание доступа, а не просто обычный адрес. 

Поэтому если HybridCPU захочет получить **настоящую CHERI-подобную строгость на каждый указатель / каждый доступ к памяти**, это уже не будет полностью невидимо для:

декодера;

модели операндов;

исполнительных дорожек `LOAD/STORE/FETCH`;

формата регистров или метаданных операндов;

модели памяти;

исключений;

retire/publication;

компилятора/ABI;

тестов ISA-легальности.

Это не обязательно “ломает” старую ISA в смысле уничтожения совместимости, но это **непрозрачное расширение ISA**.

Правильнее сказать:

> **оно не обязано ломать существующий ISA, если сделано как opt-in расширение, но оно уже перестаёт быть полностью прозрачным для ISA.**

## 3. Три уровня интеграции

### Уровень A — доменная secure policy

```text
SecureComputeDomainDescriptor
SecureMigrationDescriptor
SecureEvidencePolicy
SecureIoDomainDescriptor
SecureHypercallDescriptor
policy monotonicity
parent/child rights narrowing
host inspection narrowing
migration evidence narrowing
projection narrowing
```

Статус:

```text
ISA не ломает;
декодер почти не трогает;
Stage A не меняет;
Stage B усиливает только для secure-доменов;
non-VMX инструкции работают как раньше вне secure-домена.
```

Это рекомендуемый ближайший уровень.

### Уровень B — secure memory на уровне домена/страниц/областей

```text
SecureMemoryDomainDescriptor
private/shared/measured memory
domain tag
address-space tag
epoch
second-stage root
host-readable denied
DMA mediation
```

Статус:

```text
ISA почти не ломает;
обычный LOAD/STORE остаётся тем же;
Stage A почти не меняется;
Stage B / memory access policy усиливается;
внутри secure-домена LOAD/STORE могут получать новые отказы.
```

Это всё ещё совместимо с текущей ISA, если не менять формат операндов.

### Уровень C — настоящая CHERI-подобная memory capability ISA

```text
capability registers
capability pointers
tagged memory
capability load/store
capability-aware instruction fetch
bounds/permissions/provenance на каждом указателе
sealed capabilities
capability derivation instructions
```

Статус:

```text
это уже ISA extension;
декодер должен знать новые формы;
Stage A должен проверять новые операнды/режимы;
Stage B должен проверять capability-семантику;
LOAD/STORE/FETCH получают новую семантику;
компилятор/ABI должны измениться.
```

Это не “ломает” текущую ISA, если оставлено как отдельный режим или расширение, но оно **не прозрачно**.

## 4. Что значит “ломает” в инженерном смысле

Есть три разных случая.

### Не ломает

```text
старый код исполняется как раньше;
старые инструкции декодируются как раньше;
старые тесты проходят как раньше;
secure compute не активен без secure descriptor;
отсутствие secure descriptor означает обычный домен, а не отказ.
```

Это применимо к монотонности политик и доменному secure compute.

### Не ломает, но расширяет

```text
старый код работает;
новый secure/capability код использует новые режимы;
декодер знает новые формы;
Stage A/B получают новые ветки;
ABI получает capability-aware вариант.
```

Это применимо к CHERI-подобному opt-in ISA расширению.

### Ломает

```text
обычный LOAD/STORE внезапно требует capability;
обычный указатель перестаёт быть допустимым;
Stage A начинает отвергать старые инструкции;
отсутствие secure policy приводит к отказу;
VMX/VmxCaps включает secure compute глобально;
старый ABI больше невалиден.
```

Так делать нельзя.

## 5. Ответ на ваш вопрос напрямую

**Монотонность доменной политики** — нет, не ломает ISA и может быть прозрачной.

**SecureComputeDomainDescriptor и связанные secure-дескрипторы** — нет, не ломают ISA, если они opt-in и активны только для secure-доменов.

**SecureMemoryDomainDescriptor на уровне private/shared/measured memory** — обычно не ломает ISA, но может давать новые Stage B отказы внутри secure-домена.

**Настоящая CHERI-подобная capability-aware memory model** — да, это уже затрагивает ISA и не является полностью прозрачной. Она может быть совместимым расширением, но не невидимым изменением.

Краткая формула:

```text
HybridCPU Secure Compute as domain policy
  → прозрачно для текущей ISA

CHERI-inspired monotonic policy
  → прозрачно для текущей ISA

CHERI-like per-pointer capability memory
  → ISA extension, не полностью прозрачно

Forcing all old pointers/instructions into CHERI semantics
  → ломает совместимость
```

Поэтому ближайший безопасный путь для HybridCPU:

```text
сначала:
  secure domain policy
  monotonic descriptor derivation
  private/shared/measured memory policy
  evidence/migration/IO/hypercall deny-by-default

позже, отдельным расширением:
  capability-bearing operands
  tagged memory
  capability-aware load/store/fetch
```

Так HybridCPU получает часть CHERI-дисциплины без разрушения текущей ISA, а настоящую CHERI-подобную строгость добавляет только как отдельную, явно включаемую ISA-подмодель.