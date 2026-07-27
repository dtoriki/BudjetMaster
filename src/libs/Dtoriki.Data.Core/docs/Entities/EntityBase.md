# EntityBase / EntityBase\<TKey\>

[← Entities](./README.md) · [← Библиотека](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Классы](#классы)
- [Свойства](#свойства)
- [Методы](#методы)
- [Инварианты и правила](#инварианты-и-правила)
- [Инфраструктурный перенос меток](#инфраструктурный-перенос-меток)
- [Сценарии использования](#сценарии-использования)
- [Обработка ошибок](#обработка-ошибок)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Абстрактная реализация `IEntity<TKey>` с потокобезопасными UTC-временными метками жизненного цикла и механизмом ленивого переноса инфраструктурных данных из EF Core.

Исключения класса: `ArgumentException`, `ArgumentOutOfRangeException`.

## Классы

```csharp
public abstract class EntityBase<TKey> : IEntity<TKey>
    where TKey : IEquatable<TKey>

public abstract class EntityBase : EntityBase<long>
```

`EntityBase` — удобный алиас для наиболее частого случая (`TKey = long`).

## Свойства

#### Id { get; set; }

Уникальный идентификатор сущности. Тип определяется параметром `TKey`.

#### CreatedAtUtc { get; set; }

Дата создания (UTC). Сеттер вызывает `SetCreated()`. Не может быть позже текущего `LastUpdatedAtUtc`.

**Исключения:**
- `ArgumentException` — значение не в UTC.
- `ArgumentOutOfRangeException` — значение позже `LastUpdatedAtUtc`.

#### LastUpdatedAtUtc { get; set; }

Дата последнего обновления (UTC). Хранится как `long` (ticks) для атомарного чтения через [`Interlocked.Read`](https://learn.microsoft.com/dotnet/api/system.threading.interlocked.read). Не может быть раньше `CreatedAtUtc`.

**Исключения:**
- `ArgumentException` — значение не в UTC.
- `ArgumentOutOfRangeException` — значение раньше `CreatedAtUtc`.

## Методы

#### SetCreated(DateTime dateTime)

Устанавливает `CreatedAtUtc`. Требует UTC.

**Исключения:**
- `ArgumentException` — `dateTime.Kind != DateTimeKind.Utc`.
- `ArgumentOutOfRangeException` — `dateTime > LastUpdatedAtUtc`.

#### Touch()

Монотонно обновляет `LastUpdatedAtUtc` до `DateTime.UtcNow`. Потокобезопасен (CAS-цикл). Попытка «понизить» метку игнорируется без ошибки.

**Исключения:**
- `InvalidOperationException` — нарушение инвариантов при обнаружении после ленивого переноса инфраструктурных меток.

#### SetLastUpdatedAtUtcUnsafe(DateTime value)

Небезопасная (без гарантии монотонности) установка `LastUpdatedAtUtc`. Допускает снижение метки до значения не раньше `CreatedAtUtc`. Только для инфраструктурных сценариев: импорт, миграции, реплей истории.

**Исключения:**
- `ArgumentException` — значение не в UTC.
- `ArgumentOutOfRangeException` — значение раньше `CreatedAtUtc`.

#### FlushInfrastructure() — protected virtual

Выполняет ленивый перенос инфраструктурных полей (`_createdAtUtcInfrastructure`, `_lastUpdatedAtInfrastructure`) в рабочие поля. Вызывается автоматически геттерами `CreatedAtUtc` и `LastUpdatedAtUtc`. Потокобезопасен и идемпотентен.

**Исключения:**
- `InvalidOperationException` — нарушение инвариантов при переносе.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| Временные метки | `DateTimeKind.Utc` | Обязательно для всех значений |
| Порядок | `CreatedAtUtc` ≤ `LastUpdatedAtUtc` | Всегда |
| Touch | Монотонность | Более ранняя метка игнорируется без исключения |
| Инфраструктурный перенос | Выполняется ровно один раз | Идемпотентен, потокобезопасен |

## Инфраструктурный перенос меток

EF Core материализует сущность через конструктор и инфраструктурные поля (`_createdAtUtcInfrastructure`, `_lastUpdatedAtInfrastructure`). При первом обращении к `CreatedAtUtc` или `LastUpdatedAtUtc` выполняется `FlushInfrastructure()`, которая атомарно переносит значения в рабочие поля.

Это позволяет прикладному коду работать с корректными значениями без ручной инициализации, даже при конкурентном доступе из нескольких потоков.

Явный вызов `FlushInfrastructure()` допустим в сценариях предварительной материализации для исключения первого ленивого переноса в горячем пути.

## Сценарии использования

Базовая сущность с `long`-ключом:

```csharp
public sealed class Transaction : EntityBase
{
    public decimal Amount { get; set; }
}
```

Базовая сущность с `Guid`-ключом:

```csharp
public sealed class Category : EntityBase<Guid>
{
    public string Name { get; set; } = string.Empty;
}
```

## Обработка ошибок

| Ситуация | Метод | Поведение |
|----------|-------|-----------|
| Значение не в UTC | `SetCreated`, сеттер `LastUpdatedAtUtc` | `ArgumentException` |
| Нарушение порядка дат | `SetCreated`, сеттер `LastUpdatedAtUtc` | `ArgumentOutOfRangeException` |
| Нарушение инвариантов при flush | `FlushInfrastructure` | `InvalidOperationException` |
| Превышен лимит CAS-попыток | `Touch` (через `SetLastUpdatedAtUtcSafe`) | `InvalidOperationException` |

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| Конструктор | `protected` — тип абстрактный, наследование обязательно |
| `SetLastUpdatedAtUtcUnsafe` | Только для инфраструктурных сценариев; в прикладном коде использовать `Touch()` |
| TKey | Должен реализовывать `IEquatable<TKey>` |
