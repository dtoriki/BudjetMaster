/*
 * Этот файл сгенерирован с помощью Claude Sonnet 4.6 (claude-sonnet-4-6).
 * Он содержит модульные тесты, написанные с использованием xUnit.
 *
 * В этом файле тестируются расширения EntityExtensions.
 *
 * Тесты покрывают следующие аспекты:
 * 1. SoftDelete<TEntity,TKey>: переводит сущность в состояние удалена, устанавливает DeletedAtUtc, сбрасывает RecoveredAtUtc.
 * 2. Recover<TEntity,TKey>: восстанавливает сущность, устанавливает RecoveredAtUtc, сбрасывает DeletedAtUtc.
 * 3. Touch<TEntity,TKey>: монотонно обновляет LastUpdatedAtUtc.
 * 4. FlushInfrastructureTimestamps: переносит инфраструктурные метки в рабочие поля.
 * 5. SoftDelete для коллекции: выбрасывает InvalidOperationException при null-элементе.
 * 6. Null-guard: ArgumentNullException при null-сущности или null-коллекции.
 */

using Dtoriki.Data.Core.Entities;
using Dtoriki.Data.Core.Extensions;

namespace Dtoriki.Data.Core.Tests;

public partial class EntityExtensionsTests
{
    private sealed class TestEntity : EntityBase<long>
    {
        public TestEntity() : base()
        {
        }
    }

    private sealed class TestSoftEntity : SoftDeletableEntityBase<long>
    {
        public TestSoftEntity() : base()
        {
        }
    }

    /// <summary>
    /// Вспомогательный метод: создаёт TestEntity с уже установленным CreatedAtUtc.
    /// </summary>
    private static TestEntity CreateEntity(DateTime created)
    {
        TestEntity entity = new();
        entity.SetCreated(created);

        return entity;
    }

    /// <summary>
    /// Вспомогательный метод: создаёт TestSoftEntity с уже установленным CreatedAtUtc.
    /// </summary>
    private static TestSoftEntity CreateSoftEntity(DateTime created)
    {
        TestSoftEntity entity = new();
        entity.SetCreated(created);

        return entity;
    }

    /*
     * Этот тест проверяет, что SoftDelete<TEntity,TKey> устанавливает IsDeleted = true.
     */
    [Fact]
    public void SoftDelete_SetsIsDeletedToTrue()
    {
        // Arrange
        TestSoftEntity entity = CreateSoftEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Act
        entity.SoftDelete<TestSoftEntity, long>();

        // Assert
        Assert.True(entity.IsDeleted);
    }

    /*
     * Этот тест проверяет, что SoftDelete<TEntity,TKey> устанавливает DeletedAtUtc в UTC.
     */
    [Fact]
    public void SoftDelete_SetsDeletedAtUtcToUtcNow()
    {
        // Arrange
        TestSoftEntity entity = CreateSoftEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        DateTime before = DateTime.UtcNow;

        // Act
        entity.SoftDelete<TestSoftEntity, long>();

        // Assert
        Assert.NotNull(entity.DeletedAtUtc);
        Assert.Equal(DateTimeKind.Utc, entity.DeletedAtUtc!.Value.Kind);
        Assert.True(entity.DeletedAtUtc.Value >= before);
    }

    /*
     * Этот тест проверяет, что SoftDelete<TEntity,TKey> сбрасывает RecoveredAtUtc в null.
     */
    [Fact]
    public void SoftDelete_ClearsRecoveredAtUtc()
    {
        // Arrange
        TestSoftEntity entity = CreateSoftEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.SoftDelete<TestSoftEntity, long>();
        entity.Recover<TestSoftEntity, long>();
        // RecoveredAtUtc установлен, теперь удаляем снова

        // Act
        entity.SoftDelete<TestSoftEntity, long>();

        // Assert
        Assert.Null(entity.RecoveredAtUtc);
    }

    /*
     * Этот тест проверяет, что Recover<TEntity,TKey> устанавливает IsDeleted = false.
     */
    [Fact]
    public void Recover_SetsIsDeletedToFalse()
    {
        // Arrange
        TestSoftEntity entity = CreateSoftEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.SoftDelete<TestSoftEntity, long>();

        // Act
        entity.Recover<TestSoftEntity, long>();

        // Assert
        Assert.False(entity.IsDeleted);
    }

    /*
     * Этот тест проверяет, что Recover<TEntity,TKey> устанавливает RecoveredAtUtc в UTC.
     */
    [Fact]
    public void Recover_SetsRecoveredAtUtcToUtcNow()
    {
        // Arrange
        TestSoftEntity entity = CreateSoftEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.SoftDelete<TestSoftEntity, long>();
        DateTime before = DateTime.UtcNow;

        // Act
        entity.Recover<TestSoftEntity, long>();

        // Assert
        Assert.NotNull(entity.RecoveredAtUtc);
        Assert.Equal(DateTimeKind.Utc, entity.RecoveredAtUtc!.Value.Kind);
        Assert.True(entity.RecoveredAtUtc.Value >= before);
    }

    /*
     * Этот тест проверяет, что Recover<TEntity,TKey> сбрасывает DeletedAtUtc в null.
     */
    [Fact]
    public void Recover_ClearsDeletedAtUtc()
    {
        // Arrange
        TestSoftEntity entity = CreateSoftEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.SoftDelete<TestSoftEntity, long>();

        // Act
        entity.Recover<TestSoftEntity, long>();

        // Assert
        Assert.Null(entity.DeletedAtUtc);
    }

