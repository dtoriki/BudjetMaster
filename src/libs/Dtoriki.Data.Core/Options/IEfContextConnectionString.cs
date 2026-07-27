namespace Dtoriki.Data.Core.Options;

/// <summary>
/// Контракт для поставщика строки подключения и настроек безопасности Entity Framework Core.
/// </summary>
/// <remarks>
/// Реализуется поставщиками конфигурации (например, на основе appsettings.json, переменных окружения или кастомных источников)
/// для обеспечения единого интерфейса доступа к параметрам подключения контекста EF Core.
/// </remarks>
public interface IEfContextConnectionString
{
    /// <summary>
    /// Возвращает значение, указывающее, требуется ли использовать защищённое соединение (SSL/TLS) при подключении к базе данных.
    /// </summary>
    /// <remarks>
    /// <see langword="true"/> — обязательно использовать SSL/TLS.
    /// <see langword="false"/> — SSL/TLS не требуется.
    /// </remarks>
    bool UseSSL { get; }

    /// <summary>
    /// Возвращает полную строку подключения к базе данных, готовую к использованию Entity Framework Core.
    /// </summary>
    string ConnectionString { get; }
}
