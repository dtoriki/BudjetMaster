using Dtoriki.Data.Core.Context;
using Dtoriki.Data.Core.Extensions;

namespace Dtoriki.Data.Core.Entities;

/// <summary>
/// Базовый тип сущности с типом идентификатора <see cref="long"/>, включающий потокобезопасные UTC временные метки.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><see cref="EntityBase{TKey}.CreatedAtUtc"/> нельзя установить позже <see cref="EntityBase{TKey}.LastUpdatedAtUtc"/>.</description></item>
/// <item><description><see cref="EntityBase{TKey}.LastUpdatedAtUtc"/> нельзя установить раньше <see cref="EntityBase{TKey}.CreatedAtUtc"/>.</description></item>
/// <item><description>Оба значения требуют <see cref="DateTimeKind.Utc"/>.</description></item>
/// </list>
/// </remarks>
/// <exception cref="ArgumentException"/>
/// <exception cref="ArgumentOutOfRangeException"/>
public abstract class EntityBase : EntityBase<long>
{
    /// <summary>
    /// Создаёт сущность, инициализируя временные метки значением default с <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    /// <remarks>
    /// Фактическая инициализация меток происходит либо при вызове <see cref="EntityBase{TKey}.SetCreated"/> или <see cref="EntityBase{TKey}.Touch"/>,
    /// либо автоматически при сохранении через контекст базы данных <see cref="EfContextBase"/>, либо при материализации из базы данных <see cref="EfContextBase"/> (ленивый перенос из инфраструктурных полей).
    /// </remarks>
    protected EntityBase()
    {
    }
}

