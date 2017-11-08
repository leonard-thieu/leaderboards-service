using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal sealed class LeaderboardsWorker
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LeaderboardsWorker));

        public LeaderboardsWorker(uint appId, string connectionString)
        {
            this.appId = appId;
            this.connectionString = connectionString;
        }

        private readonly uint appId;
        private readonly string connectionString;

        public async Task UpdateAsync(ISteamClientApiClient steamClient, CancellationToken cancellationToken)
        {
            using (new UpdateActivity(Log, "leaderboards"))
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
                    new SteamCommunityDataApiTransientFaultHandler(),
                });
                using (var steamCommunityDataClient = new SteamCommunityDataClient(handler))
                {
                    await UpdateLeaderboardsAsync(steamCommunityDataClient, steamClient, leaderboards, cancellationToken).ConfigureAwait(false);
                }

                using (var connection = new SqlConnection(connectionString))
                {
                    var storeClient = new LeaderboardsStoreClient(connection);
                    await StoreLeaderboardsAsync(storeClient, leaderboards, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        internal Task<List<Leaderboard>> GetLeaderboardsAsync(
            ILeaderboardsContext db,
            CancellationToken cancellationToken)
        {
            return db.Leaderboards.ToListAsync(cancellationToken);
        }

        internal async Task UpdateLeaderboardsAsync(
            ISteamCommunityDataClient steamCommunityDataClient,
            ISteamClientApiClient steamClient,
            IEnumerable<Leaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var activity = new DownloadActivity(Log, "leaderboards"))
            {
                var leaderboardsEnvelope = await steamCommunityDataClient.GetLeaderboardsAsync(appId, activity, cancellationToken).ConfigureAwait(false);
                var headers = leaderboardsEnvelope.Leaderboards;

#if FEATURE_LEADERBOARDS_VIA_STEAMCLIENT
                steamClient.Progress = activity;
                await steamClient.ConnectAndLogOnAsync().ConfigureAwait(false); 
#endif

                var leaderboardTasks = new List<Task>();
                foreach (var leaderboard in leaderboards)
                {
                    var header = headers.FirstOrDefault(h => h.LeaderboardId == leaderboard.LeaderboardId);
                    if (header != null)
                    {
                        var leaderboardTask = UpdateLeaderboardAsync(steamCommunityDataClient, leaderboard, header.EntryCount, activity, cancellationToken);
                        leaderboardTasks.Add(leaderboardTask);
                    }
#if FEATURE_LEADERBOARDS_VIA_STEAMCLIENT
                    // Leaderboard isn't visible from Steam Community Data, update the leaderboard from Steam Client API instead.
                    else
                    {
                        var leaderboardTask = UpdateLeaderboardAsync(steamClient, leaderboard, cancellationToken);
                        leaderboardTasks.Add(leaderboardTask); 
                    }
#endif
                }

                await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);

#if FEATURE_LEADERBOARDS_VIA_STEAMCLIENT
                steamClient.Progress = null; 
#endif
            }
        }

        internal async Task UpdateLeaderboardAsync(
            ISteamCommunityDataClient steamClient,
            Leaderboard leaderboard,
            int entryCount,
            IProgress<long> progress,
            CancellationToken cancellationToken)
        {
            var batchSize = SteamCommunityDataClient.MaxLeaderboardEntriesPerRequest;
            var leaderboardId = leaderboard.LeaderboardId;

            var entriesTasks = new List<Task<IEnumerable<Entry>>>();
            for (int i = 1; i < entryCount; i += batchSize)
            {
                var entriesTask = GetLeaderboardEntriesAsync(steamClient, leaderboardId, i, progress, cancellationToken);
                entriesTasks.Add(entriesTask);
            }

            if (entriesTasks.Any())
            {
                await Task.WhenAny(entriesTasks).ConfigureAwait(false);
            }
            leaderboard.LastUpdate = DateTime.UtcNow;

            var allEntries = await Task.WhenAll(entriesTasks).ConfigureAwait(false);
            for (int i = 0; i < allEntries.Length; i++)
            {
                var entries = allEntries[i];
                leaderboard.Entries.AddRange(entries);
            }
        }

        internal async Task UpdateLeaderboardAsync(
            ISteamClientApiClient steamClient,
            Leaderboard leaderboard,
            CancellationToken cancellationToken)
        {
            var response = await steamClient
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

        internal async Task<IEnumerable<Entry>> GetLeaderboardEntriesAsync(
            ISteamCommunityDataClient steamClient,
            int leaderboardId,
            int startRange,
            IProgress<long> progress,
            CancellationToken cancellationToken)
        {
            var @params = new GetLeaderboardEntriesParams { StartRange = startRange };
            var leaderboardEntriesEnvelope = await steamClient
                .GetLeaderboardEntriesAsync(appId, leaderboardId, @params, progress, cancellationToken)
                .ConfigureAwait(false);

            return leaderboardEntriesEnvelope.Entries.Select(e =>
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
        }

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
        }
    }
}