    /*
     * Этот тест проверяет, что SoftDelete идемпотентен:
     * повторный вызов не меняет DeletedAtUtc.
     */
    [Fact]
    public void SoftDelete_IsIdempotent_DoesNotChangeDeletedAtUtcOnSecondCall()
    {
        // Arrange
        TestSoftEntity entity = CreateSoftEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.SoftDelete<TestSoftEntity, long>();
        DateTime? firstDeletedAt = entity.DeletedAtUtc;

        // Act
        entity.SoftDelete<TestSoftEntity, long>();

        // Assert
        Assert.Equal(firstDeletedAt, entity.DeletedAtUtc);
    }

    /*
     * Этот тест проверяет, что Recover идемпотентен:
     * повторный вызов на активной сущности не меняет RecoveredAtUtc.
     */
    [Fact]
    public void Recover_IsIdempotent_WhenEntityIsAlreadyActive()
    {
        // Arrange
        TestSoftEntity entity = CreateSoftEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Act — сущность и так активна, Recover не должен ничего менять
        entity.Recover<TestSoftEntity, long>();

        // Assert
        Assert.False(entity.IsDeleted);
        Assert.Null(entity.RecoveredAtUtc);
    }

    /*
     * Этот тест проверяет, что Touch<TEntity,TKey> обновляет LastUpdatedAtUtc
     * до значения не раньше момента вызова.
     */
    [Fact]
    public void Touch_UpdatesLastUpdatedAtUtc_ToCurrentOrLater()
    {
        // Arrange
        TestEntity entity = CreateEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        DateTime before = DateTime.UtcNow;

        // Act
        entity.Touch<TestEntity, long>();

        // Assert
        Assert.True(entity.LastUpdatedAtUtc >= before);
    }

    /*
     * Этот тест проверяет, что Touch<TEntity,TKey> монотонен:
     * повторный вызов не уменьшает LastUpdatedAtUtc.
     */
    [Fact]
    public void Touch_IsMonotone_DoesNotDecreaseLastUpdatedAtUtc()
    {
        // Arrange
        TestEntity entity = CreateEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.Touch<TestEntity, long>();
        DateTime afterFirstTouch = entity.LastUpdatedAtUtc;

        // Act
        entity.Touch<TestEntity, long>();

        // Assert
        Assert.True(entity.LastUpdatedAtUtc >= afterFirstTouch);
    }

    /*
     * Этот тест проверяет, что FlushInfrastructureTimestamps<TEntity,TKey>
     * переносит _createdAtUtcInfrastructure в CreatedAtUtc.
     */
    [Fact]
    public void FlushInfrastructureTimestamps_TransfersCreatedAtUtc()
    {
        // Arrange
        TestEntity entity = new();
        DateTime infraCreated = new(2024, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        entity._createdAtUtcInfrastructure = infraCreated;

        // Act
        entity.FlushInfrastructureTimestamps<TestEntity, long>();

        // Assert
        Assert.Equal(infraCreated, entity.CreatedAtUtc);
    }

    /*
     * Этот тест проверяет, что FlushInfrastructureTimestamps<TEntity,TKey>
     * переносит _lastUpdatedAtInfrastructure в LastUpdatedAtUtc.
     */
    [Fact]
    public void FlushInfrastructureTimestamps_TransfersLastUpdatedAtUtc()
    {
        // Arrange
        TestEntity entity = new();
        DateTime infraCreated = new(2024, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        DateTime infraUpdated = new(2024, 5, 20, 18, 0, 0, DateTimeKind.Utc);
        entity._createdAtUtcInfrastructure = infraCreated;
        entity._lastUpdatedAtInfrastructure = infraUpdated;

        // Act
        entity.FlushInfrastructureTimestamps<TestEntity, long>();

        // Assert
        Assert.Equal(infraUpdated, entity.LastUpdatedAtUtc);
    }

    /*
     * Этот тест проверяет, что SoftDelete для коллекции (IEnumerable)
     * выбрасывает InvalidOperationException при наличии null-элемента в коллекции.
     */
    [Fact]
    public void SoftDeleteCollection_ThrowsInvalidOperationException_WhenCollectionContainsNull()
    {
        // Arrange
        TestSoftEntity entity = CreateSoftEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        TestSoftEntity[] entities = [entity, null!];

        // Act & Assert
        Assert.Throws<InvalidOperationException>(
            () => entities.SoftDelete<TestSoftEntity, long>());
    }

    /*
     * Этот тест проверяет, что SoftDelete для коллекции (IEnumerable)
     * выбрасывает ArgumentNullException при null-коллекции.
     */
    [Fact]
    public void SoftDeleteCollection_ThrowsArgumentNullException_WhenCollectionIsNull()
    {
        // Arrange
        IEnumerable<TestSoftEntity> entities = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => entities.SoftDelete<TestSoftEntity, long>());
    }

    /*
     * Этот тест проверяет, что SoftDelete для коллекции корректно удаляет
     * все элементы списка.
     */
    [Fact]
    public void SoftDeleteCollection_SoftDeletesAllEntities()
    {
        // Arrange
        TestSoftEntity[] entities =
        [
            CreateSoftEntity(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateSoftEntity(new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
        ];

        // Act
        entities.SoftDelete<TestSoftEntity, long>();

        // Assert
        Assert.All(entities, e => Assert.True(e.IsDeleted));
    }
}
