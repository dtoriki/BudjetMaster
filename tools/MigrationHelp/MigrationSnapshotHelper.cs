using System.Text;
using System.Text.RegularExpressions;

namespace MigrationHelp;

internal static partial class MigrationSnapshotHelper
{
    public static void CleanupMigrationToSnapshot(string projectDir, string migrationName)
    {
        string migrationsDir = Path.Combine(projectDir, "Migrations");
        if (!Directory.Exists(migrationsDir))
        {
            string[] found = Directory.GetFiles(projectDir, $"*{migrationName}*.cs", SearchOption.AllDirectories);
            if (found.Length == 0)
            {
                throw new FileNotFoundException("Файл миграции не найден.");
            }

            CleanMigrationFile(found[0]);
            return;
        }

        string[] files = Directory.GetFiles(migrationsDir, $"*{migrationName}*.cs", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            string[] found = Directory.GetFiles(projectDir, $"*{migrationName}*.cs", SearchOption.AllDirectories);
            if (found.Length == 0)
            {
                throw new FileNotFoundException("Файл миграции не найден в каталоге Migrations.");
            }

            CleanMigrationFile(found[0]);
            return;
        }

        CleanMigrationFile(files[0]);
    }

    public static void RemoveExistingSnapshotMigrations(string projectDir)
    {
        string migrationsDir = Path.Combine(projectDir, "Migrations");
        if (!Directory.Exists(migrationsDir))
        {
            return;
        }

        string[] files = Directory.GetFiles(migrationsDir, "*SnapshotOnly_*.cs", SearchOption.TopDirectoryOnly);
        foreach (string file in files)
        {
            File.Delete(file);
        }
    }

    public static void RemoveExistingModelSnapshots(string projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
        {
            return;
        }

        string migrationsDir = Path.Combine(projectDir, "Migrations");
        if (!Directory.Exists(migrationsDir))
        {
            return;
        }

        string[] snapshotFiles = Directory.GetFiles(migrationsDir, "*ModelSnapshot.cs", SearchOption.TopDirectoryOnly);
        foreach (string file in snapshotFiles)
        {
            try
            {
                string content = File.ReadAllText(file, Encoding.UTF8);
                if (InheritsModelSnapshot().IsMatch(content))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Игнорируем ошибки при чтении/удалении
            }
        }
    }

    private static void CleanMigrationFile(string path)
    {
        string content = File.ReadAllText(path, Encoding.UTF8);

        string pattern = @"protected override void Up\(MigrationBuilder migrationBuilder\)\s*\{[\s\S]*?\}\s*protected override void Down\(MigrationBuilder migrationBuilder\)\s*\{[\s\S]*?\}";
        string replacement = "protected override void Up(MigrationBuilder migrationBuilder)\n        {\n        }\n\n        protected override void Down(MigrationBuilder migrationBuilder)\n        {\n        }";

        string newContent = Regex.Replace(content, pattern, replacement, RegexOptions.Singleline);

        File.WriteAllText(path, newContent, Encoding.UTF8);
    }

    [GeneratedRegex(@"class\s+\w+\s*:\s*(?:[\w\.]*ModelSnapshot)\b", RegexOptions.Singleline)]
    private static partial Regex InheritsModelSnapshot();
}
