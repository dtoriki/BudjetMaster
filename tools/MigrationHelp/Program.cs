using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore.Design;
using MigrationHelp.Common;
using MigrationHelp.Design;
using Serilog;
using Spectre.Console;

namespace MigrationHelp;

internal class Program
{
    public static async Task<int> Main()
    {
        MigrationHelpDesignLoad.Load();
        Console.OutputEncoding = Encoding.UTF8;
        AnsiConsole.Clear();

        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        try
        {
            while (true)
            {
                AnsiConsole.Clear();
                string choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Выберите операцию:")
                        .AddChoices(["Создать снепшот БД", "Создать миграцию", "Выход"]));

                if (choice == "Выход")
                {
                    return 0;
                }

                if (choice == "Создать снепшот БД")
                {
                    await CreateSnapshotInteractive();
                    continue;
                }

                if (choice == "Создать миграцию")
                {
                    await CreateMigrationInteractive();
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error");
            return 1;
        }
    }

    private static IEnumerable<(Type FactoryType, Type ContextType, string DisplayName)> GetDesignTimeFactories()
    {
        const string DESIGN_ASSEMBLY_NAME = "MigrationHelp.Design";
        Assembly asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == DESIGN_ASSEMBLY_NAME);

        List<(Type, Type, string)> result = [];

        Type[] types;
        try
        {
            types = asm.GetTypes();
        }
        catch
        {
            return [];
        }

        foreach (Type type in types)
        {
            if (!type.IsClass)
            {
                continue;
            }

            Type[] interfaces = type.GetInterfaces();
            Type? factoryInterface = interfaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDesignTimeDbContextFactory<>));
            if (factoryInterface == null)
            {
                continue;
            }

            Type contextType = factoryInterface.GetGenericArguments()[0];

            DbMigrationContextInfoAttribute? attr = type.GetCustomAttribute<DbMigrationContextInfoAttribute>(false);
            string displayName = attr?.ContextName ?? contextType.Name;

