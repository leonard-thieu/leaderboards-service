using System;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties
{
    internal interface ILeaderboardsSettings : ISettings
    {
        /// <summary>
        /// The product's application ID.
        /// </summary>
        uint AppId { get; }
        /// <summary>
        /// The user name used to log on to Steam.
        /// </summary>
        string SteamUserName { get; set; }
        /// <summary>
        /// The password used to log on to Steam.
        /// </summary>
        EncryptedSecret SteamPassword { get; set; }
        /// <summary>
        /// The maxinum number of daily leaderboards to update per cycle.
        /// </summary>
        int DailyLeaderboardsPerUpdate { get; set; }
        /// <summary>
        /// The amount of time to wait before a request to the Steam Client API times out.
        /// </summary>
        TimeSpan SteamClientTimeout { get; set; }
    }
}