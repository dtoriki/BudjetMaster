/*
 * Этот файл сгенерирован с помощью Claude Sonnet 4.6 (claude-sonnet-4-6).
 * Он содержит модульные тесты, написанные с использованием xUnit.
 *
 * В этом файле тестируется класс EntityBase<TKey>.
 *
 * Тесты покрывают следующие аспекты:
 * 1. Инициализация временных меток при создании сущности.
 * 2. Метод SetCreated: валидация UTC, инвариант порядка дат.
 * 3. Метод Touch: монотонное обновление LastUpdatedAtUtc.
 * 4. Сеттер LastUpdatedAtUtc: валидация UTC, инвариант порядка дат.
 * 5. Метод SetLastUpdatedAtUtcUnsafe: допускает понижение метки.
 * 6. Ленивый перенос инфраструктурных меток (FlushInfrastructure): CreatedAtUtc и LastUpdatedAtUtc.
 */

using Dtoriki.Data.Core.Entities;

namespace Dtoriki.Data.Core.Tests;

public partial class EntityBaseTests
{
    private sealed class TestEntity : EntityBase<long>
    {
        public TestEntity() : base()
        {
        }
    }

    /*
     * Этот тест проверяет, что после создания сущности
     * CreatedAtUtc имеет Kind == Utc, а не Kind == Unspecified.
     */
    [Fact]
    public void Constructor_SetsCreatedAtUtcKindToUtc()
    {
        // Arrange & Act
        TestEntity entity = new();

        // Assert
        Assert.Equal(DateTimeKind.Utc, entity.CreatedAtUtc.Kind);
    }

    /*
     * Этот тест проверяет, что после создания сущности
     * LastUpdatedAtUtc имеет Kind == Utc.
     */
    [Fact]
    public void Constructor_SetsLastUpdatedAtUtcKindToUtc()
    {
        // Arrange & Act
        TestEntity entity = new();

        // Assert
        Assert.Equal(DateTimeKind.Utc, entity.LastUpdatedAtUtc.Kind);
    }

    /*
     * Этот тест проверяет, что SetCreated корректно устанавливает
     * CreatedAtUtc при допустимом UTC-значении.
     */
    [Fact]
    public void SetCreated_SetsCreatedAtUtc_WhenValueIsUtc()
    {
        // Arrange
        TestEntity entity = new();
        DateTime created = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        entity.SetCreated(created);

        // Assert
        Assert.Equal(created, entity.CreatedAtUtc);
    }

