# VmxCaps projection CSR address demotion

Дата: 2026-05-24

## Правило / основание

- VMX CSR является frozen ABI alias, а не authority source.
- Generated projection logic не должна хранить CSR address рядом с capability projection behavior.

## Что изменено

- Из `VmxCapsProjection` удалена прямая зависимость от `CsrAddresses.VmxCaps`.
- Frozen CSR address остаётся в `VmxCsrAliasSet`, то есть в ABI alias layer.
- `VmxCapsProjection` теперь содержит только publication/read/write projection logic поверх `CapabilityDescriptorSet`.

## Как проверено

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
rg -n "CsrAddress|CsrAddresses\.VmxCaps" "...VmxCapsProjection.cs" "...VmxCsrAliasSet.cs"
```

## Результат сборки

Build succeeded. 54 Warning(s), 0 Error(s).
