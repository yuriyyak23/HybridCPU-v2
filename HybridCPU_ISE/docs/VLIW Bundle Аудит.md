Проверка реальных исходников HybridCPU и актуализация списка рисков
Введение

Исходный анализ выявил критические риски для VLIW‐формата HybridCPU, такие как ошибочная маска в сеттере DataType, отсутствие фиксированного порядка байтов, приведение 64‑битных величин к 32/16 битам без проверки, отсутствие единых проверок легальности флагов и др. После изучения исходников и тестов репозитория yaksysdev/HybridCPU-v2 эти риски были скорректированы и дополнены. Ниже приведены подтверждения из реального кода и документации, а также предложены практические изменения для минимизации рисков.

Что говорит документация и код
Структура VLIW-инструкции и разделение carrier/evidence

Документ «vliw-binary-format» подробно описывает разметку 256‑байтового набора инструкций. word0 содержит битовое поле Reserved [47:40], поле DataType [39:32], флаги [23:16], 16‑битный Immediate и 16‑битный OpCode. Документ подчёркивает, что флаги Acquire и Release лишь перевозятся на носителе и сами по себе не являются доказательством порядка или атомарности; также поясняется, что VirtualThreadId – это только hint, и что scheduling‑метаданные, sideband‐дескрипторы и другие факты хранятся вне raw-бандла. Это подтверждает, что 256‑байтовый bundle – несамостоятельный и требует побочной информации от компилятора и рантайма.

Политика «fail‑closed» и проверка битов

VliwDecoderV4 реализует строгие проверки: запрещает устаревшие или зарезервированные опкоды, проверяет бит «policy gap» word3[50] (при ненулевом значении декодер выдаёт исключение), запрещает некорректные дескрипторы, принудительно отклоняет инструкции для Lane6/Lane7, если отсутствует ожидаемый sideband, и требует нулевой payload для FENCE/NOP. Однако глобальной проверки флагов (Acquire/Release, Is2D, Indexed, Reduction) в зависимости от опкода нет – тесты лишь убеждаются, что флаги не попадают в IR или микрооперации. Например, тест AtomicAcquireReleaseCarrierBits_AreNotProjectedIntoInstructionIrOrAtomicMicroOp устанавливает флаги Acquire и Release для атомарной загрузки LR_W, после чего проверяет, что эти поля отсутствуют в InstructionIR и в AtomicMicroOp, подтверждая, что флаги не дают порядка сами по себе (никакой проверки, что для других опкодов эти биты запрещены).

Ошибка в сеттере DataType

В файле VLIW_Instruction.Layout.cs свойство DataType маскирует 47:40 вместо 39:32, очищая поле Reserved вместо самого DataType. Это явная ошибка.

Отсутствие фиксированного порядка байтов при сериализации

VLIW_Instruction.Serialization.cs использует BitConverter.ToUInt64 и BitConverter.TryWriteBytes, которые зависят от endianness хоста. Таким образом, один и тот же бандл на little-endian и big-endian машинах будет иметь разные двоичные представления.

Неконтролируемое приведение типов

InstructionEncoder принимает параметры ulong streamLength и ulong param2 и записывает их в 32‑битное поле StreamLength через (uint)streamLength – без проверки на переполнение. Аналогично, длина ряда для 2D‑адресации (rowLength) приводится к ushort без проверки. Если передана длина, превышающая допустимое, значения тихо обрезаются.

Canonical NOP и пустые слоты

Документация требует, чтобы OpCode==0 использовался для пустых слотов/NOP, а все другие поля (в том числе word1–word3, flags, Immediate и т. д.) были нулевыми. Однако декодер считает слот пустым только по опкоду, не проверяя остальные 192 бита. Тесты есть для FENCE (нулевой payload), но для полного NOP – нет.

Проверка зарезервированных битов

Биты Reserved[47:40] помечены «на будущее» и не должны использоваться. Текущий код не запрещает компилятору выставлять эти биты. В ValidateWord3ForProductionIngress проверяется только word3[50] (policy gap), а word0[47:40] никак не валидируется. Это создаёт риск потайного канала.

