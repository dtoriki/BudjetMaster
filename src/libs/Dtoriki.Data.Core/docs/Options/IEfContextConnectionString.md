# IEfContextConnectionString

[← Документация библиотеки](../README.md)

---

## Содержание

- [Назначение](#назначение)
- [Интерфейс](#интерфейс)
- [Свойства](#свойства)
- [Инварианты и правила](#инварианты-и-правила)
- [Сценарии использования](#сценарии-использования)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Назначение

Определяет контракт поставщика строки подключения и настроек безопасности для EF Core контекста. Реализуется провайдерами конфигурации (appsettings.json, переменные окружения, кастомные источники) и передаётся в делегат `configure` при регистрации через `ConfigureEfContextExtensions`.

## Интерфейс

```csharp
public interface IEfContextConnectionString
```

## Свойства

#### UseSSL { get; }

Требует ли соединение SSL/TLS. `true` — обязательно; `false` — не требуется.

#### ConnectionString { get; }

Полная строка подключения к базе данных, готовая к передаче в EF Core провайдер.

## Инварианты и правила

| Область | Условие | Гарантия |
|---------|---------|---------|
| ConnectionString | Строка подключения соответствует провайдеру БД | Ответственность реализации |
| UseSSL | Семантика зависит от провайдера | Реализация интерпретирует флаг самостоятельно |

## Сценарии использования

Реализация на основе конфигурации:

```csharp
public sealed class AppConnectionStringOptions : IEfContextConnectionString
{
    public bool UseSSL { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
}
```

Регистрация в DI:

```csharp
AppConnectionStringOptions options = configuration
    .GetSection("Database")
    .Get<AppConnectionStringOptions>()!;

services.ConfigureEfContext<AppDbContext>(builder =>
{
    builder.UseNpgsql(options.ConnectionString, npgsql =>
    {
        if (options.UseSSL)
        {
            npgsql.RemoteCertificateValidationCallback((_, _, _, _) => true);
        }
    });
});
```

## Ограничения и допущения

| Область | Ограничение |
|---------|-------------|
| Валидация | Интерфейс не валидирует `ConnectionString`; пустая строка обнаружится при открытии соединения |
| SSL | Интерпретация `UseSSL` зависит от реализующего класса и используемого провайдера БД |
