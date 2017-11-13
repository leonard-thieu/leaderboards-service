using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.Services;
using Xunit;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    public class WorkerRoleTests
    {
        public class RunAsyncOverrideMethod
        {
            [Fact]
            public async Task SteamUserNameIsNull_ThrowsInvalidOperationException()
            {
                // Arrange
                var settings = new StubLeaderboardsSettings
                {
                    SteamUserName = null,
                    SteamPassword = new EncryptedSecret("a", 1),
                    LeaderboardsConnectionString = new EncryptedSecret("a", 1),
                    DailyLeaderboardsPerUpdate = 100,
                };
                var workerRole = new WorkerRoleAdapter(settings);
                var cancellationToken = CancellationToken.None;

                // Act -> Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }

            [Fact]
            public async Task SteamUserNameIsEmpty_ThrowsInvalidOperationException()
            {
                // Arrange
                var settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "",
                    SteamPassword = new EncryptedSecret("a", 1),
                    LeaderboardsConnectionString = new EncryptedSecret("a", 1),
                    DailyLeaderboardsPerUpdate = 100,
                };
                var workerRole = new WorkerRoleAdapter(settings);
                var cancellationToken = CancellationToken.None;

                // Act -> Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }

            [Fact]
            public async Task SteamPasswordIsNull_ThrowsInvalidOperationException()
            {
                // Arrange
                var settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "myUserName",
                    SteamPassword = null,
                    LeaderboardsConnectionString = new EncryptedSecret("a", 1),
                    DailyLeaderboardsPerUpdate = 100,
                };
                var workerRole = new WorkerRoleAdapter(settings);
                var cancellationToken = CancellationToken.None;

                // Act -> Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }

            private class WorkerRoleAdapter : WorkerRole
            {
                public WorkerRoleAdapter(ILeaderboardsSettings settings) : base(settings, new TelemetryClient()) { }

                public Task PublicRunAsyncOverride(CancellationToken cancellationToken) => RunAsyncOverride(cancellationToken);
            }

            [Fact]
            public async Task LeaderboardsConnectionStringIsNull_ThrowsInvalidOperationException()
            {
                // Arrange
                var settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "myUserName",
                    SteamPassword = new EncryptedSecret("a", 1),
                    LeaderboardsConnectionString = null,
                    DailyLeaderboardsPerUpdate = 100,
                };
                var workerRole = new WorkerRoleAdapter(settings);
                var cancellationToken = CancellationToken.None;

                // Act -> Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }
        }
    }
}
