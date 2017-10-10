using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class LeaderboardsWorkerTests
    {
        public LeaderboardsWorkerTests()
        {
            Worker = new LeaderboardsWorker(AppId, ConnectionString);
            SteamClient = MockSteamClient.Object;
        }

        public uint AppId { get; set; } = 247080;
        public string ConnectionString { get; set; } = "myConnectionString";
        public LeaderboardsWorker Worker { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public Mock<ISteamClientApiClient> MockSteamClient { get; set; } = new Mock<ISteamClientApiClient>();
        public ISteamClientApiClient SteamClient { get; set; }

        [TestClass]
        public class Constructor
        {
            [TestMethod]
            public void ConnectionStringIsNull_ThrowsArgumentNullException()
            {
                // Arrange
                var appId = 247080U;
                string connectionString = null;

                // Act -> Assert
                Assert.ThrowsException<ArgumentNullException>(() =>
                {
                    new LeaderboardsWorker(appId, connectionString);
                });
            }

            [TestMethod]
            public void ReturnsInstance()
            {
                // Arrange
                var appId = 247080U;
                var connectionString = "myConnectionString";

                // Act
                var worker = new LeaderboardsWorker(appId, connectionString);

                // Assert
                Assert.IsInstanceOfType(worker, typeof(LeaderboardsWorker));
            }
        }

        [TestClass]
        public class UpdateLeaderboardsAsyncMethod : LeaderboardsWorkerTests
        {
            [TestMethod]
            public async Task UpdatesLeaderboards()
            {
                // Arrange
                var leaderboard1 = new Leaderboard { LeaderboardId = 1 };
                var leaderboardEntriesCallback1 = new LeaderboardEntriesCallback();
                leaderboardEntriesCallback1.Entries.AddRange(new[]
                {
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                });
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboard1.LeaderboardId, CancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback1);
                var leaderboard2 = new Leaderboard { LeaderboardId = 2 };
                var leaderboardEntriesCallback2 = new LeaderboardEntriesCallback();
                leaderboardEntriesCallback2.Entries.AddRange(new[]
                {
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                });
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboard2.LeaderboardId, CancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback2);
                var leaderboards = new[] { leaderboard1, leaderboard2 };

                // Act
                await Worker.UpdateLeaderboardsAsync(SteamClient, leaderboards, CancellationToken);

                // Assert
                Assert.AreEqual(3, leaderboard1.Entries.Count);
                Assert.AreEqual(2, leaderboard2.Entries.Count);
            }
        }

        [TestClass]
        public class UpdateLeaderboardAsyncMethod : LeaderboardsWorkerTests
        {
            [TestMethod]
            public async Task SetsLastUpdate()
            {
                // Arrange
                var leaderboard = new Leaderboard();
                var leaderboardEntriesCallback = new LeaderboardEntriesCallback();
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboard.LeaderboardId, CancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback);

                // Act
                await Worker.UpdateLeaderboardAsync(SteamClient, leaderboard, CancellationToken);

                // Assert
                Assert.IsNotNull(leaderboard.LastUpdate);
            }

            [TestMethod]
            public async Task UpdatesLeaderboard()
            {
                // Arrange
                var leaderboard = new Leaderboard();
                var leaderboardEntriesCallback = new LeaderboardEntriesCallback();
                leaderboardEntriesCallback.Entries.AddRange(new[]
                {
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                });
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboard.LeaderboardId, CancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback);

                // Act
                await Worker.UpdateLeaderboardAsync(SteamClient, leaderboard, CancellationToken);

                // Assert
                Assert.AreEqual(2, leaderboard.Entries.Count);
            }
        }
    }
}
