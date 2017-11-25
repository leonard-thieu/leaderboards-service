using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal sealed class LeaderboardsWorker
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LeaderboardsWorker));

        public LeaderboardsWorker(
            uint appId,
            ILeaderboardsContext db,
            ISteamCommunityDataClient steamCommunityDataClient,
            ILeaderboardsStoreClient storeClient,
            TelemetryClient telemetryClient)
        {
            this.appId = appId;
            this.telemetryClient = telemetryClient;
            this.db = db;
            this.steamCommunityDataClient = steamCommunityDataClient;
            this.storeClient = storeClient;
        }

        private readonly uint appId;
        private readonly TelemetryClient telemetryClient;
        private readonly ILeaderboardsContext db;
        private readonly ISteamCommunityDataClient steamCommunityDataClient;
        private readonly ILeaderboardsStoreClient storeClient;

        #region Get leaderboards

        public Task<List<Leaderboard>> GetLeaderboardsAsync(CancellationToken cancellationToken)
        {
            return db.Leaderboards.ToListAsync(cancellationToken);
        }

        #endregion

        #region Update leaderboards

        public async Task UpdateLeaderboardsAsync(
            IEnumerable<Leaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Download leaderboards"))
            using (var activity = new DownloadActivity(Log, "leaderboards"))
            {
                try
                {
                    var leaderboardsEnvelope = await steamCommunityDataClient.GetLeaderboardsAsync(appId, activity, cancellationToken).ConfigureAwait(false);
                    var headers = leaderboardsEnvelope.Leaderboards;

                    var leaderboardTasks = new List<Task>();
                    foreach (var leaderboard in leaderboards)
                    {
                        var header = headers.FirstOrDefault(h => h.LeaderboardId == leaderboard.LeaderboardId);
                        if (header != null)
                        {
                            var leaderboardTask = UpdateLeaderboardAsync(leaderboard, header.EntryCount, activity, cancellationToken);
                            leaderboardTasks.Add(leaderboardTask);
                        }
                    }

                    await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);

                    operation.Telemetry.Success = true;
                }
                catch (Exception) when (Util.FailTelemetry(operation.Telemetry))
                {
                    // Unreachable
                    throw;
                }
            }
        }

        #region Steam Community Data

        internal async Task UpdateLeaderboardAsync(
            Leaderboard leaderboard,
            int entryCount,
            IProgress<long> progress,
            CancellationToken cancellationToken)
        {
            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Download leaderboard"))
            {
                try
                {
                    var entriesTasks = new List<Task<IEnumerable<Entry>>>();
                    var leaderboardId = leaderboard.LeaderboardId;
                    for (int i = 1; i < entryCount; i += SteamCommunityDataClient.MaxLeaderboardEntriesPerRequest)
                    {
                        var entriesTask = GetLeaderboardEntriesAsync(leaderboardId, i, progress, cancellationToken);
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
                catch (Exception) when (Util.FailTelemetry(operation.Telemetry))
                {
                    // Unreachable
                    throw;
                }
            }
        }

        internal async Task<IEnumerable<Entry>> GetLeaderboardEntriesAsync(
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
                catch (Exception) when (Util.FailTelemetry(operation.Telemetry))
                {
                    // Unreachable
                    throw;
                }
            }
        }

        #endregion

        #endregion

        #region Store leaderboards

        public async Task StoreLeaderboardsAsync(
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
                catch (Exception) when (Util.FailTelemetry(operation.Telemetry))
                {
                    // Unreachable
                    throw;
                }
            }
        }

        #endregion
    }
}
