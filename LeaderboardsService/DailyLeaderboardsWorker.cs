﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal sealed class DailyLeaderboardsWorker
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DailyLeaderboardsWorker));

        public DailyLeaderboardsWorker(uint appId, string connectionString, TelemetryClient telemetryClient)
        {
            this.appId = appId;
            this.connectionString = connectionString;
            this.telemetryClient = telemetryClient;
        }

        private readonly uint appId;
        private readonly string connectionString;
        private readonly TelemetryClient telemetryClient;

        public async Task UpdateAsync(ISteamClientApiClient steamClient, int limit, CancellationToken cancellationToken)
        {
            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Update daily leaderboards"))
            using (new UpdateActivity(Log, "daily leaderboards"))
            {
                try
                {
                    await steamClient.ConnectAndLogOnAsync().ConfigureAwait(false);

                    IEnumerable<DailyLeaderboard> leaderboards;
                    using (var db = new LeaderboardsContext(connectionString))
                    {
                        leaderboards = await GetDailyLeaderboardsAsync(db, steamClient, limit, cancellationToken).ConfigureAwait(false);
                    }
                    await UpdateDailyLeaderboardsAsync(steamClient, leaderboards, cancellationToken).ConfigureAwait(false);

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                        var storeClient = new LeaderboardsStoreClient(connection);
                        await StoreDailyLeaderboardsAsync(storeClient, leaderboards, cancellationToken).ConfigureAwait(false);
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

        #region Get daily leaderboards

        internal async Task<IEnumerable<DailyLeaderboard>> GetDailyLeaderboardsAsync(
            ILeaderboardsContext db,
            ISteamClientApiClient steamClient,
            int limit,
            CancellationToken cancellationToken)
        {
            var leaderboards = new List<DailyLeaderboard>();

            var today = DateTime.UtcNow.Date;
            var productsCount = await db.Products.CountAsync(cancellationToken).ConfigureAwait(false);
            var staleDailies = await GetStaleDailyLeaderboardsAsync(db, today, Math.Max(0, limit - productsCount), cancellationToken).ConfigureAwait(false);
            leaderboards.AddRange(staleDailies);

            // TODO: Should this do something if it doesn't return the expected number of leaderboards for today?
            var currentDailies = await GetCurrentDailyLeaderboardsAsync(db, steamClient, today, cancellationToken).ConfigureAwait(false);
            leaderboards.AddRange(currentDailies);

            return leaderboards;
        }

        internal async Task<IEnumerable<DailyLeaderboard>> GetStaleDailyLeaderboardsAsync(
            ILeaderboardsContext db,
            DateTime today,
            int limit,
            CancellationToken cancellationToken)
        {
            return await (from l in db.DailyLeaderboards
                          orderby l.LastUpdate
                          where l.Date != today
                          select l)
                          .Take(limit)
                          .ToListAsync(cancellationToken)
                          .ConfigureAwait(false);
        }

        internal async Task<IEnumerable<DailyLeaderboard>> GetCurrentDailyLeaderboardsAsync(
            ILeaderboardsContext db,
            ISteamClientApiClient steamClient,
            DateTime today,
            CancellationToken cancellationToken)
        {
            var dailyLeaderboards = new List<DailyLeaderboard>();

            var existingCurrentDailyLeaderboards = await GetExistingCurrentDailyLeaderboardsAsync(db, today, cancellationToken).ConfigureAwait(false);
            dailyLeaderboards.AddRange(existingCurrentDailyLeaderboards);

            var productsIds = existingCurrentDailyLeaderboards.Select(l => l.ProductId).ToList();
            var missingProducts = await (from p in db.Products
                                         where !productsIds.Contains(p.ProductId)
                                         select p)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

            var newDailyLeaderboards = await GetNewCurrentDailyLeaderboardsAsync(steamClient, today, missingProducts, cancellationToken);
            dailyLeaderboards.AddRange(newDailyLeaderboards);

            return dailyLeaderboards;
        }

        internal async Task<IEnumerable<DailyLeaderboard>> GetExistingCurrentDailyLeaderboardsAsync(
            ILeaderboardsContext db,
            DateTime today,
            CancellationToken cancellationToken)
        {
            return await (from l in db.DailyLeaderboards.Include(l => l.Product)
                          where l.Date == today
                          select l)
                          .ToListAsync(cancellationToken)
                          .ConfigureAwait(false);
        }

        internal async Task<IEnumerable<DailyLeaderboard>> GetNewCurrentDailyLeaderboardsAsync(
            ISteamClientApiClient steamClient,
            DateTime today,
            IEnumerable<Product> missingProducts,
            CancellationToken cancellationToken)
        {
            var newDailyLeaderboardTasks = new List<Task<DailyLeaderboard>>();

            foreach (var missingProduct in missingProducts)
            {
                var newDailyLeaderboardTask = GetNewCurrentDailyLeaderboardAsync(steamClient, today, missingProduct, cancellationToken);
                newDailyLeaderboardTasks.Add(newDailyLeaderboardTask);
            }

            return await Task.WhenAll(newDailyLeaderboardTasks).ConfigureAwait(false);
        }

        internal async Task<DailyLeaderboard> GetNewCurrentDailyLeaderboardAsync(
            ISteamClientApiClient steamClient,
            DateTime date,
            Product product,
            CancellationToken cancellationToken)
        {
            var name = GetDailyLeaderboardName(product.Name, date);
            var leaderboard = await steamClient.FindLeaderboardAsync(appId, name, cancellationToken).ConfigureAwait(false);
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

        internal async Task UpdateDailyLeaderboardsAsync(
            ISteamClientApiClient steamClient,
            IEnumerable<DailyLeaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var operation = telemetryClient.StartOperation<RequestTelemetry>("Download daily leaderboards"))
            using (var activity = new DownloadActivity(Log, "daily leaderboards"))
            {
                try
                {
                    steamClient.Progress = activity;

                    var leaderboardTasks = new List<Task>();

                    foreach (var leaderboard in leaderboards)
                    {
                        var leaderboardTask = UpdateDailyLeaderboardAsync(steamClient, leaderboard, cancellationToken);
                        leaderboardTasks.Add(leaderboardTask);
                    }

                    await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);

                    steamClient.Progress = null;

                    operation.Telemetry.Success = true;
                }
                catch (Exception)
                {
                    operation.Telemetry.Success = false;
                    throw;
                }
            }
        }

        internal async Task UpdateDailyLeaderboardAsync(
            ISteamClientApiClient steamClient,
            DailyLeaderboard leaderboard,
            CancellationToken cancellationToken)
        {
            var response = await steamClient
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

        internal async Task StoreDailyLeaderboardsAsync(
            ILeaderboardsStoreClient storeClient,
            IEnumerable<DailyLeaderboard> leaderboards,
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

                    using (var activity = new StoreActivity(Log, "daily entries"))
                    {
                        var rowsAffected = await storeClient.BulkUpsertAsync(entries, cancellationToken).ConfigureAwait(false);
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
