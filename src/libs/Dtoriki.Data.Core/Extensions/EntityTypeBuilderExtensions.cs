using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Dtoriki.Data.Core.Entities;

namespace Dtoriki.Data.Core.Extensions;

/// <summary>
/// Методы расширения для конфигурирования сущностей (установка стандартных индексов и фильтров).
/// </summary>
/// <exception cref="ArgumentNullException"/>
/// <exception cref="InvalidOperationException"/>
public static class EntityTypeBuilderExtensions
{
    private const string NOT_DELETED_FILTER = "not(is_deleted)";
    private const string NOT_DELETED_AND_NOT_OUTDATED_FILTER = "not(is_deleted) and not(is_outdated)";

    /// <summary>
    /// Конфигурирует стандартный набор индексов для типа <typeparamref name="TEntity"/> с учётом поддержки мягкого удаления и устаревания.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <typeparam name="TKey">Тип идентификатора сущности.</typeparam>
    /// <param name="modelBuilder">Построитель типа сущности.</param>
    /// <returns>Тот же экземпляр <paramref name="modelBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="modelBuilder"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если фактический CLR‑тип не совпадает с <typeparamref name="TEntity"/>.</exception>
    public static EntityTypeBuilder<TEntity> SetDefaultIndexes<TEntity, TKey>(this EntityTypeBuilder<TEntity> modelBuilder)
        where TEntity : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        if (modelBuilder.Metadata.ClrType != typeof(TEntity))
        {
            throw new InvalidOperationException(
                $"Несоответствие типов при конфигурации индексов. Ожидался {typeof(TEntity).FullName}, получен {modelBuilder.Metadata.ClrType.FullName}.");
        }

        modelBuilder.HasKey(e => e.Id);

        bool supportsSoftDelete = typeof(TEntity).IsAssignableTo(typeof(ISoftDeletableEntity));
        bool supportsOutdated = typeof(TEntity).IsAssignableTo(typeof(ICanOutdated));

        if (supportsSoftDelete && supportsOutdated)
        {
            AddBaseIndexes<TEntity, TKey>(modelBuilder, withSoftDelete: true, withOutdated: true);

            return modelBuilder;
        }

        if (supportsSoftDelete)
        {
            AddBaseIndexes<TEntity, TKey>(modelBuilder, withSoftDelete: true, withOutdated: false);

            return modelBuilder;
        }

        AddBaseIndexes<TEntity, TKey>(modelBuilder, withSoftDelete: false, withOutdated: false);

