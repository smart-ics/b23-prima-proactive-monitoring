using System.Reflection;
using DbUp;

namespace PrIMA.Api.Migrations;

public static class MigrationRunner
{
    public static void Run(string connectionString)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException($"Database migration failed: {result.Error.Message}", result.Error);
        }
    }
}
