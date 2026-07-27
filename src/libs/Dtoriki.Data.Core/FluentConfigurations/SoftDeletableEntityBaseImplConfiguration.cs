using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Dtoriki.Data.Core.Entities;

namespace Dtoriki.Data.Core.FluentConfigurations;

/// <summary>
/// Расширенная конфигурация Entity Framework Core для мягко удаляемых сущностей, наследующих <see cref="SoftDeletableEntityBase{TId}"/>.
/// Наследуется из <see cref="SoftDeletableEntityConfiguration{TEntity, TId}"/> и дополняет базовую конфигурацию специфичной настройкой
/// режима доступа к инфраструктурным полям свойств мягкого удаления.
/// </summary>
/// <typeparam name="TEntity">Тип конфигурируемой сущности, должен наследоваться из <see cref="SoftDeletableEntityBase{TId}"/>.</typeparam>
/// <typeparam name="TId">Тип первичного ключа сущности, должен реализовывать <see cref="IEquatable{T}"/>.</typeparam>
/// <exception cref="ArgumentNullException"/>
/// <exception cref="InvalidOperationException"/>
public abstract class SoftDeletableEntityBaseImplConfiguration<TEntity, TId> : SoftDeletableEntityConfiguration<TEntity, TId>
    where TEntity : SoftDeletableEntityBase<TId>
    where TId : IEquatable<TId>
{
    /// <summary>
    /// Инициализирует новый экземпляр конфигурации для типа <typeparamref name="TEntity"/>.
    /// </summary>
    protected SoftDeletableEntityBaseImplConfiguration() : base()
    {
    }

    /// <summary>
    /// Применяет полную конфигурацию к типу <typeparamref name="TEntity"/>: выполняет базовую конфигурацию и дополняет её
    /// специфичной настройкой режима доступа к инфраструктурным полям мягкого удаления.
    /// </summary>
    /// <param name="builder">Построитель типа сущности Entity Framework Core.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="builder"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если фактический CLR тип не совпадает с <typeparamref name="TEntity"/>.</exception>
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        base.Configure(builder);

        builder
            .Property(e => e.DeletedAtUtc)
            .HasField(nameof(SoftDeletableEntityBase<>._deletedAtUtc))
            .UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
        builder
            .Property(e => e.RecoveredAtUtc)
            .HasField(nameof(SoftDeletableEntityBase<>._recoveredAtUtc))
            .UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
        builder
            .Property(e => e.IsDeleted)
            .HasField(nameof(SoftDeletableEntityBase<>._isDeletedInfrastructure))
            .UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
    }
}
