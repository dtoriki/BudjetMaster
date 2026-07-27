# Dtoriki.Data.Core.Entities

**Платформа:** .NET 10

[← Документация библиотеки](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Публичный API](#публичный-api)
- [Иерархия типов](#иерархия-типов)
- [Инварианты и правила](#инварианты-и-правила)

---

## Назначение

Namespace определяет контракты и базовые реализации сущностей базы данных: временные метки жизненного цикла, типизированный идентификатор, поддержку мягкого удаления и признак устаревания.

## Публичный API

Интерфейсы:
- `IEntity` — базовый контракт сущности: временные метки `CreatedAtUtc`, `LastUpdatedAtUtc`, методы `Touch()`, `SetCreated()`. [Полная документация](./IEntity.md)
- `IEntity<TKey>` — расширяет `IEntity`, добавляет типизированный идентификатор `Id`. [Полная документация](./IEntity.md)
- `ISoftDeletableEntity` — контракт мягкого удаления: `IsDeleted`, `DeletedAtUtc`, `RecoveredAtUtc`, `SoftDelete()`, `Recover()`. [Полная документация](./ISoftDeletableEntity.md)
- `ISoftDeletableEntity<TKey>` — объединяет `ISoftDeletableEntity` и `IEntity<TKey>`. [Полная документация](./ISoftDeletableEntity.md)
- `ICanOutdated` — контракт устаревания: флаг `IsOutdated`. [Полная документация](./ICanOutdated.md)

Классы:
- `EntityBase<TKey>` — абстрактная реализация `IEntity<TKey>` с потокобезопасными UTC-метками и ленивым переносом инфраструктурных данных из EF. [Полная документация](./EntityBase.md)
- `EntityBase` — алиас `EntityBase<long>`. [Полная документация](./EntityBase.md)
- `SoftDeletableEntityBase<TKey>` — расширяет `EntityBase<TKey>`, добавляет lock-free мягкое удаление. [Полная документация](./SoftDeletableEntityBase.md)
- `SoftDeletableEntityBase` — алиас `SoftDeletableEntityBase<long>`. [Полная документация](./SoftDeletableEntityBase.md)

## Иерархия типов

```
IEntity
├── IEntity<TKey>
│   └── ISoftDeletableEntity<TKey>
└── ISoftDeletableEntity
    └── ISoftDeletableEntity<TKey>

ICanOutdated  (независимый контракт)

EntityBase<TKey> : IEntity<TKey>
└── EntityBase : EntityBase<long>
    └── SoftDeletableEntityBase<TKey> : EntityBase<TKey>, ISoftDeletableEntity<TKey>
        └── SoftDeletableEntityBase : SoftDeletableEntityBase<long>
```

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| Временные метки | `CreatedAtUtc` ≤ `LastUpdatedAtUtc` | Всегда; нарушение → `ArgumentOutOfRangeException` |
| Временные метки | Оба значения — `DateTimeKind.Utc` | Всегда; нарушение → `ArgumentException` |
| Мягкое удаление | `IsDeleted == true` ↔ `DeletedAtUtc != null` | Инвариант состояния; нарушение → `InvalidOperationException` |
| Мягкое удаление | `RecoveredAtUtc >= DeletedAtUtc` | Если оба установлены |
| Мягкое удаление | `DeletedAtUtc >= CreatedAtUtc` | Всегда |
| Потокобезопасность | `Touch()`, `SoftDelete()`, `Recover()` | Lock-free (CAS / `Interlocked`) |
