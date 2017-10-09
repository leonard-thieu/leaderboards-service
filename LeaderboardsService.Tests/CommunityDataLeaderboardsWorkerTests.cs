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
        [TestClass]
        public class Constructor
        {
            [TestMethod]
            public void LeaderboardsConnectionStringIsNull_ThrowsArgumentNullException()
            {
                // Arrange
                var appId = 247080U;
                string leaderboardsConnectionString = null;

                // Act -> Assert
                Assert.ThrowsException<ArgumentNullException>(() =>
                {
                    new CommunityDataLeaderboardsWorker(appId, leaderboardsConnectionString);
                });
            }

            [TestMethod]
            public void ReturnsInstance()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnectionString = "myConnectionString";

                // Act
                var worker = new CommunityDataLeaderboardsWorker(appId, leaderboardsConnectionString);

                // Assert
                Assert.IsInstanceOfType(worker, typeof(CommunityDataLeaderboardsWorker));
            }
        }

        [TestClass]
        public class UpdateLeaderboardsAsyncMethod
        {
            [TestMethod]
            public async Task UpdatesLeaderboards()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnectionString = "myConnectionString";
                var worker = new CommunityDataLeaderboardsWorker(appId, leaderboardsConnectionString);
                var mockSteamClient = new Mock<ISteamCommunityDataClient>();
                var steamClient = mockSteamClient.Object;
                var leaderboard2047387 = new Leaderboard { LeaderboardId = 2047387 };
                var leaderboard2047540 = new Leaderboard { LeaderboardId = 2047540 };
                var leaderboards = new[] { leaderboard2047387, leaderboard2047540 };
                var cancellationToken = CancellationToken.None;
                var leaderboards_247080 = DataHelper.DeserializeLeaderboardsEnvelope(Resources.Leaderboards_247080);
                mockSteamClient
                    .Setup(c => c.GetLeaderboardsAsync(appId, It.IsAny<IProgress<long>>(), cancellationToken))
                    .Returns(Task.FromResult(leaderboards_247080));
                var leaderboardEntries_2047387_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047387_1);
                mockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, 2047387, new GetLeaderboardEntriesParams { StartRange = 1 }, It.IsAny<IProgress<long>>(), cancellationToken))
                    .Returns(Task.FromResult(leaderboardEntries_2047387_1));
                var leaderboardEntries_2047540_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_1);
                mockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, 2047540, new GetLeaderboardEntriesParams { StartRange = 1 }, It.IsAny<IProgress<long>>(), cancellationToken))
                    .Returns(Task.FromResult(leaderboardEntries_2047540_1));
                var leaderboardEntries_2047540_2 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_2);
                mockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, 2047540, new GetLeaderboardEntriesParams { StartRange = 5002 }, It.IsAny<IProgress<long>>(), cancellationToken))
                    .Returns(Task.FromResult(leaderboardEntries_2047540_2));

                // Act
                await worker.UpdateLeaderboardsAsync(steamClient, leaderboards, cancellationToken);

                // Assert
                mockSteamClient.Verify(c => c.GetLeaderboardEntriesAsync(appId, 2047540, It.IsAny<GetLeaderboardEntriesParams>(), It.IsAny<IProgress<long>>(), cancellationToken), Times.Exactly(2));
                Assert.AreEqual(319, leaderboard2047387.Entries.Count);
                Assert.AreEqual(8462, leaderboard2047540.Entries.Count);
            }
        }

        [TestClass]
        public class UpdateLeaderboardAsyncMethod
        {
            public UpdateLeaderboardAsyncMethod()
            {
                var appId = 247080U;
                var leaderboardsConnectionString = "myConnectionString";
                worker = new CommunityDataLeaderboardsWorker(appId, leaderboardsConnectionString);
                var mockSteamClient = new Mock<ISteamCommunityDataClient>();
                steamClient = mockSteamClient.Object;
                var leaderboardId = 2047540;
                leaderboard = new Leaderboard { LeaderboardId = leaderboardId };
                entryCount = 8462;
                progress = Mock.Of<IProgress<long>>();
                cancellationToken = CancellationToken.None;
                var leaderboardEntries_2047540_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_1);
                mockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, leaderboardId, new GetLeaderboardEntriesParams { StartRange = 1 }, progress, cancellationToken))
                    .Returns(Task.FromResult(leaderboardEntries_2047540_1));
                var leaderboardEntries_2047540_2 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_2);
                mockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, leaderboardId, new GetLeaderboardEntriesParams { StartRange = 5002 }, progress, cancellationToken))
                    .Returns(Task.FromResult(leaderboardEntries_2047540_2));
            }

            CommunityDataLeaderboardsWorker worker;
            ISteamCommunityDataClient steamClient;
            Leaderboard leaderboard;
            int entryCount;
            IProgress<long> progress;
            CancellationToken cancellationToken;

            [TestMethod]
            public async Task NoEntries_DoesNotThrowArgumentException()
            {
                // Arrange
                entryCount = 0;

                // Act
                await worker.UpdateLeaderboardAsync(steamClient, leaderboard, entryCount, progress, cancellationToken);

                // Assert
                Assert.IsNotNull(leaderboard.LastUpdate);
            }

            [TestMethod]
            public async Task AddsUpdatedEntries()
            {
                // Arrange -> Act
                await worker.UpdateLeaderboardAsync(steamClient, leaderboard, entryCount, progress, cancellationToken);

                // Assert
                Assert.AreEqual(entryCount, leaderboard.Entries.Count());
            }

            [TestMethod]
            public async Task SetsLastUpdate()
            {
                // Arrange -> Act
                await worker.UpdateLeaderboardAsync(steamClient, leaderboard, entryCount, progress, cancellationToken);

                // Assert
                Assert.IsNotNull(leaderboard.LastUpdate);
            }
        }

        [TestClass]
        public class GetLeaderboardEntriesAsyncMethod
        {
            [TestMethod]
            public async Task ReturnsEntries()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnectionString = "myConnectionString";
                var worker = new CommunityDataLeaderboardsWorker(appId, leaderboardsConnectionString);
                var mockSteamClient = new Mock<ISteamCommunityDataClient>();
                var steamClient = mockSteamClient.Object;
                var leaderboardId = 2047540;
                var startRange = 1;
                var progress = Mock.Of<IProgress<long>>();
                var cancellationToken = CancellationToken.None;
                var leaderboardEntries_2047540_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_1);
                mockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, leaderboardId, new GetLeaderboardEntriesParams { StartRange = startRange }, progress, cancellationToken))
                    .Returns(Task.FromResult(leaderboardEntries_2047540_1));

                // Act
                var entries = await worker.GetLeaderboardEntriesAsync(steamClient, leaderboardId, startRange, progress, cancellationToken);

                // Assert
                Assert.AreEqual(5001, entries.Count());
            }
        }
    }
}