            result.Add((type, contextType, displayName));
        }

        return result;
    }

    private static string FindSolutionRoot()
    {
        string dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            string[] slns =
            [
                ..Directory.GetFiles(dir, "*.sln", SearchOption.TopDirectoryOnly),
                ..Directory.GetFiles(dir, "*.slnx", SearchOption.TopDirectoryOnly),
            ];
            if (slns.Length > 0)
            {
                return dir;
            }

            DirectoryInfo? parentInfo = Directory.GetParent(dir);
            if (parentInfo == null)
            {
                break;
            }

            string parent = parentInfo.FullName;
            if (string.IsNullOrEmpty(parent) || parent == dir)
            {
                break;
            }

            dir = parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? PickProjectForContext(Type contextType, string solutionRoot)
    {
        string[] projects = Directory.GetFiles(solutionRoot, "*.csproj", SearchOption.AllDirectories);
        string contextName = contextType.Name;
        List<string> candidates = [];

        foreach (string proj in projects)
        {
            string projName = Path.GetFileNameWithoutExtension(proj);
            if (projName.Equals(contextType.Namespace, StringComparison.OrdinalIgnoreCase) || projName.Equals(contextType.Name, StringComparison.OrdinalIgnoreCase) || projName.Contains(contextName, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(proj);
            }
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        if (candidates.Count > 1)
        {
            string[] choiceItems = candidates.Select(p => Path.GetFileName(p)).Where(s => s != null).Select(s => s!).ToArray();
            string pick = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Выберите проект для размещения миграций:").AddChoices(choiceItems));
            return candidates.First(p => Path.GetFileName(p) == pick);
        }

        if (projects.Length == 0)
        {
            return null;
        }

        string[] projectItems = projects.Select(p => Path.GetFileName(p)).Where(s => s != null).Select(s => s!).ToArray();
        string pickAll = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Проект с миграциями не найден автоматически. Выберите проект:").AddChoices(projectItems));
        return projects.First(p => Path.GetFileName(p) == pickAll);
    }

    private static string? FindProjectByName(string root, string projNameWithoutExt)
    {
        string[] projects = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);
        foreach (string proj in projects)
        {
            if (Path.GetFileNameWithoutExtension(proj).Equals(projNameWithoutExt, StringComparison.OrdinalIgnoreCase))
            {
                return proj;
            }
        }

        return null;
    }

    private static string? FindProjectByAssemblyName(string root, string assemblyName)
    {
        string[] projects = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);
        foreach (string proj in projects)
        {
            string projName = Path.GetFileNameWithoutExtension(proj);
            if (projName.Equals(assemblyName, StringComparison.OrdinalIgnoreCase) || projName.Contains(assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return proj;
            }
        }

        return null;
    }

    private static async Task CreateSnapshotInteractive()
    {
        IEnumerable<(Type FactoryType, Type ContextType, string DisplayName)> factories = GetDesignTimeFactories();
        if (!factories.Any())
        {
            AnsiConsole.MarkupLine("[red]Не найдено классов, реализующих IDesignTimeDbContextFactory<>.[/]");
            return;
        }

        string solutionRoot = FindSolutionRoot();

        string? stashName = await ResolveUncommittedChanges(solutionRoot);
        if (stashName == null)
        {
            return;
        }

        AnsiConsole.Clear();
        string[] items = factories.Select(f => f.DisplayName).ToArray();
        string selected = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Выберите контекст для создания снепшота:").AddChoices(items));
        (Type factoryType, Type contextType, string displayName) = factories.First(f => f.DisplayName == selected);

        DbMigrationContextInfoAttribute? factoryAttr = factories.First(f => f.DisplayName == selected).FactoryType.GetCustomAttribute<DbMigrationContextInfoAttribute>(false);
        string? projectPath = null;
        if (factoryAttr != null && !string.IsNullOrWhiteSpace(factoryAttr.MigrationAssembly))
        {
            projectPath = FindProjectByAssemblyName(solutionRoot, factoryAttr.MigrationAssembly);
        }

        if (projectPath == null)
        {
            projectPath = PickProjectForContext(contextType, solutionRoot);
        }

        if (projectPath == null)
        {
            AnsiConsole.MarkupLine("[red]Не найден csproj в репозитории.[/]");
            return;
        }

        string migrationName = $"SnapshotOnly_{DateTime.UtcNow:yyyyMMddHHmmss}";

        string? startupProjectPath = FindProjectByName(solutionRoot, "MigrationHelp.Design");

        string projectDir = Path.GetDirectoryName(projectPath) ?? solutionRoot;

        string? originalBranch = await GetCurrentGitBranch(solutionRoot);
        bool switchedToMaster = false;
        if (!string.IsNullOrEmpty(originalBranch) && !originalBranch.Equals("master", StringComparison.OrdinalIgnoreCase))
        {
            int checkoutCode = await RunGitCommand(new[] { "checkout", "master" }, solutionRoot);
            if (checkoutCode != 0)
            {
                AnsiConsole.MarkupLine("[red]Не удалось переключиться на ветку 'master'. Операция прервана.[/]");
                return;
            }

            switchedToMaster = true;
        }

        bool snapshotCreated = false;

        try
        {
            MigrationSnapshotHelper.RemoveExistingModelSnapshots(projectDir);
            MigrationSnapshotHelper.RemoveExistingSnapshotMigrations(projectDir);

            string[] args = ["ef", "migrations", "add", migrationName, "--context", contextType.FullName!, "--project", projectPath, "--startup-project", startupProjectPath ?? "MigrationHelp.Design"];

            int exitCode = await RunDotnetCommand(args, Path.GetDirectoryName(projectPath) ?? solutionRoot);

            if (exitCode != 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Команда dotnet ef завершилась с кодом {exitCode}. Снепшот не создан.[/]");
                return;
            }

            try
            {
                MigrationSnapshotHelper.CleanupMigrationToSnapshot(Path.GetDirectoryName(projectPath) ?? solutionRoot, migrationName);
                MigrationSnapshotHelper.RemoveExistingSnapshotMigrations(projectDir);
                snapshotCreated = true;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Не удалось преобразовать миграцию в снепшот: {ex.Message}[/]");
            }
        }
        finally
        {
            if (switchedToMaster && !string.IsNullOrEmpty(originalBranch))
            {
                await RunGitCommand(new[] { "checkout", originalBranch }, solutionRoot);
            }

            if (!string.IsNullOrEmpty(stashName))
            {
                await PopNamedStash(stashName, solutionRoot);
            }
        }

        if (snapshotCreated)
        {
            AnsiConsole.MarkupLineInterpolated($"[green]Снепшот для [bold]{displayName}[/] создан.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Нажмите любую клавишу...[/]");
            Console.ReadKey(true);
        }
    }

    private static async Task CreateMigrationInteractive()
    {
        IEnumerable<(Type FactoryType, Type ContextType, string DisplayName)> factories = GetDesignTimeFactories();
        if (!factories.Any())
        {
            AnsiConsole.MarkupLine("[red]Не найдено классов, реализующих IDesignTimeDbContextFactory<>.[/]");
            return;
        }

        string solutionRoot = FindSolutionRoot();

        string? stashName = await ResolveUncommittedChanges(solutionRoot);
        if (stashName == null)
        {
            return;
        }

        AnsiConsole.Clear();
        string[] items = factories.Select(f => f.DisplayName).ToArray();
        string selected = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Выберите контекст для создания миграции:").AddChoices(items));
        (Type factoryType, Type contextType, string displayName) = factories.First(f => f.DisplayName == selected);

        AnsiConsole.Clear();
        string migrationName = AnsiConsole.Ask<string>("Введите имя миграции:");
        if (string.IsNullOrWhiteSpace(migrationName))
        {
            AnsiConsole.MarkupLine("[red]Имя миграции не может быть пустым.[/]");
            return;
        }

        DbMigrationContextInfoAttribute? factoryAttr = factories.First(f => f.DisplayName == selected).FactoryType.GetCustomAttribute<DbMigrationContextInfoAttribute>(false);
        string? projectPath = null;
        if (factoryAttr != null && !string.IsNullOrWhiteSpace(factoryAttr.MigrationAssembly))
        {
            projectPath = FindProjectByAssemblyName(solutionRoot, factoryAttr.MigrationAssembly);
        }

        if (projectPath == null)
        {
            projectPath = PickProjectForContext(contextType, solutionRoot);
        }

        if (projectPath == null)
        {
            AnsiConsole.MarkupLine("[red]Не найден csproj в репозитории.[/]");
            return;
        }

        string? startupProjectPath = FindProjectByName(solutionRoot, "MigrationHelp.Design");
        string projectDir = Path.GetDirectoryName(projectPath) ?? solutionRoot;

        string? originalBranch = await GetCurrentGitBranch(solutionRoot);
        bool switchedToMaster = false;
        if (!string.IsNullOrEmpty(originalBranch) && !originalBranch.Equals("master", StringComparison.OrdinalIgnoreCase))
        {
            int checkoutCode = await RunGitCommand(["checkout", "master"], solutionRoot);
            if (checkoutCode != 0)
            {
                AnsiConsole.MarkupLine("[red]Не удалось переключиться на ветку 'master'. Операция прервана.[/]");
                return;
            }

            switchedToMaster = true;
        }

        bool snapshotReady = false;

        try
        {
            string snapshotMigrationName = $"SnapshotOnly_{DateTime.UtcNow:yyyyMMddHHmmss}";

            MigrationSnapshotHelper.RemoveExistingModelSnapshots(projectDir);
            MigrationSnapshotHelper.RemoveExistingSnapshotMigrations(projectDir);

            string[] snapshotArgs = ["ef", "migrations", "add", snapshotMigrationName, "--context", contextType.FullName!, "--project", projectPath, "--startup-project", startupProjectPath ?? "MigrationHelp.Design"];
            int snapshotExitCode = await RunDotnetCommand(snapshotArgs, projectDir);

            if (snapshotExitCode != 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Не удалось создать снепшот (код {snapshotExitCode}). Миграция не будет создана.[/]");
                return;
            }

            try
            {
                MigrationSnapshotHelper.CleanupMigrationToSnapshot(projectDir, snapshotMigrationName);
                MigrationSnapshotHelper.RemoveExistingSnapshotMigrations(projectDir);
                snapshotReady = true;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Не удалось преобразовать миграцию в снепшот: {ex.Message}[/]");
            }
        }
        finally
        {
            if (switchedToMaster && !string.IsNullOrEmpty(originalBranch))
            {
                await RunGitCommand(["checkout", originalBranch], solutionRoot);
            }

            if (!string.IsNullOrEmpty(stashName))
            {
                await PopNamedStash(stashName, solutionRoot);
            }
        }

        if (!snapshotReady)
        {
            return;
        }

        string[] migrationArgs = ["ef", "migrations", "add", migrationName, "--context", contextType.FullName!, "--project", projectPath, "--startup-project", startupProjectPath ?? "MigrationHelp.Design"];
        int exitCode = await RunDotnetCommand(migrationArgs, projectDir);
        if (exitCode == 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[green]Миграция [bold]{migrationName}[/] для [bold]{displayName}[/] создана.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Нажмите любую клавишу...[/]");
            Console.ReadKey(true);
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Команда dotnet ef завершилась с кодом {exitCode}.[/]");
        }
    }

    private static async Task<int> RunDotnetCommand(string[] args, string workingDirectory)
    {
        string arguments = string.Join(' ', args.Select(a => a.Contains(' ') ? '"' + a + '"' : a));

        ProcessStartInfo psi = new()
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = new();
        process.StartInfo = psi;
        process.OutputDataReceived += (s, e) => { if (e.Data != null) AnsiConsole.WriteLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) AnsiConsole.MarkupLineInterpolated($"[red]{e.Data}[/]"); };

        AnsiConsole.MarkupLineInterpolated($"[green]Запуск: dotnet {arguments} (в каталоге {workingDirectory})[/]");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        AnsiConsole.MarkupLine($"Процесс завершился с кодом {process.ExitCode}");
        return process.ExitCode;
    }

    private static async Task<int> RunGitCommand(string[] args, string workingDirectory)
    {
        string arguments = string.Join(' ', args.Select(a => a.Contains(' ') ? '"' + a + '"' : a));

        ProcessStartInfo psi = new()
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = new();
        process.StartInfo = psi;
        process.OutputDataReceived += (s, e) => { if (e.Data != null) AnsiConsole.WriteLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) AnsiConsole.MarkupLineInterpolated($"[red]{e.Data}[/]"); };

        AnsiConsole.MarkupLineInterpolated($"[green]Запуск: git {arguments} (в каталоге {workingDirectory})[/]");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        AnsiConsole.MarkupLine($"Git завершился с кодом {process.ExitCode}");
        return process.ExitCode;
    }

    /// <summary>
    /// Возвращает <see langword="null"/> — пользователь выбрал «Назад»;
    /// пустую строку — изменений нет (или сделан коммит);
    /// непустую строку — имя стеша, который нужно восстановить после завершения.
    /// </summary>
    private static async Task<string?> ResolveUncommittedChanges(string solutionRoot)
    {
        while (true)
        {
            AnsiConsole.Clear();

            string? uncommitted = await GetUncommittedChanges(solutionRoot);
            if (uncommitted == null)
            {
                return string.Empty;
            }

            AnsiConsole.MarkupLine("[yellow]Есть незафиксированные изменения.[/]");
            AnsiConsole.WriteLine();

            string action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Выберите действие:")
                    .AddChoices("Убрать в стеш", "Сделать коммит", "Повторить", "Назад"));

            if (action == "Назад")
            {
                return null;
            }

            if (action == "Повторить")
            {
                continue;
            }

            if (action == "Убрать в стеш")
            {
                string name = $"MigrationHelp_snapshot_{DateTime.UtcNow:yyyyMMddHHmmss}";
                int code = await RunGitCommand(["stash", "push", "-m", name], solutionRoot);
                if (code != 0)
                {
                    AnsiConsole.MarkupLine("[red]Не удалось создать стеш. Попробуйте ещё раз.[/]");
                    continue;
                }

                return name;
            }

            if (action == "Сделать коммит")
            {
                bool hasStaged = await HasStagedChanges(solutionRoot);
                bool hasUnstaged = await HasUnstagedChanges(solutionRoot);

                if (hasStaged && hasUnstaged)
                {
                    AnsiConsole.Clear();
                    AnsiConsole.MarkupLine("[yellow]Есть и проиндексированные, и непроиндексированные изменения — они войдут в один коммит.[/]");
                    AnsiConsole.WriteLine();

                    string confirm = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Продолжить?")
                            .AddChoices("Продолжить", "Назад"));

                    if (confirm == "Назад")
                    {
                        continue;
                    }
                }

                AnsiConsole.Clear();
                string message = AnsiConsole.Ask<string>("Введите сообщение коммита:");
                if (string.IsNullOrWhiteSpace(message))
                {
                    AnsiConsole.MarkupLine("[red]Сообщение коммита не может быть пустым.[/]");
                    continue;
                }

                int addCode = await RunGitCommand(["add", "-A"], solutionRoot);
                if (addCode != 0)
                {
                    AnsiConsole.MarkupLine("[red]Не удалось выполнить git add. Попробуйте ещё раз.[/]");
                    continue;
                }

                int commitCode = await RunGitCommand(["commit", "-m", message], solutionRoot);
                if (commitCode != 0)
                {
                    AnsiConsole.MarkupLine("[red]Не удалось создать коммит. Попробуйте ещё раз.[/]");
                    continue;
                }

                return string.Empty;
            }
        }
    }

    private static async Task<bool> HasStagedChanges(string workingDirectory)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "git",
            Arguments = "diff --staged --quiet",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = new();
        process.StartInfo = psi;
        process.Start();
        await process.WaitForExitAsync();

        return process.ExitCode != 0;
    }

    private static async Task<bool> HasUnstagedChanges(string workingDirectory)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "git",
            Arguments = "diff --quiet",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = new();
        process.StartInfo = psi;
        process.Start();
        await process.WaitForExitAsync();

        return process.ExitCode != 0;
    }

    private static async Task PopNamedStash(string stashName, string workingDirectory)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "git",
            Arguments = "stash list",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process listProcess = new();
        listProcess.StartInfo = psi;
        listProcess.Start();
        string listOutput = await listProcess.StandardOutput.ReadToEndAsync();
        await listProcess.WaitForExitAsync();

        string? stashRef = listOutput
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Contains(stashName))
            .Select(l => l.Split(':')[0].Trim())
            .FirstOrDefault();

        if (stashRef == null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Не удалось найти стеш '{stashName}'. Восстановите изменения вручную (git stash list).[/]");
            return;
        }

        await RunGitCommand(["stash", "pop", stashRef], workingDirectory);
    }

    private static async Task<string?> GetUncommittedChanges(string workingDirectory)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "git",
            Arguments = "status --porcelain",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = new();
        process.StartInfo = psi;

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        string trimmed = output.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static async Task<string?> GetCurrentGitBranch(string workingDirectory)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "git",
            Arguments = "rev-parse --abbrev-ref HEAD",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = new();
        process.StartInfo = psi;

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        return output.Trim();
    }
}
