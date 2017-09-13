using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using toofz.NecroDancer.Leaderboards.EntityFramework;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    class WorkerRole : WorkerRoleBase<ILeaderboardsSettings>
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(WorkerRole));

        public WorkerRole(ILeaderboardsSettings settings) : base("leaderboards", settings) { }

        protected override async Task RunAsyncOverride(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(Settings.SteamUserName))
                throw new InvalidOperationException($"{nameof(Settings.SteamUserName)} is not set.");
            if (Settings.SteamPassword == null)
                throw new InvalidOperationException($"{nameof(Settings.SteamPassword)} is not set.");
            if (Settings.LeaderboardsConnectionString == null)
                throw new InvalidOperationException($"{nameof(Settings.LeaderboardsConnectionString)} is not set.");

            var leaderboardCategories = LeaderboardsResources.ReadLeaderboardCategories();
            var products = leaderboardCategories["products"];
            var runs = leaderboardCategories["runs"];
            var characters = leaderboardCategories["characters"];

            if (Settings.DailyLeaderboardsPerUpdate < products.Count)
                throw new InvalidOperationException($"{nameof(Settings.DailyLeaderboardsPerUpdate)} is set to less than the number of products ({products.Count}).");
            var dailyLeaderboardsPerUpdate = Settings.DailyLeaderboardsPerUpdate;

            var userName = Settings.SteamUserName;
            var password = Settings.SteamPassword.Decrypt();
            var leaderboardsConnectionString = Settings.LeaderboardsConnectionString.Decrypt();

            using (var steamClient = new SteamClientApiClient(userName, password))
            {
                using (new UpdateNotifier(Log, "leaderboards"))
                {
                    var headers = GetLeaderboardHeaders();
                    var leaderboards = await DownloadLeaderboardsAsync(runs, characters, headers, steamClient, cancellationToken).ConfigureAwait(false);

                    using (var connection = new SqlConnection(leaderboardsConnectionString))
                    {
                        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                        var storeClient = new LeaderboardsStoreClient(connection);
                        await StoreLeaderboardsAsync(storeClient, leaderboards, cancellationToken).ConfigureAwait(false);
                    }
                }

                using (new UpdateNotifier(Log, "daily leaderboards"))
                {
                    IEnumerable<DailyLeaderboardHeader> headers;
                    using (var db = new LeaderboardsContext(leaderboardsConnectionString))
                    {
                        headers = await GetDailyLeaderboardHeadersAsync(products, dailyLeaderboardsPerUpdate, db, steamClient, cancellationToken).ConfigureAwait(false);
                    }
                    var leaderboards = await GetDailyLeaderboardsAsync(products, headers, steamClient, cancellationToken).ConfigureAwait(false);
                    using (var connection = new SqlConnection(leaderboardsConnectionString))
                    {
                        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                        var storeClient = new LeaderboardsStoreClient(connection);
                        await StoreDailyLeaderboardsAsync(storeClient, leaderboards, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        #region Leaderboards

        internal IEnumerable<LeaderboardHeader> GetLeaderboardHeaders()
        {
            return LeaderboardsResources.ReadLeaderboardHeaders();
        }

        internal async Task<IEnumerable<Leaderboard>> DownloadLeaderboardsAsync(
            Category runs,
            Category characters,
            IEnumerable<LeaderboardHeader> headers,
            ISteamClientApiClient steamClient,
            CancellationToken cancellationToken)
        {
            using (var download = new DownloadNotifier(Log, "leaderboards"))
            {
                steamClient.Progress = download.Progress;

                var leaderboardTasks = new List<Task<Leaderboard>>();
                foreach (var header in headers)
                {
                    var leaderboardTask = DownloadLeaderboardAsync(
                        steamClient,
                        header.Id,
                        runs[header.Run].Id,
                        characters[header.Character].Id,
                        cancellationToken);
                    leaderboardTasks.Add(leaderboardTask);
                }

                return await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);
            }
        }

        internal async Task<Leaderboard> DownloadLeaderboardAsync(
            ISteamClientApiClient steamClient,
            int leaderboardId,
            int runId,
            int characterId,
            CancellationToken cancellationToken)
        {
            var response = await steamClient
                .GetLeaderboardEntriesAsync(Settings.AppId, leaderboardId, cancellationToken)
                .ConfigureAwait(false);

            var leaderboard = new Leaderboard
            {
                LeaderboardId = leaderboardId,
                RunId = runId,
                CharacterId = characterId,
                LastUpdate = DateTime.UtcNow,
            };

            leaderboard.EntriesCount = response.EntryCount;
            foreach (var entry in response.Entries)
            {
                leaderboard.Entries.Add(new Entry
                {
                    LeaderboardId = leaderboardId,
                    Rank = entry.GlobalRank,
                    SteamId = entry.SteamID.ToInt64(),
                    Score = entry.Score,
                    Zone = entry.Details[0],
                    Level = entry.Details[1],
                    ReplayId = entry.UGCId.ToReplayId(),
                });
            }

            return leaderboard;
        }

        internal async Task StoreLeaderboardsAsync(
            ILeaderboardsStoreClient storeClient,
            IEnumerable<Leaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var notifier = new StoreNotifier(Log, "leaderboards"))
            {
                var rowsAffected = await storeClient.SaveChangesAsync(leaderboards, cancellationToken).ConfigureAwait(false);
                notifier.Progress.Report(rowsAffected);
            }

            var entries = leaderboards.SelectMany(e => e.Entries).ToList();

            using (var notifier = new StoreNotifier(Log, "players"))
            {
                var players = entries.Select(e => e.SteamId)
                                .Distinct()
                                .Select(s => new Player { SteamId = s });
                var rowsAffected = await storeClient.SaveChangesAsync(players, false, cancellationToken).ConfigureAwait(false);
                notifier.Progress.Report(rowsAffected);
            }

            using (var notifier = new StoreNotifier(Log, "replays"))
            {
                var replayIds = new HashSet<long>(from e in entries
                                                  where e.ReplayId != null
                                                  select e.ReplayId.Value);
                var replays = from e in replayIds
                              select new Replay { ReplayId = e };
                var rowsAffected = await storeClient.SaveChangesAsync(replays, false, cancellationToken).ConfigureAwait(false);
                notifier.Progress.Report(rowsAffected);
            }

            using (var notifier = new StoreNotifier(Log, "entries"))
            {
                var rowsAffected = await storeClient.SaveChangesAsync(entries).ConfigureAwait(false);
                notifier.Progress.Report(rowsAffected);
            }
        }

        #endregion

        #region Daily Leaderboards

        internal async Task<List<DailyLeaderboardHeader>> GetDailyLeaderboardHeadersAsync(
            Category products,
            int dailyLeaderboardsPerUpdate,
            LeaderboardsContext db,
            SteamClientApiClient steamClient,
            CancellationToken cancellationToken)
        {
            var headers = new List<DailyLeaderboardHeader>();

            var today = DateTime.UtcNow.Date;

            var limit = dailyLeaderboardsPerUpdate - products.Count;
            var staleDailies = await GetStaleDailyLeaderboardHeadersAsync(products, db, today, limit, cancellationToken).ConfigureAwait(false);
            headers.AddRange(staleDailies);

            var currentDailies = await GetCurrentDailyLeaderboardHeadersAsync(products, db, steamClient, today, cancellationToken).ConfigureAwait(false);
            headers.AddRange(currentDailies);

            return headers;
        }

        internal async Task<IEnumerable<DailyLeaderboardHeader>> GetStaleDailyLeaderboardHeadersAsync(
            Category products,
            LeaderboardsContext db,
            DateTime today,
            int limit,
            CancellationToken cancellationToken)
        {
            var headers = new List<DailyLeaderboardHeader>();

            var staleDailies = await (from l in db.DailyLeaderboards
                                      orderby l.LastUpdate
                                      where l.Date != today
                                      select new
                                      {
                                          l.LeaderboardId,
                                          l.Date,
                                          l.ProductId,
                                          l.IsProduction,
                                      })
                                      .Take(limit)
                                      .ToListAsync(cancellationToken)
                                      .ConfigureAwait(false);

            foreach (var staleDaily in staleDailies)
            {
                var header = new DailyLeaderboardHeader
                {
                    Id = staleDaily.LeaderboardId,
                    Date = staleDaily.Date,
                    Product = products.GetName(staleDaily.ProductId),
                    IsProduction = staleDaily.IsProduction,
                };
                headers.Add(header);
            }

            return headers;
        }

        internal async Task<IEnumerable<DailyLeaderboardHeader>> GetCurrentDailyLeaderboardHeadersAsync(
            Category products,
            LeaderboardsContext db,
            ISteamClientApiClient steamClient,
            DateTime today,
            CancellationToken cancellationToken)
        {
            var dailyLeaderboardHeaders = new List<DailyLeaderboardHeader>();

            var existingCurrentDailyLeaderboards =
                await (from l in db.DailyLeaderboards
                       where l.Date == today
                       select new
                       {
                           l.LeaderboardId,
                           l.Date,
                           l.ProductId,
                           l.IsProduction,
                       })
                       .ToListAsync(cancellationToken)
                       .ConfigureAwait(false);
            var existingCurrentDailyLeaderboardHeaders = from l in existingCurrentDailyLeaderboards
                                                         select new DailyLeaderboardHeader
                                                         {
                                                             Id = l.LeaderboardId,
                                                             Date = l.Date,
                                                             Product = products.GetName(l.ProductId),
                                                             IsProduction = l.IsProduction,
                                                         };
            dailyLeaderboardHeaders.AddRange(existingCurrentDailyLeaderboardHeaders);

            var missingProducts = products.Keys.Except(existingCurrentDailyLeaderboardHeaders.Select(e => e.Product));
            var headerTasks = new List<Task<DailyLeaderboardHeader>>();
            foreach (var missingProduct in missingProducts)
            {
                var headerTask = GetDailyLeaderboardHeaderAsync(steamClient, missingProduct, today, true, cancellationToken);
                headerTasks.Add(headerTask);
            }
            var missingHeaders = await Task.WhenAll(headerTasks).ConfigureAwait(false);
            dailyLeaderboardHeaders.AddRange(missingHeaders);

            return dailyLeaderboardHeaders;
        }

        internal async Task<DailyLeaderboardHeader> GetDailyLeaderboardHeaderAsync(
            ISteamClientApiClient steamClient,
            string product,
            DateTime date,
            bool isProduction,
            CancellationToken cancellationToken)
        {
            var name = GetDailyLeaderboardName(product, date, isProduction);
            var leaderboard = await steamClient.FindLeaderboardAsync(Settings.AppId, name, cancellationToken).ConfigureAwait(false);

            return new DailyLeaderboardHeader
            {
                Id = leaderboard.ID,
                Product = product,
                Date = date,
                IsProduction = isProduction,
            };
        }

        internal static string GetDailyLeaderboardName(string product, DateTime date, bool isProduction)
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
            if (isProduction) { name += "_PROD"; }

            return name;
        }

        internal async Task<IEnumerable<DailyLeaderboard>> GetDailyLeaderboardsAsync(
            Category products,
            IEnumerable<DailyLeaderboardHeader> headers,
            ISteamClientApiClient steamClient,
            CancellationToken cancellationToken)
        {
            var leaderboardTasks = new List<Task<DailyLeaderboard>>();
            using (var download = new DownloadNotifier(Log, "daily leaderboards"))
            {
                steamClient.Progress = download.Progress;

                foreach (var header in headers)
                {
                    var leaderboardTask = GetDailyLeaderboardEntriesAsync(
                        steamClient,
                        header.Id,
                        products[header.Product].Id,
                        header.Date,
                        header.IsProduction,
                        cancellationToken);
                    leaderboardTasks.Add(leaderboardTask);
                }

                return await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);
            }
        }

        internal async Task<DailyLeaderboard> GetDailyLeaderboardEntriesAsync(
            ISteamClientApiClient steamClient,
            int leaderboardId,
            int productId,
            DateTime date,
            bool isProduction,
            CancellationToken cancellationToken)
        {
            var leaderboard = new DailyLeaderboard
            {
                LeaderboardId = leaderboardId,
                Date = date,
                ProductId = productId,
                IsProduction = isProduction,
                LastUpdate = DateTime.UtcNow,
            };

            var response = await steamClient
                .GetLeaderboardEntriesAsync(Settings.AppId, leaderboardId, cancellationToken)
                .ConfigureAwait(false);

            foreach (var entry in response.Entries)
            {
                leaderboard.Entries.Add(new DailyEntry
                {
                    LeaderboardId = leaderboardId,
                    Rank = entry.GlobalRank,
                    SteamId = entry.SteamID.ToInt64(),
                    Score = entry.Score,
                    Zone = entry.Details[0],
                    Level = entry.Details[1],
                    ReplayId = entry.UGCId.ToReplayId(),
                });
            }

            return leaderboard;
        }

        internal async Task StoreDailyLeaderboardsAsync(
            ILeaderboardsStoreClient storeClient,
            IEnumerable<DailyLeaderboard> leaderboards,
            CancellationToken cancellationToken)
        {
            using (var notifier = new StoreNotifier(Log, "daily leaderboards"))
            {
                var rowsAffected = await storeClient.SaveChangesAsync(leaderboards, cancellationToken).ConfigureAwait(false);
                notifier.Progress.Report(rowsAffected);
            }

            var entries = leaderboards.SelectMany(e => e.Entries).ToList();

            using (var notifier = new StoreNotifier(Log, "players"))
            {
                var players = entries.Select(e => e.SteamId)
                                .Distinct()
                                .Select(s => new Player { SteamId = s });
                var rowsAffected = await storeClient.SaveChangesAsync(players, false, cancellationToken).ConfigureAwait(false);
                notifier.Progress.Report(rowsAffected);
            }

            using (var notifier = new StoreNotifier(Log, "replays"))
            {
                var replayIds = new HashSet<long>(from e in entries
                                                  where e.ReplayId != null
                                                  select e.ReplayId.Value);
                var replays = from e in replayIds
                              select new Replay { ReplayId = e };
                var rowsAffected = await storeClient.SaveChangesAsync(replays, false, cancellationToken).ConfigureAwait(false);
                notifier.Progress.Report(rowsAffected);
            }

            using (var notifier = new StoreNotifier(Log, "daily entries"))
            {
                var rowsAffected = await storeClient.SaveChangesAsync(entries, cancellationToken).ConfigureAwait(false);
                notifier.Progress.Report(rowsAffected);
            }
        }

        #endregion
    }
}
