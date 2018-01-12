using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using log4net;
using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;
using Ninject;
using Ninject.Activation;
using Ninject.Extensions.NamedScope;
using Polly;
using toofz.Data;
using toofz.Services.LeaderboardsService.Properties;
using toofz.Steam;
using toofz.Steam.CommunityData;

namespace toofz.Services.LeaderboardsService
{
    [ExcludeFromCodeCoverage]
    internal static class KernelConfig
    {
        // The dev database is intended for development and demonstration scenarios.
        private const string DevDatabaseName = "DevNecroDancer";

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
                  .WhenInjectedInto<LeaderboardsWorker>();

            kernel.Bind<string>()
                  .ToMethod(GetLeaderboardsConnectionString)
                  .WhenInjectedInto(typeof(NecroDancerContextOptionsBuilder), typeof(LeaderboardsStoreClient))
                  .InScope(c => UpdateCycleScope.Instance);
            kernel.Bind<DbContextOptionsBuilder<NecroDancerContext>>()
                  .To<NecroDancerContextOptionsBuilder>();
            kernel.Bind<DbContextOptions<NecroDancerContext>>()
                  .ToMethod(GetNecroDancerContextOptions);
            kernel.Bind<ILeaderboardsContext>()
                  .To<NecroDancerContext>()
                  .InParentScope()
                  .OnActivation(InitializeNecroDancerContext);

            kernel.Bind<HttpMessageHandler>()
                  .ToMethod(GetSteamCommunityDataClientHandler)
                  .WhenInjectedInto<SteamCommunityDataClient>()
                  .InParentScope();
            kernel.Bind<ISteamCommunityDataClient>()
                  .To<SteamCommunityDataClient>()
                  .InParentScope();

            kernel.Bind<ILeaderboardsStoreClient>()
                  .To<LeaderboardsStoreClient>()
                  .InParentScope();

            kernel.Bind<LeaderboardsWorker>()
                  .ToSelf()
                  .InScope(c => UpdateCycleScope.Instance)
                  .OnDeactivation(_ => UpdateCycleScope.Instance = new object());
        }

        private static uint GetAppId(IContext c)
        {
            return c.Kernel.Get<ILeaderboardsSettings>().AppId;
        }

        #region Database

        private static string GetLeaderboardsConnectionString(IContext c)
        {
            var settings = c.Kernel.Get<ILeaderboardsSettings>();

            // Get the connection string from settings if it's available; otherwise, use the default.
            var connectionString = settings.LeaderboardsConnectionString?.Decrypt() ??
                                   StorageHelper.GetLocalDbConnectionString("NecroDancer");

            // Check if any players are in the database. If there are none (i.e. toofz Leaderboards Service hasn't been run),
            // use the dev database instead as it will be seeded with test data.
            var options = new DbContextOptionsBuilder<NecroDancerContext>()
                .UseSqlServer(connectionString)
                .Options;

            using (var context = new NecroDancerContext(options))
            {
                InitializeNecroDancerContext(context);
            }

            return connectionString;
        }

        private static DbContextOptions<NecroDancerContext> GetNecroDancerContextOptions(IContext c)
        {
            return c.Kernel.Get<NecroDancerContextOptionsBuilder>().Options;
        }

        private static void InitializeNecroDancerContext(NecroDancerContext context)
        {
            context.Database.Migrate();
            context.EnsureSeedData();
        }

        #endregion

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

        private sealed class UpdateCycleScope
        {
            public static object Instance { get; set; } = new object();
        }
    }

    internal sealed class NecroDancerContextOptionsBuilder : DbContextOptionsBuilder<NecroDancerContext>
    {
        public NecroDancerContextOptionsBuilder(string connectionString)
        {
            this.UseSqlServer(connectionString);
        }
    }
}
