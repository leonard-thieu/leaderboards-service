using System;
using System.Diagnostics.CodeAnalysis;
using log4net;
using Microsoft.ApplicationInsights;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    [ExcludeFromCodeCoverage]
    internal static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));
        private static readonly TelemetryClient TelemetryClient = new TelemetryClient();

        /// <summary>
        /// The main entry point of the application.
        /// </summary>
        /// <param name="args">Arguments passed in.</param>
        /// <returns>
        /// 0 - The application ran successfully.
        /// 1 - There was an error parsing <paramref name="args"/>.
        /// </returns>
        private static int Main(string[] args)
        {
            var settings = Settings.Default;

            using (var worker = new WorkerRole(settings, TelemetryClient))
            {
                return Application<ILeaderboardsSettings>.Run(
                    args,
                    settings,
                    worker,
                    new LeaderboardsArgsParser(Console.In, Console.Out, Console.Error),
                    Log);
            }
        }
    }
}
