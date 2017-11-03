using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class WorkerRoleTests
    {
        [TestClass]
        public class RunAsyncOverrideMethod
        {
            [TestMethod]
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
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }

            [TestMethod]
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
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }

            [TestMethod]
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
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }

            class WorkerRoleAdapter : WorkerRole
            {
                public WorkerRoleAdapter(ILeaderboardsSettings settings) : base(settings) { }

                public Task PublicRunAsyncOverride(CancellationToken cancellationToken) => RunAsyncOverride(cancellationToken);
            }

            [TestMethod]
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
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }
        }
    }
}
