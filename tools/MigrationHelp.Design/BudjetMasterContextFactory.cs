using Dtoriki.BudjetMaster.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MigrationHelp.Common;

namespace MigrationHelp.Design;

/// <summary>
/// Фабрика контекста dev-time для <see cref="BudjetMasterDbContext"/>.
/// Используется инструментами EF Core при выполнении миграций и при создании экземпляра
/// контекста вне DI-контейнера (design-time).
/// </summary>
[DbMigrationContextInfo(nameof(BudjetMasterDbContext), "Dtoriki.BudjetMaster.Infrastructure.Migrations")]
public sealed class BudjetMasterContextFactory : IDesignTimeDbContextFactory<BudjetMasterDbContext>
{
    private const string DESIGN_TIME_CONNECTION_STRING = "Data Source=design_time.db3";

    /// <inheritdoc/>
    public BudjetMasterDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<BudjetMasterDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(
            DESIGN_TIME_CONNECTION_STRING,
            x => x.MigrationsAssembly("Dtoriki.BudjetMaster.Infrastructure.Migrations"));

        return new BudjetMasterDbContext(optionsBuilder.Options);
    }
}
