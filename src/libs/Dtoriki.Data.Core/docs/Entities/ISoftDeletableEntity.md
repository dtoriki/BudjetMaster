# ISoftDeletableEntity / ISoftDeletableEntity\<TKey\>

[← Entities](./README.md) · [← Библиотека](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Интерфейсы](#интерфейсы)
- [Свойства](#свойства)
- [Методы](#методы)
- [Инварианты и правила](#инварианты-и-правила)
- [Сценарии использования](#сценарии-использования)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Описывает контракт мягкого (логического) удаления: вместо физического удаления строки проставляется признак `IsDeleted` и фиксируются временные метки операций. Позволяет сохранить данные для аудита и восстановления, а также фильтровать активные и удалённые записи на уровне запросов.

## Интерфейсы

```csharp
public interface ISoftDeletableEntity : IEntity
public interface ISoftDeletableEntity<TKey> : ISoftDeletableEntity, IEntity<TKey>
    where TKey : IEquatable<TKey>
```

## Свойства

#### IsDeleted { get; }

`true` — сущность помечена как удалённая; `false` — активна.

#### DeletedAtUtc { get; }

UTC-время мягкого удаления. `null`, если сущность не была удалена или восстановлена после удаления.

#### RecoveredAtUtc { get; }

UTC-время последнего восстановления. `null`, если сущность ни разу не восстанавливалась.

## Методы

#### SoftDelete()

Помечает сущность как удалённую: `IsDeleted = true`, фиксирует `DeletedAtUtc`, обнуляет `RecoveredAtUtc`. Идемпотентен — повторный вызов не изменяет состояние.

#### Recover()

Восстанавливает сущность: `IsDeleted = false`, фиксирует `RecoveredAtUtc`, обнуляет `DeletedAtUtc`. Идемпотентен.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| IsDeleted == true | `DeletedAtUtc != null`, `RecoveredAtUtc == null` | Всегда |
| IsDeleted == false (после удаления) | `RecoveredAtUtc != null`, `DeletedAtUtc == null` | Всегда |
| Порядок дат | `DeletedAtUtc >= CreatedAtUtc` | Всегда |
| Порядок дат | `RecoveredAtUtc >= DeletedAtUtc` | Если оба установлены |
| Идемпотентность | Повторный `SoftDelete()` / `Recover()` | Состояние не меняется, метки не обновляются |

## Сценарии использования

Мягкое удаление и восстановление через интерфейс:

```csharp
void Archive(ISoftDeletableEntity entity)
{
    entity.SoftDelete();
    // entity.IsDeleted == true
    // entity.DeletedAtUtc != null
}

void Restore(ISoftDeletableEntity entity)
{
    entity.Recover();
    // entity.IsDeleted == false
    // entity.RecoveredAtUtc != null
}
```

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| Временные метки | Только UTC |
