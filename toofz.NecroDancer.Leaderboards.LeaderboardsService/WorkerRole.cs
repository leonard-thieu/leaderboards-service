﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CredentialManagement;
using log4net;
using toofz.NecroDancer.Leaderboards.EntityFramework;
using toofz.NecroDancer.Leaderboards.Services.Common;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    sealed class WorkerRole : WorkerRoleBase<Settings>
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(WorkerRole));

        const uint AppId = 247080;

        public WorkerRole() : base("toofz Leaderboards Service") { }

        protected override void OnStartOverride() { }

        protected override async Task RunAsyncOverride(CancellationToken cancellationToken)
        {
            string userName;
            string password;
            using (var cred = new Credential { Target = "toofz/Steam", PersistanceType = PersistanceType.LocalComputer })
            {
                if (!cred.Load())
                {
                    throw new InvalidOperationException("Could not load credentials for 'toofz/Steam'.");
                }

                userName = cred.Username;
                password = cred.Password;
            }

            string leaderboardsConnectionString;
            using (var cred = new Credential { Target = "toofz/LeaderboardsConnectionString", PersistanceType = PersistanceType.LocalComputer })
            {
                if (!cred.Load())
                {
                    throw new InvalidOperationException("Could not load credentials for 'toofz/LeaderboardsConnectionString'.");
                }

                leaderboardsConnectionString = cred.Password;
            }

            var storeClient = new LeaderboardsStoreClient(leaderboardsConnectionString);

            using (var steamClient = new SteamClientApiClient(userName, password))
            {
                await UpdateLeaderboardsAsync(steamClient, storeClient, cancellationToken).ConfigureAwait(false);

                using (var db = new LeaderboardsContext(leaderboardsConnectionString))
                {
                    await UpdateDailyLeaderboardsAsync(steamClient, storeClient, db, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        internal async Task UpdateLeaderboardsAsync(
            ISteamClientApiClient steamClient,
            ILeaderboardsStoreClient storeClient,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (steamClient == null)
                throw new ArgumentNullException(nameof(steamClient), $"{nameof(steamClient)} is null.");
            if (storeClient == null)
                throw new ArgumentNullException(nameof(storeClient), $"{nameof(storeClient)} is null.");

            using (new UpdateNotifier(Log, "leaderboards"))
            {
                var headers = new List<LeaderboardHeader>();

                var leaderboardHeaders = LeaderboardsResources.ReadLeaderboardHeaders();
                headers.AddRange(leaderboardHeaders.Where(h => h.id > 0));

                var leaderboardCategories = LeaderboardsResources.ReadLeaderboardCategories();

                Leaderboard[] leaderboards;
                using (var download = new DownloadNotifier(Log, "leaderboards"))
                {
                    steamClient.Progress = download.Progress;

                    var leaderboardTasks = new List<Task<Leaderboard>>();
                    foreach (var header in headers)
                    {
                        leaderboardTasks.Add(MapLeaderboardEntries());

                        async Task<Leaderboard> MapLeaderboardEntries()
                        {
                            var leaderboard = new Leaderboard
                            {
                                LeaderboardId = header.id,
                                CharacterId = leaderboardCategories.GetItemId("characters", header.character),
                                RunId = leaderboardCategories.GetItemId("runs", header.run),
                                LastUpdate = DateTime.UtcNow,
                            };

                            var response =
                                await steamClient.GetLeaderboardEntriesAsync(AppId, header.id, cancellationToken).ConfigureAwait(false);

                            leaderboard.EntriesCount = response.EntryCount;
                            var leaderboardEntries = response.Entries.Select(e =>
                            {
                                var entry = new Entry
                                {
                                    LeaderboardId = header.id,
                                    Rank = e.GlobalRank,
                                    SteamId = (long)(ulong)e.SteamID,
                                    Score = e.Score,
                                    Zone = e.Details[0],
                                    Level = e.Details[1],
                                };
                                var ugcId = (long)(ulong)e.UGCId;
                                switch (ugcId)
                                {
                                    case -1: entry.ReplayId = null; break;
                                    default: entry.ReplayId = ugcId; break;
                                }

                                return entry;
                            });
                            leaderboard.Entries.AddRange(leaderboardEntries);

                            return leaderboard;
                        }
                    }

                    leaderboards = await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested) { return; }

                await storeClient.SaveChangesAsync(leaderboards, cancellationToken).ConfigureAwait(false);

                var entries = leaderboards.SelectMany(e => e.Entries).ToList();

                var players = entries.Select(e => e.SteamId)
                    .Distinct()
                    .Select(s => new Player { SteamId = s });
                await storeClient.SaveChangesAsync(players, false, cancellationToken).ConfigureAwait(false);

                var replayIds = new HashSet<long>(from e in entries
                                                  where e.ReplayId != null
                                                  select e.ReplayId.Value);
                var replays = from e in replayIds
                              select new Replay { ReplayId = e };
                await storeClient.SaveChangesAsync(replays, false, cancellationToken).ConfigureAwait(false);

                await storeClient.SaveChangesAsync(entries).ConfigureAwait(false);
            }
        }

        internal async Task UpdateDailyLeaderboardsAsync(
            ISteamClientApiClient steamClient,
            ILeaderboardsStoreClient storeClient,
            LeaderboardsContext db,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (steamClient == null)
                throw new ArgumentNullException(nameof(steamClient), $"{nameof(steamClient)} is null.");
            if (storeClient == null)
                throw new ArgumentNullException(nameof(storeClient), $"{nameof(storeClient)} is null.");
            if (db == null)
                throw new ArgumentNullException(nameof(db), $"{nameof(db)} is null.");

            using (new UpdateNotifier(Log, "daily leaderboards"))
            {
                var headers = new List<DailyLeaderboardHeader>();

                IEnumerable<DailyLeaderboardHeader> todaysDailies;
                var leaderboardCategories = LeaderboardsResources.ReadLeaderboardCategories();
                var today = DateTime.Today;

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
                                          .Take(98)
                                          .ToListAsync(cancellationToken)
                                          .ConfigureAwait(false);
                foreach (var staleDaily in staleDailies)
                {
                    var header = new DailyLeaderboardHeader
                    {
                        id = staleDaily.LeaderboardId,
                        date = staleDaily.Date,
                        product = leaderboardCategories.GetItemName("products", staleDaily.ProductId),
                        production = staleDaily.IsProduction,
                    };
                    headers.Add(header);
                }

                var _todaysDailies =
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
                todaysDailies = (from l in _todaysDailies
                                 select new DailyLeaderboardHeader
                                 {
                                     id = l.LeaderboardId,
                                     date = l.Date,
                                     product = leaderboardCategories.GetItemName("products", l.ProductId),
                                     production = l.IsProduction,
                                 })
                                 .ToList();

                if (todaysDailies.Count() != leaderboardCategories.GetCategory("products").Count)
                {
                    var headerTasks = new List<Task<DailyLeaderboardHeader>>();
                    foreach (var p in leaderboardCategories["products"])
                    {
                        headerTasks.Add(GetDailyLeaderboardHeaderAsync());

                        async Task<DailyLeaderboardHeader> GetDailyLeaderboardHeaderAsync()
                        {
                            var isProduction = true;
                            var name = GetDailyLeaderboardName(p.Key, today, isProduction);
                            var leaderboard = await steamClient.FindLeaderboardAsync(AppId, name, cancellationToken).ConfigureAwait(false);

                            return new DailyLeaderboardHeader
                            {
                                id = leaderboard.ID,
                                date = today,
                                product = p.Key,
                                production = isProduction,
                            };
                        }
                    }
                    todaysDailies = await Task.WhenAll(headerTasks).ConfigureAwait(false);

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
                headers.AddRange(todaysDailies);

                var leaderboardTasks = new List<Task<DailyLeaderboard>>();
                DailyLeaderboard[] leaderboards;
                using (var download = new DownloadNotifier(Log, "daily leaderboards"))
                {
                    steamClient.Progress = download.Progress;

                    foreach (var header in headers)
                    {
                        leaderboardTasks.Add(MapLeaderboardEntries());

                        async Task<DailyLeaderboard> MapLeaderboardEntries()
                        {
                            var leaderboard = new DailyLeaderboard
                            {
                                LeaderboardId = header.id,
                                Date = header.date,
                                ProductId = leaderboardCategories.GetItemId("products", header.product),
                                IsProduction = header.production,
                                LastUpdate = DateTime.UtcNow,
                            };

                            var response =
                                await steamClient.GetLeaderboardEntriesAsync(AppId, header.id, cancellationToken).ConfigureAwait(false);

                            var leaderboardEntries = response.Entries.Select(e =>
                            {
                                var entry = new DailyEntry
                                {
                                    LeaderboardId = header.id,
                                    Rank = e.GlobalRank,
                                    SteamId = (long)(ulong)e.SteamID,
                                    Score = e.Score,
                                    Zone = e.Details[0],
                                    Level = e.Details[1],
                                };
                                var ugcId = (long)(ulong)e.UGCId;
                                switch (ugcId)
                                {
                                    case -1: entry.ReplayId = null; break;
                                    default: entry.ReplayId = ugcId; break;
                                }

                                return entry;
                            });
                            leaderboard.Entries.AddRange(leaderboardEntries);

                            return leaderboard;
                        }
                    }

                    leaderboards = await Task.WhenAll(leaderboardTasks).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested) { return; }

                await storeClient.SaveChangesAsync(leaderboards, cancellationToken).ConfigureAwait(false);

                var entries = leaderboards.SelectMany(e => e.Entries).ToList();

                var players = entries.Select(e => e.SteamId)
                    .Distinct()
                    .Select(s => new Player { SteamId = s });
                await storeClient.SaveChangesAsync(players, false, cancellationToken).ConfigureAwait(false);

                var replayIds = new HashSet<long>(from e in entries
                                                  where e.ReplayId != null
                                                  select e.ReplayId.Value);
                var replays = from e in replayIds
                              select new Replay { ReplayId = e };
                await storeClient.SaveChangesAsync(replays, false, cancellationToken).ConfigureAwait(false);

                await storeClient.SaveChangesAsync(entries, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
