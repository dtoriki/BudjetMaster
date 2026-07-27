using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Dtoriki.Data.Core.Entities;

namespace Dtoriki.Data.Core.FluentConfigurations;

/// <summary>
/// Расширенная конфигурация Entity Framework Core для мягко удаляемых сущностей, реализующих интерфейс <see cref="ISoftDeletableEntity{TId}"/>.
/// Наследуется из <see cref="EntityBaseConfiguration{TEntity, TId}"/> и дополняет базовую конфигурацию установкой имён колонок
/// для свойств мягкого удаления и автоматическим query фильтром, исключающим удалённые записи из запросов.
/// </summary>
/// <typeparam name="TEntity">Тип сущности, должен реализовать <see cref="ISoftDeletableEntity{TId}"/>.</typeparam>
/// <typeparam name="TId">Тип первичного ключа, должен реализовать <see cref="IEquatable{T}"/>.</typeparam>
/// <exception cref="ArgumentNullException"/>
/// <exception cref="InvalidOperationException"/>
public abstract class SoftDeletableEntityConfiguration<TEntity, TId> : EntityBaseConfiguration<TEntity, TId>
    where TEntity : class, ISoftDeletableEntity<TId>
    where TId : IEquatable<TId>
{
    /// <summary>
    /// Инициализирует новый экземпляр конфигурации мягко удаляемых сущностей для типа <typeparamref name="TEntity"/>.
    /// </summary>
    protected SoftDeletableEntityConfiguration() : base()
    {
    }

    /// <summary>
    /// Применяет полную конфигурацию к типу <typeparamref name="TEntity"/>: выполняет базовую конфигурацию
    /// и дополняет её установкой имён колонок для свойств мягкого удаления, а также применением query фильтра.
    /// </summary>
    /// <param name="builder">Построитель типа сущности Entity Framework Core.</param>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException"/>
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        base.Configure(builder);

        builder
            .Property(e => e.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");
        builder
            .Property(e => e.RecoveredAtUtc)
            .HasColumnName("recovered_at_utc");
        builder
            .Property(e => e.IsDeleted)
            .HasColumnName("is_deleted");

        builder
            .HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>
/// Расширенная конфигурация Entity Framework Core для мягко удаляемых сущностей, реализующих интерфейс <see cref="ISoftDeletableEntity"/>
/// без явного указания типа ключа.
/// </summary>
/// <typeparam name="TEntity">Тип сущности, должен реализовать <see cref="ISoftDeletableEntity"/>.</typeparam>
/// <exception cref="ArgumentNullException"/>
/// <exception cref="InvalidOperationException"/>
public abstract class SoftDeletableEntityConfiguration<TEntity> : EntityBaseConfiguration<TEntity>
    where TEntity : class, ISoftDeletableEntity
{
    /// <summary>
    /// Инициализирует новый экземпляр конфигурации мягко удаляемых сущностей для типа <typeparamref name="TEntity"/>.
    /// </summary>
    protected SoftDeletableEntityConfiguration() : base()
    {
    }

    /// <summary>
    /// Применяет полную конфигурацию к типу <typeparamref name="TEntity"/>: выполняет базовую конфигурацию
    /// и дополняет её установкой имён колонок для свойств мягкого удаления, а также применением query фильтра.
    /// </summary>
    /// <param name="builder">Построитель типа сущности Entity Framework Core.</param>
    /// <exception cref="ArgumentNullException">Если параметр <paramref name="builder"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если фактический CLR тип не совпадает с <typeparamref name="TEntity"/>.</exception>
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        base.Configure(builder);

        builder
            .Property(e => e.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");
        builder
            .Property(e => e.RecoveredAtUtc)
            .HasColumnName("recovered_at_utc");
        builder
            .Property(e => e.IsDeleted)
            .HasColumnName("is_deleted");

        builder
            .HasQueryFilter(e => !e.IsDeleted);
    }
}
