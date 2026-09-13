
# Итог аудита

Я зафиксировал текущие ревизии и анализировал именно их:

- `DoomSharpPort` — `d6c126293ec9109f4699eddd9759e7f58144565a`. 
- `HybridCPU-v2` / ISE — `38bf0614d8a58e2543b4a956ccc23bb22e1a8170`. 
- `HybridCPU_Compiler_v2` — `2e2ec0bd119c03437a407ebe17f29dc3cba57056`. 

Главный вывод: **сам `DoomSharp.Core` уже очень близок к подходящему managed guest workload. В нём нет архитектурной зависимости от WPF, Windows filesystem, `Task`, threads или reflection, которую пришлось бы вырезать.** Основной разрыв до первого полноценного запуска — это не “слишком много .NET”, а **guest bootstrap + точная CoreLib/runtime-library closure + один нестандартный interface-dispatch case + platform adapters**. После этого основные проблемы смещаются в correctness фатальных путей и особенно в renderer/GC performance.

При этом есть важная оговорка о source of truth. Видимый HEAD compiler всё ещё содержит старые restricted/unsupported managed contracts и restricted image builder, тогда как RefPlan7 описывает их замену полноценным bootstrap/runtime contract.    По вашему условию я **не считаю это ограничением HybridCPU** и исхожу из Phase 0–16 completed. Но для реального запуска должен выбираться именно Phase16 release profile с соответствующими ABI/evidence digests, а не этот старый restricted contour. Phase16 прямо требует связывать release с конкретными compiler/runtime/kernel/ISE SHA и не объявлять capability поддержанной без закрытого evidence manifest. 

---

## Что фактически требуется от managed runtime

`DoomSharp.Core.csproj` — обычная `net8.0` library, без guest `Main`/entrypoint и без package dependencies.  Поэтому основной reachable CIL — значительно проще типичного desktop .NET приложения.

| Область | Фактическое использование Core | RefPlan7 |
|---|---|---|
| Managed objects/references | `DoomGame`, `GameController`, `MapObject`, `Column`, UI/game controllers, linked thinkers | **Покрыто** Phase 2/5 |
| Arrays | массивы объектов, jagged arrays, `byte[]` framebuffer/WAD/cache, reference-bearing arrays | **Покрыто** Phase 4/5 |
| Strings | WAD names, console/error strings, title, resource lookup | **Покрыта базовая semantics**, BCL methods должны быть в runtime-library profile |
| Value types | `Fixed`, `Angle`, map structs, `Nullable<T>`, `Patch` с reference fields | **Покрыто** Phase 4 |
| Static initialization | `DoomGame.Instance`, state tables, RNG/tables, renderer globals | **Покрыто**, static roots обязательны |
| Virtual/interface dispatch | platform interfaces, ordinary object virtuals | **Покрыто** Phase 6 |
| Default interface methods | `IConsole.WriteLine` | **не задано явно Phase 6**, отдельный gate |
| Boxing/casts/type tests | value-type `Equals(object)`, `is MapObject`, interfaces | **Покрыто** |
| Closed generics | `Nullable<T>`, `IEquatable<T>`, `IComparable<T>` и BCL helper instantiations | **Покрыто** Phase 8 |
| Managed EH | явный `throw` практически только в argument guards; implicit exceptions остаются | **Покрыто** Phase 9 |
| CPU traps | bounds/null/protection остаются отдельным failure domain | **Покрыто правильно** Phase 10 |
| Delegates | не являются основой game dispatch | Доступны, но Doom почти не зависит |
| Async | в Core отсутствует | Не нужен |
| Threads/TLS | в Core отсутствуют | Не нужны для first run |
| Reflection | `typeof` в Core не найден | Не нужна |

Поиск текущего Core действительно не обнаруживает `Task`, `Thread`, `async` или `typeof`; явный `throw` найден только в `DoomGame`.     

---

# P0 — AOT / image / boot blockers

