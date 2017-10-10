using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using toofz.TestsShared;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class LeaderboardsWorkerBaseTests
    {
        [TestClass]
        public class GetLeaderboardsAsyncMethod
        {
            [TestMethod]
            public async Task ReturnsLeaderboards()
            {
                // Arrange
                var worker = new LeaderboardsWorkerBaseAdapter();
                var db = Mock.Of<ILeaderboardsContext>();
                var mockDb = Mock.Get(db);
                var mockDbLeaderboards = new MockDbSet<Leaderboard>();
                var dbLeaderboards = mockDbLeaderboards.Object;
                mockDb.Setup(d => d.Leaderboards).Returns(dbLeaderboards);

                // Act
                var leaderboards = await worker.GetLeaderboardsAsync(db, default);

                // Assert
                Assert.IsInstanceOfType(leaderboards, typeof(IEnumerable<Leaderboard>));
            }
        }

        [TestClass]
        public class StoreLeaderboardsAsyncMethod
        {
            [TestMethod]
            public async Task StoresLeaderboards()
            {
                // Arrange
                var worker = new LeaderboardsWorkerBaseAdapter();
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new Leaderboard();
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await worker.StoreLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Leaderboard>>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresPlayers()
            {
                // Arrange
                var worker = new LeaderboardsWorkerBaseAdapter();
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry { SteamId = 453857 });
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await worker.StoreLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Player>>(), false, It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresReplays()
            {
                // Arrange
                var worker = new LeaderboardsWorkerBaseAdapter();
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry { ReplayId = 3849753489753975 });
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await worker.StoreLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Replay>>(), false, It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresEntries()
            {
                // Arrange
                var worker = new LeaderboardsWorkerBaseAdapter();
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry());
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await worker.StoreLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Entry>>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        class LeaderboardsWorkerBaseAdapter : LeaderboardsWorkerBase { }
    }
}
