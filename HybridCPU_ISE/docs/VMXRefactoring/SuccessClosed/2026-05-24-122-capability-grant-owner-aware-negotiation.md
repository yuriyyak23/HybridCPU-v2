# Capability grant owner-aware negotiation

Дата: 2026-05-24

## Правило / основание

- `Оценка рефакторинга VMX security-centric.md`: capability authority должен выражаться typed grants с owner/scope/policy metadata, а VMX/VmxCaps остаются projection/frozen compatibility surface.
- `deep-research-report (6).md`: capability grants должны принадлежать runtime/domain substrate, а не CSR/VMX-owned состоянию.

## Что изменено

- В `CapabilityDescriptorSet` добавлен overload `CreateGrant(...)` с `ownerDomainId` и typed policy metadata.
- В `CapabilityNegotiationService` добавлены overloads `Negotiate(...)` и `NegotiateGrant(...)`, выпускающие domain-owned typed grants.
- Старые overloads сохранены и продолжают работать без изменения call sites.
- Успешный negotiated grant теперь получает typed defaults: non-delegable, runtime-revocable, domain-local, host-only evidence, compatibility projection only if compatible.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена: `54 Warning(s)`, `0 Error(s)`.
