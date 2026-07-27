# Dtoriki.Data.Core.Context

**Платформа:** .NET 10

[← Документация библиотеки](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Публичный API](#публичный-api)
- [Инварианты и правила](#инварианты-и-правила)

---

## Назначение

Namespace определяет контракт и базовую реализацию контекста доступа к данным на основе EF Core. `EfContextBase` автоматически управляет временными метками сущностей и перехватывает мягкое удаление при `SaveChanges`.

## Публичный API

Интерфейсы:
- `IEfContext` — контракт контекста: управление транзакциями и фиксация изменений (`SaveChanges`, `BeginTransaction`). [Полная документация](./IEfContext.md)
- `IEfContext<TEntity, TKey>` — расширяет `IEfContext`, добавляет типизированный `DbSet<TEntity> Set()`. [Полная документация](./IEfContext.md)

Классы:
- `EfContextBase` — абстрактный базовый класс EF-контекста с автоматической обработкой временных меток и soft-delete. [Полная документация](./EfContextBase.md)

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| SaveChanges | Новые сущности получают `CreatedAtUtc` и `LastUpdatedAtUtc` | Автоматически, если значения равны `default` |
| SaveChanges | Изменённые сущности получают обновлённый `LastUpdatedAtUtc` | Всегда при `EntityState.Modified` |
| Soft delete | `EntityState.Deleted` для `SoftDeletableEntityBase` → `Modified` + `SoftDelete()` | Перехватывается в `SaveChangesInternal` |
| Dispose | Повторный `Dispose` / `DisposeAsync` | Идемпотентен, повторный вызов — no-op |