| Место | Что происходит | Требуемый CIL/runtime contract | HybridCPU status | Почему blocker | Исправление |
|---|---|---|---|---|---|
| **`DoomSharp.Core.csproj` + `DoomGame.Run`** | Core — library. `Run(GameMode, byte[])` требует WAD и заранее установленные platform services; самостоятельного guest entry нет. | `.hcexe` entry symbol → runtime registration → static roots/cctors → service setup → WAD blob → `Run`. | **RefPlan7 поддерживает**: Phase1 bootstrap/image contract, Phase13 services. | Из Core самого по себе нельзя получить bootable Doom `.hcexe`: loader не знает, откуда взять args/WAD и кто установит adapters. | Добавить **отдельный tiny guest launcher**, не переписывать Core: зарегистрировать adapters, получить boot WAD blob, вызвать `Run`. |
| **`IConsole.WriteLine`** | `WriteLine` имеет тело прямо в interface; `NullConsole` его не реализует.   | CIL `callvirt` к interface method + **default interface implementation resolution**. | **Не доказано контрактом Phase6.** Phase6 определяет interface maps/slots/ordinary interface resolution, но не DIM semantics.  | Если DIM не находится resolver'ом, compilation должен fail-closed или вызов уйдёт не туда. | Либо явно квалифицировать DIM в runtime/compiler, либо минимально убрать DIM из guest ABI — сделать `WriteLine` обычным обязательным member/static helper. |
| **Roslyn/CoreLib closure** | В Core есть `$"..."`, `Array.Copy/Clear/Empty`, `string.Equals/Compare`, `Environment.NewLine`, `Math.*`, primitive `ToString`, exception ctors. | Реальные generated MemberRef/MethodSpec должны резолвиться в managed runtime library/helper surface. | Архитектурно **предусмотрено**, но это не автоматически “весь net8 BCL”. Phase16 требует evidence для конкретного enabled surface. | AOT может успешно поддерживать objects/strings и всё равно остановиться на отсутствующем BCL member/helper. | Перед machine lowering сделать **CIL dependency closure report** и whitelist every `System.*` member. Реализовать/mapping missing members в bounded CoreLib. |
| **Interpolated strings** | Современный Roslyn для части non-constant interpolation может вводить `DefaultInterpolatedStringHandler`/`AppendFormatted<T>` вместо простого concat. В compiler HEAD DIM/handler-specific implementation evidence не найдено. | Handler value type, helper methods, closed generic formatting calls или compiler canonicalization в supported concat. | **Conditional**: runtime-library architecture есть, конкретный handler не доказан. | Это hidden dependency, которой нет явно в C# source. | Предпочтительно закрыть её bounded runtime-library implementation либо compiler-side canonical lowering; source Doom менять только если profile сознательно не поддерживает handler. |

Последние два пункта — именно **qualification gates**, а не утверждение “RefPlan7 этого не умеет”. Без готового CIL artifact в репозитории нельзя честно перечислить точные Roslyn MethodRef tokens: их надо получить из фактически используемой Roslyn/net8 сборки и пропустить через AOT preflight. В частности, поиск compiler HEAD не показывает отдельной реализации `DefaultInterpolatedStringHandler`. 

### Отдельный P0 platform/repository caveat

Видимый старый `HybridCpuRestrictedImageBuilderV1` требует explicit entry symbol и всё ещё запрещает некоторые managed-runtime features, которые RefPlan7 как раз должен заменить.  Это **не повод переписывать Doom**. Если при реальном запуске toolchain всё ещё попадает в этот profile, проблема в выборе/интеграции Phase16 production image builder.

---

# P1 — correctness/runtime risks

