# ConfigureEfContextExtensions

[← Extensions](./README.md) · [← Библиотека](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Методы](#методы)
- [Инварианты и правила](#инварианты-и-правила)
- [Сценарии использования](#сценарии-использования)
- [Обработка ошибок](#обработка-ошибок)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Предоставляет методы расширения `IServiceCollection` для регистрации и конфигурации контекстов EF Core. Поддерживает обычную и keyed-регистрацию, а также маппинг реализаций на абстракции.

Исключения класса: `ArgumentNullException`, `ArgumentException`, `InvalidOperationException`.

## Методы

#### ConfigureEfContext\<TContext\>(Action\<DbContextOptionsBuilder\> configure)

Регистрирует `TContext` через [`AddDbContext`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.entityframeworkservicecollectionextensions.adddbcontext). Проверяет наличие публичного конструктора с параметром `DbContextOptions` или `DbContextOptions<TContext>`.

**Исключения:**
- `ArgumentNullException` — `configure` равен `null`.
- `InvalidOperationException` — отсутствует подходящий конструктор.

#### ConfigureEfContextKeyed\<TContext\>(string key, Action\<DbContextOptionsBuilder\> configure)

Регистрирует `TContext` как keyed-scoped сервис. Дополнительно регистрирует keyed `DbContextOptions` и `DbContextOptions<TContext>`.

**Исключения:**
- `ArgumentNullException` — `configure` равен `null`.
- `ArgumentException` — `key` пустой или состоит из пробелов.
- `InvalidOperationException` — отсутствует подходящий конструктор или невозможно создать экземпляр через `Activator.CreateInstance`.

#### TryAddAbstractEfContext\<TAbstract, TContext\>()

Регистрирует scoped-фабрику, которая резолвит `TContext` из DI и приводит его к `TAbstract`.

**Исключения:**
- `InvalidOperationException` — `TContext` не реализует `TAbstract`.

#### TryAddAbstractEfContextKeyed\<TAbstract, TContext\>(string key)

Keyed-версия `TryAddAbstractEfContext`. Пытается получить keyed-экземпляр `TContext`, при отсутствии — fallback на не-keyed.

**Исключения:**
- `ArgumentException` — `key` пустой или состоит из пробелов.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| Конструктор TContext | Единственный параметр — `DbContextOptions` или `DbContextOptions<TContext>` | Проверяется через рефлексию перед регистрацией |
| TryAdd-семантика | `TryAddKeyedScoped` / `TryAddKeyedSingleton` | Не перезаписывает уже зарегистрированный сервис |

## Сценарии использования

Базовая регистрация:

```csharp
services.ConfigureEfContext<AppDbContext>(builder =>
{
    builder.UseNpgsql(connectionString);
});
```

Keyed-регистрация (несколько БД):

```csharp
services.ConfigureEfContextKeyed<ReportDbContext>("reports", builder =>
{
    builder.UseNpgsql(reportConnectionString);
});
```

Регистрация абстракции:

```csharp
services.ConfigureEfContext<AppDbContext>(builder => builder.UseNpgsql(cs));
services.TryAddAbstractEfContext<IAppContext, AppDbContext>();
```

## Обработка ошибок

| Ситуация | Метод | Поведение |
|----------|-------|-----------|
| `configure == null` | `ConfigureEfContext`, `ConfigureEfContextKeyed` | `ArgumentNullException` |
| `key` пустой | `ConfigureEfContextKeyed`, `TryAddAbstractEfContextKeyed` | `ArgumentException` |
| Нет подходящего конструктора | `ConfigureEfContext`, `ConfigureEfContextKeyed` | `InvalidOperationException` |
| TContext не реализует TAbstract | `TryAddAbstractEfContext` | `InvalidOperationException` |

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| TContext | Должен реализовывать `IEfContext` и наследоваться от `DbContext` |
| Keyed DI | Требует .NET 8+ (keyed services) |
| `ConfigureEfContextKeyed` | Создаёт экземпляр через `Activator.CreateInstance` — требует публичный конструктор |
