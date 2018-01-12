using System;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;
using Moq;
using Ninject;
using Ninject.Extensions.NamedScope;
using toofz.Data;
using toofz.Steam.CommunityData;
using Xunit;

namespace toofz.Services.LeaderboardsService.Tests
{
    public class WorkerRoleTests
    {
        public class IntegrationTests : IntegrationTestsBase
        {
            private readonly Mock<ILog> mockLog = new Mock<ILog>();

            [DisplayFact]
            public async Task ExecutesUpdateCycle()
            {
                // Arrange
                settings.UpdateInterval = TimeSpan.Zero;
                var telemetryClient = new TelemetryClient();
                var runOnce = true;

                var kernel = KernelConfig.CreateKernel();

                kernel.Rebind<DbContextOptions<NecroDancerContext>>()
                      .ToMethod(c =>
                      {
                          return new DbContextOptionsBuilder<NecroDancerContext>()
                            .UseSqlServer(databaseConnectionString)
                            .Options;
                      })
                      .WhenInjectedInto<NecroDancerContext>();

                kernel.Rebind<string>()
                      .ToConstant(databaseConnectionString)
                      .WhenInjectedInto<LeaderboardsStoreClient>();
                kernel.Rebind<ILeaderboardsStoreClient>()
                      .To<LeaderboardsStoreClient>()
                      .InParentScope();

                kernel.Rebind<ISteamCommunityDataClient>()
                      .To<FakeSteamCommunityDataClient>()
                      .InParentScope();

                using (var context = kernel.Get<NecroDancerContext>())
                {
                    context.EnsureSeedData();
                }

                var log = mockLog.Object;

                // Act
                using (var worker = new WorkerRole(settings, telemetryClient, runOnce, kernel, log))
                {
                    worker.Start();
                    await worker.Completion;
                }

                // Assert
                Assert.True(db.Leaderboards.Any(l => l.LastUpdate != null));
            }
        }
    }
}
