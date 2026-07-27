# IEntity / IEntity\<TKey\>

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

Определяет базовый контракт сущности базы данных. `IEntity` описывает временной жизненный цикл (создание и последнее обновление). `IEntity<TKey>` расширяет его типизированным первичным ключом.

## Интерфейсы

```csharp
public interface IEntity
public interface IEntity<TKey> : IEntity where TKey : IEquatable<TKey>
```

## Свойства

#### CreatedAtUtc { get; }

Дата и время создания сущности в UTC. Устанавливается один раз при первом сохранении или явном вызове `SetCreated()`. Неизменна после инициализации.

#### LastUpdatedAtUtc { get; }

Дата и время последнего изменения в UTC. Обновляется при каждом `Touch()` или `SaveChanges()`. Не может быть раньше `CreatedAtUtc`.

#### Id { get; } — только IEntity\<TKey\>

Типизированный уникальный идентификатор сущности. Тип `TKey` должен реализовывать [`IEquatable<T>`](https://learn.microsoft.com/dotnet/api/system.iequatable-1).

## Методы

#### Touch()

Обновляет `LastUpdatedAtUtc` до текущего UTC. Идемпотентен по отношению к попыткам «понизить» значение — более ранняя метка игнорируется. Реализован потокобезопасно.

#### SetCreated(DateTime dateTime)

Устанавливает `CreatedAtUtc`. Значение должно быть UTC и не позже текущего `LastUpdatedAtUtc`.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| CreatedAtUtc | `Kind == DateTimeKind.Utc` | Обязательно |
| LastUpdatedAtUtc | `>= CreatedAtUtc` | Всегда |
| SetCreated | Вызывается не более одного раза | Повторный вызов с более поздним значением → `ArgumentOutOfRangeException` |

## Сценарии использования

Реализация собственной сущности:

```csharp
public sealed class Budget : EntityBase<Guid>
{
    public string Name { get; set; } = string.Empty;
}
```

Доступ к меткам жизненного цикла через контракт:

```csharp
void PrintLifecycle(IEntity entity)
{
    Console.WriteLine($"Создана: {entity.CreatedAtUtc}");
    Console.WriteLine($"Обновлена: {entity.LastUpdatedAtUtc}");
}
```

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| TKey | Должен реализовывать `IEquatable<TKey>` |
| CreatedAtUtc | Не предназначен для ручного переустановления в прикладном коде |
| Временные метки | Только UTC; локальное время и `DateTimeKind.Unspecified` не допускаются |
