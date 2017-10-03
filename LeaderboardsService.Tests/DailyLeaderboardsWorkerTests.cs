using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.TestsShared;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class DailyLeaderboardsWorkerTests
    {
        [TestClass]
        public class GetStaleDailyLeaderboardsAsyncMethod
        {
            [TestMethod]
            public async Task ReturnsStaleDailyLeaderboards()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnnectionString = "myConnectionString";
                var worker = new DailyLeaderboardsWorker(appId, leaderboardsConnnectionString);
                var mockDb = new Mock<LeaderboardsContext>();
                var db = mockDb.Object;
                var today = new DateTime(2017, 9, 13);
                var mockDailyLeaderboards = new MockDbSet<DailyLeaderboard>(
                    new DailyLeaderboard { Date = today },
                    new DailyLeaderboard { Date = today.AddDays(-1) });
                var dailyLeaderboards = mockDailyLeaderboards.Object;
                mockDb.Setup(d => d.DailyLeaderboards).Returns(dailyLeaderboards);
                var limit = 100;
                var cancellationToken = CancellationToken.None;

                // Act
                var staleDailyLeaderboards = await worker.GetStaleDailyLeaderboardsAsync(db, today, limit, cancellationToken);

                // Assert
                Assert.IsInstanceOfType(staleDailyLeaderboards, typeof(IEnumerable<DailyLeaderboard>));
                foreach (var staleDailyLeaderboard in staleDailyLeaderboards)
                {
                    Assert.AreNotEqual(today, staleDailyLeaderboard.Date);
                }
            }
        }

        [TestClass]
        public class GetCurrentDailyLeaderboardsAsyncMethod
        {
            [TestMethod]
            public async Task CurrentDailyLeaderboardsExist_ReturnsExistingCurrentDailyLeaderboards()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnnectionString = "myConnectionString";
                var worker = new DailyLeaderboardsWorker(appId, leaderboardsConnnectionString);
                var mockDb = new Mock<LeaderboardsContext>();
                var db = mockDb.Object;
                var mockSteamClient = new Mock<ISteamClientApiClient>();
                var steamClient = mockSteamClient.Object;
                var today = new DateTime(2017, 9, 13);
                var current = new DailyLeaderboard
                {
                    Date = today,
                    ProductId = 0,
                    Product = new Product(0, "classic", "Classic"),
                };
                var mockDailyLeaderboards = new MockDbSet<DailyLeaderboard>(current);
                var dailyLeaderboards = mockDailyLeaderboards.Object;
                mockDailyLeaderboards.Setup(d => d.Include(It.IsAny<string>())).Returns(dailyLeaderboards);
                mockDb.Setup(d => d.DailyLeaderboards).Returns(dailyLeaderboards);
                var mockDbProducts = new MockDbSet<Product>();
                var dbProducts = mockDbProducts.Object;
                mockDb.Setup(d => d.Products).Returns(dbProducts);
                var cancellationToken = CancellationToken.None;

                // Act
                var leaderboards = await worker.GetCurrentDailyLeaderboardsAsync(db, steamClient, today, cancellationToken);

                // Assert
                var leaderboard = leaderboards.First();
                Assert.AreEqual(today, leaderboard.Date);
                Assert.AreEqual("classic", leaderboard.Product.Name);
                mockSteamClient.Verify(s => s.FindLeaderboardAsync(It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [TestMethod]
            public async Task CurrentDailyLeaderboardsDoNotExist_GetsAndReturnsCurrentDailyLeaderboards()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnnectionString = "myConnectionString";
                var worker = new DailyLeaderboardsWorker(appId, leaderboardsConnnectionString);
                var mockDb = new Mock<LeaderboardsContext>();
                var db = mockDb.Object;
                var mockSteamClient = new Mock<ISteamClientApiClient>();
                mockSteamClient
                    .Setup(s => s.FindLeaderboardAsync(It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(Mock.Of<IFindOrCreateLeaderboardCallback>()));
                var steamClient = mockSteamClient.Object;
                var today = new DateTime(2017, 9, 13);
                var mockDailyLeaderboards = new MockDbSet<DailyLeaderboard>();
                var dailyLeaderboards = mockDailyLeaderboards.Object;
                mockDailyLeaderboards.Setup(d => d.Include(It.IsAny<string>())).Returns(dailyLeaderboards);
                mockDb.Setup(d => d.DailyLeaderboards).Returns(dailyLeaderboards);
                var products = new[]
                {
                    new Product(0, "classic", "Classic"),
                    new Product(1, "amplified", "Amplified"),
                };
                var mockDbProducts = new MockDbSet<Product>(products);
                var dbProducts = mockDbProducts.Object;
                mockDb.Setup(d => d.Products).Returns(dbProducts);
                var cancellationToken = CancellationToken.None;

                // Act
                var leaderboards = await worker.GetCurrentDailyLeaderboardsAsync(db, steamClient, today, cancellationToken);

                // Assert
                var leaderboard = leaderboards.First();
                Assert.AreEqual(today, leaderboard.Date);
                Assert.AreEqual(0, leaderboard.ProductId);
                mockSteamClient.Verify(s => s.FindLeaderboardAsync(It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
        }

        [TestClass]
        public class GetDailyLeaderboardNameMethod
        {
            [TestMethod]
            public void ProductIsAmplified_ReturnsNameStartingWithDLC()
            {
                // Arrange
                var product = "amplified";
                var date = new DateTime(2017, 9, 13);

                // Act
                var name = DailyLeaderboardsWorker.GetDailyLeaderboardName(product, date);

                // Assert
                Assert.AreEqual("DLC 13/9/2017_PROD", name);
            }

            [TestMethod]
            public void ProductIsClassic_ReturnsName()
            {
                // Arrange
                var product = "classic";
                var date = new DateTime(2017, 9, 13);

                // Act
                var name = DailyLeaderboardsWorker.GetDailyLeaderboardName(product, date);

                // Assert
                Assert.AreEqual("13/9/2017_PROD", name);
            }

            [TestMethod]
            public void ProductIsInvalid_ThrowsArgumentException()
            {
                // Arrange
                var product = "";
                var date = new DateTime(2017, 9, 13);

                // Act -> Assert
                Assert.ThrowsException<ArgumentException>(() =>
                {
                    DailyLeaderboardsWorker.GetDailyLeaderboardName(product, date);
                });
            }

            [TestMethod]
            public void ReturnsName()
            {
                // Arrange
                var product = "classic";
                var date = new DateTime(2017, 9, 13);

                // Act
                var name = DailyLeaderboardsWorker.GetDailyLeaderboardName(product, date);

                // Assert
                Assert.AreEqual("13/9/2017_PROD", name);
            }
        }

        [TestClass]
        public class StoreDailyLeaderboardsAsyncMethod
        {
            [TestMethod]
            public async Task StoresLeaderboards()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnnectionString = "myConnectionString";
                var worker = new DailyLeaderboardsWorker(appId, leaderboardsConnnectionString);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new DailyLeaderboard();
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<DailyLeaderboard>>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresPlayers()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnnectionString = "myConnectionString";
                var worker = new DailyLeaderboardsWorker(appId, leaderboardsConnnectionString);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry { SteamId = 453857 });
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Player>>(), false, It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresReplays()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnnectionString = "myConnectionString";
                var worker = new DailyLeaderboardsWorker(appId, leaderboardsConnnectionString);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry { ReplayId = 3849753489753975 });
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Replay>>(), false, It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresEntries()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnnectionString = "myConnectionString";
                var worker = new DailyLeaderboardsWorker(appId, leaderboardsConnnectionString);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry());
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<DailyEntry>>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }
    }
}