        return modelBuilder;
    }

    /// <summary>
    /// Конфигурирует стандартный набор индексов для типа <typeparamref name="TEntity"/> с учётом поддержки мягкого удаления и устаревания (без указания ключа).
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <param name="modelBuilder">Построитель типа сущности.</param>
    /// <returns>Тот же экземпляр <paramref name="modelBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="modelBuilder"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если фактический CLR‑тип не совпадает с <typeparamref name="TEntity"/>.</exception>
    public static EntityTypeBuilder<TEntity> SetDefaultIndexes<TEntity>(this EntityTypeBuilder<TEntity> modelBuilder)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        if (modelBuilder.Metadata.ClrType != typeof(TEntity))
        {
            throw new InvalidOperationException(
                $"Несоответствие типов при конфигурации индексов. Ожидался {typeof(TEntity).FullName}, получен {modelBuilder.Metadata.ClrType.FullName}.");
        }

        bool supportsSoftDelete = typeof(TEntity).IsAssignableTo(typeof(ISoftDeletableEntity));
        bool supportsOutdated = typeof(TEntity).IsAssignableTo(typeof(ICanOutdated));

        if (supportsSoftDelete && supportsOutdated)
        {
            AddBaseIndexes(modelBuilder, withSoftDelete: true, withOutdated: true);

            return modelBuilder;
        }

        if (supportsSoftDelete)
        {
            AddBaseIndexes(modelBuilder, withSoftDelete: true, withOutdated: false);

            return modelBuilder;
        }

        AddBaseIndexes(modelBuilder, withSoftDelete: false, withOutdated: false);

        return modelBuilder;
    }

    /// <summary>
    /// Добавляет к индексу фильтр not(is_deleted) и, при наличии, дополняет его условием <paramref name="filter"/>.
    /// </summary>
    /// <typeparam name="T">Тип сущности.</typeparam>
    /// <param name="builder">Построитель индекса.</param>
    /// <param name="filter">Дополнительное условие (объединяется через AND).</param>
    /// <returns>Тот же <paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="builder"/> равен <see langword="null"/>.</exception>
    public static IndexBuilder<T> HasFilterWithNotDeleted<T>(this IndexBuilder<T> builder, string? filter = null)
        where T : class, ISoftDeletableEntity
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.HasFilterWithNotDeletedInternal(filter);
    }

    /// <summary>
    /// Добавляет к индексу фильтр not(is_deleted) and not(is_outdated) и, при наличии, дополняет его условием <paramref name="filter"/>.
    /// </summary>
    /// <typeparam name="T">Тип сущности.</typeparam>
    /// <param name="builder">Построитель индекса.</param>
    /// <param name="filter">Дополнительное условие (объединяется через AND).</param>
    /// <returns>Тот же <paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="builder"/> равен <see langword="null"/>.</exception>
    public static IndexBuilder<T> HasFilterWithNotDeletedAndNotOutdated<T>(this IndexBuilder<T> builder, string? filter = null)
        where T : class, ISoftDeletableEntity, ICanOutdated
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.HasFilterWithNotDeletedAndNotOutdatedInternal(filter);
    }

    /// <summary>
    /// Добавляет к индексу фильтр not(is_deleted) and not(is_outdated) и ограничение длины:
    /// length(columnName) &lt;= <paramref name="maxLength"/>.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <param name="builder">Построитель индекса.</param>
    /// <param name="columnName">Имя столбца для проверки длины.</param>
    /// <param name="maxLength">Максимально допустимая длина (≥1).</param>
    /// <returns>Тот же <paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="builder"/> или <paramref name="columnName"/> равны <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Если <paramref name="columnName"/> пустой или состоит из пробелов.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Если <paramref name="maxLength"/> &lt;1.</exception>
    public static IndexBuilder<TEntity> HasFilterWithMaxLengthAndNotDeletedAndNotOutdated<TEntity>(
        this IndexBuilder<TEntity> builder,
        string columnName,
        int maxLength)
        where TEntity : class, ISoftDeletableEntity, ICanOutdated
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(columnName);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);
        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ArgumentException("Имя столбца не может быть пустым или пробельным.", nameof(columnName));
        }

        string filterExpression = $"length(\"{columnName}\") <= {maxLength}";

        return builder.HasFilterWithNotDeletedAndNotOutdatedInternal(filterExpression);
    }

    private static void AddBaseIndexes<TEntity, TKey>(
        EntityTypeBuilder<TEntity> modelBuilder,
        bool withSoftDelete,
        bool withOutdated)
        where TEntity : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        AddBaseIndexes(modelBuilder, withSoftDelete, withOutdated);

        modelBuilder
            .HasIndex(e => new { e.Id, e.CreatedAtUtc })
            .IsUnique(true)
            .ApplyDynamicFilter(withSoftDelete, withOutdated);

        modelBuilder
            .HasIndex(e => new { e.Id, e.LastUpdatedAtUtc })
            .IsUnique(true)
            .ApplyDynamicFilter(withSoftDelete, withOutdated);

        modelBuilder
            .HasIndex(e => new { e.CreatedAtUtc, e.Id })
            .IsUnique(true)
            .ApplyDynamicFilter(withSoftDelete, withOutdated);

        modelBuilder
            .HasIndex(e => new { e.LastUpdatedAtUtc, e.Id })
            .IsUnique(true)
            .ApplyDynamicFilter(withSoftDelete, withOutdated);
    }

    private static void AddBaseIndexes<TEntity>(
        EntityTypeBuilder<TEntity> modelBuilder,
        bool withSoftDelete,
        bool withOutdated)
        where TEntity : class, IEntity
    {
        modelBuilder
            .HasIndex(e => e.CreatedAtUtc)
            .IsUnique(false)
            .IsDescending()
            .ApplyDynamicFilter(withSoftDelete, withOutdated);

        modelBuilder
            .HasIndex(e => e.LastUpdatedAtUtc)
            .IsUnique(false)
            .IsDescending()
            .ApplyDynamicFilter(withSoftDelete, withOutdated);
    }

    private static IndexBuilder<TEntity> ApplyDynamicFilter<TEntity>(
        this IndexBuilder<TEntity> indexBuilder,
        bool withSoftDelete,
        bool withOutdated)
        where TEntity : class
    {
        if (!withSoftDelete)
        {
            return indexBuilder;
        }

        if (withSoftDelete && withOutdated)
        {
            return indexBuilder.HasFilterWithNotDeletedAndNotOutdatedInternal();
        }

        return indexBuilder.HasFilterWithNotDeletedInternal();
    }

    private static IndexBuilder<T> HasFilterWithNotDeletedInternal<T>(this IndexBuilder<T> builder, string? filter = null)
        where T : class
    {
        if (!typeof(T).IsAssignableTo(typeof(ISoftDeletableEntity)))
        {
            return builder.HasFilter(filter);
        }

        string filterString = string.IsNullOrWhiteSpace(filter)
            ? NOT_DELETED_FILTER
            : $"{NOT_DELETED_FILTER} and ({filter})";

        return builder.HasFilter(filterString);
    }

    private static IndexBuilder<T> HasFilterWithNotDeletedAndNotOutdatedInternal<T>(this IndexBuilder<T> builder, string? filter = null)
        where T : class
    {
        if (!typeof(T).IsAssignableTo(typeof(ISoftDeletableEntity)) || !typeof(T).IsAssignableTo(typeof(ICanOutdated)))
        {
            return builder.HasFilter(filter);
        }

        string filterString = string.IsNullOrWhiteSpace(filter)
            ? NOT_DELETED_AND_NOT_OUTDATED_FILTER
            : $"{NOT_DELETED_AND_NOT_OUTDATED_FILTER} and ({filter})";

        return builder.HasFilter(filterString);
    }
}
