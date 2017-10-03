using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    sealed class LeaderboardsWorker
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(LeaderboardsWorker));

        public LeaderboardsWorker(uint appId, string leaderboardsConnectionString)
        {
            this.appId = appId;
            this.leaderboardsConnectionString = leaderboardsConnectionString;
        }

        readonly uint appId;
        readonly string leaderboardsConnectionString;

        public async Task UpdateAsync(ISteamClientApiClient steamClient, CancellationToken cancellationToken)
        {
            using (new UpdateNotifier(Log, "leaderboards"))
            {
                IEnumerable<Leaderboard> leaderboards;
                using (var db = new LeaderboardsContext(leaderboardsConnectionString))
                {
                    leaderboards = await GetLeaderboardsAsync(db, cancellationToken).ConfigureAwait(false);
                }
                await UpdateLeaderboardsAsync(steamClient, leaderboards, cancellationToken).ConfigureAwait(false);

                using (var connection = new SqlConnection(leaderboardsConnectionString))
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                    var storeClient = new LeaderboardsStoreClient(connection);
                    await StoreLeaderboardsAsync(storeClient, leaderboards, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        internal async Task<IEnumerable<Leaderboard>> GetLeaderboardsAsync(
            ILeaderboardsContext db,
            CancellationToken cancellationToken)
        {
            return await db.Leaderboards.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        internal async Task UpdateLeaderboardsAsync(
            ISteamClientApiClient steamClient,
            IEnumerable<Leaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var download = new DownloadNotifier(Log, "leaderboards"))
            {
                steamClient.Progress = download;

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
                    Score = entry.Score,
                    Zone = entry.Details[0],
                    Level = entry.Details[1],
                    ReplayId = entry.UGCId.ToReplayId(),
                });
            }
        }

        internal async Task StoreLeaderboardsAsync(
            ILeaderboardsStoreClient storeClient,
            IEnumerable<Leaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var storeNotifier = new StoreNotifier(Log, "leaderboards"))
            {
                var rowsAffected = await storeClient.SaveChangesAsync(leaderboards, cancellationToken).ConfigureAwait(false);
                storeNotifier.Report(rowsAffected);
            }

            var entries = leaderboards.SelectMany(e => e.Entries).ToList();

            using (var storeNotifier = new StoreNotifier(Log, "players"))
            {
                var players = entries
                    .Select(e => e.SteamId)
                    .Distinct()
                    .Select(s => new Player { SteamId = s });
                var rowsAffected = await storeClient.SaveChangesAsync(players, false, cancellationToken).ConfigureAwait(false);
                storeNotifier.Report(rowsAffected);
            }

            using (var storeNotifier = new StoreNotifier(Log, "replays"))
            {
                var replays = entries
                    .Where(e => e.ReplayId != null)
                    .Select(e => e.ReplayId.Value)
                    .Distinct()
                    .Select(r => new Replay { ReplayId = r });
                var rowsAffected = await storeClient.SaveChangesAsync(replays, false, cancellationToken).ConfigureAwait(false);
                storeNotifier.Report(rowsAffected);
            }

            using (var storeNotifier = new StoreNotifier(Log, "entries"))
            {
                var rowsAffected = await storeClient.SaveChangesAsync(entries).ConfigureAwait(false);
                storeNotifier.Report(rowsAffected);
            }
        }
    }
}
