using System;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties
{
    interface ISettings
    {
        /// <summary>
        /// The product's application ID.
        /// </summary>
        uint AppId { get; }
        /// <summary>
        /// The minimum amount of time that should pass between each cycle.
        /// </summary>
        TimeSpan UpdateInterval { get; set; }
        /// <summary>
        /// The amount of time to wait after a cycle to perform garbage collection.
        /// </summary>
        TimeSpan DelayBeforeGC { get; set; }
        /// <summary>
        /// The user name used to log on to Steam.
        /// </summary>
        string SteamUserName { get; set; }
        /// <summary>
        /// The password used to log on to Steam.
        /// </summary>
        EncryptedSecret SteamPassword { get; set; }
        /// <summary>
        /// The connection string used to connect to the leaderboards database.
        /// </summary>
        EncryptedSecret LeaderboardsConnectionString { get; set; }
        /// <summary>
        /// An Application Insights instrumentation key.
        /// </summary>
        string LeaderboardsInstrumentationKey { get; set; }

        /// <summary>
        /// Stores the current values of the application settings properties.
        /// </summary>
        void Save();
    }
}