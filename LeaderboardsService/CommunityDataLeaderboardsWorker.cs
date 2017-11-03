using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal sealed class CommunityDataLeaderboardsWorker : LeaderboardsWorkerBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CommunityDataLeaderboardsWorker));

        public CommunityDataLeaderboardsWorker(uint appId, string connectionString)
        {
            this.appId = appId;
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        private readonly uint appId;
        private readonly string connectionString;

        public async Task UpdateAsync(CancellationToken cancellationToken)
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
                using (var steamClient = new SteamCommunityDataClient(handler))
                {
                    await UpdateLeaderboardsAsync(steamClient, leaderboards, cancellationToken).ConfigureAwait(false);
                }

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                    var storeClient = new LeaderboardsStoreClient(connection);
                    await StoreLeaderboardsAsync(storeClient, leaderboards, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        internal async Task UpdateLeaderboardsAsync(
            ISteamCommunityDataClient steamCommunityDataClient,
            IEnumerable<Leaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var activity = new DownloadActivity(Log, "leaderboards"))
            {
                var leaderboardsEnvelope = await steamCommunityDataClient.GetLeaderboardsAsync(appId, activity, cancellationToken).ConfigureAwait(false);
                var headers = leaderboardsEnvelope.Leaderboards;

                var leaderboardTasks = new List<Task>();
                foreach (var header in headers)
                {
                    var leaderboard = leaderboards.FirstOrDefault(l => l.LeaderboardId == header.LeaderboardId);
                    if (leaderboard != null)
                    {
                        var leaderboardTask = UpdateLeaderboardAsync(steamCommunityDataClient, leaderboard, header.EntryCount, activity, cancellationToken);
                        leaderboardTasks.Add(leaderboardTask);
                    }
                    else
                    {
                        // TODO: Leaderboard isn't visible from Steam Community Data, update the leaderboard from Steam Client API instead.
                    }
                }

                await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);
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

            var entriesTasks = new List<Task<IEnumerable<Entry>>>();
            for (int i = 1; i < entryCount; i += batchSize)
            {
                var entriesTask = GetLeaderboardEntriesAsync(steamClient, leaderboard.LeaderboardId, i, progress, cancellationToken);
                entriesTasks.Add(entriesTask);
            }

            if (entriesTasks.Any())
            {
                await Task.WhenAny(entriesTasks).ConfigureAwait(false);
            }
            leaderboard.LastUpdate = DateTime.UtcNow;

            var entries = await Task.WhenAll(entriesTasks).ConfigureAwait(false);
            var flattened = entries.SelectMany(l => l).ToList();

            leaderboard.Entries.AddRange(flattened);
        }

        internal async Task<IEnumerable<Entry>> GetLeaderboardEntriesAsync(
            ISteamCommunityDataClient steamClient,
            int leaderboardId,
            int startRange,
            IProgress<long> progress,
            CancellationToken cancellationToken)
        {
            var leaderboardEntriesEnvelope = await steamClient
                .GetLeaderboardEntriesAsync(appId, leaderboardId, new GetLeaderboardEntriesParams { StartRange = startRange }, progress, cancellationToken)
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
    }
}
