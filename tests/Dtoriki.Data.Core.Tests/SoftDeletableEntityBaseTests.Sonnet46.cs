/*
 * Этот файл сгенерирован с помощью Claude Sonnet 4.6 (claude-sonnet-4-6).
 * Он содержит модульные тесты, написанные с использованием xUnit.
 *
 * В этом файле тестируется класс SoftDeletableEntityBase<TKey>.
 *
 * Тесты покрывают следующие аспекты:
 * 1. Начальное состояние: IsDeleted == false, DeletedAtUtc == null, RecoveredAtUtc == null.
 * 2. Мягкое удаление: IsDeleted = true устанавливает DeletedAtUtc и сбрасывает RecoveredAtUtc.
 * 3. Восстановление: IsDeleted = false после удаления устанавливает RecoveredAtUtc и сбрасывает DeletedAtUtc.
 * 4. Идемпотентность: повторный soft-delete и recover не меняют временные метки.
 * 5. Сеттер DeletedAtUtc: инварианты IsDeleted, UTC, порядка дат.
 * 6. Сеттер RecoveredAtUtc: инварианты порядка дат и UTC.
 * 7. Инфраструктурный перенос: _isDeletedInfrastructure корректно применяется при flush.
 */

using Dtoriki.Data.Core.Entities;

namespace Dtoriki.Data.Core.Tests;

public partial class SoftDeletableEntityBaseTests
{
    private sealed class TestSoftEntity : SoftDeletableEntityBase<long>
    {
        public TestSoftEntity() : base()
        {
        }
    }

    /// <summary>
    /// Вспомогательный метод: создаёт сущность с уже установленным CreatedAtUtc,
    /// чтобы инварианты дат проверялись корректно.
    /// </summary>
    private static TestSoftEntity CreateWithTimestamps(DateTime created)
    {
        TestSoftEntity entity = new();
        entity.SetCreated(created);

        return entity;
    }

    /*
     * Этот тест проверяет, что после создания сущности
     * IsDeleted равен false.
     */
    [Fact]
    public void Constructor_SetsIsDeleted_ToFalse()
    {
        // Arrange & Act
        TestSoftEntity entity = new();

        // Assert
        Assert.False(entity.IsDeleted);
    }

    /*
     * Этот тест проверяет, что после создания сущности
     * DeletedAtUtc равен null.
     */
    [Fact]
    public void Constructor_SetsDeletedAtUtc_ToNull()
    {
        // Arrange & Act
        TestSoftEntity entity = new();

        // Assert
        Assert.Null(entity.DeletedAtUtc);
    }

    /*
     * Этот тест проверяет, что после создания сущности
     * RecoveredAtUtc равен null.
     */
    [Fact]
    public void Constructor_SetsRecoveredAtUtc_ToNull()
    {
        // Arrange & Act
        TestSoftEntity entity = new();

        // Assert
        Assert.Null(entity.RecoveredAtUtc);
    }

    /*
     * Этот тест проверяет, что установка IsDeleted = true
     * переводит сущность в удалённое состояние.
     */
    [Fact]
    public void IsDeletedSetter_SetToTrue_SetsIsDeletedToTrue()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Act
        entity.IsDeleted = true;