Неоднозначный API: TryReadBytes vs Throw

Комментарии в Serialization.cs обещают, что «No exceptions will be thrown» при чтении/записи, но ValidateWord3ForProductionIngress может выбросить исключение, так что API вводит в заблуждение: метод с префиксом Try не должен бросать исключения, либо его стоит переименовать.

Актуализированный список рисков и рекомендации
Нарушение authority boundary: raw bundle или наличие опкода не должны считаться доказательством исполнимости. Риски возникают, когда tooling или тесты интерпретируют carrier как полноценный бинарник. Рекомендуется формировать конформанс‑артефакты в виде: bundle.bin + sideband.json + typed_slot_facts + hash и т. п., а в документации явно говорить, что carrier – только переносчик.
No‑emission erosion: наличие хелпера/enum не означает разрешение на генерацию. Нужно расширить тесты no‑emission: проверить, что опкоды/дескрипторы, не готовые к выпуску, вызывают ошибки на этапе компиляции; в противном случае компилятор случайно откроет forbidden surface.
Скрытый канал через Reserved[47:40]: производственный декодер должен отклонять инструкции с ненулевым Reserved. Это устранит риск, что компилятор прячет метаданные в этом поле.
Нет универсальной проверки флагов: флаги Acquire, Release, Reduction, Is2D, Indexed, TailAgnostic, MaskAgnostic, Saturating могут появиться на несовместимых опкодах. Следует ввести глобальный метод ValidateOpcodeFlagLegality, который отвергнет флаги на неподдерживаемых опкодах (например, Acquire/Release только для атомарных нагрузок/хранилищ, Is2D – только для 2D‑адресации). Это предотвратит перенос неиспользуемых флагов в IR и микрокод.
Исправить сеттер DataType: заменить маску 0xFFFF00FFFFFFFFFFUL на 0xFFFFFF00FFFFFFFFUL и тем самым чистить биты [39:32].
Фиксировать little‑endian: заменить BitConverter.ToUInt64 и BitConverter.TryWriteBytes на BinaryPrimitives.ReadUInt64LittleEndian/WriteUInt64LittleEndian. Необходимо добавить тест, подтверждающий, что byte‑dump одного бандла идентичен на всех платформах.

Безопасные преобразования типов: в InstructionEncoder перед приводлением к uint и ushort проверять диапазон. Например:

if (streamLength > uint.MaxValue)
    throw new ArgumentOutOfRangeException(nameof(streamLength));
inst.StreamLength = (uint)streamLength;
Canonical NOP: внедрить проверку, что слот с OpCode==0 имеет все слова нулевыми. Декодер должен отклонять инструкции, где NOP сопровождается ненулевым payload или sideband. Дополнительно добавить тест, проверяющий отказ при OpCode==0 и ненулевых полях.
Переименовать API: если TryReadBytes потенциально бросает исключения (например, из-за ValidateWord3ForProductionIngress), переименовать метод в ReadBytesOrThrow или изменить логику так, чтобы он возвращал bool/Result и не генерировал исключений.
Новые тесты:
Проверка Reserved==0 на производственном входе.
Проверка правильности маски DataType на запись/чтение.
Проверка little‑endian сериализации.
Проверка переполнений при cast.
Проверка, что NOP/ FENCE имеют нулевой payload.
Проверка законности каждого флага по опкоду.
Проверка no‑emission для неготовых опкодов/хелперов.

Статус закрытия 2026-05-24

[closed] DataType setter mask: VLIW_Instruction.Layout.cs теперь очищает word0[39:32], а не Reserved[47:40]. Проверено тестом VliwBundleAuditClosureTests.DataTypeSetter_UpdatesOnlyDataTypeByte.

[closed] Canonical little-endian VLIW carrier: VLIW_Instruction.Serialization.cs и fetched-bundle staging path используют BinaryPrimitives.ReadUInt64LittleEndian/WriteUInt64LittleEndian. Проверено тестом VliwBundleAuditClosureTests.Serialization_UsesCanonicalLittleEndianWords.

