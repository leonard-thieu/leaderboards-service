using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal abstract class LeaderboardsWorkerBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LeaderboardsWorkerBase));

        internal async Task<IEnumerable<Leaderboard>> GetLeaderboardsAsync(
            ILeaderboardsContext db,
            CancellationToken cancellationToken)
        {
            return await db.Leaderboards.ToListAsync(cancellationToken).ConfigureAwait(false);
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
