# 100. VmxExecutionUnit VmxCaps projection source

Дата: 2026-05-24

## Правило / основание

- `VmxCaps` должен быть compatibility alias/projection поверх `CapabilityDescriptorSet`, а не источником истины.
- VMX frontend не должен напрямую читать capability CSR как active authority.
- Capability publication должна проходить через `CapabilityPublicationPolicy` / `VmxCapsProjection`.

## Что изменено

- Актуализирован `Core/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- Добавлен `Core/VMX/Compatibility/Generated/CsrProjection/VmxCapabilityDescriptorSource.cs`.
- `VmxExecutionUnit` теперь:
  - хранит `VmxCapsProjection`;
  - получает capability authority через `IVmxCapabilityDescriptorSource`;
  - проверяет VMX-v2 capability через `VmxCapsProjection.CanPublishCapability(...)`;
  - возвращает VMFUNC `CapabilityQuery` через `VmxCapsProjection.Read(...)`;
  - больше не содержит прямых reads `CsrAddresses.VmxCaps`.
- Legacy CSR-backed source сохранен как transitional compatibility fallback и изолирован вне handler-а.

## Как проверено

Команды:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
rg -n "CsrAddresses\.VmxCaps|DirectRead\(CsrAddresses\.VmxCaps|Read\(CsrAddresses\.VmxCaps" "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\VMX\Compatibility\Frontend\Handlers\VmxExecutionUnit.cs"
```

## Результат сборки

Сборка успешно завершена: 0 errors, 54 warnings.

Статическая проверка не нашла direct `VmxCaps` reads в `VmxExecutionUnit.cs`.
