using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Dtoriki.Data.Core.Entities;
using Dtoriki.Data.Core.Extensions;

namespace Dtoriki.Data.Core.Context;

/// <summary>
/// Базовый класс контекста базы данных EntityFramework.
/// </summary>
/// <exception cref="ObjectDisposedException" />
public abstract class EfContextBase : DbContext, IEfContext
{
    /// <summary>
    /// Указывает на то, был ли освобождён текущий контекст базы данных.
    /// </summary>
    protected bool _isDisposed;

    /// <summary>
    /// Создаёт контекст базы данных EntityFramework.
    /// </summary>
    /// <param name="dbContextOptions">Конфигурация контекста базы данных.</param>
    protected EfContextBase(DbContextOptions dbContextOptions) : base(dbContextOptions)
    {
    }

    /// <inheritdoc/>
    public IDbContextTransaction BeginTransaction()
    {
        DisposeCheck();

        return Database.BeginTransaction();
    }

    /// <inheritdoc/>
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        DisposeCheck();

        return await Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc cref="IEfContext.SaveChanges()"/>
    /// <exception cref="InvalidOperationException">Если при установке временных меток обнаружено нарушение инвариантов.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Если значения временных меток находятся вне допустимого диапазона.</exception>
    public override int SaveChanges()
    {
        SaveChangesInternal();

        return base.SaveChanges();
    }

    /// <inheritdoc cref="IEfContext.SaveChanges(bool)"/>
    /// <exception cref="InvalidOperationException">Если при установке временных меток обнаружено нарушение инвариантов.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Если значения временных меток находятся вне допустимого диапазона.</exception>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SaveChangesInternal();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc cref="IEfContext.SaveChangesAsync(bool, CancellationToken)"/>
    /// <exception cref="InvalidOperationException">Если при установке временных меток обнаружено нарушение инвариантов.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Если значения временных меток находятся вне допустимого диапазона.</exception>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SaveChangesInternal();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc cref="IEfContext.SaveChangesAsync(CancellationToken)"/>
    /// <exception cref="InvalidOperationException">Если при установке временных меток обнаружено нарушение инвариантов.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Если значения временных меток находятся вне допустимого диапазона.</exception>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesInternal();

        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Асинхронно сохраняет изменения в базе данных без применения обработки временных меток и soft-delete логики.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции асинхронного сохранения.</param>
    /// <returns>Количество затронутых строк в базе данных.</returns>
    /// <remarks>
    /// Временные метки <see cref="IEntity.CreatedAtUtc"/> и <see cref="IEntity.LastUpdatedAtUtc"/>
    /// не будут автоматически установлены, и логика soft-delete не будет применена.
    /// Используй этот метод только в случаях, когда необходимо сохранить изменения без участия промежуточной обработки,
    /// например при синхронизации данных или импорте, где временные метки должны быть установлены явно.
    /// </remarks>
    /// <exception cref="DbUpdateException">Если при сохранении в базе данных возникла ошибка.</exception>
    /// <exception cref="DbUpdateConcurrencyException">Если обнаружен конфликт параллелизма.</exception>
    public Task<int> SaveChangesSilentAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Освобождает контекст базы данных.
    /// </summary>
    public override void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        base.Dispose();
        GC.SuppressFinalize(this);

        _isDisposed = true;
    }

    /// <summary>
    /// Асинхронно освобождает контекст базы данных.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await base.DisposeAsync();
        GC.SuppressFinalize(this);

        _isDisposed = true;
    }

    /// <summary>
    /// Осуществляет проверку на то, был ли освобождён текущий контекст.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Выбрасывается, когда текущий контекст освобождён.</exception>
    protected void DisposeCheck()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void SaveChangesInternal()
    {
        DisposeCheck();
        SoftDelete();
        SetEntitiesDates();
    }

    private void SetEntitiesDates()
    {
        if (!ChangeTracker.HasChanges())
        {
            return;
        }

        DateTime utcNow = DateTime.UtcNow;
        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            object entryEntity = entry.Entity;
            if (entryEntity is not IEntity entity)
            {
                continue;
            }

            EntityState state = entry.State;

            switch (state)
            {
                case EntityState.Added:
                    if (entity.CreatedAtUtc == default)
                    {
                        entity.SetCreated(utcNow);
                    }
                    if (entity.LastUpdatedAtUtc < entity.CreatedAtUtc)
                    {
                        entity.Touch();
                    }
                    break;

                case EntityState.Modified:
                    entity.Touch();
                    break;
            }
        }
    }

    private void SoftDelete()
    {
        if (!ChangeTracker.HasChanges())
        {
            return;
        }

        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && IsSoftDeletable(entry))
            {
                entry.State = EntityState.Modified;
                ((SoftDeletableEntityBase)entry.Entity).SoftDelete<SoftDeletableEntityBase, long>();

                SetOwnedEntitiesToUnchanged(entry);
            }
        }
    }

    private void SetOwnedEntitiesToUnchanged(EntityEntry principalEntry)
    {
        INavigation[] ownedNavigations = principalEntry.Metadata.GetNavigations()
            .Where(n => n.TargetEntityType.IsOwned())
            .ToArray();

        if (ownedNavigations.Length == 0)
        {
            return;
        }

        foreach (INavigation navigation in ownedNavigations)
        {
            if (navigation.IsCollection)
            {
                CollectionEntry collectionEntry = principalEntry.Collection(navigation.Name);
                if (collectionEntry.CurrentValue is null)
                {
                    continue;
                }

                foreach (object entity in collectionEntry.CurrentValue)
                {
                    EntityEntry ownedEntry = Entry(entity);
                    if (ownedEntry.State == EntityState.Deleted)
                    {
                        ownedEntry.State = EntityState.Unchanged;
                        SetOwnedEntitiesToUnchanged(ownedEntry);
                    }
                }
            }
            else
            {
                EntityEntry? ownedEntry = principalEntry.Reference(navigation.Name).TargetEntry;
                if (ownedEntry is not null && ownedEntry.State == EntityState.Deleted)
                {
                    ownedEntry.State = EntityState.Unchanged;
                    SetOwnedEntitiesToUnchanged(ownedEntry);
                }
            }
        }
    }

    private static bool IsSoftDeletable(EntityEntry entry)
    {
        return entry.Entity is SoftDeletableEntityBase;
    }
}