| Место | Что происходит | Contract | Support | Почему проблема | Исправление |
|---|---|---|---|---|---|
| **`DoomGame.Error` / `Quit`** | `Error()` пишет сообщение, вызывает `Quit()`, а `Quit()` лишь вызывает `IConsole.Shutdown()` и **возвращается**.  | Doom fatal error должен быть no-return; managed exception/process exit должны оставаться отдельны от CPU trap. | Phase9/10 + RuntimeKernel `process_exit` это покрывают. | После fatal WAD/render/game error вызывающий код может продолжить работу с invalid state. Quit из UI также не гарантирует termination. | Fatal managed exception, пойманный один раз bootstrap'ом → clean process exit, либо explicit noreturn process-exit host/kernel service. Не использовать случайный CPU trap. |
| **`IDoomClock` / wipe + `TryRunTics`** | `TryRunTics` берёт разницу clock и выполняет весь backlog; wipe имеет polling loops по `GetTime()`.  | Monotonic **virtual/emulated tic source**, который прогрессирует независимо от `WaitTic`, плюс deterministic wait/yield. | Phase13/16 это предполагают. | Clock вида нынешнего `NullDoomClock`, который растёт только в `Wait*`, может зависнуть в polling path. Wall-clock даст большие catch-up bursts после паузы ISE. | RuntimeKernel clock должен быть virtual monotonic; input/timer sequence — replayable. Не использовать host `Stopwatch` semantics guest-side. |
| **`RenderEngine.GetColumn`** | Composite path делает `new byte[composite.Length-ofs]`, копирует **весь хвост**, потом возвращает `new Column`.  | Renderer нужен bounded view `(buffer, offset, textureHeight)` либо эквивалентная exact column semantics. | Обычные arrays поддержаны; проблема в source algorithm. | Помимо allocation storm, длина source становится не длиной texture column. При texture height меньше оставшегося composite buffer sampling потенциально может читать следующий column вместо корректного wrap/repeat. | Возвращать view/offset+length без копии; exact length должна соответствовать column/texture semantics. Это надо regression-test'ить pixel hashes против CoreCLR. |
| **`WadFile` / `ByteReader`** | Header/count/offset/size в основном доверяются WAD, дальше идут allocations/indexing.   | Bounds/overflow checks должны завершаться controlled managed fatal, а не случайным OOM/bounds fault. | Managed exceptions/traps поддержаны. | Corrupt WAD может превратить data error в arbitrary `IndexOutOfRange`, huge allocation/OOM или partial initialization. | Проверять lump count, directory arithmetic, offset+size, negatives и integer overflow до allocations. |
| **`GameController.P_LoadThings`** | Для неподходящего Doom2 thing в non-commercial mode выставляется `spawn=false`, после чего выполняется `break`, то есть прекращается разбор всего THINGS lump.  | Чистой CPU/runtime зависимости нет. | N/A, source correctness. | PWAD с одним несовместимым thing потеряет все следующие map things. Для stock Doom1 обычно не проявится. | `continue`, а не `break`. |
| **`GameController` special-hit buffer** | `_specHit[_numSpecHit++]` полагается на фиксированную capacity.  | Normal array bounds semantics. | Поддержано. | На карте, превышающей оригинальные implicit limits, получится managed bounds exception в gameplay path. | Явно enforce/map-limit либо bounded growth, если нужна PWAD compatibility. |
| **Input service boundary** | Core ring buffer не synchronized — и это нормально при synchronous `StartTic` pull.  | Host service не должен асинхронно re-enter managed guest без scheduler/synchronization contract. | Phase11/12 доступны, но не нужны. | Ошибочный host implementation создаст race в input queue и nondeterminism. | На first run делать host input **pull-only at tic boundary**, на guest execution context. |

Для stock retail IWAD из этих пунктов наиболее важны **clock contract** и `GetColumn`. Fatal-path и malformed-WAD issues могут долго не проявляться.

---

# P2 — ISE performance / GC scalability

Здесь находится основная работа после первого boot.

