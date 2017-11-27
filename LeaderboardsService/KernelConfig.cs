using System;
using System.Data.Entity.Infrastructure;
using System.Net.Http;
using log4net;
using Microsoft.ApplicationInsights;
using Ninject;
using Ninject.Activation;
using Ninject.Extensions.NamedScope;
using Ninject.Syntax;
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
        public static IKernel CreateKernel(ILeaderboardsSettings settings, TelemetryClient telemetryClient)
        {
            var kernel = new StandardKernel();
            try
            {
                kernel.Bind<ILeaderboardsSettings>()
                      .ToConstant(settings);
                kernel.Bind<TelemetryClient>()
                      .ToConstant(telemetryClient);

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
            kernel.Bind<ILog>()
                  .ToConstant(Log);

            kernel.Bind<uint>()
                  .ToMethod(c => c.Kernel.Get<ILeaderboardsSettings>().AppId)
                  .WhenInjectedInto(typeof(LeaderboardsWorker), typeof(DailyLeaderboardsWorker));

            kernel.Bind<string>()
                  .ToMethod(c =>
                  {
                      var settings = c.Kernel.Get<ILeaderboardsSettings>();

                      if (settings.LeaderboardsConnectionString == null)
                      {
                          var connectionFactory = new LocalDbConnectionFactory("mssqllocaldb");
                          using (var connection = connectionFactory.CreateConnection("NecroDancer"))
                          {
                              settings.LeaderboardsConnectionString = new EncryptedSecret(connection.ConnectionString, settings.KeyDerivationIterations);
                              settings.Save();
                          }
                      }

                      return settings.LeaderboardsConnectionString.Decrypt();
                  })
                  .WhenInjectedInto(typeof(LeaderboardsContext), typeof(LeaderboardsStoreClient));

            kernel.Bind<ILeaderboardsContext>()
                  .To<LeaderboardsContext>()
                  .InParentScope();

            kernel.Bind<ILeaderboardsStoreClient>()
                  .To<LeaderboardsStoreClient>()
                  .WhenInjectedInto(typeof(LeaderboardsWorker))
                  .InParentScope();
            kernel.Bind<ILeaderboardsStoreClient>()
                  .To<LeaderboardsStoreClient>()
                  .WhenInjectedInto(typeof(DailyLeaderboardsWorker))
                  .AndWhen(SteamClientApiCredentialsAreSet)
                  .InParentScope();

            kernel.Bind<ILeaderboardsStoreClient>()
                  .To<FakeLeaderboardsStoreClient>()
                  .InParentScope();

            RegisterSteamCommunityDataClient(kernel);
            RegisterSteamClientApiClient(kernel);

            kernel.Bind<LeaderboardsWorker>()
                  .ToSelf()
                  .InScope(c => c);
            kernel.Bind<DailyLeaderboardsWorker>()
                  .ToSelf()
                  .InScope(c => c);
        }

        #region SteamCommunityDataClient

        private static void RegisterSteamCommunityDataClient(StandardKernel kernel)
        {
            kernel.Bind<HttpMessageHandler>()
                  .ToMethod(c =>
                  {
                      var telemetryClient = c.Kernel.Get<TelemetryClient>();
                      var log = c.Kernel.Get<ILog>();

                      return CreateSteamCommunityDataClientHandler(new WebRequestHandler(), log, telemetryClient);
                  })
                  .WhenInjectedInto(typeof(SteamCommunityDataClient))
                  .InParentScope();
            kernel.Bind<ISteamCommunityDataClient>()
                  .To<SteamCommunityDataClient>()
                  .InParentScope();
        }

        internal static HttpMessageHandler CreateSteamCommunityDataClientHandler(WebRequestHandler innerHandler, ILog log, TelemetryClient telemetryClient)
        {
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

            return HttpClientFactory.CreatePipeline(innerHandler, new DelegatingHandler[]
            {
                new LoggingHandler(),
                new GZipHandler(),
                new TransientFaultHandler(policy),
            });
        }

        #endregion

        #region SteamClientApiClient

        private static void RegisterSteamClientApiClient(StandardKernel kernel)
        {
            kernel.Bind<ISteamClientApiClient>()
                  .ToMethod(c =>
                  {
                      var settings = c.Kernel.Get<ILeaderboardsSettings>();
                      var telemetryClient = c.Kernel.Get<TelemetryClient>();
                      var log = c.Kernel.Get<ILog>();

                      var userName = settings.SteamUserName;
                      var password = settings.SteamPassword.Decrypt();
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
                  })
                  .When(SteamClientApiCredentialsAreSet)
                  .InParentScope();

            kernel.Bind<ISteamClientApiClient>()
                  .To<FakeSteamClientApiClient>()
                  .InParentScope();
        }

        #endregion

        private static bool SteamClientApiCredentialsAreSet(IRequest r)
        {
            var settings = r.ParentContext.Kernel.Get<ILeaderboardsSettings>();

            return SteamClientApiCredentialsAreSet(settings);
        }

        internal static bool SteamClientApiCredentialsAreSet(ILeaderboardsSettings settings)
        {
            return !string.IsNullOrEmpty(settings.SteamUserName) &&
                   settings.SteamPassword != null;
        }
    }

    internal static class IBindingInNamedWithOrOnSyntaxExtensions
    {
        public static IBindingInNamedWithOrOnSyntax<T> AndWhen<T>(
            this IBindingInNamedWithOrOnSyntax<T> binding,
            Func<IRequest, bool> condition)
        {
            var config = binding.BindingConfiguration;
            var originalCondition = config.Condition;
            config.Condition = r => originalCondition(r) && condition(r);

            return binding;
        }
    }
}