[closed] Reserved[47:40] production ingress: добавлена ValidateWord0ForProductionIngress и fail-closed проверка в TryReadBytes, SetInstruction и VliwDecoderV4. Проверено тестами VliwBundleAuditClosureTests.TryReadBytes_RejectsReservedWord0ProductionIngress и VliwBundleAuditClosureTests.VliwDecoderV4_RejectsReservedWord0OnOccupiedSlot.

[closed] Silent truncation in InstructionEncoder: streamLength/param2 и rowLength теперь проходят диапазонную проверку перед записью в 32/16-битные поля. Проверено тестом VliwBundleAuditClosureTests.InstructionEncoder_RejectsSilentTruncation.

[closed] Canonical empty slot/NOP carrier: DecodeInstructionBundle отклоняет OpCode==0 с ненулевым payload. Descriptor sideband on empty slot уже был закрыт существующим L7/Lane6 контрактом. Проверено тестом VliwBundleAuditClosureTests.DecodeInstructionBundle_RejectsNonCanonicalEmptySlotPayload.

[closed] Opcode flag legality baseline: VliwDecoderV4 теперь отклоняет Acquire/Release вне atomic, Saturating вне scoped VADD contour, Reduction вне reduction contour, Indexed/Is2D/TailAgnostic/MaskAgnostic вне vector-payload opcode. Векторные non-representable Indexed/2D contours намеренно остаются допустимыми на decode boundary и продолжают уходить в replay/trap boundary, а не в host evidence или guest architectural state. Проверено тестами VliwBundleAuditClosureTests.VliwDecoderV4_RejectsAcquireReleaseOnNonAtomicOpcode, VliwBundleAuditClosureTests.VliwDecoderV4_PreservesAcquireReleaseOnAtomicOpcode, VliwBundleAuditClosureTests.VliwDecoderV4_RejectsAddressingAndReductionFlagsOnUnsupportedOpcodes и Phase09VectorNonRepresentableAddressingClosureTests.

[closed] TryReadBytes contract wording: комментарий Serialization.cs больше не обещает отсутствие исключений для production-ingress legality faults; false остаётся только для короткого буфера, InvalidOpcodeException является fail-closed поведением.

[closed] No-emission erosion baseline: наличие строки в InstructionSupportStatusCatalog, числового opcode metadata, parser/descriptor/carrier статуса или helper-фасада не считается правом на compiler emission. Закрытые статусы OptionalDisabled/Reserved/LegacyRetained/ParserOnly/DescriptorOnly/Prohibited/CarrierOnly проверяются как no-executable-claim и no-execution-semantics. Проверено тестом CompilerNoEmissionBoundaryTests.SupportCatalogClosedRows_DoNotBecomeCompilerEmissionAuthority.

[closed] Compiler/compat ingress streamLength truncation: HybridCpuThreadCompilerContext.CompileInstruction, InsertInstruction и legacy ProcessorCompilerBridge.Add_VLIW_Instruction теперь проверяют streamLength перед записью в 32-битное поле VLIW carrier и отказывают без публикации слота. Проверено тестами CompilerNoEmissionBoundaryTests.ThreadCompilerIngress_RejectsStreamLengthOverflowBeforeCarrierEmission и CompilerNoEmissionBoundaryTests.LegacyCompilerBridge_RejectsStreamLengthOverflowBeforeCarrierEmission.

Проверка закрытия:
dotnet build HybridCPU_ISE\HybridCPU_ISE.csproj --no-restore
dotnet test HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter "FullyQualifiedName~VliwBundleAuditClosureTests|FullyQualifiedName~Phase09VectorNonRepresentableAddressingClosureTests" --no-restore
dotnet test HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter "FullyQualifiedName~Phase10FenceOrderingAndVisibilityTests|FullyQualifiedName~Phase08AtomicOrderingPropagationTests|FullyQualifiedName~L7SdcNativeCarrierValidationTests" --no-restore
dotnet test HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter "FullyQualifiedName~Phase09PolicyGapBitContractTests|FullyQualifiedName~Phase12LiveCompatIngressBoundaryTests" --no-restore
dotnet test HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter "FullyQualifiedName~CompilerNoEmissionBoundaryTests|FullyQualifiedName~VliwBundleAuditClosureTests" --no-restore

