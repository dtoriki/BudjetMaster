# Архитектура сервера авторизации — Dtoriki.BudjetMaster

**Платформа:** .NET 10 · ASP.NET Core Identity · OpenIddict · PostgreSQL

[← Назад к README](../../README.md)

---

## Содержание

- [Мотивация и цели](#мотивация-и-цели)
- [Стек технологий](#стек-технологий)
- [Обзор системы](#обзор-системы)
- [Компоненты auth-сервера](#компоненты-auth-сервера)
- [OAuth 2.0 / OIDC потоки](#oauth-20--oidc-потоки)
  - [Authorization Code + PKCE](#authorization-code--pkce)
  - [Refresh Token](#refresh-token)
- [Жизненный цикл токенов](#жизненный-цикл-токенов)
- [Структура проекта](#структура-проекта)
- [Схема базы данных](#схема-базы-данных)
- [Интеграция с API](#интеграция-с-api)
- [Конфигурация клиентов](#конфигурация-клиентов)
- [Ограничения и допущения](#ограничения-и-допущения)

---

## Мотивация и цели

Основной клиент BudjetMaster — Blazor WebAssembly. В будущем возможно подключение .NET MAUI
и сторонних интеграций. Все клиенты должны проходить аутентификацию через единую точку входа
без дублирования логики авторизации в каждом сервисе.

Цели auth-сервера:

- Единая точка выдачи токенов для всех клиентов (Blazor WASM, CLI, в будущем MAUI).
- Поддержка стандартных протоколов OAuth 2.0 + OpenID Connect.
- Управление пользователями через ASP.NET Core Identity.
- Масштабируемость: добавление нового клиента — только регистрация нового `client_id`.

---

## Стек технологий

| Компонент | Технология | Назначение |
|-----------|-----------|------------|
| Веб-клиент | Blazor WebAssembly (.NET 10) | Основной клиент |
| Фреймворк auth-сервера | ASP.NET Core 10 | Хост сервера авторизации |
| Управление пользователями | ASP.NET Core Identity | Хранение, хэши паролей, роли |
| OAuth 2.0 / OIDC сервер | OpenIddict 5.x | Выдача и валидация токенов |
| OIDC-клиент (WASM) | `Microsoft.AspNetCore.Components.WebAssembly.Authentication` | PKCE, хранение токенов |
| Хранилище | PostgreSQL + EF Core 10 | Пользователи, приложения, токены |
| Токены | JWT (access) + opaque (refresh) | Стандартные форматы |

---

## Обзор системы

Auth-сервер — отдельное ASP.NET Core-приложение. API-сервер и клиенты с ним не связаны
напрямую: они общаются только через стандартные OIDC-эндпоинты.

```mermaid
graph TD
    subgraph Clients["Клиенты"]
        WASM["Blazor WebAssembly<br/>(основной клиент)"]
        CLI["CLI / скрипты<br/>(Client Credentials)"]
        MAUI["MAUI<br/>(будущее, опционально)"]
    end

    subgraph AuthServer["Dtoriki.BudjetMaster.Auth<br/>(отдельное приложение)"]
        OIDC["OIDC Endpoints<br/>/connect/token<br/>/connect/authorize<br/>/connect/userinfo<br/>/.well-known/openid-configuration"]
        ID["ASP.NET Core Identity<br/>UserManager · SignInManager"]
        OID["OpenIddict<br/>Authorization · Token · Introspection"]
        AUTHDB[("PostgreSQL<br/>Users · Roles<br/>Applications · Tokens")]
    end

    subgraph API["Dtoriki.BudjetMaster.Api<br/>(отдельное приложение)"]
        MW["JWT Bearer Middleware"]
        CTRL["Controllers / Endpoints"]
    end

    WASM -->|"Authorization Code + PKCE"| OIDC
    CLI  -->|"Client Credentials"| OIDC
    MAUI -.->|"Authorization Code + PKCE<br/>(будущее)"| OIDC

    OIDC --> ID
    OIDC --> OID
    OID  --> AUTHDB
    ID   --> AUTHDB

    WASM -->|"Bearer access_token"| MW
    MW   -->|"Валидация JWT"| OIDC
    MW   --> CTRL

    style AuthServer fill:#dbeafe,stroke:#3b82f6
    style API fill:#dcfce7,stroke:#22c55e
    style Clients fill:#fce7f3,stroke:#ec4899
    style MAUI fill:#f3f4f6,stroke:#9ca3af,stroke-dasharray:5 5
```

---

## Компоненты auth-сервера

```mermaid
graph TD
    subgraph AuthApp["Dtoriki.BudjetMaster.Auth"]
        direction TB

        subgraph Endpoints["OIDC Endpoints (OpenIddict)"]
            EP_TOKEN["/connect/token<br/>выдача access/refresh токенов"]
            EP_AUTH["/connect/authorize<br/>старт Authorization Code flow"]
            EP_INFO["/connect/userinfo<br/>данные пользователя по токену"]
            EP_META["/.well-known/openid-configuration<br/>метаданные сервера"]
            EP_REVOKE["/connect/logout<br/>отзыв сессии"]
        end

        subgraph Identity["ASP.NET Core Identity"]
            UM["UserManager<br/>CRUD пользователей"]
            SM["SignInManager<br/>проверка пароля · 2FA"]
            RM["RoleManager<br/>роли и claims"]
        end

        subgraph OpenIddict["OpenIddict Core"]
            AM["ApplicationManager<br/>реестр клиентов"]
            TM["TokenManager<br/>выдача · отзыв токенов"]
            AuthM["AuthorizationManager<br/>сессии авторизации"]
        end

        subgraph Store["EF Core Store"]
            CTX["AuthDbContext"]
            T_USR["AspNetUsers"]
            T_APP["OpenIddictApplications"]
            T_TOK["OpenIddictTokens"]
            T_AUTH["OpenIddictAuthorizations"]
            T_SCP["OpenIddictScopes"]
        end

        EP_TOKEN --> SM
        EP_AUTH  --> SM
        EP_INFO  --> UM
        SM       --> UM
        UM       --> CTX
        AM       --> CTX
        TM       --> CTX
        AuthM    --> CTX
        CTX      --> T_USR & T_APP & T_TOK & T_AUTH & T_SCP
    end

    style Identity fill:#fef9c3,stroke:#eab308
    style OpenIddict fill:#dbeafe,stroke:#3b82f6
    style Store fill:#f3f4f6,stroke:#9ca3af
```

---

## OAuth 2.0 / OIDC потоки

### Authorization Code + PKCE

Blazor WASM — публичный клиент. `client_secret` отсутствует, PKCE обязателен.
Библиотека `Microsoft.AspNetCore.Components.WebAssembly.Authentication` генерирует `code_verifier`
и управляет редиректами автоматически.

```mermaid
sequenceDiagram
    actor User as Пользователь
    participant App as Blazor WASM
    participant Auth as Auth Server
    participant Api as BudjetMaster API

    Note over App: Генерирует code_verifier (случайная строка)<br/>code_challenge = BASE64URL(SHA256(code_verifier))

    App->>Auth: GET /connect/authorize<br/>?response_type=code<br/>&client_id=budjetmaster-web<br/>&redirect_uri=https://app.budjetmaster.local/authentication/login-callback<br/>&scope=openid profile api<br/>&code_challenge=...&code_challenge_method=S256

    Auth->>User: Показать форму входа
    User->>Auth: Ввести логин / пароль

    Auth-->>App: Redirect → /authentication/login-callback<br/>?code=AUTH_CODE

    App->>Auth: POST /connect/token<br/>grant_type=authorization_code<br/>&code=AUTH_CODE<br/>&code_verifier=CODE_VERIFIER<br/>&redirect_uri=https://app.budjetmaster.local/authentication/login-callback

    Note over Auth: Проверяет code_verifier против code_challenge<br/>Выдаёт токены

    Auth-->>App: { access_token, refresh_token,<br/>id_token, expires_in }

    Note over App: Токены хранятся в памяти браузера<br/>(управляется фреймворком, не localStorage)

    App->>Api: GET /api/transactions<br/>Authorization: Bearer access_token

    Api-->>App: 200 OK · данные
```

---

### Refresh Token

Когда `access_token` истёк, фреймворк Blazor WASM обменивает `refresh_token` автоматически,
прозрачно для компонентов. При обновлении страницы пользователь проходит повторную аутентификацию
(токены не сохраняются между сессиями браузера).

```mermaid
sequenceDiagram
    participant App as Blazor WASM
    participant Auth as Auth Server
    participant Api as BudjetMaster API

    App->>Api: GET /api/transactions<br/>Authorization: Bearer EXPIRED_TOKEN
    Api-->>App: 401 Unauthorized

    Note over App: WebAssembly.Authentication перехватывает 401<br/>и инициирует обновление токена

    App->>Auth: POST /connect/token<br/>grant_type=refresh_token<br/>&refresh_token=STORED_REFRESH_TOKEN<br/>&client_id=budjetmaster-web

    Auth-->>App: { access_token (новый),<br/>refresh_token (ротация),<br/>expires_in }

    App->>Api: GET /api/transactions<br/>Authorization: Bearer NEW_ACCESS_TOKEN
    Api-->>App: 200 OK · данные
```

---

## Жизненный цикл токенов

```mermaid
stateDiagram-v2
    [*] --> Issued : POST /connect/token

    Issued --> Valid : expires_in > 0
    Valid --> Expired : время истекло

    Expired --> Refreshed : обмен refresh_token
    Refreshed --> Valid : новый access_token

    Valid --> Revoked : POST /connect/logout
    Expired --> Revoked : refresh_token отозван
    Refreshed --> Revoked : пользователь вышел со всех устройств

    Revoked --> [*]

    note right of Valid
        access_token TTL: 1 час
        refresh_token TTL: 30 дней
        Ротация: refresh_token обновляется
        при каждом использовании
    end note
```

---

## Структура проекта

```
src/
└── apps/
    ├── Dtoriki.BudjetMaster.Auth/          ← Auth Server (отдельное приложение)
    │   ├── Controllers/
    │   │   └── AuthorizationController.cs  ← обработка /connect/* эндпоинтов
    │   ├── Data/
    │   │   └── AuthDbContext.cs            ← Identity + OpenIddict таблицы
    │   ├── Models/
    │   │   └── ApplicationUser.cs          ← расширение IdentityUser
    │   ├── Seed/
    │   │   └── ClientSeeder.cs             ← регистрация клиентских приложений
    │   ├── appsettings.json
    │   └── Program.cs
    │
    ├── Dtoriki.BudjetMaster.Api/           ← Resource API (отдельное приложение)
    │   ├── Program.cs                      ← AddAuthentication().AddJwtBearer(...)
    │   └── ...
    │
    └── Dtoriki.BudjetMaster.Web/           ← Blazor WebAssembly (основной клиент)
        ├── Pages/
        ├── Shared/
        ├── wwwroot/
        ├── Program.cs                      ← AddOidcAuthentication(...)
        └── ...
```

---

## Схема базы данных

Auth-сервер использует отдельную PostgreSQL-базу (или отдельную схему `auth`).

```mermaid
erDiagram
    AspNetUsers {
        uuid Id PK
        text UserName
        text Email
        text PasswordHash
        text NormalizedEmail
        bool EmailConfirmed
    }

    AspNetRoles {
        uuid Id PK
        text Name
    }

    AspNetUserRoles {
        uuid UserId FK
        uuid RoleId FK
    }

    OpenIddictApplications {
        uuid Id PK
        text ClientId
        text ClientSecret
        text DisplayName
        text RedirectUris
        text Permissions
        text Type
    }

    OpenIddictTokens {
        uuid Id PK
        uuid ApplicationId FK
        uuid AuthorizationId FK
        uuid SubjectId FK
        text Type
        text Status
        text Payload
        timestamp ExpirationDate
    }

    OpenIddictAuthorizations {
        uuid Id PK
        uuid ApplicationId FK
        text Subject
        text Status
        text Scopes
    }

    OpenIddictScopes {
        uuid Id PK
        text Name
        text Resources
    }

    AspNetUsers ||--o{ AspNetUserRoles : "имеет"
    AspNetRoles ||--o{ AspNetUserRoles : "включает"
    OpenIddictApplications ||--o{ OpenIddictTokens : "выдаёт"
    OpenIddictApplications ||--o{ OpenIddictAuthorizations : "авторизует"
    OpenIddictAuthorizations ||--o{ OpenIddictTokens : "порождает"
```

---

## Интеграция с API

API-сервер не хранит пользователей — он только валидирует JWT, выданные auth-сервером.
Взаимодействие происходит через стандартный OIDC Discovery endpoint.

```mermaid
sequenceDiagram
    participant Api as BudjetMaster API
    participant Auth as Auth Server

    Note over Api: Старт приложения
    Api->>Auth: GET /.well-known/openid-configuration
    Auth-->>Api: { issuer, jwks_uri, token_endpoint, ... }

    Api->>Auth: GET /.well-known/jwks
    Auth-->>Api: { keys: [ RSA public key ] }

    Note over Api: Кешировать public keys

    Note over Api: Входящий запрос от клиента
    Api->>Api: Validate JWT signature (local, без сети)
    Api->>Api: Проверить iss, aud, exp claims
    Api->>Api: Извлечь sub (userId), roles, scopes
```

Конфигурация в `Program.cs` API-сервера:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.budjetmaster.local";
        options.Audience  = "budjetmaster-api";
        options.TokenValidationParameters = new()
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
        };
    });
```

Конфигурация в `Program.cs` Blazor WASM:

```csharp
builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority    = "https://auth.budjetmaster.local";
    options.ProviderOptions.ClientId     = "budjetmaster-web";
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("api");
});
```

---

## Конфигурация клиентов

Клиенты регистрируются через `ClientSeeder` при старте auth-сервера.

```csharp
// Blazor WASM — публичный клиент (без secret), Authorization Code + PKCE
await manager.CreateAsync(new OpenIddictApplicationDescriptor
{
    ClientId    = "budjetmaster-web",
    DisplayName = "BudjetMaster Web",
    ClientType  = ClientTypes.Public,
    RedirectUris =
    {
        new Uri("https://app.budjetmaster.local/authentication/login-callback"),
        new Uri("https://localhost:5001/authentication/login-callback"), // локальная отладка
    },
    PostLogoutRedirectUris =
    {
        new Uri("https://app.budjetmaster.local/authentication/logout-callback"),
    },
    Permissions =
    {
        Permissions.Endpoints.Authorization,
        Permissions.Endpoints.Token,
        Permissions.GrantTypes.AuthorizationCode,
        Permissions.GrantTypes.RefreshToken,
        Permissions.ResponseTypes.Code,
        Permissions.Scopes.OpenId,
        Permissions.Scopes.Profile,
        Permissions.Prefixes.Scope + "api",
    },
    Requirements = { Requirements.Features.ProofKeyForCodeExchange },
}, cancellationToken);

// Машинный клиент (CLI / фоновые задачи) — Client Credentials
await manager.CreateAsync(new OpenIddictApplicationDescriptor
{
    ClientId     = "budjetmaster-cli",
    ClientSecret = "<generated-secret>",
    DisplayName  = "BudjetMaster CLI",
    ClientType   = ClientTypes.Confidential,
    Permissions  =
    {
        Permissions.Endpoints.Token,
        Permissions.GrantTypes.ClientCredentials,
        Permissions.Prefixes.Scope + "api",
    },
}, cancellationToken);
```

---

## Ограничения и допущения

| Область | Ограничение |
|---------|------------|
| Implicit flow | Не используется — устарел в OAuth 2.1 |
| `client_secret` у Blazor WASM | Отсутствует — браузер не может безопасно хранить секрет |
| Хранение токенов | Управляется фреймворком в памяти браузера — не использовать `localStorage` напрямую (XSS) |
| Обновление страницы | Токены не переживают перезагрузку — пользователь повторно аутентифицируется (можно смягчить через silent refresh) |
| Несколько устройств / вкладок | Каждая сессия получает свой `refresh_token`; отзыв одного не влияет на остальные |
| 2FA | Не реализовано в первой фазе; `SignInManager` поддерживает расширение |
| Внешние провайдеры (Google, Apple) | Не реализовано; OpenIddict поддерживает через `AddGoogle()` / `AddApple()` |
| MAUI | Будущее; потребует регистрации отдельного `client_id` с redirect URI `budjetmaster://callback` |
| Отдельная БД | Auth-сервер использует отдельную базу (или схему `auth`) — не смешивать с бизнес-данными |

---

*© Dtoriki.BudjetMaster — 2026.*
