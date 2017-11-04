using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using toofz.TestsShared;
using Xunit;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    public class LeaderboardsWorkerBaseTests
    {
        public LeaderboardsWorkerBaseTests()
        {
            Db = MockDb.Object;
            var mockDbLeaderboards = new MockDbSet<Leaderboard>();
            MockDb.Setup(d => d.Leaderboards).Returns(mockDbLeaderboards.Object);
            StoreClient = MockStoreClient.Object;
        }

        internal LeaderboardsWorkerBase Worker { get; set; } = new LeaderboardsWorkerBaseAdapter();
        public Mock<ILeaderboardsContext> MockDb { get; set; } = new Mock<ILeaderboardsContext>();
        public ILeaderboardsContext Db { get; set; }
        public Mock<ILeaderboardsStoreClient> MockStoreClient { get; set; } = new Mock<ILeaderboardsStoreClient>();
        public ILeaderboardsStoreClient StoreClient { get; set; }
        public CancellationToken CancellationToken { get; set; } = default;

        public class GetLeaderboardsAsyncMethod : LeaderboardsWorkerBaseTests
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

        public class StoreLeaderboardsAsyncMethod : LeaderboardsWorkerBaseTests
        {
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

        private class LeaderboardsWorkerBaseAdapter : LeaderboardsWorkerBase { }
    }
}
