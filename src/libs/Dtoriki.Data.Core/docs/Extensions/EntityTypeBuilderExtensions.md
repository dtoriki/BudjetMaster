# EntityTypeBuilderExtensions

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

Методы расширения для [`EntityTypeBuilder<T>`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.entitytypebuilder-1) и [`IndexBuilder<T>`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.indexbuilder-1). Конфигурируют стандартные индексы и частичные фильтры с учётом поддержки мягкого удаления и устаревания.

Исключения класса: `ArgumentNullException`, `InvalidOperationException`.

## Методы

#### SetDefaultIndexes\<TEntity, TKey\>(this EntityTypeBuilder\<TEntity\>)

Конфигурирует стандартный набор индексов для сущности с типизированным ключом:
- первичный ключ по `Id`,
- индексы `CreatedAtUtc DESC` и `LastUpdatedAtUtc DESC`,
- составные индексы `(Id, CreatedAtUtc)`, `(Id, LastUpdatedAtUtc)`, `(CreatedAtUtc, Id)`, `(LastUpdatedAtUtc, Id)`,
- частичные фильтры в зависимости от реализации `ISoftDeletableEntity` / `ICanOutdated`.

**Исключения:**
- `ArgumentNullException` — `modelBuilder` равен `null`.
- `InvalidOperationException` — CLR-тип builder не совпадает с `TEntity`.

#### SetDefaultIndexes\<TEntity\>(this EntityTypeBuilder\<TEntity\>)

Перегрузка без явного `TKey`. Не устанавливает первичный ключ и не добавляет составные ключевые индексы.

**Исключения:** аналогичны типизированной версии.

#### HasFilterWithNotDeleted\<T\>(this IndexBuilder\<T\>, string? filter)

Добавляет к индексу фильтр `not(is_deleted)`. Если передан `filter`, объединяет через `AND`.

**Исключения:**
- `ArgumentNullException` — `builder` равен `null`.

#### HasFilterWithNotDeletedAndNotOutdated\<T\>(this IndexBuilder\<T\>, string? filter)

Добавляет фильтр `not(is_deleted) and not(is_outdated)`. Применяется только если `T` реализует оба интерфейса.

**Исключения:**
- `ArgumentNullException` — `builder` равен `null`.

#### HasFilterWithMaxLengthAndNotDeletedAndNotOutdated\<TEntity\>(this IndexBuilder\<TEntity\>, string columnName, int maxLength)

Добавляет фильтр `not(is_deleted) and not(is_outdated) and (length("columnName") <= maxLength)`.

**Исключения:**
- `ArgumentNullException` — `builder` или `columnName` равны `null`.
- `ArgumentException` — `columnName` пустой или из пробелов.
- `ArgumentOutOfRangeException` — `maxLength < 1`.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| Тип builder | CLR-тип совпадает с TEntity | Проверяется, иначе `InvalidOperationException` |
| Фильтры | Применяются только при наличии нужных интерфейсов | Проверяется через `IsAssignableTo` |
| Составные индексы | Только в типизированной версии `SetDefaultIndexes<TEntity, TKey>` | Нетипизированная версия их не создаёт |

## Сценарии использования

Конфигурация в `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Transaction>()
        .SetDefaultIndexes<Transaction, long>();
}
```

Произвольный индекс с фильтром:

```csharp
modelBuilder.Entity<ExchangeRate>()
    .HasIndex(r => r.CurrencyCode)
    .HasFilterWithNotDeletedAndNotOutdated();
```

Индекс с ограничением длины:

```csharp
modelBuilder.Entity<Category>()
    .HasIndex(c => c.Name)
    .HasFilterWithMaxLengthAndNotDeletedAndNotOutdated("name", 100);
```

## Обработка ошибок

| Ситуация | Метод | Поведение |
|----------|-------|-----------|
| `modelBuilder == null` | `SetDefaultIndexes` | `ArgumentNullException` |
| CLR-тип не совпадает с TEntity | `SetDefaultIndexes` | `InvalidOperationException` |
| `builder == null` | `HasFilter*` | `ArgumentNullException` |
| `columnName` пустой | `HasFilterWithMaxLength*` | `ArgumentException` |
| `maxLength < 1` | `HasFilterWithMaxLength*` | `ArgumentOutOfRangeException` |

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| Фильтры | Зависят от диалекта БД; строки `not(is_deleted)` предназначены для PostgreSQL |
| `SetDefaultIndexes` | Не устанавливает `HasQueryFilter` — только индексы |
| `HasFilterWithNotDeleted*` | Требует `T : ISoftDeletableEntity`; при несоответствии фильтр не применяется (silent fallback к `HasFilter(filter)`) |
