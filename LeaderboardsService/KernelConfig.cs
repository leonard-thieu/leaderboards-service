using System;
using System.Net.Http;
using log4net;
using Microsoft.ApplicationInsights;
using Ninject;
using Ninject.Extensions.NamedScope;
using Polly;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal static class KernelConfig
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        public static IKernel CreateKernel(TelemetryClient telemetryClient)
        {
            var kernel = new StandardKernel();
            try
            {
                kernel.Bind<TelemetryClient>().ToConstant(telemetryClient);
                RegisterServices(kernel);
                return kernel;
            }
            catch
            {
                kernel.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Load your modules or register your services here!
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        private static void RegisterServices(StandardKernel kernel)
        {
            kernel.Bind<ILog>().ToConstant(Log);
            kernel.Bind<ILeaderboardsSettings>().ToConstant(Settings.Default);

            kernel.Bind<uint>().ToMethod(c =>
            {
                var settings = c.Kernel.Get<ILeaderboardsSettings>();

                return settings.AppId;
            }).WhenInjectedInto(typeof(LeaderboardsWorker), typeof(DailyLeaderboardsWorker));

            kernel.Bind<string>().ToMethod(c =>
            {
                var settings = c.Kernel.Get<ILeaderboardsSettings>();
                var leaderboardsConnectionString = settings.LeaderboardsConnectionString;

                if (leaderboardsConnectionString == null)
                    throw new InvalidOperationException($"{nameof(Settings.LeaderboardsConnectionString)} is not set.");

                return leaderboardsConnectionString.Decrypt();
            }).WhenInjectedInto(typeof(LeaderboardsContext), typeof(LeaderboardsStoreClient));

            kernel.Bind<ILeaderboardsContext>().To<LeaderboardsContext>().InParentScope();
            kernel.Bind<ILeaderboardsStoreClient>().To<LeaderboardsStoreClient>().InParentScope();

            RegisterSteamCommunityDataClient(kernel);
            RegisterSteamClientApiClient(kernel);

            kernel.Bind<LeaderboardsWorker>().ToSelf().InScope(c => c);
            kernel.Bind<DailyLeaderboardsWorker>().ToSelf().InScope(c => c);
        }

        private static void RegisterSteamCommunityDataClient(StandardKernel kernel)
        {
            kernel.Bind<HttpMessageHandler>().ToMethod(c =>
            {
                var telemetryClient = c.Kernel.Get<TelemetryClient>();
                var log = c.Kernel.Get<ILog>();

                var policy = SteamCommunityDataClient
                    .GetRetryStrategy()
                    .WaitAndRetryAsync(
                        3,
                        ExponentialBackoff.GetSleepDurationProvider(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(2)),
                        (ex, duration) =>
                        {
                            telemetryClient.TrackException(ex);
                            if (log.IsDebugEnabled) { log.Debug($"Retrying in {duration}...", ex); }
                        });

                return HttpClientFactory.CreatePipeline(new WebRequestHandler(), new DelegatingHandler[]
                {
                    new LoggingHandler(),
                    new GZipHandler(),
                    new TransientFaultHandler(policy),
                });
            }).WhenInjectedInto(typeof(SteamCommunityDataClient)).InParentScope();
            kernel.Bind<ISteamCommunityDataClient>().To<SteamCommunityDataClient>().InParentScope();
        }

        private static void RegisterSteamClientApiClient(StandardKernel kernel)
        {
            kernel.Bind<ISteamClientApiClient>().ToMethod(c =>
            {
                var settings = c.Kernel.Get<ILeaderboardsSettings>();
                var telemetryClient = c.Kernel.Get<TelemetryClient>();
                var log = c.Kernel.Get<ILog>();

                var userName = settings.SteamUserName;
                if (string.IsNullOrEmpty(userName))
                    throw new InvalidOperationException($"{nameof(Settings.SteamUserName)} is not set.");

                var password = settings.SteamPassword.Decrypt();
                if (password == null)
                    throw new InvalidOperationException($"{nameof(Settings.SteamPassword)} is not set.");

                var timeout = settings.SteamClientTimeout;

                var policy = SteamClientApiClient
                    .GetRetryStrategy()
                    .WaitAndRetryAsync(
                        3,
                        ExponentialBackoff.GetSleepDurationProvider(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(2)),
                        (ex, duration) =>
                        {
                            telemetryClient.TrackException(ex);
                            if (log.IsDebugEnabled) { log.Debug($"Retrying in {duration}...", ex); }
                        });

                return new SteamClientApiClient(userName, password, policy, telemetryClient) { Timeout = timeout };
            }).InParentScope();
        }
    }
}