| Место | Что происходит | Почему особенно дорого в ISE | Рекомендация |
|---|---|---|---|
| **`RenderEngine.GetColumn`** | allocation + `Array.Copy` + `Column` на composite-column access.  | Может происходить на renderer path с частотой порядка rendered columns, создавая enormous GC traffic и memory bandwidth. | **Самый первый renderer fix**: offset/view в persistent composite buffer, zero per-column allocation. |
| **`Patch.GetColumnByOffset`** | Каждый lookup линейно проходит `ColumnOffsets`.  | На CoreCLR десятки сравнений малозаметны; в cycle-accurate ISE multiplicative overhead велик. | При parse создать direct offset→column/index metadata либо передавать известный index. |
| **`GenerateLookup` / `GenerateComposite`** | Повторно вызывают `Patch.FromBytes(...)`, хотя renderer уже имеет `_patchCache`; создаются `Patch`, offsets, `Column` objects и pixel arrays.  | Большой startup/first-texture allocation burst. | Использовать единый parsed patch cache. |
| **`Patch.FromBytes`** | Каждая patch column разбирается в linked `Column` objects; каждый post получает новый pixel `byte[]`.  | Много маленьких объектов + poor locality. | Для первого запуска допустимо; затем packed immutable patch representation/view поверх WAD lump. |
| **`GameController.RunThinkers`** | `new ActionParams(...)` для каждого active thinker на каждом 35 Hz tic.  | Постоянный allocation stream → частые STW mark/sweep. Phase5 GC — precise, non-moving, non-generational.  | Передавать small struct/by-value args или reusable context; убрать per-thinker heap allocation. |
| **Player/state actions** | Дополнительные `new ActionParams` при state/weapon transitions.  | Та же проблема, меньшая частота. | После `RunThinkers`. |
| **WAD cache** | Первый read lump копирует его в новый `byte[]`; source WAD image при этом остаётся resident. PurgeTag ничего реально не освобождает.  | Raw WAD + copied lumps + parsed Patch pixel copies могут одновременно жить в heap. | На первом этапе хотя бы cache accounting/eviction; затем slice/view поверх immutable WAD image там, где lifetime позволяет. |
| **`Zone`** | Original Doom zone allocator фактически отключён; `Initialize` пуст.  | `PU_CACHE` больше не означает original Doom eviction semantics. Всё решает tracing GC + reachable refs. | Не возвращать старый zone allocator. Просто сделать managed caches действительно releasable. |
| **WAD name lookup** | Reverse linear case-insensitive scan. В одном tic path проверяется `"map01"` через WAD lookup. | O(lump-count) string comparison в 반복яющемся game path. | Вычислить capability/resource IDs один раз при startup или создать deterministic name index. |
| **`AddThinker`** | Для добавления thinker проходится linked list до tail.  | O(n) spawn path. | Хранить tail/sentinel. |
| **`DrawColumn` / `DrawColumnLow`** | Каждый pixel выполняет fixed arithmetic, array bounds, `% _dcSource.Length`, несколько chained object/array accesses.  | Именно такие tiny operations детализированный ISE делает значительно дороже native CoreCLR. | После elimination allocations профилировать; hoist buffers/lengths, eliminate modulo где texture mask известен. |
| **Sprite init** | Для каждого sprite повторно сканируются lump names, создаются `SpriteFrame` objects.  | Дорогой boot/loading, но не frame hot path. | Отложить после frame/tic allocation fixes. |
| **Framebuffer service** | Core естественно передаёт `byte[]` 320×200 одним `ScreenReady` на кадр; Windows host сегодня делает полный `Array.Copy`.  | 64 KiB host crossing/copy per frame может быть заметен в detailed ISE. | First run: synchronous bulk copy. Затем registered framebuffer handle/shared region, но **не raw managed-object knowledge в ISE**. |

Особенно важно: **не начинайте оптимизацию с `DrawColumn` arithmetic**, пока `GetColumn` и `ActionParams` продолжают выделять память. Иначе основная ISE/GC стоимость останется.

---

