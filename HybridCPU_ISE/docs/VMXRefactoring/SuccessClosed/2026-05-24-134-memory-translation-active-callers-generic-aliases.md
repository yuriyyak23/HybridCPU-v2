# Memory translation active callers generic aliases

Дата: 2026-05-24

## Правило / основание

- Memory virtualization authority принадлежит `MemoryDomainDescriptor` / runtime memory substrate, а не NPT/VPID/VMCS vocabulary.
- Compatibility-shaped carrier fields допустимы только как transitional ABI/projection inputs; active logic должна идти через generic aliases.

## Что изменено

- В `MemoryTranslationControl` добавлены generic aliases: `GuestAddressSpaceRoot`, `CompatibilityProjectionIdentity`, `AddressSpaceGeneration`.
- `IsValid()` и `ToAddressSpaceId()` переведены на generic aliases.
- `NestedPageWalker` и `NestedMemoryCompositionService` переведены с активных `NptEnabled`/`NptRoot`/`GuestCr3`/`VmcsEpoch` на `SecondStageTranslationEnabled`, `SecondStageRoot`, `GuestAddressSpaceRoot`, `AddressSpaceGeneration`.
- Сообщения composition path демотированы с NPT-specific wording к second-stage translation wording.

## Как проверено

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
rg -n "control\.NptEnabled|control\.NptRoot|control\.GuestCr3|control\.VpidEnabled|control\.VmcsEpoch|ChildTranslationControl\.Npt|HostTranslationControl\.Npt|ChildTranslationControl\.GuestCr3" Core\VMX\Substrate\Memory\Translation Core\VMX\Substrate\Nested\MemoryComposition
```

## Результат сборки

Build succeeded. 54 Warning(s), 0 Error(s). Static grep returned no active caller matches.
