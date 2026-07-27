using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Dtoriki.Data.Core.Entities;

namespace Dtoriki.Data.Core.FluentConfigurations;

/// <summary>
/// Расширенная конфигурация Entity Framework Core для сущностей, основанных на <see cref="EntityBase{TKey}"/>.
/// Наследуется из <see cref="EntityBaseConfiguration{TEntity, TId}"/> и дополняет базовую конфигурацию
/// специфичной настройкой режима доступа для аудит-свойств (UTC меток создания и последнего обновления).
/// </summary>
/// <typeparam name="TEntity">Тип конфигурируемой сущности, должен наследоваться из <see cref="EntityBase{TKey}"/>.</typeparam>
/// <typeparam name="TId">Тип первичного ключа сущности, должен реализовывать <see cref="IEquatable{T}"/>.</typeparam>
/// <exception cref="ArgumentNullException"/>
/// <exception cref="InvalidOperationException"/>
public abstract class EntityBaseImplConfiguration<TEntity, TId> : EntityBaseConfiguration<TEntity, TId>
    where TEntity : EntityBase<TId>
    where TId : IEquatable<TId>
{
    /// <summary>
    /// Инициализирует конфигурацию для типа <typeparamref name="TEntity"/>.
    /// </summary>
    protected EntityBaseImplConfiguration() : base()
    {
    }

    /// <summary>
    /// Применяет полную конфигурацию к типу <typeparamref name="TEntity"/>: выполняет базовую конфигурацию
    /// и дополняет её специфичной настройкой режима доступа к инфраструктурным полям временных меток.
    /// </summary>
    /// <param name="builder">Построитель типа сущности Entity Framework Core.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="builder"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если фактический CLR тип не совпадает с <typeparamref name="TEntity"/>.</exception>
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        base.Configure(builder);

        builder
            .Property(e => e.CreatedAtUtc)
            .HasField(nameof(EntityBase<TId>._createdAtUtcInfrastructure))
            .UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);

        builder
            .Property(e => e.LastUpdatedAtUtc)
            .HasField(nameof(EntityBase<TId>._lastUpdatedAtInfrastructure))
            .UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
    }
}
