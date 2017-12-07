using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Moq;
using toofz.Data;
using toofz.Steam.ClientApi;
using Xunit;
using static SteamKit2.SteamUserStats;

namespace toofz.Services.LeaderboardsService.Tests
{
    public class DailyLeaderboardsWorkerTests
    {
        public DailyLeaderboardsWorkerTests()
        {
            var products = new FakeDbSet<Product>(productsInner);
            mockDb.Setup(d => d.Products).Returns(products);
            var dailyLeaderboards = new FakeDbSet<DailyLeaderboard>(dailyLeaderboardsInner);
            mockDb.Setup(d => d.DailyLeaderboards).Returns(dailyLeaderboards);

            worker = new DailyLeaderboardsWorker(appId, mockDb.Object, mockSteamClientApiClient.Object, mockStoreClient.Object, telemetryClient);
        }

        private readonly uint appId = 247080;
        private readonly Mock<ILeaderboardsContext> mockDb = new Mock<ILeaderboardsContext>();
        private readonly Mock<ISteamClientApiClient> mockSteamClientApiClient = new Mock<ISteamClientApiClient>();
        private readonly Mock<ILeaderboardsStoreClient> mockStoreClient = new Mock<ILeaderboardsStoreClient>();
        private readonly TelemetryClient telemetryClient = new TelemetryClient();
        private readonly DailyLeaderboardsWorker worker;

        private readonly List<Product> productsInner = new List<Product>();
        private readonly List<DailyLeaderboard> dailyLeaderboardsInner = new List<DailyLeaderboard>();

        public class Constructor
        {
            [DisplayFact]
            public void ReturnsInstance()
            {
                // Arrange
                var appId = 247080U;
                var db = Mock.Of<ILeaderboardsContext>();
                var steamClientApiClient = Mock.Of<ISteamClientApiClient>();
                var storeClient = Mock.Of<ILeaderboardsStoreClient>();
                var telemetryClient = new TelemetryClient();

                // Act
                var worker = new DailyLeaderboardsWorker(appId, db, steamClientApiClient, storeClient, telemetryClient);

                // Assert
                Assert.IsAssignableFrom<DailyLeaderboardsWorker>(worker);
            }
        }

        public class GetDailyLeaderboardsAsyncMethod : DailyLeaderboardsWorkerTests
        {
            private readonly int limit = 100;
            private readonly CancellationToken cancellationToken = CancellationToken.None;

            [DisplayFact]
            public async Task ReturnsDailyLeaderboards()
            {
                // Arrange
                var today = DateTime.UtcNow.Date;
                dailyLeaderboardsInner.AddRange(new[]
                {
                    new DailyLeaderboard { Date = today.AddDays(-1) },
                });
                productsInner.AddRange(new[]
                {
                    new Product(0, "classic", "Classic"),
                });
                mockSteamClientApiClient
                    .Setup(s => s.FindLeaderboardAsync(appId, It.IsAny<string>(), cancellationToken))
                    .ReturnsAsync(Mock.Of<IFindOrCreateLeaderboardCallback>());

                // Act
                var dailyLeaderboards2 = await worker.GetDailyLeaderboardsAsync(limit, cancellationToken);

                // Assert
                Assert.Equal(2, dailyLeaderboards2.Count());
            }
        }

        public class GetStaleDailyLeaderboardsAsyncMethod : DailyLeaderboardsWorkerTests
        {
            private readonly int limit = 100;
            private readonly CancellationToken cancellationToken = CancellationToken.None;

            [DisplayFact]
            public async Task ReturnsStaleDailyLeaderboards()
            {
                // Arrange
                var today = new DateTime(2017, 9, 13);
                dailyLeaderboardsInner.AddRange(new[]
                {
                    new DailyLeaderboard { Date = today },
                    new DailyLeaderboard { Date = today.AddDays(-1) },
                });

                // Act
                var staleDailyLeaderboards = await worker.GetStaleDailyLeaderboardsAsync(today, limit, cancellationToken);

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
            private readonly CancellationToken cancellationToken = CancellationToken.None;

            [DisplayFact]
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
                dailyLeaderboardsInner.Add(current);

                // Act
                var leaderboards = await worker.GetCurrentDailyLeaderboardsAsync(today, cancellationToken);

                // Assert
                var leaderboard = leaderboards.First();
                Assert.Equal(today, leaderboard.Date);
                Assert.Equal("classic", leaderboard.Product.Name);
                mockSteamClientApiClient.Verify(s => s.FindLeaderboardAsync(appId, It.IsAny<string>(), cancellationToken), Times.Never);
            }

