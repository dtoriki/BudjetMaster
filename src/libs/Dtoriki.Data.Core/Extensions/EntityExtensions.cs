using Microsoft.EntityFrameworkCore;
using Dtoriki.Data.Core.Entities;

namespace Dtoriki.Data.Core.Extensions;

/// <summary>
/// Содержит методы расширения сущностей для установки временных меток,
/// мягкого удаления, восстановления и физического удаления записей.
/// </summary>
/// <exception cref="ArgumentNullException"/>
/// <exception cref="InvalidOperationException"/>
public static class EntityExtensions
{
    /// <summary>
    /// Потокобезопасно переносит лениво инициализированные инфраструктурные метки времени создания и последнего обновления
    /// в основные поля сущности. Идемпотентно: повторный вызов после успешного переноса завершается мгновенно.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <typeparam name="TKey">Тип идентификатора сущности.</typeparam>
    /// <param name="entity">Сущность, для которой выполняется перенос меток.</param>
    /// <returns>Ту же сущность для fluent-цепочек.</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="entity"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если превышен лимит ожидания конкурентной инициализации инфраструктурных меток.</exception>
    public static TEntity FlushInfrastructureTimestamps<TEntity, TKey>(this TEntity entity)
        where TEntity : EntityBase<TKey>
        where TKey : IEquatable<TKey>
    {
        return entity.FlushInfrastructureTimestampsSafe(
            ref entity._infrastructureFlushStarted,
            ref entity._createdAtUtcInfrastructure,
            ref entity._createdAtUtc,
            ref entity._lastUpdatedAtInfrastructure,
            ref entity._lastUpdatedAtTicks,
            ref entity._infrastructureTimestampsFlushed);
    }

    /// <summary>
    /// Выполняет мягкое (логическое) удаление сущности, устанавливая метку времени удаления и сбрасывая метку восстановления.
    /// Повторный вызов для уже удалённой сущности не изменяет состояние.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <typeparam name="TKey">Тип идентификатора сущности.</typeparam>
    /// <param name="entity">Сущность для мягкого удаления.</param>
    /// <returns>Ту же сущность после применения операции.</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="entity"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Нарушены пост-инварианты мягкого удаления.</exception>
    public static TEntity SoftDelete<TEntity, TKey>(this TEntity entity)
        where TEntity : SoftDeletableEntityBase<TKey>
        where TKey : IEquatable<TKey>
    {
        entity.FlushInfrastructureTimestamps<TEntity, TKey>();

        return entity.SoftDeleteSafe(
            ref entity._deletedState,
            ref entity._deletedAtUtc,
            ref entity._recoveredAtUtc,
            postDeletedAction: () => entity.TouchSafe(ref entity._lastUpdatedAtTicks, entity.CreatedAtUtc));
    }

    /// <summary>
    /// Восстанавливает ранее мягко удалённую сущность, очищая метку удаления и устанавливая метку восстановления.
    /// Повторный вызов для активной сущности не изменяет состояние.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <typeparam name="TKey">Тип идентификатора сущности.</typeparam>
    /// <param name="entity">Сущность для восстановления.</param>
    /// <returns>Ту же сущность после восстановления.</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="entity"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Нарушены пост-инварианты восстановления.</exception>
    public static TEntity Recover<TEntity, TKey>(this TEntity entity)
        where TEntity : SoftDeletableEntityBase<TKey>
        where TKey : IEquatable<TKey>
    {
        entity.FlushInfrastructureTimestamps<TEntity, TKey>();

        return entity.RecoverSafe(
            ref entity._deletedState,
            ref entity._deletedAtUtc,
            ref entity._recoveredAtUtc,
            postDeletedAction: () => entity.TouchSafe(ref entity._lastUpdatedAtTicks, entity.CreatedAtUtc));
    }

    /// <summary>
    /// Обновляет метку времени последнего обновления сущности на текущее значение (UTC), соблюдая монотонность.
    /// Метка не будет понижена, если текущее значение меньше или равно уже сохранённому.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <typeparam name="TKey">Тип идентификатора сущности.</typeparam>
    /// <param name="entity">Сущность для обновления метки.</param>
    /// <returns>Ту же сущность после обновления метки.</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="entity"/> равен <see langword="null"/>.</exception>
    public static TEntity Touch<TEntity, TKey>(this TEntity entity)
        where TEntity : EntityBase<TKey>
        where TKey : IEquatable<TKey>
    {
        return entity.TouchSafe(ref entity._lastUpdatedAtTicks, entity.CreatedAtUtc);
    }

