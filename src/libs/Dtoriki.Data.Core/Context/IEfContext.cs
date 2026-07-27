using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Dtoriki.Data.Core.Entities;

namespace Dtoriki.Data.Core.Context;

/// <summary>
/// Определяет контракт абстрактного контекста доступа/изменения данных,
/// обеспечивающего управление транзакциями и фиксацию накопленных изменений.
/// Интерфейс расширяет <see cref="IDisposable"/> и <see cref="IAsyncDisposable"/> для корректного освобождения ресурсов.
/// </summary>
public interface IEfContext : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Открывает новую транзакцию в текущем контексте хранения данных.
    /// </summary>
    /// <returns>Объект <see cref="IDbContextTransaction"/> для управления транзакцией (commit / rollback).</returns>
    IDbContextTransaction BeginTransaction();

    /// <summary>
    /// Асинхронно открывает новую транзакцию.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, результатом которой является объект транзакции.</returns>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Синхронно фиксирует все накопленные изменения.
    /// Эквивалент вызова перегрузки с параметром подтверждения, установленным в <see langword="true"/>.
    /// </summary>
    /// <returns>Количество изменённых элементов состояния (записей), отражённых в хранилище.</returns>
    int SaveChanges();

    /// <summary>
    /// Синхронно фиксирует накопленные изменения с указанием поведения подтверждения.
    /// Если <paramref name="acceptAllChangesOnSuccess"/> установлено в <see langword="true"/>,
    /// после успешной операции выполняется внутреннее принятие состояний (очистка/подтверждение локального трекинга).
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Выполнять ли внутреннее подтверждение отслеженных изменений после успешной фиксации.</param>
    /// <returns>Количество изменённых элементов состояния, отражённых в хранилище.</returns>
    int SaveChanges(bool acceptAllChangesOnSuccess);

    /// <summary>
    /// Асинхронно фиксирует накопленные изменения с указанием поведения подтверждения.
    /// При <paramref name="acceptAllChangesOnSuccess"/> = <see langword="true"/> выполняется внутреннее подтверждение отслеженных изменений.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Выполнять ли внутреннее подтверждение отслеженных изменений после успешной фиксации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество изменённых элементов состояния, отражённых в хранилище.</returns>
    Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно фиксирует все накопленные изменения.
    /// Эквивалент вызова асинхронной перегрузки с параметром подтверждения, установленным в <c>true</c>.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество изменённых элементов состояния, отражённых в хранилище.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Определяет контракт абстрактного контекста доступа/изменения данных для конкретного типа сущности,
/// обеспечивающего управление транзакциями и фиксацию накопленных изменений.
/// </summary>
/// <typeparam name="TEntity">Тип сущности, с которой будет работать контекст.</typeparam>
/// <typeparam name="TKey">Тип ключа сущности.</typeparam>
public interface IEfContext<TEntity, TKey> : IEfContext
    where TEntity : class, IEntity<TKey>, new()
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Получает набор сущностей типа <typeparamref name="TEntity"/> для выполнения операций CRUD.
    /// </summary>
    /// <returns>Набор сущностей типа <typeparamref name="TEntity"/>.</returns>
    DbSet<TEntity> Set();
}
