﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using toofz.NecroDancer.Leaderboards.EntityFramework;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    sealed class WorkerRole : WorkerRoleBase<ILeaderboardsSettings>
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(WorkerRole));

        public WorkerRole() : base("toofz Leaderboards Service") { }

        public override ILeaderboardsSettings Settings => Properties.Settings.Default;

        protected override async Task RunAsyncOverride(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(Settings.SteamUserName))
            {
                throw new InvalidOperationException($"{nameof(Settings.SteamUserName)} is not set.");
            }
            var userName = Settings.SteamUserName;
            if (Settings.SteamPassword == null)
            {
                throw new InvalidOperationException($"{nameof(Settings.SteamPassword)} is not set.");
            }
            var password = Settings.SteamPassword.Decrypt();

            if (Settings.LeaderboardsConnectionString == null)
            {
                throw new InvalidOperationException($"{nameof(Settings.LeaderboardsConnectionString)} is not set.");
            }
            var leaderboardsConnectionString = Settings.LeaderboardsConnectionString.Decrypt();

            using (var steamClient = new SteamClientApiClient(userName, password))
            {
                var storeClient = new LeaderboardsStoreClient(leaderboardsConnectionString);
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
                throw new ArgumentNullException(nameof(steamClient));
            if (storeClient == null)
                throw new ArgumentNullException(nameof(storeClient));

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

                    var characters = leaderboardCategories["characters"];
                    var runs = leaderboardCategories["runs"];

                    var leaderboardTasks = new List<Task<Leaderboard>>();
                    foreach (var header in headers)
                    {
                        leaderboardTasks.Add(MapLeaderboardEntries());

                        async Task<Leaderboard> MapLeaderboardEntries()
                        {
                            var leaderboard = new Leaderboard
                            {
                                LeaderboardId = header.id,
                                CharacterId = characters[header.character].Id,
                                RunId = runs[header.run].Id,
                                LastUpdate = DateTime.UtcNow,
                            };

                            var response =
                                await steamClient.GetLeaderboardEntriesAsync(Settings.AppId, header.id, cancellationToken).ConfigureAwait(false);

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
                throw new ArgumentNullException(nameof(steamClient));
            if (storeClient == null)
                throw new ArgumentNullException(nameof(storeClient));
            if (db == null)
                throw new ArgumentNullException(nameof(db));

            using (new UpdateNotifier(Log, "daily leaderboards"))
            {
                var headers = new List<DailyLeaderboardHeader>();

                IEnumerable<DailyLeaderboardHeader> todaysDailies;
                var leaderboardCategories = LeaderboardsResources.ReadLeaderboardCategories();
                var today = DateTime.Today;
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
                                          .Take(98)
                                          .ToListAsync(cancellationToken)
                                          .ConfigureAwait(false);
                foreach (var staleDaily in staleDailies)
                {
                    var header = new DailyLeaderboardHeader
                    {
                        id = staleDaily.LeaderboardId,
                        date = staleDaily.Date,
                        product = products.GetName(staleDaily.ProductId),
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
                                     product = products.GetName(l.ProductId),
                                     production = l.IsProduction,
                                 })
                                 .ToList();

                if (todaysDailies.Count() != products.Count)
                {
                    var headerTasks = new List<Task<DailyLeaderboardHeader>>();
                    foreach (var p in products)
                    {
                        headerTasks.Add(GetDailyLeaderboardHeaderAsync());

                        async Task<DailyLeaderboardHeader> GetDailyLeaderboardHeaderAsync()
                        {
                            var isProduction = true;
                            var name = GetDailyLeaderboardName(p.Key, today, isProduction);
                            var leaderboard = await steamClient.FindLeaderboardAsync(Settings.AppId, name, cancellationToken).ConfigureAwait(false);

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
                                ProductId = products[header.product].Id,
                                IsProduction = header.production,
                                LastUpdate = DateTime.UtcNow,
                            };

                            var response =
                                await steamClient.GetLeaderboardEntriesAsync(Settings.AppId, header.id, cancellationToken).ConfigureAwait(false);

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
