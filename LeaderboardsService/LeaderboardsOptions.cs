using System;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    sealed class LeaderboardsOptions : Options
    {
        /// <summary>
        /// The user name used to log on to Steam.
        /// </summary>
        public string SteamUserName { get; internal set; }
        /// <summary>
        /// The password used to log on to Steam.
        /// </summary>
        public string SteamPassword { get; internal set; } = "";
        /// <summary>
        /// The connection string used to connect to the leaderboards database.
        /// </summary>
        public string LeaderboardsConnectionString { get; internal set; } = "";
        /// <summary>
        /// The maxinum number of daily leaderboards to update per cycle.
        /// </summary>
        public int? DailyLeaderboardsPerUpdate { get; internal set; }
        public TimeSpan? SteamClientTimeout { get; set; }
    }
}
