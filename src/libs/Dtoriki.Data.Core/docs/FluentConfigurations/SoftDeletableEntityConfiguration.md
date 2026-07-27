# SoftDeletableEntityConfiguration\<TEntity\> / SoftDeletableEntityConfiguration\<TEntity, TId\>

[← FluentConfigurations](./README.md) · [← Библиотека](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Классы](#классы)
- [Методы](#методы)
- [Инварианты и правила](#инварианты-и-правила)
- [Сценарии использования](#сценарии-использования)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Расширяют `EntityBaseConfiguration` для сущностей с мягким удалением. Устанавливают имена колонок `is_deleted`, `deleted_at_utc`, `recovered_at_utc` и применяют глобальный query-фильтр `e => !e.IsDeleted`, исключающий удалённые записи из запросов по умолчанию.

Исключения классов: `ArgumentNullException`, `InvalidOperationException`.

## Классы

```csharp
public abstract class SoftDeletableEntityConfiguration<TEntity> : EntityBaseConfiguration<TEntity>
    where TEntity : class, ISoftDeletableEntity

public abstract class SoftDeletableEntityConfiguration<TEntity, TId> : EntityBaseConfiguration<TEntity, TId>
    where TEntity : class, ISoftDeletableEntity<TId>
    where TId : IEquatable<TId>
```

## Методы

#### Configure(EntityTypeBuilder\<TEntity\> builder) — override

Вызывает `base.Configure(builder)`, затем маппит колонки soft-delete и устанавливает `HasQueryFilter(e => !e.IsDeleted)`.

**Исключения:**
- `ArgumentNullException` — `builder` равен `null` (типизированная версия).
- `InvalidOperationException` — CLR-тип не совпадает с `TEntity`.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| Query-фильтр | `!e.IsDeleted` | Применяется ко всем запросам по умолчанию |
| Колонки | `is_deleted`, `deleted_at_utc`, `recovered_at_utc` | Маппятся всегда |
| IgnoreQueryFilters | Доступно через EF Core API | Для явного получения удалённых записей |

## Сценарии использования

Конфигурация мягко удаляемой сущности с типизированным ключом:

```csharp
public sealed class TransactionConfiguration : SoftDeletableEntityConfiguration<Transaction, long>
{
    protected override void ValidateEntityModel(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
    }
}
```

Получение удалённых записей (обход query-фильтра):

```csharp
Transaction[] deleted = await context.Set<Transaction>()
    .IgnoreQueryFilters()
    .Where(t => t.IsDeleted)
    .ToArrayAsync(ct);
```

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| Query-фильтр | Один фильтр на тип; если нужен дополнительный — используй `HasQueryFilter` с объединённым выражением |
| TEntity | Нетипизированная версия требует только `ISoftDeletableEntity`; типизированная — `ISoftDeletableEntity<TId>` |
