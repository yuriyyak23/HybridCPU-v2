# Generated projection schema artifacts

Дата: 2026-05-24

Статус: closed

## Основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: generated/read-only projection artifacts must be derived from explicit compatibility schemas, not VMX-owned runtime authority.
- `audit.md`: пункты 20, 21, 22, 23 указывают на риск generated-by-naming и отсутствие внешнего schema source для VMCS/CSR/capability projection.
- `2026-05-24-vmx-current-model-completion-audit.md`: ближайшая задача по добавлению generated schema artifacts for VMCS field projection and VmxCaps capability bits.

## Что изменено

- Добавлен каталог canonical schema artifacts:
  - `docs/VMXRefactoring/schemas/compat-alias-schema.v1.json`
  - `docs/VMXRefactoring/schemas/vmcs-field-projection-schema.v1.json`
  - `docs/VMXRefactoring/schemas/vmxcaps-capability-bit-schema.v1.json`
- Schema artifacts фиксируют ABI freeze, projection-only model, fail-closed unsupported/unmapped state, typed grant source, evidence visibility, access policy and migration policy requirements.

## Проверка

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`

## Результат

- Основной проект собирается: succeeded, 0 errors.
- Тестовый проект собирается: succeeded, 0 errors.
- Таргетный conformance набор: Passed 5/5.

## Остаточный риск

Эта задача закрывает наличие canonical schema artifacts. Полноценный executable/build-time generator остается отдельной незакрытой задачей и не объявляется закрытым этим документом.
