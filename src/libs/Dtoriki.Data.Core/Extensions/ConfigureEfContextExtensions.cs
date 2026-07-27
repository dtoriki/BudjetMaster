using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Dtoriki.Data.Core.Context;

namespace Dtoriki.Data.Core.Extensions;

/// <summary>
/// Предоставляет методы расширения для регистрации и конфигурации контекстов Entity Framework Core в контейнере внедрения зависимостей.
/// </summary>
/// <remarks>
/// Методы выполняют проверку аргументов и наличия подходящего публичного конструктора контекста.
/// </remarks>
/// <exception cref="ArgumentNullException"/>
/// <exception cref="ArgumentException"/>
/// <exception cref="InvalidOperationException"/>
public static class ConfigureEfContextExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Регистрирует и конфигурирует контекст Entity Framework Core.
        /// </summary>
        /// <typeparam name="TContext">Тип контекста.</typeparam>
        /// <param name="configure">Делегат конфигурации параметров контекста.</param>
        /// <returns>Возвращает коллекцию сервисов для цепочки вызовов.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается если <paramref name="configure"/> равен <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Выбрасывается если отсутствует публичный конструктор с единственным параметром типа <see cref="DbContextOptions"/> или <see cref="DbContextOptions{TContext}"/>.</exception>
        public IServiceCollection ConfigureEfContext<TContext>(Action<DbContextOptionsBuilder> configure)
            where TContext : DbContext, IEfContext
        {
            ArgumentNullException.ThrowIfNull(configure);
            EnsureContextConstructorExists<TContext>();

            services.AddDbContext<TContext>(
                (provider, builder) =>
                {
                    configure(builder);
                });

            return services;
        }

        /// <summary>
        /// Регистрирует и конфигурирует контекст Entity Framework Core с использованием ключа.
        /// </summary>
        /// <typeparam name="TContext">Тип контекста.</typeparam>
        /// <param name="key">Ключ зависимости.</param>
        /// <param name="configure">Делегат конфигурации параметров контекста.</param>
        /// <returns>Возвращает коллекцию сервисов для цепочки вызовов.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается если <paramref name="configure"/> равен <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Выбрасывается если <paramref name="key"/> пустой или состоит из пробелов.</exception>
        /// <exception cref="InvalidOperationException">Выбрасывается если отсутствует публичный конструктор подходящей сигнатуры либо невозможно создать экземпляр типа.</exception>
        public IServiceCollection ConfigureEfContextKeyed<TContext>(string key, Action<DbContextOptionsBuilder> configure)
            where TContext : DbContext, IEfContext
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            EnsureContextConstructorExists<TContext>();

            services.TryAddKeyedSingleton(
                key,
                (sp, k) =>
                {
                    DbContextOptionsBuilder builder = new();
                    configure(builder);

                    return builder.Options;
                });

            services.TryAddKeyedSingleton(
                key,
                (sp, k) =>
                {
                    DbContextOptionsBuilder<TContext> builder = new();
                    configure(builder);

                    return builder.Options;
                });

            services.TryAddKeyedScoped(
                key,
                (sp, k) =>
                {
                    DbContextOptions options = sp.GetRequiredKeyedService<DbContextOptions>(k);

                    object? instance = Activator.CreateInstance(typeof(TContext), options)
                        ?? throw new InvalidOperationException(
                            $"Тип {typeof(TContext).FullName} не может быть создан. Ожидается публичный конструктор с параметром {nameof(DbContextOptions)}.");

                    return (TContext)instance;
                });

            return services;
        }

        /// <summary>
        /// Регистрирует реализацию контекста как абстракцию.
        /// </summary>
        /// <typeparam name="TAbstract">Абстрактный (интерфейсный/базовый) тип контекста.</typeparam>
        /// <typeparam name="TContext">Реализация контекста.</typeparam>
        /// <returns>Возвращает коллекцию сервисов для цепочки вызовов.</returns>
        /// <exception cref="InvalidOperationException">Выбрасывается если <typeparamref name="TContext"/> не реализует <typeparamref name="TAbstract"/> или приведение невозможно.</exception>
        public IServiceCollection TryAddAbstractEfContext<TAbstract, TContext>()
            where TAbstract : class, IEfContext
            where TContext : DbContext
        {
            if (!typeof(TAbstract).IsAssignableFrom(typeof(TContext)))
            {
                throw new InvalidOperationException($"Тип {typeof(TContext).FullName} не реализует {typeof(TAbstract).FullName}.");
            }

            services.TryAddScoped(
                sp =>
                {
                    TContext context = sp.GetRequiredService<TContext>();
                    if (context is not TAbstract casted)
                    {
                        throw new InvalidOperationException($"Невозможно привести {typeof(TContext).FullName} к {typeof(TAbstract).FullName}.");
                    }

                    return casted;
                });

            return services;
        }

        /// <summary>
        /// Регистрирует реализацию контекста как абстракцию с использованием ключа.
        /// </summary>
        /// <typeparam name="TAbstract">Абстрактный (интерфейсный/базовый) тип контекста.</typeparam>
        /// <typeparam name="TContext">Реализация контекста.</typeparam>
        /// <param name="key">Ключ регистрации.</param>
        /// <returns>Возвращает коллекцию сервисов для цепочки вызовов.</returns>
        /// <exception cref="ArgumentException">Выбрасывается если <paramref name="key"/> пустой или состоит из пробелов.</exception>
        public IServiceCollection TryAddAbstractEfContextKeyed<TAbstract, TContext>(string key)
            where TAbstract : class, IEfContext
            where TContext : DbContext, TAbstract
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            services.TryAddKeyedScoped<TAbstract>(
                key,
                (sp, k) =>
                {
                    TContext? keyed = sp.GetKeyedService<TContext>(k);
                    if (keyed != null)
                    {
                        return keyed;
                    }

                    return sp.GetRequiredService<TContext>();
                });

            return services;
        }
    }

    private static void EnsureContextConstructorExists<TContext>()
        where TContext : DbContext
    {
        Type type = typeof(TContext);

        bool hasCtor = type
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Any(
                c =>
                {
                    ParameterInfo[] ps = c.GetParameters();
                    if (ps.Length != 1)
                    {
                        return false;
                    }

                    Type p = ps[0].ParameterType;

                    return p == typeof(DbContextOptions) || p == typeof(DbContextOptions<TContext>);
                });

        if (!hasCtor)
        {
            throw new InvalidOperationException($"Тип {type.FullName} должен иметь публичный конструктор с единственным параметром типа {nameof(DbContextOptions)} или {nameof(DbContextOptions<TContext>)}.");
        }
    }
}
