# Dtoriki.BudjetMaster

Приложение для учёта бюджета: транзакции, расчётные периоды, ежедневные лимиты, аналитика отклонений.

**Платформа:** .NET 10 · Blazor WebAssembly · ASP.NET Core · PostgreSQL

---

## Содержание

- [Что делает приложение](#что-делает-приложение)
- [Стек технологий](#стек-технологий)
- [Структура репозитория](#структура-репозитория)
- [Документация](#документация)
- [Запуск](#запуск)

---

## Что делает приложение

- Хранит транзакции из нескольких источников: ручной ввод, импорт из файла, импорт через API.
- Ведёт расчётные периоды с бюджетным лимитом и ежедневным пересчётом остатков.
- Показывает отклонение фактических трат от медианы за период.
- Разбивает траты на настраиваемые зоны (число зон, названия и пороги — произвольные).
- Поддерживает несколько счетов.

---

## Стек технологий

| Слой | Технология |
|------|-----------|
| Веб-клиент | Blazor WebAssembly (.NET 10) |
| API | ASP.NET Core 10 |
| Авторизация | ASP.NET Core Identity + OpenIddict 5.x |
| База данных | PostgreSQL 16 |
| ORM | Entity Framework Core 10 |
| Тесты | xUnit, Moq |

---

## Структура репозитория

```
Dtoriki.BudjetMaster/
├── src/
│   ├── libs/
│   │   └── Dtoriki.Data.Core/           — базовые EF Core абстракции
│   └── apps/
│       ├── Dtoriki.BudjetMaster.Auth/   — сервер авторизации (OpenIddict)
│       ├── Dtoriki.BudjetMaster.Api/    — REST API
│       └── Dtoriki.BudjetMaster.Web/    — Blazor WebAssembly клиент
├── tests/
├── docs/
│   └── architecture/
│       └── auth-server.md
├── Directory.Build.props
├── Directory.Packages.props
├── Dtoriki.BudjetMaster.slnx
└── README.md
```

---

## Документация

- [Архитектура сервера авторизации](./docs/architecture/auth-server.md)

---

## Запуск

> Инструкция будет дополнена по мере реализации.

Предварительные требования: .NET 10 SDK, PostgreSQL 16.

---

*© Dtoriki.BudjetMaster — 2026.*
