using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using toofz.NecroDancer.Leaderboards.EntityFramework;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.TestsShared;
using static SteamKit2.SteamUserStats.LeaderboardEntriesCallback;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class WorkerRoleTests
    {
        [TestClass]
        public class RunAsyncOverrideMethod
        {
            [TestMethod]
            public async Task SteamUserNameIsNull_ThrowsInvalidOperationException()
            {
                // Arrange
                var settings = new StubLeaderboardsSettings
                {
                    SteamUserName = null,
                    SteamPassword = new EncryptedSecret("a", 1),
                    LeaderboardsConnectionString = new EncryptedSecret("a", 1),
                    DailyLeaderboardsPerUpdate = 100,
                };
                var workerRole = new WorkerRoleAdapter(settings);
                var cancellationToken = CancellationToken.None;

                // Act -> Assert
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }

            [TestMethod]
            public async Task SteamUserNameIsEmpty_ThrowsInvalidOperationException()
            {
                // Arrange
                var settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "",
                    SteamPassword = new EncryptedSecret("a", 1),
                    LeaderboardsConnectionString = new EncryptedSecret("a", 1),
                    DailyLeaderboardsPerUpdate = 100,
                };
                var workerRole = new WorkerRoleAdapter(settings);
                var cancellationToken = CancellationToken.None;

                // Act -> Assert
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }

            [TestMethod]
            public async Task SteamPasswordIsNull_ThrowsInvalidOperationException()
            {
                // Arrange
                var settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "myUserName",
                    SteamPassword = null,
                    LeaderboardsConnectionString = new EncryptedSecret("a", 1),
                    DailyLeaderboardsPerUpdate = 100,
                };
                var workerRole = new WorkerRoleAdapter(settings);
                var cancellationToken = CancellationToken.None;

                // Act -> Assert
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }

            class WorkerRoleAdapter : WorkerRole
            {
                public WorkerRoleAdapter(ILeaderboardsSettings settings) : base(settings) { }

                public Task PublicRunAsyncOverride(CancellationToken cancellationToken) => RunAsyncOverride(cancellationToken);
            }

            [TestMethod]
            public async Task LeaderboardsConnectionStringIsNull_ThrowsInvalidOperationException()
            {
                // Arrange
                var settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "myUserName",
                    SteamPassword = new EncryptedSecret("a", 1),
                    LeaderboardsConnectionString = null,
                    DailyLeaderboardsPerUpdate = 100,
                };
                var workerRole = new WorkerRoleAdapter(settings);
                var cancellationToken = CancellationToken.None;

                // Act -> Assert
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }

            [TestMethod]
            public async Task DailyLeaderboardsPerUpdateIsLessThanNumberOfProducts_ThrowsInvalidOperationException()
            {
                // Arrange
                var settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "myUserName",
                    SteamPassword = new EncryptedSecret("a", 1),
                    LeaderboardsConnectionString = new EncryptedSecret("a", 1),
                    DailyLeaderboardsPerUpdate = 0,
                };
                var workerRole = new WorkerRoleAdapter(settings);
                var cancellationToken = CancellationToken.None;

                // Act -> Assert
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                {
                    return workerRole.PublicRunAsyncOverride(cancellationToken);
                });
            }
        }

        #region Leaderboards

        [TestClass]
        public class GetLeaderboardHeadersMethod
        {
            [TestMethod]
            public void ReturnsLeaderboardHeaders()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);

                // Act
                var leaderboards = workerRole.GetLeaderboardHeaders();

                // Assert
                Assert.IsInstanceOfType(leaderboards, typeof(IEnumerable<LeaderboardHeader>));
            }
        }

        [TestClass]
        public class DownloadLeaderboardsAsyncMethod
        {
            [TestMethod]
            public async Task DownloadsLeaderboards()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var leaderboardCategories = LeaderboardsResources.ReadLeaderboardCategories();
                var runs = leaderboardCategories["runs"];
                var characters = leaderboardCategories["characters"];
                var headers = workerRole.GetLeaderboardHeaders();
                var mockResponse = new Mock<ILeaderboardEntriesCallback>();
                mockResponse.SetupGet(r => r.Entries).Returns(new ReadOnlyCollection<LeaderboardEntry>(new List<LeaderboardEntry>()));
                var response = mockResponse.Object;
                var mockSteamClient = new Mock<ISteamClientApiClient>();
                mockSteamClient
                    .Setup(s => s.GetLeaderboardEntriesAsync(It.IsAny<uint>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));
                var steamClient = mockSteamClient.Object;

                // Act
                var leaderboards = await workerRole.DownloadLeaderboardsAsync(runs, characters, headers, steamClient, CancellationToken.None);

                // Assert
                Assert.IsInstanceOfType(leaderboards, typeof(IEnumerable<Leaderboard>));
                Assert.AreEqual(headers.Count(), leaderboards.Count());
            }
        }

        [TestClass]
        public class DownloadLeaderboardAsyncMethod
        {
            [TestMethod]
            public async Task ReturnsLeaderboard()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockResponse = new Mock<ILeaderboardEntriesCallback>();
                mockResponse.SetupGet(r => r.Entries).Returns(new ReadOnlyCollection<LeaderboardEntry>(new List<LeaderboardEntry>()));
                var response = mockResponse.Object;
                var mockSteamClient = new Mock<ISteamClientApiClient>();
                mockSteamClient
                    .Setup(s => s.GetLeaderboardEntriesAsync(It.IsAny<uint>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));
                var steamClient = mockSteamClient.Object;
                var leaderboardId = 739999;
                var characterId = 0;
                var runId = 1;

                // Act
                var leaderboard = await workerRole.DownloadLeaderboardAsync(steamClient, leaderboardId, runId, characterId, CancellationToken.None);

                // Assert
                Assert.IsInstanceOfType(leaderboard, typeof(Leaderboard));
            }
        }

        [TestClass]
        public class StoreLeaderboardsAsyncMethod
        {
            [TestMethod]
            public async Task StoresLeaderboards()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new Leaderboard();
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await workerRole.StoreLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Leaderboard>>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresPlayers()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry { SteamId = 453857 });
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await workerRole.StoreLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Player>>(), false, It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresReplays()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry { ReplayId = 3849753489753975 });
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await workerRole.StoreLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Replay>>(), false, It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresEntries()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry());
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await workerRole.StoreLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Entry>>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        #endregion

        #region Daily Leaderboards

        [TestClass]
        public class GetStaleDailyLeaderboardHeadersAsyncMethod
        {
            [TestMethod]
            public async Task ReturnsStaleDailyLeaderboards()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var leaderboardCategories = LeaderboardsResources.ReadLeaderboardCategories();
                var products = leaderboardCategories["products"];
                var mockDb = new Mock<LeaderboardsContext>();
                var db = mockDb.Object;
                var today = new DateTime(2017, 9, 13);
                var mockDailyLeaderboards = MockHelper.MockSet(
                    new DailyLeaderboard { Date = today },
                    new DailyLeaderboard { Date = today.AddDays(-1) });
                var dailyLeaderboards = mockDailyLeaderboards.Object;
                mockDb.Setup(d => d.DailyLeaderboards).Returns(dailyLeaderboards);
                var limit = 100;
                var cancellationToken = CancellationToken.None;

                // Act
                var staleDailyLeaderboards = await workerRole.GetStaleDailyLeaderboardHeadersAsync(products, db, today, limit, cancellationToken);

                // Assert
                Assert.IsInstanceOfType(staleDailyLeaderboards, typeof(IEnumerable<DailyLeaderboardHeader>));
                foreach (var staleDailyLeaderboard in staleDailyLeaderboards)
                {
                    Assert.AreNotEqual(today, staleDailyLeaderboard.Date);
                }
            }
        }

        [TestClass]
        public class GetCurrentDailyLeaderboardHeadersAsyncMethod
        {
            [TestMethod]
            public async Task CurrentDailyLeaderboardsExist_ReturnsExistingCurrentDailyLeaderboards()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var products = new Category
                {
                    { "classic", new CategoryItem { Id = 0 } },
                };
                var mockDb = new Mock<LeaderboardsContext>();
                var db = mockDb.Object;
                var mockSteamClient = new Mock<ISteamClientApiClient>();
                var steamClient = mockSteamClient.Object;
                var today = new DateTime(2017, 9, 13);
                var current = new DailyLeaderboard { Date = today, ProductId = 0 };
                var mockDailyLeaderboards = MockHelper.MockSet(current);
                var dailyLeaderboards = mockDailyLeaderboards.Object;
                mockDb.Setup(d => d.DailyLeaderboards).Returns(dailyLeaderboards);
                var cancellationToken = CancellationToken.None;

                // Act
                var headers = await workerRole.GetCurrentDailyLeaderboardHeadersAsync(products, db, steamClient, today, cancellationToken);

                // Assert
                var header = headers.First();
                Assert.AreEqual(today, header.Date);
                Assert.AreEqual("classic", header.Product);
                mockSteamClient.Verify(s => s.FindLeaderboardAsync(It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [TestMethod]
            public async Task CurrentDailyLeaderboardsDoNotExist_GetsAndReturnsCurrentDailyLeaderboards()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var products = new Category
                {
                    { "classic", new CategoryItem { Id = 0 } },
                };
                var mockDb = new Mock<LeaderboardsContext>();
                var db = mockDb.Object;
                var mockSteamClient = new Mock<ISteamClientApiClient>();
                mockSteamClient
                    .Setup(s => s.FindLeaderboardAsync(It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(Mock.Of<IFindOrCreateLeaderboardCallback>()));
                var steamClient = mockSteamClient.Object;
                var today = new DateTime(2017, 9, 13);
                var mockDailyLeaderboards = MockHelper.MockSet<DailyLeaderboard>();
                var dailyLeaderboards = mockDailyLeaderboards.Object;
                mockDb.Setup(d => d.DailyLeaderboards).Returns(dailyLeaderboards);
                var cancellationToken = CancellationToken.None;

                // Act
                var headers = await workerRole.GetCurrentDailyLeaderboardHeadersAsync(products, db, steamClient, today, cancellationToken);

                // Assert
                var header = headers.First();
                Assert.AreEqual(today, header.Date);
                Assert.AreEqual("classic", header.Product);
                mockSteamClient.Verify(s => s.FindLeaderboardAsync(It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [TestClass]
        public class GetDailyLeaderboardHeaderAsyncMethod
        {
            [TestMethod]
            public async Task ReturnsDailyLeaderboardHeader()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockSteamClient = new Mock<ISteamClientApiClient>();
                mockSteamClient
                    .Setup(s => s.FindLeaderboardAsync(It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(Mock.Of<IFindOrCreateLeaderboardCallback>()));
                var steamClient = mockSteamClient.Object;
                var product = "classic";
                var date = new DateTime(2017, 9, 13);
                var isProduction = false;

                // Act
                var header = await workerRole.GetDailyLeaderboardHeaderAsync(steamClient, product, date, isProduction, CancellationToken.None);

                // Assert
                Assert.IsInstanceOfType(header, typeof(DailyLeaderboardHeader));
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
                var isProduction = false;

                // Act
                var name = WorkerRole.GetDailyLeaderboardName(product, date, isProduction);

                // Assert
                Assert.AreEqual("DLC 13/9/2017", name);
            }

            [TestMethod]
            public void ProductIsClassic_ReturnsName()
            {
                // Arrange
                var product = "classic";
                var date = new DateTime(2017, 9, 13);
                var isProduction = false;

                // Act
                var name = WorkerRole.GetDailyLeaderboardName(product, date, isProduction);

                // Assert
                Assert.AreEqual("13/9/2017", name);
            }

            [TestMethod]
            public void ProductIsInvalid_ThrowsArgumentException()
            {
                // Arrange
                var product = "";
                var date = new DateTime(2017, 9, 13);
                var isProduction = false;

                // Act -> Assert
                Assert.ThrowsException<ArgumentException>(() =>
                {
                    WorkerRole.GetDailyLeaderboardName(product, date, isProduction);
                });
            }

            [TestMethod]
            public void ReturnsNameWithFormattedDate()
            {
                // Arrange
                var product = "classic";
                var date = new DateTime(2017, 9, 13);
                var isProduction = false;

                // Act
                var name = WorkerRole.GetDailyLeaderboardName(product, date, isProduction);

                // Assert
                Assert.AreEqual("13/9/2017", name);
            }

            [TestMethod]
            public void IsProductionIsTrue_ReturnsNameEndingWith_PROD()
            {
                // Arrange
                var product = "classic";
                var date = new DateTime(2017, 9, 13);
                var isProduction = true;

                // Act
                var name = WorkerRole.GetDailyLeaderboardName(product, date, isProduction);

                // Assert
                Assert.AreEqual("13/9/2017_PROD", name);
            }

            [TestMethod]
            public void IsProductionIsFalse_ReturnsName()
            {
                // Arrange
                var product = "classic";
                var date = new DateTime(2017, 9, 13);
                var isProduction = false;

                // Act
                var name = WorkerRole.GetDailyLeaderboardName(product, date, isProduction);

                // Assert
                Assert.AreEqual("13/9/2017", name);
            }
        }

        [TestClass]
        public class GetDailyLeaderboardsAsyncMethod
        {
            [TestMethod]
            public async Task DownloadsDailyLeaderboards()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var leaderboardCategories = LeaderboardsResources.ReadLeaderboardCategories();
                var products = leaderboardCategories["products"];
                var headers = new List<DailyLeaderboardHeader>
                {
                    new DailyLeaderboardHeader
                    {
                        Id = 3847234,
                        Product = "amplified",
                        IsProduction = true,
                        Date = new DateTime(2017, 9, 13),
                    },
                };
                var mockResponse = new Mock<ILeaderboardEntriesCallback>();
                mockResponse.SetupGet(r => r.Entries).Returns(new ReadOnlyCollection<LeaderboardEntry>(new List<LeaderboardEntry>()));
                var response = mockResponse.Object;
                var mockSteamClient = new Mock<ISteamClientApiClient>();
                mockSteamClient
                    .Setup(s => s.GetLeaderboardEntriesAsync(It.IsAny<uint>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));
                var steamClient = mockSteamClient.Object;
                var cancellationToken = CancellationToken.None;

                // Act
                var leaderboards = await workerRole.GetDailyLeaderboardsAsync(products, headers, steamClient, cancellationToken);

                // Assert
                Assert.IsInstanceOfType(leaderboards, typeof(IEnumerable<DailyLeaderboard>));
                Assert.AreEqual(headers.Count(), leaderboards.Count());
            }
        }

        [TestClass]
        public class GetDailyLeaderboardEntriesAsyncMethod
        {
            [TestMethod]
            public async Task ReturnsDailyLeaderboardEntries()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockResponse = new Mock<ILeaderboardEntriesCallback>();
                mockResponse.SetupGet(r => r.Entries).Returns(new ReadOnlyCollection<LeaderboardEntry>(new List<LeaderboardEntry>()));
                var response = mockResponse.Object;
                var mockSteamClient = new Mock<ISteamClientApiClient>();
                mockSteamClient
                    .Setup(s => s.GetLeaderboardEntriesAsync(It.IsAny<uint>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));
                var steamClient = mockSteamClient.Object;
                var leaderboardId = 739999;
                var productId = 0;
                var date = new DateTime(2017, 9, 13);
                var isProduction = true;
                var cancellationToken = CancellationToken.None;

                // Act
                var leaderboard =
                    await workerRole.GetDailyLeaderboardEntriesAsync(
                        steamClient,
                        leaderboardId,
                        productId,
                        date,
                        isProduction,
                        cancellationToken);

                // Assert
                Assert.IsInstanceOfType(leaderboard, typeof(DailyLeaderboard));
            }
        }

        [TestClass]
        public class StoreDailyLeaderboardsAsyncMethod
        {
            [TestMethod]
            public async Task StoresLeaderboards()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new DailyLeaderboard();
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await workerRole.StoreDailyLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<DailyLeaderboard>>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresPlayers()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry { SteamId = 453857 });
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await workerRole.StoreDailyLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Player>>(), false, It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresReplays()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry { ReplayId = 3849753489753975 });
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await workerRole.StoreDailyLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<Replay>>(), false, It.IsAny<CancellationToken>()), Times.Once);
            }

            [TestMethod]
            public async Task StoresEntries()
            {
                // Arrange
                var settings = Mock.Of<ILeaderboardsSettings>();
                var workerRole = new WorkerRole(settings);
                var mockStoreClient = new Mock<ILeaderboardsStoreClient>();
                var storeClient = mockStoreClient.Object;
                var leaderboard = new DailyLeaderboard();
                leaderboard.Entries.Add(new DailyEntry());
                var leaderboards = new List<DailyLeaderboard> { leaderboard };

                // Act
                await workerRole.StoreDailyLeaderboardsAsync(storeClient, leaderboards, CancellationToken.None);

                // Assert
                mockStoreClient.Verify(s => s.SaveChangesAsync(It.IsAny<IEnumerable<DailyEntry>>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        #endregion
    }
}
