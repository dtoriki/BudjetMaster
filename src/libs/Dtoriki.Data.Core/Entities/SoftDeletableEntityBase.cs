using Dtoriki.Data.Core.Extensions;

namespace Dtoriki.Data.Core.Entities;

/// <summary>
/// Базовая сущность базы данных с возможностью мягкого (логического) удаления (тип идентификатора по умолчанию <see cref="long"/>).
/// </summary>
/// <remarks>
/// Обеспечивает инфраструктурные временные метки и признак удаления <see cref="SoftDeletableEntityBase{TKey}.IsDeleted"/>.
/// Переключение признака удаления выполняется идемпотентными lock-free операциями (CAS над внутренним состоянием удаления).
/// </remarks>
/// <exception cref="InvalidOperationException"/>
public abstract class SoftDeletableEntityBase : SoftDeletableEntityBase<long>
{
    /// <summary>
    /// Создаёт экземпляр класса <see cref="SoftDeletableEntityBase"/>.
    /// </summary>
    protected SoftDeletableEntityBase()
    {
    }
}

/// <summary>
/// Базовая сущность базы данных с возможностью мягкого (логического) удаления.
/// </summary>
/// <typeparam name="TKey">Тип уникального идентификатора.</typeparam>
/// <remarks>
/// Инварианты мягкого удаления:
/// <list type="bullet">
/// <item><description>Если <see cref="IsDeleted"/> == <see langword="true"/>, то <see cref="DeletedAtUtc"/> имеет значение (UTC), а <see cref="RecoveredAtUtc"/> == <see langword="null"/>.</description></item>
/// <item><description>Если <see cref="IsDeleted"/> == <see langword="false"/> и объект ранее был удалён, то <see cref="RecoveredAtUtc"/> имеет значение, а <see cref="DeletedAtUtc"/> == <see langword="null"/>.</description></item>
/// <item><description><see cref="DeletedAtUtc"/> не может быть установлено, когда <see cref="IsDeleted"/> == <see langword="false"/>.</description></item>
/// <item><description><see cref="DeletedAtUtc"/> не может быть <see langword="null"/>, когда <see cref="IsDeleted"/> == <see langword="true"/>.</description></item>
/// <item><description><see cref="DeletedAtUtc"/> ≥ <see cref="EntityBase{TKey}.CreatedAtUtc"/> и ≤ <see cref="RecoveredAtUtc"/> (если последняя установлена).</description></item>
/// <item><description><see cref="RecoveredAtUtc"/> ≥ <see cref="DeletedAtUtc"/> (если последняя установлена).</description></item>
/// </list>
/// Потокобезопасность и отсутствие гонок:
/// <list type="bullet">
/// <item><description>Внутреннее состояние удаления характеризуется тремя состояниями (0=активно,1=удалено,2=переход).</description></item>
/// <item><description>Переходы выполняются строго по цепочке 0→2→1 (удаление) и 1→2→0 (восстановление); промежуточное значение 2 используется только как маркер захвата операции.</description></item>
/// <item><description>Чтение признака удаления через <see cref="IsDeleted"/> использует <see cref="Volatile.Read(ref readonly int)"/> для гарантии актуальной видимости результата кас.</description></item>
/// <item><description>Сеттер <see cref="IsDeleted"/> делает предварительное volatile-чтение и вызывает lock-free операции <see cref="SoftDelete"/> или <see cref="Recover"/>, которые применяют <see cref="Interlocked.CompareExchange(ref int, int, int)"/> к состоянию.</description></item>
/// <item><description>ABA-проблема исключена: значение 2 никогда не становится стабильным «конечным» состоянием и не повторяется как валидная финальная метка; циклы возврата к прежнему значению (0 или 1) всегда проходят через уникальный промежуточный маркер 2.</description></item>
/// <item><description>Идемпотентность: повторные конкурентные вызовы после фиксации конечного состояния (0 или 1) завершаются быстрым ранним возвратом без изменения временных меток.</description></item>
/// </list>
/// </remarks>
/// <exception cref="InvalidOperationException"/>
public abstract class SoftDeletableEntityBase<TKey> : EntityBase<TKey>, ISoftDeletableEntity<TKey>
    where TKey : IEquatable<TKey>
{
    internal int _deletedState; // 0 = активен, 1 = удалён, 2 = переход

    /// <summary>
    /// Рабочая метка времени (UTC) мягкого удаления. <see langword="null"/> если сущность активна или восстановлена.
    /// </summary>
    /// <remarks>
    /// Инварианты: установлено только когда <see cref="IsDeleted"/> == <see langword="true"/>; значение в формате UTC; не раньше <see cref="EntityBase{TKey}.CreatedAtUtc"/>;
    /// не позже <see cref="RecoveredAtUtc"/> (если последняя установлена).
    /// </remarks>
    protected internal DateTime? _deletedAtUtc;

    /// <summary>
    /// Рабочая метка времени восстановления после мягкого удаления (UTC либо <see cref="DateTimeKind.Unspecified"/>). <see langword="null"/>, если ранее не было удаления.
    /// </summary>
    /// <remarks>
    /// Инварианты: при установке сущность активна (<see cref="IsDeleted"/> == <see langword="false"/>); значение не раньше прежнего <see cref="DeletedAtUtc"/>.
    /// </remarks>
    protected internal DateTime? _recoveredAtUtc;

    /// <summary>
    /// Лениво переносимый инфраструктурный признак мягкого удаления (<see langword="true"/> / <see langword="false"/>), поступающий из ORM.
    /// </summary>
    /// <remarks>
    /// Используется только до первого вызова <see cref="FlushInfrastructure"/>, после чего значение интегрируется
    /// в рабочие поля (<see cref="_deletedState"/>, <see cref="_deletedAtUtc"/>, <see cref="_recoveredAtUtc"/>). Может быть <see langword="null"/>,
    /// если инфраструктура не предоставила признак (новый объект в домене).
    /// </remarks>
    protected internal bool? _isDeletedInfrastructure;

    /// <summary>
    /// Возвращает или задаёт признак того, что сущность удалена.
    /// При установке <see langword="true"/> выполняется soft-delete (устанавливаются <see cref="DeletedAtUtc"/>, очищается <see cref="RecoveredAtUtc"/>).
    /// При установке <see langword="false"/> выполняется восстановление (устанавливается <see cref="RecoveredAtUtc"/>, очищается <see cref="DeletedAtUtc"/>).
    /// Повторные установки того же значения идемпотентны и не меняют временные метки.
    /// </summary>
    public bool IsDeleted
    {
        get
        {
            FlushInfrastructure();

            return Volatile.Read(ref _deletedState) == 1;
        }

        set
        {
            FlushInfrastructure();

            bool currentlyDeleted = Volatile.Read(ref _deletedState) == 1;
            if (value == currentlyDeleted)
            {
                return;
            }

            if (value)
            {
                SoftDelete();

                return;
            }

            Recover();
        }
    }

    /// <summary>
    /// Возвращает или задаёт дату (UTC) удаления сущности.
    /// </summary>
    /// <exception cref="InvalidOperationException">Попытка установить при несоответствующем состоянии или нарушении инвариантов порядка дат.</exception>
    public DateTime? DeletedAtUtc
    {
        get => _deletedAtUtc;
        set
        {
            FlushInfrastructure();

            if (value is null && IsDeleted)
            {
                throw new InvalidOperationException($"{nameof(DeletedAtUtc)} не может быть null, если {nameof(IsDeleted)} установлено в true.");
            }

            if (value is not null && !IsDeleted)
            {
                throw new InvalidOperationException($"{nameof(DeletedAtUtc)} не может иметь значение, если {nameof(IsDeleted)} установлено в false.");
            }

            if (value.HasValue)
            {
                if (value.Value.Kind != DateTimeKind.Utc)
                {
                    throw new InvalidOperationException($"{nameof(DeletedAtUtc)} должно быть в формате UTC.");
                }
                if (CreatedAtUtc != DateTime.MinValue && value.Value < CreatedAtUtc)
                {
                    throw new InvalidOperationException($"{nameof(DeletedAtUtc)} не может быть раньше {nameof(CreatedAtUtc)}.");
                }

                if (_recoveredAtUtc.HasValue && value.Value > _recoveredAtUtc.Value)
                {
                    throw new InvalidOperationException($"{nameof(DeletedAtUtc)} не может быть позже {nameof(RecoveredAtUtc)}.");
                }
            }

            _deletedAtUtc = value;
        }
    }

    /// <summary>
    /// Возвращает или задаёт дату восстановления сущности в формате UTC.
    /// </summary>
    /// <exception cref="InvalidOperationException">Если значение раньше <see cref="DeletedAtUtc"/> или не в формате <see cref="DateTimeKind.Utc"/>.</exception>
    public DateTime? RecoveredAtUtc
    {
        get => _recoveredAtUtc;
        set
        {
            FlushInfrastructure();

            if (value.HasValue)
            {
                if (_deletedAtUtc.HasValue && value.Value < _deletedAtUtc.Value)
                {
                    throw new InvalidOperationException($"{nameof(RecoveredAtUtc)} не может быть раньше {nameof(DeletedAtUtc)}.");
                }

                if (value.Value.Kind != DateTimeKind.Utc)
                {
                    throw new InvalidOperationException($"{nameof(DeletedAtUtc)} должно быть в формате UTC.");
                }
            }

            _recoveredAtUtc = value;
        }
    }

    /// <summary>
    /// Создаёт экземпляр класса <see cref="SoftDeletableEntityBase{TKey}"/>.
    /// </summary>
    protected SoftDeletableEntityBase() : base()
    {
    }

    /// <summary>
    /// Потокобезопасно и идемпотентно помечает сущность как мягко удалённую.
    /// Устанавливает <see cref="IsDeleted"/> = <see langword="true"/>, фиксирует <see cref="DeletedAtUtc"/> (UTC now), очищает <see cref="RecoveredAtUtc"/>.
    /// Повторный вызов для уже удалённой сущности не изменяет состояние и не обновляет временные метки.
    /// </summary>
    /// <remarks>Обновляет метку последнего изменения через <c>Touch()</c>.</remarks>
    /// <exception cref="InvalidOperationException">Нарушение внутренних инвариантов дат (неожиданно).</exception>
    protected virtual void SoftDelete()
    {
        FlushInfrastructure();

        _ = this.SoftDeleteSafe(ref _deletedState, ref _deletedAtUtc, ref _recoveredAtUtc, Touch);
    }

    /// <summary>
    /// Потокобезопасно и идемпотентно восстанавливает мягко удалённую сущность.
    /// Устанавливает <see cref="IsDeleted"/> = <see langword="false"/>, фиксирует <see cref="RecoveredAtUtc"/> (UTC now), очищает <see cref="DeletedAtUtc"/>.
    /// Повторный вызов для уже активной сущности не изменяет состояние.
    /// </summary>
    /// <remarks>Обновляет метку последнего изменения через <c>Touch()</c>.</remarks>
    /// <exception cref="InvalidOperationException">Нарушение внутренних инвариантов дат (неожиданно).</exception>
    protected virtual void Recover()
    {
        FlushInfrastructure();

        _ = this.RecoverSafe(ref _deletedState, ref _deletedAtUtc, ref _recoveredAtUtc, Touch);
    }

    /// <summary>
    /// Выполняет ленивый перенос инфраструктурных временных меток (Created / LastUpdated) из EF в рабочие поля
    /// и выполняет дополнительную пост-валидацию инвариантов мягкого удаления.
    /// </summary>
    /// <exception cref="InvalidOperationException">Нарушение инвариантов: <c>DeletedAtUtc &lt; CreatedAtUtc</c> либо <c>RecoveredAtUtc &lt; DeletedAtUtc</c>.</exception>
    protected override void FlushInfrastructure()
    {
        base.FlushInfrastructure();
        this.FlushInfrastructureIsDeleted(ref _isDeletedInfrastructure, ref _deletedState);

        if (_deletedAtUtc.HasValue && _createdAtUtc != default && _deletedAtUtc.Value < _createdAtUtc)
        {
            throw new InvalidOperationException("Нарушен инвариант мягкого удаления: DeletedAtUtc раньше CreatedAtUtc после переноса инфраструктурных временных меток.");
        }

        if (_recoveredAtUtc.HasValue && _deletedAtUtc.HasValue && _recoveredAtUtc.Value < _deletedAtUtc.Value)
        {
            throw new InvalidOperationException("Нарушен инвариант мягкого удаления: RecoveredAtUtc раньше DeletedAtUtc после переноса инфраструктурных временных меток.");
        }
    }

    void ISoftDeletableEntity.SoftDelete() => SoftDelete();
    void ISoftDeletableEntity.Recover() => Recover();
}
