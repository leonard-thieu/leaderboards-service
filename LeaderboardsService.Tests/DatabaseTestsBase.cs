using System;
using System.Data.Entity;
using Xunit;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    // This class handles database initialization and cleanup for ALL database-related tests (including tests that 
    // don't use LeaderboardsContext).
    [Trait("Category", "Uses SQL Server")]
    [Collection("Uses SQL Server")]
    public abstract class DatabaseTestsBase : IDisposable
    {
        public DatabaseTestsBase(bool initialize = true)
        {
            connectionString = StorageHelper.GetDatabaseConnectionString();
            db = new LeaderboardsContext(connectionString);

            db.Database.Delete(); // Make sure it really dropped - needed for dirty database
            if (initialize)
            {
                Database.SetInitializer(new LeaderboardsContextInitializer());
                db.Database.Initialize(force: true);
                Database.SetInitializer(new NullDatabaseInitializer<LeaderboardsContext>());
            }
        }

        protected readonly string connectionString;
        protected readonly LeaderboardsContext db;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Database.Delete();
            }
        }
    }
}