/// <summary>
/// Базовый тип сущности с потокобезопасными UTC временными метками.
/// </summary>
/// <typeparam name="TKey">Тип уникального идентификатора.</typeparam>
/// <remarks>
/// <list type="bullet">
/// <item><description><see cref="EntityBase{TKey}.CreatedAtUtc"/> нельзя установить позже <see cref="EntityBase{TKey}.LastUpdatedAtUtc"/>.</description></item>
/// <item><description><see cref="EntityBase{TKey}.LastUpdatedAtUtc"/> нельзя установить раньше <see cref="EntityBase{TKey}.CreatedAtUtc"/>.</description></item>
/// <item><description>Оба значения требуют <see cref="DateTimeKind.Utc"/>.</description></item>
/// </list>
/// </remarks>
/// <exception cref="ArgumentException"/>
/// <exception cref="ArgumentOutOfRangeException"/>
public abstract class EntityBase<TKey> : IEntity<TKey>
    where TKey : IEquatable<TKey>
{
    internal DateTime _createdAtUtc;
    internal long _lastUpdatedAtTicks;
    internal bool _infrastructureTimestampsFlushed = false;
    internal int _infrastructureFlushStarted; // 0 – не начиналось, 1 – выполняется/завершено

    /// <summary>
    /// Инфраструктурное (лениво переносимое) значение даты создания (UTC) из EF.
    /// </summary>
    protected internal DateTime? _createdAtUtcInfrastructure;

    /// <summary>
    /// Инфраструктурное (лениво переносимое) значение последнего обновления (UTC) из EF.
    /// </summary>
    protected internal DateTime? _lastUpdatedAtInfrastructure;

    /// <summary>
    /// Возвращает или задаёт уникальный идентификатор сущности.
    /// </summary>
    public TKey Id { get; set; } = default!;

    /// <summary>
    /// Возвращает или задаёт дату создания (UTC). Нельзя установить позже текущей метки обновления.
    /// </summary>
    /// <exception cref="ArgumentException">Значение не в формате UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Значение позже уже установленного <see cref="LastUpdatedAtUtc"/>.</exception>
    public DateTime CreatedAtUtc
    {
        get
        {
            FlushInfrastructure();

            return _createdAtUtc;
        }

        set => SetCreated(value);
    }

    /// <summary>
    /// Возвращает или задаёт дату последнего обновления (UTC). Нельзя установить раньше <see cref="CreatedAtUtc"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Значение не в формате UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Значение раньше <see cref="CreatedAtUtc"/>.</exception>
    public DateTime LastUpdatedAtUtc
    {
        get
        {
            FlushInfrastructure();

            return new DateTime(Interlocked.Read(ref _lastUpdatedAtTicks), DateTimeKind.Utc);
        }

        set
        {
            FlushInfrastructure();
            this.SetLastUpdatedAtUtcSafe(ref _lastUpdatedAtTicks, _createdAtUtc, value);
        }
    }

    /// <summary>
    /// Создаёт сущность, инициализируя временные метки значением default с <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    /// <remarks>
    /// Фактическая инициализация меток происходит либо при вызове <see cref="SetCreated"/> или <see cref="Touch"/>,
    /// либо автоматически при сохранении через контекст базы данных <see cref="EfContextBase"/>, либо при материализации из базы данных <see cref="EfContextBase"/> (ленивый перенос из инфраструктурных полей).
    /// </remarks>
    protected EntityBase()
    {
        DateTime defaultDt = DateTime.SpecifyKind(default, DateTimeKind.Utc);
        _createdAtUtc = defaultDt;
        _lastUpdatedAtInfrastructure = defaultDt;
    }

    /// <summary>
    /// Устанавливает дату и время (UTC) создания сущности.
    /// </summary>
    /// <param name="dateTime">Дата и время (UTC), которое будет установлено как время создания.</param>
    /// <exception cref="ArgumentException">Если <paramref name="dateTime"/> не в формате UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Если <paramref name="dateTime"/> позже уже установленного <see cref="LastUpdatedAtUtc"/>.</exception>
    public virtual void SetCreated(DateTime dateTime)
    {
        FlushInfrastructure();
        this.SetCreatedAtUtcSafe(ref _createdAtUtc, ref _lastUpdatedAtTicks, dateTime);
    }

    /// <summary>
    /// Обновляет <see cref="LastUpdatedAtUtc"/> до текущего времени UTC (монотонно, потокобезопасно).
    /// Идемпотентно по отношению к попыткам «понизить» значение — более ранняя метка будет проигнорирована.
    /// </summary>
    /// <exception cref="InvalidOperationException">Если при обновлении метки обнаружено нарушение инвариантов (например, после ленивого переноса инфраструктурных меток).</exception>
    public virtual void Touch()
    {
        FlushInfrastructure();

        this.TouchSafe(ref _lastUpdatedAtTicks, _createdAtUtc);
    }

    /// <summary>
    /// Небезопасно (без гарантии монотонности) устанавливает значение <see cref="LastUpdatedAtUtc"/>.
    /// Допускает понижение временной метки до значения, не меньшего <see cref="CreatedAtUtc"/>.
    /// Использовать только в инфраструктурных сценариях (импорт, миграции, реплей истории),
    /// в прикладном коде предпочтителен потокобезопасный сеттер <see cref="LastUpdatedAtUtc"/> или <see cref="Touch"/>.
    /// </summary>
    /// <param name="value">Новое значение (UTC).</param>
    /// <exception cref="ArgumentException">Если <paramref name="value"/> не в формате UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Если <paramref name="value"/> раньше <see cref="CreatedAtUtc"/>.</exception>
    public void SetLastUpdatedAtUtcUnsafe(DateTime value)
    {
        FlushInfrastructure();

        this.SetLastUpdatedAtUtcUnsafe(ref _lastUpdatedAtTicks, _createdAtUtc, value);
    }

    /// <summary>
    /// Выполняет ленивую инициализацию инфраструктурных (загруженных из EF) временных меток
    /// <see cref="_createdAtUtcInfrastructure"/> и <see cref="_lastUpdatedAtInfrastructure"/>,
    /// перенося их в рабочие поля <see cref="_createdAtUtc"/> и <see cref="_lastUpdatedAtTicks"/> один раз за жизненный цикл экземпляра.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>Потокобезопасна: конкурирующие потоки либо дождутся завершения переноса, либо увидят конечное состояние.</description></item>
    /// <item><description>Идемпотентна: повторные вызовы после успешного переноса выполняются как no-op.</description></item>
    /// <item><description>Вызывается автоматически геттерами <see cref="CreatedAtUtc"/> и <see cref="LastUpdatedAtUtc"/> перед доступом к значениям.</description></item>
    /// <item><description>Не изменяет уже установленную пользовательским кодом монотонность метки обновления.</description></item>
    /// </list>
    /// Явный вызов допустим в сценариях предварительной материализации для исключения первого ленивого переноса в горячем пути.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Если при переносе инфраструктурных меток обнаружено нарушение инвариантов (например, <see cref="CreatedAtUtc"/> позже <see cref="LastUpdatedAtUtc"/>).</exception>
    protected virtual void FlushInfrastructure()
    {
        if (Volatile.Read(ref _infrastructureFlushStarted) == 1)
        {
            return;
        }

        this.FlushInfrastructureTimestampsSafe(
            ref _infrastructureFlushStarted,
            ref _createdAtUtcInfrastructure,
            ref _createdAtUtc,
            ref _lastUpdatedAtInfrastructure,
            ref _lastUpdatedAtTicks,
            ref _infrastructureTimestampsFlushed);
    }
}
