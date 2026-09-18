# Address space generation alias

Дата: 2026-05-24

## Правило / основание

- `AddressSpaceId` должен быть runtime-owned evidence, not guest architectural state.
- VMCS-derived epochs must be projection-only/recomputable and should not be referenced as canonical memory-domain identity vocabulary.

## Что изменено

- `MemoryTranslationControl.ToAddressSpaceId()` теперь передаёт `AddressSpaceGeneration`, а не прямой `VmcsEpoch`.
- Nested composition helper `VpidEpochOrZero()` теперь использует `AddressSpaceTaggingEnabled` и `AddressSpaceGeneration`.
- Старый positional field оставлен как compatibility-shaped carrier для existing constructors.

## Как проверено

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded. 54 Warning(s), 0 Error(s).
