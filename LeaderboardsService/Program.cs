using System;
using log4net;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

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

            using (var worker = new WorkerRole(settings))
            {
                return Application.Run(
                    args,
                    new EnvironmentAdapter(),
                    settings,
                    worker,
                    new LeaderboardsArgsParser(Console.In, Console.Out, Console.Error),
                    new ServiceBaseAdapter(),
                    Log);
            }
        }
    }
}
