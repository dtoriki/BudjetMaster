# Архитектура Dtoriki.BudjetMaster

**Платформа:** .NET 10 · MAUI · PostgreSQL · EF Core 10

[← Назад к README](../README.md)

---

## Содержание

- [Слои приложения](#слои-приложения)
- [Компонентная диаграмма](#компонентная-диаграмма)
- [Источники транзакций](#источники-транзакций)
- [Алгоритмы расчёта лимита](#алгоритмы-расчёта-лимита)
- [Типы счетов](#типы-счетов)
- [Поток данных](#поток-данных)

---

## Слои приложения

Проект построен по **Clean Architecture**: зависимости направлены строго внутрь — к слою Domain.

```mermaid
graph TD
    subgraph Presentation["Presentation (MAUI)"]
        UI[MAUI Views]
        VM[ViewModels]
    end

    subgraph Application["Application"]
        UC[Use Cases]
        DTO[DTOs]
        ISVC[Interfaces сервисов]
    end

    subgraph Domain["Domain"]
        ENT[Entities]
        VO[Value Objects]
        Irepo[Interfaces репозиториев]
        ILIM[ILimitCalculator]
        IIMP[ITransactionImporter]
    end

    subgraph Infrastructure["Infrastructure"]
        EF[EF Core / PostgreSQL]
        REPO[Репозитории]
        IMP_MAN[ManualImporter]
        IMP_API[ApiImporter]
        IMP_FILE[FileImporter]
        CALC[ArithmeticMeanCalculator]
    end

    UI --> VM
    VM --> UC
    UC --> ISVC
    UC --> Irepo
    UC --> ILIM
    UC --> IIMP
    ISVC --> ENT
    Irepo --> ENT
    REPO --> EF
    REPO -.implements.-> Irepo
    IMP_MAN -.implements.-> IIMP
    IMP_API -.implements.-> IIMP
    IMP_FILE -.implements.-> IIMP
    CALC -.implements.-> ILIM

    style Domain fill:#dbeafe,stroke:#3b82f6
    style Application fill:#dcfce7,stroke:#22c55e
    style Infrastructure fill:#fef9c3,stroke:#eab308
    style Presentation fill:#fce7f3,stroke:#ec4899
```

---

## Компонентная диаграмма

```mermaid
graph LR
    subgraph Client["MAUI Client"]
        DASH[Dashboard]
        TRANS[Транзакции]
        PERIOD[Периоды]
        ACCT[Счета]
        SETT[Настройки]
    end

    subgraph AppCore["Application Core"]
        TUS[TransactionUseCase]
        BUS[BudgetPeriodUseCase]
        AUS[AccountUseCase]
        STAT[StatisticsUseCase]
    end

    subgraph Importers["Источники транзакций"]
        MAN[Ручной ввод]
        API[Внешний API]
        FILE[Файл CSV/XLSX]
    end

    subgraph Calculators["Алгоритмы лимита"]
        AMC[ArithmeticMean\nCalc]
        FUTURE[FutureCalc...]
    end

    subgraph DB["PostgreSQL"]
        T_ACC[accounts]
        T_TRX[transactions]
        T_PER[budget_periods]
        T_DAY[daily_budgets]
        T_ZONE[zone_configs]
    end

    DASH --> STAT
    TRANS --> TUS
    PERIOD --> BUS
    ACCT --> AUS

    MAN --> TUS
    API --> TUS
    FILE --> TUS

    BUS --> AMC
    BUS --> FUTURE

    TUS --> T_TRX
    BUS --> T_PER
    BUS --> T_DAY
    AUS --> T_ACC
    STAT --> T_TRX
    STAT --> T_DAY
```

---

## Источники транзакций

Все каналы ввода реализуют единый интерфейс `ITransactionImporter`. Добавление нового канала (например, импорт из банка через Open Banking API) не затрагивает доменный слой.

```mermaid
classDiagram
    class ITransactionImporter {
        <<interface>>
        +ImportAsync(context, cancellationToken) Task~ImportResult~
        +SourceType TransactionSource
    }

    class ManualImporter {
        +SourceType = Manual
        +ImportAsync()
    }

    class ApiImporter {
        -HttpClient _client
        -string _baseUrl
        +SourceType = Api
        +ImportAsync()
    }

    class FileImporter {
        -IFileParser _parser
        +SourceType = File
        +ImportAsync()
    }

    class CsvParser {
        +Parse(stream) IEnumerable~RawTransaction~
    }

    class XlsxParser {
        +Parse(stream) IEnumerable~RawTransaction~
    }

    ITransactionImporter <|.. ManualImporter
    ITransactionImporter <|.. ApiImporter
    ITransactionImporter <|.. FileImporter
    FileImporter --> CsvParser
    FileImporter --> XlsxParser
```

---

## Алгоритмы расчёта лимита

Стратегия расчёта ежедневного лимита абстрагирована через `ILimitCalculator`. Первая реализация — `ArithmeticMeanCalculator`.

```mermaid
classDiagram
    class ILimitCalculator {
        <<interface>>
        +Calculate(context) decimal
        +CalculatorType string
    }

    class LimitCalculatorContext {
        +TotalBudget decimal
        +PeriodStart DateOnly
        +PeriodEnd DateOnly
        +CurrentDate DateOnly
        +DailySpending Dictionary~DateOnly, decimal~
    }

    class ArithmeticMeanCalculator {
        +CalculatorType = "ArithmeticMean"
        +Calculate(context) decimal
    }

    note for ArithmeticMeanCalculator "Формула:\n(TotalBudget / TotalDays)\n+ UnspentPrevious / RemainingDays"

    ILimitCalculator <|.. ArithmeticMeanCalculator
    ILimitCalculator ..> LimitCalculatorContext : использует
```

**Формула ArithmeticMeanCalculator:**

```
BaseLimit = TotalBudget / TotalDays
UnspentPrevious = Σ max(0, DailyLimit[d] - Spending[d])  для d < CurrentDate
RemainingDays = (PeriodEnd - CurrentDate).Days + 1

DailyLimit = BaseLimit + UnspentPrevious / RemainingDays
```

---

## Типы счетов

Тип счёта влияет на доступные операции и аналитику. Брокерские и сберегательные счета добавляются без изменения существующих сущностей.

```mermaid
graph TD
    ACC[Account] --> CHK[Checking\nРасчётный]
    ACC --> SAV[Savings\nСберегательный]
    ACC --> BRK[Broker\nБрокерский]

    CHK -->|участвует в| PERIOD[BudgetPeriod]
    CHK -->|содержит| TRX[Transaction]
    SAV -->|содержит| TRX
    SAV -.->|будущее: доходность| YIELD[YieldRecord]
    BRK -->|содержит| TRX
    BRK -.->|будущее: позиции| POS[PortfolioPosition]

    style SAV fill:#fef9c3
    style BRK fill:#fef9c3
    style YIELD fill:#f3f4f6,stroke-dasharray:5 5
    style POS fill:#f3f4f6,stroke-dasharray:5 5
```

---

## Поток данных

Полный цикл от ввода транзакции до отображения на дашборде.

```mermaid
sequenceDiagram
    actor User as Пользователь
    participant UI as MAUI UI
    participant UC as TransactionUseCase
    participant IMP as ITransactionImporter
    participant REPO as TransactionRepository
    participant STAT as StatisticsUseCase
    participant CALC as ILimitCalculator
    participant DB as PostgreSQL

    User->>UI: Добавить / импортировать транзакцию
    UI->>UC: AddTransactionsAsync(source, data)
    UC->>IMP: ImportAsync(context)
    IMP-->>UC: ImportResult (транзакции)
    UC->>REPO: SaveAsync(transactions)
    REPO->>DB: INSERT transactions

    User->>UI: Открыть дашборд
    UI->>STAT: GetDailyStatsAsync(periodId, date)
    STAT->>DB: SELECT transactions, daily_budgets
    STAT->>CALC: Calculate(context)
    CALC-->>STAT: dailyLimit
    STAT-->>UI: DailyStatsDto (лимит, факт, медиана, зона)
    UI-->>User: Дашборд с цветовыми зонами
```

---

*© Dtoriki.BudjetMaster — 2026.*