# P3 — cleanup

- `DoomGame` title string формируется так, что format-like `{0}.{1}` и version concatenation выглядят подозрительно; это cosmetic, не boot blocker.
- `GlobalUsings.cs` импортирует `Collections.Generic`, LINQ и `Text`, хотя фактический Core практически ими не пользуется.
- `DoomConvert.ToInt16(byte[])` проверяет `Length != 4`, хотя возвращает 16-bit значение; поиск показывает, что сейчас method не вызывается из Core, поэтому это dead-code cleanup, а не runtime issue.  
- `Zone` можно позже переименовать/документировать, чтобы никто не ожидал original Doom allocator semantics. 

---

# GC correctness

С точки зрения **correctness**, текущая модель Doom хорошо ложится именно на RefPlan7 GC.

`DoomGame.Instance` является большой static root, от него достижимы renderer/video/game/menu/HUD/WAD и значительная часть долгоживущего graph. Thinkers и world structures содержат linked lists/cycles. `Column.Next` образует chains. `Patch` интереснее: это `readonly struct`, содержащий `uint[]` и `Column?[]`, а renderer держит `Patch?[]`.  Это требует GC pointer maps для **reference-bearing value type внутри `Nullable<T>` внутри array**.

Но это не проблема Doom: Phase4/5 именно это и должны уметь. Phase5 прямо требует static roots, boxed/reference-bearing values, object fields, array elements, cycles/diamonds/shared tails и exact final-PC maps. 

То есть я **не вижу GC correctness blocker в самом object graph**, если Phase5 действительно закрыт. Вижу GC **pressure** blocker для скорости.

Non-moving GC также упрощает framebuffer адресную стабильность, но host всё равно не должен сохранять “сырой адрес payload managed byte[]” без handle/root/lifetime contract. Правильная abstraction — RuntimeKernel host service, а не ISE, читающий TypeDescriptor/object header.

---

# WAD и filesystem boundary

Здесь порт уже сделан правильно.

Windows frontend сам делает `File.ReadAllBytes("DOOM.WAD")`, после чего передаёт Core обычный `byte[]`. `Task.Run`, `System.IO`, WPF и input framework остаются снаружи guest. 

Core `WadFile` работает только с resident image. 

Поэтому HybridCPU guest **не нужен filesystem API вообще** для первого run. RuntimeKernel/host launcher может:

`host file → boot/module blob → guest byte[] → DoomGame.Run`.

Это гораздо лучше, чем портировать `System.IO` в guest.

---

# Platform contract: clock, input, console, framebuffer, exit

Существующая dependency inversion фактически уже задаёт почти готовый guest ABI. Windows frontend устанавливает реализации через:

`SetConsole`, `SetOutputRenderer`, `SetClock`. 

Для HybridCPU нужны эквивалентные managed adapters:

**Console.** `Write`, title и fatal/process exit через Phase13 host service. `Shutdown()` нельзя считать no-return.

**Clock.** Virtual deterministic `GetTime`, `WaitTic`, `WaitVBL`. Windows реализация с `Stopwatch`/`Thread.Sleep` host-only и не должна переноситься. 

**Input.** `StartTic()` синхронно забирает накопленные host events. Это хороший deterministic boundary: события timestamp/order фиксируются host/replay layer и публикуются guest только на tic boundary.

**Framebuffer.** `byte[320*200]` — обычная guest RAM. `UpdatePalette(byte[768])` задаёт palette. `ScreenReady(byte[])` означает “снимок готов”. Никакой специальный managed framebuffer opcode ISE не нужен.

**Process exit/fatal.** RuntimeKernel process exit должен быть отдельным доменом от managed exception и CPU trap. Phase9/10 как раз проводят эту границу: managed EH не является architectural trap; illegal/integrity faults не должны превращаться в произвольные Doom exceptions.  

---

# Arithmetic / Doom determinism

Здесь **не нужно переписывать Doom ради HybridCPU**.