Открыто не в рамках быстрого закрытия: полный conformance artifact bundle.bin + sideband.json + typed_slot_facts + hash требует отдельного conformance трека; этот пункт не помечен closed.
Предлагаемый план рефакторинга (исторический)

Ниже приведён исходный краткий набор изменений для минимизации рисков. Пункты, помеченные выше как [closed], уже реализованы и проверены регрессионными тестами; оставшийся conformance artifact пункт требует отдельного трека.

Правка сеттера DataType (файл VLIW_Instruction.Layout.cs):

// было:
word0 = (word0 & 0xFFFF00FFFFFFFFFFUL) | ((ulong)value << 32);
// стало:
word0 = (word0 & 0xFFFFFF00FFFFFFFFUL) | ((ulong)value << 32);

Фиксированный порядок байтов (файл VLIW_Instruction.Serialization.cs):

// чтение
Word0 = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(offset + 0));
Word1 = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(offset + 8));
Word2 = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(offset + 16));
Word3 = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(offset + 24));
// запись
BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(offset + 0), Word0);
BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(offset + 8), Word1);
BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(offset + 16), Word2);
BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(offset + 24), Word3);
Проверка резерва: добавить в ValidateWord3ForProductionIngress (или аналог) проверку word0[47:40] == 0. Любое ненулевое значение должно вызывать исключение с указанием, что поле зарезервировано.

Глобальный валидатор флагов: в декодере добавить функцию

private static void ValidateOpcodeFlagLegality(InstructionsEnum opcode, VLIW_Instruction inst)
{
    if (inst.Acquire || inst.Release)
    {
        if (!IsAtomicOpcode(opcode)) throw new InvalidOperationException("Acquire/Release only valid for atomic opcodes");
    }
    if (inst.Reduction && !IsReductionOpcode(opcode))
        throw new InvalidOperationException("Reduction flag requires a reduction opcode");
    if (inst.Is2D && !Supports2D(opcode))
        throw new InvalidOperationException("Is2D flag on non‑2D opcode");
    if (inst.Indexed && !SupportsIndexed(opcode))
        throw new InvalidOperationException("Indexed flag on non‑indexed opcode");
    // аналогично для TailAgnostic, MaskAgnostic, Saturating …
}

и вызывать её в начале декодирования, до создания InstructionIR.

Проверка переполнений в InstructionEncoder: добавить проверки диапазонов перед cast и бросать ArgumentOutOfRangeException, если параметр превышает допустимый диапазон.

Проверка canonical NOP: в декодере добавить:

if (opcode == InstructionsEnum.NOP)
{
    if (inst.Word0 != 0 || inst.Word1 != 0 || inst.Word2 != 0 || inst.Word3 != 0)
        throw new InvalidOperationException("NOP must have all fields zero");
    // сброс sideband здесь тоже должен быть проверен
}
API TryReadBytes: либо изменить сигнатуру, возвращая bool succeeded и VLIW_Instruction?, либо переименовать в ReadBytesOrThrow. При этом исключения из ValidateWord3ForProductionIngress будут ожидаемым поведением и не будут нарушать контракт.
Тесты и конформанс: добавить модульные тесты на каждое из предложенных правил. Для истоков желательно расширить Phase00InstructionInventoryTests и создать отдельные файлы no‑emission/regression для резервных битов, NOP и переполнений.
Заключение

Исходники и тесты гибридного процессора демонстрируют высокую зрелость fail‑closed‑декодирования и строгого разделения carrier/evidence. Тем не менее, обнаружены конкретные баги и «дырки», которые могут привести к тихой порче данных (ошибочная маска DataType, необъявленный endianness, silent truncation), к возможному злоупотреблению резервным полем, и к неопределённости вокруг легальности флагов. Предлагаемый план рефакторинга даёт практический список изменений для устранения этих рисков. Его реализация повысит надёжность формата VLIW и сохранит философию HybridCPU: разделение носителя и доказательств, строгое соответствие прав применения и no‑emission, отказ от скрытых каналов и недоказанных предположений.
