# Техническое задание: DoomSharp.HybridCpu.Windows — отображение Doom VGA frame

## Причина работы

В проекте уже существует контракт HybridCpuFramebufferServiceContractV1 с
операциями Initialize, Present и UpdatePalette, но DoomGuestExecutionService
подключает только HybridCpuIseConsoleProviderV1. Graphics host provider
отсутствует, а WPF viewport остаётся резервом. Поэтому loader-backed Doom
execution не может передать настоящий VGA frame в GUI.

Цель этого рефакторинга — добавить host-owned путь от guest framebuffer до
WPF-окна с корректным отображением indexed Doom pixels и palette. Работа не
меняет ISA, compiler semantics, guest image format или GUI admission policy.

## Границы

- Использовать существующие HybridCpuFramebufferServiceContractV1 операции и
  IHybridCpuHostServiceProviderV1; не вводить новый ISA или альтернативный
  graphics ABI.
- Подключить graphics provider в composition root DoomGuestExecutionService.ExecuteCore
  рядом с console provider.
- Provider принимает только валидированный read-only guest buffer на Present.
  Нельзя читать произвольную память процесса, использовать global Processor
  objects или подставлять synthetic/нулевые pixels.
- Проверять exact width*height, диапазоны guest buffer, palette размер 256*3,
  инициализацию framebuffer и context identity.
- Копировать payload в immutable host-owned frame до передачи UI. Guest CPU,
  provider и WPF UI не должны разделять изменяемый массив.
- UI обновляется через Dispatcher; graphics callback не должен напрямую
  изменять WPF controls или блокировать CPU на rendering.
- Run/qualification gates остаются прежними: наличие frame не доказывает
  completion, process exit, GC evidence или PublishQualified.

## Требуемая архитектура

1. Добавить узкий DoomFramebufferProvider с состоянием per execution context:
   dimensions, current palette, frame sequence и последний успешно принятый
   frame. Состояние очищается при завершении execution.
2. На Initialize создать bounded surface только после успешной проверки dimensions.
3. На UpdatePalette принять ровно 768 RGB bytes и сохранить immutable palette.
4. На Present прочитать ровно width*height bytes через разрешённый guest memory
   callback, проверить palette и создать immutable frame envelope: context id,
   sequence, dimensions, palette identity, pixel bytes, timestamp.
5. Передавать envelope в WPF через bounded Channel/dispatcher queue с DropNewest
   и счётчиком потерь. Медленный UI не останавливает CPU.
6. В MainWindow создать WriteableBitmap или эквивалентный WPF surface,
   преобразовать indexed pixels + RGB palette в поддерживаемый pixel format и
   отобразить frame с сохранением aspect ratio. Не интерполировать пиксели
   при диагностическом режиме.
7. Добавить явное состояние UI: NoFrame, FrameReady, FrameDropped,
   GraphicsProviderFault, ExecutionEnded. Старый успешный frame не выдавать
   после provider fault или нового execution context.
8. Сохранять по запросу diagnostic frame и palette под TempEnv в BMP/PNG с
   manifest: image SHA, run/segment identity, frame sequence, dimensions и
   palette/frame SHA-256. Экспорт не должен быть qualification evidence сам
   по себе.

## Fail-closed требования

- Не принимать Present до успешного Initialize.
- Отвергать неверный размер, переполнение width*height, отсутствующий palette,
  неизвестный context или устаревший execution generation.
- При ошибке чтения guest buffer возвращать provider failure без публикации
  частичного frame.
- При переполнении UI queue учитывать loss count и сохранять sequence gap.
- При завершении или fault execution закрывать queue и запрещать новые frames.
- Frame observation не должен менять guest terminal outcome: обработка ошибки
  отображения отключает graphics diagnostics, но не превращает guest failure в
  success и не останавливает CPU автоматически.

## Узкие проверки

- Contract tests: dimensions, exact payload sizes, palette validation,
  initialization order, context/generation mismatch и provider fault.
- Provider tests: две последовательные palette/frame публикации, immutable
  copy, sequence numbers, DropNewest и bounded memory.
- WPF test: indexed frame 320x200 с известной palette корректно отображается
  без обращения к CoreCLR guest и без fake pixels.
- Integration test: synthetic loader-backed run с graphics provider сохраняет
  одинаковый execution result при graphics diagnostics enabled/disabled.
- Отдельный loader-backed test host должен принять настоящий Present payload и
  сохранить BMP под новым TempEnv evidence path.
- Для Doom run проверить console -> framebuffer initialization -> palette ->
  first frame и отсутствие UI deadlock. Snapshot/connect/disconnect не считать
  completion или qualification.

## Acceptance

Работа считается выполненной, когда:

1. DoomGuestExecutionService использует production runtime kernel с graphics
   provider без второго CPU loop и без изменения ISA.
2. Реальный guest Present создаёт immutable frame envelope с exact dimensions,
   pixel/palette SHA и monotonic sequence.
3. WPF GUI отображает этот frame в viewport через Dispatcher, а disconnect,
   provider fault и frame loss видны явно.
4. Отдельный test host и Doom bounded run проходят Release; все логи, BMP/PNG,
   manifest, hashes и reports находятся под TempEnv.
5. При отключённой graphics diagnostics terminal execution result совпадает с
   результатом enabled режима.
6. Отчёт явно разделяет frame delivery, guest completion, GC/ECALL evidence и
   qualification. Наличие BMP не является доказательством PublishQualified.

## Ограничения текущего состояния

CPU47 и его активный run не менять и не перезапускать для этой задачи. Сначала
реализовать и проверить provider на отдельном test host, затем планировать новый
контролируемый Doom launch с opt-in frame capture.
