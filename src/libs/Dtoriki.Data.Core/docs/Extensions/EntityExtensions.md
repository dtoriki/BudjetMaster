# EntityExtensions

[← Extensions](./README.md) · [← Библиотека](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Методы](#методы)
- [Инварианты и правила](#инварианты-и-правила)
- [Сценарии использования](#сценарии-использования)
- [Обработка ошибок](#обработка-ошибок)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Содержит публичные методы расширения для операций над сущностями: инициализация инфраструктурных меток, мягкое удаление, восстановление, обновление метки, физическое удаление. Также содержит `internal`-методы, используемые `EntityBase` и `SoftDeletableEntityBase` для lock-free операций.

Исключения класса: `ArgumentNullException`, `InvalidOperationException`.

## Методы

#### FlushInfrastructureTimestamps\<TEntity, TKey\>(this TEntity entity)

Потокобезопасно переносит инфраструктурные метки EF (`_createdAtUtcInfrastructure`, `_lastUpdatedAtInfrastructure`) в рабочие поля. Идемпотентен. Возвращает ту же сущность для fluent-цепочек.

**Исключения:**
- `ArgumentNullException` — `entity` равен `null`.
- `InvalidOperationException` — превышен лимит спин-ожидания конкурентной инициализации.

#### SoftDelete\<TEntity, TKey\>(this TEntity entity)

Выполняет мягкое удаление: `IsDeleted = true`, `DeletedAtUtc = UtcNow`, `RecoveredAtUtc = null`. Идемпотентен. Возвращает сущность.

**Исключения:**
- `ArgumentNullException` — `entity` равен `null`.
- `InvalidOperationException` — нарушены пост-инварианты.

#### SoftDelete\<TEntity, TKey\>(this IEnumerable\<TEntity\> entities)

Выполняет мягкое удаление для каждого элемента коллекции.

**Исключения:**
- `ArgumentNullException` — `entities` равна `null`.
- `InvalidOperationException` — `null`-элемент в коллекции или нарушение инвариантов.

#### Recover\<TEntity, TKey\>(this TEntity entity)

Восстанавливает мягко удалённую сущность: `IsDeleted = false`, `RecoveredAtUtc = UtcNow`, `DeletedAtUtc = null`. Идемпотентен. Возвращает сущность.

**Исключения:**
- `ArgumentNullException` — `entity` равен `null`.
- `InvalidOperationException` — нарушены пост-инварианты.

#### Touch\<TEntity, TKey\>(this TEntity entity)

Монотонно обновляет `LastUpdatedAtUtc` до `DateTime.UtcNow`. Возвращает сущность.

**Исключения:**
- `ArgumentNullException` — `entity` равен `null`.

#### HardRemoveRangeAsync\<TEntity, TKey\>(this DbSet\<TEntity\>, IEnumerable\<TEntity\>, CancellationToken)

Физически удаляет записи по экземплярам сущностей. Извлекает идентификаторы, выполняет `ExecuteDeleteAsync`, удаляет из локального трекинга.

**Исключения:**
- `ArgumentNullException` — `set` или `entities` равны `null`.
- `InvalidOperationException` — `null`-элемент или сущность с `Id == default`.

#### HardRemoveRangeAsync\<TEntity, TKey\>(this DbSet\<TEntity\>, IEnumerable\<TKey\>, CancellationToken)

Физически удаляет по коллекции идентификаторов.

**Исключения:**
- `ArgumentNullException` — `set` или `ids` равны `null`.
- `InvalidOperationException` — идентификатор равен `default`.

#### HardRemoveAsync\<TEntity, TKey\>(this DbSet\<TEntity\>, TEntity, CancellationToken)

Физически удаляет одну сущность по экземпляру.

**Исключения:**
- `ArgumentNullException` — `set` или `entity` равны `null`.
- `InvalidOperationException` — `entity.Id == default`.

#### HardRemoveAsync\<TEntity, TKey\>(this DbSet\<TEntity\>, TKey, CancellationToken)

Физически удаляет одну сущность по идентификатору.

**Исключения:**
- `ArgumentNullException` — `set` равен `null`.
- `InvalidOperationException` — `id == default`.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| SoftDelete / Recover | Идемпотентность | Повторный вызов не меняет состояние и метки |
| HardRemove | `Id != default` | Проверяется перед выполнением |
| HardRemove | `null`-элементы в коллекции | `InvalidOperationException` до начала удаления |
| FlushInfrastructure | Потокобезопасность | SpinWait + CAS; лимит 100 000 спинов |

## Сценарии использования

Мягкое удаление через метод расширения:

```csharp
transaction.SoftDelete<Transaction, long>();
```

Массовое восстановление:

```csharp
Transaction[] archived = await context.Set<Transaction>()
    .IgnoreQueryFilters()
    .Where(t => t.IsDeleted)
    .ToArrayAsync(ct);

archived.Recover<Transaction, long>(); // через аналог с IEnumerable
```

Физическое удаление по идентификаторам:

```csharp
await context.Set<Transaction>().HardRemoveRangeAsync<Transaction, long>(ids, cancellationToken);
```

## Обработка ошибок

| Ситуация | Метод | Поведение |
|----------|-------|-----------|
| `entity == null` | Все методы над сущностью | `ArgumentNullException` |
| `null`-элемент в коллекции | `SoftDelete(IEnumerable)`, `HardRemoveRangeAsync(IEnumerable<TEntity>)` | `InvalidOperationException` |
| `Id == default` | `HardRemove*` | `InvalidOperationException` |
| Превышен лимит спинов | `FlushInfrastructureTimestamps` | `InvalidOperationException` |
| Нарушение пост-инвариантов | `SoftDelete`, `Recover` | `InvalidOperationException` |

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| HardRemove | Требует `ISoftDeletableEntity<TKey>` — для обычных `IEntity` физическое удаление выполняется стандартными средствами EF Core |
| HardRemoveRangeAsync | Использует `ExecuteDeleteAsync` (bulk-delete без загрузки в память) + ручная очистка local-кеша |
| Метод `Touch` | Монотонен — понижение метки невозможно |
