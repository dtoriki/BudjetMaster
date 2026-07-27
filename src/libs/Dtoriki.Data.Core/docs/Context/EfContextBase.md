# EfContextBase

[← Context](./README.md) · [← Библиотека](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Класс](#класс)
- [Методы](#методы)
- [Автоматическая обработка сущностей](#автоматическая-обработка-сущностей)
- [Инварианты и правила](#инварианты-и-правила)
- [Сценарии использования](#сценарии-использования)
- [Обработка ошибок](#обработка-ошибок)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Абстрактный базовый класс EF Core контекста, реализующий `IEfContext`. Перед каждым `SaveChanges` автоматически проставляет временные метки новым и изменённым сущностям, а также перехватывает физическое удаление `SoftDeletableEntityBase` и заменяет его мягким.

Исключения класса: `ObjectDisposedException`.

## Класс

```csharp
public abstract class EfContextBase : DbContext, IEfContext
```

## Методы

#### SaveChanges() / SaveChanges(bool)

Перед вызовом базового `DbContext.SaveChanges` выполняет `SaveChangesInternal()`: проставляет временные метки и обрабатывает soft-delete.

**Исключения:**
- `ObjectDisposedException` — контекст освобождён.
- `InvalidOperationException` — нарушение инвариантов временных меток.
- `ArgumentOutOfRangeException` — значения меток вне допустимого диапазона.

#### SaveChangesAsync(CancellationToken) / SaveChangesAsync(bool, CancellationToken)

Асинхронные версии с той же логикой предобработки.

#### SaveChangesSilentAsync(CancellationToken)

Сохраняет изменения **без** предобработки временных меток и soft-delete. Предназначен для инфраструктурных сценариев: синхронизация данных, импорт, реплей истории — когда временные метки должны быть установлены явно.

**Исключения:**
- [`DbUpdateException`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbupdateexception) — ошибка при сохранении.
- [`DbUpdateConcurrencyException`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbupdateconcurrencyexception) — конфликт параллелизма.

#### BeginTransaction() / BeginTransactionAsync(CancellationToken)

Проверяют `_isDisposed` через `DisposeCheck()` и делегируют к `Database.BeginTransaction`.

#### Dispose() / DisposeAsync()

Идемпотентны: повторный вызов — no-op. Вызывают `GC.SuppressFinalize`.

## Автоматическая обработка сущностей

При каждом вызове `SaveChanges*` (кроме `SaveChangesSilentAsync`) выполняются две операции:

**1. SoftDelete-перехват.** Для каждой записи с `EntityState.Deleted`, если сущность является `SoftDeletableEntityBase`:
- состояние меняется на `EntityState.Modified`,
- вызывается `SoftDelete()`,
- все owned-сущности переводятся в `EntityState.Unchanged`.

**2. Простановка временных меток.** Для каждой `IEntity`:
- `EntityState.Added` → если `CreatedAtUtc == default`, вызывается `SetCreated(utcNow)`; если `LastUpdatedAtUtc < CreatedAtUtc`, вызывается `Touch()`.
- `EntityState.Modified` → вызывается `Touch()`.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| Dispose | `_isDisposed` проверяется перед любой операцией | `ObjectDisposedException` при использовании после dispose |
| Soft-delete | Только `SoftDeletableEntityBase` | `EntityState.Deleted` → перехват |
| Owned-сущности | При soft-delete principal | Все owned переводятся в `Unchanged` |
| `SaveChangesSilentAsync` | Без предобработки | Метки и soft-delete — ответственность вызывающего |

## Сценарии использования

Конкретный контекст:

```csharp
public sealed class AppDbContext : EfContextBase
{
    public AppDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

Импорт данных без автоматических меток:

```csharp
// Временные метки установлены вручную из источника
transaction.CreatedAtUtc = importedCreatedAt;
await context.SaveChangesSilentAsync(cancellationToken);
```

## Обработка ошибок

| Ситуация | Метод | Поведение |
|----------|-------|-----------|
| Контекст освобождён | Любой публичный метод | `ObjectDisposedException` |
| Нарушение инвариантов меток | `SaveChanges*` | `InvalidOperationException` |
| Ошибка БД | `SaveChangesSilentAsync` | `DbUpdateException` |
| Конфликт параллелизма | `SaveChangesSilentAsync` | `DbUpdateConcurrencyException` |

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| Soft-delete | Перехватывает только `SoftDeletableEntityBase`; интерфейс `ISoftDeletableEntity` без базового класса не перехватывается |
| `SaveChangesSilentAsync` | Только асинхронная версия — нет синхронного аналога |
| Конструктор | `protected EfContextBase(DbContextOptions)` — требует передачи опций |
