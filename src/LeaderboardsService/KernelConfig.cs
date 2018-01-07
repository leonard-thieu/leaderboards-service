using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using log4net;
using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;
using Ninject;
using Ninject.Activation;
using Ninject.Extensions.NamedScope;
using Ninject.Syntax;
using Polly;
using toofz.Data;
using toofz.Services.LeaderboardsService.Properties;
using toofz.Steam;
using toofz.Steam.ClientApi;
using toofz.Steam.CommunityData;

namespace toofz.Services.LeaderboardsService
{
    [ExcludeFromCodeCoverage]
    internal static class KernelConfig
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        public static IKernel CreateKernel()
        {
            var kernel = new StandardKernel();
            try
            {
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
                  .ToMethod(GetAppId)
                  .WhenInjectedInto(typeof(LeaderboardsWorker), typeof(DailyLeaderboardsWorker));

            kernel.Bind<DbContextOptions<NecroDancerContext>>()
                  .ToMethod(GetNecroDancerContextOptions)
                  .WhenInjectedInto<NecroDancerContext>();
            kernel.Bind<ILeaderboardsContext>()
                  .To<NecroDancerContext>()
                  .InParentScope();

            kernel.Bind<string>()
                  .ToMethod(GetLeaderboardsConnectionString)
                  .WhenInjectedInto<LeaderboardsStoreClient>();
            kernel.Bind<ILeaderboardsStoreClient>()
                  .To<LeaderboardsStoreClient>()
                  .WhenInjectedInto<LeaderboardsWorker>()
                  .InParentScope();
            kernel.Bind<ILeaderboardsStoreClient>()
                  .To<LeaderboardsStoreClient>()
                  .WhenInjectedInto<DailyLeaderboardsWorker>()
                  .AndWhen(SteamClientApiCredentialsAreSet)
                  .InParentScope();
            kernel.Bind<ILeaderboardsStoreClient>()
                  .To<FakeLeaderboardsStoreClient>()
                  .InParentScope();

            kernel.Bind<HttpMessageHandler>()
                  .ToMethod(GetSteamCommunityDataClientHandler)
                  .WhenInjectedInto<SteamCommunityDataClient>()
                  .InParentScope();
            kernel.Bind<ISteamCommunityDataClient>()
                  .To<SteamCommunityDataClient>()
                  .InParentScope();

            kernel.Bind<ISteamClientApiClient>()
                  .ToMethod(GetSteamClientApiClient)
                  .When(SteamClientApiCredentialsAreSet)
                  .InParentScope();
            kernel.Bind<ISteamClientApiClient>()
                  .To<FakeSteamClientApiClient>()
                  .InParentScope();

            kernel.Bind<LeaderboardsWorker>()
                  .ToSelf()
                  .InScope(c => c);
            kernel.Bind<DailyLeaderboardsWorker>()
                  .ToSelf()
                  .InScope(c => c);
        }

        private static uint GetAppId(IContext c)
        {
            return c.Kernel.Get<ILeaderboardsSettings>().AppId;
        }

        private static string GetLeaderboardsConnectionString(IContext c)
        {
            var settings = c.Kernel.Get<ILeaderboardsSettings>();

            if (settings.LeaderboardsConnectionString == null)
            {
                var connectionString = StorageHelper.GetLocalDbConnectionString("NecroDancer");
                settings.LeaderboardsConnectionString = new EncryptedSecret(connectionString, settings.KeyDerivationIterations);
                settings.Save();
            }

            return settings.LeaderboardsConnectionString.Decrypt();
        }

        private static DbContextOptions<NecroDancerContext> GetNecroDancerContextOptions(IContext c)
        {
            var connectionString = GetLeaderboardsConnectionString(c);

            return new DbContextOptionsBuilder<NecroDancerContext>()
                .UseSqlServer(connectionString)
                .Options;
        }

        private static HttpMessageHandler GetSteamCommunityDataClientHandler(IContext c)
        {
            var telemetryClient = c.Kernel.Get<TelemetryClient>();
            var log = c.Kernel.Get<ILog>();

            return CreateSteamCommunityDataClientHandler(new WebRequestHandler(), log, telemetryClient);
        }

        internal static HttpMessageHandler CreateSteamCommunityDataClientHandler(WebRequestHandler innerHandler, ILog log, TelemetryClient telemetryClient)
        {
            var policy = Policy
                .Handle<Exception>(SteamCommunityDataClient.IsTransient)
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

        private static SteamClientApiClient GetSteamClientApiClient(IContext c)
        {
            var settings = c.Kernel.Get<ILeaderboardsSettings>();
            var telemetryClient = c.Kernel.Get<TelemetryClient>();
            var log = c.Kernel.Get<ILog>();

            var userName = settings.SteamUserName;
            var password = settings.SteamPassword.Decrypt();
            var timeout = settings.SteamClientTimeout;

            var policy = Policy
                .Handle<Exception>(SteamClientApiClient.IsTransient)
                .WaitAndRetryAsync(
                    3,
                    ExponentialBackoff.GetSleepDurationProvider(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(2)),
                    (ex, duration) =>
                    {
                        telemetryClient.TrackException(ex);
                        if (log.IsDebugEnabled) { log.Debug($"Retrying in {duration}...", ex); }
                    });

            return new SteamClientApiClient(userName, password, policy, telemetryClient) { Timeout = timeout };
        }

        private static bool SteamClientApiCredentialsAreSet(IRequest r)
        {
            return r.ParentContext.Kernel.Get<ILeaderboardsSettings>().AreSteamClientCredentialsSet();
        }
    }

    [ExcludeFromCodeCoverage]
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