    /// <summary>
    /// Выполняет мягкое удаление для каждой сущности в коллекции.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <typeparam name="TKey">Тип идентификатора сущности.</typeparam>
    /// <param name="entities">Коллекция сущностей для мягкого удаления.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="entities"/> равна <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если обнаружен <see langword="null"/> элемент в коллекции или нарушены инварианты удаления.</exception>
    public static void SoftDelete<TEntity, TKey>(this IEnumerable<TEntity> entities)
        where TEntity : SoftDeletableEntityBase<TKey>
        where TKey : IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (TEntity entity in entities)
        {
            if (entity is null)
            {
                throw new InvalidOperationException("Обнаружен null-элемент при мягком удалении коллекции сущностей.");
            }

            entity.SoftDelete<TEntity, TKey>();
        }
    }

    /// <summary>
    /// Навсегда удаляет коллекцию сущностей по их идентификаторам, извлечённым из объектов. Это физическое удаление записей из базы данных.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <typeparam name="TKey">Тип идентификатора.</typeparam>
    /// <param name="set">Набор сущностей.</param>
    /// <param name="entities">Удаляемые сущности.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="set"/> или <paramref name="entities"/> равны <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если обнаружен элемент <see langword="null"/> или сущность с идентификатором по умолчанию.</exception>
    public static async Task HardRemoveRangeAsync<TEntity, TKey>(
        this DbSet<TEntity> set,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class, ISoftDeletableEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(entities);

        TEntity[] materialized = entities as TEntity[] ?? entities.ToArray();
        if (materialized.Length == 0)
        {
            return;
        }

        if (materialized.Any(e => e is null))
        {
            throw new InvalidOperationException("Коллекция содержит null-элемент, физическое удаление невозможно.");
        }

        if (materialized.Any(e => EqualityComparer<TKey>.Default.Equals(e.Id, default!)))
        {
            throw new InvalidOperationException("Обнаружена сущность с идентификатором по умолчанию при физическом удалении.");
        }

        TKey[] ids = materialized
            .Select(e => e.Id)
            .Distinct()
            .ToArray();

        await set.HardRemoveRangeAsync(ids, cancellationToken);
    }

    /// <summary>
    /// Навсегда удаляет коллекцию сущностей по их идентификаторам. Это физическое удаление записей из базы данных.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <typeparam name="TKey">Тип идентификатора.</typeparam>
    /// <param name="set">Набор сущностей.</param>
    /// <param name="ids">Идентификаторы удаляемых сущностей.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="set"/> или <paramref name="ids"/> равны <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если коллекция содержит идентификатор по умолчанию.</exception>
    public static async Task HardRemoveRangeAsync<TEntity, TKey>(
        this DbSet<TEntity> set,
        IEnumerable<TKey> ids,
        CancellationToken cancellationToken = default)
        where TEntity : class, ISoftDeletableEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(ids);

        TKey[] materialized = ids as TKey[] ?? ids.ToArray();
        if (materialized.Length == 0)
        {
            return;
        }

        if (materialized.Any(id => EqualityComparer<TKey>.Default.Equals(id, default!)))
        {
            throw new InvalidOperationException("Коллекция идентификаторов содержит значение по умолчанию.");
        }

        await set
            .Where(entity => materialized.Contains(entity.Id))
            .ExecuteDeleteAsync(cancellationToken);

        foreach (TKey id in materialized)
        {
            set.RemoveFromLocal(id);
        }
    }

    /// <summary>
    /// Навсегда удаляет сущность из набора по экземпляру. Это физическое удаление записи из базы данных.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <typeparam name="TKey">Тип идентификатора.</typeparam>
    /// <param name="set">Набор сущностей.</param>
    /// <param name="entity">Удаляемая сущность.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="set"/> или <paramref name="entity"/> равны <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если идентификатор сущности равен значению по умолчанию.</exception>
    public static async Task HardRemoveAsync<TEntity, TKey>(
        this DbSet<TEntity> set,
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : class, ISoftDeletableEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(entity);

        if (EqualityComparer<TKey>.Default.Equals(entity.Id, default!))
        {
            throw new InvalidOperationException("Невозможно физически удалить сущность с идентификатором по умолчанию.");
        }

        await set.HardRemoveAsync(entity.Id, cancellationToken);
    }

    /// <summary>
    /// Навсегда удаляет сущность из набора по идентификатору. Это физическое удаление записи из базы данных.
    /// </summary>
    /// <typeparam name="TEntity">Тип сущности.</typeparam>
    /// <typeparam name="TKey">Тип идентификатора.</typeparam>
    /// <param name="set">Набор сущностей.</param>
    /// <param name="id">Идентификатор удаляемой сущности.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="set"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Если <paramref name="id"/> равен значению по умолчанию.</exception>
    public static async Task HardRemoveAsync<TEntity, TKey>(
        this DbSet<TEntity> set,
        TKey id,
        CancellationToken cancellationToken = default)
        where TEntity : class, ISoftDeletableEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(set);

        if (EqualityComparer<TKey>.Default.Equals(id, default!))
        {
            throw new InvalidOperationException("Невозможно физически удалить сущность с идентификатором по умолчанию.");
        }

        bool deleted = await set.HardRemoveInternalAsync(id, cancellationToken) > 0;

        if (!deleted)
        {
            return;
        }

        set.RemoveFromLocal(id);
    }

    internal static TEntity FlushInfrastructureIsDeleted<TEntity>(
        this TEntity entity,
        ref bool? isDeletedInfrastructure,
        ref int deletedState)
        where TEntity : class, ISoftDeletableEntity
    {
        if (!isDeletedInfrastructure.HasValue)
        {
            return entity;
        }

        int targetState = isDeletedInfrastructure.Value ? 1 : 0;
        int currentState = Volatile.Read(ref deletedState);
        if (currentState != targetState)
        {
            Interlocked.Exchange(ref deletedState, targetState);
        }
        isDeletedInfrastructure = null;

        return entity;
    }

    internal static TEntity FlushInfrastructureTimestampsSafe<TEntity>(
        this TEntity entity,
        ref int infrastructureFlushStarted,
        ref DateTime? createdAtUtcInfrastructure,
        ref DateTime createdAtUtc,
        ref DateTime? lastUpdatedAtInfrastructure,
        ref long lastUpdatedAtTicks,
        ref bool infrastructureTimestampsFlushed,
        Action<int>? spinCountObserver = null)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (Volatile.Read(ref infrastructureTimestampsFlushed))
        {
            return entity;
        }

        if (Interlocked.CompareExchange(ref infrastructureFlushStarted, 1, 0) != 0)
        {
            if (Volatile.Read(ref infrastructureTimestampsFlushed))
            {
                return entity;
            }

            SpinWait sw = new();
            int spinCount = 0;
            int spinLimit = 100_000;
            while (!Volatile.Read(ref infrastructureTimestampsFlushed))
            {
                if ((spinCount & 63) == 63)
                {
                    sw.SpinOnce();
                }
                spinCount++;

                if (spinCount >= spinLimit)
                {
                    throw new InvalidOperationException("Превышено максимальное количество спинов при ожидании инициализации временных меток сущности.");
                }
            }

            spinCountObserver?.Invoke(spinCount);

            return entity;
        }

        bool infraCreatedPresent = createdAtUtcInfrastructure != null;
        bool infraUpdatedPresent = lastUpdatedAtInfrastructure != null;

        if (infraCreatedPresent)
        {
            createdAtUtc = createdAtUtcInfrastructure!.Value;
            createdAtUtcInfrastructure = null;
        }

        if (infraUpdatedPresent)
        {
            Interlocked.Exchange(ref lastUpdatedAtTicks, lastUpdatedAtInfrastructure!.Value.Ticks);
            lastUpdatedAtInfrastructure = null;
        }

        Volatile.Write(ref infrastructureTimestampsFlushed, true);

        return entity;
    }

    internal static TEntity SoftDeleteSafe<TEntity>(
        this TEntity entity,
        ref int deletedState,
        ref DateTime? deletedAtUtc,
        ref DateTime? recoveredAtUtc,
        Action? postDeletedAction = null)
        where TEntity : class, ISoftDeletableEntity
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (Volatile.Read(ref deletedState) == 1)
        {
            return entity;
        }

        if (Interlocked.CompareExchange(ref deletedState, 2, 0) != 0)
        {
            return entity;
        }

        DateTime now = DateTime.UtcNow;
        deletedAtUtc = now;
        recoveredAtUtc = null;

        if (!deletedAtUtc.HasValue || recoveredAtUtc.HasValue)
        {
            throw new InvalidOperationException("Нарушен инвариант (переход SoftDelete): ожидается deletedAtUtc.HasValue и recoveredAtUtc == null.");
        }

        Volatile.Write(ref deletedState, 1);
        postDeletedAction?.Invoke();

        return entity;
    }

    internal static TEntity RecoverSafe<TEntity>(
        this TEntity entity,
        ref int deletedState,
        ref DateTime? deletedAtUtc,
        ref DateTime? recoveredAtUtc,
        Action? postDeletedAction = null)
        where TEntity : class, ISoftDeletableEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (Volatile.Read(ref deletedState) == 0)
        {
            return entity;
        }

        if (Interlocked.CompareExchange(ref deletedState, 2, 1) != 1)
        {
            return entity;
        }

        DateTime now = DateTime.UtcNow;
        recoveredAtUtc = now;
        deletedAtUtc = null;

        if (deletedAtUtc.HasValue || !recoveredAtUtc.HasValue)
        {
            throw new InvalidOperationException("Нарушен инвариант (переход Recover): ожидается deletedAtUtc == null и recoveredAtUtc.HasValue.");
        }

        Volatile.Write(ref deletedState, 0);
        postDeletedAction?.Invoke();

        return entity;
    }

    internal static TEntity SetCreatedAtUtcSafe<TEntity>(this TEntity entity, ref DateTime field, ref long lastUpdatedTicks, DateTime value)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"Значение {nameof(value)} должно быть в UTC.", nameof(value));
        }

        long currentLastUpdated = Interlocked.Read(ref lastUpdatedTicks);
        if (currentLastUpdated != 0 && value.Ticks > currentLastUpdated)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(value)} не может быть позже {nameof(lastUpdatedTicks)}.");
        }

        field = value;

        return entity;
    }

    internal static TEntity TouchSafe<TEntity>(this TEntity entity, ref long lastUpdatedAtTicks, DateTime createdAtUtc)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(entity);

        DateTime now = DateTime.UtcNow;

        return entity.SetLastUpdatedAtUtcSafe(ref lastUpdatedAtTicks, createdAtUtc, now);
    }

    internal static TEntity SetLastUpdatedAtUtcSafe<TEntity>(this TEntity entity, ref long lastUpdatedAtTicks, DateTime createdAtUtc, DateTime value)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"Значение {nameof(value)} должно быть в UTC.", nameof(value));
        }

        if (createdAtUtc != default && value < createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(value)} не может быть раньше {nameof(createdAtUtc)}.");
        }

        long newTicks = value.Ticks;
        int attempts = 0;
        int attemptLimit = 1_000_000;
        SpinWait sw = new();
        while (true)
        {
            long current = Interlocked.Read(ref lastUpdatedAtTicks);
            if (newTicks <= current)
            {
                return entity;
            }
            if (Interlocked.CompareExchange(ref lastUpdatedAtTicks, newTicks, current) == current)
            {
                return entity;
            }

            attempts++;
            if ((attempts & 63) == 63)
            {
                sw.SpinOnce();
            }
            if (attempts >= attemptLimit)
            {
                throw new InvalidOperationException("Превышено максимальное число попыток обновления метки последнего обновления (CAS) из-за высокого контеншена.");
            }
        }
    }

    internal static TEntity SetLastUpdatedAtUtcUnsafe<TEntity>(this TEntity entity, ref long lastUpdatedAtTicks, DateTime createdAtUtc, DateTime value)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"Значение {nameof(value)} должно быть в UTC.", nameof(value));
        }

        if (createdAtUtc != default && value < createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(value)} не может быть раньше {nameof(createdAtUtc)}.");
        }

        lastUpdatedAtTicks = value.Ticks;

        return entity;
    }

    private static void RemoveFromLocal<TEntity, TKey>(
        this DbSet<TEntity> set,
        TKey id)
        where TEntity : class, ISoftDeletableEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(id);

        TEntity? localEntity = set.Local.FirstOrDefault(e => e.Id.Equals(id));
        if (localEntity is null)
        {
            return;
        }

        set.Local.Remove(localEntity);
        set.Entry(localEntity).State = EntityState.Detached;
    }

    private static async Task<int> HardRemoveInternalAsync<TEntity, TKey>(
        this DbSet<TEntity> set,
        TKey id,
        CancellationToken cancellationToken)
        where TEntity : class, ISoftDeletableEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        return await set
            .Where(x => x.Id.Equals(id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