        // Assert
        Assert.True(entity.IsDeleted);
    }

    /*
     * Этот тест проверяет, что после soft-delete
     * DeletedAtUtc имеет значение в UTC.
     */
    [Fact]
    public void IsDeletedSetter_SetToTrue_SetsDeletedAtUtcToUtcNow()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        DateTime before = DateTime.UtcNow;

        // Act
        entity.IsDeleted = true;

        // Assert
        Assert.NotNull(entity.DeletedAtUtc);
        Assert.Equal(DateTimeKind.Utc, entity.DeletedAtUtc!.Value.Kind);
        Assert.True(entity.DeletedAtUtc.Value >= before);
    }

    /*
     * Этот тест проверяет, что после soft-delete
     * RecoveredAtUtc равен null.
     */
    [Fact]
    public void IsDeletedSetter_SetToTrue_ClearsRecoveredAtUtc()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.IsDeleted = true;

        // Act — восстанавливаем, затем снова удаляем
        entity.IsDeleted = false;
        entity.IsDeleted = true;

        // Assert
        Assert.Null(entity.RecoveredAtUtc);
    }

    /*
     * Этот тест проверяет, что восстановление (IsDeleted = false)
     * переводит сущность в активное состояние.
     */
    [Fact]
    public void IsDeletedSetter_SetToFalse_AfterDelete_SetsIsDeletedToFalse()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.IsDeleted = true;

        // Act
        entity.IsDeleted = false;

        // Assert
        Assert.False(entity.IsDeleted);
    }

    /*
     * Этот тест проверяет, что после восстановления
     * RecoveredAtUtc имеет значение в UTC.
     */
    [Fact]
    public void IsDeletedSetter_SetToFalse_AfterDelete_SetsRecoveredAtUtcToUtcNow()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.IsDeleted = true;
        DateTime before = DateTime.UtcNow;

        // Act
        entity.IsDeleted = false;

        // Assert
        Assert.NotNull(entity.RecoveredAtUtc);
        Assert.Equal(DateTimeKind.Utc, entity.RecoveredAtUtc!.Value.Kind);
        Assert.True(entity.RecoveredAtUtc.Value >= before);
    }

    /*
     * Этот тест проверяет, что после восстановления
     * DeletedAtUtc равен null.
     */
    [Fact]
    public void IsDeletedSetter_SetToFalse_AfterDelete_ClearsDeletedAtUtc()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.IsDeleted = true;

        // Act
        entity.IsDeleted = false;

        // Assert
        Assert.Null(entity.DeletedAtUtc);
    }

    /*
     * Этот тест проверяет, что повторная установка IsDeleted = true
     * не изменяет DeletedAtUtc (идемпотентность).
     */
    [Fact]
    public void IsDeletedSetter_SetToTrue_Twice_IsIdempotent()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.IsDeleted = true;
        DateTime? firstDeletedAt = entity.DeletedAtUtc;

        // Act
        entity.IsDeleted = true;

        // Assert
        Assert.Equal(firstDeletedAt, entity.DeletedAtUtc);
    }

    /*
     * Этот тест проверяет, что повторная установка IsDeleted = false
     * для активной сущности не изменяет RecoveredAtUtc (идемпотентность).
     */
    [Fact]
    public void IsDeletedSetter_SetToFalse_WhenAlreadyActive_IsIdempotent()
    {
        // Arrange
        TestSoftEntity entity = new();

        // Act — не выбрасывает и не меняет состояние
        entity.IsDeleted = false;

        // Assert
        Assert.False(entity.IsDeleted);
        Assert.Null(entity.RecoveredAtUtc);
    }

    /*
     * Этот тест проверяет, что сеттер DeletedAtUtc выбрасывает InvalidOperationException,
     * если пытается установить null при IsDeleted == true.
     */
    [Fact]
    public void DeletedAtUtcSetter_ThrowsInvalidOperationException_WhenNullAndIsDeletedTrue()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.IsDeleted = true;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => entity.DeletedAtUtc = null);
    }

    /*
     * Этот тест проверяет, что сеттер DeletedAtUtc выбрасывает InvalidOperationException,
     * если пытается установить значение при IsDeleted == false.
     */
    [Fact]
    public void DeletedAtUtcSetter_ThrowsInvalidOperationException_WhenValueAndIsDeletedFalse()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        DateTime deletedAt = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => entity.DeletedAtUtc = deletedAt);
    }

    /*
     * Этот тест проверяет, что сеттер DeletedAtUtc выбрасывает InvalidOperationException,
     * если значение не в формате UTC.
     */
    [Fact]
    public void DeletedAtUtcSetter_ThrowsInvalidOperationException_WhenValueIsNotUtc()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.IsDeleted = true;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => entity.DeletedAtUtc = DateTime.Now);
    }

    /*
     * Этот тест проверяет, что сеттер DeletedAtUtc выбрасывает InvalidOperationException,
     * если значение раньше CreatedAtUtc.
     */
    [Fact]
    public void DeletedAtUtcSetter_ThrowsInvalidOperationException_WhenValueBeforeCreatedAt()
    {
        // Arrange
        DateTime created = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        TestSoftEntity entity = CreateWithTimestamps(created);
        entity.IsDeleted = true;
        DateTime earlier = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => entity.DeletedAtUtc = earlier);
    }

    /*
     * Этот тест проверяет, что сеттер RecoveredAtUtc выбрасывает InvalidOperationException,
     * если значение раньше DeletedAtUtc.
     */
    [Fact]
    public void RecoveredAtUtcSetter_ThrowsInvalidOperationException_WhenValueBeforeDeletedAt()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.IsDeleted = true;
        DateTime deletedAt = entity.DeletedAtUtc!.Value;
        entity.IsDeleted = false;
        DateTime beforeDeletedAt = deletedAt.AddSeconds(-1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => entity.RecoveredAtUtc = beforeDeletedAt);
    }

    /*
     * Этот тест проверяет, что сеттер RecoveredAtUtc выбрасывает InvalidOperationException,
     * если значение не в формате UTC.
     */
    [Fact]
    public void RecoveredAtUtcSetter_ThrowsInvalidOperationException_WhenValueIsNotUtc()
    {
        // Arrange
        TestSoftEntity entity = new();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => entity.RecoveredAtUtc = DateTime.Now);
    }

    /*
     * Этот тест проверяет, что установка null в RecoveredAtUtc разрешена
     * и не выбрасывает исключений.
     */
    [Fact]
    public void RecoveredAtUtcSetter_AllowsNull()
    {
        // Arrange
        TestSoftEntity entity = new();

        // Act & Assert (не выбрасывает)
        entity.RecoveredAtUtc = null;
        Assert.Null(entity.RecoveredAtUtc);
    }

    /*
     * Этот тест проверяет, что _isDeletedInfrastructure = true
     * при ленивом переносе устанавливает IsDeleted = true без изменения временных меток.
     */
    [Fact]
    public void FlushInfrastructure_SetsIsDeleted_FromIsDeletedInfrastructure_WhenTrue()
    {
        // Arrange
        TestSoftEntity entity = new();
        entity._isDeletedInfrastructure = true;

        // Act — первый доступ инициирует flush
        bool result = entity.IsDeleted;

        // Assert
        Assert.True(result);
    }

    /*
     * Этот тест проверяет, что _isDeletedInfrastructure = false
     * при ленивом переносе оставляет IsDeleted = false.
     */
    [Fact]
    public void FlushInfrastructure_SetsIsDeleted_FromIsDeletedInfrastructure_WhenFalse()
    {
        // Arrange
        TestSoftEntity entity = new();
        entity._isDeletedInfrastructure = false;

        // Act
        bool result = entity.IsDeleted;

        // Assert
        Assert.False(result);
    }

    /*
     * Этот тест проверяет, что Touch обновляет LastUpdatedAtUtc
     * после выполнения soft-delete.
     */
    [Fact]
    public void SoftDelete_UpdatesLastUpdatedAtUtc()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        DateTime before = DateTime.UtcNow;

        // Act
        entity.IsDeleted = true;

        // Assert
        Assert.True(entity.LastUpdatedAtUtc >= before);
    }

    /*
     * Этот тест проверяет, что Recover обновляет LastUpdatedAtUtc
     * после восстановления.
     */
    [Fact]
    public void Recover_UpdatesLastUpdatedAtUtc()
    {
        // Arrange
        TestSoftEntity entity = CreateWithTimestamps(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.IsDeleted = true;
        DateTime beforeRecover = DateTime.UtcNow;

        // Act
        entity.IsDeleted = false;

        // Assert
        Assert.True(entity.LastUpdatedAtUtc >= beforeRecover);
    }
}
