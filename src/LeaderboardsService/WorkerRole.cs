using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Ninject;
using toofz.Data;
using toofz.Services.LeaderboardsService.Properties;
using toofz.Steam.CommunityData;

namespace toofz.Services.LeaderboardsService
{
    internal sealed class WorkerRole : WorkerRoleBase<ILeaderboardsSettings>
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WorkerRole));

        public WorkerRole(ILeaderboardsSettings settings, TelemetryClient telemetryClient)
            : this(settings, telemetryClient, runOnce: false, kernel: null, log: null) { }

        internal WorkerRole(ILeaderboardsSettings settings, TelemetryClient telemetryClient, bool runOnce, IKernel kernel, ILog log) :
            base("leaderboards", settings, telemetryClient, runOnce)
        {
            kernel = kernel ?? KernelConfig.CreateKernel();
            kernel.Bind<ILeaderboardsSettings>()
                  .ToConstant(settings);
            kernel.Bind<TelemetryClient>()
                  .ToConstant(telemetryClient);
            this.kernel = kernel;

            this.log = log ?? Log;
        }

        private readonly IKernel kernel;
        private readonly ILog log;

        protected override async Task RunAsyncOverride(CancellationToken cancellationToken)
        {
            using (var operation = TelemetryClient.StartOperation<RequestTelemetry>("Update leaderboards cycle"))
            using (new UpdateActivity(log, "leaderboards cycle"))
            {
                try
                {
                    await UpdateLeaderboardsAsync(cancellationToken).ConfigureAwait(false);

                    operation.Telemetry.Success = true;
                }
                catch (Exception) when (operation.Telemetry.MarkAsUnsuccessful()) { }
            }
        }

        private async Task UpdateLeaderboardsAsync(CancellationToken cancellationToken)
        {
            var worker = kernel.Get<LeaderboardsWorker>();
            using (var operation = TelemetryClient.StartOperation<RequestTelemetry>("Update leaderboards"))
            using (new UpdateActivity(log, "leaderboards"))
            {
                try
                {
                    var leaderboards = await worker.GetLeaderboardsAsync(cancellationToken).ConfigureAwait(false);
                    await worker.UpdateLeaderboardsAsync(leaderboards, cancellationToken).ConfigureAwait(false);
                    await worker.StoreLeaderboardsAsync(leaderboards, cancellationToken).ConfigureAwait(false);

                    operation.Telemetry.Success = true;
                }
                catch (Exception ex)
                    when (SteamCommunityDataClient.IsTransient(ex) ||
                          LeaderboardsStoreClient.IsTransient(ex))
                {
                    TelemetryClient.TrackException(ex);
                    log.Error("Failed to complete run due to an error.", ex);
                    operation.Telemetry.Success = false;
                }
                catch (Exception) when (operation.Telemetry.MarkAsUnsuccessful()) { }
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
