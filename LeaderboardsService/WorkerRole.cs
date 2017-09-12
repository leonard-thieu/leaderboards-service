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
    sealed class WorkerRole : WorkerRoleBase<ILeaderboardsSettings>
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

            var userName = Settings.SteamUserName;
            var password = Settings.SteamPassword.Decrypt();
            var leaderboardsConnectionString = Settings.LeaderboardsConnectionString.Decrypt();

            using (var steamClient = new SteamClientApiClient(userName, password))
            {
                using (new UpdateNotifier(Log, "leaderboards"))
                {
                    var headers = GetStaleLeaderboards();
                    var leaderboards = await DownloadLeaderboardsAsync(headers, steamClient, cancellationToken).ConfigureAwait(false);

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
                        headers = await GetStaleDailyLeaderboardsAsync(db, steamClient, cancellationToken).ConfigureAwait(false);
                    }

                    var leaderboards = await DownloadDailyLeaderboardsAsync(headers, steamClient, cancellationToken).ConfigureAwait(false);

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

        internal IEnumerable<LeaderboardHeader> GetStaleLeaderboards()
        {
            return LeaderboardsResources.ReadLeaderboardHeaders();
        }

        internal async Task<IEnumerable<Leaderboard>> DownloadLeaderboardsAsync(
            IEnumerable<LeaderboardHeader> headers,
            ISteamClientApiClient steamClient,
            CancellationToken cancellationToken)
        {
            using (var download = new DownloadNotifier(Log, "leaderboards"))
            {
                steamClient.Progress = download.Progress;

                var leaderboardCategories = LeaderboardsResources.ReadLeaderboardCategories();
                var characters = leaderboardCategories["characters"];
                var runs = leaderboardCategories["runs"];

                var leaderboardTasks = new List<Task<Leaderboard>>();
                foreach (var header in headers)
                {
                    leaderboardTasks.Add(MapLeaderboardEntries());

                    // TODO: Consider making this non-local.
                    async Task<Leaderboard> MapLeaderboardEntries()
                    {
                        var leaderboard = new Leaderboard
                        {
                            LeaderboardId = header.Id,
                            CharacterId = characters[header.Character].Id,
                            RunId = runs[header.Run].Id,
                            LastUpdate = DateTime.UtcNow,
                        };

                        var response =
                            await steamClient.GetLeaderboardEntriesAsync(Settings.AppId, header.Id, cancellationToken).ConfigureAwait(false);

                        leaderboard.EntriesCount = response.EntryCount;
                        var leaderboardEntries = response.Entries.Select(e =>
                        {
                            var entry = new Entry
                            {
                                LeaderboardId = header.Id,
                                Rank = e.GlobalRank,
                                SteamId = (long)(ulong)e.SteamID,
                                Score = e.Score,
                                Zone = e.Details[0],
                                Level = e.Details[1],
                            };
                            var ugcId = (long)(ulong)e.UGCId;
                            switch (ugcId)
                            {
                                case -1:
                                case 0: entry.ReplayId = null; break;
                                default: entry.ReplayId = ugcId; break;
                            }

                            return entry;
                        });
                        leaderboard.Entries.AddRange(leaderboardEntries);

                        return leaderboard;
                    }
                }

                return await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);
            }
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

        internal async Task<IEnumerable<DailyLeaderboardHeader>> GetStaleDailyLeaderboardsAsync(
            LeaderboardsContext db,
            ISteamClientApiClient steamClient,
            CancellationToken cancellationToken)
        {
            var headers = new List<DailyLeaderboardHeader>();

            var today = DateTime.UtcNow.Date;
            var limit = 100;
            var leaderboardCategories = LeaderboardsResources.ReadLeaderboardCategories();
            var products = leaderboardCategories["products"];

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
                                      .Take(limit - products.Count)
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

            var existingCurrentDailies =
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
            IEnumerable<DailyLeaderboardHeader> currentDailies =
                (from l in existingCurrentDailies
                 select new DailyLeaderboardHeader
                 {
                     Id = l.LeaderboardId,
                     Date = l.Date,
                     Product = products.GetName(l.ProductId),
                     IsProduction = l.IsProduction,
                 })
                 .ToList();

            // TODO: Should check which products are missing instead of assuming all are missing
            if (currentDailies.Count() != products.Count)
            {
                var headerTasks = new List<Task<DailyLeaderboardHeader>>();
                foreach (var p in products)
                {
                    headerTasks.Add(GetDailyLeaderboardHeaderAsync());

                    // TODO: Consider making this non-local.
                    async Task<DailyLeaderboardHeader> GetDailyLeaderboardHeaderAsync()
                    {
                        var isProduction = true;
                        var name = GetDailyLeaderboardName(p.Key, today, isProduction);
                        var leaderboard = await steamClient.FindLeaderboardAsync(Settings.AppId, name, cancellationToken).ConfigureAwait(false);

                        return new DailyLeaderboardHeader
                        {
                            Id = leaderboard.ID,
                            Date = today,
                            Product = p.Key,
                            IsProduction = isProduction,
                        };
                    }
                }
                currentDailies = await Task.WhenAll(headerTasks).ConfigureAwait(false);

                // TODO: Consider making this non-local.
                string GetDailyLeaderboardName(string product, DateTime date, bool isProduction)
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
            }
            headers.AddRange(currentDailies);

            return headers;
        }

        internal async Task<IEnumerable<DailyLeaderboard>> DownloadDailyLeaderboardsAsync(
            IEnumerable<DailyLeaderboardHeader> headers,
            ISteamClientApiClient steamClient,
            CancellationToken cancellationToken)
        {
            var leaderboardTasks = new List<Task<DailyLeaderboard>>();
            using (var download = new DownloadNotifier(Log, "daily leaderboards"))
            {
                steamClient.Progress = download.Progress;

                var leaderboardCategories = LeaderboardsResources.ReadLeaderboardCategories();
                var products = leaderboardCategories["products"];

                foreach (var header in headers)
                {
                    leaderboardTasks.Add(MapLeaderboardEntries());

                    // TODO: Consider making this non-local.
                    async Task<DailyLeaderboard> MapLeaderboardEntries()
                    {
                        var leaderboard = new DailyLeaderboard
                        {
                            LeaderboardId = header.Id,
                            Date = header.Date,
                            ProductId = products[header.Product].Id,
                            IsProduction = header.IsProduction,
                            LastUpdate = DateTime.UtcNow,
                        };

                        var response =
                            await steamClient.GetLeaderboardEntriesAsync(Settings.AppId, header.Id, cancellationToken).ConfigureAwait(false);

                        var leaderboardEntries = response.Entries.Select(e =>
                        {
                            var entry = new DailyEntry
                            {
                                LeaderboardId = header.Id,
                                Rank = e.GlobalRank,
                                SteamId = (long)(ulong)e.SteamID,
                                Score = e.Score,
                                Zone = e.Details[0],
                                Level = e.Details[1],
                            };
                            var ugcId = (long)(ulong)e.UGCId;
                            switch (ugcId)
                            {
                                case -1:
                                case 0: entry.ReplayId = null; break;
                                default: entry.ReplayId = ugcId; break;
                            }

                            return entry;
                        });
                        leaderboard.Entries.AddRange(leaderboardEntries);

                        return leaderboard;
                    }
                }

                return await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);
            }
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
