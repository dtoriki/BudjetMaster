# План реализации Dtoriki.BudjetMaster

[← Назад к README](../README.md)

---

## Содержание

- [Обзор фаз](#обзор-фаз)
- [Фаза 1 — Доменный слой](#фаза-1--доменный-слой)
- [Фаза 2 — Инфраструктура и БД](#фаза-2--инфраструктура-и-бд)
- [Фаза 3 — Бизнес-логика](#фаза-3--бизнес-логика)
- [Фаза 4 — Импорт транзакций](#фаза-4--импорт-транзакций)
- [Фаза 5 — Application слой](#фаза-5--application-слой)
- [Фаза 6 — MAUI UI](#фаза-6--maui-ui)
- [Фаза 7 — Тесты](#фаза-7--тесты)
- [Фаза 8 — Расширения](#фаза-8--расширения)
- [Зависимости между фазами](#зависимости-между-фазами)

---

## Обзор фаз

```mermaid
gantt
    title Очерёдность реализации BudjetMaster
    dateFormat  X
    axisFormat  Фаза %s

    section Ядро
    Доменный слой          :done,    f1, 1, 2
    Инфраструктура и БД    :active,  f2, 2, 3
    Бизнес-логика          :         f3, 3, 4

    section Данные
    Импорт транзакций      :         f4, 4, 5
    Application слой       :         f5, 3, 5

    section UI
    MAUI UI                :         f6, 5, 7

    section Качество
    Тесты                  :         f7, 1, 7

    section Будущее
    Расширения             :         f8, 7, 9
```

```mermaid
flowchart LR
    F1[Фаза 1\nДомен] --> F2[Фаза 2\nИнфраструктура]
    F1 --> F3[Фаза 3\nБизнес-логика]
    F2 --> F3
    F1 --> F5[Фаза 5\nApplication]
    F3 --> F5
    F4[Фаза 4\nИмпорт] --> F5
    F5 --> F6[Фаза 6\nMAUI UI]
    F1 -.тесты.-> F7[Фаза 7\nТесты]
    F3 -.тесты.-> F7
    F6 --> F8[Фаза 8\nРасширения]

    style F1 fill:#dbeafe,stroke:#3b82f6
    style F2 fill:#dbeafe,stroke:#3b82f6
    style F3 fill:#dcfce7,stroke:#22c55e
    style F4 fill:#dcfce7,stroke:#22c55e
    style F5 fill:#dcfce7,stroke:#22c55e
    style F6 fill:#fce7f3,stroke:#ec4899
    style F7 fill:#fef9c3,stroke:#eab308
    style F8 fill:#f3f4f6,stroke:#9ca3af
```

---

## Фаза 1 — Доменный слой

**Приоритет:** первый. Всё остальное зависит от домена.

Цель — описать сущности, value objects и интерфейсы без какой-либо зависимости от инфраструктуры или UI.

```mermaid
flowchart TD
    subgraph F1["Фаза 1 — Domain"]
        E1[Account\nAccountType]
        E2[Transaction\nTransactionType\nTransactionSource]
        E3[BudgetPeriod\nBudgetPeriodAccount]
        E4[DailyBudget]
        E5[SpendingZoneDefinition]
        I1[ILimitCalculator\nLimitCalculatorContext]
        I2[ITransactionImporter\nImportResult]
        I3[IAccountRepository\nITransactionRepository\nIBudgetPeriodRepository]
    end

    E3 --> E4
    E3 --> E5
    E4 --> E5
    I1 --> E4
```

Задачи:

- Создать проект `Dtoriki.BudjetMaster.Domain`.
- Реализовать сущности: `Account`, `Transaction`, `BudgetPeriod`, `BudgetPeriodAccount`, `DailyBudget`, `SpendingZoneDefinition`.
- Объявить перечисления: `AccountType`, `TransactionType`, `TransactionSource`.
- Объявить интерфейсы: `ILimitCalculator`, `ITransactionImporter`.
- Объявить интерфейсы репозиториев: `IAccountRepository`, `ITransactionRepository`, `IBudgetPeriodRepository`, `IDailyBudgetRepository`.
- Покрыть инварианты XML-документацией.

---

## Фаза 2 — Инфраструктура и БД

**Зависит от:** Фазы 1.

Цель — подключить PostgreSQL через EF Core, реализовать репозитории, создать миграции.

```mermaid
flowchart TD
    subgraph F2["Фаза 2 — Infrastructure"]
        CTX[BudjetMasterDbContext]
        CFG1[AccountConfiguration]
        CFG2[TransactionConfiguration]
        CFG3[BudgetPeriodConfiguration]
        CFG4[DailyBudgetConfiguration]
        CFG5[SpendingZoneDefinitionConfiguration]
        CFG6[BudgetPeriodAccountConfiguration]
        R1[AccountRepository]
        R2[TransactionRepository]
        R3[BudgetPeriodRepository]
        R4[DailyBudgetRepository]
        MIG[Первая миграция]
    end

    CTX --> CFG1 & CFG2 & CFG3 & CFG4 & CFG5 & CFG6
    R1 & R2 & R3 & R4 --> CTX
    CTX --> MIG
```

Задачи:

- Создать проект `Dtoriki.BudjetMaster.Infrastructure`.
- Настроить `BudjetMasterDbContext` с конфигурациями всех сущностей.
- Реализовать репозитории (`IAccountRepository`, `ITransactionRepository`, `IBudgetPeriodRepository`, `IDailyBudgetRepository`).
- Настроить уникальные индексы: `(PeriodId, Date)` для `DailyBudget`, `(BudgetPeriodId, AccountId)` для `BudgetPeriodAccount`.
- Создать первую миграцию и применить к PostgreSQL.
- Добавить seed-данные для ручного тестирования.

---

## Фаза 3 — Бизнес-логика

**Зависит от:** Фаз 1 и 2.

Цель — реализовать расчёт лимитов, зон и статистики.

```mermaid
flowchart TD
    subgraph F3["Фаза 3 — Business Logic"]
        CALC[ArithmeticMeanCalculator\nILimitCalculator]
        ZONE[ZoneResolver\nопределяет ActiveZoneId]
        STAT[MedianCalculator\nвычисляет медиану за период]
        DEV[DeviationCalculator\nотклонение от медианы]
        RECOMPUTE[DailyBudgetRecomputeService\nпересчёт при изменении транзакций]
    end

    CALC --> RECOMPUTE
    ZONE --> RECOMPUTE
    STAT --> DEV
```

Задачи:

- Реализовать `ArithmeticMeanCalculator` (формула: `BaseLimit + Unspent / RemainingDays`).
- Реализовать `ZoneResolver` — определяет `SpendingZoneDefinition` для заданного `ratio`.
- Реализовать `MedianCalculator` — медиана трат за все дни периода до текущей даты.
- Реализовать `DeviationCalculator` — отклонение факта от медианы в абсолютном и процентном выражении.
- Реализовать `DailyBudgetRecomputeService` — пересчитывает `CalculatedLimit`, `Carryover`, `ActiveZoneId` при добавлении/изменении транзакции.

---

## Фаза 4 — Импорт транзакций

**Зависит от:** Фазы 1. Может реализовываться параллельно с Фазой 3.

Цель — подключить внешние каналы ввода транзакций.

```mermaid
flowchart TD
    subgraph F4["Фаза 4 — Importers"]
        IIMP[ITransactionImporter]
        MAN[ManualImporter\nSource = Manual]
        FILE[FileImporter\nSource = File]
        API[ApiImporter\nSource = Api]
        CSV[CsvParser]
        XLSX[XlsxParser]
        DEDUP[DuplicateDetector]
    end

    IIMP --> MAN
    IIMP --> FILE
    IIMP --> API
    FILE --> CSV
    FILE --> XLSX
    MAN & FILE & API --> DEDUP
```

Задачи:

- Реализовать `ManualImporter` (оборачивает одиночный ввод в `ImportResult`).
- Реализовать `CsvParser` и `XlsxParser`.
- Реализовать `FileImporter` с поддержкой CSV и XLSX.
- Реализовать заготовку `ApiImporter` (интерфейс + HTTP-клиент, конкретные источники добавляются позже).
- Реализовать `DuplicateDetector` — проверка по `(AccountId, Date, Amount, Type)`.

---

## Фаза 5 — Application слой

**Зависит от:** Фаз 1, 3 и 4.

Цель — оркестрировать бизнес-логику и репозитории через use-cases; подготовить DTOs для UI.

```mermaid
flowchart TD
    subgraph F5["Фаза 5 — Application"]
        AUC[AccountUseCase\nCRUD счетов]
        TUC[TransactionUseCase\nдобавить / импортировать]
        BUC[BudgetPeriodUseCase\nсоздать период · зоны · лимиты]
        SUC[StatisticsUseCase\nдашборд · медиана · отклонение]
        DTOS[DTOs\nDailyStatsDto · PeriodSummaryDto · ...]
    end

    AUC --> DTOS
    TUC --> DTOS
    BUC --> DTOS
    SUC --> DTOS
```

Задачи:

- Создать проект `Dtoriki.BudjetMaster.Application`.
- Реализовать `AccountUseCase`: создание, обновление, получение счетов.
- Реализовать `TransactionUseCase`: добавление одиночной транзакции, импорт пакета, список с фильтрацией.
- Реализовать `BudgetPeriodUseCase`: создание периода с мультивыбором счетов, настройка зон, пересчёт при изменениях.
- Реализовать `StatisticsUseCase`: `DailyStatsDto` (лимит, факт, медиана, отклонение, зона), `PeriodSummaryDto`.
- Зарегистрировать зависимости (DI).

---

## Фаза 6 — MAUI UI

**Зависит от:** Фазы 5.

Цель — реализовать пользовательский интерфейс на .NET MAUI по паттерну MVVM.

```mermaid
flowchart TD
    subgraph F6["Фаза 6 — MAUI"]
        direction TB
        NAV[Shell-навигация]

        subgraph Screens["Экраны"]
            DASH[DashboardPage\nлимит · зона · медиана · отклонение]
            TRANS[TransactionsPage\nсписок · фильтры]
            ADD[AddTransactionPage\nручной ввод]
            IMP[ImportPage\nфайл / API]
            PER[PeriodsPage\nсписок периодов]
            PER_NEW[CreatePeriodPage\nмультивыбор счетов · алгоритм]
            ZONE[ZoneConfigPage\nредактор зон]
            ACC[AccountsPage\nсписок · добавить]
        end

        NAV --> DASH & TRANS & PER & ACC
        TRANS --> ADD & IMP
        PER --> PER_NEW --> ZONE
    end
```

Задачи:

- Создать проект `Dtoriki.BudjetMaster.Maui`.
- Настроить Shell-навигацию.
- Реализовать `DashboardPage` + `DashboardViewModel`: цветовая зона, лимит, факт, медиана, отклонение, мини-график по дням.
- Реализовать `TransactionsPage` + фильтры по дате / счёту / типу.
- Реализовать `AddTransactionPage` (ручной ввод).
- Реализовать `ImportPage`: выбор файла или настройка API.
- Реализовать `PeriodsPage` + `CreatePeriodPage` (мультивыбор счетов).
- Реализовать `ZoneConfigPage`: добавление / удаление / редактирование зон.
- Реализовать `AccountsPage`.

---

## Фаза 7 — Тесты

**Ведётся параллельно** с каждой фазой. Тесты пишутся сразу после реализации.

| Что тестируется | Тип | Фреймворк |
|-----------------|-----|-----------|
| Доменные инварианты | Unit | xUnit |
| `ArithmeticMeanCalculator` | Unit | xUnit |
| `ZoneResolver` | Unit | xUnit |
| `MedianCalculator`, `DeviationCalculator` | Unit | xUnit |
| `DailyBudgetRecomputeService` | Unit | xUnit + Moq |
| `DuplicateDetector` | Unit | xUnit |
| Use-cases | Unit | xUnit + Moq |
| Репозитории | Integration | xUnit + Testcontainers (PostgreSQL) |
| Импортеры (CSV/XLSX) | Unit | xUnit |

Задачи:

- Создать `tests/Directory.Build.props` с общими зависимостями (xUnit, Moq, coverlet).
- Создать тестовые проекты для каждого слоя: `Domain.Tests`, `Application.Tests`, `Infrastructure.Tests`.
- Настроить `InternalsVisibleTo` там, где нужно тестировать `internal`-члены.
- Настроить Testcontainers для интеграционных тестов репозиториев.
- Настроить покрытие (coverlet + отчёт).

---

## Фаза 8 — Расширения

**После** завершения основного функционала (Фазы 1–7).

```mermaid
flowchart TD
    subgraph F8["Фаза 8 — Расширения"]
        SAV[Сберегательный счёт\nдоходность · цели]
        BRK[Брокерский счёт\nпортфель · позиции]
        APIX[Новые API-источники\nконкретные банки]
        ALGX[Дополнительные алгоритмы\nрасчёта лимита]
        NOTIF[Уведомления\nпревышение лимита · конец периода]
        EXPORT[Экспорт отчётов\nCSV · PDF]
    end

    SAV -.-> BRK
    APIX -.-> ALGX
```

Задачи:

- Реализовать `AccountType.Savings`: учёт доходности, цели накопления.
- Реализовать `AccountType.Broker`: портфель, позиции, P&L.
- Добавить конкретные реализации `ApiImporter` для популярных банков.
- Добавить альтернативные `ILimitCalculator` (например, взвешенный по дням недели).
- Добавить push-уведомления при превышении лимита или окончании периода.
- Добавить экспорт статистики в CSV и PDF.

---

## Зависимости между фазами

```mermaid
graph TD
    F1["Фаза 1\nДомен\n★ Старт"]
    F2["Фаза 2\nИнфраструктура"]
    F3["Фаза 3\nБизнес-логика"]
    F4["Фаза 4\nИмпорт"]
    F5["Фаза 5\nApplication"]
    F6["Фаза 6\nMAUI UI"]
    F7["Фаза 7\nТесты\n⟳ параллельно"]
    F8["Фаза 8\nРасширения"]

    F1 --> F2
    F1 --> F3
    F2 --> F3
    F1 --> F4
    F3 --> F5
    F4 --> F5
    F5 --> F6
    F6 --> F8
    F1 -.-> F7
    F3 -.-> F7
    F5 -.-> F7

    style F1 fill:#dbeafe,stroke:#3b82f6
    style F7 fill:#fef9c3,stroke:#eab308
    style F8 fill:#f3f4f6,stroke:#9ca3af
```

Фазы 3 и 4 могут реализовываться параллельно, так как не зависят друг от друга.
Тесты (Фаза 7) пишутся по мере реализации каждой фазы — не откладываются в конец.

---

*© Dtoriki.BudjetMaster — 2026.*
