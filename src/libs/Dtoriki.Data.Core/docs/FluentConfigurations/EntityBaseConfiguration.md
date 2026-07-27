# EntityBaseConfiguration\<TEntity\> / EntityBaseConfiguration\<TEntity, TId\>

[← FluentConfigurations](./README.md) · [← Библиотека](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Классы](#классы)
- [Методы](#методы)
- [Инварианты и правила](#инварианты-и-правила)
- [Сценарии использования](#сценарии-использования)
- [Обработка ошибок](#обработка-ошибок)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Базовые абстрактные конфигурации EF Core для сущностей, реализующих `IEntity` / `IEntity<TKey>`. Устанавливают имена колонок временных меток, настраивают индексы. Реализуют паттерн Template Method через виртуальные методы `ValidateEntityModel` и `ConfigureIndexes`.

Исключения классов: `ArgumentNullException`, `InvalidOperationException`.

## Классы

```csharp
public abstract class EntityBaseConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : class, IEntity

public abstract class EntityBaseConfiguration<TEntity, TId> : EntityBaseConfiguration<TEntity>
    where TEntity : class, IEntity<TId>
    where TId : IEquatable<TId>
```

## Методы

#### Configure(EntityTypeBuilder\<TEntity\> builder) — virtual

Точка входа конфигурации. Выполняет: проверку CLR-типа, `ValidateEntityModel`, `ConfigureIndexes`, маппинг колонок `created_at_utc` / `last_updated_at_utc`. Типизированная версия дополнительно маппит `id`.

**Исключения:**
- `ArgumentNullException` — `builder` равен `null`.
- `InvalidOperationException` — CLR-тип builder не совпадает с `TEntity`.

#### ValidateEntityModel(EntityTypeBuilder\<TEntity\> builder) — protected virtual

Hook для дополнительной валидации модели. Базовая реализация пустая. Переопределите для доменных проверок.

#### ConfigureIndexes(EntityTypeBuilder\<TEntity\> builder) — protected virtual

Вызывает `SetDefaultIndexes()` / `SetDefaultIndexes<TEntity, TId>()`. Переопределите для кастомных индексов.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| CLR-тип | Совпадает с `TEntity` | Проверяется в `EnsureEntityType`; иначе `InvalidOperationException` |
| Порядок вызовов | `EnsureEntityType` → `ValidateEntityModel` → `ConfigureIndexes` → колонки | Фиксированный |
| Колонки | `created_at_utc`, `last_updated_at_utc`, `id` (типизированная версия) | Маппятся всегда |

## Сценарии использования

Конфигурация без явного ключа:

```csharp
public sealed class TransactionConfiguration : EntityBaseConfiguration<Transaction>
{
    protected override void ValidateEntityModel(EntityTypeBuilder<Transaction> builder)
    {
        builder.Property(t => t.Amount).IsRequired();
    }
}
```

Конфигурация с типизированным ключом:

```csharp
public sealed class CategoryConfiguration : EntityBaseConfiguration<Category, Guid>
{
    protected override void ConfigureIndexes(EntityTypeBuilder<Category> builder)
    {
        builder.SetDefaultIndexes<Category, Guid>();
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
```

## Обработка ошибок

| Ситуация | Метод | Поведение |
|----------|-------|-----------|
| `builder == null` | `Configure` | `ArgumentNullException` |
| CLR-тип не совпадает с TEntity | `Configure` | `InvalidOperationException` |

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| Конструктор | `protected` — класс абстрактный |
| Порядок | `ValidateEntityModel` вызывается до `ConfigureIndexes`; порядок нельзя изменить без переопределения `Configure` |
