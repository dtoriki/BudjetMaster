# EntityBaseImplConfiguration\<TEntity, TId\>

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

Расширяет `EntityBaseConfiguration<TEntity, TId>` для сущностей, наследующих `EntityBase<TKey>`. Дополнительно настраивает `PropertyAccessMode.FieldDuringConstruction` для инфраструктурных полей временных меток (`_createdAtUtcInfrastructure`, `_lastUpdatedAtInfrastructure`), что позволяет EF Core материализовать значения напрямую в поля, минуя публичные сеттеры.

Исключения класса: `ArgumentNullException`, `InvalidOperationException`.

## Класс

```csharp
public abstract class EntityBaseImplConfiguration<TEntity, TId> : EntityBaseConfiguration<TEntity, TId>
    where TEntity : EntityBase<TId>
    where TId : IEquatable<TId>
```

## Методы

#### Configure(EntityTypeBuilder\<TEntity\> builder) — override

Вызывает `base.Configure(builder)`, затем применяет:
- `CreatedAtUtc` → поле `_createdAtUtcInfrastructure`, `FieldDuringConstruction`.
- `LastUpdatedAtUtc` → поле `_lastUpdatedAtInfrastructure`, `FieldDuringConstruction`.

**Исключения:**
- `ArgumentNullException` — `builder` равен `null`.
- `InvalidOperationException` — CLR-тип не совпадает с `TEntity`.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| PropertyAccessMode | `FieldDuringConstruction` | EF пишет в инфраструктурное поле при материализации |
| FlushInfrastructure | Значения переносятся лениво при первом обращении | Гарантируется `EntityBase<TKey>` |

## Сценарии использования

Конфигурация конкретной сущности на базе `EntityBase`:

```csharp
public sealed class TransactionConfiguration : EntityBaseImplConfiguration<Transaction, long>
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
| TEntity | Должен наследовать `EntityBase<TId>` (не просто реализовывать `IEntity`) |
| Инфраструктурные поля | `_createdAtUtcInfrastructure` и `_lastUpdatedAtInfrastructure` — `protected internal` в `EntityBase<TKey>`; доступны EF Core через рефлексию |