            [DisplayFact]
            public async Task CurrentDailyLeaderboardsDoNotExist_GetsAndReturnsCurrentDailyLeaderboards()
            {
                // Arrange
                productsInner.AddRange(new[]
                {
                    new Product(0, "classic", "Classic"),
                    new Product(1, "amplified", "Amplified"),
                });
                var today = new DateTime(2017, 9, 13);
                mockSteamClientApiClient
                    .Setup(s => s.FindLeaderboardAsync(appId, It.IsAny<string>(), cancellationToken))
                    .ReturnsAsync(Mock.Of<IFindOrCreateLeaderboardCallback>());

                // Act
                var leaderboards = await worker.GetCurrentDailyLeaderboardsAsync(today, cancellationToken);

                // Assert
                var leaderboard = leaderboards.First();
                Assert.Equal(today, leaderboard.Date);
                Assert.Equal(0, leaderboard.ProductId);
                mockSteamClientApiClient.Verify(s => s.FindLeaderboardAsync(appId, It.IsAny<string>(), cancellationToken), Times.Exactly(2));
            }
        }

        public class GetDailyLeaderboardNameMethod
        {
            [DisplayFact("Amplified")]
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

            [DisplayFact("Classic")]
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

            [DisplayFact(nameof(ArgumentException))]
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

            [DisplayFact]
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
            private readonly CancellationToken cancellationToken = CancellationToken.None;

            [DisplayFact]
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
                mockSteamClientApiClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, leaderboard1.LeaderboardId, cancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback1);
                var leaderboardEntriesCallback2 = new LeaderboardEntriesCallback();
                leaderboardEntriesCallback2.Entries.AddRange(new[]
                {
                    new LeaderboardEntry(),
                    new LeaderboardEntry(),
                });
                mockSteamClientApiClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, leaderboard2.LeaderboardId, cancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback2);

                // Act
                await worker.UpdateDailyLeaderboardsAsync(leaderboards, cancellationToken);

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
                mockSteamClientApiClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId, leaderboard.LeaderboardId, cancellationToken))
                    .ReturnsAsync(leaderboardEntriesCallback);
            }

            private LeaderboardEntriesCallback leaderboardEntriesCallback;
            private DailyLeaderboard leaderboard;
            private readonly CancellationToken cancellationToken = CancellationToken.None;

            [DisplayFact(nameof(DailyLeaderboard.LastUpdate))]
            public async Task SetsLastUpdate()
            {
                // Arrange -> Act
                await worker.UpdateDailyLeaderboardAsync(leaderboard, cancellationToken);

                // Assert
                Assert.NotNull(leaderboard.LastUpdate);
            }

            [DisplayFact]
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
                await worker.UpdateDailyLeaderboardAsync(leaderboard, cancellationToken);

                // Assert
                Assert.Equal(3, leaderboard.Entries.Count);
            }
        }

        public class StoreDailyLeaderboardsAsyncMethod : DailyLeaderboardsWorkerTests
        {
            private readonly CancellationToken cancellationToken = CancellationToken.None;

            [DisplayFact]
            public async Task StoresLeaderboards()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<DailyLeaderboard>>(), null, cancellationToken), Times.Once);
            }

            [DisplayFact]
            public async Task StoresPlayers()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry { SteamId = 453857 });
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Player>>(), It.IsAny<BulkUpsertOptions>(), cancellationToken), Times.Once);
            }

            [DisplayFact]
            public async Task StoresReplays()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry { ReplayId = 3849753489753975 });
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Replay>>(), It.IsAny<BulkUpsertOptions>(), cancellationToken), Times.Once);
            }

            [DisplayFact]
            public async Task StoresEntries()
            {
                // Arrange
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry());
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await worker.StoreDailyLeaderboardsAsync(leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<DailyEntry>>(), null, cancellationToken), Times.Once);
            }
        }
    }
}
