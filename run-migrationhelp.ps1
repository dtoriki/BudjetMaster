<#
.SYNOPSIS
Запускает утилиту MigrationHelp (dotnet run --project tools/MigrationHelp) из корня репозитория.

.DESCRIPTION
Скрипт гарантирует, что он выполняется из каталога скрипта (репо root), проверяет наличие dotnet в PATH,
находит указанный проект (папку или .csproj) и запускает команду `dotnet run`.

.PARAMETER Project
Путь к проекту (папке или .csproj) относительно корня репозитория. По умолчанию: "tools\MigrationHelp".

.PARAMETER Args
Аргументы, которые нужно переслать в приложение (опционально).
#>

param(
    [string] $Project = "tools\MigrationHelp",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Args
)

Set-StrictMode -Version Latest

# Переключаемся в каталог скрипта (предполагается, что скрипт лежит в корне репозитория)
try {
    Set-Location -Path $PSScriptRoot
} catch {
    # Если $PSScriptRoot недоступен (напр., выполнение в интерактивной сессии), оставляем текущий каталог
}

Write-Host "Repository root: $PWD"

# Проверка наличия dotnet
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet CLI не найден в PATH. Установите .NET 10 SDK и убедитесь, что 'dotnet' доступен."
    exit 1
}

# Разрешаем путь к проекту
$projCandidate = Join-Path $PWD $Project
[string] $projectPath = $null

if (Test-Path $projCandidate -PathType Container) {
    # Ищем .csproj в указанной папке (только верхний уровень)
    $csproj = Get-ChildItem -Path $projCandidate -Filter *.csproj -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $csproj) {
        Write-Error "Файл .csproj не найден в каталоге: $projCandidate"
        exit 2
    }

    $projectPath = $csproj.FullName
}
elseif (Test-Path $projCandidate -PathType Leaf -and $projCandidate.ToLower().EndsWith('.csproj')) {
    $projectPath = (Get-Item $projCandidate).FullName
}
else {
    Write-Error "Указанный путь проекта не найден: $Project"
    exit 2
}

Write-Host "Используется проект: $projectPath"

# Формируем аргументы для dotnet
$runArgs = @('run', '--project', $projectPath)
if ($Args -and $Args.Length -gt 0) {
    $runArgs += '--'
    $runArgs += $Args
}

Write-Host "Выполняется: dotnet $($runArgs -join ' ')"

# Запуск команды и возврат кода выхода
& dotnet @runArgs
$exitCode = $LASTEXITCODE
Write-Host "dotnet завершился с кодом $exitCode"
exit $exitCode

# ---
# © 2026 Dtoriki.BudjetMaster
