﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            Worker = new DailyLeaderboardsWorker(AppId, ConnectionString);
            Db = MockDb.Object;
            var mockDbProducts = new MockDbSet<Product>(Products);
            MockDb.Setup(d => d.Products).Returns(mockDbProducts.Object);
            var mockDailyLeaderboards = new MockDbSet<DailyLeaderboard>(DailyLeaderboards);
            MockDb.Setup(d => d.DailyLeaderboards).Returns(mockDailyLeaderboards.Object);
            SteamClient = MockSteamClient.Object;
            StoreClient = MockStoreClient.Object;
        }

        public uint AppId { get; set; } = 247080;
        public string ConnectionString { get; set; } = "myConnectionString";
        internal DailyLeaderboardsWorker Worker { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public int Limit { get; set; } = 100;
        public Mock<ILeaderboardsContext> MockDb { get; set; } = new Mock<ILeaderboardsContext>();
        public ILeaderboardsContext Db { get; set; }
        public List<DailyLeaderboard> DailyLeaderboards { get; set; } = new List<DailyLeaderboard>();
        public List<Product> Products { get; set; } = new List<Product>();
        public Mock<ISteamClientApiClient> MockSteamClient { get; set; } = new Mock<ISteamClientApiClient>();
        public ISteamClientApiClient SteamClient { get; set; }
        public Mock<ILeaderboardsStoreClient> MockStoreClient { get; set; } = new Mock<ILeaderboardsStoreClient>();
        public ILeaderboardsStoreClient StoreClient { get; set; }

        public class Constructor
        {
            [Fact]
            public void ReturnsInstance()
            {
                // Arrange
                var appId = 247080U;
                var connectionString = "myConnectionString";

                // Act
                var worker = new DailyLeaderboardsWorker(appId, connectionString);

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
                DailyLeaderboards.AddRange(new[]
                {
                    new DailyLeaderboard { Date = today.AddDays(-1) },
                });
                Products.AddRange(new[]
                {
                    new Product(0, "classic", "Classic"),
                });
                MockSteamClient
                    .Setup(s => s.FindLeaderboardAsync(AppId, It.IsAny<string>(), CancellationToken))
                    .ReturnsAsync(Mock.Of<IFindOrCreateLeaderboardCallback>());

                // Act
                var dailyLeaderboards2 = await Worker.GetDailyLeaderboardsAsync(Db, SteamClient, Limit, CancellationToken);

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
                DailyLeaderboards.AddRange(new[]
                {
                    new DailyLeaderboard { Date = today },
                    new DailyLeaderboard { Date = today.AddDays(-1) },
                });

                // Act
                var staleDailyLeaderboards = await Worker.GetStaleDailyLeaderboardsAsync(Db, today, Limit, CancellationToken);

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
                DailyLeaderboards.Add(current);

                // Act
                var leaderboards = await Worker.GetCurrentDailyLeaderboardsAsync(Db, SteamClient, today, CancellationToken);

                // Assert
                var leaderboard = leaderboards.First();
                Assert.Equal(today, leaderboard.Date);
                Assert.Equal("classic", leaderboard.Product.Name);
                MockSteamClient.Verify(s => s.FindLeaderboardAsync(AppId, It.IsAny<string>(), CancellationToken), Times.Never);
            }

            [Fact]
            public async Task CurrentDailyLeaderboardsDoNotExist_GetsAndReturnsCurrentDailyLeaderboards()
            {
                // Arrange
                Products.AddRange(new[]
                {
                    new Product(0, "classic", "Classic"),
                    new Product(1, "amplified", "Amplified"),
                });
                var today = new DateTime(2017, 9, 13);
                MockSteamClient
                    .Setup(s => s.FindLeaderboardAsync(AppId, It.IsAny<string>(), CancellationToken))
                    .ReturnsAsync(Mock.Of<IFindOrCreateLeaderboardCallback>());

                // Act
                var leaderboards = await Worker.GetCurrentDailyLeaderboardsAsync(Db, SteamClient, today, CancellationToken);

                // Assert
                var leaderboard = leaderboards.First();
                Assert.Equal(today, leaderboard.Date);
                Assert.Equal(0, leaderboard.ProductId);
                MockSteamClient.Verify(s => s.FindLeaderboardAsync(AppId, It.IsAny<string>(), CancellationToken), Times.Exactly(2));
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
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboard1.LeaderboardId, CancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback1);
                var leaderboardEntriesCallback2 = new LeaderboardEntriesCallback();
                leaderboardEntriesCallback2.Entries.AddRange(new[]
                {
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                });
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboard2.LeaderboardId, CancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback2);

                // Act
                await Worker.UpdateDailyLeaderboardsAsync(SteamClient, leaderboards, CancellationToken);

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
                MockSteamClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(AppId, leaderboard.LeaderboardId, CancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback);
            }

            private LeaderboardEntriesCallback leaderboardEntriesCallback;
            private DailyLeaderboard leaderboard;

            [Fact]
            public async Task SetsLastUpdate()
            {
                // Arrange -> Act
                await Worker.UpdateDailyLeaderboardAsync(SteamClient, leaderboard, CancellationToken);

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
                await Worker.UpdateDailyLeaderboardAsync(SteamClient, leaderboard, CancellationToken);

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
                await Worker.StoreDailyLeaderboardsAsync(StoreClient, leaderboards, CancellationToken);

                // Assert
                MockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<DailyLeaderboard>>(), CancellationToken), Times.Once);
            }

            [Fact]
            public async Task StoresPlayers()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry { SteamId = 453857 });
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await Worker.StoreDailyLeaderboardsAsync(StoreClient, leaderboards, CancellationToken);

                // Assert
                MockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Player>>(), It.IsAny<BulkUpsertOptions>(), CancellationToken), Times.Once);
            }

            [Fact]
            public async Task StoresReplays()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry { ReplayId = 3849753489753975 });
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await Worker.StoreDailyLeaderboardsAsync(StoreClient, leaderboards, CancellationToken);

                // Assert
                MockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Replay>>(), It.IsAny<BulkUpsertOptions>(), CancellationToken), Times.Once);
            }

            [Fact]
            public async Task StoresEntries()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry());
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await Worker.StoreDailyLeaderboardsAsync(StoreClient, leaderboards, CancellationToken);

                // Assert
                MockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<DailyEntry>>(), CancellationToken), Times.Once);
            }
        }
    }
}
