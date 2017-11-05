using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal sealed class LeaderboardsWorker : LeaderboardsWorkerBase
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
                await UpdateLeaderboardsAsync(steamClient, leaderboards, cancellationToken).ConfigureAwait(false);

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                    var storeClient = new LeaderboardsStoreClient(connection);
                    await StoreLeaderboardsAsync(storeClient, leaderboards, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        internal async Task UpdateLeaderboardsAsync(
            ISteamClientApiClient steamClient,
            IEnumerable<Leaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var activity = new DownloadActivity(Log, "leaderboards"))
            {
                steamClient.Progress = activity;

                var leaderboardTasks = new List<Task>();
                foreach (var leaderboard in leaderboards)
                {
                    var leaderboardTask = UpdateLeaderboardAsync(steamClient, leaderboard, cancellationToken);
                    leaderboardTasks.Add(leaderboardTask);
                }

                await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);

                steamClient.Progress = null;
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
    }
}
