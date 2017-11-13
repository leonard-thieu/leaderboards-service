﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal sealed class LeaderboardsWorker
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LeaderboardsWorker));

        public LeaderboardsWorker(uint appId, string connectionString, TelemetryClient telemetryClient)
        {
            this.appId = appId;
            this.connectionString = connectionString;
            this.telemetryClient = telemetryClient;
        }

        private readonly uint appId;
        private readonly string connectionString;
        private readonly TelemetryClient telemetryClient;

        public async Task UpdateAsync(ISteamClientApiClient steamClientApiClient, CancellationToken cancellationToken)
        {
            using (new UpdateActivity(Log, "leaderboards"))
            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Update leaderboards"))
            {
                try
                {
                    IEnumerable<Leaderboard> leaderboards;
                    using (var db = new LeaderboardsContext(connectionString))
                    {
                        leaderboards = await GetLeaderboardsAsync(db, cancellationToken).ConfigureAwait(false);
                    }

                    var handler = HttpClientFactory.CreatePipeline(new WebRequestHandler(), new DelegatingHandler[]
                    {
                        new LoggingHandler(),
                        new GZipHandler(),
                        new SteamCommunityDataTransientFaultHandler(),
                    });
                    var steamCommunityDataClientSettings = new SteamCommunityDataClientSettings { IsCacheBustingEnabled = false };
                    using (var steamCommunityDataClient = new SteamCommunityDataClient(handler, telemetryClient, steamCommunityDataClientSettings))
                    {
                        await UpdateLeaderboardsAsync(steamClientApiClient, leaderboards, cancellationToken).ConfigureAwait(false);
                    }

                    using (var connection = new SqlConnection(connectionString))
                    {
                        var storeClient = new LeaderboardsStoreClient(connection);
                        await StoreLeaderboardsAsync(storeClient, leaderboards, cancellationToken).ConfigureAwait(false);
                    }

                    operation.Telemetry.Success = true;
                }
                catch (Exception)
                {
                    operation.Telemetry.Success = false;
                    throw;
                }
            }
        }

        #region Get leaderboards

        internal Task<List<Leaderboard>> GetLeaderboardsAsync(
            ILeaderboardsContext db,
            CancellationToken cancellationToken)
        {
            return db.Leaderboards.ToListAsync(cancellationToken);
        }

        #endregion

        #region Update leaderboards

        internal async Task UpdateLeaderboardsAsync(
            ISteamClientApiClient steamClientApiClient,
            IEnumerable<Leaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var activity = new DownloadActivity(Log, "leaderboards"))
            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Download leaderboards"))
            {
                try
                {
                    steamClientApiClient.Progress = activity;

                    await steamClientApiClient.ConnectAndLogOnAsync().ConfigureAwait(false);

                    var leaderboardTasks = new List<Task>();
                    foreach (var leaderboard in leaderboards)
                    {
                        var leaderboardTask = UpdateLeaderboardAsync(steamClientApiClient, leaderboard, cancellationToken);
                        leaderboardTasks.Add(leaderboardTask);
                    }

                    await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);

                    steamClientApiClient.Progress = null;

                    operation.Telemetry.Success = true;
                }
                catch (Exception)
                {
                    operation.Telemetry.Success = false;
                    throw;
                }
            }
        }

        #region Steam Community Data

        internal async Task UpdateLeaderboardAsync(
            ISteamCommunityDataClient steamCommunityDataClient,
            Leaderboard leaderboard,
            int entryCount,
            IProgress<long> progress,
            CancellationToken cancellationToken)
        {
            var batchSize = SteamCommunityDataClient.MaxLeaderboardEntriesPerRequest;
            var leaderboardId = leaderboard.LeaderboardId;

            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Download leaderboard"))
            {
                try
                {
                    var entriesTasks = new List<Task<IEnumerable<Entry>>>();
                    for (int i = 1; i < entryCount; i += batchSize)
                    {
                        var entriesTask = GetLeaderboardEntriesAsync(steamCommunityDataClient, leaderboardId, i, progress, cancellationToken);
                        entriesTasks.Add(entriesTask);
                    }

                    if (entriesTasks.Any())
                    {
                        await Task.WhenAny(entriesTasks).ConfigureAwait(false);
                    }
                    leaderboard.LastUpdate = DateTime.UtcNow;

                    var allEntries = await Task.WhenAll(entriesTasks).ConfigureAwait(false);
                    foreach (var entries in allEntries)
                    {
                        leaderboard.Entries.AddRange(entries);
                    }

                    operation.Telemetry.Success = true;
                }
                catch (Exception)
                {
                    operation.Telemetry.Success = false;
                    throw;
                }
            }
        }

        internal async Task<IEnumerable<Entry>> GetLeaderboardEntriesAsync(
            ISteamCommunityDataClient steamCommunityDataClient,
            int leaderboardId,
            int startRange,
            IProgress<long> progress,
            CancellationToken cancellationToken)
        {
            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Download leaderboard entries"))
            {
                try
                {
                    var @params = new GetLeaderboardEntriesParams { StartRange = startRange };
                    var leaderboardEntriesEnvelope = await steamCommunityDataClient
                        .GetLeaderboardEntriesAsync(appId, leaderboardId, @params, progress, cancellationToken)
                        .ConfigureAwait(false);

                    var entries = leaderboardEntriesEnvelope.Entries.Select(e =>
                    {
                        var entry = new Entry
                        {
                            LeaderboardId = leaderboardId,
                            SteamId = e.SteamId,
                            Rank = e.Rank,
                            ReplayId = e.UgcId.ToReplayId(),
                            Score = e.Score,
                        };

                        var details = (from d in e.Details
                                       select int.Parse(d.ToString(), NumberStyles.HexNumber))
                                       .ToList();

                        entry.Zone = details[1];
                        entry.Level = details[9];

                        return entry;
                    }).ToList();

                    operation.Telemetry.Success = true;

                    return entries;
                }
                catch (Exception)
                {
                    operation.Telemetry.Success = false;
                    throw;
                }
            }
        }

        #endregion

        #region Steam Client API

        internal async Task UpdateLeaderboardAsync(
            ISteamClientApiClient steamClientApiClient,
            Leaderboard leaderboard,
            CancellationToken cancellationToken)
        {
            var response = await steamClientApiClient
                .GetLeaderboardEntriesAsync(appId, leaderboard.LeaderboardId, cancellationToken)
                .ConfigureAwait(false);

            leaderboard.LastUpdate = DateTime.UtcNow;

            foreach (var entry in response.Entries)
            {
                leaderboard.Entries.Add(new Entry
                {
                    LeaderboardId = leaderboard.LeaderboardId,
                    Rank = entry.GlobalRank,
                    SteamId = entry.SteamID.ToInt64(),
                    ReplayId = entry.UGCId.ToReplayId(),
                    Score = entry.Score,
                    Zone = entry.Details[0],
                    Level = entry.Details[1],
                });
            }
        }

        #endregion

        #endregion

        #region Store leaderboards

        internal async Task StoreLeaderboardsAsync(
            ILeaderboardsStoreClient storeClient,
            IEnumerable<Leaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            var entries = leaderboards
                .SelectMany(e => e.Entries)
                .ToList();
            var players = entries
                .Select(e => e.SteamId)
                .Distinct()
                .Select(s => new Player { SteamId = s })
                .ToList();
            var replays = entries
                .Where(e => e.ReplayId != null)
                .Select(e => e.ReplayId.Value)
                .Distinct()
                .Select(r => new Replay { ReplayId = r })
                .ToList();

            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Store leaderboards"))
            {
                try
                {
                    using (var activity = new StoreActivity(Log, "leaderboards"))
                    {
                        var rowsAffected = await storeClient.BulkUpsertAsync(leaderboards, cancellationToken).ConfigureAwait(false);
                        activity.Report(rowsAffected);
                    }

                    using (var activity = new StoreActivity(Log, "players"))
                    {
                        var options = new BulkUpsertOptions { UpdateWhenMatched = false };
                        var rowsAffected = await storeClient.BulkUpsertAsync(players, options, cancellationToken).ConfigureAwait(false);
                        activity.Report(rowsAffected);
                    }

                    using (var activity = new StoreActivity(Log, "replays"))
                    {
                        var options = new BulkUpsertOptions { UpdateWhenMatched = false };
                        var rowsAffected = await storeClient.BulkUpsertAsync(replays, options, cancellationToken).ConfigureAwait(false);
                        activity.Report(rowsAffected);
                    }

                    using (var activity = new StoreActivity(Log, "entries"))
                    {
                        var rowsAffected = await storeClient.BulkInsertAsync(entries, cancellationToken).ConfigureAwait(false);
                        activity.Report(rowsAffected);
                    }

                    operation.Telemetry.Success = true;
                }
                catch (Exception)
                {
                    operation.Telemetry.Success = false;
                    throw;
                }
            }
        }

        #endregion
    }
}
