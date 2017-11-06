using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests.Properties;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;
using toofz.TestsShared;
using Xunit;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    public class LeaderboardsWorkerTests
    {
        public LeaderboardsWorkerTests()
        {
            Worker = new LeaderboardsWorker(AppId, ConnectionString);
            Db = MockDb.Object;
            var mockDbLeaderboards = new MockDbSet<Leaderboard>();
            MockDb.Setup(d => d.Leaderboards).Returns(mockDbLeaderboards.Object);
            SteamCommunityDataClient = MockSteamCommunityDataClient.Object;
            SteamClient = MockSteamClient.Object;
        }

        protected uint AppId = 247080;
        protected string ConnectionString = "myConnectionString";
        internal LeaderboardsWorker Worker;
        protected Mock<ILeaderboardsContext> MockDb = new Mock<ILeaderboardsContext>();
        protected ILeaderboardsContext Db;
        protected Mock<ISteamCommunityDataClient> MockSteamCommunityDataClient = new Mock<ISteamCommunityDataClient>();
        protected ISteamCommunityDataClient SteamCommunityDataClient;
        protected Mock<ISteamClientApiClient> MockSteamClient = new Mock<ISteamClientApiClient>();
        protected ISteamClientApiClient SteamClient;
        protected IProgress<long> Progress = Mock.Of<IProgress<long>>();
        protected CancellationToken CancellationToken = CancellationToken.None;

        public class Constructor
        {
            [Fact]
            public void ReturnsInstance()
            {
                // Arrange
                var appId = 247080U;
                var connectionString = "myConnectionString";

                // Act
                var worker = new LeaderboardsWorker(appId, connectionString);

                // Assert
                Assert.IsAssignableFrom<LeaderboardsWorker>(worker);
            }
        }

        public class GetLeaderboardsAsyncMethod : LeaderboardsWorkerTests
        {
            [Fact]
            public async Task ReturnsLeaderboards()
            {
                // Arrange -> Act
                var leaderboards = await Worker.GetLeaderboardsAsync(Db, CancellationToken);

                // Assert
                Assert.IsAssignableFrom<IEnumerable<Leaderboard>>(leaderboards);
            }
        }

        public class UpdateLeaderboardsAsyncMethod : LeaderboardsWorkerTests
        {
            [Fact]
            public async Task UpdatesLeaderboards()
            {
                // Arrange
                var leaderboard2047387 = new Leaderboard { LeaderboardId = 2047387 };
                var leaderboard2047540 = new Leaderboard { LeaderboardId = 2047540 };
                var leaderboards = new[] { leaderboard2047387, leaderboard2047540 };
                var leaderboards_247080 = DataHelper.DeserializeLeaderboardsEnvelope(Resources.Leaderboards_247080);
                MockSteamCommunityDataClient
                    .Setup(c => c.GetLeaderboardsAsync(AppId, It.IsAny<IProgress<long>>(), CancellationToken))
                    .ReturnsAsync(leaderboards_247080);
                var leaderboardEntries_2047387_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047387_1);
                MockSteamCommunityDataClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, 2047387, new GetLeaderboardEntriesParams { StartRange = 1 }, It.IsAny<IProgress<long>>(), CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047387_1);
                var leaderboardEntries_2047540_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_1);
                MockSteamCommunityDataClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, 2047540, new GetLeaderboardEntriesParams { StartRange = 1 }, It.IsAny<IProgress<long>>(), CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_1);
                var leaderboardEntries_2047540_2 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_2);
                MockSteamCommunityDataClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, 2047540, new GetLeaderboardEntriesParams { StartRange = 5002 }, It.IsAny<IProgress<long>>(), CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_2);

                // Act
                await Worker.UpdateLeaderboardsAsync(SteamCommunityDataClient, SteamClient, leaderboards, CancellationToken);

                // Assert
                MockSteamCommunityDataClient.Verify(c => c.GetLeaderboardEntriesAsync(AppId, 2047540, It.IsAny<GetLeaderboardEntriesParams>(), It.IsAny<IProgress<long>>(), CancellationToken), Times.Exactly(2));
                Assert.Equal(319, leaderboard2047387.Entries.Count);
                Assert.Equal(8462, leaderboard2047540.Entries.Count);
            }
        }

        public class UpdateLeaderboardAsyncMethod_ISteamCommunityDataClient : LeaderboardsWorkerTests
        {
            public UpdateLeaderboardAsyncMethod_ISteamCommunityDataClient()
            {
                var leaderboardId = 2047540;
                leaderboard = new Leaderboard { LeaderboardId = leaderboardId };
                entryCount = 8462;
                var leaderboardEntries_2047540_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_1);
                MockSteamCommunityDataClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboardId, new GetLeaderboardEntriesParams { StartRange = 1 }, Progress, CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_1);
                var leaderboardEntries_2047540_2 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_2);
                MockSteamCommunityDataClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboardId, new GetLeaderboardEntriesParams { StartRange = 5002 }, Progress, CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_2);
            }

            private Leaderboard leaderboard;
            private int entryCount;

            [Fact]
            public async Task NoEntries_DoesNotThrowArgumentException()
            {
                // Arrange
                entryCount = 0;

                // Act
                await Worker.UpdateLeaderboardAsync(SteamCommunityDataClient, leaderboard, entryCount, Progress, CancellationToken);

                // Assert
                Assert.NotNull(leaderboard.LastUpdate);
            }

            [Fact]
            public async Task AddsUpdatedEntries()
            {
                // Arrange -> Act
                await Worker.UpdateLeaderboardAsync(SteamCommunityDataClient, leaderboard, entryCount, Progress, CancellationToken);

                // Assert
                Assert.Equal(entryCount, leaderboard.Entries.Count());
            }

            [Fact]
            public async Task SetsLastUpdate()
            {
                // Arrange -> Act
                await Worker.UpdateLeaderboardAsync(SteamCommunityDataClient, leaderboard, entryCount, Progress, CancellationToken);

                // Assert
                Assert.NotNull(leaderboard.LastUpdate);
            }
        }

        public class UpdateLeaderboardAsyncMethod_ISteamClientApiClient : LeaderboardsWorkerTests
        {
            [Fact]
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
                Assert.NotNull(leaderboard.LastUpdate);
            }

            [Fact]
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
                Assert.Equal(2, leaderboard.Entries.Count);
            }
        }

        public class GetLeaderboardEntriesAsyncMethod : LeaderboardsWorkerTests
        {
            [Fact]
            public async Task ReturnsEntries()
            {
                // Arrange
                var leaderboardId = 2047540;
                var startRange = 1;
                var leaderboardEntries_2047540_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_1);
                MockSteamCommunityDataClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboardId, new GetLeaderboardEntriesParams { StartRange = startRange }, Progress, CancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_1);

                // Act
                var entries = await Worker.GetLeaderboardEntriesAsync(SteamCommunityDataClient, leaderboardId, startRange, Progress, CancellationToken);

                // Assert
                Assert.Equal(5001, entries.Count());
            }
        }

        public class StoreLeaderboardsAsyncMethod : LeaderboardsWorkerTests
        {
            public StoreLeaderboardsAsyncMethod()
            {
                StoreClient = MockStoreClient.Object;
            }

            protected Mock<ILeaderboardsStoreClient> MockStoreClient = new Mock<ILeaderboardsStoreClient>();
            protected ILeaderboardsStoreClient StoreClient;

            [Fact]
            public async Task StoresLeaderboards()
            {
                // Arrange
                var leaderboard = new Leaderboard();
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await Worker.StoreLeaderboardsAsync(StoreClient, leaderboards, CancellationToken);

                // Assert
                MockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Leaderboard>>(), CancellationToken), Times.Once);
            }

            [Fact]
            public async Task StoresPlayers()
            {
                // Arrange
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry { SteamId = 453857 });
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await Worker.StoreLeaderboardsAsync(StoreClient, leaderboards, CancellationToken);

                // Assert
                MockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Player>>(), It.IsAny<BulkUpsertOptions>(), CancellationToken), Times.Once);
            }

            [Fact]
            public async Task StoresReplays()
            {
                // Arrange
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry { ReplayId = 3849753489753975 });
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await Worker.StoreLeaderboardsAsync(StoreClient, leaderboards, CancellationToken);

                // Assert
                MockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Replay>>(), It.IsAny<BulkUpsertOptions>(), CancellationToken), Times.Once);
            }

            [Fact]
            public async Task StoresEntries()
            {
                // Arrange
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry());
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await Worker.StoreLeaderboardsAsync(StoreClient, leaderboards, CancellationToken);

                // Assert
                MockStoreClient.Verify(s => s.BulkInsertAsync(It.IsAny<IEnumerable<Entry>>(), CancellationToken), Times.Once);
            }
        }
    }
}
