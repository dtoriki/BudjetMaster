namespace MigrationHelp.Common;

/// <summary>
/// Атрибут, используемый для обозначения фабрик DbContext во время выполнения миграций.
/// </summary>
/// <remarks>
/// Класс-присваиватель фабрики может быть помечен этим атрибутом, чтобы предоставить
/// читаемое имя контекста и имя сборки, в которой находятся миграции для данного контекста.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DbMigrationContextInfoAttribute : Attribute
{
    /// <summary>
    /// Возвращает короткое отображаемое имя контекста миграций.
    /// Обычно используется в UI для выбора контекста при генерации миграций.
    /// </summary>
    public string ContextName { get; }

    /// <summary>
    /// Возвращает имя сборки или проекта, в котором расположены миграции для данного контекста.
    /// Может быть использовано для определения значения параметра <c>--project</c> при запуске
    /// команд <c>dotnet ef</c>.
    /// </summary>
    public string MigrationAssembly { get; }

    /// <summary>
    /// Инициализирует новый экземпляр атрибута с заданным именем контекста и именем сборки миграций.
    /// </summary>
    /// <param name="contextName">Отображаемое имя контекста миграций.</param>
    /// <param name="migrationAssembly">Имя сборки/проекта, содержащего миграции.</param>
    public DbMigrationContextInfoAttribute(string contextName, string migrationAssembly)
    {
        ContextName = contextName;
        MigrationAssembly = migrationAssembly;
    }
}
