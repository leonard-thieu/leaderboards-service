using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Ninject;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    using static Util;

    internal class WorkerRole : WorkerRoleBase<ILeaderboardsSettings>
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WorkerRole));

        public WorkerRole(ILeaderboardsSettings settings, TelemetryClient telemetryClient) : base("leaderboards", settings, telemetryClient)
        {
            kernel = KernelConfig.CreateKernel(settings, telemetryClient);
        }

        private readonly IKernel kernel;

        protected override async Task RunAsyncOverride(CancellationToken cancellationToken)
        {
            using (var operation = TelemetryClient.StartOperation<RequestTelemetry>("Update leaderboards cycle"))
            using (new UpdateActivity(Log, "leaderboards cycle"))
            {
                try
                {
                    await UpdateLeaderboardsAsync(cancellationToken).ConfigureAwait(false);
                    await UpdateDailyLeaderboardsAsync(cancellationToken).ConfigureAwait(false);

                    operation.Telemetry.Success = true;
                }
                catch (Exception) when (FailTelemetry(operation.Telemetry))
                {
                    // Unreachable
                    throw;
                }
            }
        }

        private async Task UpdateLeaderboardsAsync(CancellationToken cancellationToken)
        {
            var worker = kernel.Get<LeaderboardsWorker>();
            using (var operation = TelemetryClient.StartOperation<RequestTelemetry>("Update leaderboards"))
            using (new UpdateActivity(Log, "leaderboards"))
            {
                try
                {
                    var leaderboards = await worker.GetLeaderboardsAsync(cancellationToken).ConfigureAwait(false);
                    await worker.UpdateLeaderboardsAsync(leaderboards, cancellationToken).ConfigureAwait(false);
                    await worker.StoreLeaderboardsAsync(leaderboards, cancellationToken).ConfigureAwait(false);

                    operation.Telemetry.Success = true;
                }
                catch (HttpRequestStatusException ex)
                {
                    TelemetryClient.TrackException(ex);
                    Log.Error("Failed to complete run due to an error.", ex);
                    operation.Telemetry.Success = false;
                }
                catch (Exception) when (FailTelemetry(operation.Telemetry))
                {
                    // Unreachable
                    throw;
                }
                finally
                {
                    kernel.Release(worker);
                }
            }
        }

        private async Task UpdateDailyLeaderboardsAsync(CancellationToken cancellationToken)
        {
            var worker = kernel.Get<DailyLeaderboardsWorker>();
            using (var operation = TelemetryClient.StartOperation<RequestTelemetry>("Update daily leaderboards"))
            using (new UpdateActivity(Log, "daily leaderboards"))
            {
                try
                {
                    if (!Settings.AreSteamClientCredentialsSet())
                    {
                        Log.Warn("Using test data for calls to Steam Client API. Set your Steam user name and password to use the actual Steam Client API.");
                        Log.Warn("Run this application with --help to find out how to set your Steam user name and password.");
                    }

                    var leaderboards = await worker.GetDailyLeaderboardsAsync(Settings.DailyLeaderboardsPerUpdate, cancellationToken).ConfigureAwait(false);
                    await worker.UpdateDailyLeaderboardsAsync(leaderboards, cancellationToken).ConfigureAwait(false);
                    await worker.StoreDailyLeaderboardsAsync(leaderboards, cancellationToken).ConfigureAwait(false);

                    operation.Telemetry.Success = true;
                }
                catch (SteamClientApiException ex)
                {
                    TelemetryClient.TrackException(ex);
                    Log.Error("Failed to complete run due to an error.", ex);
                    operation.Telemetry.Success = false;
                }
                catch (Exception) when (FailTelemetry(operation.Telemetry))
                {
                    // Unreachable
                    throw;
                }
                finally
                {
                    kernel.Release(worker);
                }
            }
        }

        #region IDisposable Implementation

        private bool disposed;

        protected override void Dispose(bool disposing)
        {
            if (disposed) { return; }

            if (disposing)
            {
                try
                {
                    // This can throw
                    kernel.Dispose();
                }
                catch (Exception) { }
            }
            disposed = true;

            base.Dispose(disposing);
        }

        #endregion
    }
}