    /*
     * Этот тест проверяет, что SetCreated выбрасывает ArgumentException,
     * если переданное значение не является UTC.
     */
    [Fact]
    public void SetCreated_ThrowsArgumentException_WhenValueIsNotUtc()
    {
        // Arrange
        TestEntity entity = new();
        DateTime localTime = DateTime.Now;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => entity.SetCreated(localTime));
    }

    /*
     * Этот тест проверяет, что SetCreated выбрасывает ArgumentOutOfRangeException,
     * если переданное значение позже уже установленного LastUpdatedAtUtc.
     * Для воспроизведения устанавливаем _lastUpdatedAtTicks напрямую
     * и помечаем инфраструктурный flush завершённым, чтобы он не сбросил значение.
     */
    [Fact]
    public void SetCreated_ThrowsArgumentOutOfRangeException_WhenValueIsAfterLastUpdated()
    {
        // Arrange
        TestEntity entity = new();
        DateTime lastUpdated = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        entity._lastUpdatedAtTicks = lastUpdated.Ticks;
        entity._infrastructureFlushStarted = 1; // флаш уже выполнен, не сбрасываем поля
        DateTime afterLastUpdated = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => entity.SetCreated(afterLastUpdated));
    }

    /*
     * Этот тест проверяет, что Touch обновляет LastUpdatedAtUtc
     * до значения, не меньшего предыдущего (монотонность).
     */
    [Fact]
    public void Touch_UpdatesLastUpdatedAtUtc_ToCurrentOrLater()
    {
        // Arrange
        TestEntity entity = new();
        DateTime created = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        entity.SetCreated(created);
        DateTime before = DateTime.UtcNow;

        // Act
        entity.Touch();

        // Assert
        Assert.True(entity.LastUpdatedAtUtc >= before);
    }

    /*
     * Этот тест проверяет, что повторный вызов Touch не уменьшает LastUpdatedAtUtc
     * (монотонность).
     */
    [Fact]
    public void Touch_IsMonotone_DoesNotDecreaseLastUpdatedAtUtc()
    {
        // Arrange
        TestEntity entity = new();
        DateTime created = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        entity.SetCreated(created);
        entity.Touch();
        DateTime afterFirstTouch = entity.LastUpdatedAtUtc;

        // Act
        entity.Touch();

        // Assert
        Assert.True(entity.LastUpdatedAtUtc >= afterFirstTouch);
    }

    /*
     * Этот тест проверяет, что сеттер LastUpdatedAtUtc выбрасывает ArgumentException
     * при значении не в формате UTC.
     */
    [Fact]
    public void LastUpdatedAtUtcSetter_ThrowsArgumentException_WhenValueIsNotUtc()
    {
        // Arrange
        TestEntity entity = new();
        DateTime localTime = DateTime.Now;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => entity.LastUpdatedAtUtc = localTime);
    }

    /*
     * Этот тест проверяет, что сеттер LastUpdatedAtUtc выбрасывает ArgumentOutOfRangeException,
     * если значение раньше CreatedAtUtc.
     */
    [Fact]
    public void LastUpdatedAtUtcSetter_ThrowsArgumentOutOfRangeException_WhenValueIsBeforeCreatedAt()
    {
        // Arrange
        TestEntity entity = new();
        DateTime created = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        entity.SetCreated(created);
        DateTime earlier = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => entity.LastUpdatedAtUtc = earlier);
    }

    /*
     * Этот тест проверяет, что SetLastUpdatedAtUtcUnsafe допускает
     * понижение метки (не монотонно), если значение не раньше CreatedAtUtc.
     */
    [Fact]
    public void SetLastUpdatedAtUtcUnsafe_AllowsLowerValue_WhenNotBeforeCreatedAt()
    {
        // Arrange
        TestEntity entity = new();
        DateTime created = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        entity.SetCreated(created);
        DateTime high = new(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        entity.LastUpdatedAtUtc = high;
        DateTime lower = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        entity.SetLastUpdatedAtUtcUnsafe(lower);

        // Assert
        Assert.Equal(lower, entity.LastUpdatedAtUtc);
    }

    /*
     * Этот тест проверяет, что SetLastUpdatedAtUtcUnsafe выбрасывает ArgumentOutOfRangeException,
     * если переданное значение раньше CreatedAtUtc.
     */
    [Fact]
    public void SetLastUpdatedAtUtcUnsafe_ThrowsArgumentOutOfRangeException_WhenValueBeforeCreatedAt()
    {
        // Arrange
        TestEntity entity = new();
        DateTime created = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        entity.SetCreated(created);
        DateTime earlier = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => entity.SetLastUpdatedAtUtcUnsafe(earlier));
    }

    /*
     * Этот тест проверяет, что при первом обращении к CreatedAtUtc выполняется
     * ленивый перенос инфраструктурных меток из _createdAtUtcInfrastructure
     * и _lastUpdatedAtInfrastructure в рабочие поля.
     * Значения устанавливаются до первого обращения к свойству, имитируя загрузку из EF.
     */
    [Fact]
    public void FlushInfrastructure_TransfersCreatedAtUtc_FromInfrastructureField()
    {
        // Arrange
        TestEntity entity = new();
        DateTime infraCreated = new(2024, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        entity._createdAtUtcInfrastructure = infraCreated;

        // Act — первый доступ инициирует ленивый перенос
        DateTime result = entity.CreatedAtUtc;

        // Assert
        Assert.Equal(infraCreated, result);
    }

    /*
     * Этот тест проверяет, что при первом обращении к LastUpdatedAtUtc
     * ленивый перенос корректно копирует _lastUpdatedAtInfrastructure в _lastUpdatedAtTicks.
     */
    [Fact]
    public void FlushInfrastructure_TransfersLastUpdatedAtUtc_FromInfrastructureField()
    {
        // Arrange
        TestEntity entity = new();
        DateTime infraCreated = new(2024, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        DateTime infraUpdated = new(2024, 5, 20, 18, 0, 0, DateTimeKind.Utc);
        entity._createdAtUtcInfrastructure = infraCreated;
        entity._lastUpdatedAtInfrastructure = infraUpdated;

        // Act
        _ = entity.CreatedAtUtc; // инициирует flush
        DateTime result = entity.LastUpdatedAtUtc;

        // Assert
        Assert.Equal(infraUpdated, result);
    }

    /*
     * Этот тест проверяет, что сущность корректно хранит Id.
     */
    [Fact]
    public void Id_CanBeSetAndRead()
    {
        // Arrange
        TestEntity entity = new();

        // Act
        entity.Id = 42L;

        // Assert
        Assert.Equal(42L, entity.Id);
    }
}
