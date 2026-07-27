# Dtoriki.Data.Core.Extensions

**Платформа:** .NET 10

[← Документация библиотеки](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Публичный API](#публичный-api)

---

## Назначение

Namespace содержит методы расширения для трёх областей: регистрация EF-контекстов в DI, операции над сущностями (soft-delete, restore, hard-delete, touch), конфигурирование индексов через EF Fluent API.

## Публичный API

Классы:
- `ConfigureEfContextExtensions` — расширения `IServiceCollection` для регистрации EF-контекстов (обычная и keyed-регистрация, маппинг абстракций). [Полная документация](./ConfigureEfContextExtensions.md)
- `EntityExtensions` — расширения сущностей: `SoftDelete`, `Recover`, `Touch`, `FlushInfrastructureTimestamps`, `HardRemoveAsync`, `HardRemoveRangeAsync`. [Полная документация](./EntityExtensions.md)
- `EntityTypeBuilderExtensions` — расширения `EntityTypeBuilder<T>` и `IndexBuilder<T>`: `SetDefaultIndexes`, `HasFilterWithNotDeleted`, `HasFilterWithNotDeletedAndNotOutdated`, `HasFilterWithMaxLengthAndNotDeletedAndNotOutdated`. [Полная документация](./EntityTypeBuilderExtensions.md)
