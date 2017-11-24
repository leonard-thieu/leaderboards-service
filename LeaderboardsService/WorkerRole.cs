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
    internal class WorkerRole : WorkerRoleBase<ILeaderboardsSettings>
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WorkerRole));

        public WorkerRole(ILeaderboardsSettings settings, TelemetryClient telemetryClient) : base("leaderboards", settings, telemetryClient)
        {
            kernel = KernelConfig.CreateKernel(telemetryClient);
        }

        private readonly IKernel kernel;

        protected override async Task RunAsyncOverride(CancellationToken cancellationToken)
        {
            using (var operation = TelemetryClient.StartOperation<RequestTelemetry>("Update leaderboards cycle"))
            using (new UpdateActivity(Log, "leaderboards cycle"))
            {
                try
                {
                    UpdateScope.Current = new object();
                    var leaderboardsWorker = kernel.Get<LeaderboardsWorker>();
                    using (var op = TelemetryClient.StartOperation<RequestTelemetry>("Update leaderboards"))
                    using (new UpdateActivity(Log, "leaderboards"))
                    {
                        try
                        {
                            var leaderboards = await leaderboardsWorker.GetLeaderboardsAsync(cancellationToken).ConfigureAwait(false);
                            await leaderboardsWorker.UpdateLeaderboardsAsync(leaderboards, cancellationToken).ConfigureAwait(false);
                            await leaderboardsWorker.StoreLeaderboardsAsync(leaderboards, cancellationToken).ConfigureAwait(false);

                            op.Telemetry.Success = true;
                        }
                        catch (HttpRequestStatusException ex)
                        {
                            TelemetryClient.TrackException(ex);
                            Log.Error("Failed to complete run due to an error.", ex);
                            op.Telemetry.Success = false;
                        }
                        catch (Exception) when (Util.FailTelemetry(operation.Telemetry))
                        {
                            // Unreachable
                            throw;
                        }
                    }

                    UpdateScope.Current = new object();
                    var dailyLeaderboardsWorker = kernel.Get<DailyLeaderboardsWorker>();
                    using (var op = TelemetryClient.StartOperation<RequestTelemetry>("Update daily leaderboards"))
                    using (new UpdateActivity(Log, "daily leaderboards"))
                    {
                        try
                        {
                            var leaderboards = await dailyLeaderboardsWorker.GetDailyLeaderboardsAsync(Settings.DailyLeaderboardsPerUpdate, cancellationToken).ConfigureAwait(false);
                            await dailyLeaderboardsWorker.UpdateDailyLeaderboardsAsync(leaderboards, cancellationToken).ConfigureAwait(false);
                            await dailyLeaderboardsWorker.StoreDailyLeaderboardsAsync(leaderboards, cancellationToken).ConfigureAwait(false);

                            op.Telemetry.Success = true;
                        }
                        catch (SteamClientApiException ex)
                        {
                            TelemetryClient.TrackException(ex);
                            Log.Error("Failed to complete run due to an error.", ex);
                            op.Telemetry.Success = false;
                        }
                        catch (Exception) when (Util.FailTelemetry(operation.Telemetry))
                        {
                            // Unreachable
                            throw;
                        }
                    }

                    operation.Telemetry.Success = true;
                }
                catch (Exception) when (Util.FailTelemetry(operation.Telemetry))
                {
                    // Unreachable
                    throw;
                }
                finally
                {
                    UpdateScope.Current = null;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                kernel.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
