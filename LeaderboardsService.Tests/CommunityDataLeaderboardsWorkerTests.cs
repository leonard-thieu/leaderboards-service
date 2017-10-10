using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests.Properties;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class CommunityDataLeaderboardsWorkerTests
    {
        public CommunityDataLeaderboardsWorkerTests()
        {
            Worker = new CommunityDataLeaderboardsWorker(AppId, ConnectionString);
            SteamClient = MockSteamClient.Object;
        }

        public uint AppId { get; set; } = 247080;
        public string ConnectionString { get; set; } = "myConnectionString";
        public CommunityDataLeaderboardsWorker Worker { get; set; }
        public Mock<ISteamCommunityDataClient> MockSteamClient { get; set; } = new Mock<ISteamCommunityDataClient>();
        public ISteamCommunityDataClient SteamClient { get; set; }
        public IProgress<long> Progress { get; set; } = Mock.Of<IProgress<long>>();
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

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
                    new CommunityDataLeaderboardsWorker(appId, connectionString);
                });
            }

            [TestMethod]
            public void ReturnsInstance()
            {
                // Arrange
                var appId = 247080U;
                var connectionString = "myConnectionString";

                // Act
                var worker = new CommunityDataLeaderboardsWorker(appId, connectionString);

                // Assert
                Assert.IsInstanceOfType(worker, typeof(CommunityDataLeaderboardsWorker));
            }
        }

        [TestClass]
        public class UpdateLeaderboardsAsyncMethod : CommunityDataLeaderboardsWorkerTests
        {
            [TestMethod]
            public async Task UpdatesLeaderboards()
            {
                // Arrange
                var leaderboard2047387 = new Leaderboard { LeaderboardId = 2047387 };
                var leaderboard2047540 = new Leaderboard { LeaderboardId = 2047540 };
                var leaderboards = new[] { leaderboard2047387, leaderboard2047540 };
                var leaderboards_247080 = DataHelper.DeserializeLeaderboardsEnvelope(Resources.Leaderboards_247080);
                MockSteamClient
                    .Setup(c => c.GetLeaderboardsAsync(AppId, It.IsAny<IProgress<long>>(), CancellationToken))
                    .ReturnsAsync(leaderboards_247080);
                var leaderboardEntries_2047387_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047387_1);
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, 2047387, new GetLeaderboardEntriesParams { StartRange = 1 }, It.IsAny<IProgress<long>>(), CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047387_1);
                var leaderboardEntries_2047540_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_1);
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, 2047540, new GetLeaderboardEntriesParams { StartRange = 1 }, It.IsAny<IProgress<long>>(), CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_1);
                var leaderboardEntries_2047540_2 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_2);
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, 2047540, new GetLeaderboardEntriesParams { StartRange = 5002 }, It.IsAny<IProgress<long>>(), CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_2);

                // Act
                await Worker.UpdateLeaderboardsAsync(SteamClient, leaderboards, CancellationToken);

                // Assert
                MockSteamClient.Verify(c => c.GetLeaderboardEntriesAsync(AppId, 2047540, It.IsAny<GetLeaderboardEntriesParams>(), It.IsAny<IProgress<long>>(), CancellationToken), Times.Exactly(2));
                Assert.AreEqual(319, leaderboard2047387.Entries.Count);
                Assert.AreEqual(8462, leaderboard2047540.Entries.Count);
            }
        }

        [TestClass]
        public class UpdateLeaderboardAsyncMethod : CommunityDataLeaderboardsWorkerTests
        {
            public UpdateLeaderboardAsyncMethod()
            {
                var leaderboardId = 2047540;
                leaderboard = new Leaderboard { LeaderboardId = leaderboardId };
                entryCount = 8462;
                var leaderboardEntries_2047540_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_1);
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboardId, new GetLeaderboardEntriesParams { StartRange = 1 }, Progress, CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_1);
                var leaderboardEntries_2047540_2 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_2);
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboardId, new GetLeaderboardEntriesParams { StartRange = 5002 }, Progress, CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_2);
            }

            Leaderboard leaderboard;
            int entryCount;

            [TestMethod]
            public async Task NoEntries_DoesNotThrowArgumentException()
            {
                // Arrange
                entryCount = 0;

                // Act
                await Worker.UpdateLeaderboardAsync(SteamClient, leaderboard, entryCount, Progress, CancellationToken);

                // Assert
                Assert.IsNotNull(leaderboard.LastUpdate);
            }

            [TestMethod]
            public async Task AddsUpdatedEntries()
            {
                // Arrange -> Act
                await Worker.UpdateLeaderboardAsync(SteamClient, leaderboard, entryCount, Progress, CancellationToken);

                // Assert
                Assert.AreEqual(entryCount, leaderboard.Entries.Count());
            }

            [TestMethod]
            public async Task SetsLastUpdate()
            {
                // Arrange -> Act
                await Worker.UpdateLeaderboardAsync(SteamClient, leaderboard, entryCount, Progress, CancellationToken);

                // Assert
                Assert.IsNotNull(leaderboard.LastUpdate);
            }
        }

        [TestClass]
        public class GetLeaderboardEntriesAsyncMethod : CommunityDataLeaderboardsWorkerTests
        {
            [TestMethod]
            public async Task ReturnsEntries()
            {
                // Arrange
                var leaderboardId = 2047540;
                var startRange = 1;
                var leaderboardEntries_2047540_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_1);
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboardId, new GetLeaderboardEntriesParams { StartRange = startRange }, Progress, CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_1);

                // Act
                var entries = await Worker.GetLeaderboardEntriesAsync(SteamClient, leaderboardId, startRange, Progress, CancellationToken);

                // Assert
                Assert.AreEqual(5001, entries.Count());
            }
        }
    }
}
