using System;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.Services;
using Xunit;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
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

            db = new LeaderboardsContext(databaseConnectionString);
            db.Database.Delete(); // Make sure it really dropped - needed for dirty database
            Database.SetInitializer(new LeaderboardsContextInitializer());
            db.Database.Initialize(force: true);
            Database.SetInitializer(new NullDatabaseInitializer<LeaderboardsContext>());
        }

        internal readonly Settings settings;
        private readonly string settingsFileName = Path.GetTempFileName();
        protected readonly string databaseConnectionString = StorageHelper.GetDatabaseConnectionString();
        protected readonly LeaderboardsContext db;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (File.Exists(settingsFileName)) { File.Delete(settingsFileName); }
                db.Database.Delete();
            }
        }
    }
}
