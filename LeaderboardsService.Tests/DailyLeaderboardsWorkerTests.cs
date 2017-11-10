using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Moq;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.TestsShared;
using Xunit;
using static SteamKit2.SteamUserStats;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    public class DailyLeaderboardsWorkerTests
    {
        public DailyLeaderboardsWorkerTests()
        {
            worker = new DailyLeaderboardsWorker(appId, connectionString, telemetryClient);
            db = mockDb.Object;
            var mockDbProducts = new MockDbSet<Product>(products);
            mockDb.Setup(d => d.Products).Returns(mockDbProducts.Object);
            var mockDailyLeaderboards = new MockDbSet<DailyLeaderboard>(dailyLeaderboards);
            mockDb.Setup(d => d.DailyLeaderboards).Returns(mockDailyLeaderboards.Object);
            steamClient = mockSteamClient.Object;
            storeClient = mockStoreClient.Object;
        }

        private uint appId = 247080;
        private string connectionString = "myConnectionString";
        private TelemetryClient telemetryClient = new TelemetryClient();
        private DailyLeaderboardsWorker worker;
        private CancellationToken cancellationToken = CancellationToken.None;
        private int limit = 100;
        private Mock<ILeaderboardsContext> mockDb = new Mock<ILeaderboardsContext>();
        private ILeaderboardsContext db;
        private List<DailyLeaderboard> dailyLeaderboards = new List<DailyLeaderboard>();
        private List<Product> products = new List<Product>();
        private Mock<ISteamClientApiClient> mockSteamClient = new Mock<ISteamClientApiClient>();
        private ISteamClientApiClient steamClient;
        private Mock<ILeaderboardsStoreClient> mockStoreClient = new Mock<ILeaderboardsStoreClient>();
        private ILeaderboardsStoreClient storeClient;

        public class Constructor
        {
            [Fact]
            public void ReturnsInstance()
            {
                // Arrange
                var appId = 247080U;
                var connectionString = "myConnectionString";
                var telemetryClient = new TelemetryClient();

                // Act
                var worker = new DailyLeaderboardsWorker(appId, connectionString, telemetryClient);

                // Assert
                Assert.IsAssignableFrom<DailyLeaderboardsWorker>(worker);
            }
        }

        public class GetDailyLeaderboardsAsyncMethod : DailyLeaderboardsWorkerTests
        {
            [Fact]
            public async Task ReturnsDailyLeaderboards()
            {
                // Arrange
                var today = DateTime.UtcNow.Date;
                dailyLeaderboards.AddRange(new[]
                {
                    new DailyLeaderboard { Date = today.AddDays(-1) },
                });
                products.AddRange(new[]
                {
                    new Product(0, "classic", "Classic"),
                });
                mockSteamClient
                    .Setup(s => s.FindLeaderboardAsync(appId, It.IsAny<string>(), cancellationToken))
                    .ReturnsAsync(Mock.Of<IFindOrCreateLeaderboardCallback>());

                // Act
                var dailyLeaderboards2 = await worker.GetDailyLeaderboardsAsync(db, steamClient, limit, cancellationToken);

                // Assert
                Assert.Equal(2, dailyLeaderboards2.Count());
            }
        }

        public class GetStaleDailyLeaderboardsAsyncMethod : DailyLeaderboardsWorkerTests
        {
            [Fact]
            public async Task ReturnsStaleDailyLeaderboards()
            {
                // Arrange
                var today = new DateTime(2017, 9, 13);
                dailyLeaderboards.AddRange(new[]
                {
                    new DailyLeaderboard { Date = today },
                    new DailyLeaderboard { Date = today.AddDays(-1) },
                });

                // Act
                var staleDailyLeaderboards = await worker.GetStaleDailyLeaderboardsAsync(db, today, limit, cancellationToken);

                // Assert
                Assert.IsAssignableFrom<IEnumerable<DailyLeaderboard>>(staleDailyLeaderboards);
                foreach (var staleDailyLeaderboard in staleDailyLeaderboards)
                {
                    Assert.NotEqual(today, staleDailyLeaderboard.Date);
                }
            }
        }

        public class GetCurrentDailyLeaderboardsAsyncMethod : DailyLeaderboardsWorkerTests
        {
            [Fact]
            public async Task CurrentDailyLeaderboardsExist_ReturnsExistingCurrentDailyLeaderboards()
            {
                // Arrange
                var today = new DateTime(2017, 9, 13);
                var current = new DailyLeaderboard
                {
                    Date = today,
                    ProductId = 0,
                    Product = new Product(0, "classic", "Classic"),
                };
                dailyLeaderboards.Add(current);

                // Act
                var leaderboards = await worker.GetCurrentDailyLeaderboardsAsync(db, steamClient, today, cancellationToken);

                // Assert
                var leaderboard = leaderboards.First();
                Assert.Equal(today, leaderboard.Date);
                Assert.Equal("classic", leaderboard.Product.Name);
                mockSteamClient.Verify(s => s.FindLeaderboardAsync(appId, It.IsAny<string>(), cancellationToken), Times.Never);
            }

            [Fact]
            public async Task CurrentDailyLeaderboardsDoNotExist_GetsAndReturnsCurrentDailyLeaderboards()
            {
                // Arrange
                products.AddRange(new[]
                {
                    new Product(0, "classic", "Classic"),
                    new Product(1, "amplified", "Amplified"),
                });
                var today = new DateTime(2017, 9, 13);
                mockSteamClient
                    .Setup(s => s.FindLeaderboardAsync(appId, It.IsAny<string>(), cancellationToken))
                    .ReturnsAsync(Mock.Of<IFindOrCreateLeaderboardCallback>());

                // Act
                var leaderboards = await worker.GetCurrentDailyLeaderboardsAsync(db, steamClient, today, cancellationToken);

                // Assert
                var leaderboard = leaderboards.First();
                Assert.Equal(today, leaderboard.Date);
                Assert.Equal(0, leaderboard.ProductId);
                mockSteamClient.Verify(s => s.FindLeaderboardAsync(appId, It.IsAny<string>(), cancellationToken), Times.Exactly(2));
            }
        }

        public class GetDailyLeaderboardNameMethod
        {
            [Fact]
            public void ProductIsAmplified_ReturnsNameStartingWithDLC()
            {
                // Arrange
                var product = "amplified";
                var date = new DateTime(2017, 9, 13);

                // Act
                var name = DailyLeaderboardsWorker.GetDailyLeaderboardName(product, date);

                // Assert
                Assert.Equal("DLC 13/9/2017_PROD", name);
            }

            [Fact]
            public void ProductIsClassic_ReturnsName()
            {
                // Arrange
                var product = "classic";
                var date = new DateTime(2017, 9, 13);

                // Act
                var name = DailyLeaderboardsWorker.GetDailyLeaderboardName(product, date);

                // Assert
                Assert.Equal("13/9/2017_PROD", name);
            }

            [Fact]
            public void ProductIsInvalid_ThrowsArgumentException()
            {
                // Arrange
                var product = "";
                var date = new DateTime(2017, 9, 13);

                // Act -> Assert
                Assert.Throws<ArgumentException>(() =>
                {
                    DailyLeaderboardsWorker.GetDailyLeaderboardName(product, date);
                });
            }

            [Fact]
            public void ReturnsName()
            {
                // Arrange
                var product = "classic";
                var date = new DateTime(2017, 9, 13);

                // Act
                var name = DailyLeaderboardsWorker.GetDailyLeaderboardName(product, date);

                // Assert
                Assert.Equal("13/9/2017_PROD", name);
            }
        }

        public class UpdateDailyLeaderboardsAsyncMethod : DailyLeaderboardsWorkerTests
        {
            [Fact]
            public async Task UpdatesLeaderboards()
            {
                // Arrange
                var leaderboard1 = new DailyLeaderboard { LeaderboardId = 1 };
                var leaderboard2 = new DailyLeaderboard { LeaderboardId = 2 };
                var leaderboards = new[] { leaderboard1, leaderboard2 };
                var leaderboardEntriesCallback1 = new LeaderboardEntriesCallback();
                leaderboardEntriesCallback1.Entries.AddRange(new[]
                {
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                });
                mockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, leaderboard1.LeaderboardId, cancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback1);
                var leaderboardEntriesCallback2 = new LeaderboardEntriesCallback();
                leaderboardEntriesCallback2.Entries.AddRange(new[]
                {
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                });
                mockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, leaderboard2.LeaderboardId, cancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback2);

                // Act
                await worker.UpdateDailyLeaderboardsAsync(steamClient, leaderboards, cancellationToken);

                // Assert
                Assert.Equal(3, leaderboard1.Entries.Count);
                Assert.Equal(2, leaderboard2.Entries.Count);
            }
        }

        public class UpdateDailyLeaderboardAsyncMethod : DailyLeaderboardsWorkerTests
        {
            public UpdateDailyLeaderboardAsyncMethod()
            {
                leaderboard = new DailyLeaderboard();
                leaderboardEntriesCallback = new LeaderboardEntriesCallback();
                mockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, leaderboard.LeaderboardId, cancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback);
            }

            private LeaderboardEntriesCallback leaderboardEntriesCallback;
            private DailyLeaderboard leaderboard;

            [Fact]
            public async Task SetsLastUpdate()
            {
                // Arrange -> Act
                await worker.UpdateDailyLeaderboardAsync(steamClient, leaderboard, cancellationToken);

                // Assert
                Assert.NotNull(leaderboard.LastUpdate);
            }

            [Fact]
            public async Task UpdatesLeaderboard()
            {
                // Arrange
                leaderboardEntriesCallback.Entries.AddRange(new[]
                {
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                });

                // Act
                await worker.UpdateDailyLeaderboardAsync(steamClient, leaderboard, cancellationToken);

                // Assert
                Assert.Equal(3, leaderboard.Entries.Count);
            }
        }

        public class StoreDailyLeaderboardsAsyncMethod : DailyLeaderboardsWorkerTests
        {
            [Fact]
            public async Task StoresLeaderboards()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(storeClient, leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<DailyLeaderboard>>(), cancellationToken), Times.Once);
            }

            [Fact]
            public async Task StoresPlayers()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry { SteamId = 453857 });
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(storeClient, leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Player>>(), It.IsAny<BulkUpsertOptions>(), cancellationToken), Times.Once);
            }

            [Fact]
            public async Task StoresReplays()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry { ReplayId = 3849753489753975 });
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(storeClient, leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Replay>>(), It.IsAny<BulkUpsertOptions>(), cancellationToken), Times.Once);
            }

            [Fact]
            public async Task StoresEntries()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry());
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(storeClient, leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<DailyEntry>>(), cancellationToken), Times.Once);
            }
        }
    }
}
