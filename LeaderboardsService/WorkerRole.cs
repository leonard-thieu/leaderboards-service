using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
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

            var userName = Settings.SteamUserName;
            var password = Settings.SteamPassword.Decrypt();
            var leaderboardsConnectionString = Settings.LeaderboardsConnectionString.Decrypt();
            var dailyLeaderboardsPerUpdate = Settings.DailyLeaderboardsPerUpdate;

            using (var steamClient = new SteamClientApiClient(userName, password))
            {
                await steamClient.ConnectAndLogOnAsync().ConfigureAwait(false);
                steamClient.Timeout = TimeSpan.FromSeconds(30);

                var leaderboardsWorker = new LeaderboardsWorker(Settings.AppId, leaderboardsConnectionString);
                await leaderboardsWorker.UpdateAsync(steamClient, cancellationToken).ConfigureAwait(false);

                var dailyLeaderboardsWorker = new DailyLeaderboardsWorker(Settings.AppId, leaderboardsConnectionString);
                await dailyLeaderboardsWorker.UpdateAsync(steamClient, dailyLeaderboardsPerUpdate, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
