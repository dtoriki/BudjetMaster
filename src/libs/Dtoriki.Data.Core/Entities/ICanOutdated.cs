namespace Dtoriki.Data.Core.Entities;

/// <summary>
/// Определяет контракт для типа, который может становиться устаревшим
/// и сообщает об этом через флаг <see cref="IsOutdated"/>.
/// </summary>
public interface ICanOutdated
{
    /// <summary>
    /// Возвращает или задаёт признак устаревания значения:
    /// <see langword="true"/> — данные помечены как устаревшие;
    /// <see langword="false"/> — данные актуальны.
    /// </summary>
    bool IsOutdated { get; set; }
}
