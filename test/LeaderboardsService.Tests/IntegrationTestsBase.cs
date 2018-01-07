using System;
using System.Configuration;
using System.IO;
using Microsoft.EntityFrameworkCore;
using toofz.Data;
using toofz.Services.LeaderboardsService.Properties;
using Xunit;

namespace toofz.Services.LeaderboardsService.Tests
{
    [Trait("Category", "Uses SQL Server")]
    [Trait("Category", "Uses file system")]
    [Collection("Uses SQL Server and file system")]
    public abstract class IntegrationTestsBase : IDisposable
    {
        public IntegrationTestsBase()
        {
            settings = Settings.Default;
            // Should only loop once
            foreach (SettingsProvider provider in settings.Providers)
            {
                var ssp = (ServiceSettingsProvider)provider;
                ssp.GetSettingsReader = () => File.OpenText(settingsFileName);
                ssp.GetSettingsWriter = () => File.CreateText(settingsFileName);
            }

            var options = new DbContextOptionsBuilder<NecroDancerContext>()
                .UseSqlServer(databaseConnectionString)
                .Options;

            db = new NecroDancerContext(options);
            db.Database.EnsureDeleted();
            db.Database.Migrate();
        }

        internal readonly Settings settings;
        private readonly string settingsFileName = Path.GetTempFileName();
        protected readonly string databaseConnectionString = StorageHelper.GetDatabaseConnectionString(Constants.NecroDancerContextName);
        protected readonly NecroDancerContext db;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (File.Exists(settingsFileName)) { File.Delete(settingsFileName); }
                db.Database.EnsureDeleted();
            }
        }
    }
}
