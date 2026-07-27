namespace Dtoriki.Data.Core.Entities;

/// <summary>
/// Представляет сущность базы данных с типизированным уникальным идентификатором.
/// </summary>
/// <typeparam name="TKey">
/// Тип уникального идентификатора сущности. Должен реализовывать <see cref="IEquatable{T}"/>.
/// </typeparam>
public interface IEntity<TKey> : IEntity
 where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Возвращает уникальный идентификатор сущности.
    /// </summary>
    TKey Id { get; }
}

/// <summary>
/// Представляет сущность базы данных с базовыми временными метками жизненного цикла.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Возвращает дату и время (UTC) создания сущности.
    /// </summary>
    DateTime CreatedAtUtc { get; }

    /// <summary>
    /// Возвращает дату и время (UTC) последнего изменения сущности.
    /// </summary>
    DateTime LastUpdatedAtUtc { get; }

    /// <summary>
    /// Обновляет временную метку последнего изменения сущности.
    /// </summary>
    void Touch();

    /// <summary>
    /// Устанавливает дату и время (UTC) создания сущности.
    /// </summary>
    /// <param name="dateTime">Дата и время (UTC), которое будет установлено как время создания.</param>
    void SetCreated(DateTime dateTime);
}
