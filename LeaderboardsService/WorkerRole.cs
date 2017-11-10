using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal class WorkerRole : WorkerRoleBase<ILeaderboardsSettings>
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WorkerRole));

        public WorkerRole(ILeaderboardsSettings settings) : base("leaderboards", settings) { }

        private readonly TelemetryClient telemetryClient = new TelemetryClient();

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
            var appId = Settings.AppId;
            var leaderboardsConnectionString = Settings.LeaderboardsConnectionString.Decrypt();
            var dailyLeaderboardsPerUpdate = Settings.DailyLeaderboardsPerUpdate;
            var steamClientTimeout = Settings.SteamClientTimeout;

            using (var steamClient = new SteamClientApiClient(userName, password, telemetryClient))
            {
                steamClient.Timeout = steamClientTimeout;

                var leaderboardsWorker = new LeaderboardsWorker(appId, leaderboardsConnectionString, telemetryClient);
#if FEATURE_LEADERBOARDS_VIA_STEAMCLIENT
                await leaderboardsWorker.UpdateAsync(steamClient, cancellationToken).ConfigureAwait(false); 
#else
                await leaderboardsWorker.UpdateAsync(null, cancellationToken).ConfigureAwait(false);
#endif

                var dailyLeaderboardsWorker = new DailyLeaderboardsWorker(appId, leaderboardsConnectionString, telemetryClient);
                await dailyLeaderboardsWorker.UpdateAsync(steamClient, dailyLeaderboardsPerUpdate, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
