# IEfContext / IEfContext\<TEntity, TKey\>

[← Context](./README.md) · [← Библиотека](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Интерфейсы](#интерфейсы)
- [Методы](#методы)
- [Инварианты и правила](#инварианты-и-правила)
- [Сценарии использования](#сценарии-использования)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Определяет контракт абстрактного контекста доступа к данным: управление транзакциями и фиксация изменений. Расширяет [`IDisposable`](https://learn.microsoft.com/dotnet/api/system.idisposable) и [`IAsyncDisposable`](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable).

## Интерфейсы

```csharp
public interface IEfContext : IDisposable, IAsyncDisposable

public interface IEfContext<TEntity, TKey> : IEfContext
    where TEntity : class, IEntity<TKey>, new()
    where TKey : IEquatable<TKey>
```

## Методы

#### BeginTransaction()

Открывает новую транзакцию. Возвращает [`IDbContextTransaction`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.storage.idbcontexttransaction).

#### BeginTransactionAsync(CancellationToken)

Асинхронно открывает транзакцию.

#### SaveChanges()

Синхронно фиксирует все накопленные изменения. Возвращает количество затронутых строк.

#### SaveChanges(bool acceptAllChangesOnSuccess)

То же, что `SaveChanges()`, но с явным управлением подтверждением трекинга после успешной фиксации.

#### SaveChangesAsync(CancellationToken)

Асинхронно фиксирует все изменения.

#### SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken)

Асинхронная версия с управлением подтверждением трекинга.

#### Set() — только IEfContext\<TEntity, TKey\>

Возвращает [`DbSet<TEntity>`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbset-1) для выполнения CRUD-операций.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| Транзакции | Управление через `IDbContextTransaction` | Commit / Rollback на усмотрение вызывающего |

## Сценарии использования

Использование в репозитории:

```csharp
public sealed class TransactionRepository(IEfContext<Transaction, long> context)
{
    public async Task<Transaction[]> GetAllAsync(CancellationToken ct)
    {
        return await context.Set()
            .Where(t => !t.IsDeleted)
            .ToArrayAsync(ct);
    }
}
```

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| TEntity | Должен быть `class`, реализовывать `IEntity<TKey>` и иметь конструктор без параметров |
| TKey | Должен реализовывать `IEquatable<TKey>` |
