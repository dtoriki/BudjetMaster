# SoftDeletableEntityBaseImplConfiguration\<TEntity, TId\>

[← FluentConfigurations](./README.md) · [← Библиотека](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Класс](#класс)
- [Методы](#методы)
- [Инварианты и правила](#инварианты-и-правила)
- [Сценарии использования](#сценарии-использования)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Расширяет `SoftDeletableEntityConfiguration<TEntity, TId>` для сущностей, наследующих `SoftDeletableEntityBase<TId>`. Настраивает `PropertyAccessMode.FieldDuringConstruction` для инфраструктурных полей мягкого удаления (`_deletedAtUtc`, `_recoveredAtUtc`, `_isDeletedInfrastructure`), позволяя EF Core материализовать их напрямую в поля.

Исключения класса: `ArgumentNullException`, `InvalidOperationException`.

## Класс

```csharp
public abstract class SoftDeletableEntityBaseImplConfiguration<TEntity, TId>
    : SoftDeletableEntityConfiguration<TEntity, TId>
    where TEntity : SoftDeletableEntityBase<TId>
    where TId : IEquatable<TId>
```

## Методы

#### Configure(EntityTypeBuilder\<TEntity\> builder) — override

Вызывает `base.Configure(builder)`, затем применяет `FieldDuringConstruction` для:
- `DeletedAtUtc` → `_deletedAtUtc`
- `RecoveredAtUtc` → `_recoveredAtUtc`
- `IsDeleted` → `_isDeletedInfrastructure`

**Исключения:**
- `ArgumentNullException` — `builder` равен `null`.
- `InvalidOperationException` — CLR-тип не совпадает с `TEntity`.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| PropertyAccessMode | `FieldDuringConstruction` для всех трёх полей soft-delete | EF пишет в инфраструктурные поля при материализации |
| FlushInfrastructure | `_isDeletedInfrastructure` переносится в `_deletedState` лениво | Гарантируется `SoftDeletableEntityBase<TKey>` |

## Сценарии использования

Конфигурация сущности на базе `SoftDeletableEntityBase`:

```csharp
public sealed class TransactionConfiguration
    : SoftDeletableEntityBaseImplConfiguration<Transaction, long>
{
    protected override void ValidateEntityModel(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.Property(t => t.Amount).HasPrecision(18, 2);
    }
}
```

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| TEntity | Должен наследовать `SoftDeletableEntityBase<TId>` — не просто реализовывать `ISoftDeletableEntity` |
| Инфраструктурные поля | `protected internal` в `SoftDeletableEntityBase<TKey>`; доступны EF Core через рефлексию |
| Использование | Это наиболее конкретный класс конфигурации в иерархии; именно его следует использовать для сущностей на основе `SoftDeletableEntityBase` |
