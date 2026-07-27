namespace Dtoriki.Data.Core.Entities;

/// <summary>
/// Представляет сущность с поддержкой механизма мягкого (логического) удаления:
/// вместо физического удаления проставляется признак <see cref="IsDeleted"/> и временные метки.
/// </summary>
/// <remarks>
/// Мягкое удаление позволяет:
/// <list type="bullet">
/// <item>
/// <description>Сохранить данные для аудита и восстановления.</description>
/// </item>
/// <item>
/// <description>Использовать фильтрацию активных / удалённых записей.</description>
/// </item>
/// <item>
/// <description>Фиксировать время удаления (<see cref="DeletedAtUtc"/>) и восстановления (<see cref="RecoveredAtUtc"/>).</description>
/// </item>
/// </list>
/// </remarks>
public interface ISoftDeletableEntity : IEntity
{
    /// <summary>
    /// Возвращает <see langword="true"/>, если сущность помечена как удалённая;
    /// иначе возвращает <see langword="false"/>.
    /// </summary>
    bool IsDeleted { get; }

    /// <summary>
    /// Возвращает UTC-время,
    /// когда сущность была помечена как удалённая. Может быть <see langword="null"/>, если не удалена.
    /// </summary>
    DateTime? DeletedAtUtc { get; }

    /// <summary>
    /// Возвращает UTC-время,
    /// когда сущность была восстановлена после мягкого удаления. Может быть <see langword="null"/>, если не восстанавливалась.
    /// </summary>
    DateTime? RecoveredAtUtc { get; }

    /// <summary>
    /// Помечает сущность как удалённую (мягкое удаление).
    /// </summary>
    void SoftDelete();

    /// <summary>
    /// Восстанавливает сущность после мягкого удаления.
    /// </summary>
    void Recover();
}

/// <summary>
/// Представляет сущность с поддержкой мягкого удаления и типизированным идентификатором.
/// </summary>
/// <typeparam name="TKey">
/// Тип уникального идентификатора сущности.
/// </typeparam>
public interface ISoftDeletableEntity<TKey> : ISoftDeletableEntity, IEntity<TKey>
    where TKey : IEquatable<TKey>
{
}
