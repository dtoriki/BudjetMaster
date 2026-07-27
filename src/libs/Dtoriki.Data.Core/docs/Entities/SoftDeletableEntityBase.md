# SoftDeletableEntityBase / SoftDeletableEntityBase\<TKey\>

[← Entities](./README.md) · [← Библиотека](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Классы](#классы)
- [Свойства](#свойства)
- [Методы](#методы)
- [Инварианты и правила](#инварианты-и-правила)
- [Потокобезопасность](#потокобезопасность)
- [Сценарии использования](#сценарии-использования)
- [Обработка ошибок](#обработка-ошибок)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Расширяет `EntityBase<TKey>` реализацией мягкого (логического) удаления. Переключение признака удаления выполняется идемпотентными lock-free операциями (CAS над внутренним трёхзначным состоянием).

Исключения класса: `InvalidOperationException`.

## Классы

```csharp
public abstract class SoftDeletableEntityBase<TKey> : EntityBase<TKey>, ISoftDeletableEntity<TKey>
    where TKey : IEquatable<TKey>

public abstract class SoftDeletableEntityBase : SoftDeletableEntityBase<long>
```

`SoftDeletableEntityBase` — алиас для наиболее частого случая (`TKey = long`).

## Свойства

#### IsDeleted { get; set; }

Признак мягкого удаления. Установка `true` вызывает `SoftDelete()`, установка `false` — `Recover()`. Повторная установка того же значения идемпотентна.

#### DeletedAtUtc { get; set; }

UTC-время мягкого удаления. `null`, если сущность активна или восстановлена.

**Исключения при установке:**
- `InvalidOperationException` — попытка установить `null` при `IsDeleted == true`.
- `InvalidOperationException` — попытка установить значение при `IsDeleted == false`.
- `InvalidOperationException` — значение не в UTC.
- `InvalidOperationException` — значение раньше `CreatedAtUtc` или позже `RecoveredAtUtc`.

#### RecoveredAtUtc { get; set; }

UTC-время восстановления. `null`, если сущность ни разу не восстанавливалась.

**Исключения при установке:**
- `InvalidOperationException` — значение раньше `DeletedAtUtc` или не в UTC.

## Методы

#### SoftDelete() — protected virtual

Потокобезопасно и идемпотентно помечает сущность как удалённую: `IsDeleted = true`, фиксирует `DeletedAtUtc = UtcNow`, обнуляет `RecoveredAtUtc`. Вызывает `Touch()`.

**Исключения:**
- `InvalidOperationException` — нарушение пост-инвариантов (неожиданно).

#### Recover() — protected virtual

Потокобезопасно и идемпотентно восстанавливает сущность: `IsDeleted = false`, фиксирует `RecoveredAtUtc = UtcNow`, обнуляет `DeletedAtUtc`. Вызывает `Touch()`.

**Исключения:**
- `InvalidOperationException` — нарушение пост-инвариантов (неожиданно).

#### FlushInfrastructure() — protected override

Переносит инфраструктурные поля временных меток (базовый `FlushInfrastructure`) и дополнительно синхронизирует `_isDeletedInfrastructure` → `_deletedState`. Проверяет инварианты дат после переноса.

**Исключения:**
- `InvalidOperationException` — `DeletedAtUtc < CreatedAtUtc` или `RecoveredAtUtc < DeletedAtUtc`.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| IsDeleted == true | `DeletedAtUtc != null`, `RecoveredAtUtc == null` | Всегда |
| IsDeleted == false (после удаления) | `RecoveredAtUtc != null`, `DeletedAtUtc == null` | Всегда |
| Порядок дат | `DeletedAtUtc >= CreatedAtUtc` | Всегда |
| Порядок дат | `RecoveredAtUtc >= DeletedAtUtc` | Если оба установлены |
| Идемпотентность | Повторный `SoftDelete()` / `Recover()` | Состояние и метки не изменяются |

## Потокобезопасность

Внутреннее состояние удаления — трёхзначное: `0` (активен), `1` (удалён), `2` (переход). Переходы: `0→2→1` (удаление) и `1→2→0` (восстановление). Маркер `2` никогда не фиксируется как конечное состояние — ABA-проблема исключена. Чтение `IsDeleted` использует [`Volatile.Read`](https://learn.microsoft.com/dotnet/api/system.threading.volatile.read) для актуальной видимости результата CAS.

## Сценарии использования

Мягко удаляемая сущность с `long`-ключом:

```csharp
public sealed class Transaction : SoftDeletableEntityBase
{
    public decimal Amount { get; set; }
}
```

Операции через прикладной код:

```csharp
transaction.IsDeleted = true;  // вызывает SoftDelete()
// transaction.DeletedAtUtc != null

transaction.IsDeleted = false; // вызывает Recover()
// transaction.RecoveredAtUtc != null
```

Через методы расширения (для внешнего кода):

```csharp
transaction.SoftDelete<Transaction, long>();
transaction.Recover<Transaction, long>();
```

## Обработка ошибок

| Ситуация | Метод | Поведение |
|----------|-------|-----------|
| `DeletedAtUtc < CreatedAtUtc` после flush | `FlushInfrastructure` | `InvalidOperationException` |
| `RecoveredAtUtc < DeletedAtUtc` после flush | `FlushInfrastructure` | `InvalidOperationException` |
| Нарушение инвариантов DeletedAtUtc | Сеттер `DeletedAtUtc` | `InvalidOperationException` |
| Значение RecoveredAtUtc раньше DeletedAtUtc | Сеттер `RecoveredAtUtc` | `InvalidOperationException` |

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| Конструктор | `protected` — тип абстрактный |
| `SoftDelete` / `Recover` | `protected virtual` — для вызова из производных классов или через явную реализацию `ISoftDeletableEntity` |