`Fixed` использует нормальные 16.16 operations с 64-bit intermediate multiply/divide. `Angle` хранит BAM в `uint`, то есть wraparound является частью semantics. RNG — фиксированная Doom table. Это всё должно проходить обычным CIL→machine lowering. Требуются точные:

- signed/unsigned 32- и 64-bit arithmetic;
- arithmetic vs logical shifts;
- `div` и `div.un`;
- truncation toward zero;
- `conv.i4/u4`;
- unchecked integer wrap;
- correct `(uint)` comparisons.

ISE HEAD имеет scalar `MUL`, `DIV`, `DIVU`, shifts, word variants и indirect `JALR` в code-confirmed instruction inventory. 

Никакого floating-point conversion Doom fixed math для first gameplay не требует.

`DoomConvert.ToInt32` явно собирает little-endian integer по byte shifts, так что guest endianness dependency минимальна. 

---

# Roslyn-generated machinery

В текущем Core я не нашёл реальной необходимости в closures/state machines/records-generated runtime machinery на основных paths. Значимые hidden cases другие:

- interpolated strings → возможный `DefaultInterpolatedStringHandler`;
- null-coalescing throw → `newobj ArgumentNullException` + `throw`;
- `obj is Angle b` → type test/unboxing;
- `is MapObject mo` → `isinst`/cast path;
- `Nullable<T>` fields → closed generic value-type layout;
- auto-properties/static property initializers → `.cctor`;
- interface calls → `callvirt`;
- default interface implementation → особый DIM resolution case;
- array accesses → implicit bounds/null exception policy;
- ordinary string concatenation/format conversion → CoreLib calls.

То есть Roslyn не создаёт здесь сложную async/delegate ecosystem, но **может расширить BCL closure заметно относительно видимого C#**.

---

# Что выглядит необычно для bare-metal guest, но уже не требует переписывания

Вот вещи, которые я **не считаю проблемами** при вашем Phase0–16 baseline:

- `DoomGame.Instance` и крупный graph объектов;
- jagged arrays;
- strings и string literals;
- `Patch?[]` и другие reference-bearing structs;
- nullable value types;
- cyclic linked thinkers;
- interfaces и normal `callvirt`;
- casts / `isinst`;
- boxing/unboxing;
- closed generics;
- static constructors и огромные static state tables;
- managed exceptions;
- delegates/function pointers, даже если позже появятся;
- exact fixed-point 64-bit arithmetic;
- uint angle wraparound;
- resident WAD в guest `byte[]`;
- managed `byte[]` framebuffer;
- host clock/input/console/frame presentation через typed services;
- non-moving precise GC;
- absence of original Doom zone allocator.

RefPlan7 GC специально требует cycles, static roots, array/reference-bearing values и runtime-helper roots; renderer/game state по форме не выходит за этот contract. 

---

# Требования к конкретному `.hcexe` bootstrap

Для Doom нужен такой минимальный production sequence:

1. Loader проверяет Phase16 `ImageAbi/ManagedAbi/KernelAbi/TrapAbi` profile.
2. RuntimeKernel создаёт execution context, stack, VM, trap association.
3. Managed Runtime регистрирует code manager, final-PC method maps, GC stack maps, EH/unwind tables.
4. Linker применяет normal code/data/type/helper/host-service relocations.
5. Регистрируются static storage/static roots.
6. Выполняются module/type `.cctor` в deterministic order; в том числе создаётся `DoomGame.Instance`.
7. Bootstrap создаёт `HybridConsole`, `HybridGraphics`, `HybridDoomClock`.
8. Host/boot layer предоставляет WAD как immutable blob; bootstrap материализует/принимает guest `byte[]`.
9. Вызываются `DoomGame.SetConsole/SetOutputRenderer/SetClock`.
10. Bootstrap вызывает `DoomGame.Instance.Run(GameMode.Retail, wad)`.
11. `ScreenReady` выполняет один typed bulk host transition на кадр или публикует registered framebuffer handle.
12. Fatal managed error/unhandled exception завершает guest process через RuntimeKernel; CPU trap остаётся отдельным failure path.

