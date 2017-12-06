using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using toofz.Steam.ClientApi;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal sealed class DailyLeaderboardsWorker
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DailyLeaderboardsWorker));

        public DailyLeaderboardsWorker(
            uint appId,
            ILeaderboardsContext db,
            ISteamClientApiClient steamClientApiClient,
            ILeaderboardsStoreClient storeClient,
            TelemetryClient telemetryClient)
        {
            this.appId = appId;
            this.telemetryClient = telemetryClient;
            this.db = db;
            this.steamClientApiClient = steamClientApiClient;
            this.storeClient = storeClient;
        }

        private readonly uint appId;
        private readonly ILeaderboardsContext db;
        private readonly ISteamClientApiClient steamClientApiClient;
        private readonly ILeaderboardsStoreClient storeClient;
        private readonly TelemetryClient telemetryClient;

        #region Get daily leaderboards

        public async Task<IEnumerable<DailyLeaderboard>> GetDailyLeaderboardsAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            // TOOD: Workaround thread safety issue.
            await steamClientApiClient.ConnectAndLogOnAsync(cancellationToken).ConfigureAwait(false);

            var leaderboards = new List<DailyLeaderboard>();

            var today = DateTime.UtcNow.Date;
            var productsCount = await db.Products.CountAsync(cancellationToken).ConfigureAwait(false);
            var staleDailies = await GetStaleDailyLeaderboardsAsync(today, Math.Max(0, limit - productsCount), cancellationToken).ConfigureAwait(false);
            leaderboards.AddRange(staleDailies);

            var currentDailies = await GetCurrentDailyLeaderboardsAsync(today, cancellationToken).ConfigureAwait(false);
            leaderboards.AddRange(currentDailies);

            return leaderboards;
        }

        internal Task<List<DailyLeaderboard>> GetStaleDailyLeaderboardsAsync(
            DateTime today,
            int limit,
            CancellationToken cancellationToken)
        {
            return (from l in db.DailyLeaderboards
                    orderby l.LastUpdate
                    where l.Date != today
                    select l)
                    .Take(() => limit)
                    .ToListAsync(cancellationToken);
        }

        internal async Task<IEnumerable<DailyLeaderboard>> GetCurrentDailyLeaderboardsAsync(
            DateTime today,
            CancellationToken cancellationToken)
        {
            var dailyLeaderboards = new List<DailyLeaderboard>();

            var existingCurrentDailyLeaderboards = await GetExistingCurrentDailyLeaderboardsAsync(today, cancellationToken).ConfigureAwait(false);
            dailyLeaderboards.AddRange(existingCurrentDailyLeaderboards);

            var productsIds = existingCurrentDailyLeaderboards.Select(l => l.ProductId).ToList();
            var missingProducts = await (from p in db.Products
                                         where !productsIds.Contains(p.ProductId)
                                         select p)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);
            var newDailyLeaderboards = await GetNewCurrentDailyLeaderboardsAsync(today, missingProducts, cancellationToken);
            dailyLeaderboards.AddRange(newDailyLeaderboards);

            return dailyLeaderboards;
        }

        internal Task<List<DailyLeaderboard>> GetExistingCurrentDailyLeaderboardsAsync(
            DateTime today,
            CancellationToken cancellationToken)
        {
            return (from l in db.DailyLeaderboards.Include(l => l.Product)
                    where l.Date == today
                    select l)
                    .ToListAsync(cancellationToken);
        }

        internal async Task<IEnumerable<DailyLeaderboard>> GetNewCurrentDailyLeaderboardsAsync(
            DateTime today,
            IEnumerable<Product> missingProducts,
            CancellationToken cancellationToken)
        {
            var newDailyLeaderboardTasks = new List<Task<DailyLeaderboard>>();

            foreach (var missingProduct in missingProducts)
            {
                var newDailyLeaderboardTask = GetNewCurrentDailyLeaderboardAsync(today, missingProduct, cancellationToken);
                newDailyLeaderboardTasks.Add(newDailyLeaderboardTask);
            }

            var newDailyLeaderboards = new List<DailyLeaderboard>();

            while (newDailyLeaderboardTasks.Any())
            {
                var newDailyLeaderboardTask = await Task.WhenAny(newDailyLeaderboardTasks).ConfigureAwait(false);
                newDailyLeaderboardTasks.Remove(newDailyLeaderboardTask);

                try
                {
                    var newDailyLeaderboard = await newDailyLeaderboardTask.ConfigureAwait(false);
                    newDailyLeaderboards.Add(newDailyLeaderboard);
                }
                // This handles the case where the leaderboard does not exist (e.g. leaderboard not created yet or leaderboard has been deleted).
                // Note: FakeSteamClientApiClient will always throw when FindLeaderboardAsync is called.
                catch (SteamClientApiException ex)
                {
                    if (Log.IsWarnEnabled &&
                        !(steamClientApiClient is FakeSteamClientApiClient))
                    {
                        Log.Warn("Could not find leaderboard.", ex);
                    }
                }
            }

            return newDailyLeaderboards;
        }

        internal async Task<DailyLeaderboard> GetNewCurrentDailyLeaderboardAsync(
            DateTime date,
            Product product,
            CancellationToken cancellationToken)
        {
            var name = GetDailyLeaderboardName(product.Name, date);
            var leaderboard = await steamClientApiClient.FindLeaderboardAsync(appId, name, cancellationToken).ConfigureAwait(false);
            var displayName = $"Daily ({date.ToString("yyyy-MM-dd")})";
            if (product.Name != "classic") { displayName += $" ({product.DisplayName})"; }

            return new DailyLeaderboard
            {
                LeaderboardId = leaderboard.ID,
                Name = name,
                DisplayName = displayName,
                IsProduction = true,
                ProductId = product.ProductId,
                Date = date,
            };
        }

        internal static string GetDailyLeaderboardName(string product, DateTime date)
        {
            var tokens = new List<string>();

            switch (product)
            {
                case "amplified": tokens.Add("DLC"); break;
                case "classic": break;
                default:
                    throw new ArgumentException($"'{product}' is not a valid product.");
            }

            tokens.Add(date.ToString("d/M/yyyy"));

            var name = string.Join(" ", tokens);
            name += "_PROD";

            return name;
        }

        #endregion

        #region Update daily leaderboards

        public async Task UpdateDailyLeaderboardsAsync(
            IEnumerable<DailyLeaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Download daily leaderboards"))
            using (var activity = new DownloadActivity(Log, "daily leaderboards"))
            {
                try
                {
                    await steamClientApiClient.ConnectAndLogOnAsync(cancellationToken).ConfigureAwait(false);

                    steamClientApiClient.Progress = activity;

                    var leaderboardTasks = new List<Task>();

                    foreach (var leaderboard in leaderboards)
                    {
                        var leaderboardTask = UpdateDailyLeaderboardAsync(leaderboard, cancellationToken);
                        leaderboardTasks.Add(leaderboardTask);
                    }

                    await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);

                    steamClientApiClient.Progress = null;

                    operation.Telemetry.Success = true;
                }
                catch (Exception) when (operation.Telemetry.MarkAsUnsuccessful()) { }
            }
        }

        internal async Task UpdateDailyLeaderboardAsync(
            DailyLeaderboard leaderboard,
            CancellationToken cancellationToken)
        {
            var response = await steamClientApiClient
                .GetLeaderboardEntriesAsync(appId, leaderboard.LeaderboardId, cancellationToken)
                .ConfigureAwait(false);

            leaderboard.LastUpdate = DateTime.UtcNow;

            foreach (var entry in response.Entries)
            {
                leaderboard.Entries.Add(new DailyEntry
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

        #region Store daily leaderboards

        public async Task StoreDailyLeaderboardsAsync(
            IEnumerable<DailyLeaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Store daily leaderboards"))
            {
                try
                {
                    using (var activity = new StoreActivity(Log, "daily leaderboards"))
                    {
                        var rowsAffected = await storeClient.BulkUpsertAsync(leaderboards, cancellationToken).ConfigureAwait(false);
                        activity.Report(rowsAffected);
                    }

                    using (var activity = new StoreActivity(Log, "players"))
                    {
                        var players = leaderboards
                            .SelectMany(l => l.Entries)
                            .Select(e => e.SteamId)
                            .Distinct()
                            .Select(s => new Player { SteamId = s });

                        var options = new BulkUpsertOptions { UpdateWhenMatched = false };
                        var rowsAffected = await storeClient.BulkUpsertAsync(players, options, cancellationToken).ConfigureAwait(false);
                        activity.Report(rowsAffected);
                    }

                    using (var activity = new StoreActivity(Log, "replays"))
                    {
                        var replays = leaderboards
                            .SelectMany(l => l.Entries)
                            .Where(e => e.ReplayId != null)
                            .Select(e => e.ReplayId.Value)
                            .Distinct()
                            .Select(r => new Replay { ReplayId = r });

                        var options = new BulkUpsertOptions { UpdateWhenMatched = false };
                        var rowsAffected = await storeClient.BulkUpsertAsync(replays, options, cancellationToken).ConfigureAwait(false);
                        activity.Report(rowsAffected);
                    }

                    using (var activity = new StoreActivity(Log, "daily entries"))
                    {
                        var entries = leaderboards.SelectMany(e => e.Entries);

                        var rowsAffected = await storeClient.BulkUpsertAsync(entries, cancellationToken).ConfigureAwait(false);
                        activity.Report(rowsAffected);
                    }

                    operation.Telemetry.Success = true;
                }
                catch (Exception) when (operation.Telemetry.MarkAsUnsuccessful()) { }
            }
        }

        #endregion
    }
}
