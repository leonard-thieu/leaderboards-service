﻿using System;
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
                    await UpdateLeaderboardsAsync(cancellationToken).ConfigureAwait(false);
                    await UpdateDailyLeaderboardsAsync(cancellationToken).ConfigureAwait(false);

                    operation.Telemetry.Success = true;
                }
                catch (Exception) when (Util.FailTelemetry(operation.Telemetry))
                {
                    // Unreachable
                    throw;
                }
            }
        }

        private async Task UpdateLeaderboardsAsync(CancellationToken cancellationToken)
        {
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
                catch (Exception) when (Util.FailTelemetry(op.Telemetry))
                {
                    // Unreachable
                    throw;
                }
                finally
                {
                    kernel.Release(leaderboardsWorker);
                }
            }
        }

        private async Task UpdateDailyLeaderboardsAsync(CancellationToken cancellationToken)
        {
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
                catch (Exception) when (Util.FailTelemetry(op.Telemetry))
                {
                    // Unreachable
                    throw;
                }
                finally
                {
                    kernel.Release(dailyLeaderboardsWorker);
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
