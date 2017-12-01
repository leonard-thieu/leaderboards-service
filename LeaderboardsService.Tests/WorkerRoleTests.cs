using System;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using Moq;
using Ninject.Extensions.NamedScope;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;
using Xunit;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    public class WorkerRoleTests
    {
        public class IntegrationTests : IntegrationTestsBase
        {
            private readonly Mock<ILog> mockLog = new Mock<ILog>();

            [Fact]
            public async Task ExecutesUpdateCycle()
            {
                // Arrange
                settings.UpdateInterval = TimeSpan.Zero;
                var telemetryClient = new TelemetryClient();
                var runOnce = true;

                var kernel = KernelConfig.CreateKernel();

                kernel.Rebind<string>()
                      .ToConstant(databaseConnectionString)
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

                // Act
                using (var worker = new WorkerRole(settings, telemetryClient, runOnce, kernel, log))
                {
                    worker.Start();
                    await worker.Completion;
                }

                // Assert
                Assert.True(db.Leaderboards.Any(l => l.LastUpdate != null));
                Assert.True(db.DailyLeaderboards.Any(l => l.LastUpdate != null));
            }
        }
    }
}
