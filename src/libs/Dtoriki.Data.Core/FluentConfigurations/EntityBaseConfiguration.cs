using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Dtoriki.Data.Core.Entities;
using Dtoriki.Data.Core.Extensions;

namespace Dtoriki.Data.Core.FluentConfigurations;

/// <summary>
/// Представляет базовую конфигурацию Entity Framework Core для сущностей, реализующих интерфейс <see cref="IEntity"/>.
/// Реализует Template Method паттерн через виртуальные методы <see cref="ValidateEntityModel(EntityTypeBuilder{TEntity})"/> и <see cref="ConfigureIndexes(EntityTypeBuilder{TEntity})"/>.
/// </summary>
/// <typeparam name="TEntity">Тип сущности, должен реализовать <see cref="IEntity"/>.</typeparam>
/// <exception cref="ArgumentNullException"/>
/// <exception cref="InvalidOperationException"/>
public abstract class EntityBaseConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Инициализирует новый экземпляр конфигурации для типа <typeparamref name="TEntity"/>.
    /// </summary>
    protected EntityBaseConfiguration()
    {
    }

    /// <summary>
    /// Конфигурирует сущность согласно Template Method паттерну.
    /// </summary>
    /// <param name="builder">Построитель типа сущности.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="builder"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если фактический тип сущности не совпадает с <typeparamref name="TEntity"/>.</exception>
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        EnsureEntityType(builder);
        ValidateEntityModel(builder);
        ConfigureIndexes(builder);

        builder
            .Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc");
        builder
            .Property(e => e.LastUpdatedAtUtc)
            .HasColumnName("last_updated_at_utc");
    }

    /// <summary>
    /// Выполняет дополнительную валидацию модели сущности на основе доменных правил.
    /// Базовая реализация не содержит логики. Переопределите в производных классах для добавления проверок.
    /// </summary>
    /// <param name="builder">Построитель типа сущности.</param>
    protected virtual void ValidateEntityModel(EntityTypeBuilder<TEntity> builder)
    {
    }

    /// <summary>
    /// Конфигурирует индексы сущности. Базовая реализация вызывает <see cref="EntityTypeBuilderExtensions.SetDefaultIndexes{TEntity}(EntityTypeBuilder{TEntity})"/>.
    /// </summary>
    /// <param name="builder">Построитель типа сущности.</param>
    protected virtual void ConfigureIndexes(EntityTypeBuilder<TEntity> builder)
    {
        builder.SetDefaultIndexes();
    }

    private static void EnsureEntityType(EntityTypeBuilder<TEntity> builder)
    {
        if (builder.Metadata.ClrType != typeof(TEntity))
        {
            throw new InvalidOperationException(
                $"Конфигурация для типа {typeof(TEntity).FullName} получила builder с типом {builder.Metadata.ClrType.FullName}.");
        }
    }
}

/// <summary>
/// Представляет типизированную конфигурацию Entity Framework Core для сущностей с явным типом первичного ключа.
/// Расширяет <see cref="EntityBaseConfiguration{TEntity}"/> для поддержки сущностей, реализующих интерфейс <see cref="IEntity{TKey}"/>.
/// </summary>
/// <typeparam name="TEntity">Тип сущности, должен реализовать <see cref="IEntity{TKey}"/>.</typeparam>
/// <typeparam name="TId">Тип первичного ключа, должен реализовать <see cref="IEquatable{T}"/>.</typeparam>
/// <exception cref="ArgumentNullException"/>
/// <exception cref="InvalidOperationException"/>
public abstract class EntityBaseConfiguration<TEntity, TId> : EntityBaseConfiguration<TEntity>
    where TEntity : class, IEntity<TId>
    where TId : IEquatable<TId>
{
    /// <summary>
    /// Инициализирует новый экземпляр конфигурации для типа <typeparamref name="TEntity"/>.
    /// </summary>
    protected EntityBaseConfiguration() : base()
    {
    }

    /// <summary>
    /// Конфигурирует сущность: вызывает базовую конфигурацию и добавляет настройку первичного ключа.
    /// </summary>
    /// <param name="builder">Построитель типа сущности.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="builder"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если фактический тип сущности не совпадает с <typeparamref name="TEntity"/>.</exception>
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.Configure(builder);

        builder
            .Property(e => e.Id)
            .HasColumnName("id");
    }

    /// <summary>
    /// Конфигурирует индексы сущности, используя типизированный метод расширения с явным указанием типа ключа <typeparamref name="TId"/>.
    /// </summary>
    /// <param name="builder">Построитель типа сущности.</param>
    protected override void ConfigureIndexes(EntityTypeBuilder<TEntity> builder)
    {
        builder.SetDefaultIndexes<TEntity, TId>();
    }
}
