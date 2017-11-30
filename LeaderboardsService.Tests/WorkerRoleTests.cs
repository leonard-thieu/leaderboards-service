using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using Moq;
using Ninject.Extensions.NamedScope;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;
using toofz.Services;
using Xunit;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    public class WorkerRoleTests
    {
        public class IntegrationTests : DatabaseTestsBase
        {
            private readonly string settingsFileName = Path.GetTempFileName();
            private readonly Mock<ILog> mockLog = new Mock<ILog>();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (File.Exists(settingsFileName)) { File.Delete(settingsFileName); }
                }

                base.Dispose(disposing);
            }

            [Trait("Category", "Uses file system")]
            [Fact]
            public async Task ExecutesUpdateCycle()
            {
                // Arrange
                var settings = Settings.Default;
                // Should only loop once
                foreach (SettingsProvider provider in settings.Providers)
                {
                    var ssp = (ServiceSettingsProvider)provider;
                    ssp.GetSettingsReader = () => File.OpenText(settingsFileName);
                    ssp.GetSettingsWriter = () => File.CreateText(settingsFileName);
                }
                settings.UpdateInterval = TimeSpan.Zero;
                var telemetryClient = new TelemetryClient();
                var runOnce = true;

                var kernel = KernelConfig.CreateKernel();

                var connectionString = StorageHelper.GetDatabaseConnectionString();
                kernel.Rebind<string>()
                      .ToConstant(connectionString)
                      .WhenInjectedInto(typeof(LeaderboardsContext), typeof(LeaderboardsStoreClient));

                kernel.Rebind<ILeaderboardsStoreClient>()
                      .To<LeaderboardsStoreClient>()
                      .InParentScope();

                kernel.Rebind<ISteamCommunityDataClient>()
                      .To<FakeSteamCommunityDataClient>()
                      .InParentScope();

                kernel.Rebind<ISteamClientApiClient>()
                      .To<FakeSteamClientApiClient>()
                      .InParentScope();

                var log = mockLog.Object;
                var worker = new WorkerRole(settings, telemetryClient, runOnce, kernel, log);

                // Act
                worker.Start();
                await worker.Completion;

                // Assert
                Assert.True(db.Leaderboards.Any(l => l.LastUpdate != null));
                Assert.True(db.DailyLeaderboards.Any(l => l.LastUpdate != null));
            }
        }
    }
}
