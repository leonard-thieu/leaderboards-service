﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;
using Moq;
using toofz.Data;
using toofz.Services.LeaderboardsService.Tests.Properties;
using toofz.Steam.CommunityData;
using Xunit;
using Leaderboard = toofz.Data.Leaderboard;

namespace toofz.Services.LeaderboardsService.Tests
{
    public class LeaderboardsWorkerTests
    {
        public LeaderboardsWorkerTests()
        {
            var db = new NecroDancerContext(necroDancerContextOptions);

            worker = new LeaderboardsWorker(appId, db, mockSteamCommunityDataClient.Object, mockStoreClient.Object, telemetryClient);
        }

        private readonly uint appId = 247080;
        private readonly Mock<ISteamCommunityDataClient> mockSteamCommunityDataClient = new Mock<ISteamCommunityDataClient>();
        private readonly Mock<ILeaderboardsStoreClient> mockStoreClient = new Mock<ILeaderboardsStoreClient>();
        private readonly TelemetryClient telemetryClient = new TelemetryClient();
        private readonly LeaderboardsWorker worker;

        private readonly DbContextOptions<NecroDancerContext> necroDancerContextOptions = new DbContextOptionsBuilder<NecroDancerContext>()
            .UseInMemoryDatabase(databaseName: Constants.NecroDancerContextName)
            .Options;

        public class Constructor
        {
            [DisplayFact(nameof(LeaderboardsWorker))]
            public void ReturnsLeaderboardsWorker()
            {
                // Arrange
                var appId = 247080U;
                var telemetryClient = new TelemetryClient();
                var db = Mock.Of<ILeaderboardsContext>();
                var steamCommunityDataClient = Mock.Of<ISteamCommunityDataClient>();
                var storeClient = Mock.Of<ILeaderboardsStoreClient>();

                // Act
                var worker = new LeaderboardsWorker(appId, db, steamCommunityDataClient, storeClient, telemetryClient);

                // Assert
                Assert.IsAssignableFrom<LeaderboardsWorker>(worker);
            }
        }

        public class GetLeaderboardsAsyncMethod : LeaderboardsWorkerTests
        {
            private readonly CancellationToken cancellationToken = CancellationToken.None;

            [DisplayFact]
            public async Task ReturnsLeaderboards()
            {
                // Arrange -> Act
                var leaderboards = await worker.GetLeaderboardsAsync(cancellationToken);

                // Assert
                Assert.IsAssignableFrom<IEnumerable<Leaderboard>>(leaderboards);
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
                mockSteamCommunityDataClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId.ToString(), leaderboardId, It.Is<GetLeaderboardEntriesParams>(p => p.StartRange == 1), progress, cancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_1);
                var leaderboardEntries_2047540_2 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_2);
                mockSteamCommunityDataClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId.ToString(), leaderboardId, It.Is<GetLeaderboardEntriesParams>(p => p.StartRange == 5002), progress, cancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_2);
            }

            private readonly Leaderboard leaderboard;
            private int entryCount;
            private readonly IProgress<long> progress = Mock.Of<IProgress<long>>();
            private readonly CancellationToken cancellationToken = CancellationToken.None;

            [DisplayFact(nameof(ArgumentException))]
            public async Task NoEntries_DoesNotThrowArgumentException()
            {
                // Arrange
                entryCount = 0;

                // Act
                var ex = await Record.ExceptionAsync(() =>
                {
                    return worker.UpdateLeaderboardAsync(leaderboard, entryCount, progress, cancellationToken);
                });

                // Assert
                Assert.IsNotType<ArgumentException>(ex);
            }

            [DisplayFact]
            public async Task AddsUpdatedEntries()
            {
                // Arrange -> Act
                await worker.UpdateLeaderboardAsync(leaderboard, entryCount, progress, cancellationToken);

                // Assert
                Assert.Equal(entryCount, leaderboard.Entries.Count());
            }

            [DisplayFact(nameof(Leaderboard.LastUpdate))]
            public async Task SetsLastUpdate()
            {
                // Arrange -> Act
                await worker.UpdateLeaderboardAsync(leaderboard, entryCount, progress, cancellationToken);

                // Assert
                Assert.NotNull(leaderboard.LastUpdate);
            }
        }

        public class GetLeaderboardEntriesAsyncMethod : LeaderboardsWorkerTests
        {
            private readonly IProgress<long> progress = Mock.Of<IProgress<long>>();
            private readonly CancellationToken cancellationToken = CancellationToken.None;

            [DisplayFact]
            public async Task ReturnsEntries()
            {
                // Arrange
                var leaderboardId = 2047540;
                var startRange = 1;
                var leaderboardEntries_2047540_1 = DataHelper.DeserializeLeaderboardEntriesEnvelope(Resources.LeaderboardEntries_2047540_1);
                mockSteamCommunityDataClient
                    .Setup(c => c.GetLeaderboardEntriesAsync(appId.ToString(), leaderboardId, It.Is<GetLeaderboardEntriesParams>(p => p.StartRange == startRange), progress, cancellationToken))
                    .ReturnsAsync(leaderboardEntries_2047540_1);

                // Act
                var entries = await worker.GetLeaderboardEntriesAsync(leaderboardId, startRange, progress, cancellationToken);

                // Assert
                Assert.Equal(5001, entries.Count());
            }
        }

        public class StoreLeaderboardsAsyncMethod : LeaderboardsWorkerTests
        {
            private readonly CancellationToken cancellationToken = CancellationToken.None;

            [DisplayFact]
            public async Task StoresLeaderboards()
            {
                // Arrange
                var leaderboard = new Leaderboard();
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await worker.StoreLeaderboardsAsync(leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Leaderboard>>(), null, cancellationToken), Times.Once);
            }

            [DisplayFact]
            public async Task StoresPlayers()
            {
                // Arrange
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry { SteamId = 453857 });
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await worker.StoreLeaderboardsAsync(leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Player>>(), It.IsAny<BulkUpsertOptions>(), cancellationToken), Times.Once);
            }

            [DisplayFact]
            public async Task StoresReplays()
            {
                // Arrange
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry { ReplayId = 3849753489753975 });
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await worker.StoreLeaderboardsAsync(leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkUpsertAsync(It.IsAny<IEnumerable<Replay>>(), It.IsAny<BulkUpsertOptions>(), cancellationToken), Times.Once);
            }

            [DisplayFact]
            public async Task StoresEntries()
            {
                // Arrange
                var leaderboard = new Leaderboard();
                leaderboard.Entries.Add(new Entry());
                var leaderboards = new List<Leaderboard> { leaderboard };

                // Act
                await worker.StoreLeaderboardsAsync(leaderboards, cancellationToken);

                // Assert
                mockStoreClient.Verify(s => s.BulkInsertAsync(It.IsAny<IEnumerable<Entry>>(), cancellationToken), Times.Once);
            }
        }
    }
}
