using System;
using System.Configuration;
using System.Data.Entity.Infrastructure;

namespace toofz.Services.LeaderboardsService.Tests
{
    internal static class StorageHelper
    {
        private const string ProjectName = "LeaderboardsService";

        public static string GetDatabaseConnectionString(string name)
        {
            var baseName = $"Test{ProjectName}{name}";
            var connectionString = GetConnectionString(baseName);
            if (connectionString != null) { return connectionString; }

            var connectionFactory = new LocalDbConnectionFactory("mssqllocaldb");
            using (var connection = connectionFactory.CreateConnection(baseName))
            {
                return connection.ConnectionString;
            }
        }

        private static string GetConnectionString(string baseName)
        {
            return Environment.GetEnvironmentVariable($"{baseName}ConnectionString", EnvironmentVariableTarget.Machine) ??
                   ConfigurationManager.ConnectionStrings[baseName]?.ConnectionString;
        }
    }
}
