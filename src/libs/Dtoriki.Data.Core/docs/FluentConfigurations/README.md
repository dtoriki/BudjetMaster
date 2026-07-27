# Dtoriki.Data.Core.FluentConfigurations

**Платформа:** .NET 10

[← Документация библиотеки](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Публичный API](#публичный-api)
- [Иерархия конфигураций](#иерархия-конфигураций)

---

## Назначение

Namespace содержит базовые классы EF Core Fluent-конфигураций для сущностей библиотеки. Реализуют паттерн Template Method: базовые классы настраивают общие аспекты (колонки, индексы, query-фильтры), производные переопределяют или дополняют конкретные части.

## Публичный API

Классы:
- `EntityBaseConfiguration<TEntity>` — базовая конфигурация для `IEntity`: колонки `created_at_utc`, `last_updated_at_utc`, индексы через `SetDefaultIndexes`. [Полная документация](./EntityBaseConfiguration.md)
- `EntityBaseConfiguration<TEntity, TId>` — расширяет предыдущую, добавляет колонку `id` и типизированные индексы. [Полная документация](./EntityBaseConfiguration.md)
- `EntityBaseImplConfiguration<TEntity, TId>` — расширяет `EntityBaseConfiguration<TEntity, TId>`, настраивает `PropertyAccessMode.FieldDuringConstruction` для инфраструктурных полей. [Полная документация](./EntityBaseImplConfiguration.md)
- `SoftDeletableEntityConfiguration<TEntity>` — расширяет `EntityBaseConfiguration<TEntity>`: колонки soft-delete и `HasQueryFilter(e => !e.IsDeleted)`. [Полная документация](./SoftDeletableEntityConfiguration.md)
- `SoftDeletableEntityConfiguration<TEntity, TId>` — то же, но с типизированным ключом. [Полная документация](./SoftDeletableEntityConfiguration.md)
- `SoftDeletableEntityBaseImplConfiguration<TEntity, TId>` — расширяет `SoftDeletableEntityConfiguration<TEntity, TId>`, добавляет `FieldDuringConstruction` для инфраструктурных полей soft-delete. [Полная документация](./SoftDeletableEntityBaseImplConfiguration.md)

## Иерархия конфигураций

```
EntityBaseConfiguration<TEntity>
└── EntityBaseConfiguration<TEntity, TId>
    ├── EntityBaseImplConfiguration<TEntity, TId>          ← для EntityBase<TKey>
    └── SoftDeletableEntityConfiguration<TEntity, TId>
        └── SoftDeletableEntityBaseImplConfiguration<TEntity, TId>  ← для SoftDeletableEntityBase<TKey>

EntityBaseConfiguration<TEntity>
└── SoftDeletableEntityConfiguration<TEntity>              ← без явного TId
```
