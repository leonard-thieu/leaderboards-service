using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal class WorkerRole : WorkerRoleBase<ILeaderboardsSettings>
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WorkerRole));

        public WorkerRole(ILeaderboardsSettings settings, TelemetryClient telemetryClient) : base("leaderboards", settings, telemetryClient) { }

        protected override async Task RunAsyncOverride(CancellationToken cancellationToken)
        {
            using (var operation = TelemetryClient.StartOperation<RequestTelemetry>("Update leaderboards cycle"))
            {
                try
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

                    using (var steamClient = new SteamClientApiClient(userName, password, TelemetryClient))
                    {
                        steamClient.Timeout = steamClientTimeout;

                        var leaderboardsWorker = new LeaderboardsWorker(appId, leaderboardsConnectionString, TelemetryClient);
                        await leaderboardsWorker.UpdateAsync(cancellationToken).ConfigureAwait(false);

                        var dailyLeaderboardsWorker = new DailyLeaderboardsWorker(appId, leaderboardsConnectionString, TelemetryClient);
                        await dailyLeaderboardsWorker.UpdateAsync(steamClient, dailyLeaderboardsPerUpdate, cancellationToken).ConfigureAwait(false);
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
    }
}