Это соответствует направлению Phase1: `image load → kernel context → runtime/code-manager registration → heap/static roots → initialization → managed entry → process_exit`. Изменения RefPlan7 HEAD прямо описывают этот replacement для старого restricted image path. 

---

# Verdict

**1. Можно ли уже подавать текущий `DoomSharp.Core` в HybridCPU AOT?**

**Да — как AOT input/preflight workload. Нет — как самодостаточное bootable application image.**

Я не вижу в Core конструкции уровня “Doom принципиально несовместим с RefPlan7”. Objects, arrays, reference-bearing structs, static init, GC, ordinary dispatch, casts, generic value types и EH лежат внутри предусмотренного profile.

Перед machine emission нужны два обязательных admission checks: **DIM** и **точная CoreLib/Roslyn dependency closure**.

**2. Что конкретно помешает получить `.hcexe`?**

В application layer — отсутствие guest entry/bootstrap, platform adapters и WAD provisioning.

В compiler qualification — если production profile не знает DIM или один из реально emitted CoreLib helpers.

И отдельно: если toolchain по ошибке использует видимый старый restricted image builder вместо предполагаемого Phase16 implementation, он сам заблокирует image независимо от Doom.

**3. Что помешает boot/title screen?**

После bootstrap главным риском будет неправильный `IDoomClock`: pure wait-driven clock способен зависнуть в polling/wipe path. Нужен monotonically advancing virtual clock.

Console, palette/framebuffer и WAD blob должны быть подключены до `Run`.

При stock WAD я не вижу другого неизбежного source blocker до title screen.

**4. Что помешает загрузить уровень и играть?**

Для stock retail Doom после исправления platform layer я не нашёл доказанного обязательного gameplay blocker.

Но до утверждения correctness следует закрыть:

- composite `GetColumn` source-length semantics;
- fatal `Error/Quit` no-return;
- map/WAD bounds validation;
- `P_LoadThings` `break` для PWAD compatibility.

**5. Что останется только performance-проблемами ISE?**

Главные: `GetColumn` allocation/copy, repeated patch parsing, `Patch.GetColumnByOffset` linear scan, `ActionParams` per thinker/tic, retained raw-lump copies без purge, WAD name linear searches, inner renderer modulo/indexing и framebuffer bulk transfer.

Самая опасная комбинация — **renderer allocations + STW non-generational GC**.

**6. Минимальный упорядоченный список до первого полноценного Doom run**

1. Зафиксировать Phase16 production release manifest/profile и убедиться, что используется не restricted V1 builder.
2. Сделать AOT preflight текущего Core и получить полный emitted CIL/System member closure; закрыть BCL helpers.
3. Закрыть DIM для `IConsole.WriteLine` — runtime support либо минимальное изменение interface ABI.
4. Добавить tiny guest bootstrap + RuntimeKernel adapters + WAD boot blob.
5. Реализовать deterministic virtual clock, synchronous tic input, console/process-exit и framebuffer service.
6. Сделать `DoomGame.Error/Quit` реально no-return.
7. Исправить `GetColumn`: exact column view, **zero allocation/copy on render path**.
8. Запустить title/E1M1 against reference frame/tic hashes.
9. Убрать `ActionParams` per-tic allocations и repeated patch parsing.
10. После первого корректного run профилировать detailed ISE и только затем микрооптимизировать column/span pixel loops.

То есть **реальный минимальный разрыв существенно меньше, чем “портировать .NET под bare metal”**: язык/runtime часть Doom уже соответствует архитектуре RefPlan7. Критический путь сейчас — production image/bootstrap contract, bounded CoreLib closure, DIM, deterministic services и один очень плохой renderer column representation.