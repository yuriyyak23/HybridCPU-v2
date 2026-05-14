# ABI, Decode, MicroOp, And Retire Contract

Updated: 2026-05-14.

## Сквозная модель

Рефакторинг ISE закрепляет не отдельные "точки поддержки", а сквозную runtime
цепочку. Инструкция становится executable только если каждый слой либо
материализует корректный runtime fact, либо fail closed до публикации
архитектурного состояния.

Нормальная цепочка:

```text
Inventory
  -> Encoding ABI
  -> Decoder projection
  -> Instruction IR
  -> MicroOp materialization
  -> Execute/capture
  -> Retire apply
  -> Replay/rollback conformance
```

## Encoding ABI

Closed ABI facts:

- scalar W register-register: `INT32`, `Word1=(rd, rs1, rs2)`;
- scalar W immediate: `INT32`, `Word1=(rd, rs1, x0)`, immediate;
- `SEXT.W` / `ZEXT.W`: `INT32`, `Word1=(rd, rs1, x0)`, `Immediate=0`;
- branch/control target: canonical `Immediate`;
- atomic `aq/rl`: transported into runtime IR/internal ordering facts;
- `FENCE` / `FENCE_I`: canonical zero-payload ABI only;
- Lane6 `DmaStreamCompute`: typed descriptor sideband plus lane6 placement;
- Lane7 `ACCEL_SUBMIT`: typed descriptor sideband plus lane7 placement.

ABI facts do not prove execution. Они только задают допустимый transport, после
которого decoder/materializer/runtime должны подтвердить контур.

## Decoder Projection

Decoder projection закрыт для audited scalar, branch/control, load/store,
atomic, vector, Lane6, Lane7 и canonical fence contours.

Ключевые decoder gates:

- non-canonical branch/control target sideband rejected;
- scalar W и word-unary forms project canonical packed registers;
- atomics project `AcquireOrdering` и `ReleaseOrdering`;
- load/store сохраняют typed access size и memory direction;
- unsupported indexed/2D vector forms fail closed before materialization;
- `DmaStreamCompute` требует accepted descriptor sideband и lane6 slot;
- `ACCEL_SUBMIT` требует accepted descriptor sideband и lane7
  `SystemSingleton` slot;
- `FENCE` / `FENCE_I` принимают только canonical zero-payload form;
- matrix optional-disabled rows rejected;
- cache/TLB/coherency reserved rows unaccepted.

Decoder projection остается gate, а не authority. Ошибка в projection должна
останавливать execution path, но успешная projection сама по себе не закрывает
retire semantics.

## Instruction IR And Metadata

IR несет runtime facts, которые нужны materializer и retire path. Metadata и
typed-slot facts остаются evidence/validation only.

Runtime metadata должна описывать уже доказанное состояние:

| Contour | Metadata meaning |
|---|---|
| Scalar through `ZEXT.W` | Executable. |
| Branch/control | Executable published carriers. |
| Load/store | Executable typed memory semantics. |
| LR/SC and AMO W/D | Executable retire semantics. |
| Atomic aq/rl | Executable ordering only through retire proof. |
| `FENCE` / `FENCE_I` | Bounded executable system contour, zero payload only. |
| `VGATHER` / `VSCATTER` | DescriptorOnly. |
| Lane6 production | DescriptorOnly / fail-closed. |
| Lane7 production | DescriptorOnly or CarrierOnly / fail-closed. |
| Matrix | OptionalDisabled. |
| Cache/TLB/coherency | Reserved. |

## MicroOp Materialization

Materialization закрыта для текущих executable contours:

- scalar repair opcodes materialize scalar ALU MicroOps;
- scalar load/store materialize typed memory MicroOps with exact footprints;
- branch/control materialize lane7 carriers from canonical target transport;
- atomics materialize `AtomicMicroOp`, где execute resolves retire intent без
  ранней публикации памяти или регистров;
- canonical `FENCE` materializes system event carrier with `DrainMemory`;
- canonical `FENCE_I` materializes system event carrier with `FlushPipeline`;
- audited vector contours materialize только scoped 1D/runtime-owned forms;
- Lane6/Lane7 descriptor carriers materialize только из accepted descriptor
  sideband и правильного lane placement.

Materializer also has negative responsibility:

- reject unsupported fence payload/masks/flags/sideband;
- not create executable MicroOp for descriptor-only gather/scatter;
- not create executable production MicroOp for Lane6/Lane7 backend paths;
- not materialize matrix/cache/TLB/coherency rows as executable ops.

## Execute And Capture

Execute/capture stage может вычислить intent, dependency, reservation или
retire record. Но для закрытых memory/atomic/fence контуров execute не должен
публиковать architectural truth.

Examples:

- scalar ALU result becomes architectural only through writeback-retire;
- store memory effect commits at writeback-retire;
- atomic RMW effect applies at retire;
- branch PC update publishes through lane7 retire;
- `FENCE` event publishes only at retire;
- `FENCE_I` fetch-state invalidation happens only at retire apply.

Этот принцип делает replay/rollback проверяемыми: captured but cancelled window
не должен оставлять memory/register/fetch truth.

## Retire Apply

Retire является местом публикации:

- register writeback;
- store commit;
- AMO memory update and return value;
- LR/SC reservation effect;
- branch/control PC update;
- system event boundary;
- fetch-state invalidation for `FENCE_I`.

Phase 08 и Phase 10 вместе фиксируют, что order semantics не прячутся в decode
или metadata. Они доказываются по retire-window behavior.

## Rollback And Replay

Rollback/replay closure означает:

- captured effects without retire apply are cancelled;
- ownership state stays scoped to the audited SMT/VT model;
- fence/fetch invalidation не публикуется из отмененного batch;
- descriptor-only and carrier-only paths не получают accidental backend commit;
- no compatibility fallback silently executes unsupported contours.

## Compiler Boundary

Compiler can participate only in live-emitted scoped contours. Текущие
runtime-facing compiler facts:

- scalar tail through `ZEXT.W` is emitted;
- `VSETVLI` scoped helper exists;
- coordinator barrier emits canonical zero-payload `FENCE`;
- control-flow facade wrappers fail closed until relocation-aware lowering;
- atomic/vector/matrix/cache/TLB/coherency production lowering remains closed;
- Lane6/Lane7 compiler paths are descriptor-carrier only.

Compiler emission is a scope signal, not runtime authority.

